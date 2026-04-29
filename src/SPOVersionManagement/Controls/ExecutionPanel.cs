using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
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
        private CancellationTokenSource _cts;

        // Connection
        private TextBox _txtAdminUrl;
        private ComboBox _cmbSessions;
        private FlatButton _btnLoadSession, _btnStartOver, _btnDeleteSessions;
        private List<SessionRecord> _sessions = new List<SessionRecord>();

        // Version policy
        private NumericUpDown _nudMajorVer, _nudMinorVer, _nudConcurrent;
        private NumericUpDown _nudCheckBatchSize, _nudCheckBatchDelay;
        private NumericUpDown _nudDeleteBeforeDays;
        private ComboBox _cmbZeroVersion;
        private RadioButton _rbDeleteByCount, _rbDeleteByAge;
        private Panel _deleteByCountGroup, _deleteByAgeGroup;

        // Operation mode
        private CheckBox _chkSyncPolicy, _chkDeleteVersions, _chkRetention;
        private CheckBox _chkSkipGraph;

        // Input files
        private TextBox _txtSiteListCsv, _txtExclusionCsv, _txtGraphReportCsv, _txtSyncListCsv, _txtSamReportCsv, _txtCacheFile;
        private CheckBox _chkUseFileCache;

        // Console / progress
        private TextBox _console;
        private ProgressBar _progressBar;
        private Label _lblStatus, _lblProgress;
        private FlatButton _btnExecute, _btnAbort;

        // Site progress
        private Panel _siteProgressPanel;
        private System.Windows.Forms.Timer _refreshTimer;
        private bool _layoutRebuilding;

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
            _psHost = psHost;
            _psHost.OnOutput += msg => AppendConsole(msg, AppTheme.TextSecondary);
            _psHost.OnWarning += msg => AppendConsole("[WARN] " + msg, AppTheme.AccentGold);
            _psHost.OnError += msg => AppendConsole("[ERROR] " + msg, AppTheme.AccentRed);
            _psHost.OnProgress += pct =>
            {
                if (InvokeRequired) Invoke((Action)(() => UpdateProgress(pct)));
                else UpdateProgress(pct);
            };
            BuildLayout();
        }

        public void RefreshAfterReset()
        {
            if (_history == null)
                return;

            RefreshSessionControls();
            if (_console != null)
                _console.Clear();
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
            var sessionCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 58), AccentLeft = AppTheme.AccentGreen, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
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
                Size = new Size(Math.Max(220, W - 610), 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            AppTheme.StyleComboBox(_cmbSessions);
            sessionCard.Controls.Add(_cmbSessions);

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

            RefreshSessionControls();
            y += 58 + cardGap;

            // ═══ CONNECTION ═══
            var connCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 50), AccentLeft = AppTheme.AccentCyan, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(connCard);
            CL(connCard, "CONNECTION", AppTheme.AccentCyan, 14, 4);
            CL(connCard, "Admin URL:", AppTheme.TextSecondary, 14, 24); connCard.Controls[connCard.Controls.Count - 1].Font = AppTheme.FontBody;
            _txtAdminUrl = new TextBox { Location = new Point(100, 22), Size = new Size(Math.Max(200, W / 2), 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ReadOnly = true };
            AppTheme.StyleTextBox(_txtAdminUrl);
            _txtAdminUrl.Text = _config.AppConfig.AdminUrl ?? "";
            connCard.Controls.Add(_txtAdminUrl);
            connCard.Controls.Add(new Label { Text = "(from Config tab)", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(Math.Max(200, W / 2) + 108, 26) });
            _chkSkipGraph = MkChk(connCard, "Skip Graph", W - 180, 24); _chkSkipGraph.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            y += 50 + cardGap;

            // ═══ ROW: Version Policy (left 60%) + Operation Mode (right 40%) ═══
            int policyW = (int)((W - cardGap) * 0.60);
            int modeW = W - policyW - cardGap;

            // Version Policy
            var policyCard = new GlassPanel { Location = new Point(0, y), Size = new Size(policyW, 180), AccentLeft = AppTheme.AccentPurple, Anchor = AnchorStyles.Top | AnchorStyles.Left };
            Controls.Add(policyCard);
            CL(policyCard, "VERSION POLICY", AppTheme.AccentPurple, 14, 2);
            int px = 12, pw = 145;
            PL(policyCard, "Concurrent Jobs:", px, 26, pw);
            _nudConcurrent = PN(policyCard, px + pw, 24, 65, 1, 50, 10);
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

            // Delete mode groups
            var deleteModeSep = new Panel { Location = new Point(14, 78), Size = new Size(policyW - 28, 1), BackColor = Color.FromArgb(30, AppTheme.Border) };
            policyCard.Controls.Add(deleteModeSep);

            policyCard.Controls.Add(new Label
            {
                Text = "DELETE MODE",
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 82)
            });

            int groupTop = 94;
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
            var modeCard = new GlassPanel { Location = new Point(policyW + cardGap, y), Size = new Size(modeW, 180), AccentLeft = AppTheme.AccentGold, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
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
            modeCard.Controls.Add(new Label { Text = "Mutual exclusive: use Sync+Delete or DeleteOnly", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 146) });
            modeCard.Controls.Add(new Label { Text = "DeleteBeforeDays is exclusive with version limits", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 162) });

            y += 180 + cardGap;

            // ═══ INPUT FILES ═══
            int fIn = Math.Max(220, W - 640);
            var filesCard = new GlassPanel { Location = new Point(0, y), Size = new Size(W, 184), AccentLeft = AppTheme.AccentCyan, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(filesCard);
            CL(filesCard, "INPUT FILES", AppTheme.AccentCyan, 14, 2);
            int fy = 22, fLbl = 162;

            PL(filesCard, "Include Sites (CSV):", 14, fy, fLbl);
            _txtSiteListCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", _config.AppConfig.InputFiles?.IncludeSites);
            CL(filesCard, "Only process these sites", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);
            fy += 26;

            PL(filesCard, "Exclude Sites (CSV):", 14, fy, fLbl);
            _txtExclusionCsv = FileRow(filesCard, fLbl + 14, fy, fIn, "CSV|*.csv", _config.AppConfig.InputFiles?.ExcludeSites);
            CL(filesCard, "Skip these sites", AppTheme.TextMuted, fLbl + fIn + 50, fy + 2);
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
            string defCache = Path.Combine(_config.LogsPath, "AllSites.json");
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
            int bottomClearance = 52;
            int bottomH = Math.Max(210, ClientSize.Height - y - Padding.Vertical - bottomClearance);
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

            _refreshTimer = new System.Windows.Forms.Timer { Interval = 3000, Enabled = false };
            _refreshTimer.Tick += (s, e) => RefreshSiteProgress();

            SyncGraphOptions();
        }

        private async void BtnExecute_Click(object sender, EventArgs e)
        {
            string adminUrl = _txtAdminUrl.Text.Trim();
            if (string.IsNullOrEmpty(adminUrl))
            {
                MessageBox.Show("Admin URL is required.\n\nExample: https://contoso-admin.sharepoint.com", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool doSync = _chkSyncPolicy.Checked;
            bool doDelete = _chkDeleteVersions.Checked;
            if (!doSync && !doDelete)
            {
                MessageBox.Show("Select at least one operation mode.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string desc = doSync && doDelete ? "Full (Sync + Delete)" : doSync ? "Sync Only" : "Delete Only";
            bool byAge = _rbDeleteByAge.Checked;
            string deleteMode = byAge ? $"By age ({_nudDeleteBeforeDays.Value} days)" : $"By count (Major:{_nudMajorVer.Value}, Minor:{_nudMinorVer.Value})";
            string files = "";
            if (!string.IsNullOrEmpty(_txtSiteListCsv.Text)) files += $"\nInclude: {Path.GetFileName(_txtSiteListCsv.Text)}";
            if (!string.IsNullOrEmpty(_txtExclusionCsv.Text)) files += $"\nExclude: {Path.GetFileName(_txtExclusionCsv.Text)}";
            if (!string.IsNullOrEmpty(_txtGraphReportCsv.Text)) files += $"\nGraph: {Path.GetFileName(_txtGraphReportCsv.Text)}";
            if (_chkUseFileCache.Checked) files += $"\nCache: {Path.GetFileName(_txtCacheFile.Text)}";

            if (MessageBox.Show($"Execute?\n\nURL: {adminUrl}\nMode: {desc}\nDelete: {deleteMode}\nConcurrent: {_nudConcurrent.Value}{files}", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            SetExecuting(true);
            _console.Clear();
            _siteProgressPanel.Controls.Clear();
            AppendConsole("Starting execution...", AppTheme.AccentCyan);
            _cts = new CancellationTokenSource();

            try
            {
                bool hasGraphReport = !string.IsNullOrEmpty(NullIfEmpty(_txtGraphReportCsv.Text));
                bool skipGraphConnection = hasGraphReport || _chkSkipGraph.Checked;

                await _psHost.StartVersionManagementAsync(adminUrl,
                    (int)_nudMajorVer.Value, (int)_nudMinorVer.Value, (int)_nudConcurrent.Value,
                    doSync && !doDelete, !doSync && doDelete,
                    _chkRetention.Checked, _chkUseFileCache.Checked, _cts.Token,
                    inputSiteListCsv: NullIfEmpty(_txtSiteListCsv.Text),
                    inputExclusionSiteListCsv: NullIfEmpty(_txtExclusionCsv.Text),
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
            finally { SetExecuting(false); _refreshTimer.Enabled = false; _cts?.Dispose(); _cts = null; }
        }

        private void BtnAbort_Click(object sender, EventArgs e)
        {
            if (_cts != null && MessageBox.Show("Cancel?", "Abort", MessageBoxButtons.YesNo) == DialogResult.Yes) _cts.Cancel();
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
            _txtAdminUrl.ReadOnly = r;
            foreach (var c in new Control[] { _nudMajorVer, _nudMinorVer, _nudConcurrent, _nudCheckBatchSize, _nudCheckBatchDelay, _cmbZeroVersion })
                c.Enabled = !r;
            foreach (var c in new Control[] { _chkSyncPolicy, _chkDeleteVersions, _chkRetention, _chkSkipGraph, _chkUseFileCache })
                c.Enabled = !r;
            if (_cmbSessions != null) _cmbSessions.Enabled = !r;
            if (_btnLoadSession != null) _btnLoadSession.Enabled = !r;
            if (_btnStartOver != null) _btnStartOver.Enabled = !r;
            if (_btnDeleteSessions != null) _btnDeleteSessions.Enabled = !r;
            if (r) { _lblStatus.Text = "Running..."; _lblStatus.ForeColor = AppTheme.AccentCyan; _refreshTimer.Enabled = true; }
        }

        private void RefreshSiteProgress()
        {
            try
            {
                var js = _history.LoadJobStatus();
                _siteProgressPanel.SuspendLayout();
                _siteProgressPanel.Controls.Clear();
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

                _siteProgressPanel.ResumeLayout(true);
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

        private void ExecutionPanel_Resize(object sender, EventArgs e)
        {
            if (_layoutRebuilding || !Visible || IsDisposed || _config == null)
                return;

            string include = _txtSiteListCsv?.Text ?? string.Empty;
            string exclude = _txtExclusionCsv?.Text ?? string.Empty;
            string graph = _txtGraphReportCsv?.Text ?? string.Empty;
            string sync = _txtSyncListCsv?.Text ?? string.Empty;
            string sam = _txtSamReportCsv?.Text ?? string.Empty;
            string cache = _txtCacheFile?.Text ?? string.Empty;
            bool skipGraph = _chkSkipGraph?.Checked ?? false;
            string selectedSession = GetSelectedSessionId();

            bool syncPolicy = _chkSyncPolicy?.Checked ?? true;
            bool deleteVersions = _chkDeleteVersions?.Checked ?? true;
            bool retention = _chkRetention?.Checked ?? false;
            bool deleteByAge = _rbDeleteByAge?.Checked ?? false;
            bool useCache = _chkUseFileCache?.Checked ?? false;

            decimal major = _nudMajorVer?.Value ?? 100;
            decimal minor = _nudMinorVer?.Value ?? 0;
            decimal concurrent = _nudConcurrent?.Value ?? 10;
            decimal checkBatch = _nudCheckBatchSize?.Value ?? 10;
            decimal checkDelay = _nudCheckBatchDelay?.Value ?? 2;
            decimal deleteBefore = _nudDeleteBeforeDays?.Value ?? 180;

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed)
                    return;

                _layoutRebuilding = true;
                try
                {
                    BuildLayout();

                    _txtSiteListCsv.Text = include;
                    _txtExclusionCsv.Text = exclude;
                    _txtGraphReportCsv.Text = graph;
                    _txtSyncListCsv.Text = sync;
                    _txtSamReportCsv.Text = sam;
                    _txtCacheFile.Text = cache;
                    _chkSkipGraph.Checked = skipGraph;
                    RestoreSelectedSession(selectedSession);

                    _chkSyncPolicy.Checked = syncPolicy;
                    _chkDeleteVersions.Checked = deleteVersions;
                    _chkRetention.Checked = retention;
                    _chkUseFileCache.Checked = useCache;
                    SyncGraphOptions();

                    _rbDeleteByAge.Checked = deleteByAge;
                    _rbDeleteByCount.Checked = !deleteByAge;

                    _nudMajorVer.Value = Math.Max(_nudMajorVer.Minimum, Math.Min(_nudMajorVer.Maximum, major));
                    _nudMinorVer.Value = Math.Max(_nudMinorVer.Minimum, Math.Min(_nudMinorVer.Maximum, minor));
                    _nudConcurrent.Value = Math.Max(_nudConcurrent.Minimum, Math.Min(_nudConcurrent.Maximum, concurrent));
                    _nudCheckBatchSize.Value = Math.Max(_nudCheckBatchSize.Minimum, Math.Min(_nudCheckBatchSize.Maximum, checkBatch));
                    _nudCheckBatchDelay.Value = Math.Max(_nudCheckBatchDelay.Minimum, Math.Min(_nudCheckBatchDelay.Maximum, checkDelay));
                    _nudDeleteBeforeDays.Value = Math.Max(_nudDeleteBeforeDays.Minimum, Math.Min(_nudDeleteBeforeDays.Maximum, deleteBefore));
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
            Action a = () => { _console.AppendText(text + Environment.NewLine); _console.SelectionStart = _console.TextLength; _console.ScrollToCaret(); };
            if (InvokeRequired) Invoke(a); else a();
        }

        private void UpdateProgress(int pct) { _progressBar.Value = Math.Max(0, Math.Min(100, pct)); _lblProgress.Text = $"{pct}%"; }

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

                _cmbSessions.Items.Add($"{sessionId} | {session.Status ?? "Unknown"} | {started}");
                if (!string.IsNullOrEmpty(previousSelection) && string.Equals(previousSelection, session.SessionId, StringComparison.OrdinalIgnoreCase))
                    selectedIndex = i;
            }

            if (_cmbSessions.Items.Count > 0)
                _cmbSessions.SelectedIndex = Math.Max(0, Math.Min(selectedIndex, _cmbSessions.Items.Count - 1));
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
            StatusMessage?.Invoke(this, $"Loaded session {session.SessionId}.");
            AppendConsole($"Loaded session {session.SessionId}", AppTheme.AccentCyan);
        }

        private void ApplySession(SessionRecord session)
        {
            var config = session.Configuration;
            if (config == null)
                return;

            _txtAdminUrl.Text = session.AdminUrl ?? _config.AppConfig.AdminUrl ?? string.Empty;
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

            ToggleDeleteMode();
            SyncGraphOptions();
        }

        private void BtnStartOver_Click(object sender, EventArgs e)
        {
            if (MessageBox.Show("Start over with a clean execution form? This does not delete saved sessions.", "Start Over", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _txtAdminUrl.Text = _config.AppConfig.AdminUrl ?? string.Empty;
            _cmbZeroVersion.SelectedIndex = 0;
            _nudMajorVer.Value = 100;
            _nudMinorVer.Value = 0;
            _nudConcurrent.Value = 10;
            _nudCheckBatchSize.Value = 10;
            _nudCheckBatchDelay.Value = 2;
            _nudDeleteBeforeDays.Value = 180;
            _rbDeleteByCount.Checked = true;
            _chkSyncPolicy.Checked = true;
            _chkDeleteVersions.Checked = true;
            _chkRetention.Checked = false;
            _chkUseFileCache.Checked = false;
            _txtSiteListCsv.Text = _config.AppConfig.InputFiles?.IncludeSites ?? string.Empty;
            _txtExclusionCsv.Text = _config.AppConfig.InputFiles?.ExcludeSites ?? string.Empty;
            _txtGraphReportCsv.Clear();
            _txtSyncListCsv.Clear();
            _txtSamReportCsv.Clear();
            _console.Clear();
            ToggleDeleteMode();
            SyncGraphOptions();
            StatusMessage?.Invoke(this, "Execution form reset to defaults.");
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
        #endregion
    }
}
