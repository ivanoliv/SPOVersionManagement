<#
.SYNOPSIS
    Imports SAM (SharePoint Admin Center) unused sites report and generates ArchiveAnalysis.json.
.DESCRIPTION
    Reads the large AllSites.json (can be 300+ MB) and extracts only the fields needed by the Archive tab.
    Merges data from the SharePoint Admin Center (SAM) Content Management Assessment CSV report
    to add inactive/ownerless flags based on Microsoft's 180-day usage policy analysis.
    
    The output ArchiveAnalysis.json is typically 5-15 MB (vs 300+ MB), making the Dashboard Archive tab 
    load instantly instead of crashing the browser.

    How to get the SAM report:
    1. Go to Microsoft 365 Admin Center > SharePoint Admin Center
    2. Navigate to Sites > Active sites > Content Management Assessment
    3. Download the CSV report ("Report created by Content Management Assessment_*.csv")
    4. Place in the Logs folder or specify path with -SAMReportPath
.PARAMETER LogPath
    Path to the Logs folder (default: .\Logs)
.PARAMETER SAMReportPath
    Path to the SAM Content Management Assessment CSV report (optional).
    If not specified, looks for any matching file in the Logs folder.
.PARAMETER AllSitesPath
    Path to AllSites.json (default: LogPath\AllSites.json)
.EXAMPLE
    .\Import-SamUnusedSites.ps1
.EXAMPLE
    .\Import-SamUnusedSites.ps1 -SAMReportPath ".\Logs\Report created by Content Management Assessment_20260402184309000.csv"
#>
[CmdletBinding()]
param(
    [string]$LogPath,
    [string]$SAMReportPath,
    [string]$AllSitesPath
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $LogPath) { $LogPath = Join-Path $scriptRoot "Logs" }
if (-not $AllSitesPath) { $AllSitesPath = Join-Path $LogPath "AllSites.json" }

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  IMPORT SAM UNUSED SITES" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# ── 1. Load AllSites.json ─────────────────────────────────────────
if (-not (Test-Path $AllSitesPath)) {
    Write-Host "  [ERROR] AllSites.json not found: $AllSitesPath" -ForegroundColor Red
    Write-Host "  Run Export-AllSitesDataForDashboard first." -ForegroundColor Yellow
    exit 1
}

$fileSize = [math]::Round((Get-Item $AllSitesPath).Length / 1MB, 1)
Write-Host "  Loading AllSites.json ($fileSize MB)..." -ForegroundColor Gray
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$allSitesRaw = Get-Content $AllSitesPath -Raw | ConvertFrom-Json
$allSites = $allSitesRaw.Sites
Write-Host "  Loaded $($allSites.Count) sites in $($sw.Elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Green

