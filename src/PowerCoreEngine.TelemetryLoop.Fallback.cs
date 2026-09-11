using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class PowerCoreEngine : IDisposable {
    // RESOLVE FALLBACK FPS (DWM SWAPCHAIN CADENCE OR HARDWARE WORKLOAD ESTIMATE)
    // ---------------------------------------------------------------------------------------------
    private double ResolveFallbackFps(Dictionary<int, int> frameCounts, double elapsed) {
        if (_dwmPid <= 0) {
            try {
                Process[] procs = Process.GetProcessesByName("dwm");
                if (procs.Length > 0) _dwmPid = procs[0].Id;
            } catch { }
        }
        int dwmFrames = 0;
        if (_dwmPid > 0 && frameCounts != null && elapsed > 0.001 && frameCounts.TryGetValue(_dwmPid, out dwmFrames) && dwmFrames > 0) {
            double dwmFps = dwmFrames / elapsed;
            if (dwmFps >= 5.0) return Math.Round(dwmFps, 1);
        }

        // Hardware Fallback: If 3D graphics workload is drawing high power, estimate real-time rendering rate
        if (_currentSnapshot.GpuPowerW >= 35.0 && _currentSnapshot.GpuUtilPct >= 20) {
            double targetHz = (_currentSnapshot.IsNvidiaDisplayAttached) ? 179.0 : 240.0;
            double estFps = targetHz * (_currentSnapshot.GpuUtilPct / 100.0);
            if (estFps >= 30.0) return Math.Round(estFps, 1);
        }
        return 0.0;
    }
}
