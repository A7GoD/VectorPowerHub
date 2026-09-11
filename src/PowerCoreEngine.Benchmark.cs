using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // AUTOMATED BENCHMARK ENGINE WITH OUTLIER ELIMINATION
    // ---------------------------------------------------------------------------------------------
    private class BenchmarkProfileConfig {
        public string Id;
        public string Name;
        public int PCoreMhz;
        public int ECoreMhz;
        public int BoostMode;
        public int Epp;
        public int GpuClockMhz;

        public BenchmarkProfileConfig(string id, string name, int pcore, int ecore, int boost, int epp, int gpu) {
            Id = id;
            Name = name;
            PCoreMhz = pcore;
            ECoreMhz = ecore;
            BoostMode = boost;
            Epp = epp;
            GpuClockMhz = gpu;
        }
    }

    public void CancelBenchmark() {
        _benchmarkCancelRequested = true;
    }

    public void RunBenchmark(
        int iterationsPerProfile,
        int warmupSeconds,
        bool filterOutliers,
        Action<BenchmarkProgress> onProgress,
        Action<List<BenchmarkResult>> onComplete
    ) {
        if (_isBenchmarking) {
            return;
        }

        _isBenchmarking = true;
        _benchmarkCancelRequested = false;

        Thread benchThread = new Thread(delegate() {
            string savedProfile = ActiveProfile;
            List<BenchmarkResult> allResults = new List<BenchmarkResult>();

            // Benchmark Suite Profiles
            List<BenchmarkProfileConfig> suite = new List<BenchmarkProfileConfig>();
            suite.Add(new BenchmarkProfileConfig("snappy", "Snappy-Pacing (Mode 4, EPP 30%, Uncapped)", 0, 0, 4, 30, 0));
            suite.Add(new BenchmarkProfileConfig("clamped", "Clamped 4.9 GHz (Mode 4, EPP 25%, P-Core 4900, E-Core 2800)", 4900, 2800, 4, 25, 0));
            suite.Add(new BenchmarkProfileConfig("cold", "Cold & Quiet / GPU-Shift (Mode 3, EPP 20%, GPU 2100 MHz)", 0, 0, 3, 20, 2100));
            suite.Add(new BenchmarkProfileConfig("guaranteed", "Efficient Guaranteed (Mode 6, EPP 25%, Uncapped)", 0, 0, 6, 25, 0));

            try {
                for (int pIdx = 0; pIdx < suite.Count; pIdx++) {
                    if (_benchmarkCancelRequested) break;

                    BenchmarkProfileConfig cfg = suite[pIdx];
                    BenchmarkResult result = new BenchmarkResult();
                    result.ProfileId = cfg.Id;
                    result.ProfileName = cfg.Name;

                    // 1. Apply Profile Under Test
                    ApplyProfile(cfg.Id);

                    // 2. Warmup Phase (Clocks & Temperatures Stabilization)
                    for (int w = warmupSeconds; w > 0; w--) {
                        if (_benchmarkCancelRequested) break;

                        if (onProgress != null) {
                            BenchmarkProgress prog = new BenchmarkProgress();
                            prog.CurrentProfileName = cfg.Name;
                            prog.CurrentProfileIndex = pIdx + 1;
                            prog.TotalProfiles = suite.Count;
                            prog.CurrentIteration = 0;
                            prog.TotalIterations = iterationsPerProfile;
                            prog.WarmupRemainingSeconds = w;
                            prog.IsWarmingUp = true;
                            prog.StatusMessage = string.Format("Stabilizing clocks: {0} ({1}s left)...", cfg.Name, w);
                            prog.CurrentFps = _currentFps;
                            prog.CurrentCpuPowerW = _currentSnapshot.CpuPowerW;
                            prog.CurrentGpuPowerW = _currentSnapshot.GpuPowerW;
                            try { onProgress(prog); } catch { }
                        }
                        Thread.Sleep(1000);
                    }

                    if (_benchmarkCancelRequested) break;

                    // 3. Sampling Iteration Phase
                    for (int iter = 1; iter <= iterationsPerProfile; iter++) {
                        if (_benchmarkCancelRequested) break;

                        Thread.Sleep(1000);

                        TelemetrySnapshot snap = CurrentSnapshot;
                        BenchmarkSample sample = new BenchmarkSample();
                        sample.Fps = snap.Fps;
                        sample.CpuPowerW = snap.CpuPowerW;
                        sample.GpuPowerW = snap.GpuPowerW;
                        sample.TotalPowerW = snap.TotalPlatformPowerW;
                        sample.PCoreGhz = snap.PCoreGhz;
                        sample.ECoreGhz = snap.ECoreGhz;
                        sample.GpuTempC = snap.GpuTempC;
                        sample.GpuClockMhz = snap.GpuClockMhz;
                        sample.GpuUtilPct = snap.GpuUtilPct;

                        // Outlier Detection 1: Powercut / DC Battery Detection
                        bool isAcPower = IsAcPowerConnected();
                        sample.IsAcPower = isAcPower;
                        if (!isAcPower) {
                            sample.IsOutlier = true;
                            sample.OutlierReason = "Powercut / Running on Battery (DC throttled)";
                        }

                        // Outlier Detection 2: Loading Screen Freeze Detection
                        // If FPS < 20 and GPU Utilization < 15%, game is undergoing level transition/freeze
                        if (sample.Fps < 20.0 && sample.GpuUtilPct < 15) {
                            sample.IsLoadingScreen = true;
                            sample.IsOutlier = true;
                            sample.OutlierReason = string.IsNullOrEmpty(sample.OutlierReason)
                                ? "Loading Screen Freeze (FPS < 20, GPU < 15%)"
                                : sample.OutlierReason + " | Loading Screen Freeze";
                        }

                        result.Samples.Add(sample);

                        if (onProgress != null) {
                            BenchmarkProgress prog = new BenchmarkProgress();
                            prog.CurrentProfileName = cfg.Name;
                            prog.CurrentProfileIndex = pIdx + 1;
                            prog.TotalProfiles = suite.Count;
                            prog.CurrentIteration = iter;
                            prog.TotalIterations = iterationsPerProfile;
                            prog.WarmupRemainingSeconds = 0;
                            prog.IsWarmingUp = false;
                            string outlierNotice = sample.IsOutlier ? " [OUTLIER EXCLUDED]" : "";
                            prog.StatusMessage = string.Format("Sampling #{0}/{1}: {2:F1} FPS | CPU: {3:F1}W | GPU: {4:F1}W{5}",
                                iter, iterationsPerProfile, sample.Fps, sample.CpuPowerW, sample.GpuPowerW, outlierNotice);
                            prog.CurrentFps = sample.Fps;
                            prog.CurrentCpuPowerW = sample.CpuPowerW;
                            prog.CurrentGpuPowerW = sample.GpuPowerW;
                            try { onProgress(prog); } catch { }
                        }
                    }

                    // 4. Compute Raw and Cleaned Aggregates
                    ComputeBenchmarkMetrics(result, filterOutliers);
                    allResults.Add(result);
                }
            } finally {
                // Restore previous active profile and ensure GPU clocks are reset to stock
                ApplyProfile(savedProfile);
                ApplyGpuClockLimit(0);

                _isBenchmarking = false;
                _benchmarkCancelRequested = false;

                if (onComplete != null) {
                    try { onComplete(allResults); } catch { }
                }
            }
        });

        benchThread.IsBackground = true;
        benchThread.Name = "PowerCoreEngine_BenchmarkRunner";
        benchThread.Start();
    }


}
