$file = "C:\temp\SPOVersionManagement\Logs\AllSites.json"
$outFile = "C:\temp\SPOVersionManagement\_analysis_results.txt"
$results = [System.Text.StringBuilder]::new()

$null = $results.AppendLine("=== AllSites.json Analysis ===")

# Use Select-String for fast pattern counting without full JSON parse
$null = $results.AppendLine("")
$null = $results.AppendLine("--- Archive Status ---")

$lines = [IO.File]::ReadLines($file)
$counts = @{ NotArchived=0; Archived=0; FullyArchived=0; RecentlyArchived=0; Reactivating=0 }
$lockCounts = @{ Unlock=0; NoAccess=0; ReadOnly=0 }
$nullDates = 0
$totalSites = 0
$sampleDates = [System.Collections.ArrayList]::new()
$archivedUnlock = 0
$fullyArchivedUnlock = 0

foreach ($line in $lines) {
    if ($line -match '"ArchiveStatus"') {
        $totalSites++
        if ($line -match '"NotArchived"') { $counts.NotArchived++ }
        elseif ($line -match '"FullyArchived"') { $counts.FullyArchived++ }
        elseif ($line -match '"RecentlyArchived"') { $counts.RecentlyArchived++ }
        elseif ($line -match '"Reactivating"') { $counts.Reactivating++ }
        elseif ($line -match '"Archived"') { $counts.Archived++ }
    }
    if ($line -match '"LockState"') {
        if ($line -match '"Unlock"') { $lockCounts.Unlock++ }
        elseif ($line -match '"NoAccess"') { $lockCounts.NoAccess++ }
        elseif ($line -match '"ReadOnly"') { $lockCounts.ReadOnly++ }
    }
    if ($line -match '"LastContentModifiedDate":\s*null') { $nullDates++ }
    if ($sampleDates.Count -lt 5 -and $line -match '"LastContentModifiedDate":\s*"([^"]+)"') {
        $null = $sampleDates.Add($Matches[1])
    }
}

foreach ($k in $counts.Keys | Sort-Object) { $null = $results.AppendLine("$k`: $($counts[$k])") }
$null = $results.AppendLine("Total with ArchiveStatus: $totalSites")

$null = $results.AppendLine("")
$null = $results.AppendLine("--- LockState ---")
foreach ($k in $lockCounts.Keys | Sort-Object) { $null = $results.AppendLine("$k`: $($lockCounts[$k])") }

$null = $results.AppendLine("")
$null = $results.AppendLine("Null LastContentModifiedDate: $nullDates")

$null = $results.AppendLine("")
$null = $results.AppendLine("--- Sample Dates ---")
foreach ($d in $sampleDates) { $null = $results.AppendLine($d) }

# Now check SAM report
$null = $results.AppendLine("")
$null = $results.AppendLine("=== SAM Report ===")
$csv = Import-Csv "C:\temp\SPOVersionManagement\Logs\Report created by Content Management Assessment_20260402184309000.csv"
$null = $results.AppendLine("Total rows: $($csv.Count)")
$inactive = ($csv | Where-Object { $_.'Is inactive' -eq 'True' }).Count
$ownerless = ($csv | Where-Object { $_.'Is ownerless' -eq 'True' }).Count
$null = $results.AppendLine("Inactive: $inactive")
$null = $results.AppendLine("Ownerless: $ownerless")
$null = $results.AppendLine("Both: $(($csv | Where-Object { $_.'Is inactive' -eq 'True' -and $_.'Is ownerless' -eq 'True' }).Count)")

$null = $results.AppendLine("")
$null = $results.AppendLine("--- Templates ---")
$csv | Group-Object Template | Sort-Object Count -Descending | ForEach-Object {
    $null = $results.AppendLine("$($_.Count) $($_.Name)")
}

# Check JobStatus.json
$null = $results.AppendLine("")
$null = $results.AppendLine("=== JobStatus.json ===")
$js = Get-Content "C:\temp\SPOVersionManagement\Logs\JobStatus.json" -Raw | ConvertFrom-Json
$null = $results.AppendLine("ActiveJobs: $(if($js.ActiveJobs){$js.ActiveJobs.Count}else{0})")
$null = $results.AppendLine("CompletedJobs: $(if($js.RecentCompletedJobs){$js.RecentCompletedJobs.Count}else{0})")
$null = $results.AppendLine("SkippedJobs: $(if($js.SkippedJobs){$js.SkippedJobs.Count}else{0})")

# Check TenantStorage.json
$null = $results.AppendLine("")
$null = $results.AppendLine("=== TenantStorage.json ===")
$ts = Get-Content "C:\temp\SPOVersionManagement\Logs\TenantStorage.json" -Raw | ConvertFrom-Json
$null = $results.AppendLine("TotalQuota: $($ts.TotalQuotaTB) TB")
$null = $results.AppendLine("TotalUsed: $($ts.TotalUsedTB) TB")

[IO.File]::WriteAllText($outFile, $results.ToString())
Write-Host "Analysis written to $outFile"
