# Start-SPOVersionManagement.ps1
# Main script for SharePoint Online version management
# 
# Usage:
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com"
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -InputSiteListCSV "C:\sites.csv"
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -InputExclusionSiteListCSV "C:\exclude.csv"
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -GraphReportCSV "C:\SharePointSiteUsage.csv"
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -UseFileCache  # Uses AllSites.json instead of Get-SPOSite
#   .\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -CheckBatchSize 5 -CheckBatchDelaySeconds 3  # Throttle status checks

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$AdminUrl,
    
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
Write-Host "  Admin URL: $AdminUrl" -ForegroundColor Cyan
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
    
    do {
        $resetConfirm = Read-Host "  Are you sure you want to reset ALL data? (Y/N)"
        if ([string]::IsNullOrWhiteSpace($resetConfirm) -or ($resetConfirm -notmatch '^[YyNn]$')) {
            Write-Host "  [!] Please enter Y or N" -ForegroundColor Yellow
        }
    } while ([string]::IsNullOrWhiteSpace($resetConfirm) -or ($resetConfirm -notmatch '^[YyNn]$'))
    
    if ($resetConfirm -notmatch '^[Yy]$') {
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

# Import modules
Write-Host "[1/6] Importing modules..." -ForegroundColor Yellow
try {
    Import-Module "$scriptPath\SPOVersionManagement.psm1" -Force -DisableNameChecking -ErrorAction Stop
    Write-Host "  [OK] SPOVersionManagement.psm1 imported" -ForegroundColor Green
    
    Import-Module "$scriptPath\SPOSiteFilters.psm1" -Force  -DisableNameChecking  -ErrorAction Stop
    Write-Host "  [OK] SPOSiteFilters.psm1 imported" -ForegroundColor Green
    
    # Import retention policy manager module (optional)
    if ($ManageRetentionPolicy) {
        Import-Module "$scriptPath\SPORetentionPolicyManager.psm1" -Force -DisableNameChecking -ErrorAction Stop
        Write-Host "  [OK] SPORetentionPolicyManager.psm1 imported" -ForegroundColor Green
    }
    
    # Load centralized paths from module
    $appPaths = Get-SPOAppPaths
    Write-Host "  [OK] Path configuration loaded" -ForegroundColor Green
}
catch {
    Write-Error "Error importing modules: $_"
    exit 1
}

# Check for pending sessions
$pendingSessions = Get-PendingSessions
$resumeFromSession = $null
$useSessionConfig = $false

if ($pendingSessions -and $pendingSessions.Count -gt 0) {
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Yellow
    Write-Host "  PENDING SESSION(S) DETECTED" -ForegroundColor Yellow
    Write-Host "================================================================" -ForegroundColor Yellow
    Write-Host ""
    
    $sessionIndex = 1
    foreach ($session in $pendingSessions) {
        $startTime = if ($session.StartedAt) { [DateTime]::Parse($session.StartedAt).ToString("dd/MM/yyyy HH:mm") } else { "Unknown" }
        $lastUpdate = if ($session.LastUpdated) { [DateTime]::Parse($session.LastUpdated).ToString("dd/MM/yyyy HH:mm") } else { "Unknown" }
        $progress = if ($session.Progress) { "$($session.Progress.ProcessedSites)/$($session.Progress.TotalSites) sites" } else { "Unknown" }
        
        Write-Host "  [$sessionIndex] Session: $($session.SessionId)" -ForegroundColor Cyan
        Write-Host "      Started:    $startTime" -ForegroundColor Gray
        Write-Host "      Last Update: $lastUpdate" -ForegroundColor Gray
        Write-Host "      Status:     $($session.Status)" -ForegroundColor $(if ($session.Status -eq "InProgress") { "Yellow" } else { "Gray" })
        Write-Host "      Progress:   $progress" -ForegroundColor Gray
        Write-Host "      Admin URL:  $($session.AdminUrl)" -ForegroundColor Gray
        
        if ($session.Configuration) {
            Write-Host "      Config:" -ForegroundColor Gray
            Write-Host "        - Major Version Limit: $($session.Configuration.MajorVersionLimit)" -ForegroundColor DarkGray
            Write-Host "        - Max Concurrent Jobs: $($session.Configuration.MaxConcurrentJobs)" -ForegroundColor DarkGray
            if ($session.Configuration.InputSiteListCSV) {
                Write-Host "        - Input Sites CSV: $($session.Configuration.InputSiteListCSV)" -ForegroundColor DarkGray
            }
        }
        Write-Host ""
        $sessionIndex++
    }
    
    Write-Host "  Options:" -ForegroundColor White
    Write-Host "  [C] Continue the most recent pending session" -ForegroundColor Green
    Write-Host "  [N] Start a NEW session (ignore pending)" -ForegroundColor Yellow
    Write-Host "  [X] Cancel all pending sessions and start new" -ForegroundColor Red
    Write-Host ""
    
    # Loop until valid answer
    do {
        $sessionChoice = Read-Host "  Choose an option (C/N/X)"
        if ([string]::IsNullOrWhiteSpace($sessionChoice) -or ($sessionChoice -notmatch '^[CcNnXx]$')) {
            Write-Host "  [!] Please enter C, N or X" -ForegroundColor Yellow
        }
    } while ([string]::IsNullOrWhiteSpace($sessionChoice) -or ($sessionChoice -notmatch '^[CcNnXx]$'))
    
    if ($sessionChoice -eq 'C' -or $sessionChoice -eq 'c') {
        # Get most recent pending session
        $resumeFromSession = $pendingSessions | Sort-Object { [DateTime]::Parse($_.LastUpdated) } -Descending | Select-Object -First 1
        $useSessionConfig = $true
        
        Write-Host ""
        Write-Host "  [OK] Resuming session: $($resumeFromSession.SessionId)" -ForegroundColor Green
        
        # Override parameters with session configuration
        if ($resumeFromSession.Configuration) {
            $MajorVersionLimit = $resumeFromSession.Configuration.MajorVersionLimit
            $MajorWithMinorVersionsLimit = $resumeFromSession.Configuration.MajorWithMinorVersionsLimit
            $MaxConcurrentJobs = $resumeFromSession.Configuration.MaxConcurrentJobs
            
            if ($resumeFromSession.Configuration.InputSiteListCSV) {
                $InputSiteListCSV = $resumeFromSession.Configuration.InputSiteListCSV
            }
            if ($resumeFromSession.Configuration.InputExclusionSiteListCSV) {
                $InputExclusionSiteListCSV = $resumeFromSession.Configuration.InputExclusionSiteListCSV
            }
            if ($resumeFromSession.Configuration.GraphReportCSV) {
                $GraphReportCSV = $resumeFromSession.Configuration.GraphReportCSV
            }
            
            # Update AdminUrl if different
            if ($resumeFromSession.AdminUrl -and $resumeFromSession.AdminUrl -ne $AdminUrl) {
                Write-Host "  [INFO] Using session AdminUrl: $($resumeFromSession.AdminUrl)" -ForegroundColor Cyan
                $AdminUrl = $resumeFromSession.AdminUrl
            }
        }
        
        Write-Host "  Configuration restored from session" -ForegroundColor Green
    }
    elseif ($sessionChoice -eq 'X' -or $sessionChoice -eq 'x') {
        # Cancel all pending sessions
        foreach ($session in $pendingSessions) {
            Update-SessionProgress -SessionId $session.SessionId -Status "Cancelled"
        }
        Write-Host ""
        Write-Host "  [OK] All pending sessions cancelled" -ForegroundColor Yellow
    }
    else {
        Write-Host ""
        Write-Host "  [OK] Starting new session (pending sessions will remain)" -ForegroundColor Yellow
    }
}

# Generate new session ID for this execution
$currentSessionId = (Get-Date -Format 'yyyyMMdd_HHmmss') + "_" + [guid]::NewGuid().ToString().Substring(0,8)

# Connect to SPO Admin
Write-Host ""
Write-Host "[2/6] Connecting to SharePoint Online Admin..." -ForegroundColor Yellow
try {
    # Build connection parameters
    $connectParams = @{
        AdminUrl = $AdminUrl
        MaxConcurrentJobs = $MaxConcurrentJobs
    }
    
    # Skip Graph connection if user provided manual CSV or explicitly requested
    if ($SkipGraphConnection -or $GraphReportCSV) {
        $connectParams.SkipGraphConnection = $true
        
        if ($GraphReportCSV) {
            Write-Host "  (Graph connection skipped - using manual CSV report)" -ForegroundColor Gray
            $connectParams.GraphReportCSV = $GraphReportCSV
        }
    }
    
    # Pass UseFileCache to connection if specified
    if ($UseFileCache) {
        $connectParams.UseFileCache = $true
    }
    
    Connect-SPOVersionManagement @connectParams
}
catch {
    Write-Error "Error connecting: $_"
    exit 1
}

#region Tenant mismatch detection
$configPath = Join-Path $scriptPath "config"
$sessionHistoryPath = Join-Path $configPath "SessionHistory.json"
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
    catch {
        Write-Warning "Could not check session history for tenant mismatch: $_"
    }
}
#endregion

