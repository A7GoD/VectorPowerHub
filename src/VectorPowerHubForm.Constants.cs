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
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int HWND_BROADCAST = 0xffff;
        private const int SW_RESTORE = 9;
        public static readonly int WM_SHOW_HUB = RegisterWindowMessage("VECTOR_POWER_HUB_SHOW_WINDOW");

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HTCAPTION = 0x2;
        private const int CS_DROPSHADOW = 0x00020000;

        protected override CreateParams CreateParams {
            get {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= CS_DROPSHADOW;
                return cp;
            }
        }

        // Color Palette (DESIGN.md Design Tokens)
        public static readonly Color ColorBgMain          = Color.FromArgb(18, 19, 24);    // #121318 Slate background
        public static readonly Color ColorCardBg          = Color.FromArgb(27, 30, 38);    // #1B1E26 Elevated card surface
        public static readonly Color ColorCardHover       = Color.FromArgb(35, 39, 50);    // #232732
        public static readonly Color ColorCardSelected    = Color.FromArgb(24, 34, 48);    // #182230
        public static readonly Color ColorBorder          = Color.FromArgb(45, 50, 66);    // #2D3242 1px card border
        public static readonly Color ColorBorderHighlight = Color.FromArgb(70, 80, 105);
        public static readonly Color ColorAccentCyan      = Color.FromArgb(0, 242, 255);   // #00F2FF Electric Cyan (FPS & GPU)
        public static readonly Color ColorAccentGold      = Color.FromArgb(255, 159, 0);   // #FF9F00 Amber Gold (CPU)
        public static readonly Color ColorAccentPurple    = Color.FromArgb(168, 85, 247);  // #A855F7 Royal Purple (Platform balance)
        public static readonly Color ColorAccentGreen     = Color.FromArgb(0, 230, 118);   // #00E676 Emerald Green (Active profile & D3cold)
        public static readonly Color ColorAccentRed       = Color.FromArgb(255, 82, 82);   // #FF5252 Coral Red (215W ceiling alert & outliers)
        public static readonly Color ColorAccentBlue      = Color.FromArgb(56, 189, 248);  // #38BDF8
        public static readonly Color ColorTextWhite       = Color.FromArgb(255, 255, 255); // #FFFFFF Primary text
        public static readonly Color ColorTextMuted       = Color.FromArgb(143, 156, 174); // #8F9CAE Secondary text
        public static readonly Color ColorTextDim         = Color.FromArgb(90, 101, 120);  // #5A6578 Muted/Dim text
        public static readonly Color ColorTabInactiveBg   = Color.FromArgb(22, 24, 32);    // #161820
        public static readonly Color ColorTabActiveBg     = Color.FromArgb(31, 35, 45);    // #1F232D

    }
}
