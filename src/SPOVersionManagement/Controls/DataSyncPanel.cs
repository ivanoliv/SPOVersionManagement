using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
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
