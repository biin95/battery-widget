using System;
using System.Collections.Generic;
using System.Threading;
using BatteryWidget.Interop;

namespace BatteryWidget.Providers
{
    public sealed class LogitechMouseMonitor : IDisposable
    {
        private Thread _thread;
        private volatile bool _stop;

        public event EventHandler ChargingChanged;

        public void Start()
        {
            if (_thread != null)
            {
                return;
            }

            _thread = new Thread(Loop);
            _thread.IsBackground = true;
            _thread.Name = "GPW2 charge monitor";
            _thread.Start();
        }

        private void Loop()
        {
            while (!_stop)
            {
                int featureIndex = LogitechMouseProvider.KnownFeatureIndex;
                HidDeviceInfo receiver = featureIndex > 0
                    ? LogitechMouseProvider.FindReceiverLongCollection()
                    : null;
                if (receiver == null)
                {
                    Sleep(2000);
                    continue;
                }

                try
                {
                    using (HidDevice device = HidDevice.Open(receiver.Path))
                    {
                        Logger.Info("鼠标充电状态监听已启动");
                        int lastStatus = -1;
                        while (!_stop && device.IsAlive)
                        {
                            int latestFeature = LogitechMouseProvider.KnownFeatureIndex;
                            if (latestFeature > 0 && latestFeature != featureIndex)
                            {
                                break;
                            }

                            List<byte[]> reports = device.Collect(500, 50);
                            for (int i = 0; i < reports.Count; i++)
                            {
                                byte[] report = reports[i];
                                if (!IsChargeEvent(report, (byte)featureIndex))
                                {
                                    continue;
                                }

                                int status = report[6];
                                if (status == lastStatus)
                                {
                                    continue;
                                }

                                lastStatus = status;
                                Logger.Info("鼠标充电状态事件: " + status);
                                EventHandler handler = ChargingChanged;
                                if (handler != null)
                                {
                                    handler(this, EventArgs.Empty);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (!_stop)
                    {
                        Logger.Error("鼠标充电状态监听中断", ex);
                    }
                }

                Sleep(1000);
            }
        }

        private static bool IsChargeEvent(byte[] report, byte featureIndex)
        {
            return report != null
                && report.Length >= 8
                && report[0] == 0x11
                && report[1] == 0x01
                && report[2] == featureIndex
                && report[3] == 0x00;
        }

        private void Sleep(int milliseconds)
        {
            int elapsed = 0;
            while (!_stop && elapsed < milliseconds)
            {
                Thread.Sleep(100);
                elapsed += 100;
            }
        }

        public void Dispose()
        {
            _stop = true;
            Thread thread = _thread;
            if (thread != null && thread != Thread.CurrentThread)
            {
                thread.Join(800);
            }
        }
    }
}
