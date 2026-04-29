<#
.SYNOPSIS
    Tests the download and processing of storage report from Graph API
.DESCRIPTION
    Diagnostic script to verify if Get-MgReportSharePointSiteUsageStorage
    is working correctly
#>

param(
    [ValidateSet("D7", "D30", "D90", "D180")]
    [string]$Period = "D180"
)

# Configurar encoding UTF-8 para o console
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Graph API Report Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verify Graph connection
Write-Host "[1] Verifying Graph API connection..." -ForegroundColor Yellow
try {
    $context = Get-MgContext -ErrorAction SilentlyContinue
    if ($context) {
        Write-Host "  [OK] Connected as: $($context.Account)" -ForegroundColor Green
    } else {
        Write-Host "  [!] Not connected. Connecting..." -ForegroundColor Yellow
        Connect-MgGraph -Scopes "Reports.Read.All" -NoWelcome
        Write-Host "  [OK] Connected" -ForegroundColor Green
    }
}
catch {
    Write-Error "Error connecting to Graph: $_"
    exit 1
}

# Download report
Write-Host ""
Write-Host "[2] Downloading report ($Period)..." -ForegroundColor Yellow

$tempFile = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) "Logs\GraphReport_Test.csv"

try {
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    Get-MgReportSharePointSiteUsageStorage -Period $Period -OutFile $tempFile -ErrorAction Stop
    $stopwatch.Stop()
    
    Write-Host "  [OK] Download completed in $($stopwatch.ElapsedMilliseconds)ms" -ForegroundColor Green
}
catch {
    Write-Error "Error downloading report: $_"
    exit 1
}

# Verify file
Write-Host ""
Write-Host "[3] Verifying file..." -ForegroundColor Yellow

if (Test-Path $tempFile) {
    $fileInfo = Get-Item $tempFile
    Write-Host "  Size: $($fileInfo.Length) bytes" -ForegroundColor Cyan
    
    # Read raw content
    $rawContent = Get-Content $tempFile -Raw -Encoding UTF8
    
    # Show first lines
    Write-Host ""
    Write-Host "[4] File content (first 10 lines):" -ForegroundColor Yellow
    Write-Host "  ----------------------------------------" -ForegroundColor Gray
    
    $lines = $rawContent -split "`r?`n" | Select-Object -First 10
    foreach ($line in $lines) {
        Write-Host "  $line" -ForegroundColor White
    }
    Write-Host "  ----------------------------------------" -ForegroundColor Gray
    
    # Count lines
    $allLines = $rawContent -split "`r?`n" | Where-Object { $_.Trim() -ne '' }
    Write-Host ""
    Write-Host "  Total lines: $($allLines.Count)" -ForegroundColor Cyan
    
    # Try Import-Csv
    Write-Host ""
    Write-Host "[5] Testing Import-Csv..." -ForegroundColor Yellow
    
    try {
       
        
        
        $csvData = Import-Csv -Path $tempFile -ErrorAction Stop
        
        Write-Host "  [OK] Import-Csv worked: $($csvData.Count) records" -ForegroundColor Green
        
        if ($csvData.Count -gt 0) {
            Write-Host ""
            Write-Host "  Columns found:" -ForegroundColor Cyan
            $csvData[0].PSObject.Properties.Name | ForEach-Object {
                Write-Host "    - $_" -ForegroundColor White
            }
            
            Write-Host ""
            Write-Host "  First 3 records:" -ForegroundColor Cyan
            $csvData | Select-Object -First 3 | Format-Table | Out-String | Write-Host
        }
    }
    catch {
        Write-Host "  [!] Import-Csv failed: $_" -ForegroundColor Yellow
    }
    
    # Test monthly aggregation
    Write-Host ""
    Write-Host "[6] Testing monthly aggregation..." -ForegroundColor Yellow
    
    $scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
    Import-Module "$scriptPath\SPOVersionManagement.psm1" -Force -DisableNameChecking
    
    $aggregated = Get-TenantStorageHistoryAggregated -Period $Period
    
    if ($aggregated -and $aggregated.MonthlyData) {
        Write-Host "  [OK] Aggregation worked!" -ForegroundColor Green
        Write-Host ""
        Write-Host "  Monthly data (sorted chronologically):" -ForegroundColor Cyan
        
        # Sort by MonthKey to ensure chronological order
        $sortedData = $aggregated.MonthlyData | Sort-Object { $_.MonthKey }
        
        foreach ($month in $sortedData) {
            $growth = if ($month.GrowthGB -gt 0) { "+$($month.GrowthGB)" } else { "$($month.GrowthGB)" }
            Write-Host "    $($month.MonthKey) ($($month.MonthName)): $($month.EndGB) GB ($growth GB)" -ForegroundColor White
        }
        
        Write-Host ""
        Write-Host "  Average monthly growth: $($aggregated.AvgMonthlyGrowthGB) GB/month" -ForegroundColor Cyan
        Write-Host "  Period: $($aggregated.ReportStartDate) to $($aggregated.ReportEndDate)" -ForegroundColor Gray
    }
    else {
        Write-Host "  [!] Aggregation did not return data" -ForegroundColor Yellow
    }
    
} else {
    Write-Error "File was not created: $tempFile"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Test completed" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""