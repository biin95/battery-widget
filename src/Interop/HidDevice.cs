using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace BatteryWidget.Interop
{
    public sealed class HidDeviceInfo
    {
        public string Path;
        public string InstanceId;
        public ushort VendorId;
        public ushort ProductId;

        public override string ToString()
        {
            return string.Format("VID_{0:X4}&PID_{1:X4} {2}", VendorId, ProductId, Path);
        }
    }

    public sealed class HidDevice : IDisposable
    {
        private readonly IntPtr _handle;
        private readonly Thread _readerThread;
        private readonly object _sync = new object();
        private readonly List<byte[]> _reports = new List<byte[]>();
        private volatile bool _stopping;
        private int _readFailure;

        public string Path { get; private set; }
        public ushort VendorId { get; private set; }
        public ushort ProductId { get; private set; }
        public string ProductName { get; private set; }
        public ushort UsagePage { get; private set; }
        public ushort Usage { get; private set; }
        public ushort InputReportByteLength { get; private set; }
        public ushort OutputReportByteLength { get; private set; }
        public ushort FeatureReportByteLength { get; private set; }
        public bool IsAlive { get { return !_stopping && _readFailure == 0; } }
        public int LastReadError { get { return _readFailure; } }
        public int LastWriteError { get; private set; }

        private HidDevice(IntPtr handle, string path)
        {
            _handle = handle;
            Path = path;

            NativeMethods.HIDD_ATTRIBUTES attributes = new NativeMethods.HIDD_ATTRIBUTES();
            attributes.Size = Marshal.SizeOf(typeof(NativeMethods.HIDD_ATTRIBUTES));
            if (NativeMethods.HidD_GetAttributes(handle, ref attributes))
            {
                VendorId = attributes.VendorID;
                ProductId = attributes.ProductID;
            }

            IntPtr preparsed;
            if (NativeMethods.HidD_GetPreparsedData(handle, out preparsed))
            {
                try
                {
                    NativeMethods.HIDP_CAPS caps = new NativeMethods.HIDP_CAPS();
                    if (NativeMethods.HidP_GetCaps(preparsed, ref caps) == 0x00110000)
                    {
                        Usage = caps.Usage;
                        UsagePage = caps.UsagePage;
                        InputReportByteLength = caps.InputReportByteLength;
                        OutputReportByteLength = caps.OutputReportByteLength;
                        FeatureReportByteLength = caps.FeatureReportByteLength;
                    }
                }
                finally
                {
                    NativeMethods.HidD_FreePreparsedData(preparsed);
                }
            }

            StringBuilder product = new StringBuilder(260);
            if (NativeMethods.HidD_GetProductString(handle, product, product.Capacity * 2))
            {
                ProductName = product.ToString();
            }

            _readerThread = new Thread(ReadLoop);
            _readerThread.IsBackground = true;
            _readerThread.Name = "HID read " + path;
            _readerThread.Start();
        }

        public static HidDevice Open(string path)
        {
            IntPtr handle = NativeMethods.CreateFile(
                path,
                NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
                NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
                IntPtr.Zero,
                NativeMethods.OPEN_EXISTING,
                NativeMethods.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (handle == NativeMethods.INVALID_HANDLE_VALUE)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, "打开 HID 设备失败: " + path);
            }

            return new HidDevice(handle, path);
        }

        public static List<HidDeviceInfo> Enumerate()
        {
            List<HidDeviceInfo> result = new List<HidDeviceInfo>();
            Guid hidGuid;
            NativeMethods.HidD_GetHidGuid(out hidGuid);

            IntPtr deviceInfoSet = NativeMethods.SetupDiGetClassDevs(
                ref hidGuid, null, IntPtr.Zero,
                NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

            if (deviceInfoSet == NativeMethods.INVALID_HANDLE_VALUE)
            {
                return result;
            }

            try
            {
                for (int index = 0; ; index++)
                {
                    NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVICE_INTERFACE_DATA));

                    if (!NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                    {
                        break;
                    }

                    int requiredSize;
                    NativeMethods.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out requiredSize, IntPtr.Zero);

                    if (requiredSize <= 0)
                    {
                        continue;
                    }

                    IntPtr detail = Marshal.AllocHGlobal(requiredSize);
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);

                        NativeMethods.SP_DEVINFO_DATA deviceInfoData = new NativeMethods.SP_DEVINFO_DATA();
                        deviceInfoData.cbSize = Marshal.SizeOf(typeof(NativeMethods.SP_DEVINFO_DATA));

                        if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet, ref interfaceData, detail, requiredSize, out requiredSize, ref deviceInfoData))
                        {
                            continue;
                        }

                        string path = Marshal.PtrToStringUni(new IntPtr(detail.ToInt64() + 4));
                        if (string.IsNullOrEmpty(path))
                        {
                            continue;
                        }

                        HidDeviceInfo info = new HidDeviceInfo();
                        info.Path = path;

                        StringBuilder instanceId = new StringBuilder(512);
                        int instanceIdRequired;
                        if (NativeMethods.SetupDiGetDeviceInstanceId(
                            deviceInfoSet, ref deviceInfoData, instanceId, instanceId.Capacity, out instanceIdRequired))
                        {
                            info.InstanceId = instanceId.ToString();
                        }

                        Match match = Regex.Match(path, @"vid_([0-9a-fA-F]{4})&pid_([0-9a-fA-F]{4})", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            info.VendorId = Convert.ToUInt16(match.Groups[1].Value, 16);
                            info.ProductId = Convert.ToUInt16(match.Groups[2].Value, 16);
                        }

                        result.Add(info);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detail);
                    }
                }
            }
            finally
            {
                NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }

            return result;
        }

        private void ReadLoop()
        {
            IntPtr readEvent = NativeMethods.CreateEvent(IntPtr.Zero, true, false, null);
            if (readEvent == IntPtr.Zero)
            {
                _readFailure = Marshal.GetLastWin32Error();
                return;
            }

            try
            {
                byte[] buffer = new byte[512];
                while (!_stopping)
                {
                    NativeMethods.OVERLAPPED overlapped = new NativeMethods.OVERLAPPED();
                    overlapped.hEvent = readEvent;
                    NativeMethods.ResetEvent(readEvent);

                    uint bytesRead;
                    bool ok = NativeMethods.ReadFile(_handle, buffer, (uint)buffer.Length, out bytesRead, ref overlapped);
                    if (!ok)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error != NativeMethods.ERROR_IO_PENDING)
                        {
                            if (!_stopping)
                            {
                                _readFailure = error;
                            }
                            break;
                        }

                        uint wait = NativeMethods.WaitForSingleObject(readEvent, 1000);
                        if (wait == NativeMethods.WAIT_TIMEOUT)
                        {
                            NativeMethods.CancelIoEx(_handle, ref overlapped);
                            NativeMethods.WaitForSingleObject(readEvent, 1000);
                            continue;
                        }
                        if (wait != NativeMethods.WAIT_OBJECT_0)
                        {
                            if (!_stopping)
                            {
                                _readFailure = (int)wait;
                            }
                            break;
                        }

                        if (!NativeMethods.GetOverlappedResult(_handle, ref overlapped, out bytesRead, false))
                        {
                            int resultError = Marshal.GetLastWin32Error();
                            if (!_stopping)
                            {
                                _readFailure = resultError;
                            }
                            break;
                        }
                    }

                    if (bytesRead == 0)
                    {
                        continue;
                    }

                    byte[] report = new byte[bytesRead];
                    Array.Copy(buffer, report, (int)bytesRead);

                    lock (_sync)
                    {
                        _reports.Add(report);
                        if (_reports.Count > 128)
                        {
                            _reports.RemoveAt(0);
                        }
                        Monitor.PulseAll(_sync);
                    }
                }
            }
            finally
            {
                NativeMethods.CloseHandle(readEvent);
            }
        }

        public bool TrySend(byte[] report, int timeoutMs)
        {
            IntPtr writeEvent = NativeMethods.CreateEvent(IntPtr.Zero, true, false, null);
            if (writeEvent == IntPtr.Zero)
            {
                LastWriteError = Marshal.GetLastWin32Error();
                return false;
            }

            try
            {
                NativeMethods.OVERLAPPED overlapped = new NativeMethods.OVERLAPPED();
                overlapped.hEvent = writeEvent;

                uint written;
                bool ok = NativeMethods.WriteFile(_handle, report, (uint)report.Length, out written, ref overlapped);
                if (!ok)
                {
                    int error = Marshal.GetLastWin32Error();
                    if (error != NativeMethods.ERROR_IO_PENDING)
                    {
                        LastWriteError = error;
                        return false;
                    }

                    uint wait = NativeMethods.WaitForSingleObject(writeEvent, (uint)timeoutMs);
                    if (wait != NativeMethods.WAIT_OBJECT_0)
                    {
                        NativeMethods.CancelIoEx(_handle, ref overlapped);
                        NativeMethods.WaitForSingleObject(writeEvent, 1000);
                        LastWriteError = (int)NativeMethods.WAIT_TIMEOUT;
                        return false;
                    }

                    if (!NativeMethods.GetOverlappedResult(_handle, ref overlapped, out written, false))
                    {
                        LastWriteError = Marshal.GetLastWin32Error();
                        return false;
                    }
                }

                LastWriteError = 0;
                return true;
            }
            finally
            {
                NativeMethods.CloseHandle(writeEvent);
            }
        }

        public void Send(byte[] report)
        {
            if (!TrySend(report, 1000))
            {
                throw new Win32Exception(LastWriteError, "写入 HID 报告失败");
            }
        }

        public void ClearReports()
        {
            lock (_sync)
            {
                _reports.Clear();
            }
        }

        public List<byte[]> DrainReports()
        {
            lock (_sync)
            {
                List<byte[]> copy = new List<byte[]>(_reports);
                _reports.Clear();
                return copy;
            }
        }

        public List<byte[]> Collect(int firstTimeoutMs, int quietMs)
        {
            List<byte[]> result = new List<byte[]>();
            lock (_sync)
            {
                if (_reports.Count == 0)
                {
                    Monitor.Wait(_sync, firstTimeoutMs);
                }

                while (_reports.Count > 0)
                {
                    result.AddRange(_reports);
                    _reports.Clear();
                    if (!Monitor.Wait(_sync, quietMs))
                    {
                        break;
                    }
                }
            }
            return result;
        }

        public byte[] Query(byte[] report, Func<byte[], bool> match, int timeoutMs)
        {
            ClearReports();
            if (!TrySend(report, 800))
            {
                return null;
            }

            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            lock (_sync)
            {
                while (true)
                {
                    for (int i = 0; i < _reports.Count; i++)
                    {
                        if (match(_reports[i]))
                        {
                            byte[] found = _reports[i];
                            _reports.RemoveRange(0, i + 1);
                            return found;
                        }
                    }

                    int remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
                    if (remaining <= 0)
                    {
                        return null;
                    }
                    Monitor.Wait(_sync, remaining);
                }
            }
        }

        public void Dispose()
        {
            if (_stopping)
            {
                return;
            }

            _stopping = true;
            try
            {
                NativeMethods.CancelIoEx(_handle, IntPtr.Zero);
            }
            catch
            {
            }

            NativeMethods.CloseHandle(_handle);

            if (_readerThread != null && _readerThread != Thread.CurrentThread)
            {
                _readerThread.Join(500);
            }
        }

        public static string FormatReport(byte[] report)
        {
            if (report == null)
            {
                return "(null)";
            }

            StringBuilder builder = new StringBuilder(report.Length * 3 + 8);
            builder.Append("len=").Append(report.Length).Append(':');
            for (int i = 0; i < report.Length; i++)
            {
                builder.Append(' ').Append(report[i].ToString("X2"));
            }
            return builder.ToString();
        }
    }
}
