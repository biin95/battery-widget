using System;
using System.IO;
using System.Text;

namespace BatteryWidget
{
    public static class Logger
    {
        private const long MaxBytes = 512 * 1024;
        private static readonly object Sync = new object();

        public static string FilePath
        {
            get { return Path.Combine(WidgetConfig.FolderPath, "widget.log"); }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message, Exception exception)
        {
            if (exception == null)
            {
                Write("ERROR", message);
            }
            else
            {
                Write("ERROR", message + ": " + exception.GetType().Name + ": " + exception.Message);
            }
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    Directory.CreateDirectory(WidgetConfig.FolderPath);
                    string path = FilePath;
                    if (File.Exists(path) && new FileInfo(path).Length > MaxBytes)
                    {
                        string backup = Path.Combine(WidgetConfig.FolderPath, "widget.old.log");
                        if (File.Exists(backup))
                        {
                            File.Delete(backup);
                        }
                        File.Move(path, backup);
                    }

                    string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}\r\n", DateTime.Now, level, message);
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }
}
