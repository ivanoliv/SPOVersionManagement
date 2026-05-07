<#
.SYNOPSIS
    Searches SharePoint files by extension categories using Graph Search API.
    Adapted from SharePoint Files Identifier for SPO Version Management.
.DESCRIPTION
    Uses Microsoft Graph Search API (KQL) to find files matching extension groups
    across a single site. Supports both interactive and certificate-based auth via PnP.PowerShell.
    Results are exported to per-site JSON files in Logs/FileArchive/.
.PARAMETER SiteUrl
    The SharePoint site URL to search.
.PARAMETER AdminUrl
    The SharePoint admin URL (for PnP connection context).
.PARAMETER Categories
    Hashtable of category name → extensions array. If not provided, reads from ExtensionGroups.json.
.PARAMETER SummaryOnly
    Only return file counts, skip detailed file export.
.PARAMETER OutputPath
    Folder to write results (default: .\Logs\FileArchive).
.PARAMETER UseInteractiveLogin
    Use browser-based interactive login instead of certificate auth.
.PARAMETER ClientId
    Entra ID App Registration Client ID (for certificate auth).
.PARAMETER CertificateThumbprint
    Certificate thumbprint (for certificate auth).
.PARAMETER TenantId
    Tenant GUID (for certificate auth).
.PARAMETER Region
    Geographic region for Graph Search API (NAM, EUR, BRA, etc.).
.EXAMPLE
    .\Start-FileArchiveSearch.ps1 -SiteUrl "https://contoso.sharepoint.com/sites/Finance" -UseInteractiveLogin
.EXAMPLE
    .\Start-FileArchiveSearch.ps1 -SiteUrl "https://contoso.sharepoint.com/sites/HR" -ClientId "..." -CertificateThumbprint "..." -TenantId "..."
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$SiteUrl,

    [string]$AdminUrl,

    [hashtable]$Categories,

    [switch]$SummaryOnly,

    [string]$OutputPath,

    [switch]$UseInteractiveLogin,

    [string]$ClientId,
    [string]$CertificateThumbprint,
    [string]$TenantId,
    [string]$Region = "NAM",

    [string]$PnpClientId = ""
)

$ErrorActionPreference = "Continue"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# ── Setup paths ────────────────────────────────────────────────────
if (-not $OutputPath) { $OutputPath = Join-Path $scriptRoot "Logs\FileArchive" }
if (-not (Test-Path $OutputPath)) { New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null }

# ── Load extension groups from config if not provided ──────────────
if (-not $Categories -or $Categories.Count -eq 0) {
    $extGroupsPath = Join-Path $scriptRoot "config\ExtensionGroups.json"
    if (Test-Path $extGroupsPath) {
        try {
            $extConfig = Get-Content $extGroupsPath -Raw | ConvertFrom-Json
            $Categories = @{}
            foreach ($g in $extConfig.Groups) {
                if ($g.Enabled) {
                    $Categories[$g.Name] = @($g.Extensions)
                }
            }
            Write-Host "  Loaded $($Categories.Count) extension groups from config" -ForegroundColor Gray
        } catch {
            Write-Warning "Could not load ExtensionGroups.json: $_"
        }
    }
}

if (-not $Categories -or $Categories.Count -eq 0) {
    # Fallback defaults
    $Categories = @{
        "Video"  = @('.mp4', '.mov', '.wmv', '.avi', '.mkv', '.m4v', '.mpg', '.mpeg', '.3gp', '.3g2', '.mts', '.m2ts')
        "Audio"  = @('.mp3', '.wav', '.wma', '.aac', '.flac', '.m4a', '.ogg')
        "Image"  = @('.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tiff', '.tif', '.svg', '.ico', '.webp')
        "Design" = @('.psd', '.ai', '.indd', '.sketch', '.fig', '.xd')
        "CAD"    = @('.dwg', '.dxf', '.step', '.stp', '.iges', '.igs', '.stl')
    }
}

# ── Determine root URL from site URL ──────────────────────────────
$siteUri = [System.Uri]$SiteUrl
$rootUrl = "$($siteUri.Scheme)://$($siteUri.Host)"

# ── Compute site hash for per-site file ───────────────────────────
$siteKey = $SiteUrl.TrimEnd('/').ToLower()
$siteHash = [System.BitConverter]::ToString(
    [System.Security.Cryptography.SHA256]::Create().ComputeHash(
        [System.Text.Encoding]::UTF8.GetBytes($siteKey)
    )
).Replace('-','').Substring(0,12).ToLower()

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  FILE ARCHIVE SEARCH" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Site: $SiteUrl" -ForegroundColor White
Write-Host "  Categories: $($Categories.Keys -join ', ')" -ForegroundColor Gray
Write-Host "  Mode: $(if ($SummaryOnly) { 'Summary only' } else { 'Full details' })" -ForegroundColor Gray
Write-Host ""

