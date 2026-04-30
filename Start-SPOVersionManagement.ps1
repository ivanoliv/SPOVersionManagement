# Start-SPOVersionManagement.ps1
# Main script for SharePoint Online version management
#
# Supports both interactive login and Entra ID App (certificate) authentication.
# When TenantId/ClientId/Certificate are configured (in AppPaths.json or via params),
# connects using app auth. Otherwise falls back to interactive login.
# When -Unattended is set, all interactive prompts are auto-answered.
#
# Usage (Interactive - user must Connect-SPOService first):
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com"
#
# Usage (Entra ID App - reads credentials from config\AppPaths.json):
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended
#
# Usage (Entra ID App - explicit parameters):
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" `
#       -TenantId "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" `
#       -ClientId "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" `
#       -CertificateThumbprint "AABBCCDD..." `
#       -Unattended
#
# Usage (Certificate PFX file):
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" `
#       -TenantId "..." -ClientId "..." `
#       -CertificatePath "C:\certs\app.pfx" `
#       -CertificatePassword (ConvertTo-SecureString "pass" -AsPlainText -Force) `
#       -Unattended
#
# Usage (with options):
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "..." -GraphReportCSV "C:\SharePointSiteUsage.csv"
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "..." -UseFileCache
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "..." -DeleteOnly -Unattended
#
# Required Entra ID API Permissions (Application):
#   - SharePoint > Sites.FullControl.All
#   - Microsoft Graph > Reports.Read.All (only if not using -GraphReportCSV)
#   - Microsoft Graph > Sites.Read.All (optional, for site enumeration)

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AdminUrl,

    [Parameter(Mandatory = $false)]
    [string]$TenantId,

    [Parameter(Mandatory = $false)]
    [string]$ClientId,

    [Parameter(Mandatory = $false)]
    [string]$CertificateThumbprint,

    [Parameter(Mandatory = $false)]
    [string]$CertificatePath,

    [Parameter(Mandatory = $false)]
    [SecureString]$CertificatePassword,

    [Parameter(Mandatory = $false)]
    [string]$InputSiteListCSV,

    [Parameter(Mandatory = $false)]
    [string]$InputExclusionSiteListCSV,

    [Parameter(Mandatory = $false)]
    [int]$MajorVersionLimit = 4,

    [Parameter(Mandatory = $false)]
    [int]$MajorWithMinorVersionsLimit = 4,

    [Parameter(Mandatory = $false)]
    [int]$MaxConcurrentJobs = 10,

    [Parameter(Mandatory = $false)]
    [switch]$SkipGraphConnection,

    [Parameter(Mandatory = $false)]
    [string]$GraphReportCSV,

    [Parameter(Mandatory = $false)]
    [switch]$OpenDashboard,

    [Parameter(Mandatory = $false)]
    [switch]$UseFileCache,

    [Parameter(Mandatory = $false)]
    [switch]$Unattended,
    
    [Parameter(Mandatory = $false)]
    [switch]$ManageRetentionPolicy,

    [Parameter(Mandatory = $false)]
    [switch]$DeleteOnly,

    [Parameter(Mandatory = $false)]
    [switch]$SyncOnly,

    [Parameter(Mandatory = $false)]
    [string]$InputSiteSyncListCSV,

    [Parameter(Mandatory = $false)]
    [int]$CheckBatchSize = 10,

    [Parameter(Mandatory = $false)]
    [int]$CheckBatchDelaySeconds = 2,

    [Parameter(Mandatory = $false)]
    [switch]$ResetDatabase
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Read version from AppPaths.json
$appVersion = "unknown"
$appPathsPath = Join-Path $scriptPath "config\AppPaths.json"
if (Test-Path $appPathsPath) {
    try {
        $appPathsData = Get-Content $appPathsPath -Raw | ConvertFrom-Json
        if ($appPathsData.AppVersion) { $appVersion = $appPathsData.AppVersion }
    } catch { }
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  SPO VERSION MANAGEMENT v$appVersion" -ForegroundColor Cyan
Write-Host "  SharePoint Version Management" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Admin URL:   $AdminUrl" -ForegroundColor Cyan
if ($Unattended) {
    Write-Host "  Mode:        UNATTENDED (no interactive prompts)" -ForegroundColor Yellow
}
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

#region Database Reset (must run before anything else)
if ($ResetDatabase) {
    $logsDir = Join-Path $scriptPath "Logs"
    Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Red
    Write-Host "  ║  RESETTING DATABASE                                         ║" -ForegroundColor Red
    Write-Host "  ║  Clearing all execution history, site cache, sessions...     ║" -ForegroundColor Red
    Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Red
    Write-Host ""
    
    $doReset = $false
    if ($Unattended) {
        Write-Host "  [UNATTENDED] Auto-confirming database reset." -ForegroundColor Yellow
        $doReset = $true
    }
    else {
        do {
            $resetConfirm = Read-Host "  Are you sure you want to reset ALL data? (Y/N)"
            if ([string]::IsNullOrWhiteSpace($resetConfirm) -or ($resetConfirm -notmatch '^[YyNn]$')) {
                Write-Host "  [!] Please enter Y or N" -ForegroundColor Yellow
            }
        } while ([string]::IsNullOrWhiteSpace($resetConfirm) -or ($resetConfirm -notmatch '^[YyNn]$'))
        if ($resetConfirm -match '^[Yy]$') { $doReset = $true }
    }
    
    if (-not $doReset) {
        Write-Host "  Database reset cancelled." -ForegroundColor Yellow
        Write-Host ""
        $ResetDatabase = $false
    }
    else {
    
    $resetFiles = @(
        'SiteExecutionHistory.json', 'JobStatus.json', 'ExecutionHistory.csv',
        'AllSites.json', 'SessionHistory.json', 'SiteStorage.csv',
        'TenantStorage.json', 'TenantStorageTimeline.json',
        'RetentionPolicyDatabase.json', 'RetentionPolicyLog.json'
    )
    $deletedCount = 0
    foreach ($f in $resetFiles) {
        $fp = Join-Path $logsDir $f
        if (Test-Path $fp) {
            Remove-Item $fp -Force
            Write-Host "  [DELETED] $f" -ForegroundColor Yellow
            $deletedCount++
        }
    }
    # Remove per-execution CSV files
    Get-ChildItem -Path $logsDir -Filter 'Execution_*.csv' -ErrorAction SilentlyContinue | ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "  [DELETED] $($_.Name)" -ForegroundColor Yellow
        $deletedCount++
    }
    
    Write-Host ""
    Write-Host "  Database reset complete. $deletedCount file(s) removed." -ForegroundColor Green
    Write-Host "  Starting fresh with clean state..." -ForegroundColor Green
    Write-Host ""
    }
}
#endregion

