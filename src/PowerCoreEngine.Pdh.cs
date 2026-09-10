using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // CPU TELEMETRY (PDH P/INVOKE)
    // ---------------------------------------------------------------------------------------------
    private void InitPdh() {
        try {
            if (_hPdhQuery != IntPtr.Zero) {
                PdhNative.PdhCloseQuery(_hPdhQuery);
                _hPdhQuery = IntPtr.Zero;
            }

            uint res = PdhNative.PdhOpenQueryW(null, IntPtr.Zero, out _hPdhQuery);
            if (res == 0) {
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Energy Meter(RAPL_Package0_PKG)\Power", IntPtr.Zero, out _hPdhCounterPwr);
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Processor Information(0,0)\% Processor Performance", IntPtr.Zero, out _hPdhCounterPCore);
                PdhNative.PdhAddEnglishCounterW(_hPdhQuery, @"\Processor Information(0,14)\% Processor Performance", IntPtr.Zero, out _hPdhCounterECore);

                _hPdhCounterPerCore = new IntPtr[24];
                _hPdhCounterPerCoreUtil = new IntPtr[24];
                for (int i = 0; i < 24; i++) {
                    PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Performance", i), IntPtr.Zero, out _hPdhCounterPerCore[i]);
                    PdhNative.PdhAddEnglishCounterW(_hPdhQuery, string.Format(@"\Processor Information(0,{0})\% Processor Utility", i), IntPtr.Zero, out _hPdhCounterPerCoreUtil[i]);
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
        perCoreGhz = new double[24];
        perCoreUtil = new double[24];

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

                for (int i = 0; i < 24; i++) {
                    if (_hPdhCounterPerCore != null && _hPdhCounterPerCore[i] != IntPtr.Zero) {
                        PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCore[i], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                            double nominal = (i < 8) ? 2.7 : 2.1;
                            double ghz = (val.doubleValue / 100.0) * nominal;
                            if (ghz < 0.0) ghz = 0.0;
                            perCoreGhz[i] = ghz;
                            if (i < 8) { pSum += ghz; pCount++; }
                            else { eSum += ghz; eCount++; }
                        }
                    }

                    if (_hPdhCounterPerCoreUtil != null && _hPdhCounterPerCoreUtil[i] != IntPtr.Zero) {
                        PdhNative.PDH_FMT_COUNTERVALUE_DOUBLE val;
                        if (PdhNative.PdhGetFormattedCounterValue(_hPdhCounterPerCoreUtil[i], PdhNative.PDH_FMT_DOUBLE, IntPtr.Zero, out val) == 0) {
                            double u = val.doubleValue;
                            if (u < 0.0) u = 0.0;
                            if (u > 100.0) u = 100.0;
                            perCoreUtil[i] = u;
                        }
                    }
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
