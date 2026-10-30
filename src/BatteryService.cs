using System;
using System.Collections.Generic;
using System.Threading;
using BatteryWidget.Interop;
using BatteryWidget.Providers;

namespace BatteryWidget
{
    public sealed class ReadingsEventArgs : EventArgs
    {
        public DeviceReadings Readings;

        public ReadingsEventArgs(DeviceReadings readings)
        {
            Readings = readings;
        }
    }

    public sealed class BatteryService : IDisposable
    {
        private readonly VgnKeyboardProvider _keyboard = new VgnKeyboardProvider();
        private readonly LogitechMouseProvider _mouse = new LogitechMouseProvider();
        private readonly BluetoothBatteryProvider _bluetooth = new BluetoothBatteryProvider();

        private readonly AutoResetEvent _signal = new AutoResetEvent(false);
        private readonly object _pendingLock = new object();
        private readonly Thread _worker;

        private const int PresenceGraceSeconds = 90;

        private volatile bool _stop;
        private bool _fullPending;
        private DeviceReading _lastKeyboard;
        private DeviceReading _lastMouse;
        private DeviceReading _lastGamepad;
        private DateTime _keyboardMissingSince = DateTime.MinValue;
        private DateTime _mouseMissingSince = DateTime.MinValue;

        public event EventHandler<ReadingsEventArgs> ReadingsReady;

        public BatteryService()
        {
            _worker = new Thread(Loop);
            _worker.IsBackground = true;
            _worker.Name = "BatteryWidget refresh";
            _worker.Start();
        }

        public void RequestFull()
        {
            lock (_pendingLock)
            {
                _fullPending = true;
            }
            _signal.Set();
        }

        public void RequestGamepadOnly()
        {
            _signal.Set();
        }

