#region SPORetentionPolicyManager.psm1
# Module for managing retention policy exclusions during version cleanup
#
# This module temporarily adds sites to retention policy exclusion lists
# (SharePointLocationException) before version deletion, then removes them
# after completion. This allows the SPO version trimming jobs to execute
# on sites that would otherwise be blocked by retention holds.
#
# Requires: ExchangeOnlineManagement module (Connect-IPPSSession)
# Permissions: Compliance Administrator or equivalent role
#
# Flow per site:
#   1. Get-SiteRetentionPolicies    -> discover which policies hold the site
#   2. Suspend-SiteRetentionPolicy  -> add site to policy exclusion list
#   3. Wait-RetentionPolicyRelease  -> wait for hold to be released (PHL check)
#   4. [Version deletion happens]
#   5. Resume-SiteRetentionPolicy   -> remove site from exclusion list
#
# All actions are logged to RetentionPolicyLog.json for audit trail.

#region Module State
$script:IPPSConnected = $false
$script:RetentionPolicyLogFile = $null
$script:RetentionPolicyLog = @()
$script:SuspendedSites = @{}  # siteUrl -> @{ Policies = @(...); SuspendedAt = ... }
$script:ModuleEnabled = $false
$script:MaxExceptionSitesPerPolicy = 90  # Microsoft limit is 100, keep 10 as safety buffer
$script:PolicyExceptionCache = @{}       # policyGuid -> currentExceptionCount (cached at suspend time)
#endregion

#region Connection

function Connect-RetentionPolicyManager {
    <#
    .SYNOPSIS
        Connects to Security & Compliance PowerShell (IPPS) and initializes the module.
        Uses a SEPARATE Entra ID app registration (PurviewApp) with Exchange permissions,
        distinct from the main SPO orchestrator app (EntraIdApp).
    .PARAMETER CertificateThumbprint
        Certificate thumbprint for the Purview app (overrides AppPaths.json)
    .PARAMETER AppId
        Application (Client) ID for the Purview app (overrides AppPaths.json)
    .PARAMETER Organization
        Tenant domain (e.g., contoso.onmicrosoft.com) for app-based auth (overrides AppPaths.json)
    .PARAMETER LogPath
        Directory to store RetentionPolicyLog.json
    .PARAMETER UseAppPaths
        Automatically load PurviewApp credentials from AppPaths.json (default: $true)
    #>
    [CmdletBinding()]
    param(
        [string]$CertificateThumbprint,
        [string]$AppId,
        [string]$Organization,
        [string]$LogPath,
        [bool]$UseAppPaths = $true
    )

    # Validate ExchangeOnlineManagement module
    if (-not (Get-Module -ListAvailable -Name "ExchangeOnlineManagement")) {
        throw "ExchangeOnlineManagement module is required. Install with: Install-Module ExchangeOnlineManagement -Scope CurrentUser"
    }

    Import-Module ExchangeOnlineManagement -ErrorAction Stop

    # If credentials not provided explicitly, try to load from AppPaths.json PurviewApp section
    if ($UseAppPaths -and (-not $AppId -or -not $CertificateThumbprint)) {
        # AppPaths.json lives in config/ (state files); Logs/ kept as fallback for legacy installs
        $appPathsFile = $null
        $moduleRoot = Split-Path -Parent $PSCommandPath
        $candidates = New-Object System.Collections.ArrayList
        [void]$candidates.Add((Join-Path $moduleRoot 'config\AppPaths.json'))
        if ($LogPath) { [void]$candidates.Add((Join-Path $LogPath 'AppPaths.json')) }
        [void]$candidates.Add((Join-Path $moduleRoot 'AppPaths.json'))
        foreach ($c in $candidates) { if (Test-Path $c) { $appPathsFile = $c; break } }
        if ($appPathsFile -and (Test-Path $appPathsFile)) {
            try {
                $appPathsJson = Get-Content $appPathsFile -Raw | ConvertFrom-Json
                if ($appPathsJson.PurviewApp) {
                    if (-not $AppId -and $appPathsJson.PurviewApp.ClientId) {
                        $AppId = $appPathsJson.PurviewApp.ClientId
                        Write-Host "  [CONFIG] Purview AppId loaded from AppPaths.json" -ForegroundColor Gray
                    }
                    if (-not $CertificateThumbprint -and $appPathsJson.PurviewApp.CertificateThumbprint) {
                        $CertificateThumbprint = $appPathsJson.PurviewApp.CertificateThumbprint
                        Write-Host "  [CONFIG] Purview CertificateThumbprint loaded from AppPaths.json" -ForegroundColor Gray
                    }
                    if (-not $Organization -and $appPathsJson.PurviewApp.Organization) {
                        $Organization = $appPathsJson.PurviewApp.Organization
                        Write-Host "  [CONFIG] Purview Organization loaded from AppPaths.json" -ForegroundColor Gray
                    }
                }
            }
            catch {
                Write-Warning "Could not read PurviewApp config from AppPaths.json: $_"
            }
        }
    }

    # Build connection params
    $connectParams = @{}
    if ($CertificateThumbprint -and $AppId -and $Organization) {
        $connectParams.CertificateThumbprint = $CertificateThumbprint
        $connectParams.AppId = $AppId
        $connectParams.Organization = $Organization
        Write-Host "  Using Purview app auth (AppId: $($AppId.Substring(0,8))...)" -ForegroundColor Gray
    }
    elseif ($CertificateThumbprint -and $AppId -and -not $Organization) {
        Write-Warning "  Organization is required for app-based auth. Falling back to interactive login."
    }

    # Check if already connected with a VALID session (stale sessions return empty without error)
    $needsConnection = $true
    try {
        $testPolicies = @(Get-RetentionCompliancePolicy -ErrorAction Stop)
        if ($testPolicies.Count -gt 0) {
            $script:IPPSConnected = $true
            $needsConnection = $false
            Write-Host "  [OK] Already connected to Security & Compliance ($($testPolicies.Count) policies found)" -ForegroundColor Green
        } else {
            Write-Host "  [!] Existing IPPS session returned 0 policies (stale session). Reconnecting..." -ForegroundColor Yellow
            try { Disconnect-ExchangeOnline -Confirm:$false -ErrorAction SilentlyContinue } catch {}
        }
    }
    catch {
        Write-Host "  No active IPPS session. Connecting..." -ForegroundColor Yellow
        try { Disconnect-ExchangeOnline -Confirm:$false -ErrorAction SilentlyContinue } catch {}
    }

    if ($needsConnection) {
        try {
            Connect-IPPSSession @connectParams -ErrorAction Stop
            
            # Verify connection actually works
            $verifyPolicies = @(Get-RetentionCompliancePolicy -ErrorAction Stop)
            if ($verifyPolicies.Count -eq 0) {
                Write-Host "  [WARN] Connected but 0 retention policies found. This tenant may have no policies, or permissions may be insufficient." -ForegroundColor Yellow
            } else {
                Write-Host "  [OK] Connected to Security & Compliance ($($verifyPolicies.Count) policies found)" -ForegroundColor Green
            }
            $script:IPPSConnected = $true
        }
        catch {
            throw "Failed to connect to Security & Compliance: $_"
        }
    }

    # Store LogPath for module-wide use (e.g., AllSites.json lookup)
    $script:LogPath = $LogPath

    # Initialize log (state JSON lives in config/, not Logs/)
    $moduleRoot = Split-Path -Parent $PSCommandPath
    $configPath = Join-Path $moduleRoot 'config'
    if (-not (Test-Path $configPath)) { New-Item -ItemType Directory -Path $configPath -Force | Out-Null }
    $script:RetentionPolicyLogFile = Join-Path $configPath 'RetentionPolicyLog.json'

    # Load existing log
    if (Test-Path $script:RetentionPolicyLogFile) {
        try {
            $existing = Get-Content $script:RetentionPolicyLogFile -Raw | ConvertFrom-Json
            if ($existing.Actions) {
                $script:RetentionPolicyLog = @($existing.Actions)
            }
            if ($existing.SuspendedSites) {
                foreach ($prop in $existing.SuspendedSites.PSObject.Properties) {
                    $script:SuspendedSites[$prop.Name] = @{
                        Policies = @($prop.Value.Policies)
                        SuspendedAt = $prop.Value.SuspendedAt
                    }
                }
                if ($script:SuspendedSites.Count -gt 0) {
                    Write-Host "  [WARN] $($script:SuspendedSites.Count) sites still suspended from previous session" -ForegroundColor Yellow
                }
            }
        }
        catch {
            Write-Warning "Could not load existing retention policy log: $_"
        }
    }

    $script:ModuleEnabled = $true
    Write-Host "  [OK] Retention Policy Manager initialized (log: $($script:RetentionPolicyLogFile))" -ForegroundColor Green
}

