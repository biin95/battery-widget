using System;
using System.Collections.Generic;
using System.Threading;
using BatteryWidget.Interop;

namespace BatteryWidget.Probe
{
    internal static class Program
    {
        private static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Console.WriteLine("===== HID interfaces =====");
            List<HidDeviceInfo> devices = HidDevice.Enumerate();
            foreach (HidDeviceInfo device in devices)
            {
                if (device.VendorId == 0x258A || device.VendorId == 0x046D)
                {
                    Console.WriteLine(string.Format("{0:X4}:{1:X4}  {2}", device.VendorId, device.ProductId, device.Path));
                    if (!string.IsNullOrEmpty(device.InstanceId))
                    {
                        Console.WriteLine("    " + device.InstanceId);
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("===== VGN 2.4G keyboard =====");
            ProbeVgn(devices);

            Console.WriteLine();
            Console.WriteLine("===== Logitech receiver =====");
            ProbeLogitech(devices);

            Console.WriteLine();
            Console.WriteLine("===== BLE PnP battery =====");
            List<PnpBatteryInfo> batteries = PnpBatteryReader.ReadAll();
            if (batteries.Count == 0)
            {
                Console.WriteLine("(none)");
            }
            else
            {
                foreach (PnpBatteryInfo info in batteries)
                {
                    Console.WriteLine(info.ToString());
                }
            }

            Console.WriteLine();
            Console.WriteLine("Done.");
        }

        private static void ProbeVgn(List<HidDeviceInfo> devices)
        {
            List<HidDeviceInfo> candidates = new List<HidDeviceInfo>();
            foreach (HidDeviceInfo device in devices)
            {
                if (device.VendorId == 0x258A && device.ProductId == 0x024F &&
                    device.Path.ToLowerInvariant().Contains("mi_01&col01"))
                {
                    candidates.Add(device);
                }
            }

            if (candidates.Count == 0)
            {
                Console.WriteLine("VGN dongle MI_01 COL01 not found.");
                return;
            }

            foreach (HidDeviceInfo candidate in candidates)
            {
                Console.WriteLine("Opening: " + candidate.Path);
                try
                {
                    using (HidDevice hid = HidDevice.Open(candidate.Path))
                    {
                        Console.WriteLine(string.Format("caps: usagePage=0x{0:X4} usage=0x{1:X4} in={2} out={3} feature={4}",
                            hid.UsagePage, hid.Usage, hid.InputReportByteLength, hid.OutputReportByteLength, hid.FeatureReportByteLength));

                        byte[] request = BuildVgnBatteryRequest();
                        Console.WriteLine("TX " + HidDevice.FormatReport(request));
                        hid.Send(request);

                        List<byte[]> reports = hid.Collect(1200, 250);
                        if (reports.Count == 0)
                        {
                            Console.WriteLine("RX (timeout)");
                        }
                        foreach (byte[] report in reports)
                        {
                            Console.WriteLine("RX " + HidDevice.FormatReport(report));
                            if (report.Length >= 7 && report[0] == 0x13 && report[1] == 0x4A)
                            {
                                Console.WriteLine(string.Format("battery={0}% chargingNibble={1}", report[5], (report[6] >> 4) & 0x0F));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                }
            }
        }

        private static byte[] BuildVgnBatteryRequest()
        {
            byte[] command = new byte[19];
            command[0] = 0x4A;
            command[1] = 0x01;

            byte crc = 0x13;
            for (int i = 0; i < command.Length; i++)
            {
                crc += command[i];
            }
            command[command.Length - 1] = crc;

            byte[] report = new byte[20];
            report[0] = 0x13;
            Array.Copy(command, 0, report, 1, command.Length);
            return report;
        }

        private static void ProbeLogitech(List<HidDeviceInfo> devices)
        {
            HidDeviceInfo longCollection = null;

            foreach (HidDeviceInfo device in devices)
            {
                if (device.VendorId == 0x046D && device.ProductId == 0xC547 &&
                    device.Path.ToLowerInvariant().Contains("mi_02&col02"))
                {
                    longCollection = device;
                    break;
                }
            }

            if (longCollection == null)
            {
                Console.WriteLine("Logitech receiver MI_02 COL02 not found.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine("Opening: " + longCollection.Path);
            try
            {
                using (HidDevice hid = HidDevice.Open(longCollection.Path))
                {
                    Console.WriteLine(string.Format("caps: usagePage=0x{0:X4} usage=0x{1:X4} in={2} out={3} feature={4}",
                        hid.UsagePage, hid.Usage, hid.InputReportByteLength, hid.OutputReportByteLength, hid.FeatureReportByteLength));

                    int[] slots = new int[] { 1, 2, 3, 4, 5, 6, 0xFF };
                    foreach (int slot in slots)
                    {
                        Console.WriteLine();
                        Console.WriteLine("--- HID++ slot " + slot + " ---");
                        int feature1004 = QueryFeature(hid, slot, 0x1004, true);
                        if (feature1004 <= 0)
                        {
                            continue;
                        }

                        QueryBattery(hid, slot, feature1004, true);

                        int feature1000 = QueryFeature(hid, slot, 0x1000, true);
                        if (feature1000 > 0)
                        {
                            QueryBattery(hid, slot, feature1000, true);
                        }

                        int feature1001 = QueryFeature(hid, slot, 0x1001, true);
                        if (feature1001 > 0)
                        {
                            QueryBattery(hid, slot, feature1001, true);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.Message);
            }
        }

        private static int QueryFeature(HidDevice hid, int slot, int featureId, bool useLong)
        {
            byte[] request = new byte[useLong ? 20 : 7];
            request[0] = useLong ? (byte)0x11 : (byte)0x10;
            request[1] = (byte)slot;
            request[2] = 0x00;
            request[3] = 0x0F;
            request[4] = (byte)((featureId >> 8) & 0xFF);
            request[5] = (byte)(featureId & 0xFF);

            Console.WriteLine("TX " + HidDevice.FormatReport(request));
            hid.ClearReports();
            if (!hid.TrySend(request, 500))
            {
                Console.WriteLine("TX failed, win32=" + hid.LastWriteError);
                return 0;
            }

            List<byte[]> reports = hid.Collect(650, 150);
            if (reports.Count == 0)
            {
                Console.WriteLine("RX (timeout)");
                return 0;
            }

            int featureIndex = 0;
            foreach (byte[] report in reports)
            {
                Console.WriteLine("RX " + HidDevice.FormatReport(report));
                if (report.Length >= 7 && report[1] == slot && report[2] == 0x00 && report[4] > 0)
                {
                    featureIndex = report[4];
                }
            }
            return featureIndex;
        }

        private static void QueryBattery(HidDevice hid, int slot, int featureIndex, bool useLong)
        {
            byte[] request = new byte[useLong ? 20 : 7];
            request[0] = useLong ? (byte)0x11 : (byte)0x10;
            request[1] = (byte)slot;
            request[2] = (byte)featureIndex;
            request[3] = 0x0F;

            Console.WriteLine("TX (battery " + (useLong ? "long" : "short") + ") " + HidDevice.FormatReport(request));
            hid.ClearReports();
            if (!hid.TrySend(request, 500))
            {
                Console.WriteLine("TX failed, win32=" + hid.LastWriteError);
                return;
            }

            List<byte[]> reports = hid.Collect(650, 150);
            if (reports.Count == 0)
            {
                Console.WriteLine("RX (timeout)");
                return;
            }
            foreach (byte[] report in reports)
            {
                Console.WriteLine("RX " + HidDevice.FormatReport(report));
            }
        }
    }
}
