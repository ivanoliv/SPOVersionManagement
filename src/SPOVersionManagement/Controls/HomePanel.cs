using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class HomePanel : UserControl
    {
        private ConfigurationService _config;
        private ExecutionHistoryService _history;

        // Session Summary
        private Label _lblAuthStatus, _lblLastSync, _lblLastResult;
        // Storage stats
        private Label _lblStorageQuota, _lblStorageUsed, _lblStorageAvail, _lblTotalSites;
        private Label _lblPercentUsed, _lblExtraCost;
        // Stat cards
        private Label _lblFreedVal, _lblVersionsVal, _lblGlobalVal;
        // Recent executions
        private Panel _recentExecPanel;
        // Chart
        private Chart _chart;

        public event EventHandler StartClicked;
        public event EventHandler DashboardClicked;
        public event EventHandler ConfigClicked;
        public event EventHandler HistoryClicked;
        public event EventHandler BackupClicked;
        public event EventHandler SitesClicked;

        public HomePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(24, 20, 24, 20);
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
            Resize += (s, e) => { if (_config != null) { BuildLayout(); RefreshData(); } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        private void BuildLayout()
        {
            Controls.Clear();
            int m = 0; // margin handled by Padding
            int gap = 16;
            int y = m;

            // ═══ HEADER ═══
            var lblTitle = MakeLabel("Overview Dashboard", AppTheme.FontTitle, AppTheme.TextPrimary, m, y);
            Controls.Add(lblTitle);
            var lblSubtitle = MakeLabel("Tenant-wide version footprint and retention status.", AppTheme.FontBody, AppTheme.TextSecondary, m, y + 28);
            Controls.Add(lblSubtitle);
            y += 56;

            // ═══ ROW 1: Session Summary (2/3) + Quick Actions (1/3) ═══
            int fullW = Math.Max(700, ClientSize.Width - Padding.Horizontal - 16);
            int col2W = (fullW - gap) * 2 / 3;
            int col1W = fullW - col2W - gap;

            // Top-right actions
            var btnDashboard = new FlatButton
            {
                Text = "\u25A3  Open Dashboard",
                Size = new Size(170, 34),
                Location = new Point(Math.Max(m, fullW - 320), 6)
            };
            btnDashboard.SetAccentColor(AppTheme.AccentCyan);
            btnDashboard.Click += (s, e) => DashboardClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(btnDashboard);

            var btnBackup = new FlatButton
            {
                Text = "\u25A8  Backup Data",
                Size = new Size(140, 34),
                Location = new Point(Math.Max(m, fullW - 142), 6)
            };
            btnBackup.SetGhostStyle();
            btnBackup.Click += (s, e) => BackupClicked?.Invoke(this, EventArgs.Empty);
            Controls.Add(btnBackup);

            // Session Summary Card
            var sessionCard = new GlassPanel { Location = new Point(m, y), Size = new Size(col2W, 140), AccentLeft = AppTheme.AccentCyan };
            Controls.Add(sessionCard);
            AddCardTitle(sessionCard, "\u2630  Session Summary", 14, 12);

            // 3 sub-cards inside session
            int subW = (col2W - 48) / 3;
            int subY = 44;
            var pAuth = MakeSubCard(sessionCard, 14, subY, subW, 80);
            _lblAuthStatus = AddSubCardContent(pAuth, "AUTH STATUS", "\u2713 App-Only (Cert)", AppTheme.AccentGreen);

            var pSync = MakeSubCard(sessionCard, 14 + subW + 8, subY, subW, 80);
            _lblLastSync = AddSubCardContent(pSync, "LAST SYNC", "-", AppTheme.TextSecondary);

            var pResult = MakeSubCard(sessionCard, 14 + (subW + 8) * 2, subY, subW, 80);
            _lblLastResult = AddSubCardContent(pResult, "LAST RUN RESULT", "-", AppTheme.TextMuted);

            // Quick Actions Card
            var quickCard = new GlassPanel { Location = new Point(m + col2W + gap, y), Size = new Size(col1W, 140), AccentLeft = AppTheme.AccentPurple };
            Controls.Add(quickCard);
            AddCardTitle(quickCard, "\u26A1  Quick Actions", 14, 12);

            int btnW = (col1W - 40) / 2;
            int btnH = 40;
            AddQuickBtn(quickCard, 14, 44, btnW, btnH, "\u25B6 Execute", AppTheme.AccentCyan, (s, e) => StartClicked?.Invoke(this, EventArgs.Empty));
            AddQuickBtn(quickCard, 14 + btnW + 8, 44, btnW, btnH, "\u2699 Config", AppTheme.AccentGold, (s, e) => ConfigClicked?.Invoke(this, EventArgs.Empty));
            AddQuickBtn(quickCard, 14, 44 + btnH + 8, btnW, btnH, "\u2637 Sites", AppTheme.AccentPurple, (s, e) => SitesClicked?.Invoke(this, EventArgs.Empty));
            AddQuickBtn(quickCard, 14 + btnW + 8, 44 + btnH + 8, btnW, btnH, "\u29D6 History", AppTheme.TextSecondary, (s, e) => HistoryClicked?.Invoke(this, EventArgs.Empty));

            y += 140 + gap;

            // ═══ ROW 2: Tenant Storage (full width, 2×4 grid of stat sub-cards) ═══
            int halfW = (fullW - gap) / 2;
            int storageCardH = 160;
            var storageCard = new GlassPanel { Location = new Point(m, y), Size = new Size(fullW, storageCardH), AccentLeft = AppTheme.AccentCyan };
            Controls.Add(storageCard);
            AddCardTitle(storageCard, "\u26C1  Tenant Storage", 14, 12);

            int scW = (fullW - 56) / 4;
            int scH = 52;
            int scRow1 = 40, scRow2 = scRow1 + scH + 8;

            // Row 1: Quota | Used | Available | Sites
            var sc1 = MakeSubCard(storageCard, 14, scRow1, scW, scH);
            _lblStorageQuota = AddSubCardContent(sc1, "STORAGE QUOTA", "-", AppTheme.TextPrimary);
            var sc2 = MakeSubCard(storageCard, 14 + (scW + 8), scRow1, scW, scH);
            _lblStorageUsed = AddSubCardContent(sc2, "STORAGE USED", "-", AppTheme.AccentGold);
            var sc3 = MakeSubCard(storageCard, 14 + (scW + 8) * 2, scRow1, scW, scH);
            _lblStorageAvail = AddSubCardContent(sc3, "AVAILABLE / OVER", "-", AppTheme.AccentCyan);
            var sc4 = MakeSubCard(storageCard, 14 + (scW + 8) * 3, scRow1, scW, scH);
            _lblTotalSites = AddSubCardContent(sc4, "TOTAL SITES", "-", AppTheme.TextPrimary);

            // Row 2: % Used | Extra Cost | Freed (session) | Versions Deleted
            var sc5 = MakeSubCard(storageCard, 14, scRow2, scW, scH);
            _lblPercentUsed = AddSubCardContent(sc5, "% USED", "-", AppTheme.AccentRed);
            var sc6 = MakeSubCard(storageCard, 14 + (scW + 8), scRow2, scW, scH);
            _lblExtraCost = AddSubCardContent(sc6, "EXTRA COST / YEAR", "-", AppTheme.AccentRed);
            var sc7 = MakeSubCard(storageCard, 14 + (scW + 8) * 2, scRow2, scW, scH);
            _lblFreedVal = AddSubCardContent(sc7, "FREED (SESSION)", "0 GB", AppTheme.AccentGreen);
            var sc8 = MakeSubCard(storageCard, 14 + (scW + 8) * 3, scRow2, scW, scH);
            _lblVersionsVal = AddSubCardContent(sc8, "VERSIONS DELETED", "0", AppTheme.AccentGold);

            y += storageCardH + gap;

            // ═══ ROW 3: Version Chart (full width) ═══
            var chartCard = new GlassPanel { Location = new Point(m, y), Size = new Size(fullW, 200) };
            Controls.Add(chartCard);
            AddCardTitle(chartCard, "\u2261  Storage Trend (Monthly)", 14, 12);
            BuildChart(chartCard);

            y += 200 + gap;

            // ═══ ROW 3: Worldwide Impact + Recent Executions ═══
            // Worldwide
            var globalCard = new GlassPanel { Location = new Point(m, y), Size = new Size(halfW, 100) };
            Controls.Add(globalCard);
            AddCardTitle(globalCard, "\u2604  Worldwide Impact", 14, 12);
            _lblGlobalVal = MakeLabel("...", new Font("Segoe UI", 18f, FontStyle.Bold), AppTheme.AccentCyan, 14, 42);
            globalCard.Controls.Add(_lblGlobalVal);
            globalCard.Controls.Add(MakeLabel("Global Storage Freed (all users)", AppTheme.FontSmall, AppTheme.TextMuted, 14, 72));

            // Recent Executions
            var recentCard = new GlassPanel { Location = new Point(m + halfW + gap, y), Size = new Size(halfW, 100) };
            Controls.Add(recentCard);
            AddCardTitle(recentCard, "\u2263  Recent Executions", 14, 12);

            var btnSeeAll = new FlatButton { Text = "See All", Size = new Size(70, 24), Location = new Point(halfW - 84, 10) };
            btnSeeAll.SetGhostStyle();
            btnSeeAll.Click += (s, e) => HistoryClicked?.Invoke(this, EventArgs.Empty);
            recentCard.Controls.Add(btnSeeAll);

            _recentExecPanel = new Panel { Location = new Point(14, 36), Size = new Size(halfW - 28, 56), BackColor = Color.Transparent };
            recentCard.Controls.Add(_recentExecPanel);

            y += 100 + gap;
        }

        public void RefreshData()
        {
            if (_config == null || _history == null) return;
            try
            {
                var (versionsDeleted, storageGB, sites, sessionCount) = _history.GetSummaryStats();

                _lblFreedVal.Text = storageGB >= 1024 ? $"{storageGB / 1024.0:F2} TB" : $"{storageGB:F1} GB";
                _lblVersionsVal.Text = $"{versionsDeleted:N0}";

                var sessions = _history.LoadSessionHistory();
                if (sessions != null && sessions.Count > 0)
                {
                    var last = sessions[sessions.Count - 1];
                    string tenant = ExtractTenant(last.AdminUrl ?? "");

                    _lblLastSync.Text = FormatDate(last.StartedAt);
                    _lblLastResult.Text = last.Status ?? "-";
                    _lblLastResult.ForeColor = (last.Status == "Completed") ? AppTheme.AccentGreen :
                        (last.Status == "Failed") ? AppTheme.AccentRed : AppTheme.AccentGold;

                    // Populate recent executions
                    PopulateRecentExecutions(sessions);
                }

                // Load tenant storage
                LoadTenantStorage();
            }
            catch { }
        }

        private void LoadTenantStorage()
        {
            try
            {
                string path = Path.Combine(_config.ConfigPath, "TenantStorage.json");
                if (!File.Exists(path))
                {
                    ResetTenantStorageView();
                    return;
                }
                string json = File.ReadAllText(path);
                var obj = JObject.Parse(json);

                decimal usedTB = (decimal)(obj["StorageUsedTB"] ?? 0);
                decimal quotaTB = (decimal)(obj["TenantQuotaTB"] ?? 0);
                int siteCount = (int)(obj["SiteCount"] ?? 0);
                decimal percentUsed = (decimal)(obj["PercentUsed"] ?? 0);
                decimal extraCost = (decimal)(obj["ExtraCostPerYear"] ?? 0);
                decimal availGB = (decimal)(obj["StorageAvailableGB"] ?? 0);

                _lblStorageQuota.Text = $"{quotaTB:F2} TB";
                _lblStorageUsed.Text = $"{usedTB:F2} TB";
                _lblTotalSites.Text = $"{siteCount:N0}";

                bool over = availGB < 0;
                _lblStorageAvail.Text = over ? $"+{Math.Abs(availGB / 1024):F2} TB over" : $"{availGB / 1024:F2} TB";
                _lblStorageAvail.ForeColor = over ? AppTheme.AccentRed : AppTheme.AccentGreen;

                _lblPercentUsed.Text = $"{percentUsed:F1}%";
                _lblPercentUsed.ForeColor = percentUsed > 100 ? AppTheme.AccentRed :
                    percentUsed > 80 ? AppTheme.AccentGold : AppTheme.AccentGreen;

                _lblExtraCost.Text = extraCost > 0 ? $"${extraCost:N0}" : "-";
                _lblExtraCost.ForeColor = extraCost > 0 ? AppTheme.AccentRed : AppTheme.TextMuted;

                // Load chart with real monthly data
                LoadChartData(obj);
            }
            catch
            {
                ResetTenantStorageView();
            }
        }

        private void LoadChartData(JObject tenantObj)
        {
            try
            {
                var monthly = tenantObj.SelectToken("GraphData.MonthlyData") as JArray;
                if (monthly == null || monthly.Count == 0 || _chart == null) return;

                var series = _chart.Series[0];
                series.Points.Clear();
                foreach (var m in monthly)
                {
                    string month = (string)m["MonthName"] ?? "";
                    decimal tb = (decimal)(m["AvgStorageTB"] ?? 0);
                    series.Points.AddXY(month, (double)tb);
                }
            }
            catch { }
        }

        private void PopulateRecentExecutions(System.Collections.Generic.List<SessionRecord> sessions)
        {
            _recentExecPanel.Controls.Clear();
            int y = 0;
            int count = Math.Min(2, sessions.Count);
            for (int i = sessions.Count - 1; i >= sessions.Count - count && i >= 0; i--)
            {
                var s = sessions[i];
                Color statusColor = s.Status == "Completed" ? AppTheme.AccentGreen : s.Status == "Failed" ? AppTheme.AccentRed : AppTheme.AccentGold;
                string dot = s.Status == "Completed" ? "\u2713" : s.Status == "Failed" ? "\u2717" : "\u25CB";

                var lbl = new Label
                {
                    Text = $"{dot} {FormatDate(s.StartedAt)}  {s.Status}  {s.Progress?.ProcessedSites ?? 0}/{s.Progress?.TotalSites ?? 0} sites",
                    Font = AppTheme.FontSmall,
                    ForeColor = statusColor,
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Location = new Point(0, y)
                };
                _recentExecPanel.Controls.Add(lbl);
                y += 18;
            }
        }

        private void BuildChart(GlassPanel parent)
        {
            _chart = new Chart
            {
                Location = new Point(14, 36),
                Size = new Size(parent.Width - 28, 155),
                BackColor = Color.Transparent
            };

            var area = new ChartArea("main");
            area.BackColor = Color.Transparent;
            area.AxisX.LabelStyle.ForeColor = AppTheme.TextMuted;
            area.AxisX.LabelStyle.Font = new Font("Cascadia Code", 7f);
            area.AxisX.LineColor = AppTheme.Border;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisX.MajorTickMark.Enabled = false;
            area.AxisY.LabelStyle.ForeColor = AppTheme.TextMuted;
            area.AxisY.LabelStyle.Font = new Font("Cascadia Code", 7f);
            area.AxisY.LineColor = AppTheme.Border;
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(60, AppTheme.Border);
            area.AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dot;
            area.AxisY.Title = "TB";
            area.AxisY.TitleForeColor = AppTheme.TextMuted;
            area.AxisY.TitleFont = new Font("Cascadia Code", 7f);
            _chart.ChartAreas.Add(area);

            var series = new Series("Storage") { ChartType = SeriesChartType.SplineArea };
            series.Color = Color.FromArgb(60, AppTheme.AccentPurple);
            series.BorderColor = AppTheme.AccentPurple;
            series.BorderWidth = 2;
            // Sample data (will be replaced by real timeline data)
            series.Points.AddXY("Jan", 1.2);
            series.Points.AddXY("Feb", 1.5);
            series.Points.AddXY("Mar", 1.8);
            series.Points.AddXY("Apr", 1.6);
            series.Points.AddXY("May", 2.1);
            series.Points.AddXY("Jun", 2.4);
            _chart.Series.Add(series);

            var legend = new Legend { BackColor = Color.Transparent, ForeColor = AppTheme.TextMuted, Font = new Font("Segoe UI", 7f), Docking = Docking.Bottom };
            _chart.Legends.Add(legend);

            parent.Controls.Add(_chart);
        }

        public void SetGlobalStats(string formatted)
        {
            if (_lblGlobalVal == null || IsDisposed) return;
            Action a = () => _lblGlobalVal.Text = formatted;
            if (InvokeRequired) Invoke(a); else a();
        }

        private void ResetTenantStorageView()
        {
            _lblStorageQuota.Text = "-";
            _lblStorageUsed.Text = "-";
            _lblStorageAvail.Text = "-";
            _lblStorageAvail.ForeColor = AppTheme.TextMuted;
            _lblTotalSites.Text = "0";
            _lblPercentUsed.Text = "-";
            _lblPercentUsed.ForeColor = AppTheme.TextMuted;
            _lblExtraCost.Text = "-";
            _lblExtraCost.ForeColor = AppTheme.TextMuted;

            if (_chart != null && _chart.Series.Count > 0)
                _chart.Series[0].Points.Clear();
        }

        #region Helpers
        private void AddCardTitle(Control parent, string text, int x, int y)
        {
            parent.Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            });
        }

        private Panel MakeSubCard(Control parent, int x, int y, int w, int h)
        {
            var p = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.FromArgb(40, AppTheme.BgInput)
            };
            p.Paint += (s, e) =>
            {
                using (var pen = new Pen(AppTheme.Border))
                using (var path = AppTheme.CreateRoundedRect(new Rectangle(0, 0, p.Width - 1, p.Height - 1), 6))
                    e.Graphics.DrawPath(pen, path);
            };
            parent.Controls.Add(p);
            return p;
        }

        private Label AddSubCardContent(Panel card, string title, string value, Color valueColor)
        {
            card.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(10, 8)
            });
            var lbl = new Label
            {
                Text = value,
                Font = new Font("Segoe UI Semibold", 9.5f),
                ForeColor = valueColor,
                AutoSize = true,
                BackColor = Color.Transparent,
                MaximumSize = new Size(card.Width - 20, 0),
                Location = new Point(10, 28)
            };
            card.Controls.Add(lbl);
            return lbl;
        }

        private void AddQuickBtn(Control parent, int x, int y, int w, int h, string text, Color hoverColor, EventHandler click)
        {
            var btn = new Panel
            {
                Location = new Point(x, y),
                Size = new Size(w, h),
                BackColor = Color.FromArgb(30, AppTheme.BgInput),
                Cursor = Cursors.Hand
            };
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = AppTheme.TextSecondary,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            btn.Paint += (s, e) =>
            {
                using (var pen = new Pen(AppTheme.Border))
                using (var path = AppTheme.CreateRoundedRect(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), 6))
                    e.Graphics.DrawPath(pen, path);
            };
            btn.MouseEnter += (s, e) => { lbl.ForeColor = hoverColor; btn.BackColor = Color.FromArgb(50, AppTheme.BgCard); };
            btn.MouseLeave += (s, e) => { lbl.ForeColor = AppTheme.TextSecondary; btn.BackColor = Color.FromArgb(30, AppTheme.BgInput); };
            lbl.MouseEnter += (s, e) => { lbl.ForeColor = hoverColor; btn.BackColor = Color.FromArgb(50, AppTheme.BgCard); };
            lbl.MouseLeave += (s, e) => { lbl.ForeColor = AppTheme.TextSecondary; btn.BackColor = Color.FromArgb(30, AppTheme.BgInput); };
            btn.Click += click;
            lbl.Click += click;
            btn.Controls.Add(lbl);
            parent.Controls.Add(btn);
        }

        private Label MakeLabel(string text, Font font, Color color, int x, int y)
        {
            return new Label { Text = text, Font = font, ForeColor = color, AutoSize = true, BackColor = Color.Transparent, Location = new Point(x, y) };
        }

        private string ExtractTenant(string url) { try { return new Uri(url).Host.Split('.')[0].Replace("-admin", ""); } catch { return url; } }
        private string FormatDate(string s) { return DateTime.TryParse(s, out DateTime d) ? d.ToString("MMM dd, HH:mm") : s ?? "-"; }
        #endregion
    }
}
