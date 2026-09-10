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
        private double[] coreGhz = new double[24];
        private double[] coreUtil = new double[24];
        private double pkgPowerW = 0.0;

        public PerCoreTopologyControl() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(18, 19, 24); // Force dark slate — prevents white flash
            for (int i = 0; i < 24; i++) {
                coreGhz[i] = (i < 8) ? 2.7 : 2.1;
                coreUtil[i] = 0.0;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void SetCoreData(double[] ghz, double[] util, double pkgW) {
            if (ghz != null && ghz.Length == 24) {
                Array.Copy(ghz, this.coreGhz, 24);
            }
            if (util != null && util.Length == 24) {
                Array.Copy(util, this.coreUtil, 24);
            }
            this.pkgPowerW = pkgW;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;

            // Fill entire control surface with dark slate — prevents default white system Control background bleed-through
            using (Brush bgFill = new SolidBrush(Color.FromArgb(18, 19, 24))) {
                g.FillRectangle(bgFill, 0, 0, w, h);
            }

            // 1. Top Summary Strip (H = 48)
            int sumH = 48;
            Rectangle sumRect = new Rectangle(0, 0, w - 1, sumH);
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(sumRect, 6)) {
                using (Brush b = new SolidBrush(Color.FromArgb(22, 25, 33))) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            // Find peak core
            int peakCoreIdx = 0;
            double peakGhz = 0.0;
            double pSum = 0;
            double eSum = 0;
            for (int i = 0; i < 24; i++) {
                if (coreGhz[i] > peakGhz) { peakGhz = coreGhz[i]; peakCoreIdx = i; }
                if (i < 8) pSum += coreGhz[i];
                else eSum += coreGhz[i];
            }
            double avgPGhz = pSum / 8.0;
            double avgEGhz = eSum / 16.0;

            int col1X = 14;
            int col2X = (w - 28) / 4 + 14;
            int col3X = ((w - 28) / 4) * 2 + 14;
            int col4X = ((w - 28) / 4) * 3 + 14;

            using (Font fLabel = new Font("Segoe UI", 8f, FontStyle.Bold))
            using (Font fVal = new Font("Consolas", 12f, FontStyle.Bold)) {
                // Col 1: Peak Core
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString("⚡ PEAK BURST", fLabel, bL, col1X, 6);
                    string peakStr = string.Format("{0}-{1} ({2:0.00} GHz)", (peakCoreIdx < 8 ? "P" : "E"), (peakCoreIdx < 8 ? peakCoreIdx : peakCoreIdx - 8), peakGhz);
                    g.DrawString(peakStr, fVal, bV, col1X, 22);
                }

                // Col 2: P-Core Cluster Avg
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString("◆ P-CORES (8C)", fLabel, bL, col2X, 6);
                    g.DrawString(string.Format("{0:0.00} GHz Avg", avgPGhz), fVal, bV, col2X, 22);
                }

                // Col 3: E-Core Cluster Avg
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString("✦ E-CORES (16C)", fLabel, bL, col3X, 6);
                    g.DrawString(string.Format("{0:0.00} GHz Avg", avgEGhz), fVal, bV, col3X, 22);
                }

                // Col 4: RAPL CPU Package Draw
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentPurple)) {
                    g.DrawString("⚡ RAPL POWER", fLabel, bL, col4X, 6);
                    g.DrawString(string.Format("{0:0.0} W", pkgPowerW), fVal, bV, col4X, 22);
                }
            }

            // 2. Section 1: Performance Cores Cluster (P0 to P7 - 1 Row of 8 Cores)
            int pSecY = sumH + 8;
            using (Font fSec = new Font("Segoe UI", 9.5f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString("PERFORMANCE CORES — 8 LION COVE CORES (P0 – P7 • UP TO 5.5 GHz PEAK)", fSec, b, 4, pSecY);
                }
            }

            int pCardsTop = pSecY + 20;
            int pCols = 8;
            int pGap = 6;
            int pCardW = (w - (pCols - 1) * pGap) / pCols;
            int pCardH = 48;

            for (int i = 0; i < 8; i++) {
                int cx = i * (pCardW + pGap);
                int cy = pCardsTop;
                DrawCoreCard(g, cx, cy, pCardW, pCardH, string.Format("P-Core {0}", i), "Lion Cove", coreGhz[i], coreUtil[i], true);
            }

            // 3. Section 2: Efficient Cores Cluster (E0 to E15 - 2 Rows of 8 Cores)
            int eSecY = pCardsTop + pCardH + 8;
            using (Font fSec = new Font("Segoe UI", 9.5f, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString("EFFICIENT CORES — 16 SKYMONT CORES (E00 – E15 • UP TO 4.0 GHz PEAK)", fSec, b, 4, eSecY);
                }
            }

            int eCardsTop = eSecY + 20;
            int eCols = 8;
            int eGap = 6;
            int eCardW = (w - (eCols - 1) * eGap) / eCols;
            int eCardH = 44;

            for (int i = 0; i < 16; i++) {
                int col = i % eCols;
                int row = i / eCols;
                int cx = col * (eCardW + eGap);
                int cy = eCardsTop + row * (eCardH + eGap);
                DrawCoreCard(g, cx, cy, eCardW, eCardH, string.Format("E{0:00}", i), "Skymont", coreGhz[8 + i], coreUtil[8 + i], false);
            }
        }


    }
}
