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
        private void InitializeInterface() {
            BuildTitleBar();
            BuildHeroAndMidHud();
            BuildTabStrip();
            BuildProfilesView();
            BuildViewsAndFooter();
        }

        private void BuildTitleBar() {
            // 1. TITLE BAR PANEL (Top 44px)
            // =========================================================================
            panelTitle = new Panel();
            panelTitle.Dock = DockStyle.Top;
            panelTitle.Height = 44;
            panelTitle.BackColor = Color.FromArgb(14, 15, 20);
            panelTitle.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) {
                    ReleaseCapture();
                    SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0);
                }
            };

            lblTitle = new Label();
            lblTitle.Text = "⚡ VECTOR POWER HUB";
            lblTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            lblTitle.ForeColor = ColorAccentCyan;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(14, 12);
            lblTitle.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); }
            };

            lblSubtitle = new Label();
            lblSubtitle.Text = "MSI VECTOR 16 HX • 275HX × RTX 5070 MOBILE";
            lblSubtitle.Font = new Font("Segoe UI", 8.25f, FontStyle.Bold);
            lblSubtitle.ForeColor = ColorTextDim;
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = new Point(230, 15);
            lblSubtitle.MouseDown += (s, e) => {
                if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(this.Handle, WM_NCLBUTTONDOWN, HTCAPTION, 0); }
            };

            lblProfileBadge = new Label();
            lblProfileBadge.Text = "ACTIVE: SNAPPY-PACING";
            lblProfileBadge.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            lblProfileBadge.ForeColor = ColorAccentGreen;
            lblProfileBadge.BackColor = Color.FromArgb(12, 38, 24);
            lblProfileBadge.Padding = new Padding(8, 3, 8, 3);
            lblProfileBadge.AutoSize = true;
            lblProfileBadge.Location = new Point(560, 11);

            btnTrayMin = CreateTitleButton("▼", 0, 8, (s, e) => MinimizeToTray());
            btnMin = CreateTitleButton("—", 0, 8, (s, e) => { this.WindowState = FormWindowState.Minimized; });
            btnMax = CreateTitleButton("◻", 0, 8, (s, e) => ToggleMaximize());
            btnClose = CreateTitleButton("✕", 0, 8, (s, e) => ExitApplication());
            btnClose.FlatAppearance.MouseOverBackColor = ColorAccentRed;

            panelTitle.Controls.Add(lblTitle);
            panelTitle.Controls.Add(lblSubtitle);
            panelTitle.Controls.Add(lblProfileBadge);
            panelTitle.Controls.Add(btnTrayMin);
            panelTitle.Controls.Add(btnMin);
            panelTitle.Controls.Add(btnMax);
            panelTitle.Controls.Add(btnClose);
            this.Controls.Add(panelTitle);


        }
    }
}
