using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using SPOVersionManagement.Models;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    /// <summary>
    /// High-performance virtual-mode DataGridView for 190K+ site rows.
    /// Uses owner-drawn cells with two-line rows, status badges, and detail icons.
    /// </summary>
    public class VirtualSiteGrid : DataGridView
    {
        private const string ColumnConfigFile = "grid_columns.json";

        private static readonly Font FontMain = new Font("Segoe UI", 8.5f);
        private static readonly Font FontSmall = new Font("Segoe UI", 7.5f);
        private static readonly Font FontBold = new Font("Segoe UI Semibold", 8.5f);
        private static readonly Font FontHeader = new Font("Segoe UI Semibold", 8f);
        private static readonly Font FontIcon = new Font("Segoe UI", 10f);

        private List<SiteCatalogEntry> _allData = new List<SiteCatalogEntry>();
        private List<SiteCatalogEntry> _data = new List<SiteCatalogEntry>();
        private HashSet<string> _sitesWithHistory = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _queuedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _showQueueCheckboxes;
        private readonly Dictionary<int, HashSet<string>> _activeColumnFilters = new Dictionary<int, HashSet<string>>();
        private int _sortColumnIndex = -1;
        private SortOrder _sortDirection = SortOrder.None;
        private int _targetScroll = 0;
        private readonly Timer _scrollTimer;
        private bool _applyingColumnLayout;
        private bool _hasPersistedColumnLayout;

        // Column indices (catalog mode)
        private const int ColSelect = 0;
        private const int ColDetail = 1;
        private const int ColTitle = 2;
        private const int ColUrl = 3;
        private const int ColStorage = 4;
        private const int ColVersions = 5;
        private const int ColVersionSize = 6;
        private const int ColStatus = 7;
        private const int ColArchive = 8;
        private const int ColLock = 9;
        private const int ColLastMod = 10;
        private const int ColCreated = 11;
        private const int ColOwner = 12;

        private HashSet<string> _checkedUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _allChecked;

        // Clipboard-copied banner state (overlays the URL cell briefly)
        private int _copiedRowIndex = -1;
        private System.Windows.Forms.Timer _copiedTimer;

        public event EventHandler<SiteCatalogEntry> DetailRequested;
        public event EventHandler SelectionUpdated;
        public event EventHandler<SiteCatalogEntry> QueueToggled;
        public event EventHandler VisibleDataChanged;
        public event EventHandler CheckedChanged;

        public int TotalColumnsWidth
        {
            get
            {
                int w = 0;
                foreach (DataGridViewColumn col in Columns)
                    if (col.Visible) w += col.Width;
                return w;
            }
        }

        public VirtualSiteGrid()
        {
            VirtualMode = true;
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            AllowUserToResizeRows = false;
            ReadOnly = true;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            RowHeadersVisible = false;
            RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            ColumnHeadersVisible = true;
            ScrollBars = ScrollBars.Both;
            MultiSelect = true;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            _scrollTimer = new Timer { Interval = 16 };
            _scrollTimer.Tick += (s, e) =>
            {
                int maxOffset = GetMaxHorizontalOffset();
                if (_targetScroll > maxOffset)
                    _targetScroll = maxOffset;

                int diff = _targetScroll - HorizontalScrollingOffset;
                if (Math.Abs(diff) <= 1)
                {
                    HorizontalScrollingOffset = _targetScroll;
                    _scrollTimer.Stop();
                    Invalidate();
                    return;
                }

                int step = diff / 4;
                if (step == 0)
                    step = Math.Sign(diff);

                int next = HorizontalScrollingOffset + step;
                HorizontalScrollingOffset = Math.Max(0, Math.Min(next, maxOffset));
                Invalidate();
            };

            // Use reflection to enable double-buffering without interfering with scrollbar rendering
            typeof(DataGridView).InvokeMember("DoubleBuffered",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.SetProperty,
                null, this, new object[] { true });

            CellValueNeeded += OnCellValueNeeded;
            CellPainting += OnCellPainting;
            CellDoubleClick += OnCellDoubleClick;
            CellClick += OnCellClick;
            CellMouseMove += OnCellMouseMove;
            ColumnHeaderMouseClick += OnColumnHeaderMouseClick;
            SelectionChanged += (s, e) => SelectionUpdated?.Invoke(this, EventArgs.Empty);
            ColumnWidthChanged += OnColumnWidthChanged;

            ApplyTheme();
            SetupColumns();
            LoadColumnLayout();
            EnsureHeaderState();
        }

        private void ApplyTheme()
        {
            BackgroundColor = AppTheme.BgDark;
            ForeColor = AppTheme.TextPrimary;
            GridColor = AppTheme.Border;
            BorderStyle = BorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            EnableHeadersVisualStyles = false;

            DefaultCellStyle.BackColor = AppTheme.BgDark;
            DefaultCellStyle.ForeColor = AppTheme.TextPrimary;
            DefaultCellStyle.SelectionBackColor = AppTheme.BgCard;
            DefaultCellStyle.SelectionForeColor = AppTheme.AccentCyan;
            DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            DefaultCellStyle.Font = AppTheme.FontBody;

            AlternatingRowsDefaultCellStyle.BackColor = AppTheme.BgMedium;

            ColumnHeadersDefaultCellStyle.BackColor = AppTheme.BgHeader;
            ColumnHeadersDefaultCellStyle.ForeColor = AppTheme.AccentCyan;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = AppTheme.BgHeader;
            ColumnHeadersDefaultCellStyle.SelectionForeColor = AppTheme.AccentCyan;
            ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            ColumnHeadersDefaultCellStyle.Font = FontHeader;
            ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
            EnsureHeaderState();
        }

        private void EnsureHeaderState()
        {
            ColumnHeadersVisible = true;
            EnableHeadersVisualStyles = false;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            ColumnHeadersHeight = 40;
            ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            ScrollBars = ScrollBars.Both;
        }

        public void EnsureHeadersVisible()
        {
            EnsureHeaderState();
            Invalidate();
            Refresh();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            EnsureHeaderState();
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                EnsureHeaderState();
                Invalidate();
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            EnsureHeaderState();
        }

        private void SetupColumns()
        {
            Columns.Clear();
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            // 0: Select checkbox
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Select", HeaderText = "\u2610", Width = 36, MinimumWidth = 36, Resizable = DataGridViewTriState.False, SortMode = DataGridViewColumnSortMode.NotSortable });
            // 1: Detail icon
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Detail", HeaderText = "", Width = 32, MinimumWidth = 32, Resizable = DataGridViewTriState.False, SortMode = DataGridViewColumnSortMode.NotSortable });
            // 1: Title
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "TITLE", Width = 180, MinimumWidth = 120, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 2: URL
            Columns.Add(new DataGridViewTextBoxColumn { Name = "URL", HeaderText = "URL", Width = 400, MinimumWidth = 220, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 3: Storage
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Storage", HeaderText = "STORAGE", Width = 80, MinimumWidth = 60, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 4: Versions
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Versions", HeaderText = "VERSIONS", Width = 80, MinimumWidth = 60, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 5: Version Size
            Columns.Add(new DataGridViewTextBoxColumn { Name = "VersionSize", HeaderText = "VER. SIZE", Width = 100, MinimumWidth = 70, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 6: Status
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "STATUS", Width = 80, MinimumWidth = 60, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 7: Archive
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Archive", HeaderText = "ARCHIVE", Width = 110, MinimumWidth = 80, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 8: Lock
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Lock", HeaderText = "LOCK", Width = 70, MinimumWidth = 50, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 9: Last Modified
            Columns.Add(new DataGridViewTextBoxColumn { Name = "LastMod", HeaderText = "LAST MODIF.", Width = 90, MinimumWidth = 70, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 10: Created
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "CREATED", Width = 90, MinimumWidth = 70, SortMode = DataGridViewColumnSortMode.Programmatic });
            // 11: Owner
            Columns.Add(new DataGridViewTextBoxColumn { Name = "Owner", HeaderText = "OWNER", Width = 140, MinimumWidth = 80, SortMode = DataGridViewColumnSortMode.Programmatic });

            Columns["Select"].Frozen = true;
            Columns["Detail"].Frozen = true;
            Columns["Title"].Frozen = true;
        }

        /// <summary>
        /// Sets the set of site URLs that have execution history (for green icon indicator).
        /// </summary>
        public void SetHistorySites(HashSet<string> urls)
        {
            _sitesWithHistory = urls ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Columns.Count > ColDetail)
                InvalidateColumn(ColDetail);
            else
                Invalidate();
        }

        /// <summary>
        /// Enables queue checkboxes in the Detail column. When enabled, clicking the detail column
        /// toggles the queue state instead of opening the detail dialog.
        /// </summary>
        public void SetQueueMode(bool enabled, HashSet<string> queuedUrls = null)
        {
            _showQueueCheckboxes = enabled;
            _queuedUrls = queuedUrls ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Columns.Count > ColDetail)
                InvalidateColumn(ColDetail);
            else
                Invalidate();
        }

        /// <summary>
        /// Returns true if the site URL is currently marked as queued.
        /// </summary>
        public bool IsQueued(string url)
        {
            return _queuedUrls.Contains(url);
        }

        public void SetData(List<SiteCatalogEntry> data)
        {
            _allData = data?.ToList() ?? new List<SiteCatalogEntry>();
            _checkedUrls.Clear();
            _allChecked = false;
            UpdateSelectAllHeader();
            EnsureHeaderState();

            RowTemplate.Height = 48;
            ApplyFiltersAndSort();

            _targetScroll = 0;
            HorizontalScrollingOffset = 0;

            PerformLayout();
            EnsureHeaderState();
            Refresh();
            Invalidate();
        }

        public List<SiteCatalogEntry> GetVisibleSites()
        {
            return _data?.ToList() ?? new List<SiteCatalogEntry>();
        }

        public void AdjustColumnWidths()
        {
            if (Columns.Count == 0)
                return;

            if (_hasPersistedColumnLayout)
                return;

            _applyingColumnLayout = true;
            try
            {
                SetColumnWidth("Select", 36);
                SetColumnWidth("Detail", 32);
                SetColumnWidth("Storage", 80);
                SetColumnWidth("Versions", 80);
                SetColumnWidth("Lock", 70);
                SetColumnWidth("VersionSize", 110);
                SetColumnWidth("Status", 90);
                SetColumnWidth("Archive", 120);
                SetColumnWidth("LastMod", 110);
                SetColumnWidth("Created", 110);
                SetColumnWidth("Title", 260);
                SetColumnWidth("URL", 520);
                SetColumnWidth("Owner", 220);
            }
            finally
            {
                _applyingColumnLayout = false;
            }
        }

        public void SaveColumnLayout()
        {
            if (_applyingColumnLayout)
                return;

            try
            {
                var dict = Columns
                    .Cast<DataGridViewColumn>()
                    .ToDictionary(c => c.Name, c => c.Width);

                File.WriteAllText(ColumnConfigFile, JsonConvert.SerializeObject(dict));
            }
            catch
            {
                // Best-effort persistence; keep grid functional when IO is unavailable.
            }
        }

        public void LoadColumnLayout()
        {
            if (!File.Exists(ColumnConfigFile))
                return;

            try
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(ColumnConfigFile));
                if (dict == null || dict.Count == 0)
                    return;

                _applyingColumnLayout = true;
                foreach (var kv in dict)
                    SetColumnWidth(kv.Key, kv.Value);

                _hasPersistedColumnLayout = true;
            }
            catch
            {
                // Ignore malformed/locked config files.
            }
            finally
            {
                _applyingColumnLayout = false;
            }
        }

        private void OnColumnWidthChanged(object sender, DataGridViewColumnEventArgs e)
        {
            if (_applyingColumnLayout || e?.Column == null)
                return;

            _hasPersistedColumnLayout = true;
            SaveColumnLayout();
        }

        private int GetMaxHorizontalOffset()
        {
            return Math.Max(0, TotalColumnsWidth - ClientSize.Width + 1);
        }

        private void SetColumnWidth(string columnName, int width)
        {
            if (!Columns.Contains(columnName))
                return;

            var col = Columns[columnName];
            col.Width = Math.Max(col.MinimumWidth, width);
        }

        public SiteCatalogEntry GetSiteAt(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _data.Count)
                return _data[rowIndex];
            return null;
        }

        public List<SiteCatalogEntry> GetSelectedSites()
        {
            // Prefer checked rows if any are checked
            if (_checkedUrls.Count > 0)
                return GetCheckedSites();

            var result = new List<SiteCatalogEntry>();
            foreach (DataGridViewRow row in SelectedRows)
            {
                if (row.Index >= 0 && row.Index < _data.Count)
                    result.Add(_data[row.Index]);
            }
            return result;
        }

        public List<string> GetSelectedUrls()
        {
            // Prefer checked rows if any are checked
            if (_checkedUrls.Count > 0)
                return GetCheckedUrls();

            var urls = new List<string>();
            foreach (DataGridViewRow row in SelectedRows)
            {
                if (row.Index >= 0 && row.Index < _data.Count)
                {
                    var site = _data[row.Index];
                    if (!string.IsNullOrWhiteSpace(site.Url))
                        urls.Add(site.Url);
                }
            }
            return urls;
        }

        public List<string> GetCheckedQueueUrlsFromVisible()
        {
            return (_data ?? new List<SiteCatalogEntry>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Url) && _queuedUrls.Contains(s.Url))
                .Select(s => s.Url)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public int CheckedCount => _checkedUrls.Count;

        public List<SiteCatalogEntry> GetCheckedSites()
        {
            return (_data ?? new List<SiteCatalogEntry>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Url) && _checkedUrls.Contains(s.Url))
                .ToList();
        }

        public List<string> GetCheckedUrls()
        {
            return (_data ?? new List<SiteCatalogEntry>())
                .Where(s => !string.IsNullOrWhiteSpace(s.Url) && _checkedUrls.Contains(s.Url))
                .Select(s => s.Url)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void ClearChecked()
        {
            _checkedUrls.Clear();
            _allChecked = false;
            UpdateSelectAllHeader();
            if (Columns.Count > ColSelect)
                InvalidateColumn(ColSelect);
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ToggleSelectAll()
        {
            if (_allChecked)
            {
                _checkedUrls.Clear();
                _allChecked = false;
            }
            else
            {
                foreach (var site in _data)
                {
                    if (!string.IsNullOrWhiteSpace(site.Url))
                        _checkedUrls.Add(site.Url);
                }
                _allChecked = true;
            }

            UpdateSelectAllHeader();
            if (Columns.Count > ColSelect)
                InvalidateColumn(ColSelect);
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateSelectAllHeader()
        {
            if (Columns.Count > ColSelect)
                Columns[ColSelect].HeaderText = _allChecked ? "\u2611" : "\u2610";
        }

        public void ClearColumnFiltersAndSort()
        {
            _activeColumnFilters.Clear();
            _sortColumnIndex = -1;
            _sortDirection = SortOrder.None;
            ApplyFiltersAndSort();
        }

        private void OnCellValueNeeded(object sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _data.Count) return;
            var site = _data[e.RowIndex];

            switch (e.ColumnIndex)
            {
                case ColSelect:
                    bool chk = site != null && !string.IsNullOrWhiteSpace(site.Url) && _checkedUrls.Contains(site.Url);
                    e.Value = chk ? "\u2611" : "\u2610";
                    break;
                case ColDetail: e.Value = "\uD83D\uDD0D"; break; // magnifying glass
                case ColTitle: e.Value = site.Title ?? ""; break;
                case ColUrl: e.Value = site.Url ?? ""; break;
                case ColStorage: e.Value = site.StorageDisplay; break;
                case ColVersions: e.Value = site.VersionCount.ToString("N0"); break;
                case ColVersionSize: e.Value = site.VersionSizeDisplay; break;
                case ColStatus: e.Value = site.Status ?? ""; break;
                case ColArchive: e.Value = site.ArchiveStatus ?? ""; break;
                case ColLock: e.Value = site.LockState ?? ""; break;
                case ColLastMod: e.Value = site.LastModifiedDisplay; break;
                case ColCreated: e.Value = site.CreatedDisplay; break;
                case ColOwner: e.Value = site.Owner ?? ""; break;
            }
        }

        private void OnCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                // Paint header with filter dropdown indicator
                if (e.ColumnIndex >= ColTitle && e.ColumnIndex <= ColOwner)
                {
                    e.PaintBackground(e.CellBounds, false);
                    e.PaintContent(e.CellBounds);

                    // Draw small dropdown triangle on the right edge
                    int triSize = 6;
                    int triX = e.CellBounds.Right - triSize - 8;
                    int triY = e.CellBounds.Top + (e.CellBounds.Height - triSize) / 2;
                    bool hasFilter = _activeColumnFilters.ContainsKey(e.ColumnIndex);
                    var triColor = hasFilter ? AppTheme.AccentGold : Color.FromArgb(100, AppTheme.TextMuted);
                    var triPoints = new Point[]
                    {
                        new Point(triX, triY),
                        new Point(triX + triSize, triY),
                        new Point(triX + triSize / 2, triY + triSize)
                    };
                    using (var brush = new SolidBrush(triColor))
                        e.Graphics.FillPolygon(brush, triPoints);

                    e.Handled = true;
                    return;
                }
                e.Handled = false;
                return;
            }

            if (e.RowIndex >= _data.Count) return;

            e.Handled = true;

            var g = e.Graphics;
            var state = g.Save();

            // CRITICAL: prevent drawing outside the current cell bounds.
            g.SetClip(e.CellBounds);

            try
            {
                var site = _data[e.RowIndex];
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                // Draw background
                bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;
                bool isAlt = e.RowIndex % 2 == 1;
                Color bg = isSelected ? AppTheme.BgCard : (isAlt ? AppTheme.BgMedium : AppTheme.BgDark);
                using (var brush = new SolidBrush(bg))
                    g.FillRectangle(brush, e.CellBounds);

                // Bottom border
                using (var pen = new Pen(AppTheme.Border))
                    g.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);

                var bounds = e.CellBounds;
                int pad = 6;

                switch (e.ColumnIndex)
                {
                    case ColSelect:
                        DrawSelectCheckbox(g, bounds, isSelected, site);
                        break;

                    case ColDetail:
                        DrawDetailIcon(g, bounds, isSelected, site);
                        break;

                    case ColTitle:
                        DrawTwoLineCell(g, bounds, pad, site.Title ?? "", site.Template ?? "", AppTheme.TextPrimary, AppTheme.TextMuted, isSelected);
                        break;

                    case ColUrl:
                        DrawTwoLineCell(g, bounds, pad, site.Url ?? "", site.Owner ?? "", AppTheme.AccentCyan, AppTheme.TextMuted, isSelected);
                        if (e.RowIndex == _copiedRowIndex)
                            DrawCopiedBanner(g, bounds);
                        break;

                    case ColStorage:
                        DrawStorageCell(g, bounds, pad, site);
                        break;

                    case ColVersions:
                        DrawVersionsCell(g, bounds, pad, site);
                        break;

                    case ColVersionSize:
                        DrawVersionSizeCell(g, bounds, pad, site);
                        break;

                    case ColStatus:
                        DrawBadgeCell(g, bounds, pad, site.Status, GetStatusColor(site.Status));
                        break;

                    case ColArchive:
                        DrawBadgeCell(g, bounds, pad, site.ArchiveStatus, GetArchiveColor(site.ArchiveStatus));
                        break;

                    case ColLock:
                        string lockText = site.LockState ?? "";
                        Color lockColor = string.Equals(lockText, "Unlock", StringComparison.OrdinalIgnoreCase) ? AppTheme.TextMuted : AppTheme.AccentRed;
                        DrawSimpleText(g, bounds, pad, lockText, lockColor);
                        break;

                    case ColLastMod:
                        DrawSimpleText(g, bounds, pad, site.LastModifiedDisplay, AppTheme.TextSecondary);
                        break;

                    case ColCreated:
                        DrawSimpleText(g, bounds, pad, site.CreatedDisplay, AppTheme.TextSecondary);
                        break;

                    case ColOwner:
                        DrawTwoLineCell(g, bounds, pad, TruncateText(site.Owner, 28), site.IsInactive ? "Inactive" : (site.IsOwnerless ? "Ownerless" : ""),
                            AppTheme.TextSecondary, site.IsInactive || site.IsOwnerless ? AppTheme.AccentGold : AppTheme.TextMuted, isSelected);
                        break;
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            EnsureHeaderState();
            base.OnPaint(e);

            int frozenWidth = 0;
            foreach (DataGridViewColumn col in Columns)
            {
                if (col.Visible && col.Frozen)
                    frozenWidth += col.Width;
            }

            if (HorizontalScrollingOffset > 0 && frozenWidth > 0)
            {
                var shadowRect = new Rectangle(frozenWidth - 1, 0, 12, Height);
                using (var brush = new LinearGradientBrush(
                    shadowRect,
                    Color.FromArgb(120, 0, 0, 0),
                    Color.Transparent,
                    LinearGradientMode.Horizontal))
                {
                    e.Graphics.FillRectangle(brush, shadowRect);
                }
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            _targetScroll -= e.Delta / 2;
            _targetScroll = Math.Max(0, Math.Min(_targetScroll, GetMaxHorizontalOffset()));

            if (!_scrollTimer.Enabled)
                _scrollTimer.Start();
        }

        private void DrawSelectCheckbox(Graphics g, Rectangle bounds, bool isSelected, SiteCatalogEntry site)
        {
            bool isChecked = site != null
                && !string.IsNullOrWhiteSpace(site.Url)
                && _checkedUrls.Contains(site.Url);
            string glyph = isChecked ? "\u2611" : "\u2610";
            Color color = isChecked ? AppTheme.AccentCyan : (isSelected ? Color.FromArgb(120, AppTheme.AccentCyan) : AppTheme.TextMuted);
            TextRenderer.DrawText(g, glyph, FontIcon, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private void DrawDetailIcon(Graphics g, Rectangle bounds, bool isSelected, SiteCatalogEntry site)
        {
            if (_showQueueCheckboxes)
            {
                bool isChecked = site != null
                    && !string.IsNullOrWhiteSpace(site.Url)
                    && _queuedUrls.Contains(site.Url);
                string glyph = isChecked ? "\u2611" : "\u2610";
                Color color = isChecked ? AppTheme.AccentGreen : (isSelected ? AppTheme.AccentCyan : AppTheme.TextMuted);
                TextRenderer.DrawText(g, glyph, FontIcon, bounds, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            bool hasHistory = site != null && !string.IsNullOrWhiteSpace(site.Url)
                && _sitesWithHistory.Contains(NormalizeSiteUrlKey(site.Url));
            Color iconColor = isSelected ? AppTheme.AccentCyan
                : hasHistory ? AppTheme.AccentGreen
                : AppTheme.AccentPurple;
            TextRenderer.DrawText(g, "\u2315", FontIcon, bounds, iconColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
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

        private void DrawTwoLineCell(Graphics g, Rectangle bounds, int pad, string line1, string line2, Color color1, Color color2, bool isSelected)
        {
            int y1 = bounds.Top + 6;
            int y2 = bounds.Top + 24;
            int right = bounds.Right - pad;
            int width = right - bounds.Left - pad;

            var rect1 = new Rectangle(bounds.Left + pad, y1, width, 16);
            var rect2 = new Rectangle(bounds.Left + pad, y2, width, 14);

            TextRenderer.DrawText(g, line1 ?? "", FontMain, rect1, isSelected ? AppTheme.AccentCyan : color1,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            if (!string.IsNullOrWhiteSpace(line2))
                TextRenderer.DrawText(g, line2, FontSmall, rect2, color2,
                    TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }

        private void DrawStorageCell(Graphics g, Rectangle bounds, int pad, SiteCatalogEntry site)
        {
            string main = site.StorageDisplay;
            Color color = site.StorageMB >= 1024 ? AppTheme.AccentGold : AppTheme.TextPrimary;

            var rect = new Rectangle(bounds.Left + pad, bounds.Top, bounds.Width - pad * 2, bounds.Height);
            TextRenderer.DrawText(g, main, FontBold, rect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawVersionsCell(Graphics g, Rectangle bounds, int pad, SiteCatalogEntry site)
        {
            string text = site.VersionCount.ToString("N0");
            Color color = site.VersionCount > 0 ? AppTheme.AccentCyan : AppTheme.TextMuted;

            var rect = new Rectangle(bounds.Left + pad, bounds.Top, bounds.Width - pad * 2, bounds.Height);
            TextRenderer.DrawText(g, text, FontBold, rect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        private void DrawVersionSizeCell(Graphics g, Rectangle bounds, int pad, SiteCatalogEntry site)
        {
            string main = site.VersionSizeDisplay;
            double pct = site.StorageMB > 0 ? (site.VersionSizeMB / site.StorageMB * 100.0) : 0;
            string pctText = pct > 0 ? $"{pct:F0}%" : "";

            int y1 = bounds.Top + 8;
            int y2 = bounds.Top + 26;
            int width = bounds.Width - pad * 2;

            var rect1 = new Rectangle(bounds.Left + pad, y1, width, 16);
            TextRenderer.DrawText(g, main, FontMain, rect1, AppTheme.TextPrimary,
                TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);

            if (!string.IsNullOrWhiteSpace(pctText))
            {
                Color pctColor = pct > 50 ? AppTheme.AccentRed : pct > 20 ? AppTheme.AccentGold : AppTheme.AccentGreen;
                // Draw small rounded badge
                var pctSize = TextRenderer.MeasureText(pctText, FontSmall);
                int badgeW = pctSize.Width + 8;
                int badgeH = 16;
                var badgeRect = new Rectangle(bounds.Left + pad, y2, badgeW, badgeH);

                using (var bgBrush = new SolidBrush(Color.FromArgb(50, pctColor)))
                using (var path = AppTheme.CreateRoundedRect(badgeRect, 4))
                    g.FillPath(bgBrush, path);

                TextRenderer.DrawText(g, pctText, FontSmall, badgeRect, pctColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawBadgeCell(Graphics g, Rectangle bounds, int pad, string text, Color color)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var textSize = TextRenderer.MeasureText(text, FontSmall);
            int badgeW = Math.Min(textSize.Width + 12, bounds.Width - pad * 2);
            int badgeH = 20;
            int x = bounds.Left + pad;
            int y = bounds.Top + (bounds.Height - badgeH) / 2;
            var badgeRect = new Rectangle(x, y, badgeW, badgeH);

            using (var bgBrush = new SolidBrush(Color.FromArgb(40, color)))
            using (var borderPen = new Pen(Color.FromArgb(100, color)))
            using (var path = AppTheme.CreateRoundedRect(badgeRect, 4))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }

            TextRenderer.DrawText(g, text, FontSmall, badgeRect, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void DrawSimpleText(Graphics g, Rectangle bounds, int pad, string text, Color color)
        {
            var rect = new Rectangle(bounds.Left + pad, bounds.Top, bounds.Width - pad * 2, bounds.Height);
            TextRenderer.DrawText(g, text ?? "", FontMain, rect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void OnCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _data.Count)
                DetailRequested?.Invoke(this, _data[e.RowIndex]);
        }

        private void OnCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _data.Count) return;

            // Click on Select checkbox column → toggle check state
            if (e.ColumnIndex == ColSelect)
            {
                var site = _data[e.RowIndex];
                if (site != null && !string.IsNullOrWhiteSpace(site.Url))
                {
                    if (_checkedUrls.Contains(site.Url))
                        _checkedUrls.Remove(site.Url);
                    else
                        _checkedUrls.Add(site.Url);

                    _allChecked = _data.Count > 0 && _data.All(s => !string.IsNullOrWhiteSpace(s.Url) && _checkedUrls.Contains(s.Url));
                    UpdateSelectAllHeader();
                    InvalidateCell(ColSelect, e.RowIndex);
                    CheckedChanged?.Invoke(this, EventArgs.Empty);
                }
                return;
            }

            // Click on URL column → copy URL to clipboard + show inline banner over the cell
            if (e.ColumnIndex == ColUrl)
            {
                var site = _data[e.RowIndex];
                if (!string.IsNullOrWhiteSpace(site.Url))
                {
                    try { Clipboard.SetText(site.Url); } catch { }
                    ShowCopiedBanner(e.RowIndex);
                }
                return;
            }

            if (e.ColumnIndex == ColDetail)
            {
                var site = _data[e.RowIndex];

                if (_showQueueCheckboxes)
                {
                    if (!string.IsNullOrWhiteSpace(site.Url))
                    {
                        if (_queuedUrls.Contains(site.Url))
                            _queuedUrls.Remove(site.Url);
                        else
                            _queuedUrls.Add(site.Url);

                        QueueToggled?.Invoke(this, site);
                        InvalidateCell(e.ColumnIndex, e.RowIndex);
                    }
                    return;
                }

                DetailRequested?.Invoke(this, site);
            }
        }

        private void OnCellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            Cursor = (e.RowIndex >= 0 && e.ColumnIndex == ColUrl) ? Cursors.Hand : Cursors.Default;
        }


        private static Color GetStatusColor(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return AppTheme.TextMuted;
            if (status.Equals("Active", StringComparison.OrdinalIgnoreCase)) return AppTheme.AccentGreen;
            if (status.Equals("Locked", StringComparison.OrdinalIgnoreCase)) return AppTheme.AccentRed;
            return AppTheme.AccentGold;
        }

        private static Color GetArchiveColor(string archive)
        {
            if (string.IsNullOrWhiteSpace(archive) || archive.Equals("NotArchived", StringComparison.OrdinalIgnoreCase))
                return AppTheme.TextMuted;
            if (archive.Contains("Archived"))
                return AppTheme.AccentGold;
            return AppTheme.AccentPurple;
        }

        private static string TruncateUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";
            return url;
        }

        private void ShowCopiedBanner(int rowIndex)
        {
            _copiedRowIndex = rowIndex;
            InvalidateCell(ColUrl, rowIndex);
            if (_copiedTimer == null)
            {
                _copiedTimer = new System.Windows.Forms.Timer { Interval = 1200 };
                _copiedTimer.Tick += (s, e) =>
                {
                    _copiedTimer.Stop();
                    int prev = _copiedRowIndex;
                    _copiedRowIndex = -1;
                    if (prev >= 0 && prev < RowCount) InvalidateCell(ColUrl, prev);
                };
            }
            _copiedTimer.Stop();
            _copiedTimer.Start();
        }

        private static void DrawCopiedBanner(Graphics g, Rectangle bounds)
        {
            const string text = "Url copied to clipboard";
            using (var font = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            using (var bg = new SolidBrush(Color.FromArgb(230, AppTheme.AccentGreen)))
            using (var fg = new SolidBrush(Color.Black))
            {
                var size = g.MeasureString(text, font);
                int w = (int)Math.Ceiling(size.Width) + 16;
                int h = (int)Math.Ceiling(size.Height) + 6;
                int x = bounds.X + (bounds.Width - w) / 2;
                int y = bounds.Y + (bounds.Height - h) / 2;
                var rect = new Rectangle(x, y, w, h);
                using (var path = RoundedRect(rect, 6))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.FillPath(bg, path);
                }
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(text, font, fg, rect, sf);
            }
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string TruncateText(string text, int max)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Length <= max ? text : text.Substring(0, max - 1) + "\u2026";
        }

        private void OnColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            // Click on Select header → toggle all
            if (e.ColumnIndex == ColSelect)
            {
                ToggleSelectAll();
                return;
            }

            if (e.ColumnIndex == ColDetail || e.ColumnIndex < 0) return;
            if (_allData == null || _allData.Count == 0) return;

            if (e.Button == MouseButtons.Right)
            {
                ShowHeaderFilterMenu(e.ColumnIndex, Cursor.Position);
                return;
            }

            if (e.Button != MouseButtons.Left)
                return;

            int colWidth = (e.ColumnIndex >= 0 && e.ColumnIndex < Columns.Count) ? Columns[e.ColumnIndex].Width : 0;
            if (e.X >= Math.Max(0, colWidth - 20))
            {
                ShowHeaderFilterMenu(e.ColumnIndex, Cursor.Position);
                return;
            }

            if (_sortColumnIndex == e.ColumnIndex)
                _sortDirection = _sortDirection == SortOrder.Ascending ? SortOrder.Descending : SortOrder.Ascending;
            else
            {
                _sortColumnIndex = e.ColumnIndex;
                _sortDirection = SortOrder.Ascending;
            }

            ApplyFiltersAndSort();
        }

        private void SortData()
        {
            if (_data == null)
                _data = new List<SiteCatalogEntry>();

            if (_sortColumnIndex >= 0 && _data.Count > 0)
            {
                bool asc = _sortDirection != SortOrder.Descending;
                Func<SiteCatalogEntry, object> keySelector;

                switch (_sortColumnIndex)
                {
                    case ColTitle: keySelector = s => s.Title ?? ""; break;
                    case ColUrl: keySelector = s => s.Url ?? ""; break;
                    case ColStorage: keySelector = s => s.StorageMB; break;
                    case ColVersions: keySelector = s => s.VersionCount; break;
                    case ColVersionSize: keySelector = s => s.VersionSizeMB; break;
                    case ColStatus: keySelector = s => s.Status ?? ""; break;
                    case ColArchive: keySelector = s => s.ArchiveStatus ?? ""; break;
                    case ColLock: keySelector = s => s.LockState ?? ""; break;
                    case ColLastMod: keySelector = s => s.LastModified ?? DateTime.MinValue; break;
                    case ColCreated: keySelector = s => s.Created ?? DateTime.MinValue; break;
                    case ColOwner: keySelector = s => s.Owner ?? ""; break;
                    default: keySelector = s => s.Title ?? ""; break;
                }

                _data = asc
                    ? _data.OrderBy(keySelector).ToList()
                    : _data.OrderByDescending(keySelector).ToList();
            }

            UpdateHeaderGlyphs();
            RowCount = 0;
            RowCount = _data.Count;
            VisibleDataChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private void ApplyFiltersAndSort()
        {
            IEnumerable<SiteCatalogEntry> filtered = _allData ?? Enumerable.Empty<SiteCatalogEntry>();

            foreach (var kv in _activeColumnFilters.ToList())
            {
                var allowedValues = kv.Value;
                int colIdx = kv.Key;
                filtered = filtered.Where(site => allowedValues.Contains(GetColumnFilterValue(site, colIdx) ?? string.Empty));
            }

            _data = filtered.ToList();
            SortData();
        }

        private void UpdateHeaderGlyphs()
        {
            foreach (DataGridViewColumn col in Columns)
            {
                col.HeaderCell.SortGlyphDirection = SortOrder.None;
                col.HeaderCell.Style.ForeColor = _activeColumnFilters.ContainsKey(col.Index)
                    ? AppTheme.AccentGold
                    : AppTheme.AccentCyan;
            }

            if (_sortColumnIndex >= 0 && _sortColumnIndex < Columns.Count)
                Columns[_sortColumnIndex].HeaderCell.SortGlyphDirection = _sortDirection;
        }

        private ToolStripDropDown _activeFilterDropDown;

        private void ShowHeaderFilterMenu(int columnIndex, Point screenPoint)
        {
            // Dispose previous menu to avoid "Cannot access a disposed object" crash
            if (_activeFilterDropDown != null)
            {
                try { _activeFilterDropDown.Close(); _activeFilterDropDown.Dispose(); } catch { }
                _activeFilterDropDown = null;
            }

            var allValues = GetDistinctColumnValues(columnIndex).ToList();
            HashSet<string> currentFilter = _activeColumnFilters.ContainsKey(columnIndex)
                ? _activeColumnFilters[columnIndex]
                : null;

            // Build a popup Form instead of ContextMenuStrip (avoids WinForms menu disposal issues)
            var popup = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                BackColor = AppTheme.BgCard,
                ForeColor = AppTheme.TextPrimary,
                Size = new Size(260, Math.Min(400, 120 + allValues.Count * 20)),
                Location = screenPoint,
                TopMost = true
            };

            int y = 8;

            // Sort A→Z button
            var btnSortAsc = new Button
            {
                Text = "\u25B2  Sort A to Z",
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.BgCard,
                ForeColor = AppTheme.TextPrimary,
                Font = new Font("Segoe UI", 8.5f),
                Size = new Size(240, 26),
                Location = new Point(6, y),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            btnSortAsc.FlatAppearance.BorderSize = 0;
            btnSortAsc.Click += (s, ev) =>
            {
                _sortColumnIndex = columnIndex;
                _sortDirection = SortOrder.Ascending;
                ApplyFiltersAndSort();
                popup.Close();
            };
            popup.Controls.Add(btnSortAsc);
            y += 28;

            // Sort Z→A button
            var btnSortDesc = new Button
            {
                Text = "\u25BC  Sort Z to A",
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.BgCard,
                ForeColor = AppTheme.TextPrimary,
                Font = new Font("Segoe UI", 8.5f),
                Size = new Size(240, 26),
                Location = new Point(6, y),
                TextAlign = ContentAlignment.MiddleLeft,
                Cursor = Cursors.Hand
            };
            btnSortDesc.FlatAppearance.BorderSize = 0;
            btnSortDesc.Click += (s, ev) =>
            {
                _sortColumnIndex = columnIndex;
                _sortDirection = SortOrder.Descending;
                ApplyFiltersAndSort();
                popup.Close();
            };
            popup.Controls.Add(btnSortDesc);
            y += 34;

            // Separator line
            var sep = new Panel { BackColor = AppTheme.Border, Size = new Size(240, 1), Location = new Point(6, y) };
            popup.Controls.Add(sep);
            y += 6;

            // Search textbox
            var txtSearch = new TextBox
            {
                Size = new Size(240, 22),
                Location = new Point(6, y),
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary,
                Font = new Font("Segoe UI", 8.5f),
                BorderStyle = BorderStyle.FixedSingle
            };
            txtSearch.PlaceholderText = "Search";
            popup.Controls.Add(txtSearch);
            y += 28;

            // CheckedListBox with values
            int listHeight = Math.Min(220, 20 + allValues.Count * 18);
            var clb = new CheckedListBox
            {
                Size = new Size(240, listHeight),
                Location = new Point(6, y),
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary,
                Font = new Font("Segoe UI", 8.5f),
                BorderStyle = BorderStyle.FixedSingle,
                CheckOnClick = true,
                IntegralHeight = false
            };

            // Add "(Select All)" + values
            clb.Items.Add("(Select All)");
            foreach (string val in allValues)
                clb.Items.Add(string.IsNullOrWhiteSpace(val) ? "(Blanks)" : val);

            // Set initial check states
            bool allSelected = currentFilter == null || currentFilter.Count == allValues.Count;
            clb.SetItemChecked(0, allSelected);
            for (int i = 0; i < allValues.Count; i++)
            {
                bool isChecked = currentFilter == null || currentFilter.Contains(allValues[i], StringComparer.OrdinalIgnoreCase);
                clb.SetItemChecked(i + 1, isChecked);
            }

            bool _updatingChecks = false;
            clb.ItemCheck += (s, ev) =>
            {
                if (_updatingChecks) return;
                _updatingChecks = true;
                try
                {
                    if (ev.Index == 0)
                    {
                        // "(Select All)" toggled — set all items to same state
                        bool newState = ev.NewValue == CheckState.Checked;
                        for (int i = 1; i < clb.Items.Count; i++)
                            clb.SetItemChecked(i, newState);
                    }
                    else
                    {
                        // Individual item toggled — update "(Select All)" state
                        int checkedCount = clb.CheckedIndices.Cast<int>().Count(idx => idx > 0);
                        if (ev.NewValue == CheckState.Checked) checkedCount++;
                        else checkedCount--;
                        clb.SetItemChecked(0, checkedCount == clb.Items.Count - 1);
                    }
                }
                finally { _updatingChecks = false; }
            };

            popup.Controls.Add(clb);
            y += listHeight + 8;

            // Search filtering
            txtSearch.TextChanged += (s, ev) =>
            {
                string filter = txtSearch.Text.Trim();
                _updatingChecks = true;
                clb.Items.Clear();
                clb.Items.Add("(Select All)");
                var filtered = string.IsNullOrEmpty(filter)
                    ? allValues
                    : allValues.Where(v => v.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                foreach (string val in filtered)
                    clb.Items.Add(string.IsNullOrWhiteSpace(val) ? "(Blanks)" : val);
                // Check all visible items
                for (int i = 0; i < clb.Items.Count; i++)
                    clb.SetItemChecked(i, true);
                _updatingChecks = false;
            };

            // OK / Cancel buttons
            var btnOk = new Button
            {
                Text = "OK",
                Size = new Size(70, 26),
                Location = new Point(100, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.AccentCyan,
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 8.5f)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            var btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(70, 26),
                Location = new Point(176, y),
                FlatStyle = FlatStyle.Flat,
                BackColor = AppTheme.BgCard,
                ForeColor = AppTheme.TextSecondary,
                Font = new Font("Segoe UI", 8.5f)
            };
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.FlatAppearance.BorderColor = AppTheme.Border;

            btnOk.Click += (s, ev) =>
            {
                // Collect checked values (skip index 0 which is "Select All")
                var selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < clb.Items.Count; i++)
                {
                    if (clb.GetItemChecked(i))
                    {
                        string display = clb.Items[i].ToString();
                        string actual = display == "(Blanks)" ? string.Empty : display;
                        // Map display back to original value
                        int idx = i - 1;
                        if (idx < allValues.Count)
                            selected.Add(allValues[idx]);
                        else
                            selected.Add(actual);
                    }
                }

                if (selected.Count == 0 || selected.Count == allValues.Count)
                {
                    // All or none selected → remove filter
                    _activeColumnFilters.Remove(columnIndex);
                }
                else
                {
                    _activeColumnFilters[columnIndex] = selected;
                }
                ApplyFiltersAndSort();
                popup.Close();
            };

            btnCancel.Click += (s, ev) => popup.Close();

            popup.Controls.Add(btnOk);
            popup.Controls.Add(btnCancel);

            // Adjust popup height
            popup.Height = y + 40;

            // Close on deactivate
            popup.Deactivate += (s, ev) => { try { popup.Close(); } catch { } };

            popup.Show();
            txtSearch.Focus();
        }

        private IEnumerable<string> GetDistinctColumnValues(int columnIndex)
        {
            return (_allData ?? new List<SiteCatalogEntry>())
                .Select(site => GetColumnFilterValue(site, columnIndex))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value)
                .Where(value => !string.IsNullOrWhiteSpace(value));
        }

        private string GetColumnFilterValue(SiteCatalogEntry site, int columnIndex)
        {
            switch (columnIndex)
            {
                case ColTitle: return site?.Title ?? string.Empty;
                case ColUrl: return site?.Url ?? string.Empty;
                case ColStorage: return site?.StorageDisplay ?? string.Empty;
                case ColVersions: return site?.VersionCount.ToString("N0") ?? string.Empty;
                case ColVersionSize: return site?.VersionSizeDisplay ?? string.Empty;
                case ColStatus: return site?.Status ?? string.Empty;
                case ColArchive: return site?.ArchiveStatus ?? string.Empty;
                case ColLock: return site?.LockState ?? string.Empty;
                case ColLastMod: return site?.LastModifiedDisplay ?? string.Empty;
                case ColCreated: return site?.CreatedDisplay ?? string.Empty;
                case ColOwner: return site?.Owner ?? string.Empty;
                default: return string.Empty;
            }
        }
    }
}
