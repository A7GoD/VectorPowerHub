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

            int cadence = (_isGameMode || _isBenchmarking) ? 500 : (_isTrayMinimized ? 2000 : 750);
            int sign = WaitHandle.WaitAny(new WaitHandle[] { _stopEvent, _wakeLoopEvent }, cadence);
            if (sign == 0) {
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

        // Snapshot and clear frame count buffers and timestamps
        Dictionary<int, int> frameCountsThisCycle;
        Dictionary<int, long> firstTsThisCycle, lastTsThisCycle;
        lock (_syncLock) {
            frameCountsThisCycle = new Dictionary<int, int>(_frameCounterMap);
            firstTsThisCycle = new Dictionary<int, long>(_firstPresentTsMap);
            lastTsThisCycle = new Dictionary<int, long>(_lastPresentTsMap);
            _frameCounterMap.Clear();
            _firstPresentTsMap.Clear();
            _lastPresentTsMap.Clear();
            _currentGameFramesThisSecond = 0;
        }

        Dictionary<int, double> cycleFpsMap = ComputeCycleFps(frameCountsThisCycle, firstTsThisCycle, lastTsThisCycle);
        int detectedGamePid = 0;
        string detectedGameName = "";
        double detectedFps = 0.0;
        int foregroundPid = Win32Native.GetForegroundProcessId();

        // Pass 0: Foreground Window Hysteresis (Alt-Tab to VectorPowerHub)
        if ((foregroundPid == _currentHubPid || foregroundPid <= 0) && _isGameMode && _currentGamePid > 0) {
            bool isAlive = false;
            try {
                using (Process curP = Process.GetProcessById(_currentGamePid)) {
                    if (!curP.HasExited) isAlive = true;
                }
            } catch { }

            if (isAlive) {
                detectedGamePid = _currentGamePid;
                detectedGameName = _currentGameName;
                double curFps = 0.0;
                cycleFpsMap.TryGetValue(_currentGamePid, out curFps);
                detectedFps = (curFps >= 1.0) ? curFps : _currentFps;
            }
        }

        // Pass 1: If current foreground window is a game and actively presenting, prioritize it
        if (detectedGamePid == 0 && foregroundPid > 4 && foregroundPid != _currentHubPid) {
            double fgFps = 0.0;
            cycleFpsMap.TryGetValue(foregroundPid, out fgFps);
            string fgName;
            if (fgFps >= 10.0 && IsGameProcess(foregroundPid, false, out fgName)) {
                detectedGamePid = foregroundPid;
                detectedGameName = fgName;
                detectedFps = fgFps;
            }
        }

        // Pass 2: If foreground isn't a game, check if our current game is still presenting
        if (detectedGamePid == 0 && _isGameMode && _currentGamePid > 0) {
            try {
                using (Process curP = Process.GetProcessById(_currentGamePid)) {
                    if (!curP.HasExited) {
                        double curFps = 0.0;
                        cycleFpsMap.TryGetValue(_currentGamePid, out curFps);
                        if (curFps >= 5.0) {
                            detectedGamePid = _currentGamePid;
                            detectedGameName = _currentGameName;
                            detectedFps = curFps;
                        }
                    }
                }
            } catch { }
        }

        // Pass 3: Scan all presenting processes in cycleFpsMap for any valid game
        if (detectedGamePid == 0) {
            double highestFps = 0.0;
            foreach (KeyValuePair<int, double> kvp in cycleFpsMap) {
                int pid = kvp.Key;
                double fps = kvp.Value;
                if (fps >= 10.0 && pid > 4 && pid != _currentHubPid) {
                    string gameName;
                    if (IsGameProcess(pid, false, out gameName)) {
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

        // Pass 3b: Permissive check for presenting game (custom directories, child processes, EA/Xbox titles)
        if (detectedGamePid == 0) {
            double highestFps = 0.0;
            foreach (KeyValuePair<int, double> kvp in cycleFpsMap) {
                int pid = kvp.Key;
                double fps = kvp.Value;
                if (fps >= 10.0 && pid > 4 && pid != _currentHubPid && pid != _dwmPid) {
                    string gameName;
                    if (IsGameProcess(pid, true, out gameName)) {
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

        // Pass 4: Hardware Fallback (GPU Load >= 30% and Power >= 35W) - D3cold Safe
        string monNameCheck = "";
        bool dispAttached = CheckNvidiaDisplayAttached(out monNameCheck);
        if (detectedGamePid == 0 && (_isNvmlInitialized || _isGameMode || _isBenchmarking || dispAttached)) {
            double fbWatts; int fbClock, fbTemp, fbUtil; string fbStatus;
            ReadGpuTelemetrySafe(out fbWatts, out fbClock, out fbTemp, out fbUtil, out fbStatus);
            if (fbUtil >= 30 && fbWatts >= 35.0) {
                if (_isGameMode && _currentGamePid > 0) {
                    try {
                        using (Process curP = Process.GetProcessById(_currentGamePid)) {
                            if (!curP.HasExited) {
                                detectedGamePid = _currentGamePid;
                                detectedGameName = _currentGameName;
                                detectedFps = _currentFps;
                            }
                        }
                    } catch { }
                }
                if (detectedGamePid == 0 && foregroundPid > 4 && foregroundPid != _currentHubPid) {
                    string fgName;
                    if (IsGameProcess(foregroundPid, true, out fgName)) {
                        detectedGamePid = foregroundPid;
                        detectedGameName = fgName;
                        detectedFps = ResolveFallbackFps(cycleFpsMap);
                    }
                }
                if (detectedGamePid == 0) {
                    string candidateName;
                    int candidatePid = FindRunningGameCandidate(out candidateName);
                    if (candidatePid > 0) {
                        detectedGamePid = candidatePid;
                        detectedGameName = candidateName;
                        detectedFps = ResolveFallbackFps(cycleFpsMap);
                    }
                }
            }
        }

        EvaluateCycleEnforcement(elapsed, detectedGamePid, detectedGameName, detectedFps, cycleFpsMap, foregroundPid);
    }
}
