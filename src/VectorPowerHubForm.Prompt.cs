using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        public static int ShowPowerCeilingPrompt(int defaultVal) {
            int result = defaultVal > 0 ? defaultVal : 215;
            using (Form dialog = new Form()) {
                dialog.Text = "Platform Power Ceiling Setup";
                dialog.ClientSize = new Size(460, 245);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = true;
                dialog.TopMost = true;
                dialog.BackColor = ColorBgMain;
                dialog.ForeColor = ColorTextWhite;

                Label lblHeader = new Label();
                lblHeader.Text = "PLATFORM POWER CEILING CONFIGURATION";
                lblHeader.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                lblHeader.ForeColor = ColorAccentPurple;
                lblHeader.Location = new Point(20, 14);
                lblHeader.AutoSize = true;

                Label lblDesc = new Label();
                lblDesc.Text = "Specify maximum combined CPU + GPU platform power ceiling in Watts (e.g., 215W for MSI Vector 16 HX i9-14900HX + RTX 4080):";
                lblDesc.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                lblDesc.ForeColor = ColorTextMuted;
                lblDesc.Location = new Point(20, 38);
                lblDesc.Size = new Size(420, 34);

                NumericUpDown numCeiling = new NumericUpDown();
                numCeiling.Location = new Point(24, 78);
                numCeiling.Size = new Size(110, 26);
                numCeiling.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
                numCeiling.BackColor = ColorCardBg;
                numCeiling.ForeColor = ColorAccentCyan;
                numCeiling.BorderStyle = BorderStyle.FixedSingle;
                numCeiling.Minimum = 30;
                numCeiling.Maximum = 600;
                numCeiling.DecimalPlaces = 0;
                numCeiling.Value = (defaultVal >= 30 && defaultVal <= 600) ? defaultVal : 215;

                Label lblUnit = new Label();
                lblUnit.Text = "Watts (W)";
                lblUnit.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
                lblUnit.ForeColor = ColorTextWhite;
                lblUnit.Location = new Point(140, 81);
                lblUnit.AutoSize = true;

                CheckBox chkAutoDetect = new CheckBox();
                chkAutoDetect.Text = "Auto-Detect: Dynamically raise ceiling on sustained load";
                chkAutoDetect.Font = new Font("Segoe UI", 8.5f, FontStyle.Regular);
                chkAutoDetect.ForeColor = ColorTextWhite;
                chkAutoDetect.BackColor = ColorBgMain;
                chkAutoDetect.Location = new Point(24, 114);
                chkAutoDetect.Size = new Size(415, 22);
                chkAutoDetect.FlatStyle = FlatStyle.Flat;
                chkAutoDetect.Cursor = Cursors.Hand;
                chkAutoDetect.Checked = (PowerCoreEngine.Instance != null && PowerCoreEngine.Instance.AutoPowerCeiling);
                chkAutoDetect.CheckedChanged += (s, e) => {
                    if (PowerCoreEngine.Instance != null) PowerCoreEngine.Instance.AutoPowerCeiling = chkAutoDetect.Checked;
                };

                Button btnOk = new Button();
                btnOk.Text = "Save Ceiling";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
                btnOk.Location = new Point(245, 195);
                btnOk.Size = new Size(100, 32);
                btnOk.FlatStyle = FlatStyle.Flat;
                btnOk.FlatAppearance.BorderColor = ColorAccentCyan;
                btnOk.BackColor = Color.FromArgb(31, 35, 45);
                btnOk.ForeColor = ColorAccentCyan;
                btnOk.Cursor = Cursors.Hand;

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Font = new Font("Segoe UI", 9f, FontStyle.Regular);
                btnCancel.Location = new Point(355, 195);
                btnCancel.Size = new Size(85, 32);
                btnCancel.FlatStyle = FlatStyle.Flat;
                btnCancel.FlatAppearance.BorderColor = ColorBorder;
                btnCancel.BackColor = Color.FromArgb(22, 24, 32);
                btnCancel.ForeColor = ColorTextMuted;
                btnCancel.Cursor = Cursors.Hand;

                Button btnCalibrate = new Button();
                btnCalibrate.Text = "⚡ Auto-Calibrate Now (10s)";
                btnCalibrate.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                btnCalibrate.Location = new Point(24, 146);
                btnCalibrate.Size = new Size(185, 30);
                btnCalibrate.FlatStyle = FlatStyle.Flat;
                btnCalibrate.FlatAppearance.BorderColor = ColorAccentPurple;
                btnCalibrate.BackColor = Color.FromArgb(32, 28, 42);
                btnCalibrate.ForeColor = ColorAccentPurple;
                btnCalibrate.Cursor = Cursors.Hand;

                Label lblStatus = new Label();
                lblStatus.Text = "";
                lblStatus.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
                lblStatus.ForeColor = ColorAccentCyan;
                lblStatus.Location = new Point(216, 152);
                lblStatus.Size = new Size(224, 20);

                btnCalibrate.Click += (s, e) => {
                    RunPowerCeilingCalibration(numCeiling, btnCalibrate, lblStatus, btnOk, btnCancel, chkAutoDetect, dialog);
                };

                dialog.Controls.Add(lblHeader);
                dialog.Controls.Add(lblDesc);
                dialog.Controls.Add(numCeiling);
                dialog.Controls.Add(lblUnit);
                dialog.Controls.Add(chkAutoDetect);
                dialog.Controls.Add(btnCalibrate);
                dialog.Controls.Add(lblStatus);
                dialog.Controls.Add(btnOk);
                dialog.Controls.Add(btnCancel);

                dialog.AcceptButton = btnOk;
                dialog.CancelButton = btnCancel;

                dialog.Load += (s, e) => {
                    numCeiling.Focus();
                    numCeiling.Select(0, numCeiling.Text.Length);
                };

                if (dialog.ShowDialog() == DialogResult.OK) {
                    result = (int)numCeiling.Value;
                    if (PowerCoreEngine.Instance != null) {
                        PowerCoreEngine.Instance.AutoPowerCeiling = chkAutoDetect.Checked;
                    }
                }
            }
            return result;
        }
    }
}
