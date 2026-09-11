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
        private void SelectProfile(string profileId) {
            currentSelectedGamingProfile = profileId;
            bridge.SetGamingProfile(profileId);
            if (currentSnapshot != null && currentSnapshot.IsGameMode) {
                currentSelectedProfile = profileId;
            } else if (!isAutoProfileSwitchingEnabled) {
                bridge.ApplyProfile(profileId);
                currentSelectedProfile = profileId;
            }
            UpdateProfileCardsVisualState();

            string name = "Snappy-Pacing";
            if (profileId == "clamped") name = "Sweet-Spot Efficiency (4.9 GHz)";
            if (profileId == "cold") name = "Cold & Quiet (GPU 2100 MHz)";
            if (profileId == "guaranteed") name = "Guaranteed Curve (Mode 6)";

            trayIcon.Text = string.Format("Vector Power Hub - {0} Active", name);

            if (currentSnapshot != null && currentSnapshot.IsGameMode) {
                ShowNotificationBalloon("In-Game Profile Switched", string.Format("Switched in-game profile to: {0}", name));
            } else if (isAutoProfileSwitchingEnabled) {
                ShowNotificationBalloon("Gaming Target Configured", string.Format("Selected '{0}' as designated gaming profile (Auto-Engages on Game Launch).", name));
            } else {
                ShowNotificationBalloon("Profile Switched", string.Format("Activated profile: {0}", name));
            }
        }

        private void SelectDesktopProfile(string profileId) {
            currentSelectedDesktopProfile = profileId;
            bridge.SetDesktopProfile(profileId);
            if (currentSnapshot != null && !currentSnapshot.IsGameMode && isAutoProfileSwitchingEnabled) {
                currentSelectedProfile = profileId;
            }
            UpdateAutoSwitchVisuals();
            UpdateProfileCardsVisualState();

            string desc = "Balanced Standby (EPP 50%, D3cold)";
            if (profileId == "powersaver" || profileId == "silent") desc = "Silent Power Saver (EPP 80%, No Boost, D3cold)";
            if (profileId == "cold") desc = "Cold & Quiet (GPU Clamped 2100 MHz)";
            if (profileId == "guaranteed") desc = "Guaranteed Curve (EPP 25%, Boost Mode 6)";
            ShowNotificationBalloon("Game OFF Profile Changed", string.Format("Set Game OFF profile to: {0}", desc));
        }

        private void ToggleAutoProfileSwitching() {
            isAutoProfileSwitchingEnabled = !isAutoProfileSwitchingEnabled;
            bridge.SetAutoProfileSwitching(isAutoProfileSwitchingEnabled);
            UpdateAutoSwitchVisuals();

            if (isAutoProfileSwitchingEnabled) {
                string standbyDesc = (currentSelectedDesktopProfile == "powersaver" || currentSelectedDesktopProfile == "silent") ? "Silent Eco" : (currentSelectedDesktopProfile == "cold" ? "Cold" : (currentSelectedDesktopProfile == "guaranteed" ? "Guaranteed" : "Balanced"));
                ShowNotificationBalloon("Auto-Profiles Enabled", string.Format("Game ON ➔ {0} | Game OFF ➔ {1} (D3cold)", currentSelectedGamingProfile.ToUpper(), standbyDesc));
            } else {
                ShowNotificationBalloon("Auto-Profiles Disabled", "Manual profile lock engaged. Auto-switching suspended.");
            }
        }

        private void UpdateAutoSwitchVisuals() {
            if (btnToggleAutoSwitch != null) {
                if (isAutoProfileSwitchingEnabled) {
                    btnToggleAutoSwitch.Text = "⚡ AUTO-PROFILES: ON (GAME SYNC)";
                    btnToggleAutoSwitch.ButtonColor = Color.FromArgb(14, 38, 26);
                    btnToggleAutoSwitch.BorderColor = ColorAccentGreen;
                    btnToggleAutoSwitch.TextColor = ColorAccentGreen;
                } else {
                    btnToggleAutoSwitch.Text = "⚡ AUTO-PROFILES: OFF (MANUAL LOCK)";
                    btnToggleAutoSwitch.ButtonColor = Color.FromArgb(32, 28, 16);
                    btnToggleAutoSwitch.BorderColor = ColorAccentGold;
                    btnToggleAutoSwitch.TextColor = ColorAccentGold;
                }
            }
            if (trayMenuAutoSwitch != null) {
                trayMenuAutoSwitch.Checked = isAutoProfileSwitchingEnabled;
            }

            if (trayMenuDesktopBalanced != null) trayMenuDesktopBalanced.Checked = (currentSelectedDesktopProfile == "desktop");
            if (trayMenuDesktopSilent != null) trayMenuDesktopSilent.Checked = (currentSelectedDesktopProfile == "powersaver" || currentSelectedDesktopProfile == "silent");
            if (trayMenuDesktopCold != null) trayMenuDesktopCold.Checked = (currentSelectedDesktopProfile == "cold");
            if (trayMenuDesktopGuaranteed != null) trayMenuDesktopGuaranteed.Checked = (currentSelectedDesktopProfile == "guaranteed");

            if (lblFooterStatus != null) {
                string autoText = isAutoProfileSwitchingEnabled ? "Auto-Profiles: Active (Game Sync)" : "Auto-Profiles: Off (Manual Lock)";
                lblFooterStatus.Text = string.Format("● ETW DXGI Active  |  {0}  |  D3cold Safe Architecture", autoText);
            }

            UpdateStartupVisuals();
        }

        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "VectorPowerHub";
    }
}
