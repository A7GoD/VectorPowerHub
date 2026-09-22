using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Linq;

namespace VectorPowerHub {
    public class VectorPowerHubOsOptimizationsForm : Form {
        private ComboBox comboWifiMimo;
        private CheckBox chkWifiThroughput;
        private CheckBox chkWifiEee;
        private CheckBox chkWifiPacketCoal;
        private CheckBox chkPreventUsbLag;
        private CheckBox chkUsbSelSuspend;
        private ComboBox comboAudioTimeout;
        private ComboBox comboAspm;
        private ComboBox comboNvmeSleep;
        private CheckBox chkDisableBloatware;

        // Properties for processor & deep tuning
        private NumericUpDown numProcAutoActivity;
        private NumericUpDown numCoreParking;
        private NumericUpDown numProcPerfInc;
        private NumericUpDown numProcPerfDec;
        private NumericUpDown numCoreParkingDec;
        private NumericUpDown numDiskAhci;
        private NumericUpDown numGpuPref;
        private NumericUpDown numHibernate;
        private NumericUpDown numAutoHwp;
        private NumericUpDown numIntelGraphics;

        private GlowButton btnApplyOptimizations;
        private ToolTip tooltip;

        public VectorPowerHubOsOptimizationsForm() {
            tooltip = new ToolTip();
            tooltip.AutoPopDelay = 15000;
            tooltip.InitialDelay = 400;
            tooltip.ReshowDelay = 200;
            this.Text = "OS Power Optimizations";
            this.Size = new Size(1040, 780);
            this.BackColor = Color.FromArgb(10, 14, 20);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Label lblTitle = new Label();
            lblTitle.Text = "OS POWER OPTIMIZATIONS";
            lblTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.Location = new Point(20, 20);
            lblTitle.AutoSize = true;

            int y = 70;
            // Wi-Fi 7 Settings
            bool hasWifi = false;
            try { hasWifi = NetworkInterface.GetAllNetworkInterfaces().Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211); } catch { }
            
            CreateCategoryLabel(hasWifi ? "Wi-Fi 7 / Networking" : "Wi-Fi 7 / Networking (Not Detected)", y); y += 35;
            comboWifiMimo = CreateDropdown("MIMO Power Save", new string[] { "Auto SMPS", "Static SMPS", "No SMPS" }, y, out y);
            chkWifiThroughput = CreateToggle("Throughput Booster", y); y += 30;
            chkWifiEee = CreateToggle("Energy Efficient Ethernet (EEE)", y); y += 30;
            chkWifiPacketCoal = CreateToggle("Packet Coalescing", y); y += 30;

            if (!hasWifi) {
                comboWifiMimo.Enabled = false;
                chkWifiThroughput.Enabled = false;
                chkWifiEee.Enabled = false;
                chkWifiPacketCoal.Enabled = false;
            }

            y += 20;
            // USB & Input
            CreateCategoryLabel("USB & Input Latency", y); y += 35;
            chkPreventUsbLag = CreateToggle("Prevent Keyboard Wake Lag (Force D2 State)", y); y += 30;
            chkUsbSelSuspend = CreateToggle("Enable USB Selective Suspend (Battery Mode)", y); y += 30;

            y += 20;
            // Audio
            CreateCategoryLabel("Audio Devices", y); y += 35;
            comboAudioTimeout = CreateDropdown("Deep Sleep Timeout", new string[] { "Disabled", "5 Seconds", "30 Seconds", "1 Minute" }, y, out y);

            // Column 2
            int x2 = 500;
            int y2 = 70;
            
            // Storage & PCIe
            CreateCategoryLabel("PCIe & Storage", y2, x2); y2 += 35;
            comboAspm = CreateDropdown("PCIe ASPM Level", new string[] { "Off", "Moderate", "Maximum Power Savings" }, y2, out y2, x2);
            comboNvmeSleep = CreateDropdown("NVMe Deep Sleep Timeout", new string[] { "10ms", "50ms", "100ms", "1000ms" }, y2, out y2, x2);
            numDiskAhci = CreateNumericInput("AHCI Link Power Mgmt (0-4, 4=Max)", 4, 0, 4, "0 = Active (No sleep). 4 = Lowest power state (HIPM+DIPM). Microsoft recommends 4.", y2, out y2, x2);

            y2 += 10;
            // Processor Tuning
            CreateCategoryLabel("Advanced CPU Tuning", y2, x2); y2 += 35;
            numProcAutoActivity = CreateNumericInput("Auto Activity Window (Microseconds)", 2000000, 0, 10000000, "Time the processor waits before changing performance states. 2000000 = 2 seconds. Ignores micro-spikes.", y2, out y2, x2);
            numCoreParking = CreateNumericInput("Core Parking Threshold (%)", 85, 0, 100, "Percentage of utilization an active core must reach before a parked core is woken up. 85 = 85%.", y2, out y2, x2);
            numProcPerfInc = CreateNumericInput("Performance Increase Threshold (%)", 90, 0, 100, "Percentage of utilization required before the processor jumps to a higher clock speed. 90 = 90%.", y2, out y2, x2);
            numProcPerfDec = CreateNumericInput("Performance Decrease Threshold (%)", 5, 0, 100, "Percentage of utilization required before the processor drops to a lower clock speed. 5 = 5%.", y2, out y2, x2);
            numCoreParkingDec = CreateNumericInput("Core Parking Decrease Policy", 0, 0, 3, "0 = Ideal cores, 1 = Single core, 2 = All possible cores, 3 = One eighth cores.", y2, out y2, x2);
            numAutoHwp = CreateNumericInput("Autonomous HWP (0=Off, 1=On)", 1, 0, 1, "Hardware P-States. 1 = Lets the CPU handle clock speeds autonomously (Race to Halt).", y2, out y2, x2);
            
