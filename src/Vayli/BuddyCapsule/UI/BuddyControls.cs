using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.BuddyCapsule.UI
{
    public class BuddyCard : Border
    {
        public BuddyCard()
        {
            ApplyTheme();
            CornerRadius = new CornerRadius(14);
            Padding = new Thickness(12, 10, 12, 10);
            CapsuleTheme.ThemeChanged += ApplyTheme;
        }

        public void ApplyTheme()
        {
            var p = CapsuleTheme.Current;
            Background = CapsuleTheme.Brush(p.Surface);
            BorderBrush = CapsuleTheme.Brush(p.Border);
            BorderThickness = new Thickness(1);
        }
    }

    public class BuddyPillButton : Border
    {
        private readonly TextBlock _label;
        private bool _isActive;
        public event Action Clicked;
        public string ToolId { get; set; }

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                ApplyVisualState(IsMouseOver);
            }
        }

        public BuddyPillButton(string iconGlyph, string text, string toolId = null)
        {
            ToolId = toolId;
            CornerRadius = new CornerRadius(13);
            Padding = new Thickness(10, 5, 10, 5);
            Margin = new Thickness(2, 0, 2, 0);
            Cursor = Cursors.Hand;

            var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (!string.IsNullOrEmpty(iconGlyph))
            {
                row.Children.Add(new TextBlock
                {
                    Text = iconGlyph,
                    FontSize = 12,
                    Margin = new Thickness(0, 0, string.IsNullOrEmpty(text) ? 0 : 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            if (!string.IsNullOrEmpty(text))
            {
                _label = new TextBlock
                {
                    Text = text,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(_label);
            }
            Child = row;

            ApplyVisualState(false);

            MouseEnter += (_, _) => ApplyVisualState(true);
            MouseLeave += (_, _) => ApplyVisualState(false);
            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Clicked?.Invoke();
            };

            CapsuleTheme.ThemeChanged += () => ApplyVisualState(IsMouseOver);
        }

        public void ApplyVisualState(bool hovered)
        {
            var p = CapsuleTheme.Current;
            if (_isActive)
            {
                Background = CapsuleTheme.Brush(p.AccentSoft);
                BorderBrush = CapsuleTheme.Brush(Color.FromArgb(120, p.Accent.R, p.Accent.G, p.Accent.B));
                BorderThickness = new Thickness(1);
                if (Child is StackPanel sp)
                {
                    foreach (var ch in sp.Children)
                        if (ch is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Accent);
                }
            }
            else
            {
                Background = CapsuleTheme.Brush(hovered ? p.ControlFillHover : Colors.Transparent);
                BorderBrush = CapsuleTheme.Brush(hovered ? p.Border : Colors.Transparent);
                BorderThickness = new Thickness(1);
                if (Child is StackPanel sp)
                {
                    foreach (var ch in sp.Children)
                        if (ch is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(hovered ? p.Text : p.Text2);
                }
            }
        }
    }

    /// <summary>
    /// CircularGauge for System Monitor (CPU %, RAM %, DISK %) matching Screenshot 1 and 5.
    /// </summary>
    public class CircularGauge : StackPanel
    {
        private readonly Canvas _canvas;
        private readonly Path _trackPath;
        private readonly Path _progressPath;
        private readonly TextBlock _valueText;
        private readonly TextBlock _labelText;
        private double _percent;
        private readonly double _radius;
        private readonly double _thickness;

        public CircularGauge(string label, double percent, double radius = 24, double thickness = 5.0)
        {
            _radius = radius;
            _thickness = thickness;
            _percent = Math.Clamp(percent, 0, 100);

            Orientation = Orientation.Vertical;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(6, 2, 6, 2);

            double size = radius * 2 + thickness + 4;

            _canvas = new Canvas
            {
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _trackPath = new Path
            {
                StrokeThickness = _thickness,
                Stroke = CapsuleTheme.Brush(Color.FromArgb(40, 255, 255, 255))
            };
            _canvas.Children.Add(_trackPath);

            _progressPath = new Path
            {
                StrokeThickness = _thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _canvas.Children.Add(_progressPath);

            _valueText = new TextBlock
            {
                Text = $"{_percent:0}%",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };

            var valueWrapper = new Grid
            {
                Width = size,
                Height = size
            };
            valueWrapper.Children.Add(_canvas);
            valueWrapper.Children.Add(_valueText);

            _labelText = new TextBlock
            {
                Text = label.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };

            Children.Add(valueWrapper);
            Children.Add(_labelText);

            UpdateThemeAndArcs();
            CapsuleTheme.ThemeChanged += UpdateThemeAndArcs;
        }

        public void SetValue(double percent)
        {
            _percent = Math.Clamp(percent, 0, 100);
            _valueText.Text = $"{_percent:0}%";
            UpdateThemeAndArcs();
        }

        private void UpdateThemeAndArcs()
        {
            var p = CapsuleTheme.Current;
            _valueText.Foreground = CapsuleTheme.Brush(p.Text);
            _labelText.Foreground = CapsuleTheme.Brush(p.Text2);

            _trackPath.Stroke = CapsuleTheme.Brush(p.IsLight ? Color.FromArgb(28, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255));

            // Highlight color: if percent > 50 or active, use accent color
            bool isHigh = _percent >= 50;
            _progressPath.Stroke = CapsuleTheme.Brush(isHigh ? p.Accent : (p.IsLight ? Color.FromRgb(80, 80, 90) : Color.FromRgb(200, 200, 205)));

            double cx = _canvas.Width / 2.0;
            double cy = _canvas.Height / 2.0;

            // Full circle track
            _trackPath.Data = new EllipseGeometry(new Point(cx, cy), _radius, _radius);

            // Arc for progress
            if (_percent <= 0.5)
            {
                _progressPath.Data = null;
                return;
            }

            if (_percent >= 99.5)
            {
                _progressPath.Data = new EllipseGeometry(new Point(cx, cy), _radius, _radius);
                return;
            }

            double angle = (_percent / 100.0) * 360.0;
            double startAngle = -90.0; // Start at 12 o'clock
            double endAngle = startAngle + angle;

            Point pStart = AngleToPoint(cx, cy, _radius, startAngle);
            Point pEnd = AngleToPoint(cx, cy, _radius, endAngle);

            bool isLargeArc = angle > 180.0;

            var geom = new PathGeometry();
            var fig = new PathFigure { StartPoint = pStart, IsFilled = false };
            fig.Segments.Add(new ArcSegment(pEnd, new Size(_radius, _radius), 0, isLargeArc, SweepDirection.Clockwise, true));
            geom.Figures.Add(fig);
            _progressPath.Data = geom;
        }

        private static Point AngleToPoint(double cx, double cy, double r, double angleDeg)
        {
            double rad = (Math.PI / 180.0) * angleDeg;
            return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
        }
    }

    /// <summary>
    /// WeeklyBarChart: 7 rounded vertical pill bars for Mon-Sun (Screenshot 1).
    /// </summary>
    public class WeeklyBarChart : StackPanel
    {
        private readonly Border[] _bars = new Border[7];
        private readonly int _activeDayIndex;

        public WeeklyBarChart(int activeDayIndex = 2) // Default Wednesday
        {
            _activeDayIndex = Math.Clamp(activeDayIndex, 0, 6);
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Bottom;
            HorizontalAlignment = HorizontalAlignment.Left;
            Height = 36;
            Margin = new Thickness(0, 6, 0, 0);

            // Mocked representative weekly proportions
            double[] heights = { 20, 26, 32, 16, 22, 10, 28 };

            for (int i = 0; i < 7; i++)
            {
                var bar = new Border
                {
                    Width = 11,
                    Height = heights[i],
                    CornerRadius = new CornerRadius(5.5),
                    Margin = new Thickness(1.8, 0, 1.8, 0),
                    VerticalAlignment = VerticalAlignment.Bottom
                };
                _bars[i] = bar;
                Children.Add(bar);
            }

            ApplyTheme();
            CapsuleTheme.ThemeChanged += ApplyTheme;
        }

        public void ApplyTheme()
        {
            var p = CapsuleTheme.Current;
            for (int i = 0; i < 7; i++)
            {
                if (i == _activeDayIndex)
                {
                    // Active day: filled with accent or bright white
                    _bars[i].Background = CapsuleTheme.Brush(p.Accent);
                }
                else
                {
                    // Inactive days: muted gray / surface3
                    _bars[i].Background = CapsuleTheme.Brush(p.IsLight ? Color.FromArgb(35, 0, 0, 0) : Color.FromArgb(40, 255, 255, 255));
                }
            }
        }
    }

    /// <summary>
    /// Horizontal Range Bar for Weather 5-day outlook (Screenshot 3).
    /// </summary>
    public class WeatherRangeBar : Grid
    {
        private readonly Border _track;
        private readonly Border _fill;
        private readonly Ellipse _currentDot;

        public WeatherRangeBar(double minTemp, double maxTemp, double currentTemp, bool showCurrentDot = false)
        {
            Width = 92;
            Height = 5;
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(6, 0, 6, 0);

            _track = new Border
            {
                Height = 4.5,
                CornerRadius = new CornerRadius(2.25)
            };
            Children.Add(_track);

            _fill = new Border
            {
                Height = 4.5,
                CornerRadius = new CornerRadius(2.25),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Children.Add(_fill);

            if (showCurrentDot)
            {
                _currentDot = new Ellipse
                {
                    Width = 6.5,
                    Height = 6.5,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new DropShadowEffect { BlurRadius = 3, ShadowDepth = 0, Opacity = 0.8, Color = Colors.Black }
                };
                Children.Add(_currentDot);
            }

            ApplyTheme();
            CapsuleTheme.ThemeChanged += ApplyTheme;
        }

        public void ApplyTheme()
        {
            var p = CapsuleTheme.Current;
            _track.Background = CapsuleTheme.Brush(p.IsLight ? Color.FromArgb(30, 0, 0, 0) : Color.FromArgb(35, 255, 255, 255));

            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(100, 210, 255), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 159, 10), 1.0));
            _fill.Background = grad;
        }
    }

    /// <summary>
    /// Smooth Hourly Temperature Curve for Weather tool (Screenshot 3).
    /// </summary>
    public class WeatherHourlyChart : Canvas
    {
        private readonly double[] _temperatures = { 18, 19, 20, 20, 20, 19 };
        private readonly string[] _hours = { "Now", "10:00", "11:00", "12:00", "13:00", "14:00" };
        private readonly string[] _glyphs = { "☀", "⛅", "⛅", "⛅", "⛅", "☁" };

        public WeatherHourlyChart()
        {
            Width = 240;
            Height = 115;
            ClipToBounds = true;
            Loaded += (_, _) => Draw();
            CapsuleTheme.ThemeChanged += Draw;
        }

        public void Draw()
        {
            Children.Clear();
            var p = CapsuleTheme.Current;

            double w = Width;
            double h = Height;
            int count = _temperatures.Length;
            double stepX = w / count;

            double minTemp = 17.0;
            double maxTemp = 21.0;
            double graphTop = 50;
            double graphHeight = 40;

            var points = new Point[count];
            for (int i = 0; i < count; i++)
            {
                double norm = (_temperatures[i] - minTemp) / (maxTemp - minTemp);
                double y = graphTop + (1.0 - norm) * graphHeight;
                double x = i * stepX + (stepX / 2.0);
                points[i] = new Point(x, y);

                // Hour Text
                var hourText = new TextBlock
                {
                    Text = _hours[i],
                    FontSize = 9.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    Width = stepX,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(hourText, i * stepX);
                Canvas.SetTop(hourText, 2);
                Children.Add(hourText);

                // Weather Icon
                var iconText = new TextBlock
                {
                    Text = _glyphs[i],
                    FontSize = 13,
                    Foreground = CapsuleTheme.Brush(p.IsLight ? p.Text : Colors.White),
                    Width = stepX,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(iconText, i * stepX);
                Canvas.SetTop(iconText, 18);
                Children.Add(iconText);

                // Temp Text
                var tempText = new TextBlock
                {
                    Text = $"{_temperatures[i]:0}°",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    Width = stepX,
                    TextAlignment = TextAlignment.Center
                };
                Canvas.SetLeft(tempText, i * stepX);
                Canvas.SetTop(tempText, 34);
                Children.Add(tempText);
            }

            // Draw smooth curve through points
            var curveGeom = new PathGeometry();
            var curveFig = new PathFigure { StartPoint = points[0], IsFilled = false };

            var areaGeom = new PathGeometry();
            var areaFig = new PathFigure { StartPoint = new Point(points[0].X, graphTop + graphHeight + 15), IsFilled = true };
            areaFig.Segments.Add(new LineSegment(points[0], true));

            for (int i = 0; i < count - 1; i++)
            {
                var ptPrev = points[i];
                var ptNext = points[i + 1];
                var cp1 = new Point(ptPrev.X + (ptNext.X - ptPrev.X) / 2.0, ptPrev.Y);
                var cp2 = new Point(ptPrev.X + (ptNext.X - ptPrev.X) / 2.0, ptNext.Y);

                curveFig.Segments.Add(new BezierSegment(cp1, cp2, ptNext, true));
                areaFig.Segments.Add(new BezierSegment(cp1, cp2, ptNext, true));
            }

            areaFig.Segments.Add(new LineSegment(new Point(points[count - 1].X, graphTop + graphHeight + 15), true));
            areaFig.IsClosed = true;

            areaGeom.Figures.Add(areaFig);
            curveGeom.Figures.Add(curveFig);

            // Shaded Area beneath line
            var areaBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(45, 255, 255, 255), 0.0));
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1.0));

            var areaPath = new Path { Data = areaGeom, Fill = areaBrush };
            Children.Add(areaPath);

            var curvePath = new Path
            {
                Data = curveGeom,
                Stroke = CapsuleTheme.Brush(p.IsLight ? Color.FromRgb(100, 100, 110) : Color.FromRgb(220, 220, 230)),
                StrokeThickness = 1.6
            };
            Children.Add(curvePath);

            // White dots at points
            for (int i = 0; i < count; i++)
            {
                var dot = new Ellipse
                {
                    Width = 4.5,
                    Height = 4.5,
                    Fill = Brushes.White,
                    Effect = new DropShadowEffect { BlurRadius = 2, ShadowDepth = 0, Opacity = 0.5, Color = Colors.Black }
                };
                Canvas.SetLeft(dot, points[i].X - 2.25);
                Canvas.SetTop(dot, points[i].Y - 2.25);
                Children.Add(dot);
            }
        }
    }

    public class StaticBeatBars : StackPanel
    {
        private readonly Rectangle[] _bars;
        private readonly DispatcherTimer _timer;
        private readonly Random _rng = new();
        private bool _isPlaying = true;

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                if (!value)
                {
                    foreach (var b in _bars) b.Height = 3;
                }
            }
        }

        public StaticBeatBars(int count = 4, double maxHeight = 14)
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Height = maxHeight;
            _bars = new Rectangle[count];

            var accent = CapsuleTheme.Brush(CapsuleTheme.Current.Accent);
            for (int i = 0; i < count; i++)
            {
                var r = new Rectangle
                {
                    Width = 2.6,
                    Height = 4 + (i % 3) * 3,
                    RadiusX = 1.3,
                    RadiusY = 1.3,
                    Fill = accent,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(1.1, 0, 1.1, 0)
                };
                _bars[i] = r;
                Children.Add(r);
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(160) };
            _timer.Tick += (_, _) =>
            {
                var currentAccent = CapsuleTheme.Brush(CapsuleTheme.Current.Accent);
                for (int i = 0; i < _bars.Length; i++)
                {
                    _bars[i].Fill = currentAccent;
                    if (_isPlaying)
                    {
                        _bars[i].Height = 3 + _rng.NextDouble() * (maxHeight - 3);
                    }
                }
            };
            _timer.Start();
        }
    }

    public class EdgeLightWpf : Canvas
    {
        private readonly Path _glowPath;
        private readonly RotateTransform _gradientRotate;
        private readonly DispatcherTimer _animTimer;
        private double _angle;

        public EdgeLightWpf()
        {
            IsHitTestVisible = false;
            _gradientRotate = new RotateTransform(0, 0.5, 0.5);

            var brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
                RelativeTransform = _gradientRotate
            };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(210, 48, 209, 88), 0.0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(140, 10, 132, 255), 0.35));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(40, 191, 90, 242), 0.7));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(210, 48, 209, 88), 1.0));

            _glowPath = new Path
            {
                Stroke = brush,
                StrokeThickness = 1.6,
                Opacity = 0.85,
                Effect = new BlurEffect { Radius = 4, KernelType = KernelType.Gaussian }
            };
            Children.Add(_glowPath);

            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _animTimer.Tick += (_, _) =>
            {
                _angle = (_angle + 3.0) % 360.0;
                _gradientRotate.Angle = _angle;
            };
            _animTimer.Start();
        }

        public void UpdateGeometry(Geometry geometry, bool isExpanded)
        {
            _glowPath.Data = geometry;
            _glowPath.StrokeThickness = isExpanded ? 1.8 : 1.25;
            _glowPath.Opacity = isExpanded ? 0.92 : 0.65;
        }
    }

    /// <summary>
    /// MiniEqualizerControl: The exact "I · II" animated audio visualizer icon from the music notch screenshot.
    /// </summary>
    public class MiniEqualizerControl : StackPanel
    {
        private readonly Border _bar1;
        private readonly Border _dot;
        private readonly Border _bar2;
        private readonly Border _bar3;
        private readonly DispatcherTimer _timer;
        private readonly Random _rng = new();
        private bool _isPlaying = true;

        public bool IsPlaying
        {
            get => _isPlaying;
            set
            {
                _isPlaying = value;
                if (!value)
                {
                    _bar1.Height = 5;
                    _bar2.Height = 5;
                    _bar3.Height = 5;
                }
            }
        }

        public MiniEqualizerControl()
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Height = 14;
            Margin = new Thickness(6, 0, 0, 0);

            _bar1 = CreatePill(2.2, 9);
            _dot = CreatePill(2.0, 2.5);
            _bar2 = CreatePill(2.2, 11);
            _bar3 = CreatePill(2.2, 8);

            Children.Add(_bar1);
            Children.Add(_dot);
            Children.Add(_bar2);
            Children.Add(_bar3);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(130) };
            _timer.Tick += (_, _) =>
            {
                if (!_isPlaying) return;
                _bar1.Height = 4 + _rng.NextDouble() * 7;
                _bar2.Height = 5 + _rng.NextDouble() * 7;
                _bar3.Height = 3 + _rng.NextDouble() * 8;
            };
            _timer.Start();

            CapsuleTheme.ThemeChanged += ApplyTheme;
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var brush = CapsuleTheme.Brush(Color.FromRgb(250, 250, 252));
            _bar1.Background = brush;
            _dot.Background = brush;
            _bar2.Background = brush;
            _bar3.Background = brush;
        }

        private static Border CreatePill(double width, double height)
        {
            return new Border
            {
                Width = width,
                Height = height,
                CornerRadius = new CornerRadius(width / 2.0),
                Background = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1.2, 0, 1.2, 0)
            };
        }
    }

    /// <summary>
    /// WaveformProgressBar: Full-width dense horizontal audio waveform scrobbler matching the user screenshot.
    /// </summary>
    public class WaveformProgressBar : StackPanel
    {
        private const int BarCount = 42;
        private readonly Border[] _bars = new Border[BarCount];
        private readonly double[] _baseHeights = new double[BarCount];
        private readonly DispatcherTimer _animTimer;
        private readonly Random _rng = new();
        private double _progress = 0.38; // 0.0 to 1.0
        private bool _isPlaying = true;

        public WaveformProgressBar()
        {
            Orientation = Orientation.Horizontal;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            Height = 12;
            Margin = new Thickness(0, 2, 0, 1);

            // Shape silhouette matching the screenshot:
            // Left: medium/tall bars (6-9px), Center: dips to tiny dots (2.5px), Right: rises to tall wave (7-10px)
            for (int i = 0; i < BarCount; i++)
            {
                double x = (double)i / BarCount;
                double h;

                if (x < 0.40)
                {
                    // Left wave section
                    h = 5.0 + 3.5 * Math.Sin(x * Math.PI * 4.0) + (i % 2) * 1.5;
                }
                else if (x >= 0.40 && x <= 0.62)
                {
                    // Middle quiet dot section
                    h = 2.4;
                }
                else
                {
                    // Right wave section
                    h = 5.5 + 4.0 * Math.Sin((x - 0.6) * Math.PI * 3.5) + (i % 2) * 1.5;
                }

                h = Math.Clamp(Math.Round(h, 1), 2.2, 10.5);
                _baseHeights[i] = h;

                var b = new Border
                {
                    Width = 2.4,
                    Height = h,
                    CornerRadius = new CornerRadius(1.2),
                    Margin = new Thickness(2.1, 0, 2.1, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                _bars[i] = b;
                Children.Add(b);
            }

            UpdateVisuals();

            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(110) };
            _animTimer.Tick += (_, _) =>
            {
                if (!_isPlaying) return;
                int head = (int)(_progress * BarCount);
                for (int i = 0; i < BarCount; i++)
                {
                    if (Math.Abs(i - head) <= 4 || (i < head && i > 0))
                    {
                        double delta = (_rng.NextDouble() - 0.5) * 2.2;
                        _bars[i].Height = Math.Clamp(_baseHeights[i] + delta, 2.2, 11.0);
                    }
                    else
                    {
                        _bars[i].Height = _baseHeights[i];
                    }
                }
            };
            _animTimer.Start();

            CapsuleTheme.ThemeChanged += UpdateVisuals;
        }

        public void SetPlayback(double progress, bool isPlaying)
        {
            _progress = Math.Clamp(progress, 0.0, 1.0);
            _isPlaying = isPlaying;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            int head = (int)(_progress * BarCount);
            for (int i = 0; i < BarCount; i++)
            {
                bool isPlayed = i <= head;
                _bars[i].Background = CapsuleTheme.Brush(isPlayed
                    ? Color.FromRgb(245, 245, 250)
                    : Color.FromArgb(75, 255, 255, 255));
            }
        }
    }

    /// <summary>
    /// AlbumArtThumbnail: Displays the artist/track artwork thumbnail matching the screenshot.
    /// </summary>
    public class AlbumArtThumbnail : Border
    {
        private readonly Image _imageControl;
        private readonly Border _fallbackArtwork;

        public AlbumArtThumbnail()
        {
            Width = 24;
            Height = 24;
            CornerRadius = new CornerRadius(5.5);
            ClipToBounds = true;
            Background = CapsuleTheme.Brush(Color.FromRgb(22, 23, 28));
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(0, 0, 8, 0);

            var grid = new Grid();

            var canvas = new Canvas { Width = 24, Height = 24 };

            // Shoulders & jacket
            var shoulders = new Path
            {
                Fill = new SolidColorBrush(Color.FromRgb(65, 68, 80)),
                Data = Geometry.Parse("M 1 24 C 3 14, 21 14, 23 24 Z")
            };
            canvas.Children.Add(shoulders);

            // Head & face silhouette
            var head = new Ellipse
            {
                Width = 8.5,
                Height = 10,
                Fill = new SolidColorBrush(Color.FromRgb(180, 182, 192))
            };
            Canvas.SetLeft(head, 7.75);
            Canvas.SetTop(head, 4.5);
            canvas.Children.Add(head);

            // Hair
            var hair = new Path
            {
                Fill = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                Data = Geometry.Parse("M 6.5 10 C 6.5 3, 17.5 3, 17.5 10 C 17.5 13, 15.5 12, 15.5 8 C 14.5 5, 9.5 5, 8.5 8 C 8.5 12, 6.5 13, 6.5 10 Z")
            };
            canvas.Children.Add(hair);

            _fallbackArtwork = new Border
            {
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(50, 52, 64), 0.0),
                        new GradientStop(Color.FromRgb(16, 17, 22), 1.0)
                    }
                },
                Child = canvas
            };
            grid.Children.Add(_fallbackArtwork);

            _imageControl = new Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };
            grid.Children.Add(_imageControl);

            Child = grid;
        }

        public void SetArtwork(ImageSource source, string artistName = null)
        {
            if (source != null)
            {
                _imageControl.Source = source;
                _imageControl.Visibility = Visibility.Visible;
                _fallbackArtwork.Visibility = Visibility.Collapsed;
            }
            else
            {
                _imageControl.Visibility = Visibility.Collapsed;
                _fallbackArtwork.Visibility = Visibility.Visible;
            }
        }
    }
}
