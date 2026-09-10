using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    private static void ComputeBenchmarkMetrics(BenchmarkResult res, bool filterOutliers) {
        if (res.Samples.Count == 0) return;

        // Raw metrics
        double rawFpsSum = 0;
        double rawCpuSum = 0;
        double rawGpuSum = 0;
        double rawTotSum = 0;
        double rawMinFps = double.MaxValue;
        List<double> rawFpsList = new List<double>();

        for (int i = 0; i < res.Samples.Count; i++) {
            BenchmarkSample s = res.Samples[i];
            rawFpsSum += s.Fps;
            rawCpuSum += s.CpuPowerW;
            rawGpuSum += s.GpuPowerW;
            rawTotSum += s.TotalPowerW;
            rawFpsList.Add(s.Fps);
            if (s.Fps < rawMinFps) rawMinFps = s.Fps;
        }

        res.RawSampleCount = res.Samples.Count;
        res.RawAvgFps = rawFpsSum / res.Samples.Count;
        res.RawMinFps = (rawMinFps == double.MaxValue) ? 0.0 : rawMinFps;
        res.Raw1PercentLowFps = Calculate1PercentLow(rawFpsList);
        res.RawAvgCpuPowerW = rawCpuSum / res.Samples.Count;
        res.RawAvgGpuPowerW = rawGpuSum / res.Samples.Count;
        res.RawAvgTotalPowerW = rawTotSum / res.Samples.Count;

        // Cleaned metrics
        List<BenchmarkSample> validSamples = new List<BenchmarkSample>();
        for (int i = 0; i < res.Samples.Count; i++) {
            if (!filterOutliers || !res.Samples[i].IsOutlier) {
                validSamples.Add(res.Samples[i]);
            }
        }

        res.CleanedSampleCount = validSamples.Count;
        res.DiscardedSampleCount = res.Samples.Count - validSamples.Count;

        if (validSamples.Count > 0) {
            double cleanFpsSum = 0;
            double cleanCpuSum = 0;
            double cleanGpuSum = 0;
            double cleanTotSum = 0;
            double cleanMinFps = double.MaxValue;
            List<double> cleanFpsList = new List<double>();

            for (int i = 0; i < validSamples.Count; i++) {
                BenchmarkSample s = validSamples[i];
                cleanFpsSum += s.Fps;
                cleanCpuSum += s.CpuPowerW;
                cleanGpuSum += s.GpuPowerW;
                cleanTotSum += s.TotalPowerW;
                cleanFpsList.Add(s.Fps);
                if (s.Fps < cleanMinFps) cleanMinFps = s.Fps;
            }

            res.CleanedAvgFps = cleanFpsSum / validSamples.Count;
            res.CleanedMinFps = (cleanMinFps == double.MaxValue) ? 0.0 : cleanMinFps;
            res.Cleaned1PercentLowFps = Calculate1PercentLow(cleanFpsList);
            res.CleanedAvgCpuPowerW = cleanCpuSum / validSamples.Count;
            res.CleanedAvgGpuPowerW = cleanGpuSum / validSamples.Count;
            res.CleanedAvgTotalPowerW = cleanTotSum / validSamples.Count;
        } else {
            // Fallback to raw if all samples were discarded
            res.CleanedAvgFps = res.RawAvgFps;
            res.CleanedMinFps = res.RawMinFps;
            res.Cleaned1PercentLowFps = res.Raw1PercentLowFps;
            res.CleanedAvgCpuPowerW = res.RawAvgCpuPowerW;
            res.CleanedAvgGpuPowerW = res.RawAvgGpuPowerW;
            res.CleanedAvgTotalPowerW = res.RawAvgTotalPowerW;
        }
    }

    private static double Calculate1PercentLow(List<double> fpsValues) {
        if (fpsValues == null || fpsValues.Count == 0) return 0.0;
        List<double> sorted = new List<double>(fpsValues);
        sorted.Sort();

        // 1% lowest samples count (at least 1 sample)
        int lowCount = (int)Math.Ceiling(sorted.Count * 0.01);
        if (lowCount < 1) lowCount = 1;
        if (lowCount > sorted.Count) lowCount = sorted.Count;

        double sum = 0.0;
        for (int i = 0; i < lowCount; i++) {
            sum += sorted[i];
        }
        return sum / lowCount;
    }

    private static bool IsAcPowerConnected() {
        try {
            Win32Native.SYSTEM_POWER_STATUS status;
            if (Win32Native.GetSystemPowerStatus(out status)) {
                // ACLineStatus: 0 = Offline, 1 = Online, 255 = Unknown
                return status.ACLineStatus == 1;
            }
        } catch { }
        return true;
    }

    // ---------------------------------------------------------------------------------------------

}
