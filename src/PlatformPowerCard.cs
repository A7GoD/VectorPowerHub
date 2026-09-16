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
    public class PlatformPowerCard : Control {
        private double cpuW = 0.0;
        private double gpuW = 0.0;
        private double totalW = 0.0;

        public PlatformPowerCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double cW, double gW, double tW) {
            this.cpuW = cW;
            this.gpuW = gW;
            this.totalW = tW;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            // Clean background fill
            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            // Card background & border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            int ceilingW = 215;
            try {
                if (PowerCoreEngine.Instance != null) ceilingW = PowerCoreEngine.Instance.PlatformPowerCeilingW;
            } catch { }
            if (ceilingW <= 0) ceilingW = 215;

            // Purple Accent Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentPurple)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title Left
            using (Font fTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                g.DrawString(string.Format("TOTAL PLATFORM DRAW ({0}W CEILING)", ceilingW), fTitle, bTitle, 16, 10);
            }

            // Monospace Jitter-Free Readout Right (Consolas 13pt Bold)
            string valStr = string.Format("{0:0.0} W / {1:0.0} W", totalW, (double)ceilingW);
            double highWarn = ceilingW - 20.0;
            Color valColor = (totalW > (double)ceilingW) ? VectorPowerHubForm.ColorAccentRed : ((totalW > highWarn) ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorAccentPurple);
            using (Font fVal = new Font("Consolas", 14f, FontStyle.Bold))
            using (Brush bVal = new SolidBrush(valColor)) {
                SizeF sz = g.MeasureString(valStr, fVal);
                g.DrawString(valStr, fVal, bVal, w - 16 - sz.Width, 8);
            }

            // Dual Power Bar (Y = 36, H = 22)
            int barX = 16;
            int barY = 36;
            int barW = w - 32;
            int barH = 22;
            Rectangle barRect = new Rectangle(barX, barY, barW, barH);

            using (GraphicsPath bPath = DarkCardPanel.GetRoundedPath(barRect, 4)) {
                using (Brush bBg = new SolidBrush(Color.FromArgb(18, 20, 26))) g.FillPath(bBg, bPath);
            }

            // Compute widths
            float maxW = (float)ceilingW;
            float cpuPixels = (float)((cpuW / maxW) * barW);
            float gpuPixels = (float)((gpuW / maxW) * barW);
            if (cpuPixels + gpuPixels > barW) {
                float scale = barW / (cpuPixels + gpuPixels);
                cpuPixels *= scale;
                gpuPixels *= scale;
            }

            // Fill CPU (Amber Gold)
            if (cpuPixels > 1) {
                RectangleF rCpu = new RectangleF(barX, barY, cpuPixels, barH);
                using (Brush bCpu = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.FillRectangle(bCpu, rCpu);
                }
            }

            // Fill GPU (Electric Cyan)
            if (gpuPixels > 1) {
                RectangleF rGpu = new RectangleF(barX + cpuPixels, barY, gpuPixels, barH);
                using (Brush bGpu = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.FillRectangle(bGpu, rGpu);
                }
            }

            // Bar Border
            using (GraphicsPath bPath = DarkCardPanel.GetRoundedPath(barRect, 4)) {
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, bPath);
            }

            // Dashed Ceiling Line
            using (Pen pCeil = new Pen(VectorPowerHubForm.ColorAccentRed, 1.5f)) {
                pCeil.DashStyle = DashStyle.Dash;
                g.DrawLine(pCeil, barX + barW - 1, barY - 2, barX + barW - 1, barY + barH + 2);
            }

            // Power Split Subtitle (Y = 66)
            double cpuPct = (totalW > 0.1) ? (cpuW / totalW) * 100.0 : 0.0;
            double gpuPct = (totalW > 0.1) ? (gpuW / totalW) * 100.0 : 0.0;
            string splitStr = string.Format("CPU Package: {0:0.0} W ({1:0.0}%)    |    GPU Dynamic Draw: {2:0.0} W ({3:0.0}%)", cpuW, cpuPct, gpuW, gpuPct);
            using (Font fSplit = new Font("Segoe UI", 8.25f, FontStyle.Bold))
            using (Brush bSplit = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                g.DrawString(splitStr, fSplit, bSplit, 16, 66);
            }

            // Dynamic Status Pill (Y = 92, H = 22)
            string pText;
            Color pBg, pBorder, pDot, pTextCol;
            if (totalW > (double)ceilingW) {
                pText = string.Format("⚡ {0}W MAXIMUM PLATFORM CEILING EXCEEDED", ceilingW);
                pBg = Color.FromArgb(48, 18, 18);
                pBorder = Color.FromArgb(96, 32, 32);
                pDot = VectorPowerHubForm.ColorAccentRed;
                pTextCol = VectorPowerHubForm.ColorAccentRed;
            } else if (totalW > highWarn) {
                pText = "⚡ PEAK DYNAMIC BOOST ACTIVE (FULL 140W TGP ALLOCATED)";
                pBg = Color.FromArgb(45, 32, 12);
                pBorder = Color.FromArgb(90, 60, 20);
                pDot = VectorPowerHubForm.ColorAccentGold;
                pTextCol = VectorPowerHubForm.ColorAccentGold;
            } else {
                double headroom = Math.Max(0.0, (double)ceilingW - totalW);
                pText = string.Format("⚡ BALANCED LOAD • {0:0.0}W DYNAMIC BOOST HEADROOM VERIFIED", headroom);
                pBg = Color.FromArgb(16, 32, 42);
                pBorder = Color.FromArgb(24, 60, 80);
                pDot = VectorPowerHubForm.ColorAccentCyan;
                pTextCol = VectorPowerHubForm.ColorAccentCyan;
            }

            using (Font fP = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                SizeF pSz = g.MeasureString(pText, fP);
                int pW = (int)pSz.Width + 28;
                Rectangle pRect = new Rectangle(16, 92, pW, 26);

                using (GraphicsPath pPath = DarkCardPanel.GetRoundedPath(pRect, 4)) {
                    using (Brush b = new SolidBrush(pBg)) g.FillPath(b, pPath);
                    using (Pen p = new Pen(pBorder, 1f)) g.DrawPath(p, pPath);
                }
                using (Brush bDot = new SolidBrush(pDot)) g.FillEllipse(bDot, 24, 101, 6, 6);
                using (Brush bText = new SolidBrush(pTextCol)) g.DrawString(pText, fP, bText, 34, 97);
            }
        }
    }


}
