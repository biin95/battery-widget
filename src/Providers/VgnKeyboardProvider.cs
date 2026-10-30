using System;
using System.Collections.Generic;
using BatteryWidget.Interop;
using System.Text;

namespace BatteryWidget.Providers
{
    public sealed class VgnKeyboardProvider
    {
        private const byte ReportId = 0x13;
        private const ushort VgnVendorId = 0x258A;
        private const ushort V98ProV3DongleProductId = 0x024F;

        public DeviceReading Read()
        {
            HidDeviceInfo target = FindDongle();
            if (target == null)
            {
                return DeviceReading.Failure("VGN V98 Pro V3", ReadingState.NotConnected, "未找到 2.4G 接收器");
            }

            try
            {
                using (HidDevice device = HidDevice.Open(target.Path))
                {
                    byte[] request = BuildBatteryRequest();
                    byte[] response = device.Query(request, IsBatteryResponse, 1200);
                    if (response == null)
                    {
                        return DeviceReading.Failure("VGN V98 Pro V3", ReadingState.NoResponse, "2.4G 键盘未响应");
                    }

                    int percent = response[5];
                    if (percent > 100)
                    {
                        return DeviceReading.Failure("VGN V98 Pro V3", ReadingState.Error,
                            "电量字段异常: " + percent);
                    }

                    bool charging = ((response[6] >> 4) & 0x0F) == 1;
                    return DeviceReading.From("VGN V98 Pro V3", "2.4G", percent,
                        charging ? ChargeState.Charging : ChargeState.Discharging);
                }
            }
            catch (Exception ex)
            {
                return DeviceReading.Failure("VGN V98 Pro V3", ReadingState.Error, ex.Message);
            }
        }

        private static HidDeviceInfo FindDongle()
        {
            List<HidDeviceInfo> devices = HidDevice.Enumerate();
            HidDeviceInfo fallback = null;

            for (int i = 0; i < devices.Count; i++)
            {
                HidDeviceInfo info = devices[i];
                if (info.VendorId != VgnVendorId)
                {
                    continue;
                }

                string path = info.Path.ToLowerInvariant();
                if (path.IndexOf("mi_01&col01", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (info.ProductId == V98ProV3DongleProductId)
                {
                    return info;
                }
                if (fallback == null)
                {
                    fallback = info;
                }
            }

            return fallback;
        }

        private static byte[] BuildBatteryRequest()
        {
            byte[] report = new byte[20];
            report[0] = ReportId;
            report[1] = 0x4A;
            report[2] = 0x01;

            byte crc = 0;
            for (int i = 0; i < 19; i++)
            {
                crc += report[i];
            }
            report[19] = crc;
            return report;
        }

        private static bool IsBatteryResponse(byte[] report)
        {
            if (report == null || report.Length < 20)
            {
                return false;
            }
            if (report[0] != 0x13 || report[1] != 0x4A || report[2] != 0x01)
            {
                return false;
            }

            byte crc = 0;
            for (int i = 0; i < 19; i++)
            {
                crc += report[i];
            }
            return crc == report[19];
        }
    }
}
