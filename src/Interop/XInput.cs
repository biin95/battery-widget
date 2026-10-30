using System;
using System.Runtime.InteropServices;

namespace BatteryWidget.Interop
{
    public static class XInput
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort Buttons;
            public byte LeftTrigger;
            public byte RightTrigger;
            public short LeftThumbX;
            public short LeftThumbY;
            public short RightThumbX;
            public short RightThumbY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint PacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputGetState(uint index, out XINPUT_STATE state);

        // 手柄休眠后蓝牙电量节点仍然存在，只有 XInput 能反映实际连接状态
        public static bool AnyControllerConnected()
        {
            for (uint index = 0; index < 4; index++)
            {
                XINPUT_STATE state;
                if (XInputGetState(index, out state) == 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
