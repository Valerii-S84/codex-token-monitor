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
    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr value);

    [STAThread]
    public static int Main()
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
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
            }
        }
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
