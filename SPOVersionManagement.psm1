#Requires -Modules Microsoft.Online.SharePoint.PowerShell

<#
.SYNOPSIS
    Module for SharePoint Online version policy management
.DESCRIPTION
    Contains functions to execute policy sync jobs and version cleanup
    with support for parallel execution and logging for Power BI
#>

#region Configuration - Load from AppPaths.json
# Determine the module directory
$script:ModuleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# Path configuration file (look in config/ first, then Logs/ for backward compat, then Root)
$script:AppPathsFile = Join-Path $script:ModuleRoot "config\AppPaths.json"
if (-not (Test-Path $script:AppPathsFile)) {
    $script:AppPathsFile = Join-Path $script:ModuleRoot "Logs\AppPaths.json"
}
if (-not (Test-Path $script:AppPathsFile)) {
    $script:AppPathsFile = Join-Path $script:ModuleRoot "AppPaths.json"
}

# Load path configuration or use default values
if (Test-Path $script:AppPathsFile) {
    try {
        $script:AppPaths = Get-Content $script:AppPathsFile -Raw | ConvertFrom-Json
        Write-Host "[CONFIG] Loaded AppPaths.json from: $script:AppPathsFile" -ForegroundColor Gray
        
        # Construir caminho base: RootPath + ApplicationFolder
        $script:RootPath = Join-Path $script:AppPaths.RootPath $script:AppPaths.ApplicationFolder
        
        # Build directories paths relative to RootPath
        $script:LogPath = if ($script:AppPaths.Directories.Logs) { 
            Join-Path $script:RootPath $script:AppPaths.Directories.Logs 
        } else { 
            $script:RootPath 
        }
        $script:BackupPath = if ($script:AppPaths.Directories.Backup) { 
            Join-Path $script:RootPath $script:AppPaths.Directories.Backup 
        } else { 
            Join-Path $script:LogPath "Backup" 
        }
        $script:ConfigPath = if ($script:AppPaths.Directories.Config) {
            Join-Path $script:RootPath $script:AppPaths.Directories.Config
        } else {
            Join-Path $script:RootPath "config"
        }
        $script:WebPath = if ($script:AppPaths.Directories.Web) {
            Join-Path $script:RootPath $script:AppPaths.Directories.Web
        } else {
            Join-Path $script:RootPath "web"
        }
        
        # JSON data files (in config/)
        $script:JobStatusFile = Join-Path $script:ConfigPath $script:AppPaths.Files.JobStatus
        $script:TenantStorageFile = Join-Path $script:ConfigPath $script:AppPaths.Files.TenantStorage
        $script:ExcludedSitesFile = Join-Path $script:ConfigPath $script:AppPaths.Files.ExcludedSites
        $script:AllSitesFile = Join-Path $script:ConfigPath $(if ($script:AppPaths.Files.AllSites) { $script:AppPaths.Files.AllSites } else { "AllSites.json" })
        $script:SiteExecutionHistoryFile = Join-Path $script:ConfigPath $(if ($script:AppPaths.Files.SiteExecutionHistory) { $script:AppPaths.Files.SiteExecutionHistory } else { "SiteExecutionHistory.json" })
        $script:DashboardConfigFile = Join-Path $script:ConfigPath $(if ($script:AppPaths.Files.DashboardConfig) { $script:AppPaths.Files.DashboardConfig } else { "DashboardConfig.json" })
        $script:SessionHistoryFile = Join-Path $script:ConfigPath $(if ($script:AppPaths.Files.SessionHistory) { $script:AppPaths.Files.SessionHistory } else { "SessionHistory.json" })
        $script:TenantStorageTimelineFile = Join-Path $script:ConfigPath $(if ($script:AppPaths.Files.TenantStorageTimeline) { $script:AppPaths.Files.TenantStorageTimeline } else { "TenantStorageTimeline.json" })
        
        # CSV files (in Logs/)
        $script:SiteStorageFile = Join-Path $script:LogPath $script:AppPaths.Files.SiteStorage
        $script:ExecutionHistoryFile = Join-Path $script:LogPath $script:AppPaths.Files.ExecutionHistory
        
        # Input files (relative to RootPath)
        $script:IncludeSitesFile = Join-Path $script:RootPath $script:AppPaths.InputFiles.IncludeSites
        $script:ExcludeSitesInputFile = Join-Path $script:RootPath $script:AppPaths.InputFiles.ExcludeSites
        
        Write-Host "[CONFIG] RootPath: $script:RootPath" -ForegroundColor Gray
        Write-Host "[CONFIG] LogPath: $script:LogPath" -ForegroundColor Gray
    }
    catch {
        Write-Warning "[CONFIG] Error loading AppPaths.json: $_. Using default values."
        $script:AppPaths = $null
    }
}

# Default values if AppPaths.json doesn't exist or failed
if (-not $script:AppPaths) {
    Write-Host "[CONFIG] Using default configuration (AppPaths.json not found)" -ForegroundColor Yellow
    $script:RootPath = $PSScriptRoot
    $script:LogPath = Join-Path $PSScriptRoot "Logs"
    $script:BackupPath = Join-Path $PSScriptRoot "Logs\Backup"
    $script:ConfigPath = Join-Path $PSScriptRoot "config"
    $script:WebPath = Join-Path $PSScriptRoot "web"
    $script:JobStatusFile = Join-Path $PSScriptRoot "config\JobStatus.json"
    $script:TenantStorageFile = Join-Path $PSScriptRoot "config\TenantStorage.json"
    $script:ExcludedSitesFile = Join-Path $PSScriptRoot "config\ExcludedSites.json"
    $script:AllSitesFile = Join-Path $PSScriptRoot "config\AllSites.json"
    $script:SiteExecutionHistoryFile = Join-Path $PSScriptRoot "config\SiteExecutionHistory.json"
    $script:SiteStorageFile = Join-Path $PSScriptRoot "Logs\SiteStorage.csv"
    $script:ExecutionHistoryFile = Join-Path $PSScriptRoot "Logs\ExecutionHistory.csv"
    $script:DashboardConfigFile = Join-Path $PSScriptRoot "config\DashboardConfig.json"
    $script:SessionHistoryFile = Join-Path $PSScriptRoot "config\SessionHistory.json"
    $script:TenantStorageTimelineFile = Join-Path $PSScriptRoot "config\TenantStorageTimeline.json"
    $script:IncludeSitesFile = Join-Path $PSScriptRoot "IncludeSites.csv"
    $script:ExcludeSitesInputFile = Join-Path $PSScriptRoot "ExcludeSites.csv"
}

# Unique execution ID (always generated dynamically)
$script:ExecutionId = (Get-Date -Format 'yyyyMMdd_HHmmss') + "_" + [guid]::NewGuid().ToString().Substring(0,8)
$script:CurrentExecutionFile = Join-Path $script:LogPath "Execution_$($script:ExecutionId).csv"

# Other configurations (not path-related)
$script:MaxConcurrentJobs = 10
$script:PollingIntervalSeconds = 30
$script:GraphConnected = $false
$script:CurrentMajorVersionLimit = 4
$script:CurrentMajorWithMinorVersionsLimit = 4
$script:IncludedSites = @()
$script:ExcludedSites = @()
$script:AllSitesCache = $null  # Cache for Get-SPOSite -Limit All to avoid duplicate calls
$script:AllSitesCacheTime = $null

# ── First-Run Initialization ─────────────────────────────────────
# Ensure required directories and files exist on first run
$script:ConfigPath = Join-Path $script:ModuleRoot "config"
$script:WebPath = Join-Path $script:ModuleRoot "web"

if (-not (Test-Path $script:LogPath)) {
    New-Item -ItemType Directory -Path $script:LogPath -Force | Out-Null
    Write-Host "[INIT] Created Logs directory: $script:LogPath" -ForegroundColor Green
}
if (-not (Test-Path $script:BackupPath)) {
    New-Item -ItemType Directory -Path $script:BackupPath -Force | Out-Null
}
if (-not (Test-Path $script:ConfigPath)) {
    New-Item -ItemType Directory -Path $script:ConfigPath -Force | Out-Null
    Write-Host "[INIT] Created config directory: $script:ConfigPath" -ForegroundColor Green
}
if (-not (Test-Path $script:WebPath)) {
    New-Item -ItemType Directory -Path $script:WebPath -Force | Out-Null
    Write-Host "[INIT] Created web directory: $script:WebPath" -ForegroundColor Green
}

# Create AppPaths.json with safe defaults if missing
if (-not (Test-Path (Join-Path $script:ConfigPath "AppPaths.json"))) {
    $defaultAppPaths = @{
        Version = "1.3"
        AppVersion = "2.1.3.3"
        Description = "Centralized configuration for SPO Version Management"
        LastModified = (Get-Date).ToString("o")
        RootPath = $script:ModuleRoot
        ApplicationFolder = ""
        Directories = @{ Root = ""; Logs = "Logs"; Data = "Logs"; Backup = "Logs\Backup"; Config = "config"; Web = "web" }
        Files = @{
            JobStatus = "JobStatus.json"; TenantStorage = "TenantStorage.json"
            ExcludedSites = "ExcludedSites.json"; AllSites = "AllSites.json"
            SiteExecutionHistory = "SiteExecutionHistory.json"
            DashboardConfig = "DashboardConfig.json"; Dashboard = "Dashboard.html"
            ExecutionHistory = "ExecutionHistory.csv"; SiteStorage = "SiteStorage.csv"
            TenantStorageTimeline = "TenantStorageTimeline.json"; AppPaths = "AppPaths.json"
            ExtensionGroups = "ExtensionGroups.json"
        }
        InputFiles = @{ IncludeSites = "IncludeSites.csv"; ExcludeSites = "ExcludeSites.csv" }
        Scripts = @{
            MainModule = "SPOVersionManagement.psm1"; FiltersModule = "SPOSiteFilters.psm1"
            StartScript = "Start-SPOVersionManagement.ps1"; DashboardScript = "Start-Dashboard.ps1"
        }
        EntraIdApp = @{ TenantId = ""; ClientId = ""; CertificateThumbprint = "" }
        GitHubRepo = ""
        TelemetryEndpoint = ""
        TelemetryEnabled = $false
    }
    $defaultAppPaths | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $script:ConfigPath "AppPaths.json") -Encoding UTF8
    Write-Host "[INIT] Created default config\AppPaths.json (configure EntraIdApp section)" -ForegroundColor Green
}

# Create default DashboardConfig.json if missing
if (-not (Test-Path (Join-Path $script:ConfigPath "DashboardConfig.json"))) {
    @{
        Language = "en"
        Currency = @{ Symbol = "$"; Code = "USD"; Position = "before"; DecimalSeparator = "."; ThousandsSeparator = "," }
        CostPerTBYear = 13000
        DateFormat = "MM/dd/yyyy"
        RefreshIntervalSeconds = 3
        ReexecutionDays = 0
        ZeroVersionAction = "ask"
        DashboardPort = 8080
        DashboardLaunchMode = "app"
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $script:ConfigPath "DashboardConfig.json") -Encoding UTF8
    Write-Host "[INIT] Created default config\DashboardConfig.json" -ForegroundColor Green
}

# Create default ExtensionGroups.json if missing
if (-not (Test-Path (Join-Path $script:ConfigPath "ExtensionGroups.json"))) {
    @{
        Groups = @(
            @{ Name = "Video"; Color = "#ff5722"; Enabled = $true; Extensions = @(".mp4",".mov",".wmv",".avi",".mkv",".m4v") }
            @{ Name = "Audio"; Color = "#9c27b0"; Enabled = $true; Extensions = @(".mp3",".wav",".wma",".aac",".flac",".m4a",".ogg") }
            @{ Name = "Image"; Color = "#4caf50"; Enabled = $true; Extensions = @(".jpg",".jpeg",".png",".gif",".bmp",".tiff",".svg") }
            @{ Name = "Design"; Color = "#ff9800"; Enabled = $true; Extensions = @(".psd",".ai",".indd",".sketch",".fig",".xd") }
            @{ Name = "CAD"; Color = "#795548"; Enabled = $true; Extensions = @(".dwg",".dxf",".step",".stp",".iges",".stl") }
        )
    } | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $script:ConfigPath "ExtensionGroups.json") -Encoding UTF8
    Write-Host "[INIT] Created default config\ExtensionGroups.json" -ForegroundColor Green
}

# Create empty JSON data files if missing
$emptyJsonFiles = @{
    "JobStatus.json" = @{ ActiveJobs = @(); RecentCompletedJobs = @(); SkippedJobs = @(); LastUpdated = $null }
    "TenantStorage.json" = @{ TotalQuotaTB = 0; TotalUsedTB = 0; LastUpdated = $null }
    "ExcludedSites.json" = @{ Count = 0; Sites = @(); LastUpdated = $null }
    "SiteExecutionHistory.json" = @{ Sites = @{}; LastUpdated = $null }
    "SessionHistory.json" = @{ Sessions = @(); LastUpdated = $null }
    "TenantStorageTimeline.json" = @{ Timeline = @(); LastUpdated = $null }
}
foreach ($fileName in $emptyJsonFiles.Keys) {
    $filePath = Join-Path $script:ConfigPath $fileName
    if (-not (Test-Path $filePath)) {
        $emptyJsonFiles[$fileName] | ConvertTo-Json -Depth 5 | Set-Content $filePath -Encoding UTF8
    }
}

# Create empty CSV files if missing
@("ExecutionHistory.csv", "SiteStorage.csv") | ForEach-Object {
    $csvPath = Join-Path $script:LogPath $_
    if (-not (Test-Path $csvPath)) {
        "" | Set-Content $csvPath -Encoding UTF8
    }
}
#endregion

#region Exported Path Functions
function Get-SPOAppPaths {
    <#
    .SYNOPSIS
        Returns all configured paths from the module
    .DESCRIPTION
        Exports paths loaded from AppPaths.json for use by external scripts
    .EXAMPLE
        $paths = Get-SPOAppPaths
        $jobStatusFile = $paths.JobStatusFile
    #>
    [CmdletBinding()]
    param()
    
    return [PSCustomObject]@{
        RootPath = $script:RootPath
        LogPath = $script:LogPath
        BackupPath = $script:BackupPath
        JobStatusFile = $script:JobStatusFile
        TenantStorageFile = $script:TenantStorageFile
        ExcludedSitesFile = $script:ExcludedSitesFile
        AllSitesFile = $script:AllSitesFile
        SiteExecutionHistoryFile = $script:SiteExecutionHistoryFile
        SiteStorageFile = $script:SiteStorageFile
        ExecutionHistoryFile = $script:ExecutionHistoryFile
        DashboardConfigFile = $script:DashboardConfigFile
        SessionHistoryFile = $script:SessionHistoryFile
        IncludeSitesFile = $script:IncludeSitesFile
        ExcludeSitesInputFile = $script:ExcludeSitesInputFile
        AppPathsFile = $script:AppPathsFile
    }
}

function Get-DashboardConfig {
    <#
    .SYNOPSIS
        Returns Dashboard configuration settings
    .DESCRIPTION
        Reads DashboardConfig.json and returns settings like ZeroVersionAction
    #>
    [CmdletBinding()]
    param()
    
    $configPath = $script:DashboardConfigFile
    
    if (Test-Path $configPath) {
        try {
            $config = Get-Content $configPath -Raw | ConvertFrom-Json
            return $config
        }
        catch {
            Write-Warning "Error reading DashboardConfig.json: $_"
            return $null
        }
    }
    
    return $null
}

#region Session Management Functions
function Get-PendingSessions {
    <#
    .SYNOPSIS
        Returns all pending (incomplete) sessions from SessionHistory.json
    .DESCRIPTION
        Reads the session history file and returns sessions that are not completed
    #>
    [CmdletBinding()]
    param()
    
    $sessionFile = $script:SessionHistoryFile
    
    if (-not (Test-Path $sessionFile)) {
        return @()
    }
    
    try {
        $sessions = Get-Content $sessionFile -Raw | ConvertFrom-Json
        
        if (-not $sessions -or -not $sessions.Sessions) {
            return @()
        }
        
        # Return sessions that are not completed
        $pending = @($sessions.Sessions | Where-Object { 
            $_.Status -ne "Completed" -and $_.Status -ne "Cancelled" 
        })
        
        return $pending
    }
    catch {
        Write-Warning "Error reading SessionHistory.json: $_"
        return @()
    }
}

function Save-Session {
    <#
    .SYNOPSIS
        Saves or updates a session in SessionHistory.json
    .DESCRIPTION
        Creates a new session or updates an existing one
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionId,
        
        [Parameter(Mandatory)]
        [string]$AdminUrl,
        
        [Parameter()]
        [int]$MajorVersionLimit = 4,
        
        [Parameter()]
        [int]$MajorWithMinorVersionsLimit = 4,
        
        [Parameter()]
        [int]$MaxConcurrentJobs = 10,
        
        [Parameter()]
        [string]$InputSiteListCSV,
        
        [Parameter()]
        [string]$InputExclusionSiteListCSV,
        
        [Parameter()]
        [string]$GraphReportCSV,
        
        [Parameter()]
        [ValidateSet("Started", "InProgress", "Completed", "Cancelled", "Failed")]
        [string]$Status = "Started",
        
        [Parameter()]
        [int]$TotalSites = 0,
        
        [Parameter()]
        [int]$ProcessedSites = 0,
        
        [Parameter()]
        [int]$QueuedSites = 0,
        
        [Parameter()]
        [string]$ZeroVersionAction,

        [Parameter()]
        [switch]$DeleteOnly,

        [Parameter()]
        [switch]$SyncOnly,

        [Parameter()]
        [int]$CheckBatchSize = 10,

        [Parameter()]
        [int]$CheckBatchDelaySeconds = 2
    )
    
    $sessionFile = $script:SessionHistoryFile
    
    # Ensure directory exists
    $sessionDir = Split-Path -Parent $sessionFile
    if (-not (Test-Path $sessionDir)) {
        New-Item -Path $sessionDir -ItemType Directory -Force | Out-Null
        Write-Host "  [INFO] Created session directory: $sessionDir" -ForegroundColor Gray
    }
    
    $sessions = @{ Sessions = @() }
    
    # Load existing sessions
    if (Test-Path $sessionFile) {
        try {
            $existingData = Get-Content $sessionFile -Raw | ConvertFrom-Json
            if ($existingData -and $existingData.Sessions) {
                $sessions.Sessions = @($existingData.Sessions)
            }
        }
        catch {
            Write-Warning "Error loading existing sessions: $_"
        }
    }
    
    # Check if session already exists
    $existingSession = $sessions.Sessions | Where-Object { $_.SessionId -eq $SessionId }
    
    $sessionData = @{
        SessionId = $SessionId
        AdminUrl = $AdminUrl
        StartedAt = if ($existingSession) { $existingSession.StartedAt } else { (Get-Date).ToString("o") }
        LastUpdated = (Get-Date).ToString("o")
        Status = $Status
        Configuration = @{
            MajorVersionLimit = $MajorVersionLimit
            MajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
            MaxConcurrentJobs = $MaxConcurrentJobs
            InputSiteListCSV = $InputSiteListCSV
            InputExclusionSiteListCSV = $InputExclusionSiteListCSV
            GraphReportCSV = $GraphReportCSV
            ZeroVersionAction = $ZeroVersionAction
            DeleteOnly = [bool]$DeleteOnly
            SyncOnly = [bool]$SyncOnly
            CheckBatchSize = $CheckBatchSize
            CheckBatchDelaySeconds = $CheckBatchDelaySeconds
        }
        Progress = @{
            TotalSites = $TotalSites
            ProcessedSites = $ProcessedSites
            QueuedSites = $QueuedSites
        }
    }
    
    if ($existingSession) {
        # Update existing session
        $sessions.Sessions = @($sessions.Sessions | ForEach-Object {
            if ($_.SessionId -eq $SessionId) { $sessionData } else { $_ }
        })
    }
    else {
        # Add new session
        $sessions.Sessions += $sessionData
    }
    
    # Keep only last 10 sessions
    if ($sessions.Sessions.Count -gt 10) {
        $sessions.Sessions = @($sessions.Sessions | Sort-Object { [DateTime]$_.StartedAt } -Descending | Select-Object -First 10)
    }
    
    # Save to file
    try {
        $sessions | ConvertTo-Json -Depth 10 | Set-Content -Path $sessionFile -Encoding UTF8 -Force
        Write-Verbose "Session saved to: $sessionFile"
    }
    catch {
        Write-Warning "Failed to save session to '$sessionFile': $_"
    }
    
    return $sessionData
}

function Update-SessionProgress {
    <#
    .SYNOPSIS
        Updates progress of an existing session
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionId,
        
        [Parameter()]
        [ValidateSet("Started", "InProgress", "Completed", "Cancelled", "Failed")]
        [string]$Status,
        
        [Parameter()]
        [int]$TotalSites,
        
        [Parameter()]
        [int]$ProcessedSites,
        
        [Parameter()]
        [int]$QueuedSites
    )
    
    $sessionFile = $script:SessionHistoryFile
    
    if (-not (Test-Path $sessionFile)) {
        return $null
    }
    
    try {
        $sessions = Get-Content $sessionFile -Raw | ConvertFrom-Json
        
        if (-not $sessions -or -not $sessions.Sessions) {
            return $null
        }
        
        $updated = $false
        $sessions.Sessions = @($sessions.Sessions | ForEach-Object {
            if ($_.SessionId -eq $SessionId) {
                $_.LastUpdated = (Get-Date).ToString("o")
                if ($Status) { $_.Status = $Status }
                if ($null -ne $TotalSites) { $_.Progress.TotalSites = $TotalSites }
                if ($null -ne $ProcessedSites) { $_.Progress.ProcessedSites = $ProcessedSites }
                if ($null -ne $QueuedSites) { $_.Progress.QueuedSites = $QueuedSites }
                $updated = $true
            }
            $_
        })
        
        if ($updated) {
            $sessions | ConvertTo-Json -Depth 10 | Set-Content -Path $sessionFile -Encoding UTF8 -Force
        }
        
        return $sessions.Sessions | Where-Object { $_.SessionId -eq $SessionId }
    }
    catch {
        Write-Warning "Error updating session: $_"
        return $null
    }
}

function Get-SessionById {
    <#
    .SYNOPSIS
        Returns a specific session by ID
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SessionId
    )
    
    $sessionFile = $script:SessionHistoryFile
    
    if (-not (Test-Path $sessionFile)) {
        return $null
    }
    
    try {
        $sessions = Get-Content $sessionFile -Raw | ConvertFrom-Json
        
        if (-not $sessions -or -not $sessions.Sessions) {
            return $null
        }
        
        return $sessions.Sessions | Where-Object { $_.SessionId -eq $SessionId }
    }
    catch {
        Write-Warning "Error reading session: $_"
        return $null
    }
}
#endregion

#region Helper Functions
function Write-ToFileWithRetry {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [string]$Content,
        [switch]$Append,
        [int]$MaxRetries = 3,
        [int]$RetryDelayMs = 200
    )
    
    $retryCount = 0
    $success = $false
    
    while (-not $success -and $retryCount -lt $MaxRetries) {
        try {
            # Usar StreamWriter com FileShare.ReadWrite para permitir acesso compartilhado
            $fileMode = if ($Append -and (Test-Path $FilePath)) { 
                [System.IO.FileMode]::Append 
            } else { 
                [System.IO.FileMode]::Create 
            }
            
            $fileStream = [System.IO.FileStream]::new(
                $FilePath, 
                $fileMode, 
                [System.IO.FileAccess]::Write, 
                [System.IO.FileShare]::ReadWrite
            )
            
            try {
                $writer = [System.IO.StreamWriter]::new($fileStream, [System.Text.Encoding]::UTF8)
                $writer.WriteLine($Content)
                $writer.Flush()
                $success = $true
            }
            finally {
                if ($writer) { $writer.Dispose() }
                if ($fileStream) { $fileStream.Dispose() }
            }
        }
        catch {
            $retryCount++
            if ($retryCount -lt $MaxRetries) {
                Start-Sleep -Milliseconds ($RetryDelayMs * $retryCount)
            }
        }
    }
    
    # If failed on main file, write to backup file
    if (-not $success) {
        try {
            $backupFile = $FilePath -replace '\.csv$', "_backup_$(Get-Date -Format 'HHmmss').csv"
            $Content | Out-File -FilePath $backupFile -Append -Encoding UTF8 -ErrorAction Stop
            Write-Warning "Written to backup file: $backupFile"
        }
        catch {
            Write-Warning "Total failure writing log: $_"
        }
    }
}

function Write-ToCsvSafe {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,
        [Parameter(Mandatory)]
        [string]$Content
    )
    
    # Always write to current execution file (no conflict)
    $Content | Out-File -FilePath $script:CurrentExecutionFile -Append -Encoding UTF8
    
    # Try to write to consolidated file
    Write-ToFileWithRetry -FilePath $FilePath -Content $Content -Append
}
#endregion

#region Initialization
function Initialize-SPOVersionManagement {
    [CmdletBinding()]
    param(
        [string]$AdminUrl,
        [int]$MaxConcurrentJobs = 10,
        [int]$PollingIntervalSeconds = 30,
        [switch]$SkipGraphConnection,
        [string]$GraphReportCSV,
        [switch]$UseFileCache
    )
    
    if (-not (Test-Path $script:LogPath)) {
        New-Item -ItemType Directory -Path $script:LogPath -Force | Out-Null
    }
    
    $script:MaxConcurrentJobs = $MaxConcurrentJobs
    $script:PollingIntervalSeconds = $PollingIntervalSeconds
    
    try {
        $needsConnection = $true
        try {
            $currentConnection = Get-SPOTenant -ErrorAction Stop
            # Existing connection found - verify it's to the correct tenant
            $targetRoot = ($AdminUrl -replace '-admin\.sharepoint\.com.*', '.sharepoint.com').TrimEnd('/')
            try {
                $rootSite = Get-SPOSite -Identity $targetRoot -ErrorAction Stop
                $needsConnection = $false
                Write-Host "[OK] Already connected to SharePoint Online Admin: $AdminUrl" -ForegroundColor Green
            }
            catch {
                # Connected to a different tenant - disconnect and reconnect
                Write-Host "  [!] Existing SPO connection is to a different tenant. Reconnecting..." -ForegroundColor Yellow
                try { Disconnect-SPOService -ErrorAction SilentlyContinue } catch {}
            }
        }
        catch {
            # No active SPO connection
        }
        if ($needsConnection) {
            Write-Host "Connecting to SharePoint Online Admin..." -ForegroundColor Yellow
            Connect-SPOService -Url $AdminUrl
            Write-Host "[OK] Connected to SharePoint Online Admin: $AdminUrl" -ForegroundColor Green
        }
    }
    catch {
        throw "Error connecting to SPO Admin: $_"
    }
    
    # Skip Graph connection if using manual CSV or explicitly requested
    if ($SkipGraphConnection -or $GraphReportCSV) {
        if ($GraphReportCSV) {
            Write-Host "[SKIP] Graph connection skipped - using manual CSV report" -ForegroundColor Gray
        } else {
            Write-Host "[SKIP] Graph connection skipped by user request" -ForegroundColor Gray
        }
        $script:GraphConnected = $false
    }
    else {
        # Try to connect to Graph for storage reports
        try {
            if (Get-Module -ListAvailable -Name "Microsoft.Graph.Reports") {
                Import-Module Microsoft.Graph.Reports -ErrorAction Stop
                $context = Get-MgContext -ErrorAction SilentlyContinue
                if (-not $context) {
                    Connect-MgGraph -Scopes "Reports.Read.All" -NoWelcome -ErrorAction Stop
                }
                Write-Host "[OK] Connected to Microsoft Graph (Reports)" -ForegroundColor Green
                $script:GraphConnected = $true
            }
            else {
                Write-Warning "Microsoft.Graph.Reports module not found. Install with: Install-Module Microsoft.Graph.Reports"
                $script:GraphConnected = $false
            }
        }
        catch {
            Write-Warning "Could not connect to Graph API: $_"
            $script:GraphConnected = $false
        }
    }
    
    Initialize-LogFiles
    
    # Get Graph history data immediately after initialization
    Write-Host ""
    Write-Host "Updating tenant storage data..." -ForegroundColor Cyan
    
    # Use manual CSV if provided, otherwise use Graph API if connected
    if ($GraphReportCSV) {
        Write-Host "  Using manual CSV report for storage trends..." -ForegroundColor Gray
        if ($UseFileCache) {
            $tenantResult = Update-TenantStorageStatus -GraphReportCSV $GraphReportCSV -UseFileCache
        } else {
            $tenantResult = Update-TenantStorageStatus -GraphReportCSV $GraphReportCSV
        }
    }
    elseif ($script:GraphConnected) {
        Write-Host "  Including Graph API consumption history (D180)..." -ForegroundColor Gray
        if ($UseFileCache) {
            $tenantResult = Update-TenantStorageStatus -IncludeGraphHistory -UseFileCache
        } else {
            $tenantResult = Update-TenantStorageStatus -IncludeGraphHistory
        }
    }
    else {
        if ($UseFileCache) {
            $tenantResult = Update-TenantStorageStatus -UseFileCache
        } else {
            $tenantResult = Update-TenantStorageStatus
        }
    }

    # Capture tenant storage snapshot for timeline
    if ($tenantResult) {
        $snapshotParams = @{
            TenantStorage = $tenantResult
            Trigger = "Init"
        }
        # Pass raw daily data from CSV/Graph import for timeline enrichment
        if ($tenantResult.GraphData -and $tenantResult.GraphData.DailyData -and $tenantResult.GraphData.DailyData.Count -gt 0) {
            $snapshotParams.DailyStorageData = $tenantResult.GraphData.DailyData
        }
        Save-TenantStorageSnapshot @snapshotParams
    }
}

