using System;
using System.Collections.Generic;
using System.Threading;
using BatteryWidget.Interop;

namespace Probe
{
    internal static class WakeTest
    {
        private static void Main()
        {
            List<HidDeviceInfo> devices = HidDevice.Enumerate();
            HidDeviceInfo target = null;
            foreach (HidDeviceInfo info in devices)
            {
                if (info.VendorId == 0x258A && info.ProductId == 0x024F
                    && info.Path.ToLowerInvariant().Contains("mi_01&col01"))
                {
                    target = info;
                    break;
                }
            }

            if (target == null)
            {
                Console.WriteLine("dongle not found");
                return;
            }

            byte[] request = BuildRequest();
            Console.WriteLine("target: " + target.Path);

            for (int attempt = 1; attempt <= 10; attempt++)
            {
                Console.WriteLine("--- attempt " + attempt + " " + DateTime.Now.ToString("HH:mm:ss.fff"));
                using (HidDevice hid = HidDevice.Open(target.Path))
                {
                    bool sent = hid.TrySend(request, 800);
                    Console.WriteLine("send=" + sent + " err=" + hid.LastWriteError);
                    List<byte[]> reports = hid.Collect(1200, 200);
                    if (reports.Count == 0)
                    {
                        Console.WriteLine("no response");
                    }
                    foreach (byte[] report in reports)
                    {
                        Console.WriteLine("rx " + HidDevice.FormatReport(report));
                    }
                }
                Thread.Sleep(1000);
            }
        }

        private static byte[] BuildRequest()
        {
            byte[] report = new byte[20];
            report[0] = 0x13;
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
    }
}
