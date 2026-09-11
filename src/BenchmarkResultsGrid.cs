using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace VectorPowerHub {
    public class BenchmarkResultsGrid : Control {
        private List<BenchmarkResultInfo> results = new List<BenchmarkResultInfo>();

        public BenchmarkResultsGrid() {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = VectorPowerHubForm.ColorBgMain;
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) {
            // Suppress default background erase
        }

        public void ClearResults() {
            results.Clear();
            this.Invalidate();
        }

        public void AddOrUpdateResult(BenchmarkResultInfo info) {
            for (int i = 0; i < results.Count; i++) {
                if (results[i].ProfileId == info.ProfileId) {
                    results[i] = info;
                    this.Invalidate();
                    return;
                }
            }
            results.Add(info);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            int w = this.Width;
            int h = this.Height;
            Rectangle rect = new Rectangle(0, 0, w - 1, h - 1);

            using (Brush b = new SolidBrush(VectorPowerHubForm.ColorCardBg)) {
                g.FillRectangle(b, rect);
            }
            using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                g.DrawRectangle(p, rect);
            }

            // Table Header Row (Height: 26px)
            using (Brush b = new SolidBrush(Color.FromArgb(24, 28, 38))) {
                g.FillRectangle(b, 1, 1, w - 2, 26);
            }
            using (Pen p = new Pen(VectorPowerHubForm.ColorBorder, 1f)) {
                g.DrawLine(p, 1, 27, w - 2, 27);
            }

            // Proportional column coordinates with safe clearances
            int colRank = 12;
            int colName = 60;
            int colCleanFps = Math.Max(380, (int)(w * 0.35));
            int rem = w - colCleanFps;
            int colRawFps = colCleanFps + (int)(rem * 0.150);
            int colCleanLow = colCleanFps + (int)(rem * 0.280);
            int colRawLow = colCleanFps + (int)(rem * 0.410);
            int colCpuW = colCleanFps + (int)(rem * 0.535);
            int colGpuW = colCleanFps + (int)(rem * 0.640);
            int colOutliers = colCleanFps + (int)(rem * 0.745);
            int colEff = colCleanFps + (int)(rem * 0.865);

            string[] headers = new string[] { "RANK", "PROFILE NAME", "CLEAN AVG", "RAW AVG", "CLEAN 1%", "RAW 1%", "CPU W", "GPU W", "OUTLIERS", "EFFICIENCY" };
            int[] colPositions = new int[] { colRank, colName, colCleanFps, colRawFps, colCleanLow, colRawLow, colCpuW, colGpuW, colOutliers, colEff };

            using (Font fHead = new Font("Segoe UI", 8f, FontStyle.Bold)) {
                using (Brush bHead = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                    for (int i = 0; i < headers.Length; i++) {
                        g.DrawString(headers[i], fHead, bHead, colPositions[i], 6);
                    }
                }
            }

            // Rows
            if (results.Count == 0) {
                using (Font fEmpty = new Font("Segoe UI", 8.5f, FontStyle.Italic)) {
                    using (Brush bEmpty = new SolidBrush(VectorPowerHubForm.ColorTextDim)) {
                        g.DrawString("No benchmark runs recorded yet. Click 'Run Profile Benchmark' above to execute.", fEmpty, bEmpty, 20, 50);
                    }
                }
                return;
            }

            int rowY = 32;
            int rowHeight = 24;
            using (Font fRow = new Font("Segoe UI", 8f, FontStyle.Regular))
            using (Font fDigits = new Font("Consolas", 8.5f, FontStyle.Regular))
            using (Font fBoldDigits = new Font("Consolas", 8.5f, FontStyle.Bold))
            using (Font fBold = new Font("Segoe UI", 8f, FontStyle.Bold)) {
                for (int i = 0; i < results.Count; i++) {
                    BenchmarkResultInfo r = results[i];

                    // Row background highlight if winner
                    if (r.IsWinner) {
                        using (Brush bWin = new SolidBrush(Color.FromArgb(38, 30, 12))) {
                            g.FillRectangle(bWin, 2, rowY - 2, w - 4, rowHeight);
                        }
                        using (Pen pWin = new Pen(VectorPowerHubForm.ColorAccentGold, 1f)) {
                            g.DrawRectangle(pWin, 2, rowY - 2, w - 4, rowHeight);
                        }
                    }

                    // Rank
                    string rankStr = string.Format("#{0}", i + 1);
                    Color rankColor = VectorPowerHubForm.ColorTextWhite;
                    if (r.IsWinner) {
                        rankStr = "★ 1st";
                        rankColor = VectorPowerHubForm.ColorAccentGold;
                    }
                    using (Brush b = new SolidBrush(rankColor)) {
                        g.DrawString(rankStr, fBold, b, colRank, rowY);
                    }

                    // Profile Name
                    using (Brush b = new SolidBrush(r.IsWinner ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorTextWhite)) {
                        g.DrawString(r.ProfileName, fBold, b, colName, rowY);
                    }

                    bool hasFps = r.CleanedAvgFps > 0.0;
                    bool hasRawFps = r.RawAvgFps > 0.0;

                    // Cleaned Avg FPS (Consolas Bold)
                    using (Brush b = new SolidBrush(hasFps ? VectorPowerHubForm.ColorAccentCyan : VectorPowerHubForm.ColorTextDim)) {
                        string cleanFpsStr = hasFps ? string.Format("{0:0.0} FPS", r.CleanedAvgFps) : "N/A (Idle)";
                        g.DrawString(cleanFpsStr, fBoldDigits, b, colCleanFps, rowY);
                    }

                    // Raw Avg FPS (Consolas Regular)
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                        string rawFpsStr = hasRawFps ? string.Format("{0:0.0} FPS", r.RawAvgFps) : "N/A";
                        g.DrawString(rawFpsStr, fDigits, b, colRawFps, rowY);
                    }

                    // Cleaned 1% Lows (Consolas Bold)
                    using (Brush b = new SolidBrush(hasFps ? VectorPowerHubForm.ColorAccentGold : VectorPowerHubForm.ColorTextDim)) {
                        string cleanLowStr = hasFps ? string.Format("{0:0.0} FPS", r.CleanedOnePercentLow) : "N/A";
                        g.DrawString(cleanLowStr, fBoldDigits, b, colCleanLow, rowY);
                    }

                    // Raw 1% Lows (Consolas Regular)
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextMuted)) {
                        string rawLowStr = hasRawFps ? string.Format("{0:0.0} FPS", r.RawOnePercentLow) : "N/A";
                        g.DrawString(rawLowStr, fDigits, b, colRawLow, rowY);
                    }

                    // CPU Watts
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                        g.DrawString(string.Format("{0:0.0} W", r.AvgCpuPowerW), fDigits, b, colCpuW, rowY);
                    }

                    // GPU Watts
                    using (Brush b = new SolidBrush(VectorPowerHubForm.ColorTextWhite)) {
                        g.DrawString(string.Format("{0:0.0} W", r.AvgGpuPowerW), fDigits, b, colGpuW, rowY);
                    }

                    // Outliers Filtered
                    Color outColor = (r.OutliersFilteredCount > 0) ? VectorPowerHubForm.ColorAccentRed : VectorPowerHubForm.ColorAccentGreen;
                    using (Brush b = new SolidBrush(outColor)) {
                        string outStr = (r.OutliersFilteredCount > 0) ? string.Format("{0} ({1})", r.OutliersFilteredCount, r.OutlierDetails) : "0 (Clean)";
                        g.DrawString(outStr, fRow, b, colOutliers, rowY);
                    }

                    // Efficiency Score
                    using (Brush b = new SolidBrush(hasFps ? VectorPowerHubForm.ColorAccentPurple : VectorPowerHubForm.ColorTextDim)) {
                        string effStr = hasFps ? string.Format("{0:0.00} FPS/W", r.EfficiencyScore) : "N/A";
                        g.DrawString(effStr, fDigits, b, colEff, rowY);
                    }

                    rowY += rowHeight;
                }
            }
        }
    }
}
