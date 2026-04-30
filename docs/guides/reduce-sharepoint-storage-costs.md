---
layout: default
title: "How to Reduce SharePoint Storage Costs — Step-by-Step Guide"
description: "Practical strategies to reduce SharePoint Online storage costs. Learn how to identify version bloat, calculate ROI, and implement automated cleanup to avoid Microsoft 365 Extra File Storage charges."
---

<nav class="breadcrumb">
    <a href="{{ '/' | relative_url }}">Home</a> &rsaquo; Guides &rsaquo; Reduce SharePoint Storage Costs
</nav>

# How to Reduce SharePoint Storage Costs

Microsoft 365 tenants receive a base SharePoint storage allocation (1TB + 10GB per licensed user). When you exceed that allocation, Microsoft charges **$0.20 per GB per month** for Extra File Storage — adding up to **$2,400 per TB per year**.

Most organizations don't realize that a significant portion of their consumed storage is **accumulated file versions** that serve no business purpose. This guide shows you how to identify waste, calculate savings, and implement a cost reduction strategy using official, supported Microsoft APIs.

> **How it works:** SPO Version Management orchestrates native SharePoint Online operations (`New-SPOSiteManageVersionPolicyJob`, `New-SPOSiteFileVersionBatchDeleteJob`) to apply version policies and remove excess versions at scale. It does not access or modify document content directly — it instructs SharePoint to enforce the policies you configure, the same way the admin center would.

---

## Understanding SharePoint Storage Pricing

### Base Allocation

| Component | Storage Included |
|-----------|-----------------|
| Tenant base | 1 TB |
| Per licensed user | +10 GB |
| Example: 500 users | 1 TB + 5 TB = **6 TB total** |

### Overage Pricing

When you exceed the base allocation:

- **$0.20/GB/month** = **$2.40/GB/year**
- **$204.80/TB/month** = **$2,457.60/TB/year**
- Billed monthly against your Microsoft 365 subscription

### The Hidden Cost Driver

In most tenants, **30–70% of consumed storage is file version history** — previous versions of documents that SharePoint retains indefinitely by default (up to 500 major versions per file).

This means:
- A tenant showing 8TB used may have 3–5TB of recoverable version storage
- That excess version storage costs $7,200–$12,000/year in avoidable charges
- The storage grows monthly as users continue editing documents

---

## Step 1: Audit Your Current Storage

### Check Tenant Storage in Admin Center

1. Go to **Microsoft 365 Admin Center** → **SharePoint admin center**
2. Navigate to **Active sites** → view **Storage used** column
3. Note: Admin center shows total storage per site but not version-specific breakdown

### Use SPO Version Management for Detailed Analysis

```powershell
# Export all sites with storage data
.\Export-AllSPOSites.ps1 -AdminUrl "https://contoso-admin.sharepoint.com"

# Or run assessment mode (sets policies, shows potential savings, deletes nothing)
.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 20 `
    -SyncOnly `
    -OpenDashboard
```

The Dashboard shows:
- Total tenant storage consumed vs. quota
- Per-site storage breakdown
- Estimated savings from version cleanup
- Sites with highest version accumulation

---

## Step 2: Calculate Your Potential Savings

### Quick Estimation Formula

```
Potential Annual Savings = (Current Storage Used × Version Percentage × Recovery Rate) × $2.40/GB/year
```

**Example:**
- Tenant uses 10TB
- Estimated 50% is version history = 5TB of versions
- Conservative 40% recovery rate = 2TB recoverable
- **Annual savings: 2,048 GB × $2.40 = $4,915/year**

### Real-World Benchmarks

| Tenant Size | Typical Version Bloat | Recoverable | Annual Savings |
|-------------|----------------------|-------------|----------------|
| 2TB used | 800GB–1.2TB | 400–700GB | $960–$1,680 |
| 10TB used | 4–6TB | 2–3.5TB | $4,800–$8,400 |
| 50TB used | 20–30TB | 10–18TB | $24,000–$43,200 |

---

## Step 3: Identify Quick Wins

### Top Storage Consumers

Focus on sites that consume the most storage first:

1. **Large collaboration sites** — Highly active document libraries with frequent editing
2. **Former project sites** — Inactive but still holding years of version history
3. **Migration artifacts** — Sites migrated from on-premises that brought full version history
4. **Power Platform sites** — Sites used by Power Automate flows that trigger frequent saves

### File Types That Accumulate Versions

| File Type | Why It's Expensive |
|-----------|-------------------|
| Excel workbooks | Multiple users, AutoSave creates versions every few seconds |
| PowerPoint decks | Large files (50–200MB) with frequent edits |
| Visio/Project files | Large binary files, every save = full copy |
| PDF documents | Generated reports saved repeatedly |

