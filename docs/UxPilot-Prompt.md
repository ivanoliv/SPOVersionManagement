# UxPilot.ai Prompt — SPO Version Management Desktop GUI

## Context

Design a **dark-themed WinForms desktop application** (1100×750px, min 900×600) for a **SharePoint Online Version Management** tool. This is NOT a web app — it's a native Windows desktop GUI built on .NET Framework 4.8 with WinForms.

The app automates two SharePoint admin operations at tenant scale:
1. **SyncListPolicy** — Syncs version-limit policies to document libraries across all sites
2. **BatchDeleteFileVersions** — Deletes excess file versions that accumulated before the policy existed

It also supports optional **Purview retention policy suspend/resume** (temporarily removes retention holds so versions can be deleted), **site archiving** (marking inactive sites as archived via SharePoint API), and **tenant storage monitoring** with cost projections.

## Design Language

Dark navy/indigo gradient background, neon accent colors. Similar feel to a SOC dashboard or Grafana dark mode.

### Color Palette
| Token | Hex | Usage |
|---|---|---|
| BgDark | `#1a1a2e` | Form gradient start |
| BgMedium | `#16213e` | Gradient end, alternating rows |
| BgCard | `#0f3460` | Card surfaces |
| BgInput | `#1a2744` | Input field backgrounds |
| BgHeader | `#0d1b36` | Status bar, inactive tabs, column headers |
| AccentCyan | `#00d4ff` | Primary accent, links, selected tab |
| AccentPurple | `#7b2cbf` | Session info, retention policy sections |
| AccentGold | `#ffc107` | Warnings, version stats |
| AccentGreen | `#00e676` | Success, Start button, Save, storage freed |
| AccentRed | `#ff5252` | Danger, Stop button, errors, exclusion |
| AccentOrange | `#ff9800` | Running state, execution mode section |
| TextPrimary | `#FFFFFF` | Main text |
| TextSecondary | `#b0b0b0` | Labels |
| TextMuted | `#6c757d` | Hints, subtitles |
| Border | `#2a3a5c` | Card borders |

### Typography
- Body: Segoe UI 9.5pt
- Headings: Segoe UI Semibold 11pt Bold
- Stats: Segoe UI 22pt Bold
- Mono/Console: Cascadia Code 9pt
- Buttons: Segoe UI Semibold 10pt Bold

### Components
- **Cards**: 8px corner radius, BgCard fill, 1px Border stroke
- **Buttons**: 6px corner radius, custom painted (no native borders). Styles: Default (solid accent bg), Ghost (transparent bg, cyan text), Danger (red), Warning (gold, dark text), Success (green)
- **Notification bar**: Slides down from top (36px tall), auto-hides. Variants: Info/Success/Warning/Update

## Form Structure

```
┌──────────────────────────────────────────────────────┐
│ [Notification Bar - animated slide-in, 36px]         │
├──────────────────────────────────────────────────────┤
│ [Home] [Config] [Sites] [Execution] [History] [Updates] │  ← 6 tabs, 120×36px each
├──────────────────────────────────────────────────────┤
│                                                      │
│              Active Tab Panel (Fill)                 │
│                                                      │
├──────────────────────────────────────────────────────┤
│ Ready                                    v2.1.3.3    │  ← Status bar, 28px
└──────────────────────────────────────────────────────┘
```

---

## TAB 1: HOME — Overview Dashboard

At-a-glance tenant status, key stats, and quick navigation.

### Row 1 — 3 info cards (equal width, ~290×145px, 14px gap)

**Card 1: TENANT** (Cyan accent header)
- Tenant name (large bold text extracted from admin URL, e.g. "contoso")
- Admin URL (muted)
- Total sites count

**Card 2: LAST SESSION** (Purple accent header)
- Date/time of the most recent execution session
- Sites processed vs total (e.g. "Processed: 847 / 1,248 sites")
- Status: Completed / InProgress / Failed / Cancelled

**Card 3: QUICK START** (Gold accent header)
- 3 stacked buttons:
  - **▶ Start Execution** (green) → navigates to Execution tab
  - **📊 Open Dashboard** (cyan) → launches the HTML dashboard in browser
  - **⚙ Configuration** (ghost) → navigates to Config tab

