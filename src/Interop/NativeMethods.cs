using System;
using System.Runtime.InteropServices;
using System.Text;

namespace BatteryWidget.Interop
{
    internal static class NativeMethods
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        public const int ERROR_IO_PENDING = 997;
        public const uint WAIT_OBJECT_0 = 0;
        public const uint WAIT_TIMEOUT = 258;

        public const uint DIGCF_PRESENT = 0x02;
        public const uint DIGCF_ALLCLASSES = 0x04;
        public const uint DIGCF_DEVICEINTERFACE = 0x10;

        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid ClassGuid;
            public int DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OVERLAPPED
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr hEvent;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;

            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("hid.dll")]
        public static extern void HidD_GetHidGuid(out Guid guid);

        [DllImport("hid.dll")]
        public static extern bool HidD_GetAttributes(IntPtr handle, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll")]
        public static extern bool HidD_GetPreparsedData(IntPtr handle, out IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        public static extern int HidP_GetCaps(IntPtr preparsedData, ref HIDP_CAPS capabilities);

        [DllImport("hid.dll", CharSet = CharSet.Unicode)]
        public static extern bool HidD_GetProductString(IntPtr handle, StringBuilder buffer, int bufferLength);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevs(IntPtr classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, int memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDeviceInterfaceDetailW")]
        public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, int deviceInterfaceDetailDataSize, out int requiredSize, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "SetupDiGetDevicePropertyW")]
        public static extern bool SetupDiGetDeviceProperty(IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData, ref DEVPROPKEY propertyKey, out uint propertyType, byte[] propertyBuffer, int propertyBufferSize, out int requiredSize, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, int memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "ReadFile")]
        public static extern bool ReadFile(IntPtr fileHandle, byte[] buffer, uint numberOfBytesToRead, out uint numberOfBytesRead, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "ReadFile")]
        public static extern bool ReadFile(IntPtr fileHandle, byte[] buffer, uint numberOfBytesToRead, out uint numberOfBytesRead, ref OVERLAPPED overlapped);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "WriteFile")]
        public static extern bool WriteFile(IntPtr fileHandle, byte[] buffer, uint numberOfBytesToWrite, out uint numberOfBytesWritten, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true, EntryPoint = "WriteFile")]
        public static extern bool WriteFile(IntPtr fileHandle, byte[] buffer, uint numberOfBytesToWrite, out uint numberOfBytesWritten, ref OVERLAPPED overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(IntPtr fileHandle, IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(IntPtr fileHandle, ref OVERLAPPED overlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateEvent(IntPtr eventAttributes, bool manualReset, bool initialState, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ResetEvent(IntPtr eventHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(IntPtr fileHandle, ref OVERLAPPED overlapped, out uint numberOfBytesTransferred, bool wait);

        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        [DllImport("user32.dll", EntryPoint = "SendMessageW")]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(out int state);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        public static extern int GetWindowLong(IntPtr hWnd, int index);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maxCount);

        [DllImport("user32.dll")]
        public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO info);

        [DllImport("kernel32.dll")]
        public static extern uint GetTickCount();
    }
}