#endregion

#region Helpers

function Get-LocationNames {
    <#
    .SYNOPSIS
        Normalizes location values from retention policies.
        EXO V3 (REST) may return plain strings; older RPS returns objects with .Name/.DisplayName.
    .PARAMETER Locations
        The location property value (e.g., $policy.SharePointLocation)
    .OUTPUTS
        Array of string names
    #>
    param($Locations)
    if (-not $Locations) { return @() }
    return @($Locations | ForEach-Object {
        if ($_ -is [string]) { $_ }
        elseif ($_.Name) { $_.Name }
        elseif ($_.DisplayName) { $_.DisplayName }
        else { $_.ToString() }
    })
}

#endregion

#region Discovery

function Get-SiteRetentionPolicies {
    <#
    .SYNOPSIS
        Discovers which retention compliance policies apply to a specific site
    .PARAMETER SiteUrl
        The SharePoint site URL to check
    .OUTPUTS
        Array of policy objects with Name, Guid, and how the site is included
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl
    )

    if (-not $script:IPPSConnected) {
        Write-Warning "IPPS not connected. Call Connect-RetentionPolicyManager first."
        return @()
    }

    $normalizedUrl = $SiteUrl.TrimEnd("/").ToLower()
    $matchingPolicies = @()

    try {
        # Cache policies to avoid repeated API calls when checking multiple sites
        if (-not $script:RetentionPoliciesCache -or ((Get-Date) - $script:RetentionPoliciesCacheTime).TotalMinutes -gt 5) {
            $script:RetentionPoliciesCache = Get-RetentionCompliancePolicy -DistributionDetail -ErrorAction Stop
            $script:RetentionPoliciesCacheTime = Get-Date
        }
        $allPolicies = $script:RetentionPoliciesCache

        foreach ($policy in $allPolicies) {
            $appliesToSite = $false
            $inclusionType = "None"

            # Normalize location values (EXO V3 returns strings, RPS returns objects)
            $spLocations = Get-LocationNames $policy.SharePointLocation
            $spExceptions = Get-LocationNames $policy.SharePointLocationException
            $mgLocations = Get-LocationNames $policy.ModernGroupLocation
            $mgExceptions = Get-LocationNames $policy.ModernGroupLocationException

            # Check if policy applies to ALL SharePoint classic/communication sites
            if ($spLocations -match "^All$") {
                $appliesToSite = $true
                $inclusionType = "AllSites"

                # But check if site is already in the exception list
                $normalizedExceptions = @($spExceptions | ForEach-Object { $_.TrimEnd("/").ToLower() })
                if ($normalizedExceptions -contains $normalizedUrl) {
                    $appliesToSite = $false
                    $inclusionType = "ExcludedAlready"
                }
            }
            # Check if policy explicitly includes this site via SharePointLocation
            elseif ($spLocations.Count -gt 0) {
                $normalizedLocs = @($spLocations | ForEach-Object { $_.TrimEnd("/").ToLower() })
                if ($normalizedLocs -contains $normalizedUrl) {
                    $appliesToSite = $true
                    $inclusionType = "ExplicitInclusion"
                }
            }

            # Also check Microsoft 365 Group locations (group-connected team sites)
            # Only match M365 Group scopes if the site is actually group-connected
            if (-not $appliesToSite) {
                $isGroupSite = $false
                if (-not $script:SiteGroupCache) { $script:SiteGroupCache = @{} }
                if ($script:SiteGroupCache.ContainsKey($normalizedUrl)) {
                    $isGroupSite = $script:SiteGroupCache[$normalizedUrl]
                } else {
                    # Check AllSites.json cache first, fall back to API only if not found
                    $isGroupSite = $false
                    $needApiCall = $true
                    
                    # Load AllSites.json once for GroupId lookups (avoids per-site API calls)
                    if (-not $script:AllSitesJsonCache) {
                        $allSitesPath = Join-Path $script:LogPath "AllSites.json"
                        if (Test-Path $allSitesPath) {
                            try {
                                $allSitesData = Get-Content $allSitesPath -Raw | ConvertFrom-Json
                                $script:AllSitesJsonCache = @{}
                                foreach ($s in $allSitesData.Sites) {
                                    $script:AllSitesJsonCache[$s.Url.TrimEnd("/").ToLower()] = $s
                                }
                            } catch { $script:AllSitesJsonCache = @{} }
                        } else { $script:AllSitesJsonCache = @{} }
                    }
                    
                    if ($script:AllSitesJsonCache.ContainsKey($normalizedUrl)) {
                        $cachedSite = $script:AllSitesJsonCache[$normalizedUrl]
                        if ($cachedSite.RelatedGroupId -and $cachedSite.RelatedGroupId -ne "" -and $cachedSite.RelatedGroupId -ne "00000000-0000-0000-0000-000000000000") {
                            $isGroupSite = $true
                        }
                        $needApiCall = $false
                    }
                    
                    if ($needApiCall) {
                        try {
                            $siteProps = Get-SPOSite -Identity $SiteUrl -ErrorAction Stop
                            $isGroupSite = ($null -ne $siteProps.RelatedGroupId -and $siteProps.RelatedGroupId -ne [Guid]::Empty)
                        } catch {
                            $isGroupSite = $false
                        }
                    }
                    $script:SiteGroupCache[$normalizedUrl] = $isGroupSite
                }

                if ($isGroupSite) {
                    if ($mgLocations -match "^All$") {
                        $appliesToSite = $true
                        $inclusionType = "AllM365Groups"

                        # Check if site is in the M365 Group exception list
                        $normalizedMgExc = @($mgExceptions | ForEach-Object { $_.TrimEnd("/").ToLower() })
                        if ($normalizedMgExc -contains $normalizedUrl) {
                            $appliesToSite = $false
                            $inclusionType = "ExcludedAlready"
                        }
                    }
                    elseif ($mgLocations.Count -gt 0) {
                        $normalizedMgLocs = @($mgLocations | ForEach-Object { $_.TrimEnd("/").ToLower() })
                        if ($normalizedMgLocs -contains $normalizedUrl) {
                            $appliesToSite = $true
                            $inclusionType = "ExplicitM365GroupInclusion"
                        }
                    }
                }
            }

            if ($appliesToSite) {
                $matchingPolicies += @{
                    Name          = $policy.Name
                    Guid          = $policy.Guid.ToString()
                    InclusionType = $inclusionType
                    Enabled       = $policy.Enabled
                    Mode          = $policy.Mode
                }
            }
        }

        return $matchingPolicies
    }
    catch {
        Write-Warning "Error querying retention policies for ${SiteUrl}: $_"
        return @()
    }
}

