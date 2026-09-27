using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Linq;

namespace VectorPowerHub {
    public class VectorPowerHubGraphForm : Form {
        private GlowButton btnRecord;
        private GlowButton btnClear;
        private GraphCanvas canvas;

        public bool IsRecording { get; private set; }

        public VectorPowerHubGraphForm() {
            IsRecording = false;
            this.Text = "Telemetry Trends";
            this.Size = new Size(800, 500);
            this.BackColor = Color.FromArgb(10, 14, 20);
            try { this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            btnRecord = new GlowButton();
            btnRecord.Text = "● Record";
            btnRecord.Location = new Point(20, 20);
            btnRecord.Size = new Size(120, 36);
            btnRecord.ButtonColor = Color.FromArgb(36, 16, 16);
            btnRecord.BorderColor = Color.Red;
            btnRecord.TextColor = Color.Red;
            btnRecord.Click += (s, e) => {
                IsRecording = !IsRecording; btnRecord.IsPulsing = IsRecording;
                if (IsRecording) {
                    btnRecord.Text = "■ Stop";
                    btnRecord.ButtonColor = Color.FromArgb(28, 48, 36);
                    btnRecord.BorderColor = Color.LimeGreen;
                    btnRecord.TextColor = Color.LimeGreen;
                } else {
                    btnRecord.Text = "● Record";
                    btnRecord.ButtonColor = Color.FromArgb(36, 16, 16);
                    btnRecord.BorderColor = Color.Red;
                    btnRecord.TextColor = Color.Red;
                }
            };

            btnClear = new GlowButton();
            btnClear.Text = "Clear Data";
            btnClear.Location = new Point(150, 20);
            btnClear.Size = new Size(120, 36);
            btnClear.ButtonColor = Color.FromArgb(22, 28, 36);
            btnClear.BorderColor = Color.FromArgb(60, 70, 80);
            btnClear.TextColor = Color.LightGray;
            btnClear.Click += (s, e) => canvas.Clear();

            canvas = new GraphCanvas();
            canvas.Location = new Point(20, 70);
            canvas.Size = new Size(740, 380);
            canvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            this.Controls.Add(btnRecord);
            this.Controls.Add(btnClear);
            this.Controls.Add(canvas);
        }

        public void AddSnapshot(HubTelemetrySnapshot snap) {
            if (!IsRecording) return;
            canvas.AddDataPoint((float)snap.CpuPowerW, (float)snap.GpuPowerW, (float)snap.Fps);
        }
    }

    public class GraphCanvas : Control {
        private List<float> cpuData = new List<float>();
        private List<float> gpuData = new List<float>();
        private List<float> fpsData = new List<float>();
        private int maxPoints = 150; // Increased density

        public GraphCanvas() {
            this.DoubleBuffered = true;
            this.BackColor = Color.FromArgb(14, 18, 24);
        }

        public void AddDataPoint(float cpu, float gpu, float fps) {
            cpuData.Add(cpu);
            gpuData.Add(gpu);
            fpsData.Add(fps);

            if (cpuData.Count > maxPoints) {
                cpuData.RemoveAt(0);
                gpuData.RemoveAt(0);
                fpsData.RemoveAt(0);
            }
            this.Invalidate();
        }

        public void Clear() {
            cpuData.Clear();
            gpuData.Clear();
            fpsData.Clear();
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int w = this.Width;
            int h = this.Height;
            int laneH = h / 3;

            // Draw grid
            using (Pen gridPen = new Pen(Color.FromArgb(20, 25, 30), 1)) {
                for (int i = 0; i < 3; i++) g.DrawLine(gridPen, 0, i * laneH, w, i * laneH);
                for (int i = 0; i < 10; i++) g.DrawLine(gridPen, i * (w / 10), 0, i * (w / 10), h);
            }

            if (cpuData.Count < 2) return;
            float stepX = (float)w / (maxPoints - 1);

            DrawLane(g, cpuData, Color.Cyan, 0, laneH, w, stepX, "CPU POWER (W)", 200f);
            DrawLane(g, gpuData, Color.LimeGreen, laneH, laneH, w, stepX, "GPU POWER (W)", 250f);
            DrawLane(g, fpsData, Color.Gold, laneH * 2, laneH, w, stepX, "FPS", 300f);
        }

        private void DrawLane(Graphics g, List<float> data, Color baseColor, int startY, int h, int w, float stepX, string title, float defaultMax) {
            float maxVal = defaultMax;
            if (data.Count > 0) maxVal = Math.Max(defaultMax, data.Max() * 1.1f);
            if (maxVal < 1f) maxVal = 1f;

            List<PointF> pts = new List<PointF>();
            float startX = w - (data.Count - 1) * stepX;
            
            pts.Add(new PointF(startX, startY + h));
            for (int i = 0; i < data.Count; i++) {
                float x = startX + (i * stepX);
                float y = startY + h - ((data[i] / maxVal) * h);
                pts.Add(new PointF(x, y));
            }
            pts.Add(new PointF(startX + (data.Count - 1) * stepX, startY + h));

            using (GraphicsPath path = new GraphicsPath()) {
                path.AddLines(pts.ToArray());
                using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, startY, w, h), Color.FromArgb(60, baseColor), Color.FromArgb(0, baseColor), LinearGradientMode.Vertical)) {
                    g.FillPath(brush, path);
                }
            }
            using (Pen p = new Pen(baseColor, 1.5f)) {
                List<PointF> linePts = new List<PointF>();
                for (int i = 0; i < data.Count; i++) {
                    linePts.Add(new PointF(startX + (i * stepX), startY + h - ((data[i] / maxVal) * h)));
                }
                g.DrawLines(p, linePts.ToArray());
            }

            using (Font fTitle = new Font("Segoe UI", 9f, FontStyle.Bold))
            using (Font fVal = new Font("Consolas", 8f))
            using (SolidBrush bTxt = new SolidBrush(Color.FromArgb(180, 255, 255, 255))) {
                g.DrawString(title, fTitle, bTxt, 10, startY + 5);
                g.DrawString(((int)maxVal).ToString(), fVal, bTxt, 10, startY + 22);
            }
        }
    }
}
