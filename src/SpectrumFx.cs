using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class SpectrumFx
    {
        internal static readonly Color Blue = Color.FromArgb(67, 169, 255);
        internal static readonly Color Gold = Color.FromArgb(255, 214, 83);
        internal static Color[] Colors { get { return ViewSettings.Current.TokenColors; } }
        internal static bool MotionEnabled = true;
        internal static double Phase;
        internal static double ContextPhase;
        internal static bool EconomyActive { get { return ViewSettings.Current.Economy || SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline; } }

        // TextRenderer uses GDI and ignores the GDI+ world scale.
        // Apply that transform to the text bounds and font.
        internal static void DrawScaledText(Graphics g, string text, Font font,
            Rectangle bounds, Color color, TextFormatFlags flags)
        {
            using (Matrix transform = g.Transform)
            {
                float[] matrix = transform.Elements;
                float scale = (float)Math.Sqrt(matrix[2] * matrix[2] + matrix[3] * matrix[3]);
                PointF[] corners = { new PointF(bounds.Left, bounds.Top), new PointF(bounds.Right, bounds.Bottom) };
                transform.TransformPoints(corners);
                Rectangle physical = Rectangle.FromLTRB((int)Math.Round(corners[0].X), (int)Math.Round(corners[0].Y),
                    (int)Math.Round(corners[1].X), (int)Math.Round(corners[1].Y));
                GraphicsState state = g.Save();
                try
                {
                    g.ResetTransform();
                    using (Font scaled = new Font(font.FontFamily, font.SizeInPoints * Math.Max(.1f, scale), font.Style))
                        TextRenderer.DrawText(g, text, scaled, physical, color, flags);
                }
                finally { g.Restore(state); }
            }
        }

        internal static void Halo(Graphics g, RectangleF bounds, Color color, int opacity)
        {
            if (bounds.Width < 1 || bounds.Height < 1) return;
            opacity = opacity * ViewSettings.Current.Intensity / 100;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using (PathGradientBrush glow = new PathGradientBrush(path))
                {
                    glow.CenterColor = Color.FromArgb(opacity, color);
                    glow.SurroundColors = new[] { Color.FromArgb(0, color) };
                    g.FillPath(glow, path);
                }
            }
        }

        internal static void Finish(Graphics g, Rectangle face, Color accent, float radius)
        {
            if (face.Width < 8 || face.Height < 8 || ViewSettings.Current.Intensity == 0) return;
            GraphicsState saved = g.Save();
            try
            {
                using (GraphicsPath clip = AuroraUi.Round(face, radius)) g.SetClip(clip, CombineMode.Intersect);
                Halo(g, new RectangleF(face.Right - face.Width * .52f, face.Top - face.Height * .85f,
                    face.Width * .80f, face.Height * 2f), accent, 52);
                // The secondary reflection is amber, with no blue-to-red blend.
                if (face.Width > 300 && face.Height > 45)
                    Halo(g, new RectangleF(face.Left - face.Width * .26f, face.Bottom - face.Height * .4f,
                        face.Width * .6f, face.Height * 1.1f), Theme.Orange, 18);
                using (LinearGradientBrush rim = new LinearGradientBrush(face,
                    Color.FromArgb(200, accent), Color.FromArgb(0, accent), 0f))
                using (Pen light = new Pen(rim, 1.6f))
                    g.DrawLine(light, face.Left + radius, face.Top + 1.5f, face.Right - radius, face.Top + 1.5f);
                // Low-opacity travelling reflection. Geometry and text stay still.
                double progress = (Math.Sin(Phase * .22) + 1) / 2;
                float x = face.Left - face.Width * .3f + (float)progress * face.Width * 1.5f;
                PointF[] sheen = { new PointF(x, face.Top), new PointF(x + 16, face.Top),
                    new PointF(x - 9, face.Bottom), new PointF(x - 25, face.Bottom) };
                using (SolidBrush brush = new SolidBrush(Color.FromArgb(7, Color.White))) g.FillPolygon(brush, sheen);
            }
            finally { g.Restore(saved); }
        }

        internal static void DrawTokens(Graphics g, TokenBar owner)
        {
            int w = owner.Width, h = owner.Height;
            if (w < 80 || h < 30) return;
            float scale = owner.LayoutScale;
            Func<int, int> p = n => Math.Max(1, (int)Math.Round(n * scale));
            AuroraUi.Surface(g, new Rectangle(0, 0, w - 1, h - 1), Blue, p(12));
            string title = "СЕСІЯ" + (owner.TaskUsage == null ? "" : "  /  ЗАВД. " + FormatUtil.Tokens(owner.TaskUsage.Total));
            AuroraUi.Text(g, title, new Rectangle(p(12), p(4), Math.Max(20, w - p(156)), p(17)), 7.3f * scale, Theme.Text, FontStyle.Bold, TextFormatFlags.Left);
            AuroraUi.Text(g, owner.Trend, new Rectangle(w - p(138), p(4), p(126), p(17)), 7.2f * scale, Theme.Cyan, FontStyle.Bold, TextFormatFlags.Right);
            TokenUsage u = owner.Usage ?? new TokenUsage();
            string[] labels = { "УСЬОГО", "ВХІД", "ВИХІД", "КЕШ", "HIT", "API ≈" };
            string[] values = { FormatUtil.Tokens(u.Total), FormatUtil.Tokens(u.Input), FormatUtil.Tokens(u.Output),
                FormatUtil.Tokens(u.Cached), FormatUtil.CacheHitPercent(u) + "%", FormatUtil.ApiValue(owner.ApiValue) };
            bool tall = h > p(120);
            bool compact = !tall && w < p(600);
            int columns = tall ? 2 : compact ? 3 : 6;
            int columnWidth = (w - p(16)) / columns;
            for (int i = 0; i < 6; i++)
            {
                int x = p(8) + i % columns * columnWidth;
                int y = p(22) + (tall ? i / columns * ((h - p(28)) / 3) : compact ? i / columns * p(21) : 0);
                int cellHeight = tall ? (h - p(28)) / 3 - p(4) : compact ? p(20) : Math.Max(p(24), h - y - p(5));
                Rectangle cell = new Rectangle(x, y, columnWidth - p(3), cellHeight);
                Rectangle hoverBounds = cell;
                cell = owner.HoverBounds(g, cell, p(5));
                x = cell.X;
                y = cell.Y;
                cellHeight = cell.Height;
                using (GraphicsPath path = AuroraUi.Round(cell, p(5)))
                using (LinearGradientBrush tint = new LinearGradientBrush(cell, Color.FromArgb(28, Colors[i]), Color.FromArgb(4, Colors[i]), 90f)) g.FillPath(tint, path);
                owner.PaintHover(g, hoverBounds, cell, p(5));
                if (compact)
                {
                    AuroraUi.Text(g, i == 0 ? "Σ" : labels[i], new Rectangle(x + p(4), y, p(35), cellHeight),
                        6.5f * scale, Colors[i], FontStyle.Regular, TextFormatFlags.Left);
                    AuroraUi.Text(g, values[i], new Rectangle(x + p(39), y, columnWidth - p(46), cellHeight),
                        9f * scale, Theme.Text, FontStyle.Bold, TextFormatFlags.Right);
                }
                else
                {
                    ContextSculpture.DrawToken(g, new Rectangle(x + p(4), y + p(1), p(15), p(15)), Colors[i], Phase * .1 + i * .4);
                    AuroraUi.Text(g, labels[i], new Rectangle(x + p(24), y, columnWidth - p(29), p(17)), 6.5f * scale, Colors[i], FontStyle.Bold, TextFormatFlags.Left);
                    AuroraUi.Text(g, values[i], new Rectangle(x + p(5), y + p(17), columnWidth - p(13), Math.Max(10, cellHeight - p(17))),
                        (tall ? 15 : 10.5f) * scale, Theme.Text, FontStyle.Bold, TextFormatFlags.Right);
                }
            }
        }
    }

    internal sealed class SpectrumMotion : IDisposable
    {
        private readonly Form _form;
        private readonly ContextChart _context;
        private readonly Control _agents;
        private readonly Control _today;
        private readonly Timer _timer = new Timer();
        private readonly ContextMenuStrip _menu = new ContextMenuStrip();
        private readonly ToolStripMenuItem _motion = new ToolStripMenuItem("Плавний рух · Ctrl+M");
        private bool _disposed;
        private double _velocity;

        internal SpectrumMotion(Form form, ContextChart context, Control agents, Control today)
        {
            _form = form; _context = context; _agents = agents; _today = today;
            SpectrumFx.MotionEnabled = ViewSettings.Current.Motion && !SystemInformation.HighContrast;
            _motion.CheckOnClick = true;
            _motion.Checked = SpectrumFx.MotionEnabled;
            _motion.CheckedChanged += delegate
            {
                SpectrumFx.MotionEnabled = _motion.Checked;
                ViewSettings.Current.Motion = _motion.Checked;
                if (_form is MainForm) ViewSettings.Current.Save();
                SpectrumFx.Phase = 0;
                _context.ViewYaw = _context.ViewPitch = 0;
                UpdateTimer();
                _form.Invalidate(true);
            };
            _menu.Items.Add(_motion);
            AddPreferences();
            _form.ContextMenuStrip = _menu;
            _context.ContextMenuStrip = _menu;
            _context.AccessibleDescription = "Рухайте курсором, щоб змінити ракурс. Ctrl+M або права кнопка — увімкнути/вимкнути плавний рух.";
            _form.KeyPreview = true;
            _form.KeyDown += HandleKey;
            _form.VisibleChanged += HandleVisibility;
            _form.Resize += HandleVisibility;
            _form.Disposed += HandleDisposed;
            // A single 12.5 fps UI clock. No background workers or busy loops.
            _timer.Interval = 80;
            _timer.Tick += delegate
            {
                _timer.Interval = SpectrumFx.EconomyActive ? 250 : 80;
                double step = .045 * _timer.Interval / 80d;
                SpectrumFx.Phase += step;
                _velocity += ((_context.IsActive ? 1 : 0) - _velocity) * .15;
                if (_velocity < .005) _velocity = 0;
                SpectrumFx.ContextPhase = (SpectrumFx.ContextPhase + step * _velocity) % (Math.PI * 20);
                if (_context.Visible) _context.Invalidate();
                if (_today.Visible) _today.Invalidate();
                if (_agents.Visible) foreach (Control card in _agents.Controls) card.Invalidate();
            };
            UpdateTimer();
        }
        internal void ShowMenu(Control anchor) { _menu.Show(anchor, new Point(0, anchor.Height)); }
        private void AddPreferences()
        {
            ToolStripMenuItem modes = new ToolStripMenuItem("Режим вигляду");
            string[] modeNames = { "Компактний · Ctrl+1", "Аналітичний · Ctrl+2", "Презентаційний · Ctrl+3" };
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                ToolStripMenuItem item = new ToolStripMenuItem(modeNames[i]);
                item.Click += delegate { if (_form is MainForm) ((MainForm)_form).ApplyDisplayMode(selected, true); };
                modes.DropDownOpening += delegate { item.Checked = ViewSettings.Current.Mode == selected; };
                modes.DropDownItems.Add(item);
            }
            _menu.Items.Add(modes);
            ToolStripMenuItem palettes = new ToolStripMenuItem("Палітра");
            string[] paletteNames = { "Океан · синій та бірюза", "Сонце · золото та зелень", "Вогонь · помаранчевий та червоний" };
            for (int i = 0; i < 3; i++)
            {
                int selected = i;
                ToolStripMenuItem item = new ToolStripMenuItem(paletteNames[i]);
                item.Click += delegate { ViewSettings.Current.Palette = selected; PreferencesChanged(); };
                palettes.DropDownOpening += delegate { item.Checked = ViewSettings.Current.Palette == selected; };
                palettes.DropDownItems.Add(item);
            }
            _menu.Items.Add(palettes);
            ToolStripMenuItem intensity = new ToolStripMenuItem("Інтенсивність світла");
            int[] levels = { 0, 35, 80, 100 };
            string[] names = { "Без сяйва", "М'яке", "Виразне", "Максимальне" };
            for (int i = 0; i < levels.Length; i++)
            {
                int level = levels[i];
                ToolStripMenuItem item = new ToolStripMenuItem(names[i]);
                item.Click += delegate { ViewSettings.Current.Intensity = level; PreferencesChanged(); };
                intensity.DropDownOpening += delegate { item.Checked = ViewSettings.Current.Intensity == level; };
                intensity.DropDownItems.Add(item);
            }
            _menu.Items.Add(intensity);
            ToolStripMenuItem economy = new ToolStripMenuItem("Ощадний режим · автоматично від батареї");
            economy.CheckOnClick = true;
            economy.Checked = ViewSettings.Current.Economy;
            economy.CheckedChanged += delegate { ViewSettings.Current.Economy = economy.Checked; PreferencesChanged(); };
            _menu.Items.Add(economy);
        }
        private void PreferencesChanged()
        {
            if (_form is MainForm)
            {
                ViewSettings.Current.Save();
                ((MainForm)_form).ApplyDisplayOptions(false);
            }
            UpdateTimer();
            _form.Invalidate(true);
        }
        private void HandleKey(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D3 && _form is MainForm)
            {
                ((MainForm)_form).ApplyDisplayMode((int)e.KeyCode - (int)Keys.D1, true);
                e.Handled = e.SuppressKeyPress = true;
            }
            else if (e.Control && e.KeyCode == Keys.M)
            {
                _motion.Checked = !_motion.Checked;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        private void HandleVisibility(object sender, EventArgs e) { UpdateTimer(); }
        private void HandleDisposed(object sender, EventArgs e) { Dispose(); }
        private void UpdateTimer()
        {
            if (_disposed) return;
            _timer.Interval = SpectrumFx.EconomyActive ? 250 : 80;
            _timer.Enabled = SpectrumFx.MotionEnabled && ViewSettings.Current.Intensity > 0 && _form.Visible && _form.WindowState != FormWindowState.Minimized;
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
            _form.KeyDown -= HandleKey;
            _form.VisibleChanged -= HandleVisibility;
            _form.Resize -= HandleVisibility;
            _form.Disposed -= HandleDisposed;
            _menu.Dispose();
            
        }
    }
}
