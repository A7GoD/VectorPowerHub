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
    public partial class PowerCoreBridge : IDisposable {
        public HubTelemetrySnapshot GetSnapshot() {
            HubTelemetrySnapshot snapshot = new HubTelemetrySnapshot();

            // 1. Try reading from PowerCoreEngine if available
            if (isEngineLoaded && currentSnapshotProp != null && engineInstance != null) {
                try {
                    object snapObj = currentSnapshotProp.GetValue(engineInstance, null);
                    if (snapObj != null) {
                        Type t = snapObj.GetType();
                        snapshot.Fps = ReadDouble(t, snapObj, "Fps");
                        snapshot.CpuPowerW = ReadDouble(t, snapObj, "CpuPowerW");
                        snapshot.PCoreGhz = ReadDouble(t, snapObj, "PCoreGhz");
                        snapshot.ECoreGhz = ReadDouble(t, snapObj, "ECoreGhz");
                        snapshot.GpuPowerW = ReadDouble(t, snapObj, "GpuPowerW");
                        snapshot.GpuClockMhz = ReadInt(t, snapObj, "GpuClockMhz");
                        snapshot.GpuTempC = ReadInt(t, snapObj, "GpuTempC");
                        snapshot.GpuUtilPct = ReadInt(t, snapObj, "GpuUtilPct");
                        snapshot.IsGameMode = ReadBool(t, snapObj, "IsGameMode");
                        snapshot.ActiveGameName = ReadString(t, snapObj, "ActiveGameName");
                        snapshot.ActiveGamePid = ReadInt(t, snapObj, "ActiveGamePid");
                        snapshot.GpuStatus = ReadString(t, snapObj, "GpuStatus");
                        snapshot.PerCoreGhz = ReadDoubleArray(t, snapObj, "PerCoreGhz");
                        snapshot.PerCoreUtil = ReadDoubleArray(t, snapObj, "PerCoreUtil");
                        snapshot.IsNvidiaDisplayAttached = ReadBool(t, snapObj, "IsNvidiaDisplayAttached");
                        snapshot.NvidiaMonitorName = ReadString(t, snapObj, "NvidiaMonitorName");
                        snapshot.ActiveProfile = ReadString(t, snapObj, "ActiveProfile");
                        snapshot.SelectedGamingProfile = ReadString(t, snapObj, "SelectedGamingProfile");
                        snapshot.SelectedDesktopProfile = ReadString(t, snapObj, "SelectedDesktopProfile");
                        snapshot.AutoProfileSwitching = ReadBool(t, snapObj, "AutoProfileSwitching");
                        return snapshot;
                    }
                } catch { }
            }

            // 2. Fallback Standalone Telemetry Sampling
            try {
                string fpsFile = @"C:\Users\a7god\game_fps.txt";
                if (File.Exists(fpsFile)) {
                    string txt = File.ReadAllText(fpsFile).Trim();
                    double fVal;
                    if (double.TryParse(txt, out fVal)) {
                        snapshot.Fps = fVal;
                        snapshot.IsGameMode = (fVal > 0.1);
                    }
                }
            } catch { }

            if (pdhQuery != IntPtr.Zero) {
                try {
                    PdhCollectQueryData(pdhQuery);
                    PDH_FMT_COUNTERVALUE_DOUBLE val;
                    if (PdhGetFormattedCounterValue(pwrCounter, PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        snapshot.CpuPowerW = val.doubleValue / 1000.0;
                    }
                    if (PdhGetFormattedCounterValue(pcoreCounter, PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        snapshot.PCoreGhz = (val.doubleValue / 100.0) * 2.7;
                    }
                    if (PdhGetFormattedCounterValue(ecoreCounter, PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        snapshot.ECoreGhz = (val.doubleValue / 100.0) * 2.2;
                    }
                } catch { }
            }

            // D3cold Safe Rule: Only query if game rendering
            if (snapshot.IsGameMode) {
                try {
                    ProcessStartInfo psi = new ProcessStartInfo("nvidia-smi", "--query-gpu=power.draw,temperature.gpu,clocks.current.graphics,utilization.gpu --format=csv,noheader,nounits");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    psi.RedirectStandardOutput = true;
                    using (Process p = Process.Start(psi)) {
                        if (p != null) {
                            string line = p.StandardOutput.ReadToEnd();
                            p.WaitForExit(500);
                            string[] parts = line.Trim().Split(',');
                            if (parts.Length >= 4) {
                                double.TryParse(parts[0].Trim(), out snapshot.GpuPowerW);
                                int.TryParse(parts[1].Trim(), out snapshot.GpuTempC);
                                int.TryParse(parts[2].Trim(), out snapshot.GpuClockMhz);
                                int.TryParse(parts[3].Trim(), out snapshot.GpuUtilPct);
                                snapshot.GpuStatus = "Active Rendering (D0) • Full 140W Dynamic Headroom";
                            }
                        }
                    }
                } catch { }
            } else {
                snapshot.GpuPowerW = 0.0;
                snapshot.GpuClockMhz = 0;
                snapshot.GpuTempC = 0;
                snapshot.GpuUtilPct = 0;
                snapshot.GpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";
            }

            return snapshot;
        }

        public void ApplyProfile(string profileId) {
            if (isEngineLoaded && applyProfileMethod != null && engineInstance != null) {
                try {
                    applyProfileMethod.Invoke(engineInstance, new object[] { profileId });
                    return;
                } catch { }
            }

            ExecuteFallbackProfile(profileId);
        }

        public void ApplyCustomProfile(int pcore, int ecore, int boostMode, int epp, int gpuClock) {
            if (isEngineLoaded && applyCustomMethod != null && engineInstance != null) {
                try {
                    applyCustomMethod.Invoke(engineInstance, new object[] { pcore, ecore, boostMode, epp, gpuClock });
                    return;
                } catch { }
            }

            ApplyPowerCfgValues(pcore, ecore, boostMode, epp);
            if (gpuClock > 0) {
                RunCmd(string.Format("nvidia-smi -lgc 300,{0}", gpuClock));
            } else {
                RunCmd("nvidia-smi -rgc");
            }
        }

    }
}