            y2 += 10;
            // System Policies
            CreateCategoryLabel("System Policies", y2, x2); y2 += 35;
            numGpuPref = CreateNumericInput("GPU Preference (0=Perf, 1=Power)", 1, 0, 1, "0 = Force dGPU. 1 = Low Power (iGPU preferred for non-games).", y2, out y2, x2);
            numIntelGraphics = CreateNumericInput("Intel Graphics Plan (0=Max Bat)", 0, 0, 3, "0 = Maximum Battery Life. 1 = Balanced. 2 = Maximum Performance.", y2, out y2, x2);
            numHibernate = CreateNumericInput("Hibernate After Sleep (0=Never)", 0, 0, 1, "0 = Disable hybrid sleep/hibernate transition, stays in fast S3/S0ix.", y2, out y2, x2);
            chkDisableBloatware = CreateToggle("Block MSI/Nahimic/Xbox Bloatware", y2, x2); y2 += 30;

            btnApplyOptimizations = new GlowButton();
            btnApplyOptimizations.Text = "Save & Apply Settings";
            btnApplyOptimizations.Location = new Point(500, 680);
            btnApplyOptimizations.Size = new Size(200, 40);
            btnApplyOptimizations.ButtonColor = Color.FromArgb(28, 48, 36);
            btnApplyOptimizations.BorderColor = Color.LimeGreen;
            btnApplyOptimizations.TextColor = Color.LimeGreen;
            btnApplyOptimizations.Click += (s, e) => SaveAndApplyOptimizations();

            this.Controls.Add(lblTitle);
            this.Controls.Add(btnApplyOptimizations);

            // Load Values
            LoadOptimizationsUI();
        }

        private void CreateCategoryLabel(string text, int y, int x = 20) {
            Label lbl = new Label();
            lbl.Text = text.ToUpper();
            lbl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            lbl.ForeColor = Color.Cyan;
            lbl.Location = new Point(x, y);
            lbl.AutoSize = true;
            this.Controls.Add(lbl);
        }

        private CheckBox CreateToggle(string text, int y, int x = 30) {
            CheckBox chk = new CheckBox();
            chk.Text = text;
            chk.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            chk.ForeColor = Color.White;
            chk.Location = new Point(x, y);
            chk.AutoSize = true;
            this.Controls.Add(chk);
            return chk;
        }

        private ComboBox CreateDropdown(string label, string[] options, int y, out int nextY, int x = 30) {
            FlowLayoutPanel flp = new FlowLayoutPanel();
            flp.Location = new Point(x, y);
            flp.AutoSize = true;
            flp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flp.WrapContents = false;

            Label lbl = new Label();
            lbl.Text = label + ":";
            lbl.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lbl.ForeColor = Color.White;
            lbl.AutoSize = false;
            lbl.Size = new Size(240, 23);
            lbl.Anchor = AnchorStyles.Left;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Margin = new Padding(0, 4, 10, 0);

            ComboBox cb = new ComboBox();
            cb.Items.AddRange(options);
            cb.DropDownStyle = ComboBoxStyle.DropDownList;
            cb.Font = new Font("Segoe UI", 9f);
            cb.BackColor = Color.FromArgb(22, 28, 36);
            cb.ForeColor = Color.White;
            cb.FlatStyle = FlatStyle.Flat;
            cb.Size = new Size(160, 23);
            cb.Anchor = AnchorStyles.Left;

            flp.Controls.Add(lbl);
            flp.Controls.Add(cb);
            this.Controls.Add(flp);
            
            nextY = y + 35;
            return cb;
        }

        private NumericUpDown CreateNumericInput(string label, int value, int min, int max, string tip, int y, out int nextY, int x = 30) {
            FlowLayoutPanel flp = new FlowLayoutPanel();
            flp.Location = new Point(x, y);
            flp.AutoSize = true;
            flp.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flp.WrapContents = false;

            Label lbl = new Label();
            lbl.Text = label + ":";
            lbl.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            lbl.ForeColor = Color.White;
            lbl.AutoSize = false;
            lbl.Size = new Size(240, 23);
            lbl.Anchor = AnchorStyles.Left;
            lbl.TextAlign = ContentAlignment.MiddleLeft;
            lbl.Margin = new Padding(0, 4, 10, 0);

            NumericUpDown num = new NumericUpDown();
            num.Minimum = min;
            num.Maximum = max;
            num.Value = (value >= min && value <= max) ? value : min;
            num.Font = new Font("Segoe UI", 9f);
            num.BackColor = Color.FromArgb(22, 28, 36);
            num.ForeColor = Color.White;
            num.BorderStyle = BorderStyle.FixedSingle;
            num.Size = new Size(160, 23);
            num.Anchor = AnchorStyles.Left;

            flp.Controls.Add(lbl);
            flp.Controls.Add(num);
            this.Controls.Add(flp);

            tooltip.SetToolTip(lbl, tip);
            tooltip.SetToolTip(num, tip);
            
            nextY = y + 35;
            return num;
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
            c.CoreParkingDecreasePolicy = (int)numCoreParkingDec.Value;
            c.AutonomousHwp = (int)numAutoHwp.Value;
            c.GpuPreferencePolicy = (int)numGpuPref.Value;
            c.IntelGraphicsPowerPlan = (int)numIntelGraphics.Value;
            c.HibernateAfterSleep = (int)numHibernate.Value;

            c.DisableBloatware = chkDisableBloatware.Checked;

            OsOptimizationsManager.Save();
            OsOptimizationsManager.ApplyAll();
            
            MessageBox.Show("OS Power settings successfully saved and injected into Windows.", "Optimizations Applied", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.Close();
        }
    }
}