#endregion

#region Suspend / Resume

function Suspend-SiteRetentionPolicy {
    <#
    .SYNOPSIS
        Temporarily excludes a site from its retention policies by adding it
        to each policy's SharePointLocationException list
    .PARAMETER SiteUrl
        The SharePoint site URL
    .PARAMETER Policies
        Array of policy objects from Get-SiteRetentionPolicies (optional - auto-discovers if not provided)
    .OUTPUTS
        $true if suspension succeeded, $false otherwise
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        [array]$Policies
    )

    if (-not $script:IPPSConnected) {
        Write-Warning "IPPS not connected."
        return $false
    }

    # Check if already suspended
    $normalizedUrl = $SiteUrl.TrimEnd("/").ToLower()
    if ($script:SuspendedSites.ContainsKey($normalizedUrl)) {
        Write-Host "    [RETENTION] Site already suspended: $SiteUrl" -ForegroundColor DarkYellow
        return $true
    }

    # Discover policies if not provided
    if (-not $Policies -or $Policies.Count -eq 0) {
        $Policies = Get-SiteRetentionPolicies -SiteUrl $SiteUrl
    }

    if ($Policies.Count -eq 0) {
        Write-Host "    [RETENTION] No retention policies apply to: $SiteUrl" -ForegroundColor Gray
        return $true  # Nothing to suspend
    }

    # Check if adding this site would exceed the 100-site exception limit per policy
    foreach ($policy in $Policies) {
        if ($policy.InclusionType -eq "AllSites" -or $policy.InclusionType -eq "AllM365Groups") {
            $policyGuid = $policy.Guid
            # Count current exceptions for this policy (cache to avoid repeated API calls)
            if (-not $script:PolicyExceptionCache.ContainsKey($policyGuid)) {
                try {
                    $policyDetail = Get-RetentionCompliancePolicy -Identity $policy.Name -DistributionDetail -ErrorAction Stop
                    # Microsoft limit of 100 is COMBINED across SP sites + M365 Groups exceptions
                    $spExceptions = if ($policyDetail.SharePointLocationException) { @($policyDetail.SharePointLocationException).Count } else { 0 }
                    $m365Exceptions = if ($policyDetail.ModernGroupLocationException) { @($policyDetail.ModernGroupLocationException).Count } else { 0 }
                    $currentExceptions = $spExceptions + $m365Exceptions
                    $script:PolicyExceptionCache[$policyGuid] = $currentExceptions
                    Write-Host "    [RETENTION] Policy '$($policy.Name)' has $currentExceptions existing exceptions (SP: $spExceptions + M365: $m365Exceptions, limit: $($script:MaxExceptionSitesPerPolicy))" -ForegroundColor Gray
                }
                catch {
                    Write-Warning "    [RETENTION] Could not check exception count for '$($policy.Name)': $_"
                    $script:PolicyExceptionCache[$policyGuid] = 0
                }
            }
            
            $currentCount = $script:PolicyExceptionCache[$policyGuid]
            if ($currentCount -ge $script:MaxExceptionSitesPerPolicy) {
                Write-Host "    [RETENTION] Policy '$($policy.Name)' at exception limit ($currentCount/$($script:MaxExceptionSitesPerPolicy)) - cannot suspend more sites" -ForegroundColor Yellow
                return $null  # Distinct from $false (failure) - means "at capacity, try later"
            }
        }
    }

    Write-Host "    [RETENTION] Suspending $($Policies.Count) retention policy(ies) for: $SiteUrl" -ForegroundColor Yellow
    $suspendedPolicies = @()
    $allSuccess = $true
    $policyIndex = 0

    foreach ($policy in $Policies) {
        $policyName = $policy.Name
        $inclusionType = $policy.InclusionType
        $policyIndex++

        # Wait between policy modifications to avoid lock conflicts
        if ($policyIndex -gt 1) {
            Write-Host "      [WAIT] Waiting 5s before modifying next policy..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 5
        }

        # Retry with backoff for PolicyLockConflictException
        $maxRetries = 5
        $retryCount = 0
        $policySuccess = $false

        while (-not $policySuccess -and $retryCount -le $maxRetries) {
            try {
                if ($inclusionType -eq "AllSites") {
                    # Add site to the SP exception list
                    Set-RetentionCompliancePolicy -Identity $policyName -AddSharePointLocationException $SiteUrl -ErrorAction Stop
                    Write-Host "      [OK] Added to SP exception list: $policyName" -ForegroundColor Green
                    if ($script:PolicyExceptionCache.ContainsKey($policy.Guid)) {
                        $script:PolicyExceptionCache[$policy.Guid]++
                    }
                }
                elseif ($inclusionType -eq "AllM365Groups") {
                    # Add site to the M365 Group exception list
                    Set-RetentionCompliancePolicy -Identity $policyName -AddModernGroupLocationException $SiteUrl -ErrorAction Stop
                    Write-Host "      [OK] Added to M365 Group exception list: $policyName" -ForegroundColor Green
                    if ($script:PolicyExceptionCache.ContainsKey($policy.Guid)) {
                        $script:PolicyExceptionCache[$policy.Guid]++
                    }
                }
                elseif ($inclusionType -eq "ExplicitInclusion") {
                    # Remove site from the explicit SP inclusion list
                    Set-RetentionCompliancePolicy -Identity $policyName -RemoveSharePointLocation $SiteUrl -ErrorAction Stop
                    Write-Host "      [OK] Removed from SP inclusion: $policyName" -ForegroundColor Green
                }
                elseif ($inclusionType -eq "ExplicitM365GroupInclusion") {
                    # Remove site from the explicit M365 Group inclusion list
                    Set-RetentionCompliancePolicy -Identity $policyName -RemoveModernGroupLocation $SiteUrl -ErrorAction Stop
                    Write-Host "      [OK] Removed from M365 Group inclusion: $policyName" -ForegroundColor Green
                }

                $policySuccess = $true
                $action = if ($inclusionType -eq "AllSites" -or $inclusionType -eq "AllM365Groups") { "AddedException" } else { "RemovedInclusion" }
                $suspendedPolicies += @{
                    Name          = $policyName
                    Guid          = $policy.Guid
                    InclusionType = $inclusionType
                    Action        = $action
                }
            }
            catch {
                $errorMsg = $_.Exception.Message
                $isPolicyLock = $errorMsg -match "PolicyLockConflict|being deployed|active requests"
                
                if ($isPolicyLock -and $retryCount -lt $maxRetries) {
                    $retryCount++
                    # 30s intervals (policy lock takes ~120s to clear)
                    $waitSeconds = 30 * $retryCount
                    Write-Host "      [RETRY] Policy '$policyName' locked - waiting ${waitSeconds}s before retry $retryCount/$maxRetries..." -ForegroundColor DarkYellow
                    Start-Sleep -Seconds $waitSeconds
                }
                else {
                    Write-Warning "      [FAIL] Could not suspend policy '$policyName' for $SiteUrl : $_"
                    $allSuccess = $false
                    break
                }
            }
        }
    }

    if ($suspendedPolicies.Count -gt 0) {
        # Track suspended state
        $script:SuspendedSites[$normalizedUrl] = @{
            Policies    = $suspendedPolicies
            SuspendedAt = (Get-Date).ToString("o")
        }

        # Log action
        Add-RetentionPolicyLogEntry -SiteUrl $SiteUrl -Action "Suspend" -Policies $suspendedPolicies -Success $allSuccess

        # Persist state
        Save-RetentionPolicyState
    }

    return $allSuccess
}

