using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace BatteryWidget
{
    public sealed class WidgetConfig
    {
        public int RefreshSeconds;
        public int WarnPercent;
        public int CriticalPercent;
        public int? WindowX;
        public int? WindowY;
        public DockEdge Dock;

        public WidgetConfig()
        {
            RefreshSeconds = 60;
            WarnPercent = 30;
            CriticalPercent = 15;
        }

        public static string FolderPath
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "BatteryWidget");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(FolderPath, "config.ini"); }
        }

        public static WidgetConfig Load()
        {
            WidgetConfig config = new WidgetConfig();
            bool existed = File.Exists(FilePath);

            if (existed)
            {
                try
                {
                    string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (line.Length == 0 || line[0] == ';' || line[0] == '#' || line[0] == '[')
                        {
                            continue;
                        }

                        int equals = line.IndexOf('=');
                        if (equals <= 0)
                        {
                            continue;
                        }

                        string key = line.Substring(0, equals).Trim().ToLowerInvariant();
                        string value = line.Substring(equals + 1).Trim();
                        int number;

                        switch (key)
                        {
                            case "refreshseconds":
                                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                                {
                                    config.RefreshSeconds = number;
                                }
                                break;
                            case "warnpercent":
                                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                                {
                                    config.WarnPercent = number;
                                }
                                break;
                            case "criticalpercent":
                                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                                {
                                    config.CriticalPercent = number;
                                }
                                break;
                            case "windowx":
                                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                                {
                                    config.WindowX = number;
                                }
                                break;
                            case "windowy":
                                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                                {
                                    config.WindowY = number;
                                }
                                break;
                            case "dock":
                                switch (value.ToLowerInvariant())
                                {
                                    case "left":
                                        config.Dock = DockEdge.Left;
                                        break;
                                    case "right":
                                        config.Dock = DockEdge.Right;
                                        break;
                                    case "top":
                                        config.Dock = DockEdge.Top;
                                        break;
                                    default:
                                        config.Dock = DockEdge.None;
                                        break;
                                }
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("读取配置失败", ex);
                }
            }

            config.Normalize();

            if (!existed)
            {
                config.Save();
            }

            return config;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(FolderPath);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("; BatteryWidget 配置（保存本文件后重启程序生效）");
                builder.AppendLine("; RefreshSeconds：桌面可见时自动刷新的间隔秒数");
                builder.AppendLine("; WarnPercent / CriticalPercent：低电量的橙/红提示阈值");
                builder.AppendLine();
                builder.AppendLine("RefreshSeconds=" + RefreshSeconds.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("WarnPercent=" + WarnPercent.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine("CriticalPercent=" + CriticalPercent.ToString(CultureInfo.InvariantCulture));

                builder.AppendLine("Dock=" + Dock.ToString().ToLowerInvariant());

                if (WindowX.HasValue && WindowY.HasValue)
                {
                    builder.AppendLine("WindowX=" + WindowX.Value.ToString(CultureInfo.InvariantCulture));
                    builder.AppendLine("WindowY=" + WindowY.Value.ToString(CultureInfo.InvariantCulture));
                }

                File.WriteAllText(FilePath, builder.ToString(), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Logger.Error("保存配置失败", ex);
            }
        }

        private void Normalize()
        {
            if (RefreshSeconds < 5)
            {
                RefreshSeconds = 5;
            }
            if (RefreshSeconds > 3600)
            {
                RefreshSeconds = 3600;
            }

            if (WarnPercent < 1 || WarnPercent > 100)
            {
                WarnPercent = 30;
            }

            if (CriticalPercent < 0)
            {
                CriticalPercent = 0;
            }
            int maxCritical = Math.Max(0, WarnPercent - 1);
            if (CriticalPercent > maxCritical)
            {
                CriticalPercent = Math.Min(15, maxCritical);
            }
        }
    }
}
