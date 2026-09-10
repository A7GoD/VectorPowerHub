using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {

    private static class PdhNative {
        public const uint PDH_FMT_DOUBLE = 0x00000200;

        [StructLayout(LayoutKind.Explicit)]
        public struct PDH_FMT_COUNTERVALUE_DOUBLE {
            [FieldOffset(0)]
            public uint CStatus;
            [FieldOffset(8)]
            public double doubleValue;
        }

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint PdhOpenQueryW(string szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll", SetLastError = true)]
        public static extern uint PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll", SetLastError = true)]
        public static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, IntPtr lpdwType, out PDH_FMT_COUNTERVALUE_DOUBLE pValue);

        [DllImport("pdh.dll", SetLastError = true)]
        public static extern uint PdhCloseQuery(IntPtr hQuery);
    }

    private static class NvmlNative {
        [StructLayout(LayoutKind.Sequential)]
        public struct nvmlUtilization_t {
            public uint gpu;
            public uint memory;
        }

        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
        public static extern int nvmlInit_v2();

        [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
        public static extern int nvmlShutdown();

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        public static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
        public static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint power);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
        public static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetClockInfo")]
        public static extern int nvmlDeviceGetClockInfo(IntPtr device, int clockType, out uint clockMHz);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates")]
        public static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out nvmlUtilization_t utilization);
    }

    private static class EtwNative {
        public const uint EVENT_TRACE_CONTROL_STOP = 1;
        public const uint EVENT_TRACE_REAL_TIME_MODE = 0x00000100;
        public const uint PROCESS_TRACE_MODE_REAL_TIME = 0x00000100;
        public const uint PROCESS_TRACE_MODE_EVENT_RECORD = 0x10000000;
        public const uint WNODE_FLAG_TRACED_GUID = 0x00020000;

        [StructLayout(LayoutKind.Sequential)]
        public struct WNODE_HEADER {
            public uint BufferSize;
            public uint ProviderId;
            public ulong HistoricalContext;
            public ulong TimeStamp;
            public Guid Guid;
            public uint ClientContext;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct EVENT_TRACE_PROPERTIES {
            public WNODE_HEADER Wnode;
            public uint BufferSize;
            public uint MinimumBuffers;
            public uint MaximumBuffers;
            public uint MaximumFileSize;
            public uint LogFileMode;
            public uint FlushTimer;
            public uint EnableFlags;
            public int AgeLimit;
            public uint NumberOfBuffers;
            public uint FreeBuffers;
            public uint EventsLost;
            public uint BuffersWritten;
            public uint LogBuffersLost;
            public uint RealTimeBuffersLost;
            public IntPtr LoggerThreadId;
            public uint LogFileNameOffset;
            public uint LoggerNameOffset;
        }

        public delegate void EventRecordCallback(IntPtr pEventRecord);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct EVENT_TRACE_LOGFILEW {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LogFileName;
            [MarshalAs(UnmanagedType.LPWStr)]
            public string LoggerName;
            public long CurrentTime;
            public uint BuffersRead;
            public uint ProcessTraceMode;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 96)]
            public byte[] CurrentEvent;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 280)]
            public byte[] LogfileHeader;
            public IntPtr BufferCallback;
            public uint BufferSize;
            public uint Filled;
            public uint EventsLost;
            public IntPtr EventRecordCallback;
            public uint IsKernelTrace;
            public IntPtr Context;
        }

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint StartTraceW(out ulong sessionHandle, string sessionName, IntPtr properties);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern uint ControlTraceW(ulong sessionHandle, string sessionName, IntPtr properties, uint controlCode);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern uint EnableTraceEx2(ulong traceHandle, ref Guid providerId, uint controlCode, byte level, ulong matchAnyKeyword, ulong matchAllKeyword, uint timeout, IntPtr enableParameters);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern ulong OpenTraceW(IntPtr pLogfile);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern uint ProcessTrace(ulong[] handleArray, uint handleCount, IntPtr startTime, IntPtr endTime);

        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        public static extern uint CloseTrace(ulong traceHandle);
    }

}
