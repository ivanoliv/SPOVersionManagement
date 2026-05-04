<#
.SYNOPSIS
    Builds deploy packages (ZIP) for SPO Version Management.

.DESCRIPTION
    Reads the AppVersion from config\AppPaths.json, copies all distributable files
    to a staging folder, sanitizes secrets/data from JSON and CSV files, and compresses
    them into versioned ZIP files under deploy\.

    Produces TWO packages:
    - Standalone (_standalone.zip): Self-contained single-file .exe (no .NET required)
    - Standard  (_standard.zip):   Framework-dependent (requires .NET 10 Desktop Runtime)

.PARAMETER PackageType
    Which package(s) to build: 'Both' (default), 'Standalone', or 'Standard'.

.EXAMPLE
    .\Build-DeployPackage.ps1
    .\Build-DeployPackage.ps1 -PackageType Standalone
#>

[CmdletBinding()]
param(
    [ValidateSet('Both','Standalone','Standard')]
    [string]$PackageType = 'Both'
)

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

# app/ directory files (Windows App executable — populated by dotnet publish)
$appFiles = @()  # Built dynamically per package type

# --- Helper: Sanitize a staged package folder ---
function Invoke-SanitizePackage {
    param([string]$StagingRoot)

    # Sanitize AppPaths.json (remove all secrets and tenant data)
    $stagedAppPaths = Join-Path $StagingRoot "config\AppPaths.json"
    if (Test-Path $stagedAppPaths) {
        $cfg = Get-Content $stagedAppPaths -Raw | ConvertFrom-Json
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
        if ($cfg.PSObject.Properties['AdminUrl']) { $cfg.AdminUrl = "" }
        if ($cfg.PSObject.Properties['RootPath']) { $cfg.RootPath = "" }
        if ($cfg.PSObject.Properties['LastModified']) { $cfg.LastModified = "" }
        if ($cfg.PSObject.Properties['TelemetrySalt']) { $cfg.TelemetrySalt = "" }
        if ($cfg.PSObject.Properties['TelemetryConsentRequested']) { $cfg.TelemetryConsentRequested = $false }
        if ($cfg.PSObject.Properties['TelemetryConsentRequestedAt']) { $cfg.TelemetryConsentRequestedAt = "" }
        $cfg | ConvertTo-Json -Depth 10 | Set-Content -Path $stagedAppPaths -Encoding UTF8
        Write-Host "  [Sanitized] config\AppPaths.json" -ForegroundColor Cyan
    }

    # Sanitize DashboardConfig.json
    $dcPath = Join-Path $StagingRoot "config\DashboardConfig.json"
    if (Test-Path $dcPath) {
        $dc = Get-Content $dcPath -Raw | ConvertFrom-Json
        if ($dc.PSObject.Properties['AdminUrl']) { $dc.AdminUrl = "" }
        if ($dc.PSObject.Properties['LastAdminUrl']) { $dc.LastAdminUrl = "" }
        $dc | ConvertTo-Json -Depth 10 | Set-Content $dcPath -Encoding UTF8
        Write-Host "  [Sanitized] config\DashboardConfig.json" -ForegroundColor Cyan
    }

    # Clear data-only JSON files (arrays)
    foreach ($f in @("AllSites.json","ArchiveAnalysis.json","ArchiveQueue.json","ExcludedSites.json",
                     "FileArchiveQueue.json","SessionHistory.json","TenantStorageTimeline.json",
                     "RetentionPolicyDatabase.json","RetentionPolicyLog.json")) {
        $p = Join-Path $StagingRoot "config\$f"
        if (Test-Path $p) { '[]' | Set-Content $p -Encoding UTF8 }
    }
    # Clear data-only JSON files (objects)
    foreach ($f in @("SiteExecutionHistory.json","JobStatus.json","TenantStorage.json")) {
        $p = Join-Path $StagingRoot "config\$f"
        if (Test-Path $p) { '{}' | Set-Content $p -Encoding UTF8 }
    }

    # CSV files — headers only
    $csvExclude = Join-Path $StagingRoot "ExcludeSites.csv"
    $csvInclude = Join-Path $StagingRoot "IncludeSites.csv"
    if (Test-Path $csvExclude) { "Url" | Set-Content $csvExclude -Encoding UTF8 }
    if (Test-Path $csvInclude) { "Url" | Set-Content $csvInclude -Encoding UTF8 }
    Write-Host "  [Sanitized] CSV files (headers only)" -ForegroundColor Cyan
    Write-Host "  [Sanitized] Data JSON files (cleared)" -ForegroundColor Cyan
}

