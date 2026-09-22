using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // INTERNAL STATE & SYNCHRONIZATION
    // ---------------------------------------------------------------------------------------------
    private readonly object _syncLock = new object();
    private readonly ManualResetEvent _stopEvent = new ManualResetEvent(false);
    private readonly AutoResetEvent _wakeLoopEvent = new AutoResetEvent(false);
    private Thread _workerThread;

    private volatile bool _isGameMode;
    private volatile bool _isTrayMinimized;
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
    private DateTime _lastEtwAttempt = DateTime.MinValue;
    private int _currentHubPid;
    private int _dwmPid;
    private const ulong INVALID_PROCESSTRACE_HANDLE = 0xFFFFFFFFFFFFFFFF;

    // Frame & Presentation Tracking
    private readonly Dictionary<int, DateTime> _lastPresentMap = new Dictionary<int, DateTime>();
    private readonly Dictionary<int, int> _frameCounterMap = new Dictionary<int, int>();
    private readonly Dictionary<int, long> _firstPresentTsMap = new Dictionary<int, long>();
    private readonly Dictionary<int, long> _lastPresentTsMap = new Dictionary<int, long>();
    private readonly Dictionary<int, long> _prevFlushTsMap = new Dictionary<int, long>();
    private readonly Dictionary<int, double> _pidFpsMap = new Dictionary<int, double>();
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
    private IntPtr[] _hPdhCounterPerCoreParked = new IntPtr[24];
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
        "msedgewebview2", "WebViewHost", "chrome_crashpad_handler", "crashpad_handler",
        "CrashReportClient", "CrashReportClientEditor", "UnrealCEFSubProcess", "EpicWebHelper",
        "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService", "BattlEye", "UnityCrashHandler64",
        "UnityCrashHandler32", "EOSBootStrapper", "vcredist", "dotnet",
        "stremio", "stremio-runtime", "vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64",
        "potplayer", "potplayermini", "potplayermini64", "kodi", "plex", "plexmediaplayer"
    };

    private static readonly string[] GAME_PATH_HINTS = new string[] {
        @"steamapps\common\", "steamapps", "steamlibrary",
        "xboxgames", "epic games", "riot games",
        "ubisoft game launcher", "ubisoft",
        @"gog galaxy\games\", "gog galaxy",
        "battle.net", "origin games", "ea games",
        @"windowsapps\", @"\games\",
        @"binaries\win64", @"binaries\win32", "binaries/win64", "binaries/win32"
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
        try { _currentHubPid = Process.GetCurrentProcess().Id; } catch { _currentHubPid = 0; }

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

        // Load saved user preferences from settings.json
        LoadUserSettings();
    }

    // ---------------------------------------------------------------------------------------------

}
