using System;
using System.Runtime.InteropServices;
using System.Text;
using BatteryWidget.Interop;

namespace BatteryWidget
{
    public static class FullscreenDetector
    {
        private const int QunsRunningD3DFullScreen = 3;

        private const uint MonitorDefaultToNearest = 2;
        private const int GwlStyle = -16;
        private const int WsCaption = 0x00C00000;

        public static bool IsFullscreen(IntPtr ownHandle)
        {
            try
            {
                IntPtr foreground = NativeMethods.GetForegroundWindow();
                if (foreground == IntPtr.Zero || foreground == ownHandle)
                {
                    return false;
                }
                if (!NativeMethods.IsWindowVisible(foreground) || NativeMethods.IsIconic(foreground))
                {
                    return false;
                }

                string className = GetClassName(foreground);
                if (className == "Progman"
                    || className == "WorkerW"
                    || className == "Shell_TrayWnd"
                    || className == "Shell_SecondaryTrayWnd")
                {
                    return false;
                }

                int state;
                bool d3dFullscreen = NativeMethods.SHQueryUserNotificationState(out state) == 0
                    && state == QunsRunningD3DFullScreen;

                int style = NativeMethods.GetWindowLong(foreground, GwlStyle);
                if ((style & WsCaption) != 0 && NativeMethods.IsZoomed(foreground))
                {
                    // 普通最大化窗口不算全屏（任务栏可能自动隐藏）
                    return false;
                }

                NativeMethods.RECT windowRect;
                if (!NativeMethods.GetWindowRect(foreground, out windowRect))
                {
                    return false;
                }

                IntPtr monitor = NativeMethods.MonitorFromWindow(foreground, MonitorDefaultToNearest);
                if (monitor == IntPtr.Zero)
                {
                    return false;
                }

                NativeMethods.MONITORINFO monitorInfo = new NativeMethods.MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(NativeMethods.MONITORINFO));
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return false;
                }

                NativeMethods.RECT bounds = monitorInfo.rcMonitor;
                const int tolerance = 2;
                bool coversMonitor = windowRect.Left <= bounds.Left + tolerance
                    && windowRect.Top <= bounds.Top + tolerance
                    && windowRect.Right >= bounds.Right - tolerance
                    && windowRect.Bottom >= bounds.Bottom - tolerance;
                if (coversMonitor)
                {
                    return true;
                }

                // 独占全屏游戏可能使用低于桌面的分辨率，用 D3D 全屏状态补充判断
                int windowWidth = windowRect.Right - windowRect.Left;
                int windowHeight = windowRect.Bottom - windowRect.Top;
                int monitorWidth = bounds.Right - bounds.Left;
                int monitorHeight = bounds.Bottom - bounds.Top;
                bool mostlyCovers = windowWidth >= monitorWidth * 0.6
                    && windowHeight >= monitorHeight * 0.6;
                return d3dFullscreen && (style & WsCaption) == 0 && mostlyCovers;
            }
            catch
            {
                return false;
            }
        }

        private static string GetClassName(IntPtr window)
        {
            StringBuilder builder = new StringBuilder(256);
            int length = NativeMethods.GetClassName(window, builder, builder.Capacity);
            return length > 0 ? builder.ToString() : "";
        }
    }
}
