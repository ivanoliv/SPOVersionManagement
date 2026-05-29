<#
.SYNOPSIS
    Installs or updates SPO Version Management on the local machine.

.DESCRIPTION
    Copies all script and module files to the destination folder.
    Preserves user-specific configuration files (AppPaths.json, DashboardConfig.json,
    IncludeSites.csv, ExcludeSites.csv) if they already exist at the destination,
    merging new config keys when possible.

    Run this script from inside the extracted ZIP folder (SPOVersionManagement\).

.PARAMETER DestinationPath
    The target installation folder. Defaults to the script's own directory.

.PARAMETER Force
    Overwrite all files including user configs (creates backups first).

.EXAMPLE
    # Fresh install or update (preserves configs)
    .\Install-SPOVersionManagement.ps1

.EXAMPLE
    # Install to custom path
    .\Install-SPOVersionManagement.ps1 -DestinationPath "D:\Tools\SPOVersionManagement"

.EXAMPLE
    # Force overwrite all files (backs up configs first)
    .\Install-SPOVersionManagement.ps1 -Force
#>

[CmdletBinding()]
param(
    [string]$DestinationPath = (Split-Path -Parent $MyInvocation.MyCommand.Path),
    [switch]$Force,
    [switch]$NonInteractive
)

$ErrorActionPreference = "Stop"
$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path

# --- Detect same-directory install ---
$resolvedSrc  = (Resolve-Path $sourcePath).Path.TrimEnd('\')
$resolvedDest = if (Test-Path $DestinationPath) { (Resolve-Path $DestinationPath).Path.TrimEnd('\') } else { $DestinationPath.TrimEnd('\') }
$isSameDir = $resolvedSrc -eq $resolvedDest

# --- Read version ---
$appPathsFile = Join-Path $sourcePath "config\AppPaths.json"
$version = "unknown"
if (Test-Path $appPathsFile) {
    $appPaths = Get-Content $appPathsFile -Raw | ConvertFrom-Json
    if ($appPaths.AppVersion) { $version = $appPaths.AppVersion }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SPO Version Management Installer" -ForegroundColor Cyan
Write-Host "  Version: v$version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Source:      $sourcePath" -ForegroundColor White
Write-Host "  Destination: $DestinationPath" -ForegroundColor White
Write-Host ""

$isUpdate = Test-Path $DestinationPath
if ($isUpdate) {
    Write-Host "  Mode: UPDATE (existing installation detected)" -ForegroundColor Yellow
} else {
    Write-Host "  Mode: FRESH INSTALL" -ForegroundColor Green
}
Write-Host ""

# --- Confirm ---
if (-not $NonInteractive) {
    $confirm = Read-Host "Proceed with installation? (Y/N)"
    if ($confirm -notmatch '^[Yy]') {
        Write-Host "Installation cancelled." -ForegroundColor Yellow
        return
    }
}

# --- Files that should ALWAYS be updated (scripts, modules, dashboard) ---
$alwaysUpdate = @(
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
    "README.md",
    "ENTRA_ID_APP_SETUP.md",
    "web\Dashboard.html",
    "web\localization.js",
    "config\ExtensionGroups.json"
)

# --- Files to preserve if they exist at destination (user configs) ---
$preserveFiles = @(
    "IncludeSites.csv",
    "ExcludeSites.csv",
    "config\TelemetrySentLog.json"
)

# --- Folders to always update ---
$updateFolders = @("app")

# --- Create destination structure ---
New-Item -Path $DestinationPath -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $DestinationPath "Logs") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $DestinationPath "Logs\Backup") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $DestinationPath "config") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $DestinationPath "web") -ItemType Directory -Force | Out-Null
New-Item -Path (Join-Path $DestinationPath "app") -ItemType Directory -Force | Out-Null

$updatedCount = 0
$preservedCount = 0
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

# --- Update script and module files ---
Write-Host ""
Write-Host "Updating files..." -ForegroundColor Yellow
foreach ($file in $alwaysUpdate) {
    $src = Join-Path $sourcePath $file
    $dst = Join-Path $DestinationPath $file
    if (Test-Path $src) {
        # Skip if source and destination are the same file
        if ($isSameDir) {
            Write-Host "  [Skipped] $file (in-place install)" -ForegroundColor DarkGray
            continue
        }
        # Ensure target directory exists
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path $dstDir)) {
            New-Item -Path $dstDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -Path $src -Destination $dst -Force
        $updatedCount++
        Write-Host "  [Updated] $file" -ForegroundColor Green
    }
}

