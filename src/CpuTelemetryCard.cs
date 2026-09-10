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
    public class CpuTelemetryCard : Control {
        private double cpuPowerW = 0.0;
        private double pCoreGhz = 0.0;
        private double eCoreGhz = 0.0;
        public GlowButton BtnView24Cores { get; private set; }

        public CpuTelemetryCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;

            BtnView24Cores = new GlowButton();
            BtnView24Cores.Text = "◆ 24 CORES VIEW";
            BtnView24Cores.Font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            BtnView24Cores.ButtonColor = Color.FromArgb(22, 26, 36);
            BtnView24Cores.BorderColor = VectorPowerHubForm.ColorAccentGold;
            BtnView24Cores.TextColor = VectorPowerHubForm.ColorAccentGold;
            BtnView24Cores.Size = new Size(145, 24);
            this.Controls.Add(BtnView24Cores);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double pwrW, double pGhz, double eGhz) {
            this.cpuPowerW = pwrW;
            this.pCoreGhz = pGhz;
            this.eCoreGhz = eGhz;
            this.Invalidate();
        }

        protected override void OnResize(EventArgs e) {
            base.OnResize(e);
            if (BtnView24Cores != null) {
                BtnView24Cores.Location = new Point(this.Width - 158, this.Height - 34);
            }
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

            // Background & Border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            // Gold Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title
            using (Font fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                g.DrawString("INTEL CORE ULTRA 9 275HX", fTitle, bTitle, 14, 12);
            }

            // Subtitle
            using (Font fSub = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bSub = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("24 Cores (8 Lion Cove P + 16 Skymont E) • RAPL Package Sensor", fSub, bSub, 14, 32);
            }

            // 3 Columns Metrics with Monospace Consolas figures
            int colW = (w - 28) / 3;

            using (Font fVal = new Font("Consolas", 14f, FontStyle.Bold))
            using (Font fSubL = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Brush bSubL = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                // Col 0: Power
                using (Brush bVal0 = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) {
                    g.DrawString(string.Format("{0:0.0} W", cpuPowerW), fVal, bVal0, 14, 56);
                }
                g.DrawString("Package Power", fSubL, bSubL, 14, 80);

                // Col 1: P-Core
                using (Brush bVal1 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0:0.00} GHz", pCoreGhz), fVal, bVal1, 14 + colW, 56);
                }
                g.DrawString("Avg P-Core", fSubL, bSubL, 14 + colW, 80);

                // Col 2: E-Core
                using (Brush bVal2 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0:0.00} GHz", eCoreGhz), fVal, bVal2, 14 + colW * 2, 56);
                }
                g.DrawString("Avg E-Core", fSubL, bSubL, 14 + colW * 2, 80);
            }

            // Power Progress Bar (Y = 106, H = 6)
            int pBarW = w - 28;
            Rectangle barR = new Rectangle(14, 106, pBarW, 6);
            using (GraphicsPath bp = DarkCardPanel.GetRoundedPath(barR, 2)) {
                using (Brush bg = new SolidBrush(Color.FromArgb(18, 20, 26))) g.FillPath(bg, bp);
            }
            float fillPct = Math.Min(1.0f, Math.Max(0.0f, (float)(cpuPowerW / 75.0)));
            int fillW = (int)(pBarW * fillPct);
            if (fillW > 2) {
                Rectangle fR = new Rectangle(14, 106, fillW, 6);
                using (GraphicsPath fp = DarkCardPanel.GetRoundedPath(fR, 2)) {
                    using (Brush fb = new SolidBrush(VectorPowerHubForm.ColorAccentGold)) g.FillPath(fb, fp);
                }
            }

            // Bottom Target Hint
            using (Font fHint = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bHint = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("Dynamic Target: 55W-75W Max Boost Headroom", fHint, bHint, 14, 126);
            }
        }
    }


}
