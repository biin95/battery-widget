using System;
using System.Runtime.InteropServices;
using System.Text;
using BatteryWidget;
using BatteryWidget.Interop;

internal static class ScreenDiag
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);

    private static void Main()
    {
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        StringBuilder className = new StringBuilder(256);
        NativeMethods.GetClassName(foreground, className, className.Capacity);
        StringBuilder title = new StringBuilder(512);
        GetWindowText(foreground, title, title.Capacity);

        NativeMethods.RECT rect;
        NativeMethods.GetWindowRect(foreground, out rect);

        IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, 2);
        NativeMethods.MONITORINFO info = new NativeMethods.MONITORINFO();
        info.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
        NativeMethods.GetMonitorInfo(monitor, ref info);

        int state;
        int hr = NativeMethods.SHQueryUserNotificationState(out state);

        Console.WriteLine("foreground=0x{0:X} class='{1}' title='{2}'", foreground.ToInt64(), className, title);
        Console.WriteLine("visible={0} iconic={1} zoomed={2} style=0x{3:X8}",
            NativeMethods.IsWindowVisible(foreground), NativeMethods.IsIconic(foreground),
            NativeMethods.IsZoomed(foreground), NativeMethods.GetWindowLong(foreground, -16));
        Console.WriteLine("rect={0},{1},{2},{3}", rect.Left, rect.Top, rect.Right, rect.Bottom);
        Console.WriteLine("monitor={0},{1},{2},{3} qunsHr={4} qunsState={5}",
            info.rcMonitor.Left, info.rcMonitor.Top, info.rcMonitor.Right, info.rcMonitor.Bottom, hr, state);
        Console.WriteLine("fullscreen={0}", FullscreenDetector.IsFullscreen(IntPtr.Zero));

        IntPtr widget = FindWindow(null, "无线设备电量");
        Console.WriteLine("widget=0x{0:X} visible={1}", widget.ToInt64(),
            widget != IntPtr.Zero && NativeMethods.IsWindowVisible(widget));
    }
}