# --- Handle preserved files ---
Write-Host ""
Write-Host "Checking user configs..." -ForegroundColor Yellow
foreach ($file in $preserveFiles) {
    $src = Join-Path $sourcePath $file
    $dst = Join-Path $DestinationPath $file
    
    if ((Test-Path $dst) -and -not $Force) {
        $preservedCount++
        Write-Host "  [Kept] $file (user config preserved)" -ForegroundColor Cyan
    } elseif (Test-Path $src) {
        if ($isSameDir) {
            Write-Host "  [Skipped] $file (in-place install)" -ForegroundColor DarkGray
            continue
        }
        if ((Test-Path $dst) -and $Force) {
            # Backup existing before overwriting
            $backupName = [System.IO.Path]::GetFileNameWithoutExtension($file) + "_backup_$timestamp" + [System.IO.Path]::GetExtension($file)
            $backupPath = Join-Path $DestinationPath "Logs\Backup\$backupName"
            Copy-Item -Path $dst -Destination $backupPath -Force
            Write-Host "  [Backup] $file -> Logs\Backup\$backupName" -ForegroundColor DarkYellow
        }
        $dstDir = Split-Path -Parent $dst
        if (-not (Test-Path $dstDir)) {
            New-Item -Path $dstDir -ItemType Directory -Force | Out-Null
        }
        Copy-Item -Path $src -Destination $dst -Force
        $updatedCount++
        Write-Host "  [Installed] $file" -ForegroundColor Green
    }
}

# --- Handle AppPaths.json (merge new keys) ---
Write-Host ""
Write-Host "Updating AppPaths.json..." -ForegroundColor Yellow
$srcAppPaths = Join-Path $sourcePath "config\AppPaths.json"
$dstAppPaths = Join-Path $DestinationPath "config\AppPaths.json"

if ((Test-Path $dstAppPaths) -and -not $Force) {
    # Merge: update AppVersion and add any new keys, but keep user values
    $srcConfig = Get-Content $srcAppPaths -Raw | ConvertFrom-Json
    $dstConfig = Get-Content $dstAppPaths -Raw | ConvertFrom-Json
    
    # Always update these fields (use Add-Member -Force in case they don't exist yet)
    $dstConfig | Add-Member -NotePropertyName 'AppVersion' -NotePropertyValue $srcConfig.AppVersion -Force
    $dstConfig | Add-Member -NotePropertyName 'Version' -NotePropertyValue $srcConfig.Version -Force
    
    # Add new top-level keys that don't exist in destination
    $srcConfig.PSObject.Properties | ForEach-Object {
        if (-not $dstConfig.PSObject.Properties[$_.Name]) {
            $dstConfig | Add-Member -NotePropertyName $_.Name -NotePropertyValue $_.Value
            Write-Host "  [Added] New config key: $($_.Name)" -ForegroundColor DarkCyan
        }
    }
    
    # Merge all nested objects: add missing sub-keys while preserving user values
    # This covers: Files, Scripts, Directories, InputFiles, RelativePaths, EntraIdApp, PurviewApp
    foreach ($prop in $srcConfig.PSObject.Properties) {
        if ($prop.Value -is [PSCustomObject] -and $dstConfig.PSObject.Properties[$prop.Name] -and $dstConfig.($prop.Name) -is [PSCustomObject]) {
            $prop.Value.PSObject.Properties | ForEach-Object {
                if (-not $dstConfig.($prop.Name).PSObject.Properties[$_.Name]) {
                    $dstConfig.($prop.Name) | Add-Member -NotePropertyName $_.Name -NotePropertyValue $_.Value
                    Write-Host "  [Added] New $($prop.Name) entry: $($_.Name)" -ForegroundColor DarkCyan
                }
            }
        }
    }
    
    # Backup existing
    $backupName = "AppPaths_backup_$timestamp.json"
    Copy-Item -Path $dstAppPaths -Destination (Join-Path $DestinationPath "Logs\Backup\$backupName") -Force
    
    # Always update RootPath to actual install location
    $dstConfig | Add-Member -NotePropertyName 'RootPath' -NotePropertyValue $DestinationPath -Force

    # Save merged config
    $dstConfig | ConvertTo-Json -Depth 10 | Set-Content -Path $dstAppPaths -Encoding UTF8
    Write-Host "  [Merged] AppPaths.json (v$version, user paths preserved)" -ForegroundColor Green
} else {
    if ($isSameDir) {
        Write-Host "  [Skipped] AppPaths.json (in-place install)" -ForegroundColor DarkGray
    } else {
        if ((Test-Path $dstAppPaths) -and $Force) {
            $backupName = "AppPaths_backup_$timestamp.json"
            Copy-Item -Path $dstAppPaths -Destination (Join-Path $DestinationPath "Logs\Backup\$backupName") -Force
            Write-Host "  [Backup] AppPaths.json -> Logs\Backup\$backupName" -ForegroundColor DarkYellow
        }
        Copy-Item -Path $srcAppPaths -Destination $dstAppPaths -Force
        # Set RootPath to actual install location
        $freshConfig = Get-Content $dstAppPaths -Raw | ConvertFrom-Json
        $freshConfig | Add-Member -NotePropertyName 'RootPath' -NotePropertyValue $DestinationPath -Force
        $freshConfig | ConvertTo-Json -Depth 10 | Set-Content -Path $dstAppPaths -Encoding UTF8
        $updatedCount++
        Write-Host "  [Installed] AppPaths.json" -ForegroundColor Green
    }
}

