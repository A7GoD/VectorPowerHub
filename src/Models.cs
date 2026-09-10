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
    public class HubTelemetrySnapshot {
        public double Fps = 0.0;
        public double CpuPowerW = 0.0;
        public double PCoreGhz = 0.0;
        public double ECoreGhz = 0.0;
        public double[] PerCoreGhz = new double[24];
        public double[] PerCoreUtil = new double[24];
        public double GpuPowerW = 0.0;
        public int GpuClockMhz = 0;
        public int GpuTempC = 0;
        public int GpuUtilPct = 0;
        public bool IsGameMode = false;
        public string ActiveGameName = "";
        public int ActiveGamePid = 0;
        public string GpuStatus = "D3cold Sleeping (0.0W) • PCIe Link Off";
        public string ActiveProfile = "desktop";
        public string SelectedGamingProfile = "snappy";
        public string SelectedDesktopProfile = "desktop";
        public bool AutoProfileSwitching = true;
        public bool IsNvidiaDisplayAttached = false;
        public string NvidiaMonitorName = "";

        public double TotalPlatformPowerW {
            get { return CpuPowerW + GpuPowerW; }
        }
    }

    public class BenchmarkProgressInfo {
        public string ProfileName = "";
        public string Phase = "Warmup";
        public int CurrentSample = 0;
        public int TotalSamples = 10;
        public int OverallPercent = 0;
        public double CurrentFps = 0.0;
        public double CurrentCpuW = 0.0;
        public double CurrentGpuW = 0.0;
        public bool IsOutlier = false;
        public string OutlierReason = "";
    }

    public class BenchmarkResultInfo {
        public string ProfileId = "";
        public string ProfileName = "";
        public double RawAvgFps = 0.0;
        public double RawOnePercentLow = 0.0;
        public double CleanedAvgFps = 0.0;
        public double CleanedOnePercentLow = 0.0;
        public double AvgCpuPowerW = 0.0;
        public double AvgGpuPowerW = 0.0;
        public double AvgTotalPowerW = 0.0;
        public int OutliersFilteredCount = 0;
        public string OutlierDetails = "None";
        public double EfficiencyScore = 0.0;
        public bool IsWinner = false;
    }


}
