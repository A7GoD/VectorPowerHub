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
    public class FpsHeroCard : Control {
        private double fps = 0.0;
        private bool isGameMode = false;
        private string activeGameName = "";
        private int activeGamePid = 0;

        public FpsHeroCard() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void UpdateTelemetry(double f, bool gameMode, string gameName, int pid) {
            this.fps = f;
            this.isGameMode = gameMode;
            this.activeGameName = gameName;
            this.activeGamePid = pid;
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

            // Top Cyan Accent Stripe
            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                g.FillRectangle(b, 12, 0, w - 24, 2);
            }

            // Title
            using (Font fTitle = new Font("Segoe UI", 10f, FontStyle.Bold))
            using (Brush bTitle = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                g.DrawString("IN-GAME FPS TELEMETRY", fTitle, bTitle, 14, 14);
            }

            // Monospace Jitter-Free Digits (Consolas 28pt Bold)
            string fpsStr;
            string unitStr;
            if (isGameMode) {
                if (fps > 0.1) {
                    fpsStr = fps.ToString("F1");
                    unitStr = "FPS";
                } else {
                    fpsStr = "3D ON";
                    unitStr = "ACTIVE";
                }
            } else {
                fpsStr = "0.0";
                unitStr = "FPS";
            }

            Color numColor = isGameMode ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorTextWhite;
            using (Font fVal = new Font("Consolas", 28f, FontStyle.Bold))
            using (Brush bVal = new SolidBrush(numColor)) {
                g.DrawString(fpsStr, fVal, bVal, 14, 36);
                SizeF sz = g.MeasureString(fpsStr, fVal);

                using (Font fUnit = new Font("Segoe UI", 11f, FontStyle.Bold))
                using (Brush bUnit = new SolidBrush(VectorPowerHubForm.ColorAccentCyan)) {
                    g.DrawString(unitStr, fUnit, bUnit, 14 + sz.Width - 4, 50);
                }
            }

            // Status Pill (Y = 92, H = 26)
            string pillText;
            Color pillBg, pillBorder, dotColor, textCol;
            if (isGameMode && !string.IsNullOrEmpty(activeGameName)) {
                string displayGame = (activeGameName.Length > 24) ? activeGameName.Substring(0, 22) + ".." : activeGameName;
                pillText = string.Format("Active Game: {0} (PID {1})", displayGame, activeGamePid);
                pillBg = Color.FromArgb(14, 38, 26);
                pillBorder = Color.FromArgb(24, 76, 50);
                dotColor = VectorPowerHubForm.ColorAccentGreen;
                textCol = VectorPowerHubForm.ColorAccentGreen;
            } else {
                pillText = "Desktop Standby • SwapChain Idle";
                pillBg = Color.FromArgb(22, 26, 36);
                pillBorder = Color.FromArgb(36, 44, 60);
                dotColor = VectorPowerHubForm.ColorTextDim;
                textCol = VectorPowerHubForm.ColorTextMuted;
            }

            using (Font fPill = new Font("Segoe UI", 9f, FontStyle.Bold)) {
                SizeF pillSz = g.MeasureString(pillText, fPill);
                int pillW = Math.Min(w - 28, (int)pillSz.Width + 28);
                Rectangle pillRect = new Rectangle(14, 92, pillW, 26);

                using (GraphicsPath pillPath = DarkCardPanel.GetRoundedPath(pillRect, 4)) {
                    using (Brush b = new SolidBrush(pillBg)) g.FillPath(b, pillPath);
                    using (Pen p = new Pen(pillBorder, 1f)) g.DrawPath(p, pillPath);
                }

                // Dot
                using (Brush bDot = new SolidBrush(dotColor)) {
                    g.FillEllipse(bDot, 22, 102, 7, 7);
                }
                // Text
                using (Brush bText = new SolidBrush(textCol)) {
                    g.DrawString(pillText, fPill, bText, 33, 97);
                }
            }

            // Subtitle
            string subText = (isGameMode && fps <= 0.1)
                ? "Hardware Fallback • RTX 5070 Dynamic Telemetry Engine"
                : "DirectX DXGI SwapChain Hook (ETW Event 42)";
            using (Font fSub = new Font("Segoe UI", 8.5f, FontStyle.Regular))
            using (Brush bSub = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                g.DrawString(subText, fSub, bSub, 14, 126);
            }
        }
    }


}