# --- Handle DashboardConfig.json (merge new keys) ---
Write-Host ""
Write-Host "Updating DashboardConfig.json..." -ForegroundColor Yellow
$srcDashConfig = Join-Path $sourcePath "config\DashboardConfig.json"
$dstDashConfig = Join-Path $DestinationPath "config\DashboardConfig.json"

if ((Test-Path $dstDashConfig) -and -not $Force) {
    $srcDC = Get-Content $srcDashConfig -Raw | ConvertFrom-Json
    $dstDC = Get-Content $dstDashConfig -Raw | ConvertFrom-Json
    
    # Always update schema version
    $dstDC | Add-Member -NotePropertyName 'Version' -NotePropertyValue $srcDC.Version -Force
    
    # Add new keys that don't exist in destination (keep user values for existing keys)
    $srcDC.PSObject.Properties | ForEach-Object {
        if (-not $dstDC.PSObject.Properties[$_.Name]) {
            $dstDC | Add-Member -NotePropertyName $_.Name -NotePropertyValue $_.Value
            Write-Host "  [Added] New config key: $($_.Name)" -ForegroundColor DarkCyan
        }
    }
    
    # Merge nested objects (e.g., Currency, ExchangeRate)
    foreach ($prop in $srcDC.PSObject.Properties) {
        if ($prop.Value -is [PSCustomObject] -and $dstDC.PSObject.Properties[$prop.Name]) {
            $prop.Value.PSObject.Properties | ForEach-Object {
                if (-not $dstDC.($prop.Name).PSObject.Properties[$_.Name]) {
                    $dstDC.($prop.Name) | Add-Member -NotePropertyName $_.Name -NotePropertyValue $_.Value
                    Write-Host "  [Added] New $($prop.Name) entry: $($_.Name)" -ForegroundColor DarkCyan
                }
            }
        }
    }
    
    # Backup existing
    $backupName = "DashboardConfig_backup_$timestamp.json"
    Copy-Item -Path $dstDashConfig -Destination (Join-Path $DestinationPath "Logs\Backup\$backupName") -Force
    
    $dstDC | ConvertTo-Json -Depth 10 | Set-Content -Path $dstDashConfig -Encoding UTF8
    Write-Host "  [Merged] DashboardConfig.json (user preferences preserved)" -ForegroundColor Green
} else {
    if ($isSameDir) {
        Write-Host "  [Skipped] DashboardConfig.json (in-place install)" -ForegroundColor DarkGray
    } else {
        if ((Test-Path $dstDashConfig) -and $Force) {
            $backupName = "DashboardConfig_backup_$timestamp.json"
            Copy-Item -Path $dstDashConfig -Destination (Join-Path $DestinationPath "Logs\Backup\$backupName") -Force
            Write-Host "  [Backup] DashboardConfig.json -> Logs\Backup\$backupName" -ForegroundColor DarkYellow
        }
        Copy-Item -Path $srcDashConfig -Destination $dstDashConfig -Force
        $updatedCount++
        Write-Host "  [Installed] DashboardConfig.json" -ForegroundColor Green
    }
}

