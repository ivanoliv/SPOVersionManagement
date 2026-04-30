<#
.SYNOPSIS
    Helper script to connect to SharePoint Online before running version management
.EXAMPLE
    .\Connect-SPOFirst.ps1 -AdminUrl "https://m365x57757191-admin.sharepoint.com"
#>

param(
    [Parameter(Mandatory)]
    [string]$AdminUrl
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SharePoint Online Admin Connection" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# Check if the module is installed
if (-not (Get-Module -ListAvailable -Name "Microsoft.Online.SharePoint.PowerShell")) {
    Write-Host "Installing Microsoft.Online.SharePoint.PowerShell module..." -ForegroundColor Yellow
    Install-Module -Name Microsoft.Online.SharePoint.PowerShell -Force -AllowClobber -Scope CurrentUser
}

# Import the module
Import-Module Microsoft.Online.SharePoint.PowerShell -DisableNameChecking

# Try to connect
try {
    Write-Host "`nConnecting to: $AdminUrl" -ForegroundColor Yellow
    Write-Host "A login window will open..." -ForegroundColor Yellow
    
    # Disconnect previous session if it exists
    try {
        Disconnect-SPOService -ErrorAction SilentlyContinue
    }
    catch { }
    
    # Connect with interactive authentication
    Connect-SPOService -Url $AdminUrl
    
    # Test connection
    $tenant = Get-SPOTenant
    
    Write-Host "`n✓ Connection established successfully!" -ForegroundColor Green
    Write-Host "  Tenant: $($tenant.ResourceQuota) resources available" -ForegroundColor White
    
    # List some sites to confirm
    Write-Host "`nSites found (first 5):" -ForegroundColor Cyan
    Get-SPOSite -Limit 5 | Select-Object Url, StorageUsageCurrent | Format-Table -AutoSize
    
    Write-Host "`n========================================" -ForegroundColor Green
    Write-Host "  Ready! Now run the main script:" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host @"

.\Start-SPOVersionManagement.ps1 ``
    -AdminUrl "$AdminUrl" ``
    -Mode SyncThenDelete ``
    -MajorVersionLimit 20 ``
    -MaxConcurrentJobs 10

"@ -ForegroundColor White
}
catch {
    Write-Host "`n✗ Connection error: $_" -ForegroundColor Red
    Write-Host "`nPlease verify:" -ForegroundColor Yellow
    Write-Host "  1. The Admin URL is correct (should end with -admin.sharepoint.com)" -ForegroundColor White
    Write-Host "  2. You have SharePoint Administrator permissions" -ForegroundColor White
    Write-Host "  3. Your credentials are correct" -ForegroundColor White
}