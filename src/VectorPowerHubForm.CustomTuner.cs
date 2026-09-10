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
        // CUSTOM TUNER OVERLAY SETUP
        // -----------------------------------------------------------------------------------------
        private void InitializeCustomTunerPanel() {
            panelCustomTuner = new Panel();
            panelCustomTuner.Size = new Size(920, 500);
            panelCustomTuner.BackColor = Color.FromArgb(22, 25, 33);
            panelCustomTuner.BorderStyle = BorderStyle.FixedSingle;
            panelCustomTuner.Visible = false;

            lblTunerTitle = new Label();
            lblTunerTitle.Text = "⚙ REAL-TIME HARDWARE TUNER • CORE ULTRA 9 275HX & RTX 5070";
            lblTunerTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            lblTunerTitle.ForeColor = ColorAccentPurple;
            lblTunerTitle.Location = new Point(28, 18);
            lblTunerTitle.Size = new Size(860, 26);
            lblTunerTitle.AutoSize = false;
            lblTunerTitle.AutoEllipsis = true;

            // 1. P-Core Limit Slider (Col 1, Row 1: X=28, W=400, Label Y=56, Track Y=84)
            lblPcoreVal = new Label();
            lblPcoreVal.Text = "P-Core Limit: Unbounded (5.5 GHz)";
            lblPcoreVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblPcoreVal.ForeColor = ColorAccentCyan;
            lblPcoreVal.Location = new Point(28, 56);
            lblPcoreVal.Size = new Size(400, 22);
            lblPcoreVal.AutoSize = false;
            lblPcoreVal.AutoEllipsis = true;

            trackPcore = new TrackBar();
            trackPcore.Minimum = 30; // 3.0 GHz
            trackPcore.Maximum = 55; // 5.5 GHz (55 = unbounded 0)
            trackPcore.Value = 55;
            trackPcore.TickFrequency = 1;
            trackPcore.BackColor = Color.FromArgb(22, 25, 33);
            trackPcore.Location = new Point(28, 84);
            trackPcore.Size = new Size(400, 45);
            trackPcore.ValueChanged += (s, e) => {
                lblPcoreVal.Text = (trackPcore.Value == 55)
                    ? "P-Core Limit: Unbounded (5.5 GHz)"
                    : string.Format("P-Core Limit: {0:0.0} GHz ({1}00 MHz)", trackPcore.Value / 10.0, trackPcore.Value);
            };

            // 2. E-Core Limit Slider (Col 2, Row 1: X=460, W=400, Label Y=56, Track Y=84)
            lblEcoreVal = new Label();
            lblEcoreVal.Text = "E-Core Limit: 2.8 GHz (2800 MHz)";
            lblEcoreVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblEcoreVal.ForeColor = ColorAccentGold;
            lblEcoreVal.Location = new Point(460, 56);
            lblEcoreVal.Size = new Size(400, 22);
            lblEcoreVal.AutoSize = false;
            lblEcoreVal.AutoEllipsis = true;

            trackEcore = new TrackBar();
            trackEcore.Minimum = 16;
            trackEcore.Maximum = 32;
            trackEcore.Value = 28;
            trackEcore.TickFrequency = 1;
            trackEcore.BackColor = Color.FromArgb(22, 25, 33);
            trackEcore.Location = new Point(460, 84);
            trackEcore.Size = new Size(400, 45);
            trackEcore.ValueChanged += (s, e) => {
                lblEcoreVal.Text = string.Format("E-Core Limit: {0:0.0} GHz ({1}00 MHz)", trackEcore.Value / 10.0, trackEcore.Value);
            };

            // 3. EPP Slider (Col 1, Row 2: X=28, W=400, Label Y=154, Track Y=182)
            lblEppVal = new Label();
            lblEppVal.Text = "EPP Energy Policy: 30% (Snappy)";
            lblEppVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblEppVal.ForeColor = ColorAccentCyan;
            lblEppVal.Location = new Point(28, 154);
            lblEppVal.Size = new Size(400, 22);
            lblEppVal.AutoSize = false;
            lblEppVal.AutoEllipsis = true;

            trackEpp = new TrackBar();
            trackEpp.Minimum = 0;
            trackEpp.Maximum = 100;
            trackEpp.Value = 30;
            trackEpp.TickFrequency = 5;
            trackEpp.BackColor = Color.FromArgb(22, 25, 33);
            trackEpp.Location = new Point(28, 182);
            trackEpp.Size = new Size(400, 45);
            trackEpp.ValueChanged += (s, e) => {
                string desc = (trackEpp.Value <= 32) ? "Snappy" : ((trackEpp.Value <= 64) ? "Balanced" : ((trackEpp.Value <= 84) ? "Efficient" : "Power Saver"));
                lblEppVal.Text = string.Format("EPP Energy Policy: {0}% ({1})", trackEpp.Value, desc);
            };

            // 4. Boost Mode Dropdown (Col 2, Row 2: X=460, W=400, Label Y=154, Combo Y=182)
            lblBoostModeVal = new Label();
            lblBoostModeVal.Text = "Speed Shift Boost Mode:";
            lblBoostModeVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblBoostModeVal.ForeColor = ColorAccentGold;
            lblBoostModeVal.Location = new Point(460, 154);
            lblBoostModeVal.Size = new Size(400, 22);
            lblBoostModeVal.AutoSize = false;
            lblBoostModeVal.AutoEllipsis = true;

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
            comboBoostMode.Location = new Point(460, 182);
            comboBoostMode.Size = new Size(400, 28);

            // 5. GPU Clock Clamp Slider (Row 3, Full Width: X=28, W=860, Label Y=252, Track Y=280)
            lblGpuClockLimitVal = new Label();
            lblGpuClockLimitVal.Text = "RTX 5070 Mobile Clock: Unconstrained (Stock)";
            lblGpuClockLimitVal.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            lblGpuClockLimitVal.ForeColor = ColorAccentCyan;
            lblGpuClockLimitVal.Location = new Point(28, 252);
            lblGpuClockLimitVal.Size = new Size(860, 22);
            lblGpuClockLimitVal.AutoSize = false;
            lblGpuClockLimitVal.AutoEllipsis = true;

            trackGpuClock = new TrackBar();
            trackGpuClock.Minimum = 14; // 1400 MHz
            trackGpuClock.Maximum = 26; // 2600 MHz (26 = stock 0)
            trackGpuClock.Value = 26;
            trackGpuClock.TickFrequency = 1;
            trackGpuClock.BackColor = Color.FromArgb(22, 25, 33);
            trackGpuClock.Location = new Point(28, 280);
            trackGpuClock.Size = new Size(860, 45);
            trackGpuClock.ValueChanged += (s, e) => {
                lblGpuClockLimitVal.Text = (trackGpuClock.Value == 26)
                    ? "RTX 5070 Mobile Clock: Unconstrained (Stock)"
                    : string.Format("RTX 5070 Mobile Clock: Clamped to {0}00 MHz", trackGpuClock.Value);
            };

            InitializeCustomTunerBottomControls();
        }
    }
}