# --- Helper: Build a single package ---
function Build-Package {
    param(
        [string]$Type,  # 'Standalone' or 'Standard'
        [string]$Version,
        [string]$Timestamp,
        [string]$ScriptPath,
        [string[]]$RootFiles,
        [string[]]$ConfigFiles,
        [string[]]$WebFiles
    )

    $suffix = $Type.ToLower()
    $zipName = "SPOVersionManagement_v${Version}_${Timestamp}_${suffix}.zip"
    $stagingPath = Join-Path $env:TEMP "SPOVersionManagement_Build_${Timestamp}_${suffix}"
    $stagingRoot = Join-Path $stagingPath "SPOVersionManagement"

    Write-Host ""
    Write-Host "--- Building $Type package ---" -ForegroundColor Yellow

    # Create directory structure
    New-Item -Path $stagingRoot -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path $stagingRoot "Logs") -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path $stagingRoot "config") -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path $stagingRoot "web") -ItemType Directory -Force | Out-Null
    New-Item -Path (Join-Path $stagingRoot "app") -ItemType Directory -Force | Out-Null

    # Copy root files
    $copiedCount = 0
    foreach ($file in $RootFiles) {
        $src = Join-Path $ScriptPath $file
        if (Test-Path $src) {
            Copy-Item -Path $src -Destination (Join-Path $stagingRoot $file) -Force
            $copiedCount++
            Write-Host "  [+] $file" -ForegroundColor Green
        } else {
            Write-Warning "  [-] $file not found, skipping"
        }
    }

    # Copy config and web files
    foreach ($file in $ConfigFiles + $WebFiles) {
        $src = Join-Path $ScriptPath $file
        if (Test-Path $src) {
            Copy-Item -Path $src -Destination (Join-Path $stagingRoot $file) -Force
            $copiedCount++
            Write-Host "  [+] $file" -ForegroundColor Green
        } else {
            Write-Warning "  [-] $file not found, skipping"
        }
    }

    # Build and copy app executable
    $projPath = Join-Path $ScriptPath "src\SPOVersionManagement\SPOVersionManagement.csproj"
    $appOutDir = Join-Path $stagingRoot "app"

    if ($Type -eq 'Standalone') {
        Write-Host "  Building self-contained single-file..." -ForegroundColor Gray
        & dotnet publish $projPath -c Release -r win-x64 --self-contained `
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
            -o $appOutDir --nologo -v quiet 2>&1 | Out-Null
    } else {
        Write-Host "  Building framework-dependent..." -ForegroundColor Gray
        & dotnet publish $projPath -c Release --no-self-contained `
            -o $appOutDir --nologo -v quiet 2>&1 | Out-Null
    }

    $exePath = Join-Path $appOutDir "SPOVersionManagement.exe"
    if (Test-Path $exePath) {
        $appCount = (Get-ChildItem $appOutDir -Recurse -File).Count
        $copiedCount += $appCount
        Write-Host "  [+] app\ ($appCount files)" -ForegroundColor Green
    } else {
        Write-Warning "  [-] dotnet publish failed — app\SPOVersionManagement.exe not found"
    }

    # Sanitize the staged copy (secrets, data, CSVs)
    Write-Host "  Sanitizing package contents..." -ForegroundColor Gray
    Invoke-SanitizePackage -StagingRoot $stagingRoot

    # Create ZIP
    $deployDir = Join-Path $ScriptPath "deploy"
    if (-not (Test-Path $deployDir)) { New-Item -Path $deployDir -ItemType Directory -Force | Out-Null }
    $zipPath = Join-Path $deployDir $zipName

    Write-Host "  Creating ZIP: $zipName" -ForegroundColor Yellow
    Compress-Archive -Path "$stagingRoot" -DestinationPath $zipPath -CompressionLevel Optimal -Force

    # Cleanup staging
    Remove-Item -Path $stagingPath -Recurse -Force -ErrorAction SilentlyContinue

    # Report
    $zipSize = (Get-Item $zipPath).Length
    $zipSizeMB = [math]::Round($zipSize / 1MB, 2)
    Write-Host "  Done: $zipSizeMB MB ($copiedCount files)" -ForegroundColor Green

    return $zipPath
}

# --- Create staging directory ---
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$zipName = "SPOVersionManagement_v${version}_${timestamp}.zip"
$deployDir = Join-Path $scriptPath "deploy"

# --- Build packages ---
$results = @()

if ($PackageType -in @('Both','Standalone')) {
    $results += Build-Package -Type 'Standalone' -Version $version -Timestamp $timestamp `
        -ScriptPath $scriptPath -RootFiles $rootFiles -ConfigFiles $configFiles -WebFiles $webFiles
}

if ($PackageType -in @('Both','Standard')) {
    $results += Build-Package -Type 'Standard' -Version $version -Timestamp $timestamp `
        -ScriptPath $scriptPath -RootFiles $rootFiles -ConfigFiles $configFiles -WebFiles $webFiles
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build Complete!" -ForegroundColor Green
Write-Host "  Version: v$version" -ForegroundColor Green
foreach ($zip in $results) {
    $name = Split-Path $zip -Leaf
    $sizeMB = [math]::Round((Get-Item $zip).Length / 1MB, 2)
    Write-Host "  Package: deploy\$name ($sizeMB MB)" -ForegroundColor Green
}
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "To deploy:" -ForegroundColor Cyan
Write-Host "  1. Send the ZIP to the user" -ForegroundColor White
Write-Host "  2. User extracts and runs:" -ForegroundColor White
Write-Host "     .\SPOVersionManagement\Install-SPOVersionManagement.ps1" -ForegroundColor White
Write-Host ""
