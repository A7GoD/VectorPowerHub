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

        public GlowButton() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.ButtonColor = VectorPowerHubForm.ColorCardBg;
            this.BorderColor = VectorPowerHubForm.ColorBorder;
            this.TextColor = VectorPowerHubForm.ColorTextWhite;
            this.Cursor = Cursors.Hand;
            this.Font = new Font("Segoe UI", 9f, FontStyle.Bold);

            this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; this.Invalidate(); };
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (this.Parent != null) {
                using (Brush bP = new SolidBrush(this.Parent.BackColor)) {
                    g.FillRectangle(bP, 0, 0, this.Width, this.Height);
                }
            } else {
                using (Brush bP = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                    g.FillRectangle(bP, 0, 0, this.Width, this.Height);
                }
            }

            Rectangle rect = new Rectangle(0, 0, this.Width - 1, this.Height - 1);
            Color fill = !this.Enabled ? Color.FromArgb(25, 27, 35) : (isHovered ? Color.FromArgb(40, 46, 62) : ButtonColor);
            Color border = !this.Enabled ? Color.FromArgb(45, 50, 66) : (isHovered ? TextColor : BorderColor);
            Color textC = !this.Enabled ? Color.FromArgb(80, 90, 110) : TextColor;

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 4)) {
                using (Brush b = new SolidBrush(fill)) {
                    g.FillPath(b, path);
                }
                using (Pen p = new Pen(border, 1.2f)) {
                    g.DrawPath(p, path);
                }
            }

            StringFormat sf = new StringFormat() {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap
            };
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (Brush b = new SolidBrush(textC)) {
                g.DrawString(this.Text, this.Font, b, rect, sf);
            }
        }
    }


}
