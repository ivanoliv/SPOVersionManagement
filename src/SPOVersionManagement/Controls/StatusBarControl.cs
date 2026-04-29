using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class StatusBarControl : Panel
    {
        private string _leftText1 = "";
        private Color _leftColor1 = AppTheme.AccentGreen;
        private string _leftText2 = "";
        private Color _leftColor2 = AppTheme.AccentGreen;
        private string _leftText3 = "";
        private Color _leftColor3 = AppTheme.TextMuted;

        private string _rightText1 = "";
        private string _rightText2 = "";

        public StatusBarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            Dock = DockStyle.Bottom;
            Height = AppTheme.StatusBarHeight;
        }

        public void SetIndicator1(string text, Color color)
        {
            _leftText1 = text; _leftColor1 = color; Invalidate();
        }

        public void SetIndicator2(string text, Color color)
        {
            _leftText2 = text; _leftColor2 = color; Invalidate();
        }

        public void SetIndicator3(string text, Color color)
        {
            _leftText3 = text; _leftColor3 = color; Invalidate();
        }

        public void SetSession(string text) { _rightText1 = text; Invalidate(); }
        public void SetMemory(string text) { _rightText2 = text; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // Background
            using (var brush = new SolidBrush(AppTheme.BgHeader))
                g.FillRectangle(brush, ClientRectangle);

            // Top border
            using (var pen = new Pen(AppTheme.Border))
                g.DrawLine(pen, 0, 0, Width, 0);

            var monoFont = new Font("Cascadia Code", 8f);
            int x = 12;
            int y = (Height - 14) / 2;

            // Left indicators
            if (!string.IsNullOrEmpty(_leftText1))
            {
                DrawDot(g, x, y + 5, _leftColor1);
                x += 10;
                var sz = TextRenderer.MeasureText(_leftText1, monoFont);
                TextRenderer.DrawText(g, _leftText1, monoFont, new Point(x, y), _leftColor1);
                x += sz.Width + 14;
            }

            if (!string.IsNullOrEmpty(_leftText2))
            {
                DrawDot(g, x, y + 5, _leftColor2);
                x += 10;
                var sz = TextRenderer.MeasureText(_leftText2, monoFont);
                TextRenderer.DrawText(g, _leftText2, monoFont, new Point(x, y), _leftColor2);
                x += sz.Width + 14;
            }

            if (!string.IsNullOrEmpty(_leftText3))
            {
                var sz = TextRenderer.MeasureText(_leftText3, monoFont);
                TextRenderer.DrawText(g, _leftText3, monoFont, new Point(x, y), _leftColor3);
            }

            // Right side
            int rx = Width - 12;
            if (!string.IsNullOrEmpty(_rightText2))
            {
                var sz = TextRenderer.MeasureText(_rightText2, monoFont);
                rx -= sz.Width;
                TextRenderer.DrawText(g, _rightText2, monoFont, new Point(rx, y), AppTheme.TextMuted);
                rx -= 16;
            }
            if (!string.IsNullOrEmpty(_rightText1))
            {
                var sz = TextRenderer.MeasureText(_rightText1, monoFont);
                rx -= sz.Width;
                TextRenderer.DrawText(g, _rightText1, monoFont, new Point(rx, y), AppTheme.TextMuted);
            }

            monoFont.Dispose();
        }

        private void DrawDot(Graphics g, int x, int y, Color color)
        {
            using (var brush = new SolidBrush(color))
                g.FillEllipse(brush, x, y, 6, 6);
        }
    }
}