---

## Step 4: Implement Version Limits

Setting a version limit doesn't delete existing versions immediately — it defines what the new policy should be. Deletion happens in a separate phase.

### Recommended Limits by Use Case

| Scenario | Major Version Limit | Rationale |
|----------|-------------------|-----------|
| General business content | 20 | 99% of rollbacks happen within the last 10 versions |
| Heavily collaborated docs | 50 | Teams editing the same file need more history |
| Archive/inactive sites | 5–10 | Minimal retention for dormant content |
| Regulated content | 100+ or exempt | Don't touch — exclude from processing |

### Apply Limits with SPO Version Management

```powershell
# Apply 20-version limit across all sites (non-destructive first pass)
.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 20 `
    -SyncOnly

# After reviewing results, execute deletion
.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 20 `
    -MaxConcurrentJobs 10
```

---

## Step 5: Execute Cleanup and Monitor

### Execution Approach

1. **Pilot** — Run on 10–20 non-critical sites first
2. **Verify** — Check recycle bin for deleted versions, confirm no user impact
3. **Scale** — Execute across the full tenant with parallel processing
4. **Monitor** — Use the real-time Dashboard to track progress and savings

### Expected Timeline

| Tenant Size | Concurrent Jobs | Estimated Duration |
|-------------|----------------|-------------------|
| 100 sites | 5 | 30–60 minutes |
| 1,000 sites | 10 | 2–4 hours |
| 5,000 sites | 10 | 6–12 hours |
| 10,000+ sites | 10 | 12–24 hours |

### Post-Cleanup Verification

```powershell
# Open Dashboard to see results
.\Start-Dashboard.ps1
```

The Dashboard displays:
- Storage freed per site and total
- Cost savings calculation
- Sites that were skipped (retention policies, zero versions)
- Any errors or failures

---

## Step 6: Prevent Regrowth

Version cleanup is not a one-time event. Without ongoing management, versions accumulate again.

### Proactive Measures

1. **Set version limits at the tenant level** — Microsoft now supports tenant-wide version policies
2. **Schedule recurring cleanup** — Run SPO Version Management monthly or quarterly
3. **Configure reexecution intervals** — Skip recently cleaned sites automatically
4. **Monitor storage trends** — Dashboard shows growth trajectory over time

### Cost Avoidance Tracking

Track month-over-month:
- Total storage consumed (should stay flat or decrease)
- Extra File Storage charges on your Microsoft invoice
- Version-related storage as a percentage of total

---

## Why Use a Dedicated Tool vs. Custom Scripts?

| Aspect | Custom PowerShell Script | SPO Version Management |
|--------|--------------------------|------------------------|
| Scope | One site at a time | Full tenant, all sites |
| Parallelism | Manual threading | Built-in queue (10 concurrent) |
| Monitoring | Console output | Real-time Dashboard + GUI |
| Retention handling | None (fails silently) | Auto-detect, suspend, resume |
| Resume after failure | Start over | Pick up where you left off |
| Cost tracking | Manual calculation | Automatic per-session savings |
| Risk | Depends on script quality | Non-destructive assessment mode, pilot-first |
| Cost | Free (your time) | Free (your time, but 10x less of it) |

The tool is free and open-source (MIT license). It uses the same official Microsoft APIs you would use in a custom script — [`New-SPOSiteManageVersionPolicyJob`](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositemanageversionpolicyjob) for policy enforcement and [`New-SPOSiteFileVersionBatchDeleteJob`](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositefileversionbatchdeletejob) for batch deletion — but adds orchestration, monitoring, error handling, and governance that would take weeks to build from scratch.

---

## ROI Calculation Template

| Item | Value |
|------|-------|
| Current excess storage cost | $__/month |
| Storage freed after cleanup | __ TB |
| Monthly savings | $__ |
| Annual savings | $__ |
| Tool cost | $0 (open source) |
| Implementation effort | 2–4 hours |
| **Payback period** | **Immediate** |

---

<div class="cta-box">
    <h3>See Your Potential Savings in 5 Minutes</h3>
    <p>Run a non-destructive assessment (SyncOnly mode). The Dashboard shows exactly how much storage you can recover — before any versions are deleted. Deleted versions go to recycle bin (93-day recovery window).</p>
    <a href="{{ '/' | relative_url }}">Use the Savings Calculator</a>
</div>

## Related Guides

- [Complete Guide to SharePoint Version Management]({{ '/guides/sharepoint-version-management' | relative_url }})
- [PowerShell Automation for Version Cleanup]({{ '/guides/powershell-sharepoint-version-cleanup' | relative_url }})
- [SharePoint Governance Best Practices]({{ '/guides/sharepoint-governance-best-practices' | relative_url }})
