using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

public partial class PowerCoreEngine : IDisposable {
    // CPU TELEMETRY (PDH P/INVOKE)
    // ---------------------------------------------------------------------------------------------
    private void InitPdh() {
        try {
            if (_hPdhQuery != IntPtr.Zero) {
                PdhNative.PdhCloseQuery(_hPdhQuery);
                _hPdhQuery = IntPtr.Zero;
            }

            BuildTopology();

            uint res = PdhNative.PdhOpenQueryW(null, IntPtr.Zero, out _hPdhQuery);
            if (res == 0) {
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Energy Meter(RAPL_Package0_PKG)\Power", IntPtr.Zero, out _hPdhCounterPwr);

                int firstPCoreId = 0, firstECoreId = 0;
                if (this.Topology != null && this.Topology.Clusters != null) {
                    for (int c = 0; c < this.Topology.Clusters.Count; c++) {
                        CpuCluster cl = this.Topology.Clusters[c];
                        if (cl.Cores != null && cl.Cores.Count > 0) {
                            if (cl.EfficiencyClass > 0 && firstPCoreId == 0) firstPCoreId = cl.Cores[0].Id;
                            else if (cl.EfficiencyClass == 0 && firstECoreId == 0) firstECoreId = cl.Cores[0].Id;
                        }
                    }
                }
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Performance", firstPCoreId), IntPtr.Zero, out _hPdhCounterPCore);
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Performance", firstECoreId), IntPtr.Zero, out _hPdhCounterECore);

                int totalCores = (this.Topology != null && this.Topology.TotalCores > 0) ? this.Topology.TotalCores : Environment.ProcessorCount;
                _hPdhCounterPerCore = new IntPtr[totalCores];
                _hPdhCounterPerCoreUtil = new IntPtr[totalCores];

                int coreIdx = 0;
                if (this.Topology != null && this.Topology.Clusters != null) {
                    for (int c = 0; c < this.Topology.Clusters.Count; c++) {
                        CpuCluster cluster = this.Topology.Clusters[c];
                        if (cluster.Cores == null) continue;
                        for (int k = 0; k < cluster.Cores.Count; k++) {
                            if (coreIdx >= totalCores) break;
                            CpuCore core = cluster.Cores[k];
                            PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Performance", core.Id), IntPtr.Zero, out _hPdhCounterPerCore[coreIdx]);
                            PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Utility", core.Id), IntPtr.Zero, out _hPdhCounterPerCoreUtil[coreIdx]);
                            coreIdx++;
                        }
                    }
                }

                while (coreIdx < totalCores) {
                    PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Performance", coreIdx), IntPtr.Zero, out _hPdhCounterPerCore[coreIdx]);
                    PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Utility", coreIdx), IntPtr.Zero, out _hPdhCounterPerCoreUtil[coreIdx]);
                    coreIdx++;
                }

                // Initial baseline sample
                PdhNative.PdhCollectQueryData(_hPdhQuery);
                _isPdhInitialized = true;
            }
        } catch {
            _isPdhInitialized = false;
        }
    }

