using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        private void StartBenchmark() {
            if (isBenchmarkRunning) return;

            int iterations = 10;
            if (comboBenchSamples.SelectedIndex == 0) iterations = 5;
            if (comboBenchSamples.SelectedIndex == 1) iterations = 10;
            if (comboBenchSamples.SelectedIndex == 2) iterations = 20;

            bool filterOutliers = chkFilterOutliers.Checked;

            isBenchmarkRunning = true;
            cancelBenchmarkRequested = false;
            bridge.SetBenchmarking(true);

            btnStartBenchmark.Enabled = false;
            btnStopBenchmark.Enabled = true;
            btnStopBenchmark.BorderColor = ColorAccentRed;
            btnStopBenchmark.TextColor = ColorAccentRed;
            btnApplyWinningProfile.Visible = false;

            benchProgressBar.Value = 0;
            benchResultsGrid.ClearResults();

            benchmarkThread = new Thread(() => ExecuteBenchmark(iterations, filterOutliers));
            benchmarkThread.IsBackground = true;
            benchmarkThread.Start();
        }

        private void CancelBenchmark() {
            if (isBenchmarkRunning) {
                cancelBenchmarkRequested = true;
                bridge.SetBenchmarking(false);
                lblBenchStatus.Text = "Cancelling benchmark run... Restoring previous profile...";
            }
        }

        private void SafeBeginInvoke(MethodInvoker action) {
            if (this.IsHandleCreated && !this.IsDisposed) {
                try {
                    this.BeginInvoke(action);
                } catch { }
            } else {
                try {
                    action();
                } catch { }
            }
        }
    }
}
