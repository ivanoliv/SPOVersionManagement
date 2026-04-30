<#
.SYNOPSIS
    Builds a deploy package (ZIP) for SPO Version Management.

.DESCRIPTION
    Reads the AppVersion from config\AppPaths.json, copies all distributable files
    to a staging folder, and compresses them into a versioned ZIP file under deploy\.
    The ZIP includes the Install-SPOVersionManagement.ps1 installer at the root.

.EXAMPLE
    .\Build-DeployPackage.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- Read version (try AppPaths.json first, fall back to version.json) ---
$appPathsFile = Join-Path $scriptPath "config\AppPaths.json"
$versionFile = Join-Path $scriptPath "config\version.json"
if (Test-Path $appPathsFile) {
    $appPaths = Get-Content $appPathsFile -Raw | ConvertFrom-Json
    $version = $appPaths.AppVersion
} elseif (Test-Path $versionFile) {
    $ver = Get-Content $versionFile -Raw | ConvertFrom-Json
    $version = $ver.AppVersion
} else {
    Write-Error "Neither config\AppPaths.json nor config\version.json found."
    return
}
if (-not $version) {
    Write-Error "AppVersion not found. Ensure 'AppVersion' exists in config\AppPaths.json or config\version.json."
    return
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Build Deploy Package" -ForegroundColor Cyan
Write-Host "  Version: v$version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# --- Define files to include ---
# Scripts and modules (root level)
$rootFiles = @(
    "SPOVersionManagement.psm1",
    "SPOSiteFilters.psm1",
    "SPORetentionPolicyManager.psm1",
    "Start-SPOVersionManagement.ps1",
    "Start-Dashboard.ps1",
    "Connect-SPOFirst.ps1",
    "Export-AllSPOSites.ps1",
    "Get-SpoSitesVersion.ps1",
    "Rebuild-SiteExecutionHistory.ps1",
    "Reset-SPOVersionManagement.ps1",
    "Install-SPOVersionManagement.ps1",
    "Build-DeployPackage.ps1",
    "Import-SamInactiveSites.ps1",
    "Start-ArchiveWebsites.ps1",
    "IncludeSites.csv",
    "ExcludeSites.csv",
    "README.md",
    "ENTRA_ID_APP_SETUP.md"
)

# config/ directory files (JSON configs + databases)
$configFiles = @(
    "config\AppPaths.json",
    "config\version.json",
    "config\DashboardConfig.json",
    "config\ExtensionGroups.json"
)

# web/ directory files (Dashboard UI)
$webFiles = @(
    "web\Dashboard.html",
    "web\localization.js"
)

# app/ directory files (Windows App executable)
$appFiles = @(
    "app\SPOVersionManagement.exe",
    "app\SPOVersionManagement.exe.config",
    "app\Newtonsoft.Json.dll",
    "app\System.Management.Automation.dll"
)

# Folders to include entirely
$folders = @()

# --- Create staging directory ---
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zipName = "SPOVersionManagement_v${version}_${timestamp}.zip"
$stagingPath = Join-Path $env:TEMP "SPOVersionManagement_Build_$timestamp"
$stagingRoot = Join-Path $stagingPath "SPOVersionManagement"

Write-Host "Staging directory: $stagingPath" -ForegroundColor Gray

# Create directory structure
New-Item -Path $stagingRoot -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $stagingRoot "Logs") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $stagingRoot "config") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $stagingRoot "web") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $stagingRoot "app") -ItemType Directory -Force | Out-Null

# --- Copy root files ---
$copiedCount = 0
foreach ($file in $rootFiles) {
    $src = Join-Path $scriptPath $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination (Join-Path $stagingRoot $file) -Force
        $copiedCount++
        Write-Host "  [+] $file" -ForegroundColor Green
    } else {
        Write-Warning "  [-] $file not found, skipping"
    }
}

# --- Copy Logs files ---
foreach ($file in $configFiles + $webFiles + $appFiles) {
    $src = Join-Path $scriptPath $file
    if (Test-Path $src) {
        Copy-Item -Path $src -Destination (Join-Path $stagingRoot $file) -Force
        $copiedCount++
        Write-Host "  [+] $file" -ForegroundColor Green
    } else {
        Write-Warning "  [-] $file not found, skipping"
    }
}

# --- Sanitize AppPaths.json (remove secrets before packing) ---
$stagedAppPaths = Join-Path $stagingRoot "config\AppPaths.json"
if (Test-Path $stagedAppPaths) {
    $cfg = Get-Content $stagedAppPaths -Raw | ConvertFrom-Json
    # Clear tenant-specific secrets
    if ($cfg.EntraIdApp) {
        $cfg.EntraIdApp.TenantId = ""
        $cfg.EntraIdApp.ClientId = ""
        $cfg.EntraIdApp.CertificateThumbprint = ""
    }
    if ($cfg.PurviewApp) {
        $cfg.PurviewApp.ClientId = ""
        $cfg.PurviewApp.CertificateThumbprint = ""
        $cfg.PurviewApp.Organization = ""
    }
    $cfg.AdminUrl = ""
    $cfg.RootPath = ""
    $cfg.TelemetrySalt = ""
    $cfg | ConvertTo-Json -Depth 10 | Set-Content -Path $stagedAppPaths -Encoding UTF8
    Write-Host "  [Sanitized] config\AppPaths.json (secrets removed)" -ForegroundColor Cyan
}

# --- Copy folders ---
foreach ($folder in $folders) {
    $src = Join-Path $scriptPath $folder
    if (Test-Path $src) {
        $dest = Join-Path $stagingRoot $folder
        Copy-Item -Path $src -Destination $dest -Recurse -Force
        $itemCount = (Get-ChildItem -Path $dest -Recurse -File).Count
        $copiedCount += $itemCount
        Write-Host "  [+] $folder\ ($itemCount files)" -ForegroundColor Green
    } else {
        Write-Warning "  [-] $folder\ not found, skipping"
    }
}

# --- Create ZIP ---
$deployDir = Join-Path $scriptPath "deploy"
if (-not (Test-Path $deployDir)) {
    New-Item -Path $deployDir -ItemType Directory -Force | Out-Null
}
$zipPath = Join-Path $deployDir $zipName

Write-Host ""
Write-Host "Creating ZIP: $zipName" -ForegroundColor Yellow

Compress-Archive -Path "$stagingRoot" -DestinationPath $zipPath -CompressionLevel Optimal -Force

# --- Cleanup staging ---
Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue

# --- Summary ---
$zipSize = (Get-Item $zipPath).Length
$zipSizeMB = [math]::Round($zipSize / 1MB, 2)

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "  Package: deploy\$zipName" -ForegroundColor Green
Write-Host "  Size:    $zipSizeMB MB ($copiedCount files)" -ForegroundColor Green
Write-Host "  Version: v$version" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "To deploy:" -ForegroundColor Cyan
Write-Host "  1. Send the ZIP to the user" -ForegroundColor White
Write-Host "  2. User extracts and runs:" -ForegroundColor White
Write-Host "     .\SPOVersionManagement\Install-SPOVersionManagement.ps1" -ForegroundColor White
Write-Host ""