### Row 2 — 3 stat cards (equal width, ~290×105px)

**Card 4: STORAGE FREED** (Green accent)
- Large bold number: `X.X GB` or `X.XX TB`
- Subtitle: "Total Reclaimed"
- Source: sum of StorageReleasedInBytes from ExecutionHistory.csv

**Card 5: VERSIONS DELETED** (Gold accent)
- Large bold number with thousands separator
- Subtitle: "Total Cleaned Up"
- Source: sum of VersionsDeleted from ExecutionHistory.csv

**Card 6: WORLDWIDE IMPACT** (Cyan accent)
- Large bold number from telemetry API (or "N/A" if disabled)
- Subtitle: "Global Storage Freed (all users)"

### Row 3 — Action buttons
- **📋 View History** (ghost button) → navigates to History tab
- **💾 Backup Data** (ghost button) → opens folder picker, copies all JSON+CSV data files to selected location

### Footer
- `v{version} | .NET {version} | PowerShell 5.1 Compatible` (muted, small)

---

## TAB 2: CONFIG — Configuration Settings

Scrollable form with all app settings. Tracks dirty state (unsaved changes warning on close).

### Top-right button bar
- **Save** (green) — writes to AppPaths.json and DashboardConfig.json (merge-update, preserves unknown keys)
- **Cancel** (ghost) — discards unsaved changes, reloads from disk
- **💾 Backup** (gold/warning) — same backup-all-data function as Home

### Section: ENTRA ID APP REGISTRATION (Cyan header)
The app authenticates to SharePoint Online using an Entra ID (Azure AD) app registration with certificate.
- **Tenant ID** — Azure AD tenant GUID
- **Client ID** — App registration client ID
- **Certificate Thumbprint** — Local certificate thumbprint for auth

### Section: PURVIEW APP (Optional) (Purple header)
For the optional retention policy management feature. Connects to Security & Compliance (IPPS).
- **Client ID** — Separate app registration for Purview/IPPS
- **Certificate Thumbprint**
- **Organization** — tenant domain (e.g. contoso.onmicrosoft.com)

### Section: DASHBOARD SETTINGS (Gold header)
Controls the HTML monitoring dashboard behavior and display.
- **Language** — dropdown: en, pt-br, es, fr, de, it, ja, ko, zh
- **Currency Symbol** (e.g. $, R$, €) + **Currency Code** (USD, BRL, EUR) — side-by-side
- **Cost per TB/Year (USD)** — used for cost projection calculations
- **Date Format** — dropdown: MM/dd/yyyy, dd/MM/yyyy, yyyy-MM-dd
- **Zero Version Action** — dropdown: syncOnly, deleteOnly, skip — what to do with sites that report 0 versions
- **Dashboard Port** — HTTP listener port (default 8080)

### Section: AUTO-UPDATE (Cyan header)
- **GitHub Repository** — owner/repo slug for checking releases (e.g. ivanoliv/SPOVersionManagement)

### Section: ANONYMOUS TELEMETRY (Green header)
Optional community impact tracking. No PII collected.
- **Enable** checkbox
- Disclaimer text: "No personal information is collected. Only anonymous, aggregated stats: SHA256 hash of Tenant ID (irreversible), app version, storage freed, versions deleted, sites processed."
- **Preview what's sent** button → modal showing sample JSON payload
- **Telemetry Endpoint** — Azure Function URL (empty = disabled)

### Conditional: PERMISSION WARNING (Red, only if app can't write to data folder)
- Warning text with suggested user-directory fallback
- **Switch to User Folder** button

---

## TAB 3: SITES — Include/Exclude Site Lists

Manages which SharePoint sites are processed or skipped during execution.

### Section: INCLUDED SITES (Green header, count badge)
If populated, ONLY these sites will be processed (all others skipped).
- Editable grid (1 column: **Site URL**) — users can type URLs, add/delete rows
- Side buttons: **Import CSV**, **Clear All** (red), **Save** (green)
- Saves to IncludeSites.csv

