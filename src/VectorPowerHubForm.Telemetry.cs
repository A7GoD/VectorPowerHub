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
        // TELEMETRY REFRESH LOOP (Every 750ms)
        // -----------------------------------------------------------------------------------------
        public void OnTelemetryTick(object sender, EventArgs e) {
            try {
                if (sender != null && (!this.Visible || this.WindowState == FormWindowState.Minimized)) {
                    if (telemetryTimer != null && telemetryTimer.Enabled) {
                        telemetryTimer.Stop();
                    }
                    return;
                }

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

                // Game ON / Game OFF automation transition detection
                if (snap.IsGameMode != lastObservedGameMode) {
                    lastObservedGameMode = snap.IsGameMode;
                    if (snap.IsGameMode) {
                        ShowNotificationBalloon("★ Game Detected - Profile Engaged",
                            string.Format("'{0}' is rendering! Auto-switched to {1} profile.",
                                string.IsNullOrEmpty(snap.ActiveGameName) ? "Active Game" : snap.ActiveGameName,
                                snap.ActiveProfile.ToUpper()));
                    } else {
                        ShowNotificationBalloon("☆ Game Exited - Standby Restored",
                            "Game session ended. Automatically restored Desktop Standby profile (D3cold GPU sleep).");
                    }
                }

                if (!string.IsNullOrEmpty(snap.SelectedGamingProfile)) {
                    currentSelectedGamingProfile = snap.SelectedGamingProfile;
                }
                if (!string.IsNullOrEmpty(snap.SelectedDesktopProfile)) {
                    currentSelectedDesktopProfile = snap.SelectedDesktopProfile;
                }
                if (!string.IsNullOrEmpty(snap.ActiveProfile)) {
                    currentSelectedProfile = snap.ActiveProfile;
                }
                isAutoProfileSwitchingEnabled = snap.AutoProfileSwitching;

                UpdateAutoSwitchVisuals();
                UpdateProfileCardsVisualState();
            } catch { }
        }


    }
}
