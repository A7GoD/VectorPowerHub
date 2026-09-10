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
    public partial class VectorPowerHubForm : Form {
        private void InitializeCustomTunerBottomControls() {
            // Row 4: Action Buttons (Gap of 25px! Y = 350, H = 38)
            btnApplyCustom = new GlowButton();
            btnApplyCustom.Text = "✔ Apply Custom Profile";
            btnApplyCustom.Location = new Point(28, 350);
            btnApplyCustom.Size = new Size(220, 38);
            btnApplyCustom.ButtonColor = Color.FromArgb(28, 48, 36);
            btnApplyCustom.BorderColor = ColorAccentGreen;
            btnApplyCustom.TextColor = ColorAccentGreen;
            btnApplyCustom.Click += (s, e) => ApplyCustomTunerValues();

            btnCloseTuner = new GlowButton();
            btnCloseTuner.Text = "✕ Close";
            btnCloseTuner.Location = new Point(260, 350);
            btnCloseTuner.Size = new Size(110, 38);
            btnCloseTuner.ButtonColor = ColorCardBg;
            btnCloseTuner.BorderColor = ColorBorder;
            btnCloseTuner.TextColor = ColorTextMuted;
            btnCloseTuner.Click += (s, e) => ToggleCustomTuner();

            // Row 5: Startup & Hint (Gap of 16px!)
            chkRunAtStartup = new CheckBox();
            chkRunAtStartup.Text = "⚡ Start with Windows (Run at Startup Minimized to Tray)";
            chkRunAtStartup.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            chkRunAtStartup.ForeColor = ColorAccentCyan;
            chkRunAtStartup.BackColor = Color.Transparent;
            chkRunAtStartup.Cursor = Cursors.Hand;
            chkRunAtStartup.Location = new Point(28, 404);
            chkRunAtStartup.Size = new Size(600, 28);
            chkRunAtStartup.Checked = IsRunAtStartupEnabled();
            chkRunAtStartup.Click += (s, e) => ToggleRunAtStartup();

            // Row 6: Hint Label (Y = 442, H = 22)
            lblCustomTunerHint = new Label();
            lblCustomTunerHint.Text = "Custom profile applies real-time clock and power limits for active tuning.";
            lblCustomTunerHint.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
            lblCustomTunerHint.ForeColor = ColorTextMuted;
            lblCustomTunerHint.Location = new Point(28, 442);
            lblCustomTunerHint.Size = new Size(860, 22);
            lblCustomTunerHint.AutoSize = false;
            lblCustomTunerHint.AutoEllipsis = true;

            panelCustomTuner.Controls.Add(lblTunerTitle);
            panelCustomTuner.Controls.Add(lblPcoreVal);
            panelCustomTuner.Controls.Add(trackPcore);
            panelCustomTuner.Controls.Add(lblEcoreVal);
            panelCustomTuner.Controls.Add(trackEcore);
            panelCustomTuner.Controls.Add(lblEppVal);
            panelCustomTuner.Controls.Add(trackEpp);
            panelCustomTuner.Controls.Add(lblBoostModeVal);
            panelCustomTuner.Controls.Add(comboBoostMode);
            panelCustomTuner.Controls.Add(lblGpuClockLimitVal);
            panelCustomTuner.Controls.Add(trackGpuClock);
            panelCustomTuner.Controls.Add(btnApplyCustom);
            panelCustomTuner.Controls.Add(btnCloseTuner);
            panelCustomTuner.Controls.Add(chkRunAtStartup);
            panelCustomTuner.Controls.Add(lblCustomTunerHint);

            this.Controls.Add(panelCustomTuner);
            panelCustomTuner.BringToFront();
        }
    }
}
