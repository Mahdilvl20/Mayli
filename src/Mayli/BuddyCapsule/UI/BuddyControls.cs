using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
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
        public string Text { get; set; }

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
            Text = text;
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
                if (!IsVisible) return;
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
            Loaded += (_, _) => { if (IsVisible) _timer.Start(); };
            Unloaded += (_, _) => _timer.Stop();
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible && IsLoaded) _timer.Start();
                else _timer.Stop();
            };
        }
    }

    /// <summary>
    /// EdgeLightWpf: Exact Twin-Comets U-outline back-and-forth ("رفت و آمدی") edge light animation
    /// matching Vayli's native EdgeLightWpf (<AddComet>b__19: StrokeDashOffset = (segFrac - p) * L / Th).
    /// Two symmetrical glowing white streaks oscillate between the side walls/corners and the bottom center.
    /// </summary>
    public class EdgeLightWpf : Grid
    {
        private static readonly double[][] CometLayers =
        {
            new[] { 0.012, 1.00 },
            new[] { 0.018, 0.20 },
            new[] { 0.036, 0.20 },
            new[] { 0.054, 0.20 },
            new[] { 0.072, 0.20 },
            new[] { 0.090, 0.20 },
            new[] { 0.108, 0.20 },
            new[] { 0.126, 0.20 },
            new[] { 0.144, 0.20 },
            new[] { 0.162, 0.20 },
            new[] { 0.180, 0.20 }
        };

        private struct CometStrokeEntry
        {
            public Path PathElement;
            public double SegFrac;
            public double Thickness;
            public bool IsGlow;
            public bool IsReverse;
            public double Alpha;
        }

        private readonly Grid _pulseContainer;
        private readonly Grid _glowLayer;
        private readonly Grid _coreLayer;
        private readonly Path _baseBorderPath;
        private readonly Path _breatheGlowPath;
        private readonly CometStrokeEntry[] _cometEntries;
        private readonly DispatcherTimer _animTimer;

        private double _lastW = -1;
        private double _lastH = -1;
        private string _lastStyle = "";
        private double _totalLength = 276.0;
        private double _cyclePhase; // 0.0 .. 1.0
        private Color _lightColor = Colors.White;
        private bool _isMorphing;
        private string _pattern = "Twin comets";
        private string _speed = "Normal";
        private string _triggerMode = "Always";
        private string _notchStyle = "Notch";
        private DateTime _burstUntilUtc = DateTime.MinValue;
        private double _burstOpacity = 1.0;

        public EdgeLightWpf()
        {
            IsHitTestVisible = false;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            _pulseContainer = new Grid();

            _glowLayer = new Grid
            {
                Opacity = 0.52,
                Effect = new BlurEffect { Radius = 5.0, KernelType = KernelType.Box, RenderingBias = RenderingBias.Performance }
            };
            _coreLayer = new Grid
            {
                Opacity = 0.96
            };

            _breatheGlowPath = new Path { StrokeThickness = 3.2, Visibility = Visibility.Collapsed };
            _glowLayer.Children.Add(_breatheGlowPath);

            _baseBorderPath = new Path { StrokeThickness = 1.0 };
            _coreLayer.Children.Add(_baseBorderPath);

            double tailScale = 0.15 / 0.18;
            _cometEntries = new CometStrokeEntry[CometLayers.Length * 4];
            int idx = 0;
            foreach (var layer in CometLayers)
            {
                double segFrac = layer[0] * tailScale;
                double alpha = layer[1];

                var gf = CreateReusableCometPath();
                var gr = CreateReusableCometPath();
                _glowLayer.Children.Add(gf);
                _glowLayer.Children.Add(gr);
                _cometEntries[idx++] = new CometStrokeEntry { PathElement = gf, SegFrac = segFrac, IsGlow = true, IsReverse = false, Alpha = alpha };
                _cometEntries[idx++] = new CometStrokeEntry { PathElement = gr, SegFrac = segFrac, IsGlow = true, IsReverse = true, Alpha = alpha };

                var cf = CreateReusableCometPath();
                var cr = CreateReusableCometPath();
                _coreLayer.Children.Add(cf);
                _coreLayer.Children.Add(cr);
                _cometEntries[idx++] = new CometStrokeEntry { PathElement = cf, SegFrac = segFrac, IsGlow = false, IsReverse = false, Alpha = alpha };
                _cometEntries[idx++] = new CometStrokeEntry { PathElement = cr, SegFrac = segFrac, IsGlow = false, IsReverse = true, Alpha = alpha };
            }

            _pulseContainer.Children.Add(_glowLayer);
            _pulseContainer.Children.Add(_coreLayer);
            Children.Add(_pulseContainer);

            _animTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(25) };
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();

            Loaded += (_, _) =>
            {
                if (IsVisible && !_isMorphing) _animTimer.Start();
            };
            Unloaded += (_, _) => _animTimer.Stop();
            IsVisibleChanged += (_, _) =>
            {
                if (IsVisible && !_isMorphing)
                    _animTimer.Start();
                else
                    _animTimer.Stop();
            };
        }

        private static Path CreateReusableCometPath()
        {
            return new Path
            {
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeDashCap = PenLineCap.Round,
                StrokeDashArray = new DoubleCollection { 1.0, 10.0 }
            };
        }

        public void SetMorphing(bool isMorphing)
        {
            if (_isMorphing == isMorphing) return;
            _isMorphing = isMorphing;
            if (_isMorphing)
            {
                _animTimer.Stop();
                _pulseContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                _pulseContainer.Visibility = Visibility.Visible;
                if (IsVisible)
                    _animTimer.Start();
            }
        }

        public void SetLightColor(Color c)
        {
            if (_lightColor == c) return;
            _lightColor = c;
            if (_lastW > 0 && _lastH > 0 && !_isMorphing)
            {
                bool isExp = _lastH > 90.0;
                RebuildCometPaths(_lastW, _lastH, isExp ? 22.0 : 14.0, isExp ? 1.5 : 1.3);
            }
            else
            {
                _lastW = -1;
            }
        }

        public void Configure(string pattern, string speed, string triggerMode, string notchStyle = "Notch")
        {
            bool rebuild = !string.Equals(_pattern, pattern, StringComparison.OrdinalIgnoreCase) ||
                           !string.Equals(_notchStyle, notchStyle, StringComparison.OrdinalIgnoreCase);

            _pattern = string.IsNullOrWhiteSpace(pattern) ? "Twin comets" : pattern;
            _speed = string.IsNullOrWhiteSpace(speed) ? "Normal" : speed;
            _triggerMode = string.IsNullOrWhiteSpace(triggerMode) ? "Always" : triggerMode;
            _notchStyle = string.IsNullOrWhiteSpace(notchStyle) ? "Notch" : notchStyle;

            if (string.Equals(_triggerMode, "Always", StringComparison.OrdinalIgnoreCase))
            {
                _burstOpacity = 1.0;
            }

            if (rebuild)
            {
                double prevW = _lastW;
                double prevH = _lastH;
                _lastW = -1;
                _lastStyle = "";
                if (prevW > 0 && prevH > 0 && !_isMorphing)
                {
                    bool isExp = prevH > 90.0;
                    _lastW = prevW;
                    _lastH = prevH;
                    _lastStyle = _notchStyle;
                    RebuildCometPaths(prevW, prevH, isExp ? 22.0 : 14.0, isExp ? 1.5 : 1.3);
                }
            }
        }

        public void TriggerBurst(double durationSeconds = 4.2)
        {
            _burstUntilUtc = DateTime.UtcNow.AddSeconds(durationSeconds);
            _burstOpacity = 1.0;
            if (IsVisible && !_isMorphing && !_animTimer.IsEnabled)
            {
                _animTimer.Start();
            }
        }

        public void UpdateGeometry(Geometry geometry, bool isExpanded, double width = 228.0, double height = 35.0)
        {
            if (_isMorphing) return;
            if (Math.Abs(width - _lastW) < 0.5 && Math.Abs(height - _lastH) < 0.5 && string.Equals(_notchStyle, _lastStyle, StringComparison.OrdinalIgnoreCase))
                return;

            _lastW = width;
            _lastH = height;
            _lastStyle = _notchStyle;

            RebuildCometPaths(width, height, isExpanded ? 22.0 : 14.0, isExpanded ? 1.5 : 1.3);
        }

        private void RebuildCometPaths(double width, double height, double cornerRadius, double coreThickness)
        {
            bool isIsland = string.Equals(_notchStyle, "Island", StringComparison.OrdinalIgnoreCase);
            bool isLine = string.Equals(_notchStyle, "Line", StringComparison.OrdinalIgnoreCase);

            if (isIsland)
            {
                OpacityMask = null;
            }
            else
            {
                // Fade top 9px (or 4px for Line) so shoulder tops blend seamlessly into the top screen bezel
                double fadePx = isLine ? 4.0 : 9.0;
                double fadeStop = Math.Clamp(fadePx / Math.Max(1.0, height), 0.06, 0.45);
                var mask = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(0, 1)
                };
                mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.0));
                mask.GradientStops.Add(new GradientStop(Color.FromArgb(140, 255, 255, 255), fadeStop * 0.55));
                mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), fadeStop));
                mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 1.0));
                mask.Freeze();
                OpacityMask = mask;
            }

            Point[] pts;
            double totalLen;
            if (isIsland)
            {
                pts = BuildIslandOutlinePoints(width, height, NotchGeometryBuilder.IslandGap, out totalLen);
            }
            else if (isLine)
            {
                pts = BuildLineOutlinePoints(width, height, cornerRadius, out totalLen);
            }
            else
            {
                double earW = 12.0;
                double earH = Math.Min(10.0, height * 0.35);
                pts = BuildOpenNotchOutlinePoints(width, height, earW, earH, cornerRadius, out totalLen);
            }
            _totalLength = Math.Max(10.0, totalLen);

            Geometry geoForward = BuildPolylineGeometry(pts, reverse: false);
            Geometry geoReverse = BuildPolylineGeometry(pts, reverse: true);

            bool isBreathe = string.Equals(_pattern, "Breathe", StringComparison.OrdinalIgnoreCase);
            bool isSingleComet = string.Equals(_pattern, "Comet", StringComparison.OrdinalIgnoreCase);

            // Base border & breathe glow
            byte baseAlpha = isBreathe ? (byte)215 : (byte)32;
            var baseBrush = new SolidColorBrush(Color.FromArgb(baseAlpha, _lightColor.R, _lightColor.G, _lightColor.B));
            baseBrush.Freeze();
            _baseBorderPath.Data = geoForward;
            _baseBorderPath.Stroke = baseBrush;
            _baseBorderPath.StrokeThickness = isBreathe ? coreThickness * 1.35 : 1.0;

            if (isBreathe)
            {
                var glowBrush = new SolidColorBrush(Color.FromArgb(190, _lightColor.R, _lightColor.G, _lightColor.B));
                glowBrush.Freeze();
                _breatheGlowPath.Data = geoForward;
                _breatheGlowPath.Stroke = glowBrush;
                _breatheGlowPath.StrokeThickness = coreThickness * 3.0;
                _breatheGlowPath.Visibility = Visibility.Visible;
            }
            else
            {
                _breatheGlowPath.Visibility = Visibility.Collapsed;
            }

            double glowThickness = coreThickness * 2.4;
            for (int i = 0; i < _cometEntries.Length; i++)
            {
                ref var entry = ref _cometEntries[i];
                double th = Math.Max(0.5, entry.IsGlow ? glowThickness : coreThickness);
                entry.Thickness = th;

                double totalInStrokeUnits = _totalLength / th;
                double dashLen = Math.Max(0.25, entry.SegFrac * totalInStrokeUnits);
                double gapLen = Math.Max(2.0, 4.0 * totalInStrokeUnits);

                Color c = entry.IsGlow ? _lightColor : _lightColor;
                if (!entry.IsGlow)
                {
                    // Mix white core with chosen light color so colored edge lights look vibrant
                    c = Color.FromRgb(
                        (byte)((_lightColor.R * 2 + 255) / 3),
                        (byte)((_lightColor.G * 2 + 255) / 3),
                        (byte)((_lightColor.B * 2 + 255) / 3));
                }
                byte a = (byte)Math.Clamp(Math.Round(entry.Alpha * 255.0), 0, 255);
                var brush = new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B));
                brush.Freeze();

                var p = entry.PathElement;
                p.Data = entry.IsReverse ? geoReverse : geoForward;
                p.Stroke = brush;
                p.StrokeThickness = th;
                if (isBreathe)
                {
                    p.Visibility = Visibility.Collapsed;
                }
                else
                {
                    p.Visibility = Visibility.Visible;
                }

                if (p.StrokeDashArray != null && p.StrokeDashArray.Count == 2 && !p.StrokeDashArray.IsFrozen)
                {
                    p.StrokeDashArray[0] = dashLen;
                    p.StrokeDashArray[1] = gapLen;
                }
                else
                {
                    p.StrokeDashArray = new DoubleCollection { dashLen, gapLen };
                }
            }

            UpdateDashOffsets();
        }

        private void OnAnimTick(object sender, EventArgs e)
        {
            if (!IsVisible || _isMorphing) return;

            if (!string.Equals(_triggerMode, "Always", StringComparison.OrdinalIgnoreCase))
            {
                if (DateTime.UtcNow < _burstUntilUtc)
                {
                    _burstOpacity = Math.Min(1.0, _burstOpacity + 0.14);
                }
                else
                {
                    _burstOpacity = Math.Max(0.0, _burstOpacity - 0.04);
                    if (_burstOpacity <= 0.005)
                    {
                        _pulseContainer.Opacity = 0.0;
                        return;
                    }
                }
            }
            else
            {
                _burstOpacity = 1.0;
            }

            double step = _speed switch
            {
                "Slow" => 0.0038,
                "Fast" => 0.0125,
                _ => 0.0068
            };
            if (string.Equals(_pattern, "Comet", StringComparison.OrdinalIgnoreCase))
            {
                step *= 0.65;
            }
            _cyclePhase = (_cyclePhase + step) % 1.0;
            UpdateDashOffsets();
        }

        private void UpdateDashOffsets()
        {
            double wave = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * _cyclePhase));

            if (string.Equals(_pattern, "Breathe", StringComparison.OrdinalIgnoreCase))
            {
                _pulseContainer.Opacity = (0.28 + 0.72 * wave) * _burstOpacity;
                return;
            }

            bool isSingleComet = string.Equals(_pattern, "Comet", StringComparison.OrdinalIgnoreCase);
            if (isSingleComet)
            {
                // Two-way forward comet:
                // Leg 1 (0.0 to 0.5): Sweeps Right -> Left and completes/exits at Left
                // Leg 2 (0.5 to 1.0): Sweeps Left -> Right and completes/exits at Right
                // Neither leg reverses backwards ("دنده عقب"); comet head always leads and tail trails!
                bool isLegRightToLeft = _cyclePhase < 0.5;
                double legProgress = isLegRightToLeft
                    ? (_cyclePhase / 0.5)
                    : ((_cyclePhase - 0.5) / 0.5);

                double p = -0.08 + legProgress * 1.64;
                _pulseContainer.Opacity = _burstOpacity;

                for (int i = 0; i < _cometEntries.Length; i++)
                {
                    ref var entry = ref _cometEntries[i];
                    if (entry.Thickness <= 0) continue;

                    bool belongsToCurrentLeg = isLegRightToLeft ? entry.IsReverse : !entry.IsReverse;
                    if (!belongsToCurrentLeg)
                    {
                        entry.PathElement.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    entry.PathElement.Visibility = Visibility.Visible;
                    entry.PathElement.StrokeDashOffset = (entry.SegFrac - p) * (_totalLength / entry.Thickness);
                }
                return;
            }

            // Twin comets meeting symmetrically at bottom center (0.50) and returning
            double pTwin = 0.08 + wave * 0.42;
            _pulseContainer.Opacity = (0.65 + 0.35 * wave) * _burstOpacity;

            for (int i = 0; i < _cometEntries.Length; i++)
            {
                var entry = _cometEntries[i];
                if (entry.Thickness <= 0 || entry.PathElement.Visibility != Visibility.Visible) continue;
                entry.PathElement.StrokeDashOffset = (entry.SegFrac - pTwin) * (_totalLength / entry.Thickness);
            }
        }

        private static Point[] BuildIslandOutlinePoints(double w, double h, double gap, out double length)
        {
            double r = Math.Min(h / 2.0, 22.0);
            var pts = new List<Point>(80);

            double topY = gap;
            double botY = gap + h;
            double cxL = r;
            double cxR = Math.Max(cxL, w - r);
            double midX = w / 2.0;

            // Start at Top Center, move left along top edge to top-left arc
            pts.Add(new Point(midX, topY));
            pts.Add(new Point(cxL, topY));

            int arcSteps = 10;
            // Top-left arc: from (cxL, topY) to (0, topY + r)
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = -0.5 * Math.PI - (0.5 * Math.PI) * ((double)i / arcSteps);
                pts.Add(new Point(cxL + r * Math.Cos(ang), (topY + r) + r * Math.Sin(ang)));
            }

            // Left vertical edge down to bottom-left arc start
            pts.Add(new Point(0, botY - r));

            // Bottom-left arc: from (0, botY - r) to (cxL, botY)
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = Math.PI - (0.5 * Math.PI) * ((double)i / arcSteps);
                pts.Add(new Point(cxL + r * Math.Cos(ang), (botY - r) + r * Math.Sin(ang)));
            }

            // Bottom edge left half: to Bottom Center (exact 50% perimeter marker)
            pts.Add(new Point(midX, botY));

            // Bottom edge right half: to (cxR, botY)
            pts.Add(new Point(cxR, botY));

            // Bottom-right arc: from (cxR, botY) to (w, botY - r)
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = 0.5 * Math.PI - (0.5 * Math.PI) * ((double)i / arcSteps);
                pts.Add(new Point(cxR + r * Math.Cos(ang), (botY - r) + r * Math.Sin(ang)));
            }

            // Right vertical edge up to top-right arc start
            pts.Add(new Point(w, topY + r));

            // Top-right arc: from (w, topY + r) to (cxR, topY)
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = -(0.5 * Math.PI) * ((double)i / arcSteps);
                pts.Add(new Point(cxR + r * Math.Cos(ang), (topY + r) + r * Math.Sin(ang)));
            }

            // Top edge right half back to Top Center
            pts.Add(new Point(midX, topY));

            length = 0.0;
            for (int i = 1; i < pts.Count; i++)
            {
                double dx = pts[i].X - pts[i - 1].X;
                double dy = pts[i].Y - pts[i - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return pts.ToArray();
        }

        private static Point[] BuildLineOutlinePoints(double w, double h, double cornerRadius, out double length)
        {
            double r = Math.Clamp(cornerRadius * 0.55, 4.0, Math.Max(4.0, h * 0.45));
            var pts = new List<Point>(50);

            double cxL = r;
            double cxR = Math.Max(cxL, w - r);
            double cyB = h - r;
            double midX = w / 2.0;

            // Start at top-left screen bezel (0, 0)
            pts.Add(new Point(0, 0));

            // Down left vertical edge to corner
            pts.Add(new Point(0, cyB));

            // Bottom-left rounded corner
            int arcSteps = 10;
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = Math.PI - (0.5 * Math.PI) * ((double)i / arcSteps);
                pts.Add(new Point(cxL + r * Math.Cos(ang), cyB + r * Math.Sin(ang)));
            }

            // Flat bottom edge to center (exact 50% perimeter marker)
            pts.Add(new Point(midX, h));

            // Flat bottom edge from center to right corner
            pts.Add(new Point(cxR, h));

            // Bottom-right rounded corner
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = 0.5 * Math.PI - (0.5 * Math.PI) * ((double)i / arcSteps);
                pts.Add(new Point(cxR + r * Math.Cos(ang), cyB + r * Math.Sin(ang)));
            }

            // Up right vertical edge to top-right screen bezel (w, 0)
            pts.Add(new Point(w, 0));

            length = 0.0;
            for (int i = 1; i < pts.Count; i++)
            {
                double dx = pts[i].X - pts[i - 1].X;
                double dy = pts[i].Y - pts[i - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return pts.ToArray();
        }

        private static Point[] BuildOpenNotchOutlinePoints(double w, double h, double earW, double earH, double br, out double length)
        {
            var pts = new List<Point>(64);
            double r = Math.Min(br, Math.Min((w - 2 * earW) * 0.45, Math.Max(2.0, h - earH - 1.0)));

            int earSteps = 10;
            for (int i = 0; i <= earSteps; i++)
            {
                double t = (double)i / earSteps;
                pts.Add(EvalBezier(new Point(0, 0), new Point(earW * 0.65, 0), new Point(earW, earH * 0.35), new Point(earW, earH), t));
            }

            pts.Add(new Point(earW, Math.Max(earH, h - r)));

            int arcSteps = 12;
            double cxL = earW + r;
            double cyB = h - r;
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = Math.PI - (Math.PI * 0.5) * ((double)i / arcSteps);
                pts.Add(new Point(cxL + r * Math.Cos(ang), cyB + r * Math.Sin(ang)));
            }

            int bottomSteps = 16;
            double xStart = earW + r;
            double xEnd = w - earW - r;
            for (int i = 1; i <= bottomSteps; i++)
            {
                double t = (double)i / bottomSteps;
                pts.Add(new Point(xStart + (xEnd - xStart) * t, h));
            }

            double cxR = w - earW - r;
            for (int i = 1; i <= arcSteps; i++)
            {
                double ang = (Math.PI * 0.5) * (1.0 - (double)i / arcSteps);
                pts.Add(new Point(cxR + r * Math.Cos(ang), cyB + r * Math.Sin(ang)));
            }

            pts.Add(new Point(w - earW, earH));

            for (int i = 1; i <= earSteps; i++)
            {
                double t = (double)i / earSteps;
                pts.Add(EvalBezier(new Point(w - earW, earH), new Point(w - earW, earH * 0.35), new Point(w - earW * 0.65, 0), new Point(w, 0), t));
            }

            length = 0.0;
            for (int i = 1; i < pts.Count; i++)
            {
                double dx = pts[i].X - pts[i - 1].X;
                double dy = pts[i].Y - pts[i - 1].Y;
                length += Math.Sqrt(dx * dx + dy * dy);
            }

            return pts.ToArray();
        }

        private static Point EvalBezier(Point p0, Point p1, Point p2, Point p3, double t)
        {
            double u = 1.0 - t;
            double tt = t * t;
            double uu = u * u;
            double uuu = uu * u;
            double ttt = tt * t;

            double x = uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X;
            double y = uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y;
            return new Point(x, y);
        }

        private static Geometry BuildPolylineGeometry(Point[] pts, bool reverse)
        {
            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                if (!reverse)
                {
                    ctx.BeginFigure(pts[0], false, false);
                    for (int i = 1; i < pts.Length; i++)
                        ctx.LineTo(pts[i], true, true);
                }
                else
                {
                    ctx.BeginFigure(pts[pts.Length - 1], false, false);
                    for (int i = pts.Length - 2; i >= 0; i--)
                        ctx.LineTo(pts[i], true, true);
                }
            }
            geom.Freeze();
            return geom;
        }
    }

    /// <summary>
    /// MiniAnalogClock: 16x16 crisp analog clock face for the resting notch when Clock style = Analog.
    /// </summary>
    public class MiniAnalogClock : Canvas
    {
        private readonly Line _hourHand;
        private readonly Line _minuteHand;
        private readonly double _radius;

        public MiniAnalogClock(double size = 16.0)
        {
            Width = size;
            Height = size;
            _radius = size / 2.0;
            VerticalAlignment = VerticalAlignment.Center;

            var dial = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = new SolidColorBrush(Color.FromArgb(215, 255, 255, 255)),
                StrokeThickness = 1.25
            };
            Children.Add(dial);

            _hourHand = new Line
            {
                X1 = _radius,
                Y1 = _radius,
                Stroke = Brushes.White,
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            Children.Add(_hourHand);

            _minuteHand = new Line
            {
                X1 = _radius,
                Y1 = _radius,
                Stroke = Brushes.White,
                StrokeThickness = 1.15,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            Children.Add(_minuteHand);

            var centerDot = new Ellipse
            {
                Width = 2.2,
                Height = 2.2,
                Fill = new SolidColorBrush(Color.FromRgb(255, 85, 0))
            };
            SetLeft(centerDot, _radius - 1.1);
            SetTop(centerDot, _radius - 1.1);
            Children.Add(centerDot);

            UpdateTime(DateTime.Now);
        }

        public void UpdateTime(DateTime now)
        {
            double minAngle = ((now.Minute + now.Second / 60.0) / 60.0) * 2.0 * Math.PI - Math.PI / 2.0;
            double hourAngle = (((now.Hour % 12) + now.Minute / 60.0) / 12.0) * 2.0 * Math.PI - Math.PI / 2.0;

            double rHour = _radius * 0.48;
            double rMin = _radius * 0.68;

            _hourHand.X2 = _radius + Math.Cos(hourAngle) * rHour;
            _hourHand.Y2 = _radius + Math.Sin(hourAngle) * rHour;

            _minuteHand.X2 = _radius + Math.Cos(minAngle) * rMin;
            _minuteHand.Y2 = _radius + Math.Sin(minAngle) * rMin;
        }
    }

    /// <summary>
    /// MediaTransportControlsBar: Exact vector Previous (|<), Play/Pause (|| / |>), and Next (>|) buttons
    /// shown on the right side of the Wide Music Card (00:07 - 00:10 in the user's video).
    /// </summary>
    public class MediaTransportControlsBar : StackPanel
    {
        private readonly Canvas _playPauseCanvas;
        private bool _isPlaying = true;

        public MediaTransportControlsBar(Action onPrev, Action onPlayPause, Action onNext)
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalAlignment = HorizontalAlignment.Right;

            var prevBtn = CreateIconButton(BuildSkipCanvas(isNext: false), "Previous", onPrev);
            _playPauseCanvas = new Canvas { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var playPauseBtn = CreateIconButton(_playPauseCanvas, "Play / Pause", onPlayPause);
            var nextBtn = CreateIconButton(BuildSkipCanvas(isNext: true), "Next", onNext);

            Children.Add(prevBtn);
            Children.Add(playPauseBtn);
            Children.Add(nextBtn);

            SetPlaying(true);
        }

        public void SetPlaying(bool playing)
        {
            _isPlaying = playing;
            _playPauseCanvas.Children.Clear();
            var strokeBrush = CapsuleTheme.Brush(Color.FromRgb(245, 245, 250));

            if (playing)
            {
                // Two outlined vertical rounded bars ("00" pause icon in 00:07-00:10 of the video)
                var leftBar = new Rectangle
                {
                    Width = 3.2,
                    Height = 9.6,
                    RadiusX = 1.0,
                    RadiusY = 1.0,
                    Stroke = strokeBrush,
                    StrokeThickness = 1.25,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(leftBar, 1.6);
                Canvas.SetTop(leftBar, 1.2);

                var rightBar = new Rectangle
                {
                    Width = 3.2,
                    Height = 9.6,
                    RadiusX = 1.0,
                    RadiusY = 1.0,
                    Stroke = strokeBrush,
                    StrokeThickness = 1.25,
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(rightBar, 7.2);
                Canvas.SetTop(rightBar, 1.2);

                _playPauseCanvas.Children.Add(leftBar);
                _playPauseCanvas.Children.Add(rightBar);
            }
            else
            {
                var playPath = new Path
                {
                    Data = Geometry.Parse("M 2.5,1.5 L 10.5,6.0 L 2.5,10.5 Z"),
                    Stroke = strokeBrush,
                    StrokeThickness = 1.3,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = Brushes.Transparent
                };
                _playPauseCanvas.Children.Add(playPath);
            }
        }

        private static Canvas BuildSkipCanvas(bool isNext)
        {
            var c = new Canvas { Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var brush = CapsuleTheme.Brush(Color.FromRgb(235, 235, 242));

            if (isNext)
            {
                var tri = new Path
                {
                    Data = Geometry.Parse("M 2.0,2.2 L 8.2,6.0 L 2.0,9.8 Z"),
                    Stroke = brush,
                    StrokeThickness = 1.25,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = Brushes.Transparent
                };
                var bar = new Line
                {
                    X1 = 9.8, Y1 = 2.2,
                    X2 = 9.8, Y2 = 9.8,
                    Stroke = brush,
                    StrokeThickness = 1.35,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                c.Children.Add(tri);
                c.Children.Add(bar);
            }
            else
            {
                var bar = new Line
                {
                    X1 = 2.2, Y1 = 2.2,
                    X2 = 2.2, Y2 = 9.8,
                    Stroke = brush,
                    StrokeThickness = 1.35,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                var tri = new Path
                {
                    Data = Geometry.Parse("M 10.0,2.2 L 3.8,6.0 L 10.0,9.8 Z"),
                    Stroke = brush,
                    StrokeThickness = 1.25,
                    StrokeLineJoin = PenLineJoin.Round,
                    Fill = Brushes.Transparent
                };
                c.Children.Add(bar);
                c.Children.Add(tri);
            }
            return c;
        }

        private static Border CreateIconButton(UIElement icon, string tooltip, Action onClick)
        {
            var b = new Border
            {
                Width = 20,
                Height = 18,
                CornerRadius = new CornerRadius(5),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(2, 0, 0, 0),
                Child = icon
            };
            b.MouseEnter += (_, _) => b.Background = CapsuleTheme.Brush(Color.FromArgb(35, 255, 255, 255));
            b.MouseLeave += (_, _) => b.Background = Brushes.Transparent;
            b.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick?.Invoke();
            };
            return b;
        }
    }

    /// <summary>
    /// BatteryStatusGlyph: Exact green rounded battery icon matching Vayli resting notch video (frame_01.png).
    /// </summary>
    public class BatteryStatusGlyph : StackPanel
    {
        private readonly Border _bodyBorder;
        private readonly Border _fillBar;
        private readonly Border _terminalCap;

        public BatteryStatusGlyph()
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(0, 0, 5, 0);

            _fillBar = new Border
            {
                Height = 9.0,
                Width = 16.0,
                CornerRadius = new CornerRadius(2.0),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Background = CapsuleTheme.Brush(Color.FromRgb(48, 209, 88))
            };

            _bodyBorder = new Border
            {
                Width = 20.0,
                Height = 11.0,
                CornerRadius = new CornerRadius(3.0),
                Padding = new Thickness(1.0),
                Background = CapsuleTheme.Brush(Color.FromArgb(65, 48, 209, 88)),
                Child = _fillBar,
                VerticalAlignment = VerticalAlignment.Center
            };

            _terminalCap = new Border
            {
                Width = 1.6,
                Height = 4.2,
                CornerRadius = new CornerRadius(0.8),
                Margin = new Thickness(1.2, 0, 0, 0),
                Background = CapsuleTheme.Brush(Color.FromArgb(180, 48, 209, 88)),
                VerticalAlignment = VerticalAlignment.Center
            };

            Children.Add(_bodyBorder);
            Children.Add(_terminalCap);
        }

        private int _lastPercent = -1;

        public void SetBattery(int percent)
        {
            int clamped = Math.Clamp(percent, 5, 100);
            if (_lastPercent == clamped) return;
            _lastPercent = clamped;

            Color c = clamped <= 20
                ? Color.FromRgb(255, 69, 58)
                : Color.FromRgb(48, 209, 88);

            _fillBar.Width = Math.Max(2.0, 18.0 * (clamped / 100.0));
            _fillBar.Background = CapsuleTheme.Brush(c);
            _bodyBorder.Background = CapsuleTheme.Brush(Color.FromArgb(65, c.R, c.G, c.B));
            _terminalCap.Background = CapsuleTheme.Brush(Color.FromArgb(180, c.R, c.G, c.B));
        }
    }

    /// <summary>
    /// MiniEqualizerControl: The 4-bar animated audio visualizer on the top-right of the music notch,
    /// dynamically colored with the current track's album cover dominant color.
    /// </summary>
    public class MiniEqualizerControl : StackPanel
    {
        private readonly Border[] _bars = new Border[4];
        private bool _isPlaying = true;
        private Color _beatColor = Color.FromRgb(245, 245, 250);

        public bool IsPlaying
        {
            get => _isPlaying;
            set => _isPlaying = value;
        }

        public MiniEqualizerControl()
        {
            Orientation = Orientation.Horizontal;
            VerticalAlignment = VerticalAlignment.Center;
            Height = 14;
            Margin = new Thickness(8, 0, 2, 0);

            double[] initialHeights = { 9.0, 3.0, 11.0, 8.0 };
            for (int i = 0; i < 4; i++)
            {
                var pill = new Border
                {
                    Width = 2.4,
                    Height = initialHeights[i],
                    CornerRadius = new CornerRadius(1.2),
                    Background = CapsuleTheme.Brush(_beatColor),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(1.25, 0, 1.25, 0)
                };
                _bars[i] = pill;
                Children.Add(pill);
            }
        }

        public void SetBeatColor(Color color)
        {
            _beatColor = color;
            var brush = CapsuleTheme.Brush(_beatColor);
            for (int i = 0; i < 4; i++)
            {
                _bars[i].Background = brush;
            }
        }

        public void SetLevels(float[] mini4, bool isPlaying)
        {
            _isPlaying = isPlaying;
            if (!isPlaying || mini4 == null || mini4.Length < 4)
            {
                DecayLevels(3.0);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                double target = 2.5 + Math.Clamp(mini4[i], 0f, 1f) * 10.5;
                _bars[i].Height = _bars[i].Height * 0.45 + target * 0.55;
            }
        }

        public bool DecayLevels(double targetHeight = 3.0)
        {
            bool allSettled = true;
            for (int i = 0; i < 4; i++)
            {
                double cur = _bars[i].Height;
                double next = cur * 0.78 + targetHeight * 0.22;
                if (Math.Abs(next - targetHeight) > 0.08) allSettled = false;
                else next = targetHeight;
                _bars[i].Height = next;
            }
            return allSettled;
        }
    }

    /// <summary>
    /// WaveformProgressBar: Full-width real-time symmetrical audio spectrum visualizer across the bottom
    /// of the music notch (CompactBars = 40, WideBars = 56), matching the user's videos.
    /// </summary>
    public class WaveformProgressBar : StackPanel
    {
        public const int CompactBarCount = 40;
        public const int WideBarCount = 56;
        public const int BarCount = 40;

        private readonly int _count;
        private readonly Border[] _bars;
        private Color _beatColor = Color.FromRgb(245, 245, 250);
        private bool _isPlaying = true;

        public int Count => _count;

        public WaveformProgressBar(int barCount = CompactBarCount)
        {
            _count = Math.Max(8, barCount);
            _bars = new Border[_count];

            Orientation = Orientation.Horizontal;
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            Height = 11.5;
            Margin = new Thickness(0, 3, 0, 0);

            var brush = CapsuleTheme.Brush(_beatColor);
            for (int i = 0; i < _count; i++)
            {
                var b = new Border
                {
                    Width = 2.3,
                    Height = 2.3,
                    CornerRadius = new CornerRadius(1.15),
                    Margin = new Thickness(1.65, 0, 1.65, 0),
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Background = brush
                };
                _bars[i] = b;
                Children.Add(b);
            }
        }

        public void SetBeatColor(Color color)
        {
            _beatColor = color;
            var brush = CapsuleTheme.Brush(_beatColor);
            for (int i = 0; i < _count; i++)
            {
                _bars[i].Background = brush;
            }
        }

        public void SetSpectrumLevels(float[] mappedBars, bool isPlaying, bool isAudioLive, double phase)
        {
            _isPlaying = isPlaying;
            if (!isPlaying)
            {
                DecayLevels(2.2);
                return;
            }

            float maxLevel = 0f;
            if (mappedBars != null)
            {
                for (int i = 0; i < Math.Min(_count, mappedBars.Length); i++)
                    if (mappedBars[i] > maxLevel) maxLevel = mappedBars[i];
            }

            bool useLiveFft = isAudioLive && maxLevel > 0.04f;
            double half = (_count - 1) * 0.5;

            for (int i = 0; i < _count; i++)
            {
                double h;
                if (useLiveFft)
                {
                    float lvl = mappedBars[i];
                    h = 2.2 + Math.Clamp(lvl, 0f, 1f) * 8.8;
                }
                else
                {
                    double dist = Math.Abs(i - half) / half;
                    if (dist < 0.22)
                    {
                        h = 2.2 + 1.2 * Math.Max(0.0, Math.Sin(phase * 3.2 - dist * 6.0));
                    }
                    else
                    {
                        double wave1 = Math.Sin(dist * Math.PI * 2.8 - phase * 3.4);
                        double wave2 = Math.Cos(dist * Math.PI * 4.5 + phase * 2.7);
                        double env = Math.Sin(dist * Math.PI);
                        h = 2.3 + Math.Max(0.0, (wave1 * 0.55 + wave2 * 0.45 + 0.65) * env * 5.2);
                    }
                }

                h = Math.Clamp(h, 2.2, 11.0);
                _bars[i].Height = _bars[i].Height * 0.40 + h * 0.60;
            }
        }

        public void SetPlayback(double progress, bool isPlaying)
        {
            _isPlaying = isPlaying;
        }

        public bool DecayLevels(double targetHeight = 2.2)
        {
            bool allSettled = true;
            for (int i = 0; i < _count; i++)
            {
                double cur = _bars[i].Height;
                double next = cur * 0.78 + targetHeight * 0.22;
                if (Math.Abs(next - targetHeight) > 0.08) allSettled = false;
                else next = targetHeight;
                _bars[i].Height = next;
            }
            return allSettled;
        }
    }

    /// <summary>
    /// AlbumArtThumbnail: Displays the artist/track artwork thumbnail in either Compact (23x23) or Wide (42x42) size.
    /// </summary>
    public class AlbumArtThumbnail : Border
    {
        private readonly Image _imageControl;
        private readonly Border _fallbackArtwork;

        public AlbumArtThumbnail(double size = 23.0, double cornerRadius = 6.0)
        {
            Width = size;
            Height = size;
            CornerRadius = new CornerRadius(cornerRadius);
            ClipToBounds = true;
            Background = CapsuleTheme.Brush(Color.FromRgb(22, 23, 28));
            BorderBrush = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            BorderThickness = new Thickness(1);
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(0, 0, size > 30 ? 10 : 8, 0);

            var grid = new Grid();
            var updateClip = new Action(() =>
            {
                double w = ActualWidth > 0 ? ActualWidth : size;
                double h = ActualHeight > 0 ? ActualHeight : size;
                grid.Clip = new RectangleGeometry(new Rect(0, 0, w, h), cornerRadius, cornerRadius);
            });
            updateClip();
            SizeChanged += (_, _) => updateClip();

            var canvas = new Canvas { Width = 23, Height = 23 };

            var shoulders = new Path
            {
                Fill = new SolidColorBrush(Color.FromRgb(65, 68, 80)),
                Data = Geometry.Parse("M 1 23 C 3 14, 20 14, 22 23 Z")
            };
            canvas.Children.Add(shoulders);

            var head = new Ellipse
            {
                Width = 8.0,
                Height = 9.5,
                Fill = new SolidColorBrush(Color.FromRgb(180, 182, 192))
            };
            Canvas.SetLeft(head, 7.5);
            Canvas.SetTop(head, 4.2);
            canvas.Children.Add(head);

            var hair = new Path
            {
                Fill = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                Data = Geometry.Parse("M 6.5 10 C 6.5 3, 16.5 3, 16.5 10 C 16.5 13, 14.5 12, 14.5 8 C 13.5 5, 9.5 5, 8.5 8 C 8.5 12, 6.5 13, 6.5 10 Z")
            };
            canvas.Children.Add(hair);

            _fallbackArtwork = new Border
            {
                CornerRadius = new CornerRadius(cornerRadius),
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
                Child = new Viewbox { Stretch = Stretch.Uniform, Child = canvas }
            };
            grid.Children.Add(_fallbackArtwork);

            _imageControl = new Image
            {
                Stretch = Stretch.UniformToFill,
                Visibility = Visibility.Collapsed
            };
            RenderOptions.SetBitmapScalingMode(_imageControl, BitmapScalingMode.HighQuality);
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