function Initialize-LogFiles {
    if (-not (Test-Path $script:ExecutionHistoryFile)) {
        $headers = "Timestamp,SiteUrl,JobType,WorkItemId,Status,RequestTimeUTC,CompleteTimeUTC," +
                   "DurationMinutes,ListsProcessed,ListsSynced,ListSyncFailed,FilesProcessed," +
                   "VersionsProcessed,VersionsDeleted,VersionsFailed,StorageReleasedInBytes," +
                   "StorageReleasedMB,ErrorMessage,InitialStorageUsedBytes,FinalStorageUsedBytes"
        $headers | Out-File -FilePath $script:ExecutionHistoryFile -Encoding UTF8
    }
    
    # Create current execution file with headers
    $headers = "Timestamp,SiteUrl,JobType,WorkItemId,Status,RequestTimeUTC,CompleteTimeUTC," +
               "DurationMinutes,ListsProcessed,ListsSynced,ListSyncFailed,FilesProcessed," +
               "VersionsProcessed,VersionsDeleted,VersionsFailed,StorageReleasedInBytes," +
               "StorageReleasedMB,ErrorMessage,InitialStorageUsedBytes,FinalStorageUsedBytes"
    $headers | Out-File -FilePath $script:CurrentExecutionFile -Encoding UTF8
    Write-Host "  Log for this execution: $($script:CurrentExecutionFile)" -ForegroundColor Gray
    
    if (-not (Test-Path $script:SiteStorageFile)) {
        $headers = "Timestamp,SiteUrl,SiteTitle,StorageUsedBytes,StorageUsedMB,StorageUsedGB," +
                   "StorageAllocatedBytes,StoragePercentUsed,LastModifiedDateTime"
        $headers | Out-File -FilePath $script:SiteStorageFile -Encoding UTF8
    }
    
    if (-not (Test-Path $script:JobStatusFile)) {
        @{
            LastUpdated = (Get-Date).ToString("o")
            ActiveJobs = @()
            QueuedSites = @()
            CompletedJobs = @()
        } | ConvertTo-Json -Depth 10 | Out-File -FilePath $script:JobStatusFile -Encoding UTF8
    }
}
#endregion

