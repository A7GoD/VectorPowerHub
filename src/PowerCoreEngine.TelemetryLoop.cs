using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // TELEMETRY EVALUATION LOOP (500MS CADENCE FOR SMOOTH HYSTERESIS & INSTANT D3COLD SAFETY)
    // ---------------------------------------------------------------------------------------------
    private void TelemetryWorkerLoop() {
        while (_isRunning) {
            try {
                if (!_isSuspended) {
                    EvaluateCycle();
                }
            } catch { }

            // Sleep precisely 500ms, or wake immediately on stop
            if (_stopEvent.WaitOne(500)) {
                break;
            }
        }
    }

    private void EvaluateCycle() {
        DateTime now = DateTime.UtcNow;

        // 1. Calculate Elapsed Time & Frame Delta
        double elapsed = (now - _lastFpsCalcTime).TotalSeconds;
        if (elapsed <= 0.001) elapsed = 0.5;
        _lastFpsCalcTime = now;

        // Ensure ETW session is healthy and actively capturing
        if (!_isEtwActive || _etwSessionHandle == 0) {
            StartEtw();
        }

        // Periodic refresh of GameConfigStore cache
        RefreshGameConfigStore(false);

        // Snapshot and clear frame count buffers
        Dictionary<int, int> frameCountsThisCycle;
        lock (_syncLock) {
            frameCountsThisCycle = new Dictionary<int, int>(_frameCounterMap);
            _frameCounterMap.Clear();
            _currentGameFramesThisSecond = 0;
        }

        // 2. Scan presenting processes for active game
        int detectedGamePid = 0;
        string detectedGameName = "";
        double detectedFps = 0.0;

        int foregroundPid = Win32Native.GetForegroundProcessId();

        // Pass 1: If current foreground window is a game and actively presenting, prioritize it
        if (foregroundPid > 4) {
            int fgFrames = 0;
            frameCountsThisCycle.TryGetValue(foregroundPid, out fgFrames);
            double fgFps = fgFrames / elapsed;
            string fgName;
            if (fgFps >= 10.0 && IsGameProcess(foregroundPid, out fgName)) {
                detectedGamePid = foregroundPid;
                detectedGameName = fgName;
                detectedFps = fgFps;
            }
        }

        // Pass 2: If foreground isn't a game (e.g. user clicked VectorPowerHub or on dual monitors),
        // check if our current game is still presenting
        if (detectedGamePid == 0 && _isGameMode && _currentGamePid > 0) {
            try {
                using (Process curP = Process.GetProcessById(_currentGamePid)) {
                    if (!curP.HasExited) {
                        int curFrames = 0;
                        frameCountsThisCycle.TryGetValue(_currentGamePid, out curFrames);
                        double curFps = curFrames / elapsed;
                        if (curFps >= 5.0) {
                            detectedGamePid = _currentGamePid;
                            detectedGameName = _currentGameName;
                            detectedFps = curFps;
                        }
                    }
                }
            } catch { }
        }

        // Pass 3: Scan all presenting processes in frameCountsThisCycle for any valid game
        if (detectedGamePid == 0) {
            double highestFps = 0.0;
            foreach (KeyValuePair<int, int> kvp in frameCountsThisCycle) {
                int pid = kvp.Key;
                double fps = kvp.Value / elapsed;
                if (fps >= 10.0 && pid > 4) {
                    string gameName;
                    if (IsGameProcess(pid, out gameName)) {
                        if (fps > highestFps) {
                            highestFps = fps;
                            detectedGamePid = pid;
                            detectedGameName = gameName;
                            detectedFps = fps;
                        }
                    }
                }
            }
        }


            EvaluateCycleEnforcement(elapsed, detectedGamePid, detectedGameName, detectedFps, frameCountsThisCycle);
        }
}
