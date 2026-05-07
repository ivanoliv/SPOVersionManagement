---
layout: default
title: "SPO Version Management — Complete Tool Manual"
description: "Complete reference manual for SPO Version Management GUI application. Every screen, option, and feature explained with PowerShell equivalents."
---

<nav class="breadcrumb">
    <a href="{{ '/' | relative_url }}">Home</a> &rsaquo; <a href="{{ '/guides/' | relative_url }}">Guides</a> &rsaquo; Tool Manual
</nav>

# Complete Tool Manual

Full reference for every screen and feature in the SPO Version Management Windows GUI application (v2.4.0).

> **New here?** Start with the [Quick Start Guide]({{ '/guides/quick-start/' | relative_url }}) first.

---

## Navigation Structure

The application is organized into these main sections:

| Section | Purpose |
|---------|---------|
| **Home** | Dashboard overview with tenant stats |
| **Pre reqs** | System prerequisites validation |
| **Config** | Tenant connection, directories, telemetry |
| **HTTP Server** | Local web dashboard server |
| **Retention** | Retention policy management |
| **Sites** | Site catalog, scope, archiving |
| **Execution** | All operational workflows |
| **Task Scheduler** | Unattended scheduled runs |
| **History** | Full execution audit trail |
| **Updates** | Version management and auto-update |

---

## Home

![Home screen]({{ '/screenshots/gui-home.png' | relative_url }})

The Home screen shows a summary of your tenant state: total sites, storage consumed, version percentage, and last sync timestamp. This is your at-a-glance operational view.

The top status bar shows: **Version Size** (total version storage), **Versions %** (percentage of total storage consumed by versions), and **Last Updated** timestamp.

---

## Pre reqs

![Prerequisites screen]({{ '/screenshots/gui-prereqs.png' | relative_url }})

The Prerequisites screen validates your environment:

- ✅ PowerShell 7 installed
- ✅ .NET 10 Desktop Runtime
- ✅ Required PowerShell modules (`Microsoft.Online.SharePoint.PowerShell`, `Microsoft.Graph`, `PnP.PowerShell`)
- ✅ Certificate configuration
- ❌ Any missing dependencies

Items marked with ✅ are ready. Items marked with ❌ need attention before you can run the tool.

**PowerShell equivalent:**
```powershell
# Check PS7
pwsh --version

# Check SPO module
Get-Module -ListAvailable Microsoft.Online.SharePoint.PowerShell

# Check Graph module
Get-Module -ListAvailable Microsoft.Graph.Reports
```

---

## Config

![Configuration screen]({{ '/screenshots/gui-configuration.png' | relative_url }})

Configuration is split into two areas:

### Connection & Authentication (Top)

| Field | Description |
|-------|-------------|
| **Admin URL** | Your SharePoint admin center URL (`https://TENANT-admin.sharepoint.com`) |
| **Tenant ID** | From Entra ID app registration |
| **Client ID** | Application (client) ID |
| **Certificate Thumbprint** | Certificate uploaded to the app registration |
| **Dashboard Language** | UI language (en, es, pt, de, fr, ja) |
| **Currency** | Display currency for cost calculations |
| **Cost per TB/Year** | Your Microsoft 365 extra storage cost (default: $13,000 USD) |

### Execution Directories (Bottom)

| Field | Description |
|-------|-------------|
| **Root Directory** | Base installation path |
| **Application Folder** | Relative path to the app folder |
| **Logs Subfolder** | Where execution logs are saved |
| **Backup Subfolder** | Backup location for logs |
| **Config Folder** | JSON configuration database folder |
| **Web Folder** | Dashboard HTML files |
| **App Folder** | GUI application binary |

### Auto-Update

| Field | Description |
|-------|-------------|
| **GitHub Repository** | Source repository for updates (`ivanoliv/SPOVersionManagement`) |

### Anonymous Telemetry

Telemetry is **opt-in** and collects only aggregate, non-identifying metrics:

- ☑️ **Enable anonymous usage statistics** — Sends anonymized execution metrics to the community backend
- **Telemetry Endpoint** — The backend URL receiving the data
- **Preview payload** — Click to see exactly what data will be sent

**What is sent:**
- One-way hash of TenantId + local salt (impossible to reverse)
- Aggregate storage freed (bytes)
- Aggregate versions deleted (count)
- Job duration and status
- App version

