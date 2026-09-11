using System;
using System.Collections.Generic;
using System.Diagnostics;

public partial class PowerCoreEngine : IDisposable {
    // COMPUTE CYCLE FPS (HARDWARE TIMESTAMP DELTAS & HOLDING HYSTERESIS)
    // ---------------------------------------------------------------------------------------------
    private Dictionary<int, double> ComputeCycleFps(Dictionary<int, int> frameCounts, Dictionary<int, long> firstTsMap, Dictionary<int, long> lastTsMap) {
        DateTime now = DateTime.UtcNow;
        Dictionary<int, double> res = new Dictionary<int, double>();

        // 1. Process active ETW flushes with hardware timestamp deltas
        if (frameCounts != null) {
            foreach (KeyValuePair<int, int> kvp in frameCounts) {
                int pid = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;

                long lastTs = 0;
                lastTsMap.TryGetValue(pid, out lastTs);
                long prevTs = 0;
                _prevFlushTsMap.TryGetValue(pid, out prevTs);

                double durationSec = 0.0;
                int frameDelta = count;

                if (prevTs > 0 && lastTs > prevTs) {
                    durationSec = (lastTs - prevTs) / 10000000.0;
                } else {
                    long firstTs = 0;
                    firstTsMap.TryGetValue(pid, out firstTs);
                    if (count > 1 && lastTs > firstTs) {
                        durationSec = (lastTs - firstTs) / 10000000.0;
                        frameDelta = count - 1;
                    }
                }

                if (lastTs > 0) {
                    _prevFlushTsMap[pid] = lastTs;
                }

                if (durationSec > 0.05) {
                    double fps = Math.Round(frameDelta / durationSec, 1);
                    _pidFpsMap[pid] = fps;
                    res[pid] = fps;
                }
            }
        }

        // 2. Preserve FPS during holding intervals between ETW buffer flushes (< 1.5s)
        lock (_syncLock) {
            List<int> expired = new List<int>();
            foreach (KeyValuePair<int, DateTime> kvp in _lastPresentMap) {
                int pid = kvp.Key;
                if (!res.ContainsKey(pid)) {
                    if ((now - kvp.Value).TotalSeconds < 1.5) {
                        double cachedFps;
                        if (_pidFpsMap.TryGetValue(pid, out cachedFps)) {
                            res[pid] = cachedFps;
                        }
                    } else {
                        expired.Add(pid);
                    }
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                _pidFpsMap.Remove(expired[i]);
                _prevFlushTsMap.Remove(expired[i]);
            }
        }

        return res;
    }

    // RESOLVE FALLBACK FPS (DWM SWAPCHAIN CADENCE OR HARDWARE WORKLOAD ESTIMATE)
    // ---------------------------------------------------------------------------------------------
    private double ResolveFallbackFps(Dictionary<int, double> cycleFpsMap) {
        if (_dwmPid <= 0) {
            try {
                Process[] procs = Process.GetProcessesByName("dwm");
                if (procs.Length > 0) _dwmPid = procs[0].Id;
            } catch { }
        }
        double dwmFps = 0.0;
        if (_dwmPid > 0 && cycleFpsMap != null && cycleFpsMap.TryGetValue(_dwmPid, out dwmFps) && dwmFps >= 5.0) {
            return Math.Round(dwmFps, 1);
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
