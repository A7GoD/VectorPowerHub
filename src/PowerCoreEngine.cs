using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public struct TelemetrySnapshot {
    public double Fps;
    public double CpuPowerW;
    public double PCoreGhz;
    public double ECoreGhz;
    public double[] PerCoreGhz;
    public double[] PerCoreUtil;
    public double GpuPowerW;
    public int GpuClockMhz;
    public int GpuTempC;
    public int GpuUtilPct;
    public bool IsGameMode;
    public string ActiveGameName;
    public int ActiveGamePid;
    public string GpuStatus; // e.g. "D3cold Sleeping (0.0W) • PCIe Link Off" or "Active (D0) • Driving BENQ EX271Q (P8)"
    public double TotalPlatformPowerW;
    public bool IsNvidiaDisplayAttached;
    public string NvidiaMonitorName;
}

public struct BenchmarkProgress {
    public string CurrentProfileName;
    public int CurrentProfileIndex; // 1-based
    public int TotalProfiles;
    public int CurrentIteration;    // 1-based
    public int TotalIterations;
    public int WarmupRemainingSeconds;
    public bool IsWarmingUp;
    public string StatusMessage;
    public double CurrentFps;
    public double CurrentCpuPowerW;
    public double CurrentGpuPowerW;
}

public class BenchmarkSample {
    public double Fps;
    public double CpuPowerW;
    public double GpuPowerW;
    public double TotalPowerW;
    public double PCoreGhz;
    public double ECoreGhz;
    public int GpuTempC;
    public int GpuClockMhz;
    public int GpuUtilPct;
    public bool IsAcPower;
    public bool IsLoadingScreen;
    public bool IsOutlier;
    public string OutlierReason;

    public BenchmarkSample() {
        OutlierReason = "";
    }
}

public class BenchmarkResult {
    public string ProfileId;
    public string ProfileName;

    // Raw metrics
    public double RawAvgFps;
    public double RawMinFps;
    public double Raw1PercentLowFps;
    public double RawAvgCpuPowerW;
    public double RawAvgGpuPowerW;
    public double RawAvgTotalPowerW;
    public int RawSampleCount;

    // Cleaned metrics (outliers removed: power cuts, loading screens)
    public double CleanedAvgFps;
    public double CleanedMinFps;
    public double Cleaned1PercentLowFps;
    public double CleanedAvgCpuPowerW;
    public double CleanedAvgGpuPowerW;
    public double CleanedAvgTotalPowerW;
    public int CleanedSampleCount;
    public int DiscardedSampleCount;

    public List<BenchmarkSample> Samples;

    public BenchmarkResult() {
        ProfileId = "";
        ProfileName = "";
        Samples = new List<BenchmarkSample>();
    }
}

public class PowerCoreEngine : IDisposable {
    // ---------------------------------------------------------------------------------------------
    // SINGLETON PATTERN
    // ---------------------------------------------------------------------------------------------
    private static readonly PowerCoreEngine _instance = new PowerCoreEngine();
    public static PowerCoreEngine Instance {
        get { return _instance; }
    }

    // ---------------------------------------------------------------------------------------------
    // PUBLIC EVENTS & PROPERTIES
    // ---------------------------------------------------------------------------------------------
    public event EventHandler<TelemetrySnapshot> OnTelemetryUpdated;

    private TelemetrySnapshot _currentSnapshot;
    public TelemetrySnapshot CurrentSnapshot {
        get {
            lock (_syncLock) {
                return _currentSnapshot;
            }
        }
    }

    private string _activeProfile;
    public string ActiveProfile {
        get {
            lock (_syncLock) {
                return _activeProfile;
            }
        }
    }

    private string _selectedGamingProfile;
    public string SelectedGamingProfile {
        get {
            lock (_syncLock) {
                return _selectedGamingProfile;
            }
        }
        set {
            lock (_syncLock) {
                if (!string.IsNullOrEmpty(value)) {
                    _selectedGamingProfile = value.Trim().ToLowerInvariant();
                    if (_isGameMode) {
                        ApplyProfileInternal(_selectedGamingProfile);
                    }
                }
            }
        }
    }

    private volatile bool _isRunning;
    public bool IsRunning {
        get { return _isRunning; }
    }

    private volatile bool _isEtwActive;
    public bool IsEtwActive {
        get { return _isEtwActive; }
    }

    private volatile bool _isBenchmarking;
    public bool IsBenchmarking {
        get { return _isBenchmarking; }
    }

    private volatile bool _benchmarkCancelRequested;

    // Custom Profile Configurable Parameters
    private int _customPCoreMhz;
    private int _customECoreMhz;
    private int _customBoostMode;
    private int _customEpp;
    private int _customGpuClockMhz;

