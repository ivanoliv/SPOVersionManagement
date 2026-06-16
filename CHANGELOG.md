# Changelog

All notable changes to SPO Version Management are documented in this file.

---

## v2.4.0.6 (2026-06-16)

**Stability & Large Tenant Fixes**

### Fixed
- **OutOfMemoryException on large tenants (10k+ sites)**: AllSites.json was being loaded from disk on every job completion, causing OOM in PS 5.1. Now loads once into memory cache and mutates in-place, with explicit OOM catch and GC fallback
- **Delete mode radio buttons not mutually exclusive**: "Delete by version count" and "Delete by age" could both be selected simultaneously because they live in separate WinForms panels. Added reentrancy-guarded mutual exclusion logic
- **CSV export missing View columns**: File Archive Explorer CSV now uses explicit `Select-Object` with all 24 columns (ViewsLastMonths1, ViewsLastMonths2, ViewsLast7Days, etc.) to ensure null values still appear as columns

### Changed
- AllSites.json writes now use `-Compress` flag to reduce file size for large tenants
- File Archive CSV uses `Read-JsonFileSafe` (stream reader) for shared file access

---

## v2.4.0.5 (2026-06-08)

**Dashboard — Cost Savings Visibility & Version Recovery Metrics**

### Added

#### Execution History Table — Cost Savings Column
- New **"Economia/mês" (Savings/mo)** column in the per-site execution history table
- Shows the monthly cost saving for each execution based on `StorageReleasedBytes × configured cost/GB`
- Respects user-configured currency (BRL or USD) and exchange rate from Settings
- Displayed in green with bold styling for quick visual identification
- Works retroactively — all historical executions show their cost impact

#### "Versões Depois" Card — Recovery Percentage
- The "Versions After" card now displays a green **(-X%)** indicator showing how much version storage was recovered relative to the original version size
- Provides immediate visual feedback on cleanup effectiveness

#### Version Impact Section — Cost Recovered Row
- New green-styled row below the "cost still used" footer showing **total cost recovered** (monthly + annual)
- Calculated from total freed bytes across all BatchDelete executions for the site
- Helps customers understand the cumulative monetary value of version cleanup

### Fixed
- **Relative CSV path resolution**: `ResolveScopeCsvPath` in ExecutionPanel.cs now resolves relative paths (e.g., `IncludeSites.csv`) against `_config.RootPath` instead of failing with file-not-found
- **Post-BatchDelete storage refresh**: `SPOVersionManagement.psm1` now calls `Get-SPOSite` after batch delete completion to get accurate `StorageAfterBytes`; uses fallback calculation if SPO hasn't updated yet
- **Missing config directory on init**: `Initialize-SPOVersionManagement` now creates `ConfigPath` and `BackupPath` directories if they don't exist
- **Installer RootPath overwrite**: `Install-SPOVersionManagement.ps1` no longer forces `RootPath` to the destination path, preserving user's existing configuration
- **Dashboard timeline chart scale**: Fixed `VersionSize` being incorrectly multiplied by 1MB (value already in bytes)
- **Dashboard formatBytes PB overflow**: Added PB unit and `Math.min` clamp to prevent `undefined` on very large values
- **Scope badge refresh on change**: `SiteManagementPanel` now fires `ScopeChanged` event on AddToTarget/AddToSkip/SaveScopeList, triggering `RefreshScopeCountBadges` in ExecutionPanel

---

## v2.4.0.4 (2026-06-02)

**Execution Scope Management — Target Sites & Skip Sites UX Overhaul**

### Added

#### Site Catalog — "Add to Target" Button
- New **"Add to Target"** button (cyan) in Site Catalog toolbar allows selecting one or multiple sites and adding them directly to the Target Sites execution scope (`IncludeSites.csv`)
- Works alongside existing "Add to Skip" and "Add to Archive" buttons
- Confirmation message shows count of added sites and reminds user that execution will process ONLY targeted sites

#### Execution Panel — Scope Checkboxes, Badges & Manage Dialog
- **Checkboxes** on Include/Exclude Sites rows — auto-checked when file has entries, unchecking temporarily disables scope without deleting entries
- **Count badges** — green `● N site(s) targeted` or gold `● N site(s) excluded` shown inline after browse button, updates dynamically on file change or panel load
- **"Manage" button** — opens a full CRUD dialog (dark-themed popup) for each scope file:
  - DataGridView listing all sites with URL and Reason columns
  - **+ Add URL** — input form with URL field and optional reason
  - **Remove** — delete selected rows
  - **Clear All** — wipe entire scope list with confirmation
  - **Save & Close** / **Cancel** — persist changes back to CSV and refresh badges
