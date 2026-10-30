using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class ForegroundTest
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attachFlag);

    private static readonly DateTime Start = DateTime.Now;
    private static Form _fullscreen;
    private static Form _small;

    private static void Log(string message)
    {
        Console.WriteLine(string.Format("[{0:HH:mm:ss.fff}] {1}", DateTime.Now, message));
    }

    private static void Main()
    {
        _fullscreen = new Form();
        _fullscreen.Text = "BatteryWidget 全屏测试";
        _fullscreen.FormBorderStyle = FormBorderStyle.None;
        _fullscreen.StartPosition = FormStartPosition.Manual;
        _fullscreen.Bounds = Screen.PrimaryScreen.Bounds;
        _fullscreen.BackColor = Color.FromArgb(16, 18, 24);
        _fullscreen.Show();

        _small = new Form();
        _small.Text = "BatteryWidget 普通窗口测试";
        _small.FormBorderStyle = FormBorderStyle.FixedToolWindow;
        _small.StartPosition = FormStartPosition.CenterScreen;
        _small.ClientSize = new Size(480, 240);
        _small.BackColor = Color.FromArgb(40, 44, 52);

        Timer timer = new Timer();
        timer.Interval = 1000;
        timer.Tick += delegate
        {
            double elapsed = (DateTime.Now - Start).TotalSeconds;
            if (elapsed >= 7.0 && !_small.Visible)
            {
                _small.Show();
                ForceForeground(_small.Handle);
                Log("阶段B: 普通窗口置前(全屏窗口仍在后台)");
                Log("foreground=" + DescribeForeground());
            }
            if (elapsed >= 15.0)
            {
                timer.Stop();
                _small.Close();
                _fullscreen.Close();
                Log("阶段C: 测试窗口全部关闭");
            }
        };

        _fullscreen.Shown += delegate
        {
            ForceForeground(_fullscreen.Handle);
            Log("阶段A: 全屏窗口置前");
            Log("foreground=" + DescribeForeground());
            timer.Start();
        };

        Application.Run();
        Log("退出");
    }

    private static string DescribeForeground()
    {
        IntPtr foreground = GetForegroundWindow();
        return "0x" + foreground.ToInt64().ToString("X");
    }

    private static void ForceForeground(IntPtr target)
    {
        IntPtr foreground = GetForegroundWindow();
        uint foregroundThread = GetWindowThreadProcessId(foreground, IntPtr.Zero);
        uint currentThread = GetCurrentThreadId();
        bool attached = false;

        try
        {
            if (foregroundThread != 0 && foregroundThread != currentThread)
            {
                attached = AttachThreadInput(currentThread, foregroundThread, true);
            }
            SetForegroundWindow(target);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }
}
