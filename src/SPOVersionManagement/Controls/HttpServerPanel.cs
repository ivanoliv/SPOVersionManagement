using System;
using System.Drawing;
using System.Windows.Forms;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class HttpServerPanel : UserControl
    {
        private ConfigurationService _configService;
        private DashboardHttpServerService _dashboardServer;

        // UI Controls
        private Label _lblServerStatus;
        private Label _lblStatusValue;
        private Button _btnStart;
        private Button _btnStop;
        private Button _btnRestart;
        private TextBox _txtPort;
        private ComboBox _cmbLaunchMode;
        private TextBox _txtLogs;
        private TextBox _txtSource;
        private Label _lblServerUrl;
        private Timer _statusTimer;
        private CheckBox _chkAutoScroll;
        private OpenFileDialog _fileDialog;

        private bool _logsAutoScroll = true;

        public HttpServerPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = false;
            Padding = new Padding(28, 20, 28, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService configService, DashboardHttpServerService dashboardServer)
        {
            _configService = configService;
            _dashboardServer = dashboardServer;
            BuildLayout();
            LoadValues();
            SetupServerStatusMonitoring();
        }

        protected override void OnPaint(PaintEventArgs e) => AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);

        private void BuildLayout()
        {
            var scrollPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                AutoScroll = true,
                Padding = new Padding(0, 0, 20, 0)
            };

            int y = 12;
            int contentWidth = 1000;

            // ═════ Title ═════
            var lblTitle = new Label
            {
                Text = "HTTP Server Configuration",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, y)
            };
            scrollPanel.Controls.Add(lblTitle);
            y += 50;

            // ═════ Status Section ═════
            var pnlStatus = new Panel
            {
                BackColor = Color.FromArgb(40, 40, 40),
                BorderStyle = BorderStyle.None,
                Location = new Point(0, y),
                Width = contentWidth,
                Height = 100
            };

            _lblServerStatus = new Label
            {
                Text = "Server Status:",
                Font = AppTheme.FontHeading,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = false,
                Location = new Point(20, 16),
                Width = 150,
                Height = 28
            };
            pnlStatus.Controls.Add(_lblServerStatus);

            _lblStatusValue = new Label
            {
                Text = "● Stopped",
                Font = AppTheme.FontHeading,
                ForeColor = AppTheme.AccentGold,
                AutoSize = false,
                Location = new Point(180, 16),
                Width = 200,
                Height = 28
            };
            pnlStatus.Controls.Add(_lblStatusValue);

            _lblServerUrl = new Label
            {
                Text = "URL: Not running",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextMuted,
                AutoSize = false,
                Location = new Point(20, 52),
                Width = contentWidth - 40,
                Height = 28
            };
            pnlStatus.Controls.Add(_lblServerUrl);

            scrollPanel.Controls.Add(pnlStatus);
            y += 115;

            // ═════ Settings Section ═════
            var lblSettings = new Label
            {
                Text = "Settings",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, y)
            };
            scrollPanel.Controls.Add(lblSettings);
            y += 40;

            // Port
            var lblPort = new Label
            {
                Text = "Port:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, y),
                Width = 140,
                Height = 28
            };
            scrollPanel.Controls.Add(lblPort);

            _txtPort = new TextBox
            {
                Text = "8080",
                Font = AppTheme.FontBody,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = AppTheme.TextPrimary,
                Location = new Point(150, y),
                Width = 150,
                Height = 32
            };
            _txtPort.TextChanged += (s, e) => MarkDirty();
            scrollPanel.Controls.Add(_txtPort);
            y += 45;

            // Launch Mode
            var lblMode = new Label
            {
                Text = "Launch Mode:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, y),
                Width = 140,
                Height = 28
            };
            scrollPanel.Controls.Add(lblMode);

            _cmbLaunchMode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = AppTheme.FontBody,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = AppTheme.TextPrimary,
                Items = { "App (Recommended)", "PowerShell" },
                Location = new Point(150, y),
                Width = 240,
                Height = 32
            };
            _cmbLaunchMode.SelectedIndexChanged += (s, e) => MarkDirty();
            scrollPanel.Controls.Add(_cmbLaunchMode);
            y += 45;

            // Dashboard Source
            var lblSource = new Label
            {
                Text = "Dashboard Source:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = false,
                Location = new Point(0, y),
                Width = 140,
                Height = 28
            };
            scrollPanel.Controls.Add(lblSource);

            _txtSource = new TextBox
            {
                Text = _configService?.AppConfig?.Files?.Dashboard ?? "Dashboard.html",
                Font = AppTheme.FontBody,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = AppTheme.TextMuted,
                ReadOnly = true,
                Location = new Point(150, y),
                Width = 520,
                Height = 32
            };
            scrollPanel.Controls.Add(_txtSource);

            var btnBrowse = new Button
            {
                Text = "Browse...",
                Font = AppTheme.FontButton,
                BackColor = AppTheme.AccentCyan,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(680, y),
                Width = 80,
                Height = 32
            };
            btnBrowse.Click += BtnBrowse_Click;
            scrollPanel.Controls.Add(btnBrowse);

            // Initialize OpenFileDialog
            _fileDialog = new OpenFileDialog
            {
                Title = "Select Dashboard HTML File",
                Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*",
                CheckFileExists = true,
                CheckPathExists = true,
                InitialDirectory = _configService?.RootPath ?? System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "C:\\"
            };

            y += 55;

            // ═════ Control Buttons ═════
            var lblControls = new Label
            {
                Text = "Server Controls",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, y)
            };
            scrollPanel.Controls.Add(lblControls);
            y += 40;

            int btnWidth = 130;
            int btnHeight = 40;
            int btnGap = 18;

            _btnStart = new Button
            {
                Text = "Start",
                Font = AppTheme.FontButton,
                BackColor = AppTheme.AccentGreen,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(0, y),
                Width = btnWidth,
                Height = btnHeight
            };
            _btnStart.Click += BtnStart_Click;
            scrollPanel.Controls.Add(_btnStart);

            _btnStop = new Button
            {
                Text = "Stop",
                Font = AppTheme.FontButton,
                BackColor = AppTheme.AccentRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(btnWidth + btnGap, y),
                Width = btnWidth,
                Height = btnHeight
            };
            _btnStop.Click += BtnStop_Click;
            _btnStop.Enabled = false;
            scrollPanel.Controls.Add(_btnStop);

            _btnRestart = new Button
            {
                Text = "Restart",
                Font = AppTheme.FontButton,
                BackColor = AppTheme.AccentGold,
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point((btnWidth + btnGap) * 2, y),
                Width = btnWidth,
                Height = btnHeight
            };
            _btnRestart.Click += BtnRestart_Click;
            _btnRestart.Enabled = false;
            scrollPanel.Controls.Add(_btnRestart);

            y += 60;

            // ═════ Logs Section ═════
            var lblLogs = new Label
            {
                Text = "Server Logs",
                Font = AppTheme.FontSubtitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, y)
            };
            scrollPanel.Controls.Add(lblLogs);
            y += 40;

            _chkAutoScroll = new CheckBox
            {
                Text = "Auto-scroll logs",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                Location = new Point(0, y),
                Checked = true
            };
            _chkAutoScroll.CheckedChanged += (s, e) => _logsAutoScroll = _chkAutoScroll.Checked;
            scrollPanel.Controls.Add(_chkAutoScroll);
            y += 35;

            _txtLogs = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                Font = AppTheme.FontMono,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(200, 200, 200),
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(0, y),
                Width = contentWidth,
                Height = 250,
                Text = "Server logs will appear here..."
            };
            scrollPanel.Controls.Add(_txtLogs);

            Controls.Add(scrollPanel);
        }

        private void LoadValues()
        {
            try
            {
                int port = _configService?.DashboardConfig?.DashboardPort ?? 8080;
                _txtPort.Text = port > 0 ? port.ToString() : "8080";

                string launchMode = (_configService?.DashboardConfig?.DashboardLaunchMode ?? "app").Trim().ToLowerInvariant();
                _cmbLaunchMode.SelectedIndex = launchMode == "powershell" ? 1 : 0;
            }
            catch { }
        }

        private void SetupServerStatusMonitoring()
        {
            if (_dashboardServer != null)
            {
                _dashboardServer.LogOutput += DashboardServer_LogOutput;
            }

            _statusTimer = new Timer();
            _statusTimer.Interval = 500; // Update status every 500ms
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateServerStatus();
        }

        private void UpdateServerStatus()
        {
            if (_dashboardServer == null) return;

            bool isRunning = _dashboardServer.IsRunning;
            if (isRunning)
            {
                _lblStatusValue.Text = "● Running";
                _lblStatusValue.ForeColor = AppTheme.AccentGreen;
                int port = 0;
                if (int.TryParse(_txtPort.Text, out port) && port > 0)
                {
                    string dashFile = _configService?.AppConfig?.Files?.Dashboard ?? "Dashboard.html";
                    _lblServerUrl.Text = $"URL: http://localhost:{port}/{dashFile.Replace('\\', '/')}";
                }
                _btnStart.Enabled = false;
                _btnStop.Enabled = true;
                _btnRestart.Enabled = true;
            }
            else
            {
                _lblStatusValue.Text = "● Stopped";
                _lblStatusValue.ForeColor = AppTheme.AccentGold;
                _lblServerUrl.Text = "URL: Not running";
                _btnStart.Enabled = true;
                _btnStop.Enabled = false;
                _btnRestart.Enabled = false;
            }
        }

        private void BtnStart_Click(object sender, EventArgs e)
        {
            try
            {
                if (!int.TryParse(_txtPort.Text, out int port) || port <= 0)
                {
                    MessageBox.Show("Invalid port number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Save configuration
                _configService.DashboardConfig.DashboardPort = port;
                _configService.DashboardConfig.DashboardLaunchMode = _cmbLaunchMode.SelectedIndex == 0 ? "app" : "powershell";
                _configService.SaveDashboardConfig();

                if (_dashboardServer != null)
                {
                    string dashFileName = _configService.AppConfig?.Files?.Dashboard ?? "Dashboard.html";
                    string dashboardPath = System.IO.Path.Combine(_configService.LogsPath, dashFileName);
                    string rootDir = System.IO.Path.GetDirectoryName(dashboardPath) ?? _configService.LogsPath;

                    _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] Starting server on port {port}...\r\n");
                    _dashboardServer.Start(port, rootDir);
                    _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] Server started successfully!\r\n");
                }
            }
            catch (Exception ex)
            {
                _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\r\n");
                MessageBox.Show($"Failed to start server:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStop_Click(object sender, EventArgs e)
        {
            try
            {
                if (_dashboardServer != null)
                {
                    _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] Stopping server...\r\n");
                    _dashboardServer.Stop();
                    _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] Server stopped.\r\n");
                }
            }
            catch (Exception ex)
            {
                _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\r\n");
            }
        }

        private void BtnRestart_Click(object sender, EventArgs e)
        {
            BtnStop_Click(null, null);
            System.Threading.Thread.Sleep(500);
            BtnStart_Click(null, null);
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                if (_fileDialog.ShowDialog() == DialogResult.OK)
                {
                    _txtSource.Text = _fileDialog.FileName;
                    // Update configuration with the selected file
                    _configService.AppConfig.Files.Dashboard = _fileDialog.FileName;
                    _configService.SaveAppConfig();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting file:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DashboardServer_LogOutput(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(DashboardServer_LogOutput), message);
                return;
            }

            _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
            if (_logsAutoScroll)
            {
                _txtLogs.SelectionStart = _txtLogs.TextLength;
                _txtLogs.ScrollToCaret();
            }
        }

        private void MarkDirty()
        {
            // Configuration changes detected - could save automatically if desired
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _statusTimer?.Stop();
                _statusTimer?.Dispose();
                if (_dashboardServer != null)
                {
                    _dashboardServer.LogOutput -= DashboardServer_LogOutput;
                }
            }
            base.Dispose(disposing);
        }
    }
}
