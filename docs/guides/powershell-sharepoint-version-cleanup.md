---
layout: default
title: "PowerShell Automation for SharePoint Version Cleanup — Commands & Scripts"
description: "Complete reference for SharePoint Online version management PowerShell cmdlets. Learn New-SPOSiteManageVersionPolicyJob, batch deletion, parallel execution, and automation scripts."
---

<nav class="breadcrumb">
    <a href="{{ '/' | relative_url }}">Home</a> &rsaquo; Guides &rsaquo; PowerShell SharePoint Version Cleanup
</nav>

# PowerShell Automation for SharePoint Version Cleanup

SharePoint Online provides PowerShell cmdlets for managing file version policies and deleting excess versions at scale. This guide covers the official cmdlets, automation patterns, and how SPO Version Management orchestrates them for production use.

> **Important distinction:** SPO Version Management is not a replacement for the SharePoint Online Management Shell — it is an **orchestration layer on top of it**. Every operation it performs uses the same official Microsoft cmdlets documented below. It adds parallel execution, monitoring, queue management, retention policy handling, and error recovery — but the underlying API calls are identical to what you would run manually.

This means:
- **Fully supported** — uses only documented, supported Microsoft APIs
- **No direct data access** — never touches document content, only instructs SharePoint to enforce policies
- **Future-proof** — if Microsoft updates the cmdlets, the tool follows the same upgrade path
- **Auditable** — every operation is logged and traceable

---

## Built on Official Microsoft APIs

All version management operations in this guide — and in SPO Version Management — use two officially documented cmdlets from the SharePoint Online Management Shell:

| Cmdlet | Purpose | Documentation |
|--------|---------|---------------|
| `New-SPOSiteManageVersionPolicyJob` | Applies version limits to all document libraries in a site | [Microsoft Learn](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositemanageversionpolicyjob) |
| `New-SPOSiteFileVersionBatchDeleteJob` | Deletes versions exceeding configured limits (goes to recycle bin) | [Microsoft Learn](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositefileversionbatchdeletejob) |

These are the same APIs Microsoft uses internally for tenant-scale version lifecycle management. They are fully supported, documented, and maintained by the SharePoint product team. Using them ensures:

- **Compatibility** — guaranteed to work with current and future SharePoint Online versions
- **Supportability** — covered by Microsoft support if issues arise
- **Compliance** — operations respect platform-level retention and compliance settings

---

## Prerequisites

### Required Modules

```powershell
# SharePoint Online Management Shell (required)
Install-Module -Name Microsoft.Online.SharePoint.PowerShell -Force

# Microsoft Graph PowerShell (optional, for storage reports)
Install-Module -Name Microsoft.Graph -Force

# PnP PowerShell (optional, for advanced scenarios)
Install-Module -Name PnP.PowerShell -Force
```

### Required Permissions

| Module | Required Role |
|--------|--------------|
| SPO Management Shell | SharePoint Administrator or Global Administrator |
| Microsoft Graph | `Sites.Read.All`, `Reports.Read.All` |
| PnP PowerShell | SharePoint Administrator + app registration |

### Authentication

```powershell
# Interactive authentication (browser login with MFA)
Connect-SPOService -Url "https://contoso-admin.sharepoint.com"

# Certificate-based authentication (for automation/scheduled tasks)
Connect-SPOService -Url "https://contoso-admin.sharepoint.com" `
    -ClientId "your-app-id" `
    -Tenant "contoso.onmicrosoft.com" `
    -CertificatePath "C:\certs\SPOAdmin.pfx" `
    -CertificatePassword (ConvertTo-SecureString "password" -AsPlainText -Force)
```

---

## Core Cmdlets for Version Management

The two primary cmdlets below are official, documented SharePoint Online Management Shell commands. They are the same mechanisms SharePoint uses internally for version lifecycle management.

### 1. Apply Version Policy (SyncListPolicy)

The [`New-SPOSiteManageVersionPolicyJob`](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositemanageversionpolicyjob) cmdlet sets the version limit on all document libraries within a site. This is an official Microsoft API — fully supported, documented, and safe for production use. Does **not** delete existing versions.

```powershell
# Set version limit to 20 major versions for all libraries in a site
New-SPOSiteManageVersionPolicyJob `
    -Identity "https://contoso.sharepoint.com/sites/Finance" `
    -MajorVersionLimit 20 `
    -MajorWithMinorVersionsLimit 20 `
    -SyncListPolicy
```

**Parameters:**

| Parameter | Description |
|-----------|-------------|
| `-Identity` | Site URL to apply the policy to |
| `-MajorVersionLimit` | Maximum major versions to keep (1–500) |
| `-MajorWithMinorVersionsLimit` | Maximum minor (draft) versions per major version |
| `-SyncListPolicy` | Switch indicating this is a policy sync operation |

