using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SPOVersionManagement.Controls
{
    public class SidebarControl : Panel
    {
        private readonly List<NavItem> _items = new List<NavItem>();
        private string _selectedKey = "";
        private int _hoverIndex = -1;
        private string _version = "";
        private int _scrollOffset;
        private const int ItemHeight = 40;
        private const int ChildItemHeight = 32;
        private const int HeaderHeight = 72;

        public event EventHandler<string> NavigationChanged;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string SelectedKey
        {
            get => _selectedKey;
            set { _selectedKey = value; EnsureParentExpanded(value); Invalidate(); }
        }

        public SidebarControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
            Width = Theme.AppTheme.SidebarWidth;
            Dock = DockStyle.Left;
            Cursor = Cursors.Default;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int totalHeight = GetTotalContentHeight();
            int available = Height - HeaderHeight - 30;
            if (totalHeight <= available) { _scrollOffset = 0; base.OnMouseWheel(e); return; }
            _scrollOffset -= e.Delta / 3;
            _scrollOffset = Math.Max(0, Math.Min(_scrollOffset, totalHeight - available));
            Invalidate();
            base.OnMouseWheel(e);
        }

        private int GetTotalContentHeight()
        {
            int h = 0;
            foreach (var entry in GetVisibleItems()) h += entry.Height;
            return h;
        }

        public void SetVersion(string version)
        {
            _version = version;
            Invalidate();
        }

        public void AddItem(string icon, string text, string key)
        {
            _items.Add(new NavItem { Icon = icon, Text = text, Key = key });
            Invalidate();
        }

        public void AddChild(string parentKey, string text, string key)
        {
            var parent = _items.Find(i => i.Key == parentKey);
            if (parent != null)
            {
                parent.Children.Add(new NavItem { Text = text, Key = key });
                Invalidate();
            }
        }

        public void SetBadge(string key, string badge)
        {
            var item = FindItem(key);
            if (item != null)
            {
                item.Badge = badge;
                Invalidate();
            }
        }

        private NavItem FindItem(string key)
        {
            foreach (var item in _items)
            {
                if (item.Key == key) return item;
                foreach (var child in item.Children)
                    if (child.Key == key) return child;
            }
            return null;
        }

        private void EnsureParentExpanded(string key)
        {
            foreach (var item in _items)
            {
                foreach (var child in item.Children)
                {
                    if (child.Key == key)
                    {
                        item.IsExpanded = true;
                        return;
                    }
                }
            }
        }

        private List<VisibleEntry> GetVisibleItems()
        {
            var result = new List<VisibleEntry>();
            foreach (var item in _items)
            {
                result.Add(new VisibleEntry { Item = item, Indent = 0, Height = ItemHeight });
                if (item.IsExpanded)
                {
                    foreach (var child in item.Children)
                        result.Add(new VisibleEntry { Item = child, Indent = 1, Height = ChildItemHeight });
                }
            }
            return result;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            g.Clear(Theme.AppTheme.BgHeader);

            using (var pen = new Pen(Theme.AppTheme.Border))
                g.DrawLine(pen, Width - 1, 0, Width - 1, Height);

            // ── Header ──
            var iconBox = new Rectangle(20, 18, 32, 32);
            using (var bgBrush = new SolidBrush(Color.FromArgb(40, Theme.AppTheme.AccentPurple)))
            using (var borderPen = new Pen(Theme.AppTheme.AccentPurple))
            using (var path = Theme.AppTheme.CreateRoundedRect(iconBox, 4))
            {
                g.FillPath(bgBrush, path);
                g.DrawPath(borderPen, path);
            }
            TextRenderer.DrawText(g, "\u2756", new Font("Segoe UI", 12f), iconBox, Theme.AppTheme.AccentPurple,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, "SPO Manager", Theme.AppTheme.FontHeading,
                new Rectangle(58, 16, 170, 22), Theme.AppTheme.TextPrimary, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            TextRenderer.DrawText(g, "v" + _version, Theme.AppTheme.FontSmall,
                new Rectangle(58, 38, 170, 16), Theme.AppTheme.TextMuted, TextFormatFlags.Left);

            using (var pen = new Pen(Theme.AppTheme.Border))
                g.DrawLine(pen, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

            // ── Nav Label ──
            int y = HeaderHeight + 8;
            TextRenderer.DrawText(g, "NAVIGATION", new Font("Segoe UI", 7.5f, FontStyle.Bold),
                new Rectangle(20, y, 200, 16), Theme.AppTheme.TextMuted, TextFormatFlags.Left);
            y += 22;

            // ── Nav Items ──
            var visibleItems = GetVisibleItems();
            int contentStart = y;
            y -= _scrollOffset;
            g.SetClip(new Rectangle(0, contentStart, Width, Height - contentStart));

            for (int i = 0; i < visibleItems.Count; i++)
            {
                var entry = visibleItems[i];
                var item = entry.Item;
                int indent = entry.Indent;
                int h = entry.Height;
                var itemRect = new Rectangle(0, y, Width, h);

                bool isSelected = item.Key == _selectedKey;
                bool isParentOfSelected = indent == 0 && item.Children.Count > 0 && IsChildSelected(item);
                bool isHover = (i == _hoverIndex);
                bool hasChildren = item.Children.Count > 0;

                // Background
                if (isSelected)
                {
                    using (var brush = new SolidBrush(Theme.AppTheme.BgCard))
                        g.FillRectangle(brush, itemRect);
                }
                else if (isParentOfSelected)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(40, Theme.AppTheme.BgCard)))
                        g.FillRectangle(brush, itemRect);
                }
                else if (isHover)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(80, Theme.AppTheme.BgCard)))
                        g.FillRectangle(brush, itemRect);
                }

                // Left accent bar
                if (isSelected)
                {
                    using (var brush = new SolidBrush(Theme.AppTheme.AccentCyan))
                        g.FillRectangle(brush, 0, y, 4, h);
                }

                // Icon / bullet
                Color iconColor = isSelected ? Theme.AppTheme.AccentCyan
                    : isParentOfSelected ? Theme.AppTheme.AccentCyan
                    : Theme.AppTheme.TextSecondary;

                if (indent == 0)
                {
                    TextRenderer.DrawText(g, item.Icon, new Font("Segoe UI", 11f),
                        new Rectangle(20, y, 28, h), iconColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                else
                {
                    TextRenderer.DrawText(g, "\u2022", new Font("Segoe UI", 8f),
                        new Rectangle(42, y, 14, h), iconColor,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                // Text
                int textLeft = indent == 0 ? 52 : 60;
                Color textColor = isSelected ? Theme.AppTheme.TextPrimary
                    : isParentOfSelected && indent == 0 ? Theme.AppTheme.TextPrimary
                    : Theme.AppTheme.TextSecondary;
                var textFont = indent == 0 ? new Font("Segoe UI Semibold", 9.5f) : new Font("Segoe UI", 8.5f);
                TextRenderer.DrawText(g, item.Text, textFont,
                    new Rectangle(textLeft, y, Width - textLeft - 30, h), textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

                // Expand/collapse arrow for parents
                if (hasChildren && indent == 0)
                {
                    string arrow = item.IsExpanded ? "\u25BC" : "\u25B6";
                    TextRenderer.DrawText(g, arrow, new Font("Segoe UI", 7f),
                        new Rectangle(Width - 28, y, 20, h), Theme.AppTheme.TextMuted,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                // Badge
                if (!string.IsNullOrEmpty(item.Badge))
                {
                    var badgeSize = TextRenderer.MeasureText(item.Badge, Theme.AppTheme.FontSmall);
                    int badgeW = Math.Max(badgeSize.Width + 10, 22);
                    var badgeRect = new Rectangle(Width - badgeW - 16, y + (h - 18) / 2, badgeW, 18);
                    using (var bgBrush = new SolidBrush(Color.FromArgb(40, Theme.AppTheme.AccentGold)))
                    using (var borderPen = new Pen(Theme.AppTheme.AccentGold))
                    using (var path = Theme.AppTheme.CreateRoundedRect(badgeRect, 9))
                    {
                        g.FillPath(bgBrush, path);
                        g.DrawPath(borderPen, path);
                    }
                    TextRenderer.DrawText(g, item.Badge, new Font("Cascadia Code", 7f),
                        badgeRect, Theme.AppTheme.AccentGold,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }

                y += h;
            }

            g.ResetClip();

            // ── Discrete scrollbar ──
            int totalH = GetTotalContentHeight();
            int availH = Height - contentStart;
            if (totalH > availH)
            {
                int trackX = Width - 5;
                int trackH = availH;
                float ratio = (float)availH / totalH;
                int thumbH = Math.Max(20, (int)(trackH * ratio));
                int thumbY = contentStart + (int)((float)_scrollOffset / (totalH - availH) * (trackH - thumbH));
                using (var brush = new SolidBrush(Color.FromArgb(60, 255, 255, 255)))
                    g.FillRectangle(brush, trackX, thumbY, 3, thumbH);
            }
        }

        private bool IsChildSelected(NavItem parent)
        {
            foreach (var child in parent.Children)
                if (child.Key == _selectedKey) return true;
            return false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = HitTest(e.Y);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                Cursor = idx >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (_hoverIndex >= 0) { _hoverIndex = -1; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int idx = HitTest(e.Y);
            if (idx < 0) { base.OnMouseClick(e); return; }

            var visibleItems = GetVisibleItems();
            if (idx >= visibleItems.Count) { base.OnMouseClick(e); return; }

            var entry = visibleItems[idx];
            var item = entry.Item;

            if (entry.Indent == 0 && item.Children.Count > 0)
            {
                item.IsExpanded = !item.IsExpanded;
                if (item.IsExpanded && !IsChildSelected(item) && item.Children.Count > 0)
                {
                    _selectedKey = item.Children[0].Key;
                    NavigationChanged?.Invoke(this, _selectedKey);
                }
                Invalidate();
            }
            else
            {
                if (item.Key != _selectedKey)
                {
                    _selectedKey = item.Key;
                    Invalidate();
                    NavigationChanged?.Invoke(this, _selectedKey);
                }
            }
            base.OnMouseClick(e);
        }

        private int HitTest(int mouseY)
        {
            int y = HeaderHeight + 30 - _scrollOffset;
            if (mouseY < HeaderHeight + 30) return -1;
            var visibleItems = GetVisibleItems();
            for (int i = 0; i < visibleItems.Count; i++)
            {
                int h = visibleItems[i].Height;
                if (mouseY >= y && mouseY < y + h) return i;
                y += h;
            }
            return -1;
        }

        private class NavItem
        {
            public string Icon { get; set; }
            public string Text { get; set; }
            public string Key { get; set; }
            public string Badge { get; set; }
            public List<NavItem> Children { get; set; } = new List<NavItem>();
            public bool IsExpanded { get; set; }
        }

        private struct VisibleEntry
        {
            public NavItem Item;
            public int Indent;
            public int Height;
        }
    }
}
