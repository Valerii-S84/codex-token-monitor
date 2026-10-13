using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    // Shared, dependency-free rendering. Every GDI object created here is disposed.
    internal static class AuroraUi
    {
        internal static GraphicsPath Round(RectangleF r, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float d = Math.Max(1, Math.Min(radius * 2, Math.Min(r.Width, r.Height)));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        internal static void Surface(Graphics g, Rectangle bounds, Color accent, float radius, HoverCardControl owner = null)
        {
            if (bounds.Width < 3 || bounds.Height < 3) return;
            Rectangle hoverBounds = bounds;
            if (owner != null) bounds = owner.HoverBounds(g, bounds, radius);
            accent = ViewSettings.Current.Adapt(accent);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle face = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 3);
            using (GraphicsPath shadow = Round(new RectangleF(face.X, face.Y + 2, face.Width, face.Height), radius))
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(180, 2, 3, 10))) g.FillPath(shade, shadow);
            using (GraphicsPath path = Round(face, radius))
            using (LinearGradientBrush fill = new LinearGradientBrush(face, ViewSettings.Current.High, ViewSettings.Current.Low, 90f))
            using (LinearGradientBrush edge = new LinearGradientBrush(face, Color.FromArgb(135, accent), Color.FromArgb(24, 90, 106, 150), 80f))
            using (Pen border = new Pen(edge))
            {
                g.FillPath(fill, path);
                GraphicsState state = g.Save();
                g.SetClip(path, CombineMode.Intersect);
                using (GraphicsPath ellipse = new GraphicsPath())
                {
                    ellipse.AddEllipse(face.Right - face.Width * 0.55f, face.Top - face.Height, face.Width * 0.85f, face.Height * 2f);
                    using (PathGradientBrush glow = new PathGradientBrush(ellipse))
                    {
                        glow.CenterColor = Color.FromArgb(30, accent);
                        glow.SurroundColors = new[] { Color.FromArgb(0, accent) };
                        g.FillPath(glow, ellipse);
                    }
                }
                g.Restore(state);
                g.DrawPath(border, path);
            }
            using (Pen light = new Pen(Color.FromArgb(36, Color.White)))
                g.DrawLine(light, face.X + radius, face.Y + 1, face.Right - radius, face.Y + 1);
            SpectrumFx.Finish(g, face, accent, radius);
            if (owner != null) owner.PaintHover(g, hoverBounds, face, radius);
        }

        internal static void Text(Graphics g, string text, Rectangle bounds, float size, Color color,
            FontStyle style, TextFormatFlags alignment)
        {
            if (bounds.Width < 1 || bounds.Height < 1) return;
            TextFormatFlags flags = alignment | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine
                | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            float fitted = Math.Max(5.5f, size);
            while (fitted > 5.5f)
            {
                using (Font candidate = new Font("Segoe UI", fitted, style))
                {
                    Size measured = TextRenderer.MeasureText(g, text ?? "", candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (measured.Width <= bounds.Width && measured.Height <= bounds.Height) break;
                }
                fitted -= 0.25f;
            }
            using (Font font = new Font("Segoe UI", Math.Max(5.5f, fitted), style))
                TextRenderer.DrawText(g, text ?? "", font, bounds, color, flags);
        }

        internal static Bitmap CreateBrand(int size)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.ScaleTransform(size / 64f, size / 64f);
                using (GraphicsPath halo = new GraphicsPath())
                {
                    halo.AddEllipse(0, 0, 64, 64);
                    using (PathGradientBrush glow = new PathGradientBrush(halo))
                    {
                        glow.CenterColor = Color.FromArgb(140, Theme.Teal);
                        glow.SurroundColors = new[] { Color.Transparent };
                        g.FillPath(glow, halo);
                    }
                }
                PointF top = new PointF(32, 7), left = new PointF(11, 19), right = new PointF(53, 19), middle = new PointF(32, 32);
                using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(10, 6, 44, 50), Theme.Cyan, Theme.Teal, 75f))
                    g.FillPolygon(fill, new[] { top, right, middle, left });
                using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(10, 18, 24, 40), Theme.Teal, Color.FromArgb(25, 70, 87), 90f))
                    g.FillPolygon(fill, new[] { left, middle, new PointF(32, 57), new PointF(11, 44) });
                using (LinearGradientBrush fill = new LinearGradientBrush(new Rectangle(31, 18, 24, 40), Theme.Cyan, Color.FromArgb(31, 79, 118), 90f))
                    g.FillPolygon(fill, new[] { middle, right, new PointF(53, 44), new PointF(32, 57) });
                using (Pen rim = new Pen(Color.FromArgb(190, Color.White), 1.2f))
                {
                    g.DrawLines(rim, new[] { left, middle, right });
                    g.DrawLine(rim, middle, new PointF(32, 55));
                }
            }
            return bitmap;
        }

        internal static void StyleGrid(DataGridView grid)
        {
            grid.GridColor = Color.FromArgb(34, 52, 65);
            grid.DefaultCellStyle.BackColor = Color.FromArgb(18, 30, 39);
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(22, 37, 47);
            grid.DefaultCellStyle.SelectionBackColor = ViewSettings.Current.Palette == 0 ? Color.FromArgb(26, 74, 86) : ViewSettings.Current.Palette == 1 ? Color.FromArgb(83, 66, 26) : Color.FromArgb(89, 46, 31);
            grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(248, 248, 255);
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(25, 42, 53);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Cyan;
            grid.ColumnHeadersHeight = 32;
            grid.RowTemplate.Height = 39;
        }

        internal static string DaysSignature(IEnumerable<DayView> days)
        {
            System.Text.StringBuilder signature = new System.Text.StringBuilder();
            foreach (DayView day in days ?? Enumerable.Empty<DayView>())
            {
                if (day == null) continue;
                TokenUsage u = day.Usage ?? new TokenUsage();
                CostEstimate c = day.ApiValue ?? new CostEstimate();
                signature.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                    "{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10};",
                    day.Date.Ticks, day.Tasks, u.Total, u.Input, u.Output, u.Cached,
                    u.Reasoning, c.Value, c.KnownTokens, c.UnknownTokens, day.Models);
            }
            return signature.ToString();
        }

        internal static void DrawContext(Graphics g, ContextChart owner)
        {
            int w = owner.Width, h = owner.Height;
            if (w < 80 || h < 60) return;
            float scale = owner.LayoutScale;
            Func<int, int> p = value => Math.Max(1, (int)Math.Round(value * scale));
            bool known = owner.Maximum > 0;
            double ratio = known ? Math.Max(0, Math.Min(1, owner.Used / (double)owner.Maximum)) : 0;
            int percent = (int)Math.Round(ratio * 100);
            Color accent = known ? owner.Accent : Theme.Muted;
            Surface(g, new Rectangle(0, 0, w - 1, h - 1), Theme.Teal, p(14), owner);
            Text(g, L10n.T("Context.Session"), new Rectangle(p(16), p(8), w / 2, p(18)), 8f * scale, Theme.Text, FontStyle.Bold, TextFormatFlags.Left);
            Text(g, known ? L10n.F("Context.Free", 100 - percent) : L10n.T("Context.NoDataUpper"), new Rectangle(w / 2, p(8), w / 2 - p(16), p(18)), 7.5f * scale, known ? accent : Theme.Muted, FontStyle.Bold, TextFormatFlags.Right);
            int leftWidth = Math.Max(p(110), Math.Min(w * 44 / 100, p(260)));
            Rectangle sculpture = new Rectangle(p(5), p(29), leftWidth - p(6), Math.Max(30, h - p(56)));
            ContextSculpture.Draw(g, sculpture, ratio, known, accent, owner.ViewYaw, owner.ViewPitch);
            Text(g, known ? L10n.F("Context.Used", percent) : L10n.T("Context.NoData"),
                new Rectangle(p(8), h - p(24), leftWidth - p(16), p(18)),
                9f * scale, Theme.Text, FontStyle.Bold, TextFormatFlags.HorizontalCenter);
            int plotLeft = leftWidth + p(12);
            Text(g, known ? L10n.F("Context.Tokens", FormatUtil.Tokens(owner.Used), FormatUtil.Tokens(owner.Maximum)) : L10n.T("Context.Waiting"), new Rectangle(plotLeft, p(29), w - plotLeft - p(14), p(21)), 10f * scale, Theme.Text, FontStyle.Bold, TextFormatFlags.Left);
            Rectangle plot = new Rectangle(plotLeft, p(55), Math.Max(20, w - plotLeft - p(40)), Math.Max(16, h - p(83)));
            foreach (int level in new[] { 100, 50, 0 })
            {
                int y = plot.Bottom - plot.Height * level / 100;
                using (Pen grid = new Pen(Color.FromArgb(40, Theme.Muted))) { grid.DashStyle = DashStyle.Dot; g.DrawLine(grid, plot.Left, y, plot.Right, y); }
                Text(g, level + "%", new Rectangle(plot.Right + p(4), y - p(6), p(28), p(12)), 6f * scale, Theme.Muted, FontStyle.Regular, TextFormatFlags.Left);
            }
            List<UsageHistorySample> samples = (owner.History ?? new List<UsageHistorySample>()).Where(x => x != null && x.ContextLimit > 0).OrderBy(x => x.At).ToList();
            if (samples.Count == 0 && known) samples.Add(new UsageHistorySample { At = DateTime.Now, ContextUsed = owner.Used, ContextLimit = owner.Maximum });
            if (samples.Count > 0)
            {
                double span = Math.Max(1, (samples[samples.Count - 1].At - samples[0].At).TotalSeconds);
                PointF[] points = samples.Select(x => new PointF(samples.Count == 1 ? plot.Right : plot.Left + (float)((x.At - samples[0].At).TotalSeconds / span * plot.Width), plot.Bottom - (float)(Math.Max(0, Math.Min(1, x.ContextUsed / (double)x.ContextLimit)) * plot.Height))).ToArray();
                if (points.Length > 1)
                {
                    using (GraphicsPath area = new GraphicsPath())
                    {
                        area.AddLines(points);
                        area.AddLine(points[points.Length - 1], new PointF(plot.Right, plot.Bottom));
                        area.AddLine(new PointF(plot.Right, plot.Bottom), new PointF(plot.Left, plot.Bottom));
                        area.CloseFigure();
                        using (LinearGradientBrush fill = new LinearGradientBrush(plot, Color.FromArgb(78, accent), Color.FromArgb(0, accent), 90f)) g.FillPath(fill, area);
                    }
                    using (Pen glow = new Pen(Color.FromArgb(30, accent), p(7))) g.DrawLines(glow, points);
                    using (Pen line = new Pen(accent, Math.Max(2, 2 * scale))) g.DrawLines(line, points);
                }
                PointF last = points[points.Length - 1];
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(45, accent))) g.FillEllipse(glow, last.X - p(6), last.Y - p(6), p(12), p(12));
                using (SolidBrush dot = new SolidBrush(Theme.Text)) g.FillEllipse(dot, last.X - p(2), last.Y - p(2), p(4), p(4));
                string time = samples.Count < 2 ? L10n.T("Context.Now") : samples[0].At.ToString("HH:mm", L10n.Culture) + "–" + samples[samples.Count - 1].At.ToString("HH:mm", L10n.Culture);
                Text(g, time, new Rectangle(plot.Left, h - p(22), plot.Width / 2, p(15)), 6.5f * scale, Theme.Muted, FontStyle.Regular, TextFormatFlags.Left);
                Text(g, owner.Trend, new Rectangle(plot.Left + plot.Width / 2, h - p(22), plot.Width / 2, p(15)), 6.25f * scale, accent, FontStyle.Bold, TextFormatFlags.Right);
            }
            else Text(g, L10n.T("Context.EmptyChart"), plot, 7f * scale, Theme.Muted, FontStyle.Regular, TextFormatFlags.HorizontalCenter);
        }
    }

    internal sealed class AuroraPanel : Panel
    {
        internal Color Accent = Theme.Teal;
        internal bool ShowToken;
        internal AuroraPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            AuroraUi.Surface(e.Graphics, new Rectangle(0, 0, Width - 1, Height - 1), Accent, 12);
            if (ShowToken && Width >= 600) ContextSculpture.DrawToken(e.Graphics, new Rectangle(6, 10, 40, 40), Accent, SpectrumFx.Phase * .1);
        }
    }

    internal sealed class AuroraButton : Button
    {
        private bool _hover;
        private bool _pressed;
        internal bool Selected;
        internal Color Accent = SpectrumFx.Blue;
        internal AuroraButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            Cursor = Cursors.Hand;
        }
        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Background : Parent.BackColor);
            Rectangle bounds = new Rectangle(0, _pressed ? 1 : 0, Math.Max(1, Width - 1), Math.Max(1, Height - 2));
            Color accent = Selected || _hover ? Accent : Theme.Border;
            AuroraUi.Surface(e.Graphics, bounds, accent, 8);
            if (Selected)
            {
                using (GraphicsPath path = AuroraUi.Round(new RectangleF(2, 2, Math.Max(1, Width - 5), Math.Max(1, Height - 6)), 7))
                using (LinearGradientBrush fill = new LinearGradientBrush(bounds, Color.FromArgb(100, Accent), Color.FromArgb(18, Accent), 15f)) e.Graphics.FillPath(fill, path);
            }
            Rectangle text = new Rectangle(7, _pressed ? 1 : 0, Math.Max(1, Width - 14), Math.Max(1, Height - 3));
            Color foreground = Enabled ? (Selected || _hover ? Theme.Text : ForeColor) : Theme.Muted;
            TextRenderer.DrawText(e.Graphics, Text, Font, text, foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Math.Max(1, Width - 9), Math.Max(1, Height - 10)), foreground, BackColor);
        }
    }
}