### 2. Monitor Policy Sync Progress

```powershell
# Check progress of the policy sync job
$progress = Get-SPOSiteManageVersionPolicyJobProgress `
    -Identity "https://contoso.sharepoint.com/sites/Finance"

$progress | Select-Object Status, LibrariesProcessed, LibrariesTotal
```

**Status Values:**

| Status | Meaning |
|--------|---------|
| `InProgress` | Job is still running |
| `CompleteSuccess` | All libraries processed successfully |
| `CompleteFailed` | Some libraries failed |
| `NoVersionHistoryDetected` | Site has no version history to manage |

### 3. Delete Excess Versions (BatchDelete)

The [`New-SPOSiteFileVersionBatchDeleteJob`](https://learn.microsoft.com/powershell/module/sharepoint-online/new-spositefileversionbatchdeletejob) cmdlet removes versions that exceed the configured limits. This is Microsoft's official batch deletion API — deleted versions go to the site recycle bin (93-day recovery window). Only works after a policy has been set.

```powershell
# Delete versions exceeding the limit
New-SPOSiteFileVersionBatchDeleteJob `
    -Identity "https://contoso.sharepoint.com/sites/Finance" `
    -MajorVersionLimit 20 `
    -MajorWithMinorVersionsLimit 20
```

### 4. Monitor Deletion Progress

```powershell
# Check batch deletion progress
$deleteProgress = Get-SPOSiteFileVersionBatchDeleteJobProgress `
    -Identity "https://contoso.sharepoint.com/sites/Finance"

$deleteProgress | Select-Object Status, StorageReleasedInBytes, VersionsDeleted
```

**Key output fields:**

| Field | Description |
|-------|-------------|
| `Status` | Job state (InProgress, CompleteSuccess, etc.) |
| `StorageReleasedInBytes` | Bytes freed by version deletion |
| `VersionsDeleted` | Number of versions removed |
| `LastProcessTimeInUtc` | When the job last made progress |

---

## Automation Patterns

### Pattern 1: Sequential Processing (Simple)

Process one site at a time. Simple but slow for large tenants.

```powershell
Connect-SPOService -Url "https://contoso-admin.sharepoint.com"

$sites = Get-SPOSite -Limit All | Where-Object { $_.Template -notlike "SPSPERS*" }

foreach ($site in $sites) {
    Write-Host "Processing: $($site.Url)"
    
    # Phase 1: Sync policy
    New-SPOSiteManageVersionPolicyJob -Identity $site.Url `
        -MajorVersionLimit 20 -MajorWithMinorVersionsLimit 20 -SyncListPolicy
    
    # Wait for completion
    do {
        Start-Sleep -Seconds 30
        $status = Get-SPOSiteManageVersionPolicyJobProgress -Identity $site.Url
    } while ($status.Status -eq "InProgress")
    
    # Phase 2: Delete excess versions
    if ($status.Status -eq "CompleteSuccess") {
        New-SPOSiteFileVersionBatchDeleteJob -Identity $site.Url `
            -MajorVersionLimit 20 -MajorWithMinorVersionsLimit 20
        
        do {
            Start-Sleep -Seconds 30
            $deleteStatus = Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity $site.Url
        } while ($deleteStatus.Status -eq "InProgress")
        
        Write-Host "  Freed: $([math]::Round($deleteStatus.StorageReleasedInBytes / 1GB, 2)) GB"
    }
}
```

### Pattern 2: Parallel Processing (Production)

Process multiple sites simultaneously using job orchestration:

```powershell
# Using SPO Version Management module (handles parallel orchestration)
Import-Module .\SPOVersionManagement.psm1

# This is what Start-SPOVersionManagement.ps1 does internally:
# 1. Gets all sites
# 2. Applies filters (include/exclude)
# 3. Runs up to 10 parallel SyncListPolicy jobs
# 4. Monitors all jobs until completion
# 5. Runs up to 10 parallel BatchDelete jobs
# 6. Tracks storage released per site
# 7. Updates Dashboard in real-time

.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -MajorVersionLimit 20 `
    -MaxConcurrentJobs 10 `
    -OpenDashboard
```

### Pattern 3: Targeted Cleanup (Specific Sites)

Process only specific sites from a CSV:

```powershell
# IncludeSites.csv format:
# SiteUrl
# https://contoso.sharepoint.com/sites/Finance
# https://contoso.sharepoint.com/sites/Marketing

.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -InputSiteListCSV ".\IncludeSites.csv" `
    -MajorVersionLimit 20
```

### Pattern 4: Exclude Protected Sites

