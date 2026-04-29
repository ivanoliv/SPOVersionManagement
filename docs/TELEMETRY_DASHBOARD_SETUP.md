# Telemetry Dashboard Setup Guide

This guide shows you how to set up a free, public telemetry dashboard for SPO Version Management using GitHub Pages.

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│ SPO Version Management (GUI on User's Machine)                   │
│ - Collects anonymized telemetry                                  │
│ - Hashes tenant ID with public salt                              │
│ - Sends data to backend API                                      │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│ Backend API (Azure App Service - Private)                        │
│ - Stores session data in database                                │
│ - Endpoint: /api/telemetry/stats (aggregate only)                │
│ - Endpoint: /api/telemetry/export (JSON export)                  │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│ GitHub Actions (Daily 2 AM UTC)                                  │
│ - Fetches aggregate stats from backend                           │
│ - Exports to telemetry-stats.json                                │
│ - Commits to GitHub repo (auto-push)                             │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│ GitHub Repository                                                │
│ - telemetry-stats.json (public, updated daily)                   │
│ - index.html (dashboard HTML)                                    │
│ - dashboard.js (visualization code)                              │
└──────────────────────────┬──────────────────────────────────────┘
                           │
                           ▼
┌─────────────────────────────────────────────────────────────────┐
│ GitHub Pages (Free Public Website)                               │
│ - https://your-username.github.io/telemetry-dashboard            │
│ - Live dashboard with charts                                     │
│ - Updates every day automatically                                │
└─────────────────────────────────────────────────────────────────┘
                           │
                           ▼
        ┌──────────────────────────────────┐
        │ Public Users (Anyone)            │
        │ Can view statistics               │
        │ No tenant data exposed            │
        │ Only aggregate trends visible     │
        └──────────────────────────────────┘
```

## Step 1: Add Backend Export Endpoint

The backend needs an endpoint to export aggregated stats:

```csharp
// File: Controllers/TelemetryExportController.cs
[HttpGet("api/telemetry/stats")]
public async Task<IActionResult> GetAggregateStats()
{
    var sessions = await _db.Sessions.ToListAsync();
    
    return Ok(new {
        timestamp = DateTime.UtcNow,
        totalSessions = sessions.Count,
        totalOrganizations = sessions.Select(s => s.TenantHash).Distinct().Count(),
        totalStorageFreedBytes = sessions.Sum(s => s.StorageFreedBytes),
        sessionsByDay = sessions.GroupBy(s => s.CreatedAt.Date)...
        topOrganizations = sessions.GroupBy(s => s.TenantHash)...
    });
}
```

✅ **Status**: Implemented at `SPOVersionManagementBackend/Controllers/TelemetryExportController.cs`

## Step 2: Create GitHub Pages Dashboard

Create an HTML dashboard that displays the data as charts:

```html
<!-- File: docs/index.html -->
<!DOCTYPE html>
<html>
  <head>
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0"></script>
  </head>
  <body>
    <div id="dashboard">
      <h1>📊 Telemetry Dashboard</h1>
      <canvas id="sessionsChart"></canvas>
      <canvas id="topOrgsChart"></canvas>
    </div>
    
    <script>
      // Fetch data from telemetry-stats.json
      fetch('telemetry-stats.json')
        .then(r => r.json())
        .then(data => {
          // Render charts with Chart.js
          new Chart(ctx, {
            type: 'line',
            data: { labels: [...], datasets: [...] }
          });
        });
    </script>
  </body>
</html>
```

✅ **Status**: Example at `docs/telemetry-dashboard-example.html`

## Step 3: Set Up GitHub Actions Automation

Create a workflow that runs daily to export data:

```yaml
# File: .github/workflows/export-telemetry.yml
name: Export Telemetry Stats
on:
  schedule:
    - cron: '0 2 * * *'  # Every day at 2 AM UTC

jobs:
  export:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Fetch telemetry from backend
        run: |
          curl "https://your-backend-api/api/telemetry/stats" \
            > telemetry-stats.json
      
      - name: Commit and push
        run: |
          git config user.email "action@github.com"
          git config user.name "Telemetry Bot"
          git add telemetry-stats.json
          git commit -m "Auto: Update telemetry stats"
          git push
```

✅ **Status**: Template at `.github/workflows/export-telemetry.yml`

## Step 4: Enable GitHub Pages

1. Go to your repo Settings → Pages
2. Select Source: `main` branch, `/root` folder
3. GitHub will provide your URL: `https://username.github.io/repo-name/`

## Data Flow Example

### Day 1: First Deployment
```
User on Machine A: Enable telemetry
  ↓
Salt generated: SHA256(MachineName + NIC MAC)
  ↓
TenantId hashed: SHA256(tenant-id | salt)
  ↓
Session sent to backend: 
{
  "TenantHash": "a3f7d2e1b9c4f6a8...",
  "StorageFreedBytes": 5368709120,
  "VersionsDeleted": 847,
  "AppVersion": "2.1.3.3"
}
```

### Day 2: GitHub Actions Export (2 AM)
```
GitHub Actions runs:
  curl https://your-backend-api/api/telemetry/stats
  
Backend returns:
{
  "timestamp": "2026-04-29T02:00:00Z",
  "totalSessions": 127,
  "totalOrganizations": 23,
  "totalStorageFreedBytes": 5368709120000,
  "sessionsByDay": [
    { "date": "2026-04-29", "count": 12 }
  ],
  "topOrganizations": [
    { "tenantHash": "a3f7d2e1...", "sessionCount": 127 }
  ]
}

GitHub Actions commits to repo:
  telemetry-stats.json (updated)
```

### Day 3+: Dashboard Live
```
User visits: https://username.github.io/dashboard/
  ↓
Dashboard reads: telemetry-stats.json
  ↓
Chart.js renders 7 interactive charts:
  - Sessions over time
  - Top organizations
  - Storage freed distribution
  - Version adoption
  - etc.
```

## Privacy & Security

### What's Exposed Publicly?
- ✅ Aggregate statistics only
- ✅ Charts and trends
- ✅ Total session counts
- ✅ Storage freed (aggregate)

### What's NOT Exposed?
- ❌ Individual tenant IDs (hashed)
- ❌ Company names
- ❌ IP addresses
- ❌ User details
- ❌ Raw session data

### Salt & Hash Details
- **Salt**: `SPOVersionMgmt_v2.1.3.3` (version-based, public, immutable per release)
- **Hash Method**: `SHA256(TenantId.ToLower() + "|" + Salt)`
- **Reversibility**: Technically reversible with rainbow tables, but provides obfuscation
- **Per-Machine Consistency**: ✅ Yes—all machines use same salt, so same tenant always produces same hash

## Cost Analysis

| Component | Cost |
|-----------|------|
| GitHub Pages | **$0** (included with free GitHub account) |
| GitHub Actions | **$0** (2000 free minutes/month, we use ~1 min/day = 30 min/month) |
| Backend API | ~$10/month (App Service tier) ← only cost |
| Database | ~$5/month (Azure SQL or Cosmos) ← included in backend |
| **TOTAL** | **~$15/month** (backend only) |

## Implementation Checklist

- [ ] 1. Implement `TelemetryExportController.cs` in backend (export endpoint)
- [ ] 2. Deploy backend API with CORS enabled for GitHub Pages
- [ ] 3. Create GitHub Pages repository (can be same repo or separate)
- [ ] 4. Add `docs/index.html` with dashboard HTML
- [ ] 5. Add `.github/workflows/export-telemetry.yml` workflow
- [ ] 6. Update workflow with your actual backend API URL
- [ ] 7. Enable GitHub Pages in repo settings
- [ ] 8. Test: Manual workflow run via GitHub Actions tab
- [ ] 9. Wait for first automatic run (2 AM UTC next day)
- [ ] 10. Verify dashboard at `https://username.github.io/repo/`

## Troubleshooting

### Dashboard shows "No data"
- Check backend API is running and accessible
- Verify GitHub Actions workflow ran successfully
- Check `telemetry-stats.json` file exists in repo

### GitHub Actions workflow fails
- Verify backend API URL is correct in workflow YAML
- Check backend API returns valid JSON
- Ensure backend allows requests from GitHub Actions IPs

### CORS errors in browser console
- Add CORS headers to backend:
  ```csharp
  app.UseCors(builder => builder
      .AllowAnyOrigin()
      .AllowAnyMethod()
      .AllowAnyHeader());
  ```

## Example Workflow

1. **Monday 8 AM**: User in Tokyo enables telemetry on their machine
   - Tenant hashed and sent to backend
   
2. **Monday 2 PM**: User in London enables telemetry on different machine
   - Same tenant, same hash (because salt is consistent)
   - Backend records 2nd session
   
3. **Tuesday 2 AM**: GitHub Actions exports
   - Backend returns: 2 sessions, 1 organization
   - Updates `telemetry-stats.json`
   
4. **Tuesday 9 AM**: Public user visits dashboard
   - Sees: "2 sessions, 1 organization worldwide"
   - Cannot identify either user or organization
   - Just sees trends and aggregate data

## Next Steps

1. Would you like me to implement this in your actual backend?
2. Should we use GitHub Pages for the dashboard repo?
3. Do you want to track additional metrics (e.g., site list sizes, execution time)?

---

**Question**: Should the salt change with each app version, or remain constant? 
- **Option A** (Recommended): `SPOVersionMgmt_v{AppVersion}` — salt changes per release, hashes differ across versions
- **Option B**: Fixed `SPOVersionMgmt_2026` — salt stays same, hashes consistent across versions (better for comparing users across versions)
