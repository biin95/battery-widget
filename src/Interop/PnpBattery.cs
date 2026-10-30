using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace BatteryWidget.Interop
{
    public sealed class PnpBatteryInfo
    {
        public string InstanceId;
        public string FriendlyName;
        public int? BatteryPercent;
        public bool? Charging;
        public bool IsBleParent;
        public List<string> Children;

        public override string ToString()
        {
            return string.Format("{0} | {1} | battery={2} | charging={3} | children={4}",
                InstanceId,
                FriendlyName,
                BatteryPercent.HasValue ? BatteryPercent.Value.ToString() + "%" : "-",
                Charging.HasValue ? Charging.Value.ToString() : "-",
                Children == null ? "-" : Children.Count.ToString());
        }
    }

    public static class PnpBatteryReader
    {
        private static readonly Guid BatteryPropertySet = new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5");
        private static readonly Guid DevicePropertySet = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");
        private static readonly Guid DevicePropertySet2 = new Guid("540b947e-8b40-45bc-a8a2-6a0b894cbda2");
        private static readonly Guid DeviceParentChildSet = new Guid("4340a6c5-93fa-4706-972c-7b648008a5a7");

        private const uint DevPropTypeByte = 0x03;
        private const uint DevPropTypeBoolean = 0x11;
        private const uint DevPropTypeString = 0x12;
        private const uint DevPropTypeStringList = 0x2012;

        public static List<PnpBatteryInfo> ReadAll()
        {
            List<PnpBatteryInfo> result = new List<PnpBatteryInfo>();

            IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                IntPtr.Zero, null, IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_ALLCLASSES);

            if (deviceInfoSet == NativeMethods.INVALID_HANDLE_VALUE)
            {
                return result;
            }

            try
            {
                for (int index = 0; ; index++)
                {
                    NativeMethods.SP_DEVINFO_DATA deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
                    deviceInfoData.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVINFO_DATA));

                    if (!NativeMethods.SetupDiEnumDeviceInfo(deviceInfoSet, index, ref deviceInfoData))
                    {
                        break;
                    }

                    string instanceId = GetInstanceId(deviceInfoSet, ref deviceInfoData);
                    if (string.IsNullOrEmpty(instanceId))
                    {
                        continue;
                    }

                    if (!instanceId.StartsWith("BTHLE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    int? battery = GetByteProperty(deviceInfoSet, ref deviceInfoData, BatteryPropertySet, 2);
                    bool? charging = GetBoolProperty(deviceInfoSet, ref deviceInfoData, BatteryPropertySet, 3);

                    if (!battery.HasValue && !charging.HasValue)
                    {
                        continue;
                    }

                    string friendlyName = GetStringProperty(deviceInfoSet, ref deviceInfoData, DevicePropertySet, 14);
                    if (string.IsNullOrEmpty(friendlyName))
                    {
                        friendlyName = GetStringProperty(deviceInfoSet, ref deviceInfoData, DevicePropertySet, 2);
                    }
                    if (string.IsNullOrEmpty(friendlyName))
                    {
                        friendlyName = GetStringProperty(deviceInfoSet, ref deviceInfoData, DevicePropertySet2, 4);
                    }

                    PnpBatteryInfo info = new PnpBatteryInfo();
                    info.InstanceId = instanceId;
                    info.FriendlyName = friendlyName;
                    info.BatteryPercent = battery;
                    info.Charging = charging;
                    info.IsBleParent = instanceId.StartsWith("BTHLE\\", StringComparison.OrdinalIgnoreCase);
                    if (info.IsBleParent)
                    {
                        info.Children = GetStringListProperty(deviceInfoSet, ref deviceInfoData, DeviceParentChildSet, 9);
                    }
                    result.Add(info);
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return result;
        }

        private static string GetInstanceId(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVINFO_DATA deviceInfoData)
        {
            StringBuilder builder = new StringBuilder(512);
            int required;
            if (!NativeMethods.SetupDiGetDeviceInstanceId(
                deviceInfoSet, ref deviceInfoData, builder, builder.Capacity, out required))
            {
                return null;
            }
            return builder.ToString();
        }

        private static byte[] GetRawProperty(
            IntPtr deviceInfoSet,
            ref NativeMethods.SP_DEVINFO_DATA deviceInfoData,
            Guid propertySet,
            uint propertyId,
            out uint propertyType)
        {
            NativeMethods.DEVPROPKEY key = new NativeMethods.DEVPROPKEY();
            key.fmtid = propertySet;
            key.pid = propertyId;

            byte[] buffer = new byte[256];
            uint type;
            int required;
            bool ok = NativeMethods.SetupDiGetDeviceProperty(
                deviceInfoSet, ref deviceInfoData, ref key, out type,
                buffer, buffer.Length, out required, 0);

            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != 122 || required <= 0)
                {
                    propertyType = 0;
                    return null;
                }

                buffer = new byte[required];
                ok = NativeMethods.SetupDiGetDeviceProperty(
                    deviceInfoSet, ref deviceInfoData, ref key, out type,
                    buffer, buffer.Length, out required, 0);
                if (!ok)
                {
                    propertyType = 0;
                    return null;
                }
            }

            propertyType = type;
            return buffer;
        }

        private static int? GetByteProperty(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVINFO_DATA deviceInfoData, Guid propertySet, uint propertyId)
        {
            uint type;
            byte[] raw = GetRawProperty(deviceInfoSet, ref deviceInfoData, propertySet, propertyId, out type);
            if (raw == null)
            {
                return null;
            }

            if (type == DevPropTypeByte)
            {
                return raw[0];
            }

            return null;
        }

        private static bool? GetBoolProperty(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVINFO_DATA deviceInfoData, Guid propertySet, uint propertyId)
        {
            uint type;
            byte[] raw = GetRawProperty(deviceInfoSet, ref deviceInfoData, propertySet, propertyId, out type);
            if (raw == null)
            {
                return null;
            }

            if (type == DevPropTypeBoolean)
            {
                return raw[0] != 0;
            }

            return null;
        }

        private static string GetStringProperty(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVINFO_DATA deviceInfoData, Guid propertySet, uint propertyId)
        {
            uint type;
            byte[] raw = GetRawProperty(deviceInfoSet, ref deviceInfoData, propertySet, propertyId, out type);
            if (raw == null || type != DevPropTypeString)
            {
                return null;
            }

            string value = Encoding.Unicode.GetString(raw);
            int terminator = value.IndexOf('\0');
            if (terminator >= 0)
            {
                value = value.Substring(0, terminator);
            }
            return value;
        }

        private static List<string> GetStringListProperty(IntPtr deviceInfoSet, ref NativeMethods.SP_DEVINFO_DATA deviceInfoData, Guid propertySet, uint propertyId)
        {
            uint type;
            byte[] raw = GetRawProperty(deviceInfoSet, ref deviceInfoData, propertySet, propertyId, out type);
            if (raw == null || (type != DevPropTypeStringList && type != DevPropTypeString))
            {
                return null;
            }

            string value = Encoding.Unicode.GetString(raw);
            string[] parts = value.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }
            return new List<string>(parts);
        }
    }
}
