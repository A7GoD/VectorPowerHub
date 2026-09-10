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
    public partial class VectorPowerHubForm : Form {
        private void BuildProfilesView() {
            // 5. VIEW 1: PROFILE SELECTION CARDS
            // =========================================================================
            panelProfilesView = new Panel();
            panelProfilesView.BackColor = ColorBgMain;

            cardProfileSnappy = new ProfileCard(
                "⚡ Snappy-Pacing",
                "COMPETITIVE MAX FPS (DEFAULT)",
                ColorAccentCyan,
                new string[] {
                    "• Boost Mode 4 (Efficient Aggressive)",
                    "• Unbounded Freq (Up to 5.5 GHz Peak)",
                    "• EPP 30% (Eager GPU Power Release)",
                    "• Full 140W RTX 5070 Mobile Headroom",
                    "• Instant draw-call responsiveness",
                    "• Default competitive esports profile"
                },
                "ACTIVE",
                true
            );
            cardProfileSnappy.GamingTargetClicked += (s, e) => SelectProfile("snappy");
            cardProfileSnappy.IdleTargetClicked += (s, e) => SelectDesktopProfile("desktop");
            cardProfileSnappy.ProfileClicked += (s, e) => SelectProfile("snappy");

            cardProfileEfficiency = new ProfileCard(
                "✦ Sweet-Spot Efficiency",
                "CLAMPED 4.9 GHz (ZERO STARVATION)",
                ColorAccentGold,
                new string[] {
                    "• P-Core Clamped to 4.9 GHz (4900 MHz)",
                    "• E-Core Clamped to 2.8 GHz (2800 MHz)",
                    "• 58W CPU Ceiling (Zero Starvation)",
                    "• 100% Guaranteed 140W GPU Budget",
                    "• Rock-solid frame pacing & thermals",
                    "• Ideal for heavy AAA open-world titles"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileEfficiency.GamingTargetClicked += (s, e) => SelectProfile("clamped");
            cardProfileEfficiency.IdleTargetClicked += (s, e) => SelectDesktopProfile("powersaver");
            cardProfileEfficiency.ProfileClicked += (s, e) => SelectProfile("clamped");

            cardProfileCold = new ProfileCard(
                "❄ Cold & Quiet",
                "GPU-SHIFT 2100 MHz (74°C THERMALS)",
                ColorAccentBlue,
                new string[] {
                    "• GPU Clock Clamped: 2100 MHz (~100W)",
                    "• CPU Boost Mode 3 (Efficient Enabled)",
                    "• EPP: 20% (Max Draw Latency)",
                    "• Sub-74°C Sustained GPU Core Temp",
                    "• Ultra-Quiet Acoustic Operation",
                    "• Perfect for stealth & night sessions"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileCold.GamingTargetClicked += (s, e) => SelectProfile("cold");
            cardProfileCold.IdleTargetClicked += (s, e) => SelectDesktopProfile("cold");
            cardProfileCold.ProfileClicked += (s, e) => SelectProfile("cold");

            panelProfilesView.Controls.Add(cardProfileSnappy);
            panelProfilesView.Controls.Add(cardProfileEfficiency);
            panelProfilesView.Controls.Add(cardProfileCold);
            this.Controls.Add(panelProfilesView);

            // Custom Tuner Overlay
            InitializeCustomTunerPanel();

            // =========================================================================

        }

        private void BuildViewsAndFooter() {
            // 6. VIEW 2: AUTOMATED BENCHMARK PANEL
            // =========================================================================
            InitializeBenchmarkPanel();

            // =========================================================================
            // 7. VIEW 3: PER-CORE TOPOLOGY & INTEGRATED HARDWARE TUNER
            // =========================================================================
            InitializeTopologyPanel();

            // =========================================================================
            // 8. FOOTER PANEL (Bottom 46px)
            // =========================================================================
            panelFooter = new Panel();
            panelFooter.Dock = DockStyle.Bottom;
            panelFooter.Height = 52;
            panelFooter.BackColor = Color.FromArgb(14, 15, 20);

            lblFooterStatus = new Label();
            lblFooterStatus.Text = "● ETW DXGI Active  |  Telemetry: 750ms  |  D3cold Safe Architecture  |  BenQ EX271Q Aware";
            lblFooterStatus.Font = new Font("Segoe UI", 8.25f, FontStyle.Regular);
            lblFooterStatus.ForeColor = ColorTextDim;
            lblFooterStatus.Location = new Point(16, 17);
            lblFooterStatus.AutoSize = true;

            btnOpenTuner = new GlowButton();
            btnOpenTuner.Text = "⚙ Hardware Tuner";
            btnOpenTuner.Size = new Size(180, 34);
            btnOpenTuner.ButtonColor = ColorCardBg;
            btnOpenTuner.BorderColor = ColorAccentPurple;
            btnOpenTuner.TextColor = ColorAccentPurple;
            btnOpenTuner.Click += (s, e) => ToggleCustomTuner();

            btnFooterTray = new GlowButton();
            btnFooterTray.Text = "▼ Tray";
            btnFooterTray.Size = new Size(95, 34);
            btnFooterTray.ButtonColor = ColorCardBg;
            btnFooterTray.BorderColor = ColorBorder;
            btnFooterTray.TextColor = ColorTextWhite;
            btnFooterTray.Click += (s, e) => MinimizeToTray();

            btnFooterExit = new GlowButton();
            btnFooterExit.Text = "✕ Exit";
            btnFooterExit.Size = new Size(80, 34);
            btnFooterExit.ButtonColor = ColorCardBg;
            btnFooterExit.BorderColor = ColorAccentRed;
            btnFooterExit.TextColor = ColorAccentRed;
            btnFooterExit.Click += (s, e) => ExitApplication();

            panelFooter.Controls.Add(lblFooterStatus);
            panelFooter.Controls.Add(btnOpenTuner);
            panelFooter.Controls.Add(btnFooterTray);
            panelFooter.Controls.Add(btnFooterExit);
            this.Controls.Add(panelFooter);

        }
    }
}

