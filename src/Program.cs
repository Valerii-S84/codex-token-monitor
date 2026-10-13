using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            EnableSharpDpiRendering();
            L10n.SetLanguage(ViewSettings.Current.Language);
            bool created;
            using (Mutex mutex = new Mutex(true, AppSettings.InstanceMutexName, out created))
            {
                if (!created)
                {
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e) { LogError(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { LogError(e.ExceptionObject as Exception); };
                try
                {
                    using (MainForm form = new MainForm(args))
                    {
                        // Let construction detect a fresh installation for initial layout,
                        // then persist its language before window settings mark it as existing.
                        ViewSettings.Current.Save();
                        Application.Run(form);
                    }
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    MessageBox.Show(L10n.T("Program.StartError"), "CODEX TOKEN MONITOR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void EnableSharpDpiRendering()
        {
            try
            {
                if (!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware();
            }
            catch (EntryPointNotFoundException)
            {
                try { SetProcessDPIAware(); }
                catch { }
            }
            catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private static void LogError(Exception exception)
        {
            if (exception == null) return;
            try
            {
                Directory.CreateDirectory(AppSettings.SettingsDirectory);
                File.AppendAllText(Path.Combine(AppSettings.SettingsDirectory, "errors.log"), DateTime.Now.ToString("s") + " " + exception + Environment.NewLine);
            }
            catch { }
        }
    }
}
