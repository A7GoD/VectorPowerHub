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
        private void BuildHeroAndMidHud() {
            // 2. HERO HUD TOP SECTION (Self-Painting Controls, 126px)
            // =========================================================================
            cardFps = new FpsHeroCard();
            this.Controls.Add(cardFps);

            cardPlatformPower = new PlatformPowerCard();
            this.Controls.Add(cardPlatformPower);

            // =========================================================================
            // 3. TELEMETRY MID CARDS (CPU & GPU Live Metrics, 142px)
            // =========================================================================
            cardCpu = new CpuTelemetryCard();
            cardCpu.BtnView24Cores.Click += (s, e) => SwitchTab(2);
            this.Controls.Add(cardCpu);

            cardGpu = new GpuTelemetryCard();
            this.Controls.Add(cardGpu);

            // =========================================================================

        }

        private void BuildTabStrip() {
            // 4. TAB NAVIGATION STRIP (Height: 38px)
            // =========================================================================
            panelTabStrip = new Panel();
            panelTabStrip.BackColor = ColorBgMain;
            panelTabStrip.Height = 38;

            btnTabProfiles = new GlowButton();
            btnTabProfiles.Text = "[⚡] PROFILES";
            btnTabProfiles.Location = new Point(0, 2);
            btnTabProfiles.Size = new Size(160, 36);
            btnTabProfiles.ButtonColor = ColorTabActiveBg;
            btnTabProfiles.BorderColor = ColorAccentCyan;
            btnTabProfiles.TextColor = ColorAccentCyan;
            btnTabProfiles.Click += (s, e) => SwitchTab(0);

            btnTabBenchmark = new GlowButton();
            btnTabBenchmark.Text = "[★] BENCHMARK";
            btnTabBenchmark.Location = new Point(170, 2);
            btnTabBenchmark.Size = new Size(170, 36);
            btnTabBenchmark.ButtonColor = ColorTabInactiveBg;
            btnTabBenchmark.BorderColor = ColorBorder;
            btnTabBenchmark.TextColor = ColorTextMuted;
            btnTabBenchmark.Click += (s, e) => SwitchTab(1);

            btnTabTopology = new GlowButton();
            btnTabTopology.Text = "[◆] 24-CORE TOPOLOGY";
            btnTabTopology.Location = new Point(350, 2);
            btnTabTopology.Size = new Size(230, 36);
            btnTabTopology.ButtonColor = ColorTabInactiveBg;
            btnTabTopology.BorderColor = ColorBorder;
            btnTabTopology.TextColor = ColorTextMuted;
            btnTabTopology.Click += (s, e) => SwitchTab(2);

            btnToggleAutoSwitch = new GlowButton();
            btnToggleAutoSwitch.Text = "⚡ AUTO-PROFILES: ON (GAME SYNC)";
            btnToggleAutoSwitch.Size = new Size(270, 36);
            btnToggleAutoSwitch.ButtonColor = Color.FromArgb(14, 38, 26);
            btnToggleAutoSwitch.BorderColor = ColorAccentGreen;
            btnToggleAutoSwitch.TextColor = ColorAccentGreen;
            btnToggleAutoSwitch.Click += (s, e) => ToggleAutoProfileSwitching();

            panelTabStrip.Controls.Add(btnTabProfiles);
            panelTabStrip.Controls.Add(btnTabBenchmark);
            panelTabStrip.Controls.Add(btnTabTopology);
            panelTabStrip.Controls.Add(btnToggleAutoSwitch);
            this.Controls.Add(panelTabStrip);

            // =========================================================================

        }
    }
}
