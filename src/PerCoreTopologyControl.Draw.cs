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
    public partial class PerCoreTopologyControl : Control {
        private void DrawCoreCard(Graphics g, int x, int y, int w, int h, string name, string arch, double ghz, double util, bool isPCore) {
            Rectangle rect = new Rectangle(x, y, w - 1, h - 1);
            Color fill = Color.FromArgb(24, 27, 35);
            Color border = isPCore ? Color.FromArgb(45, 60, 80) : Color.FromArgb(45, 45, 55);
            Color accent = isPCore ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorAccentGold;

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 4)) {
                using (Brush b = new SolidBrush(fill)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            // Left stripe
            using (Brush b = new SolidBrush(accent)) {
                g.FillRectangle(b, x, y + 4, 3, h - 8);
            }

            // Header Name
            using (Font fName = new Font("Segoe UI", isPCore ? 8.25f : 8f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                    g.DrawString(name, fName, b, x + 7, y + 4);
                }
            }

            // Monospace Live GHz (Consolas Bold)
            using (Font fGhz = new Font("Consolas", isPCore ? 11f : 10f, FontStyle.Bold)) {
                Color ghzColor = isPCore
                    ? (ghz >= 4.8 ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorTextWhite)
                    : (ghz >= 3.6 ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorTextWhite);
                using (Brush b = new SolidBrush(ghzColor)) {
                    g.DrawString(string.Format("{0:0.00} GHz", ghz), fGhz, b, x + 7, y + 18);
                }
            }

            // Mini Load Bar
            int barY = y + h - 6;
            int barW = w - 14;
            int fillW = (int)((Math.Min(100.0, Math.Max(0.0, util)) / 100.0) * barW);

            using (Brush b = new SolidBrush(Color.FromArgb(16, 18, 24))) {
                g.FillRectangle(b, x + 7, barY, barW, 2);
            }
            if (fillW > 0) {
                using (Brush b = new SolidBrush(accent)) {
                    g.FillRectangle(b, x + 7, barY, fillW, 2);
                }
            }
        }

    }
}
