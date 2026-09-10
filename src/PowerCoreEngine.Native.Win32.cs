using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // NATIVE INTEROP STRUCTS & IMPORTS
    // ---------------------------------------------------------------------------------------------
    private static class Win32Native {
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DISPLAY_DEVICE {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static int GetForegroundProcessId() {
            try {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return 0;
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                return (int)pid;
            } catch {
                return 0;
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint flags, [Out] StringBuilder lpExeName, ref uint lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);
    }

    public static bool CheckNvidiaDisplayAttached(out string monitorName) {
        monitorName = "";
        try {
            uint i = 0;
            Win32Native.DISPLAY_DEVICE dd = new Win32Native.DISPLAY_DEVICE();
            dd.cb = Marshal.SizeOf(typeof(Win32Native.DISPLAY_DEVICE));

            while (Win32Native.EnumDisplayDevices(null, i, ref dd, 0)) {
                bool isAttached = (dd.StateFlags & Win32Native.DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) != 0;
                string devString = dd.DeviceString != null ? dd.DeviceString : "";
                string devId = dd.DeviceID != null ? dd.DeviceID : "";

                if (isAttached && (devString.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 || devId.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0)) {
                    Win32Native.DISPLAY_DEVICE ddMon = new Win32Native.DISPLAY_DEVICE();
                    ddMon.cb = Marshal.SizeOf(typeof(Win32Native.DISPLAY_DEVICE));
                    if (Win32Native.EnumDisplayDevices(dd.DeviceName, 0, ref ddMon, 0) && !string.IsNullOrEmpty(ddMon.DeviceString)) {
                        monitorName = ddMon.DeviceString;
                    } else {
                        monitorName = "External Display";
                    }
                    return true;
                }
                i++;
            }
        } catch { }
        return false;
    }

    private static class PowrProfNative {
        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerWriteACValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingsGuid, ref Guid PowerSettingGuid, uint AcValueIndex);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerWriteDCValueIndex(IntPtr RootPowerKey, ref Guid SchemeGuid, ref Guid SubGroupOfPowerSettingsGuid, ref Guid PowerSettingGuid, uint DcValueIndex);

        [DllImport("powrprof.dll", SetLastError = true)]
        public static extern uint PowerSetActiveScheme(IntPtr UserRootPowerKey, ref Guid SchemeGuid);
    }

}
