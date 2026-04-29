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
    public class RetentionPolicyPanel : UserControl
    {
        private ConfigurationService _configService;
        private PowerShellHostService _psHost;
        private TabControl _tabControl;
        private TextBox _txtPolicySearch;
        private Label _lblTotalPolicies;
        private Label _lblTotalExceptions;
        private Label _lblSuspendedByUs;
        private Label _lblCapacityAvailable;
        private DataGridView _gridPolicies;
        private DataGridView _gridExceptionSites;
        private Button _btnManageRetention;
        private Label _lblStatus;
        private Timer _refreshTimer;
        private JObject _policyData;
        private bool _isDirty;

        public event EventHandler<string> StatusMessage;

        public RetentionPolicyPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            Padding = new Padding(28, 20, 28, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService configService, PowerShellHostService psHost)
        {
            _configService = configService;
            _psHost = psHost;
            BuildLayout();
            LoadPolicyData();
            SetupAutoRefresh();
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void BuildLayout()
        {
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = true
            };

            int y = 12;
            int contentWidth = 1200;

            // ═════ Title & Refresh ═════
            var pnlHeader = new Panel
            {
                Location = new Point(0, y),
                Width = contentWidth,
                Height = 45,
                BackColor = Color.Transparent
            };

            var lblTitle = new Label
            {
                Text = "🛡️ Retention Policy Management",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 8)
            };
            pnlHeader.Controls.Add(lblTitle);

            var btnRefresh = new Button
            {
                Text = "🔄 Refresh",
                Font = AppTheme.FontButton,
                BackColor = Color.FromArgb(0, 212, 255, 30),
                ForeColor = AppTheme.AccentCyan,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(contentWidth - 110, 8),
                Width = 110,
                Height = 32
            };
            btnRefresh.Click += (s, e) => { LoadPolicyData(); StatusMessage?.Invoke(this, "Retention policy data refreshed"); };
            pnlHeader.Controls.Add(btnRefresh);

            scrollPanel.Controls.Add(pnlHeader);
            y += 50;

            // ═════ Summary Cards ═════
            var pnlSummary = new Panel
            {
                Location = new Point(0, y),
                Width = contentWidth,
                Height = 110,
                BackColor = Color.Transparent
            };

            int cardWidth = 240;
            int cardGap = 15;

            // Total Policies
            CreateSummaryCard(pnlSummary, 0, "📋 Total Policies", out _lblTotalPolicies, "--", AppTheme.AccentGold, cardWidth);

            // Total Exceptions
            CreateSummaryCard(pnlSummary, cardWidth + cardGap, "⚠️ Current Exceptions", out _lblTotalExceptions, "--", AppTheme.AccentRed, cardWidth);

            // Suspended By Us
            CreateSummaryCard(pnlSummary, (cardWidth + cardGap) * 2, "🔄 Suspended By Us", out _lblSuspendedByUs, "--", AppTheme.AccentCyan, cardWidth);

            // Capacity Available
            CreateSummaryCard(pnlSummary, (cardWidth + cardGap) * 3, "✅ Capacity Available", out _lblCapacityAvailable, "--", AppTheme.AccentGreen, cardWidth);

            scrollPanel.Controls.Add(pnlSummary);
            y += 120;

            // ═════ Search & Filter ═════
            _txtPolicySearch = new TextBox
            {
                Text = "",
                Font = AppTheme.FontBody,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = AppTheme.TextPrimary,
                Location = new Point(0, y),
                Width = contentWidth - 120,
                Height = 32
            };
            _txtPolicySearch.TextChanged += (s, e) => FilterPolicies();
            scrollPanel.Controls.Add(_txtPolicySearch);

            var btnClearSearch = new Button
            {
                Text = "Clear",
                Font = AppTheme.FontButton,
                BackColor = Color.FromArgb(80, 80, 80),
                ForeColor = AppTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(contentWidth - 110, y),
                Width = 110,
                Height = 32
            };
            btnClearSearch.Click += (s, e) => { _txtPolicySearch.Clear(); FilterPolicies(); };
            scrollPanel.Controls.Add(btnClearSearch);
            y += 45;

            // ═════ Tab Control ═════
            _tabControl = new TabControl
            {
                Location = new Point(0, y),
                Width = contentWidth,
                Height = 500,
                BackColor = AppTheme.BgCard,
                ForeColor = AppTheme.TextPrimary
            };

            // Tab 1: Policies
            var tabPolicies = new TabPage("Policies")
            {
                BackColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary
            };
            _gridPolicies = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                ScrollBars = ScrollBars.Both
            };
            _gridPolicies.CellDoubleClick += (s, e) => ShowPolicyDetails(e.RowIndex);
            SetupPoliciesGrid();
            tabPolicies.Controls.Add(_gridPolicies);
            _tabControl.TabPages.Add(tabPolicies);

            // Tab 2: Exception Sites
            var tabExceptions = new TabPage("Exception Sites")
            {
                BackColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary
            };
            _gridExceptionSites = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = AppTheme.BgDark,
                ForeColor = AppTheme.TextPrimary,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ScrollBars = ScrollBars.Both
            };
            SetupExceptionSitesGrid();
            tabExceptions.Controls.Add(_gridExceptionSites);
            _tabControl.TabPages.Add(tabExceptions);
            AppTheme.StyleTabControl(_tabControl);

            scrollPanel.Controls.Add(_tabControl);
            y += 520;

            // ═════ Action Buttons ═════
            var pnlActions = new Panel
            {
                Location = new Point(0, y),
                Width = contentWidth,
                Height = 50,
                BackColor = Color.Transparent
            };

            _btnManageRetention = new Button
            {
                Text = "▶ Manage Retention Policies",
                Font = AppTheme.FontButton,
                BackColor = AppTheme.AccentGold,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(0, 0),
                Width = 200,
                Height = 40
            };
            _btnManageRetention.Click += BtnManageRetention_Click;
            pnlActions.Controls.Add(_btnManageRetention);

            _lblStatus = new Label
            {
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                Location = new Point(220, 10),
                MaximumSize = new Size(800, 30)
            };
            pnlActions.Controls.Add(_lblStatus);

            scrollPanel.Controls.Add(pnlActions);

            Controls.Add(scrollPanel);
        }

        private void CreateSummaryCard(Panel parent, int x, string label, out Label valueLabel, string initialValue, Color accentColor, int width)
        {
            var pnl = new Panel
            {
                Location = new Point(x, 0),
                Width = width,
                Height = 100,
                BackColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None
            };

            var lblLabel = new Label
            {
                Text = label,
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Location = new Point(12, 8),
                Width = width - 24,
                Height = 20
            };
            pnl.Controls.Add(lblLabel);

            valueLabel = new Label
            {
                Text = initialValue,
                Font = new Font(AppTheme.FontTitle.FontFamily, 28, FontStyle.Bold),
                ForeColor = accentColor,
                AutoSize = false,
                Location = new Point(12, 30),
                Width = width - 24,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft
            };
            pnl.Controls.Add(valueLabel);

            parent.Controls.Add(pnl);
        }

        private void SetupPoliciesGrid()
        {
            AppTheme.StyleDataGrid(_gridPolicies);
            _gridPolicies.Columns.Clear();
            _gridPolicies.Columns.Add("Name", "Policy Name");
            _gridPolicies.Columns.Add("Mode", "Mode");
            _gridPolicies.Columns.Add("Status", "Status");
            _gridPolicies.Columns.Add("InclusionType", "Inclusion Type");
            _gridPolicies.Columns.Add("IncludedCount", "Included Sites");
            _gridPolicies.Columns.Add("Exceptions", "Exceptions");
            _gridPolicies.Columns.Add("Capacity", "Exception Capacity");
            _gridPolicies.Columns.Add("CreatedDate", "Created");
            _gridPolicies.Columns.Add("ModifiedDate", "Modified");

            foreach (DataGridViewColumn col in _gridPolicies.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            _gridPolicies.Columns["Name"].Width = 180;
            _gridPolicies.Columns["Mode"].Width = 60;
            _gridPolicies.Columns["Status"].Width = 60;
            _gridPolicies.Columns["InclusionType"].Width = 100;
            _gridPolicies.Columns["Capacity"].Width = 140;
        }

        private void SetupExceptionSitesGrid()
        {
            AppTheme.StyleDataGrid(_gridExceptionSites);
            _gridExceptionSites.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            _gridExceptionSites.Columns.Clear();
            _gridExceptionSites.Columns.Add("Policy", "Policy Name");
            _gridExceptionSites.Columns.Add("Source", "Source");
            _gridExceptionSites.Columns.Add("SuspendedAt", "Suspended At");
            _gridExceptionSites.Columns.Add("SiteUrl", "Site URL");

            foreach (DataGridViewColumn col in _gridExceptionSites.Columns)
            {
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            _gridExceptionSites.Columns["Policy"].MinimumWidth = 180;
            _gridExceptionSites.Columns["Source"].MinimumWidth = 90;
            _gridExceptionSites.Columns["SuspendedAt"].MinimumWidth = 130;
            _gridExceptionSites.Columns["SiteUrl"].MinimumWidth = 420;
        }

        private void LoadPolicyData()
        {
            try
            {
                string dbPath = Path.Combine(_configService.ConfigPath, "RetentionPolicyDatabase.json");
                if (!File.Exists(dbPath))
                {
                    _lblStatus.Text = "No retention policy data available";
                    _lblTotalPolicies.Text = "0";
                    _lblTotalExceptions.Text = "0";
                    _lblSuspendedByUs.Text = "0";
                    _lblCapacityAvailable.Text = "0";
                    return;
                }

                string json = File.ReadAllText(dbPath);
                _policyData = JObject.Parse(json);

                var policies = _policyData["Policies"] as JArray ?? new JArray();
                
                int totalExceptions = 0;
                int suspendedByUs = 0;
                int minCapacity = int.MaxValue;

                foreach (var policy in policies)
                {
                    var excluded = policy["ExcludedSites"] as JArray ?? new JArray();
                    totalExceptions += excluded.Count;
                    suspendedByUs += excluded.Count(s => (bool?)s["SuspendedByUs"] == true);
                    
                    int remaining = policy["ExceptionCapacityRemaining"]?.Value<int>() ?? 0;
                    if (remaining < minCapacity) minCapacity = remaining;
                }

                _lblTotalPolicies.Text = policies.Count.ToString();
                _lblTotalExceptions.Text = totalExceptions.ToString();
                _lblSuspendedByUs.Text = suspendedByUs.ToString();
                _lblCapacityAvailable.Text = minCapacity == int.MaxValue ? "0" : minCapacity.ToString();

                RenderPoliciesGrid();
                RenderExceptionSitesGrid();

                _lblStatus.Text = $"Loaded {policies.Count} policies at {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Error: {ex.Message}";
                _lblStatus.ForeColor = AppTheme.AccentRed;
            }
        }

        private void RenderPoliciesGrid()
        {
            if (_policyData == null) return;

            _gridPolicies.Rows.Clear();
            var policies = _policyData["Policies"] as JArray ?? new JArray();

            foreach (var policy in policies)
            {
                string name = policy["Name"]?.Value<string>() ?? "";
                string mode = policy["Mode"]?.Value<string>() ?? "";
                bool enabled = policy["Enabled"]?.Value<bool>() ?? false;
                string status = enabled ? "✓ Enabled" : "✗ Disabled";
                string inclusionType = policy["InclusionType"]?.Value<string>() ?? "";
                int includedCount = policy["IncludedSiteCount"]?.Value<int>() ?? 0;
                int exceptions = policy["ExcludedSiteCount"]?.Value<int>() ?? 0;
                int exceptionLimit = policy["ExceptionLimit"]?.Value<int>() ?? 0;
                int remaining = policy["ExceptionCapacityRemaining"]?.Value<int>() ?? 0;
                string created = policy["CreatedDate"]?.Value<DateTime>().ToShortDateString() ?? "";
                string modified = policy["ModifiedDate"]?.Value<DateTime>().ToShortDateString() ?? "";

                string capacityText = $"{exceptions} / {exceptionLimit} ({remaining} remaining)";

                _gridPolicies.Rows.Add(name, mode, status, inclusionType, includedCount, exceptions, capacityText, created, modified);
            }
        }

        private void RenderExceptionSitesGrid()
        {
            if (_policyData == null) return;

            _gridExceptionSites.Rows.Clear();
            var policies = _policyData["Policies"] as JArray ?? new JArray();

            foreach (var policy in policies)
            {
                string policyName = policy["Name"]?.Value<string>() ?? "";
                var excluded = policy["ExcludedSites"] as JArray ?? new JArray();

                foreach (var site in excluded)
                {
                    string siteUrl = site["SiteUrl"]?.Value<string>() ?? "";
                    bool suspendedByUs = site["SuspendedByUs"]?.Value<bool>() ?? false;
                    string source = suspendedByUs ? "SPO VM" : "External";
                    var suspendedAt = site["SuspendedAt"]?.Value<DateTime?>();
                    string suspendedAtStr = suspendedAt?.ToShortDateString() ?? "--";

                    _gridExceptionSites.Rows.Add(policyName, source, suspendedAtStr, siteUrl);
                }
            }

            _gridExceptionSites.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.AllCells);
        }

        private void FilterPolicies()
        {
            string query = _txtPolicySearch.Text.ToLower().Trim();

            foreach (DataGridViewRow row in _gridPolicies.Rows)
            {
                bool matches = string.IsNullOrEmpty(query) ||
                    row.Cells["Name"].Value?.ToString().ToLower().Contains(query) == true ||
                    row.Cells["Mode"].Value?.ToString().ToLower().Contains(query) == true ||
                    row.Cells["InclusionType"].Value?.ToString().ToLower().Contains(query) == true;

                row.Visible = matches;
            }
        }

        private void ShowPolicyDetails(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= _gridPolicies.Rows.Count) return;

            var policyName = _gridPolicies.Rows[rowIndex].Cells["Name"].Value?.ToString();
            if (policyName == null) return;

            var policy = _policyData["Policies"]?.FirstOrDefault(p => p["Name"]?.Value<string>() == policyName);
            if (policy == null) return;

            string details = $"Policy: {policyName}\n\n";
            details += $"Mode: {policy["Mode"]?.Value<string>()}\n";
            details += $"Enabled: {policy["Enabled"]?.Value<bool>()}\n";
            details += $"Created: {policy["CreatedDate"]?.Value<DateTime>()}\n";
            details += $"Modified: {policy["ModifiedDate"]?.Value<DateTime>()}\n";
            details += $"Inclusion Type: {policy["InclusionType"]?.Value<string>()}\n";
            details += $"Included Sites: {policy["IncludedSiteCount"]?.Value<int>()}\n";
            details += $"Exception Sites: {policy["ExcludedSiteCount"]?.Value<int>()} / {policy["ExceptionLimit"]?.Value<int>()}\n";
            details += $"GUID: {policy["Guid"]?.Value<string>()}";

            MessageBox.Show(details, "Policy Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnManageRetention_Click(object sender, EventArgs e)
        {
            try
            {
                if (_psHost == null)
                {
                    MessageBox.Show("PowerShell host not available", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _lblStatus.Text = "Launching retention policy orchestration...";
                _lblStatus.ForeColor = AppTheme.AccentGold;

                var adminUrl = _configService?.AppConfig?.AdminUrl;
                if (string.IsNullOrEmpty(adminUrl))
                {
                    MessageBox.Show("AdminUrl not configured. Please configure it in the Config panel.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Launch PowerShell orchestration with retention policy management
                _ = _psHost.StartVersionManagementAsync(
                    adminUrl: adminUrl,
                    majorVersionLimit: 10,
                    majorWithMinorVersionsLimit: 100,
                    maxConcurrentJobs: 5,
                    syncOnly: false,
                    deleteOnly: false,
                    manageRetention: true,
                    useFileCache: true,
                    skipGraphConnection: true,
                    openDashboard: false,
                    resetDatabase: false,
                    deleteBeforeDays: 0);

                _lblStatus.Text = "Retention policy orchestration started";
                _lblStatus.ForeColor = AppTheme.AccentGreen;
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Error: {ex.Message}";
                _lblStatus.ForeColor = AppTheme.AccentRed;
                StatusMessage?.Invoke(this, $"Retention policy management error: {ex.Message}");
            }
        }

        private void SetupAutoRefresh()
        {
            _refreshTimer = new Timer();
            _refreshTimer.Interval = 5000; // Refresh every 5 seconds
            _refreshTimer.Tick += (s, e) => LoadPolicyData();
            _refreshTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
