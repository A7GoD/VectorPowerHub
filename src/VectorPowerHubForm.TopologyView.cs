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
        // TOPOLOGY PANEL & INTEGRATED ON-DEMAND HARDWARE TUNER
        // -----------------------------------------------------------------------------------------
        private void InitializeTopologyPanel() {
            panelTopologyView = new Panel();
            panelTopologyView.BackColor = Color.FromArgb(18, 19, 24);
            panelTopologyView.Visible = false;

            // 1. On-Demand Tuning Toggle Button
            btnToggleTopologyTuning = new GlowButton();
            btnToggleTopologyTuning.Text = "⚙ ADVANCED HARDWARE TUNING CONTROLS [▼ Click to Expand]";
            btnToggleTopologyTuning.Location = new Point(0, 0);
            btnToggleTopologyTuning.Height = 30;
            btnToggleTopologyTuning.ButtonColor = ColorCardBg;
            btnToggleTopologyTuning.BorderColor = ColorBorder;
            btnToggleTopologyTuning.TextColor = ColorTextMuted;
            btnToggleTopologyTuning.Click += (s, e) => ToggleTopologyTuningDrawer();
            panelTopologyView.Controls.Add(btnToggleTopologyTuning);

            // 2. Expandable Tuning Drawer Panel (Hidden by default)
            panelTopologyTuningDrawer = new Panel();
            panelTopologyTuningDrawer.Location = new Point(0, 36);
            panelTopologyTuningDrawer.Height = 160;
            panelTopologyTuningDrawer.BackColor = Color.FromArgb(20, 23, 31);
            panelTopologyTuningDrawer.BorderStyle = BorderStyle.FixedSingle;
            panelTopologyTuningDrawer.Visible = false;

            // Row 1: P-Core, E-Core, EPP Sliders
            lblDrawerPcoreVal = new Label();
            lblDrawerPcoreVal.Text = "P-Core Turbo: Unbounded (Up to 5.5 GHz)";
            lblDrawerPcoreVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerPcoreVal.ForeColor = ColorAccentCyan;
            lblDrawerPcoreVal.Location = new Point(14, 8);
            lblDrawerPcoreVal.AutoSize = true;

            trackDrawerPcore = new TrackBar();
            trackDrawerPcore.Minimum = 30;
            trackDrawerPcore.Maximum = 55;
            trackDrawerPcore.Value = 55;
            trackDrawerPcore.TickFrequency = 2;
            trackDrawerPcore.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerPcore.Location = new Point(14, 24);
            trackDrawerPcore.Size = new Size(290, 32);
            trackDrawerPcore.ValueChanged += (s, e) => {
                lblDrawerPcoreVal.Text = (trackDrawerPcore.Value == 55)
                    ? "P-Core Turbo: Unbounded (Up to 5.5 GHz)"
                    : string.Format("P-Core Turbo: {0:0.0} GHz ({1}00 MHz)", trackDrawerPcore.Value / 10.0, trackDrawerPcore.Value);
            };

            lblDrawerEcoreVal = new Label();
            lblDrawerEcoreVal.Text = "E-Core Turbo: 2.8 GHz (2800 MHz)";
            lblDrawerEcoreVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerEcoreVal.ForeColor = ColorAccentGold;
            lblDrawerEcoreVal.Location = new Point(326, 8);
            lblDrawerEcoreVal.AutoSize = true;

            trackDrawerEcore = new TrackBar();
            trackDrawerEcore.Minimum = 16;
            trackDrawerEcore.Maximum = 32;
            trackDrawerEcore.Value = 28;
            trackDrawerEcore.TickFrequency = 2;
            trackDrawerEcore.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerEcore.Location = new Point(326, 24);
            trackDrawerEcore.Size = new Size(290, 32);
            trackDrawerEcore.ValueChanged += (s, e) => {
                lblDrawerEcoreVal.Text = string.Format("E-Core Turbo: {0:0.0} GHz ({1}00 MHz)", trackDrawerEcore.Value / 10.0, trackDrawerEcore.Value);
            };

            lblDrawerEppVal = new Label();
            lblDrawerEppVal.Text = "EPP Policy: 30% (Eager GPU Release)";
            lblDrawerEppVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerEppVal.ForeColor = ColorAccentCyan;
            lblDrawerEppVal.Location = new Point(630, 8);
            lblDrawerEppVal.AutoSize = true;

            trackDrawerEpp = new TrackBar();
            trackDrawerEpp.Minimum = 0;
            trackDrawerEpp.Maximum = 100;
            trackDrawerEpp.Value = 30;
            trackDrawerEpp.TickFrequency = 10;
            trackDrawerEpp.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerEpp.Location = new Point(630, 24);
            trackDrawerEpp.Size = new Size(280, 32);
            trackDrawerEpp.ValueChanged += (s, e) => {
                lblDrawerEppVal.Text = string.Format("EPP Policy: {0}% (Energy Perf Preference)", trackDrawerEpp.Value);
            };

            // Row 2: Boost Mode, GPU Clock Clamp, Apply Button
            lblDrawerBoostVal = new Label();
            lblDrawerBoostVal.Text = "Intel Boost Mode:";
            lblDrawerBoostVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerBoostVal.ForeColor = ColorAccentGold;
            lblDrawerBoostVal.Location = new Point(14, 82);
            lblDrawerBoostVal.AutoSize = true;

            comboDrawerBoost = new ComboBox();
            comboDrawerBoost.DropDownStyle = ComboBoxStyle.DropDownList;
            comboDrawerBoost.BackColor = ColorCardBg;
            comboDrawerBoost.ForeColor = ColorTextWhite;
            comboDrawerBoost.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            comboDrawerBoost.Items.AddRange(new object[] {
                "0 - Disabled",
                "1 - Enabled",
                "2 - Aggressive",
                "3 - Efficient Enabled",
                "4 - Efficient Aggressive (Snappy Default)",
                "5 - Aggressive At Guaranteed",
                "6 - Efficient Aggressive At Guaranteed"
            });
            comboDrawerBoost.SelectedIndex = 4;
            comboDrawerBoost.Location = new Point(14, 102);
            comboDrawerBoost.Size = new Size(290, 22);

            lblDrawerGpuVal = new Label();
            lblDrawerGpuVal.Text = "RTX 5070 Mobile Max Clock: Stock / Unconstrained";
            lblDrawerGpuVal.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblDrawerGpuVal.ForeColor = ColorAccentCyan;
            lblDrawerGpuVal.Location = new Point(326, 82);
            lblDrawerGpuVal.AutoSize = true;

            trackDrawerGpu = new TrackBar();
            trackDrawerGpu.Minimum = 14;
            trackDrawerGpu.Maximum = 26;
            trackDrawerGpu.Value = 26;
            trackDrawerGpu.TickFrequency = 1;
            trackDrawerGpu.BackColor = Color.FromArgb(20, 23, 31);
            trackDrawerGpu.Location = new Point(326, 100);
            trackDrawerGpu.Size = new Size(330, 32);
            trackDrawerGpu.ValueChanged += (s, e) => {
                lblDrawerGpuVal.Text = (trackDrawerGpu.Value == 26)
                    ? "RTX 5070 Mobile Max Clock: Stock / Unconstrained"
                    : string.Format("RTX 5070 Mobile Max Clock: {0}00 MHz", trackDrawerGpu.Value);
            };

            btnDrawerApply = new GlowButton();
            btnDrawerApply.Text = "✔ Apply Custom Hardware Tuning";
            btnDrawerApply.Location = new Point(680, 100);
            btnDrawerApply.Size = new Size(260, 32);
            btnDrawerApply.ButtonColor = Color.FromArgb(24, 44, 32);
            btnDrawerApply.BorderColor = ColorAccentGreen;
            btnDrawerApply.TextColor = ColorAccentGreen;
            btnDrawerApply.Click += (s, e) => ApplyDrawerTuningValues();

            panelTopologyTuningDrawer.Controls.Add(lblDrawerPcoreVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerPcore);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerEcoreVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerEcore);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerEppVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerEpp);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerBoostVal);
            panelTopologyTuningDrawer.Controls.Add(comboDrawerBoost);
            panelTopologyTuningDrawer.Controls.Add(lblDrawerGpuVal);
            panelTopologyTuningDrawer.Controls.Add(trackDrawerGpu);
            panelTopologyTuningDrawer.Controls.Add(btnDrawerApply);
            panelTopologyView.Controls.Add(panelTopologyTuningDrawer);

            // 3. Arrow Lake-HX 24-Core Topology Control
            topologyControl = new PerCoreTopologyControl();
            panelTopologyView.Controls.Add(topologyControl);

            this.Controls.Add(panelTopologyView);
        }


    }
}
