using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class RunnerDisplayProbe
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        public ushort SpecVersion, DriverVersion, Size, DriverExtra;
        public uint Fields;
        public int PositionX, PositionY;
        public uint Orientation, FixedOutput;
        public short Color, Duplex, YResolution, TTOption, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel, PelsWidth, PelsHeight, DisplayFlags, DisplayFrequency;
        public uint ICMMethod, ICMIntent, MediaType, DitherType, Reserved1, Reserved2, PanningWidth, PanningHeight;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplaySettings(string deviceName, int mode, ref DevMode value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int ChangeDisplaySettingsEx(string deviceName, ref DevMode mode, IntPtr window, uint flags, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    public static int Main(string[] args)
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
        bool prepare = args.Length == 1 && args[0] == "--prepare";
        if (prepare && (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted"))
            throw new InvalidOperationException("Display preparation is restricted to GitHub-hosted Actions runners.");
        Console.WriteLine("DISPLAY_PROBE OS=" + Environment.OSVersion + " interactive=" + SystemInformation.UserInteractive);
        Console.WriteLine("MAX_TRACK=" + SystemInformation.MaxWindowTrackSize + " VIRTUAL=" + SystemInformation.VirtualScreen);
        foreach (Screen screen in Screen.AllScreens)
        {
            Console.WriteLine("SCREEN " + screen.DeviceName + " bounds=" + screen.Bounds + " work=" + screen.WorkingArea);
            for (int index = 0; index < 256; index++)
            {
                DevMode mode = new DevMode();
                mode.Size = (ushort)Marshal.SizeOf(typeof(DevMode));
                if (!EnumDisplaySettings(screen.DeviceName, index, ref mode)) break;
                Console.WriteLine("MODE " + mode.PelsWidth + "x" + mode.PelsHeight + " @" + mode.DisplayFrequency + " bpp=" + mode.BitsPerPel);
                if (prepare && screen.Primary && mode.PelsWidth == 1280 && mode.PelsHeight == 1024 && mode.BitsPerPel == 32)
                {
                    int test = ChangeDisplaySettingsEx(screen.DeviceName, ref mode, IntPtr.Zero, 2, IntPtr.Zero);
                    if (test != 0) throw new InvalidOperationException("1280x1024 mode validation failed: " + test);
                    // Flags=0 changes this disposable runner session, without updating the registry.
                    int change = ChangeDisplaySettingsEx(screen.DeviceName, ref mode, IntPtr.Zero, 0, IntPtr.Zero);
                    if (change != 0) throw new InvalidOperationException("Display preparation failed: " + change);
                    Console.WriteLine("DISPLAY_PREPARED 1280x1024");
                    return 0;
                }
            }
        }
        if (prepare) throw new InvalidOperationException("Runner does not offer the required 1280x1024 display mode.");
        using (Form form = new Form())
        {
            form.FormBorderStyle = FormBorderStyle.None;
            foreach (Size requested in new[] { new Size(960, 820), new Size(1240, 900) })
            {
                form.Size = requested;
                Console.WriteLine("SIZE requested=" + requested + " actual=" + form.Size + " client=" + form.ClientSize);
            }
            using (Graphics graphics = form.CreateGraphics()) Console.WriteLine("DPI=" + graphics.DpiX + "x" + graphics.DpiY);
        }
        return 0;
    }
}