        private void Loop()
        {
            while (!_stop)
            {
                try
                {
                    _signal.WaitOne();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                if (_stop)
                {
                    break;
                }

                bool full;
                lock (_pendingLock)
                {
                    full = _fullPending;
                    _fullPending = false;
                }

                DeviceReadings readings;
                try
                {
                    readings = full ? ReadAll() : ReadGamepadOnly();
                }
                catch (Exception ex)
                {
                    Logger.Error("刷新失败", ex);
                    readings = BuildFailureReadings(ex.Message);
                }

                EventHandler<ReadingsEventArgs> handler = ReadingsReady;
                if (handler != null)
                {
                    try
                    {
                        handler(this, new ReadingsEventArgs(readings));
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("分发读取结果失败", ex);
                    }
                }
            }
        }

        private DeviceReadings ReadAll()
        {
            DateTime started = DateTime.Now;
            List<PnpBatteryInfo> bluetoothDevices = _bluetooth.Scan();

            DeviceReadings readings = new DeviceReadings();
            readings.Keyboard = UseCache(ApplyPresence(ReadKeyboard(bluetoothDevices),
                ref _keyboardMissingSince, "VGN V98 Pro V3", "键盘已断开"), ref _lastKeyboard);
            readings.Mouse = UseCache(ApplyPresence(ReadMouse(),
                ref _mouseMissingSince, "GPW2", "鼠标已断开"), ref _lastMouse);
            readings.Gamepad = UseCache(ReadGamepad(bluetoothDevices), ref _lastGamepad);
            readings.UpdatedAt = DateTime.Now;
            readings.GamepadOnly = false;

            Logger.Info(string.Format(
                "刷新完成({0}ms) 键盘={1} 鼠标={2} 手柄={3}",
                (int)(readings.UpdatedAt - started).TotalMilliseconds,
                readings.Keyboard,
                readings.Mouse,
                readings.Gamepad));

            return readings;
        }

        private DeviceReadings ReadGamepadOnly()
        {
            List<PnpBatteryInfo> bluetoothDevices = _bluetooth.Scan();
            DeviceReadings readings = new DeviceReadings();
            readings.Gamepad = UseCache(ReadGamepad(bluetoothDevices), ref _lastGamepad);
            readings.GamepadOnly = true;

            Logger.Info("全屏低频检查(仅手柄): " + readings.Gamepad);
            return readings;
        }

        private DeviceReading ReadKeyboard(List<PnpBatteryInfo> bluetoothDevices)
        {
            DeviceReading wireless = _keyboard.Read();
            if (wireless.State == ReadingState.Ok)
            {
                return wireless;
            }

            PnpBatteryInfo bluetooth = BluetoothBatteryProvider.FindKeyboard(bluetoothDevices);
            if (bluetooth != null && bluetooth.BatteryPercent.HasValue)
            {
                return DeviceReading.From("VGN V98 Pro V3", "蓝牙", bluetooth.BatteryPercent.Value,
                    ToChargeState(bluetooth.Charging));
            }

            if (_lastKeyboard != null && _lastKeyboard.Tag == "蓝牙")
            {
                return DeviceReading.Failure("VGN V98 Pro V3", ReadingState.NotConnected, "蓝牙键盘已断开");
            }

            return wireless;
        }

        private DeviceReading ReadMouse()
        {
            return _mouse.Read();
        }

        private static DeviceReading ReadGamepad(List<PnpBatteryInfo> bluetoothDevices)
        {
            PnpBatteryInfo bluetooth = BluetoothBatteryProvider.FindGamepad(bluetoothDevices);
            if (bluetooth != null && bluetooth.BatteryPercent.HasValue && XInput.AnyControllerConnected())
            {
                return DeviceReading.From("Xbox Series", "蓝牙", bluetooth.BatteryPercent.Value,
                    ToChargeState(bluetooth.Charging));
            }

            return DeviceReading.Failure("Xbox Series", ReadingState.NotConnected, "手柄已休眠或未连接");
        }

        private static DeviceReadings BuildFailureReadings(string detail)
        {
            DeviceReadings readings = new DeviceReadings();
            readings.Keyboard = DeviceReading.Failure("VGN V98 Pro V3", ReadingState.Error, detail);
            readings.Mouse = DeviceReading.Failure("GPW2", ReadingState.Error, detail);
            readings.Gamepad = DeviceReading.Failure("Xbox Series", ReadingState.Error, detail);
            return readings;
        }

        private static DeviceReading ApplyPresence(
            DeviceReading reading, ref DateTime missingSince, string name, string detail)
        {
            if (reading == null)
            {
                return null;
            }

            if (reading.State != ReadingState.NoResponse && reading.State != ReadingState.Error)
            {
                missingSince = DateTime.MinValue;
                return reading;
            }

            if (missingSince == DateTime.MinValue)
            {
                missingSince = DateTime.UtcNow;
            }
            if ((DateTime.UtcNow - missingSince).TotalSeconds >= PresenceGraceSeconds)
            {
                return DeviceReading.Failure(name, ReadingState.NotConnected, detail);
            }

            return reading;
        }

        private static DeviceReading UseCache(DeviceReading fresh, ref DeviceReading cache)
        {
            if (fresh != null && fresh.State == ReadingState.NotConnected)
            {
                cache = null;
                return fresh;
            }

            if (fresh != null && fresh.State == ReadingState.Ok)
            {
                cache = fresh;
                return fresh;
            }

            if (cache != null
                && cache.State == ReadingState.Ok
                && cache.Percent.HasValue
                && fresh != null
                && (fresh.State == ReadingState.NoResponse || fresh.State == ReadingState.Error))
            {
                DeviceReading stale = DeviceReading.From(cache.Name, cache.Tag, cache.Percent.Value, cache.Charge);
                stale.Stale = true;
                stale.Detail = "缓存值, 本次读取: " + fresh.Detail;
                return stale;
            }

            return fresh;
        }

        private static ChargeState ToChargeState(bool? charging)
        {
            if (!charging.HasValue)
            {
                return ChargeState.Unknown;
            }
            return charging.Value ? ChargeState.Charging : ChargeState.Discharging;
        }

        public void Dispose()
        {
            if (_stop)
            {
                return;
            }

            _stop = true;
            _signal.Set();
            if (_worker != null && _worker != Thread.CurrentThread)
            {
                _worker.Join(5000);
            }
            _signal.Close();
        }
    }
}
