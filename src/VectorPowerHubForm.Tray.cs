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

            MenuItem mGameOn = new MenuItem("★ Game ON Target Profile");
            mGameOn.MenuItems.Add(trayMenuSnappy);
            mGameOn.MenuItems.Add(trayMenuEfficiency);
            mGameOn.MenuItems.Add(trayMenuCold);
            trayMenu.MenuItems.Add(mGameOn);

            trayMenuDesktopBalanced = new MenuItem("☆ Balanced Standby (50% EPP, Boost Mode 3, D3cold)", (s, e) => SelectDesktopProfile("desktop"));
            trayMenuDesktopSilent = new MenuItem("☾ Silent Power Saver (80% EPP, No Boost, D3cold)", (s, e) => SelectDesktopProfile("powersaver"));
            trayMenuDesktopCold = new MenuItem("❄ Cold & Quiet (GPU Clamped 2100 MHz)", (s, e) => SelectDesktopProfile("cold"));

            MenuItem mGameOff = new MenuItem("☆ Game OFF Standby Profile");
            mGameOff.MenuItems.Add(trayMenuDesktopBalanced);
            mGameOff.MenuItems.Add(trayMenuDesktopSilent);
            mGameOff.MenuItems.Add(trayMenuDesktopCold);
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
            hasShownOnce = true;
            startMinimizedToTray = false;
            this.ShowInTaskbar = true;
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


    }
}
