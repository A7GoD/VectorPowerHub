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
    public class BenchmarkProgressBar : Control {
        private int currentVal = 0;
        public int Value {
            get { return currentVal; }
            set { currentVal = Math.Max(0, Math.Min(100, value)); this.Invalidate(); }
        }

        public BenchmarkProgressBar() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;

            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 3)) {
                using (Brush b = new SolidBrush(Color.FromArgb(14, 16, 22))) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            int fillW = (int)((currentVal / 100.0) * (w - 4));
            if (fillW > 2) {
                Rectangle fillRect = new Rectangle(2, 2, fillW, h - 4);
                using (GraphicsPath fillPath = DarkCardPanel.GetRoundedPath(fillRect, 2)) {
                    using (LinearGradientBrush lgb = new LinearGradientBrush(fillRect, VectorPowerHubForm.ColorAccentPurple, VectorPowerHubForm.ColorAccentCyan, 0f)) {
                        g.FillPath(lgb, fillPath);
                    }
                }
            }
        }
    }

    // BenchmarkResultsGrid: Clean dark table with Monospace Consolas digits and Raw vs Cleaned comparisons

}