# --- Update folders (handle locked files from running app) ---
Write-Host ""
foreach ($folder in $updateFolders) {
    $src = Join-Path $sourcePath $folder
    $dst = Join-Path $DestinationPath $folder
    if (Test-Path $src) {
        if ($isSameDir) {
            Write-Host "  [Skipped] $folder\ (in-place install)" -ForegroundColor DarkGray
            continue
        }
        
        # Ensure target directory exists
        if (-not (Test-Path $dst)) {
            New-Item -Path $dst -ItemType Directory -Force | Out-Null
        }
        
        $lockedFiles = @()
        $copiedCount = 0
        
        # Copy each file individually to handle locked files gracefully
        $srcFiles = Get-ChildItem -Path $src -Recurse -File
        foreach ($srcFile in $srcFiles) {
            $relativePath = $srcFile.FullName.Substring($src.Length + 1)
            $dstFile = Join-Path $dst $relativePath
            $dstDir = Split-Path -Parent $dstFile
            if (-not (Test-Path $dstDir)) {
                New-Item -Path $dstDir -ItemType Directory -Force | Out-Null
            }
            try {
                Copy-Item -Path $srcFile.FullName -Destination $dstFile -Force -ErrorAction Stop
                $copiedCount++
            }
            catch {
                # File is locked (app running) — stage for update on next restart
                $pendingFile = $dstFile + ".pending"
                try {
                    Copy-Item -Path $srcFile.FullName -Destination $pendingFile -Force
                    $lockedFiles += $relativePath
                }
                catch {
                    Write-Host "  [WARN] Cannot update: $relativePath (file locked)" -ForegroundColor Yellow
                }
            }
        }
        
        $updatedCount += $copiedCount
        
        if ($lockedFiles.Count -gt 0) {
            Write-Host "  [Updated] $folder\ ($copiedCount files updated, $($lockedFiles.Count) pending restart)" -ForegroundColor Yellow
            Write-Host "  [INFO] The following files are locked (app is running):" -ForegroundColor DarkYellow
            foreach ($lf in $lockedFiles) {
                Write-Host "    - $lf (staged as .pending)" -ForegroundColor DarkYellow
            }
            Write-Host "  [INFO] Close the app and re-run the installer, or restart to apply." -ForegroundColor DarkYellow
            
            # Create a small script that applies pending files on next launch
            $pendingScript = Join-Path $dst "apply-pending.ps1"
            $pendingContent = @"
# Auto-generated: applies pending file updates after app restart
`$appDir = Split-Path -Parent `$MyInvocation.MyCommand.Path
Get-ChildItem -Path `$appDir -Filter '*.pending' -Recurse | ForEach-Object {
    `$target = `$_.FullName -replace '\.pending$', ''
    try {
        Move-Item -Path `$_.FullName -Destination `$target -Force
    } catch { }
}
Remove-Item `$MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
"@
            $pendingContent | Set-Content -Path $pendingScript -Encoding UTF8
        } else {
            Write-Host "  [Updated] $folder\ ($copiedCount files)" -ForegroundColor Green
        }
    }
}

# --- Summary ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Installation Complete!" -ForegroundColor Green
Write-Host "  Version:   v$version" -ForegroundColor Green
Write-Host "  Updated:   $updatedCount files" -ForegroundColor Green
Write-Host "  Preserved: $preservedCount user configs" -ForegroundColor Green
Write-Host "  Location:  $DestinationPath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

if ($isUpdate) {
    Write-Host "Your existing configuration and execution history were preserved." -ForegroundColor Cyan
    Write-Host "Backups saved to: $DestinationPath\Logs\Backup\" -ForegroundColor Cyan
} else {
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Edit config\AppPaths.json with your tenant paths" -ForegroundColor White
    Write-Host "  2. Edit config\DashboardConfig.json for your preferences" -ForegroundColor White
    Write-Host "  3. Run .\Start-SPOVersionManagement.ps1 -AdminUrl https://yourtenant-admin.sharepoint.com" -ForegroundColor White
}
Write-Host ""
