using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.Weather
{
    public sealed class WeatherToolView : Grid
    {
        public static WeatherToolView Instance { get; private set; }

        private readonly WeatherService _service = WeatherService.Instance;

        private Grid _dashboardGrid;
        private Grid _pickerGrid;

        // Card 1: Current
        private Border _currentCard;
        private StackPanel _currentIconHost;
        private TextBlock _currentTempText;
        private TextBlock _currentConditionText;
        private TextBlock _currentHighLowText;
        private TextBlock _currentSunText;
        private TextBlock _currentUpdatedText;

        // Card 2: Hourly
        private Border _hourlyCard;
        private UniformGrid _hourlyColumnsGrid;
        private Canvas _hourlyChartCanvas;
        private TextBlock _humidityValText;
        private TextBlock _windValText;
        private TextBlock _uvValText;

        // Card 3: 5-Day
        private Border _forecastCard;
        private StackPanel _forecastRowsStack;

        // Picker controls
        private TextBox _searchBox;
        private TextBlock _searchPlaceholder;
        private Border _searchClearBtn;
        private WrapPanel _citiesWrapPanel;
        private DispatcherTimer _searchDebounceTimer;

        public WeatherToolView()
        {
            Instance = this;
            Width = 728;
            Height = 242;

            BuildUI();

            _service.WeatherUpdated += OnWeatherUpdated;
            Loaded += (_, _) => RefreshUI();
            Unloaded += (_, _) => _service.WeatherUpdated -= OnWeatherUpdated;
        }

        private void BuildUI()
        {
            // 1. Dashboard View (The 3 Cards)
            _dashboardGrid = new Grid();
            _dashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(206) });
            _dashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(284) });
            _dashboardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(238) });

            BuildCurrentCard();
            BuildHourlyCard();
            BuildForecastCard();

            Children.Add(_dashboardGrid);

            // 2. City Picker View (Overlay)
            BuildCityPickerView();
            Children.Add(_pickerGrid);
        }

        #region Card 1: Current Weather
        private void BuildCurrentCard()
        {
            _currentCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var dock = new DockPanel();

            // Top section: Icon, Temp, Condition, Range
            var topStack = new StackPanel();

            _currentIconHost = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 2)
            };
            topStack.Children.Add(_currentIconHost);

            _currentTempText = new TextBlock
            {
                Text = "18°",
                FontSize = 38,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 0)
            };
            topStack.Children.Add(_currentTempText);

            _currentConditionText = new TextBlock
            {
                Text = "Overcast",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 1, 0, 2)
            };
            topStack.Children.Add(_currentConditionText);

            _currentHighLowText = new TextBlock
            {
                Text = "H 25°  L 10° · Feels like 15°",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            topStack.Children.Add(_currentHighLowText);

            DockPanel.SetDock(topStack, Dock.Top);
            dock.Children.Add(topStack);

            // Bottom section: Divider, Sunrise/Sunset, Updated
            var bottomStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };

            var div = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(34, 34, 40)),
                Margin = new Thickness(0, 10, 0, 8)
            };
            bottomStack.Children.Add(div);

            _currentSunText = new TextBlock
            {
                Text = "Sunrise 6:19 · Sunset 5:13",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            bottomStack.Children.Add(_currentSunText);

            _currentUpdatedText = new TextBlock
            {
                Text = "Updated 10:22",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            bottomStack.Children.Add(_currentUpdatedText);

            DockPanel.SetDock(bottomStack, Dock.Bottom);
            dock.Children.Add(bottomStack);

            _currentCard.Child = dock;
            Grid.SetColumn(_currentCard, 0);
            _dashboardGrid.Children.Add(_currentCard);
        }
        #endregion

        #region Card 2: Hourly Forecast
        private void BuildHourlyCard()
        {
            _hourlyCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(4, 0, 4, 0)
            };

            var dock = new DockPanel();

            // Header: Hourly
            var header = new TextBlock
            {
                Text = "Hourly",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 0, 4)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // Bottom Metrics: Humidity, Wind, UV index
            var bottomArea = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };

            var div = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(34, 34, 40)),
                Margin = new Thickness(0, 8, 0, 6)
            };
            bottomArea.Children.Add(div);

            var metricsGrid = new UniformGrid { Columns = 3 };

            // Humidity
            var humStack = CreateMetricBlock("💧", "Humidity", out _humidityValText);
            metricsGrid.Children.Add(humStack);

            // Wind
            var windStack = CreateMetricBlock("≈", "Wind", out _windValText);
            metricsGrid.Children.Add(windStack);

            // UV index
            var uvStack = CreateMetricBlock("☼", "UV index", out _uvValText);
            metricsGrid.Children.Add(uvStack);

            bottomArea.Children.Add(metricsGrid);
            DockPanel.SetDock(bottomArea, Dock.Bottom);
            dock.Children.Add(bottomArea);

            // Middle: Hourly labels, icons, temperatures & Curve Chart
            var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            _hourlyColumnsGrid = new UniformGrid { Columns = 6, Margin = new Thickness(0, 0, 0, 2) };
            centerStack.Children.Add(_hourlyColumnsGrid);

            _hourlyChartCanvas = new Canvas
            {
                Width = 254,
                Height = 52,
                ClipToBounds = true,
                Margin = new Thickness(0, 2, 0, 0)
            };
            centerStack.Children.Add(_hourlyChartCanvas);

            dock.Children.Add(centerStack);

            _hourlyCard.Child = dock;
            Grid.SetColumn(_hourlyCard, 1);
            _dashboardGrid.Children.Add(_hourlyCard);
        }

        private StackPanel CreateMetricBlock(string iconGlyph, string label, out TextBlock valBlock)
        {
            var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            var top = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            top.Children.Add(new TextBlock
            {
                Text = iconGlyph + " ",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            });
            top.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            });
            sp.Children.Add(top);

            valBlock = new TextBlock
            {
                Text = "--",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            sp.Children.Add(valBlock);
            return sp;
        }
        #endregion

        #region Card 3: 5-Day Forecast
        private void BuildForecastCard()
        {
            _forecastCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 12, 14, 12),
                Margin = new Thickness(4, 0, 0, 0)
            };

            var dock = new DockPanel();

            var header = new TextBlock
            {
                Text = "5-Day",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            _forecastRowsStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            dock.Children.Add(_forecastRowsStack);

            _forecastCard.Child = dock;
            Grid.SetColumn(_forecastCard, 2);
            _dashboardGrid.Children.Add(_forecastCard);
        }
        #endregion

        #region City Picker Screen
        private void BuildCityPickerView()
        {
            _pickerGrid = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromRgb(14, 14, 18))
            };

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 26)),
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(10)
            };

            var dock = new DockPanel();

            // Header with Back button
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };

            var backBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 40)),
                CornerRadius = new CornerRadius(7),
                Padding = new Thickness(10, 5, 10, 5),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var backText = new TextBlock
            {
                Text = "← Back",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            backBtn.Child = backText;
            backBtn.MouseEnter += (_, _) => backBtn.Background = new SolidColorBrush(Color.FromRgb(45, 45, 56));
            backBtn.MouseLeave += (_, _) => backBtn.Background = new SolidColorBrush(Color.FromRgb(32, 32, 40));
            backBtn.MouseLeftButtonUp += (_, _) => HideCityPicker();
            DockPanel.SetDock(backBtn, Dock.Left);
            topBar.Children.Add(backBtn);

            var titleStack = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Select City",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            topBar.Children.Add(titleStack);

            DockPanel.SetDock(topBar, Dock.Top);
            dock.Children.Add(topBar);

            // Search Bar
            var searchBorder = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 62)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 0, 8, 0),
                Margin = new Thickness(0, 0, 0, 12)
            };

            var searchDock = new DockPanel();

            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            DockPanel.SetDock(searchIcon, Dock.Left);
            searchDock.Children.Add(searchIcon);

            _searchClearBtn = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            var clearTxt = new TextBlock
            {
                Text = "✕",
                FontSize = 9.5,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchClearBtn.Child = clearTxt;
            _searchClearBtn.MouseLeftButtonUp += (_, _) =>
            {
                _searchBox.Text = "";
                _searchBox.Focus();
            };
            DockPanel.SetDock(_searchClearBtn, Dock.Right);
            searchDock.Children.Add(_searchClearBtn);

            var searchGrid = new Grid();
            _searchPlaceholder = new TextBlock
            {
                Text = "Search city (e.g. Isfahan, Tehran, London, Tokyo, New York...)",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 128)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchGrid.Children.Add(_searchPlaceholder);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontSize = 12.5,
                CaretBrush = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += OnSearchTextChanged;
            searchGrid.Children.Add(_searchBox);

            searchDock.Children.Add(searchGrid);
            searchBorder.Child = searchDock;

            DockPanel.SetDock(searchBorder, Dock.Top);
            dock.Children.Add(searchBorder);

            // Suggestions List
            var listHeader = new TextBlock
            {
                Text = "POPULAR & RECENT CITIES",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(2, 0, 0, 6)
            };
            DockPanel.SetDock(listHeader, Dock.Top);
            dock.Children.Add(listHeader);

            _citiesWrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _citiesWrapPanel
            };
            dock.Children.Add(scroll);

            card.Child = dock;
            _pickerGrid.Children.Add(card);

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _searchDebounceTimer.Tick += async (s, e) =>
            {
                _searchDebounceTimer.Stop();
                await PerformCitySearchAsync(_searchBox.Text);
            };
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            string q = _searchBox.Text;
            _searchPlaceholder.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
            _searchClearBtn.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private async Task PerformCitySearchAsync(string q)
        {
            var results = await _service.SearchCitiesAsync(q);
            PopulateCityPills(results);
        }

        public void ShowCityPicker()
        {
            _dashboardGrid.Visibility = Visibility.Collapsed;
            _pickerGrid.Visibility = Visibility.Visible;
            _searchBox.Text = "";
            PopulateCityPills(_service.Prefs.RecentCities.Count > 0 ? _service.Prefs.RecentCities : WeatherService.GetDefaultPopularCities());
            _searchBox.Focus();
        }

        public void HideCityPicker()
        {
            _pickerGrid.Visibility = Visibility.Collapsed;
            _dashboardGrid.Visibility = Visibility.Visible;
        }

        private void PopulateCityPills(List<GeocodedCity> cities)
        {
            _citiesWrapPanel.Children.Clear();
            if (cities == null || cities.Count == 0)
            {
                _citiesWrapPanel.Children.Add(new TextBlock
                {
                    Text = "No cities found. Try another search term.",
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    Margin = new Thickness(6, 12, 0, 0)
                });
                return;
            }

            foreach (var city in cities)
            {
                var pill = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 38)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(12, 7, 12, 7),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand
                };

                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = city.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                });

                string extra = !string.IsNullOrEmpty(city.Country) ? $" · {city.Country}" : "";
                sp.Children.Add(new TextBlock
                {
                    Text = extra,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    Margin = new Thickness(2, 0, 0, 0)
                });

                pill.Child = sp;

                pill.MouseEnter += (_, _) => pill.Background = new SolidColorBrush(Color.FromRgb(48, 48, 62));
                pill.MouseLeave += (_, _) => pill.Background = new SolidColorBrush(Color.FromRgb(30, 30, 38));

                var captured = city;
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    _service.SetCity(captured);
                    HideCityPicker();
                };

                _citiesWrapPanel.Children.Add(pill);
            }
        }
        #endregion

        #region Refresh UI & Charts
        private void OnWeatherUpdated()
        {
            Dispatcher.Invoke(RefreshUI);
        }

        public void RefreshUI()
        {
            var data = _service.Current;
            if (data == null) return;

            // 1. Current Card
            _currentIconHost.Children.Clear();
            _currentIconHost.Children.Add(WeatherIcons.CreateIcon(WeatherService.GetIconType(data.WeatherCode, true), size: 42));

            _currentTempText.Text = _service.FormatTemp(data.Temperature);
            _currentConditionText.Text = data.Condition;
            _currentHighLowText.Text = $"H {_service.FormatTemp(data.HighTemp)}  L {_service.FormatTemp(data.LowTemp)} · Feels like {_service.FormatTemp(data.FeelsLike)}";
            _currentSunText.Text = $"Sunrise {data.Sunrise} · Sunset {data.Sunset}";
            _currentUpdatedText.Text = $"Updated {data.LastUpdated}";

            // 2. Hourly Card
            _hourlyColumnsGrid.Children.Clear();
            int count = Math.Min(6, data.Hourly.Count);
            for (int i = 0; i < count; i++)
            {
                var h = data.Hourly[i];
                var col = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

                col.Children.Add(new TextBlock
                {
                    Text = h.Time,
                    FontSize = 10,
                    FontWeight = i == 0 ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = i == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 4)
                });

                col.Children.Add(WeatherIcons.CreateIcon(h.IconType, size: 16));

                col.Children.Add(new TextBlock
                {
                    Text = _service.FormatTemp(h.Temp),
                    FontSize = 11.5,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                });

                _hourlyColumnsGrid.Children.Add(col);
            }

            DrawHourlyCurveChart(data.Hourly.Take(6).ToList());

            _humidityValText.Text = $"{data.Humidity}%";
            _windValText.Text = $"{Math.Round(data.WindSpeed)} km/h";
            _uvValText.Text = data.UvText;

            // 3. 5-Day Card
            _forecastRowsStack.Children.Clear();
            if (data.Daily.Count > 0)
            {
                double globalMin = data.Daily.Min(d => d.MinTemp);
                double globalMax = data.Daily.Max(d => d.MaxTemp);
                if (Math.Abs(globalMax - globalMin) < 0.1) globalMax = globalMin + 1;

                foreach (var day in data.Daily.Take(5))
                {
                    _forecastRowsStack.Children.Add(CreateDailyRow(day, globalMin, globalMax, data.Temperature));
                }
            }
        }

        private void DrawHourlyCurveChart(List<HourlyForecast> hours)
        {
            _hourlyChartCanvas.Children.Clear();
            if (hours == null || hours.Count < 2) return;

            double w = _hourlyChartCanvas.Width;
            double h = _hourlyChartCanvas.Height;
            int n = hours.Count;
            double stepX = w / n;

            double minTemp = hours.Min(x => x.Temp);
            double maxTemp = hours.Max(x => x.Temp);
            double padTemp = Math.Max(2.0, maxTemp - minTemp);
            double rangeMin = minTemp - 1.0;
            double rangeMax = maxTemp + 1.0;

            var points = new Point[n];
            for (int i = 0; i < n; i++)
            {
                double norm = (hours[i].Temp - rangeMin) / (rangeMax - rangeMin);
                double y = (1.0 - norm) * (h - 18) + 8;
                double x = (i * stepX) + (stepX / 2.0);
                points[i] = new Point(x, y);
            }

            // Filled Area Path
            var areaGeom = new StreamGeometry();
            using (var ctx = areaGeom.Open())
            {
                ctx.BeginFigure(new Point(points[0].X, h), isFilled: true, isClosed: true);
                ctx.LineTo(points[0], isStroked: false, isSmoothJoin: true);

                for (int i = 0; i < n - 1; i++)
                {
                    var p0 = points[i];
                    var p1 = points[i + 1];
                    var cp1 = new Point(p0.X + (p1.X - p0.X) / 2.0, p0.Y);
                    var cp2 = new Point(p0.X + (p1.X - p0.X) / 2.0, p1.Y);
                    ctx.BezierTo(cp1, cp2, p1, isStroked: false, isSmoothJoin: true);
                }

                ctx.LineTo(new Point(points[n - 1].X, h), isStroked: false, isSmoothJoin: true);
            }
            areaGeom.Freeze();

            var areaBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(170, 72, 36, 22), 0.0));
            areaBrush.GradientStops.Add(new GradientStop(Color.FromArgb(20, 24, 18, 16), 1.0));

            var areaPath = new Path
            {
                Data = areaGeom,
                Fill = areaBrush
            };
            _hourlyChartCanvas.Children.Add(areaPath);

            // Curve Line Path
            var lineGeom = new StreamGeometry();
            using (var ctx = lineGeom.Open())
            {
                ctx.BeginFigure(points[0], isFilled: false, isClosed: false);
                for (int i = 0; i < n - 1; i++)
                {
                    var p0 = points[i];
                    var p1 = points[i + 1];
                    var cp1 = new Point(p0.X + (p1.X - p0.X) / 2.0, p0.Y);
                    var cp2 = new Point(p0.X + (p1.X - p0.X) / 2.0, p1.Y);
                    ctx.BezierTo(cp1, cp2, p1, isStroked: true, isSmoothJoin: true);
                }
            }
            lineGeom.Freeze();

            var linePath = new Path
            {
                Data = lineGeom,
                Stroke = Brushes.White,
                StrokeThickness = 2.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _hourlyChartCanvas.Children.Add(linePath);

            // Dots on points
            for (int i = 0; i < n; i++)
            {
                var dot = new Ellipse
                {
                    Width = 5,
                    Height = 5,
                    Fill = Brushes.White
                };
                Canvas.SetLeft(dot, points[i].X - 2.5);
                Canvas.SetTop(dot, points[i].Y - 2.5);
                _hourlyChartCanvas.Children.Add(dot);
            }
        }

        private UIElement CreateDailyRow(DailyForecast day, double globalMin, double globalMax, double currentTemp)
        {
            var dock = new DockPanel { Margin = new Thickness(0, 3, 0, 3) };

            // Day Name
            var dayText = new TextBlock
            {
                Text = day.DayName,
                FontSize = 12,
                FontWeight = string.Equals(day.DayName, "Today", StringComparison.OrdinalIgnoreCase) ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = Brushes.White,
                Width = 40,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(dayText, Dock.Left);
            dock.Children.Add(dayText);

            // Weather Icon
            var iconBox = new Border
            {
                Width = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = WeatherIcons.CreateIcon(day.IconType, size: 16)
            };
            DockPanel.SetDock(iconBox, Dock.Left);
            dock.Children.Add(iconBox);

            // Max Temp (Right)
            var maxText = new TextBlock
            {
                Text = _service.FormatTemp(day.MaxTemp),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Width = 28,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(maxText, Dock.Right);
            dock.Children.Add(maxText);

            // Range Bar
            bool isToday = string.Equals(day.DayName, "Today", StringComparison.OrdinalIgnoreCase);
            var rangeBar = CreateRangeBar(day.MinTemp, day.MaxTemp, globalMin, globalMax, isToday, currentTemp);
            DockPanel.SetDock(rangeBar, Dock.Right);
            dock.Children.Add(rangeBar);

            // Min Temp (Left of Range Bar)
            var minText = new TextBlock
            {
                Text = _service.FormatTemp(day.MinTemp),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Width = 28,
                TextAlignment = TextAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(minText, Dock.Right);
            dock.Children.Add(minText);

            return dock;
        }

        private Grid CreateRangeBar(double dayMin, double dayMax, double globalMin, double globalMax, bool isToday, double currentTemp)
        {
            double barWidth = 48.0;
            var grid = new Grid
            {
                Width = barWidth,
                Height = 4.5,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 6, 0)
            };

            // Background Track
            var track = new Border
            {
                Height = 4.5,
                CornerRadius = new CornerRadius(2.25),
                Background = new SolidColorBrush(Color.FromRgb(42, 42, 50))
            };
            grid.Children.Add(track);

            // Range Pill
            double span = Math.Max(1.0, globalMax - globalMin);
            double leftFrac = Math.Clamp((dayMin - globalMin) / span, 0.0, 1.0);
            double rightFrac = Math.Clamp((dayMax - globalMin) / span, 0.0, 1.0);

            double leftMargin = leftFrac * barWidth;
            double rightMargin = (1.0 - rightFrac) * barWidth;

            var fillPill = new Border
            {
                Height = 4.5,
                CornerRadius = new CornerRadius(2.25),
                Margin = new Thickness(leftMargin, 0, rightMargin, 0)
            };

            var grad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5)
            };
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(100, 210, 255), 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 159, 10), 1.0));
            fillPill.Background = grad;

            grid.Children.Add(fillPill);

            // Today current temp dot
            if (isToday)
            {
                double curFrac = Math.Clamp((currentTemp - globalMin) / span, 0.0, 1.0);
                double dotX = curFrac * (barWidth - 5.5);

                var dot = new Ellipse
                {
                    Width = 5.5,
                    Height = 5.5,
                    Fill = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(dotX, 0, 0, 0)
                };
                grid.Children.Add(dot);
            }

            return grid;
        }
        #endregion
    }
}
