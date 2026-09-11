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
    public class ProfileCard : Control {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public Color AccentColor { get; set; }
        public string[] Specs { get; set; }
        public string ButtonText { get; set; }
        public bool IsActive { get; set; }
        public bool IsDesignatedGamingProfile { get; set; }
        public bool IsDesignatedIdleProfile { get; set; }
        public bool IsGameMode { get; set; }
        public bool AutoSwitchEnabled { get; set; }

        private bool isHovered = false;
        private int hoveredButton = 0; // 0 = card body, 1 = Gaming, 2 = Idle
        private Rectangle btnGamingRect = Rectangle.Empty;
        private Rectangle btnIdleRect = Rectangle.Empty;

        public event EventHandler ProfileClicked;
        public event EventHandler GamingTargetClicked;
        public event EventHandler IdleTargetClicked;

        public ProfileCard(string title, string subtitle, Color accentColor, string[] specs, string buttonText, bool isActive) {
            this.Title = title;
            this.Subtitle = subtitle;
            this.AccentColor = accentColor;
            this.Specs = specs;
            this.ButtonText = buttonText;
            this.IsActive = isActive;

            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
            this.Cursor = Cursors.Hand;

            this.MouseEnter += (s, e) => { isHovered = true; this.Invalidate(); };
            this.MouseLeave += (s, e) => { isHovered = false; hoveredButton = 0; this.Invalidate(); };
            this.MouseMove += OnCardMouseMove;
            this.MouseUp += OnCardMouseUp;
        }

        private void OnCardMouseMove(object sender, MouseEventArgs e) {
            int prev = hoveredButton;
            if (btnGamingRect.Contains(e.Location)) hoveredButton = 1;
            else if (btnIdleRect.Contains(e.Location)) hoveredButton = 2;
            else hoveredButton = 0;
            if (hoveredButton != prev) this.Invalidate();
        }

        private void OnCardMouseUp(object sender, MouseEventArgs e) {
            if (e.Button != MouseButtons.Left) return;
            if (btnGamingRect.Contains(e.Location)) {
                if (GamingTargetClicked != null) GamingTargetClicked(this, EventArgs.Empty);
            } else if (btnIdleRect.Contains(e.Location)) {
                if (IdleTargetClicked != null) IdleTargetClicked(this, EventArgs.Empty);
            } else {
                if (ProfileClicked != null) ProfileClicked(this, EventArgs.Empty);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            using (Brush bBg = new SolidBrush(VectorPowerHubForm.ColorBgMain)) {
                g.FillRectangle(bBg, 0, 0, w, h);
            }

            bool isHighlighted = IsActive || (IsDesignatedGamingProfile && IsGameMode) || (IsDesignatedIdleProfile && !IsGameMode && AutoSwitchEnabled);
            Color fill = isHighlighted ? VectorPowerHubForm.ColorCardSelected : (isHovered ? VectorPowerHubForm.ColorCardHover : VectorPowerHubForm.ColorCardBg);
            Color border = isHighlighted ? AccentColor : (isHovered ? VectorPowerHubForm.ColorBorderHighlight : VectorPowerHubForm.ColorBorder);
            float borderThickness = isHighlighted ? 2f : 1f;

            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(rect, 8)) {
                using (Brush b = new SolidBrush(fill)) { g.FillPath(b, path); }
                using (Pen p = new Pen(border, borderThickness)) { g.DrawPath(p, path); }
            }

            using (Brush b = new SolidBrush(AccentColor)) {
                g.FillRectangle(b, 12, 0, w - 24, isHighlighted ? 3 : 2);
            }

            float titleSize = (w < 290) ? 10f : 11.5f;
            using (Font fTitle = new Font("Segoe UI", titleSize, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(AccentColor)) {
                    g.DrawString(Title, fTitle, b, 12, 12);
                }
            }

            float subSize = (w < 290) ? 7f : 7.5f;
            using (Font fSub = new Font("Segoe UI", subSize, FontStyle.Bold)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                    g.DrawString(Subtitle, fSub, b, 13, 36);
                }
            }

            btnIdleRect = new Rectangle(14, h - 36, w - 28, 28);
            btnGamingRect = new Rectangle(14, h - 70, w - 28, 28);

            int availableHeight = btnGamingRect.Top - 58;
            int lineSpacing = Math.Min(20, Math.Max(14, availableHeight / Math.Max(1, Specs.Length)));
            int specY = 56;

            using (Font fSpec = new Font("Segoe UI", 8.25f, FontStyle.Regular)) {
                using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                    for (int i = 0; i < Specs.Length; i++) {
                        g.DrawString(Specs[i], fSpec, b, 14, specY);
                        specY += lineSpacing;
                    }
                }
            }

            DrawButton(g, btnGamingRect, GetGamingButtonLabel(), IsActive && IsGameMode, IsDesignatedGamingProfile, AccentColor, hoveredButton == 1);
            DrawButton(g, btnIdleRect, GetIdleButtonLabel(), IsActive && !IsGameMode && AutoSwitchEnabled, IsDesignatedIdleProfile, VectorPowerHubForm.ColorAccentGreen, hoveredButton == 2);
        }

        private string GetGamingButtonLabel() {
            if (IsActive && IsGameMode) return "✔ ACTIVE GAMING TARGET";
            if (IsDesignatedGamingProfile) return AutoSwitchEnabled ? "★ GAMING TARGET (AUTO-ON)" : "★ GAMING TARGET";
            return "★ SET GAMING TARGET";
        }

        private string GetIdleButtonLabel() {
            if (IsActive && !IsGameMode && AutoSwitchEnabled) return "✔ ACTIVE IDLE TARGET";
            if (IsDesignatedIdleProfile) return AutoSwitchEnabled ? "☆ IDLE TARGET (AUTO-ON)" : "☆ IDLE TARGET";
            return "☆ SET IDLE TARGET";
        }

        private void DrawButton(Graphics g, Rectangle r, string text, bool isLive, bool isDesignated, Color accent, bool isHover) {
            using (GraphicsPath path = DarkCardPanel.GetRoundedPath(r, 4)) {
                StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                if (isLive) {
                    using (Brush b = new SolidBrush(accent)) { g.FillPath(b, path); }
                    using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                        using (Brush bText = new SolidBrush(VectorPowerHubForm.ColorBgMain)) { g.DrawString(text, f, bText, r, sf); }
                    }
                } else if (isDesignated) {
                    using (Brush b = new SolidBrush(Color.FromArgb(24, 38, 50))) { g.FillPath(b, path); }
                    using (Pen p = new Pen(accent, 1.5f)) { g.DrawPath(p, path); }
                    using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold)) {
                        using (Brush bText = new SolidBrush(accent)) { g.DrawString(text, f, bText, r, sf); }
                    }
                } else {
                    using (Brush b = new SolidBrush(Color.FromArgb(20, 24, 32))) { g.FillPath(b, path); }
                    using (Pen p = new Pen(isHover ? accent : VectorPowerHubForm.ColorBorder, 1f)) { g.DrawPath(p, path); }
                    using (Font f = new Font("Segoe UI", 8.25f, FontStyle.Bold)) {
                        using (Brush bText = new SolidBrush(isHover ? accent : VectorPowerHubForm.ColorTextMuted)) { g.DrawString(text, f, bText, r, sf); }
                    }
                }
            }
        }
    }
}