#region Storage Functions
function Get-SPOSiteStorageInfo {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl
    )
    
    try {
        $spoSite = Get-SPOSite -Identity $SiteUrl -Detailed -ErrorAction Stop
        
        # Calcular VersionSize em MB e GB
        $versionSizeBytes = if ($spoSite.VersionSize) { $spoSite.VersionSize } else { 0 }
        $versionSizeMB = [math]::Round($versionSizeBytes / 1MB, 2)
        $versionSizeGB = [math]::Round($versionSizeBytes / 1GB, 4)
        
        $storageInfo = [PSCustomObject]@{
            SiteUrl = $SiteUrl
            SiteTitle = $spoSite.Title
            StorageUsedBytes = $spoSite.StorageUsageCurrent * 1MB
            StorageUsedMB = $spoSite.StorageUsageCurrent
            StorageUsedGB = [math]::Round($spoSite.StorageUsageCurrent / 1024, 2)
            StorageAllocatedMB = $spoSite.StorageQuota
            StoragePercentUsed = if ($spoSite.StorageQuota -gt 0) { 
                [math]::Round(($spoSite.StorageUsageCurrent / $spoSite.StorageQuota) * 100, 2) 
            } else { 0 }
            # New version properties
            VersionCount = if ($spoSite.VersionCount) { $spoSite.VersionCount } else { 0 }
            VersionSizeBytes = $versionSizeBytes
            VersionSizeMB = $versionSizeMB
            VersionSizeGB = $versionSizeGB
            VersionPolicyFileTypeOverride = if ($spoSite.VersionPolicyFileTypeOverride) { $spoSite.VersionPolicyFileTypeOverride } else { "None" }
            InheritVersionPolicyFromTenant = if ($null -ne $spoSite.InheritVersionPolicyFromTenant) { $spoSite.InheritVersionPolicyFromTenant } else { $true }
            # Propriedades de Archive
            ArchiveStatus = if ($spoSite.ArchiveStatus) { $spoSite.ArchiveStatus } else { "NotArchived" }
            ArchivedBy = if ($spoSite.ArchivedBy) { $spoSite.ArchivedBy } else { "" }
            ArchivedTime = if ($spoSite.ArchivedTime) { $spoSite.ArchivedTime } else { $null }
            # Outras propriedades
            CreatedTime = if ($spoSite.CreatedTime) { $spoSite.CreatedTime } else { $null }
            LastContentModifiedDate = $spoSite.LastContentModifiedDate
            WebCount = if ($spoSite.WebCount) { $spoSite.WebCount } else { 0 }
            LockState = if ($spoSite.LockState) { $spoSite.LockState } else { "Unlock" }
            Timestamp = (Get-Date).ToString("o")
        }
        
        $csvLine = "{0},{1},{2},{3},{4},{5},{6},{7},{8}" -f `
            $storageInfo.Timestamp,
            $storageInfo.SiteUrl,
            ($storageInfo.SiteTitle -replace ',', ';'),
            $storageInfo.StorageUsedBytes,
            $storageInfo.StorageUsedMB,
            $storageInfo.StorageUsedGB,
            ($storageInfo.StorageAllocatedMB * 1MB),
            $storageInfo.StoragePercentUsed,
            $storageInfo.LastContentModifiedDate
        
        Write-ToFileWithRetry -FilePath $script:SiteStorageFile -Content $csvLine -Append
        
        return $storageInfo
    }
    catch {
        Write-Warning "Error getting storage info for site $SiteUrl : $_"
        return $null
    }
}

#endregion

#region Connection Function
function Connect-SPOVersionManagement {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$AdminUrl,
        [int]$MaxConcurrentJobs = 10,
        [switch]$SkipGraphConnection,
        [string]$GraphReportCSV,
        [switch]$UseFileCache
    )
    
    $initParams = @{
        AdminUrl = $AdminUrl
        MaxConcurrentJobs = $MaxConcurrentJobs
    }
    
    if ($SkipGraphConnection) {
        $initParams.SkipGraphConnection = $true
    }
    
    if ($GraphReportCSV) {
        $initParams.GraphReportCSV = $GraphReportCSV
    }
    
    if ($UseFileCache) {
        $initParams.UseFileCache = $true
    }
    
    Initialize-SPOVersionManagement @initParams
}
#endregion

#region Tenant Functions
function Get-TenantStorageHistory {
    <#
    .SYNOPSIS
        Gets SharePoint storage consumption history via Microsoft Graph API
    .DESCRIPTION
        Uses Get-MgReportSharePointSiteUsageStorage to get data for the last 180 days
    .PARAMETER Period
        Report period: D7, D30, D90 or D180
    #>
    [CmdletBinding()]
    param(
        [ValidateSet("D7", "D30", "D90", "D180")]
        [string]$Period = "D180"
    )
    
    # Check Graph connection independently
    $graphConnected = $false
    try {
        $context = Get-MgContext -ErrorAction SilentlyContinue
        if ($context) {
            $graphConnected = $true
            $script:GraphConnected = $true
        }
    }
    catch {
        # Ignore error
    }
    
    if (-not $graphConnected -and -not $script:GraphConnected) {
        Write-Warning "Graph API not connected. Run Connect-SPOVersionManagement or Connect-MgGraph -Scopes 'Reports.Read.All' first."
        return $null
    }
    
    try {
        Write-Host "  Getting storage history from Graph API (period: $Period)..." -ForegroundColor Cyan
        
        # Create temporary file for the report
        $tempFile = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.csv'
        
        # Get report from Graph (suppress SDK download progress output)
        Write-Host "    Downloading report from Graph API..." -ForegroundColor Gray
        $oldProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            $null = & { Get-MgReportSharePointSiteUsageStorage -Period $Period -OutFile $tempFile -ErrorAction Stop } *>&1 | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }
        }
        finally {
            $ProgressPreference = $oldProgress
        }
        Write-Host "    Download complete" -ForegroundColor Green
        
        if (-not (Test-Path $tempFile)) {
            Write-Warning "Report file was not created"
            return $null
        }
        
        # Read and process CSV using Import-Csv (handles BOM automatically)
        Write-Host "    Processing report data..." -ForegroundColor Gray
        
        # Try Import-Csv
        $csvData = $null
        try {
            $csvData = Import-Csv -Path $tempFile -ErrorAction Stop
        }
        catch {
            Write-Host "    Import-Csv failed, trying manual parsing..." -ForegroundColor Yellow
        }
        
        # If Import-Csv failed or returned empty, try manual parsing
        if (-not $csvData -or $csvData.Count -eq 0) {
            Write-Host "    Using manual CSV parsing..." -ForegroundColor Gray
            
            $rawContent = Get-Content $tempFile -Raw -Encoding UTF8
            $lines = $rawContent -split "`r?`n" | Where-Object { $_.Trim() -ne '' }
            
            if ($lines.Count -lt 2) {
                Write-Warning "Empty or invalid report (less than 2 lines)"
                Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
                return $null
            }
            
            # First line is the header
            $header = $lines[0] -split ','
            Write-Host "    Headers: $($header -join ', ')" -ForegroundColor Gray
            
            $storageHistory = @()
            
            for ($i = 1; $i -lt $lines.Count; $i++) {
                $line = $lines[$i].Trim()
                if ($line -eq '') { continue }
                
                $fields = $line -split ','
                
                if ($fields.Count -ge 3) {
                    $reportDate = $fields[0].Trim('"', ' ')
                    $siteType = $fields[1].Trim('"', ' ')
                    $storageValue = $fields[2].Trim('"', ' ')
                    
                    # Convert storage to number
                    $storageUsedBytes = 0
                    try {
                        $storageUsedBytes = [long]$storageValue
                    }
                    catch {
                        try {
                            $storageUsedBytes = [long]($storageValue -replace '[^\d]', '')
                        }
                        catch { continue }
                    }
                    
                    if ($storageUsedBytes -gt 0) {
                        $storageHistory += [PSCustomObject]@{
                            ReportDate = $reportDate
                            SiteType = $siteType
                            StorageUsedBytes = $storageUsedBytes
                            StorageUsedGB = [math]::Round($storageUsedBytes / 1GB, 2)
                            StorageUsedTB = [math]::Round($storageUsedBytes / 1TB, 4)
                        }
                    }
                }
            }
            
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
            
            if ($storageHistory.Count -eq 0) {
                Write-Warning "No valid records found in report (manual parsing)"
                return $null
            }
            
            Write-Host "    Got $($storageHistory.Count) history records (manual parsing)" -ForegroundColor Green
            return $storageHistory
        }
        
        if ($csvData.Count -eq 0) {
            Write-Warning "Empty report"
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
            return $null
        }
        
        # Identify columns (may have different names)
        $firstRow = $csvData[0]
        $columns = $firstRow.PSObject.Properties.Name
        
        Write-Host "    Columns found: $($columns -join ', ')" -ForegroundColor Gray
        
        # Map columns - the report may have different formats
        $dateColumn = $columns | Where-Object { $_ -match 'date|Report' } | Select-Object -First 1
        $siteTypeColumn = $columns | Where-Object { $_ -match 'Site Type|SiteType' } | Select-Object -First 1
        $storageColumn = $columns | Where-Object { $_ -match 'Storage|Bytes|Used' } | Select-Object -First 1
        
        if (-not $dateColumn) { $dateColumn = $columns[0] }
        if (-not $storageColumn) { $storageColumn = $columns[-1] }
        
        Write-Host "    Using columns: Date='$dateColumn', Storage='$storageColumn'" -ForegroundColor Gray
        
        # Process data
        $storageHistory = @()
        
        foreach ($row in $csvData) {
            try {
                $reportDate = $row.$dateColumn
                $siteType = if ($siteTypeColumn) { $row.$siteTypeColumn } else { "All" }
                $storageValue = $row.$storageColumn
                
                # Try to convert storage to number
                $storageUsedBytes = 0
                if ($storageValue -match '^\d+$') {
                    $storageUsedBytes = [long]$storageValue
                }
                elseif ($storageValue -match '^[\d,\.]+$') {
                    $storageUsedBytes = [long]($storageValue -replace '[,\.]', '')
                }
                
                if ($storageUsedBytes -gt 0 -and $reportDate) {
                    $storageHistory += [PSCustomObject]@{
                        ReportDate = $reportDate
                        SiteType = $siteType
                        StorageUsedBytes = $storageUsedBytes
                        StorageUsedGB = [math]::Round($storageUsedBytes / 1GB, 2)
                        StorageUsedTB = [math]::Round($storageUsedBytes / 1TB, 4)
                    }
                }
            }
            catch {
                # Ignore lines with parsing error
                continue
            }
        }
        
        # Clean up temp file
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        if ($storageHistory.Count -eq 0) {
            Write-Warning "No valid records found in report"
            return $null
        }
        
        Write-Host "    Got $($storageHistory.Count) history records" -ForegroundColor Green
        
        return $storageHistory
    }
    catch {
        Write-Warning "Error getting storage history from Graph: $_"
        Write-Warning "Details: $($_.Exception.Message)"
        if ($tempFile -and (Test-Path $tempFile)) {
            Write-Host "    File content for debug:" -ForegroundColor Yellow
            Get-Content $tempFile -First 5 | ForEach-Object { Write-Host "    $_" -ForegroundColor Gray }
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        }
        return $null
    }
}

function Import-GraphReportCSV {
    <#
    .SYNOPSIS
        Imports and processes a manually exported SharePoint Site Usage report CSV
    .DESCRIPTION
        This function allows customers to import storage trend data from a CSV file
        downloaded from Microsoft 365 Admin Center or Graph API, without needing
        Graph API permissions in their environment.
        
        The CSV should be the "SharePoint site usage storage" report with columns:
        - Report Refresh Date
        - Site Type
        - Storage Used (Byte)
        - Report Date
        - Report Period
    .PARAMETER CsvPath
        Path to the SharePoint Site Usage CSV file
    .EXAMPLE
        Import-GraphReportCSV -CsvPath "C:\Reports\SharePointSiteUsage.csv"
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$CsvPath
    )
    
    if (-not (Test-Path $CsvPath)) {
        Write-Warning "CSV file not found: $CsvPath"
        return $null
    }
    
    try {
        Write-Host "    Reading CSV file: $CsvPath" -ForegroundColor Gray
        
        # Get file info
        $fileInfo = Get-Item $CsvPath
        Write-Host "    File size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Gray
        
        # Import CSV directly (Import-Csv handles BOM automatically)
        Write-Host "    Parsing CSV..." -ForegroundColor Gray
        $csvData = Import-Csv -Path $CsvPath -Encoding UTF8
        
        if (-not $csvData -or $csvData.Count -eq 0) {
            Write-Warning "CSV file is empty or invalid"
            return $null
        }
        
        Write-Host "    Found $($csvData.Count) records in CSV" -ForegroundColor Gray
        
        # Detect column names (handle different languages/formats)
        $firstRow = $csvData[0]
        $columns = $firstRow.PSObject.Properties.Name
        
        Write-Host "    Available columns: $($columns -join ', ')" -ForegroundColor DarkGray
        
        # Map column names - prefer exact "Report Date" over "Report Refresh Date"
        # The CSV has both columns, we need "Report Date" (the data date) not "Report Refresh Date" (when report was generated)
        $dateColumn = $columns | Where-Object { $_ -eq "Report Date" } | Select-Object -First 1
        if (-not $dateColumn) {
            # Fallback to pattern matching if exact match not found
            $dateColumn = $columns | Where-Object { $_ -match "^Report.?Date$|^Data.?do.?Relat|^Fecha|^Date$" } | Select-Object -First 1
        }
        $storageColumn = $columns | Where-Object { $_ -match "Storage.?Used|Armazenamento|Almacenamiento|Byte" } | Select-Object -First 1
        $siteTypeColumn = $columns | Where-Object { $_ -match "Site.?Type|Tipo.?de.?Site|Tipo.?de.?Sitio" } | Select-Object -First 1
        
        if (-not $dateColumn -or -not $storageColumn) {
            Write-Warning "Could not find required columns in CSV. Expected 'Report Date' and 'Storage Used (Byte)'"
            Write-Host "    Available columns: $($columns -join ', ')" -ForegroundColor Yellow
            return $null
        }
        
        Write-Host "    Using columns: Date='$dateColumn', Storage='$storageColumn'" -ForegroundColor Gray
        
        # Process data - aggregate by date (sum all site types)
        Write-Host "    Processing $($csvData.Count) rows..." -ForegroundColor Gray
        $dailyData = @{}
        $processedRows = 0
        $skippedRows = 0
        
        foreach ($row in $csvData) {
            $processedRows++
            
            # Progress indicator every 1000 rows
            if ($processedRows % 1000 -eq 0) {
                Write-Host "      Processed $processedRows rows..." -ForegroundColor DarkGray
            }
            
            $dateStr = $row.$dateColumn
            $storageStr = $row.$storageColumn
            
            # Debug first row
            if ($processedRows -eq 1) {
                Write-Host "      [DEBUG] First row - Date='$dateStr', Storage='$storageStr'" -ForegroundColor DarkGray
            }
            
            # Skip empty rows
            if (-not $dateStr -or -not $storageStr) { 
                $skippedRows++
                if ($processedRows -le 3) {
                    Write-Host "      [DEBUG] Row $processedRows skipped: empty date or storage" -ForegroundColor DarkGray
                }
                continue 
            }
            
            # Parse date
            $reportDate = $null
            try {
                # Normalize the date string - replace various dash/hyphen unicode chars with regular hyphen
                $dateStrClean = $dateStr.Trim()
                # Replace en-dash, em-dash, minus sign, and other unicode dashes with regular hyphen
                $dateStrClean = $dateStrClean -replace '[\u2010\u2011\u2012\u2013\u2014\u2015\u2212]', '-'
                # Remove any other non-printable characters
                $dateStrClean = $dateStrClean -replace '[^\d\-\/a-zA-Z]', ''
                
                # Handle ISO 8601 format with time (e.g., 2026-02-05T00:00:00Z)
                $dateStrClean = $dateStrClean -replace 'T.*$', ''
                
                # Debug for first few rows
                if ($processedRows -le 3) {
                    $hexBytes = ($dateStr.ToCharArray() | ForEach-Object { '{0:X4}' -f [int][char]$_ }) -join ' '
                    Write-Host "      [DEBUG] Row $processedRows date hex: $hexBytes" -ForegroundColor DarkGray
                    Write-Host "      [DEBUG] Row $processedRows date cleaned: '$dateStrClean' (length: $($dateStrClean.Length))" -ForegroundColor DarkGray
                }
                
                # Try to extract date components using regex
                if ($dateStrClean -match '^(\d{4})-(\d{1,2})-(\d{1,2})$') {
                    # yyyy-MM-dd format
                    $year = [int]$Matches[1]
                    $month = [int]$Matches[2]
                    $day = [int]$Matches[3]
                    $reportDate = [DateTime]::new($year, $month, $day)
                }
                elseif ($dateStrClean -match '^(\d{1,2})/(\d{1,2})/(\d{4})$') {
                    # MM/dd/yyyy or dd/MM/yyyy - try parse
                    $reportDate = [DateTime]::Parse($dateStrClean)
                }
                elseif ($dateStrClean -match '^(\d{4})(\d{2})(\d{2})$') {
                    # yyyyMMdd format (no separators)
                    $year = [int]$Matches[1]
                    $month = [int]$Matches[2]
                    $day = [int]$Matches[3]
                    $reportDate = [DateTime]::new($year, $month, $day)
                }
                else {
                    # Try generic parsing as fallback
                    $reportDate = [DateTime]::Parse($dateStrClean)
                }
            }
            catch {
                $skippedRows++
                if ($processedRows -le 3) {
                    Write-Host "      [DEBUG] Row $processedRows skipped: date parse failed for '$dateStr' -> '$dateStrClean' (error: $_)" -ForegroundColor DarkGray
                }
                continue
            }
            
            # Parse storage (handle different formats including quoted values)
            $storageBytes = 0
            $storageClean = $storageStr -replace '[^\d.]', ''
            
            # Handle potential comma as thousands separator or decimal separator
            if ($storageClean -match ',') {
                # Could be thousands separator (1,234,567) or decimal (1234,56)
                if ($storageClean -match '^\d+,\d{3}') {
                    # Looks like thousands separator - remove commas
                    $storageClean = $storageClean -replace ',', ''
                } else {
                    # Could be decimal separator in some locales
                    $storageClean = $storageClean -replace ',', '.'
                }
            }
            
            if ([double]::TryParse($storageClean, [ref]$storageBytes)) {
                $dateKey = $reportDate.ToString("yyyy-MM-dd")
                if ($dailyData.ContainsKey($dateKey)) {
                    $dailyData[$dateKey] += $storageBytes
                } else {
                    $dailyData[$dateKey] = $storageBytes
                }
            } else {
                $skippedRows++
                if ($processedRows -le 3) {
                    Write-Host "      [DEBUG] Row $processedRows skipped: storage parse failed for '$storageStr' (cleaned: '$storageClean')" -ForegroundColor DarkGray
                }
            }
        }
        
        Write-Host "    Processed: $processedRows rows, Skipped: $skippedRows rows" -ForegroundColor Gray
        
        # Show debug summary if all rows skipped
        if ($dailyData.Count -eq 0) {
            Write-Host "    [DEBUG] No data parsed. Sample values from first row:" -ForegroundColor Yellow
            if ($csvData.Count -gt 0) {
                $firstRow = $csvData[0]
                Write-Host "      Date column '$dateColumn': '$($firstRow.$dateColumn)'" -ForegroundColor Yellow
                Write-Host "      Storage column '$storageColumn': '$($firstRow.$storageColumn)'" -ForegroundColor Yellow
            }
            Write-Warning "No valid data found in CSV"
            return $null
        }
        
        Write-Host "    Processed $($dailyData.Count) unique dates" -ForegroundColor Gray
        
        # Group by month
        $monthlyData = @{}
        foreach ($dateStr in $dailyData.Keys) {
            $date = [DateTime]::Parse($dateStr)
            $monthKey = $date.ToString("yyyy-MM")
            
            if (-not $monthlyData.ContainsKey($monthKey)) {
                $monthlyData[$monthKey] = @{
                    TotalBytes = 0
                    DataPoints = 0
                    MonthName = $date.ToString("MMM/yy")
                }
            }
            
            $monthlyData[$monthKey].TotalBytes += $dailyData[$dateStr]
            $monthlyData[$monthKey].DataPoints++
        }
        
        # Calculate averages and build result
        $sortedMonths = $monthlyData.Keys | Sort-Object
        $monthlyResults = @()
        $previousGB = 0
        
        foreach ($month in $sortedMonths) {
            $data = $monthlyData[$month]
            $avgBytes = $data.TotalBytes / $data.DataPoints
            $avgGB = [math]::Round($avgBytes / 1GB, 2)
            $avgTB = [math]::Round($avgBytes / 1TB, 4)
            $growthGB = if ($previousGB -gt 0) { [math]::Round($avgGB - $previousGB, 2) } else { 0 }
            
            $monthlyResults += @{
                Month = $month
                MonthName = $data.MonthName
                AvgStorageBytes = [math]::Round($avgBytes, 0)
                AvgStorageGB = $avgGB
                AvgStorageTB = $avgTB
                GrowthGB = $growthGB
                DataPoints = $data.DataPoints
            }
            
            $previousGB = $avgGB
        }
        
        # Calculate average monthly growth
        $growthValues = $monthlyResults | Where-Object { $_.GrowthGB -ne 0 } | ForEach-Object { $_.GrowthGB }
        $avgMonthlyGrowth = if ($growthValues.Count -gt 0) {
            [math]::Round(($growthValues | Measure-Object -Average).Average, 2)
        } else { 0 }
        
        # Get date range
        $allDates = $dailyData.Keys | ForEach-Object { [DateTime]::Parse($_) } | Sort-Object
        $startDate = ($allDates | Select-Object -First 1).ToString("yyyy-MM-dd")
        $endDate = ($allDates | Select-Object -Last 1).ToString("yyyy-MM-dd")
        
        Write-Host "    [OK] Data range: $startDate to $endDate" -ForegroundColor Green
        Write-Host "    [OK] Average monthly growth: $avgMonthlyGrowth GB" -ForegroundColor Green
        
        return @{
            MonthlyData = $monthlyResults
            DailyData = $dailyData
            AvgMonthlyGrowthGB = $avgMonthlyGrowth
            TotalDataPoints = $dailyData.Count
            ReportStartDate = $startDate
            ReportEndDate = $endDate
        }
    }
    catch {
        Write-Warning "Error processing CSV: $_"
        return $null
    }
}

function Get-TenantStorageHistoryAggregated {
    <#
    .SYNOPSIS
        Gets aggregated history by month for chart display
    .PARAMETER Period
        Report period: D7, D30, D90 or D180
    #>
    [CmdletBinding()]
    param(
        [ValidateSet("D7", "D30", "D90", "D180")]
        [string]$Period = "D180"
    )
    
    # Check Graph connection
    $graphConnected = $false
    try {
        $context = Get-MgContext -ErrorAction SilentlyContinue
        if ($context) {
            $graphConnected = $true
        }
    }
    catch { }
    
    if (-not $graphConnected) {
        Write-Warning "Graph API not connected. Run Connect-MgGraph -Scopes 'Reports.Read.All' first."
        return $null
    }
    
    try {
        Write-Host "  Getting storage history from Graph API (period: $Period)..." -ForegroundColor Cyan
        
        # Create temp file
        $tempFile = [System.IO.Path]::GetTempFileName() -replace '\.tmp$', '.csv'
        
        # Download report (suppress SDK download progress output)
        Write-Host "    Downloading report..." -ForegroundColor Gray
        $oldProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            $null = & { Get-MgReportSharePointSiteUsageStorage -Period $Period -OutFile $tempFile -ErrorAction Stop } *>&1 | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }
        }
        finally {
            $ProgressPreference = $oldProgress
        }
        
        # Verify file
        if (-not (Test-Path $tempFile)) {
            Write-Warning "Report file was not created"
            return $null
        }
        
        $fileInfo = Get-Item $tempFile
        Write-Host "    Download complete: $($fileInfo.Length) bytes" -ForegroundColor Green
        
        # Read CSV
        $csvData = Import-Csv -Path $tempFile -ErrorAction Stop
        
        if (-not $csvData -or $csvData.Count -eq 0) {
            Write-Warning "Empty report"
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
            return $null
        }
        
        Write-Host "    Records in report: $($csvData.Count)" -ForegroundColor Gray
        
        # Identify columns
        $columns = $csvData[0].PSObject.Properties.Name
        Write-Host "    Columns: $($columns -join ', ')" -ForegroundColor Gray
        
        # Map columns - the report has: Report Date, Report Refresh Date, Site Type, Storage Used (Byte)
        # Use "Report Date" for aggregation (data date), not "Report Refresh Date" (report date)
        $dateColumn = $columns | Where-Object { $_ -eq 'Report Date' } | Select-Object -First 1
        if (-not $dateColumn) {
            $dateColumn = $columns | Where-Object { $_ -match '^Report Date$|^Date$' } | Select-Object -First 1
        }
        if (-not $dateColumn) {
            $dateColumn = $columns[0]
        }
        $siteTypeColumn = $columns | Where-Object { $_ -match 'Site.*Type' } | Select-Object -First 1
        $storageColumn = $columns | Where-Object { $_ -match 'Storage.*Byte|Used.*Byte' } | Select-Object -First 1
        
        if (-not $storageColumn) { $storageColumn = $columns[-1] }
        
        Write-Host "    Using: Date='$dateColumn', SiteType='$siteTypeColumn', Storage='$storageColumn'" -ForegroundColor Gray
        
        # Process data - group by date and sum storage
        $dailyData = @{}
        
        foreach ($row in $csvData) {
            $reportDate = $row.$dateColumn
            $siteType = if ($siteTypeColumn) { $row.$siteTypeColumn } else { "All" }
            $storageValue = $row.$storageColumn
            
            # Convert storage to number
            $storageBytes = 0
            if ($storageValue -match '^\d+$') {
                $storageBytes = [long]$storageValue
            }
            elseif ($storageValue) {
                try {
                    $storageBytes = [long]($storageValue -replace '[^\d]', '')
                }
                catch { continue }
            }
            
            if ($storageBytes -gt 0 -and $reportDate) {
                # If SiteType is "All", use directly; otherwise, sum by date
                if ($siteType -eq "All" -or -not $siteTypeColumn) {
                    if (-not $dailyData.ContainsKey($reportDate)) {
                        $dailyData[$reportDate] = $storageBytes
                    }
                }
                else {
                    # Sum all site types by date
                    if (-not $dailyData.ContainsKey($reportDate)) {
                        $dailyData[$reportDate] = 0
                    }
                    $dailyData[$reportDate] += $storageBytes
                }
            }
        }
        
        Write-Host "    Unique days processed: $($dailyData.Count)" -ForegroundColor Gray
        
        if ($dailyData.Count -eq 0) {
            Write-Warning "No valid data found"
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
            return $null
        }
        
        # Group by month
        $monthlyData = @{}
        
        foreach ($dateStr in $dailyData.Keys) {
            try {
                $date = [DateTime]::Parse($dateStr)
                $monthKey = $date.ToString("yyyy-MM")
                $storageGB = [math]::Round($dailyData[$dateStr] / 1GB, 2)
                
                if (-not $monthlyData.ContainsKey($monthKey)) {
                    $monthName = (Get-Culture).DateTimeFormat.GetAbbreviatedMonthName($date.Month) + "/" + $date.ToString("yy")
                    $monthlyData[$monthKey] = @{
                        MonthKey = $monthKey
                        MonthName = $monthName
                        StartGB = $storageGB
                        EndGB = $storageGB
                        MinGB = $storageGB
                        MaxGB = $storageGB
                        DataPoints = 1
                        FirstDate = $date
                        LastDate = $date
                    }
                }
                else {
                    $monthlyData[$monthKey].DataPoints++
                    
                    if ($date -lt $monthlyData[$monthKey].FirstDate) {
                        $monthlyData[$monthKey].StartGB = $storageGB
                        $monthlyData[$monthKey].FirstDate = $date
                    }
                    if ($date -gt $monthlyData[$monthKey].LastDate) {
                        $monthlyData[$monthKey].EndGB = $storageGB
                        $monthlyData[$monthKey].LastDate = $date
                    }
                    if ($storageGB -lt $monthlyData[$monthKey].MinGB) {
                        $monthlyData[$monthKey].MinGB = $storageGB
                    }
                    if ($storageGB -gt $monthlyData[$monthKey].MaxGB) {
                        $monthlyData[$monthKey].MaxGB = $storageGB
                    }
                }
            }
            catch {
                Write-Warning "Error processing date: $dateStr - $_"
            }
        }
        
        Write-Host "    Months aggregated: $($monthlyData.Count)" -ForegroundColor Gray
        
        # Convert to chronologically sorted array by MonthKey (yyyy-MM)
        $sortedMonths = $monthlyData.GetEnumerator() | Sort-Object { $_.Key } | ForEach-Object { $_.Value }
        
        $result = @()
        $previousEndGB = 0
        
        foreach ($month in $sortedMonths) {
            $growthGB = if ($previousEndGB -gt 0) { 
                [math]::Round($month.EndGB - $previousEndGB, 2) 
            } else { 
                0 
            }
            
            $result += [PSCustomObject]@{
                MonthKey = $month.MonthKey
                MonthName = $month.MonthName
                StartGB = $month.StartGB
                EndGB = $month.EndGB
                GrowthGB = $growthGB
                MinGB = $month.MinGB
                MaxGB = $month.MaxGB
                DataPoints = $month.DataPoints
            }
            
            $previousEndGB = $month.EndGB
        }
        
        # Calculate average monthly growth (excluding first month)
        $growthValues = $result | Where-Object { $_.GrowthGB -ne 0 } | Select-Object -ExpandProperty GrowthGB
        $avgGrowth = if ($growthValues -and $growthValues.Count -gt 0) {
            [math]::Round(($growthValues | Measure-Object -Average).Average, 2)
        } else { 0 }
        
        # Clean up temp file
        Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        
        # Get period dates (sorted)
        $sortedDates = $dailyData.Keys | Sort-Object { [DateTime]::Parse($_) }
        $startDate = $sortedDates | Select-Object -First 1
        $endDate = $sortedDates | Select-Object -Last 1
        
        Write-Host "    [OK] Aggregation completed: $($result.Count) months, average growth: $avgGrowth GB/month" -ForegroundColor Green
        
        return @{
            MonthlyData = $result
            DailyData = $dailyData
            AvgMonthlyGrowthGB = $avgGrowth
            TotalDataPoints = ($result | Measure-Object -Property DataPoints -Sum).Sum
            ReportPeriod = $Period
            ReportStartDate = $startDate
            ReportEndDate = $endDate
        }
    }
    catch {
        Write-Warning "Error getting aggregated history: $_"
        Write-Warning "Details: $($_.Exception.Message)"
        if ($tempFile -and (Test-Path $tempFile)) {
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
        }
        return $null
    }
}

function Update-TenantStorageStatus {
    [CmdletBinding()]
    param(
        [switch]$IncludeGraphHistory,
        [string]$GraphReportCSV,
        [switch]$UseFileCache
    )
    
    try {
        Write-Host "  Getting tenant info..." -ForegroundColor Gray
        $tenant = Get-SPOTenant -ErrorAction Stop
        
        # If UseFileCache is set, load from AllSites.json file and skip Get-SPOSite
        if ($UseFileCache) {
            if (Test-Path $script:AllSitesFile) {
                Write-Host "  [FILE CACHE] Loading sites from $($script:AllSitesFile)..." -ForegroundColor Cyan
                $fileData = Get-Content $script:AllSitesFile -Raw | ConvertFrom-Json
                $allSites = if ($fileData.Sites) { $fileData.Sites } else { $fileData }
                Write-Host "    [OK] $($allSites.Count) sites loaded from file cache" -ForegroundColor Green
            } else {
                throw "File cache not found: $($script:AllSitesFile). Run Export-AllSitesStorage first to create the cache."
            }
        }
        # Use cached sites if available (2880 min cache)
        elseif ($script:AllSitesCache -and $script:AllSitesCache.Count -gt 0) {
            $cacheAge = if ($script:AllSitesCacheTime) { (Get-Date) - $script:AllSitesCacheTime } else { [TimeSpan]::MaxValue }
            if ($cacheAge.TotalMinutes -lt 2880) {
                Write-Host "  Using cached site list ($($script:AllSitesCache.Count) sites, cached $([math]::Round($cacheAge.TotalMinutes, 1)) min ago)..." -ForegroundColor Cyan
                $allSites = $script:AllSitesCache
            } else {
                Write-Host "  Getting all sites (cache expired)..." -ForegroundColor Gray
                $allSites = Get-SPOSite -Limit All -Detailed -ErrorAction Stop
                $script:AllSitesCache = $allSites
                $script:AllSitesCacheTime = Get-Date
                Write-Host "    [OK] $($allSites.Count) sites retrieved and cached" -ForegroundColor Green
            }
        } else {
            Write-Host "  Getting all sites (this may take a while for large tenants)..." -ForegroundColor Gray
            $allSites = Get-SPOSite -Limit All -Detailed -ErrorAction Stop
            $script:AllSitesCache = $allSites
            $script:AllSitesCacheTime = Get-Date
            Write-Host "    [OK] $($allSites.Count) sites retrieved and cached" -ForegroundColor Green
        }
        
        $totalUsedMB = ($allSites | Measure-Object -Property StorageUsageCurrent -Sum).Sum
        $totalUsedBytes = $totalUsedMB * 1MB
        $quotaBytes = $tenant.StorageQuota * 1MB
        
        $percentUsed = if ($quotaBytes -gt 0) { [math]::Round(($totalUsedBytes / $quotaBytes) * 100, 2) } else { 0 }
        $availableBytes = $quotaBytes - $totalUsedBytes
        
        $status = "Normal"
        if ($percentUsed -ge 100) { $status = "Critical" }
        elseif ($percentUsed -ge 90) { $status = "Warning" }
        elseif ($percentUsed -ge 80) { $status = "Attention" }
        
        $extraStorageGB = 0
        $extraCost = 0
        if ($totalUsedBytes -gt $quotaBytes) {
            $extraStorageGB = [math]::Round(($totalUsedBytes - $quotaBytes) / 1GB, 2)
            $extraCost = [math]::Round(($extraStorageGB / 1024) * 13000, 2)
        }
        
        $tenantStorage = @{
            StorageUsedBytes = $totalUsedBytes
            StorageUsedMB = [math]::Round($totalUsedBytes / 1MB, 2)
            StorageUsedGB = [math]::Round($totalUsedBytes / 1GB, 2)
            StorageUsedTB = [math]::Round($totalUsedBytes / 1TB, 4)
            TenantQuotaBytes = $quotaBytes
            TenantQuotaMB = [math]::Round($quotaBytes / 1MB, 2)
            TenantQuotaGB = [math]::Round($quotaBytes / 1GB, 2)
            TenantQuotaTB = [math]::Round($quotaBytes / 1TB, 4)
            StorageAvailableBytes = $availableBytes
            StorageAvailableMB = [math]::Round($availableBytes / 1MB, 2)
            StorageAvailableGB = [math]::Round($availableBytes / 1GB, 2)
            PercentUsed = $percentUsed
            PercentAvailable = [math]::Round(100 - $percentUsed, 2)
            StorageStatus = $status
            ExtraStorageGB = $extraStorageGB
            ExtraStorageTB = [math]::Round($extraStorageGB / 1024, 4)
            ExtraCostPerYear = $extraCost
            CostPerTBPerYear = 13000
            SiteCount = $allSites.Count
            LastUpdated = (Get-Date).ToString("o")
            Source = "SPOAdmin"
            HasTrendData = $false
        }
        
        # Process manually provided Graph Report CSV
        if ($GraphReportCSV -and (Test-Path $GraphReportCSV)) {
            Write-Host "  Processing manual Graph report CSV..." -ForegroundColor Cyan
            $graphHistory = Import-GraphReportCSV -CsvPath $GraphReportCSV
            
            if ($graphHistory -and $graphHistory.MonthlyData) {
                $tenantStorage.HasTrendData = $true
                $tenantStorage.Source = "ManualCSV"
                
                # Merge with existing historical data (accumulate over time)
                $mergedMonthly = $graphHistory.MonthlyData
                $mergedDaily = $graphHistory.DailyData
                if (Test-Path $script:TenantStorageFile) {
                    try {
                        $existingTenant = Get-Content $script:TenantStorageFile -Raw | ConvertFrom-Json
                        if ($existingTenant.GraphData -and $existingTenant.GraphData.MonthlyData) {
                            $existingMonthlyMap = @{}
                            foreach ($m in $existingTenant.GraphData.MonthlyData) {
                                $existingMonthlyMap[$m.MonthName] = $m
                            }
                            # Add new months, overwrite existing months with fresh data
                            foreach ($m in $graphHistory.MonthlyData) {
                                $existingMonthlyMap[$m.MonthName] = $m
                            }
                            # Sort by date (MonthName format: "Jan/25", "Feb/25", etc.)
                            $mergedMonthly = @($existingMonthlyMap.Values | Sort-Object { 
                                try {
                                    $parts = $_.MonthName -split '/'
                                    $monthNum = [DateTime]::ParseExact($parts[0], 'MMM', [System.Globalization.CultureInfo]::InvariantCulture).Month
                                    $year = [int]("20" + $parts[1])
                                    $year * 100 + $monthNum
                                } catch { 0 }
                            })
                            Write-Host "    [OK] Merged with existing history: $($mergedMonthly.Count) months total (was $($existingTenant.GraphData.MonthlyData.Count))" -ForegroundColor Cyan
                        }
                        if ($existingTenant.GraphData -and $existingTenant.GraphData.DailyData) {
                            # Merge daily data (existing + new, new overwrites duplicates)
                            $existingDailyMap = @{}
                            foreach ($prop in $existingTenant.GraphData.DailyData.PSObject.Properties) {
                                $existingDailyMap[$prop.Name] = $prop.Value
                            }
                            if ($graphHistory.DailyData -is [hashtable]) {
                                foreach ($key in $graphHistory.DailyData.Keys) {
                                    $existingDailyMap[$key] = $graphHistory.DailyData[$key]
                                }
                            } elseif ($graphHistory.DailyData) {
                                foreach ($prop in $graphHistory.DailyData.PSObject.Properties) {
                                    $existingDailyMap[$prop.Name] = $prop.Value
                                }
                            }
                            $mergedDaily = $existingDailyMap
                        }
                    } catch {
                        Write-Host "    [INFO] No existing history to merge, using CSV data only" -ForegroundColor Gray
                    }
                }
                
                # Recalculate average growth from merged data
                $mergedAvgGrowth = $graphHistory.AvgMonthlyGrowthGB
                if ($mergedMonthly.Count -gt 1) {
                    $growthValues = @($mergedMonthly | Where-Object { $null -ne $_.GrowthGB -and $_.GrowthGB -ne 0 } | ForEach-Object { $_.GrowthGB })
                    if ($growthValues.Count -gt 0) {
                        $mergedAvgGrowth = [math]::Round(($growthValues | Measure-Object -Average).Average, 4)
                    }
                }
                
                $tenantStorage.GraphData = @{
                    MonthlyData = $mergedMonthly
                    DailyData = $mergedDaily
                    AvgMonthlyGrowthGB = $mergedAvgGrowth
                    TotalDataPoints = if ($mergedDaily -is [hashtable]) { $mergedDaily.Count } else { $graphHistory.TotalDataPoints }
                    ReportPeriod = "CSV Import (Accumulated)"
                    ReportStartDate = ($mergedMonthly | Select-Object -First 1).MonthName
                    ReportEndDate = ($mergedMonthly | Select-Object -Last 1).MonthName
                }
                
                # Calculate projections based on merged average growth
                $avgGrowthGB = $mergedAvgGrowth
                $currentGB = [math]::Round($totalUsedBytes / 1GB, 2)
                $quotaGB = [math]::Round($quotaBytes / 1GB, 2)
                
                $projections = @()
                $cumulativeCost = 0
                
                for ($i = 1; $i -le 12; $i++) {
                    $projectedGB = $currentGB + ($avgGrowthGB * $i)
                    $projectedTB = [math]::Round($projectedGB / 1024, 4)
                    $percentProj = [math]::Round(($projectedGB / $quotaGB) * 100, 2)
                    $isOverQuota = $projectedGB -gt $quotaGB
                    $overQuotaTB = if ($isOverQuota) { [math]::Round(($projectedGB - $quotaGB) / 1024, 4) } else { 0 }
                    $monthlyCost = if ($isOverQuota) { [math]::Round($overQuotaTB * 13000 / 12, 2) } else { 0 }
                    $cumulativeCost += $monthlyCost
                    
                    $futureDate = (Get-Date).AddMonths($i)
                    $monthName = (Get-Culture).DateTimeFormat.GetAbbreviatedMonthName($futureDate.Month) + "/" + $futureDate.ToString("yy")
                    
                    $projections += @{
                        MonthName = $monthName
                        ProjectedGB = [math]::Round($projectedGB, 2)
                        ProjectedTB = $projectedTB
                        PercentUsed = $percentProj
                        IsOverQuota = $isOverQuota
                        OverQuotaTB = $overQuotaTB
                        MonthlyCost = $monthlyCost
                        CumulativeCost = [math]::Round($cumulativeCost, 2)
                    }
                }
                
                # Calculate when quota is reached
                $monthsUntilFull = 999
                $estimatedFullDate = "N/A"
                
                if ($avgGrowthGB -gt 0) {
                    $availableGB = $quotaGB - $currentGB
                    if ($availableGB -le 0) {
                        $monthsUntilFull = 0
                        $estimatedFullDate = "Already exceeded!"
                    } else {
                        $monthsUntilFull = [math]::Ceiling($availableGB / $avgGrowthGB)
                        $fullDate = (Get-Date).AddMonths($monthsUntilFull)
                        $estimatedFullDate = $fullDate.ToString("MMM/yyyy")
                    }
                }
                
                $tenantStorage.TrendAnalysis = @{
                    MonthsUntilFull = $monthsUntilFull
                    EstimatedFullDate = $estimatedFullDate
                    TotalExtraCost12Months = [math]::Round($cumulativeCost, 2)
                    Projections = $projections
                }
                
                Write-Host "    [OK] Trend data imported ($($mergedMonthly.Count) months, avg growth: $avgGrowthGB GB/month)" -ForegroundColor Green
            } else {
                Write-Warning "  Could not parse trend data from CSV"
            }
        }
        # Get Graph history from API if available
        elseif ($script:GraphConnected -and $IncludeGraphHistory) {
            Write-Host "  Getting consumption history from Graph API..." -ForegroundColor Cyan
            $graphHistory = Get-TenantStorageHistoryAggregated -Period "D180"
            
            if ($graphHistory -and $graphHistory.MonthlyData) {
                $tenantStorage.HasTrendData = $true
                $tenantStorage.GraphData = @{
                    MonthlyData = $graphHistory.MonthlyData
                    DailyData = $graphHistory.DailyData
                    AvgMonthlyGrowthGB = $graphHistory.AvgMonthlyGrowthGB
                    TotalDataPoints = $graphHistory.TotalDataPoints
                    ReportPeriod = $graphHistory.ReportPeriod
                    ReportStartDate = $graphHistory.ReportStartDate
                    ReportEndDate = $graphHistory.ReportEndDate
                }
                
                # Calculate projections based on average growth
                $avgGrowthGB = $graphHistory.AvgMonthlyGrowthGB
                $currentGB = [math]::Round($totalUsedBytes / 1GB, 2)
                $quotaGB = [math]::Round($quotaBytes / 1GB, 2)
                
                $projections = @()
                $cumulativeCost = 0
                
                for ($i = 1; $i -le 12; $i++) {
                    $projectedGB = $currentGB + ($avgGrowthGB * $i)
                    $projectedTB = [math]::Round($projectedGB / 1024, 4)
                    $percentProj = [math]::Round(($projectedGB / $quotaGB) * 100, 2)
                    $isOverQuota = $projectedGB -gt $quotaGB
                    $overQuotaTB = if ($isOverQuota) { [math]::Round(($projectedGB - $quotaGB) / 1024, 4) } else { 0 }
                    $monthlyCost = if ($isOverQuota) { [math]::Round($overQuotaTB * 13000 / 12, 2) } else { 0 }
                    $cumulativeCost += $monthlyCost
                    
                    $futureDate = (Get-Date).AddMonths($i)
                    $monthName = (Get-Culture).DateTimeFormat.GetAbbreviatedMonthName($futureDate.Month) + "/" + $futureDate.ToString("yy")
                    
                    $projections += @{
                        MonthName = $monthName
                        ProjectedGB = [math]::Round($projectedGB, 2)
                        ProjectedTB = $projectedTB
                        PercentUsed = $percentProj
                        IsOverQuota = $isOverQuota
                        OverQuotaTB = $overQuotaTB
                        MonthlyCost = $monthlyCost
                        CumulativeCost = [math]::Round($cumulativeCost, 2)
                    }
                }
                
                # Calculate when quota is reached
                $monthsUntilFull = 999
                $estimatedFullDate = "N/A"
                
                if ($avgGrowthGB -gt 0) {
                    $availableGB = $quotaGB - $currentGB
                    if ($availableGB -le 0) {
                        $monthsUntilFull = 0
                        $estimatedFullDate = "Already exceeded!"
                    } else {
                        $monthsUntilFull = [math]::Ceiling($availableGB / $avgGrowthGB)
                        $fullDate = (Get-Date).AddMonths($monthsUntilFull)
                        $estimatedFullDate = $fullDate.ToString("MMM/yyyy")
                    }
                }
                
                $tenantStorage.TrendAnalysis = @{
                    MonthsUntilFull = $monthsUntilFull
                    EstimatedFullDate = $estimatedFullDate
                    TotalExtraCost12Months = [math]::Round($cumulativeCost, 2)
                    Projections = $projections
                }
                
                Write-Host "    Trend data added (average growth: $avgGrowthGB GB/month)" -ForegroundColor Green
            }
        }
        
        $tenantStorage | ConvertTo-Json -Depth 10 | Set-Content -Path $script:TenantStorageFile -Encoding UTF8
        Write-Host "  Storage: $($tenantStorage.StorageUsedGB) GB / $($tenantStorage.TenantQuotaGB) GB ($percentUsed%)" -ForegroundColor $(if ($status -eq "Critical") { "Red" } elseif ($status -eq "Warning") { "Yellow" } else { "Green" })
        
        return $tenantStorage
    }
    catch {
        Write-Warning "Error updating tenant status: $_"
        return $null
    }
}

function Save-TenantStorageSnapshot {
    <#
    .SYNOPSIS
        Captures a tenant storage snapshot and appends it to the TenantStorageTimeline.json
    .DESCRIPTION
        Creates an append-only timeline of tenant storage snapshots correlated with
        cumulative cleanup data from SiteExecutionHistory. Also merges daily storage
        data from Graph API (180-day window) so that data is preserved beyond the API limit.
        This enables a C-Level timeline showing actual tenant storage vs. space freed.
    .PARAMETER TenantStorage
        The current tenant storage object (from Update-TenantStorageStatus)
    .PARAMETER Trigger
        What triggered this snapshot (Init, SessionEnd, Manual)
    .PARAMETER DailyStorageData
        Optional hashtable of daily storage data (date-string -> bytes) from CSV or Graph API
        to be merged into the DailyStorageHistory of the timeline
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $TenantStorage,

        [ValidateSet("Init", "SessionEnd", "Manual")]
        [string]$Trigger = "Init",

        [hashtable]$DailyStorageData
    )

    try {
        # Load existing timeline
        $timeline = @{
            LastUpdated = (Get-Date).ToString("o")
            Snapshots = @()
            DailyStorageHistory = @()
        }

        if (Test-Path $script:TenantStorageTimelineFile) {
            try {
                $jsonContent = Get-Content $script:TenantStorageTimelineFile -Raw -ErrorAction Stop
                if ($jsonContent -and $jsonContent.Trim().Length -gt 2) {
                    $existing = $jsonContent | ConvertFrom-Json -ErrorAction Stop

                    # Convert Snapshots
                    if ($existing.Snapshots) {
                        foreach ($snap in $existing.Snapshots) {
                            $timeline.Snapshots += @{
                                CapturedAt             = $snap.CapturedAt
                                Trigger                = $snap.Trigger
                                Source                 = $snap.Source
                                TenantStorageUsedBytes = [long]($snap.TenantStorageUsedBytes)
                                TenantStorageUsedGB    = [double]($snap.TenantStorageUsedGB)
                                TenantQuotaGB          = [double]($snap.TenantQuotaGB)
                                PercentUsed            = [double]($snap.PercentUsed)
                                SiteCount              = [int]($snap.SiteCount)
                                CumulativeCleanedBytes = [long]($snap.CumulativeCleanedBytes)
                                CumulativeCleanedGB    = [double]($snap.CumulativeCleanedGB)
                                CumulativeVersionsDeleted = [long]($snap.CumulativeVersionsDeleted)
                                SitesCleanedToDate     = [int]($snap.SitesCleanedToDate)
                                SessionId              = $snap.SessionId
                            }
                        }
                    }

                    # Convert DailyStorageHistory
                    if ($existing.DailyStorageHistory) {
                        foreach ($day in $existing.DailyStorageHistory) {
                            $timeline.DailyStorageHistory += @{
                                Date             = $day.Date
                                StorageUsedBytes = [long]($day.StorageUsedBytes)
                                StorageUsedGB    = [double]($day.StorageUsedGB)
                                Source           = $day.Source
                            }
                        }
                    }
                }
            }
            catch {
                Write-Warning "Error reading existing timeline: $_. Starting fresh append."
            }
        }

        # --- Calculate cumulative cleanup totals from SiteExecutionHistory ---
        $cumulativeCleanedBytes = [long]0
        $cumulativeVersionsDeleted = [long]0
        $sitesCleanedCount = 0

        if (Test-Path $script:SiteExecutionHistoryFile) {
            try {
                $histJson = Get-Content $script:SiteExecutionHistoryFile -Raw | ConvertFrom-Json -ErrorAction Stop
                if ($histJson.Sites -and $histJson.Sites.PSObject.Properties) {
                    foreach ($prop in $histJson.Sites.PSObject.Properties) {
                        $site = $prop.Value
                        if ($site.TotalStorageReleasedBytes -and [long]$site.TotalStorageReleasedBytes -gt 0) {
                            $cumulativeCleanedBytes += [long]$site.TotalStorageReleasedBytes
                            $cumulativeVersionsDeleted += [long]$site.TotalVersionsDeleted
                            $sitesCleanedCount++
                        }
                    }
                }
            }
            catch {
                Write-Warning "Could not read SiteExecutionHistory for snapshot: $_"
            }
        }

        # --- Deduplicate: only add snapshot if no snapshot exists for the same date + trigger ---
        $todayKey = (Get-Date).ToString("yyyy-MM-dd")
        $existsForToday = $timeline.Snapshots | Where-Object {
            $_.CapturedAt -and $_.CapturedAt.Substring(0, 10) -eq $todayKey -and $_.Trigger -eq $Trigger
        }

        if (-not $existsForToday) {
            $snapshot = @{
                CapturedAt             = (Get-Date).ToString("o")
                Trigger                = $Trigger
                Source                 = if ($TenantStorage.Source) { $TenantStorage.Source } else { "SPOAdmin" }
                TenantStorageUsedBytes = [long]($TenantStorage.StorageUsedBytes)
                TenantStorageUsedGB    = [math]::Round([double]($TenantStorage.StorageUsedGB), 2)
                TenantQuotaGB          = [math]::Round([double]($TenantStorage.TenantQuotaGB), 2)
                PercentUsed            = [math]::Round([double]($TenantStorage.PercentUsed), 2)
                SiteCount              = [int]($TenantStorage.SiteCount)
                CumulativeCleanedBytes = $cumulativeCleanedBytes
                CumulativeCleanedGB    = [math]::Round($cumulativeCleanedBytes / 1GB, 2)
                CumulativeVersionsDeleted = $cumulativeVersionsDeleted
                SitesCleanedToDate     = $sitesCleanedCount
                SessionId              = if ($script:ExecutionId) { $script:ExecutionId } else { "manual" }
            }
            $timeline.Snapshots += $snapshot
            Write-Host "    [TIMELINE] Snapshot added ($Trigger): Tenant=$($snapshot.TenantStorageUsedGB) GB, Cleaned=$($snapshot.CumulativeCleanedGB) GB, Sites=$sitesCleanedCount" -ForegroundColor DarkCyan
        }
        else {
            # Update existing snapshot with latest cumulative data
            foreach ($snap in $timeline.Snapshots) {
                if ($snap.CapturedAt -and $snap.CapturedAt.Substring(0, 10) -eq $todayKey -and $snap.Trigger -eq $Trigger) {
                    $snap.CumulativeCleanedBytes = $cumulativeCleanedBytes
                    $snap.CumulativeCleanedGB = [math]::Round($cumulativeCleanedBytes / 1GB, 2)
                    $snap.CumulativeVersionsDeleted = $cumulativeVersionsDeleted
                    $snap.SitesCleanedToDate = $sitesCleanedCount
                    $snap.TenantStorageUsedBytes = [long]($TenantStorage.StorageUsedBytes)
                    $snap.TenantStorageUsedGB = [math]::Round([double]($TenantStorage.StorageUsedGB), 2)
                    Write-Host "    [TIMELINE] Updated existing snapshot ($Trigger) with latest data" -ForegroundColor DarkCyan
                    break
                }
            }
        }

        # --- Merge daily storage data from CSV/Graph if provided ---
        if ($DailyStorageData -and $DailyStorageData.Count -gt 0) {
            $existingDates = @{}
            foreach ($day in $timeline.DailyStorageHistory) {
                $existingDates[$day.Date] = $true
            }

            $mergedCount = 0
            foreach ($dateKey in $DailyStorageData.Keys) {
                if (-not $existingDates.ContainsKey($dateKey)) {
                    $bytes = [long]$DailyStorageData[$dateKey]
                    $timeline.DailyStorageHistory += @{
                        Date             = $dateKey
                        StorageUsedBytes = $bytes
                        StorageUsedGB    = [math]::Round($bytes / 1GB, 2)
                        Source           = if ($TenantStorage.Source -eq 'ManualCSV') { 'CSV-Import' } else { 'GraphAPI-Daily' }
                    }
                    $existingDates[$dateKey] = $true
                    $mergedCount++
                }
            }
            if ($mergedCount -gt 0) {
                Write-Host "    [TIMELINE] Merged $mergedCount new daily storage records from $(if ($TenantStorage.Source -eq 'ManualCSV') { 'CSV' } else { 'Graph API' })" -ForegroundColor DarkCyan
            }
        }
        # Fallback: use monthly aggregates if no daily data was provided
        elseif ($TenantStorage.HasTrendData -and $TenantStorage.GraphData -and $TenantStorage.GraphData.MonthlyData) {
            $existingDates = @{}
            foreach ($day in $timeline.DailyStorageHistory) {
                $existingDates[$day.Date] = $true
            }

            foreach ($month in $TenantStorage.GraphData.MonthlyData) {
                $monthKey = $month.Month
                $refDate = "${monthKey}-15"
                if (-not $existingDates.ContainsKey($refDate)) {
                    $timeline.DailyStorageHistory += @{
                        Date             = $refDate
                        StorageUsedBytes = [long]($month.AvgStorageBytes)
                        StorageUsedGB    = [double]($month.AvgStorageGB)
                        Source           = "GraphAPI-Monthly"
                    }
                    $existingDates[$refDate] = $true
                }
            }
        }

        # Sort daily history by date
        $timeline.DailyStorageHistory = @($timeline.DailyStorageHistory | Sort-Object { $_.Date })

        # Sort snapshots by date
        $timeline.Snapshots = @($timeline.Snapshots | Sort-Object { $_.CapturedAt })

        $timeline.LastUpdated = (Get-Date).ToString("o")

        # Save with retry
        $maxRetries = 3
        $retryCount = 0
        $saved = $false
        while (-not $saved -and $retryCount -lt $maxRetries) {
            try {
                $timeline | ConvertTo-Json -Depth 10 | Set-Content -Path $script:TenantStorageTimelineFile -Encoding UTF8 -Force
                $saved = $true
            }
            catch {
                $retryCount++
                if ($retryCount -lt $maxRetries) { Start-Sleep -Milliseconds 500 }
                else { throw $_ }
            }
        }

        Write-Host "    [TIMELINE] Saved: $($timeline.Snapshots.Count) snapshots, $($timeline.DailyStorageHistory.Count) daily records" -ForegroundColor DarkCyan
    }
    catch {
        Write-Warning "Error saving tenant storage snapshot: $_"
    }
}

function Get-AllTenantSites {
    [CmdletBinding()]
    param(
        [switch]$IncludePersonalSites,
        [int]$MinStorageMB = 0,
        [switch]$UseFileCache
    )
    
    # Check if there is an inclusion list defined (global variable from SPOSiteFilters)
    $includedSiteUrls = @()
    if ($Global:SPOIncludedSites -and $Global:SPOIncludedSites.Count -gt 0) {
        $includedSiteUrls = @($Global:SPOIncludedSites)
        Write-Host "  Active INCLUSION list: $($includedSiteUrls.Count) specific sites" -ForegroundColor Cyan
    }
    
    # If there is an inclusion list, fetch ONLY the sites from the list
    if ($includedSiteUrls.Count -gt 0) {
        
        # If UseFileCache is set, load from AllSites.json and filter by inclusion list
        if ($UseFileCache) {
            if (Test-Path $script:AllSitesFile) {
                Write-Host "  [FILE CACHE] Loading sites from $($script:AllSitesFile) and filtering by inclusion list..." -ForegroundColor Cyan
                $fileData = Get-Content $script:AllSitesFile -Raw | ConvertFrom-Json
                $cachedSites = if ($fileData.Sites) { $fileData.Sites } else { $fileData }
                Write-Host "    [OK] $($cachedSites.Count) total sites in cache" -ForegroundColor Green
                
                # Normalize inclusion URLs for matching
                $includedNormalized = @{}
                foreach ($url in $includedSiteUrls) {
                    $includedNormalized[$url.TrimEnd("/").ToLower()] = $true
                }
                
                # Filter cached sites by inclusion list
                $allSites = @()
                $matchedUrls = @{}
                foreach ($site in $cachedSites) {
                    $siteUrl = $site.Url.TrimEnd("/").ToLower()
                    if ($includedNormalized.ContainsKey($siteUrl)) {
                        $allSites += $site
                        $matchedUrls[$siteUrl] = $true
                    }
                }
                
                # Report sites from inclusion list NOT found in cache
                $notFoundCount = 0
                foreach ($url in $includedSiteUrls) {
                    $normalizedUrl = $url.TrimEnd("/").ToLower()
                    if (-not $matchedUrls.ContainsKey($normalizedUrl)) {
                        $notFoundCount++
                        Write-Warning "    [!] Site not in cache: $url"
                    }
                }
                
                if ($notFoundCount -gt 0) {
                    Write-Host "  [WARNING] $notFoundCount sites from inclusion list were not found in cache" -ForegroundColor Yellow
                    Write-Host "  [TIP] Run without -UseFileCache or update AllSites.json to include missing sites" -ForegroundColor Yellow
                }
                
                Write-Host "  Sites loaded from cache (filtered): $($allSites.Count) of $($includedSiteUrls.Count)" -ForegroundColor Cyan
                return $allSites
            } else {
                Write-Warning "  File cache not found: $($script:AllSitesFile). Falling back to Get-SPOSite per site."
            }
        }
        
        # No cache — fetch each site individually via Get-SPOSite
        Write-Host "  Getting details of sites from inclusion list..." -ForegroundColor Gray
        $allSites = @()
        $notFoundSites = @()
        
        foreach ($siteUrl in $includedSiteUrls) {
            try {
                $site = Get-SPOSite -Identity $siteUrl -Detailed -ErrorAction Stop
                $allSites += $site
                Write-Host "    [OK] $siteUrl" -ForegroundColor Green
            }
            catch {
                Write-Warning "    [!] Site not found or no access: $siteUrl"
                $notFoundSites += $siteUrl
            }
        }
        
        if ($notFoundSites.Count -gt 0) {
            Write-Host "  [WARNING] $($notFoundSites.Count) sites from the list were not found" -ForegroundColor Yellow
        }
        
        Write-Host "  Sites loaded from list: $($allSites.Count) of $($includedSiteUrls.Count)" -ForegroundColor Cyan
        return $allSites
    }
    
    # If there is no inclusion list, fetch all tenant sites
    Write-Host "  Getting ALL sites from tenant (no inclusion list)..." -ForegroundColor Gray
    
    # If UseFileCache is set, load from AllSites.json file and skip Get-SPOSite
    if ($UseFileCache) {
        if (Test-Path $script:AllSitesFile) {
            Write-Host "    [FILE CACHE] Loading sites from $($script:AllSitesFile)..." -ForegroundColor Cyan
            $fileData = Get-Content $script:AllSitesFile -Raw | ConvertFrom-Json
            $allSites = if ($fileData.Sites) { $fileData.Sites } else { $fileData }
            Write-Host "    [OK] $($allSites.Count) sites loaded from file cache" -ForegroundColor Green
        } else {
            throw "File cache not found: $($script:AllSitesFile). Run Export-AllSitesStorage first to create the cache."
        }
    }
    # Check if we have a valid cache (from this session)
    elseif ($script:AllSitesCache -and $script:AllSitesCache.Count -gt 0) {
        $cacheAge = if ($script:AllSitesCacheTime) { (Get-Date) - $script:AllSitesCacheTime } else { [TimeSpan]::MaxValue }
        # Cache valid for 2880 minutes
        if ($cacheAge.TotalMinutes -lt 2880) {
            Write-Host "    [CACHE] Using cached data ($($script:AllSitesCache.Count) sites, cached $([math]::Round($cacheAge.TotalMinutes, 1)) min ago)" -ForegroundColor Cyan
            $allSites = $script:AllSitesCache
        } else {
            Write-Host "    Cache expired, refreshing..." -ForegroundColor Gray
            $allSites = Get-SPOSite -Limit All -Detailed -ErrorAction Stop
            $script:AllSitesCache = $allSites
            $script:AllSitesCacheTime = Get-Date
            Write-Host "    [OK] $($allSites.Count) sites retrieved and cached" -ForegroundColor Green
        }
    } else {
        # No cache - fetch and store
        Write-Host "    Fetching all sites (this may take a while for large tenants)..." -ForegroundColor Gray
        $allSites = Get-SPOSite -Limit All -Detailed -ErrorAction Stop
        $script:AllSitesCache = $allSites
        $script:AllSitesCacheTime = Get-Date
        Write-Host "    [OK] $($allSites.Count) sites retrieved and cached" -ForegroundColor Green
    }
    
    # System sites that should be automatically excluded
    $systemSitePaths = @(
        '/search',
        '/sites/search',
        '/portals/hub',
        '/sites/appcatalog'
    )
    
    # System root URLs (OneDrive root, etc.)
    $systemRootPatterns = @(
        '-my\.sharepoint\.com/?$'  # OneDrive root URL
    )
    
    $filteredSites = $allSites | Where-Object {
        $includeThis = $true
        $siteUrlLower = $_.Url.ToLower()
        
        # Exclude personal sites (OneDrive)
        if (-not $IncludePersonalSites -and $siteUrlLower -match "/personal/") {
            $includeThis = $false
        }
        
        # Exclude locked sites (ReadOnly, NoAccess, etc.)
        if ($_.LockState -and $_.LockState -ne "Unlock") {
            Write-Host "    Excluding locked site ($($_.LockState)): $($_.Url)" -ForegroundColor DarkGray
            $includeThis = $false
        }
        
        # Exclude archived sites
        if ($_.ArchiveStatus -and $_.ArchiveStatus -ne "NotArchived" -and $_.ArchiveStatus -ne "" -and $_.ArchiveStatus -ne "None") {
            Write-Host "    Excluding archived site ($($_.ArchiveStatus)): $($_.Url)" -ForegroundColor DarkGray
            $includeThis = $false
        }
        
        # Exclude system root URLs (like OneDrive root)
        foreach ($pattern in $systemRootPatterns) {
            if ($siteUrlLower -match $pattern) {
                Write-Host "    Excluding system root URL: $($_.Url)" -ForegroundColor DarkGray
                $includeThis = $false
                break
            }
        }
        
        # Exclude system sites
        foreach ($systemPath in $systemSitePaths) {
            if ($siteUrlLower.EndsWith($systemPath) -or $siteUrlLower -match "$systemPath$") {
                Write-Host "    Excluding system site: $($_.Url)" -ForegroundColor DarkGray
                $includeThis = $false
                break
            }
        }
        
        # Minimum storage filter
        if ($MinStorageMB -gt 0 -and $_.StorageUsageCurrent -lt $MinStorageMB) {
            $includeThis = $false
        }
        
        $includeThis
    }
    
    $lockedCount = @($allSites | Where-Object { $_.LockState -and $_.LockState -ne "Unlock" }).Count
    $archivedCount = @($allSites | Where-Object { $_.ArchiveStatus -and $_.ArchiveStatus -ne "NotArchived" -and $_.ArchiveStatus -ne "" -and $_.ArchiveStatus -ne "None" }).Count
    $excludedCount = $allSites.Count - $filteredSites.Count
    
    Write-Host "  Total sites: $($filteredSites.Count) (excluded: $excludedCount - locked: $lockedCount, archived: $archivedCount)" -ForegroundColor Gray
    return $filteredSites
}
#endregion

#region Job Functions
function Get-JobStatus {
    if (Test-Path $script:JobStatusFile) {
        return Get-Content $script:JobStatusFile -Raw | ConvertFrom-Json
    }
    return @{ ActiveJobs = @(); QueuedSites = @(); CompletedJobs = @() }
}

function Get-ExistingJobProgress {
    <#
    .SYNOPSIS
        Checks if there's an existing job running for a site
    .DESCRIPTION
        For each site, checks if there's already a SyncListPolicy or BatchDelete job
        running. Returns the job progress if found, or null if no active job.
    .PARAMETER SiteUrl
        URL of the site to check
    .PARAMETER JobType
        Type of job to check: SyncListPolicy or BatchDelete
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        
        [Parameter(Mandatory)]
        [ValidateSet("SyncListPolicy", "BatchDelete")]
        [string]$JobType
    )
    
    try {
        if ($JobType -eq "SyncListPolicy") {
            $progress = Get-SPOSiteManageVersionPolicyJobProgress -Identity $SiteUrl -ErrorAction Stop
        } else {
            $progress = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $SiteUrl -ErrorAction Stop
        }
        
        if ($progress -and $progress.Status) {
            # Check if job is still running (not completed) - include "New" status
            $runningStatuses = @("InProgress", "Queued", "NotStarted", "Processing", "New")
            $completedStatuses = @("CompleteSuccess", "Completed")
            $isRunning = $runningStatuses -contains $progress.Status
            $isCompleted = $completedStatuses -contains $progress.Status
            
            return @{
                SiteUrl = $SiteUrl
                JobType = $JobType
                Status = $progress.Status
                IsRunning = $isRunning
                IsCompleted = $isCompleted
                IsFailed = ($progress.Status -eq "Failed" -or $progress.Status -eq "CompleteFailed")
                WorkItemId = if ($progress.WorkItemId) { $progress.WorkItemId.ToString() } else { $null }
                Progress = $progress
            }
        }
        
        return $null
    }
    catch {
        # No job found or error - means no active job
        return $null
    }
}

function Get-SiteLastSuccessfulExecution {
    <#
    .SYNOPSIS
        Gets the last successful execution for a site
    .DESCRIPTION
        Checks SiteExecutionHistory.json and returns the last successful execution
        (CompleteSuccess or Completed status) for a given site and job type
    .PARAMETER SiteUrl
        URL of the site to check
    .PARAMETER JobType
        Type of job to check: SyncListPolicy or BatchDelete (optional - if not specified, checks any job type)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        
        [Parameter()]
        [ValidateSet("SyncListPolicy", "BatchDelete", "")]
        [string]$JobType = ""
    )
    
    # Normalize URL to lowercase to match how it's saved in history
    $normalizedUrl = $SiteUrl.TrimEnd('/').ToLower()
    
    Write-Verbose "[Get-SiteLastSuccessfulExecution] Looking for: $normalizedUrl (JobType: $JobType)"
    
    if (-not (Test-Path $script:SiteExecutionHistoryFile)) {
        Write-Verbose "[Get-SiteLastSuccessfulExecution] History file not found: $($script:SiteExecutionHistoryFile)"
        return $null
    }
    
    try {
        $jsonContent = Get-Content $script:SiteExecutionHistoryFile -Raw -ErrorAction Stop
        if (-not $jsonContent -or $jsonContent.Trim().Length -lt 2) {
            Write-Verbose "[Get-SiteLastSuccessfulExecution] History file is empty"
            return $null
        }
        
        $historyData = $jsonContent | ConvertFrom-Json -ErrorAction Stop
        
        if (-not $historyData.Sites) {
            Write-Verbose "[Get-SiteLastSuccessfulExecution] No Sites property in history"
            return $null
        }
        
        # Find the site in history (try normalized URL - lowercase)
        $siteHistory = $null
        $siteProps = $historyData.Sites.PSObject.Properties
        
        # Try exact match first
        if ($siteProps[$normalizedUrl]) {
            $siteHistory = $historyData.Sites.$normalizedUrl
            Write-Verbose "[Get-SiteLastSuccessfulExecution] Found site with exact match"
        } else {
            # Try case-insensitive match by iterating
            foreach ($prop in $siteProps) {
                if ($prop.Name.ToLower() -eq $normalizedUrl) {
                    $siteHistory = $prop.Value
                    Write-Verbose "[Get-SiteLastSuccessfulExecution] Found site with case-insensitive match: $($prop.Name)"
                    break
                }
            }
        }
        
        if (-not $siteHistory -or -not $siteHistory.Executions) {
            Write-Verbose "[Get-SiteLastSuccessfulExecution] Site not found in history or no executions"
            return $null
        }
        
        Write-Verbose "[Get-SiteLastSuccessfulExecution] Found $($siteHistory.Executions.Count) executions for site"
        
        # Filter executions by job type if specified and successful status
        $successStatuses = @("CompleteSuccess", "Completed")
        $successfulExecs = @($siteHistory.Executions | Where-Object {
            $isSuccess = $successStatuses -contains $_.Status
            $matchesType = [string]::IsNullOrEmpty($JobType) -or $_.JobType -eq $JobType
            $isSuccess -and $matchesType
        })
        
        if ($successfulExecs.Count -eq 0) {
            return $null
        }
        
        # Get the most recent successful execution
        $lastExecution = $successfulExecs | Sort-Object { [DateTime]::Parse($_.ExecutedAt) } -Descending | Select-Object -First 1
        
        if ($lastExecution) {
            $executedAt = [DateTime]::Parse($lastExecution.ExecutedAt)
            $daysSinceExecution = ((Get-Date) - $executedAt).TotalDays
            
            return @{
                SiteUrl = $SiteUrl
                JobType = $lastExecution.JobType
                Status = $lastExecution.Status
                ExecutedAt = $executedAt
                ExecutedAtDisplay = $executedAt.ToString("dd/MM/yyyy HH:mm:ss")
                DaysSinceExecution = [Math]::Round($daysSinceExecution, 1)
                WorkItemId = $lastExecution.WorkItemId
                VersionsDeleted = $lastExecution.VersionsDeleted
                StorageReleasedBytes = $lastExecution.StorageReleasedBytes
            }
        }
        
        return $null
    }
    catch {
        Write-Verbose "Error checking site execution history for $SiteUrl : $_"
        return $null
    }
}

function Get-SiteLastExecution {
    <#
    .SYNOPSIS
        Gets the most recent execution of a site regardless of status
    .DESCRIPTION
        Returns the latest execution record for a site, used to check
        if the most recent run was a failure (to bypass reexecution interval)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        [string]$JobType
    )
    
    try {
        $historyFile = Get-SPOAppPaths | ForEach-Object { $_.SiteExecutionHistoryFile }
        if (-not $historyFile -or -not (Test-Path $historyFile)) { return $null }
        
        $historyData = Get-Content $historyFile -Raw | ConvertFrom-Json
        if (-not $historyData -or -not $historyData.Sites) { return $null }
        
        $normalizedUrl = $SiteUrl.TrimEnd('/').ToLower()
        $siteHistory = $null
        $siteProps = @($historyData.Sites.PSObject.Properties)
        
        if ($historyData.Sites.$normalizedUrl) {
            $siteHistory = $historyData.Sites.$normalizedUrl
        } else {
            foreach ($prop in $siteProps) {
                if ($prop.Name.ToLower() -eq $normalizedUrl) {
                    $siteHistory = $prop.Value
                    break
                }
            }
        }
        
        if (-not $siteHistory -or -not $siteHistory.Executions) { return $null }
        
        $filteredExecs = @($siteHistory.Executions | Where-Object {
            [string]::IsNullOrEmpty($JobType) -or $_.JobType -eq $JobType
        })
        
        if ($filteredExecs.Count -eq 0) { return $null }
        
        $lastExec = $filteredExecs | Sort-Object { [DateTime]::Parse($_.ExecutedAt) } -Descending | Select-Object -First 1
        
        if ($lastExec) {
            $executedAt = [DateTime]::Parse($lastExec.ExecutedAt)
            return @{
                SiteUrl = $SiteUrl
                JobType = $lastExec.JobType
                Status = $lastExec.Status
                ExecutedAt = $executedAt
                DaysSinceExecution = [Math]::Round(((Get-Date) - $executedAt).TotalDays, 1)
            }
        }
        
        return $null
    }
    catch {
        Write-Verbose "Error in Get-SiteLastExecution for $SiteUrl : $_"
        return $null
    }
}

function Test-ShouldProcessSite {
    <#
    .SYNOPSIS
        Checks if a site should be processed based on reexecution interval settings
    .DESCRIPTION
        Combines checks for:
        1. Existing running job (returns info to monitor instead of start new)
        2. Recent successful execution within reexecution interval (skips if within interval)
        3. "ask" mode - prompts user when site was recently processed
    .PARAMETER SiteUrl
        URL of the site to check
    .PARAMETER JobType
        Type of job to check: SyncListPolicy or BatchDelete
    .PARAMETER ReexecutionDays
        Number of days to skip reexecution (0 = always process, 1-7 = skip if within interval, "ask" = prompt user)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        
        [Parameter(Mandatory)]
        [ValidateSet("SyncListPolicy", "BatchDelete")]
        [string]$JobType,
        
        [Parameter()]
        [string]$ReexecutionDays = "0"
    )
    
    $result = @{
        ShouldProcess = $true
        Reason = "None"
        ExistingJob = $null
        LastExecution = $null
        SkipReason = $null
        NeedsUserConfirmation = $false
    }
    
    Write-Verbose "[Test-ShouldProcessSite] Site: $SiteUrl | JobType: $JobType | ReexecutionDays: $ReexecutionDays"
    
    # Check 1: Is there an existing job running?
    $existingJob = Get-ExistingJobProgress -SiteUrl $SiteUrl -JobType $JobType
    
    if ($existingJob -and $existingJob.IsRunning) {
        Write-Verbose "[Test-ShouldProcessSite] Found running job: $($existingJob.Status)"
        $result.ShouldProcess = $false
        $result.Reason = "ExistingJobRunning"
        $result.ExistingJob = $existingJob
        $result.SkipReason = "Job already running (Status: $($existingJob.Status))"
        return $result
    }
    
    # Check 2: Was site recently processed successfully?
    # Get last execution info regardless of mode
    $lastExecution = Get-SiteLastSuccessfulExecution -SiteUrl $SiteUrl -JobType $JobType
    $result.LastExecution = $lastExecution
    
    if ($lastExecution) {
        Write-Verbose "[Test-ShouldProcessSite] Found last execution: $($lastExecution.ExecutedAtDisplay) ($([Math]::Round($lastExecution.DaysSinceExecution, 2)) days ago)"
    } else {
        Write-Verbose "[Test-ShouldProcessSite] No previous execution found"
    }
    
    if ($ReexecutionDays -eq "ask") {
        # Ask mode: if site was processed recently, flag for user confirmation
        if ($lastExecution -and $lastExecution.DaysSinceExecution -lt 7) {
            $result.NeedsUserConfirmation = $true
            $result.Reason = "NeedsConfirmation"
            $result.SkipReason = "Processed $([Math]::Round($lastExecution.DaysSinceExecution, 1)) days ago - awaiting user confirmation"
        }
    }
    elseif ([int]$ReexecutionDays -gt 0) {
        $days = [int]$ReexecutionDays
        Write-Verbose "[Test-ShouldProcessSite] Checking interval: $days days"
        if ($lastExecution -and $lastExecution.DaysSinceExecution -lt $days) {
            Write-Verbose "[Test-ShouldProcessSite] SKIP: DaysSinceExecution ($($lastExecution.DaysSinceExecution)) < ReexecutionDays ($days)"
            $result.ShouldProcess = $false
            $result.Reason = "RecentlyProcessed"
            $result.SkipReason = "Processed $([Math]::Round($lastExecution.DaysSinceExecution, 1)) days ago (interval: $days days)"
            return $result
        }
    }
    
    # Site should be processed (or needs user confirmation)
    if ($result.Reason -eq "None") {
        $result.Reason = "Ready"
    }
    Write-Verbose "[Test-ShouldProcessSite] Result: ShouldProcess=$($result.ShouldProcess) Reason=$($result.Reason)"
    return $result
}

function Save-SiteExecutionHistory {
    <#
    .SYNOPSIS
        Saves the execution history of a site to the history file
    .DESCRIPTION
        Adds an execution record to the site's history, including
        processed files, deleted versions and released space
    .PARAMETER SiteUrl
        URL of the processed site
    .PARAMETER SiteTitle
        Site title
    .PARAMETER JobType
        Job type (SyncListPolicy, BatchDelete)
    .PARAMETER ExecutionData
        Hashtable with execution data
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SiteUrl,
        
        [string]$SiteTitle = "",
        
        [Parameter(Mandatory)]
        [string]$JobType,
        
        [Parameter(Mandatory)]
        [hashtable]$ExecutionData
    )
    
    try {
        # Normalize URL first
        $normalizedUrl = $SiteUrl.TrimEnd("/").ToLower()
        
        # Load existing history as PSObject and convert manually
        $history = @{
            LastUpdated = (Get-Date).ToString("o")
            Sites = @{}
        }
        
        $existingSitesCount = 0
        
        if (Test-Path $script:SiteExecutionHistoryFile) {
            try {
                $jsonContent = Get-Content $script:SiteExecutionHistoryFile -Raw -ErrorAction Stop
                if ($jsonContent -and $jsonContent.Trim().Length -gt 2) {
                    $existingData = $jsonContent | ConvertFrom-Json -ErrorAction Stop
                    
                    # Convert Sites from PSObject to Hashtable manually
                    if ($existingData.Sites -and $existingData.Sites.PSObject.Properties) {
                        $history.LastUpdated = if ($existingData.LastUpdated) { $existingData.LastUpdated } else { (Get-Date).ToString("o") }
                        
                        # Iterate over the Sites object properties
                        foreach ($prop in $existingData.Sites.PSObject.Properties) {
                            $siteKey = $prop.Name
                            $siteValue = $prop.Value
                            
                            if (-not $siteValue) { continue }
                            
                            # Convert the site to hashtable
                            $siteHash = @{
                                SiteUrl = $siteValue.SiteUrl
                                Title = $siteValue.Title
                                FirstProcessed = $siteValue.FirstProcessed
                                LastProcessed = $siteValue.LastProcessed
                                TotalExecutions = [int]($siteValue.TotalExecutions)
                                TotalVersionsDeleted = [long]($siteValue.TotalVersionsDeleted)
                                TotalStorageReleasedBytes = [long]($siteValue.TotalStorageReleasedBytes)
                                Executions = @()
                            }
                            
                            # Converter execucoes
                            if ($siteValue.Executions) {
                                foreach ($exec in $siteValue.Executions) {
                                    if (-not $exec) { continue }
                                    $siteHash.Executions += @{
                                        ExecutionId = $exec.ExecutionId
                                        ExecutedAt = $exec.ExecutedAt
                                        ExecutedAtDisplay = $exec.ExecutedAtDisplay
                                        JobType = $exec.JobType
                                        Status = $exec.Status
                                        DurationMinutes = $exec.DurationMinutes
                                        WorkItemId = $exec.WorkItemId
                                        ListsProcessed = $exec.ListsProcessed
                                        ListsSynced = $exec.ListsSynced
                                        FilesProcessed = $exec.FilesProcessed
                                        VersionsProcessed = $exec.VersionsProcessed
                                        VersionsDeleted = $exec.VersionsDeleted
                                        StorageReleasedBytes = $exec.StorageReleasedBytes
                                        StorageReleasedMB = $exec.StorageReleasedMB
                                        StorageReleasedGB = $exec.StorageReleasedGB
                                        StorageBeforeBytes = $exec.StorageBeforeBytes
                                        StorageAfterBytes = $exec.StorageAfterBytes
                                        MajorVersionLimit = $exec.MajorVersionLimit
                                        MajorWithMinorVersionsLimit = $exec.MajorWithMinorVersionsLimit
                                        VersionsKept = if ($exec.VersionsKept) { $exec.VersionsKept } else { 0 }
                                        VersionSizeBeforeBytes = if ($exec.VersionSizeBeforeBytes) { $exec.VersionSizeBeforeBytes } else { 0 }
                                        VersionSizeAfterBytes = if ($exec.VersionSizeAfterBytes) { $exec.VersionSizeAfterBytes } else { 0 }
                                        RetentionManaged = if ($exec.RetentionManaged) { $true } else { $false }
                                        RetentionPolicies = if ($exec.RetentionPolicies) { @($exec.RetentionPolicies) } else { @() }
                                        RetentionSuspendedAt = $exec.RetentionSuspendedAt
                                        RetentionResumedAt = $exec.RetentionResumedAt
                                        RetentionWaitMinutes = if ($exec.RetentionWaitMinutes) { $exec.RetentionWaitMinutes } else { 0 }
                                    }
                                }
                            }
                            
                            $history.Sites[$siteKey] = $siteHash
                            $existingSitesCount++
                        }
                        
                        Write-Host "    [HISTORY] Loaded $existingSitesCount sites from existing history" -ForegroundColor DarkGray
                    }
                }
            }
            catch {
                Write-Warning "Error reading existing history: $_"
                Write-Warning "The file will be preserved and the new site will be added"
                # DO NOT reset history - try to preserve what we have
            }
        }
        
        # Create site entry if it doesn't exist
        if (-not $history.Sites.ContainsKey($normalizedUrl)) {
            $history.Sites[$normalizedUrl] = @{
                SiteUrl = $SiteUrl
                Title = $SiteTitle
                FirstProcessed = (Get-Date).ToString("o")
                LastProcessed = (Get-Date).ToString("o")
                TotalExecutions = 0
                TotalVersionsDeleted = 0
                TotalStorageReleasedBytes = 0
                Executions = @()
            }
        }
        
        # Update title if provided
        if ($SiteTitle) {
            $history.Sites[$normalizedUrl].Title = $SiteTitle
        }
        
        # Create execution record
        $durationMins = if ($ExecutionData.DurationMinutes) { $ExecutionData.DurationMinutes } else { 0 }
        $durationHours = [math]::Floor($durationMins / 60)
        $durationRemainingMins = [math]::Floor($durationMins % 60)
        $durationFormatted = if ($durationHours -gt 0) { "{0}h {1}m" -f $durationHours, $durationRemainingMins } else { "{0:N1}m" -f $durationMins }
        
        $executionRecord = @{
            ExecutionId = $script:ExecutionId
            ExecutedAt = (Get-Date).ToString("o")
            ExecutedAtDisplay = (Get-Date).ToString("dd/MM/yyyy HH:mm:ss")
            JobType = $JobType
            Status = $ExecutionData.Status
            DurationMinutes = $durationMins
            DurationFormatted = $durationFormatted
            StartTime = if ($ExecutionData.StartTime) { $ExecutionData.StartTime } else { $null }
            EndTime = (Get-Date).ToString("o")
            SPORequestTime = if ($ExecutionData.SPORequestTime) { $ExecutionData.SPORequestTime } else { $null }
            SPOCompleteTime = if ($ExecutionData.SPOCompleteTime) { $ExecutionData.SPOCompleteTime } else { $null }
            SPOJobDurationMinutes = if ($ExecutionData.SPOJobDurationMinutes) { $ExecutionData.SPOJobDurationMinutes } else { 0 }
            WorkItemId = $ExecutionData.WorkItemId
            ListsProcessed = $ExecutionData.ListsProcessed
            ListsSynced = $ExecutionData.ListsSynced
            FilesProcessed = $ExecutionData.FilesProcessed
            VersionsProcessed = $ExecutionData.VersionsProcessed
            VersionsDeleted = $ExecutionData.VersionsDeleted
            StorageReleasedBytes = if ($ExecutionData.StorageReleasedBytes) { [long]$ExecutionData.StorageReleasedBytes } else { [long]0 }
            StorageReleasedMB = [math]::Round(([long]$ExecutionData.StorageReleasedBytes / 1MB), 2)
            StorageReleasedGB = [math]::Round(([long]$ExecutionData.StorageReleasedBytes / 1GB), 4)
            StorageBeforeBytes = if ($ExecutionData.StorageBeforeBytes) { [long]$ExecutionData.StorageBeforeBytes } else { [long]0 }
            StorageAfterBytes = if ($ExecutionData.StorageAfterBytes) { [long]$ExecutionData.StorageAfterBytes } else { [long]0 }
            MajorVersionLimit = $script:CurrentMajorVersionLimit
            MajorWithMinorVersionsLimit = $script:CurrentMajorWithMinorVersionsLimit
            VersionsKept = if ($ExecutionData.VersionsKept) { $ExecutionData.VersionsKept } else { 0 }
            VersionSizeBeforeBytes = if ($ExecutionData.VersionSizeBeforeBytes) { [long]$ExecutionData.VersionSizeBeforeBytes } else { [long]0 }
            VersionSizeAfterBytes = if ($ExecutionData.VersionSizeAfterBytes) { [long]$ExecutionData.VersionSizeAfterBytes } else { [long]0 }
            SiteStorageUsedMB = if ($ExecutionData.SiteStorageUsedMB) { $ExecutionData.SiteStorageUsedMB } else { 0 }
            VersionSizeBeforeMB = if ($ExecutionData.VersionSizeBeforeMB) { $ExecutionData.VersionSizeBeforeMB } else { 0 }
            RetentionManaged = if ($ExecutionData.RetentionManaged) { $true } else { $false }
            RetentionPolicies = if ($ExecutionData.RetentionPolicies) { @($ExecutionData.RetentionPolicies) } else { @() }
            RetentionSuspendedAt = if ($ExecutionData.RetentionSuspendedAt) { $ExecutionData.RetentionSuspendedAt } else { $null }
            RetentionResumedAt = if ($ExecutionData.RetentionResumedAt) { $ExecutionData.RetentionResumedAt } else { $null }
            RetentionWaitMinutes = if ($ExecutionData.RetentionWaitMinutes) { $ExecutionData.RetentionWaitMinutes } else { 0 }
            HollowSuccess = if ($ExecutionData.HollowSuccess) { $true } else { $false }
        }
        
        # Add execution to site
        $siteHistory = $history.Sites[$normalizedUrl]
        
        # Ensure Executions is an array
        if (-not $siteHistory.Executions) {
            $siteHistory.Executions = @()
        }
        if ($siteHistory.Executions -isnot [array]) {
            $siteHistory.Executions = @($siteHistory.Executions)
        }
        
        # Deduplicate: skip if an entry with same JobType and same results exists within 90 min
        $isDuplicate = $false
        $dedupeWindowMinutes = 90
        foreach ($existExec in $siteHistory.Executions) {
            if ($existExec.JobType -eq $JobType) {
                # Consider CompleteSuccess and CompleteSuccessNoEffect as equivalent for dedup
                $statusMatch = ($existExec.Status -eq $executionRecord.Status) -or
                               ($existExec.Status -like 'CompleteSuccess*' -and $executionRecord.Status -like 'CompleteSuccess*')
                if ($statusMatch) {
                    $timeDiffOk = $false
                    try {
                        $existTime = [DateTime]::Parse($existExec.ExecutedAt)
                        $timeDiffOk = [Math]::Abs(((Get-Date) - $existTime).TotalMinutes) -lt $dedupeWindowMinutes
                    } catch { }
                    if ($timeDiffOk) {
                        $sameResults = ($existExec.FilesProcessed -eq $executionRecord.FilesProcessed) -and
                                       ($existExec.VersionsDeleted -eq $executionRecord.VersionsDeleted) -and
                                       ($existExec.ListsProcessed -eq $executionRecord.ListsProcessed)
                        if ($sameResults) {
                            $isDuplicate = $true
                            Write-Host "    [HISTORY] Skipping duplicate entry for $JobType (same results within ${dedupeWindowMinutes}m)" -ForegroundColor DarkGray
                            break
                        }
                    }
                }
            }
        }
        
        if (-not $isDuplicate) {
            $siteHistory.Executions += $executionRecord
            $siteHistory.TotalExecutions = $siteHistory.Executions.Count
            $siteHistory.LastProcessed = (Get-Date).ToString("o")
            
            # Accumulate totals (only for BatchDelete that actually deletes)
            if ($JobType -eq "BatchDelete") {
                $siteHistory.TotalVersionsDeleted += [long]$ExecutionData.VersionsDeleted
                $siteHistory.TotalStorageReleasedBytes += [long]$ExecutionData.StorageReleasedBytes
            }
            
            $history.Sites[$normalizedUrl] = $siteHistory
            $history.LastUpdated = (Get-Date).ToString("o")
        }
        
        # Save file with retry in case of lock
        $maxRetries = 3
        $retryCount = 0
        $saved = $false
        
        while (-not $saved -and $retryCount -lt $maxRetries) {
            try {
                $jsonOutput = $history | ConvertTo-Json -Depth 10
                $jsonOutput | Set-Content -Path $script:SiteExecutionHistoryFile -Encoding UTF8 -Force
                $saved = $true
            }
            catch {
                $retryCount++
                if ($retryCount -lt $maxRetries) {
                    Start-Sleep -Milliseconds 500
                } else {
                    throw $_
                }
            }
        }
        
        Write-Host "    [HISTORY] Execution saved - Site: $($siteHistory.TotalExecutions) exec, Total sites: $($history.Sites.Count)" -ForegroundColor DarkGray
    }
    catch {
        Write-Warning "Error saving execution history: $_"
    }
}

function Update-JobStatus {
    [CmdletBinding()]
    param(
        [array]$ActiveJobs = @(),
        [array]$QueuedSites = @(),
        [array]$RecentCompletedJobs = @(),
        [int]$MajorVersionLimit = 4,
        [int]$MajorWithMinorVersionsLimit = 4,
        [switch]$DeleteOnly,
        [switch]$SyncOnly,
        [switch]$ManageRetentionPolicy
    )
    
    $syncQueue = @($QueuedSites | Where-Object { $_.Phase -eq "SyncListPolicy" })
    $deleteQueue = @($QueuedSites | Where-Object { $_.Phase -eq "BatchDelete" })
    
    $status = @{
        LastUpdated = (Get-Date).ToString("o")
        ActiveJobs = $ActiveJobs
        QueuedSites = $QueuedSites
        QueuedSitesCount = $QueuedSites.Count
        QueuedSitesSyncCount = $syncQueue.Count
        QueuedSitesDeleteCount = $deleteQueue.Count
        RecentCompletedJobs = $RecentCompletedJobs
        CompletedJobsCount = $RecentCompletedJobs.Count
        MajorVersionLimit = $MajorVersionLimit
        MajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
        DeleteOnly = [bool]$DeleteOnly
        SyncOnly = [bool]$SyncOnly
        ManageRetentionPolicy = [bool]$ManageRetentionPolicy
    }
    
    $status | ConvertTo-Json -Depth 10 | Set-Content -Path $script:JobStatusFile -Encoding UTF8
}

function Sync-PendingJobStatus {
    <#
    .SYNOPSIS
        Synchronizes pending job status by checking actual job progress in SharePoint
    .DESCRIPTION
        This function checks all sites that have pending delete jobs and updates
        the execution history with their real status. Should be called at script
        startup to ensure Dashboard reflects actual job completion status.
    #>
    [CmdletBinding()]
    param()
    
    Write-Host ""
    Write-Host "=== Synchronizing Pending Job Status ===" -ForegroundColor Cyan
    
    try {
        # Read the execution history CSV to find sites with sync completed
        $executionHistoryFile = $script:ExecutionHistoryFile
        
        if (-not (Test-Path $executionHistoryFile)) {
            Write-Host "  No execution history found. Skipping sync." -ForegroundColor Gray
            return
        }
        
        # Read CSV and find sites that had sync completed
        $csvContent = Get-Content $executionHistoryFile -Raw
        if (-not $csvContent -or $csvContent.Trim().Length -eq 0) {
            Write-Host "  Execution history is empty. Skipping sync." -ForegroundColor Gray
            return
        }
        
        # Parse CSV to find unique sites with SyncListPolicy completed
        $csvLines = @($csvContent -split "`n" | Where-Object { $_.Trim().Length -gt 0 })
        $header = $csvLines[0]
        
        # Find sites with completed sync jobs
        $sitesWithSync = @{}
        $sitesWithDelete = @{}
        
        foreach ($line in $csvLines[1..($csvLines.Length - 1)]) {
            if (-not $line -or $line.Trim().Length -eq 0) { continue }
            
            $parts = $line -split ","
            if ($parts.Count -lt 5) { continue }
            
            $siteUrl = $parts[1].Trim().TrimEnd("/").ToLower()
            $jobType = $parts[2].Trim()
            $status = $parts[4].Trim()
            
            if ($jobType -eq "SyncListPolicy" -and ($status -eq "CompleteSuccess" -or $status -eq "Completed")) {
                $sitesWithSync[$siteUrl] = $true
            }
            if ($jobType -eq "BatchDelete" -and ($status -eq "CompleteSuccess" -or $status -eq "Completed")) {
                $sitesWithDelete[$siteUrl] = $true
            }
        }
        
        # Find sites that have sync but no delete completion
        $sitesNeedingSync = @()
        foreach ($siteUrl in $sitesWithSync.Keys) {
            if (-not $sitesWithDelete.ContainsKey($siteUrl)) {
                $sitesNeedingSync += $siteUrl
            }
        }
        
        if ($sitesNeedingSync.Count -eq 0) {
            Write-Host "  All sites are fully synchronized." -ForegroundColor Green
            Write-Host ""
            return
        }
        
        Write-Host "  Found $($sitesNeedingSync.Count) sites with pending delete status. Checking actual progress..." -ForegroundColor Yellow
        
        $updatedCount = 0
        $stillPendingCount = 0
        
        foreach ($siteUrl in $sitesNeedingSync) {
            try {
                Write-Host "  Checking: $siteUrl" -ForegroundColor Gray
                
                # Get the actual job progress from SharePoint
                $progress = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                
                if ($progress) {
                    $jobStatus = $progress.Status
                    Write-Host "    Status: $jobStatus" -ForegroundColor Gray
                    
                    if ($jobStatus -eq "Completed" -or $jobStatus -eq "CompleteSuccess" -or $jobStatus -eq "CompleteFailed" -or $jobStatus -eq "CompleteNoAction") {
                        # Job is completed - save to history
                        Write-Host "    [COMPLETED] Updating execution history..." -ForegroundColor Green
                        
                        # Detect hollow success: job completed but retention hold prevented actual deletion
                        $versionsProcessed = if ($progress.VersionsProcessed) { [long]$progress.VersionsProcessed } else { [long]0 }
                        $versionsDeleted = if ($progress.VersionsDeleted) { [long]$progress.VersionsDeleted } else { [long]0 }
                        $storageReleased = if ($progress.StorageReleasedInBytes) { [long]$progress.StorageReleasedInBytes } else { [long]0 }
                        $isHollowSuccess = ($jobStatus -eq "CompleteSuccess" -or $jobStatus -eq "Completed") -and 
                                           $versionsProcessed -gt 0 -and $versionsDeleted -eq 0 -and $storageReleased -eq 0
                        
                        if ($isHollowSuccess) {
                            Write-Host "    [!!] HOLLOW SUCCESS - Versions processed ($versionsProcessed) but 0 deleted - retention hold likely active" -ForegroundColor Red
                            $jobStatus = "CompleteSuccessNoEffect"
                        }
                        
                        # Extract SPO-side job timing
                        $spoReqTime = if ($progress.RequestTimeInUTC) { ([DateTime]$progress.RequestTimeInUTC).ToString("o") } else { $null }
                        $spoCompTime = if ($progress.CompleteTimeInUTC) { ([DateTime]$progress.CompleteTimeInUTC).ToString("o") } else { $null }
                        $spoDuration = 0
                        if ($progress.RequestTimeInUTC -and $progress.CompleteTimeInUTC) {
                            try { $spoDuration = [math]::Round(([DateTime]$progress.CompleteTimeInUTC - [DateTime]$progress.RequestTimeInUTC).TotalMinutes, 2) } catch { }
                        }
                        
                        # Save to execution history
                        Save-SiteExecutionHistory -SiteUrl $siteUrl -SiteTitle "" -JobType "BatchDelete" -ExecutionData @{
                            Status = $jobStatus
                            HollowSuccess = $isHollowSuccess
                            DurationMinutes = $spoDuration
                            SPORequestTime = $spoReqTime
                            SPOCompleteTime = $spoCompTime
                            SPOJobDurationMinutes = $spoDuration
                            WorkItemId = if ($progress.WorkItemId) { $progress.WorkItemId.ToString() } else { "" }
                            ListsProcessed = if ($progress.ListsProcessed) { $progress.ListsProcessed } else { 0 }
                            ListsSynced = 0
                            FilesProcessed = if ($progress.FilesProcessed) { $progress.FilesProcessed } else { 0 }
                            VersionsProcessed = if ($progress.VersionsProcessed) { [long]$progress.VersionsProcessed } else { [long]0 }
                            VersionsDeleted = if ($progress.VersionsDeleted) { [long]$progress.VersionsDeleted } else { [long]0 }
                            StorageReleasedBytes = if ($progress.StorageReleasedInBytes) { [long]$progress.StorageReleasedInBytes } else { [long]0 }
                            StorageBeforeBytes = if ($progress.InitialStorageUsedBytes) { [long]$progress.InitialStorageUsedBytes } else { [long]0 }
                            StorageAfterBytes = [long]0
                        }
                        
                        # Also append to CSV
                        $csvLine = "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}" -f `
                            (Get-Date).ToString("o"),
                            $siteUrl,
                            "BatchDelete",
                            $(if ($progress.WorkItemId) { $progress.WorkItemId.ToString() } else { "" }),
                            $jobStatus,
                            "",
                            (Get-Date).ToString("o"),
                            0,
                            $(if ($progress.ListsProcessed) { $progress.ListsProcessed } else { 0 }),
                            0,
                            0,
                            $(if ($progress.FilesProcessed) { $progress.FilesProcessed } else { 0 }),
                            $(if ($progress.VersionsProcessed) { $progress.VersionsProcessed } else { 0 }),
                            $(if ($progress.VersionsDeleted) { $progress.VersionsDeleted } else { 0 }),
                            0,
                            $(if ($progress.StorageReleasedInBytes) { $progress.StorageReleasedInBytes } else { 0 }),
                            $(if ($progress.StorageReleasedInBytes) { [math]::Round($progress.StorageReleasedInBytes / 1MB, 2) } else { 0 }),
                            "",
                            $(if ($progress.InitialStorageUsedBytes) { $progress.InitialStorageUsedBytes } else { 0 }),
                            ""
                        
                        Write-ToCsvSafe -FilePath $script:ExecutionHistoryFile -Content $csvLine
                        
                        $updatedCount++
                    }
                    elseif ($jobStatus -eq "InProgress" -or $jobStatus -eq "Queued" -or $jobStatus -eq "New") {
                        Write-Host "    [PENDING] Job still running" -ForegroundColor Yellow
                        $stillPendingCount++
                    }
                    else {
                        Write-Host "    [INFO] Status: $jobStatus" -ForegroundColor Gray
                    }
                }
                else {
                    Write-Host "    [INFO] No job progress found" -ForegroundColor Gray
                }
            }
            catch {
                Write-Host "    [ERROR] Could not check status: $_" -ForegroundColor Red
            }
        }
        
        Write-Host ""
        Write-Host "  Sync complete: $updatedCount updated, $stillPendingCount still pending" -ForegroundColor Cyan
        Write-Host ""
    }
    catch {
        Write-Warning "Error synchronizing pending job status: $_"
    }
}

function Sync-ExternalJobResults {
    <#
    .SYNOPSIS
        Syncs job results from external script executions to the Dashboard
    .DESCRIPTION
        For each site in the provided list, checks Get-SPOSiteFileVersionBatchDeleteJobProgress
        and Get-SPOSiteManageVersionPolicyJobProgress. If there are completed jobs
        from the last 7 days that aren't already in our records, adds them to the
        Dashboard so they show as completed instead of "Waiting..."
    .PARAMETER Sites
        Array of site objects to check (must have Url property)
    .PARAMETER DaysToCheck
        Number of days to look back for external job results (default: 7)
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory=$true)]
        [array]$Sites,
        
        [int]$DaysToCheck = 7
    )
    
    Write-Host ""
    Write-Host "=== Checking for External Job Results ===" -ForegroundColor Cyan
    Write-Host "  Sites to check: $($Sites.Count)" -ForegroundColor Gray
    Write-Host "  Looking back: $DaysToCheck days" -ForegroundColor Gray
    
    $cutoffDate = (Get-Date).AddDays(-$DaysToCheck)
    $externalJobsFound = 0
    $externalSyncJobs = @()
    $externalDeleteJobs = @()
    
    # Read existing JobStatus.json to get current completed jobs
    $jobStatusFile = Join-Path $script:ConfigPath "JobStatus.json"
    $existingCompletedJobs = @()
    $existingJobUrls = @{}
    
    if (Test-Path $jobStatusFile) {
        try {
            $jobStatus = Get-Content $jobStatusFile -Raw | ConvertFrom-Json
            if ($jobStatus.RecentCompletedJobs) {
                $existingCompletedJobs = @($jobStatus.RecentCompletedJobs)
                foreach ($job in $existingCompletedJobs) {
                    $key = "$($job.JobType)_$($job.SiteUrl.TrimEnd('/').ToLower())"
                    $existingJobUrls[$key] = $true
                }
            }
        } catch {
            Write-Host "  [WARN] Could not read existing job status" -ForegroundColor Yellow
        }
    }
    
    Write-Host "  Existing completed jobs in Dashboard: $($existingCompletedJobs.Count)" -ForegroundColor Gray
    Write-Host ""
    
    $siteCount = 0
    foreach ($site in $Sites) {
        $siteCount++
        $siteUrl = if ($site.Url) { $site.Url } elseif ($site.SiteUrl) { $site.SiteUrl } else { $site.ToString() }
        $siteUrlNormalized = $siteUrl.TrimEnd("/").ToLower()
        
        # Progress indicator every 10 sites
        if ($siteCount % 10 -eq 0) {
            Write-Host "  Checking site $siteCount of $($Sites.Count)..." -ForegroundColor Gray
        }
        
        # Check for SyncListPolicy job
        $syncKey = "SyncListPolicy_$siteUrlNormalized"
        if (-not $existingJobUrls.ContainsKey($syncKey)) {
            try {
                $syncProgress = Get-SPOSiteManageVersionPolicyJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                
                if ($syncProgress -and ($syncProgress.Status -eq "CompleteSuccess" -or $syncProgress.Status -eq "Completed")) {
                    # Check if LastProcessTime is within our date range
                    $lastProcessTime = $null
                    if ($syncProgress.LastProcessTime) {
                        try {
                            $lastProcessTime = [DateTime]::Parse($syncProgress.LastProcessTime.ToString())
                        } catch { }
                    }
                    
                    $isRecent = $true  # Default to include if we can't parse date
                    if ($lastProcessTime -and $lastProcessTime -lt $cutoffDate) {
                        $isRecent = $false
                    }
                    
                    if ($isRecent) {
                        Write-Host "  [EXTERNAL SYNC] $siteUrl" -ForegroundColor Green
                        Write-Host "    Status: $($syncProgress.Status) | Lists: $($syncProgress.ListsProcessed)" -ForegroundColor Gray
                        
                        $syncJob = @{
                            SiteUrl = $siteUrl
                            WorkItemId = if ($syncProgress.WorkItemId) { $syncProgress.WorkItemId.ToString() } else { "EXT_SYNC_$siteUrlNormalized" }
                            JobType = "SyncListPolicy"
                            Status = $syncProgress.Status
                            StartTime = if ($lastProcessTime) { $lastProcessTime.ToString("o") } else { (Get-Date).AddHours(-1).ToString("o") }
                            EndTime = if ($lastProcessTime) { $lastProcessTime.ToString("o") } else { (Get-Date).ToString("o") }
                            DurationMinutes = 0
                            ListsProcessed = if ($syncProgress.ListsProcessed) { $syncProgress.ListsProcessed } else { 0 }
                            ListsSynced = if ($syncProgress.ListsSynced) { $syncProgress.ListsSynced } else { 0 }
                            FilesProcessed = 0
                            VersionsProcessed = 0
                            VersionsDeleted = 0
                            StorageReleasedInBytes = 0
                            ExternalSource = $true
                        }
                        $externalSyncJobs += $syncJob
                        $externalJobsFound++
                    }
                }
            } catch {
                # Silently continue if we can't check this site
            }
        }
        
        # Check for BatchDelete job
        $deleteKey = "BatchDelete_$siteUrlNormalized"
        if (-not $existingJobUrls.ContainsKey($deleteKey)) {
            try {
                $deleteProgress = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                
                if ($deleteProgress -and ($deleteProgress.Status -eq "CompleteSuccess" -or $deleteProgress.Status -eq "Completed")) {
                    # Check if LastProcessTime is within our date range
                    $lastProcessTime = $null
                    if ($deleteProgress.LastProcessTime) {
                        try {
                            $lastProcessTime = [DateTime]::Parse($deleteProgress.LastProcessTime.ToString())
                        } catch { }
                    }
                    
                    $isRecent = $true  # Default to include if we can't parse date
                    if ($lastProcessTime -and $lastProcessTime -lt $cutoffDate) {
                        $isRecent = $false
                    }
                    
                    if ($isRecent) {
                        Write-Host "  [EXTERNAL DELETE] $siteUrl" -ForegroundColor Yellow
                        Write-Host "    Status: $($deleteProgress.Status) | Files: $($deleteProgress.FilesProcessed) | Versions: $($deleteProgress.VersionsDeleted) | Released: $([math]::Round($deleteProgress.StorageReleasedInBytes / 1GB, 2)) GB" -ForegroundColor Gray
                        
                        $deleteJob = @{
                            SiteUrl = $siteUrl
                            WorkItemId = if ($deleteProgress.WorkItemId) { $deleteProgress.WorkItemId.ToString() } else { "EXT_DEL_$siteUrlNormalized" }
                            JobType = "BatchDelete"
                            Status = $deleteProgress.Status
                            StartTime = if ($lastProcessTime) { $lastProcessTime.AddMinutes(-5).ToString("o") } else { (Get-Date).AddHours(-1).ToString("o") }
                            EndTime = if ($lastProcessTime) { $lastProcessTime.ToString("o") } else { (Get-Date).ToString("o") }
                            DurationMinutes = 5
                            ListsProcessed = if ($deleteProgress.ListsProcessed) { $deleteProgress.ListsProcessed } else { 0 }
                            ListsSynced = 0
                            FilesProcessed = if ($deleteProgress.FilesProcessed) { $deleteProgress.FilesProcessed } else { 0 }
                            VersionsProcessed = if ($deleteProgress.VersionsProcessed) { $deleteProgress.VersionsProcessed } else { 0 }
                            VersionsDeleted = if ($deleteProgress.VersionsDeleted) { $deleteProgress.VersionsDeleted } else { 0 }
                            StorageReleasedInBytes = if ($deleteProgress.StorageReleasedInBytes) { $deleteProgress.StorageReleasedInBytes } else { 0 }
                            InitialStorageUsedBytes = if ($deleteProgress.InitialStorageUsedBytes) { $deleteProgress.InitialStorageUsedBytes } else { 0 }
                            ExternalSource = $true
                        }
                        $externalDeleteJobs += $deleteJob
                        $externalJobsFound++
                    }
                }
            } catch {
                # Silently continue if we can't check this site
            }
        }
    }
    
    if ($externalJobsFound -gt 0) {
        Write-Host ""
        Write-Host "  Found $externalJobsFound external job(s): $($externalSyncJobs.Count) SYNC, $($externalDeleteJobs.Count) DELETE" -ForegroundColor Cyan
        
        # Merge external jobs with existing completed jobs
        $allCompletedJobs = @($existingCompletedJobs) + @($externalSyncJobs) + @($externalDeleteJobs)
        
        # Update JobStatus.json
        try {
            $jobStatus = @{
                LastUpdated = (Get-Date).ToString("o")
                ActiveJobs = @()
                QueuedSites = @()
                QueuedSitesCount = 0
                QueuedSitesSyncCount = 0
                QueuedSitesDeleteCount = 0
                RecentCompletedJobs = $allCompletedJobs
                CompletedJobsCount = $allCompletedJobs.Count
                MajorVersionLimit = $script:CurrentMajorVersionLimit
                MajorWithMinorVersionsLimit = $script:CurrentMajorWithMinorVersionsLimit
            }
            
            # Read existing file to preserve other properties
            if (Test-Path $jobStatusFile) {
                $existing = Get-Content $jobStatusFile -Raw | ConvertFrom-Json
                if ($existing.ActiveJobs) { $jobStatus.ActiveJobs = $existing.ActiveJobs }
                if ($existing.QueuedSites) { 
                    $jobStatus.QueuedSites = $existing.QueuedSites 
                    $jobStatus.QueuedSitesCount = $existing.QueuedSites.Count
                }
                if ($existing.MajorVersionLimit) { $jobStatus.MajorVersionLimit = $existing.MajorVersionLimit }
                if ($existing.MajorWithMinorVersionsLimit) { $jobStatus.MajorWithMinorVersionsLimit = $existing.MajorWithMinorVersionsLimit }
            }
            
            $jobStatus | ConvertTo-Json -Depth 10 | Set-Content -Path $jobStatusFile -Encoding UTF8
            Write-Host "  [OK] Dashboard updated with external job results" -ForegroundColor Green
        } catch {
            Write-Warning "  Could not update JobStatus.json: $_"
        }
        
        # Also save to execution history
        foreach ($job in ($externalSyncJobs + $externalDeleteJobs)) {
            Save-SiteExecutionHistory -SiteUrl $job.SiteUrl -SiteTitle "" -JobType $job.JobType -ExecutionData @{
                Status = $job.Status
                DurationMinutes = $job.DurationMinutes
                WorkItemId = $job.WorkItemId
                ListsProcessed = $job.ListsProcessed
                ListsSynced = $job.ListsSynced
                FilesProcessed = $job.FilesProcessed
                VersionsProcessed = $job.VersionsProcessed
                VersionsDeleted = $job.VersionsDeleted
                StorageReleasedBytes = $job.StorageReleasedInBytes
                StorageBeforeBytes = if ($job.InitialStorageUsedBytes) { $job.InitialStorageUsedBytes } else { 0 }
                StorageAfterBytes = 0
                ExternalSource = $true
            }
        }
    } else {
        Write-Host "  No new external job results found." -ForegroundColor Gray
    }
    
    Write-Host ""
}

function Start-SPOVersionPolicyOrchestration {
    [CmdletBinding()]
    param(
        [int]$MaxConcurrentJobs = 10,
        [int]$MajorVersionLimit = 4,
        [int]$MajorWithMinorVersionsLimit = 4,
        [int]$CheckBatchSize = 10,
        [int]$CheckBatchDelaySeconds = 2,
        [switch]$Resume,
        [switch]$UseFileCache,
        [switch]$ManageRetentionPolicy,
        [switch]$DeleteOnly,
        [switch]$SyncOnly
    )
    
    $script:CurrentMajorVersionLimit = $MajorVersionLimit
    $script:CurrentMajorWithMinorVersionsLimit = $MajorWithMinorVersionsLimit
    
    # Load Dashboard config for reexecution settings
    $dashboardConfig = Get-DashboardConfig
    $reexecutionDays = "0"
    if ($dashboardConfig) {
        $configValue = $dashboardConfig.ReexecutionDays
        Write-Host "  [DEBUG] DashboardConfig.ReexecutionDays raw value: '$configValue' (type: $($configValue.GetType().Name))" -ForegroundColor DarkGray
        if ($null -ne $configValue -and $configValue -ne "") {
            if ($configValue -eq "ask") { 
                $reexecutionDays = "ask" 
            } else { 
                $reexecutionDays = [string]$configValue 
            }
        }
    } else {
        Write-Host "  [DEBUG] DashboardConfig not found or null" -ForegroundColor DarkGray
    }
    Write-Host "  [DEBUG] Final reexecutionDays: '$reexecutionDays'" -ForegroundColor DarkGray
    
    # Initialize user choice for "ask" mode
    $script:ReexecutionUserChoice = $null  # Values: "all", "none", or $null for individual prompts
    
    Write-Host "Starting version policy orchestration..." -ForegroundColor Cyan
    Write-Host "  Max Parallel Jobs: $MaxConcurrentJobs" -ForegroundColor Gray
    Write-Host "  Major Version Limit: $MajorVersionLimit" -ForegroundColor Gray
    Write-Host "  Minor Version Limit: $MajorWithMinorVersionsLimit" -ForegroundColor Gray
    if ($reexecutionDays -eq "ask") {
        Write-Host "  Reexecution Mode: ASK (prompt for recently processed sites)" -ForegroundColor Yellow
    }
    elseif ($reexecutionDays -ne "0" -and [int]$reexecutionDays -gt 0) {
        Write-Host "  Reexecution Interval: $reexecutionDays day(s) (skip recently processed sites)" -ForegroundColor Yellow
    }
    else {
        Write-Host "  Reexecution Interval: DISABLED (process all sites)" -ForegroundColor Gray
    }
    
    if ($DeleteOnly) {
        Write-Host "  Mode: DELETE ONLY (skip SyncListPolicy, run BatchDelete directly)" -ForegroundColor Yellow
    }
    if ($SyncOnly) {
        Write-Host "  Mode: SYNC ONLY (run SyncListPolicy, skip BatchDelete)" -ForegroundColor Yellow
    }
    if ($Resume) {
        Write-Host "  Mode: RESUMING previous execution" -ForegroundColor Yellow
    }
    
    # Sync pending job status before starting - check if any jobs completed while script was stopped
    Sync-PendingJobStatus
    
    # Export retention policy database for Dashboard if retention management is enabled
    if ($ManageRetentionPolicy) {
        try {
            $retStatus = Get-RetentionPolicyManagerStatus
            if ($retStatus.Connected) {
                $retDb = Export-RetentionPolicyDatabase -OutputPath (Join-Path $script:ConfigPath "RetentionPolicyDatabase.json")
                
                # Display all SharePoint retention policies upfront
                if ($retDb -and $retDb.Policies -and $retDb.Policies.Count -gt 0) {
                    Write-Host ""
                    Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
                    Write-Host "  ║  RETENTION POLICIES AFFECTING SHAREPOINT                     ║" -ForegroundColor Cyan
                    Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
                    Write-Host ""
                    $polIdx = 0
                    foreach ($pol in $retDb.Policies) {
                        $polIdx++
                        $statusLabel = if ($pol.Enabled) { "Enabled" } else { "Disabled" }
                        $statusColor = if ($pol.Enabled) { "Green" } else { "DarkGray" }
                        
                        Write-Host "  [$polIdx] $($pol.Name)" -ForegroundColor White
                        Write-Host "      Status: $statusLabel | Mode: $($pol.Mode)" -ForegroundColor $statusColor
                        
                        # SharePoint Classic/Communication sites
                        if ($pol.HasSPClassic) {
                            $spScope = if ($pol.SPClassicInclusionType -eq "AllSites") { "ALL sites" } else { "$($pol.IncludedSiteCount) specific site(s)" }
                            Write-Host "      SP Classic/Communication sites: $spScope" -ForegroundColor Gray
                            
                            if ($pol.SPClassicInclusionType -eq "ExplicitSites" -and $pol.IncludedSites.Count -gt 0) {
                                foreach ($incSite in $pol.IncludedSites) {
                                    Write-Host "        + $incSite" -ForegroundColor DarkCyan
                                }
                            }
                            
                            if ($pol.ExcludedSiteCount -gt 0) {
                                Write-Host "      SP Exceptions: $($pol.ExcludedSiteCount) site(s) excluded (capacity: $($pol.ExcludedSiteCount)/$($pol.ExceptionLimit))" -ForegroundColor Yellow
                                foreach ($excEntry in $pol.ExcludedSites) {
                                    $excLabel = if ($excEntry.SuspendedByUs) { " (suspended by us)" } else { "" }
                                    Write-Host "        - $($excEntry.SiteUrl)$excLabel" -ForegroundColor DarkYellow
                                }
                            } else {
                                Write-Host "      SP Exceptions: None" -ForegroundColor Gray
                            }
                        }
                        
                        # Microsoft 365 Group sites
                        if ($pol.HasM365Groups) {
                            $m365Scope = if ($pol.M365GroupInclusionType -eq "AllGroups") { "ALL M365 Groups" } else { "$($pol.M365GroupIncludedCount) specific group(s)" }
                            Write-Host "      M365 Group sites:              $m365Scope" -ForegroundColor Gray
                            
                            if ($pol.M365GroupInclusionType -eq "ExplicitGroups" -and $pol.M365GroupIncludedCount -gt 0) {
                                foreach ($incGrp in $pol.M365GroupIncluded) {
                                    Write-Host "        + $incGrp" -ForegroundColor DarkCyan
                                }
                            }
                            
                            if ($pol.M365GroupExcludedCount -gt 0) {
                                Write-Host "      M365 Group Exceptions: $($pol.M365GroupExcludedCount) group(s) excluded" -ForegroundColor Yellow
                                foreach ($excGrp in $pol.M365GroupExcluded) {
                                    Write-Host "        - $excGrp" -ForegroundColor DarkYellow
                                }
                            } else {
                                Write-Host "      M365 Group Exceptions: None" -ForegroundColor Gray
                            }
                        }
                        
                        Write-Host ""
                    }
                    Write-Host "  Total: $($retDb.TotalPolicies) SharePoint retention policy(ies)" -ForegroundColor Cyan
                    Write-Host ""
                } else {
                    Write-Host ""
                    Write-Host "  [RETENTION] No SharePoint retention policies found in tenant" -ForegroundColor Gray
                    Write-Host ""
                }
                
                # Alert user if retention capacity will limit concurrency
                $retCapacity = Get-RetentionPolicyAvailableCapacity
                if ($retCapacity -ne [int]::MaxValue -and $retCapacity -lt $MaxConcurrentJobs) {
                    Write-Host ""
                    Write-Host "  ╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Yellow
                    Write-Host "  ║  ⚠️  RETENTION POLICY CONCURRENCY LIMIT                      ║" -ForegroundColor Yellow
                    Write-Host "  ║                                                              ║" -ForegroundColor Yellow
                    Write-Host "  ║  MaxConcurrentJobs = $($MaxConcurrentJobs.ToString().PadRight(5))                                    ║" -ForegroundColor Yellow
                    Write-Host "  ║  Retention exception slots available = $($retCapacity.ToString().PadRight(5))                ║" -ForegroundColor Yellow
                    Write-Host "  ║                                                              ║" -ForegroundColor Yellow
                    Write-Host "  ║  Effective concurrency will be dynamically capped at $($retCapacity.ToString().PadRight(5))    ║" -ForegroundColor Yellow
                    Write-Host "  ║  during BatchDelete phase (max 90 sites per policy).         ║" -ForegroundColor Yellow
                    Write-Host "  ║  Slots free up as jobs complete and policies are resumed.     ║" -ForegroundColor Yellow
                    Write-Host "  ╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Yellow
                    Write-Host ""
                }
            }
        }
        catch {
            Write-Warning "Could not export retention policy database: $_"
        }
    }
    
    # If resuming, use the saved queue from JobStatus.json
    $sitesToProcess = @()
    
    if ($Resume) {
        $jobStatusFile = Join-Path $script:ConfigPath "JobStatus.json"
        if (Test-Path $jobStatusFile) {
            try {
                $existingStatus = Get-Content $jobStatusFile -Raw | ConvertFrom-Json
                if ($existingStatus.QueuedSites -and $existingStatus.QueuedSites.Count -gt 0) {
                    Write-Host "  Resuming $($existingStatus.QueuedSites.Count) sites from previous queue..." -ForegroundColor Cyan
                    $sitesToProcess = $existingStatus.QueuedSites
                }
            }
            catch {
                Write-Warning "Error reading saved queue: $_. Processing all sites."
                $Resume = $false
            }
        } else {
            Write-Warning "Status file not found. Processing all sites."
            $Resume = $false
        }
    }
    
    # If not resuming or failed to read queue, get all sites
    if (-not $Resume -or $sitesToProcess.Count -eq 0) {
        # Queue was empty or not resuming - treat as new processing
        $Resume = $false
        # Get sites
        if ($UseFileCache) {
            $allSites = Get-AllTenantSites -UseFileCache
        } else {
            $allSites = Get-AllTenantSites
        }
        
        # Apply inclusion/exclusion filters
        $sitesToProcess = Apply-SiteFilters -Sites $allSites
    }
    
    Write-Host "  Sites to process: $($sitesToProcess.Count)" -ForegroundColor Green
    
    if ($sitesToProcess.Count -eq 0) {
        Write-Host "No site to process." -ForegroundColor Yellow
        return
    }
    
    # Sync external job results - check for jobs completed by other scripts
    # This ensures the Dashboard shows accurate status for jobs run outside this script
    Sync-ExternalJobResults -Sites $sitesToProcess -DaysToCheck 7
    
    # Check for sites with VersionCount=0 or VersionSize=0
    $sitesZeroVersions = @($sitesToProcess | Where-Object { 
        ($_.VersionCount -eq 0 -or $null -eq $_.VersionCount) -and 
        ($_.VersionSize -eq 0 -or $null -eq $_.VersionSize) 
    })
    
    $skipBatchDeleteForZeroVersionSites = $false
    $skipZeroVersionSites = $false
    
    if ($sitesZeroVersions.Count -gt 0) {
        # Check Dashboard config for pre-configured action
        $dashboardConfig = Get-DashboardConfig
        $configuredAction = if ($dashboardConfig -and $dashboardConfig.ZeroVersionAction) { $dashboardConfig.ZeroVersionAction } else { "ask" }
        
        Write-Host ""
        Write-Host "  ================================================================" -ForegroundColor Yellow
        Write-Host "  SITES WITH ZERO VERSIONS DETECTED" -ForegroundColor Yellow
        Write-Host "  ================================================================" -ForegroundColor Yellow
        Write-Host "  Found $($sitesZeroVersions.Count) sites with VersionCount=0 and VersionSize=0" -ForegroundColor Yellow
        
        # If configured action in Dashboard, use it automatically
        if ($configuredAction -ne "ask") {
            Write-Host "  Dashboard config: ZeroVersionAction = '$configuredAction'" -ForegroundColor Cyan
            
            switch ($configuredAction) {
                "skip" {
                    $skipZeroVersionSites = $true
                    $sitesToProcess = @($sitesToProcess | Where-Object { 
                        -not (($_.VersionCount -eq 0 -or $null -eq $_.VersionCount) -and 
                              ($_.VersionSize -eq 0 -or $null -eq $_.VersionSize))
                    })
                    Write-Host "  [OK] $($sitesZeroVersions.Count) sites removed from processing (config: skip)" -ForegroundColor Yellow
                    Write-Host "  Remaining sites: $($sitesToProcess.Count)" -ForegroundColor Cyan
                }
                "syncOnly" {
                    $skipBatchDeleteForZeroVersionSites = $true
                    Write-Host "  [OK] Zero-version sites will only run SyncJob (config: syncOnly)" -ForegroundColor Green
                }
                "both" {
                    Write-Host "  [OK] All jobs will run for all sites (config: both)" -ForegroundColor Yellow
                }
            }
        }
        else {
            # Ask user interactively
            Write-Host "  BatchDelete job makes no sense on sites without versions." -ForegroundColor Gray
            Write-Host ""
            Write-Host "  Options:" -ForegroundColor White
            Write-Host "  [S] SyncJob only - Process these sites with Sync only (no BatchDelete)" -ForegroundColor Green
            Write-Host "  [B] Both jobs   - Process both jobs anyway (Sync + BatchDelete)" -ForegroundColor Yellow
            Write-Host "  [N] None/Skip   - Skip these sites entirely" -ForegroundColor Gray
            Write-Host ""
            Write-Host "  TIP: Configure this in Dashboard Settings to skip this prompt" -ForegroundColor DarkGray
            Write-Host ""
            $zeroVersionOption = Read-Host "  Choose an option (S/B/N)"
            
            if ($zeroVersionOption -eq 'N' -or $zeroVersionOption -eq 'n') {
                $skipZeroVersionSites = $true
                # Remove zero-version sites from the list
                $sitesToProcess = @($sitesToProcess | Where-Object { 
                    -not (($_.VersionCount -eq 0 -or $null -eq $_.VersionCount) -and 
                          ($_.VersionSize -eq 0 -or $null -eq $_.VersionSize))
                })
                Write-Host ""
                Write-Host "  [OK] $($sitesZeroVersions.Count) sites removed from processing" -ForegroundColor Yellow
                Write-Host "  Remaining sites: $($sitesToProcess.Count)" -ForegroundColor Cyan
            }
            elseif ($zeroVersionOption -eq 'S' -or $zeroVersionOption -eq 's') {
                $skipBatchDeleteForZeroVersionSites = $true
                Write-Host ""
                Write-Host "  [OK] Zero-version sites will only run SyncJob (no BatchDelete)" -ForegroundColor Green
            }
            else {
                Write-Host ""
                Write-Host "  [OK] All jobs will run for all sites" -ForegroundColor Yellow
            }
        }
        
        if ($sitesToProcess.Count -eq 0) {
            Write-Host "No site remaining to process after filter." -ForegroundColor Yellow
            return
        }
    }
    
    # Create site queue
    $siteQueue = [System.Collections.Generic.Queue[object]]::new()
    
    # If resuming, sites are already in the correct format (QueuedSites from JSON)
    if ($Resume) {
        $resumeFallbackPhase = if ($DeleteOnly) { "BatchDelete" } elseif ($SyncOnly) { "SyncListPolicy" } else { "SyncListPolicy" }
        foreach ($site in $sitesToProcess) {
            # Convert PSCustomObject to hashtable if necessary
            $siteData = @{
                Url = $site.Url
                Title = $site.Title
                Phase = if ($site.Phase) { $site.Phase } else { $resumeFallbackPhase }
                StorageUsedMB = if ($site.StorageUsedMB) { $site.StorageUsedMB } else { 0 }
                StorageUsedBytes = if ($site.StorageUsedBytes) { $site.StorageUsedBytes } else { 0 }
                VersionCount = if ($site.VersionCount) { $site.VersionCount } else { 0 }
                VersionSizeBytes = if ($site.VersionSizeBytes) { $site.VersionSizeBytes } else { 0 }
                VersionSizeMB = if ($site.VersionSizeMB) { $site.VersionSizeMB } else { 0 }
                VersionSizeGB = if ($site.VersionSizeGB) { $site.VersionSizeGB } else { 0 }
                LastContentModifiedDate = if ($site.LastContentModifiedDate -is [DateTime]) { $site.LastContentModifiedDate.ToString("o") } else { $site.LastContentModifiedDate }
                Template = $site.Template
                Owner = $site.Owner
            }
            $siteQueue.Enqueue($siteData)
        }
    } else {
        # Create site queue with detailed information (new processing)
        $initialPhase = if ($DeleteOnly) { "BatchDelete" } else { "SyncListPolicy" }
        if ($SyncOnly) { $initialPhase = "SyncListPolicy" }
        foreach ($site in $sitesToProcess) {
            $siteQueue.Enqueue(@{
                Url = $site.Url
                Title = $site.Title
                Phase = $initialPhase
                StorageUsedMB = $site.StorageUsageCurrent
                StorageUsedBytes = $site.StorageUsageCurrent * 1MB
                VersionCount = if ($site.VersionCount) { $site.VersionCount } else { 0 }
                VersionSizeBytes = if ($site.VersionSize) { $site.VersionSize } else { 0 }
                VersionSizeMB = if ($site.VersionSize) { [math]::Round($site.VersionSize / 1MB, 2) } else { 0 }
                VersionSizeGB = if ($site.VersionSize) { [math]::Round($site.VersionSize / 1GB, 4) } else { 0 }
                LastContentModifiedDate = if ($site.LastContentModifiedDate) { if ($site.LastContentModifiedDate -is [DateTime]) { $site.LastContentModifiedDate.ToString("o") } else { $site.LastContentModifiedDate } } else { $null }
                Template = $site.Template
                Owner = $site.Owner
            })
        }
    }
    
    $activeJobs = @{}
    $completedJobs = @{}
    $skippedJobs = @{}
    $processedCount = 0
    $totalSites = $sitesToProcess.Count
    $previousActiveJobsCount = 0
    $previousQueuedCount = 0
    
    # If resuming, restore counters and active jobs
    if ($Resume) {
        $jobStatusFile = Join-Path $script:ConfigPath "JobStatus.json"
        if (Test-Path $jobStatusFile) {
            try {
                $existingStatus = Get-Content $jobStatusFile -Raw | ConvertFrom-Json
                $processedCount = if ($existingStatus.CompletedJobsCount) { $existingStatus.CompletedJobsCount } else { 0 }
                $previousActiveJobsCount = if ($existingStatus.ActiveJobs) { $existingStatus.ActiveJobs.Count } else { 0 }
                $previousQueuedCount = if ($existingStatus.QueuedSitesCount) { $existingStatus.QueuedSitesCount } else { 0 }
                $totalSites = $totalSites + $processedCount
                
                # Restore active jobs that were in progress
                if ($existingStatus.ActiveJobs -and $existingStatus.ActiveJobs.Count -gt 0) {
                    Write-Host "  Restoring $($existingStatus.ActiveJobs.Count) active jobs..." -ForegroundColor Yellow
                    foreach ($activeJob in $existingStatus.ActiveJobs) {
                        if ($activeJob.WorkItemId -and $activeJob.Url) {
                            $activeJobs[$activeJob.WorkItemId] = @{
                                Url = $activeJob.Url
                                Title = $activeJob.Title
                                Phase = if ($activeJob.Phase) { $activeJob.Phase } else { "SyncListPolicy" }
                                StartTime = if ($activeJob.StartTime) { [DateTime]::Parse($activeJob.StartTime) } else { Get-Date }
                                WorkItemId = $activeJob.WorkItemId
                                StorageUsedMB = $activeJob.StorageUsedMB
                                VersionSizeGB = $activeJob.VersionSizeGB
                            }
                        }
                    }
                    Write-Host "  [OK] $($activeJobs.Count) jobs restored" -ForegroundColor Green
                }
            }
            catch {
                Write-Warning "Error restoring previous state: $_"
            }
        }
    }
    
    Write-Host ""
    Write-Host "Processing $totalSites sites..." -ForegroundColor Cyan
    Write-Host "  Parallel job limit: $MaxConcurrentJobs" -ForegroundColor Gray
    if ($Resume) {
        Write-Host "" -ForegroundColor Gray
        Write-Host "  === Previous Session Summary ===" -ForegroundColor Magenta
        Write-Host "  Completed: $processedCount | Active: $previousActiveJobsCount | Pending: $previousQueuedCount" -ForegroundColor Cyan
        Write-Host "  ================================" -ForegroundColor Magenta
    }
    if ($Resume -and $processedCount -gt 0) {
        Write-Host "  Already processed: $processedCount" -ForegroundColor Green
        Write-Host "  Pending: $($siteQueue.Count)" -ForegroundColor Yellow
    }
    
    # Batch pre-suspend retention policies for all BatchDelete sites (one API call per policy)
    $batchRetentionMode = $false
    if ($ManageRetentionPolicy) {
        $retentionStatus = Get-RetentionPolicyManagerStatus
        if ($retentionStatus.Connected) {
            # Collect all BatchDelete site URLs from the queue
            $batchDeleteUrls = @($siteQueue.ToArray() | Where-Object { $_.Phase -eq "BatchDelete" } | ForEach-Object { $_.Url })
            if ($batchDeleteUrls.Count -gt 0) {
                Write-Host ""
                Write-Host "  [RETENTION-BATCH] Pre-suspending retention for $($batchDeleteUrls.Count) BatchDelete site(s)..." -ForegroundColor Yellow
                $batchResult = Suspend-BatchSiteRetentionPolicies -SiteUrls $batchDeleteUrls
                if ($batchResult.SuspendedCount -gt 0) {
                    $batchRetentionMode = $true
                    # Export updated retention state for Dashboard
                    try { $null = Export-RetentionPolicyDatabase -OutputPath (Join-Path $script:ConfigPath "RetentionPolicyDatabase.json") } catch { }
                    Write-Host "  [RETENTION-BATCH] $($batchResult.SuspendedCount) site(s) suspended - waiting for hold release..." -ForegroundColor Yellow
                    # Wait once for propagation (instead of per-site waits)
                    $holdReleased = Wait-RetentionPolicyRelease -SiteUrl $batchDeleteUrls[0] -MaxWaitMinutes 30 -CheckIntervalSeconds 60
                    if (-not $holdReleased) {
                        Write-Warning "  [RETENTION-BATCH] Hold not released in time - proceeding anyway"
                    }
                }
            }
        }
    }
    
    while ($siteQueue.Count -gt 0 -or $activeJobs.Count -gt 0) {
        
        # Start new jobs ONLY if there are available slots
        $jobsToStart = [Math]::Min($MaxConcurrentJobs - $activeJobs.Count, $siteQueue.Count)
        
        # Dynamically cap by retention policy exception capacity when managing retention
        if ($ManageRetentionPolicy -and $jobsToStart -gt 0) {
            $retentionCapacity = Get-RetentionPolicyAvailableCapacity
            if ($retentionCapacity -lt $jobsToStart) {
                if ($retentionCapacity -eq 0 -and $activeJobs.Count -gt 0) {
                    # No capacity - wait for active jobs to complete and resume their policies
                    Write-Host "  [RETENTION] Exception capacity exhausted (0 slots) - waiting for active jobs to complete and free slots..." -ForegroundColor DarkYellow
                    $jobsToStart = 0
                } elseif ($retentionCapacity -gt 0) {
                    Write-Host "  [RETENTION] Capping parallel jobs to $retentionCapacity (retention exception capacity limit)" -ForegroundColor DarkYellow
                    $jobsToStart = $retentionCapacity
                }
            }
        }
        
        for ($i = 0; $i -lt $jobsToStart; $i++) {
            $siteInfo = $siteQueue.Dequeue()
            $siteUrl = $siteInfo.Url
            $phase = $siteInfo.Phase
            
            try {
                # Comprehensive check: existing job AND reexecution interval
                $jobTypeToCheck = if ($phase -eq "SyncListPolicy") { "SyncListPolicy" } else { "BatchDelete" }
                $typeLabel = if ($phase -eq "SyncListPolicy") { "SYNC" } else { "DELETE" }
                
                $shouldProcess = Test-ShouldProcessSite -SiteUrl $siteUrl -JobType $jobTypeToCheck -ReexecutionDays $reexecutionDays
                
                if (-not $shouldProcess.ShouldProcess) {
                    $jobKey = "$(if ($phase -eq 'SyncListPolicy') { 'SYNC' } else { 'DELETE' })_" + $siteUrl.Replace("https://","").Replace("/","_")
                    
                    if ($shouldProcess.Reason -eq "ExistingJobRunning") {
                        # Job already exists and is running - monitor it instead of starting a new one
                        Write-Host "  [$typeLabel] Monitoring existing job: $siteUrl" -ForegroundColor Cyan
                        Write-Host "    [EXISTING] Job detected in progress (Status: $($shouldProcess.ExistingJob.Status)) - will wait for completion" -ForegroundColor Yellow
                        
                        # Add to active jobs for monitoring instead of starting a new job
                        $activeJobs[$jobKey] = @{
                            SiteUrl = $siteUrl
                            SiteTitle = $siteInfo.Title
                            WorkItemId = if ($shouldProcess.ExistingJob.WorkItemId) { $shouldProcess.ExistingJob.WorkItemId } else { $jobKey }
                            JobType = $phase
                            StartTime = (Get-Date).ToString("o")
                            Phase = $phase
                            StorageUsedMB = $siteInfo.StorageUsedMB
                            StorageUsedBytes = $siteInfo.StorageUsedBytes
                            VersionCount = $siteInfo.VersionCount
                            VersionSizeMB = $siteInfo.VersionSizeMB
                            VersionSizeGB = $siteInfo.VersionSizeGB
                            VersionSizeBytes = $siteInfo.VersionSizeBytes
                            LastContentModifiedDate = if ($siteInfo.LastContentModifiedDate -is [DateTime]) { $siteInfo.LastContentModifiedDate.ToString("o") } else { $siteInfo.LastContentModifiedDate }
                            Template = $siteInfo.Template
                            ExistingJob = $true
                        }
                    }
                    elseif ($shouldProcess.Reason -eq "RecentlyProcessed") {
                        # Site was recently processed - skip it
                        Write-Host "  [$typeLabel] Skipping: $siteUrl" -ForegroundColor DarkGray
                        Write-Host "    [SKIPPED] $($shouldProcess.SkipReason)" -ForegroundColor DarkGray
                        
                        # Add to skipped jobs without processing
                        $skippedJobs[$jobKey] = @{
                            SiteUrl = $siteUrl
                            SiteTitle = $siteInfo.Title
                            JobType = $phase
                            Status = "Skipped"
                            Reason = "RecentlyProcessed"
                            LastExecution = $shouldProcess.LastExecution
                            Phase = $phase
                        }
                    }
                    continue
                }
                
                # Handle "ask" mode - prompt user for recently processed sites
                if ($shouldProcess.NeedsUserConfirmation) {
                    $jobKey = "$(if ($phase -eq 'SyncListPolicy') { 'SYNC' } else { 'DELETE' })_" + $siteUrl.Replace("https://","").Replace("/","_")
                    $lastExec = $shouldProcess.LastExecution
                    
                    Write-Host ""
                    Write-Host "  [$typeLabel] Site processed recently: $siteUrl" -ForegroundColor Yellow
                    Write-Host "    Last processed: $([Math]::Round($lastExec.DaysSinceExecution, 1)) days ago ($($lastExec.ExecutedAtDisplay))" -ForegroundColor Yellow
                    Write-Host "    Status: $($lastExec.Status) | Versions deleted: $($lastExec.VersionsDeleted)" -ForegroundColor Yellow
                    Write-Host ""
                    $userChoice = Read-Host "    Process again? [Y]es / [S]kip / [A]ll remaining / [N]one remaining"
                    
                    if ($userChoice -eq 'A' -or $userChoice -eq 'a') {
                        # Process all remaining without asking
                        $script:ReexecutionUserChoice = "all"
                        Write-Host "    [OK] Will process all remaining sites without asking" -ForegroundColor Green
                    }
                    elseif ($userChoice -eq 'N' -or $userChoice -eq 'n') {
                        # Skip all remaining
                        $script:ReexecutionUserChoice = "none"
                        Write-Host "    [OK] Will skip all remaining recently processed sites" -ForegroundColor Gray
                        
                        # Skip this site
                        $skippedJobs[$jobKey] = @{
                            SiteUrl = $siteUrl
                            SiteTitle = $siteInfo.Title
                            JobType = $phase
                            Status = "Skipped"
                            Reason = "UserSkipped"
                            LastExecution = $lastExec
                            Phase = $phase
                        }
                        continue
                    }
                    elseif ($userChoice -eq 'S' -or $userChoice -eq 's') {
                        # Skip just this site
                        Write-Host "    [SKIPPED] User opted to skip" -ForegroundColor DarkGray
                        $skippedJobs[$jobKey] = @{
                            SiteUrl = $siteUrl
                            SiteTitle = $siteInfo.Title
                            JobType = $phase
                            Status = "Skipped"
                            Reason = "UserSkipped"
                            LastExecution = $lastExec
                            Phase = $phase
                        }
                        continue
                    }
                    # Y or default - proceed with processing
                    Write-Host "    [OK] Proceeding with processing" -ForegroundColor Green
                }
                
                # Check if user chose to skip all remaining (from previous "N" choice)
                if ($script:ReexecutionUserChoice -eq "none" -and $shouldProcess.NeedsUserConfirmation) {
                    $jobKey = "$(if ($phase -eq 'SyncListPolicy') { 'SYNC' } else { 'DELETE' })_" + $siteUrl.Replace("https://","").Replace("/","_")
                    Write-Host "  [$typeLabel] Skipping: $siteUrl (user chose 'None remaining')" -ForegroundColor DarkGray
                    $skippedJobs[$jobKey] = @{
                        SiteUrl = $siteUrl
                        SiteTitle = $siteInfo.Title
                        JobType = $phase
                        Status = "Skipped"
                        Reason = "UserSkippedAll"
                        Phase = $phase
                    }
                    continue
                }
                
                # Site should be processed - start a new job
                if ($phase -eq "SyncListPolicy") {
                    Write-Host "  [SYNC] Starting: $siteUrl" -ForegroundColor Cyan
                    
                    # Call the actual Sync cmdlet
                    $jobResult = New-SPOSiteManageVersionPolicyJob -Identity $siteUrl -SyncListPolicy -ErrorAction Stop
                    
                    # Try to get WorkItemId from return, otherwise use Get-SPOSiteManageVersionPolicyJobProgress
                    $workItemId = $null
                    if ($jobResult -and $jobResult.WorkItemId) {
                        $workItemId = $jobResult.WorkItemId.ToString()
                    } else {
                        # Use Get-SPOSiteManageVersionPolicyJobProgress to get the WorkItemId
                        $progressInfo = Get-SPOSiteManageVersionPolicyJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                        if ($progressInfo -and $progressInfo.WorkItemId) {
                            $workItemId = $progressInfo.WorkItemId.ToString()
                        }
                    }
                    # Fallback to synthetic key if no WorkItemId was obtained
                    if (-not $workItemId) {
                        $workItemId = "SYNC_" + $siteUrl.Replace("https://","").Replace("/","_")
                    }
                    $jobKey = "SYNC_" + $siteUrl.Replace("https://","").Replace("/","_")
                    
                    $activeJobs[$jobKey] = @{
                        SiteUrl = $siteUrl
                        SiteTitle = $siteInfo.Title
                        WorkItemId = $workItemId
                        JobType = "SyncListPolicy"
                        StartTime = (Get-Date).ToString("o")
                        Phase = $phase
                        # Site information
                        StorageUsedMB = $siteInfo.StorageUsedMB
                        StorageUsedBytes = $siteInfo.StorageUsedBytes
                        VersionCount = $siteInfo.VersionCount
                        VersionSizeMB = $siteInfo.VersionSizeMB
                        VersionSizeGB = $siteInfo.VersionSizeGB
                        VersionSizeBytes = $siteInfo.VersionSizeBytes
                        LastContentModifiedDate = if ($siteInfo.LastContentModifiedDate -is [DateTime]) { $siteInfo.LastContentModifiedDate.ToString("o") } else { $siteInfo.LastContentModifiedDate }
                        Template = $siteInfo.Template
                        ExistingJob = $false
                    }
                    Write-Host "    [OK] New SyncListPolicy job started for: $siteUrl" -ForegroundColor Green
                }
                elseif ($phase -eq "BatchDelete") {
                    Write-Host "  [DELETE] Starting: $siteUrl" -ForegroundColor Yellow
                    
                    # Suspend retention policies if enabled
                    $retentionData = $null
                    $retentionFailed = $false
                    $retentionAtCapacity = $false
                    if ($ManageRetentionPolicy) {
                        $retentionStatus = Get-RetentionPolicyManagerStatus
                        if ($retentionStatus.Connected) {
                            if ($batchRetentionMode) {
                                # Batch mode: site was already pre-suspended, just get the summary
                                $normalizedCheck = $siteUrl.TrimEnd("/").ToLower()
                                $suspendedState = Get-RetentionPolicyManagerStatus
                                if ($suspendedState.SuspendedSites -and $suspendedState.SuspendedSites.ContainsKey($normalizedCheck)) {
                                    Write-Host "    [RETENTION] Already suspended via batch mode" -ForegroundColor DarkGray
                                    $retentionData = Get-SiteRetentionSummary -SiteUrl $siteUrl
                                } else {
                                    # Site wasn't included in batch (maybe added later) — suspend individually
                                    $suspended = Suspend-SiteRetentionPolicy -SiteUrl $siteUrl
                                    if ($suspended -eq $true) {
                                        $retentionData = Get-SiteRetentionSummary -SiteUrl $siteUrl
                                        if ($retentionData) {
                                            $holdReleased = Wait-RetentionPolicyRelease -SiteUrl $siteUrl -MaxWaitMinutes 30 -CheckIntervalSeconds 60
                                            if (-not $holdReleased) {
                                                Write-Warning "    [RETENTION] Hold not released in time for $siteUrl - proceeding anyway"
                                            }
                                        }
                                    } elseif ($null -eq $suspended) {
                                        $retentionAtCapacity = $true
                                    } else {
                                        $retentionFailed = $true
                                    }
                                }
                            } else {
                                # Per-site mode: suspend individually
                                $suspended = Suspend-SiteRetentionPolicy -SiteUrl $siteUrl
                                if ($suspended -eq $true) {
                                    $retentionData = Get-SiteRetentionSummary -SiteUrl $siteUrl
                                    # Only wait for hold release if policies were actually suspended
                                    if ($retentionData) {
                                        $holdReleased = Wait-RetentionPolicyRelease -SiteUrl $siteUrl -MaxWaitMinutes 30 -CheckIntervalSeconds 60
                                        if (-not $holdReleased) {
                                            Write-Warning "    [RETENTION] Hold not released in time for $siteUrl - proceeding anyway"
                                        }
                                    }
                                } elseif ($null -eq $suspended) {
                                    # At capacity (100 exception limit) - re-queue to try later
                                    Write-Host "    [RETENTION] Exception list at capacity - re-queuing $siteUrl for later" -ForegroundColor DarkYellow
                                    $retentionAtCapacity = $true
                                } else {
                                    # Suspension failed - skip this site to avoid deleting versions while retention is active
                                    Write-Warning "    [RETENTION] Could not suspend retention for $siteUrl - SKIPPING BatchDelete to protect data"
                                    $retentionFailed = $true
                                }
                            }
                        }
                    }
                    
                    # Re-queue site if retention exception list is at capacity (try again after other jobs complete)
                    if ($retentionAtCapacity) {
                        $siteQueue.Enqueue(@{
                            Url = $siteUrl
                            Title = $siteInfo.Title
                            Phase = "BatchDelete"
                            InitialStorageBytes = $siteInfo.InitialStorageBytes
                            StorageUsedMB = $siteInfo.StorageUsedMB
                            StorageUsedBytes = $siteInfo.StorageUsedBytes
                            VersionCount = $siteInfo.VersionCount
                            VersionSizeMB = $siteInfo.VersionSizeMB
                            VersionSizeGB = $siteInfo.VersionSizeGB
                            VersionSizeBytes = $siteInfo.VersionSizeBytes
                            LastContentModifiedDate = $siteInfo.LastContentModifiedDate
                            Template = $siteInfo.Template
                            Owner = $siteInfo.Owner
                        })
                        continue
                    }
                    
                    # Skip BatchDelete if retention suspension failed
                    if ($retentionFailed) {
                        $skippedJobs[$siteUrl] = @{
                            SiteUrl = $siteUrl
                            Reason = "RetentionSuspendFailed"
                            SkippedAt = (Get-Date).ToString("o")
                        }
                        $processedCount++
                        continue
                    }
                    
                    # Use New-SPOSiteFileVersionBatchDeleteJob
                    $jobResult = New-SPOSiteFileVersionBatchDeleteJob -Identity $siteUrl -MajorVersionLimit $MajorVersionLimit -MajorWithMinorVersionsLimit $MajorWithMinorVersionsLimit -Confirm:$false -ErrorAction Stop
                    
                    # Try to get WorkItemId from return, otherwise use Get-SPOSiteFileVersionBatchDeleteJobProgress
                    $workItemId = $null
                    if ($jobResult -and $jobResult.WorkItemId) {
                        $workItemId = $jobResult.WorkItemId.ToString()
                    } else {
                        # Use Get-SPOSiteFileVersionBatchDeleteJobProgress to get the WorkItemId
                        $progressInfo = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                        if ($progressInfo -and $progressInfo.WorkItemId) {
                            $workItemId = $progressInfo.WorkItemId.ToString()
                        }
                    }
                    # Fallback to synthetic key if no WorkItemId was obtained
                    if (-not $workItemId) {
                        $workItemId = "DELETE_" + $siteUrl.Replace("https://","").Replace("/","_")
                    }
                    $jobKey = "DELETE_" + $siteUrl.Replace("https://","").Replace("/","_")
                    
                    $activeJobs[$jobKey] = @{
                        SiteUrl = $siteUrl
                        SiteTitle = $siteInfo.Title
                        WorkItemId = $workItemId
                        JobType = "BatchDelete"
                        StartTime = (Get-Date).ToString("o")
                        Phase = $phase
                        InitialStorageBytes = $siteInfo.InitialStorageBytes
                        # Site information
                        StorageUsedMB = $siteInfo.StorageUsedMB
                        StorageUsedBytes = $siteInfo.StorageUsedBytes
                        VersionCount = $siteInfo.VersionCount
                        VersionSizeMB = $siteInfo.VersionSizeMB
                        VersionSizeGB = $siteInfo.VersionSizeGB
                        VersionSizeBytes = $siteInfo.VersionSizeBytes
                        LastContentModifiedDate = if ($siteInfo.LastContentModifiedDate -is [DateTime]) { $siteInfo.LastContentModifiedDate.ToString("o") } else { $siteInfo.LastContentModifiedDate }
                        Template = $siteInfo.Template
                        ExistingJob = $false
                        # Retention policy data (if managed)
                        RetentionData = $retentionData
                    }
                    Write-Host "    [OK] New BatchDelete job started for: $siteUrl" -ForegroundColor Green
                }
            }
            catch {
                $errorMessage = $_.Exception.Message
                
                # Check if error is because a job is already running
                if ($errorMessage -match "previous work item is still in progress" -or 
                    $errorMessage -match "already.*in progress" -or
                    $errorMessage -match "work item.*running") {
                    
                    # Job already running - add to monitoring queue instead of failing
                    $typeLabel = if ($phase -eq "SyncListPolicy") { "SYNC" } else { "DELETE" }
                    $jobKey = "$(if ($phase -eq 'SyncListPolicy') { 'SYNC' } else { 'DELETE' })_" + $siteUrl.Replace("https://","").Replace("/","_")
                    
                    Write-Host "    [EXISTING] Job already in progress - adding to monitoring queue" -ForegroundColor Yellow
                    
                    # Try to get the existing job progress
                    $existingProgress = $null
                    try {
                        if ($phase -eq "SyncListPolicy") {
                            $existingProgress = Get-SPOSiteManageVersionPolicyJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                        } else {
                            $existingProgress = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $siteUrl -ErrorAction SilentlyContinue
                        }
                    } catch { }
                    
                    $activeJobs[$jobKey] = @{
                        SiteUrl = $siteUrl
                        SiteTitle = $siteInfo.Title
                        WorkItemId = if ($existingProgress -and $existingProgress.WorkItemId) { $existingProgress.WorkItemId.ToString() } else { $jobKey }
                        JobType = $phase
                        StartTime = (Get-Date).ToString("o")
                        Phase = $phase
                        StorageUsedMB = $siteInfo.StorageUsedMB
                        StorageUsedBytes = $siteInfo.StorageUsedBytes
                        VersionCount = $siteInfo.VersionCount
                        VersionSizeMB = $siteInfo.VersionSizeMB
                        VersionSizeGB = $siteInfo.VersionSizeGB
                        VersionSizeBytes = $siteInfo.VersionSizeBytes
                        LastContentModifiedDate = if ($siteInfo.LastContentModifiedDate -is [DateTime]) { $siteInfo.LastContentModifiedDate.ToString("o") } else { $siteInfo.LastContentModifiedDate }
                        Template = $siteInfo.Template
                        ExistingJob = $true
                    }
                }
                else {
                    # Check if site is blocked/archived/read-only
                    if ($errorMessage -match "Access to this Web site has been blocked" -or
                        $errorMessage -match "site.*has been archived" -or
                        $errorMessage -match "read.?only" -or
                        $errorMessage -match "site.*locked") {
                        Write-Warning "  [SKIP] Site is blocked/archived/read-only: $siteUrl"
                        Write-Warning "         Error: $errorMessage"
                        $skippedJobs[$siteUrl] = @{
                            SiteUrl = $siteUrl
                            Reason = "SiteBlocked"
                            Error = $errorMessage
                            SkippedAt = (Get-Date).ToString("o")
                        }
                        $processedCount++
                        
                        # Resume retention if it was suspended for this site
                        if ($ManageRetentionPolicy -and $phase -eq "BatchDelete") {
                            try {
                                $null = Resume-SiteRetentionPolicy -SiteUrl $siteUrl
                                $null = Export-RetentionPolicyDatabase -OutputPath (Join-Path $script:ConfigPath "RetentionPolicyDatabase.json")
                            } catch { }
                        }
                    }
                    else {
                        # Other error - show warning
                        Write-Warning "Error starting job for $siteUrl : $_"
                    }
                }
            }
        }
        
        # Show current status
        Write-Host ""
        Write-Host "  === Status: $($activeJobs.Count) active | $($siteQueue.Count) queued | $($completedJobs.Count) completed | $($skippedJobs.Count) skipped ===" -ForegroundColor Magenta
        Write-Host "  Waiting $($script:PollingIntervalSeconds) seconds before checking progress..." -ForegroundColor Gray
        
        # Wait before checking progress
        if ($activeJobs.Count -gt 0) {
            Start-Sleep -Seconds $script:PollingIntervalSeconds
        }
        
        # Update status
        $queuedList = @()
        $tempQueue = $siteQueue.ToArray()
        foreach ($item in $tempQueue) {
            $queuedList += $item
        }
        
        $activeList = @()
        foreach ($job in $activeJobs.Values) {
            $activeList += $job
        }
        
        Update-JobStatus -ActiveJobs $activeList -QueuedSites $queuedList -RecentCompletedJobs @($completedJobs.Values) -MajorVersionLimit $MajorVersionLimit -MajorWithMinorVersionsLimit $MajorWithMinorVersionsLimit -DeleteOnly:$DeleteOnly -SyncOnly:$SyncOnly -ManageRetentionPolicy:$ManageRetentionPolicy
        
        # Check status of active jobs
        $completedWorkItems = @()
        
        foreach ($workItemId in @($activeJobs.Keys)) {
            $job = $activeJobs[$workItemId]
            
            try {
                # Use Get-SPOSiteFileVersionBatchDeleteJobProgress for BatchDelete
                # Use Get-SPOSiteManageVersionPolicyJobProgress for Sync
                if ($job.JobType -eq "BatchDelete") {
                    $progress = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $job.SiteUrl -ErrorAction Stop
                } else {
                    $progress = Get-SPOSiteManageVersionPolicyJobProgress -Identity $job.SiteUrl -ErrorAction Stop
                }
                
                $jobStatus = $progress.Status
                Write-Host "    Checking $($job.JobType) $($job.SiteUrl): $jobStatus" -ForegroundColor Gray
                
                if ($jobStatus -eq "Completed" -or $jobStatus -eq "CompleteSuccess" -or $jobStatus -eq "CompleteFailed" -or $jobStatus -eq "CompleteNoAction" -or $jobStatus -eq "Failed") {
                    $completedWorkItems += $workItemId
                    
                    $duration = ((Get-Date) - [DateTime]::Parse($job.StartTime)).TotalMinutes
                    
                    # Extract SPO-side job timing from progress object
                    $spoRequestTime = if ($progress.RequestTimeInUTC) { $progress.RequestTimeInUTC } else { $null }
                    $spoCompleteTime = if ($progress.CompleteTimeInUTC) { $progress.CompleteTimeInUTC } else { $null }
                    $spoLastProcessTime = if ($progress.LastProcessTimeInUTC) { $progress.LastProcessTimeInUTC } else { $null }
                    $spoJobDurationMinutes = 0
                    if ($spoRequestTime -and $spoCompleteTime) {
                        try {
                            $spoJobDurationMinutes = [math]::Round(([DateTime]$spoCompleteTime - [DateTime]$spoRequestTime).TotalMinutes, 2)
                        } catch { }
                    }
                    
                    # Resolve real SPO WorkItemId: prefer progress (freshest), then job record, then synthetic key
                    $resolvedWorkItemId = if ($progress.WorkItemId) { $progress.WorkItemId.ToString() } elseif ($job.WorkItemId) { $job.WorkItemId } else { $workItemId }

                    $completedJob = @{
                        SiteUrl = $job.SiteUrl
                        WorkItemId = $resolvedWorkItemId
                        JobType = $job.JobType
                        Status = $progress.Status
                        StartTime = $job.StartTime
                        EndTime = (Get-Date).ToString("o")
                        DurationMinutes = [math]::Round($duration, 2)
                        SPORequestTime = if ($spoRequestTime) { ([DateTime]$spoRequestTime).ToString("o") } else { $null }
                        SPOCompleteTime = if ($spoCompleteTime) { ([DateTime]$spoCompleteTime).ToString("o") } else { $null }
                        SPOJobDurationMinutes = $spoJobDurationMinutes
                        ListsProcessed = $progress.ListsProcessed
                        ListsSynced = $progress.ListsSynced
                        FilesProcessed = $progress.FilesProcessed
                        VersionsProcessed = $progress.VersionsProcessed
                        VersionsDeleted = $progress.VersionsDeleted
                        StorageReleasedInBytes = $progress.StorageReleasedInBytes
                    }
                    
                    $completedJobs[$workItemId] = $completedJob
                    $processedCount++
                    
                    # Detect "hollow success" - job reported CompleteSuccess but retention hold prevented actual deletions
                    # Pattern: BatchDelete + CompleteSuccess + VersionsProcessed > 0 + VersionsDeleted = 0 + StorageReleased = 0
                    $isHollowSuccess = $false
                    if ($job.JobType -eq "BatchDelete" -and 
                        ($progress.Status -eq "CompleteSuccess" -or $progress.Status -eq "Completed") -and
                        [long]$progress.VersionsProcessed -gt 0 -and 
                        [long]$progress.VersionsDeleted -eq 0 -and 
                        [long]$progress.StorageReleasedInBytes -eq 0) {
                        $isHollowSuccess = $true
                        Write-Host "    [!] HOLLOW SUCCESS detected: Job completed but 0 versions deleted (retention hold likely still active)" -ForegroundColor Red
                        Write-Host "        Files: $($progress.FilesProcessed) | Versions scanned: $($progress.VersionsProcessed) | Deleted: 0 | Freed: 0 B" -ForegroundColor DarkYellow
                    }
                    
                    # Build execution data with optional retention info
                    $versionsKept = [Math]::Max([long]0, ([long]$progress.VersionsProcessed - [long]$progress.VersionsDeleted))
                    $versionSizeBefore = if ($job.VersionSizeBytes) { [long]$job.VersionSizeBytes } else { [long]0 }
                    $versionSizeAfter = if ($versionSizeBefore -gt 0 -and $progress.StorageReleasedInBytes) {
                        [Math]::Max([long]0, $versionSizeBefore - [long]$progress.StorageReleasedInBytes)
                    } else { [long]0 }
                    
                    $execData = @{
                        Status = if ($isHollowSuccess) { "CompleteSuccessNoEffect" } else { $progress.Status }
                        HollowSuccess = $isHollowSuccess
                        DurationMinutes = [math]::Round($duration, 2)
                        StartTime = $job.StartTime
                        SPORequestTime = if ($spoRequestTime) { ([DateTime]$spoRequestTime).ToString("o") } else { $null }
                        SPOCompleteTime = if ($spoCompleteTime) { ([DateTime]$spoCompleteTime).ToString("o") } else { $null }
                        SPOJobDurationMinutes = $spoJobDurationMinutes
                        WorkItemId = $resolvedWorkItemId
                        ListsProcessed = $progress.ListsProcessed
                        ListsSynced = $progress.ListsSynced
                        FilesProcessed = $progress.FilesProcessed
                        VersionsProcessed = $progress.VersionsProcessed
                        VersionsDeleted = $progress.VersionsDeleted
                        StorageReleasedBytes = if ($progress.StorageReleasedInBytes) { [long]$progress.StorageReleasedInBytes } else { [long]0 }
                        StorageBeforeBytes = if ($job.StorageUsedBytes) { [long]$job.StorageUsedBytes } else { [long]0 }
                        StorageAfterBytes = if ($job.StorageUsedBytes -and $progress.StorageReleasedInBytes) { [long]$job.StorageUsedBytes - [long]$progress.StorageReleasedInBytes } else { [long]0 }
                        VersionsKept = $versionsKept
                        VersionSizeBeforeBytes = $versionSizeBefore
                        VersionSizeAfterBytes = $versionSizeAfter
                        SiteStorageUsedMB = if ($job.StorageUsedMB) { $job.StorageUsedMB } else { 0 }
                        VersionSizeBeforeMB = [math]::Round($versionSizeBefore / 1MB, 2)
                    }
                    
                    # Add retention data if this job had retention managed
                    if ($job.RetentionData) {
                        $execData.RetentionManaged = $true
                        $execData.RetentionPolicies = $job.RetentionData.RetentionPolicies
                        $execData.RetentionSuspendedAt = $job.RetentionData.RetentionSuspendedAt
                        $execData.RetentionResumedAt = (Get-Date).ToString("o")
                        if ($job.RetentionData.RetentionSuspendedAt) {
                            $execData.RetentionWaitMinutes = [math]::Round(((Get-Date) - [DateTime]::Parse($job.RetentionData.RetentionSuspendedAt)).TotalMinutes, 1)
                        }
                    }
                    
                    # Save execution to site history
                    Save-SiteExecutionHistory -SiteUrl $job.SiteUrl -SiteTitle $job.SiteTitle -JobType $job.JobType -ExecutionData $execData
                    
                    $statusIcon = if ($isHollowSuccess) { "[!!]" } elseif ($progress.Status -eq "CompleteSuccess") { "[OK]" } else { "[!]" }
                    $statusColor = if ($isHollowSuccess) { "Red" } elseif ($progress.Status -eq "CompleteSuccess") { "Green" } else { "Yellow" }
                    
                    $statusText = if ($isHollowSuccess) { "$($progress.Status) (RETENTION BLOCKED - no versions deleted)" } else { $progress.Status }
                    Write-Host "  $statusIcon $($job.JobType): $($job.SiteUrl) - $statusText" -ForegroundColor $statusColor
                    
                    # Resume retention policies after BatchDelete completes
                    if ($ManageRetentionPolicy -and $job.JobType -eq "BatchDelete") {
                        try {
                            $null = Resume-SiteRetentionPolicy -SiteUrl $job.SiteUrl
                            # Export updated retention state for Dashboard
                            $null = Export-RetentionPolicyDatabase -OutputPath (Join-Path $script:ConfigPath "RetentionPolicyDatabase.json")
                        }
                        catch {
                            Write-Warning "    [RETENTION] Error resuming policies for $($job.SiteUrl): $_"
                        }
                    }
                    
                    # If it was SyncListPolicy (Report), add BatchDelete to queue
                    if ($job.JobType -eq "SyncListPolicy" -and ($progress.Status -eq "Completed" -or $progress.Status -eq "CompleteSuccess")) {
                        # Check if this is a zero-version site that should skip BatchDelete
                        $isZeroVersionSite = ($job.VersionCount -eq 0 -or $null -eq $job.VersionCount) -and ($job.VersionSizeBytes -eq 0 -or $null -eq $job.VersionSizeBytes)
                        
                        if ($SyncOnly) {
                            Write-Host "    -> SyncOnly mode - skipping BatchDelete" -ForegroundColor Gray
                        } elseif ($skipBatchDeleteForZeroVersionSites -and $isZeroVersionSite) {
                            Write-Host "    -> Skipping BatchDelete (zero-version site)" -ForegroundColor Gray
                        } else {
                            $storageInfo = Get-SPOSiteStorageInfo -SiteUrl $job.SiteUrl
                            $siteQueue.Enqueue(@{
                                Url = $job.SiteUrl
                                Title = $job.SiteTitle
                                Phase = "BatchDelete"
                                InitialStorageBytes = if ($storageInfo) { $storageInfo.StorageUsedBytes } else { 0 }
                                # Keep site information from the original job
                                StorageUsedMB = $job.StorageUsedMB
                                StorageUsedBytes = $job.StorageUsedBytes
                                VersionCount = if ($storageInfo) { $storageInfo.VersionCount } else { $job.VersionCount }
                                VersionSizeMB = if ($storageInfo) { $storageInfo.VersionSizeMB } else { $job.VersionSizeMB }
                                VersionSizeGB = if ($storageInfo) { $storageInfo.VersionSizeGB } else { $job.VersionSizeGB }
                                VersionSizeBytes = if ($storageInfo) { $storageInfo.VersionSizeBytes } else { $job.VersionSizeBytes }
                                LastContentModifiedDate = if ($storageInfo) { $storageInfo.LastContentModifiedDate } else { $job.LastContentModifiedDate }
                                Template = $job.Template
                            })
                            Write-Host "    -> Added to BatchDelete queue" -ForegroundColor Cyan
                        }
                    }
                    
                    # Log CSV
                    $csvLine = "{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13},{14},{15},{16},{17},{18},{19}" -f `
                        (Get-Date).ToString("o"),
                        $job.SiteUrl,
                        $job.JobType,
                        $resolvedWorkItemId,
                        $progress.Status,
                        $job.StartTime,
                        (Get-Date).ToString("o"),
                        [math]::Round($duration, 2),
                        $progress.ListsProcessed,
                        $progress.ListsSynced,
                        $progress.ListSyncFailed,
                        $progress.FilesProcessed,
                        $progress.VersionsProcessed,
                        $progress.VersionsDeleted,
                        $progress.VersionsFailed,
                        $progress.StorageReleasedInBytes,
                        [math]::Round($progress.StorageReleasedInBytes / 1MB, 2),
                        "",
                        $job.InitialStorageBytes,
                        ""
                    
                    Write-ToCsvSafe -FilePath $script:ExecutionHistoryFile -Content $csvLine
                }
            }
            catch {
                Write-Warning "Error checking job progress $workItemId : $_"
            }
        }
        
        # Remove completed jobs
        foreach ($workItemId in $completedWorkItems) {
            $activeJobs.Remove($workItemId)
        }
    }
    
    Write-Host ""
    Write-Host "Orchestration completed!" -ForegroundColor Green
    Write-Host "  Sites processed: $processedCount" -ForegroundColor Cyan
    Write-Host "  Jobs completed: $($completedJobs.Count)" -ForegroundColor Cyan
    Write-Host "  Jobs skipped: $($skippedJobs.Count)" -ForegroundColor Gray
    
    # Update final status
    Update-JobStatus -ActiveJobs @() -QueuedSites @() -RecentCompletedJobs @($completedJobs.Values) -MajorVersionLimit $MajorVersionLimit -MajorWithMinorVersionsLimit $MajorWithMinorVersionsLimit -DeleteOnly:$DeleteOnly -SyncOnly:$SyncOnly -ManageRetentionPolicy:$ManageRetentionPolicy

    # Resume all suspended retention policies as safety net
    if ($ManageRetentionPolicy) {
        try {
            Resume-AllSuspendedSites
        }
        catch {
            Write-Warning "Error resuming retention policies: $_"
        }
        # Export final retention policy state for Dashboard
        try {
            $retStatus = Get-RetentionPolicyManagerStatus
            if ($retStatus.Connected) {
                $null = Export-RetentionPolicyDatabase -OutputPath (Join-Path $script:ConfigPath "RetentionPolicyDatabase.json")
            }
        }
        catch {
            Write-Warning "Could not export final retention policy database: $_"
        }
    }

    # Capture end-of-session tenant storage snapshot for timeline
    try {
        if (Test-Path $script:TenantStorageFile) {
            $tenantSnap = Get-Content $script:TenantStorageFile -Raw | ConvertFrom-Json
            if ($tenantSnap) {
                Save-TenantStorageSnapshot -TenantStorage $tenantSnap -Trigger "SessionEnd"
            }
        }
    }
    catch {
        Write-Warning "Could not save end-of-session storage snapshot: $_"
    }
}

