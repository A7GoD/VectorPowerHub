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
    public class GlowButton : Button {
        public Color ButtonColor { get; set; }
        public Color BorderColor { get; set; }
        public Color TextColor { get; set; }
        
        private bool isHovered = false;
        private float hoverProgress = 0f;
        
        public bool IsPulsing { get; set; }
        private float pulseProgress = 0f;
        private bool pulseDir = true;
        
        private System.Windows.Forms.Timer animTimer;

        public GlowButton() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.ButtonColor = VectorPowerHubForm.ColorCardBg;
            this.BorderColor = VectorPowerHubForm.ColorBorder;
            this.TextColor = VectorPowerHubForm.ColorTextWhite;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            animTimer = new System.Windows.Forms.Timer();
            animTimer.Interval = 16; // ~60fps
            animTimer.Tick += AnimTimer_Tick;
            animTimer.Start();

            this.MouseEnter += (s, e) => { isHovered = true; };
            this.MouseLeave += (s, e) => { isHovered = false; };
        }
        
        protected override void Dispose(bool disposing) {
            if (disposing && animTimer != null) {
                animTimer.Stop();
                animTimer.Dispose();
            }
            base.Dispose(disposing);
        }

        private void AnimTimer_Tick(object sender, EventArgs e) {
            bool needsPaint = false;

            if (isHovered && hoverProgress < 1f) { hoverProgress += 0.1f; if (hoverProgress > 1f) hoverProgress = 1f; needsPaint = true; }
            else if (!isHovered && hoverProgress > 0f) { hoverProgress -= 0.1f; if (hoverProgress < 0f) hoverProgress = 0f; needsPaint = true; }

            if (IsPulsing) {
                if (pulseDir) { pulseProgress += 0.05f; if (pulseProgress >= 1f) { pulseProgress = 1f; pulseDir = false; } }
                else { pulseProgress -= 0.05f; if (pulseProgress <= 0f) { pulseProgress = 0f; pulseDir = true; } }
                needsPaint = true;
            } else if (pulseProgress > 0f) {
                pulseProgress -= 0.1f; if (pulseProgress < 0f) pulseProgress = 0f;
                needsPaint = true;
            }

            if (needsPaint) this.Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        private Color Lerp(Color c1, Color c2, float t) {
            return Color.FromArgb(
                (int)(c1.A + (c2.A - c1.A) * t),
                (int)(c1.R + (c2.R - c1.R) * t),
                (int)(c1.G + (c2.G - c1.G) * t),
                (int)(c1.B + (c2.B - c1.B) * t));
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (this.Parent != null) {
                using (Brush bP = new SolidBrush(this.Parent.BackColor)) g.FillRectangle(bP, 0, 0, this.Width, this.Height);
            } else {
                using (Brush bP = new SolidBrush(VectorPowerHubForm.ColorBgMain)) g.FillRectangle(bP, 0, 0, this.Width, this.Height);
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            
            Color hoverBg = Color.FromArgb(40, 46, 62);
            Color hoverBorder = TextColor;
            
            Color fill = !this.Enabled ? Color.FromArgb(25, 27, 35) : Lerp(ButtonColor, hoverBg, hoverProgress);
            Color border = !this.Enabled ? Color.FromArgb(45, 50, 66) : Lerp(BorderColor, hoverBorder, hoverProgress);
            Color textC = !this.Enabled ? Color.FromArgb(80, 90, 110) : TextColor;

            if (IsPulsing || pulseProgress > 0f) {
                Color pulseCol = Color.FromArgb(80, 10, 10);
                fill = Lerp(fill, pulseCol, pulseProgress);
            }

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 4)) {
                using (Brush b = new SolidBrush(fill)) g.FillPath(b, path);
                using (Pen p = new Pen(border, 1.2f)) g.DrawPath(p, path);
            }

            StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Brush b = new SolidBrush(textC)) {
                g.DrawString(this.Text, this.Font, b, rect, sf);
            }
        }
    }
}