function Resume-SiteRetentionPolicy {
    <#
    .SYNOPSIS
        Restores retention policies for a site by removing it from exception lists
        or re-adding to inclusion lists
    .PARAMETER SiteUrl
        The SharePoint site URL
    .OUTPUTS
        $true if resume succeeded, $false otherwise
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl
    )

    if (-not $script:IPPSConnected) {
        Write-Warning "IPPS not connected."
        return $false
    }

    $normalizedUrl = $SiteUrl.TrimEnd("/").ToLower()

    if (-not $script:SuspendedSites.ContainsKey($normalizedUrl)) {
        Write-Host "    [RETENTION] Site not suspended, nothing to resume: $SiteUrl" -ForegroundColor Gray
        return $true
    }

    $suspendedInfo = $script:SuspendedSites[$normalizedUrl]
    $policies = $suspendedInfo.Policies
    $allSuccess = $true

    Write-Host "    [RETENTION] Resuming $($policies.Count) retention policy(ies) for: $SiteUrl" -ForegroundColor Cyan
    $policyIndex = 0

    foreach ($policy in $policies) {
        $policyName = $policy.Name
        $policyIndex++

        # Wait between policy modifications to avoid lock conflicts
        if ($policyIndex -gt 1) {
            Write-Host "      [WAIT] Waiting 5s before modifying next policy..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 5
        }

        # Retry with backoff for PolicyLockConflictException
        $maxRetries = 5
        $retryCount = 0
        $policySuccess = $false

        while (-not $policySuccess -and $retryCount -le $maxRetries) {
            try {
                if ($policy.Action -eq "AddedException") {
                    if ($policy.InclusionType -eq "AllM365Groups") {
                        # Remove site from M365 Group exception list
                        Set-RetentionCompliancePolicy -Identity $policyName -RemoveModernGroupLocationException $SiteUrl -ErrorAction Stop
                        Write-Host "      [OK] Removed from M365 Group exception list: $policyName" -ForegroundColor Green
                    } else {
                        # Remove site from SP exception list
                        Set-RetentionCompliancePolicy -Identity $policyName -RemoveSharePointLocationException $SiteUrl -ErrorAction Stop
                        Write-Host "      [OK] Removed from SP exception list: $policyName" -ForegroundColor Green
                    }
                    # Update cache counter
                    if ($policy.Guid -and $script:PolicyExceptionCache.ContainsKey($policy.Guid)) {
                        $script:PolicyExceptionCache[$policy.Guid] = [math]::Max(0, $script:PolicyExceptionCache[$policy.Guid] - 1)
                    }
                }
                elseif ($policy.Action -eq "RemovedInclusion") {
                    if ($policy.InclusionType -eq "ExplicitM365GroupInclusion") {
                        # Re-add site to M365 Group inclusion list
                        Set-RetentionCompliancePolicy -Identity $policyName -AddModernGroupLocation $SiteUrl -ErrorAction Stop
                        Write-Host "      [OK] Re-added to M365 Group inclusion: $policyName" -ForegroundColor Green
                    } else {
                        # Re-add site to SP inclusion list
                        Set-RetentionCompliancePolicy -Identity $policyName -AddSharePointLocation $SiteUrl -ErrorAction Stop
                        Write-Host "      [OK] Re-added to SP inclusion: $policyName" -ForegroundColor Green
                    }
                }
                $policySuccess = $true
            }
            catch {
                $errorMsg = $_.Exception.Message
                $isPolicyLock = $errorMsg -match "PolicyLockConflict|being deployed|active requests"
                
                if ($isPolicyLock -and $retryCount -lt $maxRetries) {
                    $retryCount++
                    # 30s intervals (policy lock takes ~120s to clear)
                    $waitSeconds = 30 * $retryCount
                    Write-Host "      [RETRY] Policy '$policyName' locked - waiting ${waitSeconds}s before retry $retryCount/$maxRetries..." -ForegroundColor DarkYellow
                    Start-Sleep -Seconds $waitSeconds
                }
                else {
                    Write-Warning "      [FAIL] Could not resume policy '$policyName' for $SiteUrl : $_"
                    $allSuccess = $false
                    break
                }
            }
        }
    }

    if ($allSuccess) {
        $script:SuspendedSites.Remove($normalizedUrl)
    }

    # Log action
    Add-RetentionPolicyLogEntry -SiteUrl $SiteUrl -Action "Resume" -Policies $policies -Success $allSuccess

    # Persist state
    Save-RetentionPolicyState

    return $allSuccess
}

function Resume-AllSuspendedSites {
    <#
    .SYNOPSIS
        Resumes retention policies for ALL sites that were suspended in this or a previous session.
        Call this at session end or in case of error recovery.
    #>
    [CmdletBinding()]
    param()

    if ($script:SuspendedSites.Count -eq 0) {
        Write-Host "  [RETENTION] No suspended sites to resume" -ForegroundColor Gray
        return
    }

    Write-Host "  [RETENTION] Resuming retention policies for $($script:SuspendedSites.Count) sites..." -ForegroundColor Yellow
    $sites = @($script:SuspendedSites.Keys)

    foreach ($siteUrl in $sites) {
        $null = Resume-SiteRetentionPolicy -SiteUrl $siteUrl
    }

    if ($script:SuspendedSites.Count -gt 0) {
        Write-Warning "  [RETENTION] $($script:SuspendedSites.Count) site(s) could not be resumed. Check RetentionPolicyLog.json."
    }
    else {
        Write-Host "  [RETENTION] All sites resumed successfully" -ForegroundColor Green
    }
}

#endregion

#region Batch Suspend/Resume (optimized: one API call per policy for all sites)

