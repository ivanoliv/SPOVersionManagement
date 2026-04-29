<#
.SYNOPSIS
    Reset SPO Version Management to default/deploy state.

.DESCRIPTION
    Clears all execution data, temporary files, and resets to deploy state.
    Keeps essential files and resets JSON/CSV files to default structure.

.PARAMETER KeepInputFiles
    Keep existing data in IncludeSites.csv and ExcludeSites.csv (default: reset to header only)

.PARAMETER Force
    Skip confirmation prompt

.EXAMPLE
    .\Reset-SPOVersionManagement.ps1
    Resets all data files with confirmation prompt.

.EXAMPLE
    .\Reset-SPOVersionManagement.ps1 -Force
    Resets everything without confirmation.

.EXAMPLE
    .\Reset-SPOVersionManagement.ps1 -KeepInputFiles
    Resets but keeps the existing inclusion/exclusion CSV data.
#>

param(
    [switch]$KeepInputFiles = $false,
    [switch]$Force = $false
)

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$logsPath = Join-Path $scriptPath "Logs"
$configPath = Join-Path $scriptPath "config"
$webPath = Join-Path $scriptPath "web"
$backupPath = Join-Path $logsPath "Backup"

# Define essential files to KEEP
$essentialRootFiles = @(
    "SPOVersionManagement.psm1",
    "SPOSiteFilters.psm1",
    "Start-SPOVersionManagement.ps1",
    "Start-Dashboard.ps1",
    "Reset-SPOVersionManagement.ps1",
    "Export-AllSPOSites.ps1",
    "README.md",
    # Input files - will be reset to header-only
    "IncludeSites.csv",
    "ExcludeSites.csv"
)

$essentialRootFolders = @(
    "Logs",
    "config",
    "web"
)

$essentialLogsFiles = @(
    # CSV files stay in Logs/
)

$essentialConfigFiles = @(
    "AppPaths.json",
    "DashboardConfig.json",
    "ExtensionGroups.json",
    # JSON database files to reset (not delete)
    "JobStatus.json",
    "TenantStorage.json",
    "SiteExecutionHistory.json",
    "ExcludedSites.json",
    "AllSites.json",
    "SessionHistory.json",
    "StorageHistory.json"
)

$essentialWebFiles = @(
    "Dashboard.html",
    "localization.js"
)

$essentialLogsFolders = @(
    "Backup"
)

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  SPO VERSION MANAGEMENT - RESET TO DEPLOY STATE" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# Analyze what will be deleted
Write-Host "  Analyzing files..." -ForegroundColor Gray

# Root level - files to delete
$rootItems = Get-ChildItem -Path $scriptPath -ErrorAction SilentlyContinue
$rootFilesToDelete = @()
$rootFoldersToDelete = @()

foreach ($item in $rootItems) {
    if ($item.PSIsContainer) {
        if ($item.Name -notin $essentialRootFolders) {
            $rootFoldersToDelete += $item.Name
        }
    } else {
        if ($item.Name -notin $essentialRootFiles) {
            $rootFilesToDelete += $item.Name
        }
    }
}

# Logs level - files to delete
$logsFilesToDelete = @()
$logsFoldersToDelete = @()

if (Test-Path $logsPath) {
    $logsItems = Get-ChildItem -Path $logsPath -ErrorAction SilentlyContinue
    
    foreach ($item in $logsItems) {
        if ($item.PSIsContainer) {
            if ($item.Name -notin $essentialLogsFolders) {
                $logsFoldersToDelete += $item.Name
            }
        } else {
            if ($item.Name -notin $essentialLogsFiles) {
                $logsFilesToDelete += $item.Name
            }
        }
    }
}

# Backup folder - clear all contents
$backupFilesToDelete = @()
if (Test-Path $backupPath) {
    $backupFiles = Get-ChildItem -Path $backupPath -ErrorAction SilentlyContinue
    $backupFilesToDelete = $backupFiles | ForEach-Object { $_.Name }
}

# Show summary
Write-Host ""
Write-Host "  Files that will be KEPT (essential):" -ForegroundColor Green

Write-Host "    Root:" -ForegroundColor White
foreach ($f in $essentialRootFiles) {
    if (Test-Path (Join-Path $scriptPath $f)) {
        Write-Host "      - $f" -ForegroundColor Gray
    }
}
foreach ($f in $essentialRootFolders) {
    Write-Host "      - $f/" -ForegroundColor Gray
}

Write-Host "    Logs:" -ForegroundColor White
foreach ($f in $essentialLogsFiles) {
    if (Test-Path (Join-Path $logsPath $f)) {
        Write-Host "      - $f" -ForegroundColor Gray
    }
}
foreach ($f in $essentialLogsFolders) {
    Write-Host "      - $f/ (empty)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Files that will be DELETED:" -ForegroundColor Yellow

$totalToDelete = 0

if ($rootFilesToDelete.Count -gt 0) {
    Write-Host "    Root:" -ForegroundColor White
    foreach ($f in $rootFilesToDelete) {
        Write-Host "      - $f" -ForegroundColor DarkYellow
        $totalToDelete++
    }
}

if ($rootFoldersToDelete.Count -gt 0) {
    foreach ($f in $rootFoldersToDelete) {
        Write-Host "      - $f/ (folder)" -ForegroundColor DarkYellow
        $totalToDelete++
    }
}

if ($logsFilesToDelete.Count -gt 0) {
    Write-Host "    Logs:" -ForegroundColor White
    foreach ($f in $logsFilesToDelete) {
        Write-Host "      - $f" -ForegroundColor DarkYellow
        $totalToDelete++
    }
}

if ($logsFoldersToDelete.Count -gt 0) {
    foreach ($f in $logsFoldersToDelete) {
        Write-Host "      - $f/ (folder)" -ForegroundColor DarkYellow
        $totalToDelete++
    }
}

if ($backupFilesToDelete.Count -gt 0) {
    Write-Host "    Backup:" -ForegroundColor White
    Write-Host "      - $($backupFilesToDelete.Count) backup files" -ForegroundColor DarkYellow
    $totalToDelete += $backupFilesToDelete.Count
}

if ($totalToDelete -eq 0) {
    Write-Host "    (no files to delete)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "  Total: $totalToDelete items will be deleted" -ForegroundColor Cyan
Write-Host ""

# Confirmation
if (-not $Force) {
    $confirm = Read-Host "  Do you want to continue? (Y/N)"
    if ($confirm -ne 'Y' -and $confirm -ne 'y' -and $confirm -ne 'S' -and $confirm -ne 's') {
        Write-Host ""
        Write-Host "  Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

Write-Host ""
Write-Host "  Cleaning up..." -ForegroundColor Cyan

$deletedCount = 0
$errorCount = 0

# Delete root files
foreach ($f in $rootFilesToDelete) {
    $path = Join-Path $scriptPath $f
    try {
        Remove-Item $path -Force -ErrorAction Stop
        Write-Host "    [DEL] $f" -ForegroundColor DarkGray
        $deletedCount++
    } catch {
        Write-Host "    [ERR] $f : $_" -ForegroundColor Red
        $errorCount++
    }
}

# Delete root folders
foreach ($f in $rootFoldersToDelete) {
    $path = Join-Path $scriptPath $f
    try {
        Remove-Item $path -Recurse -Force -ErrorAction Stop
        Write-Host "    [DEL] $f/" -ForegroundColor DarkGray
        $deletedCount++
    } catch {
        Write-Host "    [ERR] $f/ : $_" -ForegroundColor Red
        $errorCount++
    }
}

# Delete logs files
foreach ($f in $logsFilesToDelete) {
    $path = Join-Path $logsPath $f
    try {
        Remove-Item $path -Force -ErrorAction Stop
        Write-Host "    [DEL] Logs/$f" -ForegroundColor DarkGray
        $deletedCount++
    } catch {
        Write-Host "    [ERR] Logs/$f : $_" -ForegroundColor Red
        $errorCount++
    }
}

# Delete logs folders
foreach ($f in $logsFoldersToDelete) {
    $path = Join-Path $logsPath $f
    try {
        Remove-Item $path -Recurse -Force -ErrorAction Stop
        Write-Host "    [DEL] Logs/$f/" -ForegroundColor DarkGray
        $deletedCount++
    } catch {
        Write-Host "    [ERR] Logs/$f/ : $_" -ForegroundColor Red
        $errorCount++
    }
}

# Clear backup folder contents (but keep the folder)
if ($backupFilesToDelete.Count -gt 0) {
    try {
        Get-ChildItem -Path $backupPath | Remove-Item -Recurse -Force -ErrorAction Stop
        Write-Host "    [DEL] Backup/* ($($backupFilesToDelete.Count) files)" -ForegroundColor DarkGray
        $deletedCount += $backupFilesToDelete.Count
    } catch {
        Write-Host "    [ERR] Backup/* : $_" -ForegroundColor Red
        $errorCount++
    }
}

# Create default JSON files (minimal state)
Write-Host ""
Write-Host "  Resetting JSON files to default structure..." -ForegroundColor Cyan

# JobStatus.json
$jobStatusFile = Join-Path $configPath "JobStatus.json"
@{
    LastUpdated = (Get-Date).ToString("o")
    ActiveJobs = @()
    QueuedSites = @()
    QueuedSitesCount = 0
    QueuedSitesSyncCount = 0
    QueuedSitesDeleteCount = 0
    RecentCompletedJobs = @()
    CompletedJobsCount = 0
    MajorVersionLimit = 4
    MajorWithMinorVersionsLimit = 4
} | ConvertTo-Json -Depth 10 | Set-Content -Path $jobStatusFile -Encoding UTF8
Write-Host "    [RESET] JobStatus.json" -ForegroundColor Green

# TenantStorage.json
$tenantStorageFile = Join-Path $configPath "TenantStorage.json"
@{
    LastUpdated = (Get-Date).ToString("o")
    StorageUsedGB = 0
    TenantQuotaGB = 0
    PercentUsed = 0
    StorageUsedBytes = 0
    TenantQuotaBytes = 0
} | ConvertTo-Json -Depth 10 | Set-Content -Path $tenantStorageFile -Encoding UTF8
Write-Host "    [RESET] TenantStorage.json" -ForegroundColor Green

# SiteExecutionHistory.json
$siteHistoryFile = Join-Path $configPath "SiteExecutionHistory.json"
@{
    LastUpdated = (Get-Date).ToString("o")
    Sites = @{}
} | ConvertTo-Json -Depth 10 | Set-Content -Path $siteHistoryFile -Encoding UTF8
Write-Host "    [RESET] SiteExecutionHistory.json" -ForegroundColor Green

# ExcludedSites.json
$excludedFile = Join-Path $configPath "ExcludedSites.json"
@{
    ExcludedSites = @()
    LastUpdated = (Get-Date).ToString("o")
} | ConvertTo-Json -Depth 10 | Set-Content -Path $excludedFile -Encoding UTF8
Write-Host "    [RESET] ExcludedSites.json" -ForegroundColor Green

# AllSites.json
$allSitesFile = Join-Path $configPath "AllSites.json"
@{
    ExportedAt = (Get-Date).ToString("o")
    TotalSites = 0
    Sites = @()
} | ConvertTo-Json -Depth 10 | Set-Content -Path $allSitesFile -Encoding UTF8
Write-Host "    [RESET] AllSites.json" -ForegroundColor Green

# SessionHistory.json
$sessionHistoryFile = Join-Path $configPath "SessionHistory.json"
@{
    Sessions = @()
} | ConvertTo-Json -Depth 10 | Set-Content -Path $sessionHistoryFile -Encoding UTF8
Write-Host "    [RESET] SessionHistory.json" -ForegroundColor Green

# StorageHistory.json
$storageHistoryFile = Join-Path $configPath "StorageHistory.json"
@{
    History = @()
    LastUpdated = (Get-Date).ToString("o")
} | ConvertTo-Json -Depth 10 | Set-Content -Path $storageHistoryFile -Encoding UTF8
Write-Host "    [RESET] StorageHistory.json" -ForegroundColor Green

# DashboardConfig.json - Reset to defaults but keep file
$dashboardConfigFile = Join-Path $configPath "DashboardConfig.json"
@{
    RefreshIntervalSeconds = 5
    Language = "en"
    ReexecutionDays = 7
    ZeroVersionAction = "ask"
    Theme = "dark"
    LastUpdated = (Get-Date).ToString("o")
} | ConvertTo-Json -Depth 10 | Set-Content -Path $dashboardConfigFile -Encoding UTF8
Write-Host "    [RESET] DashboardConfig.json" -ForegroundColor Green

# Reset input CSV files to header-only (if not keeping them)
if (-not $KeepInputFiles) {
    # IncludeSites.csv
    $includeSitesFile = Join-Path $scriptPath "IncludeSites.csv"
    "SiteUrl" | Set-Content -Path $includeSitesFile -Encoding UTF8
    Write-Host "    [RESET] IncludeSites.csv (header only)" -ForegroundColor Green
    
    # ExcludeSites.csv
    $excludeSitesFile = Join-Path $scriptPath "ExcludeSites.csv"
    "SiteUrl" | Set-Content -Path $excludeSitesFile -Encoding UTF8
    Write-Host "    [RESET] ExcludeSites.csv (header only)" -ForegroundColor Green
}

# Ensure Backup folder exists
if (-not (Test-Path $backupPath)) {
    New-Item -ItemType Directory -Path $backupPath -Force | Out-Null
    Write-Host "    [NEW] Backup/" -ForegroundColor Green
}

Write-Host ""
Write-Host "================================================================" -ForegroundColor Green
Write-Host "  RESET COMPLETED!" -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Deleted: $deletedCount items (CSV logs, temp files)" -ForegroundColor $(if ($deletedCount -gt 0) { "Yellow" } else { "Gray" })
$resetCount = if ($KeepInputFiles) { 8 } else { 10 }
$resetMsg = if ($KeepInputFiles) { "8 JSON files" } else { "8 JSON files + 2 CSV input files" }
Write-Host "  Reset:   $resetMsg to default structure" -ForegroundColor Cyan
if ($errorCount -gt 0) {
    Write-Host "  Errors:  $errorCount" -ForegroundColor Red
}
Write-Host ""
Write-Host "  The solution is ready for deployment." -ForegroundColor Cyan
Write-Host "  Run Start-SPOVersionManagement.ps1 to begin." -ForegroundColor Cyan
Write-Host ""
