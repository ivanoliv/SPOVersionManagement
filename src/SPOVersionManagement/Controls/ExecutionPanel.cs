using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    public class ExecutionPanel : UserControl
    {
        private ConfigurationService _config;
        private ExecutionHistoryService _history;
        private PowerShellHostService _psHost;
        private SiteDataService _siteData;
        private CancellationTokenSource _cts;

        // Connection
        private ComboBox _cmbSessions;
        private FlatButton _btnLoadSession, _btnStartOver, _btnDeleteSessions, _btnRenameSession;
        private Label _lblSessionBadge;
        private List<SessionRecord> _sessions = new List<SessionRecord>();
        private bool _sessionLoaded;
        private SessionRecord _loadedSession;
        // Transient draft session shown after Start Over until Execute persists a real one
        private SessionRecord _draftSession;

        // Version policy
        private NumericUpDown _nudMajorVer, _nudMinorVer, _nudConcurrent;
        private NumericUpDown _nudCheckBatchSize, _nudCheckBatchDelay;
        private NumericUpDown _nudDeleteBeforeDays;
        private NumericUpDown _nudReexecutionDays;
        private ComboBox _cmbZeroVersion;
        private RadioButton _rbDeleteByCount, _rbDeleteByAge;
        private Panel _deleteByCountGroup, _deleteByAgeGroup;

        // Operation mode
        private CheckBox _chkSyncPolicy, _chkDeleteVersions, _chkRetention;
        private CheckBox _chkSkipGraph;

        // Input files
        private TextBox _txtSiteListCsv, _txtExclusionCsv, _txtGraphReportCsv, _txtSyncListCsv, _txtSamReportCsv, _txtCacheFile;
        private CheckBox _chkUseFileCache;
        private CheckBox _chkIncludeSites, _chkExcludeSites;
        private Label _lblIncludeCount, _lblExcludeCount;
        private FlatButton _btnViewInclude, _btnViewExclude;

        // Console / progress
        private TextBox _console;
        private ProgressBar _progressBar;
        private Label _lblStatus, _lblProgress;
        private FlatButton _btnExecute, _btnAbort;

        // Site progress
        private Panel _siteProgressPanel;
        private System.Windows.Forms.Timer _refreshTimer;
        private System.Windows.Forms.Timer _saveTimer;
        private bool _layoutRebuilding;
        private bool _loadingSettings;

        public event EventHandler<string> StatusMessage;

        public ExecutionPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(0, 0, 0, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            Resize += ExecutionPanel_Resize;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            _config = config;
            _history = new ExecutionHistoryService(config);
            _siteData = new SiteDataService(config);
            _psHost = psHost;
            BuildLayout();
        }

        public void RefreshAfterReset()
        {
            if (_history == null)
                return;

            RefreshSessionControls();
            if (_console != null)
                _console.Clear(); _preservedConsoleText = string.Empty;
            if (_siteProgressPanel != null)
                _siteProgressPanel.Controls.Clear();
            if (_lblProgress != null)
                _lblProgress.Text = string.Empty;
            if (_lblStatus != null)
            {
                _lblStatus.Text = "Ready";
                _lblStatus.ForeColor = AppTheme.TextMuted;
            }
        }

        /// <summary>
        /// Loads a session's configuration into the form and triggers execution.
        /// Called from SessionManagerPanel's Resume button.
        /// The PS script will auto-detect the interrupted session and resume from the queue.
        /// </summary>
        public void LoadSessionAndResume(SessionRecord session)
        {
            if (session?.Configuration == null) return;

            ApplySession(session);
            AppendConsole($"Resuming session {session.SessionId}...", AppTheme.AccentCyan);
            StatusMessage?.Invoke(this, $"Resuming session {session.SessionId}");

            // Trigger execution (same as clicking Execute — the PS script auto-detects pending session)
            BtnExecute_Click(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void BuildLayout()
        {
            Controls.Clear();
            int y = 0;
            int cardGap = 12;
            int W = Math.Max(700, ClientSize.Width - Padding.Horizontal - 16);

            // ═══ TOP BAR ═══
            var topBar = new Panel { Location = new Point(0, y), Size = new Size(W, 48), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(topBar);
            topBar.Controls.Add(new Label { Text = "Execution", Font = AppTheme.FontTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 2) });
            topBar.Controls.Add(new Label { Text = "Configure parameters, input files, and run version management.", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 32) });

            _btnExecute = new FlatButton { Text = "\u25B6  Execute", Size = new Size(120, 32), Location = new Point(W - 220, 6), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnExecute.SetAccentColor(AppTheme.AccentGreen);
            _btnExecute.Click += BtnExecute_Click;
            topBar.Controls.Add(_btnExecute);

            _btnAbort = new FlatButton { Text = "\u25A0  Abort", Size = new Size(90, 32), Location = new Point(W - 92, 6), Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnAbort.SetDangerStyle();
            _btnAbort.Click += BtnAbort_Click;
            topBar.Controls.Add(_btnAbort);
            y += 56;

            // ═══ SESSION CONTROL ═══
            var sessionCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 66), AccentLeft = AppTheme.AccentGreen, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(sessionCard);
            CL(sessionCard, "SESSION CONTROL", AppTheme.AccentGreen, 14, 4);
            sessionCard.Controls.Add(new Label
            {
                Text = "Load previous session, start over, or clear saved sessions.",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Size = new Size(245, 16),
                BackColor = Color.Transparent,
                Location = new Point(14, 24)
            });

            _cmbSessions = new ComboBox
            {
                Location = new Point(260, 18),
                Size = new Size(Math.Max(180, W - 700), 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AppTheme.StyleComboBox(_cmbSessions);
            sessionCard.Controls.Add(_cmbSessions);

            _btnRenameSession = new FlatButton { Text = "Rename", Size = new Size(80, 28), Location = new Point(W - 414, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnRenameSession.SetGhostStyle();
            _btnRenameSession.Click += BtnRenameSession_Click;
            sessionCard.Controls.Add(_btnRenameSession);

            _btnLoadSession = new FlatButton { Text = "Load Session", Size = new Size(104, 28), Location = new Point(W - 324, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnLoadSession.SetAccentColor(AppTheme.AccentCyan);
            _btnLoadSession.Click += BtnLoadSession_Click;
            sessionCard.Controls.Add(_btnLoadSession);

            _btnStartOver = new FlatButton { Text = "Start Over", Size = new Size(92, 28), Location = new Point(W - 214, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnStartOver.SetGhostStyle();
            _btnStartOver.Click += BtnStartOver_Click;
            sessionCard.Controls.Add(_btnStartOver);

            _btnDeleteSessions = new FlatButton { Text = "Delete All", Size = new Size(92, 28), Location = new Point(W - 110, 15), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnDeleteSessions.SetDangerStyle();
            _btnDeleteSessions.Click += BtnDeleteSessions_Click;
            sessionCard.Controls.Add(_btnDeleteSessions);

            _lblSessionBadge = new Label
            {
                Font = new Font("Cascadia Code", 7f, FontStyle.Bold),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 42)
            };
            sessionCard.Controls.Add(_lblSessionBadge);
            UpdateSessionBadge();

            RefreshSessionControls();
            y += 66 + cardGap;

            // ═══ ROW: Version Policy (left 60%) + Operation Mode (right 40%) ═══
            int policyW = (int)((W - cardGap) * 0.60);
            int modeW = W - policyW - cardGap;

            // Version Policy
            var policyCard = new GlassPanel { Location = new Point(0, y), Size = new Size(policyW, 206), AccentLeft = AppTheme.AccentPurple, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            Controls.Add(policyCard);
            CL(policyCard, "VERSION POLICY", AppTheme.AccentPurple, 14, 2);
            int px = 12, pw = 145;
            PL(policyCard, "Concurrent Jobs:", px, 26, pw);
            _nudConcurrent = PN(policyCard, px + pw, 24, 65, 1, 9999, 10);
            PL(policyCard, "Zero Version Action:", px, 52, pw);
            _cmbZeroVersion = new ComboBox { Location = new Point(px + pw, 50), Size = new Size(120, 20), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbZeroVersion.Items.AddRange(new[] { "syncOnly", "deleteOnly", "skip" });
            _cmbZeroVersion.SelectedIndex = 0;
            AppTheme.StyleComboBox(_cmbZeroVersion);
            policyCard.Controls.Add(_cmbZeroVersion);

            PL(policyCard, "Check Batch Size:", px + 270, 26, 115);
            _nudCheckBatchSize = PN(policyCard, px + 390, 24, 50, 1, 100, 10);
            PL(policyCard, "Batch Delay (s):", px + 270, 52, 115);
            _nudCheckBatchDelay = PN(policyCard, px + 390, 50, 50, 1, 30, 2);

            PL(policyCard, "Re-execution Days:", px, 78, pw);
            _nudReexecutionDays = PN(policyCard, px + pw, 76, 65, 0, 365, 60);

            // Delete mode groups
            var deleteModeSep = new Panel { Location = new Point(14, 104), Size = new Size(policyW - 28, 1), BackColor = Color.FromArgb(30, AppTheme.Border) };
            policyCard.Controls.Add(deleteModeSep);

            policyCard.Controls.Add(new Label
            {
                Text = "DELETE MODE",
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 108)
            });

            int groupTop = 120;
            int groupGap = 10;
            int groupW = (policyW - 24 - groupGap) / 2;
            int groupH = 82;

            _deleteByCountGroup = new Panel
            {
                Location = new Point(12, groupTop),
                Size = new Size(groupW, groupH),
                BackColor = Color.Transparent
            };
            policyCard.Controls.Add(_deleteByCountGroup);
            policyCard.Controls.Add(new Panel { Location = new Point(12, groupTop), Size = new Size(groupW, 1), BackColor = Color.FromArgb(52, AppTheme.Border) });
            policyCard.Controls.Add(new Panel { Location = new Point(12, groupTop + groupH - 1), Size = new Size(groupW, 1), BackColor = Color.FromArgb(52, AppTheme.Border) });

            _deleteByAgeGroup = new Panel
            {
                Location = new Point(12 + groupW + groupGap, groupTop),
                Size = new Size(groupW, groupH),
                BackColor = Color.Transparent
            };
            policyCard.Controls.Add(_deleteByAgeGroup);
            policyCard.Controls.Add(new Panel { Location = new Point(12 + groupW + groupGap, groupTop), Size = new Size(groupW, 1), BackColor = Color.FromArgb(52, AppTheme.Border) });
            policyCard.Controls.Add(new Panel { Location = new Point(12 + groupW + groupGap, groupTop + groupH - 1), Size = new Size(groupW, 1), BackColor = Color.FromArgb(52, AppTheme.Border) });
            policyCard.Controls.Add(new Panel { Location = new Point(12 + groupW + (groupGap / 2), groupTop + 8), Size = new Size(1, groupH - 16), BackColor = Color.FromArgb(45, AppTheme.Border) });

            _rbDeleteByCount = new RadioButton
            {
                Text = "  Delete by version count",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.AccentCyan,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(8, 6),
                Checked = true
            };
            _rbDeleteByCount.CheckedChanged += (s, e) => ToggleDeleteMode();
            _deleteByCountGroup.Controls.Add(_rbDeleteByCount);

            var lblMajor = new Label
            {
                Text = "Major:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                Size = new Size(64, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Location = new Point(8, 34)
            };
            _deleteByCountGroup.Controls.Add(lblMajor);
            _nudMajorVer = PN(_deleteByCountGroup, 76, 34, 64, 1, 500, 100);

            var lblMinor = new Label
            {
                Text = "Minor:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                Size = new Size(64, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Location = new Point(8, 54)
            };
            _deleteByCountGroup.Controls.Add(lblMinor);
            _nudMinorVer = PN(_deleteByCountGroup, 76, 54, 64, 0, 500, 0);

            _rbDeleteByAge = new RadioButton
            {
                Text = "  Delete by age",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.AccentCyan,
                BackColor = Color.Transparent,
                AutoSize = true,
                Location = new Point(8, 6)
            };
            _rbDeleteByAge.CheckedChanged += (s, e) => ToggleDeleteMode();
            _deleteByAgeGroup.Controls.Add(_rbDeleteByAge);

            var lblDays = new Label
            {
                Text = "Days:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = false,
                Size = new Size(52, 20),
                TextAlign = ContentAlignment.MiddleRight,
                BackColor = Color.Transparent,
                Location = new Point(8, 34)
            };
            _deleteByAgeGroup.Controls.Add(lblDays);
            _nudDeleteBeforeDays = PN(_deleteByAgeGroup, 64, 34, 76, 1, 3650, 180);
            _nudDeleteBeforeDays.Enabled = false;
            _deleteByAgeGroup.Controls.Add(new Label
            {
                Text = "Delete versions older than N days",
                Font = new Font("Cascadia Code", 6.5f),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(8, 59)
            });

            // Operation Mode
            var modeCard = new GlassPanel { Location = new Point(policyW + cardGap, y), Size = new Size(modeW, 206), AccentLeft = AppTheme.AccentGold, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(modeCard);
            CL(modeCard, "OPERATION MODE", AppTheme.AccentGold, 14, 2);

            _chkSyncPolicy = MkChk(modeCard, "Sync Version Policy", 14, 26);
            _chkSyncPolicy.Checked = true;
            modeCard.Controls.Add(new Label { Text = "New-SPOSiteManageVersionPolicyJob", Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 44) });

            _chkDeleteVersions = MkChk(modeCard, "Delete Excess Versions", 14, 62);
            _chkDeleteVersions.Checked = true;
            modeCard.Controls.Add(new Label { Text = "New-SPOSiteFileVersionBatchDeleteJob", Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 80) });

            _chkRetention = MkChk(modeCard, "Manage Retention Policies", 14, 98);
            modeCard.Controls.Add(new Label { Text = "Handle holds/retention before delete", Font = new Font("Cascadia Code", 7f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 116) });

            // Separator
            var modeSep = new Panel { Location = new Point(14, 138), Size = new Size(modeW - 28, 1), BackColor = Color.FromArgb(30, AppTheme.Border) };
            modeCard.Controls.Add(modeSep);
            _chkSkipGraph = MkChk(modeCard, "Skip Graph Connection", 14, 146);
            modeCard.Controls.Add(new Label { Text = "Skip if Graph report CSV provided", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(34, 164) });

            y += 206 + cardGap;

            // ═══ INPUT FILES ═══
            int fIn = Math.Max(220, W - 640);
            var filesCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 184), AccentLeft = AppTheme.AccentCyan, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(filesCard);
            CL(filesCard, "INPUT FILES", AppTheme.AccentCyan, 14, 2);
            int fy = 22, fLbl = 162;
            int fChkX = fLbl - 18; // checkbox at 144, label ends before it
            int fTxtX = fLbl + 4;  // textbox starts at 166
            int fAfterBrowse = fTxtX + fIn + 30; // x position after the "..." browse button

            // Include Sites row with checkbox + count badge + manage button
            PL(filesCard, "Include Sites (CSV):", 14, fy, fChkX - 16);
            _chkIncludeSites = new CheckBox { Text = "", Size = new Size(16, 16), Location = new Point(fChkX, fy + 2), BackColor = Color.Transparent, Checked = true };
            filesCard.Controls.Add(_chkIncludeSites);
            _txtSiteListCsv = FileRow(filesCard, fTxtX, fy, fIn, "CSV|*.csv", _config.AppConfig.InputFiles?.IncludeSites);
            _lblIncludeCount = new Label { Text = "", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = AppTheme.AccentGreen, AutoSize = true, BackColor = Color.Transparent, Location = new Point(fAfterBrowse + 4, fy + 3) };
            filesCard.Controls.Add(_lblIncludeCount);
            _btnViewInclude = new FlatButton { Text = "Manage", Size = new Size(54, 20), Location = new Point(fAfterBrowse + 124, fy) };
            _btnViewInclude.SetGhostStyle();
            _btnViewInclude.Click += (s, e) => ShowScopeManagerDialog("IncludeSites.csv", "Target Sites");
            filesCard.Controls.Add(_btnViewInclude);
            fy += 26;

            // Exclude Sites row with checkbox + count badge + manage button
            PL(filesCard, "Exclude Sites (CSV):", 14, fy, fChkX - 16);
            _chkExcludeSites = new CheckBox { Text = "", Size = new Size(16, 16), Location = new Point(fChkX, fy + 2), BackColor = Color.Transparent, Checked = true };
            filesCard.Controls.Add(_chkExcludeSites);
            _txtExclusionCsv = FileRow(filesCard, fTxtX, fy, fIn, "CSV|*.csv", _config.AppConfig.InputFiles?.ExcludeSites);
            _lblExcludeCount = new Label { Text = "", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(fAfterBrowse + 4, fy + 3) };
            filesCard.Controls.Add(_lblExcludeCount);
            _btnViewExclude = new FlatButton { Text = "Manage", Size = new Size(54, 20), Location = new Point(fAfterBrowse + 124, fy) };
            _btnViewExclude.SetGhostStyle();
            _btnViewExclude.Click += (s, e) => ShowScopeManagerDialog("ExcludeSites.csv", "Skip Sites");
            filesCard.Controls.Add(_btnViewExclude);
            fy += 26;

            PL(filesCard, "Graph Report (CSV):", 14, fy, fLbl);
            _txtGraphReportCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", null);
            _txtGraphReportCsv.TextChanged += (s, e) => SyncGraphOptions();
            CL(filesCard, "SharePoint Site Usage Storage report", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);
            fy += 26;

            PL(filesCard, "Sync Job List (CSV):", 14, fy, fLbl);
            _txtSyncListCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", null);
            CL(filesCard, "Sync BatchDeleteJobProgress externally", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);
            fy += 26;

            PL(filesCard, "SAM Report (CSV):", 14, fy, fLbl);
            _txtSamReportCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", null);
            CL(filesCard, "Content Management Assessment (inactive/ownerless)", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);
            fy += 26;

            _chkUseFileCache = MkChk(filesCard, "Use AllSites.json cache (skip Get-SPOSite)", 14, fy);
            PL(filesCard, "Cache:", 340, fy, 44);
            string defCache = Path.Combine(_config.ConfigPath, "AllSites.json");
            _txtCacheFile = FileRow(filesCard, 388, fy, 320, "JSON|*.json|All|*.*", File.Exists(defCache) ? defCache : null);
            _txtCacheFile.Enabled = false;
            _chkUseFileCache.CheckedChanged += (s, e) => _txtCacheFile.Enabled = _chkUseFileCache.Checked;

            y += 184 + cardGap;

            // ═══ STATUS + PROGRESS ═══
            _lblStatus = new Label { Text = "Ready", Font = AppTheme.FontBody, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, y) };
            Controls.Add(_lblStatus);
            _lblProgress = new Label { Text = "", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(80, y + 2) };
            Controls.Add(_lblProgress);
            y += 20;

            _progressBar = new ProgressBar { Location = new Point(0, y), Size = new Size(W, 5), Style = ProgressBarStyle.Continuous, Minimum = 0, Maximum = 100, Value = 0, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(_progressBar);
            y += 12;

            // ═══ CONSOLE (left 50%) + SITE PROGRESS (right 50%) ═══
            int bottomClearance = 16;
            int bottomH = Math.Max(220, ClientSize.Height - y - Padding.Vertical - bottomClearance);
            int consoleW = (W - cardGap) / 2;
            int spW = W - consoleW - cardGap;

            _console = new TextBox
            {
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                Location = new Point(0, y), Size = new Size(consoleW, bottomH),
                Font = AppTheme.FontMono, BackColor = AppTheme.BgInput, ForeColor = AppTheme.AccentGreen,
                BorderStyle = BorderStyle.FixedSingle, WordWrap = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(_console);

            var spCard = new GlassPanel { Location = new Point(consoleW + cardGap, y), Size = new Size(spW, bottomH), Anchor = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom };
            Controls.Add(spCard);
            CL(spCard, "SITE PROGRESS", AppTheme.AccentCyan, 8, 2);
            _siteProgressPanel = new Panel { Location = new Point(2, 20), Size = new Size(spW - 4, bottomH - 24), BackColor = Color.Transparent, AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            spCard.Controls.Add(_siteProgressPanel);

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 1500, Enabled = false };
            _refreshTimer.Tick += (s, e) => RefreshSiteProgress();

            LoadGuiSettings();
            SyncGraphOptions();
            WireAutoSave();
        }

        private void WireAutoSave()
        {
            _saveTimer = new System.Windows.Forms.Timer { Interval = 800, Enabled = false };
            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); SaveGuiSettings(); };

            // Wire value-changed events to trigger debounced save
            _nudConcurrent.ValueChanged += SettingChanged;
            _nudCheckBatchSize.ValueChanged += SettingChanged;
            _nudCheckBatchDelay.ValueChanged += SettingChanged;
            _nudMajorVer.ValueChanged += SettingChanged;
            _nudMinorVer.ValueChanged += SettingChanged;
            _nudDeleteBeforeDays.ValueChanged += SettingChanged;
            _nudReexecutionDays.ValueChanged += SettingChanged;
            _cmbZeroVersion.SelectedIndexChanged += SettingChanged;
            _rbDeleteByAge.CheckedChanged += SettingChanged;
            _rbDeleteByCount.CheckedChanged += SettingChanged;
            _chkSyncPolicy.CheckedChanged += SettingChanged;
            _chkDeleteVersions.CheckedChanged += SettingChanged;
            _chkRetention.CheckedChanged += SettingChanged;
            _chkSkipGraph.CheckedChanged += SettingChanged;
            _chkUseFileCache.CheckedChanged += SettingChanged;
            _chkIncludeSites.CheckedChanged += SettingChanged;
            _chkExcludeSites.CheckedChanged += SettingChanged;
            _txtSiteListCsv.TextChanged += SettingChanged;
            _txtSiteListCsv.TextChanged += (s, e) => RefreshScopeCountBadges();
            _txtExclusionCsv.TextChanged += SettingChanged;
            _txtExclusionCsv.TextChanged += (s, e) => RefreshScopeCountBadges();
            _txtGraphReportCsv.TextChanged += SettingChanged;
            _txtSyncListCsv.TextChanged += SettingChanged;
            _txtSamReportCsv.TextChanged += SettingChanged;
            _txtCacheFile.TextChanged += SettingChanged;
        }

        private void SettingChanged(object sender, EventArgs e)
        {
            if (_loadingSettings || _layoutRebuilding) return;
            SaveGuiSettings();
        }

        private async void BtnExecute_Click(object sender, EventArgs e)
        {
            // Each PowerShell script handles its own SPO/Graph auth (interactive or app-only).
            // We no longer require a global connection — just need the admin URL from config.
            string adminUrl = _psHost.IsConnected ? _psHost.AdminUrl : (_config.AppConfig.AdminUrl ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(adminUrl))
            {
                MessageBox.Show("Admin URL is not configured. Open Config and set the SharePoint Admin URL.", "Missing Admin URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Tenant mismatch pre-check: SessionHistory.json may contain a session from a different tenant.
            // If so, ask the user to clear local DB before running (otherwise PS would prompt and stale data syncs).
            if (!CheckTenantMismatchAndMaybeReset(adminUrl))
                return; // user cancelled

            // Option B: auto-create a friendly default label so every session is named, even
            // when the user did not click "Start Over". Only applied if no draft/pending label exists.
            if (string.IsNullOrWhiteSpace(_pendingLabelToApply) && _draftSession == null)
            {
                DateTime now = DateTime.Now;
                string autoLabel = "Session " + now.ToString("yyyy-MM-dd HH:mm");
                _pendingLabelToApply = autoLabel;
                _draftSession = new SessionRecord
                {
                    SessionId = "NEW_" + now.ToString("yyyyMMdd_HHmmss"),
                    Status = "Pending",
                    StartedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                    AdminUrl = adminUrl,
                    Label = autoLabel
                };
                RefreshSessionControls();
                UpdateSessionBadge();
            }

            bool doSync = _chkSyncPolicy.Checked;
            bool doDelete = _chkDeleteVersions.Checked;
            if (!doSync && !doDelete)
            {
                MessageBox.Show("Select at least one operation mode.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Resolve effective scope files (respecting checkboxes + auto-detect)
            string effectiveIncludeCsv = ResolveScopeCsvPath(_txtSiteListCsv, "IncludeSites.csv", _chkIncludeSites);
            string effectiveExcludeCsv = ResolveScopeCsvPath(_txtExclusionCsv, "ExcludeSites.csv", _chkExcludeSites);

            int includeCount = effectiveIncludeCsv != null ? GetScopeFileEntryCount(effectiveIncludeCsv) : 0;
            int excludeCount = effectiveExcludeCsv != null ? GetScopeFileEntryCount(effectiveExcludeCsv) : 0;

            // Pre-execution scope confirmation if scope files are active
            if (includeCount > 0 || excludeCount > 0)
            {
                string scopeMsg = "Execution Scope detected:\n\n";
                if (includeCount > 0)
                    scopeMsg += $"  \u2714 TARGET: {includeCount} site(s) will be processed\n";
                if (excludeCount > 0)
                    scopeMsg += $"  \u2716 SKIP: {excludeCount} site(s) will be excluded\n";
                if (includeCount == 0)
                    scopeMsg += "  \u25CB TARGET: All tenant sites (no include filter)\n";

                scopeMsg += "\nDo you want to review the scope before executing?\n\n[Yes] = Review scope  |  [No] = Continue execution  |  [Cancel] = Abort";

                var scopeResult = MessageBox.Show(scopeMsg, "Scope Confirmation", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                if (scopeResult == DialogResult.Cancel)
                    return;
                if (scopeResult == DialogResult.Yes)
                {
                    if (includeCount > 0) ShowScopeManagerDialog("IncludeSites.csv", "Target Sites");
                    if (excludeCount > 0) ShowScopeManagerDialog("ExcludeSites.csv", "Skip Sites");
                    // Refresh counts in case user edited during review
                    RefreshScopeCountBadges();
                    effectiveIncludeCsv = ResolveScopeCsvPath(_txtSiteListCsv, "IncludeSites.csv", _chkIncludeSites);
                    effectiveExcludeCsv = ResolveScopeCsvPath(_txtExclusionCsv, "ExcludeSites.csv", _chkExcludeSites);
                    includeCount = effectiveIncludeCsv != null ? GetScopeFileEntryCount(effectiveIncludeCsv) : 0;
                    excludeCount = effectiveExcludeCsv != null ? GetScopeFileEntryCount(effectiveExcludeCsv) : 0;
                    // After review, confirm execution
                    if (MessageBox.Show("Proceed with execution?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                        return;
                }
            }

            string desc = doSync && doDelete ? "Full (Sync + Delete)" : doSync ? "Sync Only" : "Delete Only";
            bool byAge = _rbDeleteByAge.Checked;
            string deleteMode = byAge ? $"By age ({_nudDeleteBeforeDays.Value} days)" : $"By count (Major:{_nudMajorVer.Value}, Minor:{_nudMinorVer.Value})";
            string files = "";
            if (includeCount > 0) files += $"\nInclude: {includeCount} site(s)";
            if (excludeCount > 0) files += $"\nExclude: {excludeCount} site(s)";
            if (!string.IsNullOrEmpty(_txtGraphReportCsv.Text)) files += $"\nGraph: {Path.GetFileName(_txtGraphReportCsv.Text)}";
            if (_chkUseFileCache.Checked) files += $"\nCache: {Path.GetFileName(_txtCacheFile.Text)}";

            if (MessageBox.Show($"Execute?\n\nURL: {adminUrl}\nMode: {desc}\nDelete: {deleteMode}\nConcurrent: {_nudConcurrent.Value}{files}", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            SaveGuiSettings();
            SetExecuting(true);
            _console.Clear(); _preservedConsoleText = string.Empty;
            _siteProgressPanel.Controls.Clear();
            AppendConsole("Starting execution...", AppTheme.AccentCyan);
            _cts = new CancellationTokenSource();

            // Scoped event handlers — only this execution feeds this console
            Action<string> outHandler = msg => AppendConsole(msg, AppTheme.TextSecondary);
            Action<string> warnHandler = msg => AppendConsole("[WARN] " + msg, AppTheme.AccentGold);
            Action<string> errHandler = msg => AppendConsole("[ERROR] " + msg, AppTheme.AccentRed);
            Action<int> progHandler = pct =>
            {
                if (InvokeRequired) Invoke((Action)(() => UpdateProgress(pct)));
                else UpdateProgress(pct);
            };

            _psHost.OnOutput += outHandler;
            _psHost.OnWarning += warnHandler;
            _psHost.OnError += errHandler;
            _psHost.OnProgress += progHandler;

            // Bridge PS Read-Host prompts to a UI dialog so users can answer with buttons.
            Func<SPOVersionManagement.Services.PromptRequest, Task<string>> promptHandler = req =>
            {
                var tcs = new TaskCompletionSource<string>();
                Action show = () =>
                {
                    string answer = ShowPromptDialog(req);
                    tcs.TrySetResult(answer ?? string.Empty);
                };
                if (InvokeRequired) BeginInvoke(show); else show();
                return tcs.Task;
            };
            _psHost.OnPromptRequest = promptHandler;

            try
            {
                bool hasGraphReport = !string.IsNullOrEmpty(NullIfEmpty(_txtGraphReportCsv.Text));
                bool skipGraphConnection = hasGraphReport || _chkSkipGraph.Checked;

                // Log scope status to console
                if (includeCount > 0)
                    AppendConsole($"[Scope] Target: {includeCount} site(s) from {Path.GetFileName(effectiveIncludeCsv)}", AppTheme.AccentCyan);
                if (excludeCount > 0)
                    AppendConsole($"[Scope] Skip: {excludeCount} site(s) from {Path.GetFileName(effectiveExcludeCsv)}", AppTheme.AccentCyan);
                if (includeCount == 0 && excludeCount == 0)
                    AppendConsole("[Scope] Processing ALL tenant sites (no scope filters)", AppTheme.TextMuted);

                await _psHost.StartVersionManagementAsync(adminUrl,
                    (int)_nudMajorVer.Value, (int)_nudMinorVer.Value, (int)_nudConcurrent.Value,
                    doSync && !doDelete, !doSync && doDelete,
                    _chkRetention.Checked, _chkUseFileCache.Checked, _cts.Token,
                    inputSiteListCsv: effectiveIncludeCsv,
                    inputExclusionSiteListCsv: effectiveExcludeCsv,
                    graphReportCsv: NullIfEmpty(_txtGraphReportCsv.Text),
                    inputSiteSyncListCsv: NullIfEmpty(_txtSyncListCsv.Text),
                    checkBatchSize: (int)_nudCheckBatchSize.Value,
                    checkBatchDelaySeconds: (int)_nudCheckBatchDelay.Value,
                    skipGraphConnection: skipGraphConnection,
                    openDashboard: false,
                    resetDatabase: false,
                    deleteBeforeDays: _rbDeleteByAge.Checked ? (int)_nudDeleteBeforeDays.Value : 0);

                AppendConsole("\nExecution completed.", AppTheme.AccentGreen);
                _lblStatus.Text = "Completed"; _lblStatus.ForeColor = AppTheme.AccentGreen;
                StatusMessage?.Invoke(this, "Execution completed.");
            }
            catch (OperationCanceledException) { AppendConsole("\nCancelled.", AppTheme.AccentGold); _lblStatus.Text = "Cancelled"; _lblStatus.ForeColor = AppTheme.AccentGold; }
            catch (Exception ex) { AppendConsole($"\nFailed: {ex.Message}", AppTheme.AccentRed); _lblStatus.Text = "Failed"; _lblStatus.ForeColor = AppTheme.AccentRed; }
            finally
            {
                _psHost.OnOutput -= outHandler;
                _psHost.OnWarning -= warnHandler;
                _psHost.OnError -= errHandler;
                _psHost.OnProgress -= progHandler;
                _psHost.OnPromptRequest = null;
                SetExecuting(false); _refreshTimer.Enabled = false; _cts?.Dispose(); _cts = null;
                // Reload sessions from disk so the newly-persisted session (with all options saved by the PS script) appears
                try
                {
                    // Apply pending label (from Start Over) to the most recent session on disk
                    if (!string.IsNullOrWhiteSpace(_pendingLabelToApply))
                    {
                        var diskSessions = _history.LoadSessionHistory();
                        if (diskSessions != null && diskSessions.Count > 0)
                        {
                            // Pick the latest by StartedAt (fallback to last)
                            SessionRecord latest = diskSessions[diskSessions.Count - 1];
                            DateTime latestDt = DateTime.MinValue;
                            foreach (var s in diskSessions)
                            {
                                if (DateTime.TryParse(s.StartedAt, out var dt) && dt > latestDt)
                                {
                                    latestDt = dt;
                                    latest = s;
                                }
                            }
                            if (latest != null && !string.IsNullOrEmpty(latest.SessionId))
                                _history.RenameSession(latest.SessionId, _pendingLabelToApply);
                        }
                        _pendingLabelToApply = null;
                    }
                    RefreshSessionControls();
                }
                catch { }
            }
        }

        private void BtnAbort_Click(object sender, EventArgs e)
        {
            if (_cts != null && MessageBox.Show("Cancel?", "Abort", MessageBoxButtons.YesNo) == DialogResult.Yes) _cts.Cancel();
        }

        /// <summary>
        /// Returns true if execution should proceed; false if user cancelled.
        /// Compares current admin URL with the tenant of the most recent session in SessionHistory.json.
        /// On mismatch, asks the user to reset the local execution database (sites scope).
        /// </summary>
        private bool CheckTenantMismatchAndMaybeReset(string currentAdminUrl)
        {
            try
            {
                var sessions = _history.LoadSessionHistory();
                if (sessions == null || sessions.Count == 0) return true;

                // Find the most-recent session that has a non-empty AdminUrl
                SessionRecord lastWithUrl = null;
                DateTime latestDt = DateTime.MinValue;
                foreach (var s in sessions)
                {
                    if (s == null || string.IsNullOrWhiteSpace(s.AdminUrl)) continue;
                    if (DateTime.TryParse(s.StartedAt, out var dt))
                    {
                        if (dt >= latestDt) { latestDt = dt; lastWithUrl = s; }
                    }
                    else if (lastWithUrl == null) lastWithUrl = s;
                }
                if (lastWithUrl == null) return true;

                string dbHost = ExtractHost(lastWithUrl.AdminUrl);
                string curHost = ExtractHost(currentAdminUrl);
                if (string.IsNullOrEmpty(dbHost) || string.IsNullOrEmpty(curHost)) return true;
                if (string.Equals(dbHost, curHost, StringComparison.OrdinalIgnoreCase)) return true;

                string msg =
                    "Tenant mismatch detected.\n\n" +
                    $"Current Admin URL:   {currentAdminUrl}\n" +
                    $"Database Admin URL:  {lastWithUrl.AdminUrl}\n\n" +
                    "The local execution database (config\\SessionHistory.json, AllSites.json, JobStatus.json, etc.) " +
                    "still contains data from a different tenant. Running without reset will sync stale data.\n\n" +
                    "Open the Reset Local Database dialog now and continue with the selected options?\n\n" +
                    "Yes  = Open reset dialog (you choose what to clear), then proceed\n" +
                    "No   = Cancel execution";

                var answer = MessageBox.Show(msg, "Tenant Mismatch", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer != DialogResult.Yes) return false;

                string resetType = ConfigurationPanel.ShowResetLocalDbDialog(ParentForm);
                if (string.IsNullOrEmpty(resetType))
                {
                    AppendConsole("Reset cancelled — execution aborted.", AppTheme.AccentGold);
                    return false;
                }

                int cleared = _config.ResetLocalExecutionDatabases(resetType);
                AppendConsole($"Local database reset ({resetType}) for tenant switch — {cleared} files cleared.", AppTheme.AccentGold);
                StatusMessage?.Invoke(this, $"Database reset ({resetType}) for tenant switch.");
                RefreshSessionControls();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not check tenant mismatch:\n{ex.Message}", "Tenant Check", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return true; // don't block the user on a check failure
            }
        }

        private static string ExtractHost(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return string.Empty;
            try
            {
                if (!url.Contains("://")) url = "https://" + url;
                return new Uri(url).Host;
            }
            catch { return url.Trim().TrimEnd('/'); }
        }

        /// <summary>
        /// Shows a styled prompt dialog for a PowerShell Read-Host request.
        /// If options are parsed (e.g. Y/N, F/S/D), shows them as buttons; otherwise shows a text box.
        /// Returns the chosen answer (or empty string).
        /// </summary>
        private string ShowPromptDialog(Services.PromptRequest req)
        {
            return ShowPromptDialog(req.QuestionText, req.PromptText, req.Options, req.ContextLines, req.DescriptiveOptions);
        }

        private string ShowPromptDialog(string questionText, string promptText, string[] options, string[] contextLines, Dictionary<string, string> descriptiveOptions)
        {
            string result = string.Empty;
            using (var dlg = new Form())
            {
                dlg.Text = "Script Question";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = AppTheme.BgDark;
                dlg.ForeColor = AppTheme.TextPrimary;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                bool hasOptions = options != null && options.Length > 0;
                bool hasDescriptive = descriptiveOptions != null && descriptiveOptions.Count > 0;
                int width = 560;
                int padding = 22;
                int yPos = 16;

                // Show the question text (extracted from context) as the main prompt
                string displayQuestion = questionText ?? promptText;
                if (string.IsNullOrWhiteSpace(displayQuestion))
                    displayQuestion = "(no prompt text)";

                var lblQuestion = new Label
                {
                    Text = displayQuestion.Trim(),
                    Font = AppTheme.FontHeading,
                    ForeColor = AppTheme.TextPrimary,
                    AutoSize = false,
                    Size = new Size(width - 2 * padding, 40),
                    Location = new Point(padding, yPos),
                    BackColor = Color.Transparent
                };
                dlg.Controls.Add(lblQuestion);
                yPos += 48;

                TextBox txt = null;

                if (hasOptions)
                {
                    // Use descriptive labels if available, otherwise just the key letter
                    var labels = new List<(string key, string label)>();
                    foreach (var opt in options)
                    {
                        string label = (hasDescriptive && descriptiveOptions.TryGetValue(opt, out string desc)) ? desc : opt;
                        labels.Add((opt, label));
                    }

                    // Measure each button's text to compute proper widths
                    using (var g = dlg.CreateGraphics())
                    {
                        var btnSizes = labels.Select(l => (int)g.MeasureString(l.label, AppTheme.FontHeading).Width + 32).ToList();
                        int totalBtns = btnSizes.Sum() + (labels.Count - 1) * 10 + 2 * padding;
                        if (totalBtns > width) width = totalBtns;

                        int x = padding;
                        for (int i = 0; i < labels.Count; i++)
                        {
                            string capturedKey = labels[i].key;
                            int bw = btnSizes[i];
                            var btn = new FlatButton
                            {
                                Text = labels[i].label,
                                Size = new Size(bw, 40),
                                Location = new Point(x, yPos),
                                Font = AppTheme.FontHeading
                            };
                            if (i == 0) btn.SetSuccessStyle(); else btn.SetGhostStyle();
                            btn.Click += (s, e) => { result = capturedKey; dlg.DialogResult = DialogResult.OK; };
                            dlg.Controls.Add(btn);
                            x += bw + 10;
                        }
                    }
                    yPos += 50;

                    // Update question label width to match expanded dialog
                    lblQuestion.Size = new Size(width - 2 * padding, 40);
                }
                else
                {
                    txt = new TextBox
                    {
                        Location = new Point(padding, yPos),
                        Size = new Size(width - 2 * padding, 24),
                        Font = AppTheme.FontMono
                    };
                    AppTheme.StyleTextBox(txt);
                    dlg.Controls.Add(txt);

                    var btnOk = new FlatButton
                    {
                        Text = "Send",
                        Size = new Size(110, 32),
                        Location = new Point(width - padding - 110, yPos + 34)
                    };
                    btnOk.SetSuccessStyle();
                    btnOk.Click += (s, e) => { result = txt.Text ?? string.Empty; dlg.DialogResult = DialogResult.OK; };
                    dlg.Controls.Add(btnOk);
                    yPos += 80;
                }

                dlg.ClientSize = new Size(width, yPos + 16);
                dlg.AcceptButton = null;
                dlg.ShowDialog(ParentForm);
            }
            return result ?? string.Empty;
        }

        private void ToggleDeleteMode()
        {
            bool byCount = _rbDeleteByCount.Checked;
            _nudMajorVer.Enabled = byCount;
            _nudMinorVer.Enabled = byCount;
            _nudDeleteBeforeDays.Enabled = !byCount;

            if (_deleteByCountGroup != null)
                _deleteByCountGroup.BackColor = Color.Transparent;
            if (_deleteByAgeGroup != null)
                _deleteByAgeGroup.BackColor = Color.Transparent;

            SetGroupInputState(_deleteByCountGroup, byCount);
            SetGroupInputState(_deleteByAgeGroup, !byCount);
        }

        private void SetGroupInputState(Panel group, bool enabled)
        {
            if (group == null)
                return;

            foreach (Control ctrl in group.Controls)
            {
                if (ctrl is RadioButton)
                    continue;
                ctrl.Enabled = enabled;
            }
        }

        private void SetExecuting(bool r)
        {
            _btnExecute.Enabled = !r; _btnAbort.Enabled = r;
            foreach (var c in new Control[] { _nudMajorVer, _nudMinorVer, _nudConcurrent, _nudCheckBatchSize, _nudCheckBatchDelay, _nudReexecutionDays, _cmbZeroVersion })
                c.Enabled = !r;
            foreach (var c in new Control[] { _chkSyncPolicy, _chkDeleteVersions, _chkRetention, _chkSkipGraph, _chkUseFileCache })
                c.Enabled = !r;
            if (_cmbSessions != null) _cmbSessions.Enabled = !r;
            if (_btnLoadSession != null) _btnLoadSession.Enabled = !r;
            if (_btnRenameSession != null) _btnRenameSession.Enabled = !r;
            if (_btnStartOver != null) _btnStartOver.Enabled = !r;
            if (_btnDeleteSessions != null) _btnDeleteSessions.Enabled = !r;
            if (r) { _lblStatus.Text = "Running..."; _lblStatus.ForeColor = AppTheme.AccentCyan; _refreshTimer.Enabled = true; }
        }

        private Dictionary<string, Panel> _siteRows = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase);
        private string _lastProgressHash = "";

        private void RefreshSiteProgress()
        {
            try
            {
                var js = _history.LoadJobStatus();

                // Build a quick hash to skip if nothing changed
                int hash = (js.ActiveJobs?.Count ?? 0) * 1000 + (js.QueuedSites?.Count ?? 0) * 100 + (js.CompletedJobs?.Count ?? 0);
                string newHash = hash.ToString();
                if (js.ActiveJobs != null)
                    foreach (var j in js.ActiveJobs)
                        newHash += "|" + (j.SiteUrl ?? "") + j.JobType;
                if (js.CompletedJobs != null)
                    foreach (var j in js.CompletedJobs)
                        newHash += "|" + (j.SiteUrl ?? "") + j.Status;

                bool fullRebuild = (newHash != _lastProgressHash);
                _lastProgressHash = newHash;

                if (fullRebuild)
                {
                    // Save scroll position
                    int scrollY = _siteProgressPanel.VerticalScroll.Value;

                    _siteProgressPanel.SuspendLayout();
                    _siteProgressPanel.Controls.Clear();
                    _siteRows.Clear();
                    int y = 0;

                    if (js.ActiveJobs != null)
                        foreach (var j in js.ActiveJobs)
                            AddSiteRow(ref y, j.SiteUrl, j.JobType, "Running", AppTheme.AccentCyan, j.StartedAt);

                    if (js.QueuedSites != null)
                    {
                        int n = 0;
                        foreach (var s in js.QueuedSites)
                        {
                            if (n++ >= 4) { _siteProgressPanel.Controls.Add(new Label { Text = $"+{js.QueuedSites.Count - 4} queued...", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(4, y) }); y += 16; break; }
                            AddSiteRow(ref y, s.SiteUrl, "", "Queued", AppTheme.AccentGold, null);
                        }
                    }

                    if (js.CompletedJobs != null)
                    {
                        int n = 0;
                        for (int i = js.CompletedJobs.Count - 1; i >= 0 && n < 4; i--, n++)
                        {
                            var j = js.CompletedJobs[i];
                            AddSiteRow(ref y, j.SiteUrl, j.JobType, j.Status == "CompleteSuccess" ? "Done" : j.Status, j.Status == "CompleteSuccess" ? AppTheme.AccentGreen : AppTheme.AccentRed, j.CompletedAt);
                        }
                    }

                    _siteProgressPanel.ResumeLayout(false);
                    _siteProgressPanel.PerformLayout();

                    // Restore scroll position
                    if (scrollY > 0 && _siteProgressPanel.VerticalScroll.Maximum >= scrollY)
                    {
                        _siteProgressPanel.AutoScrollPosition = new Point(0, scrollY);
                    }
                }

                int a = js.ActiveJobs?.Count ?? 0, q = js.QueuedSites?.Count ?? 0, c = js.CompletedJobs?.Count ?? 0;
                _lblProgress.Text = $"{a} active | {q} queued | {c} completed";
            }
            catch { }
        }

        private void AddSiteRow(ref int y, string url, string jobType, string status, Color sc, string time)
        {
            string name = "-";
            try { var u = new Uri(url ?? ""); var segs = u.AbsolutePath.TrimEnd('/').Split('/'); name = segs[segs.Length - 1]; } catch { name = url ?? "-"; }
            if (name.Length > 22) name = name.Substring(0, 20) + "..";

            int rh = 26;
            var row = new Panel { Location = new Point(0, y), Size = new Size(_siteProgressPanel.Width - 4, rh), BackColor = Color.Transparent };
            row.Controls.Add(new Label { Text = name, Font = AppTheme.FontSmall, ForeColor = AppTheme.TextPrimary, AutoSize = false, Size = new Size(130, 14), Location = new Point(2, 2), BackColor = Color.Transparent });

            // Phase pipeline: Q → S → D → ✓
            bool synced = jobType == "SyncVersionPolicy" || status == "Done" || jobType == "BatchDelete";
            bool deleting = jobType == "BatchDelete" || status == "Done";
            bool done = status == "Done";
            int dx = 134;
            PhDot(row, dx, 5, true, AppTheme.AccentGreen);
            PhLine(row, dx + 9, 8, 14, synced);
            PhDot(row, dx + 24, 5, synced, synced ? AppTheme.AccentCyan : AppTheme.TextMuted);
            PhLine(row, dx + 33, 8, 14, deleting);
            PhDot(row, dx + 48, 5, deleting, deleting ? AppTheme.AccentPurple : AppTheme.TextMuted);
            PhLine(row, dx + 57, 8, 14, done);
            PhDot(row, dx + 72, 5, done, done ? AppTheme.AccentGreen : AppTheme.TextMuted);

            // Status text
            row.Controls.Add(new Label { Text = status, Font = new Font("Cascadia Code", 6.5f), ForeColor = sc, AutoSize = true, BackColor = Color.Transparent, Location = new Point(dx + 86, 4) });

            // Time
            string ts = "";
            if (DateTime.TryParse(time, out DateTime dt)) ts = dt.ToString("HH:mm");
            row.Controls.Add(new Label { Text = ts, Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(_siteProgressPanel.Width - 46, 4) });

            row.Controls.Add(new Panel { Location = new Point(0, rh - 1), Size = new Size(_siteProgressPanel.Width, 1), BackColor = Color.FromArgb(20, AppTheme.Border) });
            _siteProgressPanel.Controls.Add(row);
            y += rh;
        }

        private void PhDot(Panel p, int x, int cy, bool active, Color c)
        {
            var d = new Panel { Location = new Point(x, cy), Size = new Size(7, 7), BackColor = Color.Transparent };
            d.Paint += (s, ev) => { ev.Graphics.SmoothingMode = SmoothingMode.AntiAlias; if (active) { using (var b = new SolidBrush(c)) ev.Graphics.FillEllipse(b, 0, 0, 6, 6); } else { using (var pen = new Pen(AppTheme.TextMuted)) ev.Graphics.DrawEllipse(pen, 0, 0, 6, 6); } };
            p.Controls.Add(d);
        }

        private void PhLine(Panel p, int x, int cy, int w, bool active)
        {
            p.Controls.Add(new Panel { Location = new Point(x, cy), Size = new Size(w, 2), BackColor = active ? AppTheme.AccentCyan : Color.FromArgb(40, AppTheme.TextMuted) });
        }

        private string _preservedConsoleText = string.Empty;

        private void ExecutionPanel_Resize(object sender, EventArgs e)
        {
            if (_layoutRebuilding || !Visible || IsDisposed || _config == null)
                return;

            // Preserve console text so it survives BuildLayout (which recreates the TextBox)
            if (_console != null) _preservedConsoleText = _console.Text;

            string selectedSession = GetSelectedSessionId();

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed)
                    return;

                _layoutRebuilding = true;
                try
                {
                    BuildLayout();

                    // Restore preserved console text (BuildLayout creates a fresh TextBox)
                    if (_console != null && !string.IsNullOrEmpty(_preservedConsoleText))
                    {
                        _console.Text = _preservedConsoleText;
                        _console.SelectionStart = _console.TextLength;
                        _console.ScrollToCaret();
                    }

                    // Restore non-persisted state (session selection, transient text fields not in GuiSettings)
                    RestoreSelectedSession(selectedSession);
                    UpdateSessionBadge();
                }
                finally
                {
                    _layoutRebuilding = false;
                }
            }));
        }

        private void AppendConsole(string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            Action a = () => { _console.AppendText(text + Environment.NewLine); _console.SelectionStart = _console.TextLength; _console.ScrollToCaret(); _preservedConsoleText = _console.Text; };
            if (InvokeRequired) Invoke(a); else a();
        }

        private void UpdateProgress(int pct) { _progressBar.Value = Math.Max(0, Math.Min(100, pct)); _lblProgress.Text = $"{pct}%"; }

        private void LoadGuiSettings()
        {
            _loadingSettings = true;
            try
            {
            var s = _config.LoadGuiSettings();
            _nudConcurrent.Value = Math.Max(_nudConcurrent.Minimum, Math.Min(_nudConcurrent.Maximum, s.ConcurrentJobs));
            _nudCheckBatchSize.Value = Math.Max(_nudCheckBatchSize.Minimum, Math.Min(_nudCheckBatchSize.Maximum, s.CheckBatchSize));
            _nudCheckBatchDelay.Value = Math.Max(_nudCheckBatchDelay.Minimum, Math.Min(_nudCheckBatchDelay.Maximum, s.CheckBatchDelay));
            _nudMajorVer.Value = Math.Max(_nudMajorVer.Minimum, Math.Min(_nudMajorVer.Maximum, s.MajorVersionLimit));
            _nudMinorVer.Value = Math.Max(_nudMinorVer.Minimum, Math.Min(_nudMinorVer.Maximum, s.MinorVersionLimit));
            _nudDeleteBeforeDays.Value = Math.Max(_nudDeleteBeforeDays.Minimum, Math.Min(_nudDeleteBeforeDays.Maximum, s.DeleteBeforeDays));
            _nudReexecutionDays.Value = Math.Max(_nudReexecutionDays.Minimum, Math.Min(_nudReexecutionDays.Maximum, s.ReexecutionDays));

            int zeroIdx = _cmbZeroVersion.Items.IndexOf(s.ZeroVersionAction);
            if (zeroIdx >= 0) _cmbZeroVersion.SelectedIndex = zeroIdx;

            _rbDeleteByAge.Checked = s.DeleteByAge;
            _rbDeleteByCount.Checked = !s.DeleteByAge;

            _chkSyncPolicy.Checked = s.SyncVersionPolicy;
            _chkDeleteVersions.Checked = s.DeleteExcessVersions;
            _chkRetention.Checked = s.ManageRetentionPolicies;
            _chkSkipGraph.Checked = s.SkipGraph;

            if (!string.IsNullOrEmpty(s.IncludeSitesCsv)) _txtSiteListCsv.Text = s.IncludeSitesCsv;
            if (!string.IsNullOrEmpty(s.ExcludeSitesCsv)) _txtExclusionCsv.Text = s.ExcludeSitesCsv;
            if (!string.IsNullOrEmpty(s.GraphReportCsv)) _txtGraphReportCsv.Text = s.GraphReportCsv;
            if (!string.IsNullOrEmpty(s.SyncJobListCsv)) _txtSyncListCsv.Text = s.SyncJobListCsv;
            if (!string.IsNullOrEmpty(s.SamReportCsv)) _txtSamReportCsv.Text = s.SamReportCsv;
            if (!string.IsNullOrEmpty(s.CacheFilePath)) _txtCacheFile.Text = s.CacheFilePath;
            _chkUseFileCache.Checked = s.UseFileCache;
            _txtCacheFile.Enabled = s.UseFileCache;
            }
            finally { _loadingSettings = false; }

            // Auto-detect scope file entries and update badges/checkboxes
            RefreshScopeCountBadges();
        }

        public void SaveGuiSettings()
        {
            if (_config == null || _nudConcurrent == null) return;
            var s = new Models.GuiSettings
            {
                ConcurrentJobs = (int)_nudConcurrent.Value,
                CheckBatchSize = (int)_nudCheckBatchSize.Value,
                CheckBatchDelay = (int)_nudCheckBatchDelay.Value,
                ZeroVersionAction = _cmbZeroVersion.SelectedItem?.ToString() ?? "syncOnly",
                DeleteByAge = _rbDeleteByAge.Checked,
                MajorVersionLimit = (int)_nudMajorVer.Value,
                MinorVersionLimit = (int)_nudMinorVer.Value,
                DeleteBeforeDays = (int)_nudDeleteBeforeDays.Value,
                ReexecutionDays = (int)_nudReexecutionDays.Value,
                SyncVersionPolicy = _chkSyncPolicy.Checked,
                DeleteExcessVersions = _chkDeleteVersions.Checked,
                ManageRetentionPolicies = _chkRetention.Checked,
                SkipGraph = _chkSkipGraph.Checked,
                IncludeSitesCsv = _txtSiteListCsv.Text,
                ExcludeSitesCsv = _txtExclusionCsv.Text,
                GraphReportCsv = _txtGraphReportCsv.Text,
                SyncJobListCsv = _txtSyncListCsv.Text,
                SamReportCsv = _txtSamReportCsv.Text,
                UseFileCache = _chkUseFileCache.Checked,
                CacheFilePath = _txtCacheFile.Text
            };
            _config.SaveGuiSettings(s);

            // Sync re-execution rules to DashboardConfig (read by PS script at runtime)
            _config.DashboardConfig.ReexecutionDays = (int)_nudReexecutionDays.Value;
            _config.SaveDashboardConfig();
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

        private NumericUpDown PN(Control p, int x, int y, int w, int min, int max, int val)
        {
            var n = new NumericUpDown { Location = new Point(x, y), Size = new Size(w, 20), Minimum = min, Maximum = max, Value = val };
            AppTheme.StyleNumericUpDown(n);
            p.Controls.Add(n);
            return n;
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

        private void RefreshSessionControls()
        {
            if (_cmbSessions == null)
                return;

            string previousSelection = GetSelectedSessionId();
            _sessions = _history.LoadSessionHistory();

            // Prepend transient draft session (created by Start Over) so user sees the new pending run.
            // Drop the draft once a real session with a later StartedAt has been persisted.
            if (_draftSession != null && _sessions != null)
            {
                bool supersededByReal = false;
                if (DateTime.TryParse(_draftSession.StartedAt, out var draftStart))
                {
                    foreach (var s in _sessions)
                    {
                        if (DateTime.TryParse(s.StartedAt, out var realStart) && realStart >= draftStart)
                        {
                            supersededByReal = true;
                            break;
                        }
                    }
                }
                if (supersededByReal)
                    _draftSession = null;
                else
                    _sessions = new List<SessionRecord>(_sessions) { _draftSession };
            }

            _cmbSessions.Items.Clear();

            if (_sessions == null || _sessions.Count == 0)
            {
                _cmbSessions.Items.Add("No saved sessions");
                _cmbSessions.SelectedIndex = 0;
                _cmbSessions.Enabled = false;
                if (_btnLoadSession != null) _btnLoadSession.Enabled = false;
                if (_btnDeleteSessions != null) _btnDeleteSessions.Enabled = false;
                return;
            }

            _cmbSessions.Enabled = true;
            if (_btnLoadSession != null) _btnLoadSession.Enabled = true;
            if (_btnDeleteSessions != null) _btnDeleteSessions.Enabled = true;

            int selectedIndex = _sessions.Count - 1;
            for (int i = 0; i < _sessions.Count; i++)
            {
                var session = _sessions[i];
                string sessionId = session.SessionId ?? "(no id)";
                string started = session.StartedAt;
                if (DateTime.TryParse(session.StartedAt, out DateTime dt))
                    started = dt.ToString("yyyy-MM-dd HH:mm");

                string label = string.IsNullOrWhiteSpace(session.Label) ? sessionId : $"{sessionId} \u2014 {session.Label}";
                _cmbSessions.Items.Add($"{label} | {session.Status ?? "Unknown"} | {started}");
                if (!string.IsNullOrEmpty(previousSelection) && string.Equals(previousSelection, session.SessionId, StringComparison.OrdinalIgnoreCase))
                    selectedIndex = i;
            }

            // If a draft is present, default-select it (last item)
            if (_draftSession != null)
                selectedIndex = _sessions.Count - 1;

            if (_cmbSessions.Items.Count > 0)
                _cmbSessions.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, _cmbSessions.Items.Count - 1));
        }

        private void UpdateSessionBadge(SessionRecord loaded = null)
        {
            if (_lblSessionBadge == null) return;

            if (loaded != null) _loadedSession = loaded;

            if (_sessionLoaded && _loadedSession != null)
            {
                string lbl = string.IsNullOrWhiteSpace(_loadedSession.Label) ? _loadedSession.SessionId : _loadedSession.Label;
                _lblSessionBadge.Text = $"\u25CF Session loaded: {lbl}";
                _lblSessionBadge.ForeColor = AppTheme.AccentCyan;
            }
            else if (_draftSession != null)
            {
                string lbl = string.IsNullOrWhiteSpace(_draftSession.Label) ? "new" : _draftSession.Label;
                _lblSessionBadge.Text = $"\u25B6 New session: {lbl}";
                _lblSessionBadge.ForeColor = AppTheme.AccentGreen;
            }
            else
            {
                _lblSessionBadge.Text = "\u25CB New session will be created on Execute";
                _lblSessionBadge.ForeColor = AppTheme.TextMuted;
            }
        }

        private string GetSelectedSessionId()
        {
            if (_cmbSessions == null || _sessions == null || _cmbSessions.SelectedIndex < 0 || _cmbSessions.SelectedIndex >= _sessions.Count)
                return null;

            return _sessions[_cmbSessions.SelectedIndex]?.SessionId;
        }

        private void RestoreSelectedSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId) || _cmbSessions == null || _sessions == null)
                return;

            for (int i = 0; i < _sessions.Count; i++)
            {
                if (string.Equals(_sessions[i]?.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))
                {
                    _cmbSessions.SelectedIndex = i;
                    return;
                }
            }
        }

        private void BtnLoadSession_Click(object sender, EventArgs e)
        {
            if (_sessions == null || _cmbSessions == null || _cmbSessions.SelectedIndex < 0 || _cmbSessions.SelectedIndex >= _sessions.Count)
                return;

            var session = _sessions[_cmbSessions.SelectedIndex];
            if (session == null)
                return;

            ApplySession(session);
            _sessionLoaded = true;
            _loadedSession = session;
            UpdateSessionBadge(session);
            StatusMessage?.Invoke(this, $"Loaded session {session.SessionId}.");
            AppendConsole($"Loaded session {session.SessionId}", AppTheme.AccentCyan);
        }

        private void ApplySession(SessionRecord session)
        {
            var config = session.Configuration;
            if (config == null)
                return;

            _loadingSettings = true;
            try
            {
            _nudMajorVer.Value = Math.Max(_nudMajorVer.Minimum, Math.Min(_nudMajorVer.Maximum, config.MajorVersionLimit > 0 ? config.MajorVersionLimit : (int)_nudMajorVer.Value));
            _nudMinorVer.Value = Math.Max(_nudMinorVer.Minimum, Math.Min(_nudMinorVer.Maximum, config.MajorWithMinorVersionsLimit >= 0 ? config.MajorWithMinorVersionsLimit : (int)_nudMinorVer.Value));
            _nudConcurrent.Value = Math.Max(_nudConcurrent.Minimum, Math.Min(_nudConcurrent.Maximum, config.MaxConcurrentJobs > 0 ? config.MaxConcurrentJobs : (int)_nudConcurrent.Value));
            _nudCheckBatchSize.Value = Math.Max(_nudCheckBatchSize.Minimum, Math.Min(_nudCheckBatchSize.Maximum, config.CheckBatchSize > 0 ? config.CheckBatchSize : (int)_nudCheckBatchSize.Value));
            _nudCheckBatchDelay.Value = Math.Max(_nudCheckBatchDelay.Minimum, Math.Min(_nudCheckBatchDelay.Maximum, config.CheckBatchDelaySeconds > 0 ? config.CheckBatchDelaySeconds : (int)_nudCheckBatchDelay.Value));

            if (!string.IsNullOrWhiteSpace(config.ZeroVersionAction))
                _cmbZeroVersion.SelectedItem = config.ZeroVersionAction;

            _chkSyncPolicy.Checked = !config.DeleteOnly;
            _chkDeleteVersions.Checked = !config.SyncOnly;
            _txtSiteListCsv.Text = config.InputSiteListCsv ?? string.Empty;
            _txtExclusionCsv.Text = config.InputExclusionSiteListCsv ?? string.Empty;
            _txtGraphReportCsv.Text = config.GraphReportCsv ?? string.Empty;
            _txtSyncListCsv.Text = config.InputSiteSyncListCsv ?? string.Empty;

            if (config.DeleteBeforeDays > 0)
            {
                _rbDeleteByAge.Checked = true;
                _nudDeleteBeforeDays.Value = Math.Max(_nudDeleteBeforeDays.Minimum, Math.Min(_nudDeleteBeforeDays.Maximum, config.DeleteBeforeDays));
            }
            else
            {
                _rbDeleteByCount.Checked = true;
            }

            _chkUseFileCache.Checked = config.UseFileCache;
            _txtCacheFile.Enabled = config.UseFileCache;
            if (!string.IsNullOrWhiteSpace(config.CacheFilePath)) _txtCacheFile.Text = config.CacheFilePath;
            if (!string.IsNullOrWhiteSpace(config.InputSiteSyncListCsv)) _txtSyncListCsv.Text = config.InputSiteSyncListCsv;
            if (!string.IsNullOrWhiteSpace(config.SamReportCsv)) _txtSamReportCsv.Text = config.SamReportCsv;
            _chkRetention.Checked = config.ManageRetention;

            ToggleDeleteMode();
            SyncGraphOptions();
            }
            finally
            {
                _loadingSettings = false;
            }
        }

        private void BtnStartOver_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Start over with a clean execution form? This does not delete saved sessions.", "Start Over", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            // Clear session-specific state but keep all settings from GuiSettings.json
            _console.Clear(); _preservedConsoleText = string.Empty;
            LoadGuiSettings();
            ToggleDeleteMode();
            SyncGraphOptions();

            // Ask user for a friendly name (with default suggestion). User can keep or edit.
            DateTime now = DateTime.Now;
            string suggested = "Session " + now.ToString("yyyy-MM-dd HH:mm");
            string entered = PromptForText(
                "New Session",
                "Give this session a friendly name (optional).\nDefault: \"" + suggested + "\". You can also describe scope, e.g. \"Top 100 sites\".",
                suggested);
            if (entered == null) return; // cancelled

            string label = SanitizeSessionLabel(entered);
            if (string.IsNullOrWhiteSpace(label)) label = suggested;

            // Create a draft session entry so the dropdown shows the new pending run.
            // SessionId is kept as the immutable timestamp key (used to track session files on disk).
            // The user-entered Label is stored separately and shown in the dropdown.
            _draftSession = new SessionRecord
            {
                SessionId = "NEW_" + now.ToString("yyyyMMdd_HHmmss"),
                Status = "Pending",
                StartedAt = now.ToString("yyyy-MM-dd HH:mm:ss"),
                AdminUrl = _psHost?.AdminUrl,
                Label = label
            };
            _pendingLabelToApply = label;
            _sessionLoaded = false;
            _loadedSession = null;
            UpdateSessionBadge();
            RefreshSessionControls();
            StatusMessage?.Invoke(this, "New session pending: " + label);
        }

        // Holds the label entered during Start Over so we can apply it to the
        // real session record once the PowerShell script persists it after Execute.
        private string _pendingLabelToApply;

        /// <summary>
        /// Strips characters unsafe for filenames and trims to a reasonable length.
        /// We only sanitize for safety; the SessionId (timestamp) remains the on-disk tracking key.
        /// </summary>
        private static string SanitizeSessionLabel(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var invalid = new HashSet<char>(System.IO.Path.GetInvalidFileNameChars());
            // Also remove characters that are technically valid but problematic in filenames/UI
            foreach (char c in new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|', '\r', '\n', '\t' })
                invalid.Add(c);
            var sb = new System.Text.StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (invalid.Contains(c)) sb.Append(' ');
                else sb.Append(c);
            }
            // Collapse whitespace and trim
            string cleaned = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
            // Cap length to keep dropdown readable and filenames manageable
            const int maxLen = 80;
            if (cleaned.Length > maxLen) cleaned = cleaned.Substring(0, maxLen).TrimEnd();
            return cleaned;
        }


        private void BtnRenameSession_Click(object sender, EventArgs e)
        {
            string id = GetSelectedSessionId();
            if (string.IsNullOrEmpty(id) || _sessions == null)
            {
                MessageBox.Show("Select a saved session to rename.", "Rename Session", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Renaming the transient draft (pending session) — update label in memory only
            if (id.StartsWith("NEW_", StringComparison.OrdinalIgnoreCase) && _draftSession != null)
            {
                string draftLabel = PromptForText("Rename Pending Session", "Update the friendly name for this pending session:", _draftSession.Label ?? string.Empty);
                if (draftLabel == null) return;
                string sanitized = SanitizeSessionLabel(draftLabel);
                _draftSession.Label = sanitized;
                _pendingLabelToApply = sanitized;
                RefreshSessionControls();
                StatusMessage?.Invoke(this, "Pending session label updated.");
                return;
            }

            var existing = _sessions.FirstOrDefault(s => string.Equals(s?.SessionId, id, StringComparison.OrdinalIgnoreCase));
            string current = existing?.Label ?? string.Empty;

            string newLabel = PromptForText("Rename Session", $"Add a friendly name for session:\n{id}\n\n(e.g. \"Top 100 sites\", \"Q2 cleanup\")", current);
            if (newLabel == null) return; // cancelled

            string clean = SanitizeSessionLabel(newLabel);
            try
            {
                if (_history.RenameSession(id, clean))
                {
                    RefreshSessionControls();
                    StatusMessage?.Invoke(this, "Session renamed.");
                }
                else
                {
                    MessageBox.Show("Could not rename session (file not found or session id missing).", "Rename Session", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Rename failed:\n{ex.Message}", "Rename Session", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string PromptForText(string title, string message, string initial)
        {
            using (var dlg = new Form())
            {
                dlg.Text = title;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.ShowInTaskbar = false;
                dlg.ClientSize = new Size(440, 160);
                dlg.BackColor = AppTheme.BgDark;
                dlg.ForeColor = AppTheme.TextPrimary;
                dlg.Font = AppTheme.FontBody;

                var lbl = new Label { Text = message, AutoSize = false, Size = new Size(420, 56), Location = new Point(10, 10), ForeColor = AppTheme.TextSecondary, BackColor = Color.Transparent };
                var txt = new TextBox { Text = initial ?? string.Empty, Location = new Point(10, 76), Size = new Size(420, 24) };
                AppTheme.StyleTextBox(txt);
                var ok = new FlatButton { Text = "Save", Size = new Size(80, 28), Location = new Point(260, 116) };
                ok.SetAccentColor(AppTheme.AccentCyan);
                ok.Click += (s, e) => { dlg.DialogResult = DialogResult.OK; dlg.Close(); };
                var cancel = new FlatButton { Text = "Cancel", Size = new Size(80, 28), Location = new Point(348, 116) };
                cancel.SetGhostStyle();
                cancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };
                dlg.AcceptButton = ok;
                dlg.CancelButton = cancel;
                dlg.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
                return dlg.ShowDialog() == DialogResult.OK ? txt.Text : null;
            }
        }

        private void BtnDeleteSessions_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Delete all saved sessions and clear current session state? This does not delete execution history.", "Delete All Sessions", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                int deleted = _history.ClearSessionState(includeCurrentJobStatus: true);
                RefreshSessionControls();
                StatusMessage?.Invoke(this, $"Saved sessions cleared. {deleted} file(s) removed.");
                AppendConsole("Saved sessions cleared.", AppTheme.AccentGold);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to clear sessions:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SyncGraphOptions()
        {
            if (_chkSkipGraph == null || _txtGraphReportCsv == null)
                return;

            bool hasGraphReport = !string.IsNullOrEmpty(NullIfEmpty(_txtGraphReportCsv.Text));
            if (hasGraphReport)
            {
                _chkSkipGraph.Checked = true;
                _chkSkipGraph.Enabled = false;
                _chkSkipGraph.Text = "  Skip Graph (auto: tenant status report provided)";
            }
            else
            {
                _chkSkipGraph.Enabled = true;
                _chkSkipGraph.Text = "  Skip Graph";
            }
        }

        private static string NullIfEmpty(string s)
        {
            var v = s?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }

        private int GetScopeFileEntryCount(string csvPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(csvPath))
                    return 0;

                // Resolve relative path against root
                string fullPath = Path.IsPathRooted(csvPath) ? csvPath : Path.Combine(_config.RootPath, csvPath);
                if (!File.Exists(fullPath))
                    return 0;

                var lines = File.ReadAllLines(fullPath).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
                return Math.Max(0, lines.Length - 1); // subtract header
            }
            catch { return 0; }
        }

        private void RefreshScopeCountBadges()
        {
            // Include sites
            string includePath = !string.IsNullOrWhiteSpace(_txtSiteListCsv.Text)
                ? _txtSiteListCsv.Text
                : Path.Combine(_config.RootPath, "IncludeSites.csv");
            int includeCount = GetScopeFileEntryCount(includePath);

            if (includeCount > 0)
            {
                _lblIncludeCount.Text = $"\u25CF {includeCount} site(s) targeted";
                _lblIncludeCount.ForeColor = AppTheme.AccentGreen;
                _chkIncludeSites.Checked = true;
                _btnViewInclude.Visible = true;
            }
            else
            {
                _lblIncludeCount.Text = "All sites (no filter)";
                _lblIncludeCount.ForeColor = AppTheme.TextMuted;
                _chkIncludeSites.Checked = false;
                _btnViewInclude.Visible = false;
            }

            // Exclude sites
            string excludePath = !string.IsNullOrWhiteSpace(_txtExclusionCsv.Text)
                ? _txtExclusionCsv.Text
                : Path.Combine(_config.RootPath, "ExcludeSites.csv");
            int excludeCount = GetScopeFileEntryCount(excludePath);

            if (excludeCount > 0)
            {
                _lblExcludeCount.Text = $"\u25CF {excludeCount} site(s) excluded";
                _lblExcludeCount.ForeColor = AppTheme.AccentGold;
                _chkExcludeSites.Checked = true;
                _btnViewExclude.Visible = true;
            }
            else
            {
                _lblExcludeCount.Text = "No exclusions";
                _lblExcludeCount.ForeColor = AppTheme.TextMuted;
                _chkExcludeSites.Checked = false;
                _btnViewExclude.Visible = false;
            }
        }

        private void ShowScopeManagerDialog(string fileName, string title)
        {
            string fullPath = Path.Combine(_config.RootPath, fileName);
            var items = _siteData != null ? _siteData.LoadScopeList(fileName) : new List<ScopeSiteItem>();

            using (var dlg = new Form())
            {
                dlg.Text = $"{title} — {fileName}";
                dlg.Size = new Size(700, 480);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.BackColor = Color.FromArgb(24, 28, 36);
                dlg.ForeColor = AppTheme.TextPrimary;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;

                var grid = new DataGridView
                {
                    Location = new Point(12, 12),
                    Size = new Size(660, 340),
                    AllowUserToAddRows = false,
                    AllowUserToDeleteRows = false,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    MultiSelect = true,
                    ReadOnly = true,
                    RowHeadersVisible = false,
                    BackgroundColor = Color.FromArgb(18, 22, 28),
                    GridColor = Color.FromArgb(40, 44, 52),
                    DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(18, 22, 28), ForeColor = AppTheme.TextPrimary, SelectionBackColor = Color.FromArgb(40, 80, 120), Font = new Font("Segoe UI", 9f) },
                    ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(30, 34, 42), ForeColor = AppTheme.AccentCyan, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) },
                    EnableHeadersVisualStyles = false
                };
                grid.Columns.Add("SiteUrl", "Site URL");
                grid.Columns.Add("Reason", "Reason");
                grid.Columns[0].Width = 440;
                grid.Columns[1].Width = 200;

                foreach (var item in items)
                    grid.Rows.Add(item.SiteUrl, item.Reason);

                dlg.Controls.Add(grid);

                var lblCount = new Label { Text = $"{items.Count} site(s)", Location = new Point(12, 360), AutoSize = true, ForeColor = AppTheme.TextMuted, Font = new Font("Segoe UI", 8f) };
                dlg.Controls.Add(lblCount);

                // Add button
                var btnAdd = new Button { Text = "+ Add URL", Location = new Point(12, 390), Size = new Size(90, 28), FlatStyle = FlatStyle.Flat, ForeColor = AppTheme.AccentGreen, BackColor = Color.FromArgb(30, 34, 42) };
                btnAdd.FlatAppearance.BorderColor = AppTheme.AccentGreen;
                btnAdd.Click += (s, e) =>
                {
                    using (var input = new Form())
                    {
                        input.Text = "Add Site URL";
                        input.Size = new Size(500, 140);
                        input.StartPosition = FormStartPosition.CenterParent;
                        input.BackColor = Color.FromArgb(24, 28, 36);
                        input.FormBorderStyle = FormBorderStyle.FixedDialog;
                        input.MaximizeBox = false; input.MinimizeBox = false;

                        var txtUrl = new TextBox { Location = new Point(12, 16), Size = new Size(460, 22), BackColor = Color.FromArgb(18, 22, 28), ForeColor = AppTheme.TextPrimary };
                        txtUrl.PlaceholderText = "https://tenant.sharepoint.com/sites/sitename";
                        input.Controls.Add(txtUrl);

                        var txtReason = new TextBox { Location = new Point(12, 44), Size = new Size(360, 22), BackColor = Color.FromArgb(18, 22, 28), ForeColor = AppTheme.TextPrimary };
                        txtReason.PlaceholderText = "Reason (optional)";
                        input.Controls.Add(txtReason);

                        var btnOk = new Button { Text = "Add", Location = new Point(380, 44), Size = new Size(60, 24), DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, ForeColor = AppTheme.AccentGreen, BackColor = Color.FromArgb(30, 34, 42) };
                        btnOk.FlatAppearance.BorderColor = AppTheme.AccentGreen;
                        input.Controls.Add(btnOk);
                        input.AcceptButton = btnOk;

                        if (input.ShowDialog(dlg) == DialogResult.OK && !string.IsNullOrWhiteSpace(txtUrl.Text))
                        {
                            string url = txtUrl.Text.Trim();
                            if (!items.Any(i => string.Equals(i.SiteUrl, url, StringComparison.OrdinalIgnoreCase)))
                            {
                                items.Add(new ScopeSiteItem { SiteUrl = url, Reason = txtReason.Text.Trim() });
                                grid.Rows.Add(url, txtReason.Text.Trim());
                                lblCount.Text = $"{items.Count} site(s)";
                            }
                        }
                    }
                };
                dlg.Controls.Add(btnAdd);

                // Remove button
                var btnRemove = new Button { Text = "Remove", Location = new Point(110, 390), Size = new Size(80, 28), FlatStyle = FlatStyle.Flat, ForeColor = AppTheme.AccentRed, BackColor = Color.FromArgb(30, 34, 42) };
                btnRemove.FlatAppearance.BorderColor = AppTheme.AccentRed;
                btnRemove.Click += (s, e) =>
                {
                    if (grid.SelectedRows.Count == 0) return;
                    var toRemove = new List<string>();
                    foreach (DataGridViewRow row in grid.SelectedRows)
                        toRemove.Add(row.Cells[0].Value?.ToString() ?? "");
                    items.RemoveAll(i => toRemove.Contains(i.SiteUrl, StringComparer.OrdinalIgnoreCase));
                    foreach (DataGridViewRow row in grid.SelectedRows.Cast<DataGridViewRow>().ToList())
                        grid.Rows.Remove(row);
                    lblCount.Text = $"{items.Count} site(s)";
                };
                dlg.Controls.Add(btnRemove);

                // Clear All button
                var btnClear = new Button { Text = "Clear All", Location = new Point(198, 390), Size = new Size(80, 28), FlatStyle = FlatStyle.Flat, ForeColor = AppTheme.AccentGold, BackColor = Color.FromArgb(30, 34, 42) };
                btnClear.FlatAppearance.BorderColor = AppTheme.AccentGold;
                btnClear.Click += (s, e) =>
                {
                    if (MessageBox.Show($"Remove all {items.Count} entries?", "Clear", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                    {
                        items.Clear();
                        grid.Rows.Clear();
                        lblCount.Text = "0 site(s)";
                    }
                };
                dlg.Controls.Add(btnClear);

                // Save & Close
                var btnSave = new Button { Text = "Save && Close", Location = new Point(550, 390), Size = new Size(110, 28), FlatStyle = FlatStyle.Flat, ForeColor = AppTheme.AccentCyan, BackColor = Color.FromArgb(30, 34, 42), DialogResult = DialogResult.OK };
                btnSave.FlatAppearance.BorderColor = AppTheme.AccentCyan;
                dlg.Controls.Add(btnSave);

                // Cancel
                var btnCancel = new Button { Text = "Cancel", Location = new Point(440, 390), Size = new Size(80, 28), FlatStyle = FlatStyle.Flat, ForeColor = AppTheme.TextMuted, BackColor = Color.FromArgb(30, 34, 42), DialogResult = DialogResult.Cancel };
                btnCancel.FlatAppearance.BorderColor = AppTheme.TextMuted;
                dlg.Controls.Add(btnCancel);
                dlg.CancelButton = btnCancel;

                if (dlg.ShowDialog(ParentForm) == DialogResult.OK)
                {
                    _siteData.SaveScopeList(fileName, items);
                    RefreshScopeCountBadges();
                }
            }
        }

        private string ResolveScopeCsvPath(TextBox txt, string defaultFileName, CheckBox chk)
        {
            if (!chk.Checked)
                return null;

            string path = NullIfEmpty(txt.Text);
            if (!string.IsNullOrEmpty(path))
                return path;

            // Auto-detect from default file
            string autoPath = Path.Combine(_config.RootPath, defaultFileName);
            if (File.Exists(autoPath) && GetScopeFileEntryCount(autoPath) > 0)
                return autoPath;

            return null;
        }
        #endregion
    }
}