# ── Connect to site ───────────────────────────────────────────────
Write-Host "  Connecting to SharePoint..." -ForegroundColor Cyan

# ── Auto-load credentials from AppPaths.json if not passed ────────
$appPathsFile = Join-Path $scriptRoot "config\AppPaths.json"
if (Test-Path $appPathsFile) {
    $appPaths = Get-Content $appPathsFile -Raw | ConvertFrom-Json

    # PnpClientId: prefer PnPApp.ClientId, then EntraIdApp.ClientId
    if (-not $PnpClientId) {
        if ($appPaths.PnPApp -and $appPaths.PnPApp.ClientId) { $PnpClientId = $appPaths.PnPApp.ClientId }
        elseif ($appPaths.EntraIdApp -and $appPaths.EntraIdApp.ClientId) { $PnpClientId = $appPaths.EntraIdApp.ClientId }
    }

    # Certificate auth creds: prefer PnPApp, then EntraIdApp
    if (-not $ClientId) {
        if ($appPaths.PnPApp -and $appPaths.PnPApp.ClientId) { $ClientId = $appPaths.PnPApp.ClientId }
        elseif ($appPaths.EntraIdApp -and $appPaths.EntraIdApp.ClientId) { $ClientId = $appPaths.EntraIdApp.ClientId }
    }
    if (-not $CertificateThumbprint) {
        if ($appPaths.PnPApp -and $appPaths.PnPApp.CertificateThumbprint) { $CertificateThumbprint = $appPaths.PnPApp.CertificateThumbprint }
        elseif ($appPaths.EntraIdApp -and $appPaths.EntraIdApp.CertificateThumbprint) { $CertificateThumbprint = $appPaths.EntraIdApp.CertificateThumbprint }
    }
    if (-not $TenantId -and $appPaths.EntraIdApp -and $appPaths.EntraIdApp.TenantId) {
        $TenantId = $appPaths.EntraIdApp.TenantId
    }
}

