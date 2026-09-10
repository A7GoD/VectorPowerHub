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


    }
}
