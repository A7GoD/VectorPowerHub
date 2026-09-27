using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Linq;

namespace VectorPowerHub {
    public partial class VectorPowerHubOsOptimizationsForm : Form {
        public VectorPowerHubOsOptimizationsForm() {
            InitializeComponent();
            LoadOptimizationsUI();
            
            // UI Constraint Logic (Performance Increase must be < Core Parking Threshold)
            numCoreParking.ValueChanged += (s, e) => {
                if (numProcPerfInc.Value >= numCoreParking.Value) {
                    numProcPerfInc.Value = Math.Max(0, numCoreParking.Value - 1);
                }
            };
            numProcPerfInc.ValueChanged += (s, e) => {
                if (numProcPerfInc.Value >= numCoreParking.Value) {
                    numProcPerfInc.Value = Math.Max(0, numCoreParking.Value - 1);
                }
            };
        }

        private void LoadOptimizationsUI() {
            OsOptimizationsManager.Load();
            var c = OsOptimizationsManager.Config;
            
            comboWifiMimo.SelectedIndex = c.WifiMimoMode;
            chkWifiThroughput.Checked = c.WifiThroughputBooster;
            chkWifiEee.Checked = c.WifiEee;
            chkWifiPacketCoal.Checked = c.WifiPacketCoalescing;
            
            chkPreventUsbLag.Checked = c.PreventUsbWakeLag;
            chkUsbSelSuspend.Checked = c.UsbSelectiveSuspendBattery;
            
            if (c.AudioIdleTimeout == 0) comboAudioTimeout.SelectedIndex = 0;
            else if (c.AudioIdleTimeout == 5) comboAudioTimeout.SelectedIndex = 1;
            else if (c.AudioIdleTimeout == 30) comboAudioTimeout.SelectedIndex = 2;
            else if (c.AudioIdleTimeout == 60) comboAudioTimeout.SelectedIndex = 3;
            else comboAudioTimeout.SelectedIndex = 1;
            
            comboAspm.SelectedIndex = c.PcieAspm;
            
            if (c.NvmeSleepTimeout == 10) comboNvmeSleep.SelectedIndex = 0;
            else if (c.NvmeSleepTimeout == 50) comboNvmeSleep.SelectedIndex = 1;
            else if (c.NvmeSleepTimeout == 100) comboNvmeSleep.SelectedIndex = 2;
            else if (c.NvmeSleepTimeout == 1000) comboNvmeSleep.SelectedIndex = 3;
            else comboNvmeSleep.SelectedIndex = 1;

            numDiskAhci.Value = (decimal)c.DiskAhciLinkPowerManagement;
            numProcAutoActivity.Value = (decimal)c.ProcAutoActivityWindow;
            numCoreParking.Value = (decimal)c.CoreParkingThreshold;
            numProcPerfInc.Value = (decimal)c.ProcPerfIncreaseThreshold;
            numProcPerfDec.Value = (decimal)c.ProcPerfDecreaseThreshold;
            numCoreParkingIncTime.Value = (decimal)c.CoreParkingIncreaseTime;
            numCoreParkingDec.Value = (decimal)c.CoreParkingDecreasePolicy;
            numAutoHwp.Value = (decimal)c.AutonomousHwp;
            numGpuPref.Value = (decimal)c.GpuPreferencePolicy;
            numIntelGraphics.Value = (decimal)c.IntelGraphicsPowerPlan;
            numHibernate.Value = (decimal)c.HibernateAfterSleep;

            chkDisableBloatware.Checked = c.DisableBloatware;
        }

        private void SaveAndApplyOptimizations() {
            var c = OsOptimizationsManager.Config;
            c.WifiMimoMode = comboWifiMimo.SelectedIndex;
            c.WifiThroughputBooster = chkWifiThroughput.Checked;
            c.WifiEee = chkWifiEee.Checked;
            c.WifiPacketCoalescing = chkWifiPacketCoal.Checked;
            
            c.PreventUsbWakeLag = chkPreventUsbLag.Checked;
            c.UsbSelectiveSuspendBattery = chkUsbSelSuspend.Checked;
            
            if (comboAudioTimeout.SelectedIndex == 0) c.AudioIdleTimeout = 0;
            else if (comboAudioTimeout.SelectedIndex == 1) c.AudioIdleTimeout = 5;
            else if (comboAudioTimeout.SelectedIndex == 2) c.AudioIdleTimeout = 30;
            else if (comboAudioTimeout.SelectedIndex == 3) c.AudioIdleTimeout = 60;
            
            c.PcieAspm = comboAspm.SelectedIndex;
            
            if (comboNvmeSleep.SelectedIndex == 0) c.NvmeSleepTimeout = 10;
            else if (comboNvmeSleep.SelectedIndex == 1) c.NvmeSleepTimeout = 50;
            else if (comboNvmeSleep.SelectedIndex == 2) c.NvmeSleepTimeout = 100;
            else if (comboNvmeSleep.SelectedIndex == 3) c.NvmeSleepTimeout = 1000;

            c.DiskAhciLinkPowerManagement = (int)numDiskAhci.Value;
            c.ProcAutoActivityWindow = (int)numProcAutoActivity.Value;
            c.CoreParkingThreshold = (int)numCoreParking.Value;
            c.ProcPerfIncreaseThreshold = (int)numProcPerfInc.Value;
            c.ProcPerfDecreaseThreshold = (int)numProcPerfDec.Value;
            c.CoreParkingIncreaseTime = (int)numCoreParkingIncTime.Value;
            c.CoreParkingDecreasePolicy = (int)numCoreParkingDec.Value;
            c.AutonomousHwp = (int)numAutoHwp.Value;
            c.GpuPreferencePolicy = (int)numGpuPref.Value;
            c.IntelGraphicsPowerPlan = (int)numIntelGraphics.Value;
            c.HibernateAfterSleep = (int)numHibernate.Value;

            c.DisableBloatware = chkDisableBloatware.Checked;

            OsOptimizationsManager.Save();
            OsOptimizationsManager.ApplyAll();
            
            MessageBox.Show("OS Power settings successfully saved and injected into Windows.", "Optimizations Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
