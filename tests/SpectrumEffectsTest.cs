using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class SpectrumEffectsTest
    {
        internal static void Run()
        {
            using (Form form = new Form())
            using (ContextChart context = new ContextChart())
            using (Panel agents = new Panel())
            using (Panel today = new Panel())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.ShowInTaskbar = false;
                context.Size = new Size(400, 160);
                form.Controls.Add(context);
                SpectrumMotion motion = new SpectrumMotion(form, context, agents, today);
                Timer timer = (Timer)typeof(SpectrumMotion).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motion);
                ToolStripMenuItem toggle = (ToolStripMenuItem)typeof(SpectrumMotion).GetField("_motion", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(motion);
                toggle.Checked = true;
                Check(!timer.Enabled, "Hidden windows must not animate.");
                Check(timer.Interval >= 80, "Animation rate exceeds the declared frame cap.");
                form.Show();
                Application.DoEvents();
                Check(timer.Enabled, "Visible window did not start the effect clock.");
                context.IsActive = false;
                SpectrumFx.Phase = SpectrumFx.ContextPhase = 0;
                MethodInfo tick = typeof(Timer).GetMethod("OnTick", BindingFlags.Instance | BindingFlags.NonPublic);
                tick.Invoke(timer, new object[] { EventArgs.Empty });
                Check(SpectrumFx.Phase > 0 && SpectrumFx.ContextPhase == 0, "Idle tasks must keep the satellite clock moving independently.");
                MethodInfo keyDown = typeof(Control).GetMethod("OnKeyDown", BindingFlags.Instance | BindingFlags.NonPublic);
                keyDown.Invoke(form, new object[] { new KeyEventArgs(Keys.Control | Keys.M) });
                Check(!timer.Enabled && !SpectrumFx.MotionEnabled, "Ctrl+M did not pause animation.");
                MethodInfo mouseMove = typeof(ContextChart).GetMethod("OnMouseMove", BindingFlags.Instance | BindingFlags.NonPublic);
                mouseMove.Invoke(context, new object[] { new MouseEventArgs(MouseButtons.None, 0, 390, 150, 0) });
                Check(context.ViewYaw == 0 && context.ViewPitch == 0, "Reduced motion still responds with camera motion.");
                keyDown.Invoke(form, new object[] { new KeyEventArgs(Keys.Control | Keys.M) });
                Check(timer.Enabled, "Ctrl+M did not resume animation.");
                mouseMove.Invoke(context, new object[] { new MouseEventArgs(MouseButtons.None, 0, 390, 150, 0) });
                Check(context.ViewYaw != 0 && context.ViewPitch != 0, "The enabled 3D camera did not respond.");
                form.WindowState = FormWindowState.Minimized;
                Application.DoEvents();
                Check(!timer.Enabled, "Minimized window kept animating.");
                form.WindowState = FormWindowState.Normal;
                form.Hide();
                Check(!timer.Enabled, "Hidden window kept animating.");
                form.Dispose();
                Check(!timer.Enabled, "Disposed window retained an active timer.");
                motion.Dispose();
            }
            SpectrumFx.Phase = 0;
            CheckSatelliteMotion();
            Console.WriteLine("SPECTRUM_EFFECTS_OK cases=12");
        }
        private static void CheckSatelliteMotion()
        {
            double oldPhase = SpectrumFx.Phase, oldContext = SpectrumFx.ContextPhase;
            try
            {
                SpectrumFx.ContextPhase = 0;
                using (Bitmap first = new Bitmap(320, 320))
                using (Bitmap next = new Bitmap(320, 320))
                using (Bitmap proof = new Bitmap(960, 320))
                using (Graphics sheet = Graphics.FromImage(proof))
                {
                    for (int frame = 0; frame < 3; frame++)
                    {
                        SpectrumFx.Phase = frame * 2;
                        Bitmap target = frame == 0 ? first : next;
                        using (Graphics g = Graphics.FromImage(target))
                        {
                            g.Clear(Theme.Background);
                            ContextSculpture.Draw(g, new Rectangle(0, 0, 320, 320), .61, true, Theme.Teal, 0, 0);
                        }
                        sheet.DrawImageUnscaled(target, frame * 320, 0);
                    }
                    int changed = 0;
                    for (int y = 0; y < 320; y++)
                    for (int x = 0; x < 320; x++)
                        if (first.GetPixel(x, y) != next.GetPixel(x, y)) changed++;
                    Check(changed > 100, "Satellites froze when the central task sculpture stopped.");
                    string directory = System.IO.Path.Combine(Environment.CurrentDirectory, "artifacts", "renders");
                    System.IO.Directory.CreateDirectory(directory);
                    proof.Save(System.IO.Path.Combine(directory, "satellites-motion.png"), System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            finally { SpectrumFx.Phase = oldPhase; SpectrumFx.ContextPhase = oldContext; }
        }
        private static void Check(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
        }
    }
}