### Section: EXCLUDED SITES (Red header, count badge)
These sites are always skipped during execution.
- Editable grid (3 columns: **Site URL**, **Site Name**, **Reason**) — users can add/remove
- Side buttons: **Import CSV**, **Clear All** (red), **Save** (green)
- Saves to ExcludeSites.csv

---

## TAB 4: EXECUTION — Run the Engine

The main control panel. Exposes all 16 parameters of the PowerShell orchestrator. Should use logical grouping and clear labels with hint text.

### Section: CONNECTION (Cyan)
- **Admin URL** — SharePoint admin center URL (e.g. `https://contoso-admin.sharepoint.com`)
  - Pre-filled from last session if available

### Section: VERSION LIMITS (Gold)
These control what the BatchDelete phase does.
- **Major Version Limit** — NumericUpDown (1–500, default 4). Hint: "Max major versions to keep per file"
- **Major+Minor Versions Limit** — NumericUpDown (0–500, default 4). Hint: "Max major versions that retain their minor drafts"

### Section: BATCH & CONCURRENCY (Purple)
- **Max Concurrent Jobs** — NumericUpDown (1–50, default 10). Hint: "Parallel SPO jobs running simultaneously"
- **Check Batch Size** — NumericUpDown (1–100, default 10). Hint: "Sites per status-check batch"
- **Batch Delay Seconds** — NumericUpDown (0–60, default 2). Hint: "Delay between batches to avoid API throttling"

### Section: EXECUTION MODE (Orange)
Checkboxes — only one of Sync Only / Delete Only should be active at a time.
- **☐ Sync Only** — "Only sync version policies to sites. No versions are deleted."
- **☐ Delete Only** — "Skip policy sync, only delete excess versions. Use when policies are already synced."
- **☐ Manage Retention Policy** — "Temporarily suspend Purview retention policies before processing, resume after. Requires Purview App config."
- **☐ Reset Database** — "Clear all execution history and start fresh. Cannot be undone."

### Section: OPTIONS (Green)
- **☐ Use File Cache** — "Use cached AllSites.json instead of querying Get-SPOSite. Faster for repeat runs."
- **☐ Skip Graph Connection** — "Skip Microsoft Graph API connection. Use manual CSV or cached storage data."
- **☑ Open Dashboard on Start** — "Auto-launch the monitoring dashboard in browser when execution begins." (default ON)

### Section: INPUT FILES (Optional) (muted header)
Each has a TextBox + Browse [...] button (CSV file picker).
- **Site List CSV** — Override site list instead of querying SPO
- **Exclusion List CSV** — Additional exclusions for this run
- **Sync Site List CSV** — Sites for sync-only pass (used with DeleteOnly mode for re-sync after delete)
- **Graph Report CSV** — Manual SharePoint Site Usage Storage CSV (for orgs without Graph API access)

### Action Buttons
- **▶ Start Execution** (green, large) — builds PowerShell command with all parameters, runs it
- **■ Stop** (red, disabled until running) — sends cancellation signal

### OUTPUT Console
- Monospace RichTextBox (dark bg, read-only, scrolling)
- Color-coded output: white=normal, gold=warnings, red=errors, cyan=status messages, muted=commands
- Shows the generated PowerShell command at the top before execution

---

## TAB 5: HISTORY — Execution Records

Browse historical execution results from ExecutionHistory.csv.

### Header
- Title: "EXECUTION HISTORY" (cyan)
- Record count badge
- Total storage freed stat (green)

### Filter bar
- **Search** — live text filter across site name, URL, status
- **Job Type** — dropdown: All, SyncListPolicy, BatchDelete
- **Session** — dropdown: All Sessions + per-session entries from SessionHistory.json
- **Refresh** button

### Data Grid (full-width, dark themed, resizable)
Columns from ExecutionHistory.csv:
- Timestamp, Site Name (from URL), Job Type (SyncListPolicy/BatchDelete), Status, Duration (minutes), Versions Deleted, Storage Released (formatted), Error Message
- Site URL column: hidden but searchable

---

## TAB 6: UPDATES — Auto-Update from GitHub

### Two cards side-by-side

**CURRENT VERSION** (Cyan) — Shows installed version in large bold text + .NET/PS info

