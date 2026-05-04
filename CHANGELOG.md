# Changelog

All notable changes to SPO Version Management are documented in this file.

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
