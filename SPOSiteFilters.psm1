# SPOSiteFilters.psm1
# Functions for managing site inclusion and exclusion lists

$script:IncludedSites = @()
$script:ExcludedSites = @()
$script:ExcludedSitesFile = Join-Path $PSScriptRoot "config\ExcludedSites.json"

function Import-SiteListFromCSV {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$CsvPath,
        [string]$UrlColumn = "SiteURL"
    )
    
    if (-not (Test-Path $CsvPath)) {
        throw "CSV file not found: $CsvPath"
    }
    
    try {
        $csvData = Import-Csv -Path $CsvPath -Encoding UTF8
        
        if (-not ($csvData | Get-Member -Name $UrlColumn -ErrorAction SilentlyContinue)) {
            $columns = $csvData | Get-Member -MemberType NoteProperty | Select-Object -ExpandProperty Name
            $urlCol = $columns | Where-Object { $_ -match "url|site|path" } | Select-Object -First 1
            
            if ($urlCol) {
                Write-Warning "Column $UrlColumn not found. Using column $urlCol"
                $UrlColumn = $urlCol
            } else {
                throw "Column $UrlColumn not found. Columns: $($columns -join ', ')"
            }
        }
        
        $sites = @()
        foreach ($row in $csvData) {
            $url = $row.$UrlColumn
            if ($url -and $url.Trim() -match "^https://") {
                $sites += $url.Trim()
            }
        }
        
        Write-Host "[OK] Imported $($sites.Count) sites from CSV" -ForegroundColor Green
        return $sites
    }
    catch {
        throw "Error importing CSV: $_"
    }
}

function Set-SiteInclusionList {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CsvPath)
    
    $script:IncludedSites = @(Import-SiteListFromCSV -CsvPath $CsvPath)
    
    # Set global variable to share with other modules
    $Global:SPOIncludedSites = $script:IncludedSites
    
    Write-Host "  INCLUSION list: $($script:IncludedSites.Count) sites" -ForegroundColor Cyan
    return $script:IncludedSites
}

function Set-SiteExclusionList {
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$CsvPath)
    
    $script:ExcludedSites = @(Import-SiteListFromCSV -CsvPath $CsvPath)
    Write-Host "  EXCLUSION list: $($script:ExcludedSites.Count) sites" -ForegroundColor Yellow
    Save-ExcludedSitesList
    return $script:ExcludedSites
}

function Save-ExcludedSitesList {
    $excludedData = @{
        LastUpdated = (Get-Date).ToString("o")
        Count = $script:ExcludedSites.Count
        Sites = @()
    }
    
    foreach ($siteUrl in $script:ExcludedSites) {
        $siteName = ($siteUrl -split "/")[-1]
        $excludedData.Sites += @{
            Url = $siteUrl
            SiteName = $siteName
            ExcludedAt = (Get-Date).ToString("o")
            Reason = "Exclusion list"
        }
    }
    
    $excludedData | ConvertTo-Json -Depth 5 | Set-Content -Path $script:ExcludedSitesFile -Encoding UTF8
}

function Get-SiteInclusionList { 
    # Return from global variable if it exists (for compatibility between modules)
    if ($Global:SPOIncludedSites -and $Global:SPOIncludedSites.Count -gt 0) {
        return $Global:SPOIncludedSites
    }
    return $script:IncludedSites 
}

function Get-SiteExclusionList { 
    return $script:ExcludedSites 
}

function Clear-SiteFilters {
    $script:IncludedSites = @()
    $script:ExcludedSites = @()
    if (Test-Path $script:ExcludedSitesFile) { 
        Remove-Item $script:ExcludedSitesFile -Force 
    }
    Write-Host "  Filters cleared" -ForegroundColor Green
}

function Test-SiteExcluded {
    param([Parameter(Mandatory)][string]$SiteUrl)
    
    if (-not $script:ExcludedSites -or $script:ExcludedSites.Count -eq 0) { 
        return $false 
    }
    
    $normalizedUrl = $SiteUrl.TrimEnd("/").ToLower()
    foreach ($excluded in $script:ExcludedSites) {
        if ($excluded.TrimEnd("/").ToLower() -eq $normalizedUrl) {
            return $true
        }
    }
    return $false
}