**LATEST VERSION** (Green) — Shows "..." until checked. Green if newer available, cyan if up-to-date.

### Buttons
- **Check for Updates** — queries GitHub Releases API
- **Download & Install** — downloads .zip asset, backs up configs, extracts, merges configs (preserves user settings, adds new keys), prompts restart. Disabled until update confirmed.
- Progress bar (hidden until downloading)

### RELEASE NOTES (Gold header)
- Shows up to 5 recent releases with names, dates, and body text

---

## WHAT THIS APP DOES NOT DO (do NOT include these)

The following features appeared in preliminary mockups but **DO NOT exist** in the actual application:

- ❌ **"Site Limits" tab** — The app does not set per-site version limits via a grid UI. Version limits are set globally as a parameter during execution.
- ❌ **"Auto-Trim Settings" / auto-trim schedule** (Weekly/Monthly toggle) — The app does not schedule automatic runs. It is manually triggered.
- ❌ **"Average Versions / File" metric** — The app does not calculate or track average versions per file.
- ❌ **"Sites Over Limit" metric** — The app does not track sites that exceed a version limit threshold.
- ❌ **"Pending Executions" count with schedule** — There is no scheduling system. The queued sites counter exists only during active execution in JobStatus.json.
- ❌ **Left sidebar navigation** — This is a desktop app with a tab bar, not a web app with sidebar nav.
- ❌ **"Search sites" search bar** in a top nav — No global search. The History tab has a local filter.
- ❌ **"Execute Runbook" button** in top nav — Execution is done via the Execution tab with full parameter configuration, not a single button.
- ❌ **Per-site "Limit Setting" column with edit icons** — Version limits are tenant-wide parameters, not per-site.
- ❌ **"Compliant"/"Review Needed" status badges per site** — The app does not assess compliance status of individual sites.
- ❌ **"Global Configuration" sidebar panel** with "Default Version Limit" dropdown and "Safety Lock"** — No such feature. Limits are execution parameters.
- ❌ **"Auto-Trim Schedule" toggle** (Weekly/Monthly) — No scheduling exists.
- ❌ **"Safety Lock Enabled"** — No safety lock feature.
- ❌ **"Storage Trend (Version History)"** line chart — The app tracks storage via TenantStorageTimeline.json but the GUI doesn't render charts. The HTML Dashboard does.
- ❌ **"Analytics" tab** — No analytics tab in the GUI. The HTML Dashboard has analytics.
- ❌ **"Policies" nav item** — Retention policies are managed as part of execution (an option checkbox), not as a separate management section.
- ❌ **"Security" nav item** — No security settings section.
- ❌ **User profile / avatar** — No user profile in the GUI.

## WHAT THE APP ACTUALLY TRACKS (use these for stats/metrics)

The real data the app produces and can display:
- **Total storage freed** (bytes from ExecutionHistory.csv, formatted as GB/TB)
- **Total versions deleted** (count from ExecutionHistory.csv)
- **Sites processed** (count from SessionHistory.json)
- **Active/queued/completed jobs** (from JobStatus.json during execution)
- **Session history** (last 10 sessions with status, progress, configuration)
- **Per-site execution history** (from SiteExecutionHistory.json: first/last processed, total executions, total freed)
- **Tenant storage** (quota, used, available from TenantStorage.json)
- **Execution records** (flat log with all metrics per job: duration, lists processed, versions deleted, storage released, errors)

---

## Design Priorities

1. **Clarity over density** — SharePoint admins need to understand what each parameter does. Use hints and tooltips generously.
2. **Execution confidence** — The Execution tab is the most critical. Users must clearly see what will happen before clicking Start.
3. **Session awareness** — Show the last session state prominently on Home so users know where they left off.
4. **Dark theme legibility** — Ensure sufficient contrast. Muted text (#6c757d) on dark backgrounds needs checking.
5. **Progressive disclosure** — Most users will use defaults for batch/concurrency settings. Consider grouping advanced params.
6. **Config safety** — Cancel = discard changes. Save = writes to disk. This must be clear.
7. **Desktop-native feel** — This is WinForms, not a web app. No sidebar nav, no breadcrumbs, no routing. Use tab bar + status bar + notification bar patterns.
