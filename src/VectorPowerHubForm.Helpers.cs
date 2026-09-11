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

        private static void RenderControlHierarchy(Control parent, Graphics g, Point origin) {
            foreach (Control c in parent.Controls) {
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                Point p = new Point(origin.X + c.Left, origin.Y + c.Top);
                using (Bitmap childBmp = new Bitmap(c.Width, c.Height)) {
                    c.DrawToBitmap(childBmp, new Rectangle(0, 0, c.Width, c.Height));
                    g.DrawImage(childBmp, p);
                }
                if (c.Controls.Count > 0) {
                    RenderControlHierarchy(c, g, p);
                }
            }
        }

    }
}
