using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace VectorPowerHub
{
    public class CustomSlider : Control
    {
        private int _minimum = 0;
        private int _maximum = 100;
        private int _value = 0;
        private bool _isDragging = false;
        private bool _isHovered = false;
        private float _hoverProgress = 0f;
        private Timer _hoverTimer;

        public event EventHandler ValueChanged;

        public int Minimum
        {
            get { return _minimum; }
            set { _minimum = value; Invalidate(); }
        }

        public int Maximum
        {
            get { return _maximum; }
            set { _maximum = value; Invalidate(); }
        }

        public int Value
        {
            get { return _value; }
            set 
            { 
                int clamped = Math.Max(_minimum, Math.Min(_maximum, value));
                if (_value != clamped)
                {
                    _value = clamped;
                    OnValueChanged(EventArgs.Empty);
                    Invalidate();
                }
            }
        }

        protected virtual void OnValueChanged(EventArgs e)
        {
            if (ValueChanged != null)
                ValueChanged(this, e);
        }

        public CustomSlider()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.ResizeRedraw | 
                     ControlStyles.UserPaint, true);
            
            _hoverTimer = new Timer();
            _hoverTimer.Interval = 15;
            _hoverTimer.Tick += HoverTimer_Tick;
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            bool changed = false;
            if (_isHovered && _hoverProgress < 1f)
            {
                _hoverProgress += 0.1f;
                if (_hoverProgress > 1f) _hoverProgress = 1f;
                changed = true;
            }
            else if (!_isHovered && _hoverProgress > 0f)
            {
                _hoverProgress -= 0.1f;
                if (_hoverProgress < 0f) _hoverProgress = 0f;
                changed = true;
            }

            if (changed) Invalidate();
            else _hoverTimer.Stop();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _isHovered = true;
            _hoverTimer.Start();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _isHovered = false;
            _hoverTimer.Start();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDragging)
            {
                UpdateValueFromMouse(e.X);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isDragging = false;
        }

        private void UpdateValueFromMouse(int x)
        {
            if (_maximum <= _minimum) return;
            
            int trackWidth = Width - 20;
            if (trackWidth <= 0) return;

            float percent = (float)(x - 10) / trackWidth;
            percent = Math.Max(0f, Math.Min(1f, percent));
            
            Value = (int)Math.Round(_minimum + percent * (_maximum - _minimum));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int trackHeight = 4;
            int trackY = (Height - trackHeight) / 2;
            int pad = 10;
            int trackWidth = Width - (pad * 2);

            using (SolidBrush bgBrush = new SolidBrush(Color.FromArgb(50, 50, 50)))
            {
                g.FillRectangle(bgBrush, pad, trackY, trackWidth, trackHeight);
            }

            if (_maximum <= _minimum) return;

            float percent = (float)(_value - _minimum) / (_maximum - _minimum);
            int fillWidth = (int)(trackWidth * percent);
            
            using (SolidBrush fillBrush = new SolidBrush(Color.FromArgb(0, 122, 204)))
            {
                g.FillRectangle(fillBrush, pad, trackY, fillWidth, trackHeight);
            }

            int thumbX = pad + fillWidth;
            int thumbY = Height / 2;
            
            float thumbBaseRadius = 6f;
            float thumbHoverRadius = 8f;
            float currentRadius = thumbBaseRadius + (_hoverProgress * (thumbHoverRadius - thumbBaseRadius));

            RectangleF thumbRect = new RectangleF(
                thumbX - currentRadius, 
                thumbY - currentRadius, 
                currentRadius * 2, 
                currentRadius * 2);

            using (SolidBrush thumbBrush = new SolidBrush(Color.FromArgb(200, 200, 200)))
            {
                if (_isHovered || _isDragging)
                    thumbBrush.Color = Color.White;
                    
                g.FillEllipse(thumbBrush, thumbRect);
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing && _hoverTimer != null)
            {
                _hoverTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
