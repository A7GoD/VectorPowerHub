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
        // -----------------------------------------------------------------------------------------
        // RESPONSIVE LAYOUT ENGINE (Executes on Resize, Maximize, and DPI Scale)
        // -----------------------------------------------------------------------------------------
        private void PerformResponsiveLayout() {
            int w = this.ClientSize.Width;
            int h = this.ClientSize.Height;
            if (w < 800 || h < 550) return;

            // 1. Title Bar (H = 44)
            panelTitle.Width = w;
            lblSubtitle.Location = new Point(lblTitle.Right + 12, 15);
            btnClose.Location = new Point(w - 38, 8);
            btnMax.Location = new Point(w - 74, 8);
            btnMin.Location = new Point(w - 110, 8);
            btnTrayMin.Location = new Point(w - 146, 8);
            btnTrends.Location = new Point(w - 186, 8);
            lblProfileBadge.Location = new Point(w - 198 - lblProfileBadge.Width, 11);
            lblSubtitle.MaximumSize = new Size(Math.Max(50, lblProfileBadge.Left - lblTitle.Right - 20), 20);
            lblSubtitle.AutoEllipsis = true;

            // 2. Hero Section (Y = 52, H = 160)
            int heroTop = 52;
            int heroH = 160;
            int fpsW = Math.Max(360, (int)(w * 0.33));
            int pwrW = w - 32 - fpsW - 12;

            cardFps.Location = new Point(16, heroTop);
            cardFps.Size = new Size(fpsW, heroH);

            cardPlatformPower.Location = new Point(16 + fpsW + 12, heroTop);
            cardPlatformPower.Size = new Size(pwrW, heroH);

            // 3. Telemetry Mid Section (Y = 220, H = 170)
            int midTop = 220;
            int midH = 170;
            int midCardW = (w - 44) / 2;

            cardCpu.Location = new Point(16, midTop);
            cardCpu.Size = new Size(midCardW, midH);

            cardGpu.Location = new Point(16 + midCardW + 12, midTop);
            cardGpu.Size = new Size(midCardW, midH);

            // 4. Tab Navigation Strip (Y = 398, H = 38)
            int tabTop = 398;
            panelTabStrip.Location = new Point(16, tabTop);
            panelTabStrip.Size = new Size(w - 32, 38);
            if (btnToggleAutoSwitch != null) {
                int autoBtnW = Math.Min(350, Math.Max(200, (w - 32) - 890));
                btnToggleAutoSwitch.Size = new Size(autoBtnW, 36);
                btnToggleAutoSwitch.Location = new Point((w - 32) - autoBtnW, 1);
            }

            // 5. Views Area
            int viewTop = 440;
            int footerH = 52;
            int viewH = h - viewTop - footerH - 8;
            int viewW = w - 32;

            panelProfilesView.Location = new Point(16, viewTop);
            panelProfilesView.Size = new Size(viewW, viewH);

            panelBenchmarkView.Location = new Point(16, viewTop);
            panelBenchmarkView.Size = new Size(viewW, viewH);

            panelTopologyView.Location = new Point(16, viewTop);
            panelTopologyView.Size = new Size(viewW, viewH);

            // Inside Profiles View (4 cards)
            int pCardW = (viewW - 36) / 4;
            cardProfileSnappy.Location = new Point(0, 0);
            cardProfileSnappy.Size = new Size(pCardW, viewH);

            cardProfileEfficiency.Location = new Point(pCardW + 12, 0);
            cardProfileEfficiency.Size = new Size(pCardW, viewH);

            cardProfileCold.Location = new Point((pCardW + 12) * 2, 0);
            cardProfileCold.Size = new Size(pCardW, viewH);

            cardProfileGuaranteed.Location = new Point((pCardW + 12) * 3, 0);
            cardProfileGuaranteed.Size = new Size(pCardW, viewH);

            // Inside Benchmark View
            benchProgressBar.Width = viewW - 28;
            benchResultsGrid.Width = viewW - 28;
            benchResultsGrid.Height = Math.Max(120, viewH - 172);

            // Inside Topology View
            btnToggleTopologyTuning.Width = viewW;
            if (panelTopologyTuningDrawer != null && panelTopologyTuningDrawer.Visible) {
                panelTopologyTuningDrawer.Width = viewW;
                topologyControl.Location = new Point(0, 196);
                topologyControl.Size = new Size(viewW, Math.Max(160, viewH - 196));
            } else if (topologyControl != null) {
                topologyControl.Location = new Point(0, 36);
                topologyControl.Size = new Size(viewW, Math.Max(220, viewH - 36));
            }

            // Inside Custom Tuner Overlay
            if (panelCustomTuner != null) {
                int tunerPanelW = Math.Min(920, w - 48);
                int tunerPanelH = 500;
                panelCustomTuner.Size = new Size(tunerPanelW, tunerPanelH);
                panelCustomTuner.Location = new Point(Math.Max(10, (w - tunerPanelW) / 2), Math.Max(20, (h - tunerPanelH) / 2));
            }

            // 6. Footer Panel
            int exitW = 80;
            int trayW = 90;
            int tunerW = 180;
            int gap = 8;
            btnFooterExit.Size = new Size(exitW, 34);
            btnFooterTray.Size = new Size(trayW, 34);
            btnOpenTuner.Size = new Size(tunerW, 34);
            btnFooterExit.Location = new Point(w - exitW - 14, 9);
            btnFooterTray.Location = new Point(btnFooterExit.Left - trayW - gap, 9);
            btnOpenTuner.Location = new Point(btnFooterTray.Left - tunerW - gap, 9);
            lblFooterStatus.MaximumSize = new Size(Math.Max(200, btnOpenTuner.Left - 20), 30);
        }

        private void ToggleMaximize() {
            if (this.WindowState == FormWindowState.Maximized) {
                this.WindowState = FormWindowState.Normal;
                btnMax.Text = "◻";
            } else {
                this.WindowState = FormWindowState.Maximized;
                btnMax.Text = "❐";
            }
            PerformResponsiveLayout();
        }

        private void SwitchTab(int tabIndex) {
            currentTabIndex = tabIndex;
            btnTabProfiles.ButtonColor = (tabIndex == 0) ? ColorTabActiveBg : ColorTabInactiveBg;
            btnTabProfiles.BorderColor = (tabIndex == 0) ? ColorAccentCyan : ColorBorder;
            btnTabProfiles.TextColor = (tabIndex == 0) ? ColorAccentCyan : ColorTextMuted;

            btnTabBenchmark.ButtonColor = (tabIndex == 1) ? Color.FromArgb(32, 24, 48) : ColorTabInactiveBg;
            btnTabBenchmark.BorderColor = (tabIndex == 1) ? ColorAccentPurple : ColorBorder;
            btnTabBenchmark.TextColor = (tabIndex == 1) ? ColorAccentPurple : ColorTextMuted;

            btnTabTopology.ButtonColor = (tabIndex == 2) ? Color.FromArgb(36, 32, 20) : ColorTabInactiveBg;
            btnTabTopology.BorderColor = (tabIndex == 2) ? ColorAccentGold : ColorBorder;
            btnTabTopology.TextColor = (tabIndex == 2) ? ColorAccentGold : ColorTextMuted;

            panelProfilesView.Visible = (tabIndex == 0);
            panelBenchmarkView.Visible = (tabIndex == 1);
            panelTopologyView.Visible = (tabIndex == 2);

            btnOpenTuner.Visible = (tabIndex == 0);
            if (panelCustomTuner.Visible && tabIndex != 0) ToggleCustomTuner();

            if (tabIndex == 2) {
                topologyControl.Invalidate();
            }
            try {
                PowerCoreEngine.SaveGuiSettingsOnly(startMinimizedToTray, tabIndex);
            } catch { }
        }


    }
}