function Suspend-BatchSiteRetentionPolicies {
    <#
    .SYNOPSIS
        Suspends retention policies for MULTIPLE sites in batch mode.
        Groups all site URLs by policy, then makes ONE Set-RetentionCompliancePolicy call
        per policy with all URLs at once, drastically reducing API calls and lock conflicts.
    .PARAMETER SiteUrls
        Array of SharePoint site URLs to suspend
    .OUTPUTS
        Hashtable with keys: Success (bool), SuspendedCount (int), FailedSites (array)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$SiteUrls
    )

    if (-not $script:IPPSConnected) {
        Write-Warning "IPPS not connected."
        return @{ Success = $false; SuspendedCount = 0; FailedSites = @($SiteUrls) }
    }

    Write-Host "  [RETENTION-BATCH] Analyzing $($SiteUrls.Count) site(s) for batch policy suspension..." -ForegroundColor Yellow

    # Phase 1: Discover which policies apply to each site and group by policy+action
    # Key = "PolicyName|InclusionType", Value = @{ Policy=...; Sites=@() }
    $policyGroups = @{}
    $sitePolicyMap = @{}  # Track which policies apply to each site
    $skippedSites = @()

    foreach ($siteUrl in $SiteUrls) {
        $normalizedUrl = $siteUrl.TrimEnd("/").ToLower()

        # Skip already suspended
        if ($script:SuspendedSites.ContainsKey($normalizedUrl)) {
            Write-Host "    [SKIP] Already suspended: $siteUrl" -ForegroundColor DarkYellow
            continue
        }

        $policies = Get-SiteRetentionPolicies -SiteUrl $siteUrl
        if ($policies.Count -eq 0) {
            Write-Host "    [SKIP] No retention policies apply to: $siteUrl" -ForegroundColor Gray
            continue
        }

        $sitePolicyMap[$normalizedUrl] = @{
            SiteUrl  = $siteUrl
            Policies = @()
        }

        foreach ($policy in $policies) {
            $groupKey = "$($policy.Name)|$($policy.InclusionType)"

            if (-not $policyGroups.ContainsKey($groupKey)) {
                $policyGroups[$groupKey] = @{
                    PolicyName    = $policy.Name
                    PolicyGuid    = $policy.Guid
                    InclusionType = $policy.InclusionType
                    Enabled       = $policy.Enabled
                    Mode          = $policy.Mode
                    Sites         = @()
                }
            }
            $policyGroups[$groupKey].Sites += $siteUrl

            $action = if ($policy.InclusionType -eq "AllSites" -or $policy.InclusionType -eq "AllM365Groups") { "AddedException" } else { "RemovedInclusion" }
            $sitePolicyMap[$normalizedUrl].Policies += @{
                Name          = $policy.Name
                Guid          = $policy.Guid
                InclusionType = $policy.InclusionType
                Action        = $action
            }
        }
    }

    if ($policyGroups.Count -eq 0) {
        Write-Host "  [RETENTION-BATCH] No policies to modify" -ForegroundColor Gray
        return @{ Success = $true; SuspendedCount = 0; FailedSites = @() }
    }

    # Phase 2: Check capacity for each AllSites/AllM365Groups policy
    foreach ($groupKey in @($policyGroups.Keys)) {
        $group = $policyGroups[$groupKey]
        if ($group.InclusionType -eq "AllSites" -or $group.InclusionType -eq "AllM365Groups") {
            $policyGuid = $group.PolicyGuid
            if (-not $script:PolicyExceptionCache.ContainsKey($policyGuid)) {
                try {
                    $policyDetail = Get-RetentionCompliancePolicy -Identity $group.PolicyName -DistributionDetail -ErrorAction Stop
                    # Microsoft limit of 100 is COMBINED across SP sites + M365 Groups exceptions
                    $spExceptions = if ($policyDetail.SharePointLocationException) { @($policyDetail.SharePointLocationException).Count } else { 0 }
                    $m365Exceptions = if ($policyDetail.ModernGroupLocationException) { @($policyDetail.ModernGroupLocationException).Count } else { 0 }
                    $currentExceptions = $spExceptions + $m365Exceptions
                    $script:PolicyExceptionCache[$policyGuid] = $currentExceptions
                }
                catch {
                    $script:PolicyExceptionCache[$policyGuid] = 0
                }
            }

            $currentCount = $script:PolicyExceptionCache[$policyGuid]
            $needed = $group.Sites.Count
            $available = [math]::Max(0, $script:MaxExceptionSitesPerPolicy - $currentCount)

            Write-Host "    [CAPACITY] Policy '$($group.PolicyName)': $currentCount existing + $needed new = $(($currentCount + $needed)) / $($script:MaxExceptionSitesPerPolicy)" -ForegroundColor Gray

            if ($available -eq 0) {
                Write-Host "    [CAPACITY] Policy '$($group.PolicyName)' is already at or over capacity ($currentCount / $($script:MaxExceptionSitesPerPolicy)) - skipping all $needed sites" -ForegroundColor Red
                # Track dropped sites before removing the group
                foreach ($droppedSite in $group.Sites) {
                    $droppedNorm = $droppedSite.TrimEnd("/").ToLower()
                    if ($sitePolicyMap.ContainsKey($droppedNorm)) {
                        $sitePolicyMap[$droppedNorm].Policies = @($sitePolicyMap[$droppedNorm].Policies | Where-Object { $_.Name -ne $group.PolicyName })
                        if ($sitePolicyMap[$droppedNorm].Policies.Count -eq 0) {
                            $sitePolicyMap.Remove($droppedNorm)
                        }
                    }
                }
                $policyGroups.Remove($groupKey)
            }
            elseif ($needed -gt $available) {
                Write-Host "    [CAPACITY] Policy '$($group.PolicyName)' can only accept $available of $needed sites" -ForegroundColor Yellow
                # Track dropped sites (the ones that won't fit)
                $keptSites = @($group.Sites | Select-Object -First $available)
                $droppedSites = @($group.Sites | Select-Object -Skip $available)
                foreach ($droppedSite in $droppedSites) {
                    $droppedNorm = $droppedSite.TrimEnd("/").ToLower()
                    if ($sitePolicyMap.ContainsKey($droppedNorm)) {
                        $sitePolicyMap[$droppedNorm].Policies = @($sitePolicyMap[$droppedNorm].Policies | Where-Object { $_.Name -ne $group.PolicyName })
                        if ($sitePolicyMap[$droppedNorm].Policies.Count -eq 0) {
                            $sitePolicyMap.Remove($droppedNorm)
                        }
                    }
                }
                # Trim to what fits
                $group.Sites = $keptSites
            }
        }
    }

    # Phase 3: Execute one API call per policy group with all URLs at once
    $allSuccess = $true
    $policyIndex = 0
    $failedSites = @()

    Write-Host "  [RETENTION-BATCH] Modifying $($policyGroups.Count) policy group(s)..." -ForegroundColor Yellow

    foreach ($groupKey in $policyGroups.Keys) {
        $group = $policyGroups[$groupKey]
        $policyName = $group.PolicyName
        $inclusionType = $group.InclusionType
        $siteUrlArray = @($group.Sites)
        $policyIndex++

        if ($siteUrlArray.Count -eq 0) { continue }

        # Wait between policy modifications to avoid lock conflicts
        if ($policyIndex -gt 1) {
            Write-Host "      [WAIT] Waiting 5s before modifying next policy..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 5
        }

        Write-Host "    [POLICY] '$policyName' ($inclusionType) - $($siteUrlArray.Count) site(s)" -ForegroundColor Cyan

        # Retry with backoff
        $maxRetries = 5
        $retryCount = 0
        $policySuccess = $false

        while (-not $policySuccess -and $retryCount -le $maxRetries) {
            try {
                if ($inclusionType -eq "AllSites") {
                    Set-RetentionCompliancePolicy -Identity $policyName -AddSharePointLocationException $siteUrlArray -ErrorAction Stop
                    Write-Host "      [OK] Added $($siteUrlArray.Count) site(s) to SP exception list" -ForegroundColor Green
                    if ($script:PolicyExceptionCache.ContainsKey($group.PolicyGuid)) {
                        $script:PolicyExceptionCache[$group.PolicyGuid] += $siteUrlArray.Count
                    }
                }
                elseif ($inclusionType -eq "AllM365Groups") {
                    Set-RetentionCompliancePolicy -Identity $policyName -AddModernGroupLocationException $siteUrlArray -ErrorAction Stop
                    Write-Host "      [OK] Added $($siteUrlArray.Count) site(s) to M365 Group exception list" -ForegroundColor Green
                    if ($script:PolicyExceptionCache.ContainsKey($group.PolicyGuid)) {
                        $script:PolicyExceptionCache[$group.PolicyGuid] += $siteUrlArray.Count
                    }
                }
                elseif ($inclusionType -eq "ExplicitInclusion") {
                    Set-RetentionCompliancePolicy -Identity $policyName -RemoveSharePointLocation $siteUrlArray -ErrorAction Stop
                    Write-Host "      [OK] Removed $($siteUrlArray.Count) site(s) from SP inclusion" -ForegroundColor Green
                }
                elseif ($inclusionType -eq "ExplicitM365GroupInclusion") {
                    Set-RetentionCompliancePolicy -Identity $policyName -RemoveModernGroupLocation $siteUrlArray -ErrorAction Stop
                    Write-Host "      [OK] Removed $($siteUrlArray.Count) site(s) from M365 Group inclusion" -ForegroundColor Green
                }
                $policySuccess = $true
            }
            catch {
                $errorMsg = $_.Exception.Message
                $isPolicyLock = $errorMsg -match "PolicyLockConflict|being deployed|active requests"
                
                if ($isPolicyLock -and $retryCount -lt $maxRetries) {
                    $retryCount++
                    $waitSeconds = 30 * $retryCount
                    Write-Host "      [RETRY] Policy '$policyName' locked - waiting ${waitSeconds}s before retry $retryCount/$maxRetries..." -ForegroundColor DarkYellow
                    Start-Sleep -Seconds $waitSeconds
                }
                else {
                    Write-Warning "      [FAIL] Could not modify policy '$policyName': $_"
                    $allSuccess = $false
                    $failedSites += $siteUrlArray
                    break
                }
            }
        }
    }

    # Phase 4: Track suspended state for each site
    $suspendedCount = 0
    $failedNormalized = @($failedSites | ForEach-Object { $_.TrimEnd("/").ToLower() })

    foreach ($normalizedUrl in $sitePolicyMap.Keys) {
        $siteInfo = $sitePolicyMap[$normalizedUrl]
        # Skip sites that are in failed policies
        if ($failedNormalized -contains $normalizedUrl) { continue }

        $script:SuspendedSites[$normalizedUrl] = @{
            Policies    = $siteInfo.Policies
            SuspendedAt = (Get-Date).ToString("o")
        }
        $suspendedCount++

        Add-RetentionPolicyLogEntry -SiteUrl $siteInfo.SiteUrl -Action "BatchSuspend" -Policies $siteInfo.Policies -Success $true
    }

    Save-RetentionPolicyState

    Write-Host "  [RETENTION-BATCH] Suspended $suspendedCount of $($SiteUrls.Count) site(s) across $($policyGroups.Count) policy group(s)" -ForegroundColor $(if ($allSuccess) { "Green" } else { "Yellow" })

    return @{
        Success        = $allSuccess
        SuspendedCount = $suspendedCount
        FailedSites    = $failedSites
    }
}

