using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Windows.Forms;
using BatteryWidget;

internal static class Preview
{
    private static void Main()
    {
        WidgetConfig config = new WidgetConfig();
        config.WarnPercent = 30;
        config.CriticalPercent = 15;

        using (WidgetForm form = new WidgetForm(config))
        {
            form.CreateControl();
            IntPtr handle = form.Handle;

            Render(form, BuildReadings(
                    "VGN V98 Pro V3", "2.4G", 74, ChargeState.Discharging,
                    "GPW2", "2.4G", 15, ChargeState.Charging,
                    "Xbox Series", "蓝牙", 84, ChargeState.Discharging),
                false, @"E:\my_pro\build\preview-live.png");

            Render(form, BuildReadings(
                    "VGN V98 Pro V3", "2.4G", 100, ChargeState.Full,
                    "GPW2", "2.4G", 62, ChargeState.Discharging,
                    "Xbox Series", "蓝牙", 84, ChargeState.Discharging),
                false, @"E:\my_pro\build\preview-healthy.png");

            DeviceReadings stale = BuildReadings(
                "VGN V98 Pro V3", "2.4G", 24, ChargeState.Discharging,
                "GPW2", "2.4G", 15, ChargeState.Charging,
                "Xbox Series", "蓝牙", 0, ChargeState.Discharging);
            stale.Keyboard.Stale = true;
            stale.Gamepad = DeviceReading.Failure("Xbox Series", ReadingState.NotConnected, "未连接");
            Render(form, stale, false, @"E:\my_pro\build\preview-stale.png");

            Render(form, null, true, @"E:\my_pro\build\preview-loading.png");

            DeviceReadings empty = new DeviceReadings();
            empty.Keyboard = DeviceReading.Failure("VGN V98 Pro V3", ReadingState.NotConnected, "未连接");
            empty.Mouse = DeviceReading.Failure("GPW2", ReadingState.NotConnected, "未连接");
            empty.Gamepad = DeviceReading.Failure("Xbox Series", ReadingState.NotConnected, "未连接");
            Render(form, empty, false, @"E:\my_pro\build\preview-empty.png");

            GC.KeepAlive(handle);
        }

        Console.WriteLine("preview images written");
    }

    private static void Render(WidgetForm form, DeviceReadings readings, bool refreshing, string path)
    {
        SetField(form, "_readings", readings);
        SetField(form, "_refreshing", refreshing);
        SetField(form, "_lastUpdated", refreshing ? DateTime.MinValue : DateTime.Now);
        Invoke(form, "ApplyAdaptiveHeight");

        using (Bitmap bitmap = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        {
            form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
            bitmap.Save(path, ImageFormat.Png);
        }
        Console.WriteLine(path);
    }

    private static DeviceReadings BuildReadings(
        string keyboardName, string keyboardTag, int keyboardPercent, ChargeState keyboardCharge,
        string mouseName, string mouseTag, int mousePercent, ChargeState mouseCharge,
        string gamepadName, string gamepadTag, int gamepadPercent, ChargeState gamepadCharge)
    {
        DeviceReadings readings = new DeviceReadings();
        readings.Keyboard = DeviceReading.From(keyboardName, keyboardTag, keyboardPercent, keyboardCharge);
        readings.Mouse = DeviceReading.From(mouseName, mouseTag, mousePercent, mouseCharge);
        if (gamepadPercent > 0)
        {
            readings.Gamepad = DeviceReading.From(gamepadName, gamepadTag, gamepadPercent, gamepadCharge);
        }
        return readings;
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException("field not found: " + name);
        }
        field.SetValue(target, value);
    }

    private static void Invoke(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        if (method == null)
        {
            throw new InvalidOperationException("method not found: " + name);
        }
        method.Invoke(target, null);
    }
}
