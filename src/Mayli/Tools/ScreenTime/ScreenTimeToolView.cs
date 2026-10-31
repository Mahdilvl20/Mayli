using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;

namespace WindowsBuddy.Tools.ScreenTime
{
    public sealed class ScreenTimeToolView : UserControl
    {
        private readonly ScreenTimeService _service = ScreenTimeService.Instance;

        // Today Card Controls
        private TextBlock _todayTotalText;
        private TextBlock _limitLeftText;
        private TextBlock _limitLabelText;
        private TextBlock _appsCountText;
        private Path _limitArcPath;

        // Last 7 Days Card Controls
        private TextBlock _weeklyAvgText;
        private UniformGrid _weekGrid;

        // By App Card Controls
        private StackPanel _appListStack;
        private TextBlock _refreshBtn;

        // Edit Limit Modal Overlay
        private Grid _limitModalOverlay;

        public ScreenTimeToolView()
        {
            var rootGrid = new Grid { Margin = new Thickness(4, 2, 4, 4) };
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) }); // Left column
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) }); // Right column

            // Left Column: Responsive Grid with 2 Star Rows
            var leftGrid = new Grid { Margin = new Thickness(0, 0, 6, 0) };
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.35, GridUnitType.Star) });

            var todayCard = BuildTodayCard();
            Grid.SetRow(todayCard, 0);
            leftGrid.Children.Add(todayCard);

            var weekCard = BuildWeekCard();
            Grid.SetRow(weekCard, 1);
            leftGrid.Children.Add(weekCard);

            Grid.SetColumn(leftGrid, 0);
            rootGrid.Children.Add(leftGrid);

            // Right Column: Card 3 By App
            var byAppCard = BuildByAppCard();
            Grid.SetColumn(byAppCard, 1);
            rootGrid.Children.Add(byAppCard);

            // Modal Overlay for Editing Daily Limit
            _limitModalOverlay = BuildLimitModalOverlay();
            Grid.SetColumnSpan(_limitModalOverlay, 2);
            rootGrid.Children.Add(_limitModalOverlay);

            Content = rootGrid;

            // Wire Service Updates
            _service.UsageUpdated += OnUsageUpdated;
            Loaded += (_, _) => RefreshUI();
            Unloaded += (_, _) => _service.UsageUpdated -= OnUsageUpdated;
        }

        private Border BuildTodayCard()
        {
            var card = new Border
            {
                MinHeight = 85,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var dock = new DockPanel();

            // Header: 📊 Today
            var header = new TextBlock
            {
                Text = "📊  Today",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            // 3-Metric Columns Grid
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });

            // Metric 1: Screen Time
            var m1 = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _todayTotalText = new TextBlock
            {
                Text = _service.TodayTotalFormatted,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            m1.Children.Add(_todayTotalText);

            var l1 = new TextBlock
            {
                Text = "Screen time",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            m1.Children.Add(l1);
            Grid.SetColumn(m1, 0);
            grid.Children.Add(m1);

            // Metric 2: Progress Ring + 5h 2m left of 7h limit ✎
            var m2 = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Circular Ring
            var ringCanvas = new Grid
            {
                Width = 44,
                Height = 44,
                Margin = new Thickness(0, 0, 10, 0)
            };

            var bgCircle = new Ellipse
            {
                Width = 40,
                Height = 40,
                Stroke = new SolidColorBrush(Color.FromRgb(36, 36, 44)),
                StrokeThickness = 3
            };
            ringCanvas.Children.Add(bgCircle);

            _limitArcPath = new Path
            {
                Stroke = Brushes.White,
                StrokeThickness = 3,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            ringCanvas.Children.Add(_limitArcPath);
            m2.Children.Add(ringCanvas);

            var m2TextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _limitLeftText = new TextBlock
            {
                Text = _service.LimitRemainingFormatted,
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            m2TextStack.Children.Add(_limitLeftText);

            var limitSubRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            _limitLabelText = new TextBlock
            {
                Text = $"of {_service.LimitFormatted} ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            limitSubRow.Children.Add(_limitLabelText);

            var pencilBorder = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Change Daily Limit",
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "✎",
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            pencilBorder.MouseEnter += (_, _) =>
            {
                pencilBorder.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                if (pencilBorder.Child is TextBlock tb) tb.Foreground = Brushes.White;
            };
            pencilBorder.MouseLeave += (_, _) =>
            {
                pencilBorder.Background = Brushes.Transparent;
                if (pencilBorder.Child is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190));
            };
            pencilBorder.MouseLeftButtonUp += (_, _) => ShowLimitPicker();
            limitSubRow.Children.Add(pencilBorder);

            m2TextStack.Children.Add(limitSubRow);
            m2.Children.Add(m2TextStack);

            Grid.SetColumn(m2, 1);
            grid.Children.Add(m2);

            // Metric 3: Apps Used
            var m3 = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 8, 0) };
            _appsCountText = new TextBlock
            {
                Text = $"{_service.AppsUsedToday}",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            m3.Children.Add(_appsCountText);

            var l3 = new TextBlock
            {
                Text = "Apps used",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 2, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            m3.Children.Add(l3);

            Grid.SetColumn(m3, 2);
            grid.Children.Add(m3);

            dock.Children.Add(grid);
            card.Child = dock;
            return card;
        }

        private Border BuildWeekCard()
        {
            var card = new Border
            {
                MinHeight = 110,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var dock = new DockPanel();

            // Header: 📅 Last 7 days (Left) and avg 1h 57m (Right)
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var title = new TextBlock
            {
                Text = "📅  Last 7 days",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            headerDock.Children.Add(title);

            _weeklyAvgText = new TextBlock
            {
                Text = $"avg {ScreenTimeService.FormatDuration(_service.GetWeeklyAverageSeconds())}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_weeklyAvgText, Dock.Right);
            headerDock.Children.Add(_weeklyAvgText);

            DockPanel.SetDock(headerDock, Dock.Top);
            dock.Children.Add(headerDock);

            // 7 Days Chart Container
            _weekGrid = new UniformGrid { Columns = 7 };
            dock.Children.Add(_weekGrid);

            card.Child = dock;
            return card;
        }

        private Border BuildByAppCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var dock = new DockPanel();

            // Header: 📊 By app (Left) and ⟳ Refresh (Right)
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var title = new TextBlock
            {
                Text = "📊  By app",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(title, Dock.Left);
            headerDock.Children.Add(title);

            var refreshBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Refresh App Usage",
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _refreshBtn = new TextBlock
            {
                Text = "⟳",
                FontSize = 13.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            refreshBorder.Child = _refreshBtn;
            refreshBorder.MouseEnter += (_, _) =>
            {
                refreshBorder.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                _refreshBtn.Foreground = Brushes.White;
            };
            refreshBorder.MouseLeave += (_, _) =>
            {
                refreshBorder.Background = Brushes.Transparent;
                _refreshBtn.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            };
            refreshBorder.MouseLeftButtonUp += (_, _) => RefreshUI();
            DockPanel.SetDock(refreshBorder, Dock.Right);
            headerDock.Children.Add(refreshBorder);

            DockPanel.SetDock(headerDock, Dock.Top);
            dock.Children.Add(headerDock);

            _appListStack = new StackPanel();
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _appListStack
            };
            dock.Children.Add(scroll);

            card.Child = dock;
            return card;
        }

        private Grid BuildLimitModalOverlay()
        {
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)),
                Visibility = Visibility.Collapsed
            };

            var dialog = new Border
            {
                Width = 280,
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "Set Daily Screen Limit",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 12)
            });

            var chipsWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            int[] hours = { 4, 5, 6, 7, 8, 10 };
            foreach (int h in hours)
            {
                int sec = h * 3600;
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 6, 12, 6),
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand,
                    Background = _service.DailyLimitSeconds == sec ? new SolidColorBrush(Color.FromRgb(230, 74, 25)) : new SolidColorBrush(Color.FromRgb(36, 36, 46))
                };
                chip.Child = new TextBlock
                {
                    Text = $"{h}h",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                chip.MouseLeftButtonUp += (_, _) =>
                {
                    _service.DailyLimitSeconds = sec;
                    overlay.Visibility = Visibility.Collapsed;
                    RefreshUI();
                };
                chipsWrap.Children.Add(chip);
            }
            stack.Children.Add(chipsWrap);

            var closeBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 50)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            closeBtn.Child = new TextBlock { Text = "Cancel", FontSize = 11, Foreground = Brushes.White };
            closeBtn.MouseLeftButtonUp += (_, _) => overlay.Visibility = Visibility.Collapsed;
            stack.Children.Add(closeBtn);

            dialog.Child = stack;
            overlay.Children.Add(dialog);
            return overlay;
        }

        private void ShowLimitPicker()
        {
            _limitModalOverlay.Visibility = Visibility.Visible;
        }

        private void RefreshUI()
        {
            _todayTotalText.Text = _service.TodayTotalFormatted;
            _limitLeftText.Text = _service.LimitRemainingFormatted;
            _limitLabelText.Text = $"of {_service.LimitFormatted} ";
            _appsCountText.Text = $"{_service.AppsUsedToday}";
            _weeklyAvgText.Text = $"avg {ScreenTimeService.FormatDuration(_service.GetWeeklyAverageSeconds())}";

            UpdateProgressRing(_service.LimitProgressFraction);
            PopulateWeekChart();
            PopulateAppList();
        }

        private void UpdateProgressRing(double fraction)
        {
            if (_limitArcPath == null) return;

            // Geometry arc around center (22, 22), radius 20
            double radius = 20.0;
            Point center = new Point(22, 22);
            double angle = Math.Clamp(fraction, 0.0, 0.9999) * 360.0;

            if (angle <= 0.01)
            {
                _limitArcPath.Data = null;
                return;
            }

            double startAngle = -90.0;
            double endAngle = startAngle + angle;

            Point startPoint = PointOnCircle(center, radius, startAngle);
            Point endPoint = PointOnCircle(center, radius, endAngle);

            bool isLargeArc = angle > 180.0;

            var geom = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = startPoint,
                IsClosed = false
            };
            figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, isLargeArc, SweepDirection.Clockwise, true));
            geom.Figures.Add(figure);
            _limitArcPath.Data = geom;
        }

        private static Point PointOnCircle(Point center, double radius, double angleInDegrees)
        {
            double rad = angleInDegrees * (Math.PI / 180.0);
            return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
        }

        private void PopulateWeekChart()
        {
            if (_weekGrid == null) return;
            _weekGrid.Children.Clear();

            var days = _service.GetRolling7Days();
            foreach (var d in days)
            {
                var col = new Grid { VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center };
                col.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Bar or marker
                col.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Day name label

                var contentStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Bottom,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                if (d.TotalSeconds > 0)
                {
                    // Duration label above bar
                    var durLabel = new TextBlock
                    {
                        Text = d.DurationFormatted,
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    contentStack.Children.Add(durLabel);

                    // Solid White Rounded Pill Bar
                    double barH = Math.Clamp(d.HeightFraction * 54.0 + 8.0, 10.0, 62.0);
                    var bar = new Border
                    {
                        Width = 24,
                        Height = barH,
                        CornerRadius = new CornerRadius(6),
                        Background = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    contentStack.Children.Add(bar);
                }
                else
                {
                    // Horizontal small dash pill
                    var dash = new Border
                    {
                        Width = 14,
                        Height = 2.5,
                        CornerRadius = new CornerRadius(1.25),
                        Background = new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 6)
                    };
                    contentStack.Children.Add(dash);
                }

                Grid.SetRow(contentStack, 0);
                col.Children.Add(contentStack);

                // Day Label below
                var dayLabel = new TextBlock
                {
                    Text = d.DayName,
                    FontSize = 10.5,
                    FontWeight = d.IsToday ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = d.IsToday ? Brushes.White : new SolidColorBrush(Color.FromRgb(120, 120, 130)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 6, 0, 0)
                };
                Grid.SetRow(dayLabel, 1);
                col.Children.Add(dayLabel);

                _weekGrid.Children.Add(col);
            }
        }

        private void PopulateAppList()
        {
            if (_appListStack == null) return;
            _appListStack.Children.Clear();

            var apps = _service.GetTopAppsToday();
            foreach (var app in apps)
            {
                var row = new Grid
                {
                    Height = 44,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Icon
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title + bar
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Time + pct

                // App Icon
                var iconContainer = new Border
                {
                    Width = 30,
                    Height = 30,
                    CornerRadius = new CornerRadius(7),
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Child = new Image
                    {
                        Source = app.IconSource,
                        Width = 26,
                        Height = 26
                    }
                };
                Grid.SetColumn(iconContainer, 0);
                row.Children.Add(iconContainer);

                // Center Title + Progress Track
                var centerStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var appNameText = new TextBlock
                {
                    Text = app.Name,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 0, 0, 4)
                };
                centerStack.Children.Add(appNameText);

                // Responsive Horizontal Track Bar
                var trackBar = new Grid
                {
                    Height = 3,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                trackBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.5, app.Percentage), GridUnitType.Star) });
                trackBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(Math.Max(0.5, 100.0 - app.Percentage), GridUnitType.Star) });

                var trackBg = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(38, 38, 46)),
                    CornerRadius = new CornerRadius(1.5)
                };
                Grid.SetColumnSpan(trackBg, 2);
                trackBar.Children.Add(trackBg);

                var fillBar = new Border
                {
                    Background = Brushes.White,
                    CornerRadius = new CornerRadius(1.5)
                };
                Grid.SetColumn(fillBar, 0);
                trackBar.Children.Add(fillBar);
                centerStack.Children.Add(trackBar);

                Grid.SetColumn(centerStack, 1);
                row.Children.Add(centerStack);

                // Right Duration & Percentage
                var rightStack = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(8, 0, 0, 0)
                };

                var timeText = new TextBlock
                {
                    Text = app.DurationFormatted,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                rightStack.Children.Add(timeText);

                var pctText = new TextBlock
                {
                    Text = app.PercentageFormatted,
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 130)),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                rightStack.Children.Add(pctText);

                Grid.SetColumn(rightStack, 2);
                row.Children.Add(rightStack);

                _appListStack.Children.Add(row);
            }
        }

        private void OnUsageUpdated()
        {
            Dispatcher.InvokeAsync(RefreshUI);
        }
    }
}
