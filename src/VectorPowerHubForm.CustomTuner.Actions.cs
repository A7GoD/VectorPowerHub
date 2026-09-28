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

        private void LoadTunerValuesForTarget() {
            if (comboTunerTarget == null || comboTunerTarget.SelectedItem == null) return;
            string target = comboTunerTarget.SelectedItem.ToString().ToLowerInvariant().Split(' ')[0]; // custom, snappy, clamped, cold, guaranteed, desktop
            
            if (target == "custom") return; // Keep current slider positions

            int[] vals = new int[] { 0, 0, 4, 30, 0 }; // Default Snappy
            string ovr = bridge.GetProfileOverride(target);

            if (!string.IsNullOrEmpty(ovr)) {
                string[] parts = ovr.Split(',');
                if (parts.Length == 5) {
                    int.TryParse(parts[0], out vals[0]);
                    int.TryParse(parts[1], out vals[1]);
                    int.TryParse(parts[2], out vals[2]);
                    int.TryParse(parts[3], out vals[3]);
                    int.TryParse(parts[4], out vals[4]);
                }
            } else {
                int maxClock = 5200;
                if (target == "snappy") vals = new int[] { 0, 0, 4, 30, 0 };
                else if (target == "clamped") vals = new int[] { (int)(maxClock * 0.85), (int)(maxClock * 0.70), 4, 25, 0 };
                else if (target == "cold") vals = new int[] { 0, 0, 3, 65, 2100 };
                else if (target == "guaranteed") vals = new int[] { 0, 0, 6, 25, 0 };
                else if (target == "desktop") vals = new int[] { 0, 0, 3, 50, 0 };
            }

            // Update sliders (Scale MHz back to trackbar values, 100MHz = 1 unit)
            if (vals[0] == 0) trackPcore.Value = 55; else trackPcore.Value = Math.Max(30, Math.Min(55, vals[0] / 100));
            if (vals[1] == 0) trackEcore.Value = 32; else trackEcore.Value = Math.Max(16, Math.Min(32, vals[1] / 100));
            comboBoostMode.SelectedIndex = Math.Max(0, Math.Min(6, vals[2]));
            trackEpp.Value = Math.Max(0, Math.Min(100, vals[3]));
            if (vals[4] == 0) trackGpuClock.Value = 26; else trackGpuClock.Value = Math.Max(14, Math.Min(26, vals[4] / 100));
        }

        private void ResetTunerTarget() {
            if (comboTunerTarget == null || comboTunerTarget.SelectedItem == null) return;
            string target = comboTunerTarget.SelectedItem.ToString().ToLowerInvariant().Split(' ')[0];
            if (target != "custom") {
                bridge.SetProfileOverride(target, ""); // Clear override
                LoadTunerValuesForTarget();
                if (currentSelectedProfile == target) SelectProfile(target); // Reapply defaults instantly
                ShowNotificationBalloon(target.ToUpper() + " Profile Reset", "Restored to factory default settings.");
            }
        }

        private void ApplyCustomTunerValues() {
            int pcore = (trackPcore.Value == 55) ? 0 : trackPcore.Value * 100;
            int ecore = trackEcore.Value * 100;
            int epp = trackEpp.Value;
            int boostMode = comboBoostMode.SelectedIndex;
            int gpu = (trackGpuClock.Value == 26) ? 0 : trackGpuClock.Value * 100;

            string target = comboTunerTarget.SelectedItem.ToString().ToLowerInvariant().Split(' ')[0];
            
            if (target == "custom") {
                bridge.ApplyCustomProfile(pcore, ecore, boostMode, epp, gpu);
                currentSelectedProfile = "custom";
                UpdateProfileCardsVisualState();
            } else {
                string ovrStr = string.Format("{0},{1},{2},{3},{4}", pcore, ecore, boostMode, epp, gpu);
                bridge.SetProfileOverride(target, ovrStr);
                if (currentSelectedProfile == target) SelectProfile(target);
            }

            ToggleCustomTuner();
            ShowNotificationBalloon(target.ToUpper() + " Profile Saved", string.Format("P-Core: {0} | EPP: {1}% | Boost: Mode {2}", (pcore == 0 ? "Unbounded" : pcore.ToString() + " MHz"), epp, boostMode));
        }


    }
}

