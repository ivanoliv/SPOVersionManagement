using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SPOVersionManagement.Controls;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Forms
{
    public class MainForm : Form
    {
        private readonly string _rootPath;

        // Services
        private ConfigurationService _configService;
        private ExecutionHistoryService _historyService;
        private PowerShellHostService _psHost;
        private TelemetryService _telemetry;

        // Layout
        private SidebarControl _sidebar;
        private NotificationBar _notificationBar;
        private StatusBarControl _statusBar;
        private Panel _contentArea;

        // Panels (lazy-loaded)
        private HomePanel _homePanel;
        private PreReqsPanel _preReqsPanel;
        private ConfigurationPanel _configPanel;
        private SiteManagementPanel _sitesPanel;
        private ExecutionPanel _executionPanel;
        private DataSyncPanel _dataSyncPanel;
        private HistoryPanel _historyPanel;
        private UpdatePanel _updatePanel;
        private FileArchivePanel _fileArchivePanel;
        private FileArchiveQueuePanel _fileArchiveQueuePanel;
        private HttpServerPanel _httpServerPanel;
        private RetentionPolicyPanel _retentionPolicyPanel;
        private DashboardHttpServerService _dashboardServer;

        private Control _activePanel;
        private string _activeKey = "home";

        public MainForm(string rootPath)
        {
            _rootPath = rootPath;
            InitializeForm();
            InitializeServices();
            BuildLayout();
            ShowPanel("home");
            LoadDataAsync();
        }

        private void InitializeForm()
        {
            Text = "SPO Version Management";
            int desiredWidth = Math.Max(AppTheme.FormWidth, 1700);
            int desiredHeight = Math.Max(AppTheme.FormHeight, 900);
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, desiredWidth, desiredHeight);

            int width = Math.Min(desiredWidth, Math.Max(1200, workingArea.Width - 12));
            int height = Math.Min(desiredHeight, Math.Max(720, workingArea.Height - 12));

            Size = new Size(width, height);
            MinimumSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = AppTheme.BgDark;
            ForeColor = AppTheme.TextPrimary;
            DoubleBuffered = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer, true);
        }

        private void InitializeServices()
        {
            _configService = new ConfigurationService(_rootPath);
            _historyService = new ExecutionHistoryService(_configService);
            _psHost = new PowerShellHostService(_rootPath);

            try { _psHost.Initialize(); } catch { }

            EnsureTelemetryConsentOnce();

            if (_configService.AppConfig.TelemetryEnabled &&
                !string.IsNullOrEmpty(_configService.AppConfig.TelemetryEndpoint) &&
                _configService.AppConfig.EntraIdApp != null)
            {
                _telemetry = new TelemetryService(
                    _configService.AppConfig.TelemetryEndpoint,
                    _configService.AppConfig.EntraIdApp.TenantId,
                    _configService.AppConfig.AppVersion,
                    _configService.AppConfig.TelemetrySalt);
            }
        }

        private void EnsureTelemetryConsentOnce()
        {
            try
            {
                var cfg = _configService.AppConfig;
                if (cfg == null)
                    return;

                if (cfg.TelemetryConsentRequested)
                    return;

                if (string.IsNullOrWhiteSpace(cfg.TelemetryEndpoint))
                    return;

                var dataPoints = new[]
                {
                    "tenantHash (SHA256 of TenantId + local secret salt)",
                    "appVersion",
                    "storageFreedBytes",
                    "versionsDeleted",
                    "sitesProcessed",
                    "executionMode (sync/delete/full)",
                    "sessionId",
                    "timestamp"
                };

                string message =
                    "Anonymous telemetry is disabled by default.\n\n" +
                    "Data that would be sent:\n - " + string.Join("\n - ", dataPoints) +
                    "\n\nNo site URLs, no usernames, no email addresses, and no raw Tenant ID are sent.\n\n" +
                    "Do you consent to enable anonymous telemetry now?\n" +
                    "(You will not be asked again unless all local data is reset.)";

                var choice = MessageBox.Show(
                    message,
                    "Telemetry Consent",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                cfg.TelemetryConsentRequested = true;
                cfg.TelemetryConsentRequestedAt = DateTime.UtcNow.ToString("o");
                cfg.TelemetryEnabled = choice == DialogResult.Yes;

                if (cfg.TelemetryEnabled && string.IsNullOrWhiteSpace(cfg.TelemetrySalt) && cfg.EntraIdApp != null)
                {
                    cfg.TelemetrySalt = TelemetryService.GenerateTenantSalt(cfg.EntraIdApp.TenantId);
                }

                _configService.SaveAppConfig();
            }
            catch
            {
            }
        }

        private void BuildLayout()
        {
            SuspendLayout();

            // ── Status Bar (bottom) ──
            _statusBar = new StatusBarControl();
            _statusBar.SetIndicator1("Cache: Ready", AppTheme.AccentGreen);
            _statusBar.SetIndicator2("Graph: Offline", AppTheme.AccentGold);
            _statusBar.SetSession("Session: None");
            _statusBar.SetMemory("");
            Controls.Add(_statusBar);

            // ── Notification Bar (top) ──
            _notificationBar = new NotificationBar();
            _notificationBar.Dock = DockStyle.Top;
            Controls.Add(_notificationBar);

            // ── Content Area (fill) ── must be added BEFORE sidebar so dock order is correct
            _contentArea = new BufferedPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(16, 12, 16, AppTheme.StatusBarHeight + 2)
            };
            _contentArea.Paint += (s, e) => AppTheme.PaintGradientBackground(e.Graphics, _contentArea.ClientRectangle);
            Controls.Add(_contentArea);

            // ── Sidebar (left) ── added after content so it docks first (WinForms z-order)
            _sidebar = new SidebarControl();
            _sidebar.SetVersion(_configService.AppConfig.AppVersion ?? "?");
            _sidebar.AddItem("\u2302", "Home", "home");
            _sidebar.AddItem("\u2714", "Pre reqs", "prereqs");
            _sidebar.AddItem("\u2699", "Config", "config");
            _sidebar.AddItem("\u268A", "HTTP Server", "httpserver");
            _sidebar.AddItem("\u2715", "Retention", "retention");
            _sidebar.AddItem("\u2637", "Sites", "sites");
            _sidebar.AddChild("sites", "Site Catalog", "sites.catalog");
            _sidebar.AddChild("sites", "Execution Scope", "sites.scope");
            _sidebar.AddChild("sites", "Archive Candidates", "sites.candidates");
            _sidebar.AddChild("sites", "Archived Sites", "sites.archived");
            _sidebar.AddChild("sites", "Archive Queue", "sites.queue");
            _sidebar.AddItem("\u25B6", "Execution", "exec");
            _sidebar.AddChild("exec", "Clean Versions", "exec.clean");
            _sidebar.AddChild("exec", "Data Sync", "exec.datasync");
            _sidebar.AddChild("exec", "Archive Sites", "exec.archive");
            _sidebar.AddChild("exec", "File Archive Explorer", "exec.filearchive");
            _sidebar.AddChild("exec", "File Archive Queue", "exec.filearchivequeue");
            _sidebar.AddItem("\u29D6", "History", "history");
            _sidebar.AddItem("\u21BB", "Updates", "updates");
            _sidebar.SelectedKey = "home";
            _sidebar.NavigationChanged += Sidebar_NavigationChanged;
            Controls.Add(_sidebar);

            _notificationBar.BringToFront();

            ResumeLayout(false);

            if (!_configService.HasWritePermission)
                _notificationBar.ShowWarning(_configService.PermissionMessage);

            UpdateStatusFromHistory();
        }

        private void Sidebar_NavigationChanged(object sender, string key)
        {
            ShowPanel(key);
        }

        private void ShowPanel(string key)
        {
            Control panel = null;
            string sitesView = null;

            if (key == "home") panel = GetHomePanel();
            else if (key == "prereqs") panel = GetPreReqsPanel();
            else if (key == "config") panel = GetConfigPanel();
            else if (key == "httpserver") panel = GetHttpServerPanel();
            else if (key == "retention") panel = GetRetentionPolicyPanel();
            else if (key.StartsWith("sites.")) { panel = GetSitesPanel(); sitesView = key.Substring(6); }
            else if (key == "exec.clean") panel = GetExecutionPanel();
            else if (key == "exec.datasync") panel = GetDataSyncPanel();
            else if (key == "exec.archive") { panel = GetSitesPanel(); sitesView = "queue"; }
            else if (key == "exec.filearchive") panel = GetFileArchivePanel();
            else if (key == "exec.filearchivequeue") panel = GetFileArchiveQueuePanel();
            else if (key == "history") panel = GetHistoryPanel();
            else if (key == "updates") panel = GetUpdatePanel();

            if (panel == null) return;

            _activeKey = key;

            bool panelChanged = panel != _activePanel;
            if (panelChanged)
            {
                _contentArea.SuspendLayout();
                if (_activePanel != null) _activePanel.Visible = false;
                if (!_contentArea.Controls.Contains(panel))
                {
                    panel.Dock = DockStyle.Fill;
                    _contentArea.Controls.Add(panel);
                }
                panel.Visible = true;
                _activePanel = panel;
                _contentArea.ResumeLayout(true);
            }

            if (sitesView != null && _sitesPanel != null)
                _sitesPanel.ShowView(sitesView);

            if (key == "home" && _homePanel != null) _homePanel.RefreshData();
        }

        #region Panel Factories
        private HomePanel GetHomePanel()
        {
            if (_homePanel == null)
            {
                _homePanel = new HomePanel();
                _homePanel.Initialize(_configService, _historyService);
                _homePanel.StartClicked += (s, e) => { _sidebar.SelectedKey = "exec.clean"; ShowPanel("exec.clean"); };
                _homePanel.DashboardClicked += (s, e) => LaunchDashboard();
                _homePanel.ConfigClicked += (s, e) => { _sidebar.SelectedKey = "config"; ShowPanel("config"); };
                _homePanel.HistoryClicked += (s, e) => { _sidebar.SelectedKey = "history"; ShowPanel("history"); };
                _homePanel.BackupClicked += (s, e) => DoBackup();
                _homePanel.SitesClicked += (s, e) => { _sidebar.SelectedKey = "sites.catalog"; ShowPanel("sites.catalog"); };
            }
            return _homePanel;
        }

        private ConfigurationPanel GetConfigPanel()
        {
            if (_configPanel == null)
            {
                _configPanel = new ConfigurationPanel();
                _configPanel.Initialize(_configService);
                _configPanel.BackupClicked += (s, e) => DoBackup();
                _configPanel.StatusMessage += (s, msg) => SetStatus(msg);
                _configPanel.DatabaseResetCompleted += ConfigPanel_DatabaseResetCompleted;
            }
            return _configPanel;
        }

        private void ConfigPanel_DatabaseResetCompleted(object sender, string resetType)
        {
            try
            {
                _homePanel?.RefreshData();
                _historyPanel?.RefreshData();
                _sitesPanel?.RefreshAfterReset();
                _fileArchiveQueuePanel?.RefreshQueue();
                _executionPanel?.RefreshAfterReset();
            }
            catch
            {
            }
        }

        private PreReqsPanel GetPreReqsPanel()
        {
            if (_preReqsPanel == null)
            {
                _preReqsPanel = new PreReqsPanel();
                _preReqsPanel.Initialize(_configService, _psHost);
                _preReqsPanel.StatusMessage += (s, msg) => SetStatus(msg);
            }
            else
            {
                _preReqsPanel.RefreshChecks();
            }

            return _preReqsPanel;
        }

        private SiteManagementPanel GetSitesPanel()
        {
            if (_sitesPanel == null)
            {
                _sitesPanel = new SiteManagementPanel();
                _sitesPanel.Initialize(_configService, _psHost, _historyService);
            }
            return _sitesPanel;
        }

        private ExecutionPanel GetExecutionPanel()
        {
            if (_executionPanel == null)
            {
                _executionPanel = new ExecutionPanel();
                _executionPanel.Initialize(_configService, _psHost);
                _executionPanel.StatusMessage += (s, msg) => SetStatus(msg);
            }
            return _executionPanel;
        }

        private HistoryPanel GetHistoryPanel()
        {
            if (_historyPanel == null)
            {
                _historyPanel = new HistoryPanel();
                _historyPanel.Initialize(_configService, _historyService);
            }
            return _historyPanel;
        }

        private UpdatePanel GetUpdatePanel()
        {
            if (_updatePanel == null)
            {
                    _updatePanel = new UpdatePanel();
                    _updatePanel.Initialize(_configService, _psHost);
                _updatePanel.StatusMessage += (s, msg) =>
                {
                    SetStatus(msg);
                    if (msg.Contains("Update available"))
                        _notificationBar.ShowUpdate(msg, "View");
                };
            }
            return _updatePanel;
        }

        private FileArchivePanel GetFileArchivePanel()
        {
            if (_fileArchivePanel == null)
            {
                _fileArchivePanel = new FileArchivePanel();
                _fileArchivePanel.Initialize(_configService, _psHost);
                _fileArchivePanel.OpenQueueRequested += (s, e) =>
                {
                    _sidebar.SelectedKey = "exec.filearchivequeue";
                    ShowPanel("exec.filearchivequeue");
                };
            }
            return _fileArchivePanel;
        }

        private FileArchiveQueuePanel GetFileArchiveQueuePanel()
        {
            if (_fileArchiveQueuePanel == null)
            {
                _fileArchiveQueuePanel = new FileArchiveQueuePanel();
                _fileArchiveQueuePanel.Initialize(_configService, _psHost);
            }
            else
            {
                _fileArchiveQueuePanel.RefreshQueue();
            }

            return _fileArchiveQueuePanel;
        }

        private DataSyncPanel GetDataSyncPanel()
        {
            if (_dataSyncPanel == null)
            {
                _dataSyncPanel = new DataSyncPanel();
                _dataSyncPanel.Initialize(_configService, _psHost);
                _dataSyncPanel.StatusMessage += (s, msg) => SetStatus(msg);
            }
            return _dataSyncPanel;
        }

        private HttpServerPanel GetHttpServerPanel()
        {
            if (_httpServerPanel == null)
            {
                if (_dashboardServer == null)
                    _dashboardServer = new DashboardHttpServerService();

                _httpServerPanel = new HttpServerPanel();
                _httpServerPanel.Initialize(_configService, _dashboardServer);
            }
            return _httpServerPanel;
        }

        private RetentionPolicyPanel GetRetentionPolicyPanel()
        {
            if (_retentionPolicyPanel == null)
            {
                _retentionPolicyPanel = new RetentionPolicyPanel();
                _retentionPolicyPanel.Initialize(_configService, _psHost);
                _retentionPolicyPanel.StatusMessage += (s, msg) => SetStatus(msg);
            }
            return _retentionPolicyPanel;
        }
        #endregion

        private void LaunchDashboard()
        {
            int port = _configService.DashboardConfig.DashboardPort;
            if (port <= 0) port = 8080;

            string dashFileName = _configService.AppConfig?.Files?.Dashboard;
            if (string.IsNullOrWhiteSpace(dashFileName))
                dashFileName = "Dashboard.html";

            string dashboardPath = Path.Combine(_configService.WebPath, dashFileName);
            string launchMode = (_configService.DashboardConfig.DashboardLaunchMode ?? "app").Trim().ToLowerInvariant();

            try
            {
                if (launchMode == "powershell")
                {
                    _psHost.LaunchDashboard(port, _configService.WebPath);
                    SetStatus($"Dashboard launched via PowerShell on port {port}");
                    return;
                }

                string rootDir = _configService.WebPath;
                if (!File.Exists(dashboardPath))
                    throw new FileNotFoundException("Dashboard HTML file not found.", dashboardPath);

                if (_dashboardServer == null)
                    _dashboardServer = new DashboardHttpServerService();

                _dashboardServer.Start(port, rootDir);

                string relativeFile = dashFileName.Replace('\\', '/').TrimStart('/');
                string url = $"http://localhost:{port}/{relativeFile}";
                DashboardHttpServerService.OpenBrowser(url);
                SetStatus($"Dashboard served by app on port {port}");
            }
            catch (Exception ex)
            {
                // Fallback keeps dashboard available even if HttpListener cannot bind.
                _psHost.LaunchDashboard(port, _configService.WebPath);
                SetStatus($"App server unavailable ({ex.Message}). Launched via PowerShell on port {port}");
            }
        }

        private void DoBackup()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Select backup destination", ShowNewFolderButton = true })
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    try
                    {
                        string dest = Path.Combine(dlg.SelectedPath, "SPO_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                        int count = _configService.BackupData(dest);
                        _notificationBar.ShowSuccess($"Backup complete: {count} files copied.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Backup failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void SetStatus(string text)
        {
            if (_statusBar == null || _statusBar.IsDisposed) return;
            Action a = () => _statusBar.SetIndicator3(text, AppTheme.TextMuted);
            if (InvokeRequired) Invoke(a); else a();
        }

        private void UpdateStatusFromHistory()
        {
            try
            {
                var sessions = _historyService.LoadSessionHistory();
                if (sessions != null && sessions.Count > 0)
                {
                    var last = sessions[sessions.Count - 1];
                    string id = last.SessionId ?? "";
                    _statusBar.SetSession("Session: " + (id.Length > 8 ? id.Substring(0, 8) : id));
                }
            }
            catch { }
        }

        private async void LoadDataAsync()
        {
            try
            {
                if (_telemetry != null)
                {
                    var stats = await _telemetry.GetGlobalStatsAsync();
                    if (stats != null && stats.TotalStorageFreedBytes > 0)
                        _homePanel?.SetGlobalStats(stats.StorageFreedFormatted);
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_configPanel != null && _configPanel.HasUnsavedChanges)
            {
                var result = MessageBox.Show("Unsaved config changes.\nSave before closing?",
                    "Unsaved Changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (result == DialogResult.Cancel) { e.Cancel = true; return; }
            }
            _executionPanel?.SaveGuiSettings();
            _dashboardServer?.Dispose();
            _psHost?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
