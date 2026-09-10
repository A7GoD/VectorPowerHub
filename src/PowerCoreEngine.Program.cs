using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

#if POWER_CORE_EXE
class Program {
    static void Main(string[] args) {
        Console.WriteLine("==================================================================");
        Console.WriteLine(" PowerCoreEngine Telemetry & Profile Management Engine (C# .NET)");
        Console.WriteLine("==================================================================");
        PowerCoreEngine engine = PowerCoreEngine.Instance;
        engine.OnTelemetryUpdated += delegate(object sender, TelemetrySnapshot snap) {
            Console.WriteLine(string.Format(
                "[{0:HH:mm:ss}] Game: {1} (PID: {2}) | FPS: {3:F1} | CPU: {4:F1}W (P:{5:F2}G E:{6:F2}G) | GPU: {7:F1}W ({8}MHz {9}C {10}%) [{11}] | Tot: {12:F1}W | Prof: {13}",
                DateTime.Now,
                snap.IsGameMode ? snap.ActiveGameName : "No Game",
                snap.ActiveGamePid,
                snap.Fps,
                snap.CpuPowerW,
                snap.PCoreGhz,
                snap.ECoreGhz,
                snap.GpuPowerW,
                snap.GpuClockMhz,
                snap.GpuTempC,
                snap.GpuUtilPct,
                snap.GpuStatus,
                snap.TotalPlatformPowerW,
                engine.ActiveProfile
            ));
        };

        engine.Start();
        Console.WriteLine("Engine started. Listening to DXGI ETW, PDH, NVML, and Modern Standby Power Events.");
        if (args.Length > 0 && args[0].ToLower() == "-test") {
            Console.WriteLine("Running 3-second self test...");
            Thread.Sleep(3000);
        } else if (args.Length > 0 && args[0].ToLower() == "-bench") {
            Console.WriteLine("Starting benchmark test: 1 iteration per profile, 1s warmup...");
            engine.RunBenchmark(1, 1, true,
                delegate(BenchmarkProgress prog) {
                    Console.WriteLine(string.Format("[BENCH PROGRESS] Profile {0}/{1} ({2}): {3}",
                        prog.CurrentProfileIndex, prog.TotalProfiles, prog.CurrentProfileName, prog.StatusMessage));
                },
                delegate(List<BenchmarkResult> results) {
                    Console.WriteLine("\n[BENCHMARK COMPLETE] Summary:");
                    for (int i = 0; i < results.Count; i++) {
                        BenchmarkResult r = results[i];
                        Console.WriteLine(string.Format("  {0}: Cleaned FPS: {1:F1} (1% Low: {2:F1}) | CPU: {3:F1}W | GPU: {4:F1}W | Samples: {5} (Discarded: {6})",
                            r.ProfileName, r.CleanedAvgFps, r.Cleaned1PercentLowFps, r.CleanedAvgCpuPowerW, r.CleanedAvgGpuPowerW, r.CleanedSampleCount, r.DiscardedSampleCount));
                    }
                }
            );
            while (engine.IsBenchmarking) {
                Thread.Sleep(500);
            }
        } else {
            Console.WriteLine("Press Enter or Ctrl+C to stop...");
            Console.ReadLine();
        }
        engine.Stop();
        Console.WriteLine("Engine stopped cleanly.");
    }
}
#endif
