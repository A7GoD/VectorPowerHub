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
        // -----------------------------------------------------------------------------------------
        // HELPER UI FACTORY METHODS
        // -----------------------------------------------------------------------------------------
        private Button CreateTitleButton(string text, int x, int y, EventHandler onClick) {
            Button btn = new Button();
            btn.Text = text;
            btn.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btn.ForeColor = ColorTextMuted;
            btn.BackColor = Color.Transparent;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = Color.FromArgb(40, 45, 60);
            btn.Location = new Point(x, y);
            btn.Size = new Size(32, 28);
            btn.Click += onClick;
            return btn;
        }

        private static void RunCmd(string cmd) {
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


    }
}