    private void ReadCpuTelemetry(out double cpuWatts, out double pCoreGhz, out double eCoreGhz, out double[] perCoreGhz, out double[] perCoreUtil) {
        cpuWatts = 0.0;
        pCoreGhz = 0.0;
        eCoreGhz = 0.0;

        int totalCores = (_hPdhCounterPerCore != null && _hPdhCounterPerCore.Length > 0)
            ? _hPdhCounterPerCore.Length
            : ((this.Topology != null && this.Topology.TotalCores > 0) ? this.Topology.TotalCores : Environment.ProcessorCount);

        perCoreGhz = new double[totalCores];
        perCoreUtil = new double[totalCores];

        if (!_isPdhInitialized || _hPdhQuery == IntPtr.Zero) {
            InitPdh();
            return;
        }

        try {
            if (PdhNative.PdhCollectQueryData(_hPdhQuery) == 0) {
                if (_hPdhCounterPwr != IntPtr.Zero) {
                    PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                    if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPwr, PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                        cpuWatts = val.doubleValue / 1000.0; // Counter reports milliwatts -> Watts
                        if (cpuWatts < 0.0) cpuWatts = 0.0;
                    }
                }

                double pSum = 0; int pCount = 0;
                double eSum = 0; int eCount = 0;
                int idx = 0;

                if (this.Topology != null && this.Topology.Clusters != null && this.Topology.Clusters.Count > 0) {
                    for (int c = 0; c < this.Topology.Clusters.Count; c++) {
                        CpuCluster cluster = this.Topology.Clusters[c];
                        if (cluster.Cores == null) continue;
                        for (int k = 0; k < cluster.Cores.Count; k++) {
                            if (idx >= totalCores) break;
                            CpuCore core = cluster.Cores[k];

                            double ghz = 0.0;
                            double u = 0.0;

                            if (_hPdhCounterPerCore != null && idx < _hPdhCounterPerCore.Length && _hPdhCounterPerCore[idx] != IntPtr.Zero) {
                                PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                                if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCore[idx], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                                    double nominal = (core.EfficiencyClass > 0) ? 2.7 : 2.1;
                                    ghz = (val.doubleValue / 100.0) * nominal;
                                    if (ghz < 0.0) ghz = 0.0;
                                }
                            }

                            if (_hPdhCounterPerCoreUtil != null && idx < _hPdhCounterPerCoreUtil.Length && _hPdhCounterPerCoreUtil[idx] != IntPtr.Zero) {
                                PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                                if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCoreUtil[idx], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                                    u = val.doubleValue;
                                    if (u < 0.0) u = 0.0;
                                    if (u > 100.0) u = 100.0;
                                }
                            }

                            perCoreGhz[idx] = ghz;
                            perCoreUtil[idx] = u;
                            core.CurrentGhz = ghz;
                            core.CurrentUtil = u;

                            if (core.EfficiencyClass > 0) { pSum += ghz; pCount++; }
                            else { eSum += ghz; eCount++; }
                            idx++;
                        }
                    }
                }

                while (idx < totalCores) {
                    double ghz = 0.0;
                    double u = 0.0;

                    if (_hPdhCounterPerCore != null && idx < _hPdhCounterPerCore.Length && _hPdhCounterPerCore[idx] != IntPtr.Zero) {
                        PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCore[idx], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                            double nominal = (idx < 8) ? 2.7 : 2.1;
                            ghz = (val.doubleValue / 100.0) * nominal;
                            if (ghz < 0.0) ghz = 0.0;
                        }
                    }

                    if (_hPdhCounterPerCoreUtil != null && idx < _hPdhCounterPerCoreUtil.Length && _hPdhCounterPerCoreUtil[idx] != IntPtr.Zero) {
                        PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCoreUtil[idx], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                            u = val.doubleValue;
                            if (u < 0.0) u = 0.0;
                            if (u > 100.0) u = 100.0;
                        }
                    }

                    perCoreGhz[idx] = ghz;
                    perCoreUtil[idx] = u;
                    if (idx < 8) { pSum += ghz; pCount++; }
                    else { eSum += ghz; eCount++; }
                    idx++;
                }

                pCoreGhz = (pCount > 0) ? (pSum / pCount) : 0.0;
                eCoreGhz = (eCount > 0) ? (eSum / eCount) : 0.0;
            }
        } catch { }
    }

    private void ClosePdh() {
        if (_hPdhQuery != IntPtr.Zero) {
            try {
                PdhNative.PdhCloseQuery(_hPdhQuery);
            } catch { }
            _hPdhQuery = IntPtr.Zero;
            _isPdhInitialized = false;
        }
    }

    // ---------------------------------------------------------------------------------------------

}