**What is NOT sent:**
- Site URLs, names, or any content
- Tenant name or domain
- User identity or credentials
- IP addresses are not logged

**Why it matters:** Telemetry powers the [community statistics](https://ivanoliv.github.io/SPOVersionManagement/) on the project website (total TB freed, sessions run, tenants participating). This data helps prioritize features and demonstrate impact to the community. Your participation is anonymous and helps improve the tool for everyone.

**PowerShell equivalent:**
```powershell
# All config is stored in config\AppPaths.json
# Edit directly or use the GUI
```

---

## HTTP Server

![HTTP Server screen]({{ '/screenshots/gui-httpserver.png' | relative_url }})

Starts a local web server to host the interactive HTML Dashboard.

- Click **Start** to launch the server
- Open **http://localhost:8080** in your browser
- The dashboard shows: site list, storage metrics, version analytics, charts, and execution progress

The dashboard reads from the JSON files in your `config/` folder (populated by Data Sync).

**PowerShell equivalent:**
```powershell
.\Start-Dashboard.ps1
# Opens http://localhost:8080
```

---

## Retention

![Retention Policy Management]({{ '/screenshots/gui-retention-policy-management.png' | relative_url }})

Manage Microsoft Purview retention policies that may block version deletion.

When a site is under a retention hold, SharePoint prevents version deletion. This panel lets you:

- View all retention policies affecting your sites
- Temporarily suspend policies during cleanup
- Resume policies after cleanup completes

> **Requires:** Purview app registration with Exchange/Security & Compliance permissions. See [Entra ID App Setup — Purview App](https://github.com/ivanoliv/SPOVersionManagement/blob/main/ENTRA_ID_APP_SETUP.md#app-2-purview-app-retention-policy-management).

**PowerShell equivalent:**
```powershell
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 5 -ManageRetentionPolicy
```

---

## Sites

### Site Catalog

![Site Catalog]({{ '/screenshots/gui-site-catalog.png' | relative_url }})

The full inventory of all SharePoint sites in your tenant. Shows:

| Column | Description |
|--------|-------------|
| **Title** | Site display name |
| **URL** | Full site URL |
| **Storage** | Total storage consumed |
| **Versions** | Number of file versions |
| **Ver. Size** | Storage consumed by versions (with % bar) |
| **Status** | Active / Inactive |
| **Archive** | Archive state |
| **Lock** | Site lock status |

**Actions:**
- **Details** — Open the site detail popup (click magnifier icon)
- **Select All** — Select all visible sites
- **Add to Skip** — Add selected sites to the exclusion list
- **Add to Archive** — Queue selected sites for archiving

#### Site Detail Popup

Click the magnifier (🔍) on any site to see:

- **Storage Overview** — Size at first execution, current size, current versions, % versions of total
- **Version Retention Impact** — Version limit, versions kept (latest), versions before, versions after
- **Execution History** — Full log of all operations on this site (date, type, status, duration, files, versions deleted, storage released)

**PowerShell equivalent:**
```powershell
# Export all sites
.\Export-AllSPOSites.ps1 -AdminUrl "https://contoso-admin.sharepoint.com"

# Get specific site info
Get-SPOSite -Identity "https://contoso.sharepoint.com/sites/teamsite1" -Detailed
```

---

### Execution Scope

![Execution Scope]({{ '/screenshots/gui-execution-scope.png' | relative_url }})

Define exactly which sites to process:

| Panel | Purpose |
|-------|---------|
| **TARGET SITES** (left) | Process **only** these sites. If empty, all tenant sites are processed. |
| **SKIP SITES** (right) | **Never** process these sites, even if they match other criteria. |

**Actions:**
- **Import Target** / **Import Skip** — Load from CSV file
- **Save Target** / **Save Skip** — Save current list to CSV
- **Export CSV** — Export combined scope
- **+ Target** / **+ Skip** — Add a site manually
- **Edit** / **Remove** — Modify or remove entries

> ⚠️ If Target Sites is empty, all sites in the tenant will be processed (minus Skip Sites).

**PowerShell equivalent:**
```powershell
# Use CSV files
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 5 `
    -IncludeSitesCSV "C:\path\to\IncludeSites.csv" `
    -ExcludeSitesCSV "C:\path\to\ExcludeSites.csv"
```

---

### Archive Candidates

![Archive Candidates]({{ '/screenshots/gui-archive-candidates-sam.png' | relative_url }})

Sites identified as candidates for archiving. These come from **SharePoint Advanced Management (SAM)** inactive site reports.

**How to get the SAM report:**

**Option A — From Advanced Management Overview:**
1. Go to [SharePoint Admin Center](https://admin.cloud.microsoft) → **Advanced** → **Advanced management**
2. Under "Content management assessment", find **Site lifecycle** → "Site Inactivity"
3. Click **View recommendations**
4. In the popup, click **Download a report**
5. Save the CSV file

**Option B — From Site Lifecycle Management:**
1. Go to **Policies** → **Site lifecycle management**
2. Open your **Inactive site policies**
3. Click **Download** on the report column

**Using the report in the tool:**
- Go to **Execution → Clean Versions** → **Input Files** section
- Set **SAM Report (CSV)** to the downloaded file path
- OR go to **Sites → Archive Candidates** to view them in the grid

**Actions:**
- **Add to Skip** — Exclude from processing
- **Add to Archive** — Move to the Archive Queue for archival

---

### Archived Sites

![Archived Sites]({{ '/screenshots/gui-archived-sites.png' | relative_url }})

Sites that have already been archived via the tool. Shows the same grid columns as Site Catalog with archive state confirmed.

**PowerShell equivalent:**
```powershell
# Check archive status
Get-SPOSite -Identity "https://contoso.sharepoint.com/sites/oldsite" | Select-Object Url, LockState, Status
```

---

### Archive Queue

![Archive Queue]({{ '/screenshots/gui-archive-queue.png' | relative_url }})

Sites queued for archiving. You can:

- Add sites from the Site Catalog or Archive Candidates
- **Remove** sites from the queue
- **Run Archive** — Launches a PowerShell process to archive all queued sites

When you click **Run Archive**, the tool executes the SharePoint archive command for each site in the queue.

**PowerShell equivalent:**
```powershell
# Archive a site (Microsoft official command)
Set-SPOSite -Identity "https://contoso.sharepoint.com/sites/oldsite" -LockState NoAccess
# For full archive (requires SAM license):
Set-SPOSiteArchiveState -Identity "https://contoso.sharepoint.com/sites/oldsite" -ArchiveState Archived
```

---

## Execution

### Clean Versions

![Clean Versions]({{ '/screenshots/gui-execution-execute-clean-versions.png' | relative_url }})

The main execution screen for version cleanup operations.

#### Session Control (top)

| Action | Description |
|--------|-------------|
| **Rename** | Rename the current session for identification |
| **Load Session** | Resume a previous interrupted session |
| **Start Over** | Create a new blank session |
| **Delete All** | Remove all session history |

Sessions auto-save on interruption. You can resume exactly where you left off.

#### Version Policy (left)

| Setting | Description |
|---------|-------------|
| **Concurrent Jobs** | Number of parallel batch operations (default: 100) |
| **Check Batch Size** | How many jobs to check at once (default: 10) |
| **Zero Version Action** | What to do with sites that have 0 versions: `skip` or `process` |
| **Batch Delay (s)** | Seconds between batch submissions (throttle control) |
| **Re-execution Days** | Skip sites processed within N days (default: 60) |

#### Delete Mode

| Mode | Description |
|------|-------------|
| **Delete by version count** | Keep N major + M minor versions, delete the rest |
| **Delete by age** | Delete versions older than N days |

#### Operation Mode (top-right)

| Option | API Used | Description |
|--------|----------|-------------|
| ☐ **Sync Version Policy** | `New-SPOSiteManageVersionPolicyJob` | Push version limits to all sites and document libraries |
| ☑️ **Delete Excess Versions** | `New-SPOSiteFileVersionBatchDeleteJob` | Delete versions exceeding the configured limit |
| ☐ **Manage Retention Policies** | Security & Compliance | Temporarily suspend retention holds before deletion |
| ☐ **Skip Graph** | — | Don't call Graph API; use manual CSV instead |

#### Input Files (bottom)

| Field | Description |
|-------|-------------|
| **Include Sites (CSV)** | Only process these sites |
| **Exclude Sites (CSV)** | Never process these sites |
| **Graph Report (CSV)** | Manual SharePoint Site Usage Storage report ([how to export]({{ '/guides/quick-start/#how-to-export-the-graph-report-manually' | relative_url }})) |
| **Sync Job List (CSV)** | External BatchDeleteJobProgress to sync |
| **SAM Report (CSV)** | Content Management Assessment from SharePoint Advanced Management |
| ☐ **Use AllSites.json cache** | Skip `Get-SPOSite` and use cached data (faster for large tenants) |

**PowerShell equivalent:**
```powershell
# Full execution with all options
.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 5 `
    -MinorVersionLimit 0 `
    -MaxConcurrentJobs 100 `
    -ReexecutionDays 60 `
    -IncludeSitesCSV "IncludeSites.csv" `
    -ExcludeSitesCSV "ExcludeSites.csv" `
    -SkipGraph `
    -GraphReportCSV "SharePointSiteUsageStorage.csv" `
    -ManageRetentionPolicy `
    -Unattended
```

---

### Data Sync

![Data Sync]({{ '/screenshots/gui-execution-data-sync-only.png' | relative_url }})

Synchronize tenant data for the dashboard and analytics.

#### Sync Options

| Option | Description |
|--------|-------------|
| ☑️ **Export All Sites** | Full site inventory via `Get-SPOSite` → `AllSites.json` |
| ☑️ **Graph API Report** | Storage data from Microsoft Graph → usage metrics |
| ☑️ **Archive Analysis** | Pre-process data for dashboard archive views |
| ☑️ **Tenant Storage Timeline** | Update trend data for storage charts |

#### Telemetry Sync

If you upgraded from a previous version and have execution history that was never synced, click **Sync History to Telemetry** to upload anonymized results to the community backend.

#### External Job Sync

Checks SharePoint for version management jobs completed **outside** this tool (by other admins, scripts, or scheduled tasks). Updates local execution history so the Dashboard and re-execution rules reflect the real state.

- **Look Back:** How many days to check (default: 60)
- Queries all sites in `AllSites.json` for `BatchDeleteJobProgress`

**PowerShell equivalent:**
```powershell
# Sync only
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -SyncOnly

# Start dashboard after sync
.\Start-Dashboard.ps1
```

---

### Archive Sites

![Archive Sites]({{ '/screenshots/gui-execution-archive-sites.png' | relative_url }})

Execute the archive operation for sites in the Archive Queue. Opens a PowerShell window to process each queued site.

**PowerShell equivalent:**
```powershell
.\Start-ArchiveWebsites.ps1
```

---

### File Archive Explorer

![File Archive Explorer]({{ '/screenshots/gui-execution-file-archive-explorer.png' | relative_url }})

Search for files by extension across a specific SharePoint site using the Graph Search API.

#### Configuration

| Field | Description |
|-------|-------------|
| **Target Site URL** | The site to scan |
| **Pick Site** | Browse and select from your site catalog |
| **Summary only** | Count files without downloading details |
| **Authentication** | Interactive (browser) or App credentials (EntraID) |
| **Region** | Your Microsoft 365 data region (for search endpoint) |

#### Extension Groups

Define groups of file extensions to search for:

| Group | Extensions |
|-------|-----------|
| **Office Documents** | .docx, .doc, .xlsx, .xls, .pptx, .ppt, .vsdx, .vsd, .one, .onetoc2, .mpp, .pub, .pdf, .xps |
| **Text & Markup** | .txt, .rtf, .csv, .xml, .html, .htm, .md, .json, .msg, .eml, .odt, .ods, .odp |
| **Videos** | .mp4, .mov, .wmv, .avi, .mkv, .m4v, .mpg, .mpeg, .3gp, .3g2, .mts, .m2ts |

You can create custom groups, add/remove extensions, and save your configuration.

**Actions:**
- **Run** — Execute the search against the target site
- Results show: site URL, files found, last scanned, duration, categories
- Select files and add them to the **File Archive Queue**

> **Note:** Requires `PnP.PowerShell` module. Management is per-site (select one site at a time).

---

### File Archive Queue

Track and execute file-level archive operations.

| Column | Description |
|--------|-------------|
| **Site URL** | Source site |
| **File URL** | Individual file path |
| **Category** | Extension group |
| **Ext** | File extension |
| **Size MB** | File size |
| **Queued At** | When added to queue |
| **Status** | Pending / Archived / Failed |

**Actions:**
- **Refresh** — Update status of all items
- **Remove Selected** — Remove from queue
- **Clear Queue** — Remove all items
- **Select All** / **Archive Selected** — Archive specific files
- **Start** — Begin archiving all queued files

---

### Session Manager

Browse, inspect, and resume past execution sessions. State is auto-saved on interruption.

| Column | Description |
|--------|-------------|
| **Status** | Completed / InProgress / Failed |
| **Session ID** | Unique session identifier |
| **Started** | Start timestamp |
| **Last Updated** | Last activity |
| **Progress** | Sites processed / total |
| **Mode** | Delete Only / Sync+Delete / etc. |

**Actions:**
- **Refresh** — Reload session list
- **Resume** — Continue an interrupted session from where it stopped
- **View Log** — See full session configuration and parameters
- **Delete** — Remove a session record

The detail panel shows the full session configuration: admin URL, mode, version limits, concurrent jobs, batch settings, and all parameters used.

---

## Task Scheduler

Schedule unattended executions via **Windows Task Scheduler**.

> ⚠️ **Administrator privileges required.** Click **Run as Admin** to elevate the application.

| Action | Description |
|--------|-------------|
| **+ New Task** | Create a new scheduled task |
| **Enable** / **Disable** | Toggle a task on/off |
| **Run Now** | Execute immediately |
| **Delete** | Remove a scheduled task |

The grid shows: enabled state, task name, schedule (cron), last run, result, and next run.

> **Important:** Scheduled tasks run unattended and **require Entra ID app registration** with certificate authentication. Interactive login cannot work without a user present. See the [Entra ID App Setup Guide](https://github.com/ivanoliv/SPOVersionManagement/blob/main/ENTRA_ID_APP_SETUP.md).

**PowerShell equivalent:**
```powershell
# The task scheduler creates a Windows scheduled task that runs:
.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 5 `
    -Unattended
```

---

## History

Full execution audit trail. Browse and filter every job ever executed by the tool.

**Filters:**
- **Search** — Free text search (site name, etc.)
- **Status** — Filter by: All, CompleteSuccess, Failed, InProgress
- **Type** — Filter by: All, BatchDelete, SyncListPolicy
- **Date range** — From/To date pickers

**Columns:**

| Column | Description |
|--------|-------------|
| **Timestamp** | Exact execution time (ISO 8601) |
| **Site** | Site name processed |
| **Job Type** | BatchDelete or SyncListPolicy |
| **Status** | CompleteSuccess, Failed, InProgress |
| **Duration (min)** | How long the job took |
| **Versions Deleted** | Number of versions removed |

**Actions:**
- **Export CSV** — Download the full history as CSV for Power BI or Excel analysis
- **Refresh** — Reload from the local database

**PowerShell equivalent:**
```powershell
# History is stored in config\SiteExecutionHistory.json
# Export to CSV for analysis:
Get-Content .\config\SiteExecutionHistory.json | ConvertFrom-Json |
    Select-Object -ExpandProperty Sites
```

---

## Updates

Check for new versions, download, and install updates automatically.

**Features:**
- **Current version** — Shows installed version (e.g., v2.4.0.0)
- **Available version** — Latest version on GitHub
- **Download & Install** — Downloads the ZIP, extracts over the existing installation (preserving all JSON config/data files)
- **View local JSON databases** — Check the state and size of your config files
- **View release notes** — See what changed in the latest release

The update process:
1. Downloads the latest release ZIP from GitHub
2. Extracts files, overwriting only application files
3. Preserves: `config/` folder, `Logs/` folder, all JSON databases
4. Shows release notes after completion

---

## Status Bar

The bottom status bar shows real-time system state:

| Indicator | Meaning |
|-----------|---------|
| 🟢 **Cache: Ready** | AllSites.json is loaded and available |
| 🔴 **Graph: Offline** | Graph API not connected (normal if using Skip Graph) |
| ✓ N passed | Prerequisites that passed |
| ✗ N failed | Prerequisites that need attention |
| **Session: YYYYMMDD** | Current active session ID |

---

<div class="cta-box">
    <h3>Need Help?</h3>
    <p>Can't find what you're looking for? Open an issue on GitHub.</p>
    <a href="https://github.com/ivanoliv/SPOVersionManagement/issues">Get Support</a>
</div>
