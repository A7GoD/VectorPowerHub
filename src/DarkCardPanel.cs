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
    public class DarkCardPanel : Panel {
        public Color BorderColor { get; set; }
        public Color FillColor { get; set; }
        public Color AccentStripeColor { get; set; }
        public bool DrawAccentStripe { get; set; }
        public int CornerRadius { get; set; }

        public DarkCardPanel() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.BorderColor = VectorPowerHubForm.ColorBorder;
            this.FillColor = VectorPowerHubForm.ColorCardBg;
            this.AccentStripeColor = VectorPowerHubForm.ColorAccentCyan;
            this.DrawAccentStripe = false;
            this.CornerRadius = 8;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);

            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, this.Width, this.Height);
            }

            using (GraphicsPath path = GetRoundedPath(rect, CornerRadius)) {
                using (Brush b = new SolidBrush(FillColor)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(BorderColor, 1f)) {
                    g.DrawPath(p, path);
                }
            }

            if (DrawAccentStripe) {
                using (Brush b = new SolidBrush(AccentStripeColor)) {
                    g.FillRectangle(b, 10, 0, this.Width - 20, 2);
                }
            }
        }

        public static GraphicsPath GetRoundedPath(Rectangle rect, int radius) {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }


}