    private static string FpsFilePath {
        get {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "game_fps.txt");
        }
    }

    private static string PcoreCapFilePath {
        get {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "game_pcore_cap.txt");
        }
    }

    // ---------------------------------------------------------------------------------------------
    // INTERNAL STATE & SYNCHRONIZATION
    // ---------------------------------------------------------------------------------------------
    private readonly object _syncLock = new object();
    private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
    private Thread _workerThread;

    private volatile bool _isGameMode;
    private int _currentGamePid;
    private string _currentGameName;

    // Sleep / Modern Standby Transition State
    private volatile bool _isSuspended;
    private bool _wasEtwRunningBeforeSuspend;

    // Windows Game Service / GameConfigStore Cache
    private readonly HashSet<string> _knownGameExes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _knownGamePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastGameStoreScan = DateTime.MinValue;

    // Sustained Rendering Hysteresis State
    private double _gameModeExitTimer;

    // ETW State
    private ulong _etwSessionHandle;
    private ulong _etwTraceHandle;
    private IntPtr _pSessionProperties;
    private IntPtr _pLogfile;
    private EtwNative.EventRecordCallback _etwCallbackDelegate;
    private Thread _etwThread;
    private const ulong INVALID_PROCESSTRACE_HANDLE = 0xFFFFFFFFFFFFFFFF;

    // Frame & Presentation Tracking
    private readonly Dictionary<int, DateTime> _lastPresentMap = new Dictionary<int, DateTime>();
    private readonly Dictionary<int, int> _frameCounterMap = new Dictionary<int, int>();
    private int _currentGameFramesThisSecond;
    private DateTime _lastFpsCalcTime;
    private double _currentFps;

    // PDH Performance Counter Handles
    private IntPtr _hPdhQuery;
    private IntPtr _hPdhCounterPwr;
    private IntPtr _hPdhCounterPCore;
    private IntPtr _hPdhCounterECore;
    private IntPtr[] _hPdhCounterPerCore = new IntPtr[24];
    private IntPtr[] _hPdhCounterPerCoreUtil = new IntPtr[24];
    private bool _isPdhInitialized;

    // NVML Device Handle
    private IntPtr _nvmlDevice;
    private bool _isNvmlInitialized;

    // ---------------------------------------------------------------------------------------------
    // POWER SCHEME & SETTING GUIDS
    // ---------------------------------------------------------------------------------------------
    private static readonly Guid BalancedSchemeGuid   = new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");
    private static readonly Guid PowerSaverSchemeGuid = new Guid("961cc777-2547-4f9d-8174-7d86181b8a7a");
    private static readonly Guid HighPerfSchemeGuid   = new Guid("ded574b5-45a0-4f42-8737-46345c09c238");

    private static readonly Guid SubProcessorGuid     = new Guid("54533251-82be-4824-96c1-47b60b740d00");
    private static readonly Guid PCoreMaxFreqGuid     = new Guid("75b0ae3f-bce0-45a7-8c89-c9611c25e101");
    private static readonly Guid ECoreMaxFreqGuid     = new Guid("75b0ae3f-bce0-45a7-8c89-c9611c25e100");
    private static readonly Guid BoostModeGuid        = new Guid("be337238-0d82-4146-a960-4f3749d470c7");
    private static readonly Guid EppGuid1             = new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6863");
    private static readonly Guid EppGuid2             = new Guid("36687f9e-e3a5-4dbf-b1dc-15eb381c6864");

    // ---------------------------------------------------------------------------------------------
    // EXCLUSION LIST & GAME DETECTION HINTS
    // ---------------------------------------------------------------------------------------------
    private static readonly HashSet<string> EXCLUDE_NAMES = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "dwm", "explorer", "ShellExperienceHost", "StartMenuExperienceHost",
        "TextInputHost", "ApplicationFrameHost", "SystemSettings", "WindowsTerminal",
        "cmd", "powershell", "pwsh", "Taskmgr", "SearchHost", "LockApp", "msedge", "chrome",
        "firefox", "zen", "opera", "brave", "Discord", "steamwebhelper", "Spotify", "OmApSvcBroker",
        "LEDKeeper2", "MControl", "ipf_helper", "audiodg", "svchost", "System", "Idle", "Registry", "Memory Compression",
        "nvcontainer", "NVDisplay.Container", "NVIDIA RTX Experience", "NVIDIA GeForce Experience",
        "gamingservices", "EdgeGameAssist", "XboxGameBarWidgets", "OpenConsole", "GooglePlayGamesServices", "agy",
        "AutoGamePowerOptimizer", "PowerCoreEngine", "VectorPowerHubForm", "VectorPowerHub", "test_gui",
        "steamservice", "steam", "OfficeClickToRun", "OfficeC2RClient", "AppVShNotify", "integratedoffice",
        "EpicGamesLauncher", "GalaxyClient", "GalaxyClientService", "Origin", "OriginWebHelperService",
        "EADesktop", "Battle.net", "RiotClientServices", "services", "lsass", "csrss", "smss",
        "wininit", "winlogon", "fontdrvhost", "spoolsv", "wlanext", "rundll32", "dllhost", "taskhostw",
        "devenv", "code", "rider", "clion", "studio64", "idea64",
        "msedgewebview2", "WebViewHost", "chrome_crashpad_handler", "vcredist", "dotnet"
    };

    private static readonly string[] GAME_PATH_HINTS = new string[] {
        "steamapps", "common", "epic games", "riot games", "xboxgames",
        "ubisoft", "ea games", "gog galaxy", "games", "game",
        "binaries\\win64", "binaries\\win32", "binaries/win64", "binaries/win32"
    };

    // ---------------------------------------------------------------------------------------------
    // CONSTRUCTOR & REGISTRATION
    // ---------------------------------------------------------------------------------------------
    public PowerCoreEngine() {
        _activeProfile = "desktop";
        _selectedGamingProfile = "snappy";
        _customPCoreMhz = 4900;
        _customECoreMhz = 2800;
        _customBoostMode = 4;
        _customEpp = 25;
        _customGpuClockMhz = 0;

        _currentGamePid = 0;
        _currentGameName = "";
        _isGameMode = false;
        _currentFps = 0.0;
        _lastFpsCalcTime = DateTime.UtcNow;

        _gameModeExitTimer = 0.0;

        _isSuspended = false;
        _wasEtwRunningBeforeSuspend = false;

        _currentSnapshot = new TelemetrySnapshot();
        _currentSnapshot.GpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";

        // Register system power modes and process lifecycle hooks
        try {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        } catch { }
        try {
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        } catch { }
        try {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        } catch { }

        // Initial scan of Windows GameConfigStore
        LoadGameConfigStore();
    }

    // ---------------------------------------------------------------------------------------------
    // ENGINE LIFECYCLE
    // ---------------------------------------------------------------------------------------------
    public void Start() {
        lock (_syncLock) {
            if (_isRunning) return;
            _isRunning = true;
            _stopEvent.Reset();

            InitPdh();
            StartEtw();

            // Set initial state to desktop profile
            ApplyProfileInternal("desktop");

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
        _stopEvent.Close();
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
    // WINDOWS GAME SERVICE / GAMECONFIGSTORE INTEGRATION
    // ---------------------------------------------------------------------------------------------
    private void LoadGameConfigStore() {
        RefreshGameConfigStore(true);
    }

    private void RefreshGameConfigStore(bool force) {
        DateTime now = DateTime.UtcNow;
        if (!force && (now - _lastGameStoreScan).TotalSeconds < 30.0) {
            return;
        }
        _lastGameStoreScan = now;

        try {
            using (RegistryKey childrenKey = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore\Children")) {
                if (childrenKey != null) {
                    string[] subKeyNames = childrenKey.GetSubKeyNames();
                    for (int i = 0; i < subKeyNames.Length; i++) {
                        string subKeyName = subKeyNames[i];
                        using (RegistryKey subKey = childrenKey.OpenSubKey(subKeyName)) {
                            if (subKey != null) {
                                object flagsObj = subKey.GetValue("Flags");
                                int flags = 0;
                                if (flagsObj is int) {
                                    flags = (int)flagsObj;
                                } else if (flagsObj != null) {
                                    int.TryParse(flagsObj.ToString(), out flags);
                                }

                                if ((flags & 1) != 0 || flags == 17) {
                                    object pathObj = subKey.GetValue("MatchedExeFullPath");
                                    if (pathObj != null) {
                                        string fullPath = pathObj.ToString().Trim();
                                        if (!string.IsNullOrEmpty(fullPath)) {
                                            fullPath = Environment.ExpandEnvironmentVariables(fullPath);
                                            lock (_syncLock) {
                                                _knownGamePaths.Add(fullPath);
                                                try {
                                                    string exeName = Path.GetFileName(fullPath);
                                                    if (!string.IsNullOrEmpty(exeName)) {
                                                        _knownGameExes.Add(exeName);
                                                        string nameWithoutExt = Path.GetFileNameWithoutExtension(exeName);
                                                        if (!string.IsNullOrEmpty(nameWithoutExt)) {
                                                            _knownGameExes.Add(nameWithoutExt);
                                                        }
                                                    }
                                                } catch { }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        } catch { }
    }

    // ---------------------------------------------------------------------------------------------
    // PROFILE MANAGEMENT
    // ---------------------------------------------------------------------------------------------
    public void ApplyProfile(string profileId) {
        if (string.IsNullOrEmpty(profileId)) return;
        lock (_syncLock) {
            ApplyProfileInternal(profileId.Trim().ToLowerInvariant());
        }
    }

    public void ApplyCustomProfile(int pcoreMhz, int ecoreMhz, int boostMode, int epp, int gpuClockMhz) {
        lock (_syncLock) {
            _customPCoreMhz = pcoreMhz;
            _customECoreMhz = ecoreMhz;
            _customBoostMode = boostMode;
            _customEpp = epp;
            _customGpuClockMhz = gpuClockMhz;

            ApplySettingsInternal("custom", pcoreMhz, ecoreMhz, boostMode, epp, gpuClockMhz);
            _activeProfile = "custom";
            if (_isGameMode) {
                _selectedGamingProfile = "custom";
            }
        }
    }

    private void ApplyProfileInternal(string profileId) {
        switch (profileId) {
            case "snappy":
                // P-core 0, E-core 0, Boost Mode 4, EPP 30%, GPU stock
                ApplySettingsInternal("snappy", 0, 0, 4, 30, 0);
                break;
            case "clamped":
                // P-core 4900, E-core 2800, Boost Mode 4, EPP 25%, GPU stock
                ApplySettingsInternal("clamped", 4900, 2800, 4, 25, 0);
                break;
            case "cold":
                // P-core 0, E-core 0, Boost Mode 3, EPP 20%, GPU clamped 2100 MHz
                ApplySettingsInternal("cold", 0, 0, 3, 20, 2100);
                break;
            case "guaranteed":
                // P-core 0, E-core 0, Boost Mode 6, EPP 25%, GPU stock
                ApplySettingsInternal("guaranteed", 0, 0, 6, 25, 0);
                break;
            case "desktop":
                // P-core 0, E-core 0, Boost Mode 3, EPP 50%, GPU stock
                ApplySettingsInternal("desktop", 0, 0, 3, 50, 0);
                break;
            case "custom":
                ApplySettingsInternal("custom", _customPCoreMhz, _customECoreMhz, _customBoostMode, _customEpp, _customGpuClockMhz);
                break;
            default:
                if (_isGameMode) {
                    ApplySettingsInternal("snappy", 0, 0, 4, 30, 0);
                    profileId = "snappy";
                } else {
                    ApplySettingsInternal("desktop", 0, 0, 3, 50, 0);
                    profileId = "desktop";
                }
                break;
        }

        _activeProfile = profileId;
        if (_isGameMode && profileId != "desktop") {
            _selectedGamingProfile = profileId;
        }
    }

    private void ApplySettingsInternal(string profileName, int pcoreMhz, int ecoreMhz, int boostMode, int epp, int gpuClockMhz) {
        // 1. Instant in-memory Windows Power Policy update via PowrProf Win32 APIs
        Guid[] schemes = new Guid[] { BalancedSchemeGuid, PowerSaverSchemeGuid, HighPerfSchemeGuid };
        Guid subProc = SubProcessorGuid;
        Guid pcoreGuid = PCoreMaxFreqGuid;
        Guid ecoreGuid = ECoreMaxFreqGuid;
        Guid boostGuid = BoostModeGuid;
        Guid eppGuid1 = EppGuid1;
        Guid eppGuid2 = EppGuid2;

        for (int i = 0; i < schemes.Length; i++) {
            Guid s = schemes[i];
            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref pcoreGuid, (uint)pcoreMhz);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref pcoreGuid, (uint)pcoreMhz);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref ecoreGuid, (uint)ecoreMhz);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref ecoreGuid, (uint)ecoreMhz);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref boostGuid, (uint)boostMode);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref boostGuid, (uint)boostMode);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid1, (uint)epp);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid1, (uint)epp);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid2, (uint)epp);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid2, (uint)epp);
        }

        Guid activeScheme = BalancedSchemeGuid;
        PowrProfNative.PowerSetActiveScheme(IntPtr.Zero, ref activeScheme);

        // 2. Persist target P-Core frequency for cooperative tools
        try {
            File.WriteAllText(PcoreCapFilePath, pcoreMhz.ToString() + "\n");
        } catch { }

        // 3. One-shot GPU locked clock management (Only at transition, never in polling loop)
        ApplyGpuClockLimit(gpuClockMhz);
    }

    private void ApplyGpuClockLimit(int gpuClockMhz) {
        ThreadPool.QueueUserWorkItem(delegate(object state) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "nvidia-smi.exe";
                if (gpuClockMhz > 0) {
                    psi.Arguments = string.Format("-lgc 300,{0}", gpuClockMhz);
                } else {
                    psi.Arguments = "-rgc";
                }
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi)) {
                    if (p != null) {
                        p.WaitForExit(2000);
                    }
                }
            } catch { }
        });
    }

    // ---------------------------------------------------------------------------------------------
    // AUTOMATED BENCHMARK ENGINE WITH OUTLIER ELIMINATION
    // ---------------------------------------------------------------------------------------------
    private class BenchmarkProfileConfig {
        public string Id;
        public string Name;
        public int PCoreMhz;
        public int ECoreMhz;
        public int BoostMode;
        public int Epp;
        public int GpuClockMhz;

        public BenchmarkProfileConfig(string id, string name, int pcore, int ecore, int boost, int epp, int gpu) {
            Id = id;
            Name = name;
            PCoreMhz = pcore;
            ECoreMhz = ecore;
            BoostMode = boost;
            Epp = epp;
            GpuClockMhz = gpu;
        }
    }

    public void CancelBenchmark() {
        _benchmarkCancelRequested = true;
    }

    public void RunBenchmark(
        int iterationsPerProfile,
        int warmupSeconds,
        bool filterOutliers,
        Action<BenchmarkProgress> onProgress,
        Action<List<BenchmarkResult>> onComplete
    ) {
        if (_isBenchmarking) {
            return;
        }

        _isBenchmarking = true;
        _benchmarkCancelRequested = false;

        Thread benchThread = new Thread(delegate() {
            string savedProfile = ActiveProfile;
            List<BenchmarkResult> allResults = new List<BenchmarkResult>();

            // Benchmark Suite Profiles
            List<BenchmarkProfileConfig> suite = new List<BenchmarkProfileConfig>();
            suite.Add(new BenchmarkProfileConfig("snappy", "Snappy-Pacing (Mode 4, EPP 30%, Uncapped)", 0, 0, 4, 30, 0));
            suite.Add(new BenchmarkProfileConfig("clamped", "Clamped 4.9 GHz (Mode 4, EPP 25%, P-Core 4900, E-Core 2800)", 4900, 2800, 4, 25, 0));
            suite.Add(new BenchmarkProfileConfig("cold", "Cold & Quiet / GPU-Shift (Mode 3, EPP 20%, GPU 2100 MHz)", 0, 0, 3, 20, 2100));
            suite.Add(new BenchmarkProfileConfig("guaranteed", "Guaranteed Curve (Mode 6, EPP 25%, Uncapped)", 0, 0, 6, 25, 0));

            try {
                for (int pIdx = 0; pIdx < suite.Count; pIdx++) {
                    if (_benchmarkCancelRequested) break;

                    BenchmarkProfileConfig cfg = suite[pIdx];
                    BenchmarkResult result = new BenchmarkResult();
                    result.ProfileId = cfg.Id;
                    result.ProfileName = cfg.Name;

                    // 1. Apply Profile Under Test
                    ApplyProfile(cfg.Id);

                    // 2. Warmup Phase (Clocks & Temperatures Stabilization)
                    for (int w = warmupSeconds; w > 0; w--) {
                        if (_benchmarkCancelRequested) break;

                        if (onProgress != null) {
                            BenchmarkProgress prog = new BenchmarkProgress();
                            prog.CurrentProfileName = cfg.Name;
                            prog.CurrentProfileIndex = pIdx + 1;
                            prog.TotalProfiles = suite.Count;
                            prog.CurrentIteration = 0;
                            prog.TotalIterations = iterationsPerProfile;
                            prog.WarmupRemainingSeconds = w;
                            prog.IsWarmingUp = true;
                            prog.StatusMessage = string.Format("Stabilizing clocks: {0} ({1}s left)...", cfg.Name, w);
                            prog.CurrentFps = _currentFps;
                            prog.CurrentCpuPowerW = _currentSnapshot.CpuPowerW;
                            prog.CurrentGpuPowerW = _currentSnapshot.GpuPowerW;
                            try { onProgress(prog); } catch { }
                        }
                        Thread.Sleep(1000);
                    }

                    if (_benchmarkCancelRequested) break;

                    // 3. Sampling Iteration Phase
                    for (int iter = 1; iter <= iterationsPerProfile; iter++) {
                        if (_benchmarkCancelRequested) break;

                        Thread.Sleep(1000);

                        TelemetrySnapshot snap = CurrentSnapshot;
                        BenchmarkSample sample = new BenchmarkSample();
                        sample.Fps = snap.Fps;
                        sample.CpuPowerW = snap.CpuPowerW;
                        sample.GpuPowerW = snap.GpuPowerW;
                        sample.TotalPowerW = snap.TotalPlatformPowerW;
                        sample.PCoreGhz = snap.PCoreGhz;
                        sample.ECoreGhz = snap.ECoreGhz;
                        sample.GpuTempC = snap.GpuTempC;
                        sample.GpuClockMhz = snap.GpuClockMhz;
                        sample.GpuUtilPct = snap.GpuUtilPct;

                        // Outlier Detection 1: Powercut / DC Battery Detection
                        bool isAcPower = IsAcPowerConnected();
                        sample.IsAcPower = isAcPower;
                        if (!isAcPower) {
                            sample.IsOutlier = true;
                            sample.OutlierReason = "Powercut / Running on Battery (DC throttled)";
                        }

                        // Outlier Detection 2: Loading Screen Freeze Detection
                        // If FPS < 20 and GPU Utilization < 15%, game is undergoing level transition/freeze
                        if (sample.Fps < 20.0 && sample.GpuUtilPct < 15) {
                            sample.IsLoadingScreen = true;
                            sample.IsOutlier = true;
                            sample.OutlierReason = string.IsNullOrEmpty(sample.OutlierReason)
                                ? "Loading Screen Freeze (FPS < 20, GPU < 15%)"
                                : sample.OutlierReason + " | Loading Screen Freeze";
                        }

                        result.Samples.Add(sample);

                        if (onProgress != null) {
                            BenchmarkProgress prog = new BenchmarkProgress();
                            prog.CurrentProfileName = cfg.Name;
                            prog.CurrentProfileIndex = pIdx + 1;
                            prog.TotalProfiles = suite.Count;
                            prog.CurrentIteration = iter;
                            prog.TotalIterations = iterationsPerProfile;
                            prog.WarmupRemainingSeconds = 0;
                            prog.IsWarmingUp = false;
                            string outlierNotice = sample.IsOutlier ? " [OUTLIER EXCLUDED]" : "";
                            prog.StatusMessage = string.Format("Sampling #{0}/{1}: {2:F1} FPS | CPU: {3:F1}W | GPU: {4:F1}W{5}",
                                iter, iterationsPerProfile, sample.Fps, sample.CpuPowerW, sample.GpuPowerW, outlierNotice);
                            prog.CurrentFps = sample.Fps;
                            prog.CurrentCpuPowerW = sample.CpuPowerW;
                            prog.CurrentGpuPowerW = sample.GpuPowerW;
                            try { onProgress(prog); } catch { }
                        }
                    }

                    // 4. Compute Raw and Cleaned Aggregates
                    ComputeBenchmarkMetrics(result, filterOutliers);
                    allResults.Add(result);
                }
            } finally {
                // Restore previous active profile and ensure GPU clocks are reset to stock
                ApplyProfile(savedProfile);
                ApplyGpuClockLimit(0);

                _isBenchmarking = false;
                _benchmarkCancelRequested = false;

                if (onComplete != null) {
                    try { onComplete(allResults); } catch { }
                }
            }
        });

        benchThread.IsBackground = true;
        benchThread.Name = "PowerCoreEngine_BenchmarkRunner";
        benchThread.Start();
    }

    private static void ComputeBenchmarkMetrics(BenchmarkResult res, bool filterOutliers) {
        if (res.Samples.Count == 0) return;

        // Raw metrics
        double rawFpsSum = 0;
        double rawCpuSum = 0;
        double rawGpuSum = 0;
        double rawTotSum = 0;
        double rawMinFps = double.MaxValue;
        List<double> rawFpsList = new List<double>();

        for (int i = 0; i < res.Samples.Count; i++) {
            BenchmarkSample s = res.Samples[i];
            rawFpsSum += s.Fps;
            rawCpuSum += s.CpuPowerW;
            rawGpuSum += s.GpuPowerW;
            rawTotSum += s.TotalPowerW;
            rawFpsList.Add(s.Fps);
            if (s.Fps < rawMinFps) rawMinFps = s.Fps;
        }

        res.RawSampleCount = res.Samples.Count;
        res.RawAvgFps = rawFpsSum / res.Samples.Count;
        res.RawMinFps = (rawMinFps == double.MaxValue) ? 0.0 : rawMinFps;
        res.Raw1PercentLowFps = Calculate1PercentLow(rawFpsList);
        res.RawAvgCpuPowerW = rawCpuSum / res.Samples.Count;
        res.RawAvgGpuPowerW = rawGpuSum / res.Samples.Count;
        res.RawAvgTotalPowerW = rawTotSum / res.Samples.Count;

        // Cleaned metrics
        List<BenchmarkSample> validSamples = new List<BenchmarkSample>();
        for (int i = 0; i < res.Samples.Count; i++) {
            if (!filterOutliers || !res.Samples[i].IsOutlier) {
                validSamples.Add(res.Samples[i]);
            }
        }

        res.CleanedSampleCount = validSamples.Count;
        res.DiscardedSampleCount = res.Samples.Count - validSamples.Count;

        if (validSamples.Count > 0) {
            double cleanFpsSum = 0;
            double cleanCpuSum = 0;
            double cleanGpuSum = 0;
            double cleanTotSum = 0;
            double cleanMinFps = double.MaxValue;
            List<double> cleanFpsList = new List<double>();

            for (int i = 0; i < validSamples.Count; i++) {
                BenchmarkSample s = validSamples[i];
                cleanFpsSum += s.Fps;
                cleanCpuSum += s.CpuPowerW;
                cleanGpuSum += s.GpuPowerW;
                cleanTotSum += s.TotalPowerW;
                cleanFpsList.Add(s.Fps);
                if (s.Fps < cleanMinFps) cleanMinFps = s.Fps;
            }

            res.CleanedAvgFps = cleanFpsSum / validSamples.Count;
            res.CleanedMinFps = (cleanMinFps == double.MaxValue) ? 0.0 : cleanMinFps;
            res.Cleaned1PercentLowFps = Calculate1PercentLow(cleanFpsList);
            res.CleanedAvgCpuPowerW = cleanCpuSum / validSamples.Count;
            res.CleanedAvgGpuPowerW = cleanGpuSum / validSamples.Count;
            res.CleanedAvgTotalPowerW = cleanTotSum / validSamples.Count;
        } else {
            // Fallback to raw if all samples were discarded
            res.CleanedAvgFps = res.RawAvgFps;
            res.CleanedMinFps = res.RawMinFps;
            res.Cleaned1PercentLowFps = res.Raw1PercentLowFps;
            res.CleanedAvgCpuPowerW = res.RawAvgCpuPowerW;
            res.CleanedAvgGpuPowerW = res.RawAvgGpuPowerW;
            res.CleanedAvgTotalPowerW = res.RawAvgTotalPowerW;
        }
    }

    private static double Calculate1PercentLow(List<double> fpsValues) {
        if (fpsValues == null || fpsValues.Count == 0) return 0.0;
        List<double> sorted = new List<double>(fpsValues);
        sorted.Sort();

        // 1% lowest samples count (at least 1 sample)
        int lowCount = (int)Math.Ceiling(sorted.Count * 0.01);
        if (lowCount < 1) lowCount = 1;
        if (lowCount > sorted.Count) lowCount = sorted.Count;

        double sum = 0.0;
        for (int i = 0; i < lowCount; i++) {
            sum += sorted[i];
        }
        return sum / lowCount;
    }

    private static bool IsAcPowerConnected() {
        try {
            Win32Native.SYSTEM_POWER_STATUS status;
            if (Win32Native.GetSystemPowerStatus(out status)) {
                // ACLineStatus: 0 = Offline, 1 = Online, 255 = Unknown
                return status.ACLineStatus == 1;
            }
        } catch { }
        return true;
    }

    // ---------------------------------------------------------------------------------------------
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

        // 3. State Machine Transition & Profile Enforcement
        if (!_isBenchmarking) {
            if (detectedGamePid > 0) {
                _gameModeExitTimer = 0.0;
                _currentFps = detectedFps;
                _currentGamePid = detectedGamePid;
                _currentGameName = detectedGameName;

                if (!_isGameMode) {
                    _isGameMode = true;
                    ApplyProfileInternal(_selectedGamingProfile);
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

                        ApplyProfileInternal("desktop");
                        ShutdownNvml();
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

    // ---------------------------------------------------------------------------------------------
    // CPU TELEMETRY (PDH P/INVOKE)
    // ---------------------------------------------------------------------------------------------
    private void InitPdh() {
        try {
            if (_hPdhQuery != IntPtr.Zero) {
                PdhNative.PdhCloseQuery(_hPdhQuery);
                _hPdhQuery = IntPtr.Zero;
            }

            uint res = PdhNative.PdhOpenQueryW(null, IntPtr.Zero, out _hPdhQuery);
            if (res == 0) {
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Energy Meter(RAPL_Package0_PKG)\Power", IntPtr.Zero, out _hPdhCounterPwr);
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Processor Information(0,0)\% Processor Performance", IntPtr.Zero, out _hPdhCounterPCore);
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Processor Information(0,14)\% Processor Performance", IntPtr.Zero, out _hPdhCounterECore);

                _hPdhCounterPerCore = new IntPtr[24];
                _hPdhCounterPerCoreUtil = new IntPtr[24];
                for (int i = 0; i < 24; i++) {
                    PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Performance", i), IntPtr.Zero, out _hPdhCounterPerCore[i]);
                    PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Utility", i), IntPtr.Zero, out _hPdhCounterPerCoreUtil[i]);
                }

                // Initial baseline sample
                PdhNative.PdhCollectQueryData(_hPdhQuery);
                _isPdhInitialized = true;
            }
        } catch {
            _isPdhInitialized = false;
        }
    }

    private void ReadCpuTelemetry(out double cpuWatts, out double pCoreGhz, out double eCoreGhz, out double[] perCoreGhz, out double[] perCoreUtil) {
        cpuWatts = 0.0;
        pCoreGhz = 0.0;
        eCoreGhz = 0.0;
        perCoreGhz = new double[24];
        perCoreUtil = new double[24];

        if (!_isPdhInitialized || _hPdhQuery == IntPtr.Zero) {
            InitPdh();
            return;
        }

        try {
            if (PdhNative.PdhCollectQueryData(_hPdhQuery) == 0) {
                if (_hPdhCounterPwr != IntPtr.Zero) {
                    PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                    if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPwr, PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        cpuWatts = val.doubleValue / 1000.0; // Counter reports milliwatts -> Watts
                        if (cpuWatts < 0.0) cpuWatts = 0.0;
                    }
                }

                double pSum = 0; int pCount = 0;
                double eSum = 0; int eCount = 0;

                for (int i = 0; i < 24; i++) {
                    if (_hPdhCounterPerCore != null && _hPdhCounterPerCore[i] != IntPtr.Zero) {
                        PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCore[i], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                            double nominal = (i < 8) ? 2.7 : 2.1;
                            double ghz = (val.doubleValue / 100.0) * nominal;
                            if (ghz < 0.0) ghz = 0.0;
                            perCoreGhz[i] = ghz;
                            if (i < 8) { pSum += ghz; pCount++; }
                            else { eSum += ghz; eCount++; }
                        }
                    }

                    if (_hPdhCounterPerCoreUtil != null && _hPdhCounterPerCoreUtil[i] != IntPtr.Zero) {
                        PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCoreUtil[i], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                            double u = val.doubleValue;
                            if (u < 0.0) u = 0.0;
                            if (u > 100.0) u = 100.0;
                            perCoreUtil[i] = u;
                        }
                    }
                }

                pCoreGhz = (pCount > 0) ? (pSum / pCount) : 0.0;
                eCoreGhz = (eCount > 0) ? (eSum / eCount) : 0.0;
            }
        } catch { }
    }

    private void ClosePdh() {
        if (_hPdhQuery != IntPtr.Zero) {
            try {
                PdhNative.PdhCloseQuery(_hPdhQuery);
            } catch { }
            _hPdhQuery = IntPtr.Zero;
            _isPdhInitialized = false;
        }
    }

    // ---------------------------------------------------------------------------------------------
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
    // PROCESS INSPECTION & GAME FILTERING
    // ---------------------------------------------------------------------------------------------
    private bool IsGameProcess(int pid, out string friendlyName) {
        friendlyName = "";
        if (pid <= 4) return false;

        try {
            using (Process p = Process.GetProcessById(pid)) {
                string procName = p.ProcessName;
                if (EXCLUDE_NAMES.Contains(procName)) return false;

                string fullPath = GetProcessPath(pid);
                string lowerPath = (!string.IsNullOrEmpty(fullPath)) ? fullPath.ToLowerInvariant() : "";

                // Disregard system and Windows background directories
                if (!string.IsNullOrEmpty(lowerPath)) {
                    if (lowerPath.Contains(@"\windows\system32\") ||
                        lowerPath.Contains(@"\windows\syswow64\") ||
                        lowerPath.Contains(@"\windows\systemapps\") ||
                        lowerPath.Contains(@"\windows\microsoft.net\")) {
                        return false;
                    }
                }

                bool isGame = false;

                // 1. Instant check against Windows GameConfigStore cache
                lock (_syncLock) {
                    if (_knownGameExes.Contains(procName) || _knownGameExes.Contains(procName + ".exe")) {
                        isGame = true;
                    } else if (!string.IsNullOrEmpty(fullPath) && (_knownGamePaths.Contains(fullPath) || _knownGamePaths.Contains(lowerPath))) {
                        isGame = true;
                    } else if (!string.IsNullOrEmpty(fullPath)) {
                        string exeName = Path.GetFileName(fullPath);
                        if (!string.IsNullOrEmpty(exeName) && _knownGameExes.Contains(exeName)) {
                            isGame = true;
                        }
                    }
                }

                // 2. Check standard gaming root directories & launchers
                if (!isGame && !string.IsNullOrEmpty(lowerPath)) {
                    for (int i = 0; i < GAME_PATH_HINTS.Length; i++) {
                        if (lowerPath.Contains(GAME_PATH_HINTS[i])) {
                            isGame = true;
                            break;
                        }
                    }
                }

                // 3. Unreal Engine & Unity markers
                if (!isGame) {
                    if (procName.EndsWith("-Win64-Shipping", StringComparison.OrdinalIgnoreCase) ||
                        procName.EndsWith("-Win32-Shipping", StringComparison.OrdinalIgnoreCase)) {
                        isGame = true;
                    } else if (!string.IsNullOrEmpty(fullPath)) {
                        try {
                            string dir = Path.GetDirectoryName(fullPath);
                            if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "UnityPlayer.dll"))) {
                                isGame = true;
                            }
                        } catch { }
                    }
                }

                if (!isGame) return false;

                // Extract friendly game title from Steam directory structure if present
                if (!string.IsNullOrEmpty(fullPath) && lowerPath.Contains(@"steamapps\common\")) {
                    int idx = lowerPath.IndexOf(@"steamapps\common\") + @"steamapps\common\".Length;
                    string sub = fullPath.Substring(idx);
                    int slash = sub.IndexOf('\\');
                    if (slash > 0) {
                        friendlyName = sub.Substring(0, slash);
                    }
                }

                if (string.IsNullOrEmpty(friendlyName)) {
                    try {
                        string desc = p.MainModule.FileVersionInfo.FileDescription;
                        if (!string.IsNullOrEmpty(desc) && desc.Length > 2 && !desc.Equals(procName, StringComparison.OrdinalIgnoreCase)) {
                            friendlyName = desc;
                        }
                    } catch { }
                }

                if (string.IsNullOrEmpty(friendlyName)) {
                    friendlyName = procName;
                }

                return true;
            }
        } catch {
            return false;
        }
    }

    private static string GetProcessPath(int pid) {
        IntPtr h = Win32Native.OpenProcess(Win32Native.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (h == IntPtr.Zero) return "";
        try {
            uint size = 1024;
            StringBuilder sb = new StringBuilder((int)size);
            if (Win32Native.QueryFullProcessImageNameW(h, 0, sb, ref size)) {
                return sb.ToString();
            }
        } finally {
            Win32Native.CloseHandle(h);
        }
        return "";
    }

    // ---------------------------------------------------------------------------------------------
    // NATIVE INTEROP STRUCTS & IMPORTS
    // ---------------------------------------------------------------------------------------------
    private static class Win32Native {
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        public const int DISPLAY_DEVICE_ATTACHED_TO_DESKTOP = 0x00000001;

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
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
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
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 88)]
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

#if POWER_CORE_EXE
class Program {
    static void Main(string[] args) {
        Console.WriteLine("==================================================================");
        Console.WriteLine(" PowerCoreEngine Telemetry & Profile Management Engine (C# .NET)");
        Console.WriteLine("==================================================================");
        PowerCoreEngine engine = PowerCoreEngine.Instance;
        engine.OnTelemetryUpdated += delegate(object sender, TelemetrySnapshot snap) {
            Console.WriteLine(string.Format(
                "[{0:HH:mm:ss}] Game: {1} (PID: {2}) | FPS: {3:F1} | CPU: {4:F1}W (P:{5:F2}G E:{6:F2}G) | GPU: {7:F1}W ({8}MHz {9}C {10}%) [{11}] | Tot: {12:F1}W | Prof: {13}",
                DateTime.Now,
                snap.IsGameMode ? snap.ActiveGameName : "No Game",
                snap.ActiveGamePid,
                snap.Fps,
                snap.CpuPowerW,
                snap.PCoreGhz,
                snap.ECoreGhz,
                snap.GpuPowerW,
                snap.GpuClockMhz,
                snap.GpuTempC,
                snap.GpuUtilPct,
                snap.GpuStatus,
                snap.TotalPlatformPowerW,
                engine.ActiveProfile
            ));
        };

        engine.Start();
        Console.WriteLine("Engine started. Listening to DXGI ETW, PDH, NVML, and Modern Standby Power Events.");
        if (args.Length > 0 && args[0].ToLower() == "-test") {
            Console.WriteLine("Running 3-second self test...");
            Thread.Sleep(3000);
        } else if (args.Length > 0 && args[0].ToLower() == "-bench") {
            Console.WriteLine("Starting benchmark test: 1 iteration per profile, 1s warmup...");
            engine.RunBenchmark(1, 1, true,
                delegate(BenchmarkProgress prog) {
                    Console.WriteLine(string.Format("[BENCH PROGRESS] Profile {0}/{1} ({2}): {3}",
                        prog.CurrentProfileIndex, prog.TotalProfiles, prog.CurrentProfileName, prog.StatusMessage));
                },
                delegate(List<BenchmarkResult> results) {
                    Console.WriteLine("\n[BENCHMARK COMPLETE] Summary:");
                    for (int i = 0; i < results.Count; i++) {
                        BenchmarkResult r = results[i];
                        Console.WriteLine(string.Format("  {0}: Cleaned FPS: {1:F1} (1% Low: {2:F1}) | CPU: {3:F1}W | GPU: {4:F1}W | Samples: {5} (Discarded: {6})",
                            r.ProfileName, r.CleanedAvgFps, r.Cleaned1PercentLowFps, r.CleanedAvgCpuPowerW, r.CleanedAvgGpuPowerW, r.CleanedSampleCount, r.DiscardedSampleCount));
                    }
                }
            );
            while (engine.IsBenchmarking) {
                Thread.Sleep(500);
            }
        } else {
            Console.WriteLine("Press Enter or Ctrl+C to stop...");
            Console.ReadLine();
        }
        engine.Stop();
        Console.WriteLine("Engine stopped cleanly.");
    }
}
#endif
