using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.Tools.Clock
{
    /// <summary>
    /// Premium vector analog clock control with high-precision hands, 
    /// sweeping accent second hand, tick marks, and customizable dial styling.
    /// </summary>
    public sealed class AnalogClockControl : Grid
    {
        private readonly Canvas _canvas;
        private readonly Line _hourHand;
        private readonly Line _minuteHand;
        private readonly Line _secondHand;
        private readonly Line _secondTail;
        private readonly Ellipse _centerDot;
        private readonly RotateTransform _hourRotate;
        private readonly RotateTransform _minuteRotate;
        private readonly RotateTransform _secondRotate;

        private double _size;
        private bool _showSeconds = true;

        public bool ShowSeconds
        {
            get => _showSeconds;
            set
            {
                _showSeconds = value;
                _secondHand.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                _secondTail.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public AnalogClockControl(double size = 130.0, bool showSeconds = true)
        {
            _size = size;
            _showSeconds = showSeconds;

            Width = size;
            Height = size;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            ClipToBounds = true;

            _canvas = new Canvas
            {
                Width = size,
                Height = size,
                ClipToBounds = true
            };
            Children.Add(_canvas);

            double r = size / 2.0;

            var p = CapsuleTheme.Current;
            // Outer subtle bezel
            var bezel = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = CapsuleTheme.Brush(p.BorderStrong),
                StrokeThickness = Math.Max(1.0, size * 0.015),
                Fill = p.IsLight
                    ? new RadialGradientBrush
                    {
                        GradientOrigin = new Point(0.5, 0.45),
                        Center = new Point(0.5, 0.5),
                        RadiusX = 0.55,
                        RadiusY = 0.55,
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromRgb(255, 255, 255), 0.0),
                            new GradientStop(Color.FromRgb(245, 246, 250), 0.85),
                            new GradientStop(Color.FromRgb(235, 237, 242), 1.0)
                        }
                    }
                    : new RadialGradientBrush
                    {
                        GradientOrigin = new Point(0.5, 0.45),
                        Center = new Point(0.5, 0.5),
                        RadiusX = 0.55,
                        RadiusY = 0.55,
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromRgb(28, 28, 36), 0.0),
                            new GradientStop(Color.FromRgb(16, 16, 22), 0.85),
                            new GradientStop(Color.FromRgb(12, 12, 16), 1.0)
                        }
                    }
            };
            Canvas.SetLeft(bezel, 0);
            Canvas.SetTop(bezel, 0);
            _canvas.Children.Add(bezel);

            // Dial tick marks (12 hour ticks and 60 minute ticks)
            for (int i = 0; i < 60; i++)
            {
                bool isHour = i % 5 == 0;
                double angleRad = (i * 6.0) * Math.PI / 180.0;

                double outerR = r - (size * 0.05);
                double innerR = isHour ? r - (size * 0.13) : r - (size * 0.08);

                double x1 = r + Math.Sin(angleRad) * innerR;
                double y1 = r - Math.Cos(angleRad) * innerR;
                double x2 = r + Math.Sin(angleRad) * outerR;
                double y2 = r - Math.Cos(angleRad) * outerR;

                var tick = new Line
                {
                    X1 = x1,
                    Y1 = y1,
                    X2 = x2,
                    Y2 = y2,
                    Stroke = isHour
                        ? CapsuleTheme.Brush(p.Text)
                        : CapsuleTheme.Brush(p.Text3),
                    StrokeThickness = isHour ? Math.Max(1.5, size * 0.018) : Math.Max(0.8, size * 0.009),
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                _canvas.Children.Add(tick);
            }

            // Numerals for 12, 3, 6, 9 if dial is large enough
            if (size >= 80.0)
            {
                AddNumeral("12", r, size * 0.15, size * 0.095);
                AddNumeral("3", size * 0.84, r - size * 0.05, size * 0.095);
                AddNumeral("6", r, size * 0.74, size * 0.095);
                AddNumeral("9", size * 0.16, r - size * 0.05, size * 0.095);
            }

            // Hour Hand
            double hourLength = r * 0.52;
            _hourHand = new Line
            {
                X1 = r,
                Y1 = r,
                X2 = r,
                Y2 = r - hourLength,
                Stroke = CapsuleTheme.Brush(p.Text),
                StrokeThickness = Math.Max(2.5, size * 0.038),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _hourRotate = new RotateTransform(0, r, r);
            _hourHand.RenderTransform = _hourRotate;
            _canvas.Children.Add(_hourHand);

            // Minute Hand
            double minLength = r * 0.74;
            _minuteHand = new Line
            {
                X1 = r,
                Y1 = r,
                X2 = r,
                Y2 = r - minLength,
                Stroke = CapsuleTheme.Brush(p.Text),
                StrokeThickness = Math.Max(1.8, size * 0.024),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _minuteRotate = new RotateTransform(0, r, r);
            _minuteHand.RenderTransform = _minuteRotate;
            _canvas.Children.Add(_minuteHand);

            // Second Hand (Accent color with counterweight tail)
            double secLength = r * 0.82;
            double tailLength = r * 0.22;
            var secBrush = CapsuleTheme.Brush(p.Accent);

            _secondHand = new Line
            {
                X1 = r,
                Y1 = r,
                X2 = r,
                Y2 = r - secLength,
                Stroke = secBrush,
                StrokeThickness = Math.Max(1.0, size * 0.014),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = _showSeconds ? Visibility.Visible : Visibility.Collapsed
            };
            _secondTail = new Line
            {
                X1 = r,
                Y1 = r,
                X2 = r,
                Y2 = r + tailLength,
                Stroke = secBrush,
                StrokeThickness = Math.Max(1.5, size * 0.02),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = _showSeconds ? Visibility.Visible : Visibility.Collapsed
            };

            _secondRotate = new RotateTransform(0, r, r);
            _secondHand.RenderTransform = _secondRotate;
            _secondTail.RenderTransform = _secondRotate;

            _canvas.Children.Add(_secondTail);
            _canvas.Children.Add(_secondHand);

            // Center Brass/Accent Hub Dot
            double dotR = Math.Max(3.0, size * 0.045);
            _centerDot = new Ellipse
            {
                Width = dotR * 2.0,
                Height = dotR * 2.0,
                Fill = secBrush,
                Stroke = CapsuleTheme.Brush(p.Surface),
                StrokeThickness = 1.0
            };
            Canvas.SetLeft(_centerDot, r - dotR);
            Canvas.SetTop(_centerDot, r - dotR);
            _canvas.Children.Add(_centerDot);

            UpdateTime(DateTime.Now);
        }

        private void AddNumeral(string text, double x, double y, double fontSize)
        {
            var p = CapsuleTheme.Current;
            var tb = new TextBlock
            {
                Text = text,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                TextAlignment = TextAlignment.Center
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2.0);
            Canvas.SetTop(tb, y - tb.DesiredSize.Height / 2.0);
            _canvas.Children.Add(tb);
        }

        public void UpdateTime(DateTime dt)
        {
            double secAngle = dt.Second * 6.0;
            double minAngle = (dt.Minute + dt.Second / 60.0) * 6.0;
            double hourAngle = ((dt.Hour % 12) + dt.Minute / 60.0 + dt.Second / 3600.0) * 30.0;

            _hourRotate.Angle = hourAngle;
            _minuteRotate.Angle = minAngle;
            _secondRotate.Angle = secAngle;
        }
    }
}
