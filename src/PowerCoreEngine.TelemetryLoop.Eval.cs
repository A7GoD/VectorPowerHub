using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
        private void EvaluateCycleEnforcement(double elapsed, int detectedGamePid, string detectedGameName, double detectedFps, Dictionary<int, int> frameCountsThisCycle) {
            DateTime now = DateTime.UtcNow;
        // 3. State Machine Transition & Profile Enforcement
        if (!_isBenchmarking) {
            if (detectedGamePid > 0) {
                _gameModeExitTimer = 0.0;
                _currentFps = detectedFps;
                _currentGamePid = detectedGamePid;
                _currentGameName = detectedGameName;

                if (!_isGameMode) {
                    _isGameMode = true;
                    if (_autoProfileSwitching) {
                        ApplyProfileInternal(_selectedGamingProfile);
                    }
                    EnsureNvmlInitialized();
                }
            } else {
                if (_isGameMode) {
                    _gameModeExitTimer += elapsed;
                    // Sustained 3.0s idle / exit before reverting to desktop profile
                    if (_gameModeExitTimer >= 3.0) {
                        _isGameMode = false;
                        _currentGamePid = 0;
                        _currentGameName = "";
                        _currentFps = 0.0;
                        _gameModeExitTimer = 0.0;

                        if (_autoProfileSwitching) {
                            ApplyProfileInternal(_selectedDesktopProfile);
                            ShutdownNvml();
                        }
                    }
                } else {
                    _currentFps = 0.0;
                    _currentGamePid = 0;
                    _currentGameName = "";
                    _gameModeExitTimer = 0.0;
                }
            }
        } else {
            // Benchmarking active: Track FPS of active game if present
            if (_currentGamePid > 0) {
                int activeFrames = 0;
                frameCountsThisCycle.TryGetValue(_currentGamePid, out activeFrames);
                _currentFps = activeFrames / elapsed;
            }
        }

        // Clean up stale present records older than 15s
        lock (_syncLock) {
            List<int> expired = new List<int>();
            foreach (KeyValuePair<int, DateTime> kvp in _lastPresentMap) {
                if ((now - kvp.Value).TotalSeconds > 15.0) {
                    expired.Add(kvp.Key);
                }
            }
            for (int i = 0; i < expired.Count; i++) {
                _lastPresentMap.Remove(expired[i]);
            }
        }

        // Write live FPS to game_fps.txt for cross-process telemetry
        try {
            string fpsStr = _isGameMode ? _currentFps.ToString("F1") : "0.0";
            File.WriteAllText(FpsFilePath, fpsStr);
        } catch { }

        // 4. Read CPU Telemetry (Intel RAPL Package Power + 24-Core Clocks & Util)
        double cpuWatts = 0.0;
        double pCoreGhz = 0.0;
        double eCoreGhz = 0.0;
        double[] perCoreGhz;
        double[] perCoreUtil;
        ReadCpuTelemetry(out cpuWatts, out pCoreGhz, out eCoreGhz, out perCoreGhz, out perCoreUtil);

        // 5. Read GPU Telemetry (Physical Display & In-Game Detection with D3cold Protection)
        double gpuWatts = 0.0;
        int gpuClockMhz = 0;
        int gpuTempC = 0;
        int gpuUtilPct = 0;
        string gpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";

        string monitorName = "";
        bool isDisplayAttached = CheckNvidiaDisplayAttached(out monitorName);

        if (isDisplayAttached) {
            // NVIDIA GPU is actively driving an attached display (e.g. BENQ EX271Q) in D0 active state!
            // Query NVML safely because the GPU is already awake refreshing the display.
            ReadGpuTelemetrySafe(out gpuWatts, out gpuClockMhz, out gpuTempC, out gpuUtilPct, out gpuStatus);
            gpuStatus = string.Format("Active (D0) • Driving {0} (P8)", monitorName);
        } else if (_isGameMode || _isBenchmarking) {
            // 3D Game or Benchmark is actively rendering on discrete GPU
            ReadGpuTelemetrySafe(out gpuWatts, out gpuClockMhz, out gpuTempC, out gpuUtilPct, out gpuStatus);
            gpuStatus = "3D Active (140W Dynamic Boost)";
        } else {
            // No display attached to NVIDIA and no game rendering!
            // Discrete GPU is in true D3cold sleep. DO NOT POLL! Zero NVML calls.
            ShutdownNvml();
            gpuWatts = 0.0;
            gpuClockMhz = 0;
            gpuTempC = 0;
            gpuUtilPct = 0;
            gpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";
        }

        // 6. Construct Snapshot
        TelemetrySnapshot snapshot = new TelemetrySnapshot();
        snapshot.Fps = _currentFps;
        snapshot.CpuPowerW = cpuWatts;
        snapshot.PCoreGhz = pCoreGhz;
        snapshot.ECoreGhz = eCoreGhz;
        snapshot.PerCoreGhz = perCoreGhz;
        snapshot.PerCoreUtil = perCoreUtil;
        snapshot.GpuPowerW = gpuWatts;
        snapshot.GpuClockMhz = gpuClockMhz;
        snapshot.GpuTempC = gpuTempC;
        snapshot.GpuUtilPct = gpuUtilPct;
        snapshot.IsGameMode = _isGameMode;
        snapshot.ActiveGameName = _currentGameName;
        snapshot.ActiveGamePid = _currentGamePid;
        snapshot.GpuStatus = gpuStatus;
        snapshot.TotalPlatformPowerW = cpuWatts + gpuWatts;
        snapshot.IsNvidiaDisplayAttached = isDisplayAttached;
        snapshot.NvidiaMonitorName = monitorName;
        snapshot.ActiveProfile = _activeProfile;
        snapshot.SelectedGamingProfile = _selectedGamingProfile;
        snapshot.SelectedDesktopProfile = _selectedDesktopProfile;
        snapshot.AutoProfileSwitching = _autoProfileSwitching;

        lock (_syncLock) {
            _currentSnapshot = snapshot;
        }

        // 7. Notify Subscribers
        EventHandler<TelemetrySnapshot> handler = OnTelemetryUpdated;
        if (handler != null) {
            try {
                handler(this, snapshot);
            } catch { }
        }
    }

}
