using System;
using System.Collections.Generic;
using System.Data;
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
    public class SiteManagementPanel : UserControl
    {
        private ConfigurationService _config;
        private PowerShellHostService _psHost;
        private ExecutionHistoryService _history;
        private SiteDataService _siteData;
        private CancellationTokenSource _archiveCts;
        private CancellationTokenSource _filterCts;

        private string _currentView = "catalog";
        private bool _initialized;
        private bool _dataLoaded;

        private Panel _contentHost;
        private Panel _dataViewPanel;
        private Panel _scopeViewPanel;
        private Panel _toolbar;
        private TableLayoutPanel _dataLayout;

        private Label _lblViewTitle;
        private Label _lblHint;
        private Label _lblSummary;
        private Label _lblArchiveStatus;
        private Label _lblLoading;
        private Panel _loadingHost;
        private FlowLayoutPanel _summaryGrid;
        private List<Control> _summaryCards = new List<Control>();
        private Label _lblSummaryTotalSites;
        private Label _lblSummaryStorage;
        private Label _lblSummaryVersions;
        private Label _lblSummaryVersionSize;
        private Label _lblSummaryVersionPct;
        private Label _lblSummaryLastUpdate;
        private int _summaryCurrentColumns = -1;
        private bool _summaryReflowPending;
        private bool _suspendSummaryReflow;

        private TextBox _txtSearch;
        private TextBox _txtArchiveAdminUrl;
        private FlatButton _btnFilterStatus;
        private FlatButton _btnFilterArchive;
        private CheckedListBox _chkStatus;
        private CheckedListBox _chkArchive;
        private ToolStripDropDown _chkStatusDropDown;
        private ToolStripDropDown _chkArchiveDropDown;

        private FlatButton _btnRefresh;
        private FlatButton _btnExport;
        private FlatButton _btnDetails;
        private FlatButton _btnAddToQueue;
        private FlatButton _btnConfirmQueueSelection;
        private FlatButton _btnAddSelectedToSkip;
        private FlatButton _btnAddSelectedToArchive;
        private FlatButton _btnRemoveFromQueue;
        private FlatButton _btnRunQueue;

        private VirtualSiteGrid _grid;
        private Panel _queueFooterHost;
        private HashSet<string> _historySiteUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DataGridView _targetGrid;
        private DataGridView _skipGrid;
        private Panel _scopeWarningBanner;
        private FlatButton _btnScopeEditDesc, _btnScopeRemove;
        private FlatButton _btnScopeImportTarget, _btnScopeSaveTarget, _btnScopeImportSkip, _btnScopeSaveSkip;
        private FlatButton _btnScopeExportCsv, _btnScopeAddTarget, _btnScopeAddSkip;
        private TextBox _archiveConsole;
        private ProgressBar _progressBar;
        private ProgressBar _loadingBar;
        private Panel _summaryBar;
        private RowStyle _queueFooterRowStyle;
        private Panel _periodFilterPanel;
        private Label _lblSamLegend;
        private int _periodFilterDays;

        private List<SiteCatalogEntry> _catalogSites = new List<SiteCatalogEntry>();
        private List<SiteCatalogEntry> _archiveCandidates = new List<SiteCatalogEntry>();
        private List<SiteCatalogEntry> _archivedSites = new List<SiteCatalogEntry>();
        private List<SiteCatalogEntry> _filteredSites = new List<SiteCatalogEntry>();
        private ArchiveQueueData _archiveQueue = new ArchiveQueueData();

        public SiteManagementPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(0, 20, 0, 0);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost, ExecutionHistoryService history)
        {
            if (_initialized) return;
            _initialized = true;
            _config = config;
            _psHost = psHost;
            _history = history;
            _siteData = new SiteDataService(config);
            BuildLayout();
            _ = LoadAllDataAsync(false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        public void ShowView(string view)
        {
            _currentView = view;
            _scopeViewPanel.Visible = view == "scope";
            _dataViewPanel.Visible = !_scopeViewPanel.Visible;

            if (view != "scope")
            {
                ConfigureToolbarForView();
                ApplyFilterAsync();
            }
        }

        public void RefreshAfterReset()
        {
            if (!_initialized || IsDisposed)
                return;

            _ = RefreshCurrentViewAsync(true);
        }

        private void BuildLayout()
        {
            Controls.Clear();

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
            };
            Controls.Add(_contentHost);

            int contentW = Math.Max(720, ClientSize.Width - Padding.Horizontal);
            int contentH = Math.Max(540, ClientSize.Height - Padding.Vertical);

            BuildDataView(contentW, contentH);
            BuildScopeView(contentW, contentH);
        }

        private void BuildDataView(int contentW, int contentH)
        {
            _dataViewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = Padding.Empty
            };
            _contentHost.Controls.Add(_dataViewPanel);

            _dataLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _dataLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _dataLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _dataLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _queueFooterRowStyle = new RowStyle(SizeType.Absolute, 0f);
            _dataLayout.RowStyles.Add(_queueFooterRowStyle);
            _dataViewPanel.Controls.Add(_dataLayout);

            var topHost = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            topHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            topHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            topHost.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _dataViewPanel.Controls.Add(topHost);

            var titleHost = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8)
            };
            topHost.Controls.Add(titleHost, 0, 2);

            _lblViewTitle = MakeLabel("Site Catalog", AppTheme.FontTitle, AppTheme.TextPrimary, 0, 0);
            titleHost.Controls.Add(_lblViewTitle);

            _lblHint = MakeLabel("", AppTheme.FontBody, AppTheme.TextSecondary, 0, 28);
            titleHost.Controls.Add(_lblHint);

            _toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 44,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 8)
            };
            topHost.Controls.Add(_toolbar, 0, 1);

            _txtSearch = new TextBox { Location = new Point(0, 8), Size = new Size(260, 24) };
            AppTheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => ApplyFilterAsync();
            _toolbar.Controls.Add(_txtSearch);

            _btnFilterStatus = new FlatButton { Text = "All statuses \u25BC", Size = new Size(160, 26), Location = new Point(272, 8) };
            _btnFilterStatus.SetGhostStyle();
            _btnFilterStatus.Click += (s, e) => ToggleFilterDropDown(_btnFilterStatus, _chkStatusDropDown);
            _toolbar.Controls.Add(_btnFilterStatus);

            _btnFilterArchive = new FlatButton { Text = "All archive states \u25BC", Size = new Size(170, 26), Location = new Point(440, 8) };
            _btnFilterArchive.SetGhostStyle();
            _btnFilterArchive.Click += (s, e) => ToggleFilterDropDown(_btnFilterArchive, _chkArchiveDropDown);
            _toolbar.Controls.Add(_btnFilterArchive);

            // Status filter popup — ToolStripDropDown floats above all controls
            _chkStatusDropDown = CreateFilterDropDown(out _chkStatus, new[] { "Active only", "Active (unlocked)", "All except Archived", "Archived", "Locked", "Inactive only", "Ownerless only", "Versions > 0" });
            ApplyDefaultStatusFilterSelection();

            // Archive filter popup
            _chkArchiveDropDown = CreateFilterDropDown(out _chkArchive, new[] { "Not archived", "Archived", "Candidates only" });

            _txtArchiveAdminUrl = new TextBox { Location = new Point(596, 8), Size = new Size(280, 24), Visible = false };
            AppTheme.StyleTextBox(_txtArchiveAdminUrl);
            _toolbar.Controls.Add(_txtArchiveAdminUrl);

            int btnRight = contentW;
            _btnRefresh = new FlatButton { Text = "Refresh", Size = new Size(78, 26), Location = new Point(btnRight - 456, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnRefresh.SetGhostStyle();
            _btnRefresh.Click += (s, e) => _ = RefreshCurrentViewAsync(true);
            _toolbar.Controls.Add(_btnRefresh);

            _btnExport = new FlatButton { Text = "Export CSV", Size = new Size(88, 26), Location = new Point(btnRight - 372, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnExport.SetGhostStyle();
            _btnExport.Click += BtnExportCsv_Click;
            _toolbar.Controls.Add(_btnExport);

            _btnDetails = new FlatButton { Text = "Details", Size = new Size(72, 26), Location = new Point(btnRight - 382, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnDetails.SetGhostStyle();
            _btnDetails.Click += (s, e) => OpenSelectedDetail();
            _toolbar.Controls.Add(_btnDetails);

            _btnAddToQueue = new FlatButton { Text = "Select All", Size = new Size(90, 26), Location = new Point(btnRight - 304, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnAddToQueue.SetGhostStyle();
            _btnAddToQueue.Click += (s, e) => SelectAllVisibleForQueue();
            _toolbar.Controls.Add(_btnAddToQueue);

            _btnConfirmQueueSelection = new FlatButton { Text = "Confirm Add", Size = new Size(104, 26), Location = new Point(btnRight - 208, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnConfirmQueueSelection.SetAccentColor(AppTheme.AccentGold);
            _btnConfirmQueueSelection.Click += BtnAddToQueue_Click;
            _toolbar.Controls.Add(_btnConfirmQueueSelection);

            _btnAddSelectedToSkip = new FlatButton { Text = "Add to Skip", Size = new Size(102, 26), Location = new Point(btnRight - 316, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnAddSelectedToSkip.SetAccentColor(AppTheme.AccentGold);
            _btnAddSelectedToSkip.Click += (s, e) => AddSelectedSitesToSkipList();
            _toolbar.Controls.Add(_btnAddSelectedToSkip);

            _btnAddSelectedToArchive = new FlatButton { Text = "Add to Archive", Size = new Size(118, 26), Location = new Point(btnRight - 430, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnAddSelectedToArchive.SetAccentColor(AppTheme.AccentGreen);
            _btnAddSelectedToArchive.Click += BtnAddToQueue_Click;
            _toolbar.Controls.Add(_btnAddSelectedToArchive);

            _btnRemoveFromQueue = new FlatButton { Text = "Remove", Size = new Size(72, 26), Location = new Point(btnRight - 180, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right, Visible = false };
            _btnRemoveFromQueue.SetDangerStyle();
            _btnRemoveFromQueue.Click += BtnRemoveFromQueue_Click;
            _toolbar.Controls.Add(_btnRemoveFromQueue);

            _btnRunQueue = new FlatButton { Text = "Run Archive", Size = new Size(100, 26), Location = new Point(btnRight - 100, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right, Visible = false };
            _btnRunQueue.SetAccentColor(AppTheme.AccentGreen);
            _btnRunQueue.Click += async (s, e) => await RunArchiveQueueAsync();
            _toolbar.Controls.Add(_btnRunQueue);
            _toolbar.Resize += (s, e) => LayoutToolbarControls();

            // Period filter buttons + SAM legend (candidates view only)
            _periodFilterPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 32,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 4),
                Visible = false
            };

            int pfx = 0;
            var periodLabel = MakeLabel("Period:", AppTheme.FontBody, AppTheme.TextSecondary, pfx, 6);
            _periodFilterPanel.Controls.Add(periodLabel);
            pfx += 52;

            foreach (var pd in new[] { ("All", 0), ("7D", 7), ("30D", 30), ("60D", 60), ("90D", 90), ("180D", 180) })
            {
                var btn = new FlatButton { Text = pd.Item1, Size = new Size(48, 24), Location = new Point(pfx, 4), Tag = pd.Item2 };
                btn.SetGhostStyle();
                if (pd.Item2 == 0) btn.SetAccentColor(AppTheme.AccentGold);
                btn.Click += PeriodFilterButton_Click;
                _periodFilterPanel.Controls.Add(btn);
                pfx += 52;
            }

            // SAM legend
            var samSwatch = new Panel { Size = new Size(14, 14), Location = new Point(pfx + 16, 9), BackColor = Color.FromArgb(25, 45, 35) };
            _periodFilterPanel.Controls.Add(samSwatch);
            _lblSamLegend = MakeLabel("SAM Inactive / Ownerless", AppTheme.FontSmall, AppTheme.TextSecondary, pfx + 34, 7);
            _lblSamLegend.AutoSize = true;
            _periodFilterPanel.Controls.Add(_lblSamLegend);

            topHost.Controls.Add(_periodFilterPanel, 0, 3);

            // Summary stats bar (dashboard-style chips)
            _summaryBar = new Panel
            {
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(120, AppTheme.BgHeader),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 0, 0, 8),
                Visible = false
            };
            topHost.Controls.Add(_summaryBar, 0, 0);

            _summaryGrid = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 8, 10, 2),
                Margin = new Padding(0, 0, 0, 2),
                AutoScroll = false
            };

            _summaryBar.Controls.Add(_summaryGrid);

            _summaryCards.Clear();
            _summaryCards.Add(CreateSummaryChip("🌐", "Total de Sites", AppTheme.AccentCyan, out _lblSummaryTotalSites));
            _summaryCards.Add(CreateSummaryChip("💾", "Storage Total", AppTheme.AccentCyan, out _lblSummaryStorage));
            _summaryCards.Add(CreateSummaryChip("📦", "Versões Total", AppTheme.AccentCyan, out _lblSummaryVersions));
            ReflowSummaryCards();
            LayoutToolbarControls();

            Resize -= SiteManagementPanel_Resize;
            Resize += SiteManagementPanel_Resize;

            _lblSummary = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextSecondary,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 0, 0, 4),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = true
            };
            _summaryBar.Controls.Add(_lblSummary);

            _queueFooterHost = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 72,
                BackColor = Color.Transparent,
                Visible = false
            };
            _dataLayout.Controls.Add(_queueFooterHost, 0, 2);

            _lblArchiveStatus = new Label
            {
                Text = "",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Height = 20,
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent,
                Visible = true
            };
            _queueFooterHost.Controls.Add(_lblArchiveStatus);

            _archiveConsole = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = true
            };
            _queueFooterHost.Controls.Add(_archiveConsole);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = AppTheme.BgDark
            };
            _dataLayout.Controls.Add(gridContainer, 0, 1);

            _loadingBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 3,
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 35,
                Visible = false
            };
            gridContainer.Controls.Add(_loadingBar);
            _loadingBar.BringToFront();

            _grid = new VirtualSiteGrid
            {
                Dock = DockStyle.Fill,
                Visible = false
            };
            _grid.DetailRequested += (s, site) => ShowSiteDetail(site);
            _grid.SelectionUpdated += (s, e) => UpdateSummaryBar();
            _grid.QueueToggled += Grid_QueueToggled;
            _grid.VisibleDataChanged += (s, e) =>
            {
                _filteredSites = _grid.GetVisibleSites();
                UpdateSummaryBar();
            };
            _grid.Resize += (s, e) => AdjustGridColumnsForViewport();
            _grid.Scroll += (s, e) => _grid.Invalidate();
            gridContainer.Controls.Add(_grid);
            AdjustGridColumnsForViewport();

            // Loading overlay on top of grid area
            _loadingHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Visible = false
            };
            gridContainer.Controls.Add(_loadingHost);
            _loadingHost.BringToFront();

            var centerLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = Color.Transparent
            };
            centerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            centerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            var content = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                WrapContents = false,
                Anchor = AnchorStyles.None,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            _lblLoading = new Label
            {
                Text = "Loading sites data...",
                Font = new Font("Segoe UI Semibold", 12f),
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Visible = true
            };

            _progressBar = new ProgressBar
            {
                Style = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Width = 300,
                Visible = true
            };

            content.Controls.Add(_lblLoading);
            content.Controls.Add(_progressBar);
            centerLayout.Controls.Add(content, 0, 1);
            _loadingHost.Controls.Add(centerLayout);

            _dataLayout.Controls.Add(topHost, 0, 0);
        }

        private void BuildScopeView(int contentW, int contentH)
        {
            _scopeViewPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(contentW, contentH),
                BackColor = Color.Transparent,
                Visible = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };
            _contentHost.Controls.Add(_scopeViewPanel);

            _scopeViewPanel.Controls.Add(MakeLabel("Execution Scope", AppTheme.FontTitle, AppTheme.TextPrimary, 0, 0));
            _scopeViewPanel.Controls.Add(MakeLabel("Target Sites are explicit allow-list entries. Skip Sites are explicit exclusions.", AppTheme.FontBody, AppTheme.TextSecondary, 0, 28));

            // Warning banner: hidden once user adds at least one Target Site
            _scopeWarningBanner = CreateScopeWarningBanner(contentW);
            _scopeWarningBanner.Location = new Point(0, 52);
            _scopeViewPanel.Controls.Add(_scopeWarningBanner);

            // Button bar above panels
            var btnBar = new Panel
            {
                Location = new Point(0, 102),
                Size = new Size(contentW, 34),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            _scopeViewPanel.Controls.Add(btnBar);

            int bx = 0;
            _btnScopeImportTarget = new FlatButton { Text = "Import Target", Size = new Size(100, 26), Location = new Point(bx, 4) };
            _btnScopeImportTarget.SetGhostStyle(); _btnScopeImportTarget.Click += (s, e) => ImportScopeCsv(true); btnBar.Controls.Add(_btnScopeImportTarget); bx += 106;

            _btnScopeSaveTarget = new FlatButton { Text = "Save Target", Size = new Size(90, 26), Location = new Point(bx, 4) };
            _btnScopeSaveTarget.SetAccentColor(AppTheme.AccentCyan); _btnScopeSaveTarget.Click += (s, e) => SaveScopeList(true); btnBar.Controls.Add(_btnScopeSaveTarget); bx += 96;

            _btnScopeImportSkip = new FlatButton { Text = "Import Skip", Size = new Size(90, 26), Location = new Point(bx, 4) };
            _btnScopeImportSkip.SetGhostStyle(); _btnScopeImportSkip.Click += (s, e) => ImportScopeCsv(false); btnBar.Controls.Add(_btnScopeImportSkip); bx += 96;

            _btnScopeSaveSkip = new FlatButton { Text = "Save Skip", Size = new Size(80, 26), Location = new Point(bx, 4) };
            _btnScopeSaveSkip.SetAccentColor(AppTheme.AccentGold); _btnScopeSaveSkip.Click += (s, e) => SaveScopeList(false); btnBar.Controls.Add(_btnScopeSaveSkip); bx += 86;

            _btnScopeExportCsv = new FlatButton { Text = "Export CSV", Size = new Size(84, 26), Location = new Point(bx, 4) };
            _btnScopeExportCsv.SetGhostStyle(); _btnScopeExportCsv.Click += (s, e) => ExportScopeCsv(GetFocusedScopeGrid() == _targetGrid); btnBar.Controls.Add(_btnScopeExportCsv); bx += 90;

            _btnScopeAddTarget = new FlatButton { Text = "+ Target", Size = new Size(72, 26), Location = new Point(bx, 4) };
            _btnScopeAddTarget.SetAccentColor(AppTheme.AccentCyan); _btnScopeAddTarget.Click += (s, e) => AddScopeEntryByPopup(true); btnBar.Controls.Add(_btnScopeAddTarget); bx += 78;

            _btnScopeAddSkip = new FlatButton { Text = "+ Skip", Size = new Size(60, 26), Location = new Point(bx, 4) };
            _btnScopeAddSkip.SetAccentColor(AppTheme.AccentGold); _btnScopeAddSkip.Click += (s, e) => AddScopeEntryByPopup(false); btnBar.Controls.Add(_btnScopeAddSkip); bx += 66;

            _btnScopeEditDesc = new FlatButton { Text = "\u270E Edit", Size = new Size(66, 26), Location = new Point(bx, 4) };
            _btnScopeEditDesc.SetGhostStyle(); _btnScopeEditDesc.Click += (s, e) => EditScopeDescriptionForFocused(); btnBar.Controls.Add(_btnScopeEditDesc); bx += 72;

            _btnScopeRemove = new FlatButton { Text = "\u2716 Remove", Size = new Size(80, 26), Location = new Point(bx, 4) };
            _btnScopeRemove.SetAccentColor(AppTheme.AccentRed); _btnScopeRemove.Click += (s, e) => RemoveScopeForFocused(); btnBar.Controls.Add(_btnScopeRemove);

            int panelTop = 140;
            var cardsHost = new SplitContainer
            {
                Location = new Point(0, panelTop),
                Size = new Size(contentW, contentH - panelTop),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent,
                Orientation = Orientation.Vertical,
                BorderStyle = BorderStyle.None,
                FixedPanel = FixedPanel.None,
                IsSplitterFixed = false,
                SplitterWidth = 8,
                SplitterDistance = Math.Max(100, contentW / 2)
            };
            cardsHost.Resize += (s, e) =>
            {
                if (cardsHost.Width > 220)
                    cardsHost.SplitterDistance = cardsHost.Width / 2;
            };
            _scopeViewPanel.Controls.Add(cardsHost);

            var targetCard = new GlassPanel { Dock = DockStyle.Fill, AccentLeft = AppTheme.AccentCyan, Margin = new Padding(0, 0, 6, 0) };
            cardsHost.Panel1.Controls.Add(targetCard);
            var targetHeader = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };
            targetCard.Controls.Add(targetHeader);
            targetHeader.Controls.Add(new Label { Text = "TARGET SITES", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = AppTheme.AccentCyan, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 10) });
            targetHeader.Controls.Add(new Label { Text = "Process only these sites when provided", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 28) });
            _targetGrid = CreateScopeGrid();
            _targetGrid.Dock = DockStyle.Fill;
            _targetGrid.Margin = new Padding(14, 0, 14, 14);
            targetCard.Controls.Add(_targetGrid);
            _targetGrid.BringToFront();

            var skipCard = new GlassPanel { Dock = DockStyle.Fill, AccentLeft = AppTheme.AccentGold, Margin = new Padding(6, 0, 0, 0) };
            cardsHost.Panel2.Controls.Add(skipCard);
            var skipHeader = new Panel { Dock = DockStyle.Top, Height = 48, BackColor = Color.Transparent };
            skipCard.Controls.Add(skipHeader);
            skipHeader.Controls.Add(new Label { Text = "SKIP SITES", Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 10) });
            skipHeader.Controls.Add(new Label { Text = "Never process these sites", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 28) });
            _skipGrid = CreateScopeGrid();
            _skipGrid.Dock = DockStyle.Fill;
            _skipGrid.Margin = new Padding(14, 0, 14, 14);
            skipCard.Controls.Add(_skipGrid);
            _skipGrid.BringToFront();

            // Wire banner visibility to target grid contents (hide when user adds an entry)
            _targetGrid.RowsAdded += (s, e) => UpdateScopeWarningVisibility();
            _targetGrid.RowsRemoved += (s, e) => UpdateScopeWarningVisibility();
            UpdateScopeWarningVisibility();
        }

        private Panel CreateScopeWarningBanner(int width)
        {
            var banner = new Panel
            {
                Size = new Size(width, 44),
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            banner.Paint += (s, pe) =>
            {
                var g = pe.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var rect = new Rectangle(0, 0, banner.Width - 1, banner.Height - 1);
                using (var path = AppTheme.CreateRoundedRect(rect, 8))
                {
                    using (var bgBrush = new SolidBrush(Color.FromArgb(30, 26, 14)))
                        g.FillPath(bgBrush, path);
                    using (var borderPen = new Pen(Color.FromArgb(80, AppTheme.AccentGold), 1f))
                        g.DrawPath(borderPen, path);
                }
                using (var accentBrush = new SolidBrush(AppTheme.AccentGold))
                    g.FillRectangle(accentBrush, 0, 6, 3, banner.Height - 12);
            };
            banner.Controls.Add(new Label
            {
                Text = "\u26A0  If Target Sites is empty, all sites in the tenant will be processed (minus Skip Sites).",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentGold,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 13)
            });
            return banner;
        }

        private void UpdateScopeWarningVisibility()
        {
            if (_scopeWarningBanner == null || _targetGrid == null) return;
            bool empty = _targetGrid.Rows.Count == 0;
            if (_scopeWarningBanner.Visible != empty)
                _scopeWarningBanner.Visible = empty;
        }

        private DataGridView CreateScopeGrid()
        {
            var grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Both,
                MultiSelect = true,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            AppTheme.StyleDataGrid(grid);

            // Checkbox column
            var chkCol = new DataGridViewCheckBoxColumn
            {
                Name = "Select",
                HeaderText = "\u2610",
                Width = 36,
                MinimumWidth = 36,
                FillWeight = 1,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                Resizable = DataGridViewTriState.False,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            grid.Columns.Add(chkCol);

            grid.Columns.Add("SiteUrl", "Site URL");
            grid.Columns.Add("Reason", "Reason / Notes");
            grid.Columns["SiteUrl"].FillWeight = 72;
            grid.Columns["SiteUrl"].SortMode = DataGridViewColumnSortMode.Automatic;
            grid.Columns["Reason"].FillWeight = 28;
            grid.Columns["Reason"].SortMode = DataGridViewColumnSortMode.Automatic;

            // Single-click checkbox toggling — commit dirty state immediately
            grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (grid.IsCurrentCellDirty && grid.CurrentCell?.ColumnIndex == grid.Columns["Select"].Index)
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            // Also handle direct click on checkbox cell to ensure it toggles
            grid.CellClick += (s, e) =>
            {
                if (e.ColumnIndex == grid.Columns["Select"].Index && e.RowIndex >= 0)
                {
                    var cell = grid.Rows[e.RowIndex].Cells["Select"];
                    bool current = cell.Value is true;
                    cell.Value = !current;
                    grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

            // Column header click on checkbox → toggle all
            grid.ColumnHeaderMouseClick += (s, e) =>
            {
                if (e.ColumnIndex == grid.Columns["Select"].Index)
                    ToggleScopeSelectAll(grid);
            };

            // Track which grid is "active" for toolbar buttons
            grid.Enter += (s, e) => UpdateScopeButtonStates();
            grid.SelectionChanged += (s, e) => UpdateScopeButtonStates();
            grid.CellValueChanged += (s, e) => UpdateScopeButtonStates();

            grid.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Delete)
                    return;
                RemoveScopeSelectedRows(grid);
            };

            // Context menu
            grid.CellMouseClick += (s, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0)
                    return;

                // Select the row under cursor if not already selected
                if (!grid.Rows[e.RowIndex].Selected)
                {
                    grid.ClearSelection();
                    grid.Rows[e.RowIndex].Selected = true;
                }

                ShowScopeContextMenu(grid, Cursor.Position);
            };

            return grid;
        }

        private void ToggleScopeSelectAll(DataGridView grid)
        {
            bool anyUnchecked = false;
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["Select"].Value == null || !(bool)row.Cells["Select"].Value)
                { anyUnchecked = true; break; }
            }
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (!row.IsNewRow)
                    row.Cells["Select"].Value = anyUnchecked;
            }
            grid.Columns["Select"].HeaderText = anyUnchecked ? "\u2611" : "\u2610";
            grid.InvalidateColumn(grid.Columns["Select"].Index);
        }

        private List<DataGridViewRow> GetScopeCheckedOrSelectedRows(DataGridView grid)
        {
            var rows = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow) continue;
                if (row.Cells["Select"].Value is true)
                    rows.Add(row);
            }
            if (rows.Count == 0)
            {
                foreach (DataGridViewRow row in grid.SelectedRows)
                    if (!row.IsNewRow) rows.Add(row);
            }
            return rows;
        }

        private void ShowScopeContextMenu(DataGridView grid, Point screenPoint)
        {
            bool isTarget = grid == _targetGrid;
            var menu = new ContextMenuStrip
            {
                ShowImageMargin = false,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary
            };

            var rows = GetScopeCheckedOrSelectedRows(grid);
            int count = rows.Count;

            var editItem = new ToolStripMenuItem($"Edit Description ({count})");
            editItem.Enabled = count > 0;
            editItem.Click += (s2, e2) => EditScopeDescription(grid);
            menu.Items.Add(editItem);

            menu.Items.Add(new ToolStripSeparator());

            var removeItem = new ToolStripMenuItem($"Remove ({count})");
            removeItem.Enabled = count > 0;
            removeItem.ForeColor = AppTheme.AccentRed;
            removeItem.Click += (s2, e2) => RemoveScopeCheckedOrSelected(grid);
            menu.Items.Add(removeItem);

            menu.Items.Add(new ToolStripSeparator());

            var selectAll = new ToolStripMenuItem("Select All");
            selectAll.Click += (s2, e2) => ToggleScopeSelectAll(grid);
            menu.Items.Add(selectAll);

            menu.Closed += (s2, e2) => BeginInvoke((Action)(() => menu.Dispose()));
            menu.Show(screenPoint);
        }

        private void RemoveScopeCheckedOrSelected(DataGridView grid)
        {
            var rows = GetScopeCheckedOrSelectedRows(grid);
            if (rows.Count == 0) return;

            if (MessageBox.Show($"Remove {rows.Count} selected site(s)?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            foreach (var row in rows)
                if (!row.IsNewRow) grid.Rows.Remove(row);
        }

        private void RemoveScopeSelectedRows(DataGridView grid)
        {
            var toRemove = new List<DataGridViewRow>();
            foreach (DataGridViewRow row in grid.SelectedRows)
                if (!row.IsNewRow) toRemove.Add(row);
            foreach (var row in toRemove)
                grid.Rows.Remove(row);
        }

        private void EditScopeDescription(DataGridView grid)
        {
            var rows = GetScopeCheckedOrSelectedRows(grid);
            if (rows.Count == 0) return;

            string current = rows.Count == 1 ? (rows[0].Cells["Reason"].Value?.ToString() ?? "") : "";
            using (var dlg = new Form
            {
                Text = "Edit Description",
                Size = new Size(480, 180),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(16, 14, 16, 10) };
                dlg.Controls.Add(host);

                host.Controls.Add(new Label
                {
                    Text = rows.Count == 1 ? "Description / Notes:" : $"Set description for {rows.Count} sites:",
                    Font = AppTheme.FontBody,
                    ForeColor = AppTheme.TextSecondary,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Location = new Point(0, 2)
                });

                var txt = new TextBox { Location = new Point(0, 26), Size = new Size(430, 24), Text = current, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                AppTheme.StyleTextBox(txt);
                host.Controls.Add(txt);

                var btnOk = new FlatButton { Text = "OK", Size = new Size(70, 28), Location = new Point(360, 64), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
                btnOk.SetAccentColor(AppTheme.AccentCyan);
                host.Controls.Add(btnOk);

                var btnCancel = new FlatButton { Text = "Cancel", Size = new Size(80, 28), Location = new Point(272, 64), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
                btnCancel.SetGhostStyle();
                host.Controls.Add(btnCancel);

                btnCancel.Click += (s, e) => dlg.Close();
                btnOk.Click += (s, e) =>
                {
                    string val = txt.Text.Trim();
                    foreach (var row in rows)
                        row.Cells["Reason"].Value = val;
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                };

                txt.Focus();
                dlg.ShowDialog(ParentForm);
            }
        }

        private async Task LoadAllDataAsync(bool forceReload)
        {
            _suspendSummaryReflow = true;
            _loadingHost.Visible = true;
            if (_loadingBar != null)
                _loadingBar.Visible = true;
            if (_grid != null)
                _grid.Visible = false;
            _loadingHost.BringToFront();
            if (_loadingBar != null)
                _loadingBar.BringToFront();
            _lblLoading.Text = "Loading sites data...";

            try
            {
                await Task.Run(() =>
                {
                    _catalogSites = _siteData.LoadCatalogSites(forceReload);
                });
                _lblLoading.Text = "Loading archive data...";
                await Task.Run(() =>
                {
                    _archiveCandidates = _siteData.LoadArchiveCandidates();
                    _archivedSites = _siteData.LoadArchivedSites();
                    _archiveQueue = _siteData.LoadArchiveQueue();
                });

                _dataLoaded = true;
                LoadScopeGrids();
                LoadHistorySiteUrls();
                if (!string.IsNullOrWhiteSpace(_psHost?.AdminUrl))
                    _txtArchiveAdminUrl.Text = _psHost.AdminUrl;
            }
            catch (Exception ex)
            {
                _lblSummary.Text = "Error loading data: " + ex.Message;
            }
            finally
            {
                _suspendSummaryReflow = false;

                if (_summaryBar != null)
                    _summaryBar.Visible = true;

                _dataViewPanel?.SuspendLayout();
                try
                {
                    _dataViewPanel?.PerformLayout();
                    _summaryBar?.PerformLayout();
                    _summaryGrid?.PerformLayout();
                    ReflowSummaryCards();
                    ShowView(_currentView);
                }
                finally
                {
                    _dataViewPanel?.ResumeLayout(true);
                }

                RunOnUiThread(() =>
                {
                    if (IsDisposed)
                        return;

                    ReflowSummaryCards();
                    AdjustGridColumnsForViewport();
                    _grid?.Refresh();
                });

                _loadingHost.Visible = false;
                if (_loadingBar != null)
                    _loadingBar.Visible = false;
            }
        }

        private void LoadScopeGrids()
        {
            LoadScopeIntoGrid(_targetGrid, "IncludeSites.csv");
            LoadScopeIntoGrid(_skipGrid, "ExcludeSites.csv");
            UpdateScopeButtonStates();
        }

        private void ConfigureToolbarForView()
        {
            _txtSearch.Visible = true;
            _btnFilterStatus.Visible = true;
            _btnFilterArchive.Visible = true;
            _btnRefresh.Visible = true;
            _btnExport.Visible = true;
            _btnDetails.Visible = true;
            _btnAddToQueue.Visible = _currentView == "catalog" || _currentView == "candidates";
            _btnConfirmQueueSelection.Visible = false;
            _btnAddSelectedToSkip.Visible = _currentView == "catalog" || _currentView == "candidates";
            _btnAddSelectedToArchive.Visible = _currentView == "catalog" || _currentView == "candidates";
            _btnRemoveFromQueue.Visible = _currentView == "queue";
            _btnRunQueue.Visible = _currentView == "queue";
            _txtArchiveAdminUrl.Visible = _currentView == "queue";
            _periodFilterPanel.Visible = _currentView == "candidates";
            if (_currentView != "candidates")
            {
                _periodFilterDays = 0;
            }
            else
            {
                // Reset period button highlights
                foreach (Control c in _periodFilterPanel.Controls)
                {
                    if (c is FlatButton fb && fb.Tag is int d)
                    {
                        if (d == _periodFilterDays)
                            fb.SetAccentColor(AppTheme.AccentGold);
                        else
                            fb.SetGhostStyle();
                    }
                }
            }
            bool isQueueView = _currentView == "queue";
            _queueFooterHost.Visible = isQueueView;
            _queueFooterHost.Height = isQueueView ? 72 : 0;
            if (_queueFooterRowStyle != null)
                _queueFooterRowStyle.Height = isQueueView ? 72f : 0f;
            if (_archiveConsole != null)
                _archiveConsole.Visible = isQueueView;
            if (_lblArchiveStatus != null)
                _lblArchiveStatus.Visible = isQueueView;

            // Archive Candidates uses first-column select boxes; other views keep details icon.
            bool showCheckboxes = _currentView == "candidates";
            var queuedUrls = new HashSet<string>(
                _archiveQueue.Sites.Where(s => s.Status != "Archived" && s.Status != "Failed")
                    .Select(s => s.SiteUrl), StringComparer.OrdinalIgnoreCase);
            _grid.SetQueueMode(showCheckboxes, queuedUrls);
            _grid.ClearColumnFiltersAndSort();
            _grid.EnsureHeadersVisible();

            // Close any open popups
            _chkStatusDropDown.Close();
            _chkArchiveDropDown.Close();

            // Reset filter checks
            ApplyDefaultStatusFilterSelection();
            for (int i = 0; i < _chkArchive.Items.Count; i++) _chkArchive.SetItemChecked(i, false);
            UpdateFilterButtonText();

            if (_currentView == "queue")
            {
                _lblViewTitle.Text = "Archive Queue";
                _lblHint.Text = "Queue sites, execute archive, and follow status.";
            }
            else if (_currentView == "catalog")
            {
                _lblViewTitle.Text = "Site Catalog";
                _lblHint.Text = "Search, filter and use first-column details icon. Use Add to Skip / Add to Archive for selected rows.";
                _btnAddToQueue.Text = "Select All";
            }
            else if (_currentView == "candidates")
            {
                _lblViewTitle.Text = "Archive Candidates";
                _lblHint.Text = "Use first-column select boxes and add selected rows directly to Skip or Archive Queue.";
                _btnAddToQueue.Text = "Select All";
            }
            else
            {
                _lblViewTitle.Text = "Archived Sites";
                _lblHint.Text = "Sites currently in archived state.";
            }

            LayoutToolbarControls();
            _dataViewPanel?.PerformLayout();
            _dataLayout?.PerformLayout();
            _grid?.BringToFront();
        }

        private void LayoutToolbarControls()
        {
            if (_toolbar == null || _toolbar.IsDisposed)
                return;

            int right = _toolbar.ClientSize.Width - 8;
            const int gap = 6;

            Action<Control> placeRight = c =>
            {
                if (c == null || !c.Visible)
                    return;
                right -= c.Width;
                c.Location = new Point(Math.Max(0, right), 8);
                right -= gap;
            };

            if (_currentView == "queue")
            {
                placeRight(_btnRunQueue);
                placeRight(_btnRemoveFromQueue);
            }
            else
            {
                placeRight(_btnAddSelectedToArchive);
                placeRight(_btnAddSelectedToSkip);
                placeRight(_btnAddToQueue);
            }

            placeRight(_btnDetails);
            placeRight(_btnExport);
            placeRight(_btnRefresh);

            int leftX = 0;
            int statusW = 160;
            int archiveW = 170;
            int minSearchW = 120;
            int desiredSearchW = 260;

            int maxSearchW = Math.Max(minSearchW, right - (statusW + archiveW + 24));
            int searchW = Math.Min(desiredSearchW, maxSearchW);

            _txtSearch.Location = new Point(leftX, 8);
            _txtSearch.Size = new Size(searchW, 24);

            _btnFilterStatus.Location = new Point(_txtSearch.Right + 12, 8);
            _btnFilterStatus.Size = new Size(statusW, 26);

            _btnFilterArchive.Location = new Point(_btnFilterStatus.Right + 8, 8);
            _btnFilterArchive.Size = new Size(archiveW, 26);

            if (_txtArchiveAdminUrl != null && _txtArchiveAdminUrl.Visible)
            {
                int x = _btnFilterArchive.Right + 10;
                int w = Math.Max(160, right - x + 4);
                _txtArchiveAdminUrl.Location = new Point(x, 8);
                _txtArchiveAdminUrl.Size = new Size(w, 24);
            }
        }

        private ToolStripDropDown CreateFilterDropDown(out CheckedListBox clb, string[] items)
        {
            clb = new CheckedListBox
            {
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                Font = AppTheme.FontBody,
                IntegralHeight = false,
                Size = new Size(190, Math.Min(items.Length * 22 + 4, 200))
            };
            foreach (var item in items) clb.Items.Add(item);
            clb.ItemCheck += (s, e) => RunOnUiThread(() =>
            {
                UpdateFilterButtonText();
                ApplyFilterAsync();
            });

            var host = new ToolStripControlHost(clb)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false,
                Size = clb.Size
            };

            var dropDown = new ToolStripDropDown
            {
                Padding = new Padding(2),
                AutoSize = true,
                BackColor = AppTheme.Border
            };
            dropDown.Items.Add(host);
            return dropDown;
        }

        private void ApplyDefaultStatusFilterSelection()
        {
            if (_chkStatus == null)
                return;

            for (int i = 0; i < _chkStatus.Items.Count; i++)
                _chkStatus.SetItemChecked(i, false);

            int defaultIndex = _chkStatus.Items.IndexOf("All except Archived");
            if (defaultIndex >= 0)
                _chkStatus.SetItemChecked(defaultIndex, true);
        }

        private void PeriodFilterButton_Click(object sender, EventArgs e)
        {
            if (sender is FlatButton btn && btn.Tag is int days)
            {
                _periodFilterDays = days;
                // Highlight only the active button
                foreach (Control c in _periodFilterPanel.Controls)
                {
                    if (c is FlatButton fb && fb.Tag is int)
                    {
                        if ((int)fb.Tag == days)
                            fb.SetAccentColor(AppTheme.AccentGold);
                        else
                            fb.SetGhostStyle();
                    }
                }
                ApplyFilterAsync();
            }
        }

        private void ToggleFilterDropDown(Control anchor, ToolStripDropDown dropDown)
        {
            // Close the other dropdown
            if (dropDown == _chkStatusDropDown)
                _chkArchiveDropDown.Close();
            else
                _chkStatusDropDown.Close();

            if (dropDown.Visible)
                dropDown.Close();
            else
                dropDown.Show(anchor, new Point(0, anchor.Height));
        }

        private void UpdateFilterButtonText()
        {
            var statusChecked = GetCheckedItems(_chkStatus);
            _btnFilterStatus.Text = statusChecked.Count == 0 ? "All statuses \u25BC" : $"{statusChecked.Count} status filter(s) \u25BC";

            var archiveChecked = GetCheckedItems(_chkArchive);
            _btnFilterArchive.Text = archiveChecked.Count == 0 ? "All archive states \u25BC" : $"{archiveChecked.Count} archive filter(s) \u25BC";
        }

        private static List<string> GetCheckedItems(CheckedListBox clb)
        {
            var items = new List<string>();
            if (clb == null || clb.IsDisposed)
                return items;

            foreach (var item in clb.CheckedItems)
                items.Add(item?.ToString() ?? string.Empty);
            return items;
        }

        private void ApplyFilterAsync()
        {
            if (!_dataLoaded) return;

            // Cancel any previous filtering task
            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            if (_currentView == "queue")
            {
                BindQueue();
                return;
            }

            string search = (_txtSearch.Text ?? string.Empty).Trim().ToLowerInvariant();
            var statusFilters = GetCheckedItems(_chkStatus);
            var archiveFilters = GetCheckedItems(_chkArchive);

            var source = _currentView == "catalog"
                ? _catalogSites
                : _currentView == "candidates"
                    ? _archiveCandidates
                    : _archivedSites;

            // Run filtering on background for large lists
            Task.Run(() =>
            {
                IEnumerable<SiteCatalogEntry> filtered = source;

                if (!string.IsNullOrWhiteSpace(search))
                {
                    filtered = filtered.Where(s =>
                        (s.Title ?? "").ToLowerInvariant().Contains(search) ||
                        (s.Url ?? "").ToLowerInvariant().Contains(search) ||
                        (s.Owner ?? "").ToLowerInvariant().Contains(search));
                }

                if (statusFilters.Count > 0)
                {
                    filtered = filtered.Where(s =>
                    {
                        foreach (var f in statusFilters)
                        {
                            switch (f)
                            {
                                case "Active only":
                                    if (!string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase)) return false;
                                    break;
                                case "Active (unlocked)":
                                    if (!(string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrWhiteSpace(s.LockState) || string.Equals(s.LockState, "Unlock", StringComparison.OrdinalIgnoreCase)))) return false;
                                    break;
                                case "All except Archived":
                                    if (!(string.IsNullOrWhiteSpace(s.ArchiveStatus) || string.Equals(s.ArchiveStatus, "NotArchived", StringComparison.OrdinalIgnoreCase))) return false;
                                    break;
                                case "Archived":
                                    if (string.IsNullOrWhiteSpace(s.ArchiveStatus) || string.Equals(s.ArchiveStatus, "NotArchived", StringComparison.OrdinalIgnoreCase)) return false;
                                    break;
                                case "Locked":
                                    if (string.IsNullOrWhiteSpace(s.LockState) || string.Equals(s.LockState, "Unlock", StringComparison.OrdinalIgnoreCase)) return false;
                                    break;
                                case "Inactive only":
                                    if (!s.IsInactive) return false;
                                    break;
                                case "Ownerless only":
                                    if (!(s.IsOwnerless || string.IsNullOrWhiteSpace(s.Owner))) return false;
                                    break;
                                case "Versions > 0":
                                    if (s.VersionCount <= 0) return false;
                                    break;
                            }
                        }
                        return true;
                    });
                }

                if (archiveFilters.Count > 0)
                {
                    filtered = filtered.Where(s =>
                    {
                        foreach (var f in archiveFilters)
                        {
                            switch (f)
                            {
                                case "Not archived":
                                    if (!(string.IsNullOrWhiteSpace(s.ArchiveStatus) || string.Equals(s.ArchiveStatus, "NotArchived", StringComparison.OrdinalIgnoreCase))) return false;
                                    break;
                                case "Archived":
                                    if (string.IsNullOrWhiteSpace(s.ArchiveStatus) || string.Equals(s.ArchiveStatus, "NotArchived", StringComparison.OrdinalIgnoreCase)) return false;
                                    break;
                                case "Candidates only":
                                    if (!s.IsCandidate) return false;
                                    break;
                            }
                        }
                        return true;
                    });
                }

                // Period filter for candidates view
                if (_currentView == "candidates" && _periodFilterDays > 0)
                {
                    var cutoff = DateTime.UtcNow.AddDays(-_periodFilterDays);
                    filtered = filtered.Where(s =>
                    {
                        if (string.IsNullOrWhiteSpace(s.EffectiveDate)) return true;
                        if (DateTime.TryParse(s.EffectiveDate, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var ed))
                            return ed >= cutoff;
                        return true;
                    });
                }

                if (token.IsCancellationRequested) return;
                var result = filtered.ToList();

                if (token.IsCancellationRequested) return;

                if (!IsDisposed && IsHandleCreated)
                {
                    RunOnUiThread(() =>
                    {
                        if (token.IsCancellationRequested) return;
                        _filteredSites = result;
                        _grid.SetData(result);
                        _grid.EnsureHeadersVisible();
                        _grid.Visible = true;
                        _grid.BringToFront();
                        if (_loadingHost != null) _loadingHost.Visible = false;
                        if (_loadingBar != null) _loadingBar.Visible = false;
                        AdjustGridColumnsForViewport();
                        UpdateSummaryBar();
                    });
                }
            }, token);
        }

        private void BindQueue()
        {
            string search = (_txtSearch.Text ?? string.Empty).Trim().ToLowerInvariant();

            var queueSource = _archiveQueue?.Sites ?? new List<ArchiveQueueItem>();
            int sourceCount = queueSource.Count;
            var validQueue = queueSource
                .Where(s => !string.IsNullOrWhiteSpace(s.SiteUrl))
                .ToList();

            IEnumerable<ArchiveQueueItem> filtered = validQueue;
            if (!string.IsNullOrWhiteSpace(search))
            {
                filtered = filtered.Where(s =>
                    (s.Title ?? "").ToLowerInvariant().Contains(search) ||
                    (s.SiteUrl ?? "").ToLowerInvariant().Contains(search) ||
                    (s.Owner ?? "").ToLowerInvariant().Contains(search));
            }

            var list = filtered.ToList();
            // Convert queue items to SiteCatalogEntry for grid display
            var entries = list.Select(q => new SiteCatalogEntry
            {
                Url = q.SiteUrl,
                Title = q.Title,
                StorageMB = q.StorageUsedMB,
                Owner = q.Owner,
                Status = q.Status,
                ArchiveStatus = q.ArchiveStatus,
                LockState = q.LockState,
                ArchivedAt = q.ArchivedAt
            }).ToList();

            _filteredSites = entries;
            _grid.SetData(entries);
            _grid.EnsureHeadersVisible();
            _grid.Visible = true;
            _grid.BringToFront();
            if (_loadingHost != null) _loadingHost.Visible = false;
            if (_loadingBar != null) _loadingBar.Visible = false;
            AdjustGridColumnsForViewport();
            UpdateSummaryBar();
            string updated = string.IsNullOrWhiteSpace(_archiveQueue.LastUpdated) ? "-" : _archiveQueue.LastUpdated;
            _lblArchiveStatus.Text = $"Queue source: ArchiveQueue.json | Rows in file: {sourceCount} | Valid rows (SiteUrl): {validQueue.Count} | Visible rows: {entries.Count} | Last updated: {updated}";
        }

        private async Task RefreshCurrentViewAsync(bool forceReload)
        {
            if (_currentView == "scope")
            {
                LoadScopeGrids();
                return;
            }

            _loadingHost.Visible = true;
            if (_loadingBar != null)
                _loadingBar.Visible = true;
            _loadingHost.BringToFront();
            if (_loadingBar != null)
                _loadingBar.BringToFront();
            _lblLoading.Text = "Refreshing...";
            try
            {
                await Task.Run(() =>
                {
                    if (_currentView == "catalog")
                        _catalogSites = _siteData.LoadCatalogSites(forceReload);
                    else if (_currentView == "candidates")
                        _archiveCandidates = _siteData.LoadArchiveCandidates();
                    else if (_currentView == "archived")
                        _archivedSites = _siteData.LoadArchivedSites();
                    else if (_currentView == "queue")
                        _archiveQueue = _siteData.LoadArchiveQueue();
                });
                ApplyFilterAsync();
            }
            finally
            {
                _loadingHost.Visible = false;
                if (_loadingBar != null)
                    _loadingBar.Visible = false;
            }
        }

        private void SelectAllVisibleForQueue()
        {
            var visible = _grid.GetVisibleSites();
            if (visible == null || visible.Count == 0)
                return;

            _grid.ClearSelection();
            for (int i = 0; i < visible.Count && i < _grid.RowCount; i++)
                _grid.Rows[i].Selected = true;
        }

        private void AddSelectedSitesToSkipList()
        {
            var urls = GetSelectedUrls();
            if (urls.Count == 0)
            {
                MessageBox.Show("Select at least one site row to add to Skip list.", "Skip List", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var items = _siteData.LoadScopeList("ExcludeSites.csv");
            int added = 0;
            foreach (var url in urls)
            {
                if (items.Any(i => string.Equals(i.SiteUrl, url, StringComparison.OrdinalIgnoreCase)))
                    continue;

                items.Add(new ScopeSiteItem
                {
                    SiteUrl = url,
                    Reason = "Added from Site Catalog"
                });
                added++;
            }

            _siteData.SaveScopeList("ExcludeSites.csv", items);
            if (_skipGrid != null)
                LoadScopeIntoGrid(_skipGrid, "ExcludeSites.csv");

            MessageBox.Show($"Added {added} site(s) to Skip list.", "Skip List", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private DataGridView GetFocusedScopeGrid()
        {
            if (_targetGrid != null && _targetGrid.Focused) return _targetGrid;
            if (_skipGrid != null && _skipGrid.Focused) return _skipGrid;
            // Default to whichever has selected rows
            if (_targetGrid != null && _targetGrid.SelectedRows.Count > 0) return _targetGrid;
            if (_skipGrid != null && _skipGrid.SelectedRows.Count > 0) return _skipGrid;
            return _targetGrid;
        }

        private void UpdateScopeButtonStates()
        {
            if (_btnScopeEditDesc == null) return;

            var focused = GetFocusedScopeGrid();
            bool hasSelection = focused != null && GetScopeCheckedOrSelectedRows(focused).Count > 0;

            _btnScopeEditDesc.Enabled = hasSelection;
            _btnScopeRemove.Enabled = hasSelection;

            // Save buttons enabled when respective grid has rows
            _btnScopeSaveTarget.Enabled = _targetGrid != null && _targetGrid.Rows.Count > 0;
            _btnScopeSaveSkip.Enabled = _skipGrid != null && _skipGrid.Rows.Count > 0;
        }

        private void EditScopeDescriptionForFocused()
        {
            var grid = GetFocusedScopeGrid();
            if (grid != null) EditScopeDescription(grid);
        }

        private void RemoveScopeForFocused()
        {
            var grid = GetFocusedScopeGrid();
            if (grid != null) RemoveScopeCheckedOrSelected(grid);
        }

        private void BtnAddToQueue_Click(object sender, EventArgs e)
        {
            var urls = GetSelectedUrls();

            if (urls.Count == 0)
            {
                MessageBox.Show("Select at least one site (checkbox or row selection).", "Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var source = _currentView == "catalog" ? _catalogSites : _archiveCandidates;
            int added = 0;
            foreach (var url in urls)
            {
                if (_archiveQueue.Sites.Any(s => string.Equals(s.SiteUrl, url, StringComparison.OrdinalIgnoreCase)
                    && s.Status != "Archived" && s.Status != "Failed"))
                    continue;

                var site = source.FirstOrDefault(s => string.Equals(s.Url, url, StringComparison.OrdinalIgnoreCase));
                if (site == null)
                    continue;

                _archiveQueue.Sites.Add(new ArchiveQueueItem
                {
                    SiteUrl = site.Url,
                    Title = site.Title,
                    StorageUsedMB = site.StorageMB,
                    DaysInactive = site.IsInactive ? 1 : 0,
                    LastModified = site.LastModified?.ToString("o"),
                    Owner = site.Owner,
                    QueuedAt = DateTime.UtcNow.ToString("o"),
                    Status = "Queued",
                    Source = _currentView == "catalog" ? "Catalog" : "Candidates",
                    ArchiveStatus = site.ArchiveStatus,
                    LockState = site.LockState
                });
                added++;
            }

            _siteData.SaveArchiveQueue(_archiveQueue);
            RefreshQueueCheckboxState();
            MessageBox.Show($"Added {added} site(s) to Archive Queue.", "Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Grid_QueueToggled(object sender, SiteCatalogEntry site)
        {
            if (site == null || string.IsNullOrWhiteSpace(site.Url))
                return;

            if (_currentView != "queue")
            {
                // In catalog/candidates, checkbox changes are staged and confirmed via button.
                return;
            }

            bool isQueued = _archiveQueue.Sites.Any(s =>
                string.Equals(s.SiteUrl, site.Url, StringComparison.OrdinalIgnoreCase)
                && s.Status != "Archived" && s.Status != "Failed");

            if (isQueued)
            {
                // Remove from queue
                _archiveQueue.Sites = _archiveQueue.Sites
                    .Where(s => !string.Equals(s.SiteUrl, site.Url, StringComparison.OrdinalIgnoreCase)
                        || s.Status == "Archived" || s.Status == "Failed")
                    .ToList();
            }
            else
            {
                // Add to queue
                _archiveQueue.Sites.Add(new ArchiveQueueItem
                {
                    SiteUrl = site.Url,
                    Title = site.Title,
                    StorageUsedMB = site.StorageMB,
                    DaysInactive = site.IsInactive ? 1 : 0,
                    LastModified = site.LastModified?.ToString("o"),
                    Owner = site.Owner,
                    QueuedAt = DateTime.UtcNow.ToString("o"),
                    Status = "Queued",
                    Source = _currentView == "catalog" ? "Catalog" : "Candidates",
                    ArchiveStatus = site.ArchiveStatus,
                    LockState = site.LockState
                });
            }

            _siteData.SaveArchiveQueue(_archiveQueue);
        }

        private void RefreshQueueCheckboxState()
        {
            var queuedUrls = new HashSet<string>(
                _archiveQueue.Sites.Where(s => s.Status != "Archived" && s.Status != "Failed")
                    .Select(s => s.SiteUrl), StringComparer.OrdinalIgnoreCase);
            _grid.SetQueueMode(_currentView == "candidates" || _currentView == "queue", queuedUrls);
        }

        private void BtnRemoveFromQueue_Click(object sender, EventArgs e)
        {
            var urls = GetSelectedUrls();
            if (urls.Count == 0)
                return;

            _archiveQueue.Sites = _archiveQueue.Sites.Where(s => !urls.Contains(s.SiteUrl, StringComparer.OrdinalIgnoreCase)).ToList();
            _siteData.SaveArchiveQueue(_archiveQueue);
            _archiveQueue = _siteData.LoadArchiveQueue();
            RefreshQueueCheckboxState();
            BindQueue();
        }

        private async Task RunArchiveQueueAsync()
        {
            string adminUrl = (_txtArchiveAdminUrl.Text ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(adminUrl))
            {
                MessageBox.Show("Provide the SharePoint Admin URL before running the archive queue.", "Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_archiveQueue.Sites.Count == 0)
            {
                MessageBox.Show("Archive Queue is empty.", "Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _archiveConsole.Clear();
            _lblArchiveStatus.Text = "Running archive queue...";
            _btnRunQueue.Enabled = false;
            _archiveCts = new CancellationTokenSource();

            Action<string> outHandler = msg => AppendArchiveConsole(msg);
            Action<string> warnHandler = msg => AppendArchiveConsole("[WARN] " + msg);
            Action<string> errHandler = msg => AppendArchiveConsole("[ERROR] " + msg);

            _psHost.OnOutput += outHandler;
            _psHost.OnWarning += warnHandler;
            _psHost.OnError += errHandler;

            try
            {
                await _psHost.StartArchiveSitesAsync(adminUrl, Path.Combine(_config.ConfigPath, "ArchiveQueue.json"), _archiveCts.Token);
            }
            catch (Exception ex)
            {
                AppendArchiveConsole("[ERROR] " + ex.Message);
            }
            finally
            {
                _psHost.OnOutput -= outHandler;
                _psHost.OnWarning -= warnHandler;
                _psHost.OnError -= errHandler;
                _btnRunQueue.Enabled = true;
                _archiveQueue = _siteData.LoadArchiveQueue();

                // Remove successfully archived sites from candidates list
                var archivedUrls = new HashSet<string>(
                    _archiveQueue.Sites.Where(s => s.Status == "Archived")
                        .Select(s => s.SiteUrl), StringComparer.OrdinalIgnoreCase);
                if (archivedUrls.Count > 0)
                    _archiveCandidates = _archiveCandidates
                        .Where(c => !archivedUrls.Contains(c.Url)).ToList();

                RefreshQueueCheckboxState();
                BindQueue();
            }
        }

        private void AppendArchiveConsole(string text)
        {
            if (_archiveConsole.IsDisposed)
                return;

            if (InvokeRequired)
            {
                Invoke((Action)(() => AppendArchiveConsole(text)));
                return;
            }

            _archiveConsole.AppendText(text + Environment.NewLine);
            _archiveConsole.SelectionStart = _archiveConsole.TextLength;
            _archiveConsole.ScrollToCaret();
        }

        private void LoadScopeIntoGrid(DataGridView grid, string fileName)
        {
            grid.Rows.Clear();
            foreach (var item in _siteData.LoadScopeList(fileName))
                grid.Rows.Add(false, item.SiteUrl, item.Reason);
        }

        private void AddScopeRow(bool isTarget)
        {
            var grid = isTarget ? _targetGrid : _skipGrid;
            int index = grid.Rows.Add(false, string.Empty, string.Empty);
            grid.CurrentCell = grid.Rows[index].Cells["SiteUrl"];
            grid.BeginEdit(true);
        }

        private void AddScopeEntryByPopup(bool isTarget)
        {
            using (var dlg = new Form
            {
                Text = isTarget ? "Add Target Site" : "Add Skip Site",
                Size = new Size(640, 240),
                MinimumSize = new Size(560, 220),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            })
            {
                var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(16, 14, 16, 10) };
                dlg.Controls.Add(host);

                host.Controls.Add(new Label { Text = "Site URL *", Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 2) });
                var txtUrl = new TextBox { Location = new Point(0, 24), Size = new Size(590, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                AppTheme.StyleTextBox(txtUrl);
                host.Controls.Add(txtUrl);

                host.Controls.Add(new Label { Text = "Reason / Notes", Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 56) });
                var txtReason = new TextBox { Location = new Point(0, 78), Size = new Size(590, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                AppTheme.StyleTextBox(txtReason);
                host.Controls.Add(txtReason);

                host.Controls.Add(new Label { Text = "* mandatory field", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 108) });

                var btnSave = new FlatButton { Text = "Add", Size = new Size(80, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Location = new Point(510, 136) };
                btnSave.SetAccentColor(isTarget ? AppTheme.AccentCyan : AppTheme.AccentGold);
                host.Controls.Add(btnSave);

                var btnCancel = new FlatButton { Text = "Cancel", Size = new Size(88, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Location = new Point(414, 136) };
                btnCancel.SetGhostStyle();
                host.Controls.Add(btnCancel);

                btnCancel.Click += (s, e) => dlg.Close();
                btnSave.Click += (s, e) =>
                {
                    string url = (txtUrl.Text ?? string.Empty).Trim();
                    string reason = (txtReason.Text ?? string.Empty).Trim();

                    if (string.IsNullOrWhiteSpace(url))
                    {
                        MessageBox.Show("Site URL is mandatory.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtUrl.Focus();
                        return;
                    }

                    if (!Uri.TryCreate(url, UriKind.Absolute, out _))
                    {
                        MessageBox.Show("Enter a valid absolute URL.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtUrl.Focus();
                        return;
                    }

                    var grid = isTarget ? _targetGrid : _skipGrid;
                    bool exists = grid.Rows.Cast<DataGridViewRow>()
                        .Where(r => !r.IsNewRow)
                        .Any(r => string.Equals(r.Cells["SiteUrl"].Value?.ToString()?.Trim(), url, StringComparison.OrdinalIgnoreCase));
                    if (exists)
                    {
                        MessageBox.Show("This site already exists in the list.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    grid.Rows.Add(false, url, reason);
                    dlg.DialogResult = DialogResult.OK;
                    dlg.Close();
                };

                dlg.ShowDialog(ParentForm);
            }
        }

        private void ImportScopeCsv(bool isTarget)
        {
            using (var dlg = new OpenFileDialog { Filter = "CSV Files|*.csv|All Files|*.*" })
            {
                if (dlg.ShowDialog(ParentForm) != DialogResult.OK)
                    return;

                var grid = isTarget ? _targetGrid : _skipGrid;
                grid.Rows.Clear();
                foreach (var line in File.ReadAllLines(dlg.FileName).Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var parts = line.Split(new[] { ',' }, 2);
                    grid.Rows.Add(false, parts[0].Trim().Trim('"'), parts.Length > 1 ? parts[1].Trim().Trim('"') : string.Empty);
                }
            }
        }

        private void SaveScopeList(bool isTarget)
        {
            var grid = isTarget ? _targetGrid : _skipGrid;
            var fileName = isTarget ? "IncludeSites.csv" : "ExcludeSites.csv";
            var items = new List<ScopeSiteItem>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                    continue;

                string url = row.Cells["SiteUrl"].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                items.Add(new ScopeSiteItem
                {
                    SiteUrl = url,
                    Reason = row.Cells["Reason"].Value?.ToString()?.Trim() ?? string.Empty
                });
            }

            _siteData.SaveScopeList(fileName, items);
            MessageBox.Show((isTarget ? "Target Sites" : "Skip Sites") + " saved.", "Sites", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportScopeCsv(bool isTarget)
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = isTarget ? "TargetSites.csv" : "SkipSites.csv" })
            {
                if (dlg.ShowDialog(ParentForm) != DialogResult.OK)
                    return;

                var lines = new List<string> { "SiteUrl,Reason" };
                var grid = isTarget ? _targetGrid : _skipGrid;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow)
                        continue;
                    string url = row.Cells["SiteUrl"].Value?.ToString() ?? string.Empty;
                    string reason = row.Cells["Reason"].Value?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(url))
                        lines.Add($"{QuoteCsv(url)},{QuoteCsv(reason)}");
                }
                File.WriteAllLines(dlg.FileName, lines);
            }
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = _currentView + ".csv" })
            {
                if (dlg.ShowDialog(ParentForm) != DialogResult.OK)
                    return;

                var data = _filteredSites;
                if (data == null || data.Count == 0) return;

                var lines = new List<string> { "Title,URL,Storage,Versions,VersionSize,Status,Archive,Lock,Owner,Inactive,Ownerless,LastModified,Created" };
                foreach (var s in data)
                    lines.Add(string.Join(",", new[] { QuoteCsv(s.Title), QuoteCsv(s.Url), QuoteCsv(s.StorageDisplay), s.VersionCount.ToString(), QuoteCsv(s.VersionSizeDisplay), QuoteCsv(s.Status), QuoteCsv(s.ArchiveStatus), QuoteCsv(s.LockState), QuoteCsv(s.Owner ?? ""), s.IsInactive ? "Yes" : "No", s.IsOwnerless ? "Yes" : "No", QuoteCsv(s.LastModifiedDisplay), QuoteCsv(s.CreatedDisplay) }));
                File.WriteAllLines(dlg.FileName, lines);
            }
        }

        private static string QuoteCsv(string value)
        {
            return value.Contains(",") || value.Contains("\"") || value.Contains("\n")
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
        }

        private void OpenSelectedDetail()
        {
            var sites = _grid.GetSelectedSites();
            var site = sites.Count > 0 ? sites[0] : null;
            if (site == null) return;

            // Try to enrich with catalog data (has more fields)
            var enriched = _catalogSites.FirstOrDefault(s => string.Equals(s.Url, site.Url, StringComparison.OrdinalIgnoreCase))
                ?? _archiveCandidates.FirstOrDefault(s => string.Equals(s.Url, site.Url, StringComparison.OrdinalIgnoreCase))
                ?? _archivedSites.FirstOrDefault(s => string.Equals(s.Url, site.Url, StringComparison.OrdinalIgnoreCase))
                ?? site;

            ShowSiteDetail(enriched);
        }

        private void ShowSiteDetail(SiteCatalogEntry site)
        {
            var dlg = new Form
            {
                Text = site.Title ?? "Site Detail",
                Size = new Size(800, 700),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary,
                FormBorderStyle = FormBorderStyle.Sizable,
                MinimumSize = new Size(700, 500),
                MaximizeBox = true,
                MinimizeBox = false
            };

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.BgDark, Padding = new Padding(10) };
            dlg.Controls.Add(scroll);

            int cardW = Math.Max(680, Math.Min(980, dlg.ClientSize.Width - 60));
            var content = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(cardW, 10),
                BackColor = Color.Transparent
            };
            scroll.Controls.Add(content);

            Action recenter = () =>
            {
                if (content.IsDisposed || scroll.IsDisposed) return;
                content.Left = Math.Max(0, (scroll.ClientSize.Width - content.Width) / 2);
            };
            scroll.Resize += (s, e) => recenter();
            recenter();

            // Close button
            var btnClose = new Label { Text = "\u2715", Font = new Font("Segoe UI", 14f), ForeColor = AppTheme.TextMuted, AutoSize = true, Cursor = Cursors.Hand, BackColor = Color.Transparent };
            btnClose.Click += (s, e) => dlg.Close();
            dlg.Controls.Add(btnClose);
            Action placeClose = () =>
            {
                if (btnClose.IsDisposed || dlg.IsDisposed) return;
                btnClose.Location = new Point(dlg.ClientSize.Width - 34, 8);
                btnClose.BringToFront();
            };
            dlg.Resize += (s, e) => placeClose();
            placeClose();

            int y = 8;

            // Title
            var lblTitle = new Label { Text = site.Title ?? "-", Font = AppTheme.FontTitle, ForeColor = AppTheme.AccentCyan, AutoSize = true, MaximumSize = new Size(cardW, 0), BackColor = Color.Transparent, Location = new Point(0, y) };
            content.Controls.Add(lblTitle);
            y += lblTitle.PreferredHeight + 8;

            // URL
            var lblUrl = new Label { Text = site.Url ?? "-", Font = AppTheme.FontBody, ForeColor = AppTheme.TextMuted, AutoSize = true, MaximumSize = new Size(cardW, 0), BackColor = Color.Transparent, Location = new Point(0, y) };
            content.Controls.Add(lblUrl);
            y += lblUrl.PreferredHeight + 16;

            // ── Storage Overview card ──
            var storageCard = new GlassPanel { Location = new Point(0, y), Size = new Size(cardW, 136), AccentLeft = AppTheme.AccentCyan };
            content.Controls.Add(storageCard);
            storageCard.Controls.Add(new Label { Text = "\U0001F4BE  Storage Overview", Font = AppTheme.FontHeading, ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 10) });

            // Load site execution history
            Newtonsoft.Json.Linq.JObject siteHistory = null;
            try
            {
                var histDict = _history.LoadSiteExecutionHistory();
                string key = NormalizeSiteUrlKey(site.Url);
                if (!string.IsNullOrWhiteSpace(key) && histDict.TryGetValue(key, out var matched))
                    siteHistory = matched;
            }
            catch { }

            double storageBefore = 0, storageNow = site.StorageMB, versionSizeNow = site.VersionSizeMB;
            double versionPct = storageNow > 0 ? (versionSizeNow / storageNow * 100.0) : 0;
            long totalExecs = 0, totalDeleted = 0;
            double totalReleasedMB = 0;
            string firstExec = "-", lastExec = "-";

            if (siteHistory != null)
            {
                totalExecs = siteHistory["TotalExecutions"]?.Value<long>() ?? 0;
                totalDeleted = siteHistory["TotalVersionsDeleted"]?.Value<long>() ?? 0;
                double releasedBytes = siteHistory["TotalStorageReleasedBytes"]?.Value<double>() ?? 0;
                totalReleasedMB = releasedBytes / (1024.0 * 1024.0);
                firstExec = siteHistory["FirstProcessed"]?.Value<string>() ?? "-";
                lastExec = siteHistory["LastProcessed"]?.Value<string>() ?? "-";
            }

            int statY = 36;
            int statBaseX = Math.Max(14, (cardW - (4 * 155 + 3 * 20)) / 2);
            AddStatCard(storageCard, "Size at First\nExecution", storageBefore > 0 ? FormatStorage(storageBefore) : "N/A", AppTheme.AccentCyan, statBaseX, statY);
            AddStatCard(storageCard, "Current Size", FormatStorage(storageNow), AppTheme.AccentCyan, statBaseX + 175, statY);
            AddStatCard(storageCard, "Current Versions", FormatStorage(versionSizeNow), AppTheme.AccentGold, statBaseX + 350, statY);
            AddStatCard(storageCard, "% Versions of Total", $"{versionPct:F1}%", versionPct > 50 ? AppTheme.AccentRed : AppTheme.AccentGreen, statBaseX + 525, statY);

            y += 142;

            // Execution stats row
            var execStatsCard = new GlassPanel { Location = new Point(0, y), Size = new Size(cardW, 56) };
            content.Controls.Add(execStatsCard);
            string execStats = $"Total Executions: {totalExecs}    First Execution: {FormatDateTime(firstExec)}    Last Execution: {FormatDateTime(lastExec)}";
            execStatsCard.Controls.Add(new Label { Text = execStats, Font = AppTheme.FontBody, ForeColor = AppTheme.TextPrimary, AutoSize = false, Size = new Size(cardW - 28, 24), BackColor = Color.Transparent, Location = new Point(14, 16) });
            y += 62;

            // Totals row
            var totalsCard = new GlassPanel { Location = new Point(0, y), Size = new Size(cardW, 36) };
            content.Controls.Add(totalsCard);
            string deletedFmt = totalDeleted >= 1000 ? $"{totalDeleted / 1000.0:F1}K" : totalDeleted.ToString("N0");
            string releasedFmt = FormatStorage(totalReleasedMB);
            totalsCard.Controls.Add(new Label { Text = $"Total Versions Deleted: {deletedFmt}    Total Space Released: {releasedFmt}", Font = AppTheme.FontBody, ForeColor = AppTheme.TextPrimary, AutoSize = false, Size = new Size(cardW - 28, 20), BackColor = Color.Transparent, Location = new Point(14, 8) });
            y += 42;

            // ── Retention Policy Management (if data exists) ──
            var executions = siteHistory?["Executions"] as Newtonsoft.Json.Linq.JArray;
            bool hasRetention = false;
            if (executions != null)
            {
                foreach (var ex in executions)
                    if (ex["RetentionManaged"]?.Value<bool>() == true) { hasRetention = true; break; }
            }

            if (hasRetention)
            {
                var retCard = new GlassPanel { Location = new Point(0, y), Size = new Size(cardW, 130), AccentLeft = AppTheme.AccentPurple };
                content.Controls.Add(retCard);
                retCard.Controls.Add(new Label { Text = "\U0001F6E1  Retention Policy Management", Font = AppTheme.FontHeading, ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 10) });

                int retCycles = 0; double retWaitTotal = 0; double retReleasedBytes = 0;
                var policies = new HashSet<string>();
                foreach (var ex in executions)
                {
                    if (ex["RetentionManaged"]?.Value<bool>() != true) continue;
                    retCycles++;
                    retWaitTotal += ex["RetentionWaitMinutes"]?.Value<double>() ?? 0;
                    retReleasedBytes += ex["StorageReleasedBytes"]?.Value<double>() ?? 0;
                    var pols = ex["RetentionPolicies"] as Newtonsoft.Json.Linq.JArray;
                    if (pols != null) foreach (var p in pols) policies.Add(p.ToString());
                }
                double retAvgWait = retCycles > 0 ? retWaitTotal / retCycles : 0;
                double retReleasedMB = retReleasedBytes / (1024.0 * 1024.0);

                int retBaseX = Math.Max(14, (cardW - (4 * 155 + 3 * 20)) / 2);
                AddStatCard(retCard, "Managed Cycles", retCycles.ToString(), AppTheme.AccentCyan, retBaseX, 36);
                AddStatCard(retCard, "Avg Wait", $"{retAvgWait:F1} min", AppTheme.AccentGold, retBaseX + 175, 36);
                AddStatCard(retCard, "Total Wait", $"{retWaitTotal:F1} min", AppTheme.AccentGold, retBaseX + 350, 36);
                AddStatCard(retCard, "Released (with Retention)", FormatStorage(retReleasedMB), AppTheme.AccentGreen, retBaseX + 525, 36);

                if (policies.Count > 0)
                {
                    int px = 14;
                    int py = 100;
                    retCard.Controls.Add(new Label { Text = "Policies involved:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(px, py) });
                    px += 120;
                    foreach (var pol in policies)
                    {
                        var badge = new Label { Text = pol, Font = new Font("Segoe UI Semibold", 8f), ForeColor = AppTheme.AccentCyan, AutoSize = true, BackColor = Color.FromArgb(40, AppTheme.AccentCyan), Padding = new Padding(6, 2, 6, 2), Location = new Point(px, py - 2) };
                        retCard.Controls.Add(badge);
                        px += badge.PreferredWidth + 8;
                    }
                }

                y += 136;
            }

            // ── Version Retention Impact ──
            var verCard = new GlassPanel { Location = new Point(0, y), Size = new Size(cardW, 110), AccentLeft = AppTheme.AccentGold };
            content.Controls.Add(verCard);
            verCard.Controls.Add(new Label { Text = "\U0001F4E6  Version Retention Impact", Font = AppTheme.FontHeading, ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 10) });

            int majLimit = 5;
            long versionsKept = 0;
            double verBeforeMB = 0, verAfterMB = 0;
            if (executions != null && executions.Count > 0)
            {
                var lastEx = executions[executions.Count - 1];
                majLimit = lastEx["MajorVersionLimit"]?.Value<int>() ?? 5;
                versionsKept = lastEx["VersionsKept"]?.Value<long>() ?? 0;
                verBeforeMB = (lastEx["VersionSizeBeforeBytes"]?.Value<double>() ?? 0) / (1024.0 * 1024.0);
                verAfterMB = (lastEx["VersionSizeAfterBytes"]?.Value<double>() ?? 0) / (1024.0 * 1024.0);
            }

            int verBaseX = Math.Max(14, (cardW - (4 * 155 + 3 * 20)) / 2);
            AddStatCard(verCard, "Version Limit", majLimit.ToString(), AppTheme.AccentCyan, verBaseX, 36);
            AddStatCard(verCard, "Versions Kept\n(latest)", versionsKept.ToString("N0"), AppTheme.AccentRed, verBaseX + 175, 36);
            AddStatCard(verCard, "Versions Before", FormatStorage(verBeforeMB), AppTheme.AccentGold, verBaseX + 350, 36);
            AddStatCard(verCard, "Versions After", FormatStorage(verAfterMB), AppTheme.AccentGreen, verBaseX + 525, 36);
            y += 116;

            // ── Execution History table ──
            var histLabel = new Label { Text = "\U0001F4CB  Execution History", Font = AppTheme.FontHeading, ForeColor = AppTheme.AccentCyan, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, y) };
            content.Controls.Add(histLabel);
            y += 24;

            var histGrid = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(cardW, Math.Max(80, (executions?.Count ?? 0) * 32 + 36)),
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, ReadOnly = true,
                RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both
            };
            AppTheme.StyleDataGrid(histGrid);
            histGrid.Columns.Add("DateTime", "Date/Time");
            histGrid.Columns.Add("Type", "Type");
            histGrid.Columns.Add("Status", "Status");
            histGrid.Columns.Add("Duration", "Duration");
            histGrid.Columns.Add("SizeBefore", "Size Before");
            histGrid.Columns.Add("Files", "Files");
            histGrid.Columns.Add("VerProc", "Versions");
            histGrid.Columns.Add("Deleted", "Deleted");
            histGrid.Columns.Add("Released", "Released");
            histGrid.Columns.Add("Cumulative", "Cumulative");
            foreach (DataGridViewColumn col in histGrid.Columns)
                col.SortMode = DataGridViewColumnSortMode.Automatic;

            if (executions != null)
            {
                double cumReleasedBytes = 0;
                foreach (var ex in executions)
                {
                    double relBytes = ex["StorageReleasedBytes"]?.Value<double>() ?? 0;
                    cumReleasedBytes += relBytes;
                    double durationMin = ex["DurationMinutes"]?.Value<double>() ?? 0;
                    string durFmt = durationMin >= 60 ? $"{(int)(durationMin / 60)}h {(int)(durationMin % 60)}m" : $"{(int)durationMin}m";
                    double sizeBeforeBytes = ex["StorageBeforeBytes"]?.Value<double>() ?? 0;

                    histGrid.Rows.Add(
                        ex["ExecutedAtDisplay"]?.ToString() ?? ex["ExecutedAt"]?.ToString() ?? "-",
                        ex["JobType"]?.ToString() ?? "-",
                        ex["Status"]?.ToString() ?? "-",
                        durFmt,
                        FormatStorage(sizeBeforeBytes / (1024.0 * 1024.0)),
                        (ex["FilesProcessed"]?.Value<long>() ?? 0).ToString("N0"),
                        (ex["VersionsProcessed"]?.Value<long>() ?? 0).ToString("N0"),
                        (ex["VersionsDeleted"]?.Value<long>() ?? 0).ToString("N0"),
                        FormatStorage(relBytes / (1024.0 * 1024.0)),
                        FormatStorage(cumReleasedBytes / (1024.0 * 1024.0))
                    );
                }
            }

            content.Controls.Add(histGrid);
            y += histGrid.Height + 16;

            // Scroll spacer
            content.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(10, 1), BackColor = Color.Transparent });
            content.Height = y + 6;

            dlg.ShowDialog(ParentForm);
        }

        private void AddStatCard(Control parent, string label, string value, Color valueColor, int x, int y)
        {
            int cardW = 155, cardH = 56;
            var card = new GlassPanel { Location = new Point(x, y), Size = new Size(cardW, cardH) };
            parent.Controls.Add(card);
            card.Controls.Add(new Label { Text = label, Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, MaximumSize = new Size(cardW - 12, 0), BackColor = Color.Transparent, Location = new Point(8, 4) });
            card.Controls.Add(new Label { Text = value, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = valueColor, AutoSize = true, BackColor = Color.Transparent, Location = new Point(8, 30) });
        }

        private static string FormatStorage(double mb)
        {
            const double gbThreshold = 1024.0;
            const double tbThreshold = 1024.0 * 1024.0;
            const double pbThreshold = 1024.0 * 1024.0 * 1024.0;

            if (mb >= pbThreshold) return $"{mb / pbThreshold:F2} PB";
            if (mb >= tbThreshold) return $"{mb / tbThreshold:F2} TB";
            if (mb >= gbThreshold) return $"{mb / gbThreshold:F2} GB";
            if (mb >= 1) return $"{mb:F2} MB";
            if (mb > 0) return $"{mb * 1024.0:F0} KB";
            return "0 B";
        }

        private static string FormatDateTime(string dt)
        {
            if (string.IsNullOrWhiteSpace(dt) || dt == "-") return "-";
            if (DateTime.TryParse(dt, out var parsed))
                return parsed.ToString("dd/MM/yyyy HH:mm");
            return dt;
        }

        private List<string> GetSelectedUrls()
        {
            if (_currentView == "candidates")
            {
                var checkedUrls = _grid.GetCheckedQueueUrlsFromVisible();
                if (checkedUrls.Count > 0)
                    return checkedUrls;
            }

            return _grid.GetSelectedUrls();
        }

        private void LoadHistorySiteUrls()
        {
            try
            {
                var histDict = _history.LoadSiteExecutionHistory();
                    _historySiteUrls = new HashSet<string>(
                        histDict.Keys.Select(NormalizeSiteUrlKey).Where(k => !string.IsNullOrWhiteSpace(k)),
                        StringComparer.OrdinalIgnoreCase);
                _grid.SetHistorySites(_historySiteUrls);
            }
            catch { }
        }

        private static string NormalizeSiteUrlKey(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;

            string key = url.Trim().ToLowerInvariant();
            while (key.EndsWith("/"))
                key = key.Substring(0, key.Length - 1);
            return key;
        }

        private void SiteManagementPanel_Resize(object sender, EventArgs e)
        {
            if (_suspendSummaryReflow)
                return;

            if (_summaryReflowPending)
                return;

            _summaryReflowPending = true;
            RunOnUiThread(() =>
            {
                _summaryReflowPending = false;
                ReflowSummaryCards();
            });
        }

        private void RunOnUiThread(Action action)
        {
            if (action == null || IsDisposed)
                return;

            if (IsHandleCreated)
            {
                if (InvokeRequired)
                    BeginInvoke(action);
                else
                    action();
                return;
            }

            if (!InvokeRequired)
            {
                action();
                return;
            }

            EventHandler onHandleCreated = null;
            onHandleCreated = (s, e) =>
            {
                HandleCreated -= onHandleCreated;
                if (!IsDisposed && IsHandleCreated)
                    BeginInvoke(action);
            };

            HandleCreated += onHandleCreated;
        }

        private void ReflowSummaryCards()
        {
            if (_summaryGrid == null || _summaryCards == null || _summaryCards.Count == 0)
                return;

            int width = 0;
            width = Math.Max(width, _summaryGrid.ClientSize.Width);
            if (_summaryBar != null)
                width = Math.Max(width, _summaryBar.ClientSize.Width - 20);
            if (_dataViewPanel != null)
                width = Math.Max(width, _dataViewPanel.ClientSize.Width - 24);
            width = Math.Max(width, ClientSize.Width - Padding.Horizontal - 24);

            if (width <= 0)
                return;

            int columns = Math.Min(3, _summaryCards.Count);
            columns = Math.Max(1, columns);

            int horizontalPadding = _summaryGrid.Padding.Left + _summaryGrid.Padding.Right;
            int gaps = Math.Max(0, columns - 1) * 10;
            int cardWidth = Math.Max(180, (width - horizontalPadding - gaps) / columns);

            if (_summaryCurrentColumns == columns &&
                _summaryGrid.Controls.Count == _summaryCards.Count &&
                _summaryCards.All(c => c.Width == cardWidth))
                return;

            _summaryGrid.SuspendLayout();
            try
            {
                _summaryGrid.Controls.Clear();
                foreach (var card in _summaryCards)
                {
                    card.Width = cardWidth;
                    card.Height = 72;
                    _summaryGrid.Controls.Add(card);
                }

                _summaryCurrentColumns = columns;
            }
            finally
            {
                _summaryGrid.ResumeLayout(true);
            }
        }

        private void AdjustGridColumnsForViewport()
        {
            if (_grid == null || _grid.IsDisposed || !_grid.IsHandleCreated)
                return;

            _grid.AdjustColumnWidths();
        }

        private void UpdateSummaryBar()
        {
            if (_lblSummaryTotalSites == null)
                return;

            var sites = _filteredSites ?? new List<SiteCatalogEntry>();

            double totalStorageMB = sites.Sum(s => s.StorageMB);
            long totalVersions = sites.Sum(s => s.VersionCount);
            double totalVersionSizeMB = sites.Sum(s => s.VersionSizeMB);
            double versionPct = totalStorageMB > 0 ? (totalVersionSizeMB / totalStorageMB * 100.0) : 0;

            _lblSummaryTotalSites.Text = FormatCompactNumber(sites.Count);
            _lblSummaryStorage.Text = FormatStorageFromMB(totalStorageMB);
            _lblSummaryVersions.Text = FormatCompactNumber(totalVersions);
            string versionSize = FormatStorageFromMB(totalVersionSizeMB);
            string lastUpdate = ResolveSummaryLastUpdate();
            _lblSummary.Text = $"📁 Version Size: {versionSize}   ◆ Versions %: {versionPct:F1}%   📅 Updated: {lastUpdate}";
        }

        private Control CreateSummaryChip(string icon, string title, Color valueColor, out Label valueLabel)
        {
            var chip = new GlassPanel
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Top,
                Height = 72,
                Margin = new Padding(0, 0, 10, 10),
                AccentLeft = Color.FromArgb(180, valueColor)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 6, 10, 5),
                Margin = Padding.Empty
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            chip.Controls.Add(layout);

            var topRow = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

            var iconLabel = new Label
            {
                Text = icon,
                AutoSize = true,
                Font = new Font("Segoe UI Emoji", 9f, FontStyle.Regular),
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 3, 0)
            };

            var titleLabel = new Label
            {
                Text = title,
                AutoSize = false,
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Regular),
                ForeColor = AppTheme.TextSecondary,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

            valueLabel = new Label
            {
                Text = "--",
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold),
                ForeColor = valueColor,
                BackColor = Color.Transparent,
                Padding = new Padding(10, 0, 6, 0)
            };

            topRow.Controls.Add(iconLabel, 0, 0);
            topRow.Controls.Add(titleLabel, 1, 0);
            layout.Controls.Add(topRow, 0, 0);
            layout.Controls.Add(valueLabel, 0, 1);

            return chip;
        }

        private static string FormatStorageFromMB(double mb)
        {
            if (mb <= 0)
                return "0 MB";

            const double gbThreshold = 1024.0;
            const double tbThreshold = 1024.0 * 1024.0;
            const double pbThreshold = 1024.0 * 1024.0 * 1024.0;

            if (mb >= pbThreshold)
                return $"{mb / pbThreshold:F2} PB";
            if (mb >= tbThreshold)
                return $"{mb / tbThreshold:F2} TB";
            if (mb >= gbThreshold)
                return $"{mb / gbThreshold:F2} GB";
            return $"{mb:F2} MB";
        }

        private static string FormatCompactNumber(long value)
        {
            if (value >= 1000000)
                return $"{value / 1000000.0:F1}M";
            if (value >= 1000)
                return $"{value / 1000.0:F1}K";
            return value.ToString("N0");
        }

        private string ResolveSummaryLastUpdate()
        {
            try
            {
                if (_currentView == "queue" && !string.IsNullOrWhiteSpace(_archiveQueue?.LastUpdated))
                    return FormatDateTimeWithSeconds(_archiveQueue.LastUpdated);

                string fileName;
                if (_currentView == "catalog") fileName = "AllSites.json";
                else if (_currentView == "candidates" || _currentView == "archived") fileName = "ArchiveAnalysis.json";
                else if (_currentView == "queue") fileName = "ArchiveQueue.json";
                else return "--";

                string path = Path.Combine(_config.ConfigPath, fileName);
                if (!File.Exists(path))
                    return "--";

                if (_currentView == "catalog")
                {
                    string exportedAt = TryReadExportedAt(path);
                    if (!string.IsNullOrWhiteSpace(exportedAt))
                        return exportedAt;
                }

                return File.GetLastWriteTime(path).ToString("dd/MM/yyyy HH:mm:ss");
            }
            catch
            {
                return "--";
            }
        }

        private static string TryReadExportedAt(string path)
        {
            try
            {
                using (var reader = new StreamReader(path))
                {
                    for (int i = 0; i < 24 && !reader.EndOfStream; i++)
                    {
                        string line = reader.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (line.IndexOf("\"ExportedAt\"", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        int colon = line.IndexOf(':');
                        if (colon < 0)
                            continue;

                        string raw = line.Substring(colon + 1).Trim().Trim(',').Trim().Trim('"');
                        return FormatDateTimeWithSeconds(raw);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string FormatDateTimeWithSeconds(string dt)
        {
            if (string.IsNullOrWhiteSpace(dt) || dt == "-")
                return "--";
            if (DateTime.TryParse(dt, out var parsed))
                return parsed.ToString("dd/MM/yyyy HH:mm:ss");
            return dt;
        }

        private Label MakeLabel(string text, Font font, Color color, int x, int y)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) };
        }
    }
}