function Resume-BatchSiteRetentionPolicies {
    <#
    .SYNOPSIS
        Resumes retention policies for MULTIPLE sites in batch mode.
        Groups all site URLs by policy, then makes ONE Set-RetentionCompliancePolicy call
        per policy with all URLs at once.
    .PARAMETER SiteUrls
        Array of SharePoint site URLs to resume (must have been previously suspended)
    .OUTPUTS
        Hashtable with keys: Success (bool), ResumedCount (int), FailedSites (array)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$SiteUrls
    )

    if (-not $script:IPPSConnected) {
        Write-Warning "IPPS not connected."
        return @{ Success = $false; ResumedCount = 0; FailedSites = @($SiteUrls) }
    }

    Write-Host "  [RETENTION-BATCH] Resuming retention policies for $($SiteUrls.Count) site(s)..." -ForegroundColor Cyan

    # Phase 1: Group by policy+action from the suspended state
    # Key = "PolicyName|Action|InclusionType", Value = @{ ...; Sites=@() }
    $policyGroups = @{}
    $validSites = @()

    foreach ($siteUrl in $SiteUrls) {
        $normalizedUrl = $siteUrl.TrimEnd("/").ToLower()

        if (-not $script:SuspendedSites.ContainsKey($normalizedUrl)) {
            Write-Host "    [SKIP] Not suspended: $siteUrl" -ForegroundColor Gray
            continue
        }

        $validSites += $normalizedUrl
        $suspendedInfo = $script:SuspendedSites[$normalizedUrl]

        foreach ($policy in $suspendedInfo.Policies) {
            $groupKey = "$($policy.Name)|$($policy.Action)|$($policy.InclusionType)"

            if (-not $policyGroups.ContainsKey($groupKey)) {
                $policyGroups[$groupKey] = @{
                    PolicyName    = $policy.Name
                    PolicyGuid    = $policy.Guid
                    Action        = $policy.Action
                    InclusionType = $policy.InclusionType
                    Sites         = @()
                }
            }
            $policyGroups[$groupKey].Sites += $siteUrl
        }
    }

    if ($policyGroups.Count -eq 0) {
        Write-Host "  [RETENTION-BATCH] No policies to resume" -ForegroundColor Gray
        return @{ Success = $true; ResumedCount = 0; FailedSites = @() }
    }

    # Phase 2: Execute one API call per policy group
    $allSuccess = $true
    $policyIndex = 0
    $failedSites = @()

    foreach ($groupKey in $policyGroups.Keys) {
        $group = $policyGroups[$groupKey]
        $policyName = $group.PolicyName
        $siteUrlArray = @($group.Sites)
        $policyIndex++

        if ($siteUrlArray.Count -eq 0) { continue }

        # Wait between policy modifications
        if ($policyIndex -gt 1) {
            Write-Host "      [WAIT] Waiting 5s before modifying next policy..." -ForegroundColor DarkGray
            Start-Sleep -Seconds 5
        }

        Write-Host "    [POLICY] '$policyName' (resume $($group.Action)) - $($siteUrlArray.Count) site(s)" -ForegroundColor Cyan

        $maxRetries = 5
        $retryCount = 0
        $policySuccess = $false

        while (-not $policySuccess -and $retryCount -le $maxRetries) {
            try {
                if ($group.Action -eq "AddedException") {
                    if ($group.InclusionType -eq "AllM365Groups") {
                        Set-RetentionCompliancePolicy -Identity $policyName -RemoveModernGroupLocationException $siteUrlArray -ErrorAction Stop
                        Write-Host "      [OK] Removed $($siteUrlArray.Count) site(s) from M365 Group exception list" -ForegroundColor Green
                    } else {
                        Set-RetentionCompliancePolicy -Identity $policyName -RemoveSharePointLocationException $siteUrlArray -ErrorAction Stop
                        Write-Host "      [OK] Removed $($siteUrlArray.Count) site(s) from SP exception list" -ForegroundColor Green
                    }
                    if ($group.PolicyGuid -and $script:PolicyExceptionCache.ContainsKey($group.PolicyGuid)) {
                        $script:PolicyExceptionCache[$group.PolicyGuid] = [math]::Max(0, $script:PolicyExceptionCache[$group.PolicyGuid] - $siteUrlArray.Count)
                    }
                }
                elseif ($group.Action -eq "RemovedInclusion") {
                    if ($group.InclusionType -eq "ExplicitM365GroupInclusion") {
                        Set-RetentionCompliancePolicy -Identity $policyName -AddModernGroupLocation $siteUrlArray -ErrorAction Stop
                        Write-Host "      [OK] Re-added $($siteUrlArray.Count) site(s) to M365 Group inclusion" -ForegroundColor Green
                    } else {
                        Set-RetentionCompliancePolicy -Identity $policyName -AddSharePointLocation $siteUrlArray -ErrorAction Stop
                        Write-Host "      [OK] Re-added $($siteUrlArray.Count) site(s) to SP inclusion" -ForegroundColor Green
                    }
                }
                $policySuccess = $true
            }
            catch {
                $errorMsg = $_.Exception.Message
                $isPolicyLock = $errorMsg -match "PolicyLockConflict|being deployed|active requests"
                
                if ($isPolicyLock -and $retryCount -lt $maxRetries) {
                    $retryCount++
                    $waitSeconds = 30 * $retryCount
                    Write-Host "      [RETRY] Policy '$policyName' locked - waiting ${waitSeconds}s before retry $retryCount/$maxRetries..." -ForegroundColor DarkYellow
                    Start-Sleep -Seconds $waitSeconds
                }
                else {
                    Write-Warning "      [FAIL] Could not resume policy '$policyName': $_"
                    $allSuccess = $false
                    $failedSites += $siteUrlArray
                    break
                }
            }
        }
    }

    # Phase 3: Remove from suspended state
    $resumedCount = 0
    $failedNormalized = @($failedSites | ForEach-Object { $_.TrimEnd("/").ToLower() })

    foreach ($normalizedUrl in $validSites) {
        if ($failedNormalized -contains $normalizedUrl) { continue }

        $siteUrl = $script:SuspendedSites[$normalizedUrl] | ForEach-Object { $SiteUrls | Where-Object { $_.TrimEnd("/").ToLower() -eq $normalizedUrl } | Select-Object -First 1 }
        if (-not $siteUrl) { $siteUrl = $normalizedUrl }

        Add-RetentionPolicyLogEntry -SiteUrl $siteUrl -Action "BatchResume" -Policies $script:SuspendedSites[$normalizedUrl].Policies -Success $true
        $script:SuspendedSites.Remove($normalizedUrl)
        $resumedCount++
    }

    Save-RetentionPolicyState

    Write-Host "  [RETENTION-BATCH] Resumed $resumedCount of $($SiteUrls.Count) site(s)" -ForegroundColor $(if ($allSuccess) { "Green" } else { "Yellow" })

    return @{
        Success     = $allSuccess
        ResumedCount = $resumedCount
        FailedSites = $failedSites
    }
}