function Apply-SiteFilters {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Sites
    )
    
    $filteredSites = @($Sites)
    
    # INCLUSION filter - check global variable (defined by SPOSiteFilters)
    # Note: If the inclusion list was used in Get-AllTenantSites, sites were already filtered
    # This filter is redundant but kept for safety
    $includedUrls = @()
    if ($Global:SPOIncludedSites -and $Global:SPOIncludedSites.Count -gt 0) {
        foreach ($url in $Global:SPOIncludedSites) {
            $includedUrls += $url.TrimEnd("/").ToLower()
        }
    }
    # Fallback to module local variable
    elseif ($script:IncludedSites -and $script:IncludedSites.Count -gt 0) {
        foreach ($url in $script:IncludedSites) {
            $includedUrls += $url.TrimEnd("/").ToLower()
        }
    }
    
    if ($includedUrls.Count -gt 0) {
        Write-Host "  Checking INCLUSION filter ($($includedUrls.Count) sites)..." -ForegroundColor Cyan
        
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
    
    # EXCLUSION filter - check multiple sources
    $excludedUrls = @()
    
    # 1. Check module variable
    if ($script:ExcludedSites -and $script:ExcludedSites.Count -gt 0) {
        foreach ($url in $script:ExcludedSites) {
            $excludedUrls += $url.TrimEnd("/").ToLower()
        }
    }
    
    # 2. Check global variable (defined by SPOSiteFilters)
    if ($Global:SPOExcludedSites -and $Global:SPOExcludedSites.Count -gt 0) {
        foreach ($url in $Global:SPOExcludedSites) {
            $normalizedUrl = $url.TrimEnd("/").ToLower()
            if ($excludedUrls -notcontains $normalizedUrl) {
                $excludedUrls += $normalizedUrl
            }
        }
    }
    
    # 3. Check JSON file as fallback
    if ($excludedUrls.Count -eq 0 -and (Test-Path $script:ExcludedSitesFile)) {
        try {
            $savedExclusion = Get-Content $script:ExcludedSitesFile -Raw | ConvertFrom-Json
            if ($savedExclusion.Sites -and $savedExclusion.Sites.Count -gt 0) {
                foreach ($site in $savedExclusion.Sites) {
                    $siteUrl = if ($site.Url) { $site.Url } else { $site }
                    $excludedUrls += $siteUrl.TrimEnd("/").ToLower()
                }
                Write-Host "  Exclusion list loaded from file: $($excludedUrls.Count) sites" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Warning "Error reading exclusion file: $_"
        }
    }
    
    # Apply exclusion filter
    if ($excludedUrls.Count -gt 0) {
        Write-Host "  Applying EXCLUSION filter ($($excludedUrls.Count) sites)..." -ForegroundColor Yellow
        
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

function Export-AllSitesDataForDashboard {
    <#
    .SYNOPSIS
        Exports data from all tenant sites to JSON used by Dashboard
    .DESCRIPTION
        Gets detailed data from all sites and saves to AllSites.json
        for display on the "SharePoint Sites List" tab of the Dashboard
    .PARAMETER IncludePersonalSites
        Whether to include personal sites (OneDrive)
    .PARAMETER InputFile
        Optional CSV file with specific site list
    .PARAMETER Upsert
        If specified, merges with existing data (inserts new, updates existing)
    .PARAMETER MarkProcessingTargets
        If specified, marks sites in inclusion list as ProcessingTarget for Dashboard highlighting
    #>
    [CmdletBinding()]
    param(
        [switch]$IncludePersonalSites,
        [string]$InputFile,
        [switch]$Upsert,
        [switch]$MarkProcessingTargets
    )
    
    Write-Host "Exporting site data for Dashboard..." -ForegroundColor Cyan
    
    # Load existing data if Upsert is enabled
    $existingDataMap = @{}
    
    if ($Upsert -and (Test-Path $script:AllSitesFile)) {
        Write-Host "  Loading existing data for merge (upsert)..." -ForegroundColor Cyan
        try {
            $existingData = Get-Content $script:AllSitesFile -Raw | ConvertFrom-Json
            if ($existingData.Sites) {
                foreach ($site in $existingData.Sites) {
                    $normalizedUrl = $site.Url.TrimEnd("/").ToLower()
                    $existingDataMap[$normalizedUrl] = $site
                }
                Write-Host "    [OK] $($existingDataMap.Count) sites loaded from cache" -ForegroundColor Green
            }
        }
        catch {
            Write-Warning "  [!] Error loading existing data: $_"
        }
    }
    
    $allSitesData = @()
    
    # Get LastActivityDate from Graph API first (used in both cases)
    Write-Host "  Getting Last Activity from Graph API..." -ForegroundColor Cyan
    $siteActivityMap = @{}
    try {
        $tempFile = Join-Path $env:TEMP "SiteUsageDetail_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
        $oldProgress = $ProgressPreference
        $ProgressPreference = 'SilentlyContinue'
        try {
            $null = & { Get-MgReportSharePointSiteUsageDetail -Period "D180" -OutFile $tempFile -ErrorAction Stop } *>&1 | Where-Object { $_ -is [System.Management.Automation.ErrorRecord] }
        }
        finally {
            $ProgressPreference = $oldProgress
        }
        
        if (Test-Path $tempFile) {
            $usageData = Import-Csv -Path $tempFile -ErrorAction Stop
            foreach ($row in $usageData) {
                $siteUrl = $row.'Site URL'
                $lastActivity = $row.'Last Activity Date'
                if ($siteUrl -and $lastActivity) {
                    $normalizedUrl = $siteUrl.TrimEnd("/").ToLower()
                    $siteActivityMap[$normalizedUrl] = $lastActivity
                }
            }
            Remove-Item $tempFile -Force -ErrorAction SilentlyContinue
            Write-Host "    [OK] LastActivity obtained for $($siteActivityMap.Count) sites" -ForegroundColor Green
        }
    }
    catch {
        Write-Warning "  [!] Error getting LastActivity from Graph: $_"
    }
    
    # Build list of input sites for marking as ProcessingTarget (if InputFile provided)
    $inputSiteUrls = @{}
    if ($InputFile -and (Test-Path $InputFile)) {
        Write-Host "  Reading input sites list for marking: $InputFile" -ForegroundColor Gray
        try {
            $inputSites = Import-Csv -Path $InputFile -ErrorAction Stop
            foreach ($row in $inputSites) {
                $siteUrl = if ($row.SiteUrl) { $row.SiteUrl } elseif ($row.Url) { $row.Url } else { $null }
                if ($siteUrl) {
                    $normalizedUrl = $siteUrl.TrimEnd("/").ToLower()
                    $inputSiteUrls[$normalizedUrl] = $true
                }
            }
            Write-Host "    [OK] $($inputSiteUrls.Count) sites in input list (will be marked as ProcessingTarget)" -ForegroundColor Green
        }
        catch {
            Write-Warning "    [!] Error reading input file: $_"
        }
    }
    
    # ALWAYS get all tenant sites (not just input file sites)
    $allSites = Get-AllTenantSites -IncludePersonalSites:$IncludePersonalSites
    
    Write-Host "    [OK] $($allSites.Count) sites obtained" -ForegroundColor Green
    Write-Host "  Processing site data locally..." -ForegroundColor Gray
    
    $total = $allSites.Count
    $current = 0
    
    # Iterate locally - no additional API calls
    foreach ($site in $allSites) {
        $current++
        $percentComplete = [math]::Round(($current / $total) * 100, 0)
        Write-Progress -Activity "Processing site data" -Status "$current of $total sites" -PercentComplete $percentComplete
        
        # Get LastActivityDate from Graph
        $normalizedUrl = $site.Url.TrimEnd("/").ToLower()
        $lastActivityDate = $null
        if ($siteActivityMap.ContainsKey($normalizedUrl)) {
            $lastActivityDate = $siteActivityMap[$normalizedUrl]
        }
        
        # Use site data directly (already obtained with -Detailed)
        $siteData = ConvertTo-SiteDataObject -Site $site -LastActivityDate $lastActivityDate
        
        # Mark as ProcessingTarget if site is in input list
        if ($inputSiteUrls.Count -gt 0 -and $inputSiteUrls.ContainsKey($normalizedUrl)) {
            $siteData | Add-Member -NotePropertyName "ProcessingTarget" -NotePropertyValue $true -Force
        }
        
        $allSitesData += $siteData
    }
    
    Write-Progress -Activity "Processing site data" -Completed
    
    # If Upsert is enabled, merge with existing data
    $finalSitesData = @()
    $insertedCount = 0
    $updatedCount = 0
    
    if ($Upsert -and $existingDataMap.Count -gt 0) {
        Write-Host "  Performing merge (upsert) of data..." -ForegroundColor Cyan
        $newSitesMap = @{}
        
        # Build map of new data
        foreach ($site in $allSitesData) {
            $normalizedUrl = $site.Url.TrimEnd("/").ToLower()
            $newSitesMap[$normalizedUrl] = $site
            
            if ($existingDataMap.ContainsKey($normalizedUrl)) {
                $updatedCount++
            } else {
                $insertedCount++
            }
        }
        
        # Merge: update existing with new data, keep those not in new
        foreach ($key in $existingDataMap.Keys) {
            if ($newSitesMap.ContainsKey($key)) {
                # Site exists in both - use updated data
                $finalSitesData += $newSitesMap[$key]
            } else {
                # Site exists only in cache - keep old data
                $finalSitesData += $existingDataMap[$key]
            }
        }
        
        # Add new sites (that didn't exist in cache)
        foreach ($key in $newSitesMap.Keys) {
            if (-not $existingDataMap.ContainsKey($key)) {
                $finalSitesData += $newSitesMap[$key]
            }
        }
        
        Write-Host "    [OK] Merge completed: $insertedCount new, $updatedCount updated" -ForegroundColor Green
    } else {
        # Without merge, use obtained data directly
        $finalSitesData = $allSitesData
    }
    
    # Mark ProcessingTargets if requested
    if ($MarkProcessingTargets) {
        $inclusionList = Get-SiteInclusionList
        if ($inclusionList -and $inclusionList.Count -gt 0) {
            Write-Host "  Marking processing target sites..." -ForegroundColor Cyan
            $inclusionNormalized = @($inclusionList | ForEach-Object { $_.TrimEnd("/").ToLower() })
            $markedCount = 0
            
            foreach ($site in $finalSitesData) {
                $normalizedUrl = $site.Url.TrimEnd("/").ToLower()
                if ($inclusionNormalized -contains $normalizedUrl) {
                    $site | Add-Member -NotePropertyName "ProcessingTarget" -NotePropertyValue $true -Force
                    $markedCount++
                } else {
                    $site | Add-Member -NotePropertyName "ProcessingTarget" -NotePropertyValue $false -Force
                }
            }
            Write-Host "    [OK] $markedCount sites marked as processing targets" -ForegroundColor Green
        }
    }
    
    # Create final object
    $exportData = @{
        LastUpdated = (Get-Date).ToString("o")
        ExportedAt = (Get-Date).ToString("dd/MM/yyyy HH:mm:ss")
        TotalSites = $finalSitesData.Count
        Sites = $finalSitesData
    }
    
    # Save to JSON file
    $exportData | ConvertTo-Json -Depth 10 | Set-Content -Path $script:AllSitesFile -Encoding UTF8
    
    Write-Host "  [OK] Exported $($finalSitesData.Count) sites to: $script:AllSitesFile" -ForegroundColor Green
    
    return $exportData
}

function ConvertTo-SiteDataObject {
    <#
    .SYNOPSIS
        Converts SPOSite object to standardized format for the Dashboard
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        $Site,
        
        [Parameter()]
        [string]$LastActivityDate
    )
    
    # Calculate VersionSize in MB and GB
    $versionSizeBytes = if ($Site.VersionSize) { $Site.VersionSize } else { 0 }
    $versionSizeMB = [math]::Round($versionSizeBytes / 1MB, 2)
    $versionSizeGB = [math]::Round($versionSizeBytes / 1GB, 4)
    
    # Calcular storage em diferentes unidades
    $storageBytes = if ($Site.StorageUsageCurrent) { $Site.StorageUsageCurrent * 1MB } else { 0 }
    $storageMB = if ($Site.StorageUsageCurrent) { $Site.StorageUsageCurrent } else { 0 }
    $storageGB = [math]::Round($storageMB / 1024, 2)
    
    # Calcular ArchivedFileDiskUsed em MB
    $archivedFileDiskUsedMB = if ($Site.ArchivedFileDiskUsed) { 
        [math]::Round($Site.ArchivedFileDiskUsed / 1MB, 2) 
    } else { 0 }
    
    return [PSCustomObject]@{
        # Identification
        Url = $Site.Url
        Title = if ($Site.Title) { $Site.Title } else { "" }
        SiteId = if ($Site.SiteId) { $Site.SiteId.ToString() } else { "" }
        
        # Storage
        StorageUsageCurrent = $storageMB
        StorageUsageCurrentMB = $storageMB
        StorageUsageCurrentGB = $storageGB
        StorageQuota = if ($Site.StorageQuota) { $Site.StorageQuota } else { 0 }
        StorageQuotaWarningLevel = if ($Site.StorageQuotaWarningLevel) { $Site.StorageQuotaWarningLevel } else { 0 }
        
        # Versions
        VersionCount = if ($Site.VersionCount) { $Site.VersionCount } else { 0 }
        VersionSize = $versionSizeBytes
        VersionSizeMB = $versionSizeMB
        VersionSizeGB = $versionSizeGB
        VersionPolicyFileTypeOverride = if ($Site.VersionPolicyFileTypeOverride) { $Site.VersionPolicyFileTypeOverride } else { "None" }
        InheritVersionPolicyFromTenant = if ($null -ne $Site.InheritVersionPolicyFromTenant) { $Site.InheritVersionPolicyFromTenant } else { $true }
        
        # Dates - Use LastActivityDate from Graph API if available
        CreatedTime = if ($Site.CreatedTime) { if ($Site.CreatedTime -is [DateTime]) { $Site.CreatedTime.ToString("o") } else { $Site.CreatedTime } } else { $null }
        LastContentModifiedDate = if ($LastActivityDate) { 
            # Converter string de data do Graph para formato ISO
            try {
                [DateTime]::Parse($LastActivityDate).ToString("o")
            }
            catch {
                $LastActivityDate
            }
        } elseif ($Site.LastContentModifiedDate) { 
            if ($Site.LastContentModifiedDate -is [DateTime]) { $Site.LastContentModifiedDate.ToString("o") } else { $Site.LastContentModifiedDate }
        } else { 
            $null 
        }
        
        # Status and Archive
        Status = if ($Site.Status) { $Site.Status } else { "Active" }
        ArchiveStatus = if ($Site.ArchiveStatus) { $Site.ArchiveStatus } else { "NotArchived" }
        ArchivedBy = if ($Site.ArchivedBy) { $Site.ArchivedBy } else { "" }
        ArchivedTime = if ($Site.ArchivedTime) { if ($Site.ArchivedTime -is [DateTime]) { $Site.ArchivedTime.ToString("o") } else { $Site.ArchivedTime } } else { $null }
        ArchivedFileDiskUsed = $archivedFileDiskUsedMB
        
        # Hub and Group
        HubSiteId = if ($Site.HubSiteId -and $Site.HubSiteId -ne [Guid]::Empty) { $Site.HubSiteId.ToString() } else { "" }
        IsHubSite = if ($null -ne $Site.IsHubSite) { $Site.IsHubSite } else { $false }
        RelatedGroupId = if ($Site.RelatedGroupId -and $Site.RelatedGroupId -ne [Guid]::Empty) { $Site.RelatedGroupId.ToString() } else { "" }
        
        # Others
        WebsCount = if ($Site.WebsCount) { $Site.WebsCount } else { 0 }
        LocaleId = if ($Site.LocaleId) { $Site.LocaleId } else { 0 }
        LockState = if ($Site.LockState) { $Site.LockState } else { "Unlock" }
        Owner = if ($Site.Owner) { $Site.Owner } else { "" }
        Template = if ($Site.Template) { $Site.Template } else { "" }
        
        # Export metadata
        ExportedAt = (Get-Date).ToString("o")
    }
}
#endregion

#region Telemetry Functions

function Get-TelemetryTenantHash {
    <#
    .SYNOPSIS
        Generates a deterministic anonymous tenant hash from TenantId.
        Same tenant always produces same hash regardless of machine.
    #>
    [CmdletBinding()]
    param()

    $tenantId = $script:AppPaths.EntraIdApp.TenantId
    if ([string]::IsNullOrWhiteSpace($tenantId)) { return 'anonymous' }

    # Salt derived from tenantId itself — machine-independent
    $input = "$($tenantId.ToLower().Trim())|spo-vm-telemetry"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($input)
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return [BitConverter]::ToString($hash).Replace('-','').Substring(0,32).ToLower()
}

function Get-HashedWorkItemId {
    <#
    .SYNOPSIS
        Hashes a WorkItemId (GUID or synthetic key) so no raw identifiers are sent.
        Same WorkItemId always produces same hash — used for deduplication.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$WorkItemId)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($WorkItemId.ToLower().Trim())
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return [BitConverter]::ToString($hash).Replace('-','').Substring(0,32).ToLower()
}

function Get-HashedSiteUrl {
    <#
    .SYNOPSIS
        Hashes a site URL so no raw URLs are sent. Same URL always produces same hash.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory)][string]$SiteUrl)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($SiteUrl.ToLower().Trim())
    $hash = [System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)
    return [BitConverter]::ToString($hash).Replace('-','').Substring(0,16).ToLower()
}

