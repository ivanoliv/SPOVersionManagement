---
layout: default
title: "SharePoint Site & File Archiving — Discover, Evaluate, and Archive at Scale"
description: "How to identify inactive SharePoint sites and large files for archival using Microsoft Graph API, SAM reports, and automated discovery. Reduce storage costs by archiving content that delivers no active business value."
---

<nav class="breadcrumb">
    <a href="{{ '/' | relative_url }}">Home</a> &rsaquo; Guides &rsaquo; SharePoint Site & File Archiving
</nav>

# SharePoint Site & File Archiving

Beyond version cleanup, a significant portion of SharePoint storage is consumed by **inactive sites** and **large files** (video, audio, CAD, backups) that could be archived to lower-cost tiers. SPO Version Management includes discovery and orchestration capabilities to identify, evaluate, and queue archive candidates — using official Microsoft APIs and Graph reports.

> **Orchestration model:** Like all other operations, archiving workflows use official Microsoft APIs. The tool discovers candidates and orchestrates the archival process — it does not move or modify files directly. All operations go through the SharePoint platform's native archiving mechanisms.

---

## Why Archive?

| Problem | Impact |
|---------|--------|
| Inactive project sites consuming quota | Direct cost: $0.20/GB/month for storage that delivers zero value |
| Large media files in document libraries | Video/audio files with full version history consume disproportionate storage |
| Abandoned team sites from past initiatives | Storage quota pressure forces unnecessary purchases |
| Files that haven't been accessed in years | Clutter for Microsoft Copilot — reduces AI result quality |

**Financial example:** 500 GB of inactive content = **$1,200/year** in avoidable storage costs. That's equivalent to ~3 Microsoft 365 Copilot licenses redirected to productivity.

---

## Discovery Capabilities

### 1. Inactive Site Detection

SPO Version Management identifies archive candidates by analyzing:

- **Last Content Modified Date** — when the most recent document change occurred
- **Last Activity Date** (via Microsoft Graph) — actual user interaction (views, edits, shares)
- **Storage consumption** — prioritize high-storage inactive sites for maximum savings
- **Site age** — correlate creation date with activity patterns

```powershell
# Import SAM inactive sites data for archive analysis
.\Import-SamInactiveSites.ps1

# Dashboard shows Archive Candidates tab with:
# - Sites sorted by last activity date
# - Storage consumed per inactive site
# - Potential savings from archival
```

### 2. Large File Discovery

The File Archive Search feature identifies storage-heavy files by extension group:

| Category | Extensions | Typical Impact |
|----------|-----------|----------------|
| Video | .mp4, .avi, .mov, .wmv, .mkv | Often 100MB–10GB per file, with version copies |
| Audio | .wav, .mp3, .flac, .aac | Recording archives consuming significant space |
| CAD/Engineering | .dwg, .step, .stl, .rvt | Large binary files with many versions |
| Archives | .zip, .rar, .7z, .tar | Backup archives that could move to cold storage |
| Disk Images | .iso, .vhd, .vmdk | Virtual machine files stored inappropriately |

```powershell
# Search for large files by extension across the tenant
.\Start-FileArchiveSearch.ps1
```

The GUI application provides an interactive search interface with:
- Extension group selection (predefined + custom)
- Microsoft Graph API–based search across all sites
- Results with file size, location, last modified date
- Queue for archival workflow

### 3. SharePoint Advanced Management (SAM) Integration

If your organization has a **SharePoint Advanced Management license** (add-on to Microsoft 365), SPO Version Management can import the SAM Content Management Assessment report to enrich archive analysis with Microsoft's own inactivity classification.

See the dedicated guide: [SharePoint Advanced Management (SAM) Integration]({{ '/guides/sharepoint-advanced-management-sam/' | relative_url }})

---

## Archive Workflow

### Step 1: Discover Candidates

Run the archive analysis to build a prioritized list:

```powershell
# Imports AllSites.json + optional SAM report → lightweight ArchiveAnalysis.json
.\Import-SamInactiveSites.ps1
```

### Step 2: Evaluate in Dashboard

The Dashboard Archive tab provides:
- **Candidate list** sorted by storage × inactivity score
- **SAM flags** (inactive, ownerless) if SAM report is available
- **Effective dates** tracking when sites first appeared as candidates
- **Filtering** by template, storage range, inactivity period

### Step 3: Queue for Archival

Select candidates and queue them for the archival process:
- Sites are moved to a locked/read-only state
- Owners are notified (configurable)
- Archival uses SharePoint's native site archive feature ([Microsoft documentation](https://learn.microsoft.com/sharepoint/archive-sites))

### Step 4: Execute Archive

```powershell
# Process the archive queue
.\Start-ArchiveWebsites.ps1
```

This orchestrates the native SharePoint archival operation for queued sites, with:
- Parallel execution
- Progress monitoring
- Audit trail
- Rollback capability (un-archive)

---

## Microsoft Copilot Readiness

Archiving inactive content directly improves Microsoft Copilot results:

- **Fewer irrelevant sources** — Copilot won't surface outdated project documents from 2019
- **Better signal-to-noise** — active, maintained content ranks higher in AI responses
- **Governance alignment** — demonstrates data lifecycle management for compliance

---

## Financial Impact

| Inactive Content | Monthly Cost | Annual Cost | Action |
|-----------------|-------------|-------------|--------|
| 200 GB | $40 | $480 | Archive → $0 ongoing |
| 1 TB | $205 | $2,460 | Archive → $0 ongoing |
| 5 TB | $1,024 | $12,288 | Archive → reinvest in 34 Copilot licenses |

*SharePoint archived sites use Microsoft's cold storage tier at significantly reduced or zero ongoing cost, depending on your licensing agreement.*

---

## Built on Official Microsoft APIs

All archiving operations use official, supported Microsoft mechanisms:

| Operation | API/Method | Documentation |
|-----------|-----------|---------------|
| Site activity data | Microsoft Graph `getSharePointSiteUsageDetail` | [Microsoft Learn](https://learn.microsoft.com/graph/api/reportroot-getsharepointsiteusagedetail) |
| File search | Microsoft Graph Search API | [Microsoft Learn](https://learn.microsoft.com/graph/search-concept-overview) |
| Site archiving | SharePoint Online Management Shell | [Microsoft Learn](https://learn.microsoft.com/sharepoint/archive-sites) |
| SAM reports | SharePoint Admin Center export | [Microsoft Learn](https://learn.microsoft.com/sharepoint/site-lifecycle-management) |

---

<div class="cta-box">
    <h3>Discover How Much Inactive Content Is Costing You</h3>
    <p>Run the archive analysis to identify inactive sites and large files consuming your storage budget. Non-destructive — discovery only. See results in the Dashboard with estimated savings in USD.</p>
    <a href="https://github.com/ivanoliv/SPOVersionManagement/releases">Download Free — Start Discovery</a>
</div>

## Related Guides

- [SharePoint Advanced Management (SAM) Integration]({{ '/guides/sharepoint-advanced-management-sam/' | relative_url }})
- [How to Reduce SharePoint Storage Costs]({{ '/guides/reduce-sharepoint-storage-costs/' | relative_url }})
- [SharePoint Governance Best Practices]({{ '/guides/sharepoint-governance-best-practices/' | relative_url }})
- [Complete Guide to SharePoint Version Management]({{ '/guides/sharepoint-version-management/' | relative_url }})
