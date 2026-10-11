using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class RenderTest
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                ViewSettings.Current = new ViewSettings();
                ProductViewTest.Run();
                SpectrumEffectsTest.Run();
                string output = args.Length > 0 ? args[0] : Environment.CurrentDirectory;
                Directory.CreateDirectory(output);

                Render(Path.Combine(output, "compact-420x570.png"), new Size(420, 570), 0);
                Render(Path.Combine(output, "minimum-400x520.png"), new Size(400, 520), 0);
                Render(Path.Combine(output, "wide-760x650.png"), new Size(760, 650), 0);
                Render(Path.Combine(output, "lower-754x648.png"), new Size(754, 648), 0);
                Render(Path.Combine(output, "days-420x570.png"), new Size(420, 570), 1);
            Render(Path.Combine(output, "days-760x650.png"), new Size(760, 650), 1);
                Render(Path.Combine(output, "analytics-ocean-960x820.png"), new Size(960, 820), 0, 1, 0);
                Render(Path.Combine(output, "analytics-solar-960x820.png"), new Size(960, 820), 0, 1, 1);
                Render(Path.Combine(output, "presentation-fire-1240x900.png"), new Size(1240, 900), 0, 2, 2);
                Console.WriteLine("RENDER_OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                Console.WriteLine("RENDER_FAILED");
                return 1;
            }
        }

        private static void Render(string path, Size size, int view, int displayMode = -1, int palette = 0)
        {
            MainForm form = new MainForm(new[] { "--codex-home", @"C:\Users\User\.codex" });
            form.TopMost = false;
            form.StartPosition = FormStartPosition.Manual;
            form.ShowInTaskbar = false;
            form.Location = new Point(-20000, -20000);
            ViewSettings.Current.Palette = palette;
            if (displayMode >= 0) form.ApplyDisplayMode(displayMode, false);
            form.Size = size;
            if (form.Size != size) throw new InvalidOperationException("Requested render size was not applied: " + size);
            MonitorSnapshot snapshot = SampleSnapshot();
            MethodInfo apply = typeof(MainForm).GetMethod("ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo setView = typeof(MainForm).GetMethod("SetView", BindingFlags.Instance | BindingFlags.NonPublic);
            form.Show();
            FieldInfo engineField = typeof(MainForm).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic);
            ((SessionEngine)engineField.GetValue(form)).Dispose();
            Application.DoEvents();
            if (view == 0)
            {
                DateTime finalRefresh = snapshot.RefreshedLocal;
                SessionView sampledSession = snapshot.Sessions[0];
                long finalInput = sampledSession.SessionTotal.Input;
                long finalTotal = sampledSession.SessionTotal.Total;
                long finalContext = sampledSession.CurrentContext.Total;
                long initialDelta = 1240000;
                snapshot.RefreshedLocal = finalRefresh.AddMinutes(-5).AddSeconds(-1);
                sampledSession.SessionTotal.Input = finalInput - initialDelta;
                sampledSession.SessionTotal.Total = finalTotal - initialDelta;
                sampledSession.CurrentContext.Total = finalContext - 42000;
                apply.Invoke(form, new object[] { snapshot });
    
                sampledSession.SessionTotal.Input = finalInput;
                sampledSession.SessionTotal.Total = finalTotal;
                sampledSession.CurrentContext.Total = finalContext;
                snapshot.RefreshedLocal = finalRefresh;
            }
            apply.Invoke(form, new object[] { snapshot });
            if (view == 1) setView.Invoke(form, new object[] { 1 });
            form.PerformLayout();
            form.Refresh();
            Application.DoEvents();
            AssertVisibleControlTextFitsVertically(form);
            if (form.IsExpandedDisplay && view == 0)
            {
                Control overview = (Control)typeof(MainForm).GetField("_overview", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Control context = (Control)typeof(MainForm).GetField("_contextChart", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Control tokens = (Control)typeof(MainForm).GetField("_tokenBar", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                if (!overview.Visible || overview.Bottom > context.Top || context.Bounds.IntersectsWith(tokens.Bounds)) throw new InvalidOperationException("Expanded overview overlaps the detail panels.");
            }
            FieldInfo footerField = typeof(MainForm).GetField("_footer", BindingFlags.Instance | BindingFlags.NonPublic);
            Control footer = (Control)footerField.GetValue(form);
            if (form.ClientSize.Height - footer.Bottom < 10)
                throw new InvalidOperationException("The footer blocks the bottom resize grip.");
            using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            FieldInfo cardsField = typeof(MainForm).GetField("_agentCards", BindingFlags.Instance | BindingFlags.NonPublic);
            System.Collections.IList cards = (System.Collections.IList)cardsField.GetValue(form);
            if (view == 0 && cards.Count < 2)
                throw new InvalidOperationException("Second active agent card is missing.");
            if (view == 0)
            {
                FieldInfo tokenBarField = typeof(MainForm).GetField("_tokenBar", BindingFlags.Instance | BindingFlags.NonPublic);
                TokenBar tokenBar = (TokenBar)tokenBarField.GetValue(form);
                FieldInfo contextChartField = typeof(MainForm).GetField("_contextChart", BindingFlags.Instance | BindingFlags.NonPublic);
                ContextChart contextChart = (ContextChart)contextChartField.GetValue(form);
                FieldInfo livePanelField = typeof(MainForm).GetField("_livePanel", BindingFlags.Instance | BindingFlags.NonPublic);
                Panel livePanel = (Panel)livePanelField.GetValue(form);
                FieldInfo activeStripField = typeof(MainForm).GetField("_activeStrip", BindingFlags.Instance | BindingFlags.NonPublic);
                Panel activeStrip = (Panel)activeStripField.GetValue(form);
                if (!tokenBar.Visible || tokenBar.Bottom > livePanel.ClientSize.Height) throw new InvalidOperationException("Token bar is clipped in the live layout.");
                if (!contextChart.Visible || contextChart.Bottom > livePanel.ClientSize.Height || (!form.IsExpandedDisplay && contextChart.Top <= tokenBar.Bottom)) throw new InvalidOperationException("Context chart is clipped or overlaps the token panel.");
                if (contextChart.Height - (int)Math.Round(73 * contextChart.LayoutScale) < 40)
                    throw new InvalidOperationException("Context plot is too short to read its axis labels.");
                if (livePanel.VerticalScroll.Visible) throw new InvalidOperationException("Live view requires scrolling.");
                if (contextChart.Height <= tokenBar.Height) throw new InvalidOperationException("Context chart is not the primary live information block.");
                if (contextChart.Used != 157700 || contextChart.Maximum != 258400 || contextChart.History.Count < 2) throw new InvalidOperationException("Context chart data or history is incomplete.");
                if (tokenBar.Usage.Total != 246913568 || tokenBar.ApiValue.Value != 129.42m) throw new InvalidOperationException("Token panel does not own the complete session usage summary.");
                if (tokenBar.TaskUsage == null || tokenBar.TaskUsage.Total != 133333332) throw new InvalidOperationException("Task usage was lost or confused with the session total.");
                Label taskCaption = (Label)typeof(MainForm).GetField("_taskCaption", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                Label taskTokens = (Label)typeof(MainForm).GetField("_taskTokens", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                if (taskCaption.Visible || taskTokens.Visible) throw new InvalidOperationException("Hero still duplicates token usage.");
                if (typeof(MainForm).GetField("_sessionUsageCard", BindingFlags.Instance | BindingFlags.NonPublic) != null || typeof(MainForm).GetField("_contextCard", BindingFlags.Instance | BindingFlags.NonPublic) != null) throw new InvalidOperationException("Legacy metric cards remain wired into the live screen.");
                if (tokenBar.Trend != "Δ5ХВ +1.24M") throw new InvalidOperationException("Five-minute token delta is not calculated from rolling samples: " + tokenBar.Trend);
                foreach (string fieldName in new[] { "_liveTab", "_daysTab" })
                {
                    Button navigation = (Button)typeof(MainForm).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    Size measured = TextRenderer.MeasureText(navigation.Text, navigation.Font, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    if (measured.Width > navigation.ClientSize.Width - 14) throw new InvalidOperationException("Navigation label lacks safe GDI padding: " + navigation.Text);
                }
                AgentMiniCard second = (AgentMiniCard)cards[1];
                if (!second.Visible || second.Right > activeStrip.ClientSize.Width)
                    throw new InvalidOperationException("Second active agent card is outside the visible strip.");
                MethodInfo click = typeof(Control).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
                click.Invoke(second, new object[] { EventArgs.Empty });
                FieldInfo selectedField = typeof(MainForm).GetField("_selectedSessionId", BindingFlags.Instance | BindingFlags.NonPublic);
                if ((string)selectedField.GetValue(form) != second.Session.Id) throw new InvalidOperationException("Agent tab did not switch the selected session.");
            }


            if (view == 1)
            {
                FieldInfo olderField = typeof(MainForm).GetField("_loadOlderDays", BindingFlags.Instance | BindingFlags.NonPublic);
                Button older = (Button)olderField.GetValue(form);
                if (!older.Enabled || !older.Visible || older.Text != "ДАВНІШІ") throw new InvalidOperationException("Days older button is not ready.");
            }

            FieldInfo minimizeField = typeof(MainForm).GetField("_minimize", BindingFlags.Instance | BindingFlags.NonPublic);
            Button minimize = (Button)minimizeField.GetValue(form);
            minimize.PerformClick();
            if (form.WindowState != FormWindowState.Minimized) throw new InvalidOperationException("Minimize button did not minimize the window.");
            form.WindowState = FormWindowState.Normal;
            form.Close();
            form.Dispose();
        }

        private static void AssertVisibleControlTextFitsVertically(Control root)
        {
            foreach (Control control in root.Controls)
            {
                if (!control.Visible) continue;
                if ((control is Label || control is Button) && !String.IsNullOrWhiteSpace(control.Text))
                {
                    string[] lines = control.Text.Replace("\r", "").Split('\n');
                    int required = 0;
                    foreach (string line in lines)
                    {
                        if (line.Length == 0) continue;
                        required += TextRenderer.MeasureText(line, control.Font,
                            new Size(Int32.MaxValue, Int32.MaxValue),
                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Height;
                    }
                    int inset = control is Button ? 4 : 0;
                    if (required > control.ClientSize.Height - inset)
                        throw new InvalidOperationException("Visible text is vertically clipped: "
                            + control.Name + " = " + control.Text + " needs " + required
                            + "px, has " + (control.ClientSize.Height - inset) + "px.");
                }
                AssertVisibleControlTextFitsVertically(control);
            }
        }

        private static MonitorSnapshot SampleSnapshot()
        {
            TokenUsage today = new TokenUsage { Input = 34510000, Output = 246700, Cached = 33380000, Total = 34760000 };
            TokenUsage task = new TokenUsage { Input = 123456789, Output = 9876543, Cached = 112233445, Total = 133333332 };
            TokenUsage total = new TokenUsage { Input = 234567890, Output = 12345678, Cached = 200000000, Total = 246913568 };
            TaskView current = new TaskView
            {
                SessionId = "sample",
                TurnId = "turn",
                Title = "Налаштувати Codex Monitor і перевірити довгу назву завдання",
                Repository = "кодекс токен монітор",
                WorkingDirectory = @"C:\Users\User\Desktop\кодекс токен монітор\дуже-довгий-підкаталог",
                StartedLocal = DateTime.Now.AddMinutes(-20),
                Usage = task
            };
            SessionView session = new SessionView
            {
                Id = "sample",
                Title = current.Title,
                Repository = current.Repository,
                WorkingDirectory = current.WorkingDirectory,
                LastActivityLocal = DateTime.Now,
                IsRunning = true,
                CurrentModel = "gpt-5.6-sol",
                CurrentEffort = "high",
                StartedLocal = DateTime.Now.AddMinutes(-20),
                ModelContextWindow = 258400,
                CurrentContext = new TokenUsage { Input = 151000, Cached = 118000, Output = 6700, Total = 157700 },
                SessionTotal = total,
                ApiValue = new CostEstimate { Value = 129.42m, KnownTokens = total.Total },
                CurrentOrLastTask = current,
                TurnCount = 127,
                Accent = Theme.Green
            };
            MonitorSnapshot snapshot = new MonitorSnapshot
            {
                CodexHome = @"C:\Users\User\.codex",
                Today = today,
                TodayTasks = 125,
                RunningSessions = 3,
                IndexedFiles = 240,
                AvailableFiles = 1250,
                IndexTarget = 240,
                PrimaryLimit = new RateLimitView { Available = true, UsedPercent = 59, WindowMinutes = 10080, ResetsLocal = DateTime.Now.AddDays(6) },
                SecondaryLimit = new RateLimitView { Available = true, UsedPercent = 28, WindowMinutes = 300, ResetsLocal = DateTime.Now.AddHours(2) },
                RefreshedLocal = DateTime.Now
            };
            snapshot.TodayApiValue = new CostEstimate { Value = 42.73m, KnownTokens = today.Total };
            snapshot.Sessions.Add(session);
            for (int agent = 2; agent <= 3; agent++)
            {
                TaskView agentTask = new TaskView
                {
                    SessionId = "sample-" + agent,
                    TurnId = "turn-" + agent,
                    Title = agent == 2 ? "Перевірити другий repository" : "Третя паралельна Codex-сесія",
                    Repository = agent == 2 ? "Wegscheider-n8n-Test" : "founder-office",
                    WorkingDirectory = @"C:\Users\User\Desktop\parallel-session",
                    StartedLocal = DateTime.Now.AddMinutes(-agent * 4),
                    Usage = new TokenUsage { Input = 2300000 * agent, Output = 42000 * agent, Cached = 1800000 * agent, Total = 2342000 * agent }
                };
                snapshot.Sessions.Add(new SessionView
                {
                    Id = agentTask.SessionId,
                    Title = agentTask.Title,
                    Repository = agentTask.Repository,
                    WorkingDirectory = agentTask.WorkingDirectory,
                    LastActivityLocal = DateTime.Now.AddSeconds(-agent),
                    IsRunning = true,
                    CurrentModel = agent == 2 ? "gpt-5.6-sol" : "gpt-5.4",
                    CurrentEffort = agent == 2 ? "high" : "medium",
                    StartedLocal = agentTask.StartedLocal,
                    ModelContextWindow = 258400,
                    CurrentContext = new TokenUsage { Input = agent == 2 ? 221000 : 74000, Output = 5000, Total = agent == 2 ? 226000 : 79000 },
                    SessionTotal = new TokenUsage { Input = 5000000 * agent, Output = 120000 * agent, Cached = 4200000 * agent, Total = 5120000 * agent },
                    CurrentOrLastTask = agentTask,
                    TurnCount = agent * 12,
                    Accent = agent == 2 ? Theme.Teal : Theme.Orange
                });
            }

            for (int i = 0; i < 14; i++)
            {
                bool partial = i == 2;
                snapshot.Days.Add(new DayView
                {
                    Date = DateTime.Today.AddDays(-i),
                    Tasks = 24 - i,
                    Usage = new TokenUsage { Input = 34510000 - i * 500000, Output = 246700 + i * 1200, Cached = 33380000 - i * 450000, Total = 34760000 - i * 498800 },
                    ApiValue = new CostEstimate { Value = 42.73m - i * 1.17m, KnownTokens = 30000000, UnknownTokens = partial ? 500000 : 0 },
                    Models = partial ? "gpt-5.6-sol, <unknown>" : "gpt-5.6-sol, gpt-5.6-luna"
                });
            }
            return snapshot;
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint flags);
    }
}