function Get-FilteredSites {
    param([Parameter(Mandatory)]$AllSites)
    
    $filteredSites = @($AllSites)
    
    # INCLUSION filter
    if ($script:IncludedSites -and $script:IncludedSites.Count -gt 0) {
        Write-Host "  Applying INCLUSION filter ($($script:IncludedSites.Count) sites)..." -ForegroundColor Cyan
        $includedUrls = @()
        foreach ($url in $script:IncludedSites) {
            $includedUrls += $url.TrimEnd("/").ToLower()
        }
        
        $newFiltered = @()
        foreach ($site in $filteredSites) {
            $siteUrl = $site.Url.TrimEnd("/").ToLower()
            if ($includedUrls -contains $siteUrl) {
                $newFiltered += $site
            }
        }
        $filteredSites = $newFiltered
        Write-Host "    Sites after inclusion: $($filteredSites.Count)" -ForegroundColor Cyan
    }
    
    # EXCLUSION filter
    if ($script:ExcludedSites -and $script:ExcludedSites.Count -gt 0) {
        Write-Host "  Applying EXCLUSION filter ($($script:ExcludedSites.Count) sites)..." -ForegroundColor Yellow
        $excludedUrls = @()
        foreach ($url in $script:ExcludedSites) {
            $excludedUrls += $url.TrimEnd("/").ToLower()
        }
        
        $beforeCount = $filteredSites.Count
        $newFiltered = @()
        foreach ($site in $filteredSites) {
            $siteUrl = $site.Url.TrimEnd("/").ToLower()
            if ($excludedUrls -notcontains $siteUrl) {
                $newFiltered += $site
            }
        }
        $filteredSites = $newFiltered
        $excludedCount = $beforeCount - $filteredSites.Count
        Write-Host "    Sites excluded: $excludedCount | Remaining: $($filteredSites.Count)" -ForegroundColor Yellow
    }
    
    return $filteredSites
}

function Clear-SiteExclusionList {
    <#
    .SYNOPSIS
        Clears the excluded sites list
    .DESCRIPTION
        Removes all sites from the exclusion list and clears the JSON file
    #>
    [CmdletBinding()]
    param()
    
    $script:ExcludedSites = @()
    
    $excludedSitesFile = Join-Path $PSScriptRoot "config\ExcludedSites.json"
    
    @{
        Count = 0
        Sites = @()
        LastUpdated = (Get-Date).ToString("o")
        ClearedAt = (Get-Date).ToString("o")
    } | ConvertTo-Json -Depth 5 | Set-Content -Path $excludedSitesFile -Encoding UTF8
    
    Write-Host "  Exclusion list cleared" -ForegroundColor Green
    
    return @()
}

function Set-SiteExclusionListFromArray {
    <#
    .SYNOPSIS
        Sets the excluded sites list from an array of URLs
    .PARAMETER Sites
        Array of site URLs to exclude
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string[]]$Sites
    )
    
    # Update local module variable
    $script:ExcludedSites = @($Sites)
    
    # Also update the global variable used by SPOVersionManagement.psm1
    $Global:SPOExcludedSites = @($Sites)
    
    # Try to update via Set-Variable in the main module scope
    try {
        $spoModule = Get-Module -Name "SPOVersionManagement" -ErrorAction SilentlyContinue
        if ($spoModule) {
            & $spoModule { $script:ExcludedSites = $args[0] } $Sites
        }
    }
    catch {
        # Ignore error if module is not loaded
    }
    
    # Save list to JSON file
    Save-ExcludedSitesList
    
    Write-Host "  Exclusion list loaded: $($Sites.Count) sites" -ForegroundColor Yellow
    
    return $script:ExcludedSites
}

Export-ModuleMember -Function @(
    "Import-SiteListFromCSV",
    "Set-SiteInclusionList",
    "Set-SiteExclusionList",
    "Get-SiteInclusionList",
    "Get-SiteExclusionList",
    "Clear-SiteFilters",
    "Test-SiteExcluded",
    "Get-FilteredSites",
    "Save-ExcludedSitesList",
    'Clear-SiteExclusionList',
    'Set-SiteExclusionListFromArray'
)
