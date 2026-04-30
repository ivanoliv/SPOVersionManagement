# Changelog

All notable changes to SPO Version Management are documented in this file.

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
