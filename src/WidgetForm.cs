using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BatteryWidget.Interop;
using BatteryWidget.Providers;

namespace BatteryWidget
{
    public sealed class WidgetForm : Form
    {
        private const int WM_NCLBUTTONDOWN = 0x00A1;
        private const int WM_DEVICECHANGE = 0x0219;
        private const int WM_ENTERSIZEMOVE = 0x0231;
        private const int WM_EXITSIZEMOVE = 0x0232;
        private const int DBT_DEVNODES_CHANGED = 0x0007;
        private const int HTCAPTION = 2;
        private const int BaseWidth = 304;
        private const int BaseHeight = 168;
        private const int RowTopBase = 40;
        private const int RowStepBase = 36;
        private const int ContentTopBase = 60;
        private const int GamepadFullscreenCheckSeconds = 180;
        private const int DockStripBase = 14;
        private const int DockSnapBase = 10;
        private const int DockAnimMs = 150;
        private const int DockHideDelayMs = 450;
        private const int DockRevealGuardMs = 400;

        private static readonly Color TitleColor = Color.FromArgb(139, 149, 167);
        private static readonly Color NameColor = Color.FromArgb(231, 236, 245);
        private static readonly Color TagColor = Color.FromArgb(124, 135, 152);
        private static readonly Color ValueColor = Color.FromArgb(233, 238, 246);
        private static readonly Color MutedColor = Color.FromArgb(113, 124, 141);
        private static readonly Color HintColor = Color.FromArgb(107, 116, 133);
        private static readonly Color ErrorColor = Color.FromArgb(203, 138, 138);
        private static readonly Color WarnColor = Color.FromArgb(242, 184, 75);
        private static readonly Color CriticalColor = Color.FromArgb(255, 107, 107);
        private static readonly Color HealthyBarColor = Color.FromArgb(88, 201, 142);
        private static readonly Color ChargingColor = Color.FromArgb(101, 178, 255);
        private static readonly Color GripColor = Color.FromArgb(116, 128, 148);
        private static readonly Color TrackColor = Color.FromArgb(38, 44, 55);
        private static readonly Color DividerColor = Color.FromArgb(35, 40, 49);

        private readonly WidgetConfig _config;
        private readonly BatteryService _service;
        private readonly LogitechMouseMonitor _mouseMonitor;
        private readonly Timer _refreshTimer;
        private readonly Timer _fullscreenTimer;
        private readonly Timer _dockTimer;
        private readonly ContextMenuStrip _contextMenu;

        private readonly Font _titleFont;
        private readonly Font _nameFont;
        private readonly Font _tagFont;
        private readonly Font _percentFont;
        private readonly Font _footerFont;
        private readonly Font _iconFont;

        private float _scale = 1f;
        private bool _positionInitialized;
        private bool _inFullscreen;
        private bool _refreshing;
        private bool _refreshQueued;
        private bool _closed;
        private bool _hoverRefresh;
        private bool _hoverClose;
        private bool _pressedRefresh;
        private bool _pressedClose;
        private DateTime _lastUpdated = DateTime.MinValue;
        private DateTime _lastGamepadCheck = DateTime.MinValue;
        private DateTime _lastRequestAt = DateTime.MinValue;
        private DateTime _lastExternalRefresh = DateTime.MinValue;
        private bool? _gamepadLinkConnected;
        private DeviceReadings _readings;

        private DockEdge _dockEdge = DockEdge.None;
        private Rectangle _dockArea;
        private bool _dockHidden;
        private bool _dragging;
        private bool _dragMoved;
        private Point _dragStartLocation;
        private bool _animating;
        private int _animFromX;
        private int _animFromY;
        private int _animToX;
        private int _animToY;
        private DateTime _animStart = DateTime.MinValue;
        private bool _hidePending;
        private DateTime _hideAt = DateTime.MinValue;
        private DateTime _hiddenAt = DateTime.MinValue;

        public WidgetForm(WidgetConfig config)
        {
            _config = config;

            Text = "无线设备电量";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(17, 20, 26);
            DoubleBuffered = true;
            Cursor = Cursors.SizeAll;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Opaque, true);

