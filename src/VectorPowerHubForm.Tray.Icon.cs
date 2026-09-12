using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        // -----------------------------------------------------------------------------------------
        // SYSTEM TRAY ICON & BALLOON NOTIFICATIONS
        // -----------------------------------------------------------------------------------------
        private Icon GenerateAppIcon() {
            Bitmap bmp = new Bitmap(32, 32);
            using (Graphics g = Graphics.FromImage(bmp)) {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (Brush b = new SolidBrush(ColorBgMain)) {
                    g.FillEllipse(b, 1, 1, 30, 30);
                }
                using (Pen p = new Pen(ColorAccentCyan, 2f)) {
                    g.DrawEllipse(p, 1, 1, 30, 30);
                }
                PointF[] bolt = new PointF[] {
                    new PointF(18, 5), new PointF(10, 16), new PointF(16, 16),
                    new PointF(13, 27), new PointF(23, 14), new PointF(17, 14)
                };
                using (Brush boltBrush = new SolidBrush(ColorAccentCyan)) {
                    g.FillPolygon(boltBrush, bolt);
                }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }

        private void ShowNotificationBalloon(string title, string text) {
            try {
                if (trayIcon != null) {
                    trayIcon.BalloonTipTitle = title;
                    trayIcon.BalloonTipText = text;
                    trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    trayIcon.ShowBalloonTip(2000);
                }
            } catch { }
        }
    }
}
