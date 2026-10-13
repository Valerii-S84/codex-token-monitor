using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class LocalizationTest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static int _checks;
        private const string UserTitle = "Користувацьке завдання Українська English";
        private const string UserRepository = "репозиторій Київ";
        private const string UserModel = "custom-Модель-v7";
        private const string UserDirectory = @"C:\синтетичні дані\репозиторій Київ";

        [STAThread]
        public static int Main()
        {
            try
            {
                string home = Environment.GetEnvironmentVariable("CODEX_HOME");
                Check(!String.IsNullOrWhiteSpace(home) && Directory.Exists(Path.Combine(home, "sessions")), "Isolated CODEX_HOME is required.");
                Check(!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_TOKEN_MONITOR_DATA_HOME")), "Isolated data home is required.");
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                TestCatalog();
                TestSettings();
                TestFormatting();
                TestRuntimeSwitch(home);
                Console.WriteLine("LOCALIZATION_OK checks=" + _checks + " languages=uk,en");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Check(bool pass, string message)
        {
            _checks++;
            if (!pass) throw new InvalidOperationException(message);
        }

        private static void TestCatalog()
        {
            Check(L10n.ValidateCatalog().Length == 0, "Catalog completeness or placeholder parity failed.");
            Dictionary<string, string> uk = (Dictionary<string, string>)typeof(L10n).GetField("Ukrainian", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Dictionary<string, string> en = (Dictionary<string, string>)typeof(L10n).GetField("English", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            Check(uk.Count >= 100 && uk.Keys.OrderBy(x => x).SequenceEqual(en.Keys.OrderBy(x => x)), "Catalog coverage is incomplete.");
            foreach (string language in new[] { "uk", "en" })
            {
                L10n.SetLanguage(language);
                foreach (string key in uk.Keys)
                {
                    string value = L10n.T(key);
                    Check(!String.IsNullOrWhiteSpace(value), "Empty translation " + language + ":" + key);
                    int highest = -1;
                    foreach (Match match in Regex.Matches(value, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")) highest = Math.Max(highest, Int32.Parse(match.Groups[1].Value));
                    object[] args = Enumerable.Range(0, highest + 1).Select(x => (object)(x + 1)).ToArray();
                    Check(!String.IsNullOrWhiteSpace(L10n.F(key, args)), "Invalid composite format " + key);
                    if (language == "en" && key != "Appearance.Ukrainian") Check(!HasUkrainian(value), "English translation retains Ukrainian: " + key);
                }
            }
            // Demonstrate that completeness checks reject omissions, not just the current happy path.
            string sample = en.Keys.First(); string original = en[sample];
            try { en.Remove(sample); Check(L10n.ValidateCatalog().Length > 0, "Catalog validator accepted a missing translation."); }
            finally { en[sample] = original; }
            try { en[sample] = "{99}"; Check(L10n.ValidateCatalog().Length > 0, "Catalog validator accepted mismatched placeholders."); }
            finally { en[sample] = original; }
        }

        private static void TestSettings()
        {
            Check(L10n.DefaultLanguage(CultureInfo.GetCultureInfo("uk-UA")) == "uk", "Ukrainian Windows must default to Ukrainian.");
            foreach (string culture in new[] { "en-US", "de-DE", "pl-PL", "ru-RU" }) Check(L10n.DefaultLanguage(CultureInfo.GetCultureInfo(culture)) == "en", "Other Windows UI languages must default to English: " + culture);
            string folder = Path.Combine(AppSettings.SettingsDirectory, "localization-fixture");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "appearance.ini");
            Check(ViewSettings.Load(path).Language == L10n.DefaultLanguage(), "Fresh installation default differs from Windows UI language.");
            ViewSettings fresh = ViewSettings.Load(path);
            fresh.Save(path);
            File.WriteAllText(Path.Combine(folder, "settings.ini"), "width=760\nheight=650\n");
            Check(ViewSettings.Load(path).Language == fresh.Language, "Fresh default changed after window settings were saved.");
            File.Delete(path);
            File.WriteAllText(Path.Combine(folder, "settings.ini"), "width=760\nheight=650\ntopmost=False\n");
            Check(ViewSettings.Load(path).Language == "uk", "Legacy window-only settings must preserve Ukrainian.");
            File.WriteAllText(path, "mode=2\npalette=1\nintensity=35\neconomy=True\nmotion=False\n");
            ViewSettings old = ViewSettings.Load(path);
            Check(old.Language == "uk" && old.Mode == 2 && old.Palette == 1 && old.Intensity == 35 && old.Economy && !old.Motion, "Legacy appearance migration lost preferences.");
            foreach (string raw in new[] { "xx", "", "../../en", "uk-UA" })
            {
                File.WriteAllText(path, "language=" + raw + "\nmode=0\n");
                Check(ViewSettings.Load(path).Language == "uk", "Invalid persisted language must safely fall back: " + raw);
            }
            foreach (string language in new[] { "en", "uk" })
            {
                old.Language = language; old.Save(path);
                ViewSettings reloaded = ViewSettings.Load(path);
                Check(reloaded.Language == language && reloaded.Mode == old.Mode && reloaded.Palette == old.Palette && reloaded.Motion == old.Motion, "Language save/atomic replacement lost appearance preferences.");
            }
            L10n.SetLanguage("invalid"); Check(L10n.Language == "uk", "Invalid runtime language failed safe fallback.");
        }

        private static void TestFormatting()
        {
            DateTime start = new DateTime(2026, 1, 2, 12, 0, 0);
            foreach (string language in new[] { "uk", "en" })
            {
                L10n.SetLanguage(language);
                Check(Thread.CurrentThread.CurrentCulture.Name == (language == "uk" ? "uk-UA" : "en-US") && Thread.CurrentThread.CurrentUICulture.Equals(L10n.Culture), "UI/number cultures were not applied together.");
                Check(FormatUtil.Duration(start, start.AddHours(1).AddMinutes(2)) == (language == "uk" ? "1г 2хв" : "1h 2m"), "Duration units were not localized.");
                Check(FormatUtil.RelativeAge(DateTime.Now.AddMinutes(-5)).Contains(language == "uk" ? "хв тому" : "m ago"), "Relative-age suffix is wrong.");
                Check(!FormatUtil.RelativeAgeShort(DateTime.Now.AddMinutes(-5)).Contains(language == "uk" ? "тому" : "ago"), "Short relative age retains sentence suffix.");
                Check(FormatUtil.Tokens(1240000) == (language == "uk" ? "1,24млн" : "1.24M"), "Compact token format is wrong.");
                Check(FormatUtil.ApiValue(new CostEstimate { Value = 129.42m, KnownTokens = 1 }) == (language == "uk" ? "≈$129,42" : "≈$129.42"), "API dollar amounts must use explicit USD and selected decimal locale.");
                Check(UsageSignals.Comparison(null, null) == (language == "uk" ? "Завдання ще не вибране" : "No task selected yet"), "Empty overview did not change language.");
                Check(FormatUtil.ModelName(UserModel) == UserModel, "Original model name was changed.");
                SessionView fallback = new SessionView { IsFallbackTitle = true, IsFallbackRepository = true };
                Check(fallback.DisplayTitle == (language == "uk" ? "Завдання Codex" : "Codex task") && fallback.DisplayRepository == (language == "uk" ? "Невідомий репозиторій" : "Unknown repository"), "Synthetic fallback names were cached in the wrong language.");
                SessionView original = new SessionView { Title = "Codex task", Repository = "Unknown repository" };
                Check(original.DisplayTitle == "Codex task" && original.DisplayRepository == "Unknown repository", "User names that resemble fallback text were translated.");
                Check(FormatUtil.ModelList("gpt-6-sol, <unknown>").Contains(language == "uk" ? "модель невідома" : "model unavailable"), "Unknown-model list placeholder was not translated.");
                Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                Check(FormatUtil.Tokens(1240000) == (language == "uk" ? "1,24млн" : "1.24M"), "Formatting depends on ambient thread culture instead of selected locale.");
            }
        }

        private static void TestRuntimeSwitch(string home)
        {
            ViewSettings.Current = new ViewSettings { Language = "uk", Mode = 0, Motion = false };
            L10n.SetLanguage("uk");
            using (MainForm form = new MainForm(new[] { "--codex-home", home }))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-20000, -20000); form.ShowInTaskbar = false; form.TopMost = false;
                form.Show(); ((SessionEngine)Field(form, "_engine")).Dispose(); Application.DoEvents();
                MonitorSnapshot snapshot = Fixture();
                Apply(form, snapshot);
                ((ComboBox)Field(form, "_sessions")).SelectedIndex = 1;
                snapshot.RefreshedLocal = snapshot.RefreshedLocal.AddMinutes(6);
                snapshot.Sessions[1].SessionTotal.Total += 1240000;
                snapshot.Sessions[1].CurrentContext.Total += 5000;
                Apply(form, snapshot);
                Call(form, "SetView", 1);
                form.Size = new Size(760, 650);
                DataGridView grid = (DataGridView)Field(form, "_days");
                grid.ClearSelection(); grid.CurrentCell = grid.Rows[1].Cells[0]; grid.Rows[1].Selected = true;
                IList<UsageHistorySample> history = ((ContextChart)Field(form, "_contextChart")).History;
                int historyCount = history.Count;
                Rectangle bounds = form.Bounds;
                string originalTitle = ((Label)Field(form, "_taskTitle")).Text;
                foreach (string language in new[] { "en", "uk", "en" })
                {
                    ToolStripMenuItem choice = MenuItems(form.ContextMenuStrip.Items).FirstOrDefault(x => x.Text == (language == "en" ? "English" : "Українська"));
                    Check(choice != null, "Language submenu is absent."); choice.PerformClick(); Application.DoEvents();
                    Check(L10n.Language == language && ViewSettings.Current.Language == language && ViewSettings.Load(ViewSettings.FilePath).Language == language, "Language menu failed immediate application or persistence.");
                    Check((string)Field(form, "_selectedSessionId") == "agent-2" && (bool)Field(form, "_daysMode"), "Language switch reset selected agent or tab.");
                    Check(form.Bounds == bounds && !form.TopMost, "Language switch changed window geometry or pin.");
                    Check(((ContextChart)Field(form, "_contextChart")).History == history && history.Count == historyCount, "Language switch reset or appended graph history.");
                    Check(grid.SelectedRows.Count == 1 && ((DayView)grid.SelectedRows[0].Tag).Date == snapshot.Days[1].Date, "Language switch reset selected analytics day.");
                    Check(Convert.ToString(grid.Rows[0].Cells[0].Value) == (language == "en" ? "TODAY" : "СЬОГОДНІ"), "Analytics cached text did not refresh.");
                    Check(((Label)Field(form, "_taskTitle")).Text == originalTitle && originalTitle == UserTitle, "User task title was translated.");
                    Check(((Label)Field(form, "_repository")).Text.Contains(UserRepository) && ((Label)Field(form, "_repository")).Text.Contains(UserModel), "Repository/model names were changed.");
                    Apply(form, snapshot); // Same signature exercises refresh paths without data changes.
                    Check(((Button)Field(form, "_liveTab")).Text == (language == "en" ? "LIVE" : "ЗАРАЗ"), "Data refresh restored old language.");
                    Check(grid.Columns[0].HeaderText == (language == "en" ? "DAY" : "ДЕНЬ"), "Analytics header did not translate.");
                    Check(((TokenBar)Field(form, "_tokenBar")).Trend == (language == "en" ? "Δ5MIN +1.24M" : "Δ5ХВ +1,24млн"), "Graph delta did not refresh language.");
                    string contextHelp = ((ContextChart)Field(form, "_contextChart")).AccessibleDescription;
                    Check(contextHelp.Contains(L10n.T("UI.ContextTip")) && contextHelp.Contains(L10n.T("Appearance.ContextHelp")), "Context help lost graph meaning or motion instructions after switching language.");
                    if (language == "en") AssertEnglish(form);
                }
                MonitorSnapshot empty = new MonitorSnapshot { RefreshedLocal = DateTime.Now, Error = "CODEX_HOME not found" };
                Apply(form, empty);
                Check(((Label)Field(form, "_activity")).Text == "CODEX_HOME not found", "Known error is wrong in English.");
                Check(((Label)Field(form, "_taskTitle")).Text == "No Codex sessions yet", "English empty session state is wrong.");
                Check(((Label)Field(form, "_dayTitle")).Text == "No indexed days", "Empty analytics state is wrong.");
                AssertEnglish(form);
                MenuItems(form.ContextMenuStrip.Items).First(x => x.Text == "Українська").PerformClick();
                Check(((Label)Field(form, "_activity")).Text == "CODEX_HOME не знайдено", "Existing error did not translate immediately.");
                Check(HasUkrainian(((Label)Field(form, "_taskTitle")).Text), "Existing empty state did not translate immediately.");
                MenuItems(form.ContextMenuStrip.Items).First(x => x.Text == "English").PerformClick();
                empty.Error = "Synthetic IO failure"; Apply(form, empty);
                Check(((Label)Field(form, "_activity")).Text.Contains("Could not refresh session logs") && ((Label)Field(form, "_activity")).Text.Contains("Synthetic IO failure"), "Unknown errors need a translated prefix and original detail.");
                empty.Error = ""; empty.AvailableFiles = 250; empty.IndexedFiles = 10; empty.IndexTarget = 130; Apply(form, empty);
                Check(((Button)Field(form, "_loadOlderDays")).Text == "LOADING" && !((Button)Field(form, "_loadOlderDays")).Enabled, "Indexing state is wrong.");
                AssertEnglish(form);
                Call(form, "BeginLoadOlder");
                MenuItems(form.ContextMenuStrip.Items).First(x => x.Text == "Українська").PerformClick();
                Check(((Label)Field(form, "_activity")).Text == "Локальне індексування давнішої історії…" && !((Button)Field(form, "_loadOlderDays")).Enabled, "Transient indexing state was lost while changing language.");
                form.Size = new Size(400, 520);
                AssertButtonFits((Button)Field(form, "_loadOlderDays"));
                ((Button)Field(form, "_refresh")).PerformClick();
                MenuItems(form.ContextMenuStrip.Items).First(x => x.Text == "English").PerformClick();
                Check(((Label)Field(form, "_activity")).Text == "Refreshing…", "Transient refresh status was lost while changing language.");
                form.Close();
            }
            // A new form with persisted settings uses the saved language without reset.
            ViewSettings.Current = ViewSettings.Load(ViewSettings.FilePath);
            L10n.SetLanguage("uk");
            using (MainForm restarted = new MainForm(new[] { "--codex-home", home })) Check(L10n.Language == "en" && ((Button)Field(restarted, "_daysTab")).Text == "ANALYTICS", "Saved language did not apply on restart.");
        }

        private static MonitorSnapshot Fixture()
        {
            MonitorSnapshot result = new MonitorSnapshot { CodexHome = UserDirectory, RunningSessions = 2, AvailableFiles = 250, IndexedFiles = 10, IndexTarget = 10, RefreshedLocal = DateTime.Now.AddMinutes(-6) };
            for (int i = 1; i <= 2; i++)
            {
                TaskView task = new TaskView { SessionId = "agent-" + i, TurnId = "turn-" + i, Title = UserTitle, Repository = UserRepository, WorkingDirectory = UserDirectory, StartedLocal = DateTime.Now.AddMinutes(-10), Usage = new TokenUsage { Total = 150 } };
                result.Sessions.Add(new SessionView { Id = task.SessionId, Title = UserTitle, Repository = UserRepository, WorkingDirectory = UserDirectory, CurrentModel = UserModel, IsRunning = true, StartedLocal = task.StartedLocal, LastActivityLocal = DateTime.Now, CurrentOrLastTask = task, SessionTotal = new TokenUsage { Total = 1000 }, CurrentContext = new TokenUsage { Total = 100 }, ModelContextWindow = 258400, Accent = Theme.Cyan });
            }
            for (int i = 0; i < 3; i++) result.Days.Add(new DayView { Date = DateTime.Today.AddDays(-i), Tasks = 10 + i, Models = UserModel, Usage = new TokenUsage { Total = 1500 } });
            return result;
        }

        private static object Field(object target, string name) { return target.GetType().GetField(name, Private).GetValue(target); }
        private static void AssertButtonFits(Button button)
        {
            Size size = TextRenderer.MeasureText(button.Text, button.Font, new Size(Int32.MaxValue, Int32.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            Check(size.Width <= button.ClientSize.Width - 14 && size.Height <= button.ClientSize.Height - 4, "Localized button is clipped: " + button.Text + " needs " + size + ", has " + button.ClientSize);
        }
        private static void Call(object target, string name, params object[] args) { target.GetType().GetMethod(name, Private).Invoke(target, args); }
        private static void Apply(MainForm form, MonitorSnapshot snapshot) { Call(form, "ApplySnapshot", snapshot); }
        private static bool HasUkrainian(string text) { return Regex.IsMatch(text ?? "", "[А-Яа-яІіЇїЄєҐґ]"); }
        private static IEnumerable<ToolStripMenuItem> MenuItems(ToolStripItemCollection items)
        {
            foreach (ToolStripItem item in items)
            {
                ToolStripMenuItem menu = item as ToolStripMenuItem;
                if (menu == null) continue;
                yield return menu;
                foreach (ToolStripMenuItem child in MenuItems(menu.DropDownItems)) yield return child;
            }
        }
        private static string RemoveOriginals(string text)
        {
            return (text ?? "").Replace(UserDirectory, "").Replace(UserTitle, "").Replace(UserRepository, "").Replace(UserModel, "");
        }
        private static void AssertEnglish(MainForm form)
        {
            ToolTip tips = (ToolTip)Field(form, "_tips");
            AssertEnglishControl(form, tips);
            foreach (ToolStripMenuItem menu in MenuItems(form.ContextMenuStrip.Items)) if (menu.Text != "Українська") Check(!HasUkrainian(menu.Text), "Menu retained Ukrainian: " + menu.Text);
        }
        private static void AssertEnglishControl(Control root, ToolTip tips)
        {
            foreach (string text in new[] { root.Text, root.AccessibleDescription, root.AccessibleName, tips.GetToolTip(root) }) Check(!HasUkrainian(RemoveOriginals(text)), "English control/help retains Ukrainian: " + root.GetType().Name + " = " + text);
            foreach (Control child in root.Controls) AssertEnglishControl(child, tips);
        }
    }
}
