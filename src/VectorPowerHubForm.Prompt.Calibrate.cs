using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        private static void RunPowerCeilingCalibration(
            NumericUpDown numCeiling, Button btnCalibrate, Label lblStatus,
            Button btnOk, Button btnCancel, CheckBox chkAutoDetect, Form dialog) {

            btnCalibrate.Enabled = false;
            btnOk.Enabled = false;
            btnCancel.Enabled = false;
            chkAutoDetect.Enabled = false;
            numCeiling.Enabled = false;
            Color origColor = btnCalibrate.ForeColor;
            Color origBorder = btnCalibrate.FlatAppearance.BorderColor;
            btnCalibrate.ForeColor = ColorAccentGold;
            btnCalibrate.FlatAppearance.BorderColor = ColorAccentGold;

            List<double> samples = new List<double>();
            try {
                for (int i = 1; i <= 10; i++) {
                    if (dialog.IsDisposed) break;
                    double currentW = 0.0;
                    if (PowerCoreEngine.Instance != null) {
                        currentW = PowerCoreEngine.Instance.CurrentSnapshot.TotalPlatformPowerW;
                    }
                    samples.Add(currentW);
                    btnCalibrate.Text = string.Format("Detecting... ({0}/10s)", i);
                    lblStatus.Text = string.Format("Sampling: {0:0.0} W ({1}/10s)", currentW, i);
                    lblStatus.ForeColor = ColorAccentCyan;
                    Application.DoEvents();
                    Thread.Sleep(1000);
                    Application.DoEvents();
                }

                double sustainedPeak = 0.0;
                if (samples.Count >= 3) {
                    for (int i = 0; i <= samples.Count - 3; i++) {
                        double wMin = Math.Min(samples[i], Math.Min(samples[i + 1], samples[i + 2]));
                        if (wMin > sustainedPeak) sustainedPeak = wMin;
                    }
                } else if (samples.Count > 0) {
                    foreach (double val in samples) {
                        if (val > sustainedPeak) sustainedPeak = val;
                    }
                }

                int peakWatts = (int)Math.Round(sustainedPeak);
                if (peakWatts >= (int)numCeiling.Minimum && peakWatts <= (int)numCeiling.Maximum) {
                    numCeiling.Value = peakWatts;
                    lblStatus.Text = string.Format("Calibrated: {0} W sustained peak", peakWatts);
                    lblStatus.ForeColor = ColorAccentGold;
                } else if (peakWatts > (int)numCeiling.Maximum) {
                    numCeiling.Value = numCeiling.Maximum;
                    lblStatus.Text = string.Format("Calibrated: {0} W (clamped)", (int)numCeiling.Maximum);
                    lblStatus.ForeColor = ColorAccentGold;
                } else {
                    lblStatus.Text = string.Format("Idle ({0:0.0} W) - kept at {1} W", sustainedPeak, (int)numCeiling.Value);
                    lblStatus.ForeColor = ColorTextMuted;
                }
            } catch { }
            finally {
                if (!dialog.IsDisposed) {
                    btnCalibrate.Text = "⚡ Auto-Calibrate Now (10s)";
                    btnCalibrate.ForeColor = origColor;
                    btnCalibrate.FlatAppearance.BorderColor = origBorder;
                    btnCalibrate.Enabled = true;
                    btnOk.Enabled = true;
                    btnCancel.Enabled = true;
                    chkAutoDetect.Enabled = true;
                    numCeiling.Enabled = true;
                }
            }
        }
    }
}
