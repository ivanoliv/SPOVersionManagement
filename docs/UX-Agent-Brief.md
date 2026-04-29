# UX Agent Brief — SPO Version Management GUI

## What This App Is

**SPO Version Management** is a Windows desktop tool (WinForms, .NET Framework 4.8) that manages SharePoint Online file version policies at scale across an entire tenant. It automates two things: syncing version-limit policies to every document library, then batch-deleting the excess file versions that accumulated before the policy existed. This can free hundreds of GB to TB of SharePoint storage.

The GUI wraps a PowerShell CLI engine. It is the visual orchestrator — configuring, launching, monitoring, and reviewing the history of these batch operations. It can also auto-update itself from GitHub Releases and optionally send anonymous telemetry to track global community impact.

---

## Target Users

SharePoint Online administrators managing tenants with 100–50,000+ sites. Technically competent but not developers. They need clarity, confidence, and a clear sense of progress.

---

## Current Design Tokens

| Token | Value |
|---|---|
| BgDark (form) | `#1a1a2e` |
| BgMedium | `#16213e` |
| BgCard | `#0f3460` |
| BgInput | `#1a2744` |
| BgHeader (status bar, inactive tabs) | `#0d1b36` |
| AccentCyan (primary) | `#00d4ff` |
| AccentPurple | `#7b2cbf` |
| AccentGold (warnings) | `#ffc107` |
| AccentGreen (success) | `#00e676` |
| AccentRed (danger) | `#ff5252` |
| AccentOrange (running) | `#ff9800` |
| TextPrimary | `#FFFFFF` |
| TextSecondary | `#b0b0b0` |
| TextMuted | `#6c757d` |
| Border | `#2a3a5c` |
| Separator | `#1e3050` |
| Fonts | Segoe UI family (body 9.5pt, headings 11pt semibold, stats 22pt bold, mono: Cascadia Code 9pt) |
| Button radius | 6px |
| Card radius | 8px |
| Form size | 1100×750 default, 900×600 minimum |

The theme is a **dark navy/indigo gradient** with neon accents — similar in feel to a SOC dashboard or Grafana dark mode.

---

## Form Structure

```
┌───────────────────────────────────────────────────────────┐
│ [Notification Bar]  (animated slide-in/out, 36px tall)    │
├───────────────────────────────────────────────────────────┤
│ [Home] [Config] [Sites] [Execution] [History] [Updates]   │  ← 6 owner-drawn tabs, 120×36px each
├───────────────────────────────────────────────────────────┤
│                                                           │
│                Active Panel (Dock: Fill)                   │
│             scrollable, gradient background                │
│                                                           │
├───────────────────────────────────────────────────────────┤
│ Ready                                          v2.1.3.3   │  ← Status bar, 28px, BgHeader
└───────────────────────────────────────────────────────────┘
```

---

## Tab-by-Tab Layout Description

---

### TAB 0: HOME — Dashboard Overview

**Purpose:** First thing the user sees. At-a-glance tenant status, key stats, and quick navigation. Should feel informative and inviting.

**Layout — 3 rows of cards (3 columns, 14px gap, 20px margin):**

#### Row 1 — Info Cards (290×145px each)

| Card | Accent | Content |
|---|---|---|
| **TENANT** | Cyan | Tenant name (big, 20pt bold) extracted from admin URL. Below: `Admin: <url>` and `Sites: <count>` in muted text. |
| **LAST SESSION** | Purple | Date/time of last run (13pt bold). Below: `Processed: X / Y sites` and `Status: Completed/Running/Failed`. |
| **QUICK START** | Gold | Three stacked full-width buttons: **▶ Start Execution** (green), **📊 Open Dashboard** (cyan), **⚙ Configuration** (ghost). These navigate to other tabs or launch the dashboard. |

#### Row 2 — Stat Cards (290×105px each)

| Card | Accent | Content |
|---|---|---|
| **STORAGE FREED** | Green | Giant stat number `X.X GB` or `X.XX TB` (28pt bold, green). Below: `Total Reclaimed`. |
| **VERSIONS DELETED** | Gold | Giant stat number with thousands separator (28pt bold, gold). Below: `Total Cleaned Up`. |
| **WORLDWIDE IMPACT** | Cyan | Fetched from telemetry API — total community storage freed (28pt bold, cyan). Below: `Global Storage Freed (all users)`. Shows `...` while loading. |

#### Row 3 — More Actions

- Section label: `MORE ACTIONS` (heading style, muted)
- Two ghost buttons side by side: **📋 View History** and **💾 Backup Data** (175×34px)

#### Footer

- `v{version} | .NET {version} | PowerShell 5.1 Compatible` — muted, small text

