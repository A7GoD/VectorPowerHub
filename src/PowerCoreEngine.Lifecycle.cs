using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // ENGINE LIFECYCLE
    // ---------------------------------------------------------------------------------------------
    public void Start() {
        lock (_syncLock) {
            if (_isRunning) return;
            _isRunning = true;
            _stopEvent.Reset();

            InitPdh();
            StartEtw();

            // Set initial state to saved desktop profile
            ApplyProfileInternal(!string.IsNullOrEmpty(_selectedDesktopProfile) ? _selectedDesktopProfile : "desktop");

            _workerThread = new Thread(TelemetryWorkerLoop);
            _workerThread.IsBackground = true;
            _workerThread.Name = "PowerCoreEngine_Worker";
            _workerThread.Start();
        }
    }

    public void Stop() {
        lock (_syncLock) {
            if (!_isRunning) return;
            _isRunning = false;
        }

        _benchmarkCancelRequested = true;
        _stopEvent.Set();

        if (_workerThread != null && _workerThread.IsAlive) {
            _workerThread.Join(1500);
            _workerThread = null;
        }

        StopEtw();
        ForceStopEtwSession();
        ShutdownNvml();
        ClosePdh();

        // Restore unconstrained desktop profile on stop
        ApplyProfileInternal("desktop");
        ApplyGpuClockLimit(0);

        try {
            File.WriteAllText(FpsFilePath, "0.0");
        } catch { }
    }

    public void Dispose() {
        try {
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        } catch { }
        try {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        } catch { }
        try {
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        } catch { }

        Stop();
        try { _stopEvent.Close(); } catch { }
        try { _wakeLoopEvent.Close(); } catch { }
    }

    // ---------------------------------------------------------------------------------------------
    // SLEEP / MODERN STANDBY & PROCESS LIFECYCLE HOOKS
    // ---------------------------------------------------------------------------------------------
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) {
        if (e.Mode == PowerModes.Suspend) {
            HandleSystemSuspend();
        } else if (e.Mode == PowerModes.Resume) {
            HandleSystemResume();
        }
    }

    private void HandleSystemSuspend() {
        lock (_syncLock) {
            _isSuspended = true;
            _wasEtwRunningBeforeSuspend = _isEtwActive;

            // 1. IMMEDIATELY stop ETW session (CloseTrace and ControlTraceW) to prevent kernel deadlocks
            StopEtw();
            ForceStopEtwSession();

            // 2. Stop/reset game mode state
            _isGameMode = false;
            _currentGamePid = 0;
            _currentGameName = "";
            _gameModeExitTimer = 0.0;
            _currentFps = 0.0;

            // 3. Revert CPU power settings to stock Balanced (PROCFREQMAX=0, PERFBOOSTMODE=3, PERFEPP=50%)
            ApplySettingsInternal("desktop", 0, 0, 3, 50, 0);
            _activeProfile = "desktop";

            // 4. Release NVML handles immediately
            ShutdownNvml();
        }

        // Revert GPU clocks (nvidia-smi -rgc) synchronously so GPU enters D3cold immediately
        try {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "nvidia-smi.exe";
            psi.Arguments = "-rgc";
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process p = Process.Start(psi)) {
                if (p != null) {
                    p.WaitForExit(1000);
                }
            }
        } catch { }

        try {
            File.WriteAllText(FpsFilePath, "0.0");
        } catch { }
    }

    private void HandleSystemResume() {
        // Wait 2.0 seconds for display subsystem and drivers to wake cleanly
        ThreadPool.QueueUserWorkItem(delegate(object state) {
            try {
                Thread.Sleep(2000);

                lock (_syncLock) {
                    if (!_isRunning) return;

                    // Re-initialize PDH performance counters after sleep
                    InitPdh();

                    // Safely resume ETW session if previously running
                    if (_wasEtwRunningBeforeSuspend) {
                        StartEtw();
                    }

                    _isSuspended = false;
                }
            } catch { }
        });
    }

    private void OnProcessExit(object sender, EventArgs e) {
        CleanupOnExit();
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        CleanupOnExit();
    }

    private void CleanupOnExit() {
        try {
            StopEtw();
            ForceStopEtwSession();
            ShutdownNvml();
            ApplyGpuClockLimit(0);
            ApplySettingsInternal("desktop", 0, 0, 3, 50, 0);
        } catch { }
    }

    // ---------------------------------------------------------------------------------------------

}
