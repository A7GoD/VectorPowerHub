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
        // CONSTRUCTOR
        // -----------------------------------------------------------------------------------------
        public VectorPowerHubForm(int initialTab = 0, bool startMinimized = false) {
            try { SetProcessDPIAware(); } catch { }

            try {
                object mod = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS", "SystemProductName", null);
                if (mod != null) sysModel = mod.ToString();
                object proc = Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null);
                if (proc != null) {
                    string s = proc.ToString().Replace("Intel(R) Core(TM) ", "").Replace("Intel(R) ", "").Trim();
                    sysCpu = s;
                }
                object gpu0 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc", null);
                object gpu1 = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0001", "DriverDesc", null);
                string g0 = gpu0 != null ? gpu0.ToString() : "";
                string g1 = gpu1 != null ? gpu1.ToString() : "";
                string realGpu = g1.Contains("NVIDIA") ? g1 : (g0.Contains("NVIDIA") ? g0 : (g1 != "" ? g1 : g0));
                if (!string.IsNullOrEmpty(realGpu)) sysGpu = realGpu.Replace("NVIDIA GeForce ", "").Replace(" Laptop GPU", "");
            } catch { }

            this.startMinimizedToTray = startMinimized;
            if (startMinimized) {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }

            this.Text = "Vector Power Hub - " + sysModel;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Size = new Size(1160, 800);
            this.MinimumSize = new Size(1120, 720);
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
                singleInstanceWakeEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "VectorPowerHub_WakeEvent_v2");
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
            if (!startMinimized) {
                telemetryTimer.Start();
                OnTelemetryTick(null, EventArgs.Empty);
            } else {
                if (bridge != null) bridge.SetTrayMinimized(true);
            }

            // Initial responsive layout pass
            PerformResponsiveLayout();

            // Force apply default standby settings on startup
            SelectDesktopProfile(currentSelectedDesktopProfile ?? "desktop");
            SelectProfile(currentSelectedGamingProfile ?? "snappy");
            
            // Apply OS Power Optimizations on boot
            OsOptimizationsManager.ApplyAll();

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

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (this.WindowState == FormWindowState.Minimized) {
                try { if (telemetryTimer != null && telemetryTimer.Enabled) telemetryTimer.Stop(); } catch { }
                if (bridge != null) bridge.SetTrayMinimized(true);
            } else if (this.Visible) {
                if (bridge != null) bridge.SetTrayMinimized(false);
                try {
                    if (telemetryTimer != null && !telemetryTimer.Enabled) {
                        telemetryTimer.Start();
                        OnTelemetryTick(null, EventArgs.Empty);
                    }
                } catch { }
            }
        }

        protected override void SetVisibleCore(bool value) {
            if (startMinimizedToTray && !hasShownOnce) {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnShown(EventArgs e) {
            base.OnShown(e);
            if (startMinimizedToTray) {
                this.Hide();
                try { if (telemetryTimer != null && telemetryTimer.Enabled) telemetryTimer.Stop(); } catch { }
                if (bridge != null) bridge.SetTrayMinimized(true);
                ShowNotificationBalloon("Vector Power Hub Active", "Started with Windows in system tray. Low-power tray idle mode engaged.");
            }
        }

        // -----------------------------------------------------------------------------------------

    }
}