---

### TAB 1: CONFIG — Configuration

**Purpose:** All app settings in one scrollable form. Must have Save, Cancel (discard changes), and Backup buttons prominently accessible. Tracks dirty state — warns on close if unsaved.

**Top-right button bar (fixed at y=12, x=620):**
- **Save** (green, 90×32)
- **Cancel** (ghost, 90×32 — reloads from disk)
- **💾 Backup** (gold/warning, 110×32 — opens folder picker, copies all data files)

**Sections (each has a colored header label + accent underline bar):**

1. **ENTRA ID APP REGISTRATION** (Cyan)
   - Tenant ID, Client ID, Certificate Thumbprint — labeled TextBoxes

2. **PURVIEW APP (Optional)** (Purple)
   - Client ID, Certificate Thumbprint, Organization — labeled TextBoxes

3. **DASHBOARD SETTINGS** (Gold)
   - Language → dropdown (en, pt-br, es, fr, de, it, ja, ko, zh)
   - Currency Symbol (60px) + Currency Code (60px) on same row
   - Cost per TB/Year (USD) → TextBox
   - Date Format → dropdown
   - Zero Version Action → dropdown (syncOnly, deleteOnly, skip)
   - Dashboard Port → TextBox (80px)

4. **AUTO-UPDATE** (Cyan)
   - GitHub Repository → TextBox with hint `owner/repo`

5. **ANONYMOUS TELEMETRY** (Green)
   - Enable checkbox + multi-line disclaimer text explaining no PII is collected (SHA256 hash, anonymous stats only)
   - **Preview what's sent** button → opens a modal showing sample JSON payload
   - Telemetry Endpoint URL → TextBox with hint

6. **PERMISSION WARNING** (Red) — *only visible if the app can't write to the data folder*
   - Warning text + suggested fallback path
   - **Switch to User Folder** button (gold)

---

### TAB 2: SITES — Include/Exclude Site Lists

**Purpose:** Manage which SharePoint sites are processed or skipped. Two editable grids.

**Section 1: INCLUDED SITES** (Green header, count badge)
- Editable DataGridView (700×180px) — column: `Site URL`
- Users can type URLs directly, add/delete rows
- Side buttons: **Import CSV** (ghost), **Clear All** (red/danger), **Save** (green)

**Section 2: EXCLUDED SITES** (Red header, count badge)
- Editable DataGridView (700×180px) — columns: `Site URL`, `Site Name`, `Reason`
- Same side buttons: Import, Clear, Save

Data is loaded from/saved to CSV files (IncludeSites.csv, ExcludeSites.csv).

---

### TAB 3: EXECUTION — Run the Engine

**Purpose:** The control room. Exposes ALL 16 CLI parameters the PowerShell script accepts, with clear labels and explanatory hints. Then shows live output.

**Sections:**

1. **CONNECTION** (Cyan)
   - Admin URL → TextBox with hint `https://tenant-admin.sharepoint.com`

2. **VERSION LIMITS** (Gold)
   - Major Version Limit → NumericUpDown (1–500, default 4) + hint
   - Major+Minor Versions Limit → NumericUpDown (0–500, default 4) + hint

3. **BATCH & CONCURRENCY** (Purple)
   - Max Concurrent Jobs → NumericUpDown (1–50, default 10) + hint
   - Check Batch Size → NumericUpDown (1–100, default 10) + hint
   - Batch Delay Seconds → NumericUpDown (0–60, default 2) + hint

4. **EXECUTION MODE** (Orange) — checkboxes with tooltip descriptions
   - ☐ Sync Only — policy sync only, no deletions
   - ☐ Delete Only — skip sync, only delete excess versions
   - ☐ Manage Retention Policy — pause/resume Purview retention
   - ☐ Reset Database — nuclear option, clears all history

5. **OPTIONS** (Green) — checkboxes with tooltips
   - ☐ Use File Cache — resume interrupted sessions
   - ☐ Skip Graph Connection — use cached/CSV data
   - ☑ Open Dashboard on Start — auto-launch monitoring (default ON)

6. **INPUT FILES (Optional)** (muted header) — 4 rows, each with TextBox + `[...]` browse button
   - Site List CSV, Exclusion List CSV, Sync Site List CSV, Graph Report CSV

**Action Buttons:**
- **▶ Start Execution** (green, 180×38) — builds the full PS command and runs it
- **■ Stop** (red, 100×38) — cancels via CancellationToken, disabled until running

**OUTPUT console:**
- `OUTPUT` section header (Cyan)
- RichTextBox (820×180px, monospace Cascadia Code, dark bg, read-only)
- Color-coded streaming output: white=normal, gold=warnings, red=errors, cyan=status, muted=commands

