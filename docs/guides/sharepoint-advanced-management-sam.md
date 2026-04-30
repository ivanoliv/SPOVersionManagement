---
layout: default
title: "SharePoint Advanced Management (SAM) Integration — Inactive Sites & Ownership Policies"
description: "How SPO Version Management integrates with SharePoint Advanced Management (SAM) inactive sites policy, site ownership policy, and Content Management Assessment reports to provide data-driven governance decisions."
---

<nav class="breadcrumb">
    <a href="{{ '/' | relative_url }}">Home</a> &rsaquo; Guides &rsaquo; SharePoint Advanced Management (SAM) Integration
</nav>

# SharePoint Advanced Management (SAM) Integration

SPO Version Management integrates with **SharePoint Advanced Management (SAM)** to leverage Microsoft's built-in site lifecycle policies — combining SAM's classification intelligence with automated orchestration for version cleanup, archival, and governance.

> **License requirement:** SharePoint Advanced Management is a **paid add-on** that requires a separate license (Microsoft 365 E5, Microsoft Syntex - SharePoint Advanced Management, or equivalent). SPO Version Management works without SAM — but if your organization has SAM licensed, it can import and act on SAM's policy data for richer governance decisions.

---

## What Is SharePoint Advanced Management?

SharePoint Advanced Management ([Microsoft documentation](https://learn.microsoft.com/sharepoint/advanced-management)) is a Microsoft add-on that provides advanced governance capabilities:

| SAM Feature | What It Does | How SPO Version Management Uses It |
|-------------|-------------|--------------------------------------|
| **Inactive Sites Policy** | Identifies sites with no user activity for a configurable period (default: 180 days) | Imports inactive site flags → prioritizes archive candidates |
| **Site Ownership Policy** | Detects sites without active owners and prompts ownership confirmation | Imports ownerless flags → identifies governance gaps |
| **Content Management Assessment** | Generates a CSV report with site health metrics | Primary data source for enriched archive analysis |
| **Site Access Review** | Periodic access governance for sensitive sites | Complementary — SPO Version Management focuses on storage |
| **Data Access Governance** | Identifies overshared content | Complementary — informs cleanup priorities |

### SAM Licensing

| License | Includes SAM |
|---------|-------------|
| Microsoft 365 E5 | ✅ Yes |
| Microsoft Syntex - SharePoint Advanced Management | ✅ Yes (standalone add-on) |
| Microsoft 365 E3 | ❌ No (requires add-on purchase) |
| Microsoft 365 Business Premium | ❌ No |
| SharePoint Online Plan 2 | ❌ No |

**Official licensing details:** [SharePoint Advanced Management overview](https://learn.microsoft.com/sharepoint/advanced-management)

> **Without SAM:** SPO Version Management still identifies inactive sites using Microsoft Graph activity data (`Last Activity Date`) and SharePoint's `LastContentModifiedDate`. SAM enriches this with Microsoft's own classification but is not required.

---

## SAM Inactive Sites Policy

### How Microsoft's Policy Works

The SAM Inactive Sites Policy ([Microsoft documentation](https://learn.microsoft.com/sharepoint/site-lifecycle-management)) monitors site activity and:

1. **Classifies sites as inactive** if no user activity occurs within the configured period (default 180 days)
2. **Sends notifications** to site owners asking them to confirm the site is still needed
3. **Exports a report** (Content Management Assessment CSV) with all classified sites

Activity signals Microsoft monitors include:
- File views and edits
- Page visits
- Sharing actions
- Site navigation

### How SPO Version Management Imports SAM Data

```powershell
# Import SAM inactive sites report
# Auto-detects the SAM CSV in the Logs folder, or specify explicitly
.\Import-SamInactiveSites.ps1

# Or specify the SAM report path
.\Import-SamInactiveSites.ps1 -SAMReportPath ".\Logs\Report created by Content Management Assessment_20260402184309000.csv"
```

The import process:
1. Reads your `AllSites.json` (from the Data Sync)
2. Loads the SAM Content Management Assessment CSV
3. Matches sites by URL
4. Enriches each site with SAM flags: `IsInactive`, `IsOwnerless`, `ConnectedToTeams`, `SensitivityLabel`
5. Outputs a lightweight `ArchiveAnalysis.json` for the Dashboard

### Dashboard Integration

After import, the Dashboard Archive tab shows:

| Column | Source | Description |
|--------|--------|-------------|
| Site URL | AllSites.json | SharePoint site address |
| Storage | AllSites.json | Current storage consumption (MB) |
| Last Activity | Graph API | Most recent user interaction |
| **SAM: Inactive** | SAM Report | Microsoft classified as inactive (180-day policy) |
| **SAM: Ownerless** | SAM Report | No active site owner detected |
| **SAM: Sensitivity** | SAM Report | Sensitivity label applied |
| **SAM: Teams Connected** | SAM Report | Whether site is linked to a Teams team |
| Effective Date | Tracking | When site first appeared as candidate |

---

## SAM Site Ownership Policy

### How Microsoft's Policy Works

The Site Ownership Policy ([Microsoft documentation](https://learn.microsoft.com/sharepoint/site-lifecycle-management)) ensures every site has an accountable owner:

1. **Detects ownerless sites** — sites where all listed owners have left the organization or been disabled
2. **Prompts for new owner assignment** — sends emails to site members asking them to claim ownership
3. **Escalates** to admin if no owner is claimed within the configured period

### Why This Matters for Storage Governance

Ownerless sites are often the largest storage consumers because:
- No one is accountable for cleanup decisions
- Version accumulation goes unnoticed
- No one responds to storage quota warnings
- Retention policies may be misconfigured

SPO Version Management flags ownerless sites in the archive analysis, enabling admins to:
- Prioritize ownerless + inactive sites for archival
- Identify sites consuming storage with no governance
- Correlate ownership gaps with version bloat

---

## Integration Workflow

### Without SAM (All Tenants)

```
┌─────────────────────────────────────────┐
│  Data Sources (no SAM required)         │
│  • AllSites.json (SPO Admin API)        │
│  • Microsoft Graph activity reports     │
│  • LastContentModifiedDate              │
└────────────────────┬────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────┐
│  Import-SamInactiveSites.ps1            │
│  → ArchiveAnalysis.json                 │
│  → Dashboard: Archive Candidates        │
└─────────────────────────────────────────┘
```

### With SAM (Licensed Tenants)

```
┌─────────────────────────────────────────┐
│  Data Sources (with SAM)                │
│  • AllSites.json (SPO Admin API)        │
│  • Microsoft Graph activity reports     │
│  • SAM Content Management Assessment    │ ← Additional
│    - Inactive flag (180-day policy)     │
│    - Ownerless flag                     │
│    - Sensitivity labels                 │
│    - Teams connection status            │
└────────────────────┬────────────────────┘
                     │
                     ▼
┌─────────────────────────────────────────┐
│  Import-SamInactiveSites.ps1            │
│  → Enriched ArchiveAnalysis.json        │
│  → Dashboard: Archive + SAM columns     │
│  → Prioritized governance decisions     │
└─────────────────────────────────────────┘
```

---

## How to Export the SAM Report

1. Go to the **SharePoint Admin Center** → `https://contoso-admin.sharepoint.com`
2. Navigate to **Sites** → **Active sites**
3. Click **Content Management Assessment** (requires SAM license)
4. Select **Export report** → downloads a CSV
5. Place the CSV in your `Logs\` folder (auto-detected) or specify with `-SAMReportPath`

The CSV contains columns:
- `URL` — Site URL
- `Is inactive` — True/False (based on configured inactivity period)
- `Is ownerless` — True/False
- `Last activity date (UTC)` — Microsoft's last detected activity
- `Template` — Site template type
- `Connected to Teams` — True/False
- `Sensitivity label` — Applied sensitivity label
- `Email address of site owners` — Current owner(s)

---

## Decision Matrix: SAM + SPO Version Management

| Scenario | SAM Says | Storage Impact | Recommended Action |
|----------|----------|---------------|-------------------|
| Inactive + Ownerless + High Storage | 🔴 Archive candidate | $X/year wasted | Archive immediately |
| Inactive + Has Owner + High Storage | 🟡 Notify owner | $X/year at risk | Notify → wait → archive if no response |
| Active + High Version Bloat | 🟢 Keep, optimize | $X/year recoverable | Run version cleanup (SyncOnly → BatchDelete) |
| Inactive + Low Storage | ⚪ Low priority | Minimal impact | Queue for next governance cycle |
| Active + Ownerless | 🟡 Governance gap | N/A | Assign owner via SAM policy |

---

## Complementary Microsoft Ecosystem

SPO Version Management leverages the full Microsoft 365 admin ecosystem:

| Tool | Purpose | Integration |
|------|---------|-------------|
| **SharePoint Admin Center** | Site management, storage quotas | Data source (Get-SPOSite) |
| **SharePoint Advanced Management** | Inactive/ownerless classification | CSV import for enriched analysis |
| **Microsoft Graph API** | Activity reports, file search | Graph Reports + Search API |
| **SharePoint Online Management Shell** | Version policy + batch deletion | Core operations ([VersionPolicyJob](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositemanageversionpolicyjob), [BatchDeleteJob](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositefileversionbatchdeletejob)) |
| **Microsoft Purview** | Retention policies, compliance | Auto-detect and handle retention conflicts |
| **Power BI** | Advanced reporting | CSV exports for custom dashboards |

This positions SPO Version Management as a **governance orchestration hub** that consumes data from across the Microsoft 365 ecosystem and executes actions through official, supported APIs.

---

## Official Microsoft Documentation

| Topic | Link |
|-------|------|
| SharePoint Advanced Management overview | [learn.microsoft.com/sharepoint/advanced-management](https://learn.microsoft.com/sharepoint/advanced-management) |
| Site lifecycle management (inactive sites policy) | [learn.microsoft.com/sharepoint/site-lifecycle-management](https://learn.microsoft.com/sharepoint/site-lifecycle-management) |
| Site ownership policy | [learn.microsoft.com/sharepoint/site-lifecycle-management](https://learn.microsoft.com/sharepoint/site-lifecycle-management) |
| SharePoint site archiving | [learn.microsoft.com/sharepoint/archive-sites](https://learn.microsoft.com/sharepoint/archive-sites) |
| Content Management Assessment reports | [learn.microsoft.com/sharepoint/data-access-governance-reports](https://learn.microsoft.com/sharepoint/data-access-governance-reports) |
| Microsoft Graph SharePoint usage reports | [learn.microsoft.com/graph/api/reportroot-getsharepointsiteusagedetail](https://learn.microsoft.com/graph/api/reportroot-getsharepointsiteusagedetail) |
| SAM licensing | [learn.microsoft.com/sharepoint/advanced-management](https://learn.microsoft.com/sharepoint/advanced-management) |

---

<div class="cta-box">
    <h3>Leverage Your SAM Investment for Storage Governance</h3>
    <p>If your organization has SharePoint Advanced Management licensed, SPO Version Management can import SAM's inactive and ownerless site classification to make data-driven archival decisions. No SAM? The tool still works using Microsoft Graph activity data.</p>
    <a href="https://github.com/ivanoliv/SPOVersionManagement/releases">Download Free — Works With or Without SAM</a>
</div>

## Related Guides

- [SharePoint Site & File Archiving]({{ '/guides/sharepoint-site-file-archiving' | relative_url }})
- [SharePoint Governance Best Practices]({{ '/guides/sharepoint-governance-best-practices' | relative_url }})
- [How to Reduce SharePoint Storage Costs]({{ '/guides/reduce-sharepoint-storage-costs' | relative_url }})
- [Complete Guide to SharePoint Version Management]({{ '/guides/sharepoint-version-management' | relative_url }})
