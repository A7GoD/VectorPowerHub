using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // ETW DXGI PRESENT EVENT TRACING
    // ---------------------------------------------------------------------------------------------
    private static void ResetTraceProperties(IntPtr pProps, int propBufferSize) {
        for (int i = 0; i < propBufferSize; i++) Marshal.WriteByte(pProps, i, 0);
        EtwNative.EVENT_TRACE_PROPERTIES props = new EtwNative.EVENT_TRACE_PROPERTIES();
        props.Wnode.BufferSize = (uint)propBufferSize;
        props.Wnode.Flags = EtwNative.WNODE_FLAG_TRACED_GUID;
        props.LogFileMode = EtwNative.EVENT_TRACE_REAL_TIME_MODE;
        props.LoggerNameOffset = (uint)Marshal.SizeOf(typeof(EtwNative.EVENT_TRACE_PROPERTIES));
        Marshal.StructureToPtr(props, pProps, false);
    }

    private void StartEtw() {
        StopEtw(); // Ensure any running ETW session is completely closed and stopped

        string sessionName = "PowerCoreEngine_DXGI_ETW";
        int propBufferSize = 1024;
        _pSessionProperties = Marshal.AllocHGlobal(propBufferSize);

        // Terminate any leftover trace session with same name
        ResetTraceProperties(_pSessionProperties, propBufferSize);
        EtwNative.ControlTraceW(0, sessionName, _pSessionProperties, EtwNative.EVENT_TRACE_CONTROL_STOP);

        // Re-initialize clean properties before StartTraceW
        ResetTraceProperties(_pSessionProperties, propBufferSize);
        uint startRes = EtwNative.StartTraceW(out _etwSessionHandle, sessionName, _pSessionProperties);
        if (startRes != 0) {
            ResetTraceProperties(_pSessionProperties, propBufferSize);
            EtwNative.ControlTraceW(0, sessionName, _pSessionProperties, EtwNative.EVENT_TRACE_CONTROL_STOP);
            ResetTraceProperties(_pSessionProperties, propBufferSize);
            startRes = EtwNative.StartTraceW(out _etwSessionHandle, sessionName, _pSessionProperties);
            if (startRes != 0) {
                return;
            }
        }

        // Enable Microsoft-Windows-DXGI ({CA11C036-0102-4A2D-A6AD-F03CFED5D3C9}) Event 42 (IDXGISwapChain::Present)
        Guid dxgiGuid = new Guid("CA11C036-0102-4A2D-A6AD-F03CFED5D3C9");
        EtwNative.EnableTraceEx2(_etwSessionHandle, ref dxgiGuid, 1, 5, 0, 0, 0, IntPtr.Zero);

        _etwCallbackDelegate = new EtwNative.EventRecordCallback(OnEtwEvent);

        EtwNative.EVENT_TRACE_LOGFILEW logfile = new EtwNative.EVENT_TRACE_LOGFILEW();
        logfile.LoggerName = sessionName;
        logfile.ProcessTraceMode = EtwNative.PROCESS_TRACE_MODE_REAL_TIME | EtwNative.PROCESS_TRACE_MODE_EVENT_RECORD;
        logfile.EventRecordCallback = Marshal.GetFunctionPointerForDelegate(_etwCallbackDelegate);
        logfile.CurrentEvent = new byte[88];
        logfile.LogfileHeader = new byte[280];

        _pLogfile = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(EtwNative.EVENT_TRACE_LOGFILEW)));
        Marshal.StructureToPtr(logfile, _pLogfile, false);

        _etwTraceHandle = EtwNative.OpenTraceW(_pLogfile);
        if (_etwTraceHandle == INVALID_PROCESSTRACE_HANDLE || _etwTraceHandle == 0) {
            return;
        }

        _isEtwActive = true;
        _etwThread = new Thread(delegate() {
            try {
                ulong[] handles = new ulong[] { _etwTraceHandle };
                EtwNative.ProcessTrace(handles, 1, IntPtr.Zero, IntPtr.Zero);
            } catch { }
        });
        _etwThread.IsBackground = true;
        _etwThread.Name = "PowerCoreEngine_ETW";
        _etwThread.Start();
    }

    private void OnEtwEvent(IntPtr pRecord) {
        try {
            int pid = Marshal.ReadInt32(pRecord, 12);
            if (pid <= 4) return;

            ushort eventId = (ushort)Marshal.ReadInt16(pRecord, 40);
            // Event ID 42: DXGI SwapChain Present Start
            if (eventId == 42) {
                DateTime now = DateTime.UtcNow;
                lock (_syncLock) {
                    _lastPresentMap[pid] = now;
                    int count;
                    if (_frameCounterMap.TryGetValue(pid, out count)) {
                        _frameCounterMap[pid] = count + 1;
                    } else {
                        _frameCounterMap[pid] = 1;
                    }

                    if (pid == _currentGamePid) {
                        _currentGameFramesThisSecond++;
                    }
                }
            }
        } catch { }
    }

    private void StopEtw() {
        _isEtwActive = false;
        if (_etwTraceHandle != 0 && _etwTraceHandle != INVALID_PROCESSTRACE_HANDLE) {
            try {
                EtwNative.CloseTrace(_etwTraceHandle);
            } catch { }
            _etwTraceHandle = 0;
        }

        if (_pSessionProperties != IntPtr.Zero) {
            try {
                EtwNative.ControlTraceW(_etwSessionHandle, "PowerCoreEngine_DXGI_ETW", _pSessionProperties, EtwNative.EVENT_TRACE_CONTROL_STOP);
            } catch { }
            _etwSessionHandle = 0;
        } else {
            ForceStopEtwSession();
        }

        if (_etwThread != null && _etwThread.IsAlive) {
            try {
                _etwThread.Join(500);
            } catch { }
            _etwThread = null;
        }

        if (_pLogfile != IntPtr.Zero) {
            try {
                Marshal.FreeHGlobal(_pLogfile);
            } catch { }
            _pLogfile = IntPtr.Zero;
        }
        if (_pSessionProperties != IntPtr.Zero) {
            try {
                Marshal.FreeHGlobal(_pSessionProperties);
            } catch { }
            _pSessionProperties = IntPtr.Zero;
        }
    }

    public static void ForceStopEtwSession() {
        try {
            int propBufferSize = 1024;
            IntPtr pProps = Marshal.AllocHGlobal(propBufferSize);
            try {
                for (int i = 0; i < propBufferSize; i++) Marshal.WriteByte(pProps, i, 0);
                EtwNative.EVENT_TRACE_PROPERTIES props = new EtwNative.EVENT_TRACE_PROPERTIES();
                props.Wnode.BufferSize = (uint)propBufferSize;
                props.Wnode.Flags = EtwNative.WNODE_FLAG_TRACED_GUID;
                props.LogFileMode = EtwNative.EVENT_TRACE_REAL_TIME_MODE;
                props.LoggerNameOffset = (uint)Marshal.SizeOf(typeof(EtwNative.EVENT_TRACE_PROPERTIES));
                Marshal.StructureToPtr(props, pProps, false);
                EtwNative.ControlTraceW(0, "PowerCoreEngine_DXGI_ETW", pProps, EtwNative.EVENT_TRACE_CONTROL_STOP);
            } finally {
                Marshal.FreeHGlobal(pProps);
            }
        } catch { }
    }

    // ---------------------------------------------------------------------------------------------

}
