using System;
using System.Runtime.InteropServices;
using System.Threading;

internal static class ClickTest
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
    private static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;

    private static void Main()
    {
        IntPtr window = FindWindow(null, "无线设备电量");
        if (window == IntPtr.Zero)
        {
            Console.WriteLine("widget not found");
            return;
        }

        Console.WriteLine("widget=0x" + window.ToInt64().ToString("X"));
        IntPtr point = new IntPtr(249 | (20 << 16));

        PostMessage(window, WM_LBUTTONDOWN, new IntPtr(1), point);
        Thread.Sleep(120);
        PostMessage(window, WM_LBUTTONUP, IntPtr.Zero, point);
        Console.WriteLine("clicked refresh at 249,20");
    }
}
