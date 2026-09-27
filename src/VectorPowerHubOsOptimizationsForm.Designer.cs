using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.NetworkInformation;
using System.Linq;

namespace VectorPowerHub {
    public partial class VectorPowerHubOsOptimizationsForm : Form {
        private ComboBox comboWifiMimo, comboAudioTimeout, comboAspm, comboNvmeSleep;
        private CheckBox chkWifiThroughput, chkWifiEee, chkWifiPacketCoal, chkPreventUsbLag, chkUsbSelSuspend, chkDisableBloatware;
        private NumericUpDown numProcAutoActivity, numCoreParking, numProcPerfInc, numProcPerfDec, numCoreParkingDec, numCoreParkingIncTime;
        private NumericUpDown numDiskAhci, numGpuPref, numHibernate, numAutoHwp, numIntelGraphics;
        private GlowButton btnApplyOptimizations;
        private ToolTip tooltip;
        private TableLayoutPanel mainLayout;

        private void InitializeComponent() {
            this.tooltip = new ToolTip { AutoPopDelay = 15000, InitialDelay = 400, ReshowDelay = 200 };
            this.Text = "OS Power Optimizations";
            this.Size = new Size(1100, 850);
            this.MinimumSize = new Size(800, 600);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.AutoScaleMode = AutoScaleMode.None;
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            mainLayout = new TableLayoutPanel {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(20),
                AutoScroll = true
            };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            TableLayoutPanel col1 = CreateColumn();
            TableLayoutPanel col2 = CreateColumn();
            mainLayout.Controls.Add(col1, 0, 0);
            mainLayout.Controls.Add(col2, 1, 0);

            // Column 1
            AddCategory(col1, "Wi-Fi 7 / Networking");
            comboWifiMimo = AddDropdown(col1, "MIMO Power Save", new[] { "Auto SMPS", "Static SMPS", "No SMPS" });
            chkWifiThroughput = AddToggle(col1, "Throughput Booster");
            chkWifiEee = AddToggle(col1, "Energy Efficient Ethernet (EEE)");
            chkWifiPacketCoal = AddToggle(col1, "Packet Coalescing");

            AddCategory(col1, "USB & Input Latency");
            chkPreventUsbLag = AddToggle(col1, "Prevent Keyboard Wake Lag (Force D2 State)");
            chkUsbSelSuspend = AddToggle(col1, "Enable USB Selective Suspend (Battery Mode)");

            AddCategory(col1, "Audio Devices");
            comboAudioTimeout = AddDropdown(col1, "Deep Sleep Timeout", new[] { "Disabled", "5 Seconds", "30 Seconds", "1 Minute" });

            // Column 2
            AddCategory(col2, "PCIe & Storage");
            comboAspm = AddDropdown(col2, "PCIe ASPM Level", new[] { "Off", "Moderate", "Maximum Power Savings" });
            comboNvmeSleep = AddDropdown(col2, "NVMe Deep Sleep Timeout", new[] { "10ms", "50ms", "100ms", "1000ms" });
            numDiskAhci = AddNumeric(col2, "AHCI Link Power Mgmt (0-4, 4=Max)", 4, 0, 4, "0 = Active (No sleep). 4 = Lowest power state (HIPM+DIPM). Microsoft recommends 4.");

            AddCategory(col2, "Advanced CPU Tuning");
            numProcAutoActivity = AddNumeric(col2, "Auto Activity Window (Microseconds)", 2000000, 0, 10000000, "Time the processor waits before changing performance states. 2000000 = 2 seconds.");
            numCoreParking = AddNumeric(col2, "Core Parking Threshold (%)", 85, 0, 100, "Percentage of utilization an active core must reach before a parked core is woken up. 85 = 85%.");
            numProcPerfInc = AddNumeric(col2, "Performance Increase Threshold (%)", 50, 0, 100, "Percentage of utilization required before the processor jumps to a higher clock speed.");
            numProcPerfDec = AddNumeric(col2, "Performance Decrease Threshold (%)", 5, 0, 100, "Percentage of utilization required before the processor drops to a lower clock speed. 5 = 5%.");
            numCoreParkingIncTime = AddNumeric(col2, "Core Parking Increase Time", 4, 1, 4, "Governs how fast background tasks can wake cores. Range 1 to 4. Default 4 blocks instant background wakeups.");
            numCoreParkingDec = AddNumeric(col2, "Core Parking Decrease Policy", 0, 0, 3, "0 = Ideal cores, 1 = Single core, 2 = All possible cores, 3 = One eighth cores.");
            numAutoHwp = AddNumeric(col2, "Intel Thread Director (HWP)", 1, 0, 1, "Turn ON for max battery (Spreads load, limits ~4.5GHz). Turn OFF for max single-core gaming (Unlocks 5.6GHz, consumes 2x power).");

            AddCategory(col2, "System Policies");
            numGpuPref = AddNumeric(col2, "GPU Preference (0=Perf, 1=Power)", 1, 0, 1, "0 = Force dGPU. 1 = Low Power (iGPU preferred for non-games).");
            numIntelGraphics = AddNumeric(col2, "Intel Graphics Plan (0=Max Bat)", 0, 0, 3, "0 = Maximum Battery Life. 1 = Balanced. 2 = Maximum Performance.");
            numHibernate = AddNumeric(col2, "Hibernate After Sleep (0=Never)", 0, 0, 1, "0 = Disable hybrid sleep/hibernate transition, stays in fast S3/S0ix.");
            chkDisableBloatware = AddToggle(col2, "Block MSI/Nahimic/Xbox Bloatware");

            // Add a final spring row to push everything to the top
            col1.RowCount++;
            col1.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            col2.RowCount++;
            col2.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            btnApplyOptimizations = new GlowButton {
                Text = "Save & Apply Settings",
                Size = new Size(240, 44),
                ButtonColor = VectorPowerHubForm.ColorCardHover,
                BorderColor = VectorPowerHubForm.ColorAccentCyan,
                TextColor = VectorPowerHubForm.ColorAccentCyan,
                Dock = DockStyle.Bottom
            };
            btnApplyOptimizations.Click += (s, e) => SaveAndApplyOptimizations();
            
            Panel footer = new Panel { Dock = DockStyle.Bottom, Height = 64, Padding = new Padding(20, 10, 20, 10) };
            footer.Controls.Add(btnApplyOptimizations);

            this.Controls.Add(mainLayout);
            this.Controls.Add(footer);
        }

