using System;
using System.Collections.Generic;
using BatteryWidget.Interop;

namespace BatteryWidget.Providers
{
    public sealed class LogitechMouseProvider
    {
        private const byte ReportId = 0x11;
        private const byte Slot = 0x01;
        private const byte SwId = 0x0F;
        private const byte RootFeatureIndex = 0x00;
        private const byte RootFeatureIdLow = 0x10;
        private const byte RootFeatureIdHigh = 0x04;
        private const byte BatteryStatusFunction = 0x01;

        internal static volatile int KnownFeatureIndex;

        public DeviceReading Read()
        {
            HidDeviceInfo target = FindReceiverLongCollection();
            if (target == null)
            {
                KnownFeatureIndex = 0;
                return DeviceReading.Failure("GPW2", ReadingState.NotConnected, "未找到 Lightspeed 接收器");
            }

            try
            {
                using (HidDevice device = HidDevice.Open(target.Path))
                {
                    int featureIndex = ResolveBatteryFeatureIndex(device);
                    if (featureIndex < 0)
                    {
                        return DeviceReading.Failure("GPW2", ReadingState.NoResponse, "鼠标未响应（可能休眠）");
                    }
                    if (featureIndex == 0)
                    {
                        KnownFeatureIndex = 0;
                        return DeviceReading.Failure("GPW2", ReadingState.Unsupported, "接收器不支持电量功能");
                    }

                    KnownFeatureIndex = featureIndex;
                    byte feature = (byte)featureIndex;
                    byte[] batteryResponse = device.Query(BuildReport(feature, BatteryStatusFunction),
                        delegate(byte[] report) { return IsBatteryResponse(report, feature); }, 900);
                    if (batteryResponse == null)
                    {
                        batteryResponse = device.Query(BuildReport(feature, BatteryStatusFunction),
                            delegate(byte[] report) { return IsBatteryResponse(report, feature); }, 900);
                    }
                    if (batteryResponse == null)
                    {
                        return DeviceReading.Failure("GPW2", ReadingState.NoResponse, "电量查询超时");
                    }

                    int percent = batteryResponse[4];
                    if (percent > 100)
                    {
                        return DeviceReading.Failure("GPW2", ReadingState.Error,
                            "电量字段异常: " + percent);
                    }

                    return DeviceReading.From("GPW2", "2.4G", percent, MapCharge(batteryResponse[6]));
                }
            }
            catch (Exception ex)
            {
                return DeviceReading.Failure("GPW2", ReadingState.Error, ex.Message);
            }
        }

        private static int ResolveBatteryFeatureIndex(HidDevice device)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                byte[] response = device.Query(
                    BuildReport(RootFeatureIndex, 0x00, RootFeatureIdLow, RootFeatureIdHigh),
                    IsRootResponse, 900);
                if (response != null)
                {
                    return response[4];
                }
            }
            return -1;
        }

        internal static HidDeviceInfo FindReceiverLongCollection()
        {
            List<HidDeviceInfo> devices = HidDevice.Enumerate();
            HidDeviceInfo fallback = null;

            for (int i = 0; i < devices.Count; i++)
            {
                HidDeviceInfo info = devices[i];
                if (info.VendorId != 0x046D)
                {
                    continue;
                }

                string path = info.Path.ToLowerInvariant();
                if (path.IndexOf("mi_02&col02", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (info.ProductId == 0xC547)
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

        private static byte[] BuildReport(byte featureIndex, byte function, params byte[] parameters)
        {
            byte[] report = new byte[20];
            report[0] = ReportId;
            report[1] = Slot;
            report[2] = featureIndex;
            report[3] = (byte)((function << 4) | SwId);

            if (parameters != null)
            {
                for (int i = 0; i < parameters.Length && 4 + i < report.Length; i++)
                {
                    report[4 + i] = parameters[i];
                }
            }
            return report;
        }

        private static bool IsRootResponse(byte[] report)
        {
            return report != null
                && report.Length >= 7
                && report[0] == ReportId
                && report[1] == Slot
                && report[2] == RootFeatureIndex
                && report[3] == SwId;
        }

        private static bool IsBatteryResponse(byte[] report, byte featureIndex)
        {
            return report != null
                && report.Length >= 8
                && report[0] == ReportId
                && report[1] == Slot
                && report[2] == featureIndex
                && report[3] == (byte)((BatteryStatusFunction << 4) | SwId);
        }

        // 0x1004 getStatus: [4] 电量百分比, [6] 充电状态。
        private static ChargeState MapCharge(byte status)
        {
            switch (status)
            {
                case 0x00:
                    return ChargeState.Discharging;
                case 0x01:
                case 0x02:
                    return ChargeState.Charging;
                case 0x03:
                    return ChargeState.Full;
                default:
                    return ChargeState.Unknown;
            }
        }
    }
}