```powershell
# ExcludeSites.csv format:
# SiteURL,SiteName,Reason
# https://contoso.sharepoint.com/sites/Legal,Legal,Under legal hold
# https://contoso.sharepoint.com/sites/CEO,CEO Office,Executive protection

.\Start-SPOVersionManagement.ps1 `
    -AdminUrl "https://contoso-admin.sharepoint.com" `
    -InputExclusionSiteListCSV ".\ExcludeSites.csv" `
    -MajorVersionLimit 20
```

---

## Handling Retention Policies

Sites under Microsoft Purview retention policies cannot have versions deleted. The cmdlets will succeed but delete nothing.

### Detect Blocking Policies

```powershell
# Using PnP PowerShell to check compliance tags
Connect-PnPOnline -Url "https://contoso.sharepoint.com/sites/Finance" -Interactive
$lists = Get-PnPList | Where-Object { $_.HasComplianceTag }
```

### SPO Version Management Retention Handler

The tool includes a Retention Policy Manager that automates:

1. **Detection** — Identifies sites with active preservation hold policies
2. **Suspension** — Temporarily suspends policies (using Security & Compliance PowerShell)
3. **Cleanup** — Runs version deletion while policies are suspended
4. **Resume** — Re-enables policies immediately after cleanup
5. **Audit** — Logs every policy change with timestamps

```powershell
# Retention policy management is automatic when enabled
# Configure in AppPaths.json:
# "RetentionPolicyHandling": "auto"  ← suspend/resume automatically
# "RetentionPolicyHandling": "skip"  ← skip sites with policies
# "RetentionPolicyHandling": "ask"   ← prompt for each site
```

---

## Monitoring and Reporting

### Real-Time Dashboard

```powershell
# Open the HTML Dashboard while jobs are running
.\Start-Dashboard.ps1

# Dashboard shows:
# - Active jobs with progress percentage
# - Queue of pending sites
# - Completed sites with storage freed
# - Tenant-wide storage trends
# - Cost savings calculations
```

### Export to Power BI

```powershell
# Execution history is automatically saved to CSV
# Located at: Logs\ExecutionHistory.csv
# Columns: SessionId, SiteUrl, Phase, Status, StorageReleased, Duration, Timestamp

# Import into Power BI for:
# - Storage freed over time
# - Cost savings trending
# - Site-level detail
# - Execution success rates
```

### Scheduled Automation

```powershell
# Windows Task Scheduler example (runs monthly)
# Action: powershell.exe
# Arguments:
-NoProfile -ExecutionPolicy Bypass -File "C:\SPOVersionManagement\Start-SPOVersionManagement.ps1" -AdminUrl "https://contoso-admin.sharepoint.com" -MajorVersionLimit 20 -MaxConcurrentJobs 10 -SkipGraphConnection
```

---

## Error Handling and Troubleshooting

### Common Issues

| Error | Cause | Solution |
|-------|-------|----------|
| `Access denied` | Insufficient permissions | Ensure SharePoint Admin role |
| `Job stuck in InProgress` | SPO backend processing delay | Wait and re-check (can take hours for large sites) |
| `NoVersionHistoryDetected` | Site has no versioned content | Normal — skip this site |
| `Throttling (429)` | Too many API calls | Reduce concurrent jobs, increase polling interval |

### Manual Recovery

```powershell
# Check individual site status
Get-SPOSiteManageVersionPolicyJobProgress -Identity "https://contoso.sharepoint.com/sites/Finance"
Get-SPOSiteFileVersionBatchDeleteJobProgress -Identity "https://contoso.sharepoint.com/sites/Finance"

# Resume interrupted execution
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Resume
```

---

## Best Practices

1. **Always run SyncOnly first** — Assess before deleting
2. **Start with a pilot group** — 10–20 non-critical sites
3. **Exclude regulated sites** — Legal holds, compliance archives
4. **Use parallel processing** — 10 concurrent jobs balances speed and throttling
5. **Monitor the Dashboard** — Real-time visibility prevents surprises
6. **Schedule recurring runs** — Monthly cleanup prevents re-accumulation
7. **Export to Power BI** — Track savings for stakeholder reporting
8. **Keep the tool updated** — New releases add API compatibility and features

---

<div class="cta-box">
    <h3>Skip the Scripting — Get Production-Ready Orchestration</h3>
    <p>SPO Version Management wraps these exact cmdlets with parallel execution, real-time monitoring, retention handling, and automatic resume. Free, open-source, and uses only the official APIs documented above.</p>
    <a href="https://github.com/ivanoliv/SPOVersionManagement/releases">Download Free — Assessment Mode Available</a>
</div>

## Related Guides

- [Complete Guide to SharePoint Version Management]({{ '/guides/sharepoint-version-management/' | relative_url }})
- [How to Reduce SharePoint Storage Costs]({{ '/guides/reduce-sharepoint-storage-costs/' | relative_url }})
- [SharePoint Governance Best Practices]({{ '/guides/sharepoint-governance-best-practices/' | relative_url }})