function Send-SPOTelemetry {
    <#
    .SYNOPSIS
        Sends anonymous telemetry for a single completed site job (BatchDelete only).
        All identifiers (WorkItemId, SiteUrl) are hashed before sending.
        Backend deduplicates on WorkItemId hash — safe to re-send.
    .PARAMETER WorkItemId
        The SPO WorkItemId (GUID) or synthetic key for the job
    .PARAMETER SiteUrl
        The SharePoint site URL that was processed
    .PARAMETER JobType
        SyncListPolicy or BatchDelete
    .PARAMETER StorageFreedBytes
        Storage freed by this job (bytes)
    .PARAMETER VersionsDeleted
        Versions deleted by this job
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)][string]$WorkItemId,
        [Parameter(Mandatory)][string]$SiteUrl,
        [Parameter(Mandatory)][string]$JobType,
        [Parameter()][long]$StorageFreedBytes = 0,
        [Parameter()][long]$VersionsDeleted = 0
    )

    if (-not $script:AppPaths) { return }
    if (-not $script:AppPaths.TelemetryEnabled) { return }
    if ([string]::IsNullOrWhiteSpace($script:AppPaths.TelemetryEndpoint)) { return }

    try {
        $payload = @{
            tenantHash       = Get-TelemetryTenantHash
            appVersion       = $script:AppPaths.AppVersion
            workItemId       = Get-HashedWorkItemId -WorkItemId $WorkItemId
            siteUrl          = Get-HashedSiteUrl -SiteUrl $SiteUrl
            jobType          = $JobType
            storageFreedBytes = $StorageFreedBytes
            versionsDeleted  = $VersionsDeleted
            sitesProcessed   = 1
            timestamp        = (Get-Date).ToString('o')
        } | ConvertTo-Json -Compress

        $uri = "$($script:AppPaths.TelemetryEndpoint.TrimEnd('/'))/api/telemetry"
        $null = Invoke-RestMethod -Uri $uri -Method Post -Body $payload -ContentType 'application/json' -TimeoutSec 10 -ErrorAction SilentlyContinue
        Write-Verbose "[TELEMETRY] Site completion sent: $JobType"
    }
    catch {
        Write-Verbose "[TELEMETRY] Failed to send (non-blocking): $_"
    }
}