#region Load Entra ID credentials from AppPaths.json if not provided via parameters
if (-not $TenantId -or -not $ClientId) {
    $appPathsFile = Join-Path $scriptPath "config\AppPaths.json"
    if (Test-Path $appPathsFile) {
        try {
            $appPathsJson = Get-Content $appPathsFile -Raw | ConvertFrom-Json
            if ($appPathsJson.EntraIdApp) {
                if (-not $TenantId -and $appPathsJson.EntraIdApp.TenantId) {
                    $TenantId = $appPathsJson.EntraIdApp.TenantId
                    Write-Host "  [CONFIG] TenantId loaded from AppPaths.json" -ForegroundColor Gray
                }
                if (-not $ClientId -and $appPathsJson.EntraIdApp.ClientId) {
                    $ClientId = $appPathsJson.EntraIdApp.ClientId
                    Write-Host "  [CONFIG] ClientId loaded from AppPaths.json" -ForegroundColor Gray
                }
                if (-not $CertificateThumbprint -and -not $CertificatePath -and $appPathsJson.EntraIdApp.CertificateThumbprint) {
                    $CertificateThumbprint = $appPathsJson.EntraIdApp.CertificateThumbprint
                    Write-Host "  [CONFIG] CertificateThumbprint loaded from AppPaths.json" -ForegroundColor Gray
                }
            }
        }
        catch {
            Write-Warning "Could not read EntraIdApp config from AppPaths.json: $_"
        }
    }
}
#endregion

#region Validate parameters and determine auth mode
$useAppAuth = $false
if ($TenantId -and $ClientId -and ($CertificateThumbprint -or $CertificatePath)) {
    $useAppAuth = $true
    if ($CertificatePath -and -not (Test-Path $CertificatePath)) {
        Write-Error "Certificate file not found: $CertificatePath"
        exit 1
    }
}
elseif ($Unattended -and (-not $TenantId -or -not $ClientId)) {
    # Unattended requires app auth credentials
    Write-Error "Unattended mode requires TenantId + ClientId + Certificate. Configure in AppPaths.json under EntraIdApp or pass as parameters."
    exit 1
}
else {
    # Interactive mode: user must have already connected via Connect-SPOService
    Write-Host "  Auth: Interactive login (ensure Connect-SPOService was run)" -ForegroundColor Gray
}
#endregion

