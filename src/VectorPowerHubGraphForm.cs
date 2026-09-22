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
        private CheckBox chkCpu;
        private CheckBox chkGpu;
        private CheckBox chkFps;
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
                IsRecording = !IsRecording;
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

            chkCpu = new CheckBox() { Text = "CPU Power (W)", ForeColor = Color.Cyan, Location = new Point(300, 28), AutoSize = true, Checked = true };
            chkGpu = new CheckBox() { Text = "GPU Power (W)", ForeColor = Color.LimeGreen, Location = new Point(430, 28), AutoSize = true, Checked = true };
            chkFps = new CheckBox() { Text = "FPS", ForeColor = Color.Gold, Location = new Point(560, 28), AutoSize = true, Checked = true };

            chkCpu.CheckedChanged += (s, e) => { canvas.ShowCpu = chkCpu.Checked; canvas.Invalidate(); };
            chkGpu.CheckedChanged += (s, e) => { canvas.ShowGpu = chkGpu.Checked; canvas.Invalidate(); };
            chkFps.CheckedChanged += (s, e) => { canvas.ShowFps = chkFps.Checked; canvas.Invalidate(); };

            canvas = new GraphCanvas();
            canvas.Location = new Point(20, 70);
            canvas.Size = new Size(740, 380);
            canvas.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            this.Controls.Add(btnRecord);
            this.Controls.Add(btnClear);
            this.Controls.Add(chkCpu);
            this.Controls.Add(chkGpu);
            this.Controls.Add(chkFps);
            this.Controls.Add(canvas);
        }

        public void AddSnapshot(HubTelemetrySnapshot snap) {
            if (!IsRecording) return;
            canvas.AddDataPoint((float)snap.CpuPowerW, (float)snap.GpuPowerW, (float)snap.Fps);
        }
    }

    public class GraphCanvas : Control {
        public bool ShowCpu = true;
        public bool ShowGpu = true;
        public bool ShowFps = true;

        private List<float> cpuData = new List<float>();
        private List<float> gpuData = new List<float>();
        private List<float> fpsData = new List<float>();
        private int maxPoints = 300;

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

            // Draw grid
            using (Pen gridPen = new Pen(Color.FromArgb(30, 40, 50), 1)) {
                for (int i = 0; i < 10; i++) {
                    int y = i * (h / 10);
                    g.DrawLine(gridPen, 0, y, w, y);
                }
                for (int i = 0; i < 10; i++) {
                    int x = i * (w / 10);
                    g.DrawLine(gridPen, x, 0, x, h);
                }
            }

            if (cpuData.Count < 2) return;

            // Find Max Value for scaling
            float maxVal = 100f;
            if (ShowCpu && cpuData.Count > 0) maxVal = Math.Max(maxVal, cpuData.Max());
            if (ShowGpu && gpuData.Count > 0) maxVal = Math.Max(maxVal, gpuData.Max());
            if (ShowFps && fpsData.Count > 0) maxVal = Math.Max(maxVal, fpsData.Max());
            maxVal = maxVal * 1.1f; // 10% headroom

            float stepX = (float)w / (maxPoints - 1);

            if (ShowCpu) DrawLine(g, cpuData, Color.Cyan, maxVal, stepX, h);
            if (ShowGpu) DrawLine(g, gpuData, Color.LimeGreen, maxVal, stepX, h);
            if (ShowFps) DrawLine(g, fpsData, Color.Gold, maxVal, stepX, h);

            // Draw Y-Axis labels
            using (Font f = new Font("Consolas", 8f))
            using (SolidBrush b = new SolidBrush(Color.LightGray)) {
                g.DrawString(((int)maxVal).ToString(), f, b, 5, 5);
                g.DrawString(((int)(maxVal / 2)).ToString(), f, b, 5, (h / 2) - 10);
                g.DrawString("0", f, b, 5, h - 20);
            }
        }

        private void DrawLine(Graphics g, List<float> data, Color color, float maxVal, float stepX, float h) {
            List<PointF> pts = new List<PointF>();
            // Align points to right
            float startX = this.Width - (data.Count - 1) * stepX;
            
            for (int i = 0; i < data.Count; i++) {
                float x = startX + (i * stepX);
                float y = h - ((data[i] / maxVal) * h);
                pts.Add(new PointF(x, y));
            }

            using (Pen p = new Pen(color, 2f)) {
                g.DrawLines(p, pts.ToArray());
            }
        }
    }
}