function Send-SPOTelemetryBatch {
    <#
    .SYNOPSIS
        Sends a batch of historical BatchDelete completions in a single request.
        Scans SiteExecutionHistory.json and sends all completed BatchDelete jobs.
        Backend deduplicates on WorkItemId hash — safe to re-run on any machine.
    .DESCRIPTION
        On first run (or after reinstall), this function reads all historical executions from
        SiteExecutionHistory.json and sends BatchDelete completions in batches of up to 500 items.
        The backend rejects duplicates automatically, so no local tracking is needed.
    #>
    [CmdletBinding()]
    param()

    if (-not $script:AppPaths) { return }
    if (-not $script:AppPaths.TelemetryEnabled) { return }
    if ([string]::IsNullOrWhiteSpace($script:AppPaths.TelemetryEndpoint)) { return }

    $historyFile = Join-Path $script:AppPaths.LogPath 'SiteExecutionHistory.json'
    if (-not (Test-Path $historyFile)) {
        Write-Verbose "[TELEMETRY] No SiteExecutionHistory.json found - nothing to sync"
        return
    }

    try {
        $history = Get-Content $historyFile -Raw | ConvertFrom-Json
        if (-not $history.Sites) { return }

        $tenantHash = Get-TelemetryTenantHash
        $appVersion = $script:AppPaths.AppVersion
        $items = @()

        foreach ($siteProp in $history.Sites.PSObject.Properties) {
            $siteUrl = $siteProp.Name
            $siteHashedUrl = Get-HashedSiteUrl -SiteUrl $siteUrl

            foreach ($exec in $siteProp.Value.Executions) {
                if ($exec.Status -ne 'CompleteSuccess') { continue }
                if ($exec.JobType -ne 'BatchDelete') { continue }
                if ([string]::IsNullOrWhiteSpace($exec.WorkItemId)) { continue }

                $items += @{
                    tenantHash        = $tenantHash
                    appVersion        = $appVersion
                    workItemId        = Get-HashedWorkItemId -WorkItemId $exec.WorkItemId
                    siteUrl           = $siteHashedUrl
                    jobType           = $exec.JobType
                    storageFreedBytes = [long]($exec.StorageReleasedBytes)
                    versionsDeleted   = [long]($exec.VersionsDeleted)
                    sitesProcessed    = 1
                    timestamp         = $exec.ExecutedAt
                }
            }
        }

        if ($items.Count -eq 0) {
            Write-Verbose "[TELEMETRY] No completed BatchDelete executions to sync"
            return
        }

        Write-Host "  Syncing $($items.Count) historical executions to telemetry backend..." -ForegroundColor DarkGray

        # Send in batches of 500
        $batchSize = 500
        $sent = 0
        $duplicates = 0

        for ($i = 0; $i -lt $items.Count; $i += $batchSize) {
            $chunk = $items[$i..([Math]::Min($i + $batchSize - 1, $items.Count - 1))]
            $body = @{ items = $chunk } | ConvertTo-Json -Depth 4 -Compress

            $uri = "$($script:AppPaths.TelemetryEndpoint.TrimEnd('/'))/api/telemetry/batch"
            $response = Invoke-RestMethod -Uri $uri -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 30 -ErrorAction Stop

            if ($response.accepted) { $sent += $response.accepted }
            if ($response.duplicates) { $duplicates += $response.duplicates }
        }

        Write-Host "  Telemetry sync complete: $sent new, $duplicates already recorded" -ForegroundColor DarkGray
    }
    catch {
        Write-Verbose "[TELEMETRY] Batch sync failed (non-blocking): $_"
    }
}

