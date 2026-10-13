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
                    Application.Run(new MainForm(args));
                }
                catch (Exception ex)
                {
                    LogError(ex);
                    MessageBox.Show("The monitor could not start. Details were saved to the local error log.", "CODEX TOKEN MONITOR", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
