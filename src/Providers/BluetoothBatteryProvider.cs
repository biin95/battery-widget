using System;
using System.Collections.Generic;
using BatteryWidget.Interop;

namespace BatteryWidget.Providers
{
    public sealed class BluetoothBatteryProvider
    {
        public List<PnpBatteryInfo> Scan()
        {
            try
            {
                List<PnpBatteryInfo> devices = PnpBatteryReader.ReadAll();
                for (int i = 0; i < devices.Count; i++)
                {
                    PnpBatteryInfo info = devices[i];
                    Logger.Info(string.Format("BLE设备: {0} | 电量={1} | 充电={2}",
                        String.IsNullOrEmpty(info.FriendlyName) ? "(未知)" : info.FriendlyName,
                        info.BatteryPercent.HasValue ? info.BatteryPercent.Value.ToString() + "%" : "-",
                        info.Charging.HasValue ? info.Charging.Value.ToString() : "-"));
                }
                return devices;
            }
            catch (Exception ex)
            {
                Logger.Error("枚举蓝牙电量失败", ex);
                return new List<PnpBatteryInfo>();
            }
        }

        public static PnpBatteryInfo FindGamepad(List<PnpBatteryInfo> devices)
        {
            return Find(devices, IsGamepad);
        }

        public static PnpBatteryInfo FindKeyboard(List<PnpBatteryInfo> devices)
        {
            return Find(devices, IsVgnKeyboard);
        }

        public static bool IsGamepad(PnpBatteryInfo info)
        {
            return Contains(info, "xbox") || Contains(info, "pid&0b13");
        }

        public static bool IsVgnKeyboard(PnpBatteryInfo info)
        {
            return Contains(info, "vgn") || Contains(info, "v98") || Contains(info, "pid&0040");
        }

        private static PnpBatteryInfo Find(List<PnpBatteryInfo> devices, Predicate<PnpBatteryInfo> match)
        {
            if (devices == null)
            {
                return null;
            }

            PnpBatteryInfo best = null;
            for (int i = 0; i < devices.Count; i++)
            {
                PnpBatteryInfo info = devices[i];
                if (!info.BatteryPercent.HasValue || !match(info))
                {
                    continue;
                }

                if (best == null)
                {
                    best = info;
                }
                else if (info.IsBleParent && !best.IsBleParent)
                {
                    best = info;
                }
            }
            return best;
        }

        private static bool Contains(PnpBatteryInfo info, string token)
        {
            if (info == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(info.FriendlyName)
                && info.FriendlyName.ToLowerInvariant().IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(info.InstanceId)
                && info.InstanceId.ToLowerInvariant().IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            if (info.Children != null)
            {
                for (int i = 0; i < info.Children.Count; i++)
                {
                    if (!string.IsNullOrEmpty(info.Children[i])
                        && info.Children[i].ToLowerInvariant().IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
