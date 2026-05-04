using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    /// <summary>
    /// Session Manager panel — browse past sessions, view details, and resume interrupted executions.
    /// </summary>
    public class SessionManagerPanel : UserControl
    {
        private ConfigurationService _config;
        private ExecutionHistoryService _history;
        private PowerShellHostService _psHost;

        private DataGridView _sessionsGrid;
        private Panel _detailPanel;
        private RichTextBox _txtDetails;
        private FlatButton _btnResume, _btnDelete, _btnRefresh, _btnViewLog;

        public event EventHandler<string> StatusMessage;
        public event EventHandler<SessionRecord> ResumeRequested;

        public SessionManagerPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(24, 20, 24, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, ExecutionHistoryService history, PowerShellHostService psHost)
        {
            _config = config;
            _history = history;
            _psHost = psHost;
            BuildLayout();
            LoadSessions();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        private void BuildLayout()
        {
            Controls.Clear();
            int fullW = Math.Max(700, ClientSize.Width - Padding.Horizontal - 16);

            // ═══ HEADER ═══
            var lblTitle = new Label
            {
                Text = "Session Manager",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 0)
            };
            Controls.Add(lblTitle);

            var lblSubtitle = new Label
            {
                Text = "Browse, inspect, and resume past execution sessions. State is auto-saved on interruption.",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(0, 28)
            };
            Controls.Add(lblSubtitle);

            // ═══ TOOLBAR ═══
            int toolY = 56;
            _btnRefresh = new FlatButton { Text = "\u21BB Refresh", Size = new Size(100, 32), Location = new Point(0, toolY) };
            _btnRefresh.SetGhostStyle();
            _btnRefresh.Click += (s, e) => LoadSessions();
            Controls.Add(_btnRefresh);

            _btnResume = new FlatButton { Text = "\u25B6 Resume", Size = new Size(110, 32), Location = new Point(108, toolY) };
            _btnResume.SetAccentColor(AppTheme.AccentGreen);
            _btnResume.Enabled = false;
            _btnResume.Click += BtnResume_Click;
            Controls.Add(_btnResume);

            _btnViewLog = new FlatButton { Text = "\u25A3 View Log", Size = new Size(110, 32), Location = new Point(226, toolY) };
            _btnViewLog.SetGhostStyle();
            _btnViewLog.Enabled = false;
            _btnViewLog.Click += BtnViewLog_Click;
            Controls.Add(_btnViewLog);

            _btnDelete = new FlatButton { Text = "\u2716 Delete", Size = new Size(90, 32), Location = new Point(344, toolY) };
            _btnDelete.SetAccentColor(AppTheme.AccentRed);
            _btnDelete.Enabled = false;
            _btnDelete.Click += BtnDelete_Click;
            Controls.Add(_btnDelete);

            // ═══ SESSIONS GRID ═══
            int gridY = toolY + 42;
            int gridH = 220;
            _sessionsGrid = new DataGridView
            {
                Location = new Point(0, gridY),
                Size = new Size(fullW, gridH),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical
            };
            AppTheme.StyleDataGrid(_sessionsGrid);
            _sessionsGrid.SelectionChanged += SessionsGrid_SelectionChanged;
            _sessionsGrid.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) BtnResume_Click(s, e); };

            _sessionsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 12 });
            _sessionsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "SessionId", HeaderText = "Session ID", FillWeight = 22 });
            _sessionsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Started", HeaderText = "Started", FillWeight = 18 });
            _sessionsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Updated", HeaderText = "Last Updated", FillWeight = 18 });
            _sessionsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Progress", HeaderText = "Progress", FillWeight = 16 });
            _sessionsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mode", HeaderText = "Mode", FillWeight = 14 });

            Controls.Add(_sessionsGrid);

            // ═══ DETAIL PANEL ═══
            int detailY = gridY + gridH + 16;
            _detailPanel = new GlassPanel
            {
                Location = new Point(0, detailY),
                Size = new Size(fullW, 280),
                AccentLeft = AppTheme.AccentCyan
            };
            Controls.Add(_detailPanel);

            _txtDetails = new RichTextBox
            {
                Location = new Point(10, 10),
                Size = new Size(fullW - 20, 260),
                ReadOnly = true,
                BackColor = Color.FromArgb(12, 20, 42),
                ForeColor = AppTheme.TextPrimary,
                Font = new Font("Consolas", 9.5f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Text = "Select a session to view details."
            };
            _detailPanel.Controls.Add(_txtDetails);
        }

        public void LoadSessions()
        {
            _sessionsGrid.Rows.Clear();
            var sessions = _history.LoadSessionHistory();
            if (sessions == null || sessions.Count == 0)
            {
                ClearDetail();
                return;
            }

            // Sort by StartedAt descending (most recent first)
            sessions = sessions.OrderByDescending(s => s.StartedAt).ToList();

            foreach (var s in sessions)
            {
                string status = s.Status ?? "Unknown";
                string progress = "";
                if (s.Progress != null)
                {
                    if (s.Progress.TotalSites > 0)
                        progress = $"{s.Progress.ProcessedSites}/{s.Progress.TotalSites}";
                    else
                        progress = "-";
                }

                string mode = "-";
                if (s.Configuration != null)
                {
                    if (s.Configuration.DeleteOnly) mode = "Delete Only";
                    else if (s.Configuration.SyncOnly) mode = "Sync Only";
                    else mode = "Full (Sync+Delete)";
                }

                string started = FormatDate(s.StartedAt);
                string updated = FormatDate(s.LastUpdated);

                int idx = _sessionsGrid.Rows.Add(status, s.SessionId, started, updated, progress, mode);

                // Color the status cell
                var statusCell = _sessionsGrid.Rows[idx].Cells["Status"];
                switch (status)
                {
                    case "Completed":
                        statusCell.Style.ForeColor = AppTheme.AccentGreen;
                        break;
                    case "Failed":
                        statusCell.Style.ForeColor = AppTheme.AccentRed;
                        break;
                    case "Interrupted":
                    case "InProgress":
                        statusCell.Style.ForeColor = AppTheme.AccentGold;
                        break;
                    case "Cancelled":
                        statusCell.Style.ForeColor = AppTheme.TextMuted;
                        break;
                    default:
                        statusCell.Style.ForeColor = AppTheme.TextSecondary;
                        break;
                }

                // Tag the row with the session record
                _sessionsGrid.Rows[idx].Tag = s;
            }

            if (_sessionsGrid.Rows.Count > 0)
                _sessionsGrid.Rows[0].Selected = true;
        }

        private void SessionsGrid_SelectionChanged(object sender, EventArgs e)
        {
            if (_sessionsGrid.SelectedRows.Count == 0)
            {
                ClearDetail();
                return;
            }

            var session = _sessionsGrid.SelectedRows[0].Tag as SessionRecord;
            if (session == null) { ClearDetail(); return; }

            ShowSessionDetail(session);

            bool canResume = session.Status == "Interrupted" || session.Status == "InProgress" || session.Status == "Failed";
            _btnResume.Enabled = canResume;
            _btnDelete.Enabled = true;
            _btnViewLog.Enabled = true;
        }

        private void ShowSessionDetail(SessionRecord session)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"SESSION ID:     {session.SessionId}");
            sb.AppendLine($"STATUS:         {session.Status}");
            sb.AppendLine($"ADMIN URL:      {session.AdminUrl ?? "-"}");
            sb.AppendLine($"STARTED:        {FormatDate(session.StartedAt)}");
            sb.AppendLine($"LAST UPDATED:   {FormatDate(session.LastUpdated)}");
            sb.AppendLine();

            if (session.Configuration != null)
            {
                var c = session.Configuration;
                string mode = c.DeleteOnly ? "Delete Only" : c.SyncOnly ? "Sync Only" : "Full (Sync + Delete)";
                sb.AppendLine("\u2500\u2500\u2500 CONFIGURATION \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                sb.AppendLine($"  Mode:                  {mode}");
                sb.AppendLine($"  Major Version Limit:   {c.MajorVersionLimit}");
                sb.AppendLine($"  Minor Version Limit:   {c.MajorWithMinorVersionsLimit}");
                sb.AppendLine($"  Max Concurrent Jobs:   {c.MaxConcurrentJobs}");
                sb.AppendLine($"  Check Batch Size:      {c.CheckBatchSize}");
                sb.AppendLine($"  Batch Delay (sec):     {c.CheckBatchDelaySeconds}");
                sb.AppendLine($"  Delete Before Days:    {(c.DeleteBeforeDays > 0 ? c.DeleteBeforeDays.ToString() : "(by count)")}");
                sb.AppendLine($"  Zero Version Action:   {c.ZeroVersionAction ?? "-"}");
                sb.AppendLine($"  Manage Retention:      {c.ManageRetention}");
                sb.AppendLine($"  Use File Cache:        {c.UseFileCache}");
                sb.AppendLine();
                sb.AppendLine("\u2500\u2500\u2500 INPUT FILES \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                sb.AppendLine($"  Include Sites CSV:     {(string.IsNullOrEmpty(c.InputSiteListCsv) ? "-" : c.InputSiteListCsv)}");
                sb.AppendLine($"  Exclude Sites CSV:     {(string.IsNullOrEmpty(c.InputExclusionSiteListCsv) ? "-" : c.InputExclusionSiteListCsv)}");
                sb.AppendLine($"  Graph Report CSV:      {(string.IsNullOrEmpty(c.GraphReportCsv) ? "-" : c.GraphReportCsv)}");
                sb.AppendLine($"  Sync Job List CSV:     {(string.IsNullOrEmpty(c.InputSiteSyncListCsv) ? "-" : c.InputSiteSyncListCsv)}");
                sb.AppendLine($"  SAM Report CSV:        {(string.IsNullOrEmpty(c.SamReportCsv) ? "-" : c.SamReportCsv)}");
                sb.AppendLine($"  Cache File Path:       {(string.IsNullOrEmpty(c.CacheFilePath) ? "-" : c.CacheFilePath)}");
            }
            else
            {
                sb.AppendLine("(No configuration recorded for this session)");
            }

            if (session.Progress != null)
            {
                var p = session.Progress;
                int pending = p.TotalSites - p.ProcessedSites;
                string pct = p.TotalSites > 0 ? $" ({p.ProcessedSites * 100 / p.TotalSites}%)" : "";
                sb.AppendLine();
                sb.AppendLine("\u2500\u2500\u2500 PROGRESS \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                sb.AppendLine($"  Total Sites:           {p.TotalSites}");
                sb.AppendLine($"  Processed:             {p.ProcessedSites}{pct}");
                sb.AppendLine($"  Pending:               {pending}");
                sb.AppendLine($"  Queued:                {p.QueuedSites}");
            }

            // Append pending queue info
            AppendQueueInfo(sb, session);

            _txtDetails.Text = sb.ToString();
        }

        private void AppendQueueInfo(System.Text.StringBuilder sb, SessionRecord session)
        {
            if (session.Status != "Interrupted" && session.Status != "InProgress")
                return;

            try
            {
                string jobPath = Path.Combine(_config.ConfigPath, "JobStatus.json");
                if (!File.Exists(jobPath)) return;

                var json = File.ReadAllText(jobPath);
                var obj = JObject.Parse(json);
                var queued = obj["QueuedSites"] as JArray;
                var active = obj["ActiveJobs"] as JArray;

                int qCount = queued?.Count ?? 0;
                int aCount = active?.Count ?? 0;

                sb.AppendLine();
                sb.AppendLine($"\u2500\u2500\u2500 PENDING QUEUE ({qCount} queued, {aCount} active) \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");

                if (queued != null)
                {
                    foreach (var site in queued.Take(10))
                    {
                        string url = site["Url"]?.ToString() ?? site["SiteUrl"]?.ToString() ?? "?";
                        string phase = site["Phase"]?.ToString() ?? "";
                        sb.AppendLine($"  \u2022 [{phase}] {url}");
                    }
                    if (qCount > 10) sb.AppendLine($"  ... and {qCount - 10} more");
                }
            }
            catch { }
        }

        private void ClearDetail()
        {
            _txtDetails.Text = "Select a session to view details.";
            _btnResume.Enabled = false;
            _btnDelete.Enabled = false;
            _btnViewLog.Enabled = false;
        }

        private void BtnResume_Click(object sender, EventArgs e)
        {
            if (_sessionsGrid.SelectedRows.Count == 0) return;
            var session = _sessionsGrid.SelectedRows[0].Tag as SessionRecord;
            if (session == null) return;

            if (session.Status != "Interrupted" && session.Status != "InProgress" && session.Status != "Failed")
            {
                MessageBox.Show("Only interrupted, in-progress, or failed sessions can be resumed.",
                    "Cannot Resume", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Resume session {session.SessionId}?\n\n" +
                $"This will reload all parameters and continue processing from where it stopped.\n" +
                $"Pending sites: {session.Progress?.TotalSites - session.Progress?.ProcessedSites ?? 0}",
                "Resume Session",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                ResumeRequested?.Invoke(this, session);
            }
        }

        private void BtnViewLog_Click(object sender, EventArgs e)
        {
            if (_sessionsGrid.SelectedRows.Count == 0) return;
            var session = _sessionsGrid.SelectedRows[0].Tag as SessionRecord;
            if (session == null) return;

            // Find the matching Execution_<sessionId>.csv
            string logsDir = _config.ResolvePath("Logs") ?? Path.Combine(_config.RootPath, "Logs");
            string pattern = $"Execution_{session.SessionId}*.csv";

            if (Directory.Exists(logsDir))
            {
                var files = Directory.GetFiles(logsDir, pattern);
                if (files.Length > 0)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = files[0],
                        UseShellExecute = true
                    });
                    return;
                }
            }

            MessageBox.Show($"No execution log found for session {session.SessionId}.\nLooked for: {pattern}",
                "Log Not Found", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_sessionsGrid.SelectedRows.Count == 0) return;
            var session = _sessionsGrid.SelectedRows[0].Tag as SessionRecord;
            if (session == null) return;

            var result = MessageBox.Show(
                $"Delete session record '{session.SessionId}'?\n\nThis removes the session from history. Execution logs are preserved.",
                "Delete Session",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                DeleteSession(session.SessionId);
                LoadSessions();
                StatusMessage?.Invoke(this, $"Session {session.SessionId} deleted.");
            }
        }

        private void DeleteSession(string sessionId)
        {
            try
            {
                string path = Path.Combine(_config.ConfigPath, "SessionHistory.json");
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var obj = JObject.Parse(json);
                var sessions = obj["Sessions"] as JArray;
                if (sessions == null) return;

                var toRemove = sessions.FirstOrDefault(s => s["SessionId"]?.ToString() == sessionId);
                if (toRemove != null)
                {
                    sessions.Remove(toRemove);
                    File.WriteAllText(path, obj.ToString(Formatting.Indented));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting session: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string FormatDate(string isoDate)
        {
            if (string.IsNullOrEmpty(isoDate)) return "-";
            if (DateTime.TryParse(isoDate, out DateTime dt))
                return dt.ToLocalTime().ToString("MMM dd, HH:mm");
            return isoDate;
        }

        public void RefreshData() => LoadSessions();
    }
}
