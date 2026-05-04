using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    /// <summary>
    /// Global connection bar that appears at the top of MainForm.
    /// Handles Connect-SPOService and shares connection state via PowerShellHostService.
    /// </summary>
    public class ConnectionBar : UserControl
    {
        private ConfigurationService _config;
        private PowerShellHostService _psHost;

        // Main row controls
        private Label _lblAdminBadge;
        private Panel _authTogglePanel;
        private FlatButton _btnInteractive, _btnAppCred;
        private FlatButton _btnConnect, _btnDisconnect;

        // App credentials panel (slides down)
        private TextBox _txtTenantId, _txtClientId, _txtCertThumb;
        private Panel _appCredPanel;

        // State
        private bool _isAppCredMode;
        private bool _isConnected;
        private string _statusText = "Disconnected";
        private Color _statusColor;
        private Color _statusDotColor;

        private const int BarHeight = 44;
        private const int CredPanelHeight = 38;

        public ConnectionBar()
        {
            Dock = DockStyle.Top;
            Height = BarHeight;
            BackColor = Color.FromArgb(14, 18, 26);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            _statusColor = AppTheme.TextMuted;
            _statusDotColor = AppTheme.TextMuted;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            _config = config;
            _psHost = psHost;
            BuildLayout();
            LoadEntraIdDefaults();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background
            using (var brush = new SolidBrush(Color.FromArgb(14, 18, 26)))
                g.FillRectangle(brush, ClientRectangle);

            // Subtle gradient overlay at bottom
            var gradRect = new Rectangle(0, Height - 6, Width, 6);
            using (var brush = new LinearGradientBrush(gradRect, Color.FromArgb(0, 0, 0, 0), Color.FromArgb(40, 0, 0, 0), 90f))
                g.FillRectangle(brush, gradRect);

            // Bottom accent line (thin glow)
            using (var pen = new Pen(Color.FromArgb(50, AppTheme.AccentCyan), 1f))
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);

            // ── Status pill (custom painted) ──
            PaintStatusPill(g);
        }

        private void PaintStatusPill(Graphics g)
        {
            var font = new Font("Segoe UI", 9f, FontStyle.Bold);
            var textSize = TextRenderer.MeasureText(_statusText, font);
            int pillW = textSize.Width + 32;
            int pillH = 24;
            int pillX = 14;
            int pillY = (BarHeight - pillH) / 2;

            var pillRect = new Rectangle(pillX, pillY, pillW, pillH);
            using (var path = AppTheme.CreateRoundedRect(pillRect, 12))
            {
                // Pill background
                using (var brush = new SolidBrush(Color.FromArgb(25, _statusDotColor)))
                    g.FillPath(brush, path);
                // Pill border
                using (var pen = new Pen(Color.FromArgb(60, _statusDotColor), 1f))
                    g.DrawPath(pen, path);
            }

            // Glowing dot
            int dotX = pillX + 10;
            int dotY = pillY + (pillH - 8) / 2;
            using (var dotBrush = new SolidBrush(_statusDotColor))
                g.FillEllipse(dotBrush, dotX, dotY, 8, 8);
            // Dot glow
            using (var glowBrush = new SolidBrush(Color.FromArgb(50, _statusDotColor)))
                g.FillEllipse(glowBrush, dotX - 2, dotY - 2, 12, 12);

            // Text
            TextRenderer.DrawText(g, _statusText, font, new Point(dotX + 14, pillY + 3), _statusColor);

            // Reposition admin badge after pill
            if (_lblAdminBadge != null)
            {
                int newBadgeX = pillX + pillW + 12;
                if (_lblAdminBadge.Left != newBadgeX)
                    _lblAdminBadge.Left = newBadgeX;
            }

            font.Dispose();
        }

        private void BuildLayout()
        {
            SuspendLayout();

            int btnY = (BarHeight - 28) / 2;
            string adminUrl = _config.AppConfig.AdminUrl ?? "";
            bool hasUrl = !string.IsNullOrWhiteSpace(adminUrl);

            // ── Admin URL badge (left, after status pill — positioned dynamically in PaintStatusPill) ──
            _lblAdminBadge = new Label
            {
                Text = hasUrl ? "\uD83C\uDF10  Tenant Configured" : "\u26A0  No Admin URL",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = hasUrl ? AppTheme.AccentGreen : AppTheme.AccentGold,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(200, (BarHeight - 16) / 2),
                Cursor = Cursors.Hand
            };
            if (hasUrl)
            {
                var tip = new ToolTip { InitialDelay = 200, ShowAlways = true };
                tip.SetToolTip(_lblAdminBadge, adminUrl);
            }
            Controls.Add(_lblAdminBadge);

            // ═══ RIGHT-ALIGNED GROUP: Auth toggle + Connect ═══

            // Connect / Disconnect (rightmost)
            _btnConnect = new FlatButton
            {
                Text = "\u25B6  Connect",
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold)
            };
            _btnConnect.Location = new Point(Width - 124, (BarHeight - 30) / 2);
            _btnConnect.SetAccentColor(AppTheme.AccentGreen);
            _btnConnect.Click += BtnConnect_Click;
            Controls.Add(_btnConnect);

            _btnDisconnect = new FlatButton
            {
                Text = "\u23F9  Disconnect",
                Size = new Size(110, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Visible = false,
                Font = new Font("Segoe UI", 9f)
            };
            _btnDisconnect.Location = new Point(Width - 124, (BarHeight - 30) / 2);
            _btnDisconnect.SetGhostStyle();
            _btnDisconnect.Click += BtnDisconnect_Click;
            Controls.Add(_btnDisconnect);

            // Separator before connect
            var sep2 = new Panel
            {
                Size = new Size(1, 24),
                BackColor = Color.FromArgb(40, 50, 70),
                Location = new Point(Width - 140, (BarHeight - 24) / 2),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Controls.Add(sep2);

            // ── Auth mode segmented toggle ──
            int segW = 224;
            int segH = 30;
            int segX = Width - 250 - segW + 212;
            int segY = (BarHeight - segH) / 2;
            _authTogglePanel = new Panel
            {
                Size = new Size(segW, segH),
                Location = new Point(Width - segW - 148, segY),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(10, 14, 22)
            };
            _authTogglePanel.Paint += (s, pe) =>
            {
                var rect = new Rectangle(0, 0, _authTogglePanel.Width - 1, _authTogglePanel.Height - 1);
                using (var path = AppTheme.CreateRoundedRect(rect, 6))
                {
                    using (var brush = new SolidBrush(Color.FromArgb(20, 28, 42)))
                        pe.Graphics.FillPath(brush, path);
                    using (var pen = new Pen(Color.FromArgb(45, 55, 80), 1f))
                        pe.Graphics.DrawPath(pen, path);
                }
            };
            Controls.Add(_authTogglePanel);

            // Interactive button (left half of segment)
            _btnInteractive = new FlatButton
            {
                Text = "\u26A1 Interactive",
                Size = new Size(110, 26),
                Location = new Point(2, 2),
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            };
            _btnInteractive.SetAccentColor(AppTheme.AccentCyan);
            _btnInteractive.Click += (s, e) => SetAuthMode(false);
            _authTogglePanel.Controls.Add(_btnInteractive);

            // App-Only button (right half of segment)
            _btnAppCred = new FlatButton
            {
                Text = "\uD83D\uDD12 App-Only",
                Size = new Size(108, 26),
                Location = new Point(114, 2),
                Font = new Font("Segoe UI", 8.5f)
            };
            _btnAppCred.SetGhostStyle();
            _btnAppCred.Click += (s, e) => SetAuthMode(true);
            _authTogglePanel.Controls.Add(_btnAppCred);

            // ── App Credentials slide-down panel ──
            _appCredPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = CredPanelHeight,
                BackColor = Color.FromArgb(18, 22, 32),
                Visible = false,
                Padding = new Padding(150, 6, 12, 6)
            };
            _appCredPanel.Paint += (s, pe) =>
            {
                using (var pen = new Pen(Color.FromArgb(35, 45, 65)))
                    pe.Graphics.DrawLine(pen, 0, 0, _appCredPanel.Width, 0);
            };
            Controls.Add(_appCredPanel);

            // Credential fields in flow layout
            int cx = 150;
            int cy = 8;
            var credFont = new Font("Segoe UI", 7.5f);
            var credInputFont = new Font("Cascadia Code", 7.5f);

            _appCredPanel.Controls.Add(new Label { Text = "Tenant ID", Font = credFont, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(cx, cy) });
            _txtTenantId = new TextBox { Location = new Point(cx + 60, cy - 2), Size = new Size(140, 20), Font = credInputFont, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(22, 28, 40), ForeColor = AppTheme.AccentCyan };
            _appCredPanel.Controls.Add(_txtTenantId);

            cx += 220;
            _appCredPanel.Controls.Add(new Label { Text = "Client ID", Font = credFont, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(cx, cy) });
            _txtClientId = new TextBox { Location = new Point(cx + 56, cy - 2), Size = new Size(140, 20), Font = credInputFont, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(22, 28, 40), ForeColor = AppTheme.AccentCyan };
            _appCredPanel.Controls.Add(_txtClientId);

            cx += 216;
            _appCredPanel.Controls.Add(new Label { Text = "Cert Thumb", Font = credFont, ForeColor = AppTheme.TextMuted, AutoSize = true, BackColor = Color.Transparent, Location = new Point(cx, cy) });
            _txtCertThumb = new TextBox { Location = new Point(cx + 72, cy - 2), Size = new Size(200, 20), Font = credInputFont, BorderStyle = BorderStyle.None, BackColor = Color.FromArgb(22, 28, 40), ForeColor = AppTheme.AccentCyan };
            _appCredPanel.Controls.Add(_txtCertThumb);

            ResumeLayout(false);
        }

        private void SetAuthMode(bool appCred)
        {
            _isAppCredMode = appCred;
            if (appCred)
            {
                _btnAppCred.SetAccentColor(AppTheme.AccentCyan);
                _btnInteractive.SetGhostStyle();
            }
            else
            {
                _btnInteractive.SetAccentColor(AppTheme.AccentCyan);
                _btnAppCred.SetGhostStyle();
            }
            _appCredPanel.Visible = appCred;
            Height = appCred ? BarHeight + CredPanelHeight : BarHeight;
        }

        private void LoadEntraIdDefaults()
        {
            var entra = _config.AppConfig.EntraIdApp;
            if (entra != null)
            {
                _txtTenantId.Text = entra.TenantId ?? "";
                _txtClientId.Text = entra.ClientId ?? "";
                _txtCertThumb.Text = entra.CertificateThumbprint ?? "";
                if (!string.IsNullOrEmpty(entra.ClientId))
                    SetAuthMode(true);
            }
        }

        private void SetStatus(string text, Color color)
        {
            _statusText = text;
            _statusColor = color;
            _statusDotColor = color;
            Invalidate(new Rectangle(0, 0, 200, BarHeight));
        }

        private async void BtnConnect_Click(object sender, EventArgs e)
        {
            string adminUrl = _config.AppConfig.AdminUrl?.Trim();
            if (string.IsNullOrEmpty(adminUrl))
            {
                MessageBox.Show("Admin URL is required.\n\nConfigure it in the Config tab first.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnConnect.Enabled = false;
            SetStatus("Connecting\u2026", AppTheme.AccentGold);

            try
            {
                string script;
                if (_isAppCredMode)
                {
                    string tenantId = _txtTenantId.Text.Trim();
                    string clientId = _txtClientId.Text.Trim();
                    string certThumb = _txtCertThumb.Text.Trim();
                    if (string.IsNullOrEmpty(tenantId) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(certThumb))
                    {
                        MessageBox.Show("Provide Tenant ID, Client ID, and Certificate Thumbprint.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        _btnConnect.Enabled = true;
                        SetStatus("Disconnected", AppTheme.TextMuted);
                        return;
                    }
                    script = $"Connect-SPOService -Url '{adminUrl}' -ClientId '{clientId}' -CertificateThumbprint '{certThumb}' -Tenant '{tenantId}'";
                }
                else
                {
                    script = $"Connect-SPOService -Url '{adminUrl}'";
                }

                // Use external PowerShell process for connection (embedded PS SDK runspace
                // cannot resolve Microsoft.Online.SharePoint.PowerShell module).
                // Prefer PS 5.1 (where SPO module is typically installed).
                string psPath = _psHost.PS51Path ?? _psHost.PS7Path;
                if (string.IsNullOrEmpty(psPath))
                {
                    MessageBox.Show("PowerShell not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _btnConnect.Enabled = true;
                    SetStatus("Disconnected", AppTheme.TextMuted);
                    return;
                }

                string result = await Task.Run(() =>
                {
                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = psPath,
                        Arguments = $"-NoProfile -Command \"Import-Module Microsoft.Online.SharePoint.PowerShell -DisableNameChecking -WarningAction SilentlyContinue -ErrorAction Stop; {script}; if(-not $?) {{ exit 1 }}; Write-Output 'OK'\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        // Interactive mode needs a visible window for the OAuth login dialog
                        CreateNoWindow = _isAppCredMode
                    };
                    psi.Environment["NO_COLOR"] = "1";

                    using (var proc = System.Diagnostics.Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd();
                        string error = proc.StandardError.ReadToEnd();
                        proc.WaitForExit(120000);

                        // Strip ANSI escape codes
                        error = System.Text.RegularExpressions.Regex.Replace(error, @"\x1B\[[0-9;]*m", "");
                        output = System.Text.RegularExpressions.Regex.Replace(output, @"\x1B\[[0-9;]*m", "");

                        if (proc.ExitCode != 0)
                            throw new Exception(string.IsNullOrWhiteSpace(error) ? output.Trim() : error.Trim());
                        return output.Trim();
                    }
                });

                _psHost.SetConnected(true, adminUrl);
                _isConnected = true;
                string method = _isAppCredMode ? "App-Only" : "Interactive";
                SetStatus($"Connected ({method})", AppTheme.AccentGreen);
                _btnConnect.Visible = false;
                _btnDisconnect.Visible = true;
                _authTogglePanel.Visible = false;

                // Auto-fill Tenant ID from PnP if missing
                if (string.IsNullOrWhiteSpace(_config.AppConfig.EntraIdApp?.TenantId) && !string.IsNullOrEmpty(_psHost.PS7Path))
                {
                    _ = Task.Run(() =>
                    {
                        try
                        {
                            // Derive tenant URL from admin URL (remove -admin)
                            string tenantUrl = adminUrl.Replace("-admin", "");
                            var psi2 = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = _psHost.PS7Path,
                                Arguments = $"-NoProfile -NonInteractive -Command \"Get-PnPTenantId -TenantUrl '{tenantUrl}'\"",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            };
                            psi2.Environment["NO_COLOR"] = "1";
                            using (var p = System.Diagnostics.Process.Start(psi2))
                            {
                                string tid = p.StandardOutput.ReadToEnd().Trim();
                                p.WaitForExit(15000);
                                if (p.ExitCode == 0 && Guid.TryParse(tid, out _))
                                {
                                    BeginInvoke((Action)(() =>
                                    {
                                        if (_config.AppConfig.EntraIdApp == null)
                                            _config.AppConfig.EntraIdApp = new SPOVersionManagement.Models.EntraIdAppConfig();
                                        _config.AppConfig.EntraIdApp.TenantId = tid;
                                        _config.SaveAppConfig();
                                        _txtTenantId.Text = tid;
                                    }));
                                }
                            }
                        }
                        catch { /* best-effort */ }
                    });
                }
            }
            catch (Exception ex)
            {
                _psHost.SetConnected(false);
                _isConnected = false;
                SetStatus("Failed", AppTheme.AccentRed);
                MessageBox.Show($"Connection failed:\n{ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnConnect.Enabled = true;
            }
        }

        private void BtnDisconnect_Click(object sender, EventArgs e)
        {
            _psHost.SetConnected(false);
            _isConnected = false;
            SetStatus("Disconnected", AppTheme.TextMuted);
            _btnConnect.Visible = true;
            _btnDisconnect.Visible = false;
            _authTogglePanel.Visible = true;
        }

        public void RefreshAdminUrl()
        {
            _config.Load();
            string adminUrl = _config.AppConfig.AdminUrl ?? "";
            bool hasUrl = !string.IsNullOrWhiteSpace(adminUrl);
            _lblAdminBadge.Text = hasUrl ? "\uD83C\uDF10  Tenant Configured" : "\u26A0  No Admin URL";
            _lblAdminBadge.ForeColor = hasUrl ? AppTheme.AccentGreen : AppTheme.AccentGold;
            if (hasUrl)
            {
                var tip = new ToolTip { InitialDelay = 200, ShowAlways = true };
                tip.SetToolTip(_lblAdminBadge, adminUrl);
            }
        }
    }
}
