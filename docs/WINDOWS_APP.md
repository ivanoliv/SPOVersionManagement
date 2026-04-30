# SPO Version Management — Windows GUI Application

## Overview

The Windows GUI application provides a graphical interface for managing SharePoint Online version policies, monitoring execution progress, configuring settings, and viewing storage analytics — without requiring direct PowerShell interaction.

It is built as a **.NET Framework 4.8 WinForms** application with a modern flat UI theme.

---

## ⚠️ Disclaimer

This application is provided "AS IS" without warranty. It is not affiliated with or supported by Microsoft Corporation. See the main [README](../README.md#-disclaimer) for full terms.

---

## System Requirements

| Requirement | Version |
|-------------|---------|
| OS | Windows 10/11 or Windows Server 2016+ |
| .NET Framework | 4.8 (pre-installed on Windows 10 1903+) |
| PowerShell | 5.1 (ships with Windows) |
| Disk Space | ~20 MB |
| Network | HTTPS outbound to SharePoint Online |

---

## Installation

### Option A: Use pre-built release

1. Download the latest release from [Releases](https://github.com/ivanoliv/SPOVersionManagement/releases)
2. Extract the ZIP to your desired installation folder
3. Run `SPOVersionManagement.exe`

### Option B: Build from source

See [Building from Source](#building-from-source) below.

---

## Building from Source

### Prerequisites

- [.NET SDK 6.0+](https://dotnet.microsoft.com/download) (builds .NET Framework 4.8 projects)
- Or [Visual Studio 2022](https://visualstudio.microsoft.com/) with ".NET desktop development" workload
- Windows only (WinForms dependency)

### Build Commands

```powershell
# Clone the repository
git clone https://github.com/ivanoliv/SPOVersionManagement.git
cd SPOVersionManagement

# Build in Release mode
dotnet build src\SPOVersionManagement.sln -c Release

# Output binary location:
# src\SPOVersionManagement\bin\Release\SPOVersionManagement.exe
```

### Build from Visual Studio

1. Open `src\SPOVersionManagement.sln`
2. Set configuration to **Release**
3. Build → Build Solution (Ctrl+Shift+B)
4. Output: `src\SPOVersionManagement\bin\Release\SPOVersionManagement.exe`

### Project Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `Newtonsoft.Json` | 13.0.3 | JSON serialization/deserialization |
| `Microsoft.PowerShell.5.ReferenceAssemblies` | 1.1.0 | PowerShell hosting in-process |

---

## Application Architecture

```
SPOVersionManagement.exe
├── Forms/
│   └── MainForm.cs              # Main window with sidebar navigation
├── Controls/                    # UI Panels (one per sidebar tab)
│   ├── HomePanel.cs             # Home — tenant status, storage overview
│   ├── ExecutionPanel.cs        # Execution — run/monitor version jobs
│   ├── SiteManagementPanel.cs   # Sites — browse, filter, manage sites
│   ├── HistoryPanel.cs          # History — execution logs and CSV viewer
│   ├── RetentionPolicyPanel.cs  # Retention — manage compliance policies
│   ├── FileArchivePanel.cs      # File Archive — search by extension
│   ├── FileArchiveQueuePanel.cs # Archive Queue — batch archive management
│   ├── DataSyncPanel.cs         # Data Sync — refresh tenant data
│   ├── HttpServerPanel.cs       # HTTP Server — serve dashboard locally
│   ├── ConfigurationPanel.cs    # Configuration — app credentials & settings
│   ├── UpdatePanel.cs           # Update — check for new versions
│   └── PreReqsPanel.cs         # Prerequisites — validate environment
├── Services/
│   ├── ConfigurationService.cs  # Load/save AppPaths.json + DashboardConfig
│   ├── PowerShellHostService.cs # Host PowerShell 5.1 in-process
│   ├── ExecutionHistoryService.cs # Read/write execution CSVs and session data
│   ├── SiteDataService.cs       # Manage AllSites, ArchiveQueue JSON databases
│   ├── DashboardHttpServerService.cs # Local HTTP server for Dashboard
│   ├── TelemetryService.cs      # Anonymous usage telemetry (opt-in)
│   ├── GitHubUpdateService.cs   # Check for new releases on GitHub
│   └── UpdateInstallerService.cs # Download and apply updates
├── Models/                      # Data models (POCO classes)
├── Theme/                       # AppTheme, FlatButton, GlassPanel
└── Properties/
    └── AssemblyInfo.cs          # Assembly metadata
```

---

## Application Panels

### Home
Displays tenant storage overview: quota, used, available, extra cost estimate. Shows global community savings counter (from anonymous telemetry). Quick-launch buttons for common operations.

### Execution
Run version management jobs directly from the GUI. Configure major version limits, select sites, launch execution, and monitor real-time progress with console output.

### Site Management
Browse all SharePoint Online sites with filtering, sorting, and storage details. View site execution history. Queue sites for processing or exclusion.

### History
View and export execution history (CSV). Filter by date, status, or site. Drill into individual executions.

### Retention Policy
Browse, suspend, and resume Microsoft Purview retention policies. Required when retention policies block version deletion. Automatically managed during execution.

### File Archive
Search for files by extension category (Video, Audio, CAD, etc.) across SharePoint using Graph Search API. Useful for identifying large file types consuming storage.

### Data Sync
Manually refresh tenant data: site list, storage report, tenant storage status. Equivalent to running `Export-AllSPOSites.ps1`.

### HTTP Server
Start a local HTTP server to host the interactive Dashboard in your browser. Serves files from `web/` (HTML/JS), `config/` (JSON data), and `Logs/` (CSVs).

### Configuration
Configure Entra ID app credentials (TenantId, ClientId, CertificateThumbprint) for both SPO and Purview apps. Manage telemetry settings and GitHub repo URL.

### Update
Check for new releases on GitHub. View file statistics (database sizes, row counts). Download and install updates automatically.

### Prerequisites
Validates the environment: required PowerShell modules, .NET version, network connectivity, certificate availability.

---

## Configuration

The application reads configuration from `config\AppPaths.json`. Credentials can be configured:
- Directly in JSON file
- Via the **Configuration** panel in the app
- Via the **Dashboard** Settings tab in the browser

See [ENTRA_ID_APP_SETUP.md](../ENTRA_ID_APP_SETUP.md) for full Entra ID app registration guide.

---

## Data Storage

| Directory | Contents |
|-----------|----------|
| `config\` | All JSON databases and configuration |
| `web\` | Dashboard HTML and localization JS |
| `Logs\` | Execution CSV files and FileArchive results |

---

## Running the Application

```powershell
# From the installation folder:
.\app\SPOVersionManagement.exe

# Or run from source build output:
.\src\SPOVersionManagement\bin\Release\SPOVersionManagement.exe
```

The application searches for `config\AppPaths.json` relative to the executable path to determine the project root.

---

## Troubleshooting

### Application won't start
- Verify .NET Framework 4.8 is installed: `(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release -ge 528040`
- Run as Administrator if file access errors occur

### PowerShell execution errors
- The app hosts PowerShell 5.1 in-process
- Ensure execution policy allows script execution: `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`
- Required modules must be installed for the **Windows PowerShell** (5.1) environment

### Cannot connect to SharePoint
- Verify credentials in Configuration panel
- Ensure certificate is in `Cert:\CurrentUser\My`
- Check network connectivity to `*.sharepoint.com`

### Dashboard not loading in HTTP Server panel
- Check that `web\Dashboard.html` exists
- Default port is 8080 — ensure it's not in use
- Try a different port in the HTTP Server panel

---

## Contributing

This is open-source software. Contributions are welcome:

1. Fork the repository
2. Create a feature branch
3. Make changes and test locally
4. Submit a Pull Request

### Development Setup

```powershell
git clone https://github.com/ivanoliv/SPOVersionManagement.git
cd SPOVersionManagement\src
dotnet restore SPOVersionManagement.sln
dotnet build SPOVersionManagement.sln
```

### Code Style
- C# 7.3 (constrained by .NET Framework 4.8)
- No nullable reference types
- Newtonsoft.Json for serialization
- Event-driven UI (no async/await in UI event handlers except fire-and-forget)
