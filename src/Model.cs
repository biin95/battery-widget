using System;

namespace BatteryWidget
{
    public enum ReadingState
    {
        Ok,
        NotConnected,
        NoResponse,
        Error,
        Unsupported
    }

    public enum ChargeState
    {
        Unknown,
        Discharging,
        Charging,
        Full
    }

    public enum DockEdge
    {
        None,
        Left,
        Right,
        Top
    }

    public sealed class DeviceReading
    {
        public string Name;
        public string Tag;
        public int? Percent;
        public ChargeState Charge;
        public ReadingState State;
        public string Detail;
        public bool Stale;

        public static DeviceReading From(string name, string tag, int percent, ChargeState charge)
        {
            DeviceReading reading = new DeviceReading();
            reading.Name = name;
            reading.Tag = tag;
            reading.Percent = percent;
            reading.Charge = charge;
            reading.State = ReadingState.Ok;
            return reading;
        }

        public string ChargeText
        {
            get
            {
                if (State != ReadingState.Ok)
                {
                    return null;
                }
                if (Charge == ChargeState.Charging)
                {
                    return "充电中";
                }
                if (Charge == ChargeState.Full)
                {
                    return "已充满";
                }
                return null;
            }
        }

        public static DeviceReading Failure(string name, ReadingState state, string detail)
        {
            DeviceReading reading = new DeviceReading();
            reading.Name = name;
            reading.State = state;
            reading.Detail = detail;
            return reading;
        }

        public string StatusText
        {
            get
            {
                switch (State)
                {
                    case ReadingState.Ok:
                        return Percent.HasValue ? Percent.Value.ToString() + "%" : "--";
                    case ReadingState.NotConnected:
                        return "未连接";
                    case ReadingState.NoResponse:
                        return "无响应";
                    case ReadingState.Unsupported:
                        return "不支持";
                    default:
                        return "读取失败";
                }
            }
        }

        public override string ToString()
        {
            string tag = Tag;
            if (ChargeText != null)
            {
                tag = (String.IsNullOrEmpty(tag) ? "" : tag + "·") + ChargeText;
            }

            return string.Format("{0} [{1}] {2}{3} ({4})",
                Name,
                tag,
                Stale ? "≈" : "",
                State == ReadingState.Ok ? StatusText : State.ToString(),
                String.IsNullOrEmpty(Detail) ? "-" : Detail);
        }
    }

    public sealed class DeviceReadings
    {
        public DeviceReading Keyboard;
        public DeviceReading Mouse;
        public DeviceReading Gamepad;
        public DateTime UpdatedAt;
        public bool GamepadOnly;

        public DeviceReadings()
        {
            UpdatedAt = DateTime.Now;
        }
    }
}
