using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class Theme
    {
        public static readonly Color Background = Color.FromArgb(11, 18, 24);
        public static readonly Color Panel = Color.FromArgb(22, 33, 42);
        public static readonly Color PanelHover = Color.FromArgb(24, 53, 63);
        public static readonly Color Border = Color.FromArgb(49, 69, 81);
        public static readonly Color Text = Color.FromArgb(237, 242, 250);
        public static readonly Color Muted = Color.FromArgb(164, 172, 196);
        public static readonly Color Cyan = Color.FromArgb(100, 235, 235);
        public static readonly Color Teal = Color.FromArgb(68, 182, 198);
        public static readonly Color Pink = Color.FromArgb(255, 111, 166);
        public static readonly Color Orange = Color.FromArgb(255, 184, 92);
        public static readonly Color Yellow = Color.FromArgb(245, 211, 92);
        public static readonly Color Green = Color.FromArgb(112, 229, 150);
        public static readonly Color Red = Color.FromArgb(255, 106, 116);

        public static Color ContextColor(long used)
        {
            return used >= 165000 ? Red : used >= 130000 ? Orange : Green;
        }
    }

    internal sealed class UsageHistorySample
    {
        public DateTime At;
        public long TotalTokens;
        public long ContextUsed;
        public long ContextLimit;
    }

    internal sealed class TokenBar : HoverCardControl
    {
        public TokenUsage Usage = new TokenUsage();
        public CostEstimate ApiValue = new CostEstimate();
        public TokenUsage TaskUsage;
        public string Trend = "";
        public float LayoutScale = 1f;

        public TokenBar()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            SpectrumFx.DrawTokens(e.Graphics, this);
        }

        private int P(int value) { return Math.Max(1, (int)Math.Round(value * LayoutScale)); }

        private Font FitFont(Graphics graphics, string text, Rectangle bounds, float maximum, float minimum)
        {
            for (float size = maximum; size >= minimum; size -= 0.5f)
            {
                Font candidate = new Font(Font.FontFamily, size, FontStyle.Bold);
                Size measured = TextRenderer.MeasureText(graphics, text, candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (measured.Width <= bounds.Width && measured.Height <= bounds.Height) return candidate;
                candidate.Dispose();
            }
            return new Font(Font.FontFamily, minimum, FontStyle.Bold);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class ContextChart : HoverCardControl
    {
        public long Used;
        public long Maximum;
        public string Trend = "";
        public Color Accent = Theme.Green;
        public IList<UsageHistorySample> History = new List<UsageHistorySample>();
        public bool IsActive;
        public float ViewYaw;
        public float ViewPitch;
        public float LayoutScale = 1f;

        public ContextChart()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AuroraUi.DrawContext(e.Graphics, this);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!SpectrumFx.MotionEnabled) return;
            ViewYaw = (e.X / (float)Math.Max(1, Width) - 0.5f) * 0.8f;
            ViewPitch = (e.Y / (float)Math.Max(1, Height) - 0.5f) * 0.5f;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            ViewYaw = ViewPitch = 0;
            Invalidate();
        }

        private int P(int value) { return Math.Max(1, (int)Math.Round(value * LayoutScale)); }

        private Font FitFont(Graphics graphics, string text, Rectangle bounds, float maximum, float minimum)
        {
            for (float size = maximum; size >= minimum; size -= 0.5f)
            {
                Font candidate = new Font(Font.FontFamily, size, FontStyle.Bold);
                Size measured = TextRenderer.MeasureText(graphics, text, candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (measured.Width <= bounds.Width && measured.Height <= bounds.Height) return candidate;
                candidate.Dispose();
            }
            return new Font(Font.FontFamily, minimum, FontStyle.Bold);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class MetricCard : HoverCardControl
    {
        private float _layoutScale = 1f;
        public string Caption { get; set; }
        public string Value { get; set; }
        public string Detail { get; set; }
        public string Secondary { get; set; }
        public int ProgressPercent { get; set; }
        public Color Accent { get; set; }
        public float LayoutScale { get { return _layoutScale; } set { _layoutScale = Math.Max(1f, value); } }

        public MetricCard()
        {
            Caption = "";
            Value = "—";
            Detail = "";
            Secondary = "";
            ProgressPercent = -1;
            Accent = Theme.Cyan;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            using (GraphicsPath path = Rounded(rect, P(8)))
            using (SolidBrush fill = new SolidBrush(Theme.Panel))
            using (Pen border = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                AuroraUi.Surface(e.Graphics, Rectangle.Round(path.GetBounds()), Theme.Teal, 12f * LayoutScale, this);
            }
            using (SolidBrush accent = new SolidBrush(Accent))
                e.Graphics.FillRectangle(accent, 0, P(10), P(3), Math.Max(P(8), Height - P(20)));
            Rectangle captionRect = new Rectangle(P(12), P(6), Width - P(18), P(16));
            Rectangle valueRect = new Rectangle(P(11), P(21), Width - P(17), P(27));
            using (Font captionFont = FitFont(e.Graphics, Caption ?? "", captionRect, Font.FontFamily, 7.5f * LayoutScale, 6f * LayoutScale, FontStyle.Regular))
                TextRenderer.DrawText(e.Graphics, Caption ?? "", captionFont, captionRect, Theme.Muted, TextFormatFlags.EndEllipsis);
            using (Font valueFont = FitFont(e.Graphics, Value ?? "—", valueRect, Font.FontFamily, 13f * LayoutScale, 8f * LayoutScale, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Value ?? "—", valueFont, valueRect, Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            int detailTop = P(51);
            if (ProgressPercent >= 0)
            {
                Rectangle track = new Rectangle(P(12), P(52), Math.Max(P(10), Width - P(24)), P(5));
                using (SolidBrush trackBrush = new SolidBrush(Theme.Border)) e.Graphics.FillRectangle(trackBrush, track);
                int fillWidth = (int)Math.Round(track.Width * Math.Max(0, Math.Min(100, ProgressPercent)) / 100d);
                if (fillWidth > 0)
                    using (SolidBrush fillBrush = new SolidBrush(Accent)) e.Graphics.FillRectangle(fillBrush, track.X, track.Y, fillWidth, track.Height);
                detailTop = P(61);
            }
            Rectangle detailRect = new Rectangle(P(12), detailTop, Width - P(24), P(18));
            using (Font detailFont = FitFont(e.Graphics, Detail ?? "", detailRect, Font.FontFamily, 7.5f * LayoutScale, 6f * LayoutScale, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Detail ?? "", detailFont, detailRect, Accent, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            if (Height >= P(91) && !String.IsNullOrWhiteSpace(Secondary))
            {
                Rectangle secondaryRect = new Rectangle(P(12), detailTop + P(20), Width - P(24), P(18));
                using (Font secondaryFont = FitFont(e.Graphics, Secondary, secondaryRect, Font.FontFamily, 7f * LayoutScale, 5.75f * LayoutScale, FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, Secondary, secondaryFont, secondaryRect, Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        private int P(int value)
        {
            return Math.Max(1, (int)Math.Round(value * LayoutScale));
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Font FitFont(Graphics graphics, string text, Rectangle bounds, FontFamily family, float maximum, float minimum, FontStyle style)
        {
            for (float size = maximum; size >= minimum; size -= 0.5f)
            {
                Font candidate = new Font(family, size, style);
                Size measured = TextRenderer.MeasureText(graphics, text, candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (measured.Width <= bounds.Width && measured.Height <= bounds.Height) return candidate;
                candidate.Dispose();
            }
            return new Font(family, minimum, style);
        }
    }

    internal sealed class TodayStrip : HoverCardControl
    {
        public TokenUsage Usage = new TokenUsage();
        public CostEstimate ApiValue = new CostEstimate();
        public TokenUsage TaskUsage;
        public int Tasks;
        public int RunningSessions;
        public RateLimitView PrimaryLimit = new RateLimitView();
        public RateLimitView SecondaryLimit = new RateLimitView();
        public float LayoutScale = 1f;

        public TodayStrip()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath path = Rounded(rect, P(9)))
            using (LinearGradientBrush fill = new LinearGradientBrush(rect, Color.FromArgb(27, 37, 55), Color.FromArgb(19, 27, 41), 0f))
            using (Pen border = new Pen(Theme.Border))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                AuroraUi.Surface(e.Graphics, Rectangle.Round(path.GetBounds()), Theme.Teal, 12f * LayoutScale, this);
            }
            bool compact = Width < P(500);
            int topWidth = Math.Max(P(70), (Width - P(20)) / 3);
            if (compact)
            {
                int halfWidth = (Width - P(28)) / 2;
                DrawHeadline(e.Graphics, L10n.F("UI.ActiveCount", RunningSessions), new Rectangle(P(10), P(4), halfWidth, P(16)), RunningSessions > 0 ? Theme.Green : Theme.Muted);
                DrawHeadline(e.Graphics, L10n.F("UI.TodayTotal", FormatUtil.Tokens(Usage.Total)), new Rectangle(P(18) + halfWidth, P(4), halfWidth, P(16)), Theme.Cyan);
                DrawHeadline(e.Graphics, "API ≈ " + FormatUtil.ApiValue(ApiValue), new Rectangle(P(10), P(21), Width - P(20), P(16)), Theme.Green);
            }
            else
            {
                DrawHeadline(e.Graphics, L10n.F("UI.ActiveCount", RunningSessions), new Rectangle(P(10), P(9), topWidth, P(20)), RunningSessions > 0 ? Theme.Green : Theme.Muted);
                DrawHeadline(e.Graphics, L10n.F("UI.TodayTotal", FormatUtil.Tokens(Usage.Total)), new Rectangle(P(10) + topWidth, P(9), topWidth, P(20)), Theme.Cyan);
                DrawHeadline(e.Graphics, "API ≈ " + FormatUtil.ApiValue(ApiValue), new Rectangle(P(10) + topWidth * 2, P(9), Width - P(20) - topWidth * 2, P(20)), Theme.Green);
            }

            List<RateLimitView> limits = new List<RateLimitView>();
            if (PrimaryLimit != null && PrimaryLimit.Available) limits.Add(PrimaryLimit);
            if (SecondaryLimit != null && SecondaryLimit.Available) limits.Add(SecondaryLimit);
            if (limits.Count == 0)
            {
                using (Font font = new Font(Font.FontFamily, 6.5f * LayoutScale, FontStyle.Regular))
                    TextRenderer.DrawText(e.Graphics, L10n.T("UI.LimitUnavailable"), font, new Rectangle(P(10), compact ? P(39) : P(34), Width - P(20), P(16)), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            else
            {
                int limitWidth = (Width - P(20) - (limits.Count > 1 ? P(8) : 0)) / limits.Count;
                for (int i = 0; i < limits.Count; i++)
                {
                    RateLimitView limit = limits[i];
                    Rectangle bounds = new Rectangle(P(10) + i * (limitWidth + P(8)), compact ? P(39) : P(34), limitWidth, compact ? P(16) : P(17));
                    Color color = limit.UsedPercent >= 90 ? Theme.Red : limit.UsedPercent >= 75 ? Theme.Orange : Theme.Muted;
                    string text = compact ? CompactLimitText(limit) : LimitText(limit);
                    using (Font font = FitFont(e.Graphics, text, bounds, compact ? 7f * LayoutScale : 6.75f * LayoutScale, 5.5f * LayoutScale, FontStyle.Bold))
                        TextRenderer.DrawText(e.Graphics, text, font, bounds, color, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                }
            }
        }

        private void DrawHeadline(Graphics graphics, string text, Rectangle bounds, Color color)
        {
            using (Font font = FitFont(graphics, text, bounds, 8f * LayoutScale, 5.75f * LayoutScale, FontStyle.Bold))
                TextRenderer.DrawText(graphics, text, font, bounds, color, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }

        private static string LimitText(RateLimitView limit)
        {
            string window = limit.WindowMinutes <= 360 ? L10n.T("UI.LimitFiveHours") : limit.WindowMinutes >= 9000 ? L10n.T("UI.LimitWeek") : L10n.F("UI.LimitMinutes", limit.WindowMinutes);
            string reset = "";
            if (limit.ResetsLocal.HasValue && limit.WindowMinutes <= 360)
            {
                TimeSpan remaining = limit.ResetsLocal.Value - DateTime.Now;
                if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;
                reset = remaining.TotalHours >= 1
                    ? L10n.F("UI.ResetHours", (int)remaining.TotalHours, remaining.Minutes)
                    : L10n.F("UI.ResetMinutes", Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes)));
            }
            else if (limit.ResetsLocal.HasValue)
            {
                reset = L10n.F("UI.ResetDate", limit.ResetsLocal.Value.ToString("d MMM", L10n.Culture));
            }
            return L10n.F("UI.LimitRemaining", window, 100 - limit.UsedPercent, reset);
        }

        private static string CompactLimitText(RateLimitView limit)
        {
            string window = limit.WindowMinutes <= 360 ? L10n.T("UI.LimitFiveHoursShort") : limit.WindowMinutes >= 9000 ? L10n.T("UI.LimitWeek") : L10n.F("UI.LimitMinutesShort", limit.WindowMinutes);
            return window + " " + (100 - limit.UsedPercent) + "%";
        }

        public string LimitTooltip()
        {
            List<string> details = new List<string>();
            if (PrimaryLimit != null && PrimaryLimit.Available) details.Add(LimitText(PrimaryLimit));
            if (SecondaryLimit != null && SecondaryLimit.Available) details.Add(LimitText(SecondaryLimit));
            return String.Join("  |  ", details.ToArray());
        }

        private int P(int value)
        {
            return Math.Max(1, (int)Math.Round(value * LayoutScale));
        }

        private Font FitFont(Graphics graphics, string text, Rectangle bounds, float maximum, float minimum, FontStyle style)
        {
            for (float size = maximum; size >= minimum; size -= 0.5f)
            {
                Font candidate = new Font(Font.FontFamily, size, style);
                Size measured = TextRenderer.MeasureText(graphics, text, candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                if (measured.Width <= bounds.Width && measured.Height <= bounds.Height) return candidate;
                candidate.Dispose();
            }
            return new Font(Font.FontFamily, minimum, style);
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class AgentMiniCard : HoverCardControl
    {
        public SessionView Session;
        public int AgentNumber;
        public bool Selected;
        public float LayoutScale = 1f;

        public AgentMiniCard()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            BackColor = Theme.Background;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = Session == null || (!Session.IsRunning && !Session.IsStale) ? Theme.Muted : Session.IsStale ? Theme.Orange : Session.Accent;
            Rectangle rect = new Rectangle(P(1), P(1), Math.Max(1, Width - P(3)), Math.Max(1, Height - P(3)));
            using (GraphicsPath path = Rounded(rect, P(7)))
            using (SolidBrush fill = new SolidBrush(Selected ? Theme.PanelHover : Theme.Panel))
            using (Pen border = new Pen(Selected ? accent : Theme.Border, Selected ? 2f : 1f))
            {
                e.Graphics.FillPath(fill, path);
                e.Graphics.DrawPath(border, path);
                AuroraUi.Surface(e.Graphics, Rectangle.Round(path.GetBounds()), accent, 12f * LayoutScale, this);
            }
            if (Selected)
            {
                using (SolidBrush active = new SolidBrush(accent))
                    e.Graphics.FillRectangle(active, P(7), Height - P(4), Math.Max(P(8), Width - P(14)), P(3));
            }
            if (Session == null) return;
            TokenUsage task = Session.CurrentOrLastTask == null ? new TokenUsage() : Session.CurrentOrLastTask.Usage;
            bool narrow = Width < P(160);
            string agent = narrow ? "#" + AgentNumber : L10n.F("UI.AgentNumber", AgentNumber);
            int contextPercent = FormatUtil.ContextPercent(Session.CurrentContext, Session.ModelContextWindow);
            Color contextColor = contextPercent < 0 ? Theme.Muted : Theme.ContextColor(Session.CurrentContext.Total);
            string context;
            Font fittedContextFont;
            Rectangle contextRect, statusRect;
            GetContextLayout(e.Graphics, out context, out fittedContextFont, out contextRect, out statusRect);
            using (Font contextFont = fittedContextFont)
            {
                ContextSculpture.DrawToken(e.Graphics, new Rectangle(P(7), P(3), P(20), P(20)), accent, (Session != null && Session.IsRunning ? SpectrumFx.Phase * .1 : 0) + AgentNumber);
                using (Font statusFont = new Font(Font.FontFamily, 6f * LayoutScale, FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, agent, statusFont, statusRect, Session.IsRunning ? Theme.Green : Theme.Orange, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, context, contextFont, contextRect, contextColor, TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            using (Font titleFont = new Font(Font.FontFamily, 8f * LayoutScale, FontStyle.Bold))
                TextRenderer.DrawText(e.Graphics, Session.DisplayTitle, titleFont, new Rectangle(P(9), P(23), Width - P(18), P(19)), Theme.Text, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            using (Font metaFont = new Font(Font.FontFamily, 6.5f * LayoutScale))
            {
                string summary = L10n.F("UI.LastActivityShort", FormatUtil.RelativeAgeShort(Session.LastActivityLocal));
                string model = FormatUtil.CompactModelName(Session.CurrentModel);
                Rectangle row = new Rectangle(P(9), P(44), Width - P(18), P(16));
                Size modelSize = TextRenderer.MeasureText(e.Graphics, model, metaFont, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                int modelWidth = Math.Min(row.Width, modelSize.Width + P(3));
                int summaryWidth = row.Width - modelWidth - P(6);
                if (summaryWidth >= P(45))
                    TextRenderer.DrawText(e.Graphics, summary, metaFont, new Rectangle(row.X, row.Y, summaryWidth, row.Height), Theme.Muted, TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(e.Graphics, model, metaFont, new Rectangle(row.Right - modelWidth, row.Y, modelWidth, row.Height), Theme.Muted, TextFormatFlags.Right | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
        }

        private int P(int value)
        {
            return Math.Max(1, (int)Math.Round(value * LayoutScale));
        }

        private Font FitContextFont(Graphics graphics, string text, int width, float maximum)
        {
            for (float size = maximum; size >= 4.5f; size -= .25f)
            {
                Font candidate = new Font(Font.FontFamily, size * LayoutScale, FontStyle.Bold);
                int measured = TextRenderer.MeasureText(graphics, text, candidate,
                    new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                if (measured <= width - P(2)) return candidate;
                candidate.Dispose();
            }
            return new Font(Font.FontFamily, 4.5f * LayoutScale, FontStyle.Bold);
        }

        // The painter and render checks share the same measured geometry. The caller
        // owns and disposes the returned font.
        internal void GetContextLayout(Graphics graphics, out string text, out Font font,
            out Rectangle contextBounds, out Rectangle agentBounds)
        {
            bool narrow = Width < P(160);
            int percent = Session == null ? -1 : FormatUtil.ContextPercent(Session.CurrentContext, Session.ModelContextWindow);
            text = L10n.T("UI.ContextNarrow") + (percent < 0 ? " —" : " " + percent + "%");
            int maximumWidth = Math.Max(P(40), Width - P(narrow ? 58 : 112));
            font = FitContextFont(graphics, text, maximumWidth, narrow ? 5.75f : 6.25f);
            int measured = TextRenderer.MeasureText(graphics, text, font,
                new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            int width = Math.Min(maximumWidth, Math.Max(P(48), measured + P(2)));
            int left = Width - width - P(8);
            contextBounds = new Rectangle(left, P(4), width, P(18));
            agentBounds = new Rectangle(P(28), P(5), Math.Max(P(18), left - P(31)), P(16));
        }

        private static GraphicsPath Rounded(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static string CompactTokens(long value)
        {
            return FormatUtil.Tokens(value);
        }
    }

    internal sealed class MainForm : Form
    {
        private const int ResizeGrip = 10;
        private readonly AppSettings _settings;
        private readonly SessionEngine _engine;
        private readonly ToolTip _tips = new ToolTip();
        private MonitorSnapshot _snapshot = new MonitorSnapshot();
        private bool _daysMode;
        private string _selectedSessionId = "";
        private string _sessionSignature = "";
        private string _daysSignature = "";
        private bool _refreshingLanguage;
        private string _activityMessageKey = "";

        private PictureBox _brandIcon;
        private Label _appTitle;
        private Label _activity;
        private Button _refresh;
        private Button _pin;
        private Button _minimize;
        private Button _close;
        private Button _liveTab;
        private Button _daysTab;
        private TodayStrip _today;
        private Panel _livePanel;
        private Panel _daysPanel;
        private Panel _activeStrip;
        private readonly List<AgentMiniCard> _agentCards = new List<AgentMiniCard>();
        private readonly Dictionary<string, List<UsageHistorySample>> _tokenRateSamples = new Dictionary<string, List<UsageHistorySample>>(StringComparer.OrdinalIgnoreCase);
        private ComboBox _sessions;
        private Button _previous;
        private Button _next;
        private Panel _hero;
        private Label _badge;
        private Label _taskTitle;
        private Label _repository;
        private Label _taskTokens;
        private Label _taskCaption;
        private Label _taskTime;
        private TokenBar _tokenBar;
        private ContextChart _contextChart;
        private readonly UsageOverview _overview = new UsageOverview();
        private SpectrumMotion _displayMotion;
        internal bool IsExpandedDisplay { get { return ViewSettings.Current.Mode != 0 && ClientSize.Width >= 820 && ClientSize.Height >= 700; } }
        private DataGridView _days;
        private Label _daysHeader;
        private Button _loadOlderDays;
        private Panel _dayDetail;
        private Label _dayTitle;
        private Label _dayMeta;
        private Label _dayValue;
        private Label _footer;

        public MainForm(string[] args)
        {
            L10n.SetLanguage(ViewSettings.Current.Language);
            _settings = AppSettings.Load();
            _engine = new SessionEngine(args);
            InitializeWindow();
            BuildControls();
            _settings.Apply(this);
            TopMost = _settings.TopMost;
            UpdatePinButton();
            LayoutControls();
            _engine.SnapshotReady += EngineSnapshotReady;
            ApplySnapshot(_engine.Latest);
            _displayMotion = new SpectrumMotion(this, _contextChart, _activeStrip, _today);
            _contextChart.AccessibleDescription = L10n.T("UI.ContextTip") + " " + L10n.T("Appearance.ContextHelp");
            _footer.Cursor = Cursors.Hand;
            _footer.Click += delegate { _displayMotion.ShowMenu(_footer); };
            _tips.SetToolTip(_footer, L10n.T("UI.AppearanceTip"));
            ApplyDisplayOptions(!System.IO.File.Exists(ViewSettings.FilePath));
            L10n.LanguageChanged += OnLanguageChanged;
        }

        private void InitializeWindow()
        {
            Text = "CODEX TOKEN MONITOR";
            BackColor = Theme.Background;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            MinimumSize = new Size(400, 520);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.None;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);
            UpdateStyles();
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application; }
            catch { Icon = SystemIcons.Application; }
        }

        private void BuildControls()
        {
            _brandIcon = new PictureBox
            {
                BackColor = Theme.Background,
                Image = AuroraUi.CreateBrand(96),
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false
            };
            _appTitle = MakeLabel("CODEX TOKEN MONITOR", 10f, FontStyle.Bold, Theme.Text);
            _activity = MakeLabel(L10n.T("UI.Searching"), 8f, FontStyle.Regular, Theme.Muted);
            _activity.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            _refresh = MakeButton("↻");
            _pin = MakeButton(L10n.T("UI.PinOn"));
            _minimize = MakeButton("—");
            _close = MakeButton("×");
            _liveTab = MakeButton(L10n.T("UI.LiveTab"));
            _daysTab = MakeButton(L10n.T("UI.AnalyticsTab"));
            ((AuroraButton)_liveTab).Accent = Theme.Cyan;
            ((AuroraButton)_daysTab).Accent = SpectrumFx.Gold;
            _today = new TodayStrip { Font = Font };
            _livePanel = new Panel { BackColor = Theme.Background };
            _livePanel.Controls.Add(_overview);
            _daysPanel = new Panel { BackColor = Theme.Background, Visible = false };
            _footer = MakeLabel(L10n.T("UI.LocalOnly"), 8f, FontStyle.Regular, Theme.Muted);

            Controls.AddRange(new Control[] { _brandIcon, _appTitle, _activity, _refresh, _pin, _minimize, _close, _liveTab, _daysTab, _today, _livePanel, _daysPanel, _footer });

            _refresh.Click += delegate { _activityMessageKey = "UI.Refreshing"; _activity.Text = L10n.T(_activityMessageKey); _engine.RefreshNow(); };
            _pin.Click += delegate { TopMost = !TopMost; UpdatePinButton(); LayoutControls(); };
            _minimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            _close.Click += delegate { Close(); };
            _appTitle.MouseDown += BeginWindowDrag;
            _brandIcon.MouseDown += BeginWindowDrag;
            _activity.MouseDown += BeginWindowDrag;
            _liveTab.Click += delegate { SetMode(false); };
            _daysTab.Click += delegate { SetView(1); };
            _tips.SetToolTip(_refresh, L10n.T("UI.RefreshTip"));
            _tips.SetToolTip(_pin, L10n.T("UI.PinTip"));
            _tips.SetToolTip(_minimize, L10n.T("UI.MinimizeTip"));
            _tips.SetToolTip(_close, L10n.T("UI.CloseTip"));
            _today.AccessibleDescription = L10n.F("UI.SubscriptionTip", PriceCatalog.RateDate);
            _close.ForeColor = Theme.Red;
            _close.FlatAppearance.BorderColor = Theme.Red;
            _pin.Font = new Font("Segoe UI", 7f, FontStyle.Bold);

            BuildLivePanel();
            BuildDaysPanel();
            AuroraUi.StyleGrid(_days);
            SetMode(false);
        }

        private void BuildLivePanel()
        {
            _activeStrip = new Panel
            {
                BackColor = Theme.Background,
                AutoScroll = true
            };
            _sessions = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 30,
                BackColor = Theme.Panel,
                ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9f)
            };
            _sessions.DrawItem += DrawSessionItem;
            _sessions.SelectedIndexChanged += delegate
            {
                SessionView selected = _sessions.SelectedItem as SessionView;
                if (selected != null) _selectedSessionId = selected.Id;
                UpdateAgentCardSelection();
                UpdateLiveDetail();
            };
            _previous = MakeButton("‹");
            _next = MakeButton("›");
            _previous.Click += delegate { MoveSession(-1); };
            _next.Click += delegate { MoveSession(1); };

            _hero = new AuroraPanel { BackColor = Theme.Panel, Accent = Theme.Orange, ShowToken = true };
            
            _badge = MakeLabel(L10n.T("UI.Waiting"), 8f, FontStyle.Bold, Theme.Muted);
            _taskTitle = MakeLabel(L10n.T("UI.NoSessions"), 11f, FontStyle.Bold, Theme.Text);
            _taskTitle.AutoEllipsis = true;
            _repository = MakeLabel("", 8f, FontStyle.Regular, Theme.Muted);
            _repository.AutoEllipsis = true;
            _taskTokens = MakeLabel("—", 20f, FontStyle.Bold, Theme.Cyan);
            _taskCaption = MakeLabel(L10n.T("UI.TaskTokens"), 8f, FontStyle.Bold, Theme.Muted);
            _taskTokens.Visible = false;
            _taskCaption.Visible = false;
            _taskTime = MakeLabel("", 8f, FontStyle.Regular, Theme.Muted);
            _taskTime.TextAlign = ContentAlignment.MiddleRight;
            _hero.Controls.AddRange(new Control[] { _badge, _taskTitle, _repository, _taskTokens, _taskCaption, _taskTime });

            _badge.AutoEllipsis = true;
            _tokenBar = new TokenBar { Font = Font };
            _contextChart = new ContextChart { Font = Font };
            _tokenBar.AccessibleDescription = L10n.T("UI.TrendTip");
            _contextChart.AccessibleDescription = L10n.T("UI.ContextTip");
            _livePanel.Controls.AddRange(new Control[] { _activeStrip, _sessions, _previous, _next, _hero, _tokenBar, _contextChart });
        }

        private void BuildDaysPanel()
        {
            _daysHeader = MakeLabel(L10n.T("UI.DaysHistory"), 8f, FontStyle.Bold, Theme.Muted);
            _daysHeader.TextAlign = ContentAlignment.MiddleLeft;
            _loadOlderDays = MakeButton(L10n.T("UI.Older"));
            _loadOlderDays.Font = new Font("Segoe UI", 7f, FontStyle.Bold);
            _loadOlderDays.Click += delegate { BeginLoadOlder(); };
            _days = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoGenerateColumns = false,
                BackgroundColor = Theme.Background,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                ColumnHeadersHeight = 26,
                EnableHeadersVisualStyles = false,
                GridColor = Theme.Border,
                MultiSelect = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                RowTemplate = { Height = 36 },
                ScrollBars = ScrollBars.Vertical,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _days.DefaultCellStyle.BackColor = Theme.Panel;
            _days.DefaultCellStyle.ForeColor = Theme.Text;
            _days.DefaultCellStyle.SelectionBackColor = Theme.PanelHover;
            _days.DefaultCellStyle.SelectionForeColor = Theme.Text;
            _days.DefaultCellStyle.Font = new Font("Segoe UI", 8.5f);
            _days.ColumnHeadersDefaultCellStyle.BackColor = Theme.Background;
            _days.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
            _days.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            _days.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _days.DefaultCellStyle.WrapMode = DataGridViewTriState.False;
            _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L10n.T("UI.DayColumn"), Width = 82 });
            _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L10n.T("UI.TasksColumn"), Width = 52, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L10n.T("UI.TokensColumn"), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 72, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } });
            _days.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = L10n.T("UI.ApiColumn"), Width = 88, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, ForeColor = Theme.Green } });
            foreach (DataGridViewColumn column in _days.Columns)
                column.SortMode = DataGridViewColumnSortMode.NotSortable;
            _days.SelectionChanged += delegate { UpdateDayDetail(); };

            _dayDetail = new AuroraPanel { BackColor = Theme.Panel, Accent = SpectrumFx.Gold };
            
            _dayTitle = MakeLabel(L10n.T("UI.ChooseDay"), 9f, FontStyle.Bold, Theme.Text);
            _dayMeta = MakeLabel("", 8f, FontStyle.Regular, Theme.Muted);
            _dayMeta.AutoEllipsis = true;
            _dayValue = MakeLabel("", 9f, FontStyle.Bold, Theme.Green);
            _dayValue.TextAlign = ContentAlignment.MiddleRight;
            _dayDetail.Controls.AddRange(new Control[] { _dayTitle, _dayMeta, _dayValue });
            _daysPanel.Controls.AddRange(new Control[] { _daysHeader, _loadOlderDays, _days, _dayDetail });
        }

        private static Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                AutoSize = false,
                BackColor = Color.Transparent,
                ForeColor = color,
                Font = new Font("Segoe UI", size, style),
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft
            };
        }

        private static Button MakeButton(string text)
        {
            Button button = new AuroraButton
            {
                BackColor = Theme.Panel,
                ForeColor = Theme.Muted,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Text = text,
                TabStop = true,
                Padding = new Padding(0),
                UseVisualStyleBackColor = false
            };
            button.FlatAppearance.BorderColor = Theme.Border;
            button.FlatAppearance.MouseOverBackColor = Theme.PanelHover;
            return button;
        }

        private void LayoutControls()
        {
            int width = ClientSize.Width;
            int height = ClientSize.Height;
            float scale = GetLayoutScale();
            ApplyResponsiveFonts(scale);
            SetResponsiveFont(_liveTab, 7f, FontStyle.Bold, scale);
            SetResponsiveFont(_daysTab, 7f, FontStyle.Bold, scale);
            int margin = S(14, scale);
            _brandIcon.SetBounds(margin, S(2, scale), S(24, scale), S(24, scale));
            int titleLeft = S(44, scale);
            UpdatePinButton();
            int pinWidth = Math.Max(S(56, scale), TextRenderer.MeasureText(_pin.Text, _pin.Font,
                new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + S(18, scale));
            int pinLeft = width - S(84, scale) - pinWidth;
            int refreshLeft = pinLeft - S(36, scale);
            _appTitle.SetBounds(titleLeft, S(2, scale), Math.Max(S(100, scale), refreshLeft - S(8, scale) - titleLeft), S(24, scale));
            _activity.SetBounds(margin, S(26, scale), refreshLeft - S(8, scale) - margin, S(21, scale));
            _close.SetBounds(width - S(43, scale), S(9, scale), S(29, scale), S(27, scale));
            _minimize.SetBounds(width - S(78, scale), S(9, scale), S(29, scale), S(27, scale));
            _pin.SetBounds(pinLeft, S(9, scale), pinWidth, S(27, scale));
            _refresh.SetBounds(refreshLeft, S(9, scale), S(30, scale), S(27, scale));
            bool narrowNav = width < 700;
            _appTitle.Text = width < 700 ? "CODEX TOKEN" : "CODEX TOKEN MONITOR";
            FitLabel(_appTitle, 10f * scale, 7f * scale, FontStyle.Bold);
            FitLabel(_activity, 8f * scale, 6f * scale, FontStyle.Bold);
            UpdatePinButton();
            _liveTab.Text = L10n.T("UI.LiveTab");
            _daysTab.Text = L10n.T("UI.AnalyticsTab");
            int tabsLeft = margin;
            int gap = S(narrowNav ? 2 : 4, scale);
            int liveWidth = S(76, scale);
            int daysWidth = S(116, scale);
            int navX = tabsLeft;
            _liveTab.SetBounds(navX, S(48, scale), liveWidth, S(29, scale)); navX += liveWidth + gap;
            _daysTab.SetBounds(navX, S(48, scale), daysWidth, S(29, scale)); navX += daysWidth + gap;
            _today.SetBounds(margin, S(84, scale), width - margin * 2, S(59, scale));
            int contentTop = S(151, scale);
            int footerHeight = S(30, scale);
            int footerGap = S(4, scale);
            int contentHeight = Math.Max(330, height - contentTop - footerHeight - footerGap - ResizeGrip);
            _livePanel.SetBounds(margin, contentTop, width - margin * 2, contentHeight);
            _daysPanel.SetBounds(margin, contentTop, width - margin * 2, contentHeight);
            _footer.SetBounds(margin, height - footerHeight - ResizeGrip, width - margin * 2, footerHeight);
            _footer.TextAlign = ContentAlignment.MiddleCenter;
            LayoutLive();
            LayoutDays();
            FitLabelHeights(_hero);
            FitLabelHeights(_dayDetail);
        }

        private void LayoutLive()
        {
            int width = _livePanel.ClientSize.Width;
            int height = _livePanel.ClientSize.Height;
            float scale = GetLayoutScale();
            _overview.Visible = IsExpandedDisplay;
            if (IsExpandedDisplay) { LayoutExpandedLive(width, height, scale); return; }
            int activeCount = _agentCards.Count;
            int stripHeight = S(activeCount > 4 ? 80 : 68, scale);
            _activeStrip.SetBounds(0, 0, width, stripHeight);
            LayoutAgentCards(scale);
            _sessions.Visible = false;
            _previous.Visible = false;
            _next.Visible = false;
            int heroTop = stripHeight + S(5, scale);
            _hero.SetBounds(0, heroTop, width, S(58, scale));
            int heroInset = width >= 600 ? S(48, scale) : S(12, scale);
            _badge.SetBounds(heroInset, S(5, scale), width - heroInset - S(12, scale), S(17, scale));
            _taskTitle.SetBounds(heroInset, S(21, scale), width - heroInset - S(12, scale), S(20, scale));
            
            _repository.SetBounds(heroInset, S(40, scale), Math.Max(S(80, scale), width - heroInset - S(142, scale)), S(17, scale));
            _taskTime.SetBounds(width - S(128, scale), S(39, scale), S(116, scale), S(18, scale));
            int cardsTop = heroTop + S(65, scale);
            int gap = S(7, scale);
            int tokenBarHeight = S(66, scale);
            _tokenBar.LayoutScale = scale;
            _tokenBar.SetBounds(0, cardsTop, width, tokenBarHeight);
            int contextTop = cardsTop + tokenBarHeight + gap;
            _contextChart.LayoutScale = scale;
            _contextChart.SetBounds(0, contextTop, width, Math.Max(S(96, scale), height - contextTop - S(4, scale)));
            FitLiveText();
        }

        internal void ApplyDisplayMode(int mode, bool persist)
        {
            ViewSettings.Current.Mode = Math.Max(0, Math.Min(2, mode));
            if (persist) ViewSettings.Current.Save();
            ApplyDisplayOptions(true);
        }
        internal void ApplyDisplayOptions(bool resize)
        {
            if (_livePanel == null) return;
            if (resize)
            {
                Size preferred = ViewSettings.Current.Mode == 0 ? new Size(420, 620)
                    : ViewSettings.Current.Mode == 1 ? new Size(960, 820) : new Size(1240, 900);
                Rectangle work = Screen.FromControl(this).WorkingArea;
                Size = new Size(Math.Min(preferred.Width, work.Width - 16), Math.Min(preferred.Height, work.Height - 16));
                if (Visible) Location = new Point(Math.Max(work.Left, Math.Min(Left, work.Right - Width)), Math.Max(work.Top, Math.Min(Top, work.Bottom - Height)));
            }
            AuroraUi.StyleGrid(_days);
            LayoutControls();
            Invalidate(true);
        }
        private void LayoutExpandedLive(int width, int height, float scale)
        {
            int gap = S(8, scale);
            _activeStrip.SetBounds(0, 0, width, S(_agentCards.Count > 4 ? 80 : 70, scale));
            LayoutAgentCards(scale);
            _sessions.Visible = _previous.Visible = _next.Visible = false;
            int y = _activeStrip.Bottom + gap;
            _hero.SetBounds(0, y, width, S(100, scale));
            int inset = S(48, scale);
            _badge.SetBounds(inset, S(5, scale), width - inset - S(14, scale), S(18, scale));
            _taskTitle.SetBounds(inset, S(24, scale), width - inset - S(14, scale), S(42, scale));
            _repository.SetBounds(inset, S(68, scale), width - inset - S(150, scale), S(28, scale));
            _taskTime.SetBounds(width - S(134, scale), S(68, scale), S(120, scale), S(28, scale));
            SetResponsiveFont(_taskTitle, ViewSettings.Current.Mode == 2 ? 15 : 13, FontStyle.Bold, scale);
            _taskTitle.AutoEllipsis = true;
            y = _hero.Bottom + gap;
            _overview.LayoutScale = scale;
            _overview.SetBounds(0, y, width, S(90, scale));
            y = _overview.Bottom + gap;
            int contextWidth = (width - gap) * 62 / 100;
            int bodyHeight = Math.Max(S(120, scale), height - y - S(4, scale));
            _contextChart.LayoutScale = scale;
            _contextChart.SetBounds(0, y, contextWidth, bodyHeight);
            _tokenBar.LayoutScale = scale;
            _tokenBar.SetBounds(contextWidth + gap, y, width - contextWidth - gap, bodyHeight - S(8, scale));
        }
        private void LayoutAgentCards(float scale)
        {
            if (_activeStrip == null) return;
            int count = _agentCards.Count;
            if (count == 0)
            {
                _activeStrip.AutoScrollMinSize = Size.Empty;
                return;
            }
            int gap = S(count > 4 ? 4 : 6, scale);
            int visibleColumns = Math.Min(5, count);
            int cardWidth = Math.Max(S(110, scale), (_activeStrip.ClientSize.Width - gap * (visibleColumns - 1) - S(2, scale)) / visibleColumns);
            int x = 0;
            foreach (AgentMiniCard card in _agentCards)
            {
                card.LayoutScale = scale;
                card.SetBounds(x, 0, cardWidth, S(64, scale));
                x += cardWidth + gap;
            }
            _activeStrip.AutoScrollMinSize = new Size(Math.Max(0, x - gap), 0);
        }

        private void RebuildAgentTabs()
        {
            List<SessionView> active = _snapshot.Sessions.Where(s => s.IsRunning).ToList();
            DateTime sampleTime = SnapshotTime();
            foreach (SessionView sample in _snapshot.Sessions) RecordTokenSample(sample, sampleTime);
            if (active.Count == 0 && _snapshot.Sessions.Count > 0) active.Add(_snapshot.Sessions[0]);
            string existing = String.Join("|", _agentCards.Select(c => c.Session == null ? "" : c.Session.Id));
            string wanted = String.Join("|", active.Select(s => s.Id));
            if (existing != wanted)
            {
                foreach (AgentMiniCard old in _agentCards)
                {
                    _activeStrip.Controls.Remove(old);
                    old.Dispose();
                }
                _agentCards.Clear();
                for (int i = 0; i < active.Count; i++)
                {
                    AgentMiniCard card = new AgentMiniCard { Font = Font, Session = active[i], AgentNumber = i + 1 };
                    card.Click += AgentTabClicked;
                    card.AccessibleDescription = AgentToolTip(active[i]);
                    _agentCards.Add(card);
                    _activeStrip.Controls.Add(card);
                }
            }
            else
            {
                for (int i = 0; i < active.Count; i++)
                {
                    _agentCards[i].Session = active[i];
                    _agentCards[i].AgentNumber = i + 1;
                    _agentCards[i].AccessibleDescription = AgentToolTip(active[i]);
                }
            }
            if (active.Count > 0 && !active.Any(s => s.Id == _selectedSessionId)) _selectedSessionId = active[0].Id;
            UpdateAgentCardSelection();
            LayoutLive();
        }

        private void AgentTabClicked(object sender, EventArgs e)
        {
            AgentMiniCard card = sender as AgentMiniCard;
            if (card == null || card.Session == null) return;
            _selectedSessionId = card.Session.Id;
            for (int i = 0; i < _sessions.Items.Count; i++)
            {
                SessionView item = _sessions.Items[i] as SessionView;
                if (item != null && item.Id == _selectedSessionId) { _sessions.SelectedIndex = i; break; }
            }
            UpdateAgentCardSelection();
            UpdateLiveDetail();
        }

        private void UpdateAgentCardSelection()
        {
            foreach (AgentMiniCard card in _agentCards)
            {
                card.Selected = card.Session != null && card.Session.Id == _selectedSessionId;
                card.Invalidate();
            }
        }

        private void LayoutDays()
        {
            int width = _daysPanel.ClientSize.Width;
            int height = _daysPanel.ClientSize.Height;
            int detailHeight = 76;
            int olderWidth = 94;
            foreach (string key in new[] { "UI.Older", "UI.Loading", "UI.AllLoaded" })
                olderWidth = Math.Max(olderWidth, TextRenderer.MeasureText(L10n.T(key), _loadOlderDays.Font,
                    new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 18);
            _daysHeader.SetBounds(6, 3, Math.Max(100, width - olderWidth - 13), 29);
            _loadOlderDays.SetBounds(width - olderWidth, 3, olderWidth, 29);
            _loadOlderDays.BringToFront();
            _days.SetBounds(0, 40, width, Math.Max(170, height - 40 - detailHeight - 7));
            FitAnalyticsColumns();
            _dayDetail.SetBounds(0, height - detailHeight - 4, width, detailHeight);
            _dayTitle.SetBounds(10, 7, width - 130, 24);
            _dayMeta.SetBounds(10, 37, width - 20, 25);
            _dayValue.SetBounds(width - 120, 7, 110, 24);
        }

        private void FitAnalyticsColumns()
        {
            if (_days.ClientSize.Width <= 0 || _days.Columns.Count != 4) return;
            int available = _days.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2;
            int[] widths = new int[4];
            Font body = null, header = null;
            // Measure actual translated headers and visible data together. Shrink the
            // narrow grid font only when their combined minimum widths require it.
            for (float size = 8.5f; size >= 6f; size -= .25f)
            {
                if (body != null) body.Dispose();
                if (header != null) header.Dispose();
                body = new Font(_days.Font.FontFamily, size);
                header = new Font(_days.Font.FontFamily, Math.Min(7.5f, size), FontStyle.Bold);
                for (int column = 0; column < widths.Length; column++)
                {
                    widths[column] = TextRenderer.MeasureText(_days.Columns[column].HeaderText, header,
                        new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 14;
                    foreach (DataGridViewRow row in _days.Rows)
                    {
                        string text = Convert.ToString(row.Cells[column].Value, L10n.Culture);
                        widths[column] = Math.Max(widths[column], TextRenderer.MeasureText(text, body,
                            new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 14);
                    }
                }
                widths[2] = Math.Max(72, widths[2]);
                if (widths.Sum() <= available) break;
            }
            Font oldBody = _days.DefaultCellStyle.Font;
            Font oldHeader = _days.ColumnHeadersDefaultCellStyle.Font;
            _days.DefaultCellStyle.Font = body;
            _days.ColumnHeadersDefaultCellStyle.Font = header;
            if (oldBody != null) oldBody.Dispose();
            if (oldHeader != null) oldHeader.Dispose();
            _days.Columns[0].Width = widths[0];
            _days.Columns[1].Width = widths[1];
            _days.Columns[3].Width = widths[3];
            _days.Columns[2].MinimumWidth = widths[2];
        }

        private void SetMode(bool analytics)
        {
            SetView(analytics ? 1 : 0);
        }

        private void SetView(int view)
        {
            _daysMode = view == 1;
            _livePanel.Visible = !_daysMode;
            _daysPanel.Visible = _daysMode;
            StyleTab(_liveTab, !_daysMode);
            StyleTab(_daysTab, _daysMode);
            if (_daysMode) RefreshDays(true);
            LayoutControls();
        }

        private static void StyleTab(Button button, bool selected)
        {
            AuroraButton aurora = button as AuroraButton;
            if (aurora != null) { aurora.Selected = selected; aurora.Invalidate(); }
            button.BackColor = selected ? Theme.PanelHover : Theme.Background;
            button.ForeColor = selected ? Theme.Cyan : Theme.Muted;
            button.FlatAppearance.BorderColor = selected ? Theme.Cyan : Theme.Border;
        }

        private void UpdatePinButton()
        {
            _pin.ForeColor = TopMost ? Theme.Cyan : Theme.Muted;
            _pin.FlatAppearance.BorderColor = TopMost ? Theme.Cyan : Theme.Border;
            _pin.Text = TopMost ? L10n.T("UI.PinOn") : L10n.T("UI.PinOff");
        }

        internal void ApplyLanguage(string language, bool persist)
        {
            L10n.SetLanguage(language);
            if (persist)
            {
                ViewSettings.Current.Language = L10n.Language;
                ViewSettings.Current.Save();
            }
        }

        private void OnLanguageChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, EventArgs>(OnLanguageChanged), sender, e);
                return;
            }
            // Update existing controls. Keeping their instances preserves focus, the chosen
            // agent, the selected tab and the complete local graph history.
            _refreshingLanguage = true;
            Color transientActivityColor = _activity.ForeColor;
            SuspendLayout();
            try
            {
                // RemoveAll also dismisses a currently open tooltip with the old language.
                _tips.RemoveAll();
                _tips.SetToolTip(_footer, L10n.T("UI.AppearanceTip"));
                _tips.SetToolTip(_refresh, L10n.T("UI.RefreshTip"));
                _tips.SetToolTip(_pin, L10n.T("UI.PinTip"));
                _tips.SetToolTip(_minimize, L10n.T("UI.MinimizeTip"));
                _tips.SetToolTip(_close, L10n.T("UI.CloseTip"));
                _contextChart.AccessibleDescription = L10n.T("UI.ContextTip") + " " + L10n.T("Appearance.ContextHelp");
                _daysHeader.Text = L10n.T("UI.DaysHistory");
                _days.Columns[0].HeaderText = L10n.T("UI.DayColumn");
                _days.Columns[1].HeaderText = L10n.T("UI.TasksColumn");
                _days.Columns[2].HeaderText = L10n.T("UI.TokensColumn");
                _days.Columns[3].HeaderText = L10n.T("UI.ApiColumn");
                _daysSignature = "";
                ApplySnapshot(_snapshot);
                if (!_daysMode) RefreshDays(true);
                if (_activityMessageKey.Length > 0)
                {
                    _activity.Text = L10n.T(_activityMessageKey);
                    _activity.ForeColor = transientActivityColor;
                    if (_activityMessageKey == "UI.IndexingOlder")
                    {
                        _loadOlderDays.Text = L10n.T("UI.Loading");
                        _loadOlderDays.Enabled = false;
                        _tips.SetToolTip(_loadOlderDays, L10n.T("UI.IndexingOlder"));
                    }
                }
                LayoutControls();
            }
            finally
            {
                _refreshingLanguage = false;
                ResumeLayout(false);
            }
            Invalidate(true);
        }

        private void MoveSession(int direction)
        {
            if (_sessions.Items.Count == 0) return;
            int index = _sessions.SelectedIndex;
            if (index < 0) index = 0;
            index = (index + direction + _sessions.Items.Count) % _sessions.Items.Count;
            _sessions.SelectedIndex = index;
        }

        private void DrawSessionItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || e.Index >= _sessions.Items.Count) return;
            SessionView session = _sessions.Items[e.Index] as SessionView;
            if (session == null) return;
            Color background = (e.State & DrawItemState.Selected) != 0 ? Theme.PanelHover : Theme.Panel;
            using (SolidBrush fill = new SolidBrush(background)) e.Graphics.FillRectangle(fill, e.Bounds);
            using (SolidBrush dot = new SolidBrush(session.IsRunning ? Theme.Green : session.Accent))
                e.Graphics.FillEllipse(dot, e.Bounds.Left + 8, e.Bounds.Top + 11, 8, 8);
            string text = session.DisplayRepository + "  ·  " + session.DisplayTitle;
            TextRenderer.DrawText(e.Graphics, text, Font, new Rectangle(e.Bounds.Left + 23, e.Bounds.Top + 3, e.Bounds.Width - 28, e.Bounds.Height - 5), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();
        }

        private void EngineSnapshotReady(MonitorSnapshot snapshot)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(new Action<MonitorSnapshot>(ApplySnapshot), snapshot); }
            catch (InvalidOperationException) { }
        }

        private void ApplySnapshot(MonitorSnapshot snapshot)
        {
            if (snapshot == null || IsDisposed) return;
            if (!_refreshingLanguage) _activityMessageKey = "";
            _snapshot = snapshot;
            _today.Usage = snapshot.Today;
            _today.ApiValue = snapshot.TodayApiValue;
            _today.Tasks = snapshot.TodayTasks;
            _today.RunningSessions = snapshot.RunningSessions;
            _today.PrimaryLimit = snapshot.PrimaryLimit;
            _today.SecondaryLimit = snapshot.SecondaryLimit;
            _today.Invalidate();
            string limitDetails = _today.LimitTooltip();
            _today.AccessibleDescription = L10n.F("UI.TodayTip", snapshot.TodayTasks, PriceCatalog.RateDate) + (limitDetails.Length == 0 ? "" : "  " + limitDetails);
            _appTitle.AccessibleDescription = String.IsNullOrEmpty(snapshot.CodexHome) ? L10n.T("UI.HomeMissing") : snapshot.CodexHome;

            if (!String.IsNullOrEmpty(snapshot.Error))
            {
                _activity.Text = snapshot.DisplayError;
                _activity.ForeColor = Theme.Red;
            }
            else
            {
                _activity.Text = (snapshot.RunningSessions > 0 ? "● " : "○ ") + L10n.F("UI.ActiveAgents", snapshot.RunningSessions);
                _activity.ForeColor = snapshot.RunningSessions > 0 ? Theme.Green : Theme.Muted;
            }
            _liveTab.Text = L10n.T("UI.LiveTab");
            bool hasOlder = snapshot.AvailableFiles > snapshot.IndexedFiles;
            bool indexingOlder = snapshot.IndexedFiles < snapshot.IndexTarget;
            UpdateOlderButton(_loadOlderDays, snapshot, hasOlder, indexingOlder);
            string historyProgress = indexingOlder
                ? L10n.F("UI.IndexingProgress", snapshot.IndexedFiles, snapshot.IndexTarget)
                : hasOlder ? L10n.F("UI.HistoryProgress", snapshot.IndexedFiles, snapshot.AvailableFiles) : "";
            _footer.Text = L10n.F("UI.Footer", snapshot.RefreshedLocal.ToString("HH:mm:ss", L10n.Culture), historyProgress);
            UpdateSessionList();
            RebuildAgentTabs();
            UpdateLiveDetail();

            string daysSignature = L10n.Language + ":" + AuroraUi.DaysSignature(snapshot.Days);
            if (daysSignature != _daysSignature)
            {
                _daysSignature = daysSignature;
                if (_daysMode) RefreshDays(true);
            }
        }

        private void BeginLoadOlder()
        {
            _loadOlderDays.Enabled = false;
            _loadOlderDays.Text = L10n.T("UI.LoadingShort");
            _activityMessageKey = "UI.IndexingOlder";
            _activity.Text = L10n.T(_activityMessageKey);
            _activity.ForeColor = Theme.Cyan;
            _engine.LoadOlder();
        }

        private void UpdateOlderButton(Button button, MonitorSnapshot snapshot, bool hasOlder, bool loading)
        {
            if (button == null) return;
            button.Enabled = hasOlder && !loading;
            string availableText = L10n.T("UI.Older");
            button.Text = loading ? L10n.T("UI.Loading") : hasOlder ? availableText : L10n.T("UI.AllLoaded");
            button.ForeColor = loading ? Theme.Orange : hasOlder ? Theme.Cyan : Theme.Muted;
            button.FlatAppearance.BorderColor = loading ? Theme.Orange : hasOlder ? Theme.Border : Theme.Border;
            _tips.SetToolTip(button, loading
                ? L10n.F("UI.IndexingTip", snapshot.IndexedFiles, snapshot.IndexTarget, snapshot.AvailableFiles)
                : hasOlder ? L10n.T("UI.OlderTip") : L10n.T("UI.AllLoadedTip"));
        }

        private void UpdateSessionList()
        {
            string signature = String.Join("|", _snapshot.Sessions.Select(s => s.Id + ":" + s.DisplayTitle + ":" + s.IsRunning));
            if (signature == _sessionSignature)
            {
                _sessions.Invalidate();
                return;
            }
            _sessionSignature = signature;
            string wanted = _selectedSessionId;
            _sessions.BeginUpdate();
            _sessions.Items.Clear();
            foreach (SessionView session in _snapshot.Sessions) _sessions.Items.Add(session);
            _sessions.EndUpdate();
            int selectedIndex = -1;
            for (int i = 0; i < _sessions.Items.Count; i++)
            {
                SessionView item = _sessions.Items[i] as SessionView;
                if (item != null && item.Id == wanted) { selectedIndex = i; break; }
            }
            if (selectedIndex < 0 && _sessions.Items.Count > 0) selectedIndex = 0;
            if (selectedIndex >= 0) _sessions.SelectedIndex = selectedIndex;
        }

        private void UpdateLiveDetail()
        {
            SessionView session = _snapshot.Sessions.FirstOrDefault(s => s.Id == _selectedSessionId);
            if (session == null && _snapshot.Sessions.Count > 0) session = _snapshot.Sessions[0];
            if (session == null)
            {
                _badge.Text = L10n.T("UI.Waiting");
                _badge.ForeColor = Theme.Muted;
                _taskTitle.Text = L10n.T("UI.NoSessions");
                _repository.Text = L10n.T("UI.AwaitingLogs");
                _repository.AccessibleDescription = _repository.Text;
                _taskTokens.Text = "—";
                _taskCaption.Text = L10n.T("UI.TaskTokens");
                _taskTime.Text = "";
                SetCards(null, new TokenUsage());
                return;
            }
            _selectedSessionId = session.Id;
            TaskView task = session.CurrentOrLastTask;
            string agents = L10n.F("UI.AgentsActive", _snapshot.RunningSessions);
            string duration = task == null ? "" : FormatUtil.Duration(task.StartedLocal, task.CompletedLocal);
            _badge.Text = session.IsRunning ? L10n.F("UI.RunningBadge", duration, FormatUtil.RelativeAgeShort(session.LastActivityLocal)) : session.IsStale ? L10n.F("UI.StaleBadge", agents) : L10n.F("UI.WaitingBadge", agents);
            _badge.ForeColor = session.IsRunning ? Theme.Green : session.IsStale ? Theme.Orange : session.Accent;
            _taskTitle.Text = session.DisplayTitle;
            string modelMeta = FormatUtil.ModelName(session.CurrentModel) + (String.IsNullOrWhiteSpace(session.CurrentEffort) ? "" : " / " + session.CurrentEffort.ToUpperInvariant());
            _repository.Text = session.DisplayRepository + "  •  " + modelMeta;
            _repository.AccessibleDescription = session.WorkingDirectory;
            TokenUsage taskUsage = task == null ? new TokenUsage() : task.Usage;
            _taskTokens.Text = FormatUtil.Tokens(taskUsage.Total);
            _taskTokens.ForeColor = session.Accent;
            _taskCaption.Text = session.IsRunning ? L10n.T("UI.CurrentRun") : L10n.T("UI.LastRun");
            _taskTime.Text = task == null ? session.LastActivityLocal.ToString("HH:mm", L10n.Culture) : duration;
            SetCards(session, taskUsage);
        }

        private void SetCards(SessionView session, TokenUsage task)
        {
            if (session == null)
            {
                _tokenBar.Usage = new TokenUsage();
                _tokenBar.ApiValue = new CostEstimate();
                _tokenBar.Trend = "";
                _contextChart.Used = 0;
                _contextChart.Maximum = 0;
                _contextChart.Trend = "";
                _contextChart.Accent = Theme.Muted;
                _contextChart.History = new List<UsageHistorySample>();
            }
            else
            {
                int contextPercent = FormatUtil.ContextPercent(session.CurrentContext, session.ModelContextWindow);
                TokenUsage total = session.SessionTotal ?? new TokenUsage();
                RecordTokenSample(session, SnapshotTime());
                _tokenBar.Usage = total;
                _tokenBar.ApiValue = session.ApiValue;
                _tokenBar.Trend = FiveMinuteTrend(session);
                _contextChart.Used = session.CurrentContext == null ? 0 : session.CurrentContext.Total;
                _contextChart.Maximum = session.ModelContextWindow;
                _contextChart.Trend = FiveMinuteContextTrend(session);
                _contextChart.Accent = contextPercent < 0 ? Theme.Muted : Theme.ContextColor(_contextChart.Used);
                List<UsageHistorySample> samples;
                _contextChart.History = _tokenRateSamples.TryGetValue(session.Id, out samples) ? samples : new List<UsageHistorySample>();
            }
            _contextChart.IsActive = session != null && session.IsRunning;
            _overview.Session = session;
            _overview.Snapshot = _snapshot;
            _overview.LayoutScale = GetLayoutScale();
            _overview.Rate = UsageSignals.TokensPerMinute(_contextChart.History, SnapshotTime());
            _overview.Invalidate();
            _tokenBar.TaskUsage = session == null || session.CurrentOrLastTask == null ? null : task;
            string detail = session == null ? L10n.T("UI.AwaitingData") : L10n.F("UI.TaskUsageTip", FormatUtil.Tokens(task.Total), FormatUtil.Tokens(task.Input), FormatUtil.Tokens(task.Output), FormatUtil.Tokens(task.Cached), FormatUtil.Tokens(task.Reasoning));
            _tokenBar.AccessibleDescription = detail + L10n.F("UI.ApiTip", PriceCatalog.RateDate) + " " + L10n.T("UI.TrendTip");
            _hero.AccessibleDescription = detail;
            _tokenBar.AccessibleName = detail;
            _overview.AccessibleDescription = detail + L10n.T("UI.ComparisonTip");
            _tokenBar.Invalidate();
            _contextChart.Invalidate();
            FitLiveText();
        }

        private DateTime SnapshotTime()
        {
            return _snapshot != null && _snapshot.RefreshedLocal != default(DateTime) ? _snapshot.RefreshedLocal : DateTime.Now;
        }

        private static string AgentToolTip(SessionView session)
        {
            string model = session.CurrentModel + (String.IsNullOrWhiteSpace(session.CurrentEffort) ? "" : " / " + session.CurrentEffort.ToUpperInvariant());
            return session.DisplayTitle + Environment.NewLine + model + Environment.NewLine + session.WorkingDirectory;
        }

        private void RecordTokenSample(SessionView session, DateTime sampledAt)
        {
            if (_refreshingLanguage) return;
            if (session == null || String.IsNullOrWhiteSpace(session.Id)) return;
            List<UsageHistorySample> samples;
            if (!_tokenRateSamples.TryGetValue(session.Id, out samples))
            {
                samples = new List<UsageHistorySample>();
                _tokenRateSamples[session.Id] = samples;
            }
            long total = session.SessionTotal == null ? 0 : session.SessionTotal.Total;
            long context = session.CurrentContext == null ? 0 : session.CurrentContext.Total;
            long limit = session.ModelContextWindow;
            if (samples.Count > 0 && sampledAt <= samples[samples.Count - 1].At)
            {
                if (sampledAt == samples[samples.Count - 1].At)
                {
                    samples[samples.Count - 1].TotalTokens = total;
                    samples[samples.Count - 1].ContextUsed = context;
                    samples[samples.Count - 1].ContextLimit = limit;
                }
                return;
            }
            // Keep unchanged samples: zero consumption is different from missing telemetry.
            samples.Add(new UsageHistorySample { At = sampledAt, TotalTokens = total, ContextUsed = context, ContextLimit = limit });
            while (samples.Count > 720) samples.RemoveAt(0);
        }

        private string FiveMinuteTrend(SessionView session)
        {
            List<UsageHistorySample> samples;
            if (session == null || !_tokenRateSamples.TryGetValue(session.Id, out samples) || samples.Count < 2) return "";
            DateTime cutoff = SnapshotTime().AddMinutes(-5);
            UsageHistorySample baseline = null;
            foreach (UsageHistorySample sample in samples)
            {
                if (sample.At > cutoff) break;
                baseline = sample;
            }
            if (baseline == null) return "";
            long delta = samples[samples.Count - 1].TotalTokens - baseline.TotalTokens;
            return delta < 0 ? "" : L10n.F("UI.TrendPositive", FormatUtil.Tokens(delta));
        }

        private string FiveMinuteContextTrend(SessionView session)
        {
            List<UsageHistorySample> samples;
            if (session == null || !_tokenRateSamples.TryGetValue(session.Id, out samples) || samples.Count < 2) return "";
            DateTime cutoff = SnapshotTime().AddMinutes(-5);
            UsageHistorySample baseline = null;
            foreach (UsageHistorySample sample in samples)
            {
                if (sample.At > cutoff) break;
                baseline = sample;
            }
            if (baseline == null) return "";
            long delta = samples[samples.Count - 1].ContextUsed - baseline.ContextUsed;
            return L10n.T("UI.Trend") + (delta >= 0 ? "+" : "−") + FormatUtil.Tokens(Math.Abs(delta));
        }

        private void FitLiveText()
        {
            if (IsExpandedDisplay)
            {
                SetResponsiveFont(_taskTitle, ViewSettings.Current.Mode == 2 ? 15 : 13, FontStyle.Bold, GetLayoutScale());
                FitLabelHeights(_hero);
                return;
            }
            float scale = GetLayoutScale();
            FitLabel(_badge, 8f * scale, 6f * scale, FontStyle.Bold);
            FitLabel(_repository, 8f * scale, 6f * scale, FontStyle.Regular);
            FitLabel(_taskCaption, 8f * scale, 5.5f * scale, FontStyle.Bold);
            FitLabel(_taskTokens, 20f * scale, 11f * scale, FontStyle.Bold);
            FitLabel(_taskTitle, 11f * scale, 8.5f * scale, FontStyle.Bold);
            FitLabel(_taskTime, 8f * scale, 6f * scale, FontStyle.Regular);
        }

        private float GetLayoutScale()
        {
            if (IsExpandedDisplay) return Math.Max(1.1f, Math.Min(1.5f, Math.Min(ClientSize.Width / 900f, ClientSize.Height / 780f))) * (ViewSettings.Current.Mode == 2 ? 1.08f : 1f);
            float widthScale = ClientSize.Width / 420f;
            float heightScale = ClientSize.Height / 590f;
            return Math.Max(1f, Math.Min(2f, Math.Min(widthScale, heightScale)));
        }

        private static int S(int value, float scale)
        {
            return Math.Max(1, (int)Math.Round(value * scale));
        }

        private void ApplyResponsiveFonts(float scale)
        {
            SetResponsiveFont(_appTitle, 10f, FontStyle.Bold, scale);
            SetResponsiveFont(_activity, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_liveTab, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_daysTab, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_refresh, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_pin, 7f, FontStyle.Bold, scale);
            SetResponsiveFont(_minimize, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_close, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_badge, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_repository, 8f, FontStyle.Regular, scale);
            SetResponsiveFont(_taskCaption, 8f, FontStyle.Bold, scale);
            SetResponsiveFont(_taskTime, 8f, FontStyle.Regular, scale);
            SetResponsiveFont(_footer, 8f, FontStyle.Regular, scale);
            _today.LayoutScale = scale;
            _today.Invalidate();
        }

        private static void SetResponsiveFont(Control control, float baseSize, FontStyle style, float scale)
        {
            if (control == null) return;
            float target = baseSize * scale;
            if (Math.Abs(control.Font.SizeInPoints - target) < 0.1f && control.Font.Style == style) return;
            Font old = control.Font;
            control.Font = new Font(old.FontFamily, target, style);
            old.Dispose();
        }

        private static void FitLabel(Label label, float maximum, float minimum, FontStyle style)
        {
            if (label == null || label.Width <= 0 || label.Height <= 0 || String.IsNullOrEmpty(label.Text)) return;
            float selected = minimum;
            bool fitted = false;
            using (Graphics graphics = label.CreateGraphics())
            {
                for (float size = maximum; size >= minimum; size -= 0.5f)
                {
                    using (Font candidate = new Font(label.Font.FontFamily, size, style))
                    {
                        Size measured = TextRenderer.MeasureText(graphics, label.Text, candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                        if (measured.Width <= label.ClientSize.Width && measured.Height <= label.ClientSize.Height)
                        {
                            selected = size;
                            fitted = true;
                            break;
                        }
                    }
                }
                for (float size = minimum; !fitted && size >= 4.5f; size -= 0.5f)
                {
                    using (Font candidate = new Font(label.Font.FontFamily, size, style))
                    {
                        Size measured = TextRenderer.MeasureText(graphics, label.Text, candidate, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                        if (measured.Height > label.ClientSize.Height) continue;
                        selected = size;
                        fitted = true;
                    }
                }
            }
            if (Math.Abs(label.Font.Size - selected) > 0.1f)
            {
                Font old = label.Font;
                label.Font = new Font(old.FontFamily, selected, style);
                old.Dispose();
            }
        }

        private static void FitLabelHeights(Control root)
        {
            foreach (Control control in root.Controls)
            {
                Label label = control as Label;
                if (label != null && label.Width > 0 && label.Height > 0 && !String.IsNullOrEmpty(label.Text))
                {
                    float selected = label.Font.SizeInPoints;
                    using (Graphics graphics = label.CreateGraphics())
                    {
                        for (float size = label.Font.SizeInPoints; size >= 4.5f; size -= 0.25f)
                        {
                            int required = 0;
                            using (Font candidate = new Font(label.Font.FontFamily, size, label.Font.Style))
                            {
                                foreach (string line in label.Text.Replace("\r", "").Split('\n'))
                                {
                                    if (line.Length == 0) continue;
                                    required += TextRenderer.MeasureText(graphics, line, candidate,
                                        new Size(Int32.MaxValue, Int32.MaxValue),
                                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
                                }
                            }
                            selected = size;
                            if (required <= label.ClientSize.Height) break;
                        }
                    }
                    if (Math.Abs(label.Font.SizeInPoints - selected) > 0.1f)
                    {
                        Font old = label.Font;
                        label.Font = new Font(old.FontFamily, selected, old.Style);
                        old.Dispose();
                    }
                }
                FitLabelHeights(control);
            }
        }

        private void RefreshDays(bool force)
        {
            if (!_daysMode && !force) return;
            int scrollIndex = _days.FirstDisplayedScrollingRowIndex;
            DateTime? selectedDate = null;
            if (_days.SelectedRows.Count > 0)
            {
                DayView old = _days.SelectedRows[0].Tag as DayView;
                if (old != null) selectedDate = old.Date;
            }
            _days.SuspendLayout();
            _days.Rows.Clear();
            int selectedIndex = -1;
            foreach (DayView day in _snapshot.Days.Take(366))
            {
                string label = day.Date == DateTime.Today ? L10n.T("UI.Today") : day.Date.ToString("dd MMM yy", L10n.Culture);
                int index = _days.Rows.Add(label, day.Tasks.ToString("N0", L10n.Culture), FormatUtil.Tokens(day.Usage.Total), FormatUtil.ApiValue(day.ApiValue));
                _days.Rows[index].Tag = day;
                if (selectedDate.HasValue && selectedDate.Value == day.Date) selectedIndex = index;
            }
            FitAnalyticsColumns();
            _days.ResumeLayout();
            if (_days.Rows.Count > 0)
            {
                int restoredIndex = selectedIndex >= 0 ? selectedIndex : 0;
                _days.ClearSelection();
                _days.CurrentCell = _days.Rows[restoredIndex].Cells[0];
                _days.Rows[restoredIndex].Selected = true;
                if (scrollIndex >= 0 && scrollIndex < _days.Rows.Count)
                    _days.FirstDisplayedScrollingRowIndex = scrollIndex;
            }
            UpdateDayDetail();
            FitLabelHeights(_dayDetail);
        }

        private void UpdateDayDetail()
        {
            DayView day = _days.SelectedRows.Count > 0 ? _days.SelectedRows[0].Tag as DayView : null;
            if (day == null)
            {
                _dayTitle.Text = L10n.T("UI.NoDays");
                _dayMeta.Text = "";
                _dayValue.Text = "";
                _dayMeta.AccessibleDescription = "";
                _tips.SetToolTip(_dayMeta, "");
                _tips.SetToolTip(_dayValue, "");
                return;
            }
            _dayTitle.Text = day.Date == DateTime.Today ? L10n.T("UI.TodaySubscription") : day.Date.ToString("dddd, dd MMMM yyyy", L10n.Culture) + L10n.T("UI.SubscriptionSuffix");
            _dayValue.Text = FormatUtil.ApiValue(day.ApiValue);
            _dayMeta.Text = L10n.F("UI.DayUsage", day.Tasks.ToString("N0", L10n.Culture), FormatUtil.Tokens(day.Usage.Input), FormatUtil.Tokens(day.Usage.Output), FormatUtil.Tokens(day.Usage.Cached));
            string note = L10n.F("UI.DayTip", FormatUtil.ModelList(day.Models));
            if (day.ApiValue.IsPartial) note += L10n.T("UI.PartialNote");
            _dayMeta.AccessibleDescription = _dayMeta.Text + Environment.NewLine + note;
            _tips.SetToolTip(_dayMeta, _dayMeta.AccessibleDescription);
            _tips.SetToolTip(_dayValue, note);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _engine.Start();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_appTitle == null) return;

            SuspendLayout();
            LayoutControls();
            ResumeLayout(false);
            Invalidate(true);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            Invalidate(true);
            Update();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Width > 2 && Height > 2)
            using (LinearGradientBrush glow = new LinearGradientBrush(new Rectangle(0, 0, Width, 2), Theme.Teal, Theme.Cyan, 0f)) e.Graphics.FillRectangle(glow, 1, 0, Width - 2, 2);
            using (Pen pen = new Pen(Theme.Border)) e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void WndProc(ref Message message)
        {
            const int WmNcHitTest = 0x0084;
            if (message.Msg == WmNcHitTest && WindowState == FormWindowState.Normal)
            {
                long packed = message.LParam.ToInt64();
                Point point = PointToClient(new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff))));
                bool left = point.X <= ResizeGrip;
                bool right = point.X >= ClientSize.Width - ResizeGrip;
                bool top = point.Y <= ResizeGrip;
                bool bottom = point.Y >= ClientSize.Height - ResizeGrip;
                if (left && top) { message.Result = (IntPtr)13; return; }
                if (right && top) { message.Result = (IntPtr)14; return; }
                if (left && bottom) { message.Result = (IntPtr)16; return; }
                if (right && bottom) { message.Result = (IntPtr)17; return; }
                if (left) { message.Result = (IntPtr)10; return; }
                if (right) { message.Result = (IntPtr)11; return; }
                if (top) { message.Result = (IntPtr)12; return; }
                if (bottom) { message.Result = (IntPtr)15; return; }
                if (point.Y < 45 && !_refresh.Bounds.Contains(point) && !_pin.Bounds.Contains(point) && !_minimize.Bounds.Contains(point) && !_close.Bounds.Contains(point))
                {
                    message.Result = (IntPtr)2;
                    return;
                }
            }
            base.WndProc(ref message);
        }

        private void BeginWindowDrag(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || WindowState != FormWindowState.Normal) return;
            ReleaseCapture();
            SendMessage(Handle, 0x00A1, (IntPtr)2, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _settings.Capture(this);
            _settings.Save();
            _engine.Dispose();
            ViewSettings.Current.Save();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                L10n.LanguageChanged -= OnLanguageChanged;
                _tips.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
