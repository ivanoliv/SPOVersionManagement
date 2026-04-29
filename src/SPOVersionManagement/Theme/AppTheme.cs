using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOVersionManagement.Theme
{
    public static class AppTheme
    {
        // ── Primary Background ──
        public static readonly Color BgDark = ColorTranslator.FromHtml("#1a1a2e");
        public static readonly Color BgMedium = ColorTranslator.FromHtml("#16213e");
        public static readonly Color BgCard = ColorTranslator.FromHtml("#0f3460");
        public static readonly Color BgInput = ColorTranslator.FromHtml("#1a2744");
        public static readonly Color BgHeader = ColorTranslator.FromHtml("#0d1b36");

        // ── Accent Colors ──
        public static readonly Color AccentCyan = ColorTranslator.FromHtml("#00d4ff");
        public static readonly Color AccentPurple = ColorTranslator.FromHtml("#7b2cbf");
        public static readonly Color AccentGold = ColorTranslator.FromHtml("#ffc107");
        public static readonly Color AccentGreen = ColorTranslator.FromHtml("#00e676");
        public static readonly Color AccentRed = ColorTranslator.FromHtml("#ff5252");
        public static readonly Color AccentOrange = ColorTranslator.FromHtml("#ff9800");

        // ── Text ──
        public static readonly Color TextPrimary = Color.White;
        public static readonly Color TextSecondary = ColorTranslator.FromHtml("#b0b0b0");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6c757d");
        public static readonly Color TextLink = AccentCyan;

        // ── Status ──
        public static readonly Color StatusSuccess = AccentGreen;
        public static readonly Color StatusError = AccentRed;
        public static readonly Color StatusWarning = AccentGold;
        public static readonly Color StatusInfo = AccentCyan;
        public static readonly Color StatusRunning = AccentOrange;

        // ── Border & Separator ──
        public static readonly Color Border = ColorTranslator.FromHtml("#2a3a5c");
        public static readonly Color Separator = ColorTranslator.FromHtml("#1e3050");

        // ── Tab Colors ──
        public static readonly Color TabActive = BgCard;
        public static readonly Color TabInactive = BgDark;
        public static readonly Color TabHover = ColorTranslator.FromHtml("#122a50");

        // ── Notification Bar ──
        public static readonly Color NotifyBg = ColorTranslator.FromHtml("#0a2948");
        public static readonly Color NotifyBorder = AccentCyan;

        // ── Fonts ──
        public static readonly Font FontTitle = new Font("Segoe UI", 18f, FontStyle.Bold);
        public static readonly Font FontSubtitle = new Font("Segoe UI", 13f, FontStyle.Regular);
        public static readonly Font FontHeading = new Font("Segoe UI Semibold", 11f, FontStyle.Bold);
        public static readonly Font FontBody = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        public static readonly Font FontSmall = new Font("Segoe UI", 8.5f, FontStyle.Regular);
        public static readonly Font FontGridHeader = new Font("Segoe UI Semibold", 8f, FontStyle.Regular);
        public static readonly Font FontMono = new Font("Cascadia Code", 9f, FontStyle.Regular);
        public static readonly Font FontButton = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        public static readonly Font FontTab = new Font("Segoe UI Semibold", 10f, FontStyle.Regular);
        public static readonly Font FontStat = new Font("Segoe UI", 22f, FontStyle.Bold);
        public static readonly Font FontStatLabel = new Font("Segoe UI", 9f, FontStyle.Regular);
        public static readonly Font FontNotification = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // ── Sizing ──
        public const int CardRadius = 8;
        public const int ButtonRadius = 6;
        public const int CardPadding = 16;
        public const int FormWidth = 1200;
        public const int FormHeight = 800;
        public const int NotificationHeight = 36;
        public const int StatusBarHeight = 32;
        public const int SidebarWidth = 240;

        // ── Glass Panel ──
        public static readonly Color GlassBg = Color.FromArgb(153, 15, 52, 96); // rgba(15,52,96,0.6)
        public static readonly Color GlassBorder = Border;

        /// <summary>
        /// Paints a gradient background on any control.
        /// </summary>
        public static void PaintGradientBackground(Graphics g, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            using (var brush = new LinearGradientBrush(rect, BgDark, BgMedium, 45f))
            {
                g.FillRectangle(brush, rect);
            }
        }

        /// <summary>
        /// Paints a rounded rectangle card background.
        /// </summary>
        public static void PaintCard(Graphics g, Rectangle rect, Color? bgColor = null)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            var color = bgColor ?? BgCard;
            using (var path = CreateRoundedRect(rect, CardRadius))
            using (var brush = new SolidBrush(color))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(brush, path);
            }
        }

        /// <summary>
        /// Creates a rounded rectangle GraphicsPath.
        /// </summary>
        public static GraphicsPath CreateRoundedRect(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();

            if (rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(new Rectangle(rect.X, rect.Y, 1, 1));
                return path;
            }

            int maxRadius = System.Math.Max(1, System.Math.Min(rect.Width, rect.Height) / 2);
            radius = System.Math.Max(1, System.Math.Min(radius, maxRadius));
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// Applies dark theme to a DataGridView.
        /// </summary>
        public static void StyleDataGrid(DataGridView grid)
        {
            grid.BackgroundColor = BgDark;
            grid.ForeColor = TextPrimary;
            grid.GridColor = Border;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.EnableHeadersVisualStyles = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersVisible = true;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ScrollBars = ScrollBars.Both;
            grid.Font = FontBody;

            grid.DefaultCellStyle.BackColor = BgDark;
            grid.DefaultCellStyle.ForeColor = TextPrimary;
            grid.DefaultCellStyle.SelectionBackColor = BgCard;
            grid.DefaultCellStyle.SelectionForeColor = AccentCyan;
            grid.DefaultCellStyle.Padding = new Padding(4, 2, 4, 2);
            grid.DefaultCellStyle.Font = FontBody;

            grid.AlternatingRowsDefaultCellStyle.BackColor = BgMedium;

            grid.ColumnHeadersDefaultCellStyle.BackColor = BgHeader;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AccentCyan;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = BgHeader;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AccentCyan;
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            grid.ColumnHeadersDefaultCellStyle.Font = FontGridHeader;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 2, 6, 2);
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersHeight = 40;
        }

        /// <summary>
        /// Applies the Site Catalog tab look to a TabControl.
        /// </summary>
        public static void StyleTabControl(TabControl tabControl)
        {
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Normal;
            tabControl.Padding = new Point(16, 6);
            tabControl.BackColor = BgDark;
            tabControl.ForeColor = TextPrimary;
            tabControl.DrawItem -= DrawTabControlItem;
            tabControl.DrawItem += DrawTabControlItem;

            foreach (TabPage page in tabControl.TabPages)
            {
                page.BackColor = BgDark;
                page.ForeColor = TextPrimary;
            }
        }

        private static void DrawTabControlItem(object sender, DrawItemEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null || e.Index < 0 || e.Index >= tabControl.TabPages.Count)
                return;

            var tabPage = tabControl.TabPages[e.Index];
            var rect = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color backColor = selected ? TabActive : TabInactive;
            Color textColor = selected ? AccentCyan : TextSecondary;

            using (var brush = new SolidBrush(backColor))
            using (var textBrush = new SolidBrush(textColor))
            using (var borderPen = new Pen(Border))
            {
                e.Graphics.FillRectangle(brush, rect);
                e.Graphics.DrawRectangle(borderPen, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                TextRenderer.DrawText(
                    e.Graphics,
                    tabPage.Text,
                    FontGridHeader,
                    rect,
                    textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }

        /// <summary>
        /// Applies dark theme to a TextBox.
        /// </summary>
        public static void StyleTextBox(TextBox txt)
        {
            txt.BackColor = BgInput;
            txt.ForeColor = TextPrimary;
            txt.BorderStyle = BorderStyle.FixedSingle;
            txt.Font = FontBody;
        }

        /// <summary>
        /// Applies dark theme to a ComboBox.
        /// </summary>
        public static void StyleComboBox(ComboBox cmb)
        {
            cmb.BackColor = BgInput;
            cmb.ForeColor = TextPrimary;
            cmb.FlatStyle = FlatStyle.Flat;
            cmb.Font = FontBody;
        }

        /// <summary>
        /// Applies dark theme to a Label.
        /// </summary>
        public static void StyleLabel(Label lbl, Font font = null, Color? color = null)
        {
            lbl.ForeColor = color ?? TextPrimary;
            lbl.Font = font ?? FontBody;
            lbl.BackColor = Color.Transparent;
        }

        /// <summary>
        /// Paints a glass panel background (semi-transparent card with border).
        /// </summary>
        public static void PaintGlassPanel(Graphics g, Rectangle rect, Color? accentLeft = null)
        {
            if (rect.Width <= 0 || rect.Height <= 0)
                return;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = CreateRoundedRect(rect, CardRadius))
            using (var fill = new SolidBrush(GlassBg))
            using (var pen = new Pen(GlassBorder, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            if (accentLeft.HasValue)
            {
                using (var pen = new Pen(accentLeft.Value, 3f))
                {
                    g.DrawLine(pen, rect.X + 1, rect.Y + CardRadius, rect.X + 1, rect.Bottom - CardRadius);
                }
            }
        }

        /// <summary>
        /// Applies dark theme to a NumericUpDown.
        /// </summary>
        public static void StyleNumericUpDown(NumericUpDown nud)
        {
            nud.BackColor = BgInput;
            nud.ForeColor = TextPrimary;
            nud.BorderStyle = BorderStyle.FixedSingle;
            nud.Font = FontBody;
        }

        /// <summary>
        /// Applies dark theme to a RichTextBox used as a console.
        /// </summary>
        public static void StyleConsole(RichTextBox rtb)
        {
            rtb.BackColor = ColorTranslator.FromHtml("#0a0a1a");
            rtb.ForeColor = TextPrimary;
            rtb.Font = FontMono;
            rtb.ReadOnly = true;
            rtb.BorderStyle = BorderStyle.None;
            rtb.WordWrap = true;
            rtb.ScrollBars = RichTextBoxScrollBars.Vertical;
        }

        /// <summary>
        /// Applies dark theme to a CheckBox.
        /// </summary>
        public static void StyleCheckBox(CheckBox chk)
        {
            chk.ForeColor = TextPrimary;
            chk.Font = FontBody;
            chk.BackColor = Color.Transparent;
            chk.FlatStyle = FlatStyle.Standard;
        }
    }
}
