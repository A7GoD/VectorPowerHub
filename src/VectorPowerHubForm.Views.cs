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
                    "• Boost Mode 4 (Eff. Aggressive)",
                    "• Unbounded Freq (5.5 GHz Peak)",
                    "• EPP 30% (Eager GPU Power)",
                    "• Full 140W RTX 5070 Headroom",
                    "• Instant draw-call response",
                    "• Default competitive esports"
                },
                "ACTIVE",
                true
            );
            cardProfileSnappy.GamingTargetClicked += (s, e) => SelectProfile("snappy");
            cardProfileSnappy.IdleTargetClicked += (s, e) => SelectDesktopProfile("desktop");
            cardProfileSnappy.ProfileClicked += (s, e) => SelectProfile("snappy");

            cardProfileEfficiency = new ProfileCard(
                "✦ Sweet-Spot Efficiency",
                "CLAMPED 4.9 GHz (58W CEILING)",
                ColorAccentGold,
                new string[] {
                    "• P-Core Clamped 4.9 GHz",
                    "• E-Core Clamped 2.8 GHz",
                    "• 58W CPU Ceiling (Zero Starve)",
                    "• 100% Guaranteed 140W GPU",
                    "• Rock-solid frame pacing",
                    "• Ideal for heavy AAA titles"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileEfficiency.GamingTargetClicked += (s, e) => SelectProfile("clamped");
            cardProfileEfficiency.IdleTargetClicked += (s, e) => SelectDesktopProfile("powersaver");
            cardProfileEfficiency.ProfileClicked += (s, e) => SelectProfile("clamped");

            cardProfileCold = new ProfileCard(
                "❄ Cold & Quiet",
                "GPU-SHIFT 2100 MHz (74°C)",
                ColorAccentBlue,
                new string[] {
                    "• GPU Clamped: 2100 MHz (~100W)",
                    "• Boost Mode 3 (Efficient Enabled)",
                    "• EPP: 20% (Max Draw Latency)",
                    "• Sub-74°C Sustained GPU Temp",
                    "• Ultra-Quiet Acoustic Profile",
                    "• Perfect for stealth sessions"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileCold.GamingTargetClicked += (s, e) => SelectProfile("cold");
            cardProfileCold.IdleTargetClicked += (s, e) => SelectDesktopProfile("cold");
            cardProfileCold.ProfileClicked += (s, e) => SelectProfile("cold");

            cardProfileGuaranteed = new ProfileCard(
                "★ Efficient Guaranteed",
                "MODE 6 AT GUARANTEED (PEAK IPC)",
                ColorAccentPurple,
                new string[] {
                    "• Boost Mode 6 (Efficient Guaranteed)",
                    "• Unbounded Freq (5.5 GHz Peak)",
                    "• EPP 25% (Ultra-Responsive Bias)",
                    "• Full 140W RTX 5070 Headroom",
                    "• Sustained peak IPC headroom",
                    "• Benchmark validated profile"
                },
                "APPLY PROFILE",
                false
            );
            cardProfileGuaranteed.GamingTargetClicked += (s, e) => SelectProfile("guaranteed");
            cardProfileGuaranteed.IdleTargetClicked += (s, e) => SelectDesktopProfile("guaranteed");
            cardProfileGuaranteed.ProfileClicked += (s, e) => SelectProfile("guaranteed");

            panelProfilesView.Controls.Add(cardProfileSnappy);
            panelProfilesView.Controls.Add(cardProfileEfficiency);
            panelProfilesView.Controls.Add(cardProfileCold);
            panelProfilesView.Controls.Add(cardProfileGuaranteed);
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

