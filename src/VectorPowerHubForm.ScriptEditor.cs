using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        private Panel panelScriptEditor;
        private TextBox txtScriptEditor;
        private Label lblScriptTitle;
        private GlowButton btnSaveScript;
        private GlowButton btnRunScript;
        private GlowButton btnCloseScript;
        private GlowButton btnOpenScriptEditor;

        private void InitializeScriptEditor() {
            panelScriptEditor = new Panel();
            panelScriptEditor.BackColor = Color.FromArgb(10, 14, 20);
            panelScriptEditor.Size = new Size(1000, 640);
            panelScriptEditor.Location = new Point(80, 80);
            panelScriptEditor.Visible = false;
            panelScriptEditor.BorderStyle = BorderStyle.FixedSingle;

            lblScriptTitle = new Label();
            lblScriptTitle.Text = "OS Power Optimizations Script";
            lblScriptTitle.Font = new Font("Segoe UI", 16f, FontStyle.Bold);
            lblScriptTitle.ForeColor = ColorTextWhite;
            lblScriptTitle.Location = new Point(20, 20);
            lblScriptTitle.AutoSize = true;

            txtScriptEditor = new TextBox();
            txtScriptEditor.Multiline = true;
            txtScriptEditor.ScrollBars = ScrollBars.Vertical;
            txtScriptEditor.Font = new Font("Consolas", 10f);
            txtScriptEditor.BackColor = Color.FromArgb(16, 22, 30);
            txtScriptEditor.ForeColor = ColorTextWhite;
            txtScriptEditor.Location = new Point(20, 60);
            txtScriptEditor.Size = new Size(960, 500);
            txtScriptEditor.AcceptsReturn = true;
            txtScriptEditor.AcceptsTab = true;

            btnSaveScript = new GlowButton();
            btnSaveScript.Text = "Save Script";
            btnSaveScript.Location = new Point(20, 580);
            btnSaveScript.Size = new Size(140, 38);
            btnSaveScript.ButtonColor = Color.FromArgb(28, 48, 36);
            btnSaveScript.BorderColor = ColorAccentGreen;
            btnSaveScript.TextColor = ColorAccentGreen;
            btnSaveScript.Click += (s, e) => SaveOsScript();

            btnRunScript = new GlowButton();
            btnRunScript.Text = "Run Script Now";
            btnRunScript.Location = new Point(180, 580);
            btnRunScript.Size = new Size(160, 38);
            btnRunScript.ButtonColor = Color.FromArgb(28, 48, 36);
            btnRunScript.BorderColor = ColorAccentGreen;
            btnRunScript.TextColor = ColorAccentGreen;
            btnRunScript.Click += (s, e) => { SaveOsScript(); RunOsScript(); };

            btnCloseScript = new GlowButton();
            btnCloseScript.Text = "Close";
            btnCloseScript.Location = new Point(360, 580);
            btnCloseScript.Size = new Size(120, 38);
            btnCloseScript.ButtonColor = ColorCardBg;
            btnCloseScript.BorderColor = ColorBorder;
            btnCloseScript.TextColor = ColorTextMuted;
            btnCloseScript.Click += (s, e) => panelScriptEditor.Visible = false;

            panelScriptEditor.Controls.Add(lblScriptTitle);
            panelScriptEditor.Controls.Add(txtScriptEditor);
            panelScriptEditor.Controls.Add(btnSaveScript);
            panelScriptEditor.Controls.Add(btnRunScript);
            panelScriptEditor.Controls.Add(btnCloseScript);

            this.Controls.Add(panelScriptEditor);
        }

        private void LoadOsScript() {
            string path = @"C:\Users\a7god\Apply-PowerOptimizations.ps1";
            if (File.Exists(path)) {
                txtScriptEditor.Text = File.ReadAllText(path);
            }
        }

        private void SaveOsScript() {
            string path = @"C:\Users\a7god\Apply-PowerOptimizations.ps1";
            File.WriteAllText(path, txtScriptEditor.Text);
            ShowNotificationBalloon("Script Saved", "OS Power Optimizations script saved successfully.");
        }

        public void RunOsScript() {
            try {
                string path = @"C:\Users\a7god\Apply-PowerOptimizations.ps1";
                if (File.Exists(path)) {
                    ProcessStartInfo psi = new ProcessStartInfo("powershell.exe", "-ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + path + "\"");
                    psi.CreateNoWindow = true;
                    psi.UseShellExecute = false;
                    Process.Start(psi);
                    ShowNotificationBalloon("Script Executed", "OS Power Optimizations applied.");
                }
            } catch { }
        }

        private void ToggleScriptEditor() {
            if (!panelScriptEditor.Visible) {
                LoadOsScript();
                panelScriptEditor.Visible = true;
                panelScriptEditor.BringToFront();
            } else {
                panelScriptEditor.Visible = false;
            }
        }
    }
}
