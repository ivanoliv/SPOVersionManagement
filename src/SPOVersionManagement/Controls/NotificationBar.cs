using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class NotificationBar : UserControl
    {
        private Label _lblIcon;
        private Label _lblMessage;
        private LinkLabel _lnkAction;
        private Label _btnClose;
        private Timer _slideTimer;
        private Timer _autoHideTimer;
        private int _targetHeight;
        private bool _isShowing;
        private Color _accentColor;

        public event EventHandler ActionClicked;

        public NotificationBar()
        {
            _targetHeight = AppTheme.NotificationHeight;
            Height = 0;
            Dock = DockStyle.Top;
            Visible = false;
            _accentColor = AppTheme.AccentCyan;
            SetupControls();
            SetupTimers();
        }

        private void SetupControls()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

            _lblIcon = new Label
            {
                Text = "\u26A0",
                Font = new Font("Segoe UI", 11f),
                ForeColor = AppTheme.AccentGold,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(12, 8)
            };

            _lblMessage = new Label
            {
                Text = "",
                Font = AppTheme.FontNotification,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = false,
                BackColor = Color.Transparent,
                Location = new Point(36, 9),
                Size = new Size(500, 18),
                MaximumSize = new Size(600, 0) // Allow wrapping, 0 height = auto
            };

            _lnkAction = new LinkLabel
            {
                Text = "View",
                Font = AppTheme.FontNotification,
                LinkColor = AppTheme.AccentCyan,
                ActiveLinkColor = AppTheme.AccentGold,
                AutoSize = true,
                BackColor = Color.Transparent,
                Visible = false
            };
            _lnkAction.LinkClicked += (s, e) => ActionClicked?.Invoke(this, EventArgs.Empty);

            _btnClose = new Label
            {
                Text = "\u2715",
                Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            _btnClose.Click += (s, e) => Hide();
            _btnClose.MouseEnter += (s, e) => _btnClose.ForeColor = AppTheme.TextPrimary;
            _btnClose.MouseLeave += (s, e) => _btnClose.ForeColor = AppTheme.TextSecondary;

            Controls.Add(_lblIcon);
            Controls.Add(_lblMessage);
            Controls.Add(_lnkAction);
            Controls.Add(_btnClose);
        }

        private void SetupTimers()
        {
            _slideTimer = new Timer { Interval = 16 }; // ~60fps
            _slideTimer.Tick += SlideTimer_Tick;

            _autoHideTimer = new Timer { Interval = 15000 };
            _autoHideTimer.Tick += (s, e) =>
            {
                _autoHideTimer.Stop();
                SlideOut();
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Gradient background
            using (var brush = new LinearGradientBrush(ClientRectangle,
                AppTheme.NotifyBg, Color.FromArgb(200, AppTheme.NotifyBg), 0f))
            {
                g.FillRectangle(brush, ClientRectangle);
            }

            // Bottom border line
            using (var pen = new Pen(_accentColor, 2f))
            {
                g.DrawLine(pen, 0, Height - 1, Width, Height - 1);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_btnClose != null)
                _btnClose.Location = new Point(Width - 30, 8);
            if (_lnkAction != null && _lnkAction.Visible)
                _lnkAction.Location = new Point(_lblMessage.Right + 12, 9);
        }

        public void ShowNotification(string message, string actionText = null,
            Color? accentColor = null, int autoHideMs = 0)
        {
            // Clear old message
            _lblMessage.Text = "";
            
            // Set new message and calculate required height
            _lblMessage.Text = message;
            Size prefSize = _lblMessage.GetPreferredSize(new Size(600, 0));
            _lblMessage.Size = prefSize;
            
            // Calculate required height based on message
            int messageHeight = _lblMessage.Height + 18; // 18 = 9px top + 9px bottom padding
            _targetHeight = Math.Max(AppTheme.NotificationHeight, Math.Min(messageHeight, 100)); // Max 100px for very long messages
            
            _accentColor = accentColor ?? AppTheme.AccentCyan;

            if (!string.IsNullOrEmpty(actionText))
            {
                _lnkAction.Text = actionText;
                _lnkAction.Visible = true;
                _lnkAction.Location = new Point(_lblMessage.Right + 12, 9);
            }
            else
            {
                _lnkAction.Visible = false;
            }

            _btnClose.Location = new Point(Width - 30, 8);

            Visible = true;
            _isShowing = true;
            _slideTimer.Start();

            if (autoHideMs > 0)
            {
                _autoHideTimer.Interval = autoHideMs;
                _autoHideTimer.Start();
            }
        }

        public void ShowInfo(string message, string actionText = null)
        {
            _lblIcon.Text = "\u2139";
            _lblIcon.ForeColor = AppTheme.AccentCyan;
            ShowNotification(message, actionText, AppTheme.AccentCyan);
        }

        public void ShowSuccess(string message, int autoHideMs = 5000)
        {
            _lblIcon.Text = "\u2714";
            _lblIcon.ForeColor = AppTheme.AccentGreen;
            ShowNotification(message, null, AppTheme.AccentGreen, autoHideMs);
        }

        public void ShowWarning(string message, string actionText = null)
        {
            _lblIcon.Text = "\u26A0";
            _lblIcon.ForeColor = AppTheme.AccentGold;
            ShowNotification(message, actionText, AppTheme.AccentGold);
        }

        public void ShowUpdate(string version, string actionText = "View Release Notes")
        {
            _lblIcon.Text = "\uD83D\uDD14";
            _lblIcon.ForeColor = AppTheme.AccentCyan;
            ShowNotification($"Update v{version} is available!", actionText, AppTheme.AccentCyan);
        }

        public new void Hide()
        {
            _autoHideTimer.Stop();
            _isShowing = false;
            _slideTimer.Start();
        }

        private void SlideOut()
        {
            _isShowing = false;
            _slideTimer.Start();
        }

        private void SlideTimer_Tick(object sender, EventArgs e)
        {
            if (_isShowing)
            {
                if (Height < _targetHeight)
                {
                    Height = Math.Min(Height + 4, _targetHeight);
                }
                else
                {
                    _slideTimer.Stop();
                }
            }
            else
            {
                if (Height > 0)
                {
                    Height = Math.Max(Height - 4, 0);
                }
                else
                {
                    _slideTimer.Stop();
                    Visible = false;
                }
            }
        }
    }
}
