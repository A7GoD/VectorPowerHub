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
        // -----------------------------------------------------------------------------------------
        // SYSTEM TRAY SETUP
        // -----------------------------------------------------------------------------------------
        private void InitializeSystemTray() {
            trayMenu = new ContextMenu();

            MenuItem mHeader = new MenuItem("Vector Power Hub - MSI Vector 16 HX");
            mHeader.Enabled = false;
            trayMenu.MenuItems.Add(mHeader);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            trayMenuAutoSwitch = new MenuItem("⚡ Auto-Profile Switching (Game ON/OFF)", (s, e) => ToggleAutoProfileSwitching());
            trayMenuAutoSwitch.Checked = true;
            trayMenu.MenuItems.Add(trayMenuAutoSwitch);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            trayMenuSnappy = new MenuItem("⚡ Snappy-Pacing (Competitive Max FPS)", (s, e) => SelectProfile("snappy"));
            trayMenuEfficiency = new MenuItem("✦ Sweet-Spot Efficiency (4.9 GHz / 58W)", (s, e) => SelectProfile("clamped"));
            trayMenuCold = new MenuItem("❄ Cold & Quiet (GPU 2100 MHz)", (s, e) => SelectProfile("cold"));
            trayMenuGuaranteed = new MenuItem("★ Efficient Guaranteed (Mode 6 / EPP 25%)", (s, e) => SelectProfile("guaranteed"));

            MenuItem mGameOn = new MenuItem("★ Game ON Target Profile");
            mGameOn.MenuItems.Add(trayMenuSnappy);
            mGameOn.MenuItems.Add(trayMenuEfficiency);
            mGameOn.MenuItems.Add(trayMenuCold);
            mGameOn.MenuItems.Add(trayMenuGuaranteed);
            trayMenu.MenuItems.Add(mGameOn);

            trayMenuDesktopBalanced = new MenuItem("☆ Balanced Standby (50% EPP, Boost Mode 3, D3cold)", (s, e) => SelectDesktopProfile("desktop"));
            trayMenuDesktopSilent = new MenuItem("☾ Silent Power Saver (80% EPP, No Boost, D3cold)", (s, e) => SelectDesktopProfile("powersaver"));
            trayMenuDesktopCold = new MenuItem("❄ Cold & Quiet (GPU Clamped 2100 MHz)", (s, e) => SelectDesktopProfile("cold"));
            trayMenuDesktopGuaranteed = new MenuItem("★ Efficient Guaranteed (EPP 25%, Boost Mode 6)", (s, e) => SelectDesktopProfile("guaranteed"));

            MenuItem mGameOff = new MenuItem("☆ Game OFF Standby Profile");
            mGameOff.MenuItems.Add(trayMenuDesktopBalanced);
            mGameOff.MenuItems.Add(trayMenuDesktopSilent);
            mGameOff.MenuItems.Add(trayMenuDesktopCold);
            mGameOff.MenuItems.Add(trayMenuDesktopGuaranteed);
            trayMenu.MenuItems.Add(mGameOff);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            trayMenuCustom = new MenuItem("⚙ Hardware Tuner...", (s, e) => {
                RestoreFromTray();
                SwitchTab(2);
                if (!panelTopologyTuningDrawer.Visible) ToggleTopologyTuningDrawer();
            });
            trayMenuBenchmark = new MenuItem("★ Automated Benchmark Suite...", (s, e) => {
                RestoreFromTray();
                SwitchTab(1);
            });

            trayMenu.MenuItems.Add(trayMenuCustom);
            trayMenu.MenuItems.Add(trayMenuBenchmark);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            trayMenuStartup = new MenuItem("⚡ Start with Windows (Run at Startup)", (s, e) => ToggleRunAtStartup());
            trayMenuStartup.Checked = IsRunAtStartupEnabled();
            trayMenu.MenuItems.Add(trayMenuStartup);
            trayMenu.MenuItems.Add(new MenuItem("-"));

            MenuItem mShow = new MenuItem("Open Dashboard", (s, e) => RestoreFromTray());
            MenuItem mExit = new MenuItem("Exit Vector Power Hub", (s, e) => ExitApplication());
            trayMenu.MenuItems.Add(mShow);
            trayMenu.MenuItems.Add(mExit);

            trayIcon = new NotifyIcon();
            trayIcon.Text = "Vector Power Hub - Snappy-Pacing Active";
            trayIcon.ContextMenu = trayMenu;
            if (this.Icon != null) {
                trayIcon.Icon = this.Icon;
            } else {
                try { trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
                if (trayIcon.Icon == null) trayIcon.Icon = GenerateAppIcon();
            }
            trayIcon.Visible = true;
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();
        }

        private void MinimizeToTray() {
            this.Hide();
            try { if (telemetryTimer != null && telemetryTimer.Enabled) telemetryTimer.Stop(); } catch { }
            if (bridge != null) bridge.SetTrayMinimized(true);
            ShowNotificationBalloon("Vector Power Hub Running", "Minimized to system tray. Low-power tray idle mode engaged.");
        }

        public void RestoreFromTray() {
            if (this.InvokeRequired) {
                this.BeginInvoke(new MethodInvoker(RestoreFromTray));
                return;
            }
            hasShownOnce = true;
            startMinimizedToTray = false;
            this.ShowInTaskbar = true;
            this.Show();
            this.WindowState = FormWindowState.Normal;
            if (bridge != null) bridge.SetTrayMinimized(false);
            try {
                if (telemetryTimer != null && !telemetryTimer.Enabled) {
                    telemetryTimer.Start();
                }
            } catch { }
            OnTelemetryTick(null, EventArgs.Empty);
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

        private void ExitApplication() {
            isDisposingOrClosed = true;
            if (singleInstanceWakeEvent != null) {
                try { singleInstanceWakeEvent.Set(); singleInstanceWakeEvent.Close(); } catch { }
                singleInstanceWakeEvent = null;
            }
            if (telemetryTimer != null) telemetryTimer.Stop();
            if (trayIcon != null) {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            bridge.Dispose();
            Application.Exit();
        }
    }
}
