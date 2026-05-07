using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    /// <summary>
    /// Panel for synchronizing data from SharePoint Online APIs.
    /// Supports both interactive login and app-credential (EntraID) authentication.
    /// </summary>
    public class DataSyncPanel : UserControl
    {
        private ConfigurationService _config;
        private PowerShellHostService _psHost;
        private bool _initialized;
        private CancellationTokenSource _cts;

        // Sync options
        private CheckBox _chkExportAllSites, _chkExportGraphReport, _chkExportArchiveAnalysis, _chkExportTenantStorage;

        // Input files
        private TextBox _txtGraphReportCsv, _txtSamReportCsv;

        // Execution
        private FlatButton _btnSync, _btnAbort;
        private TextBox _console;
        private ProgressBar _progressBar;
        private Label _lblStatus;

        // Telemetry sync
        private FlatButton _btnTelemetrySync;
        private Label _lblTelemetryStatus;
        private ProgressBar _telemetryProgress;

        // External job sync
        private NumericUpDown _nudLookBackDays;
        private FlatButton _btnSyncExternalJobs;
        private Label _lblExternalJobStatus;

        public event EventHandler<string> StatusMessage;

        public DataSyncPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = false;
            Padding = new Padding(0, 0, 0, 0);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            if (_initialized) return;
            _initialized = true;
            _config = config;
            _psHost = psHost;
            BuildLayout();
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void BuildLayout()
        {
            Controls.Clear();
            int y = 0;
            int cardGap = 16;
            int W = Math.Max(700, ClientSize.Width - Padding.Horizontal - 16);

            // ═══ TOP BAR ═══
            var topBar = new Panel { Location = new Point(0, y), Size = new Size(W, 48), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(topBar);
            topBar.Controls.Add(new Label { Text = "Data Synchronization", Font = AppTheme.FontTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 2) });
            topBar.Controls.Add(new Label { Text = "Sync site data, graph reports, and archive analysis.", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 28) });
            y += 56;

            // ═══ SYNC OPTIONS ═══
            var syncCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 180), AccentLeft = AppTheme.AccentGold, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(syncCard);
            CL(syncCard, "SYNC OPTIONS", AppTheme.AccentGold, 14, 2);

            _chkExportAllSites = MkChk(syncCard, "Export All Sites (Get-SPOSite)", 14, 26);
            _chkExportAllSites.Checked = true;
            syncCard.Controls.Add(new Label { Text = "Exports AllSites.json with full site inventory", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 44) });

            _chkExportGraphReport = MkChk(syncCard, "Graph API Report (usage/activity)", 14, 60);
            _chkExportGraphReport.Checked = true;
            syncCard.Controls.Add(new Label { Text = "Site usage storage report via Microsoft Graph", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 78) });

            _chkExportArchiveAnalysis = MkChk(syncCard, "Archive Analysis (pre-process)", 14, 94);
            _chkExportArchiveAnalysis.Checked = true;
            syncCard.Controls.Add(new Label { Text = "Builds lightweight ArchiveAnalysis.json for Dashboard", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 112) });

            _chkExportTenantStorage = MkChk(syncCard, "Tenant Storage Timeline", 14, 128);
            _chkExportTenantStorage.Checked = true;
            syncCard.Controls.Add(new Label { Text = "Updates TenantStorageTimeline.json for trend charts", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 146) });

            y += 180 + cardGap;

            // ═══ INPUT FILES ═══
            var filesCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 78), AccentLeft = AppTheme.AccentCyan, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(filesCard);
            CL(filesCard, "INPUT FILES (optional — load from local CSV instead of API)", AppTheme.AccentCyan, 14, 2);
            int fIn = Math.Max(220, W - 440);
            int fLbl = 162;
            int fy = 24;

            PL(filesCard, "Graph Report (CSV):", 14, fy, fLbl);
            _txtGraphReportCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", null);
            CL(filesCard, "SharePoint Site Usage Storage report", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);
            fy += 26;

            PL(filesCard, "SAM Report (CSV):", 14, fy, fLbl);
            _txtSamReportCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", null);
            CL(filesCard, "Content Management Assessment (for Archive Analysis)", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);

            y += 78 + cardGap;

            // ═══ SYNC BUTTONS ═══
            var actionBar = new Panel { Location = new Point(0, y), Size = new Size(W, 36), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(actionBar);

            _btnSync = new FlatButton { Text = "\u25B6  Run Sync", Size = new Size(110, 30), Location = new Point(0, 2) };
            _btnSync.SetAccentColor(AppTheme.AccentGreen);
            _btnSync.Click += BtnSync_Click;
            actionBar.Controls.Add(_btnSync);

            _btnAbort = new FlatButton { Text = "\u25A0  Abort", Size = new Size(80, 30), Location = new Point(118, 2), Enabled = false };
            _btnAbort.SetDangerStyle();
            _btnAbort.Click += BtnAbort_Click;
            actionBar.Controls.Add(_btnAbort);

            _lblStatus = new Label { Text = "Ready", Font = AppTheme.FontBody, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(210, 8) };
            actionBar.Controls.Add(_lblStatus);
            y += 36 + 8;

            _progressBar = new ProgressBar { Location = new Point(0, y), Size = new Size(W, 5), Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(_progressBar);
            y += 12;

            // ═══ TELEMETRY SYNC ═══
            var telemetryCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 80), AccentLeft = AppTheme.AccentCyan, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(telemetryCard);
            CL(telemetryCard, "TELEMETRY SYNC", AppTheme.AccentCyan, 14, 4);
            telemetryCard.Controls.Add(new Label { Text = "Upload processed site history to the global telemetry backend (deduplicated, anonymous).", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 22) });

            _btnTelemetrySync = new FlatButton { Text = "\u2191  Sync History to Telemetry", Size = new Size(200, 28), Location = new Point(14, 46) };
            _btnTelemetrySync.SetAccentColor(AppTheme.AccentCyan);
            _btnTelemetrySync.Click += BtnTelemetrySync_Click;
            telemetryCard.Controls.Add(_btnTelemetrySync);

            _lblTelemetryStatus = new Label { Text = "", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(224, 52) };
            telemetryCard.Controls.Add(_lblTelemetryStatus);

            _telemetryProgress = new ProgressBar { Location = new Point(W - 212, 50), Size = new Size(200, 5), Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            telemetryCard.Controls.Add(_telemetryProgress);
            y += 80 + cardGap;

            // ═══ EXTERNAL JOB SYNC ═══
            var extJobCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 96), AccentLeft = AppTheme.AccentPurple, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(extJobCard);
            CL(extJobCard, "EXTERNAL JOB SYNC", AppTheme.AccentPurple, 14, 4);
            extJobCard.Controls.Add(new Label
            {
                Text = "Check SharePoint for version management jobs completed outside this tool (other admins, scripts, or scheduled tasks).\nUpdates local execution history so Dashboard and re-execution rules reflect the real state.",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                Size = new Size(W - 28, 30),
                BackColor = Color.Transparent,
                Location = new Point(14, 22)
            });

            extJobCard.Controls.Add(new Label { Text = "Look Back:", Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 62) });
            _nudLookBackDays = new NumericUpDown { Location = new Point(90, 60), Size = new Size(50, 22), Minimum = 1, Maximum = 90, Value = 7 };
            AppTheme.StyleNumericUpDown(_nudLookBackDays);
            extJobCard.Controls.Add(_nudLookBackDays);
            extJobCard.Controls.Add(new Label { Text = "days", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(144, 64) });

            _btnSyncExternalJobs = new FlatButton { Text = "\u21BB  Sync External Jobs", Size = new Size(170, 28), Location = new Point(190, 58) };
            _btnSyncExternalJobs.SetAccentColor(AppTheme.AccentPurple);
            _btnSyncExternalJobs.Click += BtnSyncExternalJobs_Click;
            extJobCard.Controls.Add(_btnSyncExternalJobs);

            _lblExternalJobStatus = new Label { Text = "", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(370, 64) };
            extJobCard.Controls.Add(_lblExternalJobStatus);
            y += 96 + cardGap;

            // ═══ CONSOLE ═══
            int consoleTop = y;
            _console = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(0, consoleTop), Size = new Size(W, 300),
                Font = AppTheme.FontMono, BackColor = AppTheme.BgInput, ForeColor = AppTheme.AccentGreen,
                BorderStyle = BorderStyle.FixedSingle, WordWrap = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_console);

            // Adjust console height after layout is ready
            this.Resize += (s, e) =>
            {
                int h = ClientSize.Height - consoleTop - 4;
                if (h > 100) _console.Height = h;
            };

            LoadGuiSettings();
            WireAutoSave();
        }

        private void LoadGuiSettings()
        {
            var s = _config.LoadGuiSettings();
            _chkExportAllSites.Checked = s.DataSyncExportAllSites;
            _chkExportGraphReport.Checked = s.DataSyncExportGraphReport;
            _chkExportArchiveAnalysis.Checked = s.DataSyncExportArchiveAnalysis;
            _chkExportTenantStorage.Checked = s.DataSyncExportTenantStorage;

            int lookBack = _config.DashboardConfig.LookBackDays;
            if (lookBack >= (int)_nudLookBackDays.Minimum && lookBack <= (int)_nudLookBackDays.Maximum)
                _nudLookBackDays.Value = lookBack;
        }

        private void WireAutoSave()
        {
            _chkExportAllSites.CheckedChanged += (s, e) => SaveDataSyncSettings();
            _chkExportGraphReport.CheckedChanged += (s, e) => SaveDataSyncSettings();
            _chkExportArchiveAnalysis.CheckedChanged += (s, e) => SaveDataSyncSettings();
            _chkExportTenantStorage.CheckedChanged += (s, e) => SaveDataSyncSettings();
            _nudLookBackDays.ValueChanged += (s, e) => SaveLookBackDays();
        }

        private void SaveDataSyncSettings()
        {
            var s = _config.LoadGuiSettings();
            s.DataSyncExportAllSites = _chkExportAllSites.Checked;
            s.DataSyncExportGraphReport = _chkExportGraphReport.Checked;
            s.DataSyncExportArchiveAnalysis = _chkExportArchiveAnalysis.Checked;
            s.DataSyncExportTenantStorage = _chkExportTenantStorage.Checked;
            _config.SaveGuiSettings(s);
        }

        private void SaveLookBackDays()
        {
            int days = (int)_nudLookBackDays.Value;
            _config.DashboardConfig.LookBackDays = days;
            _config.SaveDashboardConfig();

            var gs = _config.LoadGuiSettings();
            gs.LookBackDays = days;
            _config.SaveGuiSettings(gs);
        }

        private async void BtnSync_Click(object sender, EventArgs e)
        {
            string adminUrl = _psHost.IsConnected ? _psHost.AdminUrl : _config.AppConfig.AdminUrl?.Trim();
            if (string.IsNullOrEmpty(adminUrl))
            {
                MessageBox.Show("Admin URL is not configured.\n\nSet it in the Config tab first.", "No Admin URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_chkExportAllSites.Checked && !_chkExportGraphReport.Checked &&
                !_chkExportArchiveAnalysis.Checked && !_chkExportTenantStorage.Checked)
            {
                MessageBox.Show("Select at least one sync option.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Ask user about AllSites.json before starting
            string autoConfirm = "";
            if (_chkExportAllSites.Checked)
            {
                var saveJson = MessageBox.Show(
                    "After exporting, do you want to also save the data to AllSites.json for the Dashboard?\n\n" +
                    "This updates the local database so the Dashboard displays fresh data.",
                    "Save to Dashboard Database",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                autoConfirm = saveJson == DialogResult.Yes ? " -AutoConfirm" : "";
            }

            SetExecuting(true);
            _console.Clear();
            _cts = new CancellationTokenSource();

            Action<string> outHandler = msg => AppendConsole(msg, AppTheme.TextSecondary);
            Action<string> warnHandler = msg => AppendConsole("[WARN] " + msg, AppTheme.AccentGold);
            Action<string> errHandler = msg => AppendConsole("[ERROR] " + msg, AppTheme.AccentRed);

            _psHost.OnOutput += outHandler;
            _psHost.OnWarning += warnHandler;
            _psHost.OnError += errHandler;

            try
            {
                string rootPath = _config.RootPath;
                int total = (_chkExportAllSites.Checked ? 1 : 0) + (_chkExportGraphReport.Checked ? 1 : 0) +
                            (_chkExportArchiveAnalysis.Checked ? 1 : 0) + (_chkExportTenantStorage.Checked ? 1 : 0);

                // Build a single combined script so all steps run in ONE process (shared auth session)
                var sb = new System.Text.StringBuilder();
                int step = 0;

                // Preamble: connect to all required services UPFRONT in one browser session
                sb.AppendLine("# --- Authentication ---");
                sb.AppendLine("$WarningPreference = 'SilentlyContinue'");
                sb.AppendLine("Write-Host 'Authenticating to required services...' -ForegroundColor Yellow");
                if (_chkExportAllSites.Checked || _chkExportTenantStorage.Checked)
                {
                    sb.AppendLine("Import-Module Microsoft.Online.SharePoint.PowerShell -DisableNameChecking -WarningAction SilentlyContinue -ErrorAction SilentlyContinue");
                    sb.AppendLine($"try {{ Connect-SPOService -Url '{adminUrl}' -ErrorAction Stop; Write-Host '  [OK] SPO connected' -ForegroundColor Green }} catch {{ Write-Host \"  [ERROR] SPO: $($_.Exception.Message)\" -ForegroundColor Red; exit 1 }}");
                }
                sb.AppendLine("$WarningPreference = 'Continue'");
                sb.AppendLine("Write-Host ''");

                if (_chkExportAllSites.Checked)
                {
                    step++;
                    sb.AppendLine($"Write-Host ''; Write-Host '=== Step {step}/{total}: Export All SPO Sites ===' -ForegroundColor Cyan");
                    sb.AppendLine($"& '{Path.Combine(rootPath, "Export-AllSPOSites.ps1")}' -AdminUrl '{adminUrl}'{autoConfirm}");
                    sb.AppendLine("if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }");
                }

                if (_chkExportGraphReport.Checked)
                {
                    step++;
                    string graphCsv = _txtGraphReportCsv.Text?.Trim();
                    if (!string.IsNullOrEmpty(graphCsv) && File.Exists(graphCsv))
                    {
                        // Use local CSV file — skip Graph API call entirely
                        sb.AppendLine($"Write-Host ''; Write-Host '=== Step {step}/{total}: Graph Report (from local CSV) ===' -ForegroundColor Cyan");
                        sb.AppendLine($"Import-Module '{Path.Combine(rootPath, "SPOVersionManagement.psm1").Replace("'", "''")}' -Force -DisableNameChecking -WarningAction SilentlyContinue");
                        sb.AppendLine($"Import-GraphReportCSV -CsvPath '{graphCsv.Replace("'", "''")}'" );
                    }
                    else
                    {
                        // Download from Graph API
                        sb.AppendLine($"Write-Host ''; Write-Host '=== Step {step}/{total}: Graph API Report ===' -ForegroundColor Cyan");
                        sb.AppendLine($"powershell.exe -ExecutionPolicy Bypass -File '{Path.Combine(rootPath, "Test-GraphReport.ps1")}'");
                    }
                    sb.AppendLine("if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }");
                }

                if (_chkExportArchiveAnalysis.Checked)
                {
                    step++;
                    sb.AppendLine($"Write-Host ''; Write-Host '=== Step {step}/{total}: Import SAM Inactive Sites ===' -ForegroundColor Cyan");
                    string samCsv = _txtSamReportCsv.Text?.Trim();
                    string samParam = (!string.IsNullOrEmpty(samCsv) && File.Exists(samCsv))
                        ? $" -SAMReportPath '{samCsv.Replace("'", "''")}'"
                        : "";
                    sb.AppendLine($"& '{Path.Combine(rootPath, "Import-SamInactiveSites.ps1")}'{samParam}");
                    sb.AppendLine("if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }");
                }

                if (_chkExportTenantStorage.Checked)
                {
                    step++;
                    sb.AppendLine($"Write-Host ''; Write-Host '=== Step {step}/{total}: Update Tenant Storage Timeline ===' -ForegroundColor Cyan");
                    sb.AppendLine($"& '{Path.Combine(rootPath, "Get-SpoSitesVersion.ps1")}' -AdminUrl '{adminUrl}'");
                    sb.AppendLine("if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { exit $LASTEXITCODE }");
                }

                _lblStatus.Text = $"Running {total} sync steps...";
                _progressBar.Value = 50;

                // Run all steps in a single process — connection is shared across scripts
                string combinedScript = sb.ToString().Replace("\"", "\\\"");
                string scriptFile = Path.Combine(Path.GetTempPath(), "SPOSync_" + Guid.NewGuid().ToString("N") + ".ps1");
                File.WriteAllText(scriptFile, sb.ToString(), System.Text.Encoding.UTF8);

                try
                {
                    string execScript = $"& '{scriptFile}'";
                    await _psHost.RunScriptAsync(execScript, cancellationToken: _cts.Token);
                }
                finally
                {
                    try { File.Delete(scriptFile); } catch { }
                }

                _progressBar.Value = 100;
                AppendConsole("\nSync completed successfully.", AppTheme.AccentGreen);
                _lblStatus.Text = "Sync completed";
                _lblStatus.ForeColor = AppTheme.AccentGreen;
                StatusMessage?.Invoke(this, "Data sync completed.");
            }
            catch (OperationCanceledException)
            {
                AppendConsole("\nSync cancelled.", AppTheme.AccentGold);
                _lblStatus.Text = "Cancelled";
                _lblStatus.ForeColor = AppTheme.AccentGold;
            }
            catch (Exception ex)
            {
                AppendConsole($"\nSync failed: {ex.Message}", AppTheme.AccentRed);
                _lblStatus.Text = "Failed";
                _lblStatus.ForeColor = AppTheme.AccentRed;
            }
            finally
            {
                _psHost.OnOutput -= outHandler;
                _psHost.OnWarning -= warnHandler;
                _psHost.OnError -= errHandler;
                SetExecuting(false);
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BtnAbort_Click(object sender, EventArgs e)
        {
            if (_cts != null && MessageBox.Show("Cancel sync?", "Abort", MessageBoxButtons.YesNo) == DialogResult.Yes)
                _cts.Cancel();
        }

        private void SetExecuting(bool running)
        {
            _btnSync.Enabled = !running;
            _btnAbort.Enabled = running;
            _chkExportAllSites.Enabled = !running;
            _chkExportGraphReport.Enabled = !running;
            _chkExportArchiveAnalysis.Enabled = !running;
            _chkExportTenantStorage.Enabled = !running;
            if (running)
            {
                _lblStatus.Text = "Running...";
                _lblStatus.ForeColor = AppTheme.AccentCyan;
            }
        }

        private void AppendConsole(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            Action a = () => { _console.AppendText(text + Environment.NewLine); _console.SelectionStart = _console.TextLength; _console.ScrollToCaret(); };
            if (InvokeRequired) Invoke(a); else a();
        }

        private void BtnSyncExternalJobs_Click(object sender, EventArgs e)
        {
            string adminUrl = (_config.AppConfig.AdminUrl ?? "").Trim();
            if (string.IsNullOrWhiteSpace(adminUrl))
            {
                MessageBox.Show("Admin URL is not configured. Set it in Config first.", "Missing Config", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int days = (int)_nudLookBackDays.Value;
            string rootPath = _config.RootPath;
            string modulePath = Path.Combine(rootPath, "SPOVersionManagement.psm1");
            string allSitesPath = Path.Combine(_config.ConfigPath, "AllSites.json");

            if (!File.Exists(modulePath))
            {
                MessageBox.Show($"Module not found:\n{modulePath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(allSitesPath))
            {
                MessageBox.Show($"AllSites.json not found:\n{allSitesPath}\n\nRun Data Sync first to export site inventory.", "Missing Data", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Build inline PS script that runs in a new visible window
            string script = string.Join("; ",
                $"$ErrorActionPreference = 'Stop'",
                $"Write-Host '=== External Job Sync ===' -ForegroundColor Cyan",
                $"Write-Host 'Looking back: {days} days' -ForegroundColor Gray",
                $"Write-Host ''",
                $"Import-Module '{modulePath.Replace("'", "''")}' -Force",
                $"Connect-SPOService -Url '{adminUrl.Replace("'", "''")}'",
                $"$sites = (Get-Content '{allSitesPath.Replace("'", "''")}' -Raw | ConvertFrom-Json).Sites",
                $"Write-Host \"Loaded $($sites.Count) sites\" -ForegroundColor Gray",
                $"Sync-ExternalJobResults -Sites $sites -DaysToCheck {days}",
                $"Write-Host ''",
                $"Write-Host 'Done. Press any key to close...' -ForegroundColor Green",
                $"$null = $Host.UI.RawUI.ReadKey('NoEcho,IncludeKeyDown')"
            );

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                UseShellExecute = true,
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
            };

            try
            {
                System.Diagnostics.Process.Start(psi);
                _lblExternalJobStatus.Text = $"Launched ({DateTime.Now:HH:mm})";
                _lblExternalJobStatus.ForeColor = AppTheme.AccentGreen;
                AppendConsole($"External Job Sync launched (look back: {days} days)", AppTheme.AccentPurple);
                StatusMessage?.Invoke(this, "External Job Sync launched in new window.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to launch:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnTelemetrySync_Click(object sender, EventArgs e)
        {
            if (!_config.AppConfig.TelemetryEnabled)
            {
                MessageBox.Show("Telemetry is disabled.\n\nEnable it in Config > Telemetry settings first.", "Telemetry Disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(_config.AppConfig.TelemetryEndpoint))
            {
                MessageBox.Show("No telemetry endpoint configured.", "Configuration Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Resolve TenantId — needed for telemetry hash
            string tenantId = _config.AppConfig.EntraIdApp?.TenantId;
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = await ResolveTenantIdViaPnP();
                if (string.IsNullOrWhiteSpace(tenantId))
                {
                    MessageBox.Show(
                        "TenantId is required for telemetry sync.\n\n" +
                        "Either configure it in Config > EntraID App, or ensure PnP.PowerShell is installed and pwsh (PS7+) is available.",
                        "TenantId Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _btnTelemetrySync.Enabled = false;
            SetTelemetryStatus("Loading history...", AppTheme.AccentCyan);
            StatusMessage?.Invoke(this, "Telemetry sync starting...");

            try
            {
                string resolvedTenantId = tenantId;
                await Task.Run(() => RunTelemetrySync(resolvedTenantId));
            }
            catch (Exception ex)
            {
                SetTelemetryStatus($"Failed: {ex.Message}", AppTheme.AccentRed);
                StatusMessage?.Invoke(this, $"Telemetry sync failed: {ex.Message}");
            }
            finally
            {
                _btnTelemetrySync.Enabled = true;
            }
        }

        /// <summary>
        /// Attempts to resolve TenantId via PnP PowerShell (PS7+).
        /// Requires pwsh and PnP.PowerShell module.
        /// </summary>
        private async Task<string> ResolveTenantIdViaPnP()
        {
            string adminUrl = _config.AppConfig.AdminUrl;
            if (string.IsNullOrWhiteSpace(adminUrl))
                return null;

            try
            {
                SetTelemetryStatus("Resolving TenantId via PnP...", AppTheme.AccentCyan);
                FireStatus("Resolving TenantId via PnP PowerShell...");

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = $"-NoProfile -NonInteractive -Command \"Connect-PnPOnline -Url '{adminUrl}' -Interactive; Get-PnPTenantId\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                string output = null;
                await Task.Run(() =>
                {
                    using (var proc = System.Diagnostics.Process.Start(psi))
                    {
                        output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit(60000);
                    }
                });

                // Validate it looks like a GUID
                Guid guid;
                if (!string.IsNullOrWhiteSpace(output) && Guid.TryParse(output.Split('\n').Last().Trim(), out guid))
                {
                    SetTelemetryStatus($"TenantId resolved: {guid.ToString().Substring(0, 8)}...", AppTheme.AccentGreen);
                    return guid.ToString();
                }
            }
            catch
            {
                // pwsh not available or PnP not installed — fall through
            }

            return null;
        }

        private void RunTelemetrySync(string tenantId)
        {
            // Load site execution history
            string historyFile = Path.Combine(_config.ConfigPath, "SiteExecutionHistory.json");
            if (!File.Exists(historyFile))
            {
                SetTelemetryStatus("No execution history found.", AppTheme.AccentGold);
                FireStatus("Telemetry: no history to sync.");
                return;
            }

            // Load sent log (tracks which WorkItemIds have already been sent)
            string sentLogFile = Path.Combine(_config.ConfigPath, "TelemetrySentLog.json");
            var sentIds = LoadSentLog(sentLogFile);

            // Parse history and collect unsent BatchDelete completions
            var json = File.ReadAllText(historyFile);
            var root = JObject.Parse(json);
            var sites = root["Sites"] as JObject;
            if (sites == null || !sites.HasValues)
            {
                SetTelemetryStatus("No sites in history.", AppTheme.TextMuted);
                FireStatus("Telemetry: no sites in history.");
                return;
            }

            var salt = _config.AppConfig.TelemetrySalt ?? TelemetryService.GenerateTenantSalt(tenantId);
            var telemetry = new TelemetryService(
                _config.AppConfig.TelemetryEndpoint, tenantId,
                _config.AppConfig.AppVersion ?? "unknown", salt);

            var unsent = new List<TelemetryPayload>();
            var newSentIds = new List<string>();

            foreach (var siteProp in sites.Properties())
            {
                string siteUrl = siteProp.Name;
                var siteObj = siteProp.Value as JObject;
                if (siteObj == null) continue;

                var executions = siteObj["Executions"] as JArray;
                if (executions == null) continue;

                foreach (var exec in executions)
                {
                    string status = exec["Status"]?.ToString() ?? "";
                    string jobType = exec["JobType"]?.ToString() ?? "";
                    string workItemId = exec["WorkItemId"]?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(workItemId)) continue;
                    if (jobType != "BatchDelete") continue;
                    if (status != "CompleteSuccess" && status != "Completed") continue;

                    // Skip if already sent
                    string hashedId = TelemetryService.HashWorkItemId(workItemId);
                    if (sentIds.Contains(hashedId)) continue;

                    long storage = 0;
                    long versions = 0;
                    long.TryParse(exec["StorageReleasedBytes"]?.ToString() ?? "0", out storage);
                    long.TryParse(exec["VersionsDeleted"]?.ToString() ?? "0", out versions);

                    string timestamp = exec["ExecutedAt"]?.ToString();

                    unsent.Add(telemetry.BuildPayload(workItemId, siteUrl, jobType, storage, versions, timestamp));
                    newSentIds.Add(hashedId);
                }
            }

            if (unsent.Count == 0)
            {
                SetTelemetryStatus("All history already synced.", AppTheme.AccentGreen);
                FireStatus("Telemetry: all history already synced.");
                return;
            }

            FireStatus($"Telemetry: syncing {unsent.Count} items...");

            // Send in batches of 500
            int batchSize = 500;
            int totalSent = 0;
            int totalBatches = (int)Math.Ceiling((double)unsent.Count / batchSize);

            for (int i = 0; i < unsent.Count; i += batchSize)
            {
                int count = Math.Min(batchSize, unsent.Count - i);
                var batch = unsent.GetRange(i, count).ToArray();
                int batchNum = (i / batchSize) + 1;

                SetTelemetryStatus($"Batch {batchNum}/{totalBatches} ({totalSent + count}/{unsent.Count})...", AppTheme.AccentCyan);
                SetTelemetryProgress(batchNum * 100 / totalBatches);
                FireStatus($"Telemetry: batch {batchNum}/{totalBatches} ({totalSent + count}/{unsent.Count})");

                telemetry.SendBatchAsync(batch).GetAwaiter().GetResult();
                totalSent += count;

                // Save progress after each batch (crash-safe)
                foreach (var id in newSentIds.Skip(i).Take(count))
                    sentIds.Add(id);
                SaveSentLog(sentLogFile, sentIds);
            }

            SetTelemetryStatus($"Done: {totalSent} sent.", AppTheme.AccentGreen);
            SetTelemetryProgress(100);
            FireStatus($"Telemetry sync complete: {totalSent} items sent.");
        }

        private void FireStatus(string msg)
        {
            Action a = () => StatusMessage?.Invoke(this, msg);
            if (InvokeRequired) Invoke(a); else a();
        }

        private HashSet<string> LoadSentLog(string path)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path)) return set;

            try
            {
                var json = File.ReadAllText(path);
                var arr = JArray.Parse(json);
                foreach (var item in arr)
                {
                    string id = item.ToString();
                    if (!string.IsNullOrWhiteSpace(id))
                        set.Add(id);
                }
            }
            catch { }

            return set;
        }

        private void SaveSentLog(string path, HashSet<string> sentIds)
        {
            var arr = new JArray();
            foreach (var id in sentIds)
                arr.Add(id);
            File.WriteAllText(path, arr.ToString(Newtonsoft.Json.Formatting.None));
        }

        private void SetTelemetryStatus(string text, Color color)
        {
            Action a = () => { _lblTelemetryStatus.Text = text; _lblTelemetryStatus.ForeColor = color; };
            if (InvokeRequired) Invoke(a); else a();
        }

        private void SetTelemetryProgress(int value)
        {
            Action a = () => _telemetryProgress.Value = Math.Min(value, 100);
            if (InvokeRequired) Invoke(a); else a();
        }

        #region Helpers
        private void CL(Control p, string t, Color c, int x, int y)
        {
            p.Controls.Add(new Label { Text = t, Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = c, AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) });
        }

        private void PL(Control p, string t, int x, int y, int w)
        {
            p.Controls.Add(new Label { Text = t, Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = false, Size = new Size(w, 20), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent, Location = new Point(x, y) });
        }

        private CheckBox MkChk(Control p, string t, int x, int y)
        {
            var c = new CheckBox { Text = "  " + t, Font = AppTheme.FontSmall, ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent, AutoSize = true, Location = new Point(x, y) };
            p.Controls.Add(c);
            return c;
        }

        private TextBox FileRow(Control p, int x, int y, int w, string filter, string def)
        {
            var t = new TextBox { Location = new Point(x, y), Size = new Size(w, 20) };
            AppTheme.StyleTextBox(t);
            t.Text = def ?? "";
            p.Controls.Add(t);
            var b = new FlatButton { Text = "...", Size = new Size(26, 20), Location = new Point(x + w + 2, y) };
            b.SetGhostStyle();
            b.Click += (s, e) =>
            {
                using (var d = new OpenFileDialog { Filter = filter })
                {
                    if (!string.IsNullOrEmpty(t.Text) && File.Exists(t.Text)) d.InitialDirectory = Path.GetDirectoryName(t.Text);
                    else d.InitialDirectory = _config.RootPath;
                    if (d.ShowDialog(ParentForm) == DialogResult.OK) t.Text = d.FileName;
                }
            };
            p.Controls.Add(b);
            return t;
        }
        #endregion
    }
}
