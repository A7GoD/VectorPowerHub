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

namespace VectorPowerHub {
    // -----------------------------------------------------------------------------------------
    // TELEMETRY & BENCHMARK DATA STRUCTURES
    // -----------------------------------------------------------------------------------------
    public class HubTelemetrySnapshot {
        public double Fps = 0.0;
        public double CpuPowerW = 0.0;
        public double PCoreGhz = 0.0;
        public double ECoreGhz = 0.0;
        public double[] PerCoreGhz = new double[24];
        public double[] PerCoreUtil = new double[24];
        public double GpuPowerW = 0.0;
        public int GpuClockMhz = 0;
        public int GpuTempC = 0;
        public int GpuUtilPct = 0;
        public bool IsGameMode = false;
        public string ActiveGameName = "";
        public int ActiveGamePid = 0;
        public string GpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";
        public string ActiveProfile = "snappy";
        public bool IsNvidiaDisplayAttached = false;
        public string NvidiaMonitorName = "";

        public double TotalPlatformPowerW {
            get { return CpuPowerW + GpuPowerW; }
        }
    }

    public class BenchmarkProgressInfo {
        public string ProfileName = "";
        public string Phase = "Warmup";
        public int CurrentSample = 0;
        public int TotalSamples = 10;
        public int OverallPercent = 0;
        public double CurrentFps = 0.0;
        public double CurrentCpuW = 0.0;
        public double CurrentGpuW = 0.0;
        public bool IsOutlier = false;
        public string OutlierReason = "";
    }

    public class BenchmarkResultInfo {
        public string ProfileId = "";
        public string ProfileName = "";
        public double RawAvgFps = 0.0;
        public double RawOnePercentLow = 0.0;
        public double CleanedAvgFps = 0.0;
        public double CleanedOnePercentLow = 0.0;
        public double AvgCpuPowerW = 0.0;
        public double AvgGpuPowerW = 0.0;
        public double AvgTotalPowerW = 0.0;
        public int OutliersFilteredCount = 0;
        public string OutlierDetails = "None";
        public double EfficiencyScore = 0.0;
        public bool IsWinner = false;
    }

    // -----------------------------------------------------------------------------------------
    // MAIN GUI WINDOW: VectorPowerHubForm
    // -----------------------------------------------------------------------------------------
    public class VectorPowerHubForm : Form {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int HWND_BROADCAST = 0xffff;
        private const int SW_RESTORE = 9;
        public static readonly int WM_SHOW_HUB = RegisterWindowMessage("VECTOR_POWER_HUB_SHOW_WINDOW");

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int CS_DROPSHADOW = 0x00020000;

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        // Color Palette (DESIGN.md Design Tokens)
        public static readonly Color ColorBgMain          = Color.FromArgb(18, 19, 24);    // #121318 Slate background
        public static readonly Color ColorCardBg          = Color.FromArgb(27, 30, 38);    // #1B1E26 Elevated card surface
        public static readonly Color ColorCardHover       = Color.FromArgb(35, 39, 50);    // #232732
        public static readonly Color ColorCardSelected    = Color.FromArgb(24, 34, 48);    // #182230
        public static readonly Color ColorBorder          = Color.FromArgb(45, 50, 66);    // #2D3242 1px card border
        public static readonly Color ColorBorderHighlight = Color.FromArgb(70, 80, 105);
        public static readonly Color ColorAccentCyan      = Color.FromArgb(0, 242, 255);   // #00F2FF Electric Cyan (FPS & GPU)
        public static readonly Color ColorAccentGold      = Color.FromArgb(255, 159, 0);   // #FF9F00 Amber Gold (CPU)
        public static readonly Color ColorAccentPurple    = Color.FromArgb(168, 85, 247);  // #A855F7 Royal Purple (Platform balance)
        public static readonly Color ColorAccentGreen     = Color.FromArgb(0, 230, 118);   // #00E676 Emerald Green (Active profile & D3cold)
        public static readonly Color ColorAccentRed       = Color.FromArgb(255, 82, 82);   // #FF5252 Coral Red (215W ceiling alert & outliers)
        public static readonly Color ColorAccentBlue      = Color.FromArgb(56, 189, 248);  // #38BDF8
        public static readonly Color ColorTextWhite       = Color.FromArgb(255, 255, 255); // #FFFFFF Primary text
        public static readonly Color ColorTextMuted       = Color.FromArgb(143, 156, 174); // #8F9CAE Secondary text
        public static readonly Color ColorTextDim         = Color.FromArgb(90, 101, 120);  // #5A6578 Muted/Dim text
        public static readonly Color ColorTabInactiveBg   = Color.FromArgb(22, 24, 32);    // #161820
        public static readonly Color ColorTabActiveBg     = Color.FromArgb(31, 35, 45);    // #1F232D

        // Core Engine & Bridge
        private PowerCoreBridge bridge;
        private System.Windows.Forms.Timer telemetryTimer;
        public HubTelemetrySnapshot currentSnapshot = new HubTelemetrySnapshot();

        // System Tray
        private NotifyIcon trayIcon;
        private ContextMenu trayMenu;
        private MenuItem trayMenuSnappy;
        private MenuItem trayMenuEfficiency;
        private MenuItem trayMenuCold;
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
        private int currentTabIndex = 0; // 0 = Profiles, 1 = Benchmark, 2 = Per-Core Topology

        // View 1: Profile Selection Cards
        private Panel panelProfilesView;
        private ProfileCard cardProfileSnappy;
        private ProfileCard cardProfileEfficiency;
        private ProfileCard cardProfileCold;

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

        // Active Selected Profile Tracker
        public string currentSelectedProfile = "snappy";

        // -----------------------------------------------------------------------------------------
        // CONSTRUCTOR
        // -----------------------------------------------------------------------------------------
        public VectorPowerHubForm(int initialTab = 0) {
            try { SetProcessDPIAware(); } catch { }

            this.Text = "Vector Power Hub - MSI Vector 16 HX";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1160, 800);
            this.MinimumSize = new Size(1080, 720);
            this.BackColor = ColorBgMain;
            this.ForeColor = ColorTextWhite;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            this.UpdateStyles();

            // Initialize Engine Bridge
            bridge = new PowerCoreBridge();

            // Build GUI Layout
            InitializeInterface();
            InitializeSystemTray();

            // Setup Single-Instance Wake Listener
            try {
                singleInstanceWakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "VectorPowerHub_WakeEvent");
                wakeListenerThread = new Thread(WakeListenerLoop);
                wakeListenerThread.IsBackground = true;
                wakeListenerThread.Name = "VectorPowerHub_WakeListener";
                wakeListenerThread.Start();
            } catch { }

            // Layout calculation on resize
            this.Resize += (s, e) => PerformResponsiveLayout();

            // Initialize Telemetry Polling Timer (750ms cadence)
            telemetryTimer = new System.Windows.Forms.Timer();
            telemetryTimer.Interval = 750;
            telemetryTimer.Tick += OnTelemetryTick;
            telemetryTimer.Start();

            // Initial telemetry sample
            OnTelemetryTick(null, EventArgs.Empty);

            // Initial responsive layout pass
            PerformResponsiveLayout();