        private TableLayoutPanel CreateColumn() {
            var col = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            col.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            return col;
        }

        private void AddCategory(TableLayoutPanel panel, string text) {
            Label lbl = new Label { Text = text.ToUpper(), Font = new Font("Segoe UI", 11f, FontStyle.Bold), ForeColor = VectorPowerHubForm.ColorAccentCyan, AutoSize = true, Margin = new Padding(0, 25, 0, 10) };
            panel.Controls.Add(lbl, 0, panel.RowCount);
            panel.SetColumnSpan(lbl, 2);
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowCount++;
        }

        private CheckBox AddToggle(TableLayoutPanel panel, string text) {
            CheckBox chk = new CheckBox { Text = text, Font = new Font("Segoe UI", 9.5f), ForeColor = Color.White, AutoSize = true, Margin = new Padding(5, 5, 0, 5) };
            panel.Controls.Add(chk, 0, panel.RowCount);
            panel.SetColumnSpan(chk, 2);
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowCount++;
            return chk;
        }

        private ComboBox AddDropdown(TableLayoutPanel panel, string label, string[] options) {
            Label lbl = new Label { Text = label + ":", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.LightGray, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(5, 5, 5, 5) };
            ComboBox cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9f), BackColor = VectorPowerHubForm.ColorCardBg, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Dock = DockStyle.Fill, Margin = new Padding(5, 5, 15, 5) };
            cb.Items.AddRange(options);
            panel.Controls.Add(lbl, 0, panel.RowCount);
            panel.Controls.Add(cb, 1, panel.RowCount);
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowCount++;
            return cb;
        }

        private NumericUpDown AddNumeric(TableLayoutPanel panel, string label, int val, int min, int max, string tip) {
            Label lbl = new Label { Text = label + ":", Font = new Font("Segoe UI", 9.5f), ForeColor = Color.LightGray, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(5, 5, 5, 5) };
            NumericUpDown num = new NumericUpDown { Minimum = min, Maximum = max, Value = (val >= min && val <= max) ? val : min, Font = new Font("Segoe UI", 9f), BackColor = VectorPowerHubForm.ColorCardBg, ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Fill, Margin = new Padding(5, 5, 15, 5) };
            tooltip.SetToolTip(lbl, tip);
            tooltip.SetToolTip(num, tip);
            panel.Controls.Add(lbl, 0, panel.RowCount);
            panel.Controls.Add(num, 1, panel.RowCount);
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); panel.RowCount++;
            return num;
        }
    }
}