- **Auto-detection** — if `IncludeSites.csv` or `ExcludeSites.csv` has entries, execution automatically uses them even if the textbox path is empty (no more need to manually type the CSV path)

#### Pre-Execution Scope Confirmation
- When scope files are active, a confirmation dialog appears before execution:
  - Shows target count and skip count
  - Options: **Review** (opens Manage dialog for last-minute edits), **Continue** (proceed), **Abort** (cancel)
- Console logs scope status at execution start: `[Scope] Target: N site(s) from IncludeSites.csv`

### Fixed
- **Critical bug**: Adding sites to Target Scope via the Execution Scope page and running execution would process ALL tenant sites instead of only targeted sites — the inclusion list was only passed to the script if the user manually typed the CSV path in the textbox. Now auto-detected from file content.

---

## v2.4.0.2 (2026-05-08)

**Install Script Fix — App Folder**

### Fixed
- `Install-SPOVersionManagement.ps1`: app folder was hardcoded to copy only 4 files (from old PS2EXE build), now copies entire `app\` directory recursively (357+ files for framework-dependent .NET 10 build)
- Moved `app\` from individual file list to `$updateFolders` array for proper recursive copy with cleanup of previous version

---

## v2.4.0.1 (2026-05-08)

**File Archive Fixes, PnP Module Management & Documentation**

### 🔧 Fixes

#### File Archive Queue — Disabled (File-Level Archiving Not GA)
- **File-level archiving temporarily disabled** — The Microsoft Graph beta `/drives/{id}/items/{id}/archive` endpoint returns `MethodNotAllowed: File Archive is not supported` on all tested tenants, even with:
  - M365 Archive provisioned and active (Pay-as-you-go → Storage → File Archive → Status: Ativado)
  - `Set-PnPTenant -EnableSiteArchive $true` (tenant-level toggle)
  - `Set-SPOSite -AllowFileArchive $true` (per-site setting)
  - PnP.PowerShell nightly build with `Set-PnPFileArchiveState` cmdlet
  - Correct delegated permissions (`Files.ReadWrite.All`)
- **Root cause**: File-level archiving (archiving individual files to cold tier) is a separate preview feature from site-level archiving (archiving entire sites). The Graph API endpoint exists in beta but is not yet flighted to most tenants. Site-level archiving is GA and works normally.
- **What's disabled**: The "File Archive Queue" menu item is hidden from the sidebar. The archive execution button and code are preserved but commented out, ready to re-enable when Microsoft rolls out file-level archiving to General Availability.
- **What still works**: File Archive Explorer (search/browse files by extension across sites) continues to work normally.

#### PnP.PowerShell Module Management
- **Minimum version check** — Pre-requisites panel now validates PnP.PowerShell version against minimum required (3.1.367). If installed version is below minimum, the check fails with an upgrade instruction.
- **Update available alert** — Pre-requisites panel queries PSGallery for the latest PnP.PowerShell nightly version and shows "(update available: X.Y.Z)" when a newer version exists.
- **Pre-requisites panel** — "File Archive (execution)" row now shows as disabled with explanation about Graph beta API status.

#### Archive Execution Flow (Code Preserved)
- **Tenant-level enable** — Added `Set-PnPTenant -EnableSiteArchive $true` via PnP (PS7) before archive execution loop
- **Site-level enable** — Added `Set-SPOSite -AllowFileArchive $true` via SPO Management Shell (PS 5.1) per-site in archive loop
- **ClientId passthrough** — PnP connections now correctly pass `-ClientId` for interactive login (required for delegated permissions)
- **Error messages** — Archive errors now show actual Graph API error text instead of generic messages

#### Console & UI
- **File Archive Queue panel sizing** — Fixed console textbox being cut off by adjusting SplitContainer panel sizes

### 📖 Documentation

#### ENTRA_ID_APP_SETUP.md
- **PnP cleanup guide** — New troubleshooting section "PnP.PowerShell assembly conflicts or multiple versions" with step-by-step cleanup and reinstall instructions covering AllUsers, CurrentUser, and OneDrive-synced module paths
- **File-level archive status** — Updated "MethodNotAllowed" section explaining file-level vs site-level archiving distinction, current limitations, and site-level archiving workaround via SharePoint Admin Center
- **Permissions reference** — Clarified that Graph archive API only supports Delegated permissions (Application permissions not supported)

---

## v2.4.0.0 (2026-05-04)

**Major Platform Upgrade: .NET 10 + PowerShell 7 Hosting**

This release represents a major architecture upgrade — the Windows GUI application has been migrated from .NET Framework 4.7.2 to **.NET 10**, with in-process PowerShell 7 hosting via the official SDK.

#### Why .NET 10?

| Benefit | Detail |
|---------|--------|
| Security | Modern TLS 1.3 by default, up-to-date cryptographic libraries, regular LTS security patches |
| Performance | ~30% faster startup, reduced memory allocations, improved GC |
| PowerShell 7 Hosting | Run PS scripts in-process via Microsoft.PowerShell.SDK 7.5.0 — no external process spawning |
| Self-Contained | Single-file executable, no runtime prerequisites for end users |
| Long-Term Support | .NET 10 is an LTS release (supported until Nov 2029) |

### ✨ New Features

#### GUI Application
- **Tenant ID Auto-Resolution** — Automatically resolves TenantId from AdminUrl via Azure AD's public OpenID discovery endpoint (no authentication needed). Includes a manual "Resolve" button in Config panel.
- **Purview Organization Auto-Fill** — Derived from AdminUrl prefix (no HTTP call needed).
- **Excel-Like Column Filter** — Site Management grid now has proper Excel-style filtering: Sort A→Z/Z→A, search box, multi-select checkboxes with "(Select All)", OK/Cancel buttons. Replaces the old single-value ContextMenuStrip.
- **Worldwide Impact Refresh Button** — Manual refresh on the Home panel's global stats card for community telemetry data.
- **Session Manager Panel** — Full session save/resume with inline detail view.
- **Task Scheduler Panel** — Create, manage, enable/disable Windows scheduled tasks for unattended execution (run as administrator required).
- **Execution Panel Improvements** — GUI settings persistence, auto-save on change, tenant mismatch detection, interactive prompt bridge for PS scripts.
- **Site Management Enhancements** — Virtual grid with checkbox selection, copy URL on click, scope management with include/exclude lists.
- **Data Sync Panel** — External job sync with configurable look-back days.
- **File Archive Panel** — Improved settings persistence.
- **Prerequisites Panel** — Enhanced environment validation.

#### PowerShell Module
- **Opportunistic Job Sync** — `Test-ShouldProcessSite` now captures completed jobs found via the SPO API that weren't yet recorded in local history (avoids re-processing already-completed sites).
- **Telemetry** — Real-time `Send-SPOTelemetry` on BatchDelete completion with per-site metrics, `Send-SPOTelemetryBatch` for historical bulk sync, `SessionProgress` tracking.

### 🔒 Security

- TenantId resolved via public `.well-known/openid-configuration` endpoint — no credentials stored or transmitted
- Telemetry uses one-way SHA-256 hashing (TenantId + local salt) — original values never sent
- .NET 10 brings modern TLS stack and security patches

### 🐛 Bug Fixes

- **Fixed "Cannot access a disposed object" crash** — Column header filter popup no longer uses ContextMenuStrip (replaced with proper Form-based popup).
- **Fixed `_loadingSettings` guard** — `ApplySession` now properly wraps control assignments in try/finally to prevent event cascade during session load.

### 📦 Packaging

- Framework-dependent package (requires [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0))
- Self-contained (standalone) build not available as download due to GitHub's 100 MB file size limit — users can build locally from source if preferred
- `Build-DeployPackage.ps1` sanitizes all secrets, tenant data, and CSV content before packing
- `app/` directory removed from repository (built dynamically via `dotnet publish`)

### 📖 Documentation

- `docs/WINDOWS_APP.md` updated with both package options and .NET 10 build instructions
- GitHub Pages version bump

---

## v2.3.1.0 (2026-04-30)

**Release: Unified Script, Executable Packaging & Documentation**

### ✨ New Features

- **Unified Start Script** — Merged `Start-SPOVersionManagement_app.ps1` into `Start-SPOVersionManagement.ps1`. Single entry point now supports both interactive and Entra ID App (certificate) authentication.
- **Auto-detect Auth Mode** — When TenantId + ClientId + Certificate params are provided (via parameters or `config\AppPaths.json`), the script automatically uses app-based authentication. Otherwise falls back to interactive login.
- **`-Unattended` Switch** — All interactive prompts are auto-answered with safe defaults. Requires app auth credentials to be configured.
- **`-SkipGraphConnection` Parameter** — Explicitly skip Microsoft Graph API connection when Graph data is not needed.
- **Executable in Deploy Package** — New `app/` directory contains SPOVersionManagement.exe and required DLLs, included in both the build ZIP and installer.

### 🗑️ Removed

- **`Start-SPOVersionManagement_app.ps1`** — Deleted. All functionality merged into main script.
- **`StartScriptApp` config key** — Removed from `AppPaths.json` Scripts section.

### 📦 Packaging

- `Build-DeployPackage.ps1` now includes `app/` directory (exe, DLLs, config) in the deployment ZIP.
- `Install-SPOVersionManagement.ps1` creates `app/` directory at destination and copies executable files.
- Both scripts no longer reference the deleted `_app.ps1` file.

### 📖 Documentation

- README updated: file structure, parameters table (all auth params documented), Quick Start examples, execution modes
- `ENTRA_ID_APP_SETUP.md` usage examples updated to reference unified script
- `docs/WINDOWS_APP.md` updated with new `app/` path for running the executable

---

## v2.3.0.0 (2026-04-29)

**Release: GUI Persistence, Telemetry Backend Deployment & Bug Fixes**

### ✨ New Features

- **GUI Settings Persistence** — All execution panel fields (concurrent jobs, batch sizes, policies, file paths) now persist across sessions via `config/GuiSettings.json`
- **Dynamic PowerShell Prompts** — Custom PSHost routes `Read-Host`, `PromptForChoice`, and credential prompts to native WinForms dialogs (InputDialog, ChoiceDialog with buttons)
- **URL Click-to-Copy** — Clicking a site URL in the Site Catalog copies it to clipboard with visual feedback
- **Telemetry Backend on Azure** — Deployed to Azure App Service (`spo-telemetry-6406.azurewebsites.net`) with CORS, JSON persistence, and detailed stats API
- **GitHub Pages Live Stats** — Landing page now fetches real-time worldwide savings data from the Azure backend

### 🐛 Bug Fixes

- **Duplicate Console Output** — Fixed PSHost UI Write methods duplicating stream output (made no-ops since streams already capture)
- **SplitterDistance Crash** — Fixed `FileArchiveQueuePanel` crash when SplitterDistance was set before control had valid dimensions (deferred to first Resize)
- **Aggressive Site Progress Refresh** — Replaced full panel rebuild every tick with hash-based comparison + scroll position preservation (1.5s interval)
- **PII Hardcoded in Scripts** — Removed hardcoded PnP Client ID from `Start-FileArchiveSearch.ps1`

### 🔧 Changes

- Telemetry endpoint URL corrected from `spo-telemetry` to `spo-telemetry-6406` in AppPaths.json and GitHub Pages
- README screenshot paths fixed to reference actual files
- Timer interval for site progress reduced from 3s to 1.5s

---

## v2.2.0.0 (2026-04-29)

**Release: Windows GUI Orchestration, Telemetry & Documentation Overhaul**

### 🤖 AI-Generated Summary

> This release transforms SPO Version Management from a PowerShell-only automation tool into a full-stack management platform. A native Windows Forms application now provides visual orchestration of all version management operations — policy sync, batch deletion, retention handling, and real-time monitoring — without requiring any PowerShell knowledge. The application is built on .NET 8 with a modern dark-themed UI featuring tabbed panels for execution control, configuration, site history, and tenant-wide storage analytics. Additionally, anonymous opt-in telemetry enables worldwide savings tracking, allowing organizations to see their collective impact on storage cost reduction across all participating tenants.

### ✨ New Features

- **Windows Forms GUI Application** (`SPOVersionManagement.exe`)
  - Full visual orchestration — start/stop/monitor version management jobs
  - Dark-themed modern UI with tabbed panels: Home, Execution, Configuration, History, Archive
  - Real-time execution progress with per-site status tracking
  - Built-in Updates panel: check for new versions, download releases, and install/update scripts directly from the GUI
  - Built-in configuration editor for all settings (credentials, thresholds, filters)
  - One-click site inclusion/exclusion management
  - Tenant storage visualization with historical trends
  - Archive analysis panel for identifying inactive sites

- **Worldwide Savings Telemetry**
  - Anonymous, opt-in telemetry backend for aggregated storage savings
  - `Send-SPOTelemetry` — report session results (storage freed, versions deleted, sites processed)
  - `Get-SPOGlobalStats` — retrieve worldwide aggregate savings data
  - SHA-256 hashed tenant identification (no PII transmitted)
  - WW Savings status page (`docs/ww-savings.html`) with live statistics
  - Configurable via `TelemetryEndpoint`, `TelemetryEnabled`, `TelemetrySalt` in AppPaths.json

- **GitHub Pages & Release Infrastructure**
  - Jekyll-based GitHub Pages configuration (`_config.yml`)
  - Automated release workflow (tag-triggered build + ZIP attachment)
  - Release badge in README

- **Directory Reorganization**
  - `config/` — Application configuration (AppPaths.json, DashboardConfig.json, templates)
  - `web/` — Dashboard HTML and localization assets
  - Clean separation of runtime data (Logs/) from app config

### 📖 Documentation

- Complete README rewrite: problem/solution/outcomes narrative
- Added disclaimer (no Microsoft affiliation, no guarantee)
- Added author section
- Screenshot placeholders for all major UI panels
- `docs/WINDOWS_APP.md` — Full Windows app documentation (build, architecture, panels, troubleshooting)
- `ENTRA_ID_APP_SETUP.md` — Updated to match current Azure Portal UI wording
- Three credential configuration methods documented (JSON, Dashboard, GUI App)
- Manifest fallback for Exchange.ManageAsApp permission

### 🔧 Improvements

- First-run initialization with template files
- PowerShell 5.1 AND 7+ dual requirement documented
- Removed Power BI Integration section (superseded by Dashboard)
- Explicit secrets/certificates exclusions in .gitignore

### 🏗️ Technical

- .NET 8 WinForms application (single-file publish, Windows x64)
- TelemetryService with async HTTP client for backend communication
- ConfigurationService with JSON persistence and schema migration
- Session management with execution history tracking
- Retention policy suspension/resume via Exchange Online integration

---

## v2.1.3.0 (2026-03-15)

**Initial Open Source Release**

- Full WinForms application with PowerShell module orchestration
- Telemetry dashboard setup guide
- Archive management features
- Retention policy manager module
- Site execution history tracking

---

## v2.0.0 (2026-02-05)

**Major Release: Dashboard Accuracy & External Job Sync**

- Phase Badges System (SYNC/DELETE status indicators)
- External Job Sync (`Sync-ExternalJobResults`)
- Reexecution Interval (skip recently processed sites)
- Tenant Storage Timeline tracking
- Dashboard accuracy improvements
- Session-based execution model

---

## v1.5.0 (2026-01-15)

**Multi-Session & Localization**

- Session History with execution tracking
- Complete localization system (English/Portuguese)
- Language selector in header and Settings
- Reset script for deploy mode

---

## v1.4.0 (2025-12-01)

**Archive Analysis & Retention Management**

- Archive Analysis tab in Dashboard
- Retention Policy Manager module
- Automatic policy suspension/resume for batch deletes
- Archive queue management

---

## v1.3.0 (2025-10-15)

**Site History & Optimization**

- Site execution history with popup in Dashboard
- Optimized: single `Get-SPOSite -Limit All -Detailed` call
- Centralized configuration via AppPaths.json
- Storage release evolution chart per site

---

## v1.2.0 (2025-08-01)

**Dashboard & Filtering**

- Interactive HTML Dashboard with multiple tabs
- Graph API integration for Last Activity Date
- Site inclusion/exclusion filters (CSV-based)
- Execution resume after interruption

---

## v1.1.0 (2025-06-01)

**Parallel Execution**

- Parallel execution with automatic orchestration (up to 10 jobs)
- Export to Power BI (CSV format)
- Real-time monitoring via JobStatus.json
- Automatic queue management

---

## v1.0.0 (2025-04-01)

**Initial Release**

- Sequential processing of SPO sites
- SyncListPolicy and BatchDelete phases
- Basic console output
- Version limit configuration
