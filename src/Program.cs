using System;
using System.Threading;
using System.Windows.Forms;

namespace BatteryWidget
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool createdNew;
            using (Mutex mutex = new Mutex(true, "BatteryWidget_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += OnThreadException;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                Logger.Info("启动 BatteryWidget");

                WidgetConfig config = WidgetConfig.Load();
                Application.Run(new WidgetForm(config));

                GC.KeepAlive(mutex);
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            Logger.Error("界面线程异常", e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Error("未处理异常", e.ExceptionObject as Exception);
        }
    }
}
