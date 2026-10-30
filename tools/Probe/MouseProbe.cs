using System;
using System.Collections.Generic;
using BatteryWidget.Interop;

internal static class MouseProbe
{
    private static void Main()
    {
        List<HidDeviceInfo> devices = HidDevice.Enumerate();
        HidDeviceInfo target = null;
        for (int i = 0; i < devices.Count; i++)
        {
            HidDeviceInfo info = devices[i];
            if (info.VendorId != 0x046D)
            {
                continue;
            }
            if (info.Path.ToLowerInvariant().IndexOf("mi_02&col02", StringComparison.Ordinal) < 0)
            {
                continue;
            }
            if (info.ProductId == 0xC547)
            {
                target = info;
                break;
            }
            if (target == null)
            {
                target = info;
            }
        }

        if (target == null)
        {
            Console.WriteLine("receiver not found");
            return;
        }

        Console.WriteLine("Opening: " + target.Path);
        using (HidDevice device = HidDevice.Open(target.Path))
        {
            byte[] root = device.Query(Build(0x00, 0x00, 0x10, 0x04), delegate(byte[] r)
            {
                return r != null && r.Length >= 7 && r[0] == 0x11 && r[1] == 0x01 && r[2] == 0x00 && r[3] == 0x0F;
            }, 900);
            if (root == null)
            {
                Console.WriteLine("root query timeout");
                return;
            }
            Console.WriteLine("ROOT " + Hex(root));

            byte feature = root[4];
            byte[] battery = device.Query(Build(feature, 0x00), delegate(byte[] r)
            {
                return r != null && r.Length >= 6 && r[0] == 0x11 && r[1] == 0x01 && r[2] == feature && r[3] == 0x0F;
            }, 900);
            if (battery == null)
            {
                Console.WriteLine("battery query timeout");
                return;
            }
            Console.WriteLine("BATT " + Hex(battery));
            Console.WriteLine(string.Format("level={0}  bytes5..10={1:X2} {2:X2} {3:X2} {4:X2} {5:X2} {6:X2}",
                battery[4], battery[5], battery[6], battery[7], battery[8], battery[9], battery[10]));
        }
    }

    private static byte[] Build(byte featureIndex, byte function, params byte[] parameters)
    {
        byte[] report = new byte[20];
        report[0] = 0x11;
        report[1] = 0x01;
        report[2] = featureIndex;
        report[3] = (byte)((function << 4) | 0x0F);
        for (int i = 0; i < parameters.Length && 4 + i < report.Length; i++)
        {
            report[4 + i] = parameters[i];
        }
        return report;
    }

    private static string Hex(byte[] data)
    {
        string text = "";
        for (int i = 0; i < data.Length; i++)
        {
            if (i > 0) text += " ";
            text += data[i].ToString("X2");
        }
        return text;
    }
}
