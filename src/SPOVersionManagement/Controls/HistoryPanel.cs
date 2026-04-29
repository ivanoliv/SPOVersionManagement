using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class HistoryPanel : UserControl
    {
        private ConfigurationService _config;
        private ExecutionHistoryService _history;
        private DataGridView _grid;
        private TextBox _txtSearch;
        private ComboBox _cmbStatus, _cmbJobType;
        private DateTimePicker _dtFrom, _dtTo;
        private Label _lblSummary;
        private List<ExecutionRecord> _allRecords;
        private JobStatusData _jobStatus;

        public HistoryPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(28, 20, 28, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, ExecutionHistoryService history)
        {
            _config = config;
            _history = history;
            BuildLayout();
            RefreshData();
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void BuildLayout()
        {
            Controls.Clear();
            int y = 0;

            // Header
            Controls.Add(ML("Execution History", AppTheme.FontTitle, AppTheme.TextPrimary, 0, y));
            Controls.Add(ML("Browse and filter past execution records.", AppTheme.FontBody, AppTheme.TextSecondary, 0, y + 28));
            y += 56;

            // ═══ FILTER TOOLBAR ═══
            var filterBar = new Panel { Location = new Point(0, y), Size = new Size(900, 68), BackColor = Color.Transparent, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            Controls.Add(filterBar);

            // Row 1: search + status + job type
            filterBar.Controls.Add(new Label { Text = "Search:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 4) });
            _txtSearch = new TextBox { Location = new Point(50, 2), Size = new Size(180, 22) };
            AppTheme.StyleTextBox(_txtSearch);
            _txtSearch.TextChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_txtSearch);

            filterBar.Controls.Add(new Label { Text = "Status:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(244, 4) });
            _cmbStatus = new ComboBox { Location = new Point(290, 2), Size = new Size(110, 22), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbStatus.Items.AddRange(new object[] { "(All)", "Completed", "Failed", "InProgress", "Queued" });
            _cmbStatus.SelectedIndex = 0;
            AppTheme.StyleComboBox(_cmbStatus);
            _cmbStatus.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_cmbStatus);

            filterBar.Controls.Add(new Label { Text = "Type:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(414, 4) });
            _cmbJobType = new ComboBox { Location = new Point(450, 2), Size = new Size(130, 22), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbJobType.Items.AddRange(new object[] { "(All)", "SetVersionPolicy", "DeleteVersions", "StorageScan", "RetentionSync" });
            _cmbJobType.SelectedIndex = 0;
            AppTheme.StyleComboBox(_cmbJobType);
            _cmbJobType.SelectedIndexChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_cmbJobType);

            // Row 2: date range + export
            filterBar.Controls.Add(new Label { Text = "From:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, 36) });
            _dtFrom = new DateTimePicker { Location = new Point(40, 34), Size = new Size(130, 22), Format = DateTimePickerFormat.Short, Value = DateTime.Now.AddMonths(-3) };
            _dtFrom.ValueChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_dtFrom);

            filterBar.Controls.Add(new Label { Text = "To:", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(180, 36) });
            _dtTo = new DateTimePicker { Location = new Point(204, 34), Size = new Size(130, 22), Format = DateTimePickerFormat.Short };
            _dtTo.ValueChanged += (s, e) => ApplyFilters();
            filterBar.Controls.Add(_dtTo);

            var btnExportCsv = new FlatButton { Text = "Export CSV", Size = new Size(100, 26), Location = new Point(700, 34) };
            btnExportCsv.SetGhostStyle();
            btnExportCsv.Click += BtnExportCsv_Click;
            filterBar.Controls.Add(btnExportCsv);

            var btnRefresh = new FlatButton { Text = "\u21BB Refresh", Size = new Size(90, 26), Location = new Point(808, 34) };
            btnRefresh.SetAccentColor(AppTheme.AccentCyan);
            btnRefresh.Click += (s, e) => RefreshData();
            filterBar.Controls.Add(btnRefresh);

            y += 74;

            // ═══ SUMMARY ROW ═══
            _lblSummary = new Label { Text = "", Font = AppTheme.FontSmall, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(0, y) };
            Controls.Add(_lblSummary);
            y += 20;

            // ═══ DATA GRID ═══
            _grid = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(900, 370),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                ScrollBars = ScrollBars.Both
            };
            AppTheme.StyleDataGrid(_grid);
            _grid.CellFormatting += Grid_CellFormatting;
            _grid.CellDoubleClick += Grid_CellDoubleClick;
            _grid.CellClick += Grid_CellClick;
            _grid.DataBindingComplete += (s, e) =>
            {
                foreach (DataGridViewColumn col in _grid.Columns)
                    if (col.Name != "Detail")
                        col.SortMode = DataGridViewColumnSortMode.Automatic;
            };

            // Add a detail button column as the first column
            var btnCol = new DataGridViewButtonColumn
            {
                Name = "Detail",
                HeaderText = "",
                Text = "\uD83D\uDD0D",
                UseColumnTextForButtonValue = true,
                Width = 36,
                MinimumWidth = 36,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                FlatStyle = FlatStyle.Flat
            };
            _grid.Columns.Add(btnCol);

            Controls.Add(_grid);
        }

        public void RefreshData()
        {
            _allRecords = _history.LoadExecutionHistory();
            _jobStatus = _history.LoadJobStatus();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allRecords == null) return;

            IEnumerable<ExecutionRecord> filtered = _allRecords;

            // Text search
            string search = _txtSearch.Text.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(search))
                filtered = filtered.Where(r => (r.SiteUrl?.ToLowerInvariant() ?? "").Contains(search) || (r.ErrorMessage?.ToLowerInvariant() ?? "").Contains(search));

            // Status filter
            string status = _cmbStatus.SelectedItem?.ToString();
            if (status != null && status != "(All)")
                filtered = filtered.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase));

            // Job type filter
            string jobType = _cmbJobType.SelectedItem?.ToString();
            if (jobType != null && jobType != "(All)")
                filtered = filtered.Where(r => string.Equals(r.JobType, jobType, StringComparison.OrdinalIgnoreCase));

            // Date range
            filtered = filtered.Where(r =>
            {
                if (!DateTime.TryParse(r.Timestamp, out DateTime ts)) return true;
                return ts >= _dtFrom.Value.Date && ts <= _dtTo.Value.Date.AddDays(1);
            });

            var list = filtered.ToList();
            _grid.DataSource = _history.ToDataTable(list);

            // Update summary
            long versionsDeleted = list.Sum(r => r.VersionsDeleted);
            double storageGB = list.Sum(r => r.StorageReleasedInBytes) / (1024.0 * 1024 * 1024);
            int sites = list.Select(r => r.SiteUrl?.ToLowerInvariant()).Distinct().Count();
            _lblSummary.Text = $"Showing {list.Count} records  |  {sites} sites  |  {versionsDeleted:N0} versions deleted  |  {storageGB:F2} GB freed";
        }

        private void Grid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_grid.Columns[e.ColumnIndex].Name == "Status" && e.Value != null)
            {
                string val = e.Value.ToString();
                if (val == "Completed") e.CellStyle.ForeColor = AppTheme.AccentGreen;
                else if (val == "Failed") e.CellStyle.ForeColor = AppTheme.AccentRed;
                else if (val == "InProgress") e.CellStyle.ForeColor = AppTheme.AccentCyan;
                else e.CellStyle.ForeColor = AppTheme.AccentGold;
            }
        }

        private void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            if (_grid.Columns[e.ColumnIndex].Name != "Detail") return;
            OpenDetailForRow(e.RowIndex);
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || _allRecords == null) return;
            OpenDetailForRow(e.RowIndex);
        }

        private void OpenDetailForRow(int rowIndex)
        {
            if (rowIndex < 0 || _allRecords == null) return;

            // Match by URL column in the grid row
            string url = _grid.Rows[rowIndex].Cells["URL"]?.Value?.ToString();
            string ts = _grid.Rows[rowIndex].Cells["Timestamp"]?.Value?.ToString();
            ExecutionRecord rec = null;
            foreach (var r in _allRecords)
            {
                if (r.SiteUrl == url && r.Timestamp == ts) { rec = r; break; }
            }
            if (rec == null) return;

            ShowDetailDialog(rec);
        }

        private void ShowDetailDialog(ExecutionRecord rec)
        {
            var dlg = new Form
            {
                Text = "Execution Detail",
                Size = new Size(640, 640),
                StartPosition = FormStartPosition.CenterParent,
                BackColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = AppTheme.BgDark, Padding = new Padding(20) };
            dlg.Controls.Add(panel);

            int lblW = 180;
            int valX = lblW + 16;
            int valW = 380;
            int y = 8;
            string resolvedWorkItemId = ResolveSpoWorkItemId(rec);
            y = AddDetailRow(panel, "Site URL", rec.SiteUrl, y, true, lblW, valX, valW);
            y = AddDetailRow(panel, "Site Name", rec.SiteName, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Timestamp", rec.Timestamp, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Job Type", rec.JobType, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Work Item ID", resolvedWorkItemId, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Status", rec.Status, y, false, lblW, valX, valW);
            y += 8;

            // Separator
            panel.Controls.Add(new Panel { Location = new Point(16, y), Size = new Size(540, 1), BackColor = AppTheme.Border });
            y += 10;

            y = AddDetailRow(panel, "Request Time", rec.RequestTimeUTC, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Complete Time", rec.CompleteTimeUTC, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Duration", $"{rec.DurationMinutes:F1} minutes", y, false, lblW, valX, valW);
            y += 8;

            panel.Controls.Add(new Panel { Location = new Point(16, y), Size = new Size(540, 1), BackColor = AppTheme.Border });
            y += 10;

            y = AddDetailRow(panel, "Lists Processed", rec.ListsProcessed.ToString("N0"), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Lists Synced", rec.ListsSynced.ToString("N0"), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Lists Sync Failed", rec.ListSyncFailed.ToString("N0"), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Files Processed", rec.FilesProcessed.ToString("N0"), y, false, lblW, valX, valW);
            y += 8;

            panel.Controls.Add(new Panel { Location = new Point(16, y), Size = new Size(540, 1), BackColor = AppTheme.Border });
            y += 10;

            y = AddDetailRow(panel, "Versions Processed", rec.VersionsProcessed.ToString("N0"), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Versions Deleted", rec.VersionsDeleted.ToString("N0"), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Versions Failed", rec.VersionsFailed.ToString("N0"), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Storage Released", rec.StorageReleasedFormatted, y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Storage Released (bytes)", rec.StorageReleasedInBytes.ToString("N0"), y, false, lblW, valX, valW);
            y += 8;

            panel.Controls.Add(new Panel { Location = new Point(16, y), Size = new Size(540, 1), BackColor = AppTheme.Border });
            y += 10;

            y = AddDetailRow(panel, "Initial Storage", FormatBytes(rec.InitialStorageUsedBytes), y, false, lblW, valX, valW);
            y = AddDetailRow(panel, "Final Storage", FormatBytes(rec.FinalStorageUsedBytes), y, false, lblW, valX, valW);

            if (!string.IsNullOrEmpty(rec.ErrorMessage))
            {
                y += 8;
                panel.Controls.Add(new Panel { Location = new Point(16, y), Size = new Size(540, 1), BackColor = AppTheme.AccentRed });
                y += 10;
                panel.Controls.Add(new Label { Text = "Error:", Font = AppTheme.FontSmall, ForeColor = AppTheme.AccentRed, AutoSize = true, Location = new Point(16, y) });
                y += 18;
                panel.Controls.Add(new Label { Text = rec.ErrorMessage, Font = AppTheme.FontMono, ForeColor = AppTheme.AccentRed, AutoSize = false, MaximumSize = new Size(540, 0), Size = new Size(540, 60), Location = new Point(16, y) });
                y += 60;
            }

            // Bottom spacer so content doesn't touch the window edge
            y += 24;
            panel.Controls.Add(new Panel { Location = new Point(0, y), Size = new Size(10, 1), BackColor = Color.Transparent });

            dlg.ShowDialog(ParentForm);
        }

        private int AddDetailRow(Panel parent, string label, string value, int y, bool highlight, int lblW, int valX, int valW)
        {
            parent.Controls.Add(new Label
            {
                Text = label + ":",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Size = new Size(lblW, 18),
                TextAlign = ContentAlignment.MiddleRight,
                Location = new Point(8, y)
            });
            var valLabel = new Label
            {
                Text = value ?? "-",
                Font = highlight ? new Font("Segoe UI Semibold", 9f) : AppTheme.FontBody,
                ForeColor = highlight ? AppTheme.AccentCyan : AppTheme.TextPrimary,
                AutoSize = true,
                MaximumSize = new Size(valW, 0),
                Location = new Point(valX, y)
            };
            parent.Controls.Add(valLabel);
            int rowH = Math.Max(20, valLabel.PreferredHeight + 4);
            return y + rowH;
        }

        private string FormatBytes(long bytes)
        {
            if (bytes <= 0) return "-";
            double gb = bytes / (1024.0 * 1024 * 1024);
            if (gb >= 1) return $"{gb:F2} GB";
            double mb = bytes / (1024.0 * 1024);
            return $"{mb:F1} MB";
        }

        private string ResolveSpoWorkItemId(ExecutionRecord rec)
        {
            if (rec == null)
                return "-";

            string original = rec.WorkItemId;
            if (!string.IsNullOrWhiteSpace(original) && IsSpoWorkItemId(original))
                return original;

            if (_jobStatus == null)
                return string.IsNullOrWhiteSpace(original) ? "-" : original;

            DateTime recTs;
            bool hasRecTs = DateTime.TryParse(rec.CompleteTimeUTC, out recTs) || DateTime.TryParse(rec.Timestamp, out recTs);

            var candidates = new List<(string WorkItemId, DateTime? Time)>();

            if (_jobStatus.CompletedJobs != null)
            {
                foreach (var j in _jobStatus.CompletedJobs)
                {
                    if (!string.Equals(j.SiteUrl ?? string.Empty, rec.SiteUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!JobTypeMatches(rec.JobType, j.JobType))
                        continue;

                    if (string.IsNullOrWhiteSpace(j.WorkItemId) || !IsSpoWorkItemId(j.WorkItemId))
                        continue;

                    DateTime jt;
                    candidates.Add((j.WorkItemId, DateTime.TryParse(j.CompletedAt, out jt) ? jt : (DateTime?)null));
                }
            }

            if (_jobStatus.ActiveJobs != null)
            {
                foreach (var j in _jobStatus.ActiveJobs)
                {
                    if (!string.Equals(j.SiteUrl ?? string.Empty, rec.SiteUrl ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!JobTypeMatches(rec.JobType, j.JobType))
                        continue;

                    if (string.IsNullOrWhiteSpace(j.WorkItemId) || !IsSpoWorkItemId(j.WorkItemId))
                        continue;

                    DateTime jt;
                    candidates.Add((j.WorkItemId, DateTime.TryParse(j.StartedAt, out jt) ? jt : (DateTime?)null));
                }
            }

            if (candidates.Count == 0)
                return string.IsNullOrWhiteSpace(original) ? "-" : original;

            if (!hasRecTs)
                return candidates[0].WorkItemId;

            return candidates
                .OrderBy(c => c.Time.HasValue ? Math.Abs((c.Time.Value - recTs).TotalSeconds) : double.MaxValue)
                .First().WorkItemId;
        }

        private static bool IsSpoWorkItemId(string value)
        {
            Guid parsed;
            return Guid.TryParse((value ?? string.Empty).Trim(), out parsed);
        }

        private static bool JobTypeMatches(string recordType, string statusType)
        {
            string a = (recordType ?? string.Empty).Trim().ToLowerInvariant();
            string b = (statusType ?? string.Empty).Trim().ToLowerInvariant();

            if (a == b)
                return true;

            if (a.Contains("sync") && b.Contains("sync"))
                return true;
            if (a.Contains("delete") && b.Contains("delete"))
                return true;
            if (a.Contains("archive") && b.Contains("archive"))
                return true;

            return false;
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"ExecutionHistory_{DateTime.Now:yyyyMMdd}.csv" })
            {
                if (dlg.ShowDialog(ParentForm) != DialogResult.OK) return;
                try
                {
                    var dt = _grid.DataSource as DataTable;
                    if (dt == null) return;
                    var lines = new List<string>();
                    lines.Add(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => c.ColumnName)));
                    foreach (DataRow row in dt.Rows)
                        lines.Add(string.Join(",", row.ItemArray.Select(v => $"\"{v}\"")));
                    File.WriteAllLines(dlg.FileName, lines);
                    MessageBox.Show($"Exported {dt.Rows.Count} rows.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex) { MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private Label ML(string text, Font font, Color color, int x, int y)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) };
        }
    }
}
