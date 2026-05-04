using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Newtonsoft.Json;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class ConfigurationPanel : UserControl
    {
        private ConfigurationService _config;
        private TextBox _txtAdminUrl;
        private TextBox _txtTenantId, _txtClientId, _txtCertThumb;
        private TextBox _txtPurviewClientId, _txtPurviewCertThumb, _txtPurviewOrg;
        private TextBox _txtCurrencySymbol, _txtCurrencyCode, _txtCostTBYear, _txtPort, _txtReexecutionDays;
        private TextBox _txtRootPath, _txtApplicationFolder, _txtLogsFolder, _txtBackupFolder;
        private TextBox _txtConfigFolder, _txtWebFolder, _txtAppFolder;
        private ComboBox _cmbLanguage, _cmbDateFormat, _cmbZeroVersion, _cmbDashboardLaunchMode;
        private CheckBox _chkTelemetry;
        private TextBox _txtTelemetryEndpoint, _txtGitHubRepo;
        private Label _lblPathPreviewRoot, _lblPathPreviewLogs, _lblPathPreviewBackup, _lblPathPreviewJobStatus;
        private Label _lblPathPreviewConfig, _lblPathPreviewWeb, _lblPathPreviewApp;
        private bool _isDirty;

        public event EventHandler BackupClicked;
        public event EventHandler<string> StatusMessage;
        public event EventHandler<string> DatabaseResetCompleted;
        public bool HasUnsavedChanges => _isDirty;

        public ConfigurationPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(28, 20, 28, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config)
        {
            _config = config;
            BuildLayout();
            LoadValues();
            _isDirty = false;
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void BuildLayout()
        {
            Controls.Clear();
            int lblW = 190, inW = 320, rowH = 30;
            int sectionGap = 24;

            // ═══ TOP BAR (fixed, non-scrolling feel via placement at top) ═══
            int y = 0;
            var topBar = new Panel { Location = new Point(0, y), Size = new Size(920, 52), BackColor = Color.Transparent };
            Controls.Add(topBar);

            topBar.Controls.Add(new Label { Text = "Configuration", Font = AppTheme.FontTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 4) });
            topBar.Controls.Add(new Label { Text = "All application settings. Changes are saved to disk.", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 30) });

            var btnSave = new FlatButton { Text = "\u2713  Save", Size = new Size(90, 32), Location = new Point(450, 8) };
            btnSave.SetAccentColor(AppTheme.AccentGreen);
            btnSave.Click += BtnSave_Click;
            topBar.Controls.Add(btnSave);

            var btnCancel = new FlatButton { Text = "Cancel", Size = new Size(80, 32), Location = new Point(548, 8) };
            btnCancel.SetGhostStyle();
            btnCancel.Click += BtnCancel_Click;
            topBar.Controls.Add(btnCancel);

            var btnBackup = new FlatButton { Text = "\u25A8  Backup", Size = new Size(100, 32), Location = new Point(636, 8) };
            btnBackup.SetWarningStyle();
            btnBackup.Click += (s, e) => BackupClicked?.Invoke(this, EventArgs.Empty);
            topBar.Controls.Add(btnBackup);

            var btnResetDb = new FlatButton { Text = "Reset Local DB", Size = new Size(148, 32), Location = new Point(744, 8) };
            btnResetDb.SetDangerStyle();
            btnResetDb.Click += BtnResetLocalDb_Click;
            topBar.Controls.Add(btnResetDb);

            y += 60;

            // ═══ TENANT CONNECTION ═══
            y = AddSection("TENANT CONNECTION", AppTheme.AccentCyan, y);
            var tenantCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 100), AccentLeft = AppTheme.AccentCyan };
            Controls.Add(tenantCard);
            int ty = 14;
            AddFieldInCard(tenantCard, "Admin URL:", ref _txtAdminUrl, lblW, inW, ty);
            tenantCard.Controls.Add(new Label
            {
                Text = "https://contoso-admin.sharepoint.com",
                Font = new Font("Cascadia Code", 6.5f),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(lblW + inW + 22, ty + 2)
            });
            ty += rowH + 4;
            AddFieldInCard(tenantCard, "Tenant ID:", ref _txtTenantId, lblW, inW, ty);
            var btnResolve = new FlatButton { Text = "Resolve", Size = new Size(64, 22), Location = new Point(lblW + inW + 18, ty - 1) };
            btnResolve.SetGhostStyle();
            btnResolve.Click += BtnResolveTenantId_Click;
            tenantCard.Controls.Add(btnResolve);
            tenantCard.Controls.Add(new Label
            {
                Text = "Auto-resolved from Admin URL",
                Font = new Font("Cascadia Code", 6.5f),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(lblW + inW + 88, ty + 2)
            });
            y += 100 + sectionGap;

            // ═══ ENTRA ID ═══
            y = AddSection("ENTRA ID APP REGISTRATION", AppTheme.AccentCyan, y);
            var entraCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 96), AccentLeft = AppTheme.AccentCyan };
            Controls.Add(entraCard);
            int ey = 14;
            AddFieldInCard(entraCard, "Client ID:", ref _txtClientId, lblW, inW, ey); ey += rowH + 4;
            AddFieldInCard(entraCard, "Certificate Thumbprint:", ref _txtCertThumb, lblW, inW, ey);
            y += 96 + sectionGap;

            // ═══ PURVIEW ═══
            y = AddSection("PURVIEW APP (Optional)", AppTheme.AccentPurple, y);
            var purviewCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 130), AccentLeft = AppTheme.AccentPurple };
            Controls.Add(purviewCard);
            int py = 14;
            AddFieldInCard(purviewCard, "Client ID:", ref _txtPurviewClientId, lblW, inW, py); py += rowH + 4;
            AddFieldInCard(purviewCard, "Certificate Thumbprint:", ref _txtPurviewCertThumb, lblW, inW, py); py += rowH + 4;
            AddFieldInCard(purviewCard, "Organization:", ref _txtPurviewOrg, lblW, inW, py);
            y += 130 + sectionGap;

            // ═══ DASHBOARD ═══
            y = AddSection("DASHBOARD SETTINGS", AppTheme.AccentGold, y);
            var dashCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 240), AccentLeft = AppTheme.AccentGold };
            Controls.Add(dashCard);
            int dy = 14;

            AddLblInCard(dashCard, "Language:", 14, dy, lblW);
            _cmbLanguage = AddCmbInCard(dashCard, lblW + 14, dy, 140, new[] { "en", "pt-br", "es", "fr", "de", "it", "ja", "ko", "zh" });
            dy += rowH + 4;

            AddLblInCard(dashCard, "Currency Symbol:", 14, dy, 120);
            _txtCurrencySymbol = AddTxtInCard(dashCard, 140, dy, 60);
            AddLblInCard(dashCard, "Code:", 210, dy, 40);
            _txtCurrencyCode = AddTxtInCard(dashCard, 256, dy, 70);
            AddLblInCard(dashCard, "Cost per TB/Year (USD):", 350, dy, 170);
            _txtCostTBYear = AddTxtInCard(dashCard, 526, dy, 120);
            dy += rowH + 4;

            AddLblInCard(dashCard, "Date Format:", 14, dy, lblW);
            _cmbDateFormat = AddCmbInCard(dashCard, lblW + 14, dy, 160, new[] { "MM/dd/yyyy", "dd/MM/yyyy", "yyyy-MM-dd" });
            dy += rowH + 4;

            AddLblInCard(dashCard, "Dashboard Port:", 14, dy, lblW);
            _txtPort = AddTxtInCard(dashCard, lblW + 14, dy, 80);
            dy += rowH + 4;

            AddLblInCard(dashCard, "Dashboard Launch:", 14, dy, lblW);
            _cmbDashboardLaunchMode = AddCmbInCard(dashCard, lblW + 14, dy, 180, new[] { "App HTTP Server", "PowerShell Script" });
            dy += rowH + 4;

            y += 240 + sectionGap;

            // ═══ EXECUTION RULES ═══
            y = AddSection("EXECUTION RE-RUN RULES", AppTheme.AccentGold, y);
            var rulesCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 126), AccentLeft = AppTheme.AccentGold };
            Controls.Add(rulesCard);
            int ry = 14;

            AddLblInCard(rulesCard, "Empty / Zero-Version Action:", 14, ry, lblW);
            _cmbZeroVersion = AddCmbInCard(rulesCard, lblW + 14, ry, 160, new[] { "ask", "skip", "syncOnly", "both" });
            rulesCard.Controls.Add(new Label
            {
                Text = "Sites/folders with VersionCount=0 and VersionSize=0",
                Font = new Font("Cascadia Code", 6.5f),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(lblW + 180, ry + 4)
            });
            ry += rowH + 6;

            AddLblInCard(rulesCard, "Re-execution Days:", 14, ry, lblW);
            _txtReexecutionDays = AddTxtInCard(rulesCard, lblW + 14, ry, 80);
            rulesCard.Controls.Add(new Label
            {
                Text = "0 = disabled, ask = prompt, 1-365 = skip recently processed sites",
                Font = new Font("Cascadia Code", 6.5f),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(lblW + 102, ry + 4)
            });
            y += 126 + sectionGap;

            // ═══ EXECUTION DIRECTORIES ═══
            y = AddSection("EXECUTION DIRECTORIES", AppTheme.AccentCyan, y);
            var dirCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 370), AccentLeft = AppTheme.AccentCyan };
            Controls.Add(dirCard);
            int dirY = 14;
            int browseW = 30;
            int txtDirW = 260;

            AddLblInCard(dirCard, "Root Directory:", 14, dirY, lblW);
            _txtRootPath = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtRootPath, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 4;

            AddLblInCard(dirCard, "Application Folder:", 14, dirY, lblW);
            _txtApplicationFolder = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtApplicationFolder, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 4;

            AddLblInCard(dirCard, "Logs Subfolder:", 14, dirY, lblW);
            _txtLogsFolder = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtLogsFolder, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 4;

            AddLblInCard(dirCard, "Backup Subfolder:", 14, dirY, lblW);
            _txtBackupFolder = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtBackupFolder, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 4;

            AddLblInCard(dirCard, "Config Folder:", 14, dirY, lblW);
            _txtConfigFolder = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtConfigFolder, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 4;

            AddLblInCard(dirCard, "Web Folder:", 14, dirY, lblW);
            _txtWebFolder = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtWebFolder, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 4;

            AddLblInCard(dirCard, "App Folder:", 14, dirY, lblW);
            _txtAppFolder = AddTxtInCard(dirCard, lblW + 14, dirY, txtDirW);
            AddBrowseButton(dirCard, _txtAppFolder, lblW + 14 + txtDirW + 6, dirY);
            dirY += rowH + 10;

            var previewCard = new Panel
            {
                Location = new Point(14, dirY),
                Size = new Size(852, 90),
                BackColor = Color.FromArgb(24, 255, 255, 255)
            };
            dirCard.Controls.Add(previewCard);

            previewCard.Controls.Add(new Label
            {
                Text = "Calculated Full Paths",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(8, 6)
            });

            _lblPathPreviewRoot = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(10, 24) };
            _lblPathPreviewLogs = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(10, 38) };
            _lblPathPreviewBackup = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(10, 52) };
            _lblPathPreviewJobStatus = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(10, 66) };
            _lblPathPreviewConfig = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(430, 24) };
            _lblPathPreviewWeb = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(430, 38) };
            _lblPathPreviewApp = new Label { Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(430, 52) };
            previewCard.Controls.Add(_lblPathPreviewRoot);
            previewCard.Controls.Add(_lblPathPreviewLogs);
            previewCard.Controls.Add(_lblPathPreviewBackup);
            previewCard.Controls.Add(_lblPathPreviewJobStatus);
            previewCard.Controls.Add(_lblPathPreviewConfig);
            previewCard.Controls.Add(_lblPathPreviewWeb);
            previewCard.Controls.Add(_lblPathPreviewApp);

            _txtRootPath.TextChanged += (s, e) => UpdateDirectoryPreview();
            _txtApplicationFolder.TextChanged += (s, e) => UpdateDirectoryPreview();
            _txtLogsFolder.TextChanged += (s, e) => UpdateDirectoryPreview();
            _txtBackupFolder.TextChanged += (s, e) => UpdateDirectoryPreview();
            _txtConfigFolder.TextChanged += (s, e) => UpdateDirectoryPreview();
            _txtWebFolder.TextChanged += (s, e) => UpdateDirectoryPreview();
            _txtAppFolder.TextChanged += (s, e) => UpdateDirectoryPreview();

            y += 370 + sectionGap;

            // ═══ AUTO-UPDATE ═══
            y = AddSection("AUTO-UPDATE", AppTheme.AccentCyan, y);
            var updateCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 64) };
            Controls.Add(updateCard);
            AddLblInCard(updateCard, "GitHub Repository:", 14, 14, lblW);
            _txtGitHubRepo = AddTxtInCard(updateCard, lblW + 14, 14, inW);
            updateCard.Controls.Add(new Label { Text = "owner/repo (e.g., ivanoliv/SPOVersionManagement)", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(lblW + 14, 38) });
            y += 64 + sectionGap;

            // ═══ TELEMETRY ═══
            y = AddSection("ANONYMOUS TELEMETRY", AppTheme.AccentGreen, y);
            var telCard = new GlassPanel { Location = new Point(0, y), Size = new Size(880, 130), AccentLeft = AppTheme.AccentGreen };
            Controls.Add(telCard);

            _chkTelemetry = new CheckBox
            {
                Text = "  Enable anonymous usage statistics (opt-in)",
                Font = AppTheme.FontBody, ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent,
                AutoSize = true, Location = new Point(14, 14)
            };
            _chkTelemetry.CheckedChanged += (s, e) => { _txtTelemetryEndpoint.Enabled = _chkTelemetry.Checked; _isDirty = true; };
            telCard.Controls.Add(_chkTelemetry);

            telCard.Controls.Add(new Label { Text = "No PII collected. One-way hash only (TenantId + local secret salt) and aggregate metrics.", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(36, 38) });

            var btnPreview = new FlatButton { Text = "Preview payload", Size = new Size(130, 26), Location = new Point(14, 58) };
            btnPreview.SetGhostStyle();
            btnPreview.Click += BtnPreviewTelemetry_Click;
            telCard.Controls.Add(btnPreview);

            AddLblInCard(telCard, "Telemetry Endpoint:", 14, 92, lblW);
            _txtTelemetryEndpoint = AddTxtInCard(telCard, lblW + 14, 92, inW);
            y += 130 + sectionGap;

            // Permission warning
            if (!_config.HasWritePermission)
            {
                y = AddSection("PERMISSION WARNING", AppTheme.AccentRed, y);
                Controls.Add(ML(_config.PermissionMessage, AppTheme.FontSmall, AppTheme.AccentRed, 8, y));
                y += 20;
                Controls.Add(ML("Suggested: " + _config.GetUserDataFolderSuggestion(), AppTheme.FontSmall, AppTheme.TextMuted, 8, y));
                y += 24;
                var btnSwitch = new FlatButton { Text = "Switch to User Folder", Size = new Size(170, 28), Location = new Point(8, y) };
                btnSwitch.SetWarningStyle();
                btnSwitch.Click += (s, e) => { _config.SwitchToUserDataFolder(); Initialize(_config); };
                Controls.Add(btnSwitch);
            }

            // Bottom spacer for scroll breathing room
            Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(10, 40), BackColor = Color.Transparent });
        }

        private void LoadValues()
        {
            var ac = _config.AppConfig;
            var dc = _config.DashboardConfig;
            _txtAdminUrl.Text = ac.AdminUrl ?? "";
            _txtTenantId.Text = ac.EntraIdApp?.TenantId ?? "";
            _txtClientId.Text = ac.EntraIdApp?.ClientId ?? "";
            _txtCertThumb.Text = ac.EntraIdApp?.CertificateThumbprint ?? "";
            _txtPurviewClientId.Text = ac.PurviewApp?.ClientId ?? "";
            _txtPurviewCertThumb.Text = ac.PurviewApp?.CertificateThumbprint ?? "";
            _txtPurviewOrg.Text = ac.PurviewApp?.Organization ?? "";
            SelectCombo(_cmbLanguage, dc.Language ?? "en");
            _txtCurrencySymbol.Text = dc.Currency?.Symbol ?? "$";
            _txtCurrencyCode.Text = dc.Currency?.Code ?? "USD";
            _txtCostTBYear.Text = dc.CostPerTBYear.ToString("F2");
            SelectCombo(_cmbDateFormat, dc.DateFormat ?? "MM/dd/yyyy");
            SelectCombo(_cmbZeroVersion, dc.ZeroVersionAction ?? "ask");
            _txtReexecutionDays.Text = dc.ReexecutionDays != null ? dc.ReexecutionDays.ToString() : "0";
            _txtPort.Text = (dc.DashboardPort > 0 ? dc.DashboardPort : 8080).ToString();
            _txtRootPath.Text = ac.RootPath ?? _config.RootPath;
            _txtApplicationFolder.Text = ac.ApplicationFolder ?? "SPOVersionManagement";
            _txtLogsFolder.Text = ac.Directories?.Logs ?? "Logs";
            _txtBackupFolder.Text = ac.Directories?.Backup ?? @"Logs\Backup";
            _txtConfigFolder.Text = ac.Directories?.Config ?? "config";
            _txtWebFolder.Text = ac.Directories?.Web ?? "web";
            _txtAppFolder.Text = ac.Directories?.App ?? "app";
            SelectCombo(_cmbDashboardLaunchMode,
                string.Equals(dc.DashboardLaunchMode, "powershell", StringComparison.OrdinalIgnoreCase)
                    ? "PowerShell Script"
                    : "App HTTP Server");
            _txtGitHubRepo.Text = ac.GitHubRepo ?? "";
            _chkTelemetry.Checked = ac.TelemetryEnabled;
            _txtTelemetryEndpoint.Text = ac.TelemetryEndpoint ?? "";
            _txtTelemetryEndpoint.Enabled = _chkTelemetry.Checked;
            UpdateDirectoryPreview();
            _isDirty = false;
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            try
            {
                _config.AppConfig.AdminUrl = _txtAdminUrl.Text.Trim();
                if (_config.AppConfig.EntraIdApp == null) _config.AppConfig.EntraIdApp = new EntraIdAppConfig();
                _config.AppConfig.EntraIdApp.TenantId = _txtTenantId.Text.Trim();
                _config.AppConfig.EntraIdApp.ClientId = _txtClientId.Text.Trim();
                _config.AppConfig.EntraIdApp.CertificateThumbprint = _txtCertThumb.Text.Trim();

                // Auto-resolve TenantId and Purview Org from AdminUrl if empty
                AutoResolveFromAdminUrl();
                if (_config.AppConfig.PurviewApp == null) _config.AppConfig.PurviewApp = new PurviewAppConfig();
                _config.AppConfig.PurviewApp.ClientId = _txtPurviewClientId.Text.Trim();
                _config.AppConfig.PurviewApp.CertificateThumbprint = _txtPurviewCertThumb.Text.Trim();
                _config.AppConfig.PurviewApp.Organization = _txtPurviewOrg.Text.Trim();
                _config.AppConfig.GitHubRepo = _txtGitHubRepo.Text.Trim();
                _config.AppConfig.TelemetryEndpoint = _txtTelemetryEndpoint.Text.Trim();
                _config.AppConfig.TelemetryEnabled = _chkTelemetry.Checked;
                _config.AppConfig.RootPath = _txtRootPath.Text.Trim();
                _config.AppConfig.ApplicationFolder = _txtApplicationFolder.Text.Trim();
                if (_config.AppConfig.Directories == null) _config.AppConfig.Directories = new DirectoryPaths();
                _config.AppConfig.Directories.Logs = _txtLogsFolder.Text.Trim();
                _config.AppConfig.Directories.Backup = _txtBackupFolder.Text.Trim();
                _config.AppConfig.Directories.Config = _txtConfigFolder.Text.Trim();
                _config.AppConfig.Directories.Web = _txtWebFolder.Text.Trim();
                _config.AppConfig.Directories.App = _txtAppFolder.Text.Trim();
                _config.DashboardConfig.Language = _cmbLanguage.SelectedItem?.ToString() ?? "en";
                if (_config.DashboardConfig.Currency == null) _config.DashboardConfig.Currency = new CurrencyConfig();
                _config.DashboardConfig.Currency.Symbol = _txtCurrencySymbol.Text.Trim();
                _config.DashboardConfig.Currency.Code = _txtCurrencyCode.Text.Trim();
                decimal cost; if (decimal.TryParse(_txtCostTBYear.Text, out cost)) _config.DashboardConfig.CostPerTBYear = cost;
                _config.DashboardConfig.DateFormat = _cmbDateFormat.SelectedItem?.ToString() ?? "MM/dd/yyyy";
                _config.DashboardConfig.ZeroVersionAction = _cmbZeroVersion.SelectedItem?.ToString() ?? "ask";
                _config.DashboardConfig.ReexecutionDays = NormalizeReexecutionDays(_txtReexecutionDays.Text);
                int port; if (int.TryParse(_txtPort.Text, out port) && port > 0 && port <= 65535) _config.DashboardConfig.DashboardPort = port;
                _config.DashboardConfig.DashboardLaunchMode =
                    string.Equals(_cmbDashboardLaunchMode.SelectedItem?.ToString(), "PowerShell Script", StringComparison.OrdinalIgnoreCase)
                        ? "powershell"
                        : "app";
                _config.SaveAppConfig();
                _config.SaveDashboardConfig();
                _isDirty = false;
                StatusMessage?.Invoke(this, "Configuration saved.");
            }
            catch (Exception ex) { MessageBox.Show($"Save error:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void BtnCancel_Click(object sender, EventArgs e)
        {
            if (_isDirty && MessageBox.Show("Discard changes?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            _config.Load();
            LoadValues();
            StatusMessage?.Invoke(this, "Changes discarded.");
        }

        /// <summary>
        /// Manual "Resolve" button — always attempts resolution regardless of current field values.
        /// </summary>
        private void BtnResolveTenantId_Click(object sender, EventArgs e)
        {
            string adminUrl = _txtAdminUrl.Text.Trim();
            if (string.IsNullOrWhiteSpace(adminUrl))
            {
                MessageBox.Show("Enter the Admin URL first.", "Resolve Tenant ID", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                var uri = new Uri(adminUrl.TrimEnd('/'));
                string host = uri.Host;
                string tenantName = host.Split('.')[0].Replace("-admin", "");

                // Fill Purview Organization
                _txtPurviewOrg.Text = tenantName;

                // Resolve TenantId via Azure AD OpenID discovery
                string tenantDomain = $"{tenantName}.onmicrosoft.com";
                string discoveryUrl = $"https://login.microsoftonline.com/{tenantDomain}/.well-known/openid-configuration";

                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    string json = http.GetStringAsync(discoveryUrl).GetAwaiter().GetResult();
                    var match = System.Text.RegularExpressions.Regex.Match(json,
                        @"""issuer""\s*:\s*""https://sts\.windows\.net/([0-9a-fA-F\-]{36})/?""");
                    if (match.Success)
                    {
                        _txtTenantId.Text = match.Groups[1].Value;
                        StatusMessage?.Invoke(this, $"Tenant ID resolved: {match.Groups[1].Value}");
                    }
                    else
                    {
                        MessageBox.Show($"Could not resolve Tenant ID from '{tenantDomain}'.\nVerify the Admin URL is correct.", "Resolve Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error resolving Tenant ID:\n{ex.Message}", "Resolve Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Auto-resolves TenantId (via Azure AD discovery) and Purview Organization
        /// from AdminUrl if those fields are empty. Updates both config and UI.
        /// </summary>
        private void AutoResolveFromAdminUrl()
        {
            string adminUrl = _config.AppConfig.AdminUrl;
            if (string.IsNullOrWhiteSpace(adminUrl))
                return;

            try
            {
                var uri = new Uri(adminUrl.TrimEnd('/'));
                string host = uri.Host; // e.g. "contoso-admin.sharepoint.com"
                string tenantName = host.Split('.')[0].Replace("-admin", "");

                // Auto-fill Purview Organization if empty
                if (_config.AppConfig.PurviewApp != null &&
                    string.IsNullOrWhiteSpace(_config.AppConfig.PurviewApp.Organization))
                {
                    _config.AppConfig.PurviewApp.Organization = tenantName;
                    _txtPurviewOrg.Text = tenantName;
                }

                // Auto-resolve TenantId via Azure AD OpenID discovery if empty
                if (string.IsNullOrWhiteSpace(_config.AppConfig.EntraIdApp?.TenantId))
                {
                    string tenantDomain = $"{tenantName}.onmicrosoft.com";
                    string discoveryUrl = $"https://login.microsoftonline.com/{tenantDomain}/.well-known/openid-configuration";

                    using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                    {
                        string json = http.GetStringAsync(discoveryUrl).GetAwaiter().GetResult();
                        var match = System.Text.RegularExpressions.Regex.Match(json,
                            @"""issuer""\s*:\s*""https://sts\.windows\.net/([0-9a-fA-F\-]{36})/?""");
                        if (match.Success)
                        {
                            string resolved = match.Groups[1].Value;
                            _config.AppConfig.EntraIdApp.TenantId = resolved;
                            _txtTenantId.Text = resolved;
                            StatusMessage?.Invoke(this, $"Tenant ID auto-resolved: {resolved}");
                        }
                    }
                }
            }
            catch { /* best-effort — user can always fill manually */ }
        }

        private void BtnPreviewTelemetry_Click(object sender, EventArgs e)
        {
            var sample = new { tenantHash = "(SHA256 of TenantId)", appVersion = _config.AppConfig.AppVersion ?? "2.x", storageFreedBytes = 536870912, versionsDeleted = 1250, sitesProcessed = 47, timestamp = DateTime.UtcNow.ToString("o") };
            string json = JsonConvert.SerializeObject(sample, Formatting.Indented);
            var dlg = new Form { Text = "Telemetry Preview", Size = new Size(440, 320), StartPosition = FormStartPosition.CenterParent, BackColor = AppTheme.BgDark, ForeColor = AppTheme.TextPrimary, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
            dlg.Controls.Add(new Label { Text = "This is what gets sent after each session:", Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = true, Location = new Point(16, 12) });
            dlg.Controls.Add(new TextBox { Multiline = true, ReadOnly = true, Text = json, Font = AppTheme.FontMono, BackColor = AppTheme.BgInput, ForeColor = AppTheme.AccentGreen, Location = new Point(16, 34), Size = new Size(392, 200), ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.FixedSingle });
            dlg.Controls.Add(new Label { Text = "No PII. SHA256 hash is irreversible.", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentGold, AutoSize = true, Location = new Point(16, 244) });
            dlg.ShowDialog(ParentForm);
        }

        private void BtnResetLocalDb_Click(object sender, EventArgs e)
        {
            string resetType = ShowResetLocalDbDialog(ParentForm);
            if (resetType == null) return;
            PerformReset(resetType);
        }

        /// <summary>
        /// Shows the "Reset Local Database" dialog and returns the chosen reset type
        /// ("sites" / "tenant" / "both") or null if the user cancelled. Does not perform the reset itself.
        /// Reusable across panels so the same UI is shown whenever a reset is needed.
        /// </summary>
        public static string ShowResetLocalDbDialog(IWin32Window owner)
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Reset Local Database";
                dlg.ClientSize = new Size(640, 400);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = AppTheme.BgDark;
                dlg.ForeColor = AppTheme.TextPrimary;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                int clientW = dlg.ClientSize.Width;
                int clientH = dlg.ClientSize.Height;

                dlg.Controls.Add(new Label
                {
                    Text = "What would you like to reset?",
                    Font = AppTheme.FontHeading,
                    ForeColor = AppTheme.AccentRed,
                    AutoSize = true,
                    Location = new Point(18, 18)
                });

                dlg.Controls.Add(new Label
                {
                    Text = "Choose which local database to clear. This does not delete anything in the tenant.",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.TextSecondary,
                    AutoSize = false,
                    Size = new Size(clientW - 36, 38),
                    Location = new Point(18, 52)
                });

                var rbSitesOnly = new RadioButton
                {
                    Text = "Sites data only",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.TextPrimary,
                    AutoSize = true,
                    Location = new Point(36, 100),
                    Checked = true
                };
                dlg.Controls.Add(rbSitesOnly);

                dlg.Controls.Add(new Label
                {
                    Text = "Clears: site execution history, session history, job status, execution history",
                    Font = AppTheme.FontSmall,
                    ForeColor = AppTheme.TextSecondary,
                    AutoSize = true,
                    Location = new Point(56, 126)
                });

                var rbTenantOnly = new RadioButton
                {
                    Text = "Tenant data only",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.TextPrimary,
                    AutoSize = true,
                    Location = new Point(36, 150)
                };
                dlg.Controls.Add(rbTenantOnly);

                dlg.Controls.Add(new Label
                {
                    Text = "Clears: tenant storage tracking and timeline",
                    Font = AppTheme.FontSmall,
                    ForeColor = AppTheme.TextSecondary,
                    AutoSize = true,
                    Location = new Point(56, 176)
                });

                var rbBoth = new RadioButton
                {
                    Text = "Both sites and tenant data",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.TextPrimary,
                    AutoSize = true,
                    Location = new Point(36, 200)
                };
                dlg.Controls.Add(rbBoth);

                dlg.Controls.Add(new Label
                {
                    Text = "Clears: all of the above",
                    Font = AppTheme.FontSmall,
                    ForeColor = AppTheme.TextSecondary,
                    AutoSize = true,
                    Location = new Point(56, 226)
                });

                dlg.Controls.Add(new Label
                {
                    Text = "Type YES to confirm:",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.TextPrimary,
                    AutoSize = true,
                    Location = new Point(18, clientH - 116)
                });

                var txtConfirm = new TextBox { Location = new Point(18, clientH - 92), Size = new Size(180, 22) };
                AppTheme.StyleTextBox(txtConfirm);
                dlg.Controls.Add(txtConfirm);

                var btnCancel = new FlatButton
                {
                    Text = "Cancel",
                    Size = new Size(90, 30),
                    Location = new Point(clientW - 230, clientH - 42),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };
                btnCancel.SetGhostStyle();
                btnCancel.Click += (s, ev) => dlg.DialogResult = DialogResult.Cancel;
                dlg.Controls.Add(btnCancel);

                var btnConfirm = new FlatButton
                {
                    Text = "Yes, confirm",
                    Size = new Size(120, 30),
                    Location = new Point(clientW - 132, clientH - 42),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };
                btnConfirm.SetDangerStyle();
                btnConfirm.Click += (s, ev) =>
                {
                    if (!string.Equals(txtConfirm.Text?.Trim(), "YES", StringComparison.OrdinalIgnoreCase))
                    {
                        MessageBox.Show(dlg, "Type YES to confirm this reset.", "Confirmation Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    dlg.DialogResult = DialogResult.OK;
                };
                dlg.Controls.Add(btnConfirm);

                if (dlg.ShowDialog(owner) != DialogResult.OK)
                    return null;

                return rbSitesOnly.Checked ? "sites" : (rbTenantOnly.Checked ? "tenant" : "both");
            }
        }

        private void PerformReset(string resetType)
        {
            try
            {
                int cleared = _config.ResetLocalExecutionDatabases(resetType);
                
                string dataType = resetType == "sites" ? "sites" : (resetType == "tenant" ? "tenant" : "sites and tenant");
                string details = resetType == "sites" 
                    ? "site execution history, session history, job status, and execution history"
                    : (resetType == "tenant" 
                        ? "tenant storage tracking and timeline"
                        : "all site and tenant data");

                StatusMessage?.Invoke(this, $"Local {dataType} data reset. {cleared} file(s) cleared.");
                DatabaseResetCompleted?.Invoke(this, resetType);
                MessageBox.Show(ParentForm,
                    $"Local {dataType} data was reset successfully.\n\nCleared: {details}\nNothing was deleted from the tenant.",
                    "Reset Completed",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ParentForm, $"Reset failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #region Helpers
        private int AddSection(string text, Color accent, int y)
        {
            Controls.Add(new Label { Text = text, Font = AppTheme.FontHeading, ForeColor = accent, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, y) });
            Controls.Add(new Panel { BackColor = accent, Location = new Point(0, y + 18), Size = new Size(40, 2) });
            return y + 28;
        }

        private int AddField(string label, ref TextBox txt, int lblW, int inW, int y)
        {
            AddLbl(label, 0, y, lblW);
            txt = AddTxt(lblW, y, inW);
            return y;
        }

        private void AddFieldInCard(Control card, string label, ref TextBox txt, int lblW, int inW, int y)
        {
            AddLblInCard(card, label, 14, y, lblW);
            txt = AddTxtInCard(card, lblW + 14, y, inW);
        }

        private void AddLbl(string text, int x, int y, int w)
        {
            Controls.Add(new Label { Text = text, Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = false, Size = new Size(w, 22), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent, Location = new Point(x, y) });
        }

        private void AddLblInCard(Control card, string text, int x, int y, int w)
        {
            card.Controls.Add(new Label { Text = text, Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = false, Size = new Size(w, 22), TextAlign = ContentAlignment.MiddleRight, BackColor = Color.Transparent, Location = new Point(x, y) });
        }

        private TextBox AddTxt(int x, int y, int w)
        {
            var txt = new TextBox { Location = new Point(x, y), Size = new Size(w, 22) };
            AppTheme.StyleTextBox(txt);
            txt.TextChanged += (s, e) => _isDirty = true;
            Controls.Add(txt);
            return txt;
        }

        private TextBox AddTxtInCard(Control card, int x, int y, int w)
        {
            var txt = new TextBox { Location = new Point(x, y), Size = new Size(w, 22) };
            AppTheme.StyleTextBox(txt);
            txt.TextChanged += (s, e) => _isDirty = true;
            card.Controls.Add(txt);
            return txt;
        }

        private void AddBrowseButton(Control card, TextBox target, int x, int y)
        {
            var btn = new Button
            {
                Text = "\u2026",
                Size = new Size(30, 22),
                Location = new Point(x, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderColor = AppTheme.Border;
            btn.FlatAppearance.BorderSize = 1;
            btn.Click += (s, e) =>
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    // Resolve current value to full path for initial selection
                    string currentFull = ResolveToFullPath(target.Text);
                    if (!string.IsNullOrEmpty(currentFull) && Directory.Exists(currentFull))
                        dlg.SelectedPath = currentFull;

                    if (dlg.ShowDialog() == DialogResult.OK)
                    {
                        // Root directory always stores full path
                        if (target == _txtRootPath)
                        {
                            target.Text = dlg.SelectedPath;
                        }
                        else
                        {
                            // Make relative if inside root, otherwise keep full
                            target.Text = MakeRelativeIfInRoot(dlg.SelectedPath);
                        }
                    }
                }
            };
            card.Controls.Add(btn);
        }

        private string GetEffectiveRoot()
        {
            string root = _txtRootPath?.Text?.Trim();
            if (string.IsNullOrEmpty(root))
                root = _config.RootPath;
            return root;
        }

        private string ResolveToFullPath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (Path.IsPathRooted(value)) return value;
            string root = GetEffectiveRoot();
            if (string.IsNullOrEmpty(root)) return value;
            return Path.GetFullPath(Path.Combine(root, value));
        }

        private string MakeRelativeIfInRoot(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath)) return fullPath;
            string root = GetEffectiveRoot();
            if (string.IsNullOrEmpty(root)) return fullPath;

            // Normalize both paths for comparison
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedPath = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar);

            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                string relative = normalizedPath.Substring(normalizedRoot.Length);
                return string.IsNullOrEmpty(relative) ? "." : relative;
            }

            // Outside root — keep full path
            return fullPath;
        }

        private ComboBox AddCmb(int x, int y, int w, string[] items)
        {
            var cmb = new ComboBox { Location = new Point(x, y), Size = new Size(w, 22), DropDownStyle = ComboBoxStyle.DropDownList };
            cmb.Items.AddRange(items);
            AppTheme.StyleComboBox(cmb);
            cmb.SelectedIndexChanged += (s, e) => _isDirty = true;
            Controls.Add(cmb);
            return cmb;
        }

        private ComboBox AddCmbInCard(Control card, int x, int y, int w, string[] items)
        {
            var cmb = new ComboBox { Location = new Point(x, y), Size = new Size(w, 22), DropDownStyle = ComboBoxStyle.DropDownList };
            cmb.Items.AddRange(items);
            AppTheme.StyleComboBox(cmb);
            cmb.SelectedIndexChanged += (s, e) => _isDirty = true;
            card.Controls.Add(cmb);
            return cmb;
        }

        private void SelectCombo(ComboBox cmb, string val)
        {
            int idx = cmb.Items.IndexOf(val);
            cmb.SelectedIndex = idx >= 0 ? idx : 0;
        }

        private static object NormalizeReexecutionDays(string raw)
        {
            string value = (raw ?? string.Empty).Trim();
            if (string.Equals(value, "ask", StringComparison.OrdinalIgnoreCase))
                return "ask";

            int days;
            if (!int.TryParse(value, out days))
                return 0;

            if (days < 0)
                days = 0;
            if (days > 365)
                days = 365;

            return days;
        }

        private void UpdateDirectoryPreview()
        {
            if (_lblPathPreviewRoot == null)
                return;

            string rootDir = (_txtRootPath?.Text ?? string.Empty).Trim();
            string appFolder = (_txtApplicationFolder?.Text ?? string.Empty).Trim();
            string logsFolder = (_txtLogsFolder?.Text ?? string.Empty).Trim();
            string backupFolder = (_txtBackupFolder?.Text ?? string.Empty).Trim();
            string configFolder = (_txtConfigFolder?.Text ?? string.Empty).Trim();
            string webFolder = (_txtWebFolder?.Text ?? string.Empty).Trim();
            string appDirFolder = (_txtAppFolder?.Text ?? string.Empty).Trim();

            string appRoot = string.IsNullOrWhiteSpace(rootDir)
                ? appFolder
                : Path.Combine(rootDir, appFolder);

            string logsPath = Path.IsPathRooted(logsFolder)
                ? logsFolder
                : Path.Combine(appRoot, logsFolder);

            string backupPath = Path.IsPathRooted(backupFolder)
                ? backupFolder
                : Path.Combine(appRoot, backupFolder);

            string configPath = Path.IsPathRooted(configFolder)
                ? configFolder
                : Path.Combine(appRoot, configFolder);

            string webPath = Path.IsPathRooted(webFolder)
                ? webFolder
                : Path.Combine(appRoot, webFolder);

            string appDirPath = Path.IsPathRooted(appDirFolder)
                ? appDirFolder
                : Path.Combine(appRoot, appDirFolder);

            string jobStatusFile = _config?.AppConfig?.Files?.JobStatus ?? "JobStatus.json";
            string jobStatusPath = Path.Combine(logsPath, jobStatusFile);

            _lblPathPreviewRoot.Text = "Root: " + appRoot;
            _lblPathPreviewLogs.Text = "Logs: " + logsPath;
            _lblPathPreviewBackup.Text = "Backup: " + backupPath;
            _lblPathPreviewJobStatus.Text = "JobStatus: " + jobStatusPath;
            if (_lblPathPreviewConfig != null) _lblPathPreviewConfig.Text = "Config: " + configPath;
            if (_lblPathPreviewWeb != null) _lblPathPreviewWeb.Text = "Web: " + webPath;
            if (_lblPathPreviewApp != null) _lblPathPreviewApp.Text = "App: " + appDirPath;
        }

        private Label ML(string text, Font font, Color color, int x, int y)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) };
        }
        #endregion
    }
}
