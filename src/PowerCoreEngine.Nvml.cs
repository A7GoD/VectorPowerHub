using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // GPU TELEMETRY (SAFE NVML IN-GAME QUERIES)
    // ---------------------------------------------------------------------------------------------
    private void EnsureNvmlInitialized() {
        if (_isNvmlInitialized) return;
        try {
            int res = NvmlNative.nvmlInit_v2();
            if (res == 0) {
                IntPtr dev;
                if (NvmlNative.nvmlDeviceGetHandleByIndex_v2(0, out dev) == 0) {
                    _nvmlDevice = dev;
                    _isNvmlInitialized = true;
                }
            }
        } catch {
            _isNvmlInitialized = false;
            _nvmlDevice = IntPtr.Zero;
        }
    }

    private void ReadGpuTelemetrySafe(out double gpuWatts, out int gpuClockMhz, out int gpuTempC, out int gpuUtilPct, out string gpuStatus) {
        gpuWatts = 0.0;
        gpuClockMhz = 0;
        gpuTempC = 0;
        gpuUtilPct = 0;
        gpuStatus = "3D Active (140W Boost)";

        if (!_isNvmlInitialized || _nvmlDevice == IntPtr.Zero) {
            EnsureNvmlInitialized();
        }

        if (_isNvmlInitialized && _nvmlDevice != IntPtr.Zero) {
            try {
                uint mw;
                if (NvmlNative.nvmlDeviceGetPowerUsage(_nvmlDevice, out mw) == 0) {
                    gpuWatts = mw / 1000.0;
                }

                uint temp;
                if (NvmlNative.nvmlDeviceGetTemperature(_nvmlDevice, 0, out temp) == 0) {
                    gpuTempC = (int)temp;
                }

                uint clk;
                if (NvmlNative.nvmlDeviceGetClockInfo(_nvmlDevice, 0, out clk) == 0) {
                    gpuClockMhz = (int)clk;
                }

                NvmlNative.nvmlUtilization_t util;
                if (NvmlNative.nvmlDeviceGetUtilizationRates(_nvmlDevice, out util) == 0) {
                    gpuUtilPct = (int)util.gpu;
                }

                gpuStatus = string.Format("3D Active ({0:F1}W)", gpuWatts);
            } catch {
                gpuStatus = "3D Active";
            }
        }
    }

    private void ShutdownNvml() {
        if (!_isNvmlInitialized) return;
        try {
            _nvmlDevice = IntPtr.Zero;
            _isNvmlInitialized = false;
            NvmlNative.nvmlShutdown();
        } catch { }
    }

    // ---------------------------------------------------------------------------------------------

}
