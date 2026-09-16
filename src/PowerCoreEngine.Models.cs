using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public struct TelemetrySnapshot {
    public double Fps;
    public double CpuPowerW;
    public double PCoreGhz;
    public double ECoreGhz;
    public double[] PerCoreGhz;
    public double[] PerCoreUtil;
    public double GpuPowerW;
    public int GpuClockMhz;
    public int GpuTempC;
    public int GpuUtilPct;
    public bool IsGameMode;
    public string ActiveGameName;
    public int ActiveGamePid;
    public string GpuStatus; // e.g. "D3cold Sleeping (0.0W) • PCIe Link Off" or "Active (D0) • Driving BENQ EX271Q (P8)"
    public double TotalPlatformPowerW;
    public bool IsNvidiaDisplayAttached;
    public string NvidiaMonitorName;
    public string ActiveProfile;
    public string SelectedGamingProfile;
    public string SelectedDesktopProfile;
    public bool AutoProfileSwitching;
}

public struct BenchmarkProgress {
    public string CurrentProfileName;
    public int CurrentProfileIndex; // 1-based
    public int TotalProfiles;
    public int CurrentIteration;    // 1-based
    public int TotalIterations;
    public int WarmupRemainingSeconds;
    public bool IsWarmingUp;
    public string StatusMessage;
    public double CurrentFps;
    public double CurrentCpuPowerW;
    public double CurrentGpuPowerW;
}

public class BenchmarkSample {
    public double Fps;
    public double CpuPowerW;
    public double GpuPowerW;
    public double TotalPowerW;
    public double PCoreGhz;
    public double ECoreGhz;
    public int GpuTempC;
    public int GpuClockMhz;
    public int GpuUtilPct;
    public bool IsAcPower;
    public bool IsLoadingScreen;
    public bool IsOutlier;
    public string OutlierReason;

    public BenchmarkSample() {
        OutlierReason = "";
    }
}

public class BenchmarkResult {
    public string ProfileId;
    public string ProfileName;

    // Raw metrics
    public double RawAvgFps;
    public double RawMinFps;
    public double Raw1PercentLowFps;
    public double RawAvgCpuPowerW;
    public double RawAvgGpuPowerW;
    public double RawAvgTotalPowerW;
    public int RawSampleCount;

    // Cleaned metrics (outliers removed: power cuts, loading screens)
    public double CleanedAvgFps;
    public double CleanedMinFps;
    public double Cleaned1PercentLowFps;
    public double CleanedAvgCpuPowerW;
    public double CleanedAvgGpuPowerW;
    public double CleanedAvgTotalPowerW;
    public int CleanedSampleCount;
    public int DiscardedSampleCount;

    public List<BenchmarkSample> Samples;

    public BenchmarkResult() {
        ProfileId = "";
        ProfileName = "";
        Samples = new List<BenchmarkSample>();
    }
}

public class CpuCore {
    public int Id;
    public byte EfficiencyClass;
    public double CurrentGhz;
    public double CurrentUtil;
    public ulong AffinityMask;
    public bool IsParked;
    public byte Flags;

    public CpuCore() { }

    public CpuCore(int id, byte efficiencyClass) {
        this.Id = id;
        this.EfficiencyClass = efficiencyClass;
    }
}

public class CpuCluster<TCore> where TCore : CpuCore {
    public int ClusterId;
    public string Name;
    public byte EfficiencyClass;
    public List<TCore> Cores;

    public CpuCluster() {
        this.Name = "";
        this.Cores = new List<TCore>();
    }
}

public class CpuCluster : CpuCluster<CpuCore> {
    public CpuCluster() : base() { }

    public CpuCluster(int clusterId, string name, byte efficiencyClass) : base() {
        this.ClusterId = clusterId;
        this.Name = name;
        this.EfficiencyClass = efficiencyClass;
    }
}

public class CpuTopology<TCluster> where TCluster : CpuCluster {
    public List<TCluster> Clusters;
    public int TotalCores;

    public CpuTopology() {
        this.Clusters = new List<TCluster>();
    }
}

public class CpuTopology : CpuTopology<CpuCluster> {
    public CpuTopology() : base() { }
}