# ── 1b. Load previous ArchiveAnalysis.json (preserve EffectiveDates) ──
$previousED = @{}
$previousAnalysisPath = Join-Path $LogPath "ArchiveAnalysis.json"
if (Test-Path $previousAnalysisPath) {
    Write-Host "  Loading previous ArchiveAnalysis.json to preserve EffectiveDates..." -ForegroundColor Gray
    try {
        $prevData = Get-Content $previousAnalysisPath -Raw | ConvertFrom-Json
        foreach ($s in $prevData.Candidates) {
            if ($s.ED -and $s.U) { $previousED[$s.U.TrimEnd("/").ToLower()] = $s.ED }
        }
        foreach ($s in $prevData.ArchivedSites) {
            if ($s.ED -and $s.U) { $previousED[$s.U.TrimEnd("/").ToLower()] = $s.ED }
        }
        Write-Host "  Preserved $($previousED.Count) EffectiveDate entries from previous export" -ForegroundColor Green
    } catch {
        Write-Host "  [WARN] Could not parse previous ArchiveAnalysis.json: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# ── 2. Load SAM Report (optional) ───────────────────────────────
$samData = @{}
if (-not $SAMReportPath) {
    # Auto-detect SAM report in Logs folder
    $samFiles = @(Get-ChildItem -Path $LogPath -Filter "Report created by Content Management*" -ErrorAction SilentlyContinue)
    if ($samFiles.Count -gt 0) {
        $SAMReportPath = ($samFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
        Write-Host "  [SAM] Auto-detected report: $(Split-Path $SAMReportPath -Leaf)" -ForegroundColor Cyan
    }
}

$samStats = @{ Total = 0; Inactive = 0; Ownerless = 0; Both = 0; Matched = 0 }
if ($SAMReportPath -and (Test-Path $SAMReportPath)) {
    Write-Host "  [SAM] Loading SAM report..." -ForegroundColor Gray
    $samCsv = Import-Csv $SAMReportPath
    $samStats.Total = $samCsv.Count
    
    foreach ($row in $samCsv) {
        $url = $row.URL
        if (-not $url) { continue }
        $urlNorm = $url.TrimEnd("/").ToLower()
        
        $isInactive = $row.'Is inactive' -eq 'True'
        $isOwnerless = $row.'Is ownerless' -eq 'True'
        
        if ($isInactive) { $samStats.Inactive++ }
        if ($isOwnerless) { $samStats.Ownerless++ }
        if ($isInactive -and $isOwnerless) { $samStats.Both++ }
        
        $samData[$urlNorm] = @{
            IsInactive       = $isInactive
            IsOwnerless      = $isOwnerless
            SAMLastActivity  = $row.'Last activity date (UTC)'
            SAMTemplate      = $row.Template
            ConnectedToTeams = $row.'Connected to Teams' -eq 'True'
            SensitivityLabel = $row.'Sensitivity label'
            SiteOwners       = $row.'Email address of site owners'
        }
    }
    
    Write-Host "  [SAM] Loaded $($samStats.Total) sites: $($samStats.Inactive) inactive, $($samStats.Ownerless) ownerless" -ForegroundColor Green
} else {
    Write-Host "  [SAM] No SAM report found (optional - skip)" -ForegroundColor DarkGray
}

# ── 3. Build lightweight analysis data ────────────────────────────
Write-Host ""
Write-Host "  Processing sites..." -ForegroundColor Gray

$candidates = [System.Collections.ArrayList]::new()
$archivedSites = [System.Collections.ArrayList]::new()
$statusCounts = @{}
$totalStorageMB = 0
$totalVersionSizeMB = 0
$totalVersionCount = 0

foreach ($site in $allSites) {
    $archiveStatus = if ($site.ArchiveStatus) { $site.ArchiveStatus } else { "NotArchived" }
    $lockState = if ($site.LockState) { $site.LockState } else { "Unlock" }
    
    # Count statuses
    if (-not $statusCounts.ContainsKey($archiveStatus)) { $statusCounts[$archiveStatus] = 0 }
    $statusCounts[$archiveStatus]++
    
    $storageMB = if ($site.StorageUsageCurrent) { $site.StorageUsageCurrent } 
                 elseif ($site.StorageUsedMB) { $site.StorageUsedMB } else { 0 }
    $totalStorageMB += $storageMB
    $totalVersionSizeMB += if ($site.VersionSizeMB) { $site.VersionSizeMB } else { 0 }
    $totalVersionCount += if ($site.VersionCount) { $site.VersionCount } else { 0 }
    
    # Lookup SAM data
    $urlNorm = ($site.Url).TrimEnd("/").ToLower()
    $sam = $samData[$urlNorm]
    if ($sam) { $samStats.Matched++ }
    
    # Build lightweight site object - minimal fields for Dashboard archive tab
    $siteObj = [ordered]@{
        U  = $site.Url
        T  = if ($site.Title) { $site.Title } else { "" }
        S  = $storageMB
        D  = $site.LastContentModifiedDate
        C  = $site.CreatedTime
        AS = $archiveStatus
        LS = $lockState
        O  = if ($site.Owner) { $site.Owner } else { "" }
        ST = if ($site.Status) { $site.Status } else { "Active" }
        VC = if ($site.VersionCount) { $site.VersionCount } else { 0 }
        VS = if ($site.VersionSizeMB) { $site.VersionSizeMB } else { 0 }
        TM = if ($site.Template) { $site.Template } else { "" }
    }
    
    # Add SAM flags (compact: I=inactive, OL=ownerless, SA=SAM last activity, CT=connected to teams)
    if ($sam) {
        $siteObj.I  = $sam.IsInactive
        $siteObj.OL = $sam.IsOwnerless
        $siteObj.SA = $sam.SAMLastActivity
        $siteObj.CT = $sam.ConnectedToTeams
    }
    
    $statusLower = $archiveStatus.ToLower()
    if ($statusLower -eq 'archived' -or $statusLower -eq 'fullyarchived' -or $statusLower -eq 'recentlyarchived' -or $statusLower -eq 'reactivating') {
        $siteObj.AB = if ($site.ArchivedBy) { $site.ArchivedBy } else { "" }
        $siteObj.AT = $site.ArchivedTime
        $null = $archivedSites.Add([PSCustomObject]$siteObj)
    } else {
        $null = $candidates.Add([PSCustomObject]$siteObj)
    }
}

# ── 4. Pre-compute period buckets and sort candidates ────────────
Write-Host "  Computing period buckets..." -ForegroundColor Gray

$now = Get-Date
$periods = @(7, 30, 60, 90, 180, 365, 730)  # D7 through D730 (2 years)
$periodStats = [ordered]@{}
$lockFilteredCount = 0
$hasSAM = $samData.Count -gt 0

# Filter candidates: only unlocked, not archived
$eligibleCandidates = [System.Collections.ArrayList]::new()
foreach ($c in $candidates) {
    $ls = $c.LS
    if ($ls -and $ls.ToLower() -ne 'unlock') { $lockFilteredCount++; continue }
    $null = $eligibleCandidates.Add($c)
}

# Determine effective "last activity" date for each candidate
# Priority: Previous ED (preserved) > SAM Last Activity Date > SPO LastContentModifiedDate
# Once captured, ED is preserved across re-exports to survive version cleanup date changes
foreach ($c in $eligibleCandidates) {
    $effectiveDate = $null
    $urlKey = $c.U.TrimEnd("/").ToLower()
    
    # Try previously preserved EffectiveDate first (survives version cleanup)
    $prevED = $previousED[$urlKey]
    if ($prevED) {
        try { $effectiveDate = [DateTime]::Parse($prevED) } catch { }
    }
    
    # Try SAM date (more reliable for user activity than SPO)
    if (-not $effectiveDate -and $c.SA) {
        try { $effectiveDate = [DateTime]::Parse($c.SA) } catch { }
    }
    
    # Fall back to SPO LastContentModifiedDate
    if (-not $effectiveDate -and $c.D) {
        try { $effectiveDate = [DateTime]::Parse($c.D) } catch { }
    }
    
    # Store effective date as ISO string for the Dashboard
    if ($effectiveDate) {
        $c | Add-Member -NotePropertyName 'ED' -NotePropertyValue $effectiveDate.ToString("o") -Force
    }
}

# Sort by effective date ascending (oldest first) - stable for period slicing
$sortedCandidates = $eligibleCandidates | Sort-Object { 
    if ($_.ED) { try { [DateTime]::Parse($_.ED) } catch { [DateTime]::MaxValue } } else { [DateTime]::MaxValue }
}

# Compute how many candidates fall into each period
foreach ($p in $periods) {
    $cutoff = $now.AddDays(-$p)
    $count = 0
    $storageMB = 0
    $versionCount = 0
    $versionSizeMB = 0
    $samInactive = 0
    $samOwnerless = 0
    foreach ($s in $sortedCandidates) {
        if ($s.ED) {
            try {
                $dateVal = [DateTime]::Parse($s.ED)
                if ($dateVal -lt $cutoff) {
                    $count++
                    $storageMB += $s.S
                    $versionCount += $s.VC
                    $versionSizeMB += $s.VS
                    if ($s.I) { $samInactive++ }
                    if ($s.OL) { $samOwnerless++ }
                }
            } catch { }
        }
    }
    $periodStats["D$p"] = [ordered]@{
        Days       = $p
        Count      = $count
        StorageMB  = [math]::Round($storageMB, 2)
        StorageGB  = [math]::Round($storageMB / 1024, 2)
        VersionCount    = $versionCount
        VersionSizeMB   = [math]::Round($versionSizeMB, 2)
        SAMInactive     = $samInactive
        SAMOwnerless    = $samOwnerless
    }
}

# Compute SAM-only stats (sites flagged inactive regardless of date)
$samOnlyStats = [ordered]@{
    InactiveSites = 0
    InactiveStorageMB = 0
    OwnerlessSites = 0
    OwnerlessStorageMB = 0
}
foreach ($s in $sortedCandidates) {
    if ($s.I) { $samOnlyStats.InactiveSites++; $samOnlyStats.InactiveStorageMB += $s.S }
    if ($s.OL) { $samOnlyStats.OwnerlessSites++; $samOnlyStats.OwnerlessStorageMB += $s.S }
}
$samOnlyStats.InactiveStorageGB = [math]::Round($samOnlyStats.InactiveStorageMB / 1024, 2)
$samOnlyStats.OwnerlessStorageGB = [math]::Round($samOnlyStats.OwnerlessStorageMB / 1024, 2)

# ── 5. Write ArchiveAnalysis.json ─────────────────────────────────
$outputPath = Join-Path $LogPath "ArchiveAnalysis.json"

$analysis = [ordered]@{
    LastUpdated      = (Get-Date).ToString("o")
    ExportedAt       = (Get-Date).ToString("dd/MM/yyyy HH:mm:ss")
    SourceFile       = Split-Path $AllSitesPath -Leaf
    SAMReportFile    = if ($SAMReportPath) { Split-Path $SAMReportPath -Leaf } else { $null }
    TotalSites       = $allSites.Count
    TotalStorageMB   = [math]::Round($totalStorageMB, 2)
    TotalStorageGB   = [math]::Round($totalStorageMB / 1024, 2)
    TotalStorageTB   = [math]::Round($totalStorageMB / 1048576, 2)
    TotalVersionCount = $totalVersionCount
    TotalVersionSizeMB = [math]::Round($totalVersionSizeMB, 2)
    
    ArchiveStatusSummary = $statusCounts
    PeriodStats          = $periodStats
    SAMCandidateStats    = $samOnlyStats
    DateSource           = if ($hasSAM) { "SAM" } else { "SPO" }
    
    SAMReport = [ordered]@{
        Available     = ($null -ne $SAMReportPath)
        TotalSites    = $samStats.Total
        InactiveSites = $samStats.Inactive
        OwnerlessSites = $samStats.Ownerless
        BothInactiveAndOwnerless = $samStats.Both
        MatchedWithAllSites = $samStats.Matched
    }
    
    Candidates    = @($sortedCandidates)
    ArchivedSites = $archivedSites
}

Write-Host ""
Write-Host "  Writing ArchiveAnalysis.json..." -ForegroundColor Gray
$analysis | ConvertTo-Json -Depth 5 -Compress | Set-Content -Path $outputPath -Encoding UTF8

$outSize = [math]::Round((Get-Item $outputPath).Length / 1MB, 1)

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  EXPORT COMPLETE" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total sites:    $($allSites.Count)" -ForegroundColor White
Write-Host "  Candidates:     $($sortedCandidates.Count) (active, unlocked, not archived)" -ForegroundColor Green
Write-Host "  Archived:       $($archivedSites.Count)" -ForegroundColor Yellow
if ($lockFilteredCount -gt 0) {
    Write-Host "  Locked/filtered: $lockFilteredCount" -ForegroundColor DarkGray
}
foreach ($key in $statusCounts.Keys | Sort-Object) {
    Write-Host "    $key`: $($statusCounts[$key])" -ForegroundColor Gray
}
Write-Host ""
Write-Host "  Period Buckets:" -ForegroundColor Cyan
foreach ($pKey in $periodStats.Keys) {
    $ps = $periodStats[$pKey]
    Write-Host "    $pKey`: $($ps.Count) sites ($($ps.StorageGB) GB)" -ForegroundColor Gray
}
if ($samStats.Matched -gt 0) {
    Write-Host ""
    Write-Host "  SAM Report:" -ForegroundColor Cyan
    Write-Host "    Matched:     $($samStats.Matched) / $($samStats.Total)" -ForegroundColor Gray
    Write-Host "    Inactive:    $($samStats.Inactive)" -ForegroundColor Gray
    Write-Host "    Ownerless:   $($samStats.Ownerless)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "  Output: $outputPath ($outSize MB)" -ForegroundColor Green
Write-Host "  (vs AllSites.json: $fileSize MB - $([math]::Round($fileSize / [math]::Max($outSize, 0.1), 0))x reduction)" -ForegroundColor Gray
Write-Host ""
