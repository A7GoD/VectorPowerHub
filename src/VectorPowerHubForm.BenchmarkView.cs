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
        // AUTOMATED BENCHMARK PANEL SETUP
        // -----------------------------------------------------------------------------------------
        private void InitializeBenchmarkPanel() {
            panelBenchmarkView = new Panel();
            panelBenchmarkView.BackColor = ColorBgMain;
            panelBenchmarkView.Visible = false;

            lblBenchHeader = new Label();
            lblBenchHeader.Text = "1-CLICK AUTOMATED PROFILE BENCHMARK SUITE";
            lblBenchHeader.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            lblBenchHeader.ForeColor = ColorAccentPurple;
            lblBenchHeader.Location = new Point(14, 10);
            lblBenchHeader.AutoSize = true;

            lblBenchSub = new Label();
            lblBenchSub.Text = "Multi-run performance evaluation with hardware power-cut and loading-freeze outlier elimination.";
            lblBenchSub.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblBenchSub.ForeColor = ColorTextMuted;
            lblBenchSub.Location = new Point(14, 32);
            lblBenchSub.AutoSize = true;

            lblBenchDuration = new Label();
            lblBenchDuration.Text = "Iterations:";
            lblBenchDuration.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            lblBenchDuration.ForeColor = ColorTextWhite;
            lblBenchDuration.Location = new Point(14, 60);
            lblBenchDuration.AutoSize = true;

            comboBenchSamples = new ComboBox();
            comboBenchSamples.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBenchSamples.BackColor = ColorCardBg;
            comboBenchSamples.ForeColor = ColorTextWhite;
            comboBenchSamples.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            comboBenchSamples.Items.AddRange(new object[] {
                "5 Iterations (Fast Validation)",
                "10 Iterations (Recommended Standard)",
                "20 Iterations (Rigorous Statistical Suite)"
            });
            comboBenchSamples.SelectedIndex = 1;
            comboBenchSamples.Location = new Point(105, 57);
            comboBenchSamples.Size = new Size(245, 24);

            chkFilterOutliers = new CheckBox();
            chkFilterOutliers.Text = "Intelligent Outlier Elimination (Filter AC Drops • Loading Freezes)";
            chkFilterOutliers.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            chkFilterOutliers.ForeColor = ColorAccentCyan;
            chkFilterOutliers.Checked = true;
            chkFilterOutliers.Location = new Point(365, 58);
            chkFilterOutliers.AutoSize = true;

            btnStartBenchmark = new GlowButton();
            btnStartBenchmark.Text = "▶ Run Benchmark";
            btnStartBenchmark.Location = new Point(14, 92);
            btnStartBenchmark.Size = new Size(180, 32);
            btnStartBenchmark.ButtonColor = ColorCardBg;
            btnStartBenchmark.BorderColor = ColorAccentPurple;
            btnStartBenchmark.TextColor = ColorAccentPurple;
            btnStartBenchmark.Click += (s, e) => StartBenchmark();

            btnStopBenchmark = new GlowButton();
            btnStopBenchmark.Text = "⏹ Cancel";
            btnStopBenchmark.Location = new Point(202, 92);
            btnStopBenchmark.Size = new Size(88, 32);
            btnStopBenchmark.ButtonColor = ColorCardBg;
            btnStopBenchmark.BorderColor = ColorBorder;
            btnStopBenchmark.TextColor = ColorTextDim;
            btnStopBenchmark.Enabled = false;
            btnStopBenchmark.Click += (s, e) => CancelBenchmark();

            btnApplyWinningProfile = new GlowButton();
            btnApplyWinningProfile.Text = "★ Apply Winner";
            btnApplyWinningProfile.Location = new Point(298, 92);
            btnApplyWinningProfile.Size = new Size(180, 32);
            btnApplyWinningProfile.ButtonColor = Color.FromArgb(36, 30, 16);
            btnApplyWinningProfile.BorderColor = ColorAccentGold;
            btnApplyWinningProfile.TextColor = ColorAccentGold;
            btnApplyWinningProfile.Visible = false;
            btnApplyWinningProfile.Click += (s, e) => ApplyWinningProfile();

            lblBenchStatus = new Label();
            lblBenchStatus.Text = "Ready. Launch 3D game, then start benchmark suite.";
            lblBenchStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblBenchStatus.ForeColor = ColorTextMuted;
            lblBenchStatus.Location = new Point(490, 100);
            lblBenchStatus.AutoSize = true;

            benchProgressBar = new BenchmarkProgressBar();
            benchProgressBar.Location = new Point(14, 132);
            benchProgressBar.Height = 12;

            lblOutlierAlertPill = new Label();
            lblOutlierAlertPill.Text = "⚡ AC LINE STABLE • DUAL OUTLIER REJECTION ARMED";
            lblOutlierAlertPill.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            lblOutlierAlertPill.ForeColor = ColorAccentGreen;
            lblOutlierAlertPill.BackColor = Color.FromArgb(10, 32, 22);
            lblOutlierAlertPill.Padding = new Padding(6, 2, 6, 2);
            lblOutlierAlertPill.Location = new Point(14, 150);
            lblOutlierAlertPill.AutoSize = true;

            benchResultsGrid = new BenchmarkResultsGrid();
            benchResultsGrid.Location = new Point(14, 174);

            panelBenchmarkView.Controls.Add(lblBenchHeader);
            panelBenchmarkView.Controls.Add(lblBenchSub);
            panelBenchmarkView.Controls.Add(lblBenchDuration);
            panelBenchmarkView.Controls.Add(comboBenchSamples);
            panelBenchmarkView.Controls.Add(chkFilterOutliers);
            panelBenchmarkView.Controls.Add(btnStartBenchmark);
            panelBenchmarkView.Controls.Add(btnStopBenchmark);
            panelBenchmarkView.Controls.Add(btnApplyWinningProfile);
            panelBenchmarkView.Controls.Add(lblBenchStatus);
            panelBenchmarkView.Controls.Add(benchProgressBar);
            panelBenchmarkView.Controls.Add(lblOutlierAlertPill);
            panelBenchmarkView.Controls.Add(benchResultsGrid);
            this.Controls.Add(panelBenchmarkView);
        }


    }
}
