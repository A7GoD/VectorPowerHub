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


    }
}
