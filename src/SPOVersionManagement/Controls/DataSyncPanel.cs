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

        // Auth mode
        private RadioButton _rbInteractive, _rbAppCredentials;
        private TextBox _txtAdminUrl;
        private TextBox _txtTenantId, _txtClientId, _txtCertThumb;
        private Panel _appCredPanel;

        // Sync options
        private CheckBox _chkExportAllSites, _chkExportGraphReport, _chkExportArchiveAnalysis, _chkExportTenantStorage;

        // Execution
        private FlatButton _btnConnect, _btnSync, _btnAbort;
        private TextBox _console;
        private ProgressBar _progressBar;
        private Label _lblStatus;

        // Telemetry sync
        private FlatButton _btnTelemetrySync;
        private Label _lblTelemetryStatus;
        private ProgressBar _telemetryProgress;

        public event EventHandler<string> StatusMessage;

        public DataSyncPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(0, 0, 0, 20);
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
            LoadEntraIdDefaults();
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void LoadEntraIdDefaults()
        {
            var entra = _config.AppConfig.EntraIdApp;
            if (entra != null)
            {
                _txtTenantId.Text = entra.TenantId ?? "";
                _txtClientId.Text = entra.ClientId ?? "";
                _txtCertThumb.Text = entra.CertificateThumbprint ?? "";
            }
        }

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
            topBar.Controls.Add(new Label { Text = "Connect to SharePoint Online and sync site data, graph reports, and archive analysis.", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 28) });
            y += 56;

            // ═══ CONNECTION ═══
            var connCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 50), AccentLeft = AppTheme.AccentCyan, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(connCard);
            CL(connCard, "CONNECTION", AppTheme.AccentCyan, 14, 4);
            CL(connCard, "Admin URL:", AppTheme.TextSecondary, 14, 24);
            connCard.Controls[connCard.Controls.Count - 1].Font = AppTheme.FontBody;
            _txtAdminUrl = new TextBox { Location = new Point(100, 22), Size = new Size(400, 20), ReadOnly = true };
            AppTheme.StyleTextBox(_txtAdminUrl);
            _txtAdminUrl.Text = _config.AppConfig.AdminUrl ?? "";
            connCard.Controls.Add(_txtAdminUrl);
            connCard.Controls.Add(new Label { Text = "(from Config tab)", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(508, 26) });

            _btnConnect = new FlatButton { Text = "\u25B6  Connect", Size = new Size(100, 26), Location = new Point(W - 112, 12), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnConnect.SetAccentColor(AppTheme.AccentGreen);
            _btnConnect.Click += BtnConnect_Click;
            connCard.Controls.Add(_btnConnect);
            y += 50 + cardGap;

            // ═══ ROW: Auth Mode (left) + Sync Options (right) ═══
            int halfW = (W - cardGap) / 2;

            // Auth Mode
            var authCard = new GlassPanel { Location = new Point(0, y), Size = new Size(halfW, 180), AccentLeft = AppTheme.AccentPurple, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            Controls.Add(authCard);
            CL(authCard, "AUTHENTICATION", AppTheme.AccentPurple, 14, 2);

            _rbInteractive = new RadioButton { Text = "  Interactive Login (browser)", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentCyan, BackColor = Color.Transparent, AutoSize = true, Location = new Point(14, 24), Checked = true };
            _rbInteractive.CheckedChanged += (s, e) => ToggleAuthMode();
            authCard.Controls.Add(_rbInteractive);
            authCard.Controls.Add(new Label { Text = "Opens browser for MFA/SSO sign-in", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 42) });

            _rbAppCredentials = new RadioButton { Text = "  App Credentials (EntraID)", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentCyan, BackColor = Color.Transparent, AutoSize = true, Location = new Point(14, 58) };
            _rbAppCredentials.CheckedChanged += (s, e) => ToggleAuthMode();
            authCard.Controls.Add(_rbAppCredentials);

            _appCredPanel = new Panel { Location = new Point(14, 80), Size = new Size(halfW - 28, 90), BackColor = Color.Transparent, Visible = false };
            authCard.Controls.Add(_appCredPanel);

            PL(_appCredPanel, "Tenant ID:", 0, 2, 90);
            _txtTenantId = new TextBox { Location = new Point(94, 0), Size = new Size(Math.Max(180, halfW - 140), 20) };
            AppTheme.StyleTextBox(_txtTenantId);
            _appCredPanel.Controls.Add(_txtTenantId);

            PL(_appCredPanel, "Client ID:", 0, 28, 90);
            _txtClientId = new TextBox { Location = new Point(94, 26), Size = new Size(Math.Max(180, halfW - 140), 20) };
            AppTheme.StyleTextBox(_txtClientId);
            _appCredPanel.Controls.Add(_txtClientId);

            PL(_appCredPanel, "Cert Thumb:", 0, 54, 90);
            _txtCertThumb = new TextBox { Location = new Point(94, 52), Size = new Size(Math.Max(180, halfW - 140), 20) };
            AppTheme.StyleTextBox(_txtCertThumb);
            _appCredPanel.Controls.Add(_txtCertThumb);

            // Sync Options
            var syncCard = new GlassPanel { Location = new Point(halfW + cardGap, y), Size = new Size(halfW, 180), AccentLeft = AppTheme.AccentGold, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
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

            // ═══ CONSOLE ═══
            _console = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                Location = new Point(0, y), Size = new Size(W, 250),
                Font = AppTheme.FontMono, BackColor = AppTheme.BgInput, ForeColor = AppTheme.AccentGreen,
                BorderStyle = BorderStyle.FixedSingle, WordWrap = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            Controls.Add(_console);
        }

        private void ToggleAuthMode()
        {
            _appCredPanel.Visible = _rbAppCredentials.Checked;
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            string adminUrl = _txtAdminUrl.Text.Trim();
            if (string.IsNullOrEmpty(adminUrl))
            {
                MessageBox.Show("Admin URL is required.\n\nExample: https://contoso-admin.sharepoint.com", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnConnect.Enabled = false;
            AppendConsole("Connecting to SharePoint Online...", AppTheme.AccentCyan);

            try
            {
                string script;
                if (_rbAppCredentials.Checked)
                {
                    string tenantId = _txtTenantId.Text.Trim();
                    string clientId = _txtClientId.Text.Trim();
                    string certThumb = _txtCertThumb.Text.Trim();
                    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(certThumb))
                    {
                        MessageBox.Show("Provide Tenant ID, Client ID, and Certificate Thumbprint.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _btnConnect.Enabled = true;
                        return;
                    }
                    script = $"Connect-SPOService -Url '{adminUrl}' -ClientId '{clientId}' -CertificateThumbprint '{certThumb}' -Tenant '{tenantId}'";
                }
                else
                {
                    // Interactive login - browser-based MFA/SSO
                    script = $"Connect-SPOService -Url '{adminUrl}'";
                }

                await _psHost.RunScriptAsync(script);
                AppendConsole("Connected successfully.", AppTheme.AccentGreen);
                _lblStatus.Text = "Connected";
                _lblStatus.ForeColor = AppTheme.AccentGreen;
            }
            catch (Exception ex)
            {
                AppendConsole($"Connection failed: {ex.Message}", AppTheme.AccentRed);
                _lblStatus.Text = "Connection failed";
                _lblStatus.ForeColor = AppTheme.AccentRed;
            }
            finally
            {
                _btnConnect.Enabled = true;
            }
        }

        private async void BtnSync_Click(object sender, EventArgs e)
        {
            string adminUrl = _txtAdminUrl.Text.Trim();
            if (string.IsNullOrEmpty(adminUrl))
            {
                MessageBox.Show("Admin URL is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_chkExportAllSites.Checked && !_chkExportGraphReport.Checked &&
                !_chkExportArchiveAnalysis.Checked && !_chkExportTenantStorage.Checked)
            {
                MessageBox.Show("Select at least one sync option.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
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
                int step = 0;
                int total = (_chkExportAllSites.Checked ? 1 : 0) + (_chkExportGraphReport.Checked ? 1 : 0) +
                            (_chkExportArchiveAnalysis.Checked ? 1 : 0) + (_chkExportTenantStorage.Checked ? 1 : 0);

                string rootPath = _config.RootPath;

                if (_chkExportAllSites.Checked)
                {
                    step++;
                    _lblStatus.Text = $"Step {step}/{total}: Exporting All Sites...";
                    _progressBar.Value = step * 100 / total;
                    AppendConsole($"\n=== Step {step}/{total}: Export All SPO Sites ===", AppTheme.AccentCyan);

                    // Ask user via MessageBox whether to also save to AllSites.json
                    var saveJson = MessageBox.Show(
                        "After exporting, do you want to also save the data to AllSites.json for the Dashboard?\n\n" +
                        "This updates the local database so the Dashboard displays fresh data.",
                        "Save to Dashboard Database",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    string autoConfirm = saveJson == DialogResult.Yes ? " -AutoConfirm" : "";

                    string script = $"& '{Path.Combine(rootPath, "Export-AllSPOSites.ps1")}' -AdminUrl '{adminUrl}'{autoConfirm}";
                    await _psHost.RunScriptAsync(script, cancellationToken: _cts.Token);
                }

                if (_chkExportGraphReport.Checked)
                {
                    step++;
                    _lblStatus.Text = $"Step {step}/{total}: Graph API Report...";
                    _progressBar.Value = step * 100 / total;
                    AppendConsole($"\n=== Step {step}/{total}: Graph API Report ===", AppTheme.AccentCyan);
                    string script = $"& '{Path.Combine(rootPath, "Test-GraphReport.ps1")}'";
                    await _psHost.RunScriptAsync(script, cancellationToken: _cts.Token);
                }

                if (_chkExportArchiveAnalysis.Checked)
                {
                    step++;
                    _lblStatus.Text = $"Step {step}/{total}: Archive Analysis...";
                    _progressBar.Value = step * 100 / total;
                    AppendConsole($"\n=== Step {step}/{total}: Import SAM Inactive Sites ===", AppTheme.AccentCyan);
                    string script = $"& '{Path.Combine(rootPath, "Import-SamInactiveSites.ps1")}'";
                    await _psHost.RunScriptAsync(script, cancellationToken: _cts.Token);
                }

                if (_chkExportTenantStorage.Checked)
                {
                    step++;
                    _lblStatus.Text = $"Step {step}/{total}: Tenant Storage...";
                    _progressBar.Value = step * 100 / total;
                    AppendConsole($"\n=== Step {step}/{total}: Update Tenant Storage Timeline ===", AppTheme.AccentCyan);
                    string script = $"& '{Path.Combine(rootPath, "Get-SpoSitesVersion.ps1")}' -AdminUrl '{adminUrl}'";
                    await _psHost.RunScriptAsync(script, cancellationToken: _cts.Token);
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
            _btnConnect.Enabled = !running;
            _rbInteractive.Enabled = !running;
            _rbAppCredentials.Enabled = !running;
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
        #endregion
    }
}
