using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using System.Windows.Forms;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class PreReqsPanel : UserControl
    {
        private ConfigurationService _config;
        private PowerShellHostService _psHost;

        private DataGridView _grid;
        private TextBox _txtDebug;
        private FlatButton _btnRefresh;
        private FlatButton _btnJwtDebug;
        private Label _lblSummary;
        private Label _lblInstallInfo;

        public event EventHandler<string> StatusMessage;

        public PreReqsPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(20, 16, 20, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            _config = config;
            _psHost = psHost;
            BuildLayout();
            _ = RunChecksAsync();
        }

        public void RefreshChecks()
        {
            _ = RunChecksAsync();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        private void BuildLayout()
        {
            Controls.Clear();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            var top = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.Transparent };
            root.Controls.Add(top, 0, 0);

            top.Controls.Add(new Label
            {
                Text = "Pre reqs",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 0)
            });

            top.Controls.Add(new Label
            {
                Text = "Validate modules, PowerShell connectivity, app configuration, and token claims in debug mode.",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 34)
            });

            var action = new GlassPanel { Dock = DockStyle.Top, Height = 52, AccentLeft = AppTheme.AccentGold };
            root.Controls.Add(action, 0, 1);

            _btnRefresh = new FlatButton { Text = "Run Checks", Size = new Size(108, 28), Location = new Point(10, 12) };
            _btnRefresh.SetAccentColor(AppTheme.AccentGreen);
            _btnRefresh.Click += async (s, e) => await RunChecksAsync();
            action.Controls.Add(_btnRefresh);

            _btnJwtDebug = new FlatButton { Text = "JWT Debug", Size = new Size(98, 28), Location = new Point(126, 12) };
            _btnJwtDebug.SetGhostStyle();
            _btnJwtDebug.Click += async (s, e) => await RunJwtDebugAsync();
            action.Controls.Add(_btnJwtDebug);

            _lblSummary = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(236, 7),
                Size = new Size(action.Width - 236 - 12, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            action.Controls.Add(_lblSummary);

            _lblInstallInfo = new Label
            {
                Text = "PnP.PowerShell requires PS 7.4+, others work on PS 5.1. Click Install to add.",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(236, 27),
                Size = new Size(action.Width - 236 - 12, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            action.Controls.Add(_lblInstallInfo);

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 300,
                Panel1MinSize = 200,
                Panel2MinSize = 120,
                SplitterWidth = 6,
                BackColor = Color.Transparent
            };
            root.Controls.Add(split, 0, 2);

            var gridCard = new GlassPanel { Dock = DockStyle.Fill, AccentLeft = AppTheme.AccentCyan, Padding = new Padding(8) };
            split.Panel1.Controls.Add(gridCard);

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true
            };
            AppTheme.StyleDataGrid(_grid);
            _grid.Columns.Add("Module", "MODULE");
            _grid.Columns.Add("Status", "STATUS");
            _grid.Columns.Add("Version", "VERSION");
            _grid.Columns.Add("Required", "REQUIRED FOR");
            _grid.Columns.Add("Action", "ACTION");
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _grid.Columns["Module"].Width = 200;
            _grid.Columns["Module"].MinimumWidth = 200;
            _grid.Columns["Status"].Width = 70;
            _grid.Columns["Status"].MinimumWidth = 70;
            _grid.Columns["Version"].Width = 120;
            _grid.Columns["Version"].MinimumWidth = 120;
            _grid.Columns["Required"].Width = 200;
            _grid.Columns["Required"].MinimumWidth = 200;
            _grid.Columns["Required"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns["Action"].Width = 80;
            _grid.Columns["Action"].MinimumWidth = 80;
            _grid.CellClick += Grid_CellClick;
            gridCard.Controls.Add(_grid);

            var debugCard = new GlassPanel { Dock = DockStyle.Fill, AccentLeft = AppTheme.AccentPurple, Padding = new Padding(8) };
            split.Panel2.Controls.Add(debugCard);

            var debugHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 20,
                BackColor = Color.Transparent
            };
            debugCard.Controls.Add(debugHeader);

            debugHeader.Controls.Add(new Label
            {
                Text = "DEBUG OUTPUT",
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = AppTheme.AccentPurple,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 2)
            });

            _txtDebug = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextSecondary,
                Font = AppTheme.FontMono,
                WordWrap = false
            };
            debugCard.Controls.Add(_txtDebug);
        }

        private async Task RunChecksAsync()
        {
            if (_grid == null || _grid.IsDisposed)
                return;

            _btnRefresh.Enabled = false;
            _btnRefresh.Text = "Checking\u2026";
            _lblSummary.Text = "\u23F3  Analyzing prerequisites\u2026";
            _lblSummary.ForeColor = AppTheme.AccentGold;
            _lblInstallInfo.Visible = false;
            _grid.Rows.Clear();
            _txtDebug.Clear();
            int pass = 0;
            int fail = 0;

            try
            {
                AppendDebug("Running prerequisite checks...\n");

                // Check config
                AddCheckRow("Configuration write access", _config.HasWritePermission, 
                    _config.PermissionMessage, "Config");
                if (_config.HasWritePermission) pass++; else fail++;

                bool hasAdminUrl = !string.IsNullOrWhiteSpace(_config.AppConfig.AdminUrl);
                AddCheckRow("Admin URL configured", hasAdminUrl, 
                    hasAdminUrl ? _config.AppConfig.AdminUrl : "Admin URL is empty in config", "Config");
                if (hasAdminUrl) pass++; else fail++;

                bool hasEntra = _config.AppConfig.EntraIdApp != null
                    && !string.IsNullOrWhiteSpace(_config.AppConfig.EntraIdApp.ClientId)
                    && !string.IsNullOrWhiteSpace(_config.AppConfig.EntraIdApp.TenantId)
                    && !string.IsNullOrWhiteSpace(_config.AppConfig.EntraIdApp.CertificateThumbprint);
                AddCheckRow("Entra App config", hasEntra, 
                    hasEntra ? "ClientId/TenantId/CertificateThumbprint found" : "Missing EntraIdApp fields", 
                    "Auth");
                if (hasEntra) pass++; else fail++;

                bool hasPnpApp = _config.AppConfig.PnPApp != null
                    && !string.IsNullOrWhiteSpace(_config.AppConfig.PnPApp.ClientId);
                AddCheckRow("PnP App config (File Archive)", hasPnpApp,
                    hasPnpApp ? "ClientId found. Requires Sites.Read.All + Files.ReadWrite.All"
                              : "Missing PnPApp.ClientId \u2014 needed for File Archive Explorer",
                    "Auth (PnP)");
                if (hasPnpApp) pass++; else fail++;

                // M365 Archive — file-level archiving is disabled (Graph beta API not GA)
                AddCheckRow("File Archive (execution)", false,
                    "Disabled \u2014 Graph beta /archive API returns MethodNotAllowed on most tenants. File-level archiving is not yet GA. Site-level archiving works via SharePoint Admin Center.",
                    "File Archive Queue");
                fail++;

                // Check modules
                AppendDebug("\nChecking PowerShell versions and modules...\n");
                var result = await _psHost.CheckPrerequisitesAsync();
                if (result != null)
                {
                    foreach (PSObject row in result)
                    {
                        string module = GetPrereqValue(row, "Module", "Name", "ModuleName") ?? "Module";
                        bool installed = ParseBool(GetPrereqValue(row, "Installed", "IsInstalled", "Available"));
                        string version = GetPrereqValue(row, "Version", "DetectedVersion", "Details") ?? "Unknown";
                        string required = GetPrereqValue(row, "Required", "RequiredFor", "Purpose") ?? "";
                        
                        AddCheckRow(module, installed, version, required);
                        AppendDebug($"{module}: {(installed ? "OK" : "MISSING")}");
                        
                        if (installed) pass++; else fail++;
                    }
                }

                _lblSummary.Text = $"\u2713 {pass} passed  |  \u2717 {fail} failed";
                _lblSummary.ForeColor = fail == 0 ? AppTheme.AccentGreen : AppTheme.AccentGold;
                _lblInstallInfo.Visible = true;
                StatusMessage?.Invoke(this, _lblSummary.Text);
                AppendDebug($"\nCheck complete: {pass} passed, {fail} failed");
            }
            catch (Exception ex)
            {
                AddCheckRow("Prerequisite execution", false, ex.Message, "Error");
                _lblSummary.Text = "\u2717  Checks failed with errors.";
                _lblSummary.ForeColor = AppTheme.AccentRed;
                _lblInstallInfo.Visible = true;
                AppendDebug("[ERROR] " + ex.Message);
            }
            finally
            {
                _btnRefresh.Enabled = true;
                _btnRefresh.Text = "Run Checks";
            }
        }

        private async Task RunJwtDebugAsync()
        {
            _btnJwtDebug.Enabled = false;
            try
            {
                string adminUrl = _config?.AppConfig?.AdminUrl?.Trim();
                if (string.IsNullOrEmpty(adminUrl))
                {
                    AppendDebug("\n[ERROR] Admin URL not configured. Set it in Config first.");
                    return;
                }

                AppendDebug("\nRunning JWT debug (connecting to SPO and inspecting token) ...\n");

                string script = $@"
Import-Module Microsoft.Online.SharePoint.PowerShell -ErrorAction Stop
try {{
    Connect-SPOService -Url '{adminUrl}' -ErrorAction Stop
}} catch {{
    Write-Output ('Connection failed: ' + $_.Exception.Message)
    return
}}
Write-Output 'Connected to SPO successfully.'
Write-Output ''
# Get tenant info as proof of connection
try {{
    $tenant = Get-SPOTenant -ErrorAction Stop
    Write-Output ('Tenant: ' + $tenant.RootSiteUrl)
    Write-Output ('StorageQuota: ' + $tenant.StorageQuota + ' MB')
    Write-Output ('StorageQuotaAllocated: ' + $tenant.StorageQuotaAllocated + ' MB')
    Write-Output ('ResourceQuota: ' + $tenant.ResourceQuota)
    Write-Output ('SharingCapability: ' + $tenant.SharingCapability)
    Write-Output ('ConditionalAccessPolicy: ' + $tenant.ConditionalAccessPolicy)
}} catch {{
    Write-Output ('Get-SPOTenant failed: ' + $_.Exception.Message)
}}
";

                var output = await _psHost.RunScriptAsync(script);
                if (output != null)
                {
                    foreach (var line in output)
                        AppendDebug(line?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                AppendDebug("[ERROR] " + ex.Message);
            }
            finally
            {
                _btnJwtDebug.Enabled = true;
            }
        }

        private void AddCheckRow(string name, bool ok, string details, string requiredFor, string actionOverride = null)
        {
            string action = actionOverride ?? (!ok && ShouldAllowInstall(name) ? "Install" : "");
            _grid.Rows.Add(name, ok ? "OK" : "FAIL", details ?? string.Empty, requiredFor, action);
            var row = _grid.Rows[_grid.Rows.Count - 1];
            row.Cells["Status"].Style.ForeColor = ok ? AppTheme.AccentGreen : AppTheme.AccentRed;
            row.Tag = name; // Store module name for install action
            
            if (!string.IsNullOrEmpty(action))
            {
                // Style the action cell as a button
                row.Cells["Action"].Style.BackColor = AppTheme.AccentGold;
                row.Cells["Action"].Style.ForeColor = AppTheme.BgDark;
                row.Cells["Action"].Style.Font = new Font(AppTheme.FontBody.FontFamily, 9f, FontStyle.Bold);
                row.Cells["Action"].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
        }

        private bool ShouldAllowInstall(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return false;

            // Do not show install for PS runtime rows; only for module rows.
            if (moduleName.StartsWith("PowerShell ", StringComparison.OrdinalIgnoreCase))
                return false;

            return moduleName.Contains("SPO Mgmt Shell")
                || moduleName.Contains("Online.SharePoint")
                || moduleName.Contains("PnP.PowerShell")
                || moduleName.Contains("PnP Nightly")
                || moduleName.Contains("Nightly (Archive)")
                || moduleName.Contains("Graph");
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
                return;

            if (e.ColumnIndex != _grid.Columns["Action"].Index)
                return;

            var row = _grid.Rows[e.RowIndex];
            string moduleName = row.Tag?.ToString() ?? "";
            string actionText = row.Cells["Action"].Value?.ToString() ?? "";

            if (string.IsNullOrEmpty(moduleName) || string.IsNullOrEmpty(actionText))
                return;



            string statusText = row.Cells["Status"].Value?.ToString() ?? "";
            if (statusText == "OK")
                return;

            _ = InstallModuleAsync(moduleName);
        }

        private async Task InstallModuleAsync(string moduleName)
        {
            _btnRefresh.Enabled = false;
            try
            {
                AppendDebug($"\n[INFO] Installing {moduleName}...\n");

                // Determine which PS version and module name to use
                string psVersion = GetPSVersionForModule(moduleName);
                string actualModuleName = GetActualModuleName(moduleName);

                string installScript = GenerateInstallScript(actualModuleName, psVersion);

                var output = await _psHost.RunScriptAsync(installScript);
                if (output != null)
                {
                    foreach (var line in output)
                        AppendDebug(line?.ToString() ?? string.Empty);
                }

                AppendDebug("\n[INFO] Installation complete. Running checks again...\n");
                await RunChecksAsync();
            }
            catch (Exception ex)
            {
                AppendDebug($"\n[ERROR] Installation failed: {ex.Message}\n");
                MessageBox.Show(
                    $"Failed to install {moduleName}:\n\n{ex.Message}\n\nMake sure you have the appropriate PowerShell version available.",
                    "Installation Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }

        private string GetPSVersionForModule(string moduleName)
        {
            if (moduleName.Contains("PnP"))
                return "7";
            return "5.1";
        }

        private string GetActualModuleName(string moduleName)
        {
            if (moduleName.Contains("SPO Mgmt Shell") || moduleName.Contains("SPO Management") || moduleName.Contains("Online.SharePoint"))
                return "Microsoft.Online.SharePoint.PowerShell";
            if (moduleName.Contains("Nightly"))
                return "PnP.PowerShell.Nightly";
            if (moduleName.Contains("PnP"))
                return "PnP.PowerShell";
            if (moduleName.Contains("Graph") || moduleName.Contains("Microsoft.Graph"))
                return "Microsoft.Graph.Authentication";
            return moduleName;
        }

        private string GenerateInstallScript(string moduleName, string psVersion)
        {
            if (psVersion == "7")
            {
                // For PnP nightly, use -AllowPrerelease
                bool isNightly = moduleName.Contains("Nightly", StringComparison.OrdinalIgnoreCase);
                string installFlags = isNightly ? "-AllowPrerelease -Force -AllowClobber -Confirm:$false" : "-Force -AllowClobber -Confirm:$false";
                string displayName = isNightly ? "PnP.PowerShell (Nightly/Prerelease)" : moduleName;

                return $@"
# PowerShell 7.4+ required for PnP.PowerShell
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -lt 7 -or ($psVersion.Major -eq 7 -and $psVersion.Minor -lt 4))
{{
    Write-Output 'ERROR: PowerShell 7.4 or later required. You are running $psVersion'
    Write-Output 'Download from: https://github.com/PowerShell/PowerShell/releases'
    exit 1
}}

# Ensure NuGet provider
if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue))
{{
    Write-Output 'Installing NuGet provider...'
    Install-PackageProvider -Name NuGet -Force -Confirm:$false | Out-Null
}}

{(isNightly ? @"
# Remove stable PnP.PowerShell first to avoid conflicts
$stable = Get-Module -ListAvailable 'PnP.PowerShell' -ErrorAction SilentlyContinue | Where-Object {{ $_.PrivateData.PSData.Prerelease -eq $null -or $_.PrivateData.PSData.Prerelease -eq '' }}
if ($stable) {{
    Write-Output 'Removing stable PnP.PowerShell to install nightly...'
    Uninstall-Module -Name 'PnP.PowerShell' -AllVersions -Force -ErrorAction SilentlyContinue
}}
" : "")}
Write-Output 'Installing {displayName}...'
Install-Module -Name 'PnP.PowerShell' {installFlags} -ErrorAction Stop
$m = Get-Module -ListAvailable 'PnP.PowerShell' -ErrorAction SilentlyContinue | Select-Object -First 1
Write-Output ('{displayName} installed: ' + $m.Version.ToString())
Write-Output ''
Write-Output 'Verifying Set-PnPFileArchiveState cmdlet...'
$cmd = Get-Command 'Set-PnPFileArchiveState' -ErrorAction SilentlyContinue
if ($cmd) {{ Write-Output 'Set-PnPFileArchiveState: OK' }} else {{ Write-Output 'WARNING: Set-PnPFileArchiveState still not found. You may need the latest nightly build.' }}
";
            }
            else
            {
                return $@"
# PowerShell 5.1 compatible module installation
$psVersion = $PSVersionTable.PSVersion
if ($psVersion.Major -lt 5 -or ($psVersion.Major -eq 5 -and $psVersion.Minor -lt 1))
{{
    Write-Output 'ERROR: PowerShell 5.1 or later required. You are running $psVersion'
    exit 1
}}

# Ensure NuGet provider
if (-not (Get-PackageProvider -Name NuGet -ErrorAction SilentlyContinue))
{{
    Write-Output 'Installing NuGet provider...'
    Install-PackageProvider -Name NuGet -Force -Confirm:$false | Out-Null
}}

$module = Get-Module -ListAvailable '{moduleName}' -ErrorAction SilentlyContinue
if ($module)
{{
    Write-Output 'Module {moduleName} version $($module.Version) is already installed.'
}}
else
{{
    Write-Output 'Installing {moduleName}...'
    Install-Module -Name '{moduleName}' -Force -AllowClobber -Confirm:$false -ErrorAction Stop
    Write-Output '{moduleName} installed successfully.'
}}
";
            }
        }

        private static string GetPrereqValue(PSObject row, params string[] keys)
        {
            if (row == null)
                return null;

            foreach (var key in keys)
            {
                var prop = row.Properties[key];
                if (prop != null && prop.Value != null)
                    return prop.Value.ToString();
            }

            var dict = row.BaseObject as IDictionary<string, object>;
            if (dict != null)
            {
                foreach (var key in keys)
                {
                    object value;
                    if (dict.TryGetValue(key, out value) && value != null)
                        return value.ToString();
                }
            }

            return null;
        }

        private static bool ParseBool(string value)
        {
            bool parsed;
            if (bool.TryParse(value, out parsed))
                return parsed;

            int n;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return n != 0;

            return string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "ok", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "available", StringComparison.OrdinalIgnoreCase);
        }

        private void AppendDebug(string text)
        {
            if (_txtDebug == null || _txtDebug.IsDisposed)
                return;

            if (_txtDebug.InvokeRequired)
            {
                _txtDebug.Invoke(new Action(() => AppendDebug(text)));
                return;
            }

            _txtDebug.AppendText(text);
            _txtDebug.SelectionStart = _txtDebug.TextLength;
            _txtDebug.ScrollToCaret();
        }

    }
}
