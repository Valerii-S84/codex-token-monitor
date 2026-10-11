using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal abstract class HoverCardControl : Control
    {
        private sealed class HoverRegion
        {
            internal float Progress;
            internal float Target;
        }

        private readonly Dictionary<Rectangle, HoverRegion> _regions = new Dictionary<Rectangle, HoverRegion>();
        private readonly Timer _hoverTimer = new Timer { Interval = 16 };
        private Point _pointer;
        private bool _inside;
        private long _lastFrame;

        protected HoverCardControl()
        {
            _hoverTimer.Tick += AnimateHover;
        }

        private HoverRegion Region(Rectangle bounds)
        {
            HoverRegion region;
            if (!_regions.TryGetValue(bounds, out region))
            {
                region = new HoverRegion();
                _regions.Add(bounds, region);
                UpdateTargets();
            }
            return region;
        }

        private void UpdateTargets()
        {
            bool animate = SpectrumFx.MotionEnabled && !SpectrumFx.EconomyActive && !SystemInformation.HighContrast;
            bool pending = false;
            foreach (KeyValuePair<Rectangle, HoverRegion> entry in _regions)
            {
                entry.Value.Target = _inside && Visible && entry.Key.Contains(_pointer) ? 1f : 0f;
                if (!animate) entry.Value.Progress = entry.Value.Target;
                pending |= entry.Value.Progress != entry.Value.Target;
            }
            if (pending && !_hoverTimer.Enabled)
            {
                _lastFrame = Stopwatch.GetTimestamp();
                _hoverTimer.Start();
            }
            else if (!pending) _hoverTimer.Stop();
        }

        private void AnimateHover(object sender, EventArgs e)
        {
            UpdateTargets();
            long now = Stopwatch.GetTimestamp();
            float step = (float)((now - _lastFrame) / (double)Stopwatch.Frequency / .18);
            _lastFrame = now;
            bool pending = false;
            foreach (HoverRegion region in _regions.Values)
            {
                region.Progress = region.Target > region.Progress
                    ? Math.Min(region.Target, region.Progress + step)
                    : Math.Max(region.Target, region.Progress - step);
                pending |= region.Progress != region.Target;
            }
            Invalidate();
            if (!pending) _hoverTimer.Stop();
        }

        internal Rectangle HoverBounds(Graphics g, Rectangle bounds, float radius)
        {
            if (bounds.Width < 12 || bounds.Height < 12 || SystemInformation.HighContrast) return bounds;
            float progress = Region(bounds).Progress;
            float eased = progress * progress * (3f - 2f * progress);
            int grow = (int)Math.Round(eased * 2);
            int lift = SpectrumFx.MotionEnabled ? (int)Math.Round(eased) : 0;
            Rectangle face = Rectangle.FromLTRB(bounds.Left + 2 - grow, bounds.Top + 2 - lift,
                bounds.Right - 2 + grow, bounds.Bottom - 2 - lift);
            if (eased > 0)
            {
                Rectangle shadow = face;
                shadow.Offset(0, 2 + (int)Math.Round(eased * 3));
                using (GraphicsPath path = AuroraUi.Round(shadow, radius))
                using (SolidBrush shade = new SolidBrush(Color.FromArgb((int)(100 * eased), 0, 2, 8)))
                    g.FillPath(shade, path);
            }
            return face;
        }

        internal void PaintHover(Graphics g, Rectangle bounds, Rectangle face, float radius)
        {
            if (face.Width < 3 || face.Height < 3 || SystemInformation.HighContrast) return;
            float progress = Region(bounds).Progress;
            float strength = progress * progress * (3f - 2f * progress)
                * Math.Max(0, Math.Min(100, ViewSettings.Current.Intensity)) / 100f;
            if (strength <= 0) return;
            Color light = Color.FromArgb(183, 225, 255);
            GraphicsState saved = g.Save();
            try
            {
                using (GraphicsPath outline = AuroraUi.Round(face, radius))
                {
                    g.SetClip(outline, CombineMode.Intersect);
                    float spread = Math.Max(45, Math.Min(170, face.Width * .6f));
                    using (GraphicsPath spot = new GraphicsPath())
                    {
                        spot.AddEllipse(_pointer.X - spread, _pointer.Y - spread * .7f, spread * 2, spread * 1.4f);
                        using (PathGradientBrush glow = new PathGradientBrush(spot))
                        {
                            glow.CenterPoint = _pointer;
                            glow.CenterColor = Color.FromArgb((int)(68 * strength), light);
                            glow.SurroundColors = new[] { Color.FromArgb(0, light) };
                            g.FillPath(glow, spot);
                        }
                    }
                    using (Pen rim = new Pen(Color.FromArgb((int)(190 * strength), light), 1.4f))
                        g.DrawPath(rim, outline);
                }
            }
            finally { g.Restore(saved); }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            _pointer = e.Location;
            _inside = true;
            UpdateTargets();
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _inside = false;
            UpdateTargets();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_regions != null) _regions.Clear();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (!Visible && _hoverTimer != null)
            {
                _inside = false;
                _hoverTimer.Stop();
                _regions.Clear();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hoverTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
