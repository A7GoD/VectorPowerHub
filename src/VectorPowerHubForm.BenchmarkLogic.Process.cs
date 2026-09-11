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
        private void CompileBenchmarkProfileResult(string pKey, string pName, List<double> fpsSamples, List<double> cpuSamples, List<double> gpuSamples, List<double> cleanFpsSamples, List<double> cleanCpuSamples, List<double> cleanGpuSamples, int outlierCount, List<string> outlierReasons, List<BenchmarkResultInfo> results) {
                BenchmarkResultInfo r = new BenchmarkResultInfo();
                r.ProfileId = pKey;
                r.ProfileName = pName;

                r.RawAvgFps = ComputeAverage(fpsSamples);
                r.RawOnePercentLow = ComputeOnePercentLow(fpsSamples);

                List<double> finalFps = (cleanFpsSamples.Count > 0) ? cleanFpsSamples : fpsSamples;
                List<double> finalCpu = (cleanCpuSamples.Count > 0) ? cleanCpuSamples : cpuSamples;
                List<double> finalGpu = (cleanGpuSamples.Count > 0) ? cleanGpuSamples : gpuSamples;

                r.CleanedAvgFps = ComputeAverage(finalFps);
                r.CleanedOnePercentLow = ComputeOnePercentLow(finalFps);
                r.AvgCpuPowerW = ComputeAverage(finalCpu);
                r.AvgGpuPowerW = ComputeAverage(finalGpu);
                r.AvgTotalPowerW = r.AvgCpuPowerW + r.AvgGpuPowerW;
                r.OutliersFilteredCount = outlierCount;
                r.OutlierDetails = (outlierCount > 0) ? string.Join(", ", outlierReasons.ToArray()) : "None";

                if (r.AvgTotalPowerW > 0.1) {
                    r.EfficiencyScore = r.CleanedAvgFps / r.AvgTotalPowerW;
                }

                results.Add(r);

                SafeBeginInvoke((MethodInvoker)(() => {
                    benchResultsGrid.AddOrUpdateResult(r);
                }));

        }

        private void FinishBenchmarkRun(string originalProfile, List<BenchmarkResultInfo> results) {
            // Cleanup & Restore
            bridge.SetBenchmarking(false);
            ApplyBenchmarkProfile(originalProfile);
            RunCmd("nvidia-smi -rgc");

            // Determine Winner (must have > 0.0 FPS)
            BenchmarkResultInfo winner = null;
            double maxScore = 0.0;
            foreach (BenchmarkResultInfo res in results) {
                if (res.CleanedAvgFps > maxScore) {
                    maxScore = res.CleanedAvgFps;
                    winner = res;
                }
            }
            if (winner != null) {
                winner.IsWinner = true;
            }

            lastBenchmarkResults = results;

            SafeBeginInvoke((MethodInvoker)(() => {
                isBenchmarkRunning = false;
                btnStartBenchmark.Enabled = true;
                btnStopBenchmark.Enabled = false;
                btnStopBenchmark.BorderColor = ColorBorder;
                btnStopBenchmark.TextColor = ColorTextDim;
                benchProgressBar.Value = 100;

                if (!cancelBenchmarkRequested) {
                    if (winner != null) {
                        lblBenchStatus.Text = string.Format("✔ Benchmark Complete! Optimal Gaming Profile: {0} ({1:0.0} Cleaned FPS)", winner.ProfileName, winner.CleanedAvgFps);
                        btnApplyWinningProfile.Visible = true;
                        btnApplyWinningProfile.Text = string.Format("★ Apply Winner: {0}", winner.ProfileId.ToUpper());
                        lblOutlierAlertPill.Text = string.Format("★ WINNER: {0} • {1:0.0} FPS (1% LOW: {2:0.0})", winner.ProfileName, winner.CleanedAvgFps, winner.CleanedOnePercentLow);
                        lblOutlierAlertPill.ForeColor = ColorAccentGold;
                        lblOutlierAlertPill.BackColor = Color.FromArgb(40, 30, 12);
                        ShowNotificationBalloon("Benchmark Complete", string.Format("Winning Profile: {0}\nCleaned FPS: {1:0.0} (1% Low: {2:0.0})\nEfficiency: {3:0.00} FPS/W", winner.ProfileName, winner.CleanedAvgFps, winner.CleanedOnePercentLow, winner.EfficiencyScore));
                    } else {
                        lblBenchStatus.Text = "⚠️ Benchmark Complete: 0.0 FPS captured (ETW SwapChain idle / Anti-Cheat active). Check game presentation mode.";
                        btnApplyWinningProfile.Visible = false;
                        lblOutlierAlertPill.Text = "⚠️ ETW SWAPCHAIN IDLE • NO FRAMES CAPTURED (ANTI-CHEAT / VULKAN ACTIVE)";
                        lblOutlierAlertPill.ForeColor = ColorAccentGold;
                        lblOutlierAlertPill.BackColor = Color.FromArgb(40, 30, 12);
                    }
                } else {
                    lblBenchStatus.Text = "Benchmark Cancelled. Restored initial profile.";
                    btnApplyWinningProfile.Visible = false;
                }

                benchResultsGrid.Refresh();
            }));
        }

        private void ApplyBenchmarkProfile(string profileId) {
            if (profileId == "snappy") {
                bridge.ApplyProfile("snappy");
            } else if (profileId == "clamped") {
                bridge.ApplyProfile("clamped");
            } else if (profileId == "cold") {
                bridge.ApplyProfile("cold");
            } else if (profileId == "guaranteed") {
                bridge.ApplyProfile("guaranteed");
            }
        }

        private void ApplyWinningProfile() {
            foreach (BenchmarkResultInfo r in lastBenchmarkResults) {
                if (r.IsWinner) {
                    SelectProfile(r.ProfileId);
                    SwitchTab(0);
                    break;
                }
            }
        }

        private double ComputeAverage(List<double> list) {
            if (list == null || list.Count == 0) return 0.0;
            double s = 0.0;
            for (int i = 0; i < list.Count; i++) s += list[i];
            return s / list.Count;
        }

        private double ComputeOnePercentLow(List<double> list) {
            if (list == null || list.Count == 0) return 0.0;
            List<double> sorted = new List<double>(list);
            sorted.Sort();
            int count = (int)Math.Ceiling(sorted.Count * 0.01);
            if (count < 1) count = 1;
            double s = 0.0;
            for (int i = 0; i < count; i++) s += sorted[i];
            return s / count;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_POWER_STATUS {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte Reserved1;
            public int BatteryLifeTime;
            public int BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        public static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);


    }
}
