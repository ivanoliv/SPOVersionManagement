using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class FileArchivePanel : UserControl
    {
        private ConfigurationService _config;
        private PowerShellHostService _psHost;
        private bool _initialized;
        private ExtensionGroupConfig _groupConfig;
        private SiteDataService _siteData;
        private List<SiteCatalogEntry> _allSitesCache = new List<SiteCatalogEntry>();
        private Panel _groupsPanel;
        private TextBox _txtSiteUrl;
        private FlatButton _btnPickSite;
        private Label _lblStatus;

        // Execution controls
        private CancellationTokenSource _searchCts;
        private RadioButton _rbInteractive, _rbAppCreds;
        private CheckBox _chkSummaryOnly;
        private ComboBox _cmbRegion;
        private TextBox _searchConsole;
        private FlatButton _btnSearch, _btnAbort;
        private Label _lblSearchStatus;
        private DataGridView _resultsGrid;
        private Label _lblResultsSummary;

        public event EventHandler OpenQueueRequested;

        public FileArchivePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(28, 20, 28, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;

            Resize -= FileArchivePanel_Resize;
            Resize += FileArchivePanel_Resize;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            if (_initialized) return;
            _initialized = true;
            _config = config;
            _psHost = psHost;
            _siteData = new SiteDataService(config);
            LoadConfig();
            BuildLayout();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        private void LoadConfig()
        {
            string path = Path.Combine(_config.ConfigPath, "ExtensionGroups.json");
            if (File.Exists(path))
            {
                try { _groupConfig = JsonConvert.DeserializeObject<ExtensionGroupConfig>(File.ReadAllText(path)); } catch { }
            }
            if (_groupConfig == null || _groupConfig.Groups.Count == 0)
                _groupConfig = ExtensionGroupConfig.GetDefaults();
        }

        private void SaveConfig()
        {
            string path = Path.Combine(_config.ConfigPath, "ExtensionGroups.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(_groupConfig, Formatting.Indented));
        }

        private void BuildLayout()
        {
            Controls.Clear();
            int contentW = Math.Max(720, ClientSize.Width - Padding.Horizontal - 10);
            int contentH = Math.Max(540, ClientSize.Height - Padding.Vertical - 10);

            // ── Main split: top (config + groups) / bottom (console + results) ──
            var splitter = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                BackColor = Color.Transparent,
                SplitterDistance = Math.Max(300, (int)(contentH * 0.55)),
                SplitterWidth = 6,
                Panel1MinSize = 200,
                Panel2MinSize = 120
            };
            splitter.Paint += (s, pe) => AppTheme.PaintGradientBackground(pe.Graphics, splitter.ClientRectangle);
            splitter.Panel1.Paint += (s, pe) => AppTheme.PaintGradientBackground(pe.Graphics, splitter.Panel1.ClientRectangle);
            splitter.Panel2.Paint += (s, pe) => AppTheme.PaintGradientBackground(pe.Graphics, splitter.Panel2.ClientRectangle);
            Controls.Add(splitter);

            // ════════════════════════════════════════════════════
            //  TOP PANEL: Title + Search/Auth + Extension Groups
            // ════════════════════════════════════════════════════
            var topHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = false,
                Padding = new Padding(0, 0, 10, 14)
            };
            splitter.Panel1.Controls.Add(topHost);

            topHost.Controls.Add(new Label { Text = "File Archive Explorer", Font = AppTheme.FontTitle, ForeColor = AppTheme.TextPrimary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 0) });
            topHost.Controls.Add(new Label { Text = "Search SharePoint files by extension using Graph Search API. Select a site, configure extensions, and scan.", Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = true, MaximumSize = new Size(contentW - 10, 0), BackColor = Color.Transparent, Location = new Point(0, 28) });

            int y = 62;
            int panelW = Math.Max(680, contentW - 10);

            // ═══ SINGLE ROW: Search (left 55%) + Auth (right 45%) ═══
            int searchW = (int)(panelW * 0.55);
            int authW = panelW - searchW - 12;

            var searchCard = new GlassPanel { Location = new Point(0, y), Size = new Size(searchW, 112), AccentLeft = AppTheme.AccentCyan };
            topHost.Controls.Add(searchCard);
            searchCard.Controls.Add(new Label { Text = "TARGET SITE", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = AppTheme.AccentCyan, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 4) });
            searchCard.Controls.Add(new Label { Text = "Site URL:", Font = AppTheme.FontBody, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 26) });
            _txtSiteUrl = new TextBox { Location = new Point(80, 24), Size = new Size(Math.Max(170, searchW - 216), 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            AppTheme.StyleTextBox(_txtSiteUrl);
            _txtSiteUrl.TextChanged += (s, e) => UpdateRunButtonState();
            searchCard.Controls.Add(_txtSiteUrl);
            _btnPickSite = new FlatButton { Text = "Pick Site", Size = new Size(96, 22), Location = new Point(searchW - 110, 23), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnPickSite.SetGhostStyle();
            _btnPickSite.Click += (s, e) => PickSiteFromCatalog();
            searchCard.Controls.Add(_btnPickSite);
            _chkSummaryOnly = new CheckBox { Text = "  Summary only (count, no details)", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextPrimary, BackColor = Color.Transparent, AutoSize = true, Location = new Point(14, 50) };
            searchCard.Controls.Add(_chkSummaryOnly);
            _btnSearch = new FlatButton { Text = "\u25B6  Run", Size = new Size(90, 26), Location = new Point(14, 72) };
            _btnSearch.SetAccentColor(AppTheme.AccentGreen);
            _btnSearch.Click += (s, e) => RunFileSearch();
            searchCard.Controls.Add(_btnSearch);
            _btnAbort = new FlatButton { Text = "\u25A0  Abort", Size = new Size(70, 26), Location = new Point(112, 72), Enabled = false };
            _btnAbort.SetDangerStyle();
            _btnAbort.Click += (s, e) => { if (_searchCts != null) _searchCts.Cancel(); };
            searchCard.Controls.Add(_btnAbort);
            _lblSearchStatus = new Label { Text = "Ready", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(190, 78) };
            searchCard.Controls.Add(_lblSearchStatus);
            UpdateRunButtonState();

            // ═══ AUTH CARD (right) ═══
            var authCard = new GlassPanel { Location = new Point(searchW + 12, y), Size = new Size(authW, 112), AccentLeft = AppTheme.AccentPurple };
            topHost.Controls.Add(authCard);
            authCard.Controls.Add(new Label { Text = "AUTHENTICATION", Font = new Font("Segoe UI", 7f, FontStyle.Bold), ForeColor = AppTheme.AccentPurple, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 4) });
            _rbInteractive = new RadioButton { Text = "  Interactive (browser)", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentCyan, BackColor = Color.Transparent, AutoSize = true, Location = new Point(14, 24), Checked = true };
            authCard.Controls.Add(_rbInteractive);
            _rbAppCreds = new RadioButton { Text = "  App credentials (EntraID)", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentCyan, BackColor = Color.Transparent, AutoSize = true, Location = new Point(14, 44) };
            authCard.Controls.Add(_rbAppCreds);
            authCard.Controls.Add(new Label { Text = "Region:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextSecondary, AutoSize = true, BackColor = Color.Transparent, Location = new Point(14, 72) });
            _cmbRegion = new ComboBox { Location = new Point(64, 70), Size = new Size(70, 20), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbRegion.Items.AddRange(new[] { "NAM", "EUR", "GBR", "FRA", "DEU", "APC", "AUS", "JPN", "IND", "CAN", "KOR", "BRA", "LAM", "ZAF", "ARE" });
            _cmbRegion.SelectedItem = "BRA";
            AppTheme.StyleComboBox(_cmbRegion);
            authCard.Controls.Add(_cmbRegion);
            authCard.Controls.Add(new Label { Text = "PnP.PowerShell required", Font = new Font("Cascadia Code", 6.5f), ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(150, 74) });
            var btnExportConfig = new FlatButton { Text = "Export Config", Size = new Size(96, 24), Location = new Point(authW - 108, 24) };
            btnExportConfig.SetGhostStyle();
            btnExportConfig.Click += (s, e) => ExportConfig();
            authCard.Controls.Add(btnExportConfig);

            y += 126;

            // ═══ PnP WARNING BANNER ═══
            var warnBanner = new Panel { Location = new Point(0, y), Size = new Size(panelW, 28), BackColor = Color.FromArgb(40, AppTheme.AccentGold) };
            warnBanner.Controls.Add(new Label
            {
                Text = "\u26A0  App credentials mode requires a PnP App (EntraID) with ClientId configured in Config \u2192 PnP APP section.",
                Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentGold, AutoSize = true, BackColor = Color.Transparent, Location = new Point(8, 6)
            });
            topHost.Controls.Add(warnBanner);
            y += 32;

            topHost.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(panelW, 1), BackColor = Color.FromArgb(34, AppTheme.Border) });
            y += 8;

            // ═══ EXTENSION GROUPS header + buttons ═══
            var btnBar = new Panel { Location = new Point(0, y), Size = new Size(panelW, 30), BackColor = Color.Transparent };
            topHost.Controls.Add(btnBar);
            btnBar.Controls.Add(new Label { Text = "EXTENSION GROUPS", Font = new Font("Segoe UI Semibold", 9f), ForeColor = AppTheme.AccentCyan, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 5) });
            var btnAddGroup = new FlatButton { Text = "+ New Group", Size = new Size(96, 24), Location = new Point(160, 3) };
            btnAddGroup.SetAccentColor(AppTheme.AccentGreen);
            btnAddGroup.Click += (s, e) => AddNewGroup();
            btnBar.Controls.Add(btnAddGroup);
            var btnReset = new FlatButton { Text = "Reset Defaults", Size = new Size(104, 24), Location = new Point(264, 3) };
            btnReset.SetGhostStyle();
            btnReset.Click += (s, e) => { _groupConfig = ExtensionGroupConfig.GetDefaults(); SaveConfig(); RebuildGroups(); };
            btnBar.Controls.Add(btnReset);
            var btnSave = new FlatButton { Text = "Save Config", Size = new Size(90, 24), Location = new Point(376, 3) };
            btnSave.SetAccentColor(AppTheme.AccentGold);
            btnSave.Click += (s, e) => { SaveConfig(); MessageBox.Show("Extension groups saved.", "Config", MessageBoxButtons.OK, MessageBoxIcon.Information); };
            btnBar.Controls.Add(btnSave);
            y += 32;

            _lblStatus = new Label { Text = "", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentGreen, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, y) };
            topHost.Controls.Add(_lblStatus);
            y += 22;

            // ═══ EXTENSION GROUPS scrollable area ═══
            _groupsPanel = new Panel
            {
                Location = new Point(0, y),
                Size = new Size(panelW, Math.Max(200, splitter.Panel1.Height - y - 10)),
                BackColor = Color.Transparent,
                AutoScroll = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                Padding = new Padding(0, 0, 8, 8)
            };
            topHost.Controls.Add(_groupsPanel);

            RebuildGroups();

            // ════════════════════════════════════════════════════
            //  BOTTOM PANEL: Console + Search Results
            // ════════════════════════════════════════════════════
            var bottomHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            splitter.Panel2.Controls.Add(bottomHost);

            var resultsCard = new GlassPanel
            {
                Dock = DockStyle.Top,
                Height = 140,
                AccentLeft = AppTheme.AccentCyan,
                Padding = new Padding(6, 4, 6, 6)
            };
            bottomHost.Controls.Add(resultsCard);

            // Results grid (scanned sites index)
            _resultsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Both,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            AppTheme.StyleDataGrid(_resultsGrid);
            _resultsGrid.Columns.Add("SiteUrl", "SITE URL");
            _resultsGrid.Columns.Add("Files", "FILES");
            _resultsGrid.Columns.Add("Scanned", "LAST SCANNED");
            _resultsGrid.Columns.Add("Duration", "DURATION");
            _resultsGrid.Columns.Add("Categories", "CATEGORIES");
            _resultsGrid.Columns["Files"].Width = 60;
            _resultsGrid.Columns["Scanned"].Width = 120;
            _resultsGrid.Columns["Duration"].Width = 70;
            resultsCard.Controls.Add(_resultsGrid);

            _lblResultsSummary = new Label { Text = "No search results yet.", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = false, Height = 18, Padding = new Padding(2, 2, 0, 0), BackColor = Color.Transparent, Dock = DockStyle.Top };
            resultsCard.Controls.Add(_lblResultsSummary);
            _lblResultsSummary.BringToFront();

            // Console output
            var consoleCard = new GlassPanel
            {
                Dock = DockStyle.Fill,
                AccentLeft = AppTheme.AccentGold,
                Padding = new Padding(6, 4, 6, 6)
            };
            bottomHost.Controls.Add(consoleCard);

            _searchConsole = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both,
                Font = AppTheme.FontMono, BackColor = AppTheme.BgInput, ForeColor = AppTheme.AccentGreen,
                BorderStyle = BorderStyle.FixedSingle, WordWrap = false
            };
            consoleCard.Controls.Add(_searchConsole);

            // Z-order: console card fills behind the results card
            consoleCard.SendToBack();

            LoadFileArchiveSettings();
            WireFileArchiveAutoSave();
        }

        private void LoadFileArchiveSettings()
        {
            var s = _config.LoadGuiSettings();
            _chkSummaryOnly.Checked = s.FileArchiveSummaryOnly;
            int idx = _cmbRegion.Items.IndexOf(s.FileArchiveRegion);
            if (idx >= 0) _cmbRegion.SelectedIndex = idx;
        }

        private void WireFileArchiveAutoSave()
        {
            _chkSummaryOnly.CheckedChanged += (s, e) => SaveFileArchiveSettings();
            _cmbRegion.SelectedIndexChanged += (s, e) => SaveFileArchiveSettings();
        }

        private void SaveFileArchiveSettings()
        {
            var s = _config.LoadGuiSettings();
            s.FileArchiveSummaryOnly = _chkSummaryOnly.Checked;
            s.FileArchiveRegion = _cmbRegion.SelectedItem?.ToString() ?? "BRA";
            _config.SaveGuiSettings(s);
        }

        private void RebuildGroups()
        {
            if (_groupsPanel == null || _groupsPanel.IsDisposed)
                return;

            _groupsPanel.Controls.Clear();
            int y = 0;
            int gutter = 10 + SystemInformation.VerticalScrollBarWidth;
            int cardW = Math.Max(400, _groupsPanel.ClientSize.Width - gutter);

            int totalExtensions = 0, enabledGroups = 0;

            for (int gi = 0; gi < _groupConfig.Groups.Count; gi++)
            {
                var group = _groupConfig.Groups[gi];
                int groupIdx = gi;

                Color accentColor;
                try { accentColor = ColorTranslator.FromHtml(group.Color ?? "#00d4ff"); } catch { accentColor = AppTheme.AccentCyan; }

                int cardH = 78;
                var card = new GlassPanel { Location = new Point(0, y), Size = new Size(cardW, cardH), AccentLeft = group.Enabled ? accentColor : AppTheme.TextMuted, Anchor = AnchorStyles.Top | AnchorStyles.Left };
                _groupsPanel.Controls.Add(card);

                var chk = new CheckBox { Checked = group.Enabled, AutoSize = true, BackColor = Color.Transparent, Location = new Point(12, 8) };
                chk.CheckedChanged += (s, e) => { _groupConfig.Groups[groupIdx].Enabled = chk.Checked; card.AccentLeft = chk.Checked ? accentColor : AppTheme.TextMuted; UpdateStatus(); };
                card.Controls.Add(chk);

                var txtName = new TextBox { Text = group.Name, Location = new Point(36, 8), Size = new Size(180, 20), BackColor = AppTheme.BgInput, ForeColor = accentColor, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI Semibold", 10f) };
                txtName.TextChanged += (s, e) => { _groupConfig.Groups[groupIdx].Name = txtName.Text; };
                card.Controls.Add(txtName);

                var lblCount = new Label { Text = $"{group.Extensions.Count} ext", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(224, 12) };
                card.Controls.Add(lblCount);

                var btnDel = new Label { Text = "\u2715", Font = new Font("Segoe UI", 10f), ForeColor = AppTheme.AccentRed, AutoSize = true, Cursor = Cursors.Hand, BackColor = Color.Transparent, Location = new Point(cardW - 30, 8), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                btnDel.Click += (s, e) =>
                {
                    if (MessageBox.Show($"Delete group '{_groupConfig.Groups[groupIdx].Name}'?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    { _groupConfig.Groups.RemoveAt(groupIdx); SaveConfig(); RebuildGroups(); }
                };
                card.Controls.Add(btnDel);

                var txtExts = new TextBox
                {
                    Text = string.Join(", ", group.Extensions),
                    Location = new Point(12, 34), Size = new Size(cardW - 34, 34),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Multiline = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
                    BackColor = AppTheme.BgInput, ForeColor = AppTheme.TextSecondary,
                    BorderStyle = BorderStyle.FixedSingle, Font = AppTheme.FontSmall
                };
                txtExts.Leave += (s, e) =>
                {
                    var exts = txtExts.Text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim().ToLowerInvariant()).Where(x => x.StartsWith(".")).Distinct().ToList();
                    _groupConfig.Groups[groupIdx].Extensions = exts;
                    lblCount.Text = $"{exts.Count} ext";
                    UpdateStatus();
                };
                card.Controls.Add(txtExts);

                if (group.Enabled) { totalExtensions += group.Extensions.Count; enabledGroups++; }
                y += cardH + 6;
            }

            _groupsPanel.Controls.Add(new Panel { Location = new Point(0, y + 10), Size = new Size(10, 1), BackColor = Color.Transparent });
            _groupsPanel.AutoScrollMinSize = new Size(0, y + 24);

            // Resize groups panel to fit content, letting the topHost scroll
            int neededH = y + 24;
            _groupsPanel.Height = neededH;
            var scrollParent = _groupsPanel.Parent as ScrollableControl;
            if (scrollParent != null)
            {
                scrollParent.AutoScrollMinSize = new Size(0, _groupsPanel.Top + neededH + 20);
            }

            _lblStatus.Text = $"{enabledGroups} group(s) enabled, {totalExtensions} extension(s) configured for search.";
        }

        private void UpdateStatus()
        {
            int total = 0, enabled = 0;
            foreach (var g in _groupConfig.Groups)
                if (g.Enabled) { enabled++; total += g.Extensions.Count; }
            _lblStatus.Text = $"{enabled} group(s) enabled, {total} extension(s) configured for search.";
        }

        private void AddNewGroup()
        {
            _groupConfig.Groups.Add(new ExtensionGroup { Name = "New Group", Color = "#00d4ff", Enabled = true, Extensions = new List<string>() });
            RebuildGroups();
        }

        private void RunFileSearch()
        {
            var enabledExts = _groupConfig.Groups.Where(g => g.Enabled).SelectMany(g => g.Extensions).Distinct().ToList();
            if (enabledExts.Count == 0) { MessageBox.Show("No extension groups enabled.", "File Search", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

            string siteUrl = (_txtSiteUrl.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(siteUrl))
            {
                MessageBox.Show("Enter a site URL to search.\n\nExample: https://contoso.sharepoint.com/sites/Finance", "File Search", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveConfig();
            _ = RunFileSearchAsync(siteUrl);
        }

        private async Task RunFileSearchAsync(string siteUrl)
        {
            _btnSearch.Enabled = false;
            _btnAbort.Enabled = true;
            _searchConsole.Clear();
            _searchCts = new CancellationTokenSource();
            _lblSearchStatus.Text = "Searching...";
            _lblSearchStatus.ForeColor = AppTheme.AccentCyan;

            Action<string> outHandler = msg => AppendSearchConsole(msg);
            Action<string> warnHandler = msg => AppendSearchConsole("[WARN] " + msg);
            Action<string> errHandler = msg => AppendSearchConsole("[ERROR] " + msg);

            _psHost.OnOutput += outHandler;
            _psHost.OnWarning += warnHandler;
            _psHost.OnError += errHandler;

            try
            {
                bool useInteractive = _rbInteractive.Checked;
                bool summaryOnly = _chkSummaryOnly.Checked;
                string region = _cmbRegion.SelectedItem?.ToString() ?? "NAM";

                string clientId = null, certThumb = null, tenantId = null;
                if (!useInteractive)
                {
                    var pnp = _config.AppConfig.PnPApp;
                    var entra = _config.AppConfig.EntraIdApp;
                    if (pnp != null && !string.IsNullOrWhiteSpace(pnp.ClientId))
                    {
                        clientId = pnp.ClientId;
                        certThumb = pnp.CertificateThumbprint;
                    }
                    else if (entra != null)
                    {
                        clientId = entra.ClientId;
                        certThumb = entra.CertificateThumbprint;
                    }
                    if (entra != null)
                        tenantId = entra.TenantId;
                }

                await _psHost.StartFileArchiveSearchAsync(siteUrl, useInteractive, summaryOnly, _searchCts.Token,
                    clientId, certThumb, tenantId, region);

                _lblSearchStatus.Text = "Search completed";
                _lblSearchStatus.ForeColor = AppTheme.AccentGreen;
                LoadSearchResults(siteUrl);
                ShowQueueSelectionModal(siteUrl);
            }
            catch (OperationCanceledException)
            {
                _lblSearchStatus.Text = "Cancelled";
                _lblSearchStatus.ForeColor = AppTheme.AccentGold;
            }
            catch (Exception ex)
            {
                AppendSearchConsole("[ERROR] " + ex.Message);
                _lblSearchStatus.Text = "Failed";
                _lblSearchStatus.ForeColor = AppTheme.AccentRed;
            }
            finally
            {
                _psHost.OnOutput -= outHandler;
                _psHost.OnWarning -= warnHandler;
                _psHost.OnError -= errHandler;
                UpdateRunButtonState();
                _btnAbort.Enabled = false;
                _searchCts?.Dispose();
                _searchCts = null;
            }
        }

        private void LoadSearchResults(string siteUrl)
        {
            try
            {
                string archivePath = Path.Combine(_config.LogsPath, "FileArchive");
                string indexPath = Path.Combine(archivePath, "index.json");
                if (!File.Exists(indexPath)) return;

                var index = JObject.Parse(File.ReadAllText(indexPath));
                var sites = index["Sites"] as JArray;
                if (sites == null) return;

                _resultsGrid.Rows.Clear();
                int totalFiles = 0;

                foreach (var site in sites)
                {
                    string url = site["SiteUrl"]?.ToString() ?? "";
                    int files = site["TotalFiles"]?.Value<int>() ?? 0;
                    string scanned = "";
                    try { scanned = DateTime.Parse(site["LastScanned"]?.ToString() ?? "").ToString("dd/MM/yyyy HH:mm"); } catch { }
                    string duration = site["Duration"]?.ToString() ?? "";
                    string cats = site["Categories"]?.ToString() ?? "";

                    _resultsGrid.Rows.Add(url, files, scanned, duration + "s", cats);
                    totalFiles += files;
                }

                _lblResultsSummary.Text = $"{sites.Count} site(s) scanned, {totalFiles:N0} total files found.";
            }
            catch (Exception ex)
            {
                _lblResultsSummary.Text = $"Error loading results: {ex.Message}";
            }
        }

        private void ShowQueueSelectionModal(string siteUrl)
        {
            try
            {
                string siteHash = ComputeSiteHash(siteUrl);
                string resultPath = Path.Combine(_config.LogsPath, "FileArchive", "site_" + siteHash + ".json");
                if (!File.Exists(resultPath))
                    return;

                var root = JObject.Parse(File.ReadAllText(resultPath));
                var files = root["Files"] as JArray;
                if (files == null || files.Count == 0)
                {
                    MessageBox.Show("Search completed, but there are no detailed file rows to queue. Disable 'Summary only' to queue individual files.", "File Archive", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using (var dlg = new Form())
                {
                    dlg.Text = "Search Results - Select Files for Queue";
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.Size = new Size(1320, 740);
                    dlg.MinimumSize = new Size(980, 580);
                    dlg.BackColor = AppTheme.BgDark;
                    dlg.ForeColor = AppTheme.TextPrimary;

                    var topHost = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Color.Transparent, Padding = new Padding(12, 8, 12, 8) };
                    dlg.Controls.Add(topHost);

                    var lblTitle = new Label
                    {
                        Text = "Select files to add into File Archive Queue",
                        Font = AppTheme.FontBody,
                        ForeColor = AppTheme.TextSecondary,
                        AutoSize = true,
                        BackColor = Color.Transparent,
                        Location = new Point(0, 2)
                    };
                    topHost.Controls.Add(lblTitle);

                    var txtFilter = new TextBox { Location = new Point(0, 24), Size = new Size(720, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    AppTheme.StyleTextBox(txtFilter);
                    topHost.Controls.Add(txtFilter);

                    var btnSelectAll = new FlatButton { Text = "Select All", Size = new Size(90, 24), Location = new Point(730, 24), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                    btnSelectAll.SetGhostStyle();
                    topHost.Controls.Add(btnSelectAll);

                    var btnClearSel = new FlatButton { Text = "Clear", Size = new Size(70, 24), Location = new Point(826, 24), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                    btnClearSel.SetGhostStyle();
                    topHost.Controls.Add(btnClearSel);

                    var lblInfo = new Label
                    {
                        Text = string.Format("Site: {0} | Files found: {1:N0}", siteUrl, files.Count),
                        Font = new Font("Cascadia Code", 7f),
                        ForeColor = AppTheme.TextMuted,
                        AutoSize = true,
                        BackColor = Color.Transparent,
                        Location = new Point(0, 52)
                    };
                    topHost.Controls.Add(lblInfo);

                    var grid = new DataGridView
                    {
                        Dock = DockStyle.Fill,
                        ReadOnly = true,
                        AllowUserToAddRows = false,
                        AllowUserToDeleteRows = false,
                        AllowUserToResizeRows = false,
                        RowHeadersVisible = false,
                        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                        MultiSelect = true,
                        BackgroundColor = AppTheme.BgInput,
                        ForeColor = AppTheme.TextPrimary,
                        GridColor = AppTheme.Border,
                        BorderStyle = BorderStyle.FixedSingle,
                        ScrollBars = ScrollBars.Both,
                        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
                    };
                    AppTheme.StyleDataGrid(grid);
                    grid.Columns.Add("Category", "CATEGORY");
                    grid.Columns.Add("Title", "TITLE");
                    grid.Columns.Add("FileUrl", "FILE URL");
                    grid.Columns.Add("Ext", "EXT");
                    grid.Columns.Add("SizeMB", "SIZE MB");
                    grid.Columns.Add("LastModified", "LAST MODIFIED");

                    grid.Columns["Category"].Width = 120;
                    grid.Columns["Title"].Width = 220;
                    grid.Columns["FileUrl"].Width = 640;
                    grid.Columns["Ext"].Width = 90;
                    grid.Columns["SizeMB"].Width = 90;
                    grid.Columns["LastModified"].Width = 140;
                    dlg.Controls.Add(grid);

                    var footer = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.Transparent };
                    var btnQueue = new FlatButton { Text = "Queue Selected", Size = new Size(132, 28), Location = new Point(12, 8) };
                    btnQueue.SetAccentColor(AppTheme.AccentGreen);
                    footer.Controls.Add(btnQueue);

                    var btnQueueCancel = new FlatButton { Text = "Close", Size = new Size(82, 28), Location = new Point(152, 8) };
                    btnQueueCancel.SetGhostStyle();
                    footer.Controls.Add(btnQueueCancel);

                    var lblSelected = new Label
                    {
                        Text = "Selected: 0",
                        Font = AppTheme.FontSmall,
                        ForeColor = AppTheme.TextSecondary,
                        AutoSize = true,
                        BackColor = Color.Transparent,
                        Location = new Point(244, 13)
                    };
                    footer.Controls.Add(lblSelected);
                    dlg.Controls.Add(footer);

                    var rows = files
                        .OfType<JObject>()
                        .Select(f => new
                        {
                            Category = f["Category"]?.ToString() ?? string.Empty,
                            Title = f["Title"]?.ToString() ?? string.Empty,
                            FileUrl = f["FileUrl"]?.ToString() ?? string.Empty,
                            Ext = f["FileExtension"]?.ToString() ?? string.Empty,
                            SizeMB = f["FileSizeMB"]?.Value<double?>() ?? 0d,
                            LastModified = f["LastModified"]?.ToString() ?? string.Empty
                        })
                        .ToList();

                    Action<string> bind = filter =>
                    {
                        string q = (filter ?? string.Empty).Trim().ToLowerInvariant();
                        var filtered = rows.Where(r =>
                                string.IsNullOrWhiteSpace(q) ||
                                (r.Title ?? string.Empty).ToLowerInvariant().Contains(q) ||
                                (r.FileUrl ?? string.Empty).ToLowerInvariant().Contains(q) ||
                                (r.Category ?? string.Empty).ToLowerInvariant().Contains(q))
                            .ToList();

                        grid.Rows.Clear();
                        foreach (var r in filtered)
                            grid.Rows.Add(r.Category, r.Title, r.FileUrl, r.Ext, r.SizeMB.ToString("0.##"), r.LastModified);

                        lblTitle.Text = string.Format("Select files to add into File Archive Queue - {0:N0} visible", filtered.Count);
                        lblSelected.Text = "Selected: 0";
                    };

                    bind(string.Empty);
                    txtFilter.TextChanged += (s, e) => bind(txtFilter.Text);
                    btnSelectAll.Click += (s, e) =>
                    {
                        grid.SelectAll();
                        lblSelected.Text = "Selected: " + grid.SelectedRows.Count;
                    };
                    btnClearSel.Click += (s, e) =>
                    {
                        grid.ClearSelection();
                        lblSelected.Text = "Selected: 0";
                    };
                    grid.SelectionChanged += (s, e) => lblSelected.Text = "Selected: " + grid.SelectedRows.Count;

                    btnQueue.Click += (s, e) =>
                    {
                        if (grid.SelectedRows.Count == 0)
                        {
                            MessageBox.Show("Select at least one file to queue.", "File Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }

                        var queue = _siteData.LoadFileArchiveQueue() ?? new FileArchiveQueueData();
                        int added = 0;
                        int skipped = 0;

                        foreach (DataGridViewRow row in grid.SelectedRows)
                        {
                            string fileUrl = row.Cells["FileUrl"].Value?.ToString() ?? string.Empty;
                            if (string.IsNullOrWhiteSpace(fileUrl))
                                continue;

                            bool exists = queue.Items.Any(i => string.Equals(i.FileUrl, fileUrl, StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(i.Status, "Archived", StringComparison.OrdinalIgnoreCase));
                            if (exists)
                            {
                                skipped++;
                                continue;
                            }

                            double fileSizeMb = 0;
                            double.TryParse(row.Cells["SizeMB"].Value?.ToString() ?? "0", out fileSizeMb);

                            queue.Items.Add(new FileArchiveQueueItem
                            {
                                SiteUrl = siteUrl,
                                FileUrl = fileUrl,
                                Title = row.Cells["Title"].Value?.ToString() ?? string.Empty,
                                Category = row.Cells["Category"].Value?.ToString() ?? string.Empty,
                                FileExtension = row.Cells["Ext"].Value?.ToString() ?? string.Empty,
                                FileSizeMB = fileSizeMb,
                                LastModified = row.Cells["LastModified"].Value?.ToString() ?? string.Empty,
                                QueuedAt = DateTime.UtcNow.ToString("o"),
                                Status = "Queued",
                                Source = "FileArchiveSearch"
                            });
                            added++;
                        }

                        _siteData.SaveFileArchiveQueue(queue);

                        var result = MessageBox.Show(
                            string.Format("Added {0} file(s) to queue. Skipped {1} already queued.\n\nOpen File Archive Queue now?", added, skipped),
                            "File Archive Queue",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Information);

                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();

                        if (result == DialogResult.Yes)
                            OpenQueueRequested?.Invoke(this, EventArgs.Empty);
                    };

                    btnQueueCancel.Click += (s, e) =>
                    {
                        dlg.DialogResult = DialogResult.Cancel;
                        dlg.Close();
                    };

                    dlg.ShowDialog(ParentForm);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to show queue selection modal: " + ex.Message, "File Archive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string ComputeSiteHash(string siteUrl)
        {
            string key = (siteUrl ?? string.Empty).Trim().TrimEnd('/').ToLowerInvariant();
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                var hex = BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
                return hex.Substring(0, 12);
            }
        }

        private void ExportConfig()
        {
            using (var dlg = new SaveFileDialog { Filter = "JSON|*.json", FileName = "ExtensionGroups.json" })
            {
                if (dlg.ShowDialog(ParentForm) != DialogResult.OK) return;
                File.WriteAllText(dlg.FileName, JsonConvert.SerializeObject(_groupConfig, Formatting.Indented));
            }
        }

        private void AppendSearchConsole(string text)
        {
            if (string.IsNullOrEmpty(text) || _searchConsole == null || _searchConsole.IsDisposed) return;
            Action a = () => { _searchConsole.AppendText(text + Environment.NewLine); _searchConsole.SelectionStart = _searchConsole.TextLength; _searchConsole.ScrollToCaret(); };
            if (InvokeRequired) Invoke(a); else a();
        }

        private void UpdateRunButtonState()
        {
            if (_btnSearch == null || _btnSearch.IsDisposed)
                return;

            _btnSearch.Enabled = !string.IsNullOrWhiteSpace(_txtSiteUrl?.Text);
        }

        private void PickSiteFromCatalog()
        {
            try
            {
                if (_allSitesCache == null || _allSitesCache.Count == 0)
                    _allSitesCache = _siteData.LoadCatalogSites(false);

                if (_allSitesCache.Count == 0)
                {
                    MessageBox.Show("No sites found in AllSites.json. Run Data Sync first.", "Pick Site", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var dlg = new Form())
                {
                    dlg.Text = "Select Target Site";
                    dlg.StartPosition = FormStartPosition.CenterParent;
                    dlg.Size = new Size(880, 560);
                    dlg.MinimumSize = new Size(720, 460);
                    dlg.BackColor = AppTheme.BgDark;
                    dlg.ForeColor = AppTheme.TextPrimary;

                    var body = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
                    dlg.Controls.Add(body);

                    var topHost = new Panel { Dock = DockStyle.Top, Height = 66, BackColor = Color.Transparent, Padding = new Padding(12, 8, 12, 8) };
                    body.Controls.Add(topHost);
                    var lblFilter = new Label
                    {
                        Text = "Search site (Title or URL)",
                        Font = AppTheme.FontSmall,
                        ForeColor = AppTheme.TextSecondary,
                        AutoSize = true,
                        BackColor = Color.Transparent,
                        Location = new Point(0, 2)
                    };
                    topHost.Controls.Add(lblFilter);

                    var txtFilter = new TextBox { Location = new Point(0, 22), Size = new Size(650, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    AppTheme.StyleTextBox(txtFilter);
                    topHost.Controls.Add(txtFilter);

                    var btnClearFilter = new FlatButton { Text = "Clear", Size = new Size(70, 24), Location = new Point(660, 22), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                    btnClearFilter.SetGhostStyle();
                    topHost.Controls.Add(btnClearFilter);

                    var lblFilterHint = new Label
                    {
                        Text = "Type to filter, then select one row and click 'Select URL'.",
                        Font = new Font("Cascadia Code", 6.5f),
                        ForeColor = AppTheme.TextMuted,
                        AutoSize = true,
                        BackColor = Color.Transparent,
                        Location = new Point(0, 49)
                    };
                    topHost.Controls.Add(lblFilterHint);

                    var grid = new DataGridView
                    {
                        Dock = DockStyle.Fill,
                        ReadOnly = true,
                        AllowUserToAddRows = false,
                        AllowUserToDeleteRows = false,
                        RowHeadersVisible = false,
                        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                        MultiSelect = false,
                        BackgroundColor = AppTheme.BgInput,
                        ForeColor = AppTheme.TextPrimary,
                        GridColor = AppTheme.Border,
                        BorderStyle = BorderStyle.FixedSingle,
                        ScrollBars = ScrollBars.Both,
                        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                        AllowUserToResizeRows = false,
                        ColumnHeadersVisible = true
                    };
                    AppTheme.StyleDataGrid(grid);
                    grid.Columns.Add("Title", "TITLE");
                    grid.Columns.Add("Url", "URL");
                    grid.Columns.Add("Owner", "OWNER");
                    grid.Columns.Add("Status", "STATUS");
                    grid.Columns.Add("Archive", "ARCHIVE");
                    grid.Columns.Add("Storage", "STORAGE (GB)");
                    grid.Columns.Add("Versions", "VERSIONS");
                    grid.Columns["Title"].Width = 180;
                    grid.Columns["Url"].Width = 360;
                    grid.Columns["Owner"].Width = 180;
                    grid.Columns["Status"].Width = 90;
                    grid.Columns["Archive"].Width = 100;
                    grid.Columns["Storage"].Width = 92;
                    grid.Columns["Versions"].Width = 80;
                    body.Controls.Add(grid);

                    var actionHost = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Color.Transparent };
                    var btnSelect = new FlatButton { Text = "Select URL", Size = new Size(128, 28), Location = new Point(8, 6) };
                    btnSelect.SetAccentColor(AppTheme.AccentGreen);
                    actionHost.Controls.Add(btnSelect);
                    var btnCancel = new FlatButton { Text = "Cancel", Size = new Size(80, 28), Location = new Point(144, 6) };
                    btnCancel.SetGhostStyle();
                    actionHost.Controls.Add(btnCancel);
                    var lblSelected = new Label
                    {
                        Text = "Selected URL: (none)",
                        Font = AppTheme.FontSmall,
                        ForeColor = AppTheme.TextMuted,
                        AutoSize = false,
                        Size = new Size(620, 24),
                        Location = new Point(232, 10),
                        BackColor = Color.Transparent,
                        Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
                    };
                    actionHost.Controls.Add(lblSelected);
                    body.Controls.Add(actionHost);
                    actionHost.BringToFront();
                    topHost.BringToFront();

                    Action<string> bind = filter =>
                    {
                        string q = (filter ?? string.Empty).Trim().ToLowerInvariant();
                        var rows = _allSitesCache.Where(s =>
                            string.IsNullOrWhiteSpace(q) ||
                            (s.Title ?? string.Empty).ToLowerInvariant().Contains(q) ||
                            (s.Url ?? string.Empty).ToLowerInvariant().Contains(q))
                            .Take(5000)
                            .ToList();

                        grid.Rows.Clear();
                        foreach (var s in rows)
                        {
                            double storageGb = Math.Round((s.StorageMB <= 0 ? 0d : s.StorageMB / 1024d), 2);
                            grid.Rows.Add(
                                s.Title ?? string.Empty,
                                s.Url ?? string.Empty,
                                s.Owner ?? string.Empty,
                                s.Status ?? string.Empty,
                                string.IsNullOrWhiteSpace(s.ArchiveStatus) ? "NotArchived" : s.ArchiveStatus,
                                storageGb.ToString("0.##"),
                                s.VersionCount.ToString());
                        }

                        lblFilter.Text = $"Search site (Title or URL) - {rows.Count:N0} result(s)";
                    };

                    bind(string.Empty);
                    txtFilter.TextChanged += (s, e) => bind(txtFilter.Text);
                    btnClearFilter.Click += (s, e) => { txtFilter.Text = string.Empty; txtFilter.Focus(); };

                    grid.SelectionChanged += (s, e) =>
                    {
                        if (grid.SelectedRows.Count == 0)
                        {
                            lblSelected.Text = "Selected URL: (none)";
                            return;
                        }

                        string selectedUrl = grid.SelectedRows[0].Cells["Url"].Value?.ToString() ?? string.Empty;
                        lblSelected.Text = string.IsNullOrWhiteSpace(selectedUrl)
                            ? "Selected URL: (none)"
                            : "Selected URL: " + selectedUrl;
                    };

                    Action choose = () =>
                    {
                        if (grid.SelectedRows.Count == 0)
                            return;

                        _txtSiteUrl.Text = grid.SelectedRows[0].Cells["Url"].Value?.ToString() ?? string.Empty;
                        UpdateRunButtonState();
                        dlg.DialogResult = DialogResult.OK;
                        dlg.Close();
                    };

                    grid.CellDoubleClick += (s, e) => choose();
                    grid.KeyDown += (s, e) =>
                    {
                        if (e.KeyCode == Keys.Enter)
                        {
                            e.Handled = true;
                            choose();
                        }
                    };
                    btnSelect.Click += (s, e) => choose();
                    btnCancel.Click += (s, e) => { dlg.DialogResult = DialogResult.Cancel; dlg.Close(); };

                    dlg.ShowDialog(ParentForm);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load site picker: " + ex.Message, "Pick Site", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FileArchivePanel_Resize(object sender, EventArgs e)
        {
            if (!Visible || IsDisposed || _groupsPanel == null || _groupsPanel.IsDisposed)
                return;

            string currentSite = _txtSiteUrl?.Text ?? string.Empty;
            bool interactive = _rbInteractive?.Checked ?? true;
            bool summaryOnly = _chkSummaryOnly?.Checked ?? false;
            string region = _cmbRegion?.SelectedItem?.ToString() ?? "BRA";

            BeginInvoke((Action)(() =>
            {
                if (IsDisposed)
                    return;

                BuildLayout();

                _txtSiteUrl.Text = currentSite;
                _rbInteractive.Checked = interactive;
                _rbAppCreds.Checked = !interactive;
                _chkSummaryOnly.Checked = summaryOnly;
                if (_cmbRegion.Items.Contains(region))
                    _cmbRegion.SelectedItem = region;
                UpdateRunButtonState();
            }));
        }
    }
}
