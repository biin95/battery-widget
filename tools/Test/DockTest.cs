using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;

internal static class DockTest
{
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowW")]
    private static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out RECT rect);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr window);

    private const uint WM_ENTERSIZEMOVE = 0x0231;
    private const uint WM_EXITSIZEMOVE = 0x0232;
    private const uint WM_CLOSE = 0x0010;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    private static void Main(string[] args)
    {
        string command = args.Length > 0 ? args[0].ToLowerInvariant() : "info";

        if (command == "cursor")
        {
            POINT p;
            GetCursorPos(out p);
            Console.WriteLine("cursor=" + p.X + "," + p.Y);
            return;
        }

        if (command == "movecursor" && args.Length >= 3)
        {
            SetCursorPos(int.Parse(args[1]), int.Parse(args[2]));
            Console.WriteLine("cursor moved");
            return;
        }

        IntPtr window = FindWindow(null, "无线设备电量");
        if (window == IntPtr.Zero)
        {
            Console.WriteLine("widget not found");
            Environment.ExitCode = 2;
            return;
        }

        if (command == "close")
        {
            PostMessage(window, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(800);
            Console.WriteLine("alive=" + IsWindow(window));
            return;
        }

        if (command == "devicechange")
        {
            PostMessage(window, 0x0219, new IntPtr(0x0007), IntPtr.Zero);
            Thread.Sleep(500);
            Console.WriteLine("device-change posted");
            return;
        }

        if (command == "dock" && args.Length >= 2)
        {
            DockToEdge(window, args[1].ToLowerInvariant());
            return;
        }

        if (command == "undock" && args.Length >= 3)
        {
            SimulateDrag(window, int.Parse(args[1]), int.Parse(args[2]));
            return;
        }

        if (command == "move" && args.Length >= 3)
        {
            SetWindowPos(window, IntPtr.Zero, int.Parse(args[1]), int.Parse(args[2]), 0, 0,
                SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
            Thread.Sleep(120);
            PostMessage(window, WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(120);
            PostMessage(window, WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
            Thread.Sleep(300);
            PrintState(window, "move");
            return;
        }

        if (command == "pin")
        {
            POINT saved;
            GetCursorPos(out saved);

            RECT rect = GetRect(window);
            RECT work = GetWorkArea(window);
            RECT visible = Intersect(rect, work);
            int x = (visible.Left + visible.Right) / 2;
            int y = (visible.Top + visible.Bottom) / 2;

            for (int i = 0; i < 40; i++)
            {
                SetCursorPos(x, y);
                Thread.Sleep(20);
            }
            PrintState(window, "pinned " + x + "," + y);

            SetCursorPos(saved.X, saved.Y);
            Thread.Sleep(1200);
            PrintState(window, "after-leave");
            return;
        }

        if (command == "hovershot" && args.Length >= 2)
        {
            POINT saved;
            GetCursorPos(out saved);

            RECT rect = GetRect(window);
            RECT work = GetWorkArea(window);
            RECT visible = Intersect(rect, work);
            int x = (visible.Left + visible.Right) / 2;
            int y = (visible.Top + visible.Bottom) / 2;

            for (int i = 0; i < 32; i++)
            {
                SetCursorPos(x, y);
                Thread.Sleep(20);
            }

            PrintState(window, "pinned " + x + "," + y);

            using (Bitmap bitmap = new Bitmap(340, 215))
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.CopyFromScreen(2220, 1195, 0, 0, new Size(340, 215));
                bitmap.Save(args[1], ImageFormat.Png);
            }
            Console.WriteLine("saved " + args[1]);

            SetCursorPos(saved.X, saved.Y);
            Thread.Sleep(1200);
            PrintState(window, "after-leave");
            return;
        }

        if (command == "hover")
        {
            RECT rect = GetRect(window);
            RECT work = GetWorkArea(window);
            RECT visible = Intersect(rect, work);
            int x = (visible.Left + visible.Right) / 2;
            int y = (visible.Top + visible.Bottom) / 2;
            SetCursorPos(x, y);
            Thread.Sleep(600);
            PrintState(window, "hover " + x + "," + y);
            return;
        }

        if (command == "leave")
        {
            RECT work = GetWorkArea(window);
            SetCursorPos(work.Left + 80, work.Top + 160);
            Thread.Sleep(1000);
            PrintState(window, "leave");
            return;
        }

        if (command == "shot" && args.Length >= 6)
        {
            int x = int.Parse(args[1]);
            int y = int.Parse(args[2]);
            int width = int.Parse(args[3]);
            int height = int.Parse(args[4]);
            string path = args[5];
            int zoom = args.Length >= 7 ? int.Parse(args[6]) : 1;
            using (Bitmap source = new Bitmap(width, height))
            using (Bitmap bitmap = new Bitmap(width * zoom, height * zoom))
            {
                using (Graphics graphics = Graphics.FromImage(source))
                {
                    graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height));
                }
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                    graphics.DrawImage(source, 0, 0, bitmap.Width, bitmap.Height);
                }
                bitmap.Save(path, ImageFormat.Png);
            }
            Console.WriteLine("saved " + path);
            return;
        }

        PrintState(window, command);
    }

    private static void DockToEdge(IntPtr window, string edge)
    {
        RECT rect = GetRect(window);
        RECT work = GetWorkArea(window);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        int x = rect.Left;
        int y = rect.Top;

        if (edge == "right")
        {
            x = work.Right - width;
        }
        else if (edge == "left")
        {
            x = work.Left;
        }
        else if (edge == "top")
        {
            y = work.Top;
        }
        else if (edge == "bottom")
        {
            y = work.Bottom - height;
        }

        SimulateDrag(window, x, y);
    }

    private static void SimulateDrag(IntPtr window, int x, int y)
    {
        PostMessage(window, WM_ENTERSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(150);
        SetWindowPos(window, IntPtr.Zero, x, y, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        Thread.Sleep(150);
        PostMessage(window, WM_EXITSIZEMOVE, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(500);
        PrintState(window, "drag to " + x + "," + y);
    }

    private static RECT GetRect(IntPtr window)
    {
        RECT rect;
        GetWindowRect(window, out rect);
        return rect;
    }

    private static RECT GetWorkArea(IntPtr window)
    {
        IntPtr monitor = MonitorFromWindow(window, 2);
        MONITORINFO info = new MONITORINFO();
        info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
        GetMonitorInfo(monitor, ref info);
        return info.rcWork;
    }

    private static RECT Intersect(RECT a, RECT b)
    {
        RECT result = new RECT();
        result.Left = Math.Max(a.Left, b.Left);
        result.Top = Math.Max(a.Top, b.Top);
        result.Right = Math.Min(a.Right, b.Right);
        result.Bottom = Math.Min(a.Bottom, b.Bottom);
        return result;
    }

    private static void PrintState(IntPtr window, string label)
    {
        RECT rect = GetRect(window);
        RECT work = GetWorkArea(window);
        POINT cursor;
        GetCursorPos(out cursor);

        Console.WriteLine(string.Format(
            "{0}: rect={1},{2},{3},{4} work={5},{6},{7},{8} visible={9} cursor={10},{11}",
            label,
            rect.Left, rect.Top, rect.Right, rect.Bottom,
            work.Left, work.Top, work.Right, work.Bottom,
            IsWindowVisible(window), cursor.X, cursor.Y));
    }
}