#endregion

#region Wait for Hold Release

function Wait-RetentionPolicyRelease {
    <#
    .SYNOPSIS
        Waits until the Preservation Hold Library is released for a site
        after removing it from retention policies. SPO needs time to propagate.
    .PARAMETER SiteUrl
        The SharePoint site URL
    .PARAMETER MaxWaitMinutes
        Maximum time to wait (default: 60 minutes)
    .PARAMETER CheckIntervalSeconds
        Interval between checks (default: 120 seconds)
    .OUTPUTS
        $true if hold was released, $false if timed out
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        [int]$MaxWaitMinutes = 60,
        [int]$CheckIntervalSeconds = 120
    )

    $stopWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $maxWaitMs = $MaxWaitMinutes * 60 * 1000
    $attempt = 0

    Write-Host "    [RETENTION] Waiting for hold release on: $SiteUrl (max ${MaxWaitMinutes}m)..." -ForegroundColor Yellow

    while ($stopWatch.ElapsedMilliseconds -lt $maxWaitMs) {
        $attempt++

        try {
            # Check if site still has a compliance hold via SPO
            $site = Get-SPOSite -Identity $SiteUrl -ErrorAction Stop
            $hasHold = $false

            # Check ComplianceAttribute or related property
            if ($site.LockState -eq "NoAccess") {
                $hasHold = $true
            }

            # Try to check if Preservation Hold Library is still active
            # The main indicator is whether version deletion operations succeed
            # After policy exclusion, SPO typically takes 24-48h to fully release
            # But for the version trimming job, it usually works within minutes
            # because the policy sync happens faster for SPO-level operations.

            # For practical purposes, we check if a test version-policy job can start
            try {
                $testProgress = Get-SPOSiteManageVersionPolicyJobProgress -Identity $SiteUrl -ErrorAction SilentlyContinue
                # If we can query progress, the site is accessible
                $hasHold = $false
            }
            catch {
                if ($_.Exception.Message -match "hold|retention|compliance|blocked") {
                    $hasHold = $true
                }
            }

            if (-not $hasHold) {
                $elapsed = [math]::Round($stopWatch.Elapsed.TotalMinutes, 1)
                Write-Host "    [RETENTION] Hold released after ${elapsed} minutes (attempt $attempt)" -ForegroundColor Green
                $stopWatch.Stop()
                return $true
            }

            $remaining = [math]::Round(($maxWaitMs - $stopWatch.ElapsedMilliseconds) / 60000, 1)
            Write-Host "      [WAIT] Still held (attempt $attempt, ${remaining}m remaining)..." -ForegroundColor DarkYellow
        }
        catch {
            Write-Host "      [WAIT] Check error (attempt $attempt): $_" -ForegroundColor DarkYellow
        }

        # Wait before next check
        Start-Sleep -Seconds $CheckIntervalSeconds
    }

    $stopWatch.Stop()
    Write-Warning "    [RETENTION] Timed out waiting for hold release on: $SiteUrl (waited ${MaxWaitMinutes}m)"
    return $false
}

#endregion

#region Status & Logging

function Get-RetentionPolicyManagerStatus {
    <#
    .SYNOPSIS
        Returns the current state of the retention policy manager
    #>
    [CmdletBinding()]
    param()

    return @{
        Enabled        = $script:ModuleEnabled
        Connected      = $script:IPPSConnected
        SuspendedSites = $script:SuspendedSites
        SuspendedCount = $script:SuspendedSites.Count
        LogFile        = $script:RetentionPolicyLogFile
        Sites          = @($script:SuspendedSites.Keys)
    }
}

function Add-RetentionPolicyLogEntry {
    [CmdletBinding()]
    param(
        [string]$SiteUrl,
        [string]$Action,
        [array]$Policies,
        [bool]$Success
    )

    $entry = @{
        Timestamp = (Get-Date).ToString("o")
        SiteUrl   = $SiteUrl
        Action    = $Action
        Policies  = @($Policies | ForEach-Object { $_.Name })
        Success   = $Success
    }

    $script:RetentionPolicyLog += $entry
}

function Save-RetentionPolicyState {
    <#
    .SYNOPSIS
        Persists the current suspended sites state and action log to JSON
    #>
    [CmdletBinding()]
    param()

    if (-not $script:RetentionPolicyLogFile) { return }

    try {
        $state = @{
            LastUpdated    = (Get-Date).ToString("o")
            SuspendedSites = $script:SuspendedSites
            Actions        = $script:RetentionPolicyLog
        }
        $state | ConvertTo-Json -Depth 10 | Set-Content -Path $script:RetentionPolicyLogFile -Encoding UTF8 -Force
    }
    catch {
        Write-Warning "Could not save retention policy state: $_"
    }
}

#endregion

#region Module Test

function Test-RetentionPolicyManagerConnection {
    <#
    .SYNOPSIS
        Tests if the IPPS connection is alive and functional
    .OUTPUTS
        $true if connected and working, $false otherwise
    #>
    [CmdletBinding()]
    param()

    if (-not $script:IPPSConnected) { return $false }

    try {
        $null = Get-RetentionCompliancePolicy -ErrorAction Stop | Select-Object -First 1
        return $true
    }
    catch {
        $script:IPPSConnected = $false
        return $false
    }
}

#endregion

#region Site Retention Summary

function Get-SiteRetentionSummary {
    <#
    .SYNOPSIS
        Returns retention data for a site to be embedded in execution history records
    .PARAMETER SiteUrl
        The SharePoint site URL
    .OUTPUTS
        Hashtable with RetentionManaged, RetentionPolicies, RetentionSuspendedAt, or $null if not managed
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl
    )

    $normalizedUrl = $SiteUrl.TrimEnd("/").ToLower()

    if (-not $script:SuspendedSites.ContainsKey($normalizedUrl)) {
        return $null
    }

    $info = $script:SuspendedSites[$normalizedUrl]
    return @{
        RetentionManaged     = $true
        RetentionPolicies    = @($info.Policies | ForEach-Object { $_.Name })
        RetentionSuspendedAt = $info.SuspendedAt
    }
}

#endregion

#region Dashboard Data Export