function Get-SPOGlobalStats {
    <#
    .SYNOPSIS
        Retrieves worldwide aggregate stats from the telemetry backend
    .OUTPUTS
        PSCustomObject with TotalStorageFreedBytes, TotalVersionsDeleted, TotalSessions
    #>
    [CmdletBinding()]
    param()

    if (-not $script:AppPaths) { return $null }
    if ([string]::IsNullOrWhiteSpace($script:AppPaths.TelemetryEndpoint)) { return $null }

    try {
        $uri = "$($script:AppPaths.TelemetryEndpoint.TrimEnd('/'))/api/stats"
        $result = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec 10 -ErrorAction Stop
        return $result
    }
    catch {
        Write-Verbose "[TELEMETRY] Failed to retrieve global stats: $_"
        return $null
    }
}
#endregion

# Export module members
Export-ModuleMember -Function @(
    'Connect-SPOVersionManagement',
    'Get-AllTenantSites',
    'Start-SPOVersionPolicyOrchestration',
    'Update-TenantStorageStatus',
    'Get-SPOSiteStorageInfo',
    'Get-JobStatus',
    'Update-JobStatus',
    'Apply-SiteFilters',
    'Get-TenantStorageHistory',
    'Get-TenantStorageHistoryAggregated',
    'Import-GraphReportCSV',
    'Export-AllSitesDataForDashboard',
    'ConvertTo-SiteDataObject',
    'Get-SPOAppPaths',
    'Get-DashboardConfig',
    'Get-PendingSessions',
    'Save-Session',
    'Update-SessionProgress',
    'Get-SessionById',
    'Get-ExistingJobProgress',
    'Get-SiteLastSuccessfulExecution',
    'Test-ShouldProcessSite',
    'Sync-PendingJobStatus',
    'Sync-ExternalJobResults',
    'Save-TenantStorageSnapshot',
    'Send-SPOTelemetry',
    'Send-SPOTelemetryBatch',
    'Get-SPOGlobalStats'
)
