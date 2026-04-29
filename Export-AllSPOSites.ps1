<#
.SYNOPSIS
    Exports all SharePoint Online sites to CSV with complete details.

.DESCRIPTION
    This script connects to SharePoint Online Admin Center and exports all sites
    using Get-SPOSite -Limit All -Detailed, saving the result to a CSV file.

.PARAMETER AdminUrl
    URL of SharePoint Admin Center (e.g., https://contoso-admin.sharepoint.com)

.PARAMETER OutputPath
    Output CSV file path. If not specified, saves to C:\temp\SPOVersionManagement\Logs\AllSPOSites.csv

.EXAMPLE
    .\Export-AllSPOSites.ps1 -AdminUrl "https://contoso-admin.sharepoint.com"

.EXAMPLE
    .\Export-AllSPOSites.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -OutputPath "C:\Reports\sites.csv"
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$AdminUrl,
    
    [Parameter(Mandatory = $false)]
    [string]$OutputPath = "C:\temp\SPOVersionManagement\Logs\AllSPOSites.csv"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  EXPORT ALL SHAREPOINT ONLINE SITES" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Check if SharePoint Online module is available
if (-not (Get-Module -ListAvailable -Name Microsoft.Online.SharePoint.PowerShell)) {
    Write-Host "[ERROR] Microsoft.Online.SharePoint.PowerShell module not found." -ForegroundColor Red
    Write-Host "Run: Install-Module -Name Microsoft.Online.SharePoint.PowerShell" -ForegroundColor Yellow
    exit 1
}

# Import module
Import-Module Microsoft.Online.SharePoint.PowerShell -DisableNameChecking -WarningAction SilentlyContinue

# Connect to SharePoint Online
Write-Host "Connecting to SharePoint Online Admin Center..." -ForegroundColor Yellow
Write-Host "  URL: $AdminUrl" -ForegroundColor Gray

try {
    # Check if already connected
    $existingConnection = $null
    try {
        $existingConnection = Get-SPOSite -Limit 1 -ErrorAction SilentlyContinue
    }
    catch {
        $existingConnection = $null
    }
    
    if (-not $existingConnection) {
        Connect-SPOService -Url $AdminUrl
    }
    
    Write-Host "[OK] Connected successfully!" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Connection failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Getting all sites (this may take a few minutes)..." -ForegroundColor Yellow

$startTime = Get-Date

try {
    # Get all sites with details
    $allSites = Get-SPOSite -Limit All -Detailed
    
    $elapsed = (Get-Date) - $startTime
    Write-Host "[OK] $($allSites.Count) sites found in $($elapsed.TotalSeconds.ToString('N1')) seconds" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to get sites: $_" -ForegroundColor Red
    exit 1
}

# Create output directory if it doesn't exist
$outputDir = Split-Path -Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    Write-Host "Directory created: $outputDir" -ForegroundColor Gray
}

# Select all properties for CSV
Write-Host ""
Write-Host "Processing data for export..." -ForegroundColor Yellow

$sitesData = $allSites | Select-Object *

# Export to CSV
Write-Host "Exporting to CSV..." -ForegroundColor Yellow

try {
    $sitesData | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8
    Write-Host "[OK] File saved: $OutputPath" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to save CSV: $_" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  EXPORT COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "  Total sites: $($allSites.Count)" -ForegroundColor White
Write-Host "  File: $OutputPath" -ForegroundColor Cyan
Write-Host "  Size: $([math]::Round((Get-Item $OutputPath).Length / 1KB, 2)) KB" -ForegroundColor Gray
Write-Host ""

# Quick statistics
$teamSites = @($allSites | Where-Object { $_.Template -like "*GROUP*" }).Count
$commSites = @($allSites | Where-Object { $_.Template -like "*SITEPAGEPUBLISHING*" }).Count
$classicSites = @($allSites | Where-Object { $_.Template -like "*STS*" -or $_.Template -like "*TEAM*" }).Count

Write-Host "  Statistics:" -ForegroundColor Cyan
Write-Host "    Group Sites (Teams): $teamSites" -ForegroundColor Gray
Write-Host "    Communication Sites: $commSites" -ForegroundColor Gray
Write-Host "    Classic Sites: $classicSites" -ForegroundColor Gray

$totalStorageGB = ($allSites | Measure-Object -Property StorageUsageCurrent -Sum).Sum / 1024
Write-Host "    Total Storage: $($totalStorageGB.ToString('N2')) GB" -ForegroundColor Gray
Write-Host ""

# Ask user if they want to save to AllSites.json for Dashboard
Write-Host "Do you want to save this data to AllSites.json for the Dashboard?" -ForegroundColor Yellow
Write-Host "  This will update the local database so the Dashboard can display fresh data" -ForegroundColor Gray
Write-Host ""
$saveToJson = Read-Host "Save to AllSites.json? (Y/N)"

if ($saveToJson -eq 'Y' -or $saveToJson -eq 'y') {
    $jsonOutputPath = Join-Path (Split-Path $OutputPath -Parent) "AllSites.json"
    
    Write-Host ""
    Write-Host "Converting data for Dashboard format..." -ForegroundColor Yellow
    Write-Host "  Using VersionCount and VersionSize directly from Get-SPOSite" -ForegroundColor Gray
    
    # Convert to Dashboard format
    $dashboardSites = $allSites | ForEach-Object {
        # Get version data directly from SPO (Get-SPOSite returns VersionCount and VersionSize in bytes)
        $versionCount = $_.VersionCount
        $versionSize = $_.VersionSize  # In bytes
        $versionSizeMB = if ($versionSize) { [math]::Round($versionSize / 1MB, 2) } else { 0 }
        $versionSizeGB = if ($versionSize) { [math]::Round($versionSize / 1GB, 4) } else { 0 }
        
        # Storage in different units
        $storageUsageCurrentMB = $_.StorageUsageCurrent  # Already in MB from SPO
        $storageUsageCurrentGB = if ($storageUsageCurrentMB) { [math]::Round($storageUsageCurrentMB / 1024, 2) } else { 0 }
        
        [PSCustomObject]@{
            Url                             = $_.Url
            Title                           = $_.Title
            SiteId                          = $_.SiteId
            StorageUsageCurrent             = $_.StorageUsageCurrent
            StorageUsageCurrentMB           = $storageUsageCurrentMB
            StorageUsageCurrentGB           = $storageUsageCurrentGB
            StorageQuota                    = $_.StorageQuota
            StorageQuotaWarningLevel        = $_.StorageQuotaWarningLevel
            VersionCount                    = $versionCount
            VersionSize                     = $versionSize
            VersionSizeMB                   = $versionSizeMB
            VersionSizeGB                   = $versionSizeGB
            VersionPolicyFileTypeOverride   = $_.VersionPolicyFileTypeOverride
            InheritVersionPolicyFromTenant  = $_.InheritVersionPolicyFromTenant
            CreatedTime                     = if ($_.CreatedTime) { $_.CreatedTime.ToString("o") } else { $null }
            LastContentModifiedDate         = if ($_.LastContentModifiedDate) { $_.LastContentModifiedDate.ToString("o") } else { $null }
            Status                          = $_.Status
            ArchiveStatus                   = $_.ArchiveStatus
            ArchivedBy                      = $_.ArchivedBy
            ArchivedTime                    = if ($_.ArchivedTime) { $_.ArchivedTime.ToString("o") } else { $null }
            ArchivedFileDiskUsed            = $_.ArchivedFileDiskUsed
            HubSiteId                       = $_.HubSiteId
            IsHubSite                       = $_.IsHubSite
            RelatedGroupId                  = $_.RelatedGroupId
            WebsCount                       = $_.WebsCount
            LocaleId                        = $_.LocaleId
            LockState                       = $_.LockState
            Owner                           = $_.Owner
            Template                        = $_.Template
            ExportedAt                      = (Get-Date).ToString("o")
        }
    }
    
    # Calculate totals from SPO data
    $totalVersions = ($dashboardSites | Measure-Object -Property VersionCount -Sum).Sum
    $totalVersionSizeGB = ($dashboardSites | Measure-Object -Property VersionSizeGB -Sum).Sum
    
    $jsonData = @{
        ExportDate         = (Get-Date).ToString("o")
        TotalSites         = $dashboardSites.Count
        TotalStorageGB     = [math]::Round($totalStorageGB, 2)
        TotalVersions      = if ($totalVersions) { $totalVersions } else { 0 }
        TotalVersionSizeGB = if ($totalVersionSizeGB) { [math]::Round($totalVersionSizeGB, 4) } else { 0 }
        Sites              = $dashboardSites
    }
    
    try {
        $jsonData | ConvertTo-Json -Depth 10 -Compress:$false | Out-File -FilePath $jsonOutputPath -Encoding UTF8
        Write-Host "[OK] Data saved to: $jsonOutputPath" -ForegroundColor Green
        Write-Host "     Size: $([math]::Round((Get-Item $jsonOutputPath).Length / 1KB, 2)) KB" -ForegroundColor Gray
        
        # Show version summary from SPO data
        $sitesWithVersions = @($dashboardSites | Where-Object { $_.VersionCount -gt 0 }).Count
        Write-Host "     Sites with versions: $sitesWithVersions" -ForegroundColor Gray
        Write-Host "     Total versions: $($totalVersions.ToString('N0'))" -ForegroundColor Gray
        Write-Host "     Total version size: $($totalVersionSizeGB.ToString('N2')) GB" -ForegroundColor Gray
    }
    catch {
        Write-Host "[ERROR] Failed to save JSON: $_" -ForegroundColor Red
    }
}
else {
    Write-Host "Skipped saving to AllSites.json" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Cyan
