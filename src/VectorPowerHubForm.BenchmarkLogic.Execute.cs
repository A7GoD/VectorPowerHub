using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        public void ExecuteBenchmark(int iterationsPerProfile, bool filterOutliers) {
            string[] profileKeys = new string[] { "snappy", "clamped", "cold", "guaranteed" };
            string[] profileNames = new string[] {
                "⚡ Snappy-Pacing (Mode 4 / EPP 30%)",
                "✦ Sweet-Spot Efficiency (4.9 GHz / 58W)",
                "❄ Cold & Quiet (GPU 2100 MHz)",
                "★ Guaranteed Curve (Mode 6 / EPP 25%)"
            };

            string originalProfile = currentSelectedProfile;
            List<BenchmarkResultInfo> results = new List<BenchmarkResultInfo>();

            int totalSteps = profileKeys.Length * (3 + iterationsPerProfile);
            int currentStep = 0;
            int delayMs = (iterationsPerProfile <= 1) ? 50 : 1000;

            for (int pIdx = 0; pIdx < profileKeys.Length; pIdx++) {
                if (cancelBenchmarkRequested) break;

                string pKey = profileKeys[pIdx];
                string pName = profileNames[pIdx];

                ApplyBenchmarkProfile(pKey);

                // Warmup Phase (3 Seconds Countdown)
                for (int w = 3; w >= 1; w--) {
                    if (cancelBenchmarkRequested) break;
                    currentStep++;
                    int pct = (int)((currentStep / (double)totalSteps) * 100);

                    string warmupStatus = string.Format("⏱ Warmup Countdown: {0}s left — Stabilizing clocks & thermals for {1}...", w, pName);
                    int remainingSec = w;
                    SafeBeginInvoke((MethodInvoker)(() => {
                        lblBenchStatus.Text = warmupStatus;
                        benchProgressBar.Value = pct;
                        lblOutlierAlertPill.Text = string.Format("⏱ WARMUP COUNTDOWN: {0}s — CLOCKS STABILIZING", remainingSec);
                        lblOutlierAlertPill.ForeColor = ColorAccentGold;
                        lblOutlierAlertPill.BackColor = Color.FromArgb(40, 30, 12);
                    }));
                    Thread.Sleep(delayMs);
                }

                if (cancelBenchmarkRequested) break;

                // Sampling Phase
                List<double> fpsSamples = new List<double>();
                List<double> cpuSamples = new List<double>();
                List<double> gpuSamples = new List<double>();
                List<double> cleanFpsSamples = new List<double>();
                List<double> cleanCpuSamples = new List<double>();
                List<double> cleanGpuSamples = new List<double>();
                int outlierCount = 0;
                List<string> outlierReasons = new List<string>();

                for (int s = 1; s <= iterationsPerProfile; s++) {
                    if (cancelBenchmarkRequested) break;
                    currentStep++;
                    int pct = (int)((currentStep / (double)totalSteps) * 100);

                    HubTelemetrySnapshot snap = bridge.GetSnapshot();
                    double fps = snap.Fps;
                    double cpuW = snap.CpuPowerW;
                    double gpuW = snap.GpuPowerW;

                    fpsSamples.Add(fps);
                    cpuSamples.Add(cpuW);
                    gpuSamples.Add(gpuW);

                    bool isOutlier = false;
                    string reason = "";

                    if (filterOutliers) {
                        SYSTEM_POWER_STATUS pwrStatus;
                        if (GetSystemPowerStatus(out pwrStatus)) {
                            if (pwrStatus.ACLineStatus == 0) {
                                isOutlier = true;
                                reason = "AC Power Cut / DC Battery";
                            }
                        }

                        if (!isOutlier && fps < 20.0 && snap.GpuUtilPct < 15) {
                            isOutlier = true;
                            reason = "Loading Screen Freeze";
                        }
                    }

                    if (isOutlier) {
                        outlierCount++;
                        if (!outlierReasons.Contains(reason)) outlierReasons.Add(reason);
                    } else {
                        cleanFpsSamples.Add(fps);
                        cleanCpuSamples.Add(cpuW);
                        cleanGpuSamples.Add(gpuW);
                    }

                    string sampleStatus;
                    if (fps <= 0.0) {
                        if (gpuW >= 30.0) {
                            sampleStatus = string.Format("Testing {0} | Iter {1}/{2} — ⚠️ ETW SwapChain idle: FPS not captured (Vulkan/Anti-Cheat active) | CPU: {3:0.0}W | GPU: {4:0.0}W", pName, s, iterationsPerProfile, cpuW, gpuW);
                        } else {
                            sampleStatus = string.Format("Testing {0} | Iter {1}/{2} — ⚠️ No 3D workload / FPS detected | CPU: {3:0.0}W | GPU: {4:0.0}W", pName, s, iterationsPerProfile, cpuW, gpuW);
                        }
                    } else {
                        sampleStatus = string.Format("Testing {0} | Iteration {1}/{2} — FPS: {3:0.0} | CPU: {4:0.0}W | GPU: {5:0.0}W", pName, s, iterationsPerProfile, fps, cpuW, gpuW);
                    }

                    bool sampleOutlier = isOutlier;
                    string outlierMsg = reason;
                    SafeBeginInvoke((MethodInvoker)(() => {
                        lblBenchStatus.Text = sampleStatus;
                        benchProgressBar.Value = pct;
                        if (sampleOutlier) {
                            lblOutlierAlertPill.Text = string.Format("⚠️ OUTLIER REJECTED: {0}", outlierMsg);
                            lblOutlierAlertPill.ForeColor = ColorAccentRed;
                            lblOutlierAlertPill.BackColor = Color.FromArgb(45, 16, 16);
                        } else if (fps <= 0.0) {
                            lblOutlierAlertPill.Text = "⚠️ ETW SWAPCHAIN IDLE • FPS NOT CAPTURED (ANTI-CHEAT / VULKAN ACTIVE)";
                            lblOutlierAlertPill.ForeColor = ColorAccentGold;
                            lblOutlierAlertPill.BackColor = Color.FromArgb(40, 30, 12);
                        } else {
                            lblOutlierAlertPill.Text = "⚡ AC LINE STABLE • HARDWARE PACING NOMINAL";
                            lblOutlierAlertPill.ForeColor = ColorAccentGreen;
                            lblOutlierAlertPill.BackColor = Color.FromArgb(10, 32, 22);
                        }
                    }));

                    Thread.Sleep(delayMs);
                }

                CompileBenchmarkProfileResult(pKey, pName, fpsSamples, cpuSamples, gpuSamples, cleanFpsSamples, cleanCpuSamples, cleanGpuSamples, outlierCount, outlierReasons, results);
            }

            FinishBenchmarkRun(originalProfile, results);
        }
    }
}