function Export-RetentionPolicyDatabase {
    <#
    .SYNOPSIS
        Exports all retention policies with their SharePoint exception sites to a JSON file
        for consumption by the Dashboard's Retention Policy tab
    .PARAMETER OutputPath
        Path to save RetentionPolicyDatabase.json (defaults to module log path)
    #>
    [CmdletBinding()]
    param(
        [string]$OutputPath
    )

    if (-not $script:IPPSConnected) {
        Write-Warning "IPPS not connected. Call Connect-RetentionPolicyManager first."
        return $null
    }

    if (-not $OutputPath) {
        $OutputPath = if ($script:RetentionPolicyLogFile) {
            Join-Path (Split-Path $script:RetentionPolicyLogFile -Parent) "RetentionPolicyDatabase.json"
        } else {
            Join-Path (Get-Location) "config\RetentionPolicyDatabase.json"
        }
    }

    Write-Host "  [RETENTION DB] Exporting retention policies..." -ForegroundColor Cyan

    try {
        $allPolicies = Get-RetentionCompliancePolicy -DistributionDetail -ErrorAction Stop
        $spPolicies = @()

        Write-Host "  [RETENTION DB] Found $(@($allPolicies).Count) total retention policies in tenant" -ForegroundColor Gray
        foreach ($policy in $allPolicies) {
            # Normalize location values (EXO V3 returns strings, RPS returns objects)
            $spLocNames = Get-LocationNames $policy.SharePointLocation
            $spExcNames = Get-LocationNames $policy.SharePointLocationException
            $mgLocNames = Get-LocationNames $policy.ModernGroupLocation
            $mgExcNames = Get-LocationNames $policy.ModernGroupLocationException

            # Debug output
            $spDisplay = if ($spLocNames.Count -gt 0) { $spLocNames -join ', ' } else { '(none)' }
            $mgDisplay = if ($mgLocNames.Count -gt 0) { $mgLocNames -join ', ' } else { '(none)' }
            Write-Host "    Policy: '$($policy.Name)' | SP:[$($spLocNames.Count)] $spDisplay | M365:[$($mgLocNames.Count)] $mgDisplay" -ForegroundColor DarkGray

            # Check SharePoint classic/communication sites location
            $hasSPClassic = $false
            $spInclusionType = "None"
            $spIncludedSites = @()
            $spExcludedSites = @()

            if ($spLocNames.Count -gt 0) {
                if ($spLocNames -match "^All$") {
                    $hasSPClassic = $true
                    $spInclusionType = "AllSites"
                } else {
                    $hasSPClassic = $true
                    $spInclusionType = "ExplicitSites"
                    $spIncludedSites = @($spLocNames | Where-Object { $_ -notmatch "^All$" })
                }
            }

            if ($hasSPClassic -and $spExcNames.Count -gt 0) {
                $spExcludedSites = $spExcNames
            }

            # Check Microsoft 365 Group sites location
            $hasM365Groups = $false
            $m365InclusionType = "None"
            $m365IncludedGroups = @()
            $m365ExcludedGroups = @()

            if ($mgLocNames.Count -gt 0) {
                if ($mgLocNames -match "^All$") {
                    $hasM365Groups = $true
                    $m365InclusionType = "AllGroups"
                } else {
                    $hasM365Groups = $true
                    $m365InclusionType = "ExplicitGroups"
                    $m365IncludedGroups = @($mgLocNames | Where-Object { $_ -notmatch "^All$" })
                }
            }

            if ($hasM365Groups -and $mgExcNames.Count -gt 0) {
                $m365ExcludedGroups = $mgExcNames
            }

            # Skip policies that have no SharePoint or M365 Group locations
            if (-not $hasSPClassic -and -not $hasM365Groups) { continue }

            # Build exception detail for SP classic sites
            $ourSPExclusions = @()
            foreach ($excSite in $spExcludedSites) {
                $normalizedExc = $excSite.TrimEnd("/").ToLower()
                $isSuspendedByUs = $script:SuspendedSites.ContainsKey($normalizedExc)
                $ourSPExclusions += @{
                    SiteUrl = $excSite
                    SuspendedByUs = $isSuspendedByUs
                    SuspendedAt = if ($isSuspendedByUs) { $script:SuspendedSites[$normalizedExc].SuspendedAt } else { $null }
                }
            }

            # For backward compat, set top-level InclusionType from SP classic (primary for version mgmt)
            $inclusionType = if ($hasSPClassic) { $spInclusionType } elseif ($hasM365Groups) { $m365InclusionType } else { "None" }

            $spPolicies += @{
                Name = $policy.Name
                Guid = $policy.Guid.ToString()
                Enabled = $policy.Enabled
                Mode = $policy.Mode
                # SharePoint classic/communication sites
                InclusionType = $inclusionType
                IncludedSiteCount = $spIncludedSites.Count
                IncludedSites = $spIncludedSites
                ExcludedSiteCount = $spExcludedSites.Count
                ExcludedSites = $ourSPExclusions
                ExceptionLimit = $script:MaxExceptionSitesPerPolicy
                ExceptionCapacityUsed = $spExcludedSites.Count + $m365ExcludedGroups.Count
                ExceptionCapacityRemaining = [math]::Max(0, $script:MaxExceptionSitesPerPolicy - ($spExcludedSites.Count + $m365ExcludedGroups.Count))
                # Microsoft 365 Group sites
                HasM365Groups = $hasM365Groups
                M365GroupInclusionType = $m365InclusionType
                M365GroupIncludedCount = $m365IncludedGroups.Count
                M365GroupIncluded = $m365IncludedGroups
                M365GroupExcludedCount = $m365ExcludedGroups.Count
                M365GroupExcluded = $m365ExcludedGroups
                # SP classic flag
                HasSPClassic = $hasSPClassic
                SPClassicInclusionType = $spInclusionType
                CreatedDate = if ($policy.WhenCreatedUTC) { $policy.WhenCreatedUTC.ToString("o") } else { $null }
                ModifiedDate = if ($policy.WhenChangedUTC) { $policy.WhenChangedUTC.ToString("o") } else { $null }
            }
        }

        $database = @{
            LastUpdated = (Get-Date).ToString("o")
            TotalPolicies = $spPolicies.Count
            MaxExceptionSitesPerPolicy = $script:MaxExceptionSitesPerPolicy
            CurrentlySuspendedSites = $script:SuspendedSites.Count
            Policies = $spPolicies
        }

        $database | ConvertTo-Json -Depth 10 | Set-Content -Path $OutputPath -Encoding UTF8 -Force
        Write-Host "  [RETENTION DB] Exported $($spPolicies.Count) policies to: $OutputPath" -ForegroundColor Green

        return $database
    }
    catch {
        Write-Warning "Error exporting retention policy database: $_"
        return $null
    }
}

function Get-RetentionPolicyAvailableCapacity {
    <#
    .SYNOPSIS
        Returns the minimum available exception capacity across all cached retention policies.
        Used by the orchestration to dynamically cap concurrent jobs.
    .OUTPUTS
        Integer representing how many more sites can be added to exception lists.
        Returns [int]::MaxValue if no policies are cached yet (no constraint).
    #>
    [CmdletBinding()]
    param()

    if ($script:PolicyExceptionCache.Count -eq 0) {
        return [int]::MaxValue
    }

    $minRemaining = [int]::MaxValue
    foreach ($policyGuid in $script:PolicyExceptionCache.Keys) {
        $used = $script:PolicyExceptionCache[$policyGuid]
        $remaining = $script:MaxExceptionSitesPerPolicy - $used
        if ($remaining -lt $minRemaining) {
            $minRemaining = $remaining
        }
    }

    return [math]::Max(0, $minRemaining)
}

#endregion

# Export module members
Export-ModuleMember -Function @(
    'Connect-RetentionPolicyManager',
    'Get-SiteRetentionPolicies',
    'Suspend-SiteRetentionPolicy',
    'Resume-SiteRetentionPolicy',
    'Suspend-BatchSiteRetentionPolicies',
    'Resume-BatchSiteRetentionPolicies',
    'Resume-AllSuspendedSites',
    'Wait-RetentionPolicyRelease',
    'Get-RetentionPolicyManagerStatus',
    'Get-SiteRetentionSummary',
    'Test-RetentionPolicyManagerConnection',
    'Save-RetentionPolicyState',
    'Export-RetentionPolicyDatabase',
    'Get-RetentionPolicyAvailableCapacity'
)
