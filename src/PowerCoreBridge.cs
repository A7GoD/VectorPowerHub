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
        private object engineInstance = null;
        private MethodInfo applyProfileMethod = null;
        private MethodInfo applyCustomMethod = null;
        private MethodInfo setAutoSwitchMethod = null;
        private MethodInfo setGamingProfileMethod = null;
        private MethodInfo setDesktopProfileMethod = null;
        private MethodInfo setBenchmarkingMethod = null;
        private PropertyInfo currentSnapshotProp = null;
        private bool isEngineLoaded = false;

        // Fallback PDH counter handles (when running standalone)
        private IntPtr pdhQuery = IntPtr.Zero;
        private IntPtr pwrCounter = IntPtr.Zero;
        private IntPtr pcoreCounter = IntPtr.Zero;
        private IntPtr ecoreCounter = IntPtr.Zero;

        [StructLayout(LayoutKind.Explicit)]
        private struct PDH_FMT_COUNTERVALUE_DOUBLE {
            [FieldOffset(0)]
            public uint CStatus;
            [FieldOffset(8)]
            public double doubleValue;
        }

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint PdhOpenQueryW(string szDataSource, IntPtr dwUserData, out IntPtr phQuery);

        [DllImport("pdh.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint PdhAddEnglishCounterW(IntPtr hQuery, string szFullCounterPath, IntPtr dwUserData, out IntPtr phCounter);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern uint PdhCollectQueryData(IntPtr hQuery);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern uint PdhGetFormattedCounterValue(IntPtr hCounter, uint dwFormat, IntPtr lpdwType, out PDH_FMT_COUNTERVALUE_DOUBLE pValue);

        [DllImport("pdh.dll", SetLastError = true)]
        private static extern uint PdhCloseQuery(IntPtr hQuery);

        private const uint PDH_FMT_DOUBLE = 0x00000200;

        public PowerCoreBridge() {
            TryConnectEngine();
            if (!isEngineLoaded) {
                InitPdhFallback();
            }
        }

        private void TryConnectEngine() {
            try {
                Type engineType = null;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) {
                    engineType = asm.GetType("PowerCoreEngine");
                    if (engineType == null) engineType = asm.GetType("VectorPowerHub.PowerCoreEngine");
                    if (engineType != null) break;
                }

                if (engineType != null) {
                    PropertyInfo instanceProp = engineType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instanceProp != null) {
                        engineInstance = instanceProp.GetValue(null, null);
                    }
                    if (engineInstance == null) {
                        engineInstance = Activator.CreateInstance(engineType);
                    }

                    applyProfileMethod = engineType.GetMethod("ApplyProfile");
                    applyCustomMethod = engineType.GetMethod("ApplyCustomProfile");
                    setAutoSwitchMethod = engineType.GetMethod("SetAutoProfileSwitching");
                    setGamingProfileMethod = engineType.GetMethod("SetGamingProfile");
                    setDesktopProfileMethod = engineType.GetMethod("SetDesktopProfile") ?? engineType.GetMethod("SetSelectedDesktopProfile");
                    setBenchmarkingMethod = engineType.GetMethod("SetBenchmarking");
                    currentSnapshotProp = engineType.GetProperty("CurrentSnapshot");

                    MethodInfo startMethod = engineType.GetMethod("Start");
                    if (startMethod != null && engineInstance != null) {
                        startMethod.Invoke(engineInstance, null);
                    }

                    isEngineLoaded = (engineInstance != null);
                }
            } catch {
                isEngineLoaded = false;
            }
        }

        private void InitPdhFallback() {
            try {
                if (PdhOpenQueryW(null, IntPtr.Zero, out pdhQuery) == 0) {
                    PdhAddEnglishCounterW(pdhQuery, @"\Energy Meter(RAPL_Package0_PKG)\Power", IntPtr.Zero, out pwrCounter);
                    PdhAddEnglishCounterW(pdhQuery, @"\Processor Information(0,0)\% Processor Performance", IntPtr.Zero, out pcoreCounter);
                    PdhAddEnglishCounterW(pdhQuery, @"\Processor Information(0,14)\% Processor Performance", IntPtr.Zero, out ecoreCounter);
                    PdhCollectQueryData(pdhQuery);
                }
            } catch { }
        }


        public void Dispose() {
            if (pdhQuery != IntPtr.Zero) {
                try { PdhCloseQuery(pdhQuery); } catch { }
                pdhQuery = IntPtr.Zero;
            }
        }

    }
}