# Configure INCLUSION list (sites to process)
Write-Host ""
Write-Host "[3/6] Configuring site filters..." -ForegroundColor Yellow

if ($InputSiteListCSV) {
    if (Test-Path $InputSiteListCSV) {
        Write-Host "  Loading INCLUSION list: $InputSiteListCSV" -ForegroundColor Cyan
        try {
            $includedSites = Set-SiteInclusionList -CsvPath $InputSiteListCSV
            Write-Host "  [OK] $($includedSites.Count) sites configured for processing" -ForegroundColor Green
        }
        catch {
            Write-Warning "Error loading inclusion list: $_"
        }
    } else {
        Write-Warning "Inclusion file not found: $InputSiteListCSV"
    }
} else {
    Write-Host "  Inclusion list: NOT CONFIGURED (process all sites)" -ForegroundColor Gray
}

# Configure EXCLUSION list (sites NOT to process)
if ($InputExclusionSiteListCSV) {
    if (Test-Path $InputExclusionSiteListCSV) {
        Write-Host "  Loading EXCLUSION list: $InputExclusionSiteListCSV" -ForegroundColor Yellow
        
        # Check if there's already a saved exclusion list
        $excludedSitesFile = $appPaths.ExcludedSitesFile
        $existingExclusion = @()
        $hasExistingList = $false
        
        if (Test-Path $excludedSitesFile) {
            try {
                $savedExclusion = Get-Content $excludedSitesFile -Raw | ConvertFrom-Json
                if ($savedExclusion.Count -gt 0 -and $savedExclusion.Sites -and $savedExclusion.Sites.Count -gt 0) {
                    $hasExistingList = $true
                    $existingExclusion = @($savedExclusion.Sites | ForEach-Object { if ($_.Url) { $_.Url } else { $_ } })
                    
                    Write-Host ""
                    Write-Host "  ================================================================" -ForegroundColor Cyan
                    Write-Host "  EXISTING EXCLUSION LIST DETECTED" -ForegroundColor Cyan
                    Write-Host "  ================================================================" -ForegroundColor Cyan
                    Write-Host "  There are $($savedExclusion.Count) sites already configured for exclusion." -ForegroundColor Cyan
                    Write-Host ""
                    Write-Host "  Options:" -ForegroundColor White
                    Write-Host "  [O] Overwrite (replace existing list with CSV)" -ForegroundColor Yellow
                    Write-Host "  [M] Merge (add only new sites)" -ForegroundColor Green
                    Write-Host "  [C] Cancel (keep existing list, ignore CSV)" -ForegroundColor Gray
                    Write-Host ""
                    
                    # Loop until valid answer
                    do {
                        $mergeOption = Read-Host "  Choose an option (O/M/C)"
                        if ([string]::IsNullOrWhiteSpace($mergeOption) -or ($mergeOption -notmatch '^[OoMmCc]$')) {
                            Write-Host "  [!] Please enter O, M or C" -ForegroundColor Yellow
                        }
                    } while ([string]::IsNullOrWhiteSpace($mergeOption) -or ($mergeOption -notmatch '^[OoMmCc]$'))
                    
                    if ($mergeOption -eq 'C' -or $mergeOption -eq 'c') {
                        Write-Host ""
                        Write-Host "  [OK] Operation cancelled. Existing list kept." -ForegroundColor Gray
                        Write-Host "  $($savedExclusion.Count) sites remain in the exclusion list." -ForegroundColor Yellow
                    }
                    elseif ($mergeOption -eq 'M' -or $mergeOption -eq 'm') {
                        # Load sites from CSV
                        $csvSites = @(Import-SiteListFromCSV -CsvPath $InputExclusionSiteListCSV)
                        
                        # Normalize existing URLs for comparison
                        $existingNormalized = @($existingExclusion | ForEach-Object { $_.TrimEnd("/").ToLower() })
                        
                        # Identify new sites (delta)
                        $newSites = @()
                        $duplicateCount = 0
                        foreach ($site in $csvSites) {
                            $siteNormalized = $site.TrimEnd("/").ToLower()
                            if ($existingNormalized -notcontains $siteNormalized) {
                                $newSites += $site
                            } else {
                                $duplicateCount++
                            }
                        }
                        
                        if ($newSites.Count -eq 0) {
                            Write-Host ""
                            Write-Host "  [INFO] No new sites found in CSV." -ForegroundColor Cyan
                            Write-Host "  All $duplicateCount sites from CSV already exist in the list." -ForegroundColor Cyan
                            Write-Host "  Existing list kept with $($savedExclusion.Count) sites." -ForegroundColor Yellow
                        } else {
                            Write-Host ""
                            Write-Host "  CSV Analysis:" -ForegroundColor Cyan
                            Write-Host "  - Sites in CSV: $($csvSites.Count)" -ForegroundColor White
                            Write-Host "  - Already existing (ignored): $duplicateCount" -ForegroundColor Gray
                            Write-Host "  - New sites to add: $($newSites.Count)" -ForegroundColor Green
                            Write-Host ""
                            
                            # Merge: existing + new
                            $mergedSites = $existingExclusion + $newSites
                            $excludedSites = Set-SiteExclusionListFromArray -Sites $mergedSites
                            
                            Write-Host "  [OK] List merged successfully!" -ForegroundColor Green
                            Write-Host "  Total sites in exclusion list: $($excludedSites.Count)" -ForegroundColor Yellow
                        }
                    }
                    else {
                        # Overwrite (default)
                        Write-Host ""
                        Write-Host "  Overwriting existing list..." -ForegroundColor Yellow
                        try {
                            $excludedSites = Set-SiteExclusionList -CsvPath $InputExclusionSiteListCSV
                            Write-Host "  [OK] List replaced. $($excludedSites.Count) sites configured for EXCLUSION." -ForegroundColor Yellow
                        }
                        catch {
                            Write-Warning "Error loading exclusion list: $_"
                        }
                    }
                }
            }
            catch {
                Write-Host "  [WARNING] Error reading existing list. It will be overwritten." -ForegroundColor DarkYellow
                $hasExistingList = $false
            }
        }
        
        # If there was no existing list, simply load the CSV
        if (-not $hasExistingList) {
            try {
                $excludedSites = Set-SiteExclusionList -CsvPath $InputExclusionSiteListCSV
                Write-Host "  [OK] $($excludedSites.Count) sites configured for EXCLUSION (will not be processed)" -ForegroundColor Yellow
            }
            catch {
                Write-Warning "Error loading exclusion list: $_"
            }
        }
        
        Write-Host ""
        Write-Host "  ================================================================" -ForegroundColor Yellow
        Write-Host "  WARNING: Sites in the exclusion list will NOT have" -ForegroundColor Yellow
        Write-Host "  versions deleted. They will be skipped in the process." -ForegroundColor Yellow
        Write-Host "  ================================================================" -ForegroundColor Yellow
    } else {
        Write-Warning "Exclusion file not found: $InputExclusionSiteListCSV"
    }
} else {
    # Check if there's a previously saved exclusion list
    $excludedSitesFile = $appPaths.ExcludedSitesFile
    if (Test-Path $excludedSitesFile) {
        try {
            $savedExclusion = Get-Content $excludedSitesFile -Raw | ConvertFrom-Json
            $savedCount = $savedExclusion.Count
            $savedSites = $savedExclusion.Sites
            
            if ($savedCount -gt 0 -and $savedSites -and $savedSites.Count -gt 0) {
                Write-Host ""
                Write-Host "  ================================================================" -ForegroundColor Yellow
                Write-Host "  EXISTING EXCLUSION LIST DETECTED" -ForegroundColor Yellow
                Write-Host "  ================================================================" -ForegroundColor Yellow
                Write-Host "  There are $savedCount sites configured for EXCLUSION." -ForegroundColor Yellow
                Write-Host "  These sites will NOT have versions deleted." -ForegroundColor Yellow
                Write-Host ""
                
                $viewList = Read-Host "  Do you want to see the list of excluded sites? (Y/N)"
                
                if ($viewList -eq 'Y' -or $viewList -eq 'y' -or $viewList -eq 'S' -or $viewList -eq 's') {
                    Write-Host ""
                    Write-Host "  Sites in exclusion list:" -ForegroundColor Cyan
                    Write-Host "  ----------------------------" -ForegroundColor Cyan
                    $index = 1
                    foreach ($site in $savedSites) {
                        $siteUrl = if ($site.Url) { $site.Url } else { $site }
                        $siteName = if ($site.SiteName) { $site.SiteName } else { "" }
                        $reason = if ($site.Reason) { " - Reason: $($site.Reason)" } else { "" }
                        
                        if ($siteName) {
                            Write-Host "  $index. $siteName" -ForegroundColor White
                            Write-Host "     $siteUrl$reason" -ForegroundColor Gray
                        } else {
                            Write-Host "  $index. $siteUrl$reason" -ForegroundColor White
                        }
                        $index++
                    }
                    Write-Host ""
                }
                
                Write-Host "  Options:" -ForegroundColor Cyan
                Write-Host "  [K] Keep current exclusion list" -ForegroundColor Green
                Write-Host "  [C] Clear list (process ALL sites)" -ForegroundColor Red
                Write-Host ""
                
                # Loop until valid answer
                do {
                    $keepList = Read-Host "  Choose an option (K/C)"
                    if ([string]::IsNullOrWhiteSpace($keepList) -or ($keepList -notmatch '^[KkCc]$')) {
                        Write-Host "  [!] Please enter K or C" -ForegroundColor Yellow
                    }
                } while ([string]::IsNullOrWhiteSpace($keepList) -or ($keepList -notmatch '^[KkCc]$'))
                
                if ($keepList -eq 'C' -or $keepList -eq 'c') {
                    Write-Host ""
                    Write-Host "  Clearing exclusion list..." -ForegroundColor Yellow
                    Clear-SiteExclusionList
                    Write-Host "  [OK] Exclusion list cleared. All sites will be processed." -ForegroundColor Green
                } else {
                    Write-Host ""
                    Write-Host "  [OK] Exclusion list KEPT. $savedCount sites will be PROTECTED." -ForegroundColor Yellow
                    # Load list into module for processing
                    $excludedUrls = @()
                    foreach ($site in $savedSites) {
                        $siteUrl = if ($site.Url) { $site.Url } else { $site }
                        $excludedUrls += $siteUrl
                    }
                    Set-SiteExclusionListFromArray -SiteUrls $excludedUrls
                }
            } else {
                Write-Host "  Exclusion list: NOT CONFIGURED" -ForegroundColor Gray
            }
        }
        catch {
            Write-Host "  Exclusion list: NOT CONFIGURED (error reading file: $_)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  Exclusion list: NOT CONFIGURED" -ForegroundColor Gray
    }
}

# Update tenant status BEFORE starting the processing
Write-Host ""
Write-Host "[4/6] Getting tenant status..." -ForegroundColor Yellow
try {
    # Build parameters for Update-TenantStorageStatus
    $updateParams = @{}
    
    # If UseFileCache, skip Get-SPOSite and use AllSites.json
    if ($UseFileCache) {
        Write-Host "  Using file cache (AllSites.json) - skipping Get-SPOSite..." -ForegroundColor Cyan
        $updateParams.UseFileCache = $true
    }
    
    # Check if user provided a Graph report CSV manually
    if ($GraphReportCSV) {
        if (Test-Path $GraphReportCSV) {
            Write-Host "  Using manual Graph report: $GraphReportCSV" -ForegroundColor Cyan
            Write-Host "  (Graph API connection will be skipped)" -ForegroundColor Gray
            $updateParams.GraphReportCSV = $GraphReportCSV
        } else {
            Write-Warning "Graph report file not found: $GraphReportCSV"
        }
    }
    # Use Graph API if connected and no manual CSV
    elseif (-not $SkipGraphConnection -and -not $UseFileCache) {
        Write-Host "  Including Graph API consumption history (D180)..." -ForegroundColor Cyan
        $updateParams.IncludeGraphHistory = $true
    }
    
    # Call Update-TenantStorageStatus with all parameters
    $tenantStorage = Update-TenantStorageStatus @updateParams
    
    if ($tenantStorage) {
        $percentUsed = $tenantStorage.PercentUsed
        $status = $tenantStorage.StorageStatus
        $storageMsg = '  Storage: {0} GB / {1} GB ({2}%)' -f $tenantStorage.StorageUsedGB, $tenantStorage.TenantQuotaGB, $percentUsed
        $storageColor = if ($status -eq 'Critical') { 'Red' } elseif ($status -eq 'Warning') { 'Yellow' } else { 'Green' }
        Write-Host '  [OK] Tenant status updated' -ForegroundColor Green
        Write-Host $storageMsg -ForegroundColor $storageColor
        Write-Host "  Total sites: $($tenantStorage.SiteCount)" -ForegroundColor Gray
        
        if ($tenantStorage.HasTrendData) {
            Write-Host "  [OK] Trend data included - 180 days history" -ForegroundColor Green
            if ($tenantStorage.GraphData -and $tenantStorage.GraphData.MonthlyData) {
                $monthCount = $tenantStorage.GraphData.MonthlyData.Count
                $avgGrowth = $tenantStorage.GraphData.AvgMonthlyGrowthGB
                Write-Host "    Months analyzed: $monthCount | Average growth: $avgGrowth GB/month" -ForegroundColor Cyan
            }
        } else {
            Write-Host "  [!] Trend data not available" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Warning "Error getting tenant status: $_"
}

# Export all sites data for Dashboard
$allSitesFile = $appPaths.AllSitesFile
$useCache = $false
$loadFullTenantWithInputSites = $false

# If UseFileCache is set, skip all prompts and use existing AllSites.json
if ($UseFileCache) {
    Write-Host ""
    Write-Host "  [FILE CACHE] Using existing AllSites.json - skipping site list update" -ForegroundColor Cyan
    $useCache = $true
}
# If InputSiteListCSV is provided, ask user if they want to load all tenant sites for accurate counts
elseif ($InputSiteListCSV -and $includedSites -and $includedSites.Count -gt 0) {
    Write-Host ""
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "  INPUT SITES LIST CONFIGURATION" -ForegroundColor Cyan
    Write-Host "  ================================================================" -ForegroundColor Cyan
    Write-Host "  Sites selected for processing: $($includedSites.Count)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "  Do you want to load ALL tenant sites for accurate Dashboard counts?" -ForegroundColor White
    Write-Host "  (Only the selected sites will be processed, but Dashboard will show all)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  [Y] Yes - Load all tenant sites (recommended for accurate metrics)" -ForegroundColor Green
    Write-Host "  [N] No  - Only load/show the selected sites" -ForegroundColor Yellow
    Write-Host ""
    
    # Loop until valid answer
    do {
        $loadAllOption = Read-Host "  Choose an option (Y/N)"
        if ([string]::IsNullOrWhiteSpace($loadAllOption) -or ($loadAllOption -notmatch '^[YyNnSs]$')) {
            Write-Host "  [!] Please enter Y or N" -ForegroundColor Yellow
        }
    } while ([string]::IsNullOrWhiteSpace($loadAllOption) -or ($loadAllOption -notmatch '^[YyNnSs]$'))
    
    if ($loadAllOption -eq 'Y' -or $loadAllOption -eq 'y' -or $loadAllOption -eq 'S' -or $loadAllOption -eq 's') {
        $loadFullTenantWithInputSites = $true
        Write-Host ""
        Write-Host "  [OK] Will load all tenant sites and mark selected ones as processing targets" -ForegroundColor Green
    } else {
        Write-Host ""
        Write-Host "  [OK] Will only load the selected sites" -ForegroundColor Yellow
    }
}

# Only show cache options if UseFileCache was not specified (already using cache)
if (-not $UseFileCache -and (Test-Path $allSitesFile)) {
    try {
        $cachedData = Get-Content $allSitesFile -Raw | ConvertFrom-Json
        $cachedSiteCount = if ($cachedData.Sites) { $cachedData.Sites.Count } else { 0 }
        $cachedDate = if ($cachedData.ExportedAt) { $cachedData.ExportedAt } else { "unknown" }
        
        if ($cachedSiteCount -gt 0) {
            Write-Host ""
            Write-Host "  ================================================================" -ForegroundColor Cyan
            Write-Host "  CACHED SITE LIST DETECTED" -ForegroundColor Cyan
            Write-Host "  ================================================================" -ForegroundColor Cyan
            Write-Host "  Sites in cache: $cachedSiteCount" -ForegroundColor Cyan
            Write-Host "  Cache date:     $cachedDate" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "  Options:" -ForegroundColor White
            Write-Host "  [C] Use CACHED data (faster)" -ForegroundColor Green
            Write-Host "  [U] UPDATE base (insert new and update existing)" -ForegroundColor Yellow
            Write-Host ""
            
            # Loop until valid answer
            do {
                $cacheOption = Read-Host "  Choose an option (C/U)"
                if ([string]::IsNullOrWhiteSpace($cacheOption) -or ($cacheOption -notmatch '^[CcUu]$')) {
                    Write-Host "  [!] Please enter C or U" -ForegroundColor Yellow
                }
            } while ([string]::IsNullOrWhiteSpace($cacheOption) -or ($cacheOption -notmatch '^[CcUu]$'))
            
            if ($cacheOption -eq 'C' -or $cacheOption -eq 'c') {
                $useCache = $true
                Write-Host ""
                $cacheMsg = '  [OK] Using cached data ({0} sites)' -f $cachedSiteCount
                Write-Host $cacheMsg -ForegroundColor Green
            } else {
                Write-Host ""
                Write-Host "  Updating site base (upsert)..." -ForegroundColor Cyan
            }
        }
    }
    catch {
        Write-Host "  [WARNING] Error reading cache. It will be updated." -ForegroundColor DarkYellow
    }
}

if (-not $useCache) {
    Write-Host "  Exporting site list for Dashboard..." -ForegroundColor Cyan
    try {
        if ($loadFullTenantWithInputSites) {
            # Load all tenant sites but mark InputSites as ProcessingTarget
            Export-AllSitesDataForDashboard -Upsert -MarkProcessingTargets
        } else {
            Export-AllSitesDataForDashboard -Upsert
        }
        Write-Host "  [OK] Site list exported (AllSites.json)" -ForegroundColor Green
    }
    catch {
        Write-Warning "Error exporting site list: $_"
    }
}

# Check if there are active jobs from a previous execution
$jobStatusFile = $appPaths.JobStatusFile
$resumeExecution = $false

if (Test-Path $jobStatusFile) {
    try {
        $existingStatus = Get-Content $jobStatusFile -Raw | ConvertFrom-Json
        $activeJobsCount = if ($existingStatus.ActiveJobs) { $existingStatus.ActiveJobs.Count } else { 0 }
        $queuedSitesCount = if ($existingStatus.QueuedSitesCount) { $existingStatus.QueuedSitesCount } else { 0 }
        $completedCount = if ($existingStatus.CompletedJobsCount) { $existingStatus.CompletedJobsCount } else { 0 }
        
        # Check if previous execution was completed
        if ($activeJobsCount -eq 0 -and $queuedSitesCount -eq 0 -and $completedCount -gt 0) {
            Write-Host ""
            Write-Host "================================================================" -ForegroundColor Green
            Write-Host "  PREVIOUS EXECUTION COMPLETED" -ForegroundColor Green
            Write-Host "================================================================" -ForegroundColor Green
            Write-Host "  Jobs completed: $completedCount" -ForegroundColor Green
            if ($existingStatus.LastUpdated) {
                Write-Host "  Last update: $($existingStatus.LastUpdated)" -ForegroundColor Gray
            }
            Write-Host ""
            Write-Host "  The previous execution finished successfully." -ForegroundColor Green
            Write-Host "  Starting new execution..." -ForegroundColor Cyan
            Write-Host ""
        }
        elseif ($activeJobsCount -gt 0 -or $queuedSitesCount -gt 0) {
            Write-Host ""
            Write-Host "================================================================" -ForegroundColor Yellow
            Write-Host "  PREVIOUS EXECUTION DETECTED" -ForegroundColor Yellow
            Write-Host "================================================================" -ForegroundColor Yellow
            Write-Host "  Active jobs:    $activeJobsCount" -ForegroundColor Cyan
            Write-Host "  Sites in queue: $queuedSitesCount" -ForegroundColor Cyan
            Write-Host "  Jobs completed: $completedCount" -ForegroundColor Cyan
            if ($existingStatus.LastUpdated) {
                Write-Host "  Last update: $($existingStatus.LastUpdated)" -ForegroundColor Gray
            }
            Write-Host ""
            Write-Host "  Options:" -ForegroundColor White
            Write-Host "  [C] Continue previous execution (resume from where it stopped)" -ForegroundColor Green
            Write-Host "  [R] Restart from scratch (discard progress)" -ForegroundColor Yellow
            Write-Host ""
            
            # Loop until valid answer
            do {
                $resumeOption = Read-Host "  Choose an option (C/R)"
                if ([string]::IsNullOrWhiteSpace($resumeOption) -or ($resumeOption -notmatch '^[CcRr]$')) {
                    Write-Host "  [!] Please enter C or R" -ForegroundColor Yellow
                }
            } while ([string]::IsNullOrWhiteSpace($resumeOption) -or ($resumeOption -notmatch '^[CcRr]$'))
            
            if ($resumeOption -eq 'C' -or $resumeOption -eq 'c') {
                Write-Host ""
                Write-Host "  [OK] Continuing previous execution..." -ForegroundColor Green
                $resumeExecution = $true
            } else {
                Write-Host ""
                Write-Host "  [OK] Restarting from scratch..." -ForegroundColor Yellow
            }
        }
    }
    catch {
        Write-Host "  [WARNING] Error reading previous status: $_" -ForegroundColor DarkYellow
    }
}

# Initialize job status file (only if NOT resuming)
if (-not $resumeExecution) {
    @{
        LastUpdated = (Get-Date).ToString("o")
        ActiveJobs = @()
        QueuedSites = @()
        QueuedSitesCount = 0
        QueuedSitesSyncCount = 0
        QueuedSitesDeleteCount = 0
        RecentCompletedJobs = @()
        CompletedJobsCount = 0
        MajorVersionLimit = $MajorVersionLimit
        MajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
    } | ConvertTo-Json -Depth 10 | Set-Content -Path $jobStatusFile -Encoding UTF8
    Write-Host "  [OK] Job status initialized" -ForegroundColor Green
} else {
    Write-Host "  [OK] Job status kept (resuming)" -ForegroundColor Green
}

# Open Dashboard
Write-Host ""
Write-Host "[5/6] Dashboard..." -ForegroundColor Yellow
$dashboardPath = "$scriptPath\web\Dashboard.html"
if (Test-Path $dashboardPath) {
    Write-Host "  Dashboard available at: $dashboardPath" -ForegroundColor Cyan
    if ($OpenDashboard) {
        Start-Process $dashboardPath
        Write-Host "  [OK] Dashboard opened in browser" -ForegroundColor Green
    } else {
        Write-Host "  Use -OpenDashboard to open automatically" -ForegroundColor Gray
    }
} else {
    Write-Warning "Dashboard not found: $dashboardPath"
}

# Start orchestration
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
Write-Host "[6/6] Starting orchestration..." -ForegroundColor Yellow
Write-Host ""

# Configuration summary
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  CONFIGURATION SUMMARY" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Admin URL:              $AdminUrl" -ForegroundColor Cyan
Write-Host "  Max Jobs:               $MaxConcurrentJobs" -ForegroundColor Cyan
Write-Host "  Major Version Limit:    $MajorVersionLimit" -ForegroundColor Cyan
Write-Host "  Minor Version Limit:    $MajorWithMinorVersionsLimit" -ForegroundColor Cyan

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
} else {
    Write-Host "  Sites Included:         ALL" -ForegroundColor Gray
}

if ($InputExclusionSiteListCSV) {
    $excCount = (Get-SiteExclusionList).Count
    Write-Host "  Sites Excluded:         $excCount sites (protected)" -ForegroundColor Yellow
} else {
    Write-Host "  Sites Excluded:         NONE" -ForegroundColor Gray
}

if ($UseFileCache) {
    Write-Host "  File Cache:             ENABLED (using AllSites.json)" -ForegroundColor Cyan
} else {
    Write-Host "  File Cache:             DISABLED (using Get-SPOSite)" -ForegroundColor Gray
}

if ($ResetDatabase) { Write-Host "  Reset Database:         YES (clean start)" -ForegroundColor Red }

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Prompt for execution mode if not explicitly set
if (-not $SyncOnly -and -not $DeleteOnly) {
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

# Confirm before starting - loop until valid answer
do {
    $confirm = Read-Host "Do you want to start processing? (Y/N)"
    if ([string]::IsNullOrWhiteSpace($confirm) -or ($confirm -notmatch '^[YyNnSs]$')) {
        Write-Host "  [!] Please enter Y or N" -ForegroundColor Yellow
    }
} while ([string]::IsNullOrWhiteSpace($confirm) -or ($confirm -notmatch '^[YyNnSs]$'))

if ($confirm -ne 'Y' -and $confirm -ne 'y' -and $confirm -ne 'S' -and $confirm -ne 's') {
    Write-Host "Operation cancelled by user." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Starting processing..." -ForegroundColor Green
Write-Host ""

# Save session before starting
$sessionId = if ($useSessionConfig -and $resumeFromSession) { $resumeFromSession.SessionId } else { $currentSessionId }
try {
    $dashboardConfig = Get-DashboardConfig
    $zeroVersionAction = if ($dashboardConfig -and $dashboardConfig.ZeroVersionAction) { $dashboardConfig.ZeroVersionAction } else { "ask" }
    
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
catch {
    Write-Warning "Could not save session: $_"
}

try {
    # Build orchestration parameters
    $orchestrationParams = @{
        MaxConcurrentJobs = $MaxConcurrentJobs
        MajorVersionLimit = $MajorVersionLimit
        MajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
        CheckBatchSize = $CheckBatchSize
        CheckBatchDelaySeconds = $CheckBatchDelaySeconds
    }
    
    if ($resumeExecution -or $useSessionConfig) {
        $orchestrationParams.Resume = $true
    }
    
    if ($UseFileCache) {
        $orchestrationParams.UseFileCache = $true
    }
    
    if ($DeleteOnly) {
        $orchestrationParams.DeleteOnly = $true
    }
    
    if ($SyncOnly) {
        $orchestrationParams.SyncOnly = $true
    }
    
    if ($ManageRetentionPolicy) {
        Write-Host ""
        Write-Host "  ================================================================" -ForegroundColor Magenta
        Write-Host "  RETENTION POLICY MANAGEMENT" -ForegroundColor Magenta
        Write-Host "  ================================================================" -ForegroundColor Magenta
        Write-Host "  Flow: Suspend retention → Delete versions → Resume retention" -ForegroundColor Magenta
        Write-Host "  ================================================================" -ForegroundColor Magenta
        
        $retentionConnectParams = @{
            LogPath = Join-Path $scriptPath "config"
        }
        
        # Check if PurviewApp is configured in AppPaths.json
        $purviewConfigured = $false
        $purviewAppPathsFile = Join-Path $scriptPath "config\AppPaths.json"
        if (Test-Path $purviewAppPathsFile) {
            try {
                $purviewAppPaths = Get-Content $purviewAppPathsFile -Raw | ConvertFrom-Json
                if ($purviewAppPaths.PurviewApp -and $purviewAppPaths.PurviewApp.ClientId -and $purviewAppPaths.PurviewApp.CertificateThumbprint -and $purviewAppPaths.PurviewApp.Organization) {
                    $purviewConfigured = $true
                }
            } catch { }
        }
        
        if ($purviewConfigured) {
            Write-Host ""
            Write-Host "  Purview App registration found in AppPaths.json" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "  Authentication method:" -ForegroundColor White
            Write-Host "  [A] App-based (Entra ID certificate - recommended for unattended)" -ForegroundColor Green
            Write-Host "  [I] Interactive login (browser prompt)" -ForegroundColor Yellow
            Write-Host ""
            
            do {
                $authChoice = Read-Host "  Choose authentication method (A/I)"
                if ([string]::IsNullOrWhiteSpace($authChoice) -or ($authChoice -notmatch '^[AaIi]$')) {
                    Write-Host "  [!] Please enter A or I" -ForegroundColor Yellow
                }
            } while ([string]::IsNullOrWhiteSpace($authChoice) -or ($authChoice -notmatch '^[AaIi]$'))
            
            if ($authChoice -eq 'I' -or $authChoice -eq 'i') {
                Write-Host "  Using interactive login..." -ForegroundColor Yellow
                $retentionConnectParams.UseAppPaths = $false
            } else {
                Write-Host "  Using Purview app authentication..." -ForegroundColor Green
            }
        } else {
            Write-Host ""
            Write-Host "  No Purview App configured in AppPaths.json" -ForegroundColor Yellow
            Write-Host "  Using interactive login (browser prompt)..." -ForegroundColor Yellow
            $retentionConnectParams.UseAppPaths = $false
        }
        
        Connect-RetentionPolicyManager @retentionConnectParams
        $orchestrationParams.ManageRetentionPolicy = $true
    }
    
    Start-SPOVersionPolicyOrchestration @orchestrationParams
    
    # Mark session as completed
    Update-SessionProgress -SessionId $sessionId -Status "Completed" | Out-Null
}
catch {
    # Mark session as failed
    Update-SessionProgress -SessionId $sessionId -Status "Failed" | Out-Null
    Write-Error "Error during orchestration: $_"
    exit 1
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  PROCESSING COMPLETED!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
