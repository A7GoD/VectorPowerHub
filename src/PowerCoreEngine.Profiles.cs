using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

public partial class PowerCoreEngine : IDisposable {
    // PROFILE MANAGEMENT
    // ---------------------------------------------------------------------------------------------
    public void ApplyProfile(string profileId) {
        if (string.IsNullOrEmpty(profileId)) return;
        lock (_syncLock) {
            ApplyProfileInternal(profileId.Trim().ToLowerInvariant());
        }
    }

    public void ApplyCustomProfile(int pcoreMhz, int ecoreMhz, int boostMode, int epp, int gpuClockMhz) {
        lock (_syncLock) {
            _customPCoreMhz = pcoreMhz;
            _customECoreMhz = ecoreMhz;
            _customBoostMode = boostMode;
            _customEpp = epp;
            _customGpuClockMhz = gpuClockMhz;

            ApplySettingsInternal("custom", pcoreMhz, ecoreMhz, boostMode, epp, gpuClockMhz);
            _activeProfile = "custom";
            if (_isGameMode) {
                _selectedGamingProfile = "custom";
            }
            SaveUserSettings();
        }
    }

    public string SnappyOverride = "";
    public string ClampedOverride = "";
    public string ColdOverride = "";
    public string GuaranteedOverride = "";
    public string DesktopOverride = "";

    public void ResetProfileOverrides() {
        SnappyOverride = "";
        ClampedOverride = "";
        ColdOverride = "";
        GuaranteedOverride = "";
        DesktopOverride = "";
        SaveUserSettings();
    }

    private void ApplyProfileInternal(string profileId) {
        int maxClock = 5200; 

        // Helper to parse overrides
        Func<string, int[], bool> parseOverride = (ovr, defaults) => {
            if (string.IsNullOrEmpty(ovr)) return false;
            string[] parts = ovr.Split(',');
            if (parts.Length == 5) {
                int.TryParse(parts[0], out defaults[0]);
                int.TryParse(parts[1], out defaults[1]);
                int.TryParse(parts[2], out defaults[2]);
                int.TryParse(parts[3], out defaults[3]);
                int.TryParse(parts[4], out defaults[4]);
                return true;
            }
            return false;
        };

        int[] vals = new int[5];

        switch (profileId) {
            case "snappy":
                vals = new int[] { 0, 0, 4, 30, 0 };
                parseOverride(SnappyOverride, vals);
                ApplySettingsInternal("snappy", vals[0], vals[1], vals[2], vals[3], vals[4]);
                break;
            case "clamped":
                vals = new int[] { (int)(maxClock * 0.85), (int)(maxClock * 0.70), 4, 25, 0 };
                parseOverride(ClampedOverride, vals);
                ApplySettingsInternal("clamped", vals[0], vals[1], vals[2], vals[3], vals[4]);
                break;
            case "cold":
                vals = new int[] { 0, 0, 3, 65, 2100 };
                parseOverride(ColdOverride, vals);
                ApplySettingsInternal("cold", vals[0], vals[1], vals[2], vals[3], vals[4]);
                break;
            case "guaranteed":
                vals = new int[] { 0, 0, 6, 25, 0 };
                parseOverride(GuaranteedOverride, vals);
                ApplySettingsInternal("guaranteed", vals[0], vals[1], vals[2], vals[3], vals[4]);
                break;
            case "desktop":
                vals = new int[] { 0, 0, 3, 50, 0 };
                parseOverride(DesktopOverride, vals);
                ApplySettingsInternal("desktop", vals[0], vals[1], vals[2], vals[3], vals[4]);
                break;
            case "powersaver":
            case "silent":
            case "eco":
                ApplySettingsInternal("powersaver", 0, 0, 0, 80, 0);
                break;
            case "custom":
                ApplySettingsInternal("custom", _customPCoreMhz, _customECoreMhz, _customBoostMode, _customEpp, _customGpuClockMhz);
                break;
            default:
                if (_isGameMode) {
                    ApplySettingsInternal("snappy", 0, 0, 4, 30, 0);
                    profileId = "snappy";
                } else {
                    ApplySettingsInternal("desktop", 0, 0, 3, 50, 0);
                    profileId = "desktop";
                }
                break;
        }

        _activeProfile = profileId;
    }

    private void ApplySettingsInternal(string profileName, int pcoreMhz, int ecoreMhz, int boostMode, int epp, int gpuClockMhz) {
        // 1. Instant in-memory Windows Power Policy update via PowrProf Win32 APIs
        Guid[] schemes = new Guid[] { BalancedSchemeGuid, PowerSaverSchemeGuid, HighPerfSchemeGuid };
        Guid subProc = SubProcessorGuid;
        Guid pcoreGuid = PCoreMaxFreqGuid;
        Guid ecoreGuid = ECoreMaxFreqGuid;
        Guid boostGuid = BoostModeGuid;
        Guid eppGuid1 = EppGuid1;
        Guid eppGuid2 = EppGuid2;

        for (int i = 0; i < schemes.Length; i++) {
            Guid s = schemes[i];
            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref pcoreGuid, (uint)pcoreMhz);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref pcoreGuid, (uint)pcoreMhz);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref ecoreGuid, (uint)ecoreMhz);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref ecoreGuid, (uint)ecoreMhz);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref boostGuid, (uint)boostMode);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref boostGuid, (uint)boostMode);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid1, (uint)epp);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid1, (uint)epp);

            PowrProfNative.PowerWriteACValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid2, (uint)epp);
            PowrProfNative.PowerWriteDCValueIndex(IntPtr.Zero, ref s, ref subProc, ref eppGuid2, (uint)epp);
        }

        Guid activeScheme = BalancedSchemeGuid;
        PowrProfNative.PowerSetActiveScheme(IntPtr.Zero, ref activeScheme);

        // 2. Persist target P-Core frequency for cooperative tools
        try {
            File.WriteAllText(PcoreCapFilePath, pcoreMhz.ToString() + "\n");
        } catch { }

        // 3. One-shot GPU locked clock management (Only at transition, never in polling loop)
        ApplyGpuClockLimit(gpuClockMhz);
    }

    private void ApplyGpuClockLimit(int gpuClockMhz) {
        ThreadPool.QueueUserWorkItem(delegate(object state) {
            try {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "nvidia-smi.exe";
                if (gpuClockMhz > 0) {
                    psi.Arguments = string.Format("-lgc 300,{0}", gpuClockMhz);
                } else {
                    psi.Arguments = "-rgc";
                }
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                using (Process p = Process.Start(psi)) {
                    if (p != null) {
                        p.WaitForExit(2000);
                    }
                }
            } catch { }
        });
    }

    // ---------------------------------------------------------------------------------------------

}