            // Switch to requested initial tab if non-zero
            if (initialTab > 0) {
                SwitchTab(initialTab);
            }
        }

        // -----------------------------------------------------------------------------------------
        // BORDERLESS WINDOW RESIZING (WM_NCHITTEST) & SINGLE-INSTANCE WAKE (WM_SHOW_HUB)
        // -----------------------------------------------------------------------------------------
        protected override void WndProc(ref Message m) {
            if (WM_SHOW_HUB != 0 && m.Msg == WM_SHOW_HUB) {
                RestoreFromTray();
                return;
            }

            const int WM_NCHITTEST = 0x84;
            const int HTCLIENT = 1;
            const int HTLEFT = 10;
            const int HTRIGHT = 11;
            const int HTTOP = 12;
            const int HTTOPLEFT = 13;
            const int HTTOPRIGHT = 14;
            const int HTBOTTOM = 15;
            const int HTBOTTOMLEFT = 16;
            const int HTBOTTOMRIGHT = 17;

            if (m.Msg == WM_NCHITTEST) {
                base.WndProc(ref m);
                if ((int)m.Result == HTCLIENT && this.WindowState != FormWindowState.Maximized) {
                    Point p = this.PointToClient(new Point(m.LParam.ToInt32()));
                    int border = 8;
                    bool left = p.X <= border;
                    bool right = p.X >= this.ClientSize.Width - border;
                    bool top = p.Y <= border;
                    bool bottom = p.Y >= this.ClientSize.Height - border;

                    if (top && left) m.Result = (IntPtr)HTTOPLEFT;
                    else if (top && right) m.Result = (IntPtr)HTTOPRIGHT;
                    else if (bottom && left) m.Result = (IntPtr)HTBOTTOMLEFT;
                    else if (bottom && right) m.Result = (IntPtr)HTBOTTOMRIGHT;
                    else if (left) m.Result = (IntPtr)HTLEFT;
                    else if (right) m.Result = (IntPtr)HTRIGHT;
                    else if (top) m.Result = (IntPtr)HTTOP;
                    else if (bottom) m.Result = (IntPtr)HTBOTTOM;
                }
                return;
            }
            base.WndProc(ref m);
        }

        // -----------------------------------------------------------------------------------------
        // INTERFACE INITIALIZATION
        // -----------------------------------------------------------------------------------------
        private void InitializeInterface() {
            // =========================================================================
            // 1. TITLE BAR PANEL (Top 44px)
            // =========================================================================
            panelTitle = new Panel();
            panelTitle.Dock = DockStyle.Top;
            panelTitle.Height = 44;
            panelTitle.BackColor = Color.FromArgb(14, 15, 20);
            panelTitle.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };

            lblTitle = new Label();
            lblTitle.Text = "⚡ VECTOR POWER HUB";
            lblTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            lblTitle.ForeColor = ColorAccentCyan;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(14, 12);
            lblTitle.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); }
            };

            lblSubtitle = new Label();
            lblSubtitle.Text = "MSI VECTOR 16 HX • 275HX × RTX 5070 MOBILE";
            lblSubtitle.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            lblSubtitle.ForeColor = ColorTextDim;
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = new Point(230, 15);
            lblSubtitle.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); }
            };

            lblProfileBadge = new Label();
            lblProfileBadge.Text = "ACTIVE: SNAPPY-PACING";
            lblProfileBadge.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lblProfileBadge.ForeColor = ColorAccentGreen;
            lblProfileBadge.BackColor = Color.FromArgb(12, 38, 24);
            lblProfileBadge.Padding = new Padding(8, 3, 8, 3);
            lblProfileBadge.AutoSize = true;
            lblProfileBadge.Location = new Point(560, 11);

            btnTrayMin = CreateTitleButton("▼", 0, 8, (s, e) => MinimizeToTray());
            btnMin = CreateTitleButton("—", 0, 8, (s, e) => { this.WindowState = FormWindowState.Minimized; });
            btnMax = CreateTitleButton("◻", 0, 8, (s, e) => ToggleMaximize());
            btnClose = CreateTitleButton("✕", 0, 8, (s, e) => ExitApplication());
            btnClose.FlatAppearance.MouseOverBackColor = ColorAccentRed;

            panelTitle.Controls.Add(lblTitle);
            panelTitle.Controls.Add(lblSubtitle);
            panelTitle.Controls.Add(lblProfileBadge);
            panelTitle.Controls.Add(btnTrayMin);
            panelTitle.Controls.Add(btnMin);
            panelTitle.Controls.Add(btnMax);
            panelTitle.Controls.Add(btnClose);
            this.Controls.Add(panelTitle);

            // =========================================================================
            // 2. HERO HUD TOP SECTION (Self-Painting Controls, 126px)
            // =========================================================================
            cardFps = new FpsHeroCard();
            this.Controls.Add(cardFps);

            cardPlatformPower = new PlatformPowerCard();
            this.Controls.Add(cardPlatformPower);

            // =========================================================================
            // 3. TELEMETRY MID CARDS (CPU & GPU Live Metrics, 142px)
            // =========================================================================
            cardCpu = new CpuTelemetryCard();
            cardCpu.BtnView24Cores.Click += (s, e) => SwitchTab(2);
            this.Controls.Add(cardCpu);

            cardGpu = new GpuTelemetryCard();
            this.Controls.Add(cardGpu);

            // =========================================================================
            // 4. TAB NAVIGATION STRIP (Height: 38px)
            // =========================================================================
            panelTabStrip = new Panel();
            panelTabStrip.BackColor = ColorBgMain;
            panelTabStrip.Height = 38;

            btnTabProfiles = new GlowButton();
            btnTabProfiles.Text = "[⚡] PROFILES";
            btnTabProfiles.Location = new Point(0, 2);
            btnTabProfiles.Size = new Size(160, 36);
            btnTabProfiles.ButtonColor = ColorTabActiveBg;
            btnTabProfiles.BorderColor = ColorAccentCyan;
            btnTabProfiles.TextColor = ColorAccentCyan;
            btnTabProfiles.Click += (s, e) => SwitchTab(0);

            btnTabBenchmark = new GlowButton();
            btnTabBenchmark.Text = "[★] BENCHMARK";
            btnTabBenchmark.Location = new Point(170, 2);
            btnTabBenchmark.Size = new Size(170, 36);
            btnTabBenchmark.ButtonColor = ColorTabInactiveBg;
            btnTabBenchmark.BorderColor = ColorBorder;
            btnTabBenchmark.TextColor = ColorTextMuted;
            btnTabBenchmark.Click += (s, e) => SwitchTab(1);

            btnTabTopology = new GlowButton();
            btnTabTopology.Text = "[◆] 24-CORE TOPOLOGY";
            btnTabTopology.Location = new Point(350, 2);
            btnTabTopology.Size = new Size(230, 36);
            btnTabTopology.ButtonColor = ColorTabInactiveBg;
            btnTabTopology.BorderColor = ColorBorder;
            btnTabTopology.TextColor = ColorTextMuted;
            btnTabTopology.Click += (s, e) => SwitchTab(2);

            panelTabStrip.Controls.Add(btnTabProfiles);
            panelTabStrip.Controls.Add(btnTabBenchmark);
            panelTabStrip.Controls.Add(btnTabTopology);
            this.Controls.Add(panelTabStrip);

            // =========================================================================
            // 5. VIEW 1: PROFILE SELECTION CARDS
            // =========================================================================
            panelProfilesView = new Panel();
            panelProfilesView.BackColor = ColorBgMain;

            cardProfileSnappy = new ProfileCard(
                "⚡ Snappy-Pacing",
                "COMPETITIVE MAX FPS (DEFAULT)",
                ColorAccentCyan,
                new string[] {
                    "• Boost Mode 4 (Efficient Aggressive)",
                    "• Unbounded Freq (Up to 5.5 GHz Peak)",
                    "• EPP 30% (Eager GPU Power Release)",
                    "• Full 140W RTX 5070 Mobile Headroom",
                    "• Instant draw-call responsiveness",
                    "• Default competitive esports profile"
                },
                "ACTIVE",
                true
            );
            cardProfileSnappy.ProfileClicked += (s, e) => SelectProfile("snappy");

            cardProfileEfficiency = new ProfileCard(
                "✦ Sweet-Spot Efficiency",
                "CLAMPED 4.9 GHz (ZERO STARVATION)",
                ColorAccentGold,
                new string[] {
                    "• P-Core Clamped to 4.9 GHz (4900 MHz)",
                    "• E-Core Clamped to 2.8 GHz (2800 MHz)",
                    "• 58W CPU Ceiling (Zero Starvation)",
                    "• 100% Guaranteed 140W GPU Budget",
                    "• Rock-solid frame pacing & thermals",
                    "• Ideal for heavy AAA open-world titles"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileEfficiency.ProfileClicked += (s, e) => SelectProfile("clamped");

            cardProfileCold = new ProfileCard(
                "❄ Cold & Quiet",
                "GPU-SHIFT 2100 MHz (74°C THERMALS)",
                ColorAccentBlue,
                new string[] {
                    "• GPU Clock Clamped: 2100 MHz (~100W)",
                    "• CPU Boost Mode 3 (Efficient Enabled)",
                    "• EPP: 20% (Max Draw Latency)",
                    "• Sub-74°C Sustained GPU Core Temp",
                    "• Ultra-Quiet Acoustic Operation",
                    "• Perfect for stealth & night sessions"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileCold.ProfileClicked += (s, e) => SelectProfile("cold");

            panelProfilesView.Controls.Add(cardProfileSnappy);
            panelProfilesView.Controls.Add(cardProfileEfficiency);
            panelProfilesView.Controls.Add(cardProfileCold);
            this.Controls.Add(panelProfilesView);

            // Custom Tuner Overlay
            InitializeCustomTunerPanel();

            // =========================================================================
            // 6. VIEW 2: AUTOMATED BENCHMARK PANEL
            // =========================================================================
            InitializeBenchmarkPanel();

            // =========================================================================
            // 7. VIEW 3: PER-CORE TOPOLOGY & INTEGRATED HARDWARE TUNER
            // =========================================================================
            InitializeTopologyPanel();

            // =========================================================================
            // 8. FOOTER PANEL (Bottom 46px)
            // =========================================================================
            panelFooter = new Panel();
            panelFooter.Dock = DockStyle.Bottom;
            panelFooter.Height = 52;
            panelFooter.BackColor = Color.FromArgb(14, 15, 20);

            lblFooterStatus = new Label();
            lblFooterStatus.Text = "● ETW DXGI Active  |  Telemetry: 750ms  |  D3cold Safe Architecture  |  BenQ EX271Q Aware";
            lblFooterStatus.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblFooterStatus.ForeColor = ColorTextDim;
            lblFooterStatus.Location = new Point(16, 17);
            lblFooterStatus.AutoSize = true;

            btnOpenTuner = new GlowButton();
            btnOpenTuner.Text = "⚙ Tuner";
            btnOpenTuner.Size = new Size(110, 34);
            btnOpenTuner.ButtonColor = ColorCardBg;
            btnOpenTuner.BorderColor = ColorAccentPurple;
            btnOpenTuner.TextColor = ColorAccentPurple;
            btnOpenTuner.Click += (s, e) => ToggleCustomTuner();

            btnFooterTray = new GlowButton();
            btnFooterTray.Text = "▼ Tray";
            btnFooterTray.Size = new Size(95, 34);
            btnFooterTray.ButtonColor = ColorCardBg;
            btnFooterTray.BorderColor = ColorBorder;
            btnFooterTray.TextColor = ColorTextWhite;
            btnFooterTray.Click += (s, e) => MinimizeToTray();

            btnFooterExit = new GlowButton();
            btnFooterExit.Text = "✕ Exit";
            btnFooterExit.Size = new Size(80, 34);
            btnFooterExit.ButtonColor = ColorCardBg;
            btnFooterExit.BorderColor = ColorAccentRed;
            btnFooterExit.TextColor = ColorAccentRed;
            btnFooterExit.Click += (s, e) => ExitApplication();

            panelFooter.Controls.Add(lblFooterStatus);
            panelFooter.Controls.Add(btnOpenTuner);
            panelFooter.Controls.Add(btnFooterTray);
            panelFooter.Controls.Add(btnFooterExit);
            this.Controls.Add(panelFooter);
        }

        // -----------------------------------------------------------------------------------------
        // RESPONSIVE LAYOUT ENGINE (Executes on Resize, Maximize, and DPI Scale)
        // -----------------------------------------------------------------------------------------
        private void PerformResponsiveLayout() {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w < 800 || h < 550) return;

            // 1. Title Bar (H = 44)
            panelTitle.Width = w;
            lblSubtitle.Location = new Point(lblTitle.Right + 12, 15);
            btnClose.Location = new Point(w - 38, 8);
            btnMax.Location = new Point(w - 74, 8);
            btnMin.Location = new Point(w - 110, 8);
            btnTrayMin.Location = new Point(w - 146, 8);
            lblProfileBadge.Location = new Point(w - 158 - lblProfileBadge.Width, 11);

            // 2. Hero Section (Y = 52, H = 160)
            int heroTop = 52;
            int heroH = 160;
            int fpsW = Math.Max(350, (int)(w * 0.33));
            int pwrW = w - 32 - fpsW - 12;

            cardFps.Location = new Point(16, heroTop);
            cardFps.Size = new Size(fpsW, heroH);

            cardPlatformPower.Location = new Point(16 + fpsW + 12, heroTop);
            cardPlatformPower.Size = new Size(pwrW, heroH);

            // 3. Telemetry Mid Section (Y = 220, H = 170)
            int midTop = 220;
            int midH = 170;
            int midCardW = (w - 44) / 2;

            cardCpu.Location = new Point(16, midTop);
            cardCpu.Size = new Size(midCardW, midH);

            cardGpu.Location = new Point(16 + midCardW + 12, midTop);
            cardGpu.Size = new Size(midCardW, midH);

            // 4. Tab Navigation Strip (Y = 398, H = 38)
            int tabTop = 398;
            panelTabStrip.Location = new Point(16, tabTop);
            panelTabStrip.Size = new Size(w - 32, 38);

            // 5. Views Area
            int viewTop = 440;
            int footerH = 52;
            int viewH = h - viewTop - footerH - 8;
            int viewW = w - 32;

            panelProfilesView.Location = new Point(16, viewTop);
            panelProfilesView.Size = new Size(viewW, viewH);

            panelBenchmarkView.Location = new Point(16, viewTop);
            panelBenchmarkView.Size = new Size(viewW, viewH);

            panelTopologyView.Location = new Point(16, viewTop);
            panelTopologyView.Size = new Size(viewW, viewH);

            // Inside Profiles View (3 cards)
            int pCardW = (viewW - 24) / 3;
            cardProfileSnappy.Location = new Point(0, 0);
            cardProfileSnappy.Size = new Size(pCardW, viewH);

            cardProfileEfficiency.Location = new Point(pCardW + 12, 0);
            cardProfileEfficiency.Size = new Size(pCardW, viewH);

            cardProfileCold.Location = new Point((pCardW + 12) * 2, 0);
            cardProfileCold.Size = new Size(pCardW, viewH);

            // Inside Benchmark View
            benchProgressBar.Width = viewW - 28;
            benchResultsGrid.Width = viewW - 28;
            benchResultsGrid.Height = Math.Max(120, viewH - 172);

            // Inside Topology View
            btnToggleTopologyTuning.Width = viewW;
            if (panelTopologyTuningDrawer != null && panelTopologyTuningDrawer.Visible) {
                panelTopologyTuningDrawer.Width = viewW;
                topologyControl.Location = new Point(0, 196);
                topologyControl.Size = new Size(viewW, Math.Max(160, viewH - 196));
            } else if (topologyControl != null) {
                topologyControl.Location = new Point(0, 36);
                topologyControl.Size = new Size(viewW, Math.Max(220, viewH - 36));
            }

            // Inside Custom Tuner Overlay
            if (panelCustomTuner != null) {
                panelCustomTuner.Location = new Point(Math.Max(20, (w - 840) / 2), Math.Max(50, (h - 420) / 2));
            }

            // 6. Footer Panel
            btnFooterExit.Size = new Size(80, 34);
            btnFooterTray.Size = new Size(95, 34);
            btnOpenTuner.Size = new Size(110, 34);
            btnFooterExit.Location = new Point(w - 92, 9);
            btnFooterTray.Location = new Point(w - 197, 9);
            btnOpenTuner.Location = new Point(w - 317, 9);
            lblFooterStatus.MaximumSize = new Size(Math.Max(200, w - 335), 30);
        }

        private void ToggleMaximize() {
            if (this.WindowState == FormWindowState.Maximized) {
                this.WindowState = FormWindowState.Normal;
                btnMax.Text = "◻";
            } else {
                this.WindowState = FormWindowState.Maximized;
                btnMax.Text = "❐";
            }
            PerformResponsiveLayout();
        }

        private void SwitchTab(int tabIndex) {
            currentTabIndex = tabIndex;
            btnTabProfiles.ButtonColor = (tabIndex == 0) ? ColorTabActiveBg : ColorTabInactiveBg;
            btnTabProfiles.BorderColor = (tabIndex == 0) ? ColorAccentCyan : ColorBorder;
            btnTabProfiles.TextColor = (tabIndex == 0) ? ColorAccentCyan : ColorTextMuted;

            btnTabBenchmark.ButtonColor = (tabIndex == 1) ? Color.FromArgb(32, 24, 48) : ColorTabInactiveBg;
            btnTabBenchmark.BorderColor = (tabIndex == 1) ? ColorAccentPurple : ColorBorder;
            btnTabBenchmark.TextColor = (tabIndex == 1) ? ColorAccentPurple : ColorTextMuted;

            btnTabTopology.ButtonColor = (tabIndex == 2) ? Color.FromArgb(36, 32, 20) : ColorTabInactiveBg;
            btnTabTopology.BorderColor = (tabIndex == 2) ? ColorAccentGold : ColorBorder;
            btnTabTopology.TextColor = (tabIndex == 2) ? ColorAccentGold : ColorTextMuted;

            panelProfilesView.Visible = (tabIndex == 0);
            panelBenchmarkView.Visible = (tabIndex == 1);
            panelTopologyView.Visible = (tabIndex == 2);

            btnOpenTuner.Visible = (tabIndex == 0);
            if (panelCustomTuner.Visible && tabIndex != 0) ToggleCustomTuner();

            if (tabIndex == 2) {
                topologyControl.Invalidate();
            }
        }

        // -----------------------------------------------------------------------------------------
        // AUTOMATED BENCHMARK PANEL SETUP
        // -----------------------------------------------------------------------------------------
        private void InitializeBenchmarkPanel() {
            panelBenchmarkView = new Panel();
            panelBenchmarkView.BackColor = ColorBgMain;
            panelBenchmarkView.Visible = false;

            lblBenchHeader = new Label();
            lblBenchHeader.Text = "1-CLICK AUTOMATED PROFILE BENCHMARK SUITE";
            lblBenchHeader.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            lblBenchHeader.ForeColor = ColorAccentPurple;
            lblBenchHeader.Location = new Point(14, 10);
            lblBenchHeader.AutoSize = true;

            lblBenchSub = new Label();
            lblBenchSub.Text = "Multi-run performance evaluation with hardware power-cut and loading-freeze outlier elimination.";
            lblBenchSub.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblBenchSub.ForeColor = ColorTextMuted;
            lblBenchSub.Location = new Point(14, 32);
            lblBenchSub.AutoSize = true;

            lblBenchDuration = new Label();
            lblBenchDuration.Text = "Iterations:";
            lblBenchDuration.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblBenchDuration.ForeColor = ColorTextWhite;
            lblBenchDuration.Location = new Point(14, 60);
            lblBenchDuration.AutoSize = true;

            comboBenchSamples = new ComboBox();
            comboBenchSamples.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBenchSamples.BackColor = ColorCardBg;
            comboBenchSamples.ForeColor = ColorTextWhite;
            comboBenchSamples.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            comboBenchSamples.Items.AddRange(new object[] {
                "5 Iterations (Fast Validation)",
                "10 Iterations (Recommended Standard)",
                "20 Iterations (Rigorous Statistical Suite)"
            });
            comboBenchSamples.SelectedIndex = 1;
            comboBenchSamples.Location = new Point(105, 57);
            comboBenchSamples.Size = new Size(245, 24);

            chkFilterOutliers = new CheckBox();
            chkFilterOutliers.Text = "Intelligent Outlier Elimination (Filter AC Drops • Loading Freezes)";
            chkFilterOutliers.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            chkFilterOutliers.ForeColor = ColorAccentCyan;
            chkFilterOutliers.Checked = true;
            chkFilterOutliers.Location = new Point(365, 58);
            chkFilterOutliers.AutoSize = true;

            btnStartBenchmark = new GlowButton();
            btnStartBenchmark.Text = "▶ Run Benchmark";
            btnStartBenchmark.Location = new Point(14, 92);
            btnStartBenchmark.Size = new Size(180, 32);
            btnStartBenchmark.ButtonColor = ColorCardBg;
            btnStartBenchmark.BorderColor = ColorAccentPurple;
            btnStartBenchmark.TextColor = ColorAccentPurple;
            btnStartBenchmark.Click += (s, e) => StartBenchmark();

            btnStopBenchmark = new GlowButton();
            btnStopBenchmark.Text = "⏹ Cancel";
            btnStopBenchmark.Location = new Point(202, 92);
            btnStopBenchmark.Size = new Size(88, 32);
            btnStopBenchmark.ButtonColor = ColorCardBg;
            btnStopBenchmark.BorderColor = ColorBorder;
            btnStopBenchmark.TextColor = ColorTextDim;
            btnStopBenchmark.Enabled = false;
            btnStopBenchmark.Click += (s, e) => CancelBenchmark();

            btnApplyWinningProfile = new GlowButton();
            btnApplyWinningProfile.Text = "🏆 Apply Winner";
            btnApplyWinningProfile.Location = new Point(298, 92);
            btnApplyWinningProfile.Size = new Size(180, 32);
            btnApplyWinningProfile.ButtonColor = Color.FromArgb(36, 30, 16);
            btnApplyWinningProfile.BorderColor = ColorAccentGold;
            btnApplyWinningProfile.TextColor = ColorAccentGold;
            btnApplyWinningProfile.Visible = false;
            btnApplyWinningProfile.Click += (s, e) => ApplyWinningProfile();

            lblBenchStatus = new Label();
            lblBenchStatus.Text = "Ready. Launch 3D game, then start benchmark suite.";
            lblBenchStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblBenchStatus.ForeColor = ColorTextMuted;
            lblBenchStatus.Location = new Point(490, 100);
            lblBenchStatus.AutoSize = true;

            benchProgressBar = new BenchmarkProgressBar();
            benchProgressBar.Location = new Point(14, 132);
            benchProgressBar.Height = 12;

            lblOutlierAlertPill = new Label();
            lblOutlierAlertPill.Text = "⚡ AC LINE STABLE • DUAL OUTLIER REJECTION ARMED";
            lblOutlierAlertPill.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            lblOutlierAlertPill.ForeColor = ColorAccentGreen;
            lblOutlierAlertPill.BackColor = Color.FromArgb(10, 32, 22);
            lblOutlierAlertPill.Padding = new Padding(6, 2, 6, 2);
            lblOutlierAlertPill.Location = new Point(14, 150);
            lblOutlierAlertPill.AutoSize = true;

            benchResultsGrid = new BenchmarkResultsGrid();
            benchResultsGrid.Location = new Point(14, 174);

            panelBenchmarkView.Controls.Add(lblBenchHeader);
            panelBenchmarkView.Controls.Add(lblBenchSub);
            panelBenchmarkView.Controls.Add(lblBenchDuration);
            panelBenchmarkView.Controls.Add(comboBenchSamples);
            panelBenchmarkView.Controls.Add(chkFilterOutliers);
            panelBenchmarkView.Controls.Add(btnStartBenchmark);
            panelBenchmarkView.Controls.Add(btnStopBenchmark);
            panelBenchmarkView.Controls.Add(btnApplyWinningProfile);
            panelBenchmarkView.Controls.Add(lblBenchStatus);
            panelBenchmarkView.Controls.Add(benchProgressBar);
            panelBenchmarkView.Controls.Add(lblOutlierAlertPill);
            panelBenchmarkView.Controls.Add(benchResultsGrid);
            this.Controls.Add(panelBenchmarkView);
        }

        // -----------------------------------------------------------------------------------------
        // TOPOLOGY PANEL & INTEGRATED ON-DEMAND HARDWARE TUNER
        // -----------------------------------------------------------------------------------------
        private void InitializeTopologyPanel() {
            panelTopologyView = new Panel();
            panelTopologyView.BackColor = Color.FromArgb(18, 19, 24);
            panelTopologyView.Visible = false;

            // 1. On-Demand Tuning Toggle Button
            btnToggleTopologyTuning = new GlowButton();
            btnToggleTopologyTuning.Text = "⚙ ADVANCED HARDWARE TUNING CONTROLS [▼ Click to Expand]";
            btnToggleTopologyTuning.Location = new Point(0, 0);
            btnToggleTopologyTuning.Height = 30;
            btnToggleTopologyTuning.ButtonColor = ColorCardBg;
            btnToggleTopologyTuning.BorderColor = ColorBorder;
            btnToggleTopologyTuning.TextColor = ColorTextMuted;
            btnToggleTopologyTuning.Click += (s, e) => ToggleTopologyTuningDrawer();
            panelTopologyView.Controls.Add(btnToggleTopologyTuning);

            // 2. Expandable Tuning Drawer Panel (Hidden by default)
            panelTopologyTuningDrawer = new Panel();
            panelTopologyTuningDrawer.Location = new Point(0, 36);
            panelTopologyTuningDrawer.Height = 160;
            panelTopologyTuningDrawer.BackColor = Color.FromArgb(20, 23, 31);
            panelTopologyTuningDrawer.BorderStyle = BorderStyle.FixedSingle;
            panelTopologyTuningDrawer.Visible = false;

            // Row 1: P-Core, E-Core, EPP Sliders
            lblDrawerPcoreVal = new Label();
            lblDrawerPcoreVal.Text = "P-Core Turbo: Unbounded (Up to 5.5 GHz)";
            lblDrawerPcoreVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerPcoreVal.ForeColor = ColorAccentCyan;
            lblDrawerPcoreVal.Location = new Point(14, 8);
            lblDrawerPcoreVal.AutoSize = true;

            trackDrawerPcore = new TrackBar();
            trackDrawerPcore.Minimum = 30;
            trackDrawerPcore.Maximum = 55;
            trackDrawerPcore.Value = 55;
            trackDrawerPcore.TickFrequency = 2;
            trackDrawerPcore.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerPcore.Location = new Point(14, 24);
            trackDrawerPcore.Size = new Size(290, 32);
            trackDrawerPcore.ValueChanged += (s, e) => {
                lblDrawerPcoreVal.Text = (trackDrawerPcore.Value == 55)
                    ? "P-Core Turbo: Unbounded (Up to 5.5 GHz)"
                    : string.Format("P-Core Turbo: {0:0.0} GHz ({1}00 MHz)", trackDrawerPcore.Value / 10.0, trackDrawerPcore.Value);
            };

            lblDrawerEcoreVal = new Label();
            lblDrawerEcoreVal.Text = "E-Core Turbo: 2.8 GHz (2800 MHz)";
            lblDrawerEcoreVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerEcoreVal.ForeColor = ColorAccentGold;
            lblDrawerEcoreVal.Location = new Point(326, 8);
            lblDrawerEcoreVal.AutoSize = true;

            trackDrawerEcore = new TrackBar();
            trackDrawerEcore.Minimum = 16;
            trackDrawerEcore.Maximum = 32;
            trackDrawerEcore.Value = 28;
            trackDrawerEcore.TickFrequency = 2;
            trackDrawerEcore.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerEcore.Location = new Point(326, 24);
            trackDrawerEcore.Size = new Size(290, 32);
            trackDrawerEcore.ValueChanged += (s, e) => {
                lblDrawerEcoreVal.Text = string.Format("E-Core Turbo: {0:0.0} GHz ({1}00 MHz)", trackDrawerEcore.Value / 10.0, trackDrawerEcore.Value);
            };

            lblDrawerEppVal = new Label();
            lblDrawerEppVal.Text = "EPP Policy: 30% (Eager GPU Release)";
            lblDrawerEppVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerEppVal.ForeColor = ColorAccentCyan;
            lblDrawerEppVal.Location = new Point(630, 8);
            lblDrawerEppVal.AutoSize = true;

            trackDrawerEpp = new TrackBar();
            trackDrawerEpp.Minimum = 0;
            trackDrawerEpp.Maximum = 100;
            trackDrawerEpp.Value = 30;
            trackDrawerEpp.TickFrequency = 10;
            trackDrawerEpp.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerEpp.Location = new Point(630, 24);
            trackDrawerEpp.Size = new Size(280, 32);
            trackDrawerEpp.ValueChanged += (s, e) => {
                lblDrawerEppVal.Text = string.Format("EPP Policy: {0}% (Energy Perf Preference)", trackDrawerEpp.Value);
            };

            // Row 2: Boost Mode, GPU Clock Clamp, Apply Button
            lblDrawerBoostVal = new Label();
            lblDrawerBoostVal.Text = "Intel Boost Mode:";
            lblDrawerBoostVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerBoostVal.ForeColor = ColorAccentGold;
            lblDrawerBoostVal.Location = new Point(14, 82);
            lblDrawerBoostVal.AutoSize = true;

            comboDrawerBoost = new ComboBox();
            comboDrawerBoost.DropDownStyle = ComboBoxStyle.DropDownList;
            comboDrawerBoost.BackColor = ColorCardBg;
            comboDrawerBoost.ForeColor = ColorTextWhite;
            comboDrawerBoost.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            comboDrawerBoost.Items.AddRange(new object[] {
                "0 - Disabled",
                "1 - Enabled",
                "2 - Aggressive",
                "3 - Efficient Enabled",
                "4 - Efficient Aggressive (Snappy Default)",
                "5 - Aggressive At Guaranteed",
                "6 - Efficient Aggressive At Guaranteed"
            });
            comboDrawerBoost.SelectedIndex = 4;
            comboDrawerBoost.Location = new Point(14, 102);
            comboDrawerBoost.Size = new Size(290, 22);

            lblDrawerGpuVal = new Label();
            lblDrawerGpuVal.Text = "RTX 5070 Mobile Max Clock: Stock / Unconstrained";
            lblDrawerGpuVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerGpuVal.ForeColor = ColorAccentCyan;
            lblDrawerGpuVal.Location = new Point(326, 82);
            lblDrawerGpuVal.AutoSize = true;

            trackDrawerGpu = new TrackBar();
            trackDrawerGpu.Minimum = 14;
            trackDrawerGpu.Maximum = 26;
            trackDrawerGpu.Value = 26;
            trackDrawerGpu.TickFrequency = 1;
            trackDrawerGpu.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerGpu.Location = new Point(326, 100);
            trackDrawerGpu.Size = new Size(330, 32);
            trackDrawerGpu.ValueChanged += (s, e) => {
                lblDrawerGpuVal.Text = (trackDrawerGpu.Value == 26)
                    ? "RTX 5070 Mobile Max Clock: Stock / Unconstrained"
                    : string.Format("RTX 5070 Mobile Max Clock: {0}00 MHz", trackDrawerGpu.Value);
            };

            btnDrawerApply = new GlowButton();
            btnDrawerApply.Text = "✔ Apply Custom Hardware Tuning";
            btnDrawerApply.Location = new Point(680, 100);
            btnDrawerApply.Size = new Size(260, 32);
            btnDrawerApply.ButtonColor = Color.FromArgb(24, 44, 32);
            btnDrawerApply.BorderColor = ColorAccentGreen;
            btnDrawerApply.TextColor = ColorAccentGreen;
            btnDrawerApply.Click += (s, e) => ApplyDrawerTuningValues();

            panelTopologyTuningDrawer.Controls.Add(lblDrawerPcoreVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerPcore);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerEcoreVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerEcore);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerEppVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerEpp);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerBoostVal);
            panelTopologyTuningDrawer.Controls.Add(comboDrawerBoost);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerGpuVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerGpu);
            panelTopologyTuningDrawer.Controls.Add(btnDrawerApply);
            panelTopologyView.Controls.Add(panelTopologyTuningDrawer);

            // 3. Arrow Lake-HX 24-Core Topology Control
            topologyControl = new PerCoreTopologyControl();
            panelTopologyView.Controls.Add(topologyControl);

            this.Controls.Add(panelTopologyView);
        }

        private void ToggleTopologyTuningDrawer() {
            panelTopologyTuningDrawer.Visible = !panelTopologyTuningDrawer.Visible;
            if (panelTopologyTuningDrawer.Visible) {
                btnToggleTopologyTuning.Text = "⚙ ADVANCED HARDWARE TUNING CONTROLS [▲ Click to Collapse]";
                btnToggleTopologyTuning.BorderColor = ColorAccentGold;
                btnToggleTopologyTuning.TextColor = ColorAccentGold;
            } else {
                btnToggleTopologyTuning.Text = "⚙ ADVANCED HARDWARE TUNING CONTROLS [▼ Click to Expand]";
                btnToggleTopologyTuning.BorderColor = ColorBorder;
                btnToggleTopologyTuning.TextColor = ColorTextMuted;
            }
            PerformResponsiveLayout();
        }

        private void ApplyDrawerTuningValues() {
            int pcore = (trackDrawerPcore.Value == 55) ? 0 : trackDrawerPcore.Value * 100;
            int ecore = trackDrawerEcore.Value * 100;
            int epp = trackDrawerEpp.Value;
            int boostMode = comboDrawerBoost.SelectedIndex;
            int gpu = (trackDrawerGpu.Value == 26) ? 0 : trackDrawerGpu.Value * 100;

            bridge.ApplyCustomProfile(pcore, ecore, boostMode, epp, gpu);
            currentSelectedProfile = "custom";
            UpdateProfileCardsVisualState();

            ShowNotificationBalloon("Hardware Tuning Applied", string.Format("P-Core: {0} | EPP: {1}% | Boost: Mode {2}", (pcore == 0 ? "Unbounded" : pcore.ToString() + " MHz"), epp, boostMode));
        }

        // -----------------------------------------------------------------------------------------
        // CUSTOM TUNER OVERLAY SETUP
        // -----------------------------------------------------------------------------------------
        private void InitializeCustomTunerPanel() {
            panelCustomTuner = new Panel();
            panelCustomTuner.Size = new Size(820, 400);
            panelCustomTuner.BackColor = Color.FromArgb(22, 25, 33);
            panelCustomTuner.BorderStyle = BorderStyle.FixedSingle;
            panelCustomTuner.Visible = false;

            lblTunerTitle = new Label();
            lblTunerTitle.Text = "🛠️ REAL-TIME HARDWARE TUNER (CORE ULTRA 9 275HX & RTX 5070 MOBILE)";
            lblTunerTitle.Font = new Font("Segoe UI", 12f, FontStyle.Bold);
            lblTunerTitle.ForeColor = ColorAccentPurple;
            lblTunerTitle.Location = new Point(18, 14);
            lblTunerTitle.AutoSize = true;

            // 1. P-Core Limit Slider
            lblPcoreVal = new Label();
            lblPcoreVal.Text = "P-Core Turbo Limit: Unbounded (Up to 5.5 GHz)";
            lblPcoreVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblPcoreVal.ForeColor = ColorAccentCyan;
            lblPcoreVal.Location = new Point(18, 52);
            lblPcoreVal.AutoSize = true;

            trackPcore = new TrackBar();
            trackPcore.Minimum = 30; // 3.0 GHz
            trackPcore.Maximum = 55; // 5.5 GHz (55 = unbounded 0)
            trackPcore.Value = 55;
            trackPcore.TickFrequency = 1;
            trackPcore.BackColor = Color.FromArgb(22, 25, 33);
            trackPcore.Location = new Point(18, 72);
            trackPcore.Size = new Size(360, 45);
            trackPcore.ValueChanged += (s, e) => {
                lblPcoreVal.Text = (trackPcore.Value == 55)
                    ? "P-Core Turbo Limit: Unbounded (Up to 5.5 GHz)"
                    : string.Format("P-Core Turbo Limit: {0:0.0} GHz ({1}00 MHz)", trackPcore.Value / 10.0, trackPcore.Value);
            };

            // 2. E-Core Limit Slider
            lblEcoreVal = new Label();
            lblEcoreVal.Text = "E-Core Turbo Limit: 2.8 GHz (2800 MHz)";
            lblEcoreVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblEcoreVal.ForeColor = ColorAccentGold;
            lblEcoreVal.Location = new Point(430, 52);
            lblEcoreVal.AutoSize = true;

            trackEcore = new TrackBar();
            trackEcore.Minimum = 16;
            trackEcore.Maximum = 32;
            trackEcore.Value = 28;
            trackEcore.TickFrequency = 1;
            trackEcore.BackColor = Color.FromArgb(22, 25, 33);
            trackEcore.Location = new Point(430, 72);
            trackEcore.Size = new Size(360, 45);
            trackEcore.ValueChanged += (s, e) => {
                lblEcoreVal.Text = string.Format("E-Core Turbo Limit: {0:0.0} GHz ({1}00 MHz)", trackEcore.Value / 10.0, trackEcore.Value);
            };

            // 3. EPP Slider
            lblEppVal = new Label();
            lblEppVal.Text = "EPP Policy: 30% (Eager GPU Wattage Release)";
            lblEppVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblEppVal.ForeColor = ColorAccentCyan;
            lblEppVal.Location = new Point(18, 130);
            lblEppVal.AutoSize = true;

            trackEpp = new TrackBar();
            trackEpp.Minimum = 0;
            trackEpp.Maximum = 100;
            trackEpp.Value = 30;
            trackEpp.TickFrequency = 5;
            trackEpp.BackColor = Color.FromArgb(22, 25, 33);
            trackEpp.Location = new Point(18, 154);
            trackEpp.Size = new Size(360, 45);
            trackEpp.ValueChanged += (s, e) => {
                lblEppVal.Text = string.Format("EPP Policy: {0}% (Energy Performance Preference)", trackEpp.Value);
            };

            // 4. Boost Mode Dropdown
            lblBoostModeVal = new Label();
            lblBoostModeVal.Text = "Intel Speed Shift Boost Mode:";
            lblBoostModeVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblBoostModeVal.ForeColor = ColorAccentGold;
            lblBoostModeVal.Location = new Point(430, 130);
            lblBoostModeVal.AutoSize = true;

            comboBoostMode = new ComboBox();
            comboBoostMode.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoostMode.BackColor = ColorCardBg;
            comboBoostMode.ForeColor = ColorTextWhite;
            comboBoostMode.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            comboBoostMode.Items.AddRange(new object[] {
                "0 - Disabled",
                "1 - Enabled",
                "2 - Aggressive",
                "3 - Efficient Enabled",
                "4 - Efficient Aggressive (Snappy Default)",
                "5 - Aggressive At Guaranteed",
                "6 - Efficient Aggressive At Guaranteed"
            });
            comboBoostMode.SelectedIndex = 4;
            comboBoostMode.Location = new Point(430, 148);
            comboBoostMode.Size = new Size(360, 26);

            // 5. GPU Clock Clamp Slider
            lblGpuClockLimitVal = new Label();
            lblGpuClockLimitVal.Text = "RTX 5070 Mobile Max Clock: Stock / Unconstrained";
            lblGpuClockLimitVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblGpuClockLimitVal.ForeColor = ColorAccentCyan;
            lblGpuClockLimitVal.Location = new Point(18, 210);
            lblGpuClockLimitVal.AutoSize = true;

            trackGpuClock = new TrackBar();
            trackGpuClock.Minimum = 14; // 1400 MHz
            trackGpuClock.Maximum = 26; // 2600 MHz (26 = stock 0)
            trackGpuClock.Value = 26;
            trackGpuClock.TickFrequency = 1;
            trackGpuClock.BackColor = Color.FromArgb(22, 25, 33);
            trackGpuClock.Location = new Point(18, 218);
            trackGpuClock.Size = new Size(780, 45);
            trackGpuClock.ValueChanged += (s, e) => {
                lblGpuClockLimitVal.Text = (trackGpuClock.Value == 26)
                    ? "RTX 5070 Mobile Max Clock: Stock / Unconstrained"
                    : string.Format("RTX 5070 Mobile Max Clock: Clamped to {0}00 MHz", trackGpuClock.Value);
            };

            btnApplyCustom = new GlowButton();
            btnApplyCustom.Text = "✔ Apply Custom Profile";
            btnApplyCustom.Location = new Point(18, 300);
            btnApplyCustom.Size = new Size(200, 36);
            btnApplyCustom.ButtonColor = Color.FromArgb(28, 48, 36);
            btnApplyCustom.BorderColor = ColorAccentGreen;
            btnApplyCustom.TextColor = ColorAccentGreen;
            btnApplyCustom.Click += (s, e) => ApplyCustomTunerValues();

            btnCloseTuner = new GlowButton();
            btnCloseTuner.Text = "✕ Close";
            btnCloseTuner.Location = new Point(230, 300);
            btnCloseTuner.Size = new Size(100, 36);
            btnCloseTuner.ButtonColor = ColorCardBg;
            btnCloseTuner.BorderColor = ColorBorder;
            btnCloseTuner.TextColor = ColorTextMuted;
            btnCloseTuner.Click += (s, e) => ToggleCustomTuner();

            panelCustomTuner.Controls.Add(lblTunerTitle);
            panelCustomTuner.Controls.Add(lblPcoreVal);
            panelCustomTuner.Controls.Add(trackPcore);
            panelCustomTuner.Controls.Add(lblEcoreVal);
            panelCustomTuner.Controls.Add(trackEcore);
            panelCustomTuner.Controls.Add(lblEppVal);
            panelCustomTuner.Controls.Add(trackEpp);
            panelCustomTuner.Controls.Add(lblBoostModeVal);
            panelCustomTuner.Controls.Add(comboBoostMode);
            panelCustomTuner.Controls.Add(lblGpuClockLimitVal);
            panelCustomTuner.Controls.Add(trackGpuClock);
            panelCustomTuner.Controls.Add(btnApplyCustom);
            panelCustomTuner.Controls.Add(btnCloseTuner);

            this.Controls.Add(panelCustomTuner);
            panelCustomTuner.BringToFront();
        }

        private void ToggleCustomTuner() {
            panelCustomTuner.Visible = !panelCustomTuner.Visible;
            if (panelCustomTuner.Visible) {
                panelCustomTuner.BringToFront();
                btnOpenTuner.Text = "✕ Close Tuner";
                btnOpenTuner.BorderColor = ColorAccentCyan;
                btnOpenTuner.TextColor = ColorAccentCyan;
            } else {
                btnOpenTuner.Text = "⚙ Hardware Tuner";
                btnOpenTuner.BorderColor = ColorAccentPurple;
                btnOpenTuner.TextColor = ColorAccentPurple;
            }
        }

        private void ApplyCustomTunerValues() {
            int pcore = (trackPcore.Value == 55) ? 0 : trackPcore.Value * 100;
            int ecore = trackEcore.Value * 100;
            int epp = trackEpp.Value;
            int boostMode = comboBoostMode.SelectedIndex;
            int gpu = (trackGpuClock.Value == 26) ? 0 : trackGpuClock.Value * 100;

            bridge.ApplyCustomProfile(pcore, ecore, boostMode, epp, gpu);
            currentSelectedProfile = "custom";
            UpdateProfileCardsVisualState();
            ToggleCustomTuner();

            ShowNotificationBalloon("Custom Profile Applied", string.Format("P-Core: {0} | EPP: {1}% | Boost: Mode {2}", (pcore == 0 ? "Unbounded" : pcore.ToString() + " MHz"), epp, boostMode));
        }

        // -----------------------------------------------------------------------------------------
        // SYSTEM TRAY SETUP
        // -----------------------------------------------------------------------------------------
        private void InitializeSystemTray() {
            trayMenu = new ContextMenu();

            MenuItem mHeader = new MenuItem("Vector Power Hub - MSI Vector 16 HX");
            mHeader.Enabled = false;
            trayMenu.MenuItems.Add(mHeader);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            trayMenuSnappy = new MenuItem("⚡ Snappy-Pacing (Competitive Max FPS)", (s, e) => SelectProfile("snappy"));
            trayMenuEfficiency = new MenuItem("✦ Sweet-Spot Efficiency (4.9 GHz / 58W)", (s, e) => SelectProfile("clamped"));
            trayMenuCold = new MenuItem("❄ Cold & Quiet (GPU 2100 MHz)", (s, e) => SelectProfile("cold"));
            trayMenuCustom = new MenuItem("⚙ Hardware Tuner...", (s, e) => {
                RestoreFromTray();
                SwitchTab(2);
                if (!panelTopologyTuningDrawer.Visible) ToggleTopologyTuningDrawer();
            });
            trayMenuBenchmark = new MenuItem("★ Automated Benchmark Suite...", (s, e) => {
                RestoreFromTray();
                SwitchTab(1);
            });

            trayMenu.MenuItems.Add(trayMenuSnappy);
            trayMenu.MenuItems.Add(trayMenuEfficiency);
            trayMenu.MenuItems.Add(trayMenuCold);
            trayMenu.MenuItems.Add(trayMenuCustom);
            trayMenu.MenuItems.Add(trayMenuBenchmark);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            MenuItem mShow = new MenuItem("Open Dashboard", (s, e) => RestoreFromTray());
            MenuItem mExit = new MenuItem("Exit Vector Power Hub", (s, e) => ExitApplication());
            trayMenu.MenuItems.Add(mShow);
            trayMenu.MenuItems.Add(mExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Vector Power Hub - Snappy-Pacing Active";
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Icon = GenerateAppIcon();
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private Icon GenerateAppIcon() {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush b = new SolidBrush(ColorBgMain)) {
                    g.FillEllipse(b, 1, 1, 30, 30);
                }
                using (Pen p = new Pen(ColorAccentCyan, 2f)) {
                    g.DrawEllipse(p, 1, 1, 30, 30);
                }
                PointF[] bolt = new PointF[] {
                    new PointF(18, 5),
                    new PointF(10, 16),
                    new PointF(16, 16),
                    new PointF(13, 27),
                    new PointF(23, 14),
                    new PointF(17, 14)
                };
                using (Brush boltBrush = new SolidBrush(ColorAccentCyan)) {
                    g.FillPolygon(boltBrush, bolt);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void MinimizeToTray() {
            this.Hide();
            ShowNotificationBalloon("Vector Power Hub Running", "Minimized to system tray. Telemetry and active profile remain fully engaged.");
        }

        public void RestoreFromTray() {
            if (this.InvokeRequired) {
                this.BeginInvoke(new MethodInvoker(RestoreFromTray));
                return;
            }
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
            try { SetForegroundWindow(this.Handle); } catch { }
            PerformResponsiveLayout();
        }

        private void WakeListenerLoop() {
            while (!isDisposingOrClosed) {
                try {
                    if (singleInstanceWakeEvent != null && singleInstanceWakeEvent.WaitOne(500)) {
                        if (isDisposingOrClosed) break;
                        this.BeginInvoke(new MethodInvoker(RestoreFromTray));
                    }
                } catch {
                    break;
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e) {
            isDisposingOrClosed = true;
            if (singleInstanceWakeEvent != null) {
                try { singleInstanceWakeEvent.Set(); singleInstanceWakeEvent.Close(); } catch { }
                singleInstanceWakeEvent = null;
            }
            if (trayIcon != null) {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            base.OnFormClosing(e);
        }

        private void ShowNotificationBalloon(string title, string text) {
            try {
                trayIcon.BalloonTipTitle = title;
                trayIcon.BalloonTipText = text;
                trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.ShowBalloonTip(2000);
            } catch { }
        }

        private void ExitApplication() {
            isDisposingOrClosed = true;
            if (singleInstanceWakeEvent != null) {
                try { singleInstanceWakeEvent.Set(); singleInstanceWakeEvent.Close(); } catch { }
                singleInstanceWakeEvent = null;
            }
            telemetryTimer.Stop();
            if (trayIcon != null) {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            bridge.Dispose();
            Application.Exit();
        }

        // -----------------------------------------------------------------------------------------
        // TELEMETRY REFRESH LOOP (Every 750ms)
        // -----------------------------------------------------------------------------------------
        public void OnTelemetryTick(object sender, EventArgs e) {
            try {
                HubTelemetrySnapshot snap = bridge.GetSnapshot();
                currentSnapshot = snap;

                // 1. Update FPS Card
                cardFps.UpdateTelemetry(snap.Fps, snap.IsGameMode, snap.ActiveGameName, snap.ActiveGamePid);

                // 2. Update Total Platform Draw Card
                cardPlatformPower.UpdateTelemetry(snap.CpuPowerW, snap.GpuPowerW, snap.TotalPlatformPowerW);

                // 3. Update CPU Card
                cardCpu.UpdateTelemetry(snap.CpuPowerW, snap.PCoreGhz, snap.ECoreGhz);

                // 4. Update GPU Card
                cardGpu.UpdateTelemetry(snap.GpuPowerW, snap.GpuClockMhz, snap.GpuTempC, snap.GpuUtilPct, snap.IsGameMode, snap.IsNvidiaDisplayAttached, snap.NvidiaMonitorName, snap.GpuStatus);

                // 5. Update Per-Core Topology Control
                if (topologyControl != null) {
                    topologyControl.SetCoreData(snap.PerCoreGhz, snap.PerCoreUtil, snap.CpuPowerW);
                }

                // Synchronize Active Profile
                if (!string.IsNullOrEmpty(snap.ActiveProfile) && snap.ActiveProfile != currentSelectedProfile) {
                    currentSelectedProfile = snap.ActiveProfile;
                    UpdateProfileCardsVisualState();
                }
            } catch { }
        }

        private void SelectProfile(string profileId) {
            currentSelectedProfile = profileId;
            bridge.ApplyProfile(profileId);
            UpdateProfileCardsVisualState();

            string name = "Snappy-Pacing";
            if (profileId == "clamped") name = "Sweet-Spot Efficiency (4.9 GHz)";
            if (profileId == "cold") name = "Cold & Quiet (GPU 2100 MHz)";

            trayIcon.Text = string.Format("Vector Power Hub - {0} Active", name);
            ShowNotificationBalloon("Profile Switched", string.Format("Activated profile: {0}", name));
        }

        private void UpdateProfileCardsVisualState() {
            cardProfileSnappy.IsActive = (currentSelectedProfile == "snappy");
            cardProfileEfficiency.IsActive = (currentSelectedProfile == "clamped");
            cardProfileCold.IsActive = (currentSelectedProfile == "cold");

            lblProfileBadge.Text = string.Format("ACTIVE: {0}", currentSelectedProfile.ToUpper());

            trayMenuSnappy.Checked = (currentSelectedProfile == "snappy");
            trayMenuEfficiency.Checked = (currentSelectedProfile == "clamped");
            trayMenuCold.Checked = (currentSelectedProfile == "cold");
            trayMenuCustom.Checked = (currentSelectedProfile == "custom");
        }

        // -----------------------------------------------------------------------------------------
        // BENCHMARK EXECUTION LOGIC
        // -----------------------------------------------------------------------------------------
        private void StartBenchmark() {
            if (isBenchmarkRunning) return;

            int iterations = 10;
            if (comboBenchSamples.SelectedIndex == 0) iterations = 5;
            if (comboBenchSamples.SelectedIndex == 1) iterations = 10;
            if (comboBenchSamples.SelectedIndex == 2) iterations = 20;

            bool filterOutliers = chkFilterOutliers.Checked;

            isBenchmarkRunning = true;
            cancelBenchmarkRequested = false;

            btnStartBenchmark.Enabled = false;
            btnStopBenchmark.Enabled = true;
            btnStopBenchmark.BorderColor = ColorAccentRed;
            btnStopBenchmark.TextColor = ColorAccentRed;
            btnApplyWinningProfile.Visible = false;

            benchProgressBar.Value = 0;
            benchResultsGrid.ClearResults();

            benchmarkThread = new Thread(() => ExecuteBenchmark(iterations, filterOutliers));
            benchmarkThread.IsBackground = true;
            benchmarkThread.Start();
        }

        private void CancelBenchmark() {
            if (isBenchmarkRunning) {
                cancelBenchmarkRequested = true;
                lblBenchStatus.Text = "Cancelling benchmark run... Restoring previous profile...";
            }
        }

        private void SafeBeginInvoke(MethodInvoker action) {
            if (this.IsHandleCreated && !this.IsDisposed) {
                try {
                    this.BeginInvoke(action);
                } catch { }
            } else {
                try {
                    action();
                } catch { }
            }
        }

        public void ExecuteBenchmark(int iterationsPerProfile, bool filterOutliers) {
            string[] profileKeys = new string[] { "snappy", "clamped", "cold", "guaranteed" };
            string[] profileNames = new string[] {
                "⚡ Snappy-Pacing (Mode 4 / EPP 30%)",
                "🎯 Sweet-Spot Efficiency (4.9 GHz / 58W)",
                "❄️ Cold & Quiet (GPU 2100 MHz)",
                "🚀 Guaranteed Curve (Mode 6 / EPP 25%)"
            };

            string originalProfile = currentSelectedProfile;
            List<BenchmarkResultInfo> results = new List<BenchmarkResultInfo>();

            int totalSteps = profileKeys.Length * (3 + iterationsPerProfile);
            int currentStep = 0;
            int delayMs = (iterationsPerProfile <= 1) ? 50 : 1000;

            for (int pIdx = 0; pIdx < profileKeys.Length; pIdx++) {
                if (cancelBenchmarkRequested) break;

                string pKey = profileKeys[pIdx];
                string pName = profileNames[pIdx];

                ApplyBenchmarkProfile(pKey);

                // Warmup Phase (3 Seconds Countdown)
                for (int w = 3; w >= 1; w--) {
                    if (cancelBenchmarkRequested) break;
                    currentStep++;
                    int pct = (int)((currentStep / (double)totalSteps) * 100);

                    string warmupStatus = string.Format("⏱ Warmup Countdown: {0}s left — Stabilizing clocks & thermals for {1}...", w, pName);
                    int remainingSec = w;
                    SafeBeginInvoke((MethodInvoker)(() => {
                        lblBenchStatus.Text = warmupStatus;
                        benchProgressBar.Value = pct;
                        lblOutlierAlertPill.Text = string.Format("⏱ WARMUP COUNTDOWN: {0}s — CLOCKS STABILIZING", remainingSec);
                        lblOutlierAlertPill.ForeColor = ColorAccentGold;
                        lblOutlierAlertPill.BackColor = Color.FromArgb(40, 30, 12);
                    }));
                    Thread.Sleep(delayMs);
                }

                if (cancelBenchmarkRequested) break;

                // Sampling Phase
                List<double> fpsSamples = new List<double>();
                List<double> cpuSamples = new List<double>();
                List<double> gpuSamples = new List<double>();
                List<double> cleanFpsSamples = new List<double>();
                List<double> cleanCpuSamples = new List<double>();
                List<double> cleanGpuSamples = new List<double>();
                int outlierCount = 0;
                List<string> outlierReasons = new List<string>();

                for (int s = 1; s <= iterationsPerProfile; s++) {
                    if (cancelBenchmarkRequested) break;
                    currentStep++;
                    int pct = (int)((currentStep / (double)totalSteps) * 100);

                    HubTelemetrySnapshot snap = bridge.GetSnapshot();
                    double fps = snap.Fps;
                    double cpuW = snap.CpuPowerW;
                    double gpuW = snap.GpuPowerW;

                    fpsSamples.Add(fps);
                    cpuSamples.Add(cpuW);
                    gpuSamples.Add(gpuW);

                    bool isOutlier = false;
                    string reason = "";

                    if (filterOutliers) {
                        SYSTEM_POWER_STATUS pwrStatus;
                        if (GetSystemPowerStatus(out pwrStatus)) {
                            if (pwrStatus.ACLineStatus == 0) {
                                isOutlier = true;
                                reason = "AC Power Cut / DC Battery";
                            }
                        }

                        if (!isOutlier && fps < 20.0 && snap.GpuUtilPct < 15) {
                            isOutlier = true;
                            reason = "Loading Screen Freeze";
                        }
                    }

                    if (isOutlier) {
                        outlierCount++;
                        if (!outlierReasons.Contains(reason)) outlierReasons.Add(reason);
                    } else {
                        cleanFpsSamples.Add(fps);
                        cleanCpuSamples.Add(cpuW);
                        cleanGpuSamples.Add(gpuW);
                    }

                    string sampleStatus = string.Format("Testing {0} | Iteration {1}/{2} — FPS: {3:0.0} | CPU: {4:0.0}W | GPU: {5:0.0}W", pName, s, iterationsPerProfile, fps, cpuW, gpuW);
                    bool sampleOutlier = isOutlier;
                    string outlierMsg = reason;
                    SafeBeginInvoke((MethodInvoker)(() => {
                        lblBenchStatus.Text = sampleStatus;
                        benchProgressBar.Value = pct;
                        if (sampleOutlier) {
                            lblOutlierAlertPill.Text = string.Format("⚠️ OUTLIER REJECTED: {0}", outlierMsg);
                            lblOutlierAlertPill.ForeColor = ColorAccentRed;
                            lblOutlierAlertPill.BackColor = Color.FromArgb(45, 16, 16);
                        } else {
                            lblOutlierAlertPill.Text = "⚡ AC LINE STABLE • HARDWARE PACING NOMINAL";
                            lblOutlierAlertPill.ForeColor = ColorAccentGreen;
                            lblOutlierAlertPill.BackColor = Color.FromArgb(10, 32, 22);
                        }
                    }));

                    Thread.Sleep(delayMs);
                }

                // Compile Profile Results
                BenchmarkResultInfo r = new BenchmarkResultInfo();
                r.ProfileId = pKey;
                r.ProfileName = pName;

                r.RawAvgFps = ComputeAverage(fpsSamples);
                r.RawOnePercentLow = ComputeOnePercentLow(fpsSamples);

                List<double> finalFps = (cleanFpsSamples.Count > 0) ? cleanFpsSamples : fpsSamples;
                List<double> finalCpu = (cleanCpuSamples.Count > 0) ? cleanCpuSamples : cpuSamples;
                List<double> finalGpu = (cleanGpuSamples.Count > 0) ? cleanGpuSamples : gpuSamples;

                r.CleanedAvgFps = ComputeAverage(finalFps);
                r.CleanedOnePercentLow = ComputeOnePercentLow(finalFps);
                r.AvgCpuPowerW = ComputeAverage(finalCpu);
                r.AvgGpuPowerW = ComputeAverage(finalGpu);
                r.AvgTotalPowerW = r.AvgCpuPowerW + r.AvgGpuPowerW;
                r.OutliersFilteredCount = outlierCount;
                r.OutlierDetails = (outlierCount > 0) ? string.Join(", ", outlierReasons.ToArray()) : "None";

                if (r.AvgTotalPowerW > 0.1) {
                    r.EfficiencyScore = r.CleanedAvgFps / r.AvgTotalPowerW;
                }

                results.Add(r);

                SafeBeginInvoke((MethodInvoker)(() => {
                    benchResultsGrid.AddOrUpdateResult(r);
                }));
            }

            // Cleanup & Restore
            ApplyBenchmarkProfile(originalProfile);
            RunCmd("nvidia-smi -rgc");

            // Determine Winner
            BenchmarkResultInfo winner = null;
            double maxScore = -1.0;
            foreach (BenchmarkResultInfo res in results) {
                if (res.CleanedAvgFps > maxScore) {
                    maxScore = res.CleanedAvgFps;
                    winner = res;
                }
            }
            if (winner != null) {
                winner.IsWinner = true;
            }

            lastBenchmarkResults = results;

            SafeBeginInvoke((MethodInvoker)(() => {
                isBenchmarkRunning = false;
                btnStartBenchmark.Enabled = true;
                btnStopBenchmark.Enabled = false;
                btnStopBenchmark.BorderColor = ColorBorder;
                btnStopBenchmark.TextColor = ColorTextDim;
                benchProgressBar.Value = 100;

                if (!cancelBenchmarkRequested && winner != null) {
                    lblBenchStatus.Text = string.Format("✅ Benchmark Complete! Optimal Gaming Profile: {0} ({1:0.0} Cleaned FPS)", winner.ProfileName, winner.CleanedAvgFps);
                    btnApplyWinningProfile.Visible = true;
                    btnApplyWinningProfile.Text = string.Format("🏆 Apply Winner: {0}", winner.ProfileId.ToUpper());
                    lblOutlierAlertPill.Text = string.Format("🏆 WINNER: {0} • {1:0.0} FPS (1% LOW: {2:0.0})", winner.ProfileName, winner.CleanedAvgFps, winner.CleanedOnePercentLow);
                    lblOutlierAlertPill.ForeColor = ColorAccentGold;
                    lblOutlierAlertPill.BackColor = Color.FromArgb(40, 30, 12);
                    ShowNotificationBalloon("Benchmark Complete", string.Format("Winning Profile: {0}\nCleaned FPS: {1:0.0} (1% Low: {2:0.0})\nEfficiency: {3:0.00} FPS/W", winner.ProfileName, winner.CleanedAvgFps, winner.CleanedOnePercentLow, winner.EfficiencyScore));
                } else {
                    lblBenchStatus.Text = "Benchmark Cancelled. Restored initial profile.";
                }

                benchResultsGrid.Refresh();
            }));
        }

        private void ApplyBenchmarkProfile(string profileId) {
            if (profileId == "snappy") {
                bridge.ApplyProfile("snappy");
            } else if (profileId == "clamped") {
                bridge.ApplyProfile("clamped");
            } else if (profileId == "cold") {
                bridge.ApplyProfile("cold");
            } else if (profileId == "guaranteed") {
                bridge.ApplyCustomProfile(0, 0, 6, 25, 0);
            }
        }

        private void ApplyWinningProfile() {
            foreach (BenchmarkResultInfo r in lastBenchmarkResults) {
                if (r.IsWinner) {
                    SelectProfile(r.ProfileId);
                    SwitchTab(0);
                    break;
                }
            }
        }

        private double ComputeAverage(List<double> list) {
            if (list == null || list.Count == 0) return 0.0;
            double s = 0.0;
            for (int i = 0; i < list.Count; i++) s += list[i];
            return s / list.Count;
        }

        private double ComputeOnePercentLow(List<double> list) {
            if (list == null || list.Count == 0) return 0.0;
            List<double> sorted = new List<double>(list);
            sorted.Sort();
            int count = (int)Math.Ceiling(sorted.Count * 0.01);
            if (count < 1) count = 1;
            double s = 0.0;
            for (int i = 0; i < count; i++) s += sorted[i];
            return s / count;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte Reserved1;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);

        // -----------------------------------------------------------------------------------------
        // HELPER UI FACTORY METHODS
        // -----------------------------------------------------------------------------------------
        private Button CreateTitleButton(string text, int x, int y, EventHandler onClick) {
            Button btn = new Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btn.ForeColor = ColorTextMuted;
            btn.BackColor = Color.Transparent;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 45, 60);
            btn.Location = new Point(x, y);
            btn.Size = new Size(32, 28);
            btn.Click += onClick;
            return btn;
        }

        private static void RunCmd(string cmd) {
            try {
                int firstSpace = cmd.IndexOf(' ');
                string exe = (firstSpace > 0) ? cmd.Substring(0, firstSpace) : cmd;
                string args = (firstSpace > 0) ? cmd.Substring(firstSpace + 1) : "";
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) {
                    if (p != null) {
                        p.WaitForExit(1000);
                    }
                }
            } catch { }
        }

        // -----------------------------------------------------------------------------------------
        // ENTRY POINT
        // -----------------------------------------------------------------------------------------
        [STAThread]
        public static void Main(string[] args) {
            int initialTab = 0;
            if (args != null && args.Length > 0) {
                if (args[0] == "/test" || args[0] == "--test") {
                    Console.WriteLine("[TEST] Starting VectorPowerHubForm headless verification...");
                    using (VectorPowerHubForm form = new VectorPowerHubForm()) {
                        Thread.Sleep(1200);
                        form.OnTelemetryTick(null, EventArgs.Empty);
                        HubTelemetrySnapshot s = form.currentSnapshot;
                        Console.WriteLine("[TEST] Telemetry Snapshot:");
                        Console.WriteLine(string.Format("  In-Game FPS: {0:0.0}", s.Fps));
                        Console.WriteLine(string.Format("  CPU Package Power: {0:0.0} W (P-Core: {1:0.00} GHz | E-Core: {2:0.00} GHz)", s.CpuPowerW, s.PCoreGhz, s.ECoreGhz));
                        Console.WriteLine(string.Format("  GPU Dynamic Draw: {0:0.0} W (Clock: {1} MHz | Temp: {2} C | Util: {3}%)", s.GpuPowerW, s.GpuClockMhz, s.GpuTempC, s.GpuUtilPct));
                        Console.WriteLine(string.Format("  Total Platform Draw: {0:0.0} W / 215.0 W Ceiling", s.TotalPlatformPowerW));
                        Console.WriteLine(string.Format("  GPU Status Badge: {0}", s.GpuStatus));
                        Console.WriteLine(string.Format("  Display Attached: {0} ({1})", s.IsNvidiaDisplayAttached, s.NvidiaMonitorName));
                        Console.WriteLine(string.Format("  Active Profile: {0}", form.currentSelectedProfile));
                        Console.WriteLine("[TEST] VectorPowerHubForm verification completed successfully!");
                    }
                    return;
                }
                if (args[0] == "/render" || args[0] == "/screenshot") {
                    string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "previews");
                    if (!Directory.Exists(outDir)) { Directory.CreateDirectory(outDir); }
                    for (int t = 0; t <= 2; t++) {
                        using (VectorPowerHubForm form = new VectorPowerHubForm(t)) {
                            form.StartPosition = FormStartPosition.Manual;
                            form.Location = new Point(-2000, -2000);
                            form.Show();
                            Application.DoEvents();
                            Thread.Sleep(2200);
                            form.OnTelemetryTick(null, EventArgs.Empty);
                            Application.DoEvents();

                            Bitmap bmp = new Bitmap(form.Width, form.Height);
                            using (Graphics g = Graphics.FromImage(bmp)) {
                                g.Clear(VectorPowerHubForm.ColorBgMain);
                                RenderControlHierarchy(form, g, Point.Empty);
                            }
                            string outName = Path.Combine(outDir, string.Format("tab{0}_preview.png", t));
                            bmp.Save(outName, System.Drawing.Imaging.ImageFormat.Png);
                            Console.WriteLine(string.Format("[SCREENSHOT] Saved tab {0} to {1}", t, outName));
                            form.Close();
                        }
                    }
                    return;
                }
                if (args[0] == "/tab2" || args[0] == "/topology" || (args.Length > 1 && args[0] == "/tab" && args[1] == "2")) {
                    initialTab = 2;
                } else if (args[0] == "/tab1" || args[0] == "/bench" || (args.Length > 1 && args[0] == "/tab" && args[1] == "1")) {
                    initialTab = 1;
                }
            }
            bool createdNew = false;
            using (Mutex appMutex = new Mutex(true, "VectorPowerHub_SingleInstance_Mutex", out createdNew)) {
                if (!createdNew) {
                    // Another instance is already running!
                    // 1. Signal named EventWaitHandle to restore and activate primary instance
                    try {
                        using (EventWaitHandle wakeEvent = EventWaitHandle.OpenExisting("VectorPowerHub_WakeEvent")) {
                            wakeEvent.Set();
                        }
                    } catch { }

                    // 2. Broadcast registered window message
                    try {
                        if (WM_SHOW_HUB != 0) {
                            PostMessage((IntPtr)HWND_BROADCAST, WM_SHOW_HUB, IntPtr.Zero, IntPtr.Zero);
                        }
                    } catch { }

                    // 3. Bring existing process window to foreground
                    try {
                        Process current = Process.GetCurrentProcess();
                        foreach (Process p in Process.GetProcessesByName(current.ProcessName)) {
                            if (p.Id != current.Id) {
                                if (p.MainWindowHandle != IntPtr.Zero) {
                                    ShowWindow(p.MainWindowHandle, SW_RESTORE);
                                    SetForegroundWindow(p.MainWindowHandle);
                                }
                                break;
                            }
                        }
                    } catch { }

                    // Exit immediately without creating duplicate windows or tray icons
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new VectorPowerHubForm(initialTab));
                GC.KeepAlive(appMutex);
            }
        }

        private static void RenderControlHierarchy(Control parent, Graphics g, Point origin) {
            foreach (Control c in parent.Controls) {
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                Point p = new Point(origin.X + c.Left, origin.Y + c.Top);
                using (Bitmap childBmp = new Bitmap(c.Width, c.Height)) {
                    c.DrawToBitmap(childBmp, new Rectangle(0, 0, c.Width, c.Height));
                    g.DrawImage(childBmp, p);
                }
                if (c.Controls.Count > 0) {
                    RenderControlHierarchy(c, g, p);
                }
            }
        }
    }

    // =========================================================================================
    // SELF-PAINTING HUD CARD CONTROLS (Zero WinForms Label compositing bugs)
    // =========================================================================================

    public class FpsHeroCard : Control {
        private double fps = 0.0;
        private bool isGameMode = false;
        private string activeGameName = "";
        private int activeGamePid = 0;

        public FpsHeroCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double f, bool gameMode, string gameName, int pid) {
            this.fps = f;
            this.isGameMode = gameMode;
            this.activeGameName = gameName;
            this.activeGamePid = pid;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            // Clean background fill
            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            // Card background & border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            // Top Cyan Accent Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title
            using (Font fTitle = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                g.DrawString("IN-GAME FPS TELEMETRY", fTitle, bTitle, 14, 10);
            }

            // Monospace Jitter-Free Digits (Consolas 28pt Bold)
            string fpsStr = (isGameMode && fps > 0.1) ? fps.ToString("F1") : "0.0";
            Color numColor = (isGameMode && fps > 0.1) ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorTextWhite;
            using (Font fVal = new Font("Consolas", 28f, FontStyle.Bold))
            using (Brush bVal = new SolidBrush(numColor)) {
                g.DrawString(fpsStr, fVal, bVal, 14, 28);
                SizeF sz = g.MeasureString(fpsStr, fVal);

                using (Font fUnit = new Font("Segoe UI", 11f, FontStyle.Bold))
                using (Brush bUnit = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString("FPS", fUnit, bUnit, 14 + sz.Width - 4, 42);
                }
            }

            // Status Pill (Y = 76, H = 22)
            string pillText;
            Color pillBg, pillBorder, dotColor, textCol;
            if (isGameMode && !string.IsNullOrEmpty(activeGameName)) {
                string displayGame = (activeGameName.Length > 24) ? activeGameName.Substring(0, 22) + ".." : activeGameName;
                pillText = string.Format("Active Game: {0} (PID {1})", displayGame, activeGamePid);
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            } else {
                pillText = "Desktop Standby • SwapChain Idle";
                pillBg = Color.FromArgb(22, 26, 36);
                pillBorder = Color.FromArgb(36, 44, 60);
                dotColor = VectorPowerHubForm.ColorTextDim;
                textCol = VectorPowerHubForm.ColorTextMuted;
            }

            using (Font fPill = new Font("Segoe UI", 9f, FontStyle.Bold)) {
                SizeF pillSz = g.MeasureString(pillText, fPill);
                int pillW = (int)pillSz.Width + 28;
                Rectangle pillRect = new Rectangle(14, 76, pillW, 28);

                using (GraphicsPath pillPath = DarkCardPanel.GetRoundedPath(pillRect, 4)) {
                    using (Brush b = new SolidBrush(pillBg)) g.FillPath(b, pillPath);
                    using (Pen p = new Pen(pillBorder, 1f)) g.DrawPath(p, pillPath);
                }

                // Dot
                using (Brush bDot = new SolidBrush(dotColor)) {
                    g.FillEllipse(bDot, 22, 87, 7, 7);
                }
                // Text
                using (Brush bText = new SolidBrush(textCol)) {
                    g.DrawString(pillText, fPill, bText, 33, 83);
                }
            }

            // Subtitle
            using (Font fSub = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Brush bSub = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("DirectX DXGI SwapChain Hook (ETW Event 42)", fSub, bSub, 14, 116);
            }
        }
    }

    public class PlatformPowerCard : Control {
        private double cpuW = 0.0;
        private double gpuW = 0.0;
        private double totalW = 0.0;

        public PlatformPowerCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double cW, double gW, double tW) {
            this.cpuW = cW;
            this.gpuW = gW;
            this.totalW = tW;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            // Clean background fill
            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            // Card background & border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            // Purple Accent Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentPurple)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title Left
            using (Font fTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                g.DrawString("TOTAL PLATFORM DRAW (215W CEILING)", fTitle, bTitle, 16, 10);
            }

            // Monospace Jitter-Free Readout Right (Consolas 13pt Bold)
            string valStr = string.Format("{0:0.0} W / 215.0 W", totalW);
            Color valColor = (totalW > 215.0) ? VectorPowerHubForm.ColorAccentRed : ((totalW > 195.0) ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorAccentPurple);
            using (Font fVal = new Font("Consolas", 14f, FontStyle.Bold))
            using (Brush bVal = new SolidBrush(valColor)) {
                SizeF sz = g.MeasureString(valStr, fVal);
                g.DrawString(valStr, fVal, bVal, w - 16 - sz.Width, 8);
            }

            // Dual Power Bar (Y = 36, H = 22)
            int barX = 16;
            int barY = 36;
            int barW = w - 32;
            int barH = 22;
            Rectangle barRect = new Rectangle(barX, barY, barW, barH);

            using (GraphicsPath bPath = DarkCardPanel.GetRoundedPath(barRect, 4)) {
                using (Brush bBg = new SolidBrush(Color.FromArgb(18, 20, 26))) g.FillPath(bBg, bPath);
            }

            // Compute widths
            float maxW = 215.0f;
            float cpuPixels = (float)((cpuW / maxW) * barW);
            float gpuPixels = (float)((gpuW / maxW) * barW);
            if (cpuPixels + gpuPixels > barW) {
                float scale = barW / (cpuPixels + gpuPixels);
                cpuPixels *= scale;
                gpuPixels *= scale;
            }

            // Fill CPU (Amber Gold)
            if (cpuPixels > 1) {
                RectangleF rCpu = new RectangleF(barX, barY, cpuPixels, barH);
                using (Brush bCpu = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.FillRectangle(bCpu, rCpu);
                }
            }

            // Fill GPU (Electric Cyan)
            if (gpuPixels > 1) {
                RectangleF rGpu = new RectangleF(barX + cpuPixels, barY, gpuPixels, barH);
                using (Brush bGpu = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.FillRectangle(bGpu, rGpu);
                }
            }

            // Bar Border
            using (GraphicsPath bPath = DarkCardPanel.GetRoundedPath(barRect, 4)) {
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, bPath);
            }

            // Dashed 215W Ceiling Line
            using (Pen pCeil = new Pen(VectorPowerHubForm.ColorAccentRed, 1.5f)) {
                pCeil.DashStyle = DashStyle.Dash;
                g.DrawLine(pCeil, barX + barW - 1, barY - 2, barX + barW - 1, barY + barH + 2);
            }

            // Power Split Subtitle (Y = 66)
            double cpuPct = (totalW > 0.1) ? (cpuW / totalW) * 100.0 : 0.0;
            double gpuPct = (totalW > 0.1) ? (gpuW / totalW) * 100.0 : 0.0;
            string splitStr = string.Format("CPU Package: {0:0.0} W ({1:0.0}%)    |    GPU Dynamic Draw: {2:0.0} W ({3:0.0}%)", cpuW, cpuPct, gpuW, gpuPct);
            using (Font fSplit = new Font("Segoe UI", 8.25f, FontStyle.Bold))
            using (Brush bSplit = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                g.DrawString(splitStr, fSplit, bSplit, 16, 66);
            }

            // Dynamic Status Pill (Y = 92, H = 22)
            string pText;
            Color pBg, pBorder, pDot, pTextCol;
            if (totalW > 215.0) {
                pText = "⚡ 215W MAXIMUM PLATFORM CEILING EXCEEDED";
                pBg = Color.FromArgb(48, 18, 18);
                pBorder = Color.FromArgb(96, 32, 32);
                pDot = VectorPowerHubForm.ColorAccentRed;
                pTextCol = VectorPowerHubForm.ColorAccentRed;
            } else if (totalW > 195.0) {
                pText = "⚡ PEAK DYNAMIC BOOST ACTIVE (FULL 140W TGP ALLOCATED)";
                pBg = Color.FromArgb(45, 32, 12);
                pBorder = Color.FromArgb(90, 60, 20);
                pDot = VectorPowerHubForm.ColorAccentGold;
                pTextCol = VectorPowerHubForm.ColorAccentGold;
            } else {
                double headroom = Math.Max(0.0, 215.0 - totalW);
                pText = string.Format("⚡ BALANCED LOAD • {0:0.0}W DYNAMIC BOOST HEADROOM VERIFIED", headroom);
                pBg = Color.FromArgb(16, 32, 42);
                pBorder = Color.FromArgb(24, 60, 80);
                pDot = VectorPowerHubForm.ColorAccentCyan;
                pTextCol = VectorPowerHubForm.ColorAccentCyan;
            }

            using (Font fP = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                SizeF pSz = g.MeasureString(pText, fP);
                int pW = (int)pSz.Width + 28;
                Rectangle pRect = new Rectangle(16, 92, pW, 26);

                using (GraphicsPath pPath = DarkCardPanel.GetRoundedPath(pRect, 4)) {
                    using (Brush b = new SolidBrush(pBg)) g.FillPath(b, pPath);
                    using (Pen p = new Pen(pBorder, 1f)) g.DrawPath(p, pPath);
                }
                using (Brush bDot = new SolidBrush(pDot)) g.FillEllipse(bDot, 24, 101, 6, 6);
                using (Brush bText = new SolidBrush(pTextCol)) g.DrawString(pText, fP, bText, 34, 97);
            }
        }
    }

    public class CpuTelemetryCard : Control {
        private double cpuPowerW = 0.0;
        private double pCoreGhz = 0.0;
        private double eCoreGhz = 0.0;
        public GlowButton BtnView24Cores { get; private set; }

        public CpuTelemetryCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;

            BtnView24Cores = new GlowButton();
            BtnView24Cores.Text = "◆ 24 CORES VIEW";
            BtnView24Cores.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            BtnView24Cores.ButtonColor = Color.FromArgb(22, 26, 36);
            BtnView24Cores.BorderColor = VectorPowerHubForm.ColorAccentGold;
            BtnView24Cores.TextColor = VectorPowerHubForm.ColorAccentGold;
            BtnView24Cores.Size = new Size(145, 24);
            this.Controls.Add(BtnView24Cores);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double pwrW, double pGhz, double eGhz) {
            this.cpuPowerW = pwrW;
            this.pCoreGhz = pGhz;
            this.eCoreGhz = eGhz;
            this.Invalidate();
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (BtnView24Cores != null) {
                BtnView24Cores.Location = new Point(this.Width - 158, this.Height - 34);
            }
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            // Clean background fill
            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            // Background & Border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            // Gold Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title
            using (Font fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                g.DrawString("INTEL CORE ULTRA 9 275HX", fTitle, bTitle, 14, 12);
            }

            // Subtitle
            using (Font fSub = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bSub = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("24 Cores (8 Lion Cove P + 16 Skymont E) • RAPL Package Sensor", fSub, bSub, 14, 32);
            }

            // 3 Columns Metrics with Monospace Consolas figures
            int colW = (w - 28) / 3;

            using (Font fVal = new Font("Consolas", 14f, FontStyle.Bold))
            using (Font fSubL = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Brush bSubL = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                // Col 0: Power
                using (Brush bVal0 = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString(string.Format("{0:0.0} W", cpuPowerW), fVal, bVal0, 14, 56);
                }
                g.DrawString("Package Power", fSubL, bSubL, 14, 80);

                // Col 1: P-Core
                using (Brush bVal1 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0:0.00} GHz", pCoreGhz), fVal, bVal1, 14 + colW, 56);
                }
                g.DrawString("Avg P-Core", fSubL, bSubL, 14 + colW, 80);

                // Col 2: E-Core
                using (Brush bVal2 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0:0.00} GHz", eCoreGhz), fVal, bVal2, 14 + colW * 2, 56);
                }
                g.DrawString("Avg E-Core", fSubL, bSubL, 14 + colW * 2, 80);
            }

            // Power Progress Bar (Y = 106, H = 6)
            int pBarW = w - 28;
            Rectangle barR = new Rectangle(14, 106, pBarW, 6);
            using (GraphicsPath bp = DarkCardPanel.GetRoundedPath(barR, 2)) {
                using (Brush bg = new SolidBrush(Color.FromArgb(18, 20, 26))) g.FillPath(bg, bp);
            }
            float fillPct = Math.Min(1.0f, Math.Max(0.0f, (float)(cpuPowerW / 75.0)));
            int fillW = (int)(pBarW * fillPct);
            if (fillW > 2) {
                Rectangle fR = new Rectangle(14, 106, fillW, 6);
                using (GraphicsPath fp = DarkCardPanel.GetRoundedPath(fR, 2)) {
                    using (Brush fb = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) g.FillPath(fb, fp);
                }
            }

            // Bottom Target Hint
            using (Font fHint = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bHint = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("Dynamic Target: 55W-75W Max Boost Headroom", fHint, bHint, 14, 126);
            }
        }
    }

    public class GpuTelemetryCard : Control {
        private double gpuPowerW = 0.0;
        private int gpuClockMhz = 0;
        private int gpuTempC = 0;
        private int gpuUtilPct = 0;
        private bool isGameMode = false;
        private bool isNvidiaDisplayAttached = false;
        private string nvidiaMonitorName = "";
        private string gpuStatus = "";

        public GpuTelemetryCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double pwrW, int clkMhz, int tempC, int utilPct, bool gameMode, bool displayAttached, string monitorName, string status) {
            this.gpuPowerW = pwrW;
            this.gpuClockMhz = clkMhz;
            this.gpuTempC = tempC;
            this.gpuUtilPct = utilPct;
            this.isGameMode = gameMode;
            this.isNvidiaDisplayAttached = displayAttached;
            this.nvidiaMonitorName = monitorName;
            this.gpuStatus = status;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            // Clean background fill
            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            // Background & Border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            // Cyan Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title
            using (Font fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                g.DrawString("NVIDIA GEFORCE RTX 5070 MOBILE", fTitle, bTitle, 14, 12);
            }

            // Subtitle
            using (Font fSub = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bSub = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("140W Max Dynamic TGP • Active Display D3cold Safety", fSub, bSub, 14, 32);
            }

            // 4 Columns Metrics with Monospace Consolas figures
            int colW = (w - 28) / 4;

            using (Font fVal = new Font("Consolas", 14f, FontStyle.Bold))
            using (Font fSubL = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Brush bSubL = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                // Col 0: Power
                using (Brush bVal0 = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString(string.Format("{0:0.0} W", gpuPowerW), fVal, bVal0, 14, 56);
                }
                g.DrawString("Dynamic TGP", fSubL, bSubL, 14, 80);

                // Col 1: Clock
                using (Brush bVal1 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0} MHz", gpuClockMhz), fVal, bVal1, 14 + colW, 56);
                }
                g.DrawString("Core Clock", fSubL, bSubL, 14 + colW, 80);

                // Col 2: Temp
                string tempStr = (gpuTempC > 0) ? string.Format("{0} °C", gpuTempC) : "-- °C";
                using (Brush bVal2 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(tempStr, fVal, bVal2, 14 + colW * 2, 56);
                }
                g.DrawString("Hotspot Temp", fSubL, bSubL, 14 + colW * 2, 80);

                // Col 3: Util
                using (Brush bVal3 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0}%", gpuUtilPct), fVal, bVal3, 14 + colW * 3, 56);
                }
                g.DrawString("GPU Load", fSubL, bSubL, 14 + colW * 3, 80);
            }

            // Accurate GPU Status Pill (Y = 114, H = 26)
            string pillText;
            Color pillBg, pillBorder, dotColor, textCol;

            if (isNvidiaDisplayAttached) {
                string mon = string.IsNullOrEmpty(nvidiaMonitorName) ? "BenQ EX2710Q" : nvidiaMonitorName;
                pillText = string.Format("Active (D0) • Driving {0}", mon);
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            } else if (isGameMode || gpuPowerW > 25.0) {
                pillText = "Active Rendering (D0) • Full 140W Dynamic Headroom";
                pillBg = Color.FromArgb(42, 32, 14);
                pillBorder = Color.FromArgb(88, 64, 22);
                dotColor = VectorPowerHubForm.ColorAccentGold;
                textCol = VectorPowerHubForm.ColorAccentGold;
            } else if (!string.IsNullOrEmpty(gpuStatus) && gpuStatus.Contains("D3cold")) {
                pillText = gpuStatus;
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            } else {
                pillText = string.IsNullOrEmpty(gpuStatus) ? "D3cold Sleeping (0.0W) • PCIe Link Off" : gpuStatus;
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            }

            using (Font fPill = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                SizeF pillSz = g.MeasureString(pillText, fPill);
                int pillW = (int)pillSz.Width + 28;
                Rectangle pillRect = new Rectangle(14, 114, pillW, 26);

                using (GraphicsPath pillPath = DarkCardPanel.GetRoundedPath(pillRect, 4)) {
                    using (Brush b = new SolidBrush(pillBg)) g.FillPath(b, pillPath);
                    using (Pen p = new Pen(pillBorder, 1f)) g.DrawPath(p, pillPath);
                }

                // Dot
                using (Brush bDot = new SolidBrush(dotColor)) {
                    g.FillEllipse(bDot, 22, 124, 6, 6);
                }
                // Text
                using (Brush bText = new SolidBrush(textCol)) {
                    g.DrawString(pillText, fPill, bText, 32, 119);
                }
            }
        }
    }

    // =========================================================================================
    // CUSTOM GDI+ CONTROLS
    // =========================================================================================

    public class DarkCardPanel : Panel {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public Color AccentStripeColor { get; set; }
        public bool DrawAccentStripe { get; set; }
        public int CornerRadius { get; set; }

        public DarkCardPanel() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.BorderColor = VectorPowerHubForm.ColorBorder;
            this.FillColor = VectorPowerHubForm.ColorCardBg;
            this.AccentStripeColor = VectorPowerHubForm.ColorAccentCyan;
            this.DrawAccentStripe = false;
            this.CornerRadius = 8;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, this.Width, this.Height);
            }

            using (GraphicsPath path = GetRoundedPath(rect, CornerRadius)) {
                using (Brush b = new SolidBrush(FillColor)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(BorderColor, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            if (DrawAccentStripe) {
                using (Brush b = new SolidBrush(AccentStripeColor)) {
                    g.FillRectangle(b, 10, 0, this.Width - 20, 2);
                }
            }
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius) {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class ProfileCard : Control {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public Color AccentColor { get; set; }
        public string[] Specs { get; set; }
        public string ButtonText { get; set; }
        public bool IsActive { get; set; }
        private bool isHovered = false;

        public event EventHandler ProfileClicked;

        public ProfileCard(string title, string subtitle, Color accentColor, string[] specs, string buttonText, bool isActive) {
            this.Title = title;
            this.Subtitle = subtitle;
            this.AccentColor = accentColor;
            this.Specs = specs;
            this.ButtonText = buttonText;
            this.IsActive = isActive;

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.Cursor = Cursors.Hand;

            this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; this.Invalidate(); };
            this.Click += (s, e) => {
                if (ProfileClicked != null) ProfileClicked(this, EventArgs.Empty);
            };
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            Color fill = IsActive ? VectorPowerHubForm.ColorCardSelected : (isHovered ? VectorPowerHubForm.ColorCardHover : VectorPowerHubForm.ColorCardBg);
            Color border = IsActive ? AccentColor : (isHovered ? VectorPowerHubForm.ColorBorderHighlight : VectorPowerHubForm.ColorBorder);
            float borderThickness = IsActive ? 2f : 1f;

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 8)) {
                using (Brush b = new SolidBrush(fill)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, borderThickness)) {
                    g.DrawPath(p, path);
                }
            }

            using (Brush b = new SolidBrush(AccentColor)) {
                g.FillRectangle(b, 12, 0, w - 24, IsActive ? 3 : 2);
            }

            using (Font fTitle = new Font("Segoe UI", 11.5f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(AccentColor)) {
                    g.DrawString(Title, fTitle, b, 14, 12);
                }
            }

            using (Font fSub = new Font("Segoe UI", 7.5f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                    g.DrawString(Subtitle, fSub, b, 16, 36);
                }
            }

            // Specs with responsive vertical spacing to prevent clipping
            Rectangle btnRect = new Rectangle(14, h - 38, w - 28, 26);
            int availableHeight = btnRect.Top - 60;
            int lineSpacing = Math.Min(22, Math.Max(16, availableHeight / Math.Max(1, Specs.Length)));
            int specY = 58;

            using (Font fSpec = new Font("Segoe UI", 8.25f, FontStyle.Regular)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    for (int i = 0; i < Specs.Length; i++) {
                        g.DrawString(Specs[i], fSpec, b, 14, specY);
                        specY += lineSpacing;
                    }
                }
            }

            using (GraphicsPath btnPath = DarkCardPanel.GetRoundedPath(btnRect, 4)) {
                if (IsActive) {
                    using (Brush b = new SolidBrush(AccentColor)) {
                        g.FillPath(b, btnPath);
                    }
                    using (Font fBtn = new Font("Segoe UI", 9f, FontStyle.Bold)) {
                        using (Brush b = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                            StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString("✔ ACTIVE PROFILE", fBtn, b, btnRect, sf);
                        }
                    }
                } else {
                    using (Brush b = new SolidBrush(Color.FromArgb(20, 24, 32))) {
                        g.FillPath(b, btnPath);
                    }
                    using (Pen p = new Pen(isHovered ? AccentColor : VectorPowerHubForm.ColorBorder, 1f)) {
                        g.DrawPath(p, btnPath);
                    }
                    using (Font fBtn = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                        using (Brush b = new SolidBrush(isHovered ? AccentColor : VectorPowerHubForm.ColorTextMuted)) {
                            StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                            g.DrawString(ButtonText, fBtn, b, btnRect, sf);
                        }
                    }
                }
            }
        }
    }

    public class GlowButton : Button {
        public Color ButtonColor { get; set; }
        public Color BorderColor { get; set; }
        public Color TextColor { get; set; }
        private bool isHovered = false;

        public GlowButton() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.ButtonColor = VectorPowerHubForm.ColorCardBg;
            this.BorderColor = VectorPowerHubForm.ColorBorder;
            this.TextColor = VectorPowerHubForm.ColorTextWhite;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; this.Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (this.Parent != null) {
                using (Brush bP = new SolidBrush(this.Parent.BackColor)) {
                    g.FillRectangle(bP, 0, 0, this.Width, this.Height);
                }
            } else {
                using (Brush bP = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                    g.FillRectangle(bP, 0, 0, this.Width, this.Height);
                }
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            Color fill = !this.Enabled ? Color.FromArgb(25, 27, 35) : (isHovered ? Color.FromArgb(40, 46, 62) : ButtonColor);
            Color border = !this.Enabled ? Color.FromArgb(45, 50, 66) : (isHovered ? TextColor : BorderColor);
            Color textC = !this.Enabled ? Color.FromArgb(80, 90, 110) : TextColor;

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 4)) {
                using (Brush b = new SolidBrush(fill)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, 1.2f)) {
                    g.DrawPath(p, path);
                }
            }

            StringFormat sf = new StringFormat() {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Brush b = new SolidBrush(textC)) {
                g.DrawString(this.Text, this.Font, b, rect, sf);
            }
        }
    }

    public class BenchmarkProgressBar : Control {
        private int currentVal = 0;
        public int Value {
            get { return currentVal; }
            set { currentVal = Math.Max(0, Math.Min(100, value)); this.Invalidate(); }
        }

        public BenchmarkProgressBar() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;

            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 3)) {
                using (Brush b = new SolidBrush(Color.FromArgb(14, 16, 22))) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            int fillW = (int)((currentVal / 100.0) * (w - 4));
            if (fillW > 2) {
                Rectangle fillRect = new Rectangle(2, 2, fillW, h - 4);
                using (GraphicsPath fillPath = DarkCardPanel.GetRoundedPath(fillRect, 2)) {
                    using (LinearGradientBrush lgb = new LinearGradientBrush(fillRect, VectorPowerHubForm.ColorAccentPurple, VectorPowerHubForm.ColorAccentCyan, 0f)) {
                        g.FillPath(lgb, fillPath);
                    }
                }
            }
        }
    }

    // BenchmarkResultsGrid: Clean dark table with Monospace Consolas digits and Raw vs Cleaned comparisons
    public class BenchmarkResultsGrid : Control {
        private List<BenchmarkResultInfo> results = new List<BenchmarkResultInfo>();

        public BenchmarkResultsGrid() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void ClearResults() {
            results.Clear();
            this.Invalidate();
        }

        public void AddOrUpdateResult(BenchmarkResultInfo info) {
            for (int i = 0; i < results.Count; i++) {
                if (results[i].ProfileId == info.ProfileId) {
                    results[i] = info;
                    this.Invalidate();
                    return;
                }
            }
            results.Add(info);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) {
                g.FillRectangle(b, rect);
            }
            using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                g.DrawRectangle(p, rect);
            }

            // Table Header Row (Height: 26px)
            using (Brush b = new SolidBrush(Color.FromArgb(24, 28, 38))) {
                g.FillRectangle(b, 1, 1, w - 2, 26);
            }
            using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                g.DrawLine(p, 1, 27, w - 2, 27);
            }

            // Proportional column coordinates
            int colRank = 12;
            int colName = 65;
            int colCleanFps = (int)(w * 0.28);
            int colRawFps = (int)(w * 0.39);
            int colCleanLow = (int)(w * 0.49);
            int colRawLow = (int)(w * 0.59);
            int colCpuW = (int)(w * 0.69);
            int colGpuW = (int)(w * 0.77);
            int colOutliers = (int)(w * 0.84);
            int colEff = (int)(w * 0.92);

            string[] headers = new string[] { "RANK", "PROFILE NAME", "CLEAN AVG", "RAW AVG", "CLEAN 1%", "RAW 1%", "CPU W", "GPU W", "OUTLIERS", "EFFICIENCY" };
            int[] colPositions = new int[] { colRank, colName, colCleanFps, colRawFps, colCleanLow, colRawLow, colCpuW, colGpuW, colOutliers, colEff };

            using (Font fHead = new Font("Segoe UI", 8f, FontStyle.Bold)) {
                using (Brush bHead = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                    for (int i = 0; i < headers.Length; i++) {
                        g.DrawString(headers[i], fHead, bHead, colPositions[i], 6);
                    }
                }
            }

            // Rows
            if (results.Count == 0) {
                using (Font fEmpty = new Font("Segoe UI", 8.5f, FontStyle.Italic)) {
                    using (Brush bEmpty = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                        g.DrawString("No benchmark runs recorded yet. Click 'Run Profile Benchmark' above to execute.", fEmpty, bEmpty, 20, 50);
                    }
                }
                return;
            }

            int rowY = 32;
            int rowHeight = 24;
            using (Font fRow = new Font("Segoe UI", 8f, FontStyle.Regular))
            using (Font fDigits = new Font("Consolas", 8.5f, FontStyle.Regular))
            using (Font fBoldDigits = new Font("Consolas", 8.5f, FontStyle.Bold))
            using (Font fBold = new Font("Segoe UI", 8f, FontStyle.Bold)) {
                for (int i = 0; i < results.Count; i++) {
                    BenchmarkResultInfo r = results[i];

                    // Row background highlight if winner
                    if (r.IsWinner) {
                        using (Brush bWin = new SolidBrush(Color.FromArgb(38, 30, 12))) {
                            g.FillRectangle(bWin, 2, rowY - 2, w - 4, rowHeight);
                        }
                        using (Pen pWin = new Pen(VectorPowerHubForm.ColorAccentGold, 1f)) {
                            g.DrawRectangle(pWin, 2, rowY - 2, w - 4, rowHeight);
                        }
                    }

                    // Rank
                    string rankStr = string.Format("#{0}", i + 1);
                    Color rankColor = VectorPowerHubForm.ColorTextWhite;
                    if (r.IsWinner) {
                        rankStr = "🏆 1st";
                        rankColor = VectorPowerHubForm.ColorAccentGold;
                    }
                    using (Brush b = new SolidBrush(rankColor)) {
                        g.DrawString(rankStr, fBold, b, colRank, rowY);
                    }

                    // Profile Name
                    using (Brush b = new SolidBrush(r.IsWinner ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorTextWhite)) {
                        g.DrawString(r.ProfileName, fBold, b, colName, rowY);
                    }

                    // Cleaned Avg FPS (Consolas Bold)
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                        g.DrawString(string.Format("{0:0.0} FPS", r.CleanedAvgFps), fBoldDigits, b, colCleanFps, rowY);
                    }

                    // Raw Avg FPS (Consolas Regular)
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                        g.DrawString(string.Format("{0:0.0} FPS", r.RawAvgFps), fDigits, b, colRawFps, rowY);
                    }

                    // Cleaned 1% Lows (Consolas Bold)
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                        g.DrawString(string.Format("{0:0.0} FPS", r.CleanedOnePercentLow), fBoldDigits, b, colCleanLow, rowY);
                    }

                    // Raw 1% Lows (Consolas Regular)
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                        g.DrawString(string.Format("{0:0.0} FPS", r.RawOnePercentLow), fDigits, b, colRawLow, rowY);
                    }

                    // CPU Watts
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                        g.DrawString(string.Format("{0:0.0} W", r.AvgCpuPowerW), fDigits, b, colCpuW, rowY);
                    }

                    // GPU Watts
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                        g.DrawString(string.Format("{0:0.0} W", r.AvgGpuPowerW), fDigits, b, colGpuW, rowY);
                    }

                    // Outliers Filtered
                    Color outColor = (r.OutliersFilteredCount > 0) ? VectorPowerHubForm.ColorAccentRed : VectorPowerHubForm.ColorAccentGreen;
                    using (Brush b = new SolidBrush(outColor)) {
                        string outStr = (r.OutliersFilteredCount > 0) ? string.Format("{0} ({1})", r.OutliersFilteredCount, r.OutlierDetails) : "0 (Clean)";
                        g.DrawString(outStr, fRow, b, colOutliers, rowY);
                    }

                    // Efficiency Score
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentPurple)) {
                        g.DrawString(string.Format("{0:0.00} FPS/W", r.EfficiencyScore), fDigits, b, colEff, rowY);
                    }

                    rowY += rowHeight;
                }
            }
        }
    }

    // =========================================================================================
    // PER-CORE TOPOLOGY CONTROL (24 Physical Cores: 8 Lion Cove P-Cores + 16 Skymont E-Cores)
    // =========================================================================================
    public class PerCoreTopologyControl : Control {
        private double[] coreGhz = new double[24];
        private double[] coreUtil = new double[24];
        private double pkgPowerW = 0.0;

        public PerCoreTopologyControl() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(18, 19, 24); // Force dark slate — prevents white flash
            for (int i = 0; i < 24; i++) {
                coreGhz[i] = (i < 8) ? 2.7 : 2.1;
                coreUtil[i] = 0.0;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void SetCoreData(double[] ghz, double[] util, double pkgW) {
            if (ghz != null && ghz.Length == 24) {
                Array.Copy(ghz, this.coreGhz, 24);
            }
            if (util != null && util.Length == 24) {
                Array.Copy(util, this.coreUtil, 24);
            }
            this.pkgPowerW = pkgW;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;

            // Fill entire control surface with dark slate — prevents default white system Control background bleed-through
            using (Brush bgFill = new SolidBrush(Color.FromArgb(18, 19, 24))) {
                g.FillRectangle(bgFill, 0, 0, w, h);
            }

            // 1. Top Summary Strip (H = 48)
            int sumH = 48;
            Rectangle sumRect = new Rectangle(0, 0, w - 1, sumH);
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(sumRect, 6)) {
                using (Brush b = new SolidBrush(Color.FromArgb(22, 25, 33))) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            // Find peak core
            int peakCoreIdx = 0;
            double peakGhz = 0.0;
            double pSum = 0;
            double eSum = 0;
            for (int i = 0; i < 24; i++) {
                if (coreGhz[i] > peakGhz) { peakGhz = coreGhz[i]; peakCoreIdx = i; }
                if (i < 8) pSum += coreGhz[i];
                else eSum += coreGhz[i];
            }
            double avgPGhz = pSum / 8.0;
            double avgEGhz = eSum / 16.0;

            int col1X = 14;
            int col2X = (w - 28) / 4 + 14;
            int col3X = ((w - 28) / 4) * 2 + 14;
            int col4X = ((w - 28) / 4) * 3 + 14;

            using (Font fLabel = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (Font fVal = new Font("Consolas", 12f, FontStyle.Bold)) {
                // Col 1: Peak Core
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString("⚡ PEAK BURST", fLabel, bL, col1X, 6);
                    string peakStr = string.Format("{0}-{1} ({2:0.00} GHz)", (peakCoreIdx < 8 ? "P" : "E"), (peakCoreIdx < 8 ? peakCoreIdx : peakCoreIdx - 8), peakGhz);
                    g.DrawString(peakStr, fVal, bV, col1X, 22);
                }

                // Col 2: P-Core Cluster Avg
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString("◆ P-CORES (8C)", fLabel, bL, col2X, 6);
                    g.DrawString(string.Format("{0:0.00} GHz Avg", avgPGhz), fVal, bV, col2X, 22);
                }

                // Col 3: E-Core Cluster Avg
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString("✦ E-CORES (16C)", fLabel, bL, col3X, 6);
                    g.DrawString(string.Format("{0:0.00} GHz Avg", avgEGhz), fVal, bV, col3X, 22);
                }

                // Col 4: RAPL CPU Package Draw
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentPurple)) {
                    g.DrawString("⚡ RAPL POWER", fLabel, bL, col4X, 6);
                    g.DrawString(string.Format("{0:0.0} W", pkgPowerW), fVal, bV, col4X, 22);
                }
            }

            // 2. Section 1: Performance Cores Cluster (P0 to P7 - 1 Row of 8 Cores)
            int pSecY = sumH + 8;
            using (Font fSec = new Font("Segoe UI", 9.5f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString("PERFORMANCE CORES — 8 LION COVE CORES (P0 – P7 • UP TO 5.5 GHz PEAK)", fSec, b, 4, pSecY);
                }
            }

            int pCardsTop = pSecY + 20;
            int pCols = 8;
            int pGap = 6;
            int pCardW = (w - (pCols - 1) * pGap) / pCols;
            int pCardH = 48;

            for (int i = 0; i < 8; i++) {
                int cx = i * (pCardW + pGap);
                int cy = pCardsTop;
                DrawCoreCard(g, cx, cy, pCardW, pCardH, string.Format("P-Core {0}", i), "Lion Cove", coreGhz[i], coreUtil[i], true);
            }

            // 3. Section 2: Efficient Cores Cluster (E0 to E15 - 2 Rows of 8 Cores)
            int eSecY = pCardsTop + pCardH + 8;
            using (Font fSec = new Font("Segoe UI", 9.5f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString("EFFICIENT CORES — 16 SKYMONT CORES (E00 – E15 • UP TO 4.0 GHz PEAK)", fSec, b, 4, eSecY);
                }
            }

            int eCardsTop = eSecY + 20;
            int eCols = 8;
            int eGap = 6;
            int eCardW = (w - (eCols - 1) * eGap) / eCols;
            int eCardH = 44;

            for (int i = 0; i < 16; i++) {
                int col = i % eCols;
                int row = i / eCols;
                int cx = col * (eCardW + eGap);
                int cy = eCardsTop + row * (eCardH + eGap);
                DrawCoreCard(g, cx, cy, eCardW, eCardH, string.Format("E{0:00}", i), "Skymont", coreGhz[8 + i], coreUtil[8 + i], false);
            }
        }

        private void DrawCoreCard(Graphics g, int x, int y, int w, int h, string name, string arch, double ghz, double util, bool isPCore) {
            Rectangle rect = new Rectangle(x, y, w - 1, h - 1);
            Color fill = Color.FromArgb(24, 27, 35);
            Color border = isPCore ? Color.FromArgb(45, 60, 80) : Color.FromArgb(45, 45, 55);
            Color accent = isPCore ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorAccentGold;

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 4)) {
                using (Brush b = new SolidBrush(fill)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            // Left stripe
            using (Brush b = new SolidBrush(accent)) {
                g.FillRectangle(b, x, y + 4, 3, h - 8);
            }

            // Header Name
            using (Font fName = new Font("Segoe UI", isPCore ? 8.25f : 8f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                    g.DrawString(name, fName, b, x + 7, y + 4);
                }
            }

            // Monospace Live GHz (Consolas Bold)
            using (Font fGhz = new Font("Consolas", isPCore ? 11f : 10f, FontStyle.Bold)) {
                Color ghzColor = isPCore
                    ? (ghz >= 4.8 ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorTextWhite)
                    : (ghz >= 3.6 ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorTextWhite);
                using (Brush b = new SolidBrush(ghzColor)) {
                    g.DrawString(string.Format("{0:0.00} GHz", ghz), fGhz, b, x + 7, y + 18);
                }
            }

            // Mini Load Bar
            int barY = y + h - 6;
            int barW = w - 14;
            int fillW = (int)((Math.Min(100.0, Math.Max(0.0, util)) / 100.0) * barW);

            using (Brush b = new SolidBrush(Color.FromArgb(16, 18, 24))) {
                g.FillRectangle(b, x + 7, barY, barW, 2);
            }
            if (fillW > 0) {
                using (Brush b = new SolidBrush(accent)) {
                    g.FillRectangle(b, x + 7, barY, fillW, 2);
                }
            }
        }
    }

    // =========================================================================================
    // POWER CORE ENGINE BRIDGE (RESILIENT ADAPTER & DIRECT CONNECT)
    // =========================================================================================
    public class PowerCoreBridge : IDisposable {
        private object engineInstance = null;
        private MethodInfo applyProfileMethod = null;
        private MethodInfo applyCustomMethod = null;
        private PropertyInfo currentSnapshotProp = null;
        private bool isEngineLoaded = false;

        // Fallback PDH counter handles (when running standalone)
        private IntPtr pdhQuery = IntPtr.Zero;
        private IntPtr pwrCounter = IntPtr.Zero;
        private IntPtr pcoreCounter = IntPtr.Zero;
        private IntPtr ecoreCounter = IntPtr.Zero;

        [StructLayout(LayoutKind.Explicit)]
        private struct PDH_FMT_COUNTERVALUE_DOUBLE {
            [FieldOffset(0)]
            public uint CStatus;
            [FieldOffset(8)]
            public double doubleValue;
        }

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQueryW(string szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern uint PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, IntPtr lpdwType, out PDH_FMT_COUNTERVALUE_DOUBLE pValue);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern uint PdhCloseQuery(IntPtr hQuery);

        private const uint PDH_FMT_DOUBLE = 0x00000200;

        public PowerCoreBridge() {
            TryConnectEngine();
            if (!isEngineLoaded) {
                InitPdhFallback();
            }
        }

        private void TryConnectEngine() {
            try {
                Type engineType = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    engineType = asm.GetType("PowerCoreEngine");
                    if (engineType == null) engineType = asm.GetType("VectorPowerHub.PowerCoreEngine");
                    if (engineType != null) break;
                }

                if (engineType != null) {
                    PropertyInfo instanceProp = engineType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) {
                        engineInstance = instanceProp.GetValue(null, null);
                    }
                    if (engineInstance == null) {
                        engineInstance = Activator.CreateInstance(engineType);
                    }

                    applyProfileMethod = engineType.GetMethod("ApplyProfile");
                    applyCustomMethod = engineType.GetMethod("ApplyCustomProfile");
                    currentSnapshotProp = engineType.GetProperty("CurrentSnapshot");

                    MethodInfo startMethod = engineType.GetMethod("Start");
                    if (startMethod != null && engineInstance != null) {
                        startMethod.Invoke(engineInstance, null);
                    }

                    isEngineLoaded = (engineInstance != null);
                }
            } catch {
                isEngineLoaded = false;
            }
        }

        private void InitPdhFallback() {
            try {
                if (PdhOpenQueryW(null, IntPtr.Zero, out pdhQuery) == 0) {
                    PdhAddEnglishCounterW(pdhQuery, @"\Energy Meter(RAPL_Package0_PKG)\Power", IntPtr.Zero, out pwrCounter);
                    PdhAddEnglishCounterW(pdhQuery, @"\Processor Information(0,0)\% Processor Performance", IntPtr.Zero, out pcoreCounter);
                    PdhAddEnglishCounterW(pdhQuery, @"\Processor Information(0,14)\% Processor Performance", IntPtr.Zero, out ecoreCounter);
                    PdhCollectQueryData(pdhQuery);
                }
            } catch { }
        }

        public HubTelemetrySnapshot GetSnapshot() {
            HubTelemetrySnapshot snapshot = new HubTelemetrySnapshot();

            // 1. Try reading from PowerCoreEngine if available
            if (isEngineLoaded && currentSnapshotProp != null && engineInstance != null) {
                try {
                    object snapObj = currentSnapshotProp.GetValue(engineInstance, null);
                    if (snapObj != null) {
                        Type t = snapObj.GetType();
                        snapshot.Fps = ReadDouble(t, snapObj, "Fps");
                        snapshot.CpuPowerW = ReadDouble(t, snapObj, "CpuPowerW");
                        snapshot.PCoreGhz = ReadDouble(t, snapObj, "PCoreGhz");
                        snapshot.ECoreGhz = ReadDouble(t, snapObj, "ECoreGhz");
                        snapshot.GpuPowerW = ReadDouble(t, snapObj, "GpuPowerW");
                        snapshot.GpuClockMhz = ReadInt(t, snapObj, "GpuClockMhz");
                        snapshot.GpuTempC = ReadInt(t, snapObj, "GpuTempC");
                        snapshot.GpuUtilPct = ReadInt(t, snapObj, "GpuUtilPct");
                        snapshot.IsGameMode = ReadBool(t, snapObj, "IsGameMode");
                        snapshot.ActiveGameName = ReadString(t, snapObj, "ActiveGameName");
                        snapshot.ActiveGamePid = ReadInt(t, snapObj, "ActiveGamePid");
                        snapshot.GpuStatus = ReadString(t, snapObj, "GpuStatus");
                        snapshot.PerCoreGhz = ReadDoubleArray(t, snapObj, "PerCoreGhz");
                        snapshot.PerCoreUtil = ReadDoubleArray(t, snapObj, "PerCoreUtil");
                        snapshot.IsNvidiaDisplayAttached = ReadBool(t, snapObj, "IsNvidiaDisplayAttached");
                        snapshot.NvidiaMonitorName = ReadString(t, snapObj, "NvidiaMonitorName");
                        return snapshot;
                    }
                } catch { }
            }

            // 2. Fallback Standalone Telemetry Sampling
            try {
                string fpsFile = @"C:\Users\a7god\game_fps.txt";
                if (File.Exists(fpsFile)) {
                    string txt = File.ReadAllText(fpsFile).Trim();
                    double fVal;
                    if (double.TryParse(txt, out fVal)) {
                        snapshot.Fps = fVal;
                        snapshot.IsGameMode = (fVal > 0.1);
                    }
                }
            } catch { }

            if (pdhQuery != IntPtr.Zero) {
                try {
                    PdhCollectQueryData(pdhQuery);
                    PDH_FMT_COUNTERVALUE_DOUBLE val;
                    if (PdhGetFormattedCounterValue(pwrCounter, PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        snapshot.CpuPowerW = val.doubleValue / 1000.0;
                    }
                    if (PdhGetFormattedCounterValue(pcoreCounter, PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        snapshot.PCoreGhz = (val.doubleValue / 100.0) * 2.7;
                    }
                    if (PdhGetFormattedCounterValue(ecoreCounter, PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        snapshot.ECoreGhz = (val.doubleValue / 100.0) * 2.2;
                    }
                } catch { }
            }

            // D3cold Safe Rule: Only query if game rendering
            if (snapshot.IsGameMode) {
                try {
                    ProcessStartInfo psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=power.draw,temperature.gpu,clocks.current.graphics,utilization.gpu --format=csv,noheader,nounits");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    using (Process p = Process.Start(psi)) {
                        if (p != null) {
                            string line = p.StandardOutput.ReadToEnd();
                            p.WaitForExit(500);
                            string[] parts = line.Trim().Split(',');
                            if (parts.Length >= 4) {
                                double.TryParse(parts[0].Trim(), out snapshot.GpuPowerW);
                                int.TryParse(parts[1].Trim(), out snapshot.GpuTempC);
                                int.TryParse(parts[2].Trim(), out snapshot.GpuClockMhz);
                                int.TryParse(parts[3].Trim(), out snapshot.GpuUtilPct);
                                snapshot.GpuStatus = "Active Rendering (D0) • Full 140W Dynamic Headroom";
                            }
                        }
                    }
                } catch { }
            } else {
                snapshot.GpuPowerW = 0.0;
                snapshot.GpuClockMhz = 0;
                snapshot.GpuTempC = 0;
                snapshot.GpuUtilPct = 0;
                snapshot.GpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";
            }

            return snapshot;
        }

        public void ApplyProfile(string profileId) {
            if (isEngineLoaded && applyProfileMethod != null && engineInstance != null) {
                try {
                    applyProfileMethod.Invoke(engineInstance, new object[] { profileId });
                    return;
                } catch { }
            }

            ExecuteFallbackProfile(profileId);
        }

        public void ApplyCustomProfile(int pcore, int ecore, int boostMode, int epp, int gpuClock) {
            if (isEngineLoaded && applyCustomMethod != null && engineInstance != null) {
                try {
                    applyCustomMethod.Invoke(engineInstance, new object[] { pcore, ecore, boostMode, epp, gpuClock });
                    return;
                } catch { }
            }

            ApplyPowerCfgValues(pcore, ecore, boostMode, epp);
            if (gpuClock > 0) {
                RunCmd(string.Format("nvidia-smi -lgc 300,{0}", gpuClock));
            } else {
                RunCmd("nvidia-smi -rgc");
            }
        }

        private void ExecuteFallbackProfile(string profileId) {
            if (profileId == "snappy") {
                ApplyPowerCfgValues(0, 0, 4, 30);
                RunCmd("nvidia-smi -rgc");
            } else if (profileId == "clamped") {
                ApplyPowerCfgValues(4900, 2800, 4, 25);
                RunCmd("nvidia-smi -rgc");
            } else if (profileId == "cold") {
                ApplyPowerCfgValues(0, 0, 3, 20);
                RunCmd("nvidia-smi -lgc 300,2100");
            }
        }

        private void ApplyPowerCfgValues(int pcore, int ecore, int boostMode, int epp) {
            string[] schemes = new string[] {
                "381b4222-f694-41f0-9685-ff5bb260df2e",
                "961cc777-2547-4f9d-8174-7d86181b8a7a",
                "ded574b5-45a0-4f42-8737-46345c09c238"
            };
            foreach (string s in schemes) {
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e101 {1}", s, pcore));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e101 {1}", s, pcore));
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 {1}", s, ecore));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 {1}", s, ecore));
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 {1}", s, boostMode));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 {1}", s, boostMode));
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {1}", s, epp));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {1}", s, epp));
            }
            RunCmd("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e");

            try {
                File.WriteAllText(@"C:\Users\a7god\game_pcore_cap.txt", pcore.ToString() + "\n");
            } catch { }
        }

        private void RunCmd(string cmd) {
            try {
                int firstSpace = cmd.IndexOf(' ');
                string exe = (firstSpace > 0) ? cmd.Substring(0, firstSpace) : cmd;
                string args = (firstSpace > 0) ? cmd.Substring(firstSpace + 1) : "";
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) {
                    if (p != null) {
                        p.WaitForExit(1000);
                    }
                }
            } catch { }
        }

        private double[] ReadDoubleArray(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) {
                object val = f.GetValue(obj);
                if (val is double[]) return (double[])val;
            }
            PropertyInfo p = t.GetProperty(name);
            if (p != null) {
                object val = p.GetValue(obj, null);
                if (val is double[]) return (double[])val;
            }
            return new double[24];
        }

        private double ReadDouble(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) return Convert.ToDouble(f.GetValue(obj));
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return Convert.ToDouble(p.GetValue(obj, null));
            return 0.0;
        }

        private int ReadInt(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) return Convert.ToInt32(f.GetValue(obj));
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return Convert.ToInt32(p.GetValue(obj, null));
            return 0;
        }

        private bool ReadBool(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) return Convert.ToBoolean(f.GetValue(obj));
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return Convert.ToBoolean(p.GetValue(obj, null));
            return false;
        }

        private string ReadString(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) {
                object val = f.GetValue(obj);
                return val != null ? val.ToString() : "";
            }
            PropertyInfo p = t.GetProperty(name);
            if (p != null) {
                object val = p.GetValue(obj, null);
                return val != null ? val.ToString() : "";
            }
            return "";
        }

        public void Dispose() {
            if (pdhQuery != IntPtr.Zero) {
                try { PdhCloseQuery(pdhQuery); } catch { }
                pdhQuery = IntPtr.Zero;
            }
        }
    }
}