try {
    if ($UseInteractiveLogin) {
        if (-not $PnpClientId) {
            Write-Host "  [ERROR] Interactive login requires a PnP App ClientId." -ForegroundColor Red
            Write-Host "  Configure in AppPaths.json PnPApp.ClientId or EntraIdApp.ClientId, or pass -PnpClientId." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "  Using interactive login (PnpClientId: $($PnpClientId.Substring(0,8))...)" -ForegroundColor Gray
        Connect-PnPOnline -Url $rootUrl -Interactive -ClientId $PnpClientId
    } else {
        if (-not $ClientId -or -not $CertificateThumbprint -or -not $TenantId) {
            Write-Host "  [ERROR] Certificate auth requires ClientId, CertificateThumbprint, and TenantId." -ForegroundColor Red
            Write-Host "  Configure in AppPaths.json PnPApp or EntraIdApp section, or use -UseInteractiveLogin." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "  Using certificate auth (ClientId: $($ClientId.Substring(0,8))...)" -ForegroundColor Gray
        Connect-PnPOnline -Url $rootUrl -ClientId $ClientId -Thumbprint $CertificateThumbprint -Tenant $TenantId
    }
    Write-Host "  Connected [OK]" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Connection failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# ── Search functions (from SharePoint Files Identifier) ───────────

function Search-FilesByExtensionLocal {
    param (
        [string]$SearchSiteUrl,
        [string[]]$Extensions,
        [string]$Category
    )

    $searchQuery_RestAPI = "https://graph.microsoft.com/beta/search/query"
    $extFilter = ($Extensions | ForEach-Object { "filetype:$($_.TrimStart('.'))" }) -join " OR "

    if ($SearchSiteUrl.TrimEnd('/') -ieq $rootUrl.TrimEnd('/')) {
        $path = "path:\`"$SearchSiteUrl\`" AND NOT path:\`"$rootUrl/sites\`""
    } else {
        $path = "path:\`"$($SearchSiteUrl)/\`""
    }

    $from = 0
    $size = 500
    $totalFound = 0
    $allResults = [System.Collections.ArrayList]::new()
    $regionLine = if (-not $UseInteractiveLogin) { "`"region`": `"$Region`"," } else { "" }

    do {
        $searchJsonBody = @"
{
    "requests": [
        {
            "entityTypes": ["driveItem","listItem"],
            "query": {
                "queryString": "($path) AND ($extFilter)"
            },
            "from":$($from),
            "size": $($size),
            $regionLine
            "fields": [
                "SPWebUrl","DocumentLink","Size","SecondaryFileExtension","LastModifiedTime","LastModifiedTimeForRetention","Created","Title",
                "ViewsLifeTime","ViewsLifeTimeUniqueUsers","ViewsRecent","ViewsRecentUniqueUsers",
                "ViewsLastMonths1","ViewsLastMonths1Unique","ViewsLastMonths2","ViewsLastMonths2Unique","ViewsLastMonths3","ViewsLastMonths3Unique",
                "ViewsLast1Days","ViewsLast1DaysUniqueUsers","ViewsLast7Days","ViewsLast7DaysUniqueUsers"
            ]
        }
    ]
}
"@

        $maxRetries = 3
        $retryCount = 0
        $SearchResult = $null

        while ($retryCount -lt $maxRetries -and -not $SearchResult) {
            try {
                $token = Get-PnPAccessToken
                $headers = @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }
                $SearchResult = Invoke-RestMethod -Method Post -Uri $searchQuery_RestAPI -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($searchJsonBody)) -ErrorAction Stop
            } catch {
                $retryCount++
                $errMsg = $_.Exception.Message
                if ($errMsg -match 'Forbidden|Access to ListItem.*requires the following permissions') {
                    Write-Host "    [PERMISSION ERROR] Graph Search requires Sites.Read.All" -ForegroundColor Red
                    break
                }
                Write-Warning "  Search retry $retryCount/3: $errMsg"
                if ($retryCount -lt $maxRetries) {
                    Start-Sleep -Seconds 5
                    try { Connect-PnPOnline -Url $rootUrl -Interactive -ClientId $PnpClientId } catch {}
                }
            }
        }

        if (-not $SearchResult -or -not $SearchResult.value -or -not $SearchResult.value.hitsContainers) {
            break
        }

        $hitsContainer = $SearchResult.value.hitsContainers[0]
        $total = $hitsContainer.Total
        $hits = $hitsContainer.hits

        if ($from -eq 0 -and $total -gt 0) {
            Write-Host "    $Category : $total files found" -ForegroundColor White
        }

        foreach ($hit in $hits) {
            $fields = $hit.resource.listItem.fields
            if (-not $fields) { $fields = $hit.resource.fields }

            $null = $allResults.Add([ordered]@{
                Category      = $Category
                WebUrl        = $fields.spWebUrl
                FileUrl       = if ($fields.documentLink) { $fields.documentLink } else { $fields.path }
                FileSizeMB    = if ($fields.size) { [math]::Round($fields.size / 1MB, 2) } else { 0 }
                FileExtension = $fields.secondaryFileExtension
                Created       = $fields.created
                LastModified  = $fields.lastModifiedTime
                Title         = $fields.title
                ViewsLifeTime = $fields.viewsLifeTime
                ViewsRecent   = $fields.viewsRecent
            })
        }

        $totalFound += $hits.Count
        $from += $size

        # Progress update
        if ($total -gt 0) {
            $pct = [math]::Min(100, [math]::Round($from / $total * 100))
            Write-Host "    $Category : $from / $total ($pct%)" -ForegroundColor Gray
        }

    } while ($from -lt $total -and $from -lt 1000000)

    return @{
        Count   = $totalFound
        Total   = if ($total) { $total } else { 0 }
        Results = $allResults
    }
}

function Search-FileCountLocal {
    param (
        [string]$SearchSiteUrl,
        [string[]]$Extensions,
        [string]$Category
    )

    $searchQuery_RestAPI = "https://graph.microsoft.com/v1.0/search/query"
    $extFilter = ($Extensions | ForEach-Object { "filetype:$($_.TrimStart('.'))" }) -join " OR "

    if ($SearchSiteUrl.TrimEnd('/') -ieq $rootUrl.TrimEnd('/')) {
        $path = "path:\`"$SearchSiteUrl\`" AND NOT path:\`"$rootUrl/sites\`""
    } else {
        $path = "path:\`"$($SearchSiteUrl)/\`""
    }

    $regionLine = if (-not $UseInteractiveLogin) { "`"region`": `"$Region`"," } else { "" }

    $searchJsonBody = @"
{
    "requests": [
        {
            "entityTypes": ["driveItem","listItem"],
            "query": {
                "queryString": "($path) AND ($extFilter)"
            },
            "from": 0,
            "size": 1,
            $regionLine
            "fields": ["id"]
        }
    ]
}
"@

    try {
        $token = Get-PnPAccessToken
        $headers = @{ "Authorization" = "Bearer $token"; "Content-Type" = "application/json" }
        $result = Invoke-RestMethod -Method Post -Uri $searchQuery_RestAPI -Headers $headers -Body ([System.Text.Encoding]::UTF8.GetBytes($searchJsonBody)) -ErrorAction Stop
        $count = $result.value.hitsContainers[0].Total
        if (-not $count) { $count = 0 }
        return [int]$count
    } catch {
        $errMsg = $_.Exception.Message
        if ($errMsg -match 'Forbidden|Access to ListItem.*requires the following permissions') {
            Write-Host "    [PERMISSION ERROR] Graph Search requires Sites.Read.All" -ForegroundColor Red
        }
        Write-Warning "  Count search failed for $Category : $errMsg"
        return 0
    }
}

# ── Execute search ────────────────────────────────────────────────
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$summaryResults = @()
$allFileResults = [System.Collections.ArrayList]::new()
$totalFiles = 0

foreach ($catName in $Categories.Keys) {
    $extensions = $Categories[$catName]
    Write-Host "  Searching: $catName ($($extensions.Count) extensions)..." -ForegroundColor Cyan

    if ($SummaryOnly) {
        $count = Search-FileCountLocal -SearchSiteUrl $SiteUrl -Extensions $extensions -Category $catName
        $summaryResults += [ordered]@{
            Category   = $catName
            FileCount  = $count
            Extensions = ($extensions -join ', ')
        }
        $totalFiles += $count
    } else {
        $result = Search-FilesByExtensionLocal -SearchSiteUrl $SiteUrl -Extensions $extensions -Category $catName
        $summaryResults += [ordered]@{
            Category   = $catName
            FileCount  = $result.Count
            TotalInAPI = $result.Total
            Extensions = ($extensions -join ', ')
        }
        foreach ($r in $result.Results) { $null = $allFileResults.Add($r) }
        $totalFiles += $result.Count
    }
}

# ── Save results to per-site JSON ─────────────────────────────────
$siteResult = [ordered]@{
    SiteUrl      = $SiteUrl
    SiteHash     = $siteHash
    ScannedAt    = (Get-Date).ToString("o")
    SummaryOnly  = $SummaryOnly.IsPresent
    TotalFiles   = $totalFiles
    Duration     = $sw.Elapsed.TotalSeconds
    Categories   = $summaryResults
}

if (-not $SummaryOnly) {
    $siteResult.Files = @($allFileResults)
}

$siteJsonPath = Join-Path $OutputPath "site_$siteHash.json"
$siteResult | ConvertTo-Json -Depth 5 -Compress | Set-Content -Path $siteJsonPath -Encoding UTF8

# ── Update index ──────────────────────────────────────────────────
$indexPath = Join-Path $OutputPath "index.json"
$index = @{ Sites = @() }
if (Test-Path $indexPath) {
    try { $index = Get-Content $indexPath -Raw | ConvertFrom-Json } catch { $index = @{ Sites = @() } }
}
if (-not $index.Sites) { $index.Sites = @() }

# Update or add entry
$existingIdx = -1
for ($i = 0; $i -lt $index.Sites.Count; $i++) {
    if ($index.Sites[$i].SiteUrl -eq $SiteUrl) { $existingIdx = $i; break }
}

$indexEntry = [ordered]@{
    SiteUrl    = $SiteUrl
    SiteHash   = $siteHash
    TotalFiles = $totalFiles
    LastScanned = (Get-Date).ToString("o")
    Duration   = [math]::Round($sw.Elapsed.TotalSeconds, 1)
    Categories = ($summaryResults | ForEach-Object { "$($_.Category):$($_.FileCount)" }) -join ', '
}

if ($existingIdx -ge 0) {
    $sitesList = [System.Collections.ArrayList]@($index.Sites)
    $sitesList[$existingIdx] = $indexEntry
    $index.Sites = @($sitesList)
} else {
    $index.Sites = @($index.Sites) + @($indexEntry)
}

$index.LastUpdated = (Get-Date).ToString("o")
$index | ConvertTo-Json -Depth 3 | Set-Content -Path $indexPath -Encoding UTF8

$sw.Stop()

# ── Summary ───────────────────────────────────────────────────────
Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  SEARCH COMPLETE" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Site:  $SiteUrl" -ForegroundColor White
Write-Host "  Files: $totalFiles" -ForegroundColor Green
Write-Host "  Time:  $([math]::Round($sw.Elapsed.TotalSeconds, 1))s" -ForegroundColor Gray
foreach ($s in $summaryResults) {
    Write-Host "    $($s.Category): $($s.FileCount) files" -ForegroundColor Gray
}
Write-Host "  Output: $siteJsonPath" -ForegroundColor Green
Write-Host ""
