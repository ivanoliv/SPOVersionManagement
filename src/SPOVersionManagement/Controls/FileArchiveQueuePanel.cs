using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class FileArchiveQueuePanel : UserControl
    {
        private ConfigurationService _config;
        private SiteDataService _siteData;
        private PowerShellHostService _psHost;

        private DataGridView _grid;
        private Label _lblSummary;
        private Label _lblUpdated;
        private TextBox _console;

        private FlatButton _btnRefresh;
        private FlatButton _btnRemove;
        private FlatButton _btnClear;
        private FlatButton _btnSelectAll;
        private FlatButton _btnArchiveSelected;
        private FlatButton _btnStart;

        private FileArchiveQueueData _queue = new FileArchiveQueueData();

        public FileArchiveQueuePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(24, 18, 24, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            _config = config;
            _psHost = psHost;
            _siteData = new SiteDataService(config);
            try
            {
                BuildLayout();
                LoadQueue();
            }
            catch (Exception ex)
            {
                Controls.Clear();
                var lbl = new Label
                {
                    Text = "File Archive Queue panel failed to load: " + ex.Message,
                    ForeColor = Color.OrangeRed,
                    Font = AppTheme.FontBody,
                    AutoSize = true,
                    Dock = DockStyle.Top,
                    Padding = new Padding(20)
                };
                Controls.Add(lbl);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        public void RefreshQueue()
        {
            LoadQueue();
        }

        private void BuildLayout()
        {
            Controls.Clear();

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            Controls.Add(root);

            var actionCard = new GlassPanel
            {
                Dock = DockStyle.Fill,
                AccentLeft = AppTheme.AccentGold,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(actionCard, 0, 0);

            var actionLeft = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(10, 11, 0, 11),
                Padding = Padding.Empty,
                AutoScroll = true
            };
            actionCard.Controls.Add(actionLeft);

            var actionRight = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                Width = 420,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = Padding.Empty,
                Padding = new Padding(0, 15, 10, 0)
            };
            actionCard.Controls.Add(actionRight);

            _btnRefresh = new FlatButton { Text = "Refresh", Size = new Size(92, 28), Margin = new Padding(0, 0, 8, 0) };
            _btnRefresh.SetGhostStyle();
            _btnRefresh.Click += (s, e) => LoadQueue();
            actionLeft.Controls.Add(_btnRefresh);

            _btnRemove = new FlatButton { Text = "Remove Selected", Size = new Size(130, 28), Margin = new Padding(0, 0, 8, 0) };
            _btnRemove.SetDangerStyle();
            _btnRemove.Click += (s, e) => RemoveSelected();
            actionLeft.Controls.Add(_btnRemove);

            _btnClear = new FlatButton { Text = "Clear Queue", Size = new Size(112, 28), Margin = new Padding(0, 0, 8, 0) };
            _btnClear.SetGhostStyle();
            _btnClear.Click += (s, e) => ClearQueue();
            actionLeft.Controls.Add(_btnClear);

            _btnSelectAll = new FlatButton { Text = "Select All", Size = new Size(98, 28), Margin = new Padding(0, 0, 8, 0) };
            _btnSelectAll.SetGhostStyle();
            _btnSelectAll.Click += (s, e) => _grid.SelectAll();
            actionLeft.Controls.Add(_btnSelectAll);

            _btnArchiveSelected = new FlatButton { Text = "Archive Selected", Size = new Size(132, 28), Margin = new Padding(0, 0, 8, 0) };
            _btnArchiveSelected.SetAccentColor(AppTheme.AccentGold);
            _btnArchiveSelected.Click += (s, e) => MarkSelectedForArchive();
            actionLeft.Controls.Add(_btnArchiveSelected);

            _btnStart = new FlatButton { Text = "Start", Size = new Size(80, 28), Margin = new Padding(0, 0, 8, 0) };
            _btnStart.SetAccentColor(AppTheme.AccentGreen);
            _btnStart.Click += async (s, e) => await StartArchiveForSelectedAsync();
            actionLeft.Controls.Add(_btnStart);

            _lblSummary = new Label
            {
                Text = "Queue: 0 item(s)",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 3, 16, 0)
            };
            actionRight.Controls.Add(_lblSummary);

            _lblUpdated = new Label
            {
                Text = "Last updated: -",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 5, 0, 0)
            };
            actionRight.Controls.Add(_lblUpdated);

            var topHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 10)
            };
            root.Controls.Add(topHost, 0, 1);

            topHost.Controls.Add(new Label
            {
                Text = "File Archive Queue",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 0)
            });

            topHost.Controls.Add(new Label
            {
                Text = "Review queued files selected from search results. Remove or clear items before running archive actions.",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                MaximumSize = new Size(Math.Max(500, ClientSize.Width - Padding.Horizontal - 16), 0),
                BackColor = Color.Transparent,
                Location = new Point(0, 30)
            });

            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6,
                Panel1MinSize = 220,
                Panel2MinSize = 110,
                BackColor = Color.Transparent,
                Margin = Padding.Empty
            };
            root.Controls.Add(split, 0, 2);

            split.SplitterDistance = split.Panel1MinSize;
            split.SplitterMoved += (s, e) => { };
            split.Resize += (s, e) =>
            {
                try
                {
                    int desired = Math.Max(split.Panel1MinSize, split.Height - 130);
                    if (desired >= split.Panel1MinSize && desired <= split.Height - split.Panel2MinSize)
                        split.SplitterDistance = desired;
                }
                catch { }
            };

            var gridCard = new GlassPanel
            {
                Dock = DockStyle.Fill,
                AccentLeft = AppTheme.AccentCyan,
                Padding = new Padding(8)
            };
            split.Panel1.Controls.Add(gridCard);

            _grid = new DataGridView
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
            AppTheme.StyleDataGrid(_grid);

            _grid.Columns.Add("SiteUrl", "SITE URL");
            _grid.Columns.Add("FileUrl", "FILE URL");
            _grid.Columns.Add("Category", "CATEGORY");
            _grid.Columns.Add("Ext", "EXT");
            _grid.Columns.Add("SizeMB", "SIZE MB");
            _grid.Columns.Add("QueuedAt", "QUEUED AT");
            _grid.Columns.Add("Status", "STATUS");

            _grid.Columns["SiteUrl"].Width = 260;
            _grid.Columns["FileUrl"].Width = 460;
            _grid.Columns["Category"].Width = 120;
            _grid.Columns["Ext"].Width = 70;
            _grid.Columns["SizeMB"].Width = 82;
            _grid.Columns["QueuedAt"].Width = 130;
            _grid.Columns["Status"].Width = 100;

            gridCard.Controls.Add(_grid);

            var consoleCard = new GlassPanel
            {
                Dock = DockStyle.Fill,
                AccentLeft = AppTheme.AccentPurple,
                Padding = new Padding(8)
            };
            split.Panel2.Controls.Add(consoleCard);

            var consoleHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 18,
                BackColor = Color.Transparent
            };
            consoleCard.Controls.Add(consoleHeader);

            consoleHeader.Controls.Add(new Label
            {
                Text = "ARCHIVE OUTPUT",
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = AppTheme.AccentPurple,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, 2)
            });

            _console = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = AppTheme.FontMono,
                WordWrap = false
            };
            consoleCard.Controls.Add(_console);
        }

        private void LoadQueue()
        {
            try
            {
                _queue = _siteData.LoadFileArchiveQueue() ?? new FileArchiveQueueData();
                if (_queue.Items == null)
                    _queue.Items = new System.Collections.Generic.List<FileArchiveQueueItem>();
            }
            catch
            {
                _queue = new FileArchiveQueueData();
            }
            BindQueue();
        }

        private void BindQueue()
        {
            if (_grid == null) return;
            _grid.Rows.Clear();

            if (_queue == null || _queue.Items == null || _queue.Items.Count == 0)
            {
                _lblSummary.Text = "Queue: 0 item(s)";
                _lblUpdated.Text = "Last updated: -";
                return;
            }

            foreach (var item in _queue.Items.OrderByDescending(i => i.QueuedAt ?? string.Empty))
            {
                string queuedAt = item.QueuedAt;
                if (DateTime.TryParse(item.QueuedAt, out DateTime dt))
                    queuedAt = dt.ToString("dd/MM/yyyy HH:mm");

                _grid.Rows.Add(
                    item.SiteUrl ?? string.Empty,
                    item.FileUrl ?? string.Empty,
                    item.Category ?? string.Empty,
                    item.FileExtension ?? string.Empty,
                    item.FileSizeMB.ToString("0.##"),
                    queuedAt ?? string.Empty,
                    string.IsNullOrWhiteSpace(item.Status) ? "Queued" : item.Status);
            }

            _lblSummary.Text = $"Queue: {_queue.Items.Count:N0} item(s)";
            if (DateTime.TryParse(_queue.LastUpdated, out DateTime updatedAt))
                _lblUpdated.Text = "Last updated: " + updatedAt.ToString("dd/MM/yyyy HH:mm");
            else
                _lblUpdated.Text = "Last updated: -";
        }

        private void RemoveSelected()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select at least one row to remove.", "File Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedFileUrls = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.Cells["FileUrl"].Value?.ToString() ?? string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (selectedFileUrls.Count == 0)
                return;

            _queue.Items = _queue.Items
                .Where(i => string.IsNullOrWhiteSpace(i.FileUrl) || !selectedFileUrls.Contains(i.FileUrl))
                .ToList();

            _siteData.SaveFileArchiveQueue(_queue);
            LoadQueue();
        }

        private void ClearQueue()
        {
            if (_queue.Items.Count == 0)
                return;

            if (MessageBox.Show("Clear all items from File Archive Queue?", "File Archive Queue", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _queue.Items.Clear();
            _siteData.SaveFileArchiveQueue(_queue);
            LoadQueue();
        }

        private void MarkSelectedForArchive()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                MessageBox.Show("Select one or more rows to mark for archive.", "File Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var selectedUrls = _grid.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.Cells["FileUrl"].Value?.ToString() ?? string.Empty)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int marked = 0;
            foreach (var item in _queue.Items)
            {
                if (string.IsNullOrWhiteSpace(item.FileUrl) || !selectedUrls.Contains(item.FileUrl))
                    continue;

                item.Status = "Selected";
                item.Error = null;
                marked++;
            }

            _siteData.SaveFileArchiveQueue(_queue);
            LoadQueue();
            AppendConsole($"Marked {marked} file(s) for archive.");
        }

        private async Task StartArchiveForSelectedAsync()
        {
            var selectedItems = _queue.Items
                .Where(i => string.Equals(i.Status, "Selected", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("No files marked as Selected. Use 'Archive Selected' first.", "File Archive Queue", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _btnStart.Enabled = false;
            _btnArchiveSelected.Enabled = false;
            AppendConsole($"Starting archive for {selectedItems.Count} file(s)...");

            Action<string> outHandler = msg => AppendConsole(msg);
            Action<string> warnHandler = msg => AppendConsole("[WARN] " + msg);
            Action<string> errHandler = msg => AppendConsole("[ERROR] " + msg);

            _psHost.OnOutput += outHandler;
            _psHost.OnWarning += warnHandler;
            _psHost.OnError += errHandler;

            try
            {
                foreach (var item in selectedItems)
                {
                    string fileUrl = (item.FileUrl ?? string.Empty).Replace("'", "''");
                    if (string.IsNullOrWhiteSpace(fileUrl))
                        continue;

                    try
                    {
                        string script =
                            "$cmd = Get-Command Set-PnPFileArchiveState -ErrorAction SilentlyContinue; " +
                            "if(-not $cmd){ throw 'Set-PnPFileArchiveState cmdlet not found. Install PnP.PowerShell nightly.' }; " +
                            "Set-PnPFileArchiveState -Url '" + fileUrl + "' -ArchiveState Archived -ErrorAction Stop; " +
                            "Write-Output ('Archived: ' + '" + fileUrl + "')";

                        await _psHost.RunScriptAsync(script);
                        item.Status = "Archived";
                        item.Error = null;
                    }
                    catch (Exception ex)
                    {
                        item.Status = "Failed";
                        item.Error = ex.Message;
                        AppendConsole("[ERROR] " + fileUrl + " :: " + ex.Message);
                    }
                }

                _siteData.SaveFileArchiveQueue(_queue);
                LoadQueue();
                AppendConsole("Archive run completed.");
            }
            finally
            {
                _psHost.OnOutput -= outHandler;
                _psHost.OnWarning -= warnHandler;
                _psHost.OnError -= errHandler;
                _btnStart.Enabled = true;
                _btnArchiveSelected.Enabled = true;
            }
        }

        private void AppendConsole(string text)
        {
            if (_console == null || _console.IsDisposed)
                return;

            if (InvokeRequired)
            {
                Invoke((Action)(() => AppendConsole(text)));
                return;
            }

            _console.AppendText((text ?? string.Empty) + Environment.NewLine);
            _console.SelectionStart = _console.TextLength;
            _console.ScrollToCaret();
        }
    }
}
