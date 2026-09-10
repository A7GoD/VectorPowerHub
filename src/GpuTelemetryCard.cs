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
    public class GpuTelemetryCard : Control {
        private double gpuPowerW = 0.0;
        private int gpuClockMhz = 0;
        private int gpuTempC = 0;
        private int gpuUtilPct = 0;
        private bool isGameMode = false;
        private bool isNvidiaDisplayAttached = false;
        private string nvidiaMonitorName = "";
        private string gpuStatus = "";

        public GpuTelemetryCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double pwrW, int clkMhz, int tempC, int utilPct, bool gameMode, bool displayAttached, string monitorName, string status) {
            this.gpuPowerW = pwrW;
            this.gpuClockMhz = clkMhz;
            this.gpuTempC = tempC;
            this.gpuUtilPct = utilPct;
            this.isGameMode = gameMode;
            this.isNvidiaDisplayAttached = displayAttached;
            this.nvidiaMonitorName = monitorName;
            this.gpuStatus = status;
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

            // Background & Border
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 6)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) g.FillPath(b, path);
                using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) g.DrawPath(p, path);
            }

            // Cyan Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title
            using (Font fTitle = new Font("Segoe UI", 10.5f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                g.DrawString("NVIDIA GEFORCE RTX 5070 MOBILE", fTitle, bTitle, 14, 12);
            }

            // Subtitle
            using (Font fSub = new Font("Segoe UI", 8.25f, FontStyle.Regular))
            using (Brush bSub = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString("140W Max Dynamic TGP • Active Display D3cold Safety", fSub, bSub, 14, 32);
            }

            // 4 Columns Metrics with Monospace Consolas figures
            int colW = (w - 28) / 4;

            using (Font fVal = new Font("Consolas", 14f, FontStyle.Bold))
            using (Font fSubL = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Brush bSubL = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                // Col 0: Power
                using (Brush bVal0 = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString(string.Format("{0:0.0} W", gpuPowerW), fVal, bVal0, 14, 56);
                }
                g.DrawString("Dynamic TGP", fSubL, bSubL, 14, 80);

                // Col 1: Clock
                using (Brush bVal1 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0} MHz", gpuClockMhz), fVal, bVal1, 14 + colW, 56);
                }
                g.DrawString("Core Clock", fSubL, bSubL, 14 + colW, 80);

                // Col 2: Temp
                string tempStr = (gpuTempC > 0) ? string.Format("{0} °C", gpuTempC) : "-- °C";
                using (Brush bVal2 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(tempStr, fVal, bVal2, 14 + colW * 2, 56);
                }
                g.DrawString("Hotspot Temp", fSubL, bSubL, 14 + colW * 2, 80);

                // Col 3: Util
                using (Brush bVal3 = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    g.DrawString(string.Format("{0}%", gpuUtilPct), fVal, bVal3, 14 + colW * 3, 56);
                }
                g.DrawString("GPU Load", fSubL, bSubL, 14 + colW * 3, 80);
            }

            // Accurate GPU Status Pill (Y = 114, H = 26)
            string pillText;
            Color pillBg, pillBorder, dotColor, textCol;

            if (isNvidiaDisplayAttached) {
                string mon = string.IsNullOrEmpty(nvidiaMonitorName) ? "BenQ EX2710Q" : nvidiaMonitorName;
                pillText = string.Format("Active (D0) • Driving {0}", mon);
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            } else if (isGameMode || gpuPowerW > 25.0) {
                pillText = "Active Rendering (D0) • Full 140W Dynamic Headroom";
                pillBg = Color.FromArgb(42, 32, 14);
                pillBorder = Color.FromArgb(88, 64, 22);
                dotColor = VectorPowerHubForm.ColorAccentGold;
                textCol = VectorPowerHubForm.ColorAccentGold;
            } else if (!string.IsNullOrEmpty(gpuStatus) && gpuStatus.Contains("D3cold")) {
                pillText = gpuStatus;
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            } else {
                pillText = string.IsNullOrEmpty(gpuStatus) ? "D3cold Sleeping (0.0W) • PCIe Link Off" : gpuStatus;
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            }

            using (Font fPill = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                SizeF pillSz = g.MeasureString(pillText, fPill);
                int pillW = (int)pillSz.Width + 28;
                Rectangle pillRect = new Rectangle(14, 114, pillW, 26);

                using (GraphicsPath pillPath = DarkCardPanel.GetRoundedPath(pillRect, 4)) {
                    using (Brush b = new SolidBrush(pillBg)) g.FillPath(b, pillPath);
                    using (Pen p = new Pen(pillBorder, 1f)) g.DrawPath(p, pillPath);
                }

                // Dot
                using (Brush bDot = new SolidBrush(dotColor)) {
                    g.FillEllipse(bDot, 22, 124, 6, 6);
                }
                // Text
                using (Brush bText = new SolidBrush(textCol)) {
                    g.DrawString(pillText, fPill, bText, 32, 119);
                }
            }
        }
    }

    // =========================================================================================
    // CUSTOM GDI+ CONTROLS
    // =========================================================================================


}
