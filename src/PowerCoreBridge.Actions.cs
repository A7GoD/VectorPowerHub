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

        public void SetAutoProfileSwitching(bool enabled) {
            if (isEngineLoaded && setAutoSwitchMethod != null && engineInstance != null) {
                try {
                    setAutoSwitchMethod.Invoke(engineInstance, new object[] { enabled });
                } catch { }
            }
        }

        public void SetGamingProfile(string profileId) {
            if (isEngineLoaded && setGamingProfileMethod != null && engineInstance != null) {
                try {
                    setGamingProfileMethod.Invoke(engineInstance, new object[] { profileId });
                    return;
                } catch { }
            }
        }

        public void SetDesktopProfile(string profileId) {
            if (isEngineLoaded && setDesktopProfileMethod != null && engineInstance != null) {
                try {
                    setDesktopProfileMethod.Invoke(engineInstance, new object[] { profileId });
                    return;
                } catch { }
            }
        }

        public void SetSelectedDesktopProfile(string profileId) {
            SetDesktopProfile(profileId);
        }

        public void SetBenchmarking(bool benchmarking) {
            if (isEngineLoaded && setBenchmarkingMethod != null && engineInstance != null) {
                try {
                    setBenchmarkingMethod.Invoke(engineInstance, new object[] { benchmarking });
                } catch { }
            }
        }

        public void SetTrayMinimized(bool minimized) {
            if (isEngineLoaded && setTrayMinimizedMethod != null && engineInstance != null) {
                try {
                    setTrayMinimizedMethod.Invoke(engineInstance, new object[] { minimized });
                } catch { }
            }
        }

        private void ExecuteFallbackProfile(string profileId) {
            if (profileId == "snappy") {
                ApplyPowerCfgValues(0, 0, 4, 30);
                RunCmd("nvidia-smi -rgc");
            } else if (profileId == "clamped") {
                ApplyPowerCfgValues(4900, 2800, 4, 25);
                RunCmd("nvidia-smi -rgc");
            } else if (profileId == "cold") {
                ApplyPowerCfgValues(0, 0, 3, 20);
                RunCmd("nvidia-smi -lgc 300,2100");
            } else if (profileId == "guaranteed") {
                ApplyPowerCfgValues(0, 0, 6, 25);
                RunCmd("nvidia-smi -rgc");
            } else if (profileId == "powersaver" || profileId == "silent" || profileId == "eco") {
                ApplyPowerCfgValues(0, 0, 0, 80);
                RunCmd("nvidia-smi -rgc");
            } else if (profileId == "desktop") {
                ApplyPowerCfgValues(0, 0, 3, 50);
                RunCmd("nvidia-smi -rgc");
            }
        }

        private void ApplyPowerCfgValues(int pcore, int ecore, int boostMode, int epp) {
            string[] schemes = new string[] {
                "381b4222-f694-41f0-9685-ff5bb260df2e",
                "961cc777-2547-4f9d-8174-7d86181b8a7a",
                "ded574b5-45a0-4f42-8737-46345c09c238"
            };
            foreach (string s in schemes) {
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e101 {1}", s, pcore));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e101 {1}", s, pcore));
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 {1}", s, ecore));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 75b0ae3f-bce0-45a7-8c89-c9611c25e100 {1}", s, ecore));
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 {1}", s, boostMode));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR be337238-0d82-4146-a960-4f3749d470c7 {1}", s, boostMode));
                RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {1}", s, epp));
                RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 36687f9e-e3a5-4dbf-b1dc-15eb381c6863 {1}", s, epp));
            }
            RunCmd("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e");

            try {
                File.WriteAllText(@"C:\Users\a7god\game_pcore_cap.txt", pcore.ToString() + "\n");
            } catch { }
        }

        private void RunCmd(string cmd) {
            try {
                int firstSpace = cmd.IndexOf(' ');
                string exe = (firstSpace > 0) ? cmd.Substring(0, firstSpace) : cmd;
                string args = (firstSpace > 0) ? cmd.Substring(firstSpace + 1) : "";
                ProcessStartInfo psi = new ProcessStartInfo(exe, args);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                using (Process p = Process.Start(psi)) {
                    if (p != null) {
                        p.WaitForExit(1000);
                    }
                }
            } catch { }
        }

        private double[] ReadDoubleArray(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) {
                object val = f.GetValue(obj);
                if (val is double[]) return (double[])val;
            }
            PropertyInfo p = t.GetProperty(name);
            if (p != null) {
                object val = p.GetValue(obj, null);
                if (val is double[]) return (double[])val;
            }
            return new double[24];
        }

        private double ReadDouble(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) return Convert.ToDouble(f.GetValue(obj));
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return Convert.ToDouble(p.GetValue(obj, null));
            return 0.0;
        }

        private int ReadInt(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) return Convert.ToInt32(f.GetValue(obj));
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return Convert.ToInt32(p.GetValue(obj, null));
            return 0;
        }

        private bool ReadBool(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) return Convert.ToBoolean(f.GetValue(obj));
            PropertyInfo p = t.GetProperty(name);
            if (p != null) return Convert.ToBoolean(p.GetValue(obj, null));
            return false;
        }

        private string ReadString(Type t, object obj, string name) {
            FieldInfo f = t.GetField(name);
            if (f != null) {
                object val = f.GetValue(obj);
                return val != null ? val.ToString() : "";
            }
            PropertyInfo p = t.GetProperty(name);
            if (p != null) {
                object val = p.GetValue(obj, null);
                return val != null ? val.ToString() : "";
            }
            return "";
        }


    }
}
