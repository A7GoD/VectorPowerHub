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
        private CpuTopology topology;
        private double pkgPowerW = 0.0;

        public PerCoreTopologyControl() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(18, 19, 24); // Force dark slate — prevents white flash
            if (PowerCoreEngine.Instance != null) {
                this.topology = PowerCoreEngine.Instance.Topology;
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void SetCoreData(CpuTopology topology, double pkgPowerW) {
            this.topology = topology;
            this.pkgPowerW = pkgPowerW;
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            if (w <= 0 || h <= 0) return;

            // Fill entire control surface with dark slate — prevents default white system Control background bleed-through
            using (Brush bgFill = new SolidBrush(Color.FromArgb(18, 19, 24))) {
                g.FillRectangle(bgFill, 0, 0, w, h);
            }

            CpuTopology topo = this.topology;
            if (topo == null && PowerCoreEngine.Instance != null) {
                topo = PowerCoreEngine.Instance.Topology;
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

            CpuCore peakCore = null;
            CpuCluster peakCluster = null;
            double peakGhz = 0.0;
            CpuCluster pCluster = null;
            CpuCluster eCluster = null;

            if (topo != null && topo.Clusters != null) {
                for (int c = 0; c < topo.Clusters.Count; c++) {
                    CpuCluster cluster = topo.Clusters[c];
                    if (cluster == null || cluster.Cores == null) continue;
                    if (cluster.EfficiencyClass > 0 && pCluster == null) pCluster = cluster;
                    else if (cluster.EfficiencyClass == 0 && eCluster == null) eCluster = cluster;

                    for (int k = 0; k < cluster.Cores.Count; k++) {
                        CpuCore core = cluster.Cores[k];
                        if (core.CurrentGhz > peakGhz) {
                            peakGhz = core.CurrentGhz;
                            peakCore = core;
                            peakCluster = cluster;
                        }
                    }
                }
            }
            if (pCluster == null && topo != null && topo.Clusters != null && topo.Clusters.Count > 0) pCluster = topo.Clusters[0];
            if (eCluster == null && topo != null && topo.Clusters != null && topo.Clusters.Count > 1) eCluster = topo.Clusters[1];

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
                    string peakStr = "N/A";
                    if (peakCore != null) {
                        bool isPeakP = (peakCluster != null && peakCluster.EfficiencyClass > 0);
                        peakStr = string.Format("{0}-{1} ({2:0.00} GHz)", isPeakP ? "P" : "E", peakCore.Id, peakGhz);
                    }
                    g.DrawString(peakStr, fVal, bV, col1X, 22);
                }

                // Col 2: P-Core Cluster Avg
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    string lbl = "◆ P-CORES";
                    string val = "-- GHz Avg";
                    if (pCluster != null && pCluster.Cores != null && pCluster.Cores.Count > 0) {
                        double sum = 0;
                        for (int k = 0; k < pCluster.Cores.Count; k++) sum += pCluster.Cores[k].CurrentGhz;
                        lbl = string.Format("◆ {0} ({1}C)", (pCluster.EfficiencyClass > 0 ? "P-CORES" : "CORES"), pCluster.Cores.Count);
                        val = string.Format("{0:0.00} GHz Avg", sum / pCluster.Cores.Count);
                    }
                    g.DrawString(lbl, fLabel, bL, col2X, 6);
                    g.DrawString(val, fVal, bV, col2X, 22);
                }

                // Col 3: E-Core Cluster Avg
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    string lbl = "✦ E-CORES";
                    string val = "-- GHz Avg";
                    if (eCluster != null && eCluster.Cores != null && eCluster.Cores.Count > 0) {
                        double sum = 0;
                        for (int k = 0; k < eCluster.Cores.Count; k++) sum += eCluster.Cores[k].CurrentGhz;
                        lbl = string.Format("✦ {0} ({1}C)", (eCluster.EfficiencyClass == 0 ? "E-CORES" : "CLUSTER 2"), eCluster.Cores.Count);
                        val = string.Format("{0:0.00} GHz Avg", sum / eCluster.Cores.Count);
                    }
                    g.DrawString(lbl, fLabel, bL, col3X, 6);
                    g.DrawString(val, fVal, bV, col3X, 22);
                }

                // Col 4: RAPL CPU Package Draw
                using (Brush bL = new SolidBrush(VectorPowerHubForm.ColorTextDim))
                using (Brush bV = new SolidBrush(VectorPowerHubForm.ColorAccentPurple)) {
                    g.DrawString("⚡ RAPL POWER", fLabel, bL, col4X, 6);
                    g.DrawString(string.Format("{0:0.0} W", pkgPowerW), fVal, bV, col4X, 22);
                }
            }

            if (topo == null || topo.Clusters == null || topo.Clusters.Count == 0) return;

            int curY = sumH + 8;
            for (int c = 0; c < topo.Clusters.Count; c++) {
                CpuCluster cluster = topo.Clusters[c];
                if (cluster == null || cluster.Cores == null || cluster.Cores.Count == 0) continue;

                int count = cluster.Cores.Count;
                bool isPCore = (cluster.EfficiencyClass > 0);
                Color titleColor = isPCore ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorAccentGold;

                int firstId = cluster.Cores[0].Id;
                int lastId = cluster.Cores[count - 1].Id;
                string rangeStr = isPCore
                    ? (count == 1 ? string.Format("P{0}", firstId) : string.Format("P{0} – P{1}", firstId, lastId))
                    : (count == 1 ? string.Format("E{0:00}", firstId) : string.Format("E{0:00} – E{1:00}", firstId, lastId));

                string title = string.Format("{0} — {1} CORES ({2} • UP TO {3:0.0} GHz PEAK)",
                    cluster.Name.ToUpper(), count, rangeStr, isPCore ? 5.5 : 4.0);

                using (Font fSec = new Font("Segoe UI", 9.5f, FontStyle.Bold))
                using (Brush bTitle = new SolidBrush(titleColor)) {
                    g.DrawString(title, fSec, bTitle, 4, curY);
                }

                int cardsTop = curY + 20;
                int cols = (count < 8 && count > 0) ? count : ((count == 12) ? 6 : 8);
                int gap = 6;
                int cardW = (w - (cols - 1) * gap) / cols;
                if (cardW <= 0) cardW = 1;
                int cardH = isPCore ? 48 : 44;

                for (int k = 0; k < count; k++) {
                    CpuCore core = cluster.Cores[k];
                    int col = k % cols;
                    int row = k / cols;
                    int cx = col * (cardW + gap);
                    int cy = cardsTop + row * (cardH + gap);

                    string coreLabel = isPCore
                        ? string.Format("P-Core {0}", core.Id)
                        : string.Format("E{0:00}", core.Id);

                    DrawCoreCard(g, cx, cy, cardW, cardH, coreLabel, cluster.Name, core.CurrentGhz, core.CurrentUtil, isPCore);
                }

                int rows = (count + cols - 1) / cols;
                curY = cardsTop + rows * cardH + (rows - 1) * gap + 10;
            }
        }
    }
}
