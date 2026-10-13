using System;
using System.Collections;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class ReleaseBinaryTest
{
    [DllImport("user32.dll")] private static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException("Expected EXE path, source commit, and version.");
            try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string executable = Path.GetFullPath(args[0]);
            Assembly assembly = Assembly.LoadFrom(executable);
            Check(assembly.GetName().Version.ToString() == args[2] + ".0", "Assembly version mismatch.");
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(executable);
            Check(version.FileVersion == args[2] + ".0", "File version mismatch.");
            Check(version.ProductVersion == args[2] + "+" + args[1], "Embedded source commit mismatch.");
            bool sourceCommitFound = false;
            foreach (AssemblyMetadataAttribute metadata in assembly.GetCustomAttributes(typeof(AssemblyMetadataAttribute), false))
                if (metadata.Key == "SourceCommit" && metadata.Value == args[1]) sourceCommitFound = true;
            Check(sourceCommitFound, "SourceCommit assembly metadata missing.");
            System.Runtime.Versioning.TargetFrameworkAttribute target = (System.Runtime.Versioning.TargetFrameworkAttribute)
                Attribute.GetCustomAttribute(assembly, typeof(System.Runtime.Versioning.TargetFrameworkAttribute));
            Check(target != null && target.FrameworkName == ".NETFramework,Version=v4.8", "Unexpected target framework.");
            string home = Environment.GetEnvironmentVariable("CODEX_HOME");
            string data = Environment.GetEnvironmentVariable("CODEX_TOKEN_MONITOR_DATA_HOME");
            Check(!String.IsNullOrWhiteSpace(home) && Directory.Exists(Path.Combine(home, "sessions")), "Missing isolated logs.");
            Check(!String.IsNullOrWhiteSpace(data), "Missing isolated application data.");
            string timestamp = DateTimeOffset.Now.ToString("O");
            string log = Path.Combine(home, "sessions", "release-fixture.jsonl");
            string fixture = "{\"timestamp\":\"" + timestamp + "\",\"type\":\"session_meta\",\"payload\":{\"id\":\"release-fixture\",\"cwd\":\"C:\\\\release-fixture\"}}\n"
                + "{\"timestamp\":\"" + timestamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"task_started\",\"turn_id\":\"release-turn\"}}\n"
                + "{\"timestamp\":\"" + timestamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"info\":{\"total_token_usage\":{\"input_tokens\":100,\"cached_input_tokens\":20,\"output_tokens\":25,\"total_tokens\":125},\"model_context_window\":258400}}}\n";
            File.WriteAllText(log, fixture, new UTF8Encoding(false));

            Type settings = assembly.GetType("CodexTokenMonitor.AppSettings", true);
            Check((string)settings.GetProperty("SettingsDirectory").GetValue(null, null) == Path.GetFullPath(data), "Data isolation was ignored.");
            Type formType = assembly.GetType("CodexTokenMonitor.MainForm", true);
            using (Form form = (Form)Activator.CreateInstance(formType, new object[] { new[] { "--codex-home", home } }))
            {
                form.TopMost = false;
                form.ShowInTaskbar = false;
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-20000, -20000);
                form.Show();
                object engine = formType.GetField("_engine", Private).GetValue(form);
                DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                bool indexed = false;
                do
                {
                    Application.DoEvents();
                    object snapshot = engine.GetType().GetProperty("Latest").GetValue(engine, null);
                    IList sessions = (IList)snapshot.GetType().GetField("Sessions").GetValue(snapshot);
                    if (sessions.Count == 1)
                    {
                        object usage = snapshot.GetType().GetField("Today").GetValue(snapshot);
                        indexed = (long)usage.GetType().GetField("Total").GetValue(usage) == 125;
                    }
                    if (indexed) break;
                    Thread.Sleep(20);
                } while (DateTime.UtcNow < deadline);
                Check(indexed, "Final EXE did not index fixture tokens.");
                Check(((IList)formType.GetField("_agentCards", Private).GetValue(form)).Count == 1, "Fixture agent card missing.");
                MethodInfo applyMode = formType.GetMethod("ApplyDisplayMode", Private);
                foreach (int mode in new[] { 0, 1, 2 })
                {
                    applyMode.Invoke(form, new object[] { mode, false });
                    form.Refresh(); Application.DoEvents();
                    bool expanded = (bool)formType.GetProperty("IsExpandedDisplay", Private).GetValue(form, null);
                    Check(expanded == (mode != 0), "Final EXE layout mode mismatch: " + mode);
                }
                // Verify localization in the actual candidate assembly, not a source-built substitute.
                ((IDisposable)engine).Dispose();
                Type localization = assembly.GetType("CodexTokenMonitor.L10n", true);
                string[] catalogErrors = (string[])localization.GetMethod("ValidateCatalog", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                Check(catalogErrors.Length == 0, "Final EXE has incomplete translations.");
                MethodInfo applyLanguage = formType.GetMethod("ApplyLanguage", Private);
                Check(applyLanguage != null, "Final EXE has no immediate language switch.");
                formType.GetMethod("SetView", Private).Invoke(form, new object[] { 1 });
                Size retainedSize = form.Size;
                string retainedSession = (string)formType.GetField("_selectedSessionId", Private).GetValue(form);
                object retainedSnapshot = formType.GetField("_snapshot", Private).GetValue(form);
                object chart = formType.GetField("_contextChart", Private).GetValue(form);
                IList retainedHistory = (IList)chart.GetType().GetField("History").GetValue(chart);
                int retainedHistoryCount = retainedHistory.Count;
                DataGridView days = (DataGridView)formType.GetField("_days", Private).GetValue(form);
                object selectedDay = days.CurrentRow == null ? null : days.CurrentRow.Tag;
                foreach (string language in new[] { "uk", "en", "uk", "en" })
                {
                    applyLanguage.Invoke(form, new object[] { language, true });
                    Application.DoEvents();
                    string expectedTab = language == "uk" ? "АНАЛІТИКА" : "ANALYTICS";
                    Check(((Control)formType.GetField("_daysTab", Private).GetValue(form)).Text == expectedTab, "Final EXE navigation was not translated.");
                    Check(form.Size == retainedSize, "Language switch resized final EXE.");
                    Check((bool)formType.GetField("_daysMode", Private).GetValue(form), "Language switch lost analytics tab.");
                    Check((string)formType.GetField("_selectedSessionId", Private).GetValue(form) == retainedSession, "Language switch changed selected agent.");
                    Check(Object.ReferenceEquals(formType.GetField("_snapshot", Private).GetValue(form), retainedSnapshot), "Language switch replaced snapshot.");
                    Check(retainedHistory.Count == retainedHistoryCount && Object.ReferenceEquals(chart.GetType().GetField("History").GetValue(chart), retainedHistory), "Language switch reset chart history.");
                    Check(Object.Equals(days.CurrentRow == null ? null : days.CurrentRow.Tag, selectedDay), "Language switch changed selected day.");
                    Check(File.ReadAllText(Path.Combine(data, "appearance.ini")).Contains("language=" + language), "Language selection was not persisted by final EXE.");
                }
                form.Close();
            }
            Check(File.ReadAllText(log) == fixture, "Verification changed source logs.");
            Check(File.Exists(Path.Combine(data, "settings.ini")), "Isolated settings were not saved.");

            // Exercise the real entry point in a separate process after releasing the reflected form.
            using (Process process = Process.Start(new ProcessStartInfo(executable, "--codex-home \"" + home + "\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                try
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                    while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
                    { Thread.Sleep(50); process.Refresh(); }
                    Check(!process.HasExited && process.MainWindowHandle != IntPtr.Zero, "Final EXE entry point did not open its window (close an existing monitor before this verification).");
                    using (Process second = Process.Start(new ProcessStartInfo(executable, "--codex-home \"" + home + "\"")
                        { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    {
                        try { Check(second.WaitForExit(5000) && second.ExitCode == 0 && !process.HasExited, "Single-instance behavior failed."); }
                        finally { if (!second.HasExited) { second.Kill(); second.WaitForExit(); } }
                    }
                    Check(process.CloseMainWindow(), "Unable to close verification window normally.");
                    Check(process.WaitForExit(10000) && process.ExitCode == 0, "Final EXE did not exit cleanly.");
                }
                finally
                {
                    if (!process.HasExited) { process.Kill(); process.WaitForExit(); }
                }
            }
            Check(File.ReadAllText(log) == fixture, "Entry point changed source logs.");
            Check(!File.Exists(Path.Combine(data, "errors.log")), "Final EXE logged an error.");
            // Fresh startup must persist its Windows-derived default before settings.ini
            // can make the next launch resemble a pre-language installation.
            Type l10n = assembly.GetType("CodexTokenMonitor.L10n", true);
            string defaultLanguage = (string)l10n.GetMethod("DefaultLanguage", BindingFlags.Static | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null).Invoke(null, null);
            string freshData = Path.Combine(data, "fresh-startup");
            for (int launch = 0; launch < 2; launch++)
            {
                ProcessStartInfo start = new ProcessStartInfo(executable, "--codex-home \"" + home + "\"")
                    { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                start.EnvironmentVariables["CODEX_TOKEN_MONITOR_DATA_HOME"] = freshData;
                using (Process process = Process.Start(start))
                {
                    try
                    {
                        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
                        while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
                        { Thread.Sleep(50); process.Refresh(); }
                        Check(!process.HasExited && process.MainWindowHandle != IntPtr.Zero, "Fresh EXE startup failed.");
                        string appearance = Path.Combine(freshData, "appearance.ini");
                        Check(File.Exists(appearance) && File.ReadAllText(appearance).Contains("language=" + defaultLanguage), "Fresh EXE did not persist its Windows language default, launch " + launch);
                        Check(process.CloseMainWindow() && process.WaitForExit(10000) && process.ExitCode == 0, "Fresh EXE did not close cleanly.");
                        Check(File.Exists(Path.Combine(freshData, "settings.ini")), "Fresh EXE did not save window settings.");
                        Check(!File.Exists(Path.Combine(freshData, "errors.log")), "Fresh EXE logged an error.");
                    }
                    finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
                }
            }
            Console.WriteLine("RELEASE_BINARY_OK version=" + args[2] + " commit=" + args[1] + " tokens=125 layouts=3 languages=2 switches=4 state=retained startup=OK singleInstance=OK logs=unchanged");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
