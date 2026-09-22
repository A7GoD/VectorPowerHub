using System;
using System.IO;
using System.Diagnostics;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace VectorPowerHub {
    public class OsOptimizationsConfig {
        public int WifiMimoMode { get; set; }
        public bool WifiThroughputBooster { get; set; }
        public bool WifiEee { get; set; }
        public bool WifiPacketCoalescing { get; set; }
        public bool PreventUsbWakeLag { get; set; }
        public bool UsbSelectiveSuspendBattery { get; set; }
        public int AudioIdleTimeout { get; set; }
        public int PcieAspm { get; set; }
        public int NvmeSleepTimeout { get; set; }
        public bool DisableBloatware { get; set; }
        
        public int ProcAutoActivityWindow { get; set; }
        public int CoreParkingThreshold { get; set; }
        public int ProcPerfIncreaseThreshold { get; set; }
        public int CoreParkingDecreasePolicy { get; set; }
        public int ProcPerfDecreaseThreshold { get; set; }
        public int DiskAhciLinkPowerManagement { get; set; }
        public int GpuPreferencePolicy { get; set; }
        public int HibernateAfterSleep { get; set; }
        public int AutonomousHwp { get; set; }
        public int IntelGraphicsPowerPlan { get; set; }

        public OsOptimizationsConfig() {
            WifiMimoMode = 0;
            WifiThroughputBooster = true;
            WifiEee = true;
            WifiPacketCoalescing = true;
            PreventUsbWakeLag = true;
            UsbSelectiveSuspendBattery = true;
            AudioIdleTimeout = 5;
            PcieAspm = 2;
            NvmeSleepTimeout = 50;
            DisableBloatware = true;
            
            ProcAutoActivityWindow = 2000000;
            CoreParkingThreshold = 85;
            ProcPerfIncreaseThreshold = 90;
            CoreParkingDecreasePolicy = 0;
            ProcPerfDecreaseThreshold = 5;
            DiskAhciLinkPowerManagement = 4;
            GpuPreferencePolicy = 1;
            HibernateAfterSleep = 0;
            AutonomousHwp = 1;
            IntelGraphicsPowerPlan = 0;
        }
    }

    public static class OsOptimizationsManager {
        public static OsOptimizationsConfig Config = new OsOptimizationsConfig();
        
        private static string GetConfigPath() {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VectorPowerHub");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return Path.Combine(dir, "os_optimizations.json");
        }

        public static void Load() {
            try {
                string path = GetConfigPath();
                if (File.Exists(path)) {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    Config = js.Deserialize<OsOptimizationsConfig>(File.ReadAllText(path));
                }
            } catch { }
        }

        public static void Save() {
            try {
                JavaScriptSerializer js = new JavaScriptSerializer();
                File.WriteAllText(GetConfigPath(), js.Serialize(Config));
            } catch { }
        }

        public static void ApplyAll() {
            Load();
            try {
                // 1. Wi-Fi
                string mimo = Config.WifiMimoMode == 0 ? "Auto SMPS" : (Config.WifiMimoMode == 1 ? "Static SMPS" : "No SMPS");
                string tb = Config.WifiThroughputBooster ? "Enabled" : "Disabled";
                string eee = Config.WifiEee ? "Enabled" : "Disabled";
                string pc = Config.WifiPacketCoalescing ? "Enabled" : "Disabled";
                
                string script = "$a = Get-NetAdapter | ? {$_.InterfaceDescription -match 'BE1750|Killer|Wi-Fi|Wireless'}; if($a){ Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'MIMO Power Save Mode' -DisplayValue '" + mimo + "' -ErrorAction SilentlyContinue; Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Throughput Booster' -DisplayValue '" + tb + "' -ErrorAction SilentlyContinue; Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Energy Efficient Ethernet' -DisplayValue '" + eee + "' -ErrorAction SilentlyContinue; Set-NetAdapterAdvancedProperty -Name $a.Name -DisplayName 'Packet Coalescing' -DisplayValue '" + pc + "' -ErrorAction SilentlyContinue; }";

                // 2. Bloatware
                if (Config.DisableBloatware) {
                    script += " 'LightKeeperService', 'Mystic_Light_Service', 'LEDKeeper2', 'NahimicService', 'GamingServices', 'GamingServicesNet' | % { Stop-Service $_ -Force -ErrorAction SilentlyContinue; Set-Service $_ -StartupType Disabled -ErrorAction SilentlyContinue };";
                }
                
                ProcessStartInfo psi = new ProcessStartInfo("powershell", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + script + "\"");
                psi.CreateNoWindow = true; psi.UseShellExecute = false; Process.Start(psi);

                // 3. Audio Idle
                try {
                    using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}")) {
                        if (k != null) {
                            foreach (string sk in k.GetSubKeyNames()) {
                                using (RegistryKey pk = k.OpenSubKey(sk + @"\PowerSettings", true)) {
                                    if (pk != null) {
                                        byte[] t = BitConverter.GetBytes(Config.AudioIdleTimeout);
                                        if (Config.AudioIdleTimeout > 0) {
                                            pk.SetValue("ConservationIdleTime", t, RegistryValueKind.Binary);
                                            pk.SetValue("PerformanceIdleTime", t, RegistryValueKind.Binary);
                                            pk.SetValue("IdlePowerState", new byte[] {3,0,0,0}, RegistryValueKind.Binary);
                                        } else {
                                            pk.SetValue("ConservationIdleTime", new byte[] {0,0,0,0}, RegistryValueKind.Binary);
                                            pk.SetValue("PerformanceIdleTime", new byte[] {0,0,0,0}, RegistryValueKind.Binary);
                                            pk.SetValue("IdlePowerState", new byte[] {0,0,0,0}, RegistryValueKind.Binary);
                                        }
                                    }
                                }
                            }
                        }
                    }
                } catch { }

                // 4. Powercfg settings (ASPM, NVMe, USB)
                string[] schemes = new string[] { "381b4222-f694-41f0-9685-ff5bb260df2e", "961cc777-2547-4f9d-8174-7d86181b8a7a", "ded574b5-45a0-4f42-8737-46345c09c238" };
                foreach (string s in schemes) {
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PCIEXPRESS ee12f906-d277-404b-b6da-e5fa1a576df5 {1}", s, Config.PcieAspm));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PCIEXPRESS ee12f906-d277-404b-b6da-e5fa1a576df5 {1}", s, Config.PcieAspm));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_DISK d639518a-e56d-4345-8af2-b9f32fb26109 {1}", s, Config.NvmeSleepTimeout));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_DISK d639518a-e56d-4345-8af2-b9f32fb26109 {1}", s, Config.NvmeSleepTimeout));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 {1}", s, Config.UsbSelectiveSuspendBattery ? "1" : "0"));
                    
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR cfeda3d0-7697-4566-a922-a9086cd49dfa {1}", s, Config.ProcAutoActivityWindow));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR cfeda3d0-7697-4566-a922-a9086cd49dfa {1}", s, Config.ProcAutoActivityWindow));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 943c8cb6-6f93-4227-ad87-e9a3feec08d1 {1}", s, Config.CoreParkingThreshold));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 943c8cb6-6f93-4227-ad87-e9a3feec08d1 {1}", s, Config.CoreParkingThreshold));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 06cadf0e-64ed-448a-8927-ce7bf90eb35d {1}", s, Config.ProcPerfIncreaseThreshold));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 06cadf0e-64ed-448a-8927-ce7bf90eb35d {1}", s, Config.ProcPerfIncreaseThreshold));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 12a0ab44-fe28-4fa9-b3bd-4b64f44960a6 {1}", s, Config.ProcPerfDecreaseThreshold));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 12a0ab44-fe28-4fa9-b3bd-4b64f44960a6 {1}", s, Config.ProcPerfDecreaseThreshold));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 71021b41-c749-4d21-be74-a00f335d582b {1}", s, Config.CoreParkingDecreasePolicy));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 71021b41-c749-4d21-be74-a00f335d582b {1}", s, Config.CoreParkingDecreasePolicy));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} 0012ee47-9041-4b5d-9b77-535fba8b1442 0b2d69d7-a2a1-449c-9680-f91c70521c60 {1}", s, Config.DiskAhciLinkPowerManagement));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} 0012ee47-9041-4b5d-9b77-535fba8b1442 0b2d69d7-a2a1-449c-9680-f91c70521c60 {1}", s, Config.DiskAhciLinkPowerManagement));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} 5fb4938d-1ee8-4b0f-9a3c-5036b0ab995c DD844638-FA00-4E05-9B94-71F4F382612B {1}", s, Config.GpuPreferencePolicy));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} 5fb4938d-1ee8-4b0f-9a3c-5036b0ab995c DD844638-FA00-4E05-9B94-71F4F382612B {1}", s, Config.GpuPreferencePolicy));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 {1}", s, Config.HibernateAfterSleep));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 {1}", s, Config.HibernateAfterSleep));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} SUB_PROCESSOR 8baa4a8a-14c6-4451-8e8b-14bdbd197537 {1}", s, Config.AutonomousHwp));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} SUB_PROCESSOR 8baa4a8a-14c6-4451-8e8b-14bdbd197537 {1}", s, Config.AutonomousHwp));
                    RunCmd(string.Format("powercfg /setacvalueindex {0} 44f971e7-7249-4100-976a-787097f3844f 36195052-d397-4222-b29e-1a81dc22464e {1}", s, Config.IntelGraphicsPowerPlan));
                    RunCmd(string.Format("powercfg /setdcvalueindex {0} 44f971e7-7249-4100-976a-787097f3844f 36195052-d397-4222-b29e-1a81dc22464e {1}", s, Config.IntelGraphicsPowerPlan));
                }
                RunCmd("powercfg /setactive SCHEME_CURRENT");

                // 5. USB Keep-Alive
                if (Config.PreventUsbWakeLag) {
                    string uScript = @"'ROOT_HUB30','VID_0BDA','VID_05E3','VID_25A7','VID_A8A5','VID_258A' | % { $i=$_; Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Enum\USB' -Recurse -ErrorAction SilentlyContinue | ? {$_.PSChildName -like '*'+$i+'*'} | % { Get-ChildItem $_.PSPath -ErrorAction SilentlyContinue | % { $dp=Join-Path $_.PSPath 'Device Parameters'; if(Test-Path $dp){ Set-ItemProperty $dp 'AllowIdleIrpInD3' 0 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty $dp 'D3ColdSupported' 0 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty $dp 'EnhancedPowerManagementEnabled' 0 -Type DWord -Force -ErrorAction SilentlyContinue; Set-ItemProperty $dp 'SelectiveSuspendEnabled' 0 -Type DWord -Force -ErrorAction SilentlyContinue } } } }";
                    ProcessStartInfo pu = new ProcessStartInfo("powershell", "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command \"" + uScript + "\"");
                    pu.CreateNoWindow = true; pu.UseShellExecute = false; Process.Start(pu);
                }

            } catch { }
        }

        private static void RunCmd(string cmd) {
            try {
                int firstSpace = cmd.IndexOf(' ');
                string exe = (firstSpace > 0) ? cmd.Substring(0, firstSpace) : cmd;
                string args = (firstSpace > 0) ? cmd.Substring(firstSpace + 1) : "";
                ProcessStartInfo psi = new ProcessStartInfo(exe, args) { CreateNoWindow = true, UseShellExecute = false };
                Process p = Process.Start(psi);
                if (p != null) p.WaitForExit(1000);
            } catch { }
        }
    }
}
