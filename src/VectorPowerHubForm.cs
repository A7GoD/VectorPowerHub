using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {

        // Core Engine & Bridge
        private PowerCoreBridge bridge;
        private System.Windows.Forms.Timer telemetryTimer;
        public HubTelemetrySnapshot currentSnapshot = new HubTelemetrySnapshot();

        // System Specs
        public static string sysModel = "PC System";
        public static string sysCpu = "Intel Processor";
        public static string sysGpu = "NVIDIA GPU";

        // System Tray
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private MenuItem trayMenuSnappy;
        private MenuItem trayMenuEfficiency;
        private MenuItem trayMenuCold;
        private MenuItem trayMenuGuaranteed;
        private MenuItem trayMenuCustom;
        private MenuItem trayMenuBenchmark;

        // Single-Instance Synchronization
        private EventWaitHandle singleInstanceWakeEvent;
        private Thread wakeListenerThread;
        private volatile bool isDisposingOrClosed;

        // Custom UI Components - Title Bar
        private Panel panelTitle;
        private Label lblTitle;
        private Label lblSubtitle;
        private Label lblProfileBadge;
        private Button btnTrayMin;
        private Button btnMin;
        private Button btnMax;
        private Button btnClose;
        private Button btnTrends;

        // HUD Cards (Self-Painting Controls - Monospace Consolas figures)
        private FpsHeroCard cardFps;
        private PlatformPowerCard cardPlatformPower;
        private CpuTelemetryCard cardCpu;
        private GpuTelemetryCard cardGpu;

        // Tab Navigation Strip
        private Panel panelTabStrip;
        private GlowButton btnTabProfiles;
        private GlowButton btnTabBenchmark;
        private GlowButton btnTabTopology;
        private GlowButton btnTabOptimizations;
        private GlowButton btnTabTrends;
        private GlowButton btnToggleAutoSwitch;
        private int currentTabIndex = 0; // 0 = Profiles, 1 = Benchmark, 2 = Per-Core Topology

        // View 1: Profile Selection Cards
        private Panel panelProfilesView;
        private ProfileCard cardProfileSnappy;
        private ProfileCard cardProfileEfficiency;
        private ProfileCard cardProfileCold;
        private ProfileCard cardProfileGuaranteed;

        // Custom Tuner Overlay (Global shortcut)
        private Panel panelCustomTuner;
        private Label lblTunerTitle;
        private Label lblPcoreVal;
        private TrackBar trackPcore;
        private Label lblEcoreVal;
        private TrackBar trackEcore;
        private Label lblEppVal;
        private TrackBar trackEpp;
        private Label lblBoostModeVal;
        private ComboBox comboBoostMode;
        private Label lblGpuClockLimitVal;
        private TrackBar trackGpuClock;
        private GlowButton btnApplyCustom;
        private GlowButton btnCloseTuner;

        // View 2: Automated Benchmark Panel
        private Panel panelBenchmarkView;
        private Label lblBenchHeader;
        private Label lblBenchSub;
        private Label lblBenchDuration;
        private ComboBox comboBenchSamples;
        private CheckBox chkFilterOutliers;
        private GlowButton btnStartBenchmark;
        private GlowButton btnStopBenchmark;
        private Label lblBenchStatus;
        private BenchmarkProgressBar benchProgressBar;
        private Label lblOutlierAlertPill;
        private BenchmarkResultsGrid benchResultsGrid;
        private GlowButton btnApplyWinningProfile;

        // View 3: Per-Core Topology Panel & Integrated On-Demand Hardware Tuner
        private Panel panelTopologyView;
        private GlowButton btnToggleTopologyTuning;
        private Panel panelTopologyTuningDrawer;
        private Label lblDrawerPcoreVal;
        private TrackBar trackDrawerPcore;
        private Label lblDrawerEcoreVal;
        private TrackBar trackDrawerEcore;
        private Label lblDrawerEppVal;
        private TrackBar trackDrawerEpp;
        private Label lblDrawerBoostVal;
        private ComboBox comboDrawerBoost;
        private Label lblDrawerGpuVal;
        private TrackBar trackDrawerGpu;
        private GlowButton btnDrawerApply;
        private PerCoreTopologyControl topologyControl;

        // Benchmark Runner State
        private Thread benchmarkThread = null;
        private volatile bool isBenchmarkRunning = false;
        private volatile bool cancelBenchmarkRequested = false;
        public List<BenchmarkResultInfo> lastBenchmarkResults = new List<BenchmarkResultInfo>();

        // Bottom Footer
        private Panel panelFooter;
        private Label lblFooterStatus;
        private GlowButton btnOpenTuner;
        private GlowButton btnFooterTray;
        private GlowButton btnFooterExit;

        // Active Selected Profile & Game Automation Trackers
        public string currentSelectedProfile = "desktop";
        public string currentSelectedGamingProfile = "snappy";
        public string currentSelectedDesktopProfile = "desktop";
        public bool isAutoProfileSwitchingEnabled = true;
        private bool lastObservedGameMode = false;
        private MenuItem trayMenuAutoSwitch;
        private MenuItem trayMenuDesktopBalanced;
        private MenuItem trayMenuDesktopSilent;
        private MenuItem trayMenuDesktopCold;
        private MenuItem trayMenuDesktopGuaranteed;
        private MenuItem trayMenuStartup;
        private CheckBox chkRunAtStartup;
        private Label lblCustomTunerHint;
        private bool startMinimizedToTray = false;
        private bool hasShownOnce = false;

        private VectorPowerHubGraphForm graphForm = null;
        private VectorPowerHubOsOptimizationsForm optimForm = null;

        // -----------------------------------------------------------------------------------------

    }
}