---

### TAB 4: HISTORY — Execution Records

**Purpose:** Browse and search all past execution records (loaded from ExecutionHistory.csv).

**Header bar:**
- Title: `EXECUTION HISTORY` (Cyan)
- Record count: `N records` (muted)
- Total freed stat: `Total freed: X.X GB` (green)

**Filter bar:**
- Search TextBox (250px) — live filter across Site, URL, Status columns
- Job Type dropdown: All, SyncListPolicy, BatchDelete
- Session dropdown: All Sessions + per-session entries
- Refresh button (ghost)

**Data Grid:**
- Full-width DataGridView (940×480px, anchored to all edges for resizing)
- Dark themed: alternating rows, cyan header text, full-row select
- Columns from ExecutionHistory.csv: Timestamp, Site URL (hidden but searchable), Site Name, Job Type, Status, Duration, Versions Deleted, Storage Released, Error Message

---

### TAB 5: UPDATES — Auto-Update from GitHub

**Purpose:** Check for new releases, read changelogs, download and install updates with config preservation.

**Two side-by-side cards (380×130px, 15px gap):**

| Card | Content |
|---|---|
| **CURRENT VERSION** (Cyan) | Version number in 26pt bold cyan. Footer: `.NET version + PS 5.1` |
| **LATEST VERSION** (Green) | `...` initially → shows version after check. Green if newer, Cyan if current. Status text below. |

**Buttons row:**
- **Check for Updates** (cyan, 170×36) — queries `github.com/repos/{owner}/{repo}/releases/latest`
- **Download & Install** (green, 170×36) — disabled until update confirmed
- Hidden ProgressBar (300×22) — appears during download with percentage

**RELEASE NOTES section (Gold header):**
- RichTextBox (750×280px) — shows up to 5 releases with headers, dates, and markdown-ish body text, separated by horizontal rules

**Update flow:** Check → Compare versions → Download ZIP → Backup configs → Extract → Merge configs (add new keys, preserve user values) → Prompt restart

---

## Notification Bar Behavior

- Slides down from top (0→36px) at ~60fps animation
- Overlays the tab area (BringToFront)
- Structure: Icon (emoji) | Message text | Optional action link | Close button (✕)
- Background: `#0a2948` with a 2px colored bottom border
- Variants:
  - **Info** (ℹ, Cyan border)
  - **Success** (✔, Green border, auto-hide 5s)
  - **Warning** (⚠, Gold border)
  - **Update** (🔔, Cyan border, with "View Release Notes" action link)
- Auto-hide timer (default 15s, success 5s)
- Close button fades on hover

---

## Status Bar

- 28px tall, docked bottom, `#0d1b36` background
- Left: dynamic status text (save confirmations, execution progress %, errors)
- Right: version label `v2.1.3.3`, anchored to right edge

---

## Custom Button Component (FlatButton)

All buttons use an owner-drawn rounded rectangle button:
- 6px corner radius, no native borders
- Styles: **Default** (solid accent bg, white text), **Ghost** (transparent bg, cyan text, subtle hover glow), **Danger** (red bg), **Warning** (gold bg, dark text), **Success** (green bg)
- Hover brightens, press darkens
- Hand cursor
- Font: Segoe UI Semibold 10pt Bold

---

## Key UX Concerns to Address

1. **Information density** — The Execution tab has 16 parameters. They need logical grouping and clear visual hierarchy so it doesn't feel like a wall of inputs.
2. **Progressive disclosure** — Most users will use defaults. Consider collapsible sections or an "Advanced" toggle for rarely-used parameters.
3. **Responsive layout** — The form can be resized (900×600 minimum). Cards and grids should adapt. Currently most controls use absolute pixel positioning.
4. **Navigation clarity** — Quick Start buttons on Home should make it obvious they navigate to other tabs. The tab bar needs clear active state.
5. **Execution feedback** — When a run is active, the user needs to know: what's happening, how far along, and that the Stop button works. Consider a progress indicator beyond the console output.
6. **Config safety** — The Cancel button (discard changes) and unsaved-changes-on-close prompt are critical. The Backup button needs to feel trustworthy.
7. **Update trust** — The update flow modifies the installation. The backup-before-update step should be visible and reassuring.
8. **Dark theme legibility** — Ensure sufficient contrast ratios. Muted text (`#6c757d`) on dark backgrounds may need attention for accessibility.
9. **Permission handling** — The app may not have write access to its data folder. The warning + fallback must be noticeable but not alarming.
10. **Cards layout** — The 3×2 grid on Home is the visual centerpiece. Cards should have consistent sizing and alignment.