#region Pre-connect to SharePoint Online using Entra ID App
if ($useAppAuth) {
Write-Host "[1/7] Connecting to SharePoint Online (Entra ID App)..." -ForegroundColor Yellow
try {
    # Check if already connected
    $alreadyConnected = $false
    try {
        $null = Get-SPOTenant -ErrorAction Stop
        $alreadyConnected = $true
        Write-Host "  [OK] Already connected to SharePoint Online Admin" -ForegroundColor Green
    }
    catch {
        # Not connected, proceed with app auth
    }

    if (-not $alreadyConnected) {
        $spoParams = @{
            Url = $AdminUrl
        }

        if ($CertificateThumbprint) {
            # Certificate from local store (CurrentUser\My)
            $spoParams.ClientId = $ClientId
            $spoParams.CertificateThumbprint = $CertificateThumbprint
            $spoParams.Tenant = $TenantId
            Write-Host "  Authenticating with certificate thumbprint: $($CertificateThumbprint.Substring(0, [Math]::Min(8, $CertificateThumbprint.Length)))..." -ForegroundColor Gray
        }
        elseif ($CertificatePath) {
            # Certificate from PFX file
            $spoParams.ClientId = $ClientId
            $spoParams.CertificatePath = $CertificatePath
            $spoParams.Tenant = $TenantId
            if ($CertificatePassword) {
                $spoParams.CertificatePassword = $CertificatePassword
            }
            Write-Host "  Authenticating with certificate file: $CertificatePath" -ForegroundColor Gray
        }

        Connect-SPOService @spoParams
        Write-Host "  [OK] Connected to SharePoint Online Admin: $AdminUrl" -ForegroundColor Green
    }
}
catch {
    Write-Error "Error connecting to SharePoint Online with app credentials: $_"
    exit 1
}
#endregion

#region Pre-connect to Microsoft Graph using Entra ID App
$skipGraph = $false
if ($GraphReportCSV -or $SkipGraphConnection) {
    $skipGraph = $true
    Write-Host "[2/7] Graph connection skipped (using manual CSV report)" -ForegroundColor Gray
}

if (-not $skipGraph) {
    Write-Host "[2/7] Connecting to Microsoft Graph (Entra ID App)..." -ForegroundColor Yellow
    try {
        $graphAvailable = Get-Module -ListAvailable -Name "Microsoft.Graph.Authentication" -ErrorAction SilentlyContinue
        if ($graphAvailable) {
            Import-Module Microsoft.Graph.Authentication -ErrorAction Stop

            $context = Get-MgContext -ErrorAction SilentlyContinue
            if (-not $context) {
                $mgParams = @{
                    ClientId = $ClientId
                    TenantId = $TenantId
                    NoWelcome = $true
                }

                if ($CertificateThumbprint) {
                    $mgParams.CertificateThumbprint = $CertificateThumbprint
                }
                elseif ($CertificatePath) {
                    # Load certificate from PFX for Graph SDK
                    $certLoadParams = @{}
                    if ($CertificatePassword) {
                        $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new(
                            $CertificatePath,
                            $CertificatePassword,
                            [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::MachineKeySet
                        )
                    }
                    else {
                        $cert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($CertificatePath)
                    }
                    $mgParams.Certificate = $cert
                }

                Connect-MgGraph @mgParams -ErrorAction Stop
            }

            # Import Reports module
            if (Get-Module -ListAvailable -Name "Microsoft.Graph.Reports") {
                Import-Module Microsoft.Graph.Reports -ErrorAction Stop
            }

            Write-Host "  [OK] Connected to Microsoft Graph (Reports)" -ForegroundColor Green
        }
        else {
            Write-Warning "Microsoft.Graph.Authentication module not found. Graph features will be unavailable."
            Write-Host "  Install with: Install-Module Microsoft.Graph.Authentication, Microsoft.Graph.Reports" -ForegroundColor Gray
        }
    }
    catch {
        Write-Warning "Could not connect to Graph API with app credentials: $_"
        Write-Host "  Graph features will be unavailable. Use -GraphReportCSV for manual import." -ForegroundColor Gray
    }
}
#endregion

} # end if ($useAppAuth)

#region Import modules
Write-Host "[3/7] Importing modules..." -ForegroundColor Yellow
try {
    Import-Module "$scriptPath\SPOVersionManagement.psm1" -Force -DisableNameChecking -ErrorAction Stop
    Write-Host "  [OK] SPOVersionManagement.psm1 imported" -ForegroundColor Green

    Import-Module "$scriptPath\SPOSiteFilters.psm1" -Force -DisableNameChecking -ErrorAction Stop
    Write-Host "  [OK] SPOSiteFilters.psm1 imported" -ForegroundColor Green

    # Import retention policy manager module (optional)
    if ($ManageRetentionPolicy) {
        Import-Module "$scriptPath\SPORetentionPolicyManager.psm1" -Force -DisableNameChecking -ErrorAction Stop
        Write-Host "  [OK] SPORetentionPolicyManager.psm1 imported" -ForegroundColor Green
    }

    $appPaths = Get-SPOAppPaths
    Write-Host "  [OK] Path configuration loaded" -ForegroundColor Green
}
catch {
    Write-Error "Error importing modules: $_"
    exit 1
}
#endregion

#region Check for pending sessions
$pendingSessions = Get-PendingSessions
$resumeFromSession = $null
$useSessionConfig = $false

if ($pendingSessions -and $pendingSessions.Count -gt 0) {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Yellow
    Write-Host "  PENDING SESSION(S) DETECTED" -ForegroundColor Yellow
    Write-Host "================================================================" -ForegroundColor Yellow

    foreach ($session in $pendingSessions) {
        $startTime = if ($session.StartedAt) { [DateTime]::Parse($session.StartedAt).ToString("dd/MM/yyyy HH:mm") } else { "Unknown" }
        $progress = if ($session.Progress) { "$($session.Progress.ProcessedSites)/$($session.Progress.TotalSites) sites" } else { "Unknown" }
        Write-Host "  Session: $($session.SessionId) | Started: $startTime | Progress: $progress" -ForegroundColor Cyan
    }

    if ($Unattended) {
        # Unattended: auto-continue the most recent pending session
        $resumeFromSession = $pendingSessions | Sort-Object { [DateTime]::Parse($_.LastUpdated) } -Descending | Select-Object -First 1
        $useSessionConfig = $true
        Write-Host ""
        Write-Host "  [UNATTENDED] Auto-resuming most recent session: $($resumeFromSession.SessionId)" -ForegroundColor Yellow
    }
    else {
        Write-Host ""
        Write-Host "  Options:" -ForegroundColor White
        Write-Host "  [C] Continue the most recent pending session" -ForegroundColor Green
        Write-Host "  [N] Start a NEW session (ignore pending)" -ForegroundColor Yellow
        Write-Host "  [X] Cancel all pending sessions and start new" -ForegroundColor Red
        Write-Host ""

        do {
            $sessionChoice = Read-Host "  Choose an option (C/N/X)"
            if ([string]::IsNullOrWhiteSpace($sessionChoice) -or ($sessionChoice -notmatch '^[CcNnXx]$')) {
                Write-Host "  [!] Please enter C, N or X" -ForegroundColor Yellow
            }
        } while ([string]::IsNullOrWhiteSpace($sessionChoice) -or ($sessionChoice -notmatch '^[CcNnXx]$'))

        if ($sessionChoice -eq 'C' -or $sessionChoice -eq 'c') {
            $resumeFromSession = $pendingSessions | Sort-Object { [DateTime]::Parse($_.LastUpdated) } -Descending | Select-Object -First 1
            $useSessionConfig = $true
            Write-Host "  [OK] Resuming session: $($resumeFromSession.SessionId)" -ForegroundColor Green
        }
        elseif ($sessionChoice -eq 'X' -or $sessionChoice -eq 'x') {
            foreach ($session in $pendingSessions) {
                Update-SessionProgress -SessionId $session.SessionId -Status "Cancelled"
            }
            Write-Host "  [OK] All pending sessions cancelled" -ForegroundColor Yellow
        }
        else {
            Write-Host "  [OK] Starting new session" -ForegroundColor Yellow
        }
    }

    # Restore configuration from session
    if ($useSessionConfig -and $resumeFromSession -and $resumeFromSession.Configuration) {
        $MajorVersionLimit = $resumeFromSession.Configuration.MajorVersionLimit
        $MajorWithMinorVersionsLimit = $resumeFromSession.Configuration.MajorWithMinorVersionsLimit
        $MaxConcurrentJobs = $resumeFromSession.Configuration.MaxConcurrentJobs
        if ($resumeFromSession.Configuration.InputSiteListCSV) { $InputSiteListCSV = $resumeFromSession.Configuration.InputSiteListCSV }
        if ($resumeFromSession.Configuration.InputExclusionSiteListCSV) { $InputExclusionSiteListCSV = $resumeFromSession.Configuration.InputExclusionSiteListCSV }
        if ($resumeFromSession.Configuration.GraphReportCSV) { $GraphReportCSV = $resumeFromSession.Configuration.GraphReportCSV }
        if ($resumeFromSession.AdminUrl -and $resumeFromSession.AdminUrl -ne $AdminUrl) {
            Write-Host "  [INFO] Using session AdminUrl: $($resumeFromSession.AdminUrl)" -ForegroundColor Cyan
            $AdminUrl = $resumeFromSession.AdminUrl
        }
        Write-Host "  Configuration restored from session" -ForegroundColor Green
    }
}
#endregion

# Generate new session ID
$currentSessionId = (Get-Date -Format 'yyyyMMdd_HHmmss') + "_" + [guid]::NewGuid().ToString().Substring(0, 8)

#region Initialize module (SPO + Graph already connected above, module will detect active sessions)
Write-Host ""
Write-Host "[4/7] Initializing module..." -ForegroundColor Yellow
try {
    $connectParams = @{
        AdminUrl = $AdminUrl
        MaxConcurrentJobs = $MaxConcurrentJobs
    }
    if ($skipGraph -or $GraphReportCSV) {
        $connectParams.SkipGraphConnection = $true
        if ($GraphReportCSV) { $connectParams.GraphReportCSV = $GraphReportCSV }
    }
    if ($UseFileCache) { $connectParams.UseFileCache = $true }

    Connect-SPOVersionManagement @connectParams
}
catch {
    Write-Error "Error initializing module: $_"
    exit 1
}
#endregion

#region Tenant mismatch detection
$logPath = Join-Path $scriptPath "Logs"
$sessionHistoryPath = Join-Path $logPath "SessionHistory.json"
if (-not $ResetDatabase -and (Test-Path $sessionHistoryPath)) {
    try {
        $historyData = Get-Content $sessionHistoryPath -Raw | ConvertFrom-Json
        if ($historyData.Sessions -and $historyData.Sessions.Count -gt 0) {
            $lastSession = $historyData.Sessions | Select-Object -Last 1
            if ($lastSession.AdminUrl -and $lastSession.AdminUrl -ne $AdminUrl) {
                Write-Host ""
                Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
                Write-Host "  ║  ⚠️  TENANT MISMATCH DETECTED                                ║" -ForegroundColor Yellow
                Write-Host "  ║                                                              ║" -ForegroundColor Yellow
                Write-Host "  ║  Current AdminUrl:  $($AdminUrl.PadRight(40))║" -ForegroundColor Yellow
                Write-Host "  ║  Database AdminUrl: $($lastSession.AdminUrl.PadRight(40))║" -ForegroundColor Yellow
                Write-Host "  ║                                                              ║" -ForegroundColor Yellow
                Write-Host "  ║  The execution database contains data from a different        ║" -ForegroundColor Yellow
                Write-Host "  ║  tenant. Running without reset will sync stale data.          ║" -ForegroundColor Yellow
                Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Yellow
                Write-Host ""
                if ($Unattended) {
                    Write-Host "  [UNATTENDED] Automatically resetting database for tenant switch." -ForegroundColor Yellow
                    $ResetDatabase = $true
                }
                else {
                    do {
                        $tenantChoice = Read-Host "  Reset database and start fresh? (Y/N)"
                        if ([string]::IsNullOrWhiteSpace($tenantChoice) -or ($tenantChoice -notmatch '^[YyNn]$')) {
                            Write-Host "  [!] Please enter Y or N" -ForegroundColor Yellow
                        }
                    } while ([string]::IsNullOrWhiteSpace($tenantChoice) -or ($tenantChoice -notmatch '^[YyNn]$'))
                    
                    if ($tenantChoice -match '^[Yy]$') {
                        $ResetDatabase = $true
                        Write-Host "  [OK] Database will be reset before processing." -ForegroundColor Green
                    }
                    else {
                        Write-Host "  [OK] Continuing with existing database (data may be stale)." -ForegroundColor Yellow
                    }
                }
            }
        }
    }
    catch {
        Write-Warning "Could not check session history for tenant mismatch: $_"
    }
}
#endregion

#region Configure site filters
Write-Host ""
Write-Host "[5/7] Configuring site filters..." -ForegroundColor Yellow

if ($InputSiteListCSV) {
    if (Test-Path $InputSiteListCSV) {
        Write-Host "  Loading INCLUSION list: $InputSiteListCSV" -ForegroundColor Cyan
        try {
            $includedSites = Set-SiteInclusionList -CsvPath $InputSiteListCSV
            Write-Host "  [OK] $($includedSites.Count) sites configured for processing" -ForegroundColor Green
        }
        catch { Write-Warning "Error loading inclusion list: $_" }
    }
    else { Write-Warning "Inclusion file not found: $InputSiteListCSV" }
}
else {
    Write-Host "  Inclusion list: NOT CONFIGURED (process all sites)" -ForegroundColor Gray
}

if ($InputExclusionSiteListCSV) {
    if (Test-Path $InputExclusionSiteListCSV) {
        Write-Host "  Loading EXCLUSION list: $InputExclusionSiteListCSV" -ForegroundColor Yellow
        $excludedSitesFile = $appPaths.ExcludedSitesFile
        $hasExistingList = $false

        if (Test-Path $excludedSitesFile) {
            try {
                $savedExclusion = Get-Content $excludedSitesFile -Raw | ConvertFrom-Json
                if ($savedExclusion.Count -gt 0 -and $savedExclusion.Sites -and $savedExclusion.Sites.Count -gt 0) {
                    $hasExistingList = $true

                    if ($Unattended) {
                        # Unattended: merge (add new, keep existing)
                        Write-Host "  [UNATTENDED] Merging exclusion list (existing: $($savedExclusion.Count) sites)" -ForegroundColor Yellow
                        $existingExclusion = @($savedExclusion.Sites | ForEach-Object { if ($_.Url) { $_.Url } else { $_ } })
                        $csvSites = @(Import-SiteListFromCSV -CsvPath $InputExclusionSiteListCSV)
                        $existingNormalized = @($existingExclusion | ForEach-Object { $_.TrimEnd("/").ToLower() })
                        $newSites = @()
                        foreach ($site in $csvSites) {
                            if ($existingNormalized -notcontains $site.TrimEnd("/").ToLower()) { $newSites += $site }
                        }
                        if ($newSites.Count -gt 0) {
                            $mergedSites = $existingExclusion + $newSites
                            $excludedSites = Set-SiteExclusionListFromArray -Sites $mergedSites
                            Write-Host "  [OK] Merged: $($newSites.Count) new + $($existingExclusion.Count) existing = $($excludedSites.Count) total" -ForegroundColor Yellow
                        }
                        else {
                            Write-Host "  [OK] No new sites to add. Exclusion list unchanged ($($savedExclusion.Count) sites)" -ForegroundColor Yellow
                        }
                    }
                    else {
                        # Interactive: ask user
                        Write-Host "  Existing exclusion list: $($savedExclusion.Count) sites" -ForegroundColor Cyan
                        Write-Host "  [O] Overwrite | [M] Merge | [C] Cancel" -ForegroundColor White
                        do {
                            $mergeOption = Read-Host "  Choose (O/M/C)"
                        } while ($mergeOption -notmatch '^[OoMmCc]$')

                        if ($mergeOption -match '^[Cc]$') {
                            Write-Host "  [OK] Existing list kept." -ForegroundColor Gray
                        }
                        elseif ($mergeOption -match '^[Mm]$') {
                            $existingExclusion = @($savedExclusion.Sites | ForEach-Object { if ($_.Url) { $_.Url } else { $_ } })
                            $csvSites = @(Import-SiteListFromCSV -CsvPath $InputExclusionSiteListCSV)
                            $existingNormalized = @($existingExclusion | ForEach-Object { $_.TrimEnd("/").ToLower() })
                            $newSites = @(); $dupCount = 0
                            foreach ($site in $csvSites) {
                                if ($existingNormalized -notcontains $site.TrimEnd("/").ToLower()) { $newSites += $site } else { $dupCount++ }
                            }
                            if ($newSites.Count -gt 0) {
                                $excludedSites = Set-SiteExclusionListFromArray -Sites ($existingExclusion + $newSites)
                                Write-Host "  [OK] Merged: +$($newSites.Count) new, $dupCount duplicates skipped. Total: $($excludedSites.Count)" -ForegroundColor Green
                            }
                            else { Write-Host "  [OK] All CSV sites already exist. List unchanged." -ForegroundColor Cyan }
                        }
                        else {
                            $excludedSites = Set-SiteExclusionList -CsvPath $InputExclusionSiteListCSV
                            Write-Host "  [OK] List replaced. $($excludedSites.Count) sites excluded." -ForegroundColor Yellow
                        }
                    }
                }
            }
            catch {
                Write-Host "  [WARNING] Error reading existing list. Overwriting." -ForegroundColor DarkYellow
                $hasExistingList = $false
            }
        }

        if (-not $hasExistingList) {
            try {
                $excludedSites = Set-SiteExclusionList -CsvPath $InputExclusionSiteListCSV
                Write-Host "  [OK] $($excludedSites.Count) sites configured for EXCLUSION" -ForegroundColor Yellow
            }
            catch { Write-Warning "Error loading exclusion list: $_" }
        }
    }
    else { Write-Warning "Exclusion file not found: $InputExclusionSiteListCSV" }
}
else {
    # Load previously saved exclusion list if it exists
    $excludedSitesFile = $appPaths.ExcludedSitesFile
    if (Test-Path $excludedSitesFile) {
        try {
            $savedExclusion = Get-Content $excludedSitesFile -Raw | ConvertFrom-Json
            if ($savedExclusion.Count -gt 0 -and $savedExclusion.Sites -and $savedExclusion.Sites.Count -gt 0) {
                if ($Unattended) {
                    # Unattended: keep existing exclusion list
                    Write-Host "  [UNATTENDED] Existing exclusion list kept ($($savedExclusion.Count) sites protected)" -ForegroundColor Yellow
                    $excludedUrls = @($savedExclusion.Sites | ForEach-Object { if ($_.Url) { $_.Url } else { $_ } })
                    Set-SiteExclusionListFromArray -SiteUrls $excludedUrls
                }
                else {
                    Write-Host "  Existing exclusion list: $($savedExclusion.Count) sites" -ForegroundColor Yellow
                    Write-Host "  [K] Keep | [C] Clear" -ForegroundColor White
                    do {
                        $keepList = Read-Host "  Choose (K/C)"
                    } while ($keepList -notmatch '^[KkCc]$')

                    if ($keepList -match '^[Cc]$') {
                        Clear-SiteExclusionList
                        Write-Host "  [OK] Exclusion list cleared." -ForegroundColor Green
                    }
                    else {
                        $excludedUrls = @($savedExclusion.Sites | ForEach-Object { if ($_.Url) { $_.Url } else { $_ } })
                        Set-SiteExclusionListFromArray -SiteUrls $excludedUrls
                        Write-Host "  [OK] Exclusion list kept ($($savedExclusion.Count) sites)" -ForegroundColor Yellow
                    }
                }
            }
            else { Write-Host "  Exclusion list: NOT CONFIGURED" -ForegroundColor Gray }
        }
        catch { Write-Host "  Exclusion list: NOT CONFIGURED" -ForegroundColor Gray }
    }
    else { Write-Host "  Exclusion list: NOT CONFIGURED" -ForegroundColor Gray }
}
#endregion

#region Tenant status (already handled by module init, show summary)
Write-Host ""
Write-Host "[5/7] Tenant status..." -ForegroundColor Yellow
try {
    $tenantStorageFile = $appPaths.TenantStorageFile
    if (Test-Path $tenantStorageFile) {
        $tenantStorage = Get-Content $tenantStorageFile -Raw | ConvertFrom-Json
        if ($tenantStorage) {
            $percentUsed = $tenantStorage.PercentUsed
            $status = $tenantStorage.StorageStatus
            $storageMsg = '  Storage: {0} GB / {1} GB ({2}%)' -f $tenantStorage.StorageUsedGB, $tenantStorage.TenantQuotaGB, $percentUsed
            $storageColor = if ($status -eq 'Critical') { 'Red' } elseif ($status -eq 'Warning') { 'Yellow' } else { 'Green' }
            Write-Host $storageMsg -ForegroundColor $storageColor
            Write-Host "  Total sites: $($tenantStorage.SiteCount)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Warning "Could not read tenant status: $_"
}
#endregion

#region Export AllSites for Dashboard
$allSitesFile = $appPaths.AllSitesFile
$useCache = $false

if ($UseFileCache) {
    Write-Host "  [FILE CACHE] Using existing AllSites.json" -ForegroundColor Cyan
    $useCache = $true
}
elseif (Test-Path $allSitesFile) {
    try {
        $cachedData = Get-Content $allSitesFile -Raw | ConvertFrom-Json
        $cachedSiteCount = if ($cachedData.Sites) { $cachedData.Sites.Count } else { 0 }

        if ($cachedSiteCount -gt 0) {
            if ($Unattended) {
                # Unattended: update (upsert) by default
                Write-Host "  [UNATTENDED] Updating site base (upsert, existing cache: $cachedSiteCount sites)..." -ForegroundColor Cyan
            }
            else {
                Write-Host "  Cached sites: $cachedSiteCount (from $($cachedData.ExportedAt))" -ForegroundColor Cyan
                Write-Host "  [C] Use Cache | [U] Update" -ForegroundColor White
                do {
                    $cacheOption = Read-Host "  Choose (C/U)"
                } while ($cacheOption -notmatch '^[CcUu]$')

                if ($cacheOption -match '^[Cc]$') {
                    $useCache = $true
                    Write-Host "  [OK] Using cached data" -ForegroundColor Green
                }
            }
        }
    }
    catch { Write-Host "  [WARNING] Error reading cache." -ForegroundColor DarkYellow }
}

if (-not $useCache) {
    Write-Host "  Exporting site list for Dashboard..." -ForegroundColor Cyan
    try {
        if ($InputSiteListCSV -and $includedSites -and $includedSites.Count -gt 0) {
            if ($Unattended) {
                # Unattended: load all tenant sites for accurate Dashboard counts
                Export-AllSitesDataForDashboard -Upsert -MarkProcessingTargets
                Write-Host "  [OK] All sites exported with processing targets marked" -ForegroundColor Green
            }
            else {
                Export-AllSitesDataForDashboard -Upsert
            }
        }
        else {
            Export-AllSitesDataForDashboard -Upsert
        }
        Write-Host "  [OK] Site list exported (AllSites.json)" -ForegroundColor Green
    }
    catch { Write-Warning "Error exporting site list: $_" }
}
#endregion

#region Check previous execution / resume
$jobStatusFile = $appPaths.JobStatusFile
$resumeExecution = $false

if (Test-Path $jobStatusFile) {
    try {
        $existingStatus = Get-Content $jobStatusFile -Raw | ConvertFrom-Json
        $activeJobsCount = if ($existingStatus.ActiveJobs) { $existingStatus.ActiveJobs.Count } else { 0 }
        $queuedSitesCount = if ($existingStatus.QueuedSitesCount) { $existingStatus.QueuedSitesCount } else { 0 }
        $completedCount = if ($existingStatus.CompletedJobsCount) { $existingStatus.CompletedJobsCount } else { 0 }

        if ($activeJobsCount -eq 0 -and $queuedSitesCount -eq 0 -and $completedCount -gt 0) {
            Write-Host "  Previous execution completed ($completedCount jobs). Starting new." -ForegroundColor Green
        }
        elseif ($activeJobsCount -gt 0 -or $queuedSitesCount -gt 0) {
            Write-Host "  Previous execution: $activeJobsCount active, $queuedSitesCount queued, $completedCount completed" -ForegroundColor Yellow

            if ($Unattended) {
                # Unattended: auto-continue
                $resumeExecution = $true
                Write-Host "  [UNATTENDED] Auto-continuing previous execution" -ForegroundColor Yellow
            }
            else {
                Write-Host "  [C] Continue | [R] Restart" -ForegroundColor White
                do {
                    $resumeOption = Read-Host "  Choose (C/R)"
                } while ($resumeOption -notmatch '^[CcRr]$')

                if ($resumeOption -match '^[Cc]$') {
                    $resumeExecution = $true
                    Write-Host "  [OK] Continuing previous execution" -ForegroundColor Green
                }
                else { Write-Host "  [OK] Restarting from scratch" -ForegroundColor Yellow }
            }
        }
    }
    catch { Write-Host "  [WARNING] Error reading previous status" -ForegroundColor DarkYellow }
}

if (-not $resumeExecution) {
    @{
        LastUpdated                = (Get-Date).ToString("o")
        ActiveJobs                 = @()
        QueuedSites                = @()
        QueuedSitesCount           = 0
        QueuedSitesSyncCount       = 0
        QueuedSitesDeleteCount     = 0
        RecentCompletedJobs        = @()
        CompletedJobsCount         = 0
        MajorVersionLimit          = $MajorVersionLimit
        MajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
    } | ConvertTo-Json -Depth 10 | Set-Content -Path $jobStatusFile -Encoding UTF8
    Write-Host "  [OK] Job status initialized" -ForegroundColor Green
}
#endregion

#region Dashboard
Write-Host ""
Write-Host "[6/7] Dashboard..." -ForegroundColor Yellow
$dashboardPath = "$scriptPath\web\Dashboard.html"
if (Test-Path $dashboardPath) {
    Write-Host "  Dashboard: $dashboardPath" -ForegroundColor Cyan
    if ($OpenDashboard) {
        Start-Process $dashboardPath
        Write-Host "  [OK] Dashboard opened" -ForegroundColor Green
    }
}
#endregion

#region Start orchestration
if ($SyncOnly -and -not $InputSiteSyncListCSV) {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "  SYNC COMPLETE - Dashboard data updated" -ForegroundColor Green
    Write-Host "================================================================" -ForegroundColor Green
    Write-Host "  AllSites.json, TenantStorage.json, and filters are up to date." -ForegroundColor Cyan
    Write-Host "  Open the Dashboard to view current tenant status." -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Green
    if ($OpenDashboard) {
        Start-Process "$scriptPath\web\Dashboard.html"
    }
    exit 0
}

if ($SyncOnly -and $InputSiteSyncListCSV) {
    # Load sync CSV as inclusion list for orchestration
    if (Test-Path $InputSiteSyncListCSV) {
        Write-Host "  Loading SYNC site list: $InputSiteSyncListCSV" -ForegroundColor Cyan
        try {
            $syncSites = Set-SiteInclusionList -CsvPath $InputSiteSyncListCSV
            Write-Host "  [OK] $($syncSites.Count) sites configured for sync" -ForegroundColor Green
        } catch {
            Write-Warning "Error loading sync site list: $_"
            exit 1
        }
    } else {
        Write-Warning "Sync site list file not found: $InputSiteSyncListCSV"
        exit 1
    }
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  SYNC ONLY MODE - Processing CSV sites" -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "  Only SyncListPolicy will run (no BatchDelete)." -ForegroundColor Cyan
    Write-Host "  This imports version data from SPO into local JSON databases." -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "[7/7] Starting orchestration..." -ForegroundColor Yellow
Write-Host ""

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURATION SUMMARY" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Admin URL:              $AdminUrl" -ForegroundColor Cyan
Write-Host "  Auth:                   Entra ID App ($ClientId)" -ForegroundColor Cyan
Write-Host "  Max Jobs:               $MaxConcurrentJobs" -ForegroundColor Cyan
Write-Host "  Major Version Limit:    $MajorVersionLimit" -ForegroundColor Cyan
Write-Host "  Minor Version Limit:    $MajorWithMinorVersionsLimit" -ForegroundColor Cyan
Write-Host "  Unattended:             $Unattended" -ForegroundColor $(if ($Unattended) { "Yellow" } else { "Gray" })

if ($SyncOnly) {
    Write-Host "  Mode:                   SYNC ONLY (no BatchDelete)" -ForegroundColor Yellow
} elseif ($DeleteOnly) {
    Write-Host "  Mode:                   DELETE ONLY (no SyncListPolicy)" -ForegroundColor Yellow
} else {
    Write-Host "  Mode:                   Full (Sync + Delete)" -ForegroundColor Gray
}

if ($InputSiteListCSV) {
    $incCount = (Get-SiteInclusionList).Count
    Write-Host "  Sites Included:         $incCount sites" -ForegroundColor Green
}
else { Write-Host "  Sites Included:         ALL" -ForegroundColor Gray }

if ($InputExclusionSiteListCSV) {
    $excCount = (Get-SiteExclusionList).Count
    Write-Host "  Sites Excluded:         $excCount sites (protected)" -ForegroundColor Yellow
}
else { Write-Host "  Sites Excluded:         NONE" -ForegroundColor Gray }

if ($UseFileCache) { Write-Host "  File Cache:             ENABLED" -ForegroundColor Cyan }
else { Write-Host "  File Cache:             DISABLED" -ForegroundColor Gray }

if ($GraphReportCSV) { Write-Host "  Graph Report:           $GraphReportCSV" -ForegroundColor Cyan }

if ($ResetDatabase) { Write-Host "  Reset Database:         YES (clean start)" -ForegroundColor Red }

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Prompt for execution mode if not explicitly set
if (-not $SyncOnly -and -not $DeleteOnly -and -not $Unattended) {
    Write-Host "  ┌──────────────────────────────────────────────────────────────┐" -ForegroundColor Cyan
    Write-Host "  │  EXECUTION MODE                                              │" -ForegroundColor Cyan
    Write-Host "  │                                                              │" -ForegroundColor Cyan
    Write-Host "  │  [F] Full   - Sync + Delete in this session                  │" -ForegroundColor Green
    Write-Host "  │  [S] Sync   - Run SyncListPolicy only (collect data first)   │" -ForegroundColor Yellow
    Write-Host "  │  [D] Delete - Run BatchDelete only (sync was already done)   │" -ForegroundColor Yellow
    Write-Host "  └──────────────────────────────────────────────────────────────┘" -ForegroundColor Cyan
    Write-Host ""
    
    do {
        $modeChoice = Read-Host "  Choose execution mode (F/S/D)"
        if ([string]::IsNullOrWhiteSpace($modeChoice) -or ($modeChoice -notmatch '^[FfSsDd]$')) {
            Write-Host "  [!] Please enter F, S or D" -ForegroundColor Yellow
        }
    } while ([string]::IsNullOrWhiteSpace($modeChoice) -or ($modeChoice -notmatch '^[FfSsDd]$'))
    
    if ($modeChoice -match '^[Ss]$') {
        $SyncOnly = $true
        Write-Host "  [OK] Mode: SYNC ONLY" -ForegroundColor Green
    }
    elseif ($modeChoice -match '^[Dd]$') {
        $DeleteOnly = $true
        Write-Host "  [OK] Mode: DELETE ONLY" -ForegroundColor Green
    }
    else {
        Write-Host "  [OK] Mode: FULL (Sync + Delete)" -ForegroundColor Green
    }
    Write-Host ""
}

if (-not $Unattended) {
    do {
        $confirm = Read-Host "Do you want to start processing? (Y/N)"
        if ([string]::IsNullOrWhiteSpace($confirm) -or ($confirm -notmatch '^[YyNnSs]$')) {
            Write-Host "  [!] Please enter Y or N" -ForegroundColor Yellow
        }
    } while ([string]::IsNullOrWhiteSpace($confirm) -or ($confirm -notmatch '^[YyNnSs]$'))

    if ($confirm -notmatch '^[YySs]$') {
        Write-Host "Operation cancelled by user." -ForegroundColor Yellow
        exit 0
    }
}
else {
    Write-Host "[UNATTENDED] Starting processing automatically..." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Starting processing..." -ForegroundColor Green
Write-Host ""

# Save session
$sessionId = if ($useSessionConfig -and $resumeFromSession) { $resumeFromSession.SessionId } else { $currentSessionId }
try {
    $dashboardConfig = Get-DashboardConfig
    $zeroVersionAction = if ($dashboardConfig -and $dashboardConfig.ZeroVersionAction) { $dashboardConfig.ZeroVersionAction } else { "ask" }
    if ($Unattended -and $zeroVersionAction -eq "ask") { $zeroVersionAction = "skip" }

    Save-Session -SessionId $sessionId `
        -AdminUrl $AdminUrl `
        -MajorVersionLimit $MajorVersionLimit `
        -MajorWithMinorVersionsLimit $MajorWithMinorVersionsLimit `
        -MaxConcurrentJobs $MaxConcurrentJobs `
        -InputSiteListCSV $InputSiteListCSV `
        -InputExclusionSiteListCSV $InputExclusionSiteListCSV `
        -GraphReportCSV $GraphReportCSV `
        -ZeroVersionAction $zeroVersionAction `
        -DeleteOnly:$DeleteOnly `
        -SyncOnly:$SyncOnly `
        -CheckBatchSize $CheckBatchSize `
        -CheckBatchDelaySeconds $CheckBatchDelaySeconds `
        -Status "InProgress" | Out-Null

    Write-Host "  [OK] Session saved: $sessionId" -ForegroundColor Gray
}
catch { Write-Warning "Could not save session: $_" }

try {
    $orchestrationParams = @{
        MaxConcurrentJobs           = $MaxConcurrentJobs
        MajorVersionLimit           = $MajorVersionLimit
        MajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
        CheckBatchSize              = $CheckBatchSize
        CheckBatchDelaySeconds      = $CheckBatchDelaySeconds
    }
    if ($resumeExecution -or $useSessionConfig) { $orchestrationParams.Resume = $true }
    if ($UseFileCache) { $orchestrationParams.UseFileCache = $true }
    if ($DeleteOnly) { $orchestrationParams.DeleteOnly = $true }
    if ($SyncOnly) { $orchestrationParams.SyncOnly = $true }
    
    if ($ManageRetentionPolicy) {
        # Connect to IPPS using the separate Purview app registration
        # Credentials are loaded automatically from AppPaths.json PurviewApp section
        # or can be overridden via Connect-RetentionPolicyManager parameters
        Write-Host "  Connecting to Security & Compliance (Purview app)..." -ForegroundColor Yellow
        $ippsParams = @{
            LogPath = Join-Path $scriptPath "Logs"
        }
        Connect-RetentionPolicyManager @ippsParams
        $orchestrationParams.ManageRetentionPolicy = $true
    }

    Start-SPOVersionPolicyOrchestration @orchestrationParams

    Update-SessionProgress -SessionId $sessionId -Status "Completed" | Out-Null
}
catch {
    Update-SessionProgress -SessionId $sessionId -Status "Failed" | Out-Null
    Write-Error "Error during orchestration: $_"
    exit 1
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  PROCESSING COMPLETED!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
#endregion
