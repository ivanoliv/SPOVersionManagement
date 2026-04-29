# Test-ExportSPOSites.ps1
# Diagnostic script: connects to SPO and exports Get-SPOSite raw output to CSV
# Use this to verify what properties SPO returns for your tenant

param(
    [Parameter(Mandatory = $false)]
    [string]$AdminUrl,

    [Parameter(Mandatory = $false)]
    [string]$TenantId,

    [Parameter(Mandatory = $false)]
    [string]$ClientId,

    [Parameter(Mandatory = $false)]
    [string]$CertificateThumbprint,

    [Parameter(Mandatory = $false)]
    [string]$OutputPath = (Join-Path $PSScriptRoot "Logs\SPOSites_Export.csv"),

    [switch]$Detailed
)

$scriptPath = $PSScriptRoot

Write-Host ""
Write-Host "=== SPO SITES EXPORT DIAGNOSTIC ===" -ForegroundColor Cyan
Write-Host ""

#region Load credentials from AppPaths.json if not provided
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
            if (-not $CertificateThumbprint -and $appPathsJson.EntraIdApp.CertificateThumbprint) {
                $CertificateThumbprint = $appPathsJson.EntraIdApp.CertificateThumbprint
                Write-Host "  [CONFIG] CertificateThumbprint loaded from AppPaths.json" -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Warning "Could not read EntraIdApp config from AppPaths.json: $_"
    }
}

# Derive AdminUrl from TenantId if not provided
if (-not $AdminUrl -and $appPathsJson) {
    # Try to infer from Organization or existing config
    $org = $appPathsJson.PurviewApp.Organization
    if ($org -match '^([^.]+)\.onmicrosoft\.com$') {
        $AdminUrl = "https://$($Matches[1])-admin.sharepoint.com"
        Write-Host "  [CONFIG] AdminUrl derived: $AdminUrl" -ForegroundColor Gray
    }
}
#endregion

#region Connect to SPO
Write-Host ""
if ($ClientId -and $CertificateThumbprint -and $TenantId) {
    Write-Host "Connecting to SPO via app-based auth..." -ForegroundColor Yellow
    Write-Host "  AdminUrl:    $AdminUrl" -ForegroundColor Gray
    Write-Host "  TenantId:    $($TenantId.Substring(0,8))..." -ForegroundColor Gray
    Write-Host "  ClientId:    $($ClientId.Substring(0,8))..." -ForegroundColor Gray
    Write-Host "  Thumbprint:  $($CertificateThumbprint.Substring(0,8))..." -ForegroundColor Gray
    Write-Host ""

    try {
        Import-Module Microsoft.Online.SharePoint.PowerShell -ErrorAction Stop
        Connect-SPOService -Url $AdminUrl -ClientId $ClientId -CertificateThumbprint $CertificateThumbprint -Tenant $TenantId
        Write-Host "  [OK] Connected to SPO" -ForegroundColor Green
    }
    catch {
        Write-Host "  [FAIL] Connection failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
else {
    Write-Host "Connecting to SPO interactively..." -ForegroundColor Yellow
    if (-not $AdminUrl) {
        Write-Error "AdminUrl is required. Use -AdminUrl parameter."
        exit 1
    }
    try {
        Import-Module Microsoft.Online.SharePoint.PowerShell -ErrorAction Stop
        Connect-SPOService -Url $AdminUrl
        Write-Host "  [OK] Connected to SPO" -ForegroundColor Green
    }
    catch {
        Write-Host "  [FAIL] Connection failed: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}
#endregion

#region Export sites
Write-Host ""
Write-Host "Retrieving all SPO sites..." -ForegroundColor Yellow

try {
    if ($Detailed) {
        $sites = Get-SPOSite -Limit All -Detailed
        Write-Host "  [OK] Retrieved $($sites.Count) sites (Detailed mode)" -ForegroundColor Green
    }
    else {
        $sites = Get-SPOSite -Limit All
        Write-Host "  [OK] Retrieved $($sites.Count) sites" -ForegroundColor Green
    }
}
catch {
    Write-Host "  [FAIL] Get-SPOSite failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Show properties available
$properties = $sites[0] | Get-Member -MemberType Property, NoteProperty | Select-Object -ExpandProperty Name
Write-Host ""
Write-Host "  Properties available ($($properties.Count)):" -ForegroundColor Cyan
$properties | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }

# Export to CSV
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$sites | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8
Write-Host ""
Write-Host "  [OK] Exported to: $OutputPath" -ForegroundColor Green
Write-Host "  [OK] Total sites: $($sites.Count)" -ForegroundColor Green
Write-Host ""

# Show summary table
Write-Host "=== SITE SUMMARY ===" -ForegroundColor Cyan
$sites | Format-Table -Property Url, Title, StorageUsageCurrent, LastContentModifiedDate, LockState, Status -AutoSize | Out-String | Write-Host
#endregion
