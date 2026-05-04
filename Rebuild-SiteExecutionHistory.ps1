# Rebuild-SiteExecutionHistory.ps1
# Rebuilds SiteExecutionHistory.json from ExecutionHistory.csv

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$logsPath = Join-Path $scriptPath "Logs"
$configPath = Join-Path $scriptPath "config"
if (-not (Test-Path $configPath)) { New-Item -ItemType Directory -Path $configPath -Force | Out-Null }
$executionHistoryFile = Join-Path $logsPath "ExecutionHistory.csv"
$siteExecutionHistoryFile = Join-Path $configPath "SiteExecutionHistory.json"

Write-Host "Rebuilding SiteExecutionHistory.json..." -ForegroundColor Cyan
Write-Host "  Source: $executionHistoryFile" -ForegroundColor Gray

if (-not (Test-Path $executionHistoryFile)) {
    Write-Error "ExecutionHistory.csv file not found!"
    exit 1
}

# Read CSV (raw data without header)
$csvContent = Get-Content $executionHistoryFile

Write-Host "  Lines found: $($csvContent.Count)" -ForegroundColor Gray

# Structure of CSV:
# 0: Timestamp, 1: SiteUrl, 2: JobType, 3: WorkItemId, 4: Status, 
# 5: StartTime, 6: EndTime, 7: DurationMinutes, 8: ListsProcessed, 9: ListsSynced,
# 10: ListSyncFailed, 11: FilesProcessed, 12: VersionsProcessed, 13: VersionsDeleted,
# 14: VersionsFailed, 15: StorageReleasedInBytes, 16: StorageReleasedMB,
# 17: ?, 18: InitialStorageBytes, 19: FinalStorageBytes

$history = @{
    LastUpdated = (Get-Date).ToString("o")
    Sites = @{}
}

$processedCount = 0

foreach ($line in $csvContent) {
    if (-not $line -or $line.Trim().Length -eq 0) { continue }
    
    $parts = $line.Split(',')
    if ($parts.Count -lt 16) { continue }
    
    $timestamp = $parts[0]
    $siteUrl = $parts[1]
    $jobType = $parts[2]
    $workItemId = $parts[3]
    $status = $parts[4]
    $startTime = $parts[5]
    $endTime = $parts[6]
    $durationMinutes = [double]$parts[7]
    $listsProcessed = [int]$parts[8]
    $listsSynced = [int]$parts[9]
    $filesProcessed = [int]$parts[11]
    $versionsProcessed = [int]$parts[12]
    $versionsDeleted = [int]$parts[13]
    $storageReleasedBytes = [long]$parts[15]
    $storageBeforeBytes = if ($parts.Count -gt 18 -and $parts[18]) { [long]$parts[18] } else { 0 }
    $storageAfterBytes = if ($parts.Count -gt 19 -and $parts[19]) { [long]$parts[19] } else { 0 }
    
    if (-not $siteUrl) { continue }
    
    $normalizedUrl = $siteUrl.TrimEnd("/").ToLower()
    
    # Create site entry if it doesn't exist
    if (-not $history.Sites.ContainsKey($normalizedUrl)) {
        # Extract title from URL
        $urlParts = $siteUrl.Split('/')
        $title = if ($urlParts.Count -gt 4) { $urlParts[-1] } else { $siteUrl }
        
        $history.Sites[$normalizedUrl] = @{
            SiteUrl = $siteUrl
            Title = $title
            FirstProcessed = $timestamp
            LastProcessed = $timestamp
            TotalExecutions = 0
            TotalVersionsDeleted = 0
            TotalStorageReleasedBytes = 0
            Executions = @()
        }
    }
    
    $siteHistory = $history.Sites[$normalizedUrl]
    
    # Create execution record
    $executedAtDisplay = $timestamp
    try { $executedAtDisplay = [DateTime]::Parse($timestamp).ToString("dd/MM/yyyy HH:mm:ss") } catch { }
    
    $executionRecord = @{
        ExecutionId = "Recovered_" + [guid]::NewGuid().ToString().Substring(0,8)
        ExecutedAt = $timestamp
        ExecutedAtDisplay = $executedAtDisplay
        JobType = $jobType
        Status = $status
        DurationMinutes = $durationMinutes
        WorkItemId = $workItemId
        ListsProcessed = $listsProcessed
        ListsSynced = $listsSynced
        FilesProcessed = $filesProcessed
        VersionsProcessed = $versionsProcessed
        VersionsDeleted = $versionsDeleted
        StorageReleasedBytes = $storageReleasedBytes
        StorageReleasedMB = [math]::Round(($storageReleasedBytes / 1MB), 2)
        StorageReleasedGB = [math]::Round(($storageReleasedBytes / 1GB), 4)
        StorageBeforeBytes = $storageBeforeBytes
        StorageAfterBytes = $storageAfterBytes
        MajorVersionLimit = 0
        MajorWithMinorVersionsLimit = 0
    }
    
    $siteHistory.Executions += $executionRecord
    $siteHistory.TotalExecutions = $siteHistory.Executions.Count
    $siteHistory.LastProcessed = $timestamp
    
    # Accumulate totals (only for BatchDelete)
    if ($jobType -eq "BatchDelete") {
        $siteHistory.TotalVersionsDeleted += $versionsDeleted
        $siteHistory.TotalStorageReleasedBytes += $storageReleasedBytes
    }
    
    $history.Sites[$normalizedUrl] = $siteHistory
    $processedCount++
}

$history.LastUpdated = (Get-Date).ToString("o")

# Backup existing file
if (Test-Path $siteExecutionHistoryFile) {
    $backupFile = $siteExecutionHistoryFile -replace '\.json$', "_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').json"
    Copy-Item $siteExecutionHistoryFile $backupFile -Force
    Write-Host "  Backup created: $backupFile" -ForegroundColor Yellow
}

# Save new file
$history | ConvertTo-Json -Depth 10 | Set-Content -Path $siteExecutionHistoryFile -Encoding UTF8

Write-Host ""
Write-Host "Rebuild completed!" -ForegroundColor Green
Write-Host "  Executions processed: $processedCount" -ForegroundColor Cyan
Write-Host "  Sites recovered: $($history.Sites.Count)" -ForegroundColor Cyan
Write-Host "  File saved: $siteExecutionHistoryFile" -ForegroundColor Cyan
