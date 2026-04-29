using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOVersionManagement.Theme
{
    public class FlatButton : Button
    {
        private Color _bgColor;
        private Color _hoverColor;
        private Color _pressColor;
        private bool _isHovered;
        private bool _isPressed;

        public FlatButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.MouseOverBackColor = Color.Transparent;
            FlatAppearance.MouseDownBackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = AppTheme.FontButton;
            ForeColor = AppTheme.TextPrimary;
            BackColor = Color.Transparent;
            SetAccentColor(AppTheme.AccentCyan);
        }

        public void SetAccentColor(Color accent)
        {
            _bgColor = accent;
            _hoverColor = ControlPaint.Light(accent, 0.2f);
            _pressColor = ControlPaint.Dark(accent, 0.2f);
            ForeColor = accent.GetBrightness() > 0.62f ? AppTheme.BgDark : AppTheme.TextPrimary;
            Invalidate();
        }

        public void SetDangerStyle()
        {
            SetAccentColor(AppTheme.AccentRed);
        }

        public void SetSuccessStyle()
        {
            SetAccentColor(AppTheme.AccentGreen);
        }

        public void SetWarningStyle()
        {
            SetAccentColor(AppTheme.AccentGold);
            ForeColor = AppTheme.BgDark;
        }

        public void SetGhostStyle()
        {
            _bgColor = Color.Transparent;
            _hoverColor = Color.FromArgb(30, AppTheme.AccentCyan);
            _pressColor = Color.FromArgb(50, AppTheme.AccentCyan);
            ForeColor = AppTheme.AccentCyan;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width <= 1 || Height <= 1)
                return;

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent?.BackColor ?? AppTheme.BgDark);

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            Color bg = _isPressed ? _pressColor : _isHovered ? _hoverColor : _bgColor;

            using (var path = AppTheme.CreateRoundedRect(rect, AppTheme.ButtonRadius))
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            // Draw text centered
            TextRenderer.DrawText(g, Text, Font, rect, ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _isHovered = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            _isPressed = true;
            Invalidate();
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _isPressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }
    }
}
