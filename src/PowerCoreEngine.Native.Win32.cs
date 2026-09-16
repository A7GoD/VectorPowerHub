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
    public static class Win32Native {
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        public enum LOGICAL_PROCESSOR_RELATIONSHIP {
            RelationProcessorCore = 0, RelationNumaNode = 1, RelationCache = 2,
            RelationProcessorPackage = 3, RelationGroup = 4, RelationProcessorDie = 5,
            RelationNumaNodeEx = 6, RelationProcessorModule = 7, RelationAll = 0xffff
        }

        public enum PROCESSOR_CACHE_TYPE {
            CacheUnified = 0, CacheInstruction = 1, CacheData = 2, CacheTrace = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GROUP_AFFINITY {
            public UIntPtr Mask;
            public ushort Group;
            public ushort Reserved0, Reserved1, Reserved2;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSOR_RELATIONSHIP {
            public byte Flags, EfficiencyClass;
            public byte Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7;
            public byte Reserved8, Reserved9, Reserved10, Reserved11, Reserved12, Reserved13, Reserved14, Reserved15;
            public byte Reserved16, Reserved17, Reserved18, Reserved19;
            public ushort GroupCount;
            public GROUP_AFFINITY GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CACHE_RELATIONSHIP {
            public byte Level, Associativity;
            public ushort LineSize;
            public uint CacheSize;
            public PROCESSOR_CACHE_TYPE Type;
            public byte Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7;
            public byte Reserved8, Reserved9, Reserved10, Reserved11, Reserved12, Reserved13, Reserved14, Reserved15;
            public byte Reserved16, Reserved17;
            public ushort GroupCount;
            public GROUP_AFFINITY GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NUMA_NODE_RELATIONSHIP {
            public uint NodeNumber;
            public byte Reserved0, Reserved1, Reserved2, Reserved3, Reserved4, Reserved5, Reserved6, Reserved7;
            public byte Reserved8, Reserved9, Reserved10, Reserved11, Reserved12, Reserved13, Reserved14, Reserved15;
            public byte Reserved16, Reserved17;
            public ushort GroupCount;
            public GROUP_AFFINITY GroupMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESSOR_GROUP_INFO {
            public byte MaximumProcessorCount, ActiveProcessorCount;
            public byte Reserved0, Reserved1, Reserved2, Reserved3;
            public UIntPtr ActiveProcessorMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct GROUP_RELATIONSHIP {
            public ushort MaximumGroupCount, ActiveGroupCount;
            public byte Reserved0, Reserved1, Reserved2, Reserved3;
            public PROCESSOR_GROUP_INFO GroupInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX {
            [FieldOffset(0)] public LOGICAL_PROCESSOR_RELATIONSHIP Relationship;
            [FieldOffset(4)] public uint Size;
            [FieldOffset(8)] public PROCESSOR_RELATIONSHIP Processor;
            [FieldOffset(8)] public NUMA_NODE_RELATIONSHIP NumaNode;
            [FieldOffset(8)] public CACHE_RELATIONSHIP Cache;
            [FieldOffset(8)] public GROUP_RELATIONSHIP Group;
        }

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
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
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

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, IntPtr Buffer, ref uint ReturnedLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetLogicalProcessorInformationEx(LOGICAL_PROCESSOR_RELATIONSHIP RelationshipType, [Out] byte[] Buffer, ref uint ReturnedLength);
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