            _titleFont = new Font("Microsoft YaHei UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            _nameFont = new Font("Microsoft YaHei UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
            _tagFont = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            _percentFont = new Font("Microsoft YaHei UI", 11.5f, FontStyle.Bold, GraphicsUnit.Point);
            _footerFont = new Font("Microsoft YaHei UI", 7.5f, FontStyle.Regular, GraphicsUnit.Point);
            _iconFont = new Font("Segoe MDL2 Assets", 10f, FontStyle.Regular, GraphicsUnit.Point);

            _contextMenu = new ContextMenuStrip();
            _contextMenu.ShowImageMargin = false;
            _contextMenu.Items.Add("立即刷新", null, OnMenuRefresh);
            _contextMenu.Items.Add(new ToolStripSeparator());
            _contextMenu.Items.Add("退出", null, OnMenuExit);
            ContextMenuStrip = _contextMenu;

            _refreshTimer = new Timer();
            _refreshTimer.Interval = Math.Max(5, _config.RefreshSeconds) * 1000;
            _refreshTimer.Tick += OnRefreshTimerTick;

            _fullscreenTimer = new Timer();
            _fullscreenTimer.Interval = 500;
            _fullscreenTimer.Tick += OnFullscreenTimerTick;

            _dockTimer = new Timer();
            _dockTimer.Interval = 16;
            _dockTimer.Tick += OnDockTimerTick;

            _service = new BatteryService();
            _service.ReadingsReady += OnServiceReadingsReady;

            _mouseMonitor = new LogitechMouseMonitor();
            _mouseMonitor.ChargingChanged += OnMouseChargingChanged;
            _mouseMonitor.Start();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return parameters;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            using (Graphics graphics = CreateGraphics())
            {
                _scale = graphics.DpiX / 96f;
            }

            ClientSize = new Size(P(BaseWidth), P(BaseHeight));
            ApplyRegion();

            if (!_positionInitialized)
            {
                _positionInitialized = true;
                PlaceWindow();
                RestoreDockState();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            _refreshTimer.Start();
            _fullscreenTimer.Start();

            Logger.Info("窗口已显示，位置=" + Location.X + "," + Location.Y
                + " 尺寸=" + Width + "x" + Height + " 自动刷新=" + _config.RefreshSeconds + "s");

            _inFullscreen = FullscreenDetector.IsFullscreen(Handle);
            if (_inFullscreen)
            {
                _lastGamepadCheck = DateTime.UtcNow;
                Logger.Info("启动时处于全屏状态，窗口保持隐藏");
                Hide();
            }
            else
            {
                RequestRefresh();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_closed)
            {
                _closed = true;
                _refreshTimer.Stop();
                _fullscreenTimer.Stop();
                _dockTimer.Stop();
                _mouseMonitor.ChargingChanged -= OnMouseChargingChanged;
                _mouseMonitor.Dispose();
                _service.ReadingsReady -= OnServiceReadingsReady;
                _service.Dispose();

                SaveConfig();
                Logger.Info("程序退出");
            }

            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer.Dispose();
                _fullscreenTimer.Dispose();
                _dockTimer.Dispose();
                _contextMenu.Dispose();
                _titleFont.Dispose();
                _nameFont.Dispose();
                _tagFont.Dispose();
                _percentFont.Dispose();
                _footerFont.Dispose();
                _iconFont.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            int width = ClientSize.Width;
            int height = ClientSize.Height;

            RectangleF card = new RectangleF(0.5f, 0.5f, width - 1f, height - 1f);
            using (GraphicsPath cardPath = RoundedRect(card, P(14)))
            {
                using (LinearGradientBrush background = new LinearGradientBrush(
                    new RectangleF(0, 0, width, height),
                    Color.FromArgb(26, 30, 38),
                    Color.FromArgb(16, 19, 25),
                    90f))
                {
                    graphics.FillPath(background, cardPath);
                }
                using (Pen border = new Pen(Color.FromArgb(43, 49, 60), 1f))
                {
                    graphics.DrawPath(border, cardPath);
                }
            }

            TextRenderer.DrawText(graphics, "无线设备电量", _titleFont,
                new Rectangle(P(18), P(9), P(190), P(22)), TitleColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

            DrawIconButton(graphics, GetRefreshRect(), "\uE72C", _hoverRefresh || _pressedRefresh, _refreshing, false);
            DrawIconButton(graphics, GetCloseRect(), "\uE8BB", _hoverClose || _pressedClose, false, true);

            using (Pen divider = new Pen(DividerColor, 1f))
            {
                graphics.DrawLine(divider, P(18), P(34), width - P(18), P(34));
            }

            int rowWidth = width - P(36);
            List<DeviceRow> rows = BuildRows();
            if (rows.Count == 0)
            {
                TextRenderer.DrawText(graphics, "暂未识别设备连接", _nameFont,
                    new Rectangle(P(18), P(RowTopBase), rowWidth, P(20)), MutedColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    DrawDeviceRow(graphics, P(18), P(RowTopBase + RowStepBase * i), rowWidth,
                        rows[i].Reading, rows[i].FallbackName);
                }
            }

            bool dockStrip = _dockEdge != DockEdge.None && _dockHidden;
            if (!dockStrip)
            {
                string footer;
                if (_refreshing)
                {
                    footer = "正在读取…";
                }
                else if (_lastUpdated == DateTime.MinValue)
                {
                    footer = "等待更新";
                }
                else
                {
                    footer = _lastUpdated.ToString("HH:mm:ss") + " 更新";
                }

                Rectangle footerRect = new Rectangle(P(18), height - P(21), width - P(36), P(14));
                TextRenderer.DrawText(graphics, footer, _footerFont, footerRect,
                    HintColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                if (!_refreshing && HasStaleReading())
                {
                    TextRenderer.DrawText(graphics, "≈ 上次读数", _footerFont, footerRect,
                        HintColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter
                        | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                }
            }
            else
            {
                DrawDockGrip(graphics);
            }
        }

        private sealed class DeviceRow
        {
            public DeviceReading Reading;
            public string FallbackName;
        }

        private List<DeviceRow> BuildRows()
        {
            List<DeviceRow> rows = new List<DeviceRow>();
            if (_readings == null)
            {
                AddLoadingRow(rows, "VGN V98 Pro V3");
                AddLoadingRow(rows, "GPW2");
                AddLoadingRow(rows, "Xbox Series");
                return rows;
            }

            AddDeviceRow(rows, _readings.Keyboard, "VGN V98 Pro V3");
            AddDeviceRow(rows, _readings.Mouse, "GPW2");
            AddDeviceRow(rows, _readings.Gamepad, "Xbox Series");
            return rows;
        }

        private static void AddLoadingRow(List<DeviceRow> rows, string fallbackName)
        {
            DeviceRow row = new DeviceRow();
            row.FallbackName = fallbackName;
            rows.Add(row);
        }

        private static void AddDeviceRow(List<DeviceRow> rows, DeviceReading reading, string fallbackName)
        {
            if (reading == null
                || reading.State == ReadingState.NotConnected
                || reading.State == ReadingState.NoResponse)
            {
                return;
            }

            DeviceRow row = new DeviceRow();
            row.Reading = reading;
            row.FallbackName = fallbackName;
            rows.Add(row);
        }

        private int GetDesiredHeight()
        {
            int rows = BuildRows().Count;
            return P(ContentTopBase + RowStepBase * Math.Max(1, rows));
        }

        private void ApplyAdaptiveHeight()
        {
            if (!IsHandleCreated || IsDisposed)
            {
                return;
            }

            int desired = GetDesiredHeight();
            if (ClientSize.Height == desired)
            {
                return;
            }

            int oldBottom = Bottom;
            bool wasAnimating = _animating;
            if (wasAnimating)
            {
                _animating = false;
                _dockTimer.Stop();
            }

            ClientSize = new Size(ClientSize.Width, desired);
            ApplyRegion();

            if (!_positionInitialized)
            {
                return;
            }

            if (_dockEdge != DockEdge.None)
            {
                Location = DockPosition(_dockArea, _dockEdge, _dockHidden, Location);
                if (wasAnimating && _dockHidden)
                {
                    _hiddenAt = DateTime.UtcNow;
                }
            }
            else
            {
                Rectangle area = WorkAreaFromWindow();
                Location = new Point(
                    Clamp(Location.X, area.Left, area.Right - Width),
                    Clamp(oldBottom - Height, area.Top, area.Bottom - Height));
            }
        }

        private bool HasStaleReading()
        {
            List<DeviceRow> rows = BuildRows();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Reading != null && rows[i].Reading.Stale)
                {
                    return true;
                }
            }
            return false;
        }

        private void DrawDeviceRow(Graphics graphics, int x, int y, int rowWidth, DeviceReading reading, string fallbackName)
        {
            string name = reading != null && !String.IsNullOrEmpty(reading.Name) ? reading.Name : fallbackName;
            string tag = reading != null && reading.State == ReadingState.Ok ? reading.Tag : null;
            string charge = reading != null && !reading.Stale ? reading.ChargeText : null;

            int valueWidth = P(74);
            int nameLimit = rowWidth - valueWidth - P(10);
            int lineHeight = P(20);

            int nameWidth = TextRenderer.MeasureText(graphics, name, _nameFont,
                new Size(1000, lineHeight), TextFormatFlags.NoPadding).Width;
            int tagWidth = String.IsNullOrEmpty(tag) ? 0 : TextRenderer.MeasureText(graphics, tag, _tagFont,
                new Size(1000, lineHeight), TextFormatFlags.NoPadding).Width;
            int chargeTextWidth = String.IsNullOrEmpty(charge) ? 0 : TextRenderer.MeasureText(graphics, charge, _tagFont,
                new Size(1000, lineHeight), TextFormatFlags.NoPadding).Width;
            int chargeWidth = chargeTextWidth > 0 ? P(8) + P(4) + chargeTextWidth : 0;

            bool nameFits = nameWidth <= nameLimit;
            bool showTag = tagWidth > 0 && nameWidth + P(6) + tagWidth <= nameLimit;
            bool showCharge = chargeWidth > 0
                && nameWidth + (showTag ? P(6) + tagWidth : 0) + P(6) + chargeWidth <= nameLimit;
            if (!showCharge && showTag && chargeWidth > 0)
            {
                showTag = false;
                showCharge = nameWidth + P(6) + chargeWidth <= nameLimit;
            }

            Color chargeColor = reading != null && reading.Charge == ChargeState.Full ? HealthyBarColor : ChargingColor;
            int cursor = x;

            if (nameFits)
            {
                TextRenderer.DrawText(graphics, name, _nameFont,
                    new Rectangle(cursor, y, nameWidth + P(2), lineHeight), NameColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                cursor += nameWidth;
            }
            else
            {
                TextRenderer.DrawText(graphics, name, _nameFont,
                    new Rectangle(cursor, y, nameLimit, lineHeight), NameColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding
                    | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }

            if (nameFits && showTag)
            {
                cursor += P(6);
                TextRenderer.DrawText(graphics, tag, _tagFont,
                    new Rectangle(cursor, y + P(1), tagWidth + P(2), lineHeight), TagColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
                cursor += tagWidth;
            }

            if (nameFits && showCharge)
            {
                cursor += P(6);
                DrawBolt(graphics, new RectangleF(cursor, y + P(4), P(8), P(12)), chargeColor);
                TextRenderer.DrawText(graphics, charge, _tagFont,
                    new Rectangle(cursor + P(8) + P(4), y + P(1), chargeTextWidth + P(2), lineHeight), chargeColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }

            string value;
            if (reading == null)
            {
                value = _refreshing ? "…" : "--";
            }
            else
            {
                value = reading.StatusText;
                if (reading.Stale && reading.Percent.HasValue)
                {
                    value = "≈" + value;
                }
            }

            TextRenderer.DrawText(graphics, value, _percentFont,
                new Rectangle(x + rowWidth - valueWidth, y - P(1), valueWidth, lineHeight + P(2)),
                GetValueColor(reading),
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);

            DrawBatteryBar(graphics, new Rectangle(x, y + P(22), rowWidth, P(5)), reading);
        }

        private Color GetValueColor(DeviceReading reading)
        {
            if (reading == null)
            {
                return MutedColor;
            }
            if (reading.State != ReadingState.Ok)
            {
                return reading.State == ReadingState.Error ? ErrorColor : MutedColor;
            }
            if (!reading.Percent.HasValue)
            {
                return MutedColor;
            }

            int percent = reading.Percent.Value;
            Color color;
            if (percent <= _config.CriticalPercent)
            {
                color = CriticalColor;
            }
            else if (percent <= _config.WarnPercent)
            {
                color = WarnColor;
            }
            else
            {
                color = ValueColor;
            }

            return reading.Stale ? Dim(color) : color;
        }

        private Color GetBarColor(DeviceReading reading)
        {
            if (reading == null || !reading.Percent.HasValue)
            {
                return HealthyBarColor;
            }
            int percent = reading.Percent.Value;
            Color color;
            if (percent <= _config.CriticalPercent)
            {
                color = CriticalColor;
            }
            else if (percent <= _config.WarnPercent)
            {
                color = WarnColor;
            }
            else
            {
                color = HealthyBarColor;
            }

            return reading.Stale ? Dim(color) : color;
        }

        private static Color Dim(Color color)
        {
            const float factor = 0.45f;
            int backgroundR = 22;
            int backgroundG = 26;
            int backgroundB = 33;
            return Color.FromArgb(
                (int)Math.Round(color.R * (1 - factor) + backgroundR * factor),
                (int)Math.Round(color.G * (1 - factor) + backgroundG * factor),
                (int)Math.Round(color.B * (1 - factor) + backgroundB * factor));
        }

        private void DrawBatteryBar(Graphics graphics, Rectangle bounds, DeviceReading reading)
        {
            float radius = bounds.Height / 2f;
            using (GraphicsPath track = RoundedRect(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), radius))
            {
                using (SolidBrush trackBrush = new SolidBrush(TrackColor))
                {
                    graphics.FillPath(trackBrush, track);
                }
            }

            if (reading == null || reading.State != ReadingState.Ok || !reading.Percent.HasValue)
            {
                return;
            }

            int percent = Math.Max(0, Math.Min(100, reading.Percent.Value));
            if (percent == 0)
            {
                return;
            }

            float fillWidth = bounds.Width * (percent / 100f);
            if (fillWidth < bounds.Height)
            {
                fillWidth = bounds.Height;
            }

            using (GraphicsPath fill = RoundedRect(new RectangleF(bounds.X, bounds.Y, fillWidth, bounds.Height), radius))
            {
                using (SolidBrush fillBrush = new SolidBrush(GetBarColor(reading)))
                {
                    graphics.FillPath(fillBrush, fill);
                }
            }
        }

        private static void DrawBolt(Graphics graphics, RectangleF bounds, Color color)
        {
            float w = bounds.Width;
            float h = bounds.Height;
            PointF[] points = new PointF[]
            {
                new PointF(bounds.X + w * 0.58f, bounds.Y),
                new PointF(bounds.X + w * 0.02f, bounds.Y + h * 0.56f),
                new PointF(bounds.X + w * 0.44f, bounds.Y + h * 0.56f),
                new PointF(bounds.X + w * 0.30f, bounds.Y + h),
                new PointF(bounds.X + w * 0.98f, bounds.Y + h * 0.38f),
                new PointF(bounds.X + w * 0.54f, bounds.Y + h * 0.38f)
            };
            using (SolidBrush brush = new SolidBrush(color))
            {
                graphics.FillPolygon(brush, points);
            }
        }

        private void DrawDockGrip(Graphics graphics)
        {
            int strip = P(DockStripBase);
            float radius = Math.Max(1f, P(1.5f));
            float gap = Math.Max(3f, P(5f));
            Rectangle client = ClientRectangle;

            using (SolidBrush brush = new SolidBrush(GripColor))
            {
                if (_dockEdge == DockEdge.Left || _dockEdge == DockEdge.Right)
                {
                    float cx = _dockEdge == DockEdge.Left
                        ? client.Width - strip / 2f
                        : strip / 2f;
                    float cy = client.Height / 2f;
                    for (int i = -1; i <= 1; i++)
                    {
                        graphics.FillEllipse(brush,
                            cx - radius, cy + i * gap - radius, radius * 2f, radius * 2f);
                    }
                }
                else if (_dockEdge == DockEdge.Top)
                {
                    float cx = client.Width / 2f;
                    float cy = client.Height - strip / 2f;
                    for (int i = -1; i <= 1; i++)
                    {
                        graphics.FillEllipse(brush,
                            cx + i * gap - radius, cy - radius, radius * 2f, radius * 2f);
                    }
                }
            }
        }

        private void DrawIconButton(Graphics graphics, Rectangle bounds, string glyph, bool hover, bool dim, bool danger)
        {
            if (hover)
            {
                using (GraphicsPath path = RoundedRect(new RectangleF(bounds.X, bounds.Y, bounds.Width, bounds.Height), P(6)))
                {
                    using (SolidBrush brush = new SolidBrush(danger ? Color.FromArgb(58, 32, 38) : Color.FromArgb(39, 45, 56)))
                    {
                        graphics.FillPath(brush, path);
                    }
                }
            }

            Color color;
            if (dim)
            {
                color = Color.FromArgb(78, 87, 102);
            }
            else if (hover)
            {
                color = danger ? Color.FromArgb(255, 122, 122) : Color.FromArgb(231, 236, 245);
            }
            else
            {
                color = Color.FromArgb(132, 142, 159);
            }

            TextRenderer.DrawText(graphics, glyph, _iconFont, bounds, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        private void OnServiceReadingsReady(object sender, ReadingsEventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action<DeviceReadings>(ApplyReadings), e.Readings);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ApplyReadings(DeviceReadings readings)
        {
            if (readings == null || IsDisposed)
            {
                return;
            }

            if (readings.GamepadOnly)
            {
                if (_readings == null)
                {
                    _readings = new DeviceReadings();
                }
                _readings.Gamepad = readings.Gamepad;
            }
            else
            {
                _readings = readings;
                _lastUpdated = readings.UpdatedAt;
            }

            _refreshing = false;
            ApplyAdaptiveHeight();
            if (_refreshQueued)
            {
                _refreshQueued = false;
                RequestRefresh();
            }
            Invalidate();
        }

        private void OnMouseChargingChanged(object sender, EventArgs e)
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            try
            {
                BeginInvoke(new Action<string>(RefreshOnExternalChange), "鼠标充电状态变化");
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void RefreshOnExternalChange(string reason)
        {
            if (_closed || _inFullscreen || !Visible)
            {
                return;
            }
            if ((DateTime.UtcNow - _lastExternalRefresh).TotalMilliseconds < 1500)
            {
                return;
            }

            _lastExternalRefresh = DateTime.UtcNow;
            Logger.Info(reason + "，立即刷新");
            RequestRefresh();
        }

        private void RequestRefresh()
        {
            if (_refreshing)
            {
                _refreshQueued = true;
                return;
            }
            _refreshing = true;
            _lastRequestAt = DateTime.UtcNow;
            Invalidate();
            _service.RequestFull();
        }

        private void OnRefreshTimerTick(object sender, EventArgs e)
        {
            if (!_inFullscreen && Visible)
            {
                RequestRefresh();
            }
        }

        private void OnFullscreenTimerTick(object sender, EventArgs e)
        {
            bool isFullscreen = FullscreenDetector.IsFullscreen(Handle);

            if (isFullscreen != _inFullscreen)
            {
                _inFullscreen = isFullscreen;
                if (isFullscreen)
                {
                    _lastGamepadCheck = DateTime.UtcNow;
                    Logger.Info("进入全屏，隐藏窗口并暂停键鼠刷新");
                    Hide();
                }
                else
                {
                    Logger.Info("退出全屏，恢复窗口并立即刷新");
                    Show();
                    EnsureTopMost();
                    RequestRefresh();
                }
            }

            if (_inFullscreen
                && (DateTime.UtcNow - _lastGamepadCheck).TotalSeconds >= GamepadFullscreenCheckSeconds)
            {
                _lastGamepadCheck = DateTime.UtcNow;
                _service.RequestGamepadOnly();
            }

            if (!_inFullscreen && Visible)
            {
                bool gamepadConnected = XInput.AnyControllerConnected();
                if (_gamepadLinkConnected.HasValue && _gamepadLinkConnected.Value != gamepadConnected)
                {
                    Logger.Info(gamepadConnected ? "检测到手柄已连接" : "检测到手柄已休眠或断开");
                    RequestRefresh();
                }
                _gamepadLinkConnected = gamepadConnected;
            }

            if (!_inFullscreen && Visible && !_refreshing)
            {
                int inputRefreshSeconds = Math.Max(15, _config.RefreshSeconds / 2);
                if ((DateTime.UtcNow - _lastRequestAt).TotalSeconds >= inputRefreshSeconds)
                {
                    NativeMethods.LASTINPUTINFO input = new NativeMethods.LASTINPUTINFO();
                    input.cbSize = (uint)Marshal.SizeOf(typeof(NativeMethods.LASTINPUTINFO));
                    if (NativeMethods.GetLastInputInfo(ref input))
                    {
                        uint idle = unchecked(NativeMethods.GetTickCount() - input.dwTime);
                        if (idle < 3000)
                        {
                            RequestRefresh();
                        }
                    }
                }
            }
        }

        private void OnMenuRefresh(object sender, EventArgs e)
        {
            RequestRefresh();
        }

        private void OnMenuExit(object sender, EventArgs e)
        {
            Close();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (GetRefreshRect().Contains(e.Location))
            {
                _pressedRefresh = true;
                Invalidate();
                return;
            }

            if (GetCloseRect().Contains(e.Location))
            {
                _pressedClose = true;
                Invalidate();
                return;
            }

            if (_dockHidden)
            {
                RevealDock(true);
            }

            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, WM_NCLBUTTONDOWN, new IntPtr(HTCAPTION), IntPtr.Zero);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            bool refresh = _pressedRefresh;
            bool close = _pressedClose;
            _pressedRefresh = false;
            _pressedClose = false;

            if (refresh && GetRefreshRect().Contains(e.Location))
            {
                RequestRefresh();
            }
            else if (close && GetCloseRect().Contains(e.Location))
            {
                Close();
                return;
            }

            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            bool overRefresh = GetRefreshRect().Contains(e.Location);
            bool overClose = GetCloseRect().Contains(e.Location);

            if (overRefresh != _hoverRefresh || overClose != _hoverClose)
            {
                _hoverRefresh = overRefresh;
                _hoverClose = overClose;
                Invalidate();
            }

            Cursor = (overRefresh || overClose) ? Cursors.Hand : Cursors.SizeAll;
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);

            _hidePending = false;
            if (_dockHidden
                && _dockEdge != DockEdge.None
                && (DateTime.UtcNow - _hiddenAt).TotalMilliseconds >= DockRevealGuardMs)
            {
                RevealDock(false);
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);

            if (_hoverRefresh || _hoverClose)
            {
                _hoverRefresh = false;
                _hoverClose = false;
                Invalidate();
            }

            Cursor = Cursors.Default;
            ScheduleDockHide();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (m.Msg == WM_DEVICECHANGE && m.WParam.ToInt32() == DBT_DEVNODES_CHANGED)
            {
                RefreshOnExternalChange("检测到设备变化");
            }
            else if (m.Msg == WM_ENTERSIZEMOVE && _positionInitialized && !_closed)
            {
                _dragging = true;
                _dragMoved = false;
                _dragStartLocation = Location;
                _hidePending = false;
                _animating = false;
                _dockTimer.Stop();
            }
            else if (m.Msg == WM_EXITSIZEMOVE && _positionInitialized && !_closed)
            {
                _dragging = false;
                _dragMoved = Location != _dragStartLocation;
                if (_dragMoved)
                {
                    FinishDrag();
                }
                else
                {
                    SaveConfig();
                }
            }
        }

        private void RestoreDockState()
        {
            if (_config.Dock == DockEdge.None || !_config.WindowX.HasValue || !_config.WindowY.HasValue)
            {
                return;
            }

            Point saved = new Point(_config.WindowX.Value, _config.WindowY.Value);
            if (!IsPositionVisible(saved))
            {
                return;
            }

            _dockEdge = _config.Dock;
            _dockArea = Screen.FromPoint(saved).WorkingArea;
            Location = DockPosition(_dockArea, _dockEdge, true, saved);
            _dockHidden = true;
            _hiddenAt = DateTime.UtcNow;
            Logger.Info("恢复边缘停靠: " + _dockEdge + " 位置=" + Location.X + "," + Location.Y);
        }

        private void FinishDrag()
        {
            Rectangle area;
            DockEdge edge = DetectDockEdge(out area);

            if (edge != DockEdge.None)
            {
                _dockEdge = edge;
                _dockArea = area;
                _dockHidden = false;
                Location = DockPosition(area, edge, false, Location);
                SaveConfig();
                Logger.Info("窗口停靠到" + edge + "边缘，自动隐藏");
                HideDock();
                return;
            }

            bool wasDocked = _dockEdge != DockEdge.None;
            _dockEdge = DockEdge.None;
            _dockHidden = false;
            _hidePending = false;
            ClampToWorkArea();
            SaveConfig();

            if (wasDocked)
            {
                Logger.Info("已拖离屏幕边缘，取消自动隐藏");
            }
        }

        private DockEdge DetectDockEdge(out Rectangle area)
        {
            area = WorkAreaFromWindow();
            int snap = P(DockSnapBase);
            DockEdge edge = DockEdge.None;
            int best = snap + 1;

            int left = Math.Max(0, Location.X - area.Left);
            if (left <= snap && left < best)
            {
                best = left;
                edge = DockEdge.Left;
            }

            int right = Math.Max(0, area.Right - (Location.X + Width));
            if (right <= snap && right < best)
            {
                best = right;
                edge = DockEdge.Right;
            }

            int top = Math.Max(0, Location.Y - area.Top);
            if (top <= snap && top < best)
            {
                best = top;
                edge = DockEdge.Top;
            }

            return edge;
        }

        private Point DockPosition(Rectangle area, DockEdge edge, bool hidden, Point anchor)
        {
            int strip = P(DockStripBase);
            int x = anchor.X;
            int y = anchor.Y;

            switch (edge)
            {
                case DockEdge.Left:
                    x = hidden ? area.Left - (Width - strip) : area.Left;
                    y = Clamp(y, area.Top, area.Bottom - Height);
                    break;
                case DockEdge.Right:
                    x = hidden ? area.Right - strip : area.Right - Width;
                    y = Clamp(y, area.Top, area.Bottom - Height);
                    break;
                case DockEdge.Top:
                    x = Clamp(x, area.Left, area.Right - Width);
                    y = hidden ? area.Top - (Height - strip) : area.Top;
                    break;
                default:
                    x = Clamp(x, area.Left, area.Right - Width);
                    y = Clamp(y, area.Top, area.Bottom - Height);
                    break;
            }

            return new Point(x, y);
        }

        private void ClampToWorkArea()
        {
            Rectangle area = WorkAreaFromWindow();
            Location = new Point(
                Clamp(Location.X, area.Left, area.Right - Width),
                Clamp(Location.Y, area.Top, area.Bottom - Height));
        }

        private Rectangle WorkAreaFromWindow()
        {
            Point center = new Point(Location.X + Width / 2, Location.Y + Height / 2);
            return Screen.FromPoint(center).WorkingArea;
        }

        private void HideDock()
        {
            if (_dockEdge == DockEdge.None)
            {
                return;
            }

            _dockHidden = true;
            _hidePending = false;
            Invalidate();
            StartDockAnimation(DockPosition(_dockArea, _dockEdge, true, Location), true);
        }

        private void RevealDock(bool instant)
        {
            if (_dockEdge == DockEdge.None || !_dockHidden)
            {
                return;
            }

            _dockHidden = false;
            _hiddenAt = DateTime.MinValue;
            _hidePending = false;
            Invalidate();

            Point target = DockPosition(_dockArea, _dockEdge, false, Location);
            if (instant)
            {
                _animating = false;
                Location = target;
                _dockTimer.Stop();
            }
            else
            {
                StartDockAnimation(target, false);
            }

            Logger.Info("显示停靠窗口");
        }

        private void StartDockAnimation(Point target, bool hiding)
        {
            _animFromX = Location.X;
            _animFromY = Location.Y;
            _animToX = target.X;
            _animToY = target.Y;
            _animStart = DateTime.UtcNow;

            if (_animFromX == _animToX && _animFromY == _animToY)
            {
                _animating = false;
                if (hiding)
                {
                    _hiddenAt = DateTime.UtcNow;
                }
                return;
            }

            _animating = true;
            _dockTimer.Start();
        }

        private void OnDockTimerTick(object sender, EventArgs e)
        {
            if (_animating)
            {
                double progress = (DateTime.UtcNow - _animStart).TotalMilliseconds / DockAnimMs;
                if (progress < 0)
                {
                    progress = 0;
                }
                if (progress > 1)
                {
                    progress = 1;
                }

                double eased = 1.0 - Math.Pow(1.0 - progress, 3.0);
                Location = new Point(
                    (int)Math.Round(_animFromX + (_animToX - _animFromX) * eased),
                    (int)Math.Round(_animFromY + (_animToY - _animFromY) * eased));

                if (progress >= 1.0)
                {
                    Location = new Point(_animToX, _animToY);
                    _animating = false;
                    if (_dockHidden)
                    {
                        _hiddenAt = DateTime.UtcNow;
                    }
                }
            }

            if (_hidePending)
            {
                if (_dragging || _dockEdge == DockEdge.None || _dockHidden)
                {
                    _hidePending = false;
                }
                else if (!_animating && DateTime.UtcNow >= _hideAt)
                {
                    _hidePending = false;
                    HideDock();
                }
            }

            if (!_animating && !_hidePending)
            {
                _dockTimer.Stop();
            }
        }

        private void ScheduleDockHide()
        {
            if (_dockEdge == DockEdge.None || _dockHidden || _dragging)
            {
                return;
            }

            _hidePending = true;
            _hideAt = DateTime.UtcNow.AddMilliseconds(DockHideDelayMs);
            _dockTimer.Start();
        }

        private void SaveConfig()
        {
            Point persisted = _dockEdge != DockEdge.None
                ? DockPosition(_dockArea, _dockEdge, false, Location)
                : Location;

            _config.WindowX = persisted.X;
            _config.WindowY = persisted.Y;
            _config.Dock = _dockEdge;
            _config.Save();
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                return min;
            }
            return Math.Max(min, Math.Min(value, max));
        }

        private void ApplyRegion()
        {
            using (GraphicsPath path = RoundedRect(new RectangleF(0, 0, ClientSize.Width, ClientSize.Height), P(14)))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null)
                {
                    old.Dispose();
                }
            }
        }

        private void PlaceWindow()
        {
            if (_config.WindowX.HasValue && _config.WindowY.HasValue)
            {
                Point saved = new Point(_config.WindowX.Value, _config.WindowY.Value);
                if (IsPositionVisible(saved))
                {
                    Location = saved;
                    return;
                }
            }

            Rectangle workingArea = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(
                workingArea.Right - Width - P(24),
                workingArea.Bottom - Height - P(24));
        }

        private bool IsPositionVisible(Point location)
        {
            Rectangle bounds = new Rectangle(location, Size);
            Rectangle visible = Rectangle.Intersect(bounds, SystemInformation.VirtualScreen);
            return visible.Width >= P(80) && visible.Height >= P(40);
        }

        private void EnsureTopMost()
        {
            TopMost = true;
            NativeMethods.SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                0x0001 | 0x0002 | 0x0010);
        }

        private Rectangle GetRefreshRect()
        {
            int size = P(22);
            int x = ClientSize.Width - P(16) - size - P(6) - size;
            return new Rectangle(x, P(9), size, size);
        }

        private Rectangle GetCloseRect()
        {
            int size = P(22);
            int x = ClientSize.Width - P(16) - size;
            return new Rectangle(x, P(9), size, size);
        }

        private int P(float value)
        {
            return (int)Math.Round(value * _scale);
        }

        private static GraphicsPath RoundedRect(RectangleF rect, float radius)
        {
            GraphicsPath path = new GraphicsPath();
            float diameter = Math.Max(1f, radius * 2f);
            if (diameter > rect.Width)
            {
                diameter = rect.Width;
            }
            if (diameter > rect.Height)
            {
                diameter = rect.Height;
            }

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
