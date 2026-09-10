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
        public static bool IsRunAtStartupEnabled() {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false)) {
                    if (key != null) {
                        object val = key.GetValue(RunValueName);
                        if (val != null) {
                            return true;
                        }
                    }
                }
            } catch { }
            return false;
        }

        public static void SetRunAtStartup(bool enable) {
            try {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true)) {
                    if (key != null) {
                        if (enable) {
                            string exePath = Application.ExecutablePath;
                            if (string.IsNullOrEmpty(exePath) || exePath.IndexOf("powershell", StringComparison.OrdinalIgnoreCase) >= 0 || !File.Exists(exePath)) {
                                if (File.Exists(@"C:\Users\a7god\VectorPowerHub.exe")) {
                                    exePath = @"C:\Users\a7god\VectorPowerHub.exe";
                                }
                            }
                            key.SetValue(RunValueName, string.Format("\"{0}\" /minimized", exePath));
                        } else {
                            key.DeleteValue(RunValueName, false);
                        }
                    }
                }
            } catch { }
        }

        private void ToggleRunAtStartup() {
            bool newState = !IsRunAtStartupEnabled();
            SetRunAtStartup(newState);
            UpdateStartupVisuals();

            if (newState) {
                ShowNotificationBalloon("Run at Startup Enabled", "Vector Power Hub will launch automatically with Windows in the system tray.");
            } else {
                ShowNotificationBalloon("Run at Startup Disabled", "Vector Power Hub was removed from Windows startup.");
            }
        }

        private void UpdateStartupVisuals() {
            bool isEnabled = IsRunAtStartupEnabled();
            if (trayMenuStartup != null && trayMenuStartup.Checked != isEnabled) {
                trayMenuStartup.Checked = isEnabled;
            }
            if (chkRunAtStartup != null && chkRunAtStartup.Checked != isEnabled) {
                chkRunAtStartup.Checked = isEnabled;
            }
        }

        private void UpdateProfileCardsVisualState() {
            bool isGameOn = (currentSnapshot != null && currentSnapshot.IsGameMode);

            cardProfileSnappy.IsActive = (currentSelectedProfile == "snappy" || (!isGameOn && currentSelectedProfile == "desktop"));
            cardProfileEfficiency.IsActive = (currentSelectedProfile == "clamped" || (!isGameOn && (currentSelectedProfile == "powersaver" || currentSelectedProfile == "silent")));
            cardProfileCold.IsActive = (currentSelectedProfile == "cold");

            cardProfileSnappy.IsDesignatedGamingProfile = (currentSelectedGamingProfile == "snappy");
            cardProfileEfficiency.IsDesignatedGamingProfile = (currentSelectedGamingProfile == "clamped");
            cardProfileCold.IsDesignatedGamingProfile = (currentSelectedGamingProfile == "cold");

            cardProfileSnappy.IsDesignatedIdleProfile = (currentSelectedDesktopProfile == "desktop");
            cardProfileEfficiency.IsDesignatedIdleProfile = (currentSelectedDesktopProfile == "powersaver" || currentSelectedDesktopProfile == "silent");
            cardProfileCold.IsDesignatedIdleProfile = (currentSelectedDesktopProfile == "cold");

            cardProfileSnappy.IsGameMode = isGameOn;
            cardProfileEfficiency.IsGameMode = isGameOn;
            cardProfileCold.IsGameMode = isGameOn;

            cardProfileSnappy.AutoSwitchEnabled = isAutoProfileSwitchingEnabled;
            cardProfileEfficiency.AutoSwitchEnabled = isAutoProfileSwitchingEnabled;
            cardProfileCold.AutoSwitchEnabled = isAutoProfileSwitchingEnabled;

            cardProfileSnappy.Invalidate();
            cardProfileEfficiency.Invalidate();
            cardProfileCold.Invalidate();

            if (lblProfileBadge != null) {
                string activeId = (currentSnapshot != null && !string.IsNullOrEmpty(currentSnapshot.ActiveProfile))
                    ? currentSnapshot.ActiveProfile.ToLowerInvariant()
                    : currentSelectedProfile.ToLowerInvariant();

                if (isGameOn) {
                    string gameName = (activeId == "clamped") ? "EFFICIENCY" : (activeId == "cold" ? "COLD & QUIET" : (activeId == "custom" ? "CUSTOM" : "SNAPPY"));
                    lblProfileBadge.Text = string.Format("LIVE: {0} (GAME ON)", gameName);
                    lblProfileBadge.ForeColor = ColorAccentGreen;
                    lblProfileBadge.BackColor = Color.FromArgb(12, 38, 24);
                } else if (isAutoProfileSwitchingEnabled) {
                    string standbyName = (activeId == "powersaver" || activeId == "silent") ? "SILENT ECO" : (activeId == "cold" ? "COLD" : (activeId == "custom" ? "CUSTOM" : "BALANCED"));
                    lblProfileBadge.Text = string.Format("LIVE: {0} (STANDBY)", standbyName);
                    lblProfileBadge.ForeColor = ColorAccentCyan;
                    lblProfileBadge.BackColor = Color.FromArgb(16, 28, 40);
                } else {
                    lblProfileBadge.Text = string.Format("MANUAL LOCK: {0}", activeId.ToUpper());
                    lblProfileBadge.ForeColor = ColorAccentGold;
                    lblProfileBadge.BackColor = Color.FromArgb(40, 32, 12);
                }
                lblProfileBadge.Location = new Point(this.ClientSize.Width - 158 - lblProfileBadge.Width, 11);
            }

            if (trayMenuSnappy != null) trayMenuSnappy.Checked = (currentSelectedGamingProfile == "snappy");
            if (trayMenuEfficiency != null) trayMenuEfficiency.Checked = (currentSelectedGamingProfile == "clamped");
            if (trayMenuCold != null) trayMenuCold.Checked = (currentSelectedGamingProfile == "cold");
            if (trayMenuCustom != null) trayMenuCustom.Checked = (currentSelectedProfile == "custom");
        }


    }
}
