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
using WindowsBuddy.Services;

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

        // Chart Card Controls (7 Days vs 30 Days)
        private bool _is30DaysMode = false;
        private Border _tab7DaysBtn;
        private Border _tab30DaysBtn;
        private TextBlock _tab7DaysText;
        private TextBlock _tab30DaysText;
        private TextBlock _chartTitleText;
        private TextBlock _weeklyAvgText;
        private UniformGrid _weekGrid;
        private Grid _monthGrid;
        private Grid _monthHeatmapTilesGrid;
        private TextBlock _monthDetailText;
        private Border _selectedMonthTileBorder;
        private string _selectedMonthDateStr;

        // By App Card Controls
        private StackPanel _appListStack;
        private TextBlock _refreshBtn;

        // Edit Limit Modal Overlay
        private Grid _limitModalOverlay;

        public ScreenTimeToolView()
        {
            FlowDirection = LocalizationService.CurrentFlowDirection;
            var rootGrid = new Grid { Margin = new Thickness(4, 2, 4, 4) };
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.8, GridUnitType.Star) }); // Left column
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) }); // Right column

            // Left Column: Auto height for Today card, remaining Star height for Chart card
            var leftGrid = new Grid { Margin = new Thickness(0, 0, 6, 0) };
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1.0, GridUnitType.Star) });

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
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                MinHeight = 80,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var dock = new DockPanel();

            // Header: 📊 Today
            var header = new TextBlock
            {
                Text = "📊  " + LocalizationService.T("screentime.today", "Today"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 4)
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
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            m1.Children.Add(_todayTotalText);

            var l1 = new TextBlock
            {
                Text = LocalizationService.T("screentime.screen_time", "Screen time"),
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                Stroke = CapsuleTheme.Brush(p.Surface3),
                StrokeThickness = 3
            };
            ringCanvas.Children.Add(bgCircle);

            _limitArcPath = new Path
            {
                Stroke = CapsuleTheme.Brush(p.Accent),
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
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            m2TextStack.Children.Add(_limitLeftText);

            var limitSubRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            _limitLabelText = new TextBlock
            {
                Text = $"of {_service.LimitFormatted} ",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            pencilBorder.MouseEnter += (_, _) =>
            {
                pencilBorder.Background = CapsuleTheme.Brush(p.Surface3);
                if (pencilBorder.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Text);
            };
            pencilBorder.MouseLeave += (_, _) =>
            {
                pencilBorder.Background = Brushes.Transparent;
                if (pencilBorder.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Text2);
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
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            m3.Children.Add(_appsCountText);

            var l3 = new TextBlock
            {
                Text = LocalizationService.T("screentime.apps_used", "Apps used"),
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                MinHeight = 110,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 8, 12, 8)
            };

            var dock = new DockPanel();

            // Header: 📅 Last 7 days / 30 days (Left) and avg + switcher (Right)
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            _chartTitleText = new TextBlock
            {
                Text = "📅  " + LocalizationService.T("screentime.last7days", "Last 7 days"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_chartTitleText, Dock.Left);
            headerDock.Children.Add(_chartTitleText);

            var rightHeaderStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _weeklyAvgText = new TextBlock
            {
                Text = string.Format(LocalizationService.T("screentime.avg_fmt", "avg {0}"), ScreenTimeService.FormatDuration(_service.GetWeeklyAverageSeconds())),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            rightHeaderStack.Children.Add(_weeklyAvgText);

            var switcherPill = BuildTimeRangeSwitcher();
            rightHeaderStack.Children.Add(switcherPill);

            DockPanel.SetDock(rightHeaderStack, Dock.Right);
            headerDock.Children.Add(rightHeaderStack);

            DockPanel.SetDock(headerDock, Dock.Top);
            dock.Children.Add(headerDock);

            // Chart area container hosting both 7-day bar chart and 30-day heatmap
            var chartContentHost = new Grid { VerticalAlignment = VerticalAlignment.Stretch };

            // 1. 7 Days Chart Container
            _weekGrid = new UniformGrid { Columns = 7, VerticalAlignment = VerticalAlignment.Stretch };
            chartContentHost.Children.Add(_weekGrid);

            // 2. 30 Days Heatmap Container
            _monthGrid = Build30DaysHeatmapView();
            _monthGrid.Visibility = Visibility.Collapsed;
            chartContentHost.Children.Add(_monthGrid);

            dock.Children.Add(chartContentHost);

            card.Child = dock;
            return card;
        }

        private Border BuildTimeRangeSwitcher()
        {
            var p = CapsuleTheme.Current;
            var pill = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(2),
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            _tab7DaysBtn = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 1, 8, 1),
                Background = CapsuleTheme.Brush(p.Accent),
                Cursor = Cursors.Hand
            };
            _tab7DaysText = new TextBlock
            {
                Text = LocalizationService.T("screentime.tab_7d", "7 Days"),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tab7DaysBtn.Child = _tab7DaysText;
            _tab7DaysBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                SetTimeRangeMode(is30Days: false);
            };
            stack.Children.Add(_tab7DaysBtn);

            _tab30DaysBtn = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 1, 8, 1),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };
            _tab30DaysText = new TextBlock
            {
                Text = LocalizationService.T("screentime.tab_30d", "30 Days"),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            _tab30DaysBtn.Child = _tab30DaysText;
            _tab30DaysBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                SetTimeRangeMode(is30Days: true);
            };
            stack.Children.Add(_tab30DaysBtn);

            pill.Child = stack;
            return pill;
        }

        private void SetTimeRangeMode(bool is30Days)
        {
            _is30DaysMode = is30Days;
            _selectedMonthDateStr = null;
            var p = CapsuleTheme.Current;

            if (_is30DaysMode)
            {
                _tab7DaysBtn.Background = Brushes.Transparent;
                _tab7DaysText.Foreground = CapsuleTheme.Brush(p.Text2);
                _tab7DaysText.FontWeight = FontWeights.Medium;

                _tab30DaysBtn.Background = CapsuleTheme.Brush(p.Accent);
                _tab30DaysText.Foreground = CapsuleTheme.Brush(p.AccentFg);
                _tab30DaysText.FontWeight = FontWeights.SemiBold;

                _chartTitleText.Text = "📅  " + LocalizationService.T("screentime.last30days", "Last 30 days");
                _weeklyAvgText.Text = string.Format(LocalizationService.T("screentime.avg_fmt", "avg {0}"), ScreenTimeService.FormatDuration(_service.GetMonthlyAverageSeconds()));

                _weekGrid.Visibility = Visibility.Collapsed;
                _monthGrid.Visibility = Visibility.Visible;
                PopulateMonthHeatmap();
            }
            else
            {
                _tab7DaysBtn.Background = CapsuleTheme.Brush(p.Accent);
                _tab7DaysText.Foreground = CapsuleTheme.Brush(p.AccentFg);
                _tab7DaysText.FontWeight = FontWeights.SemiBold;

                _tab30DaysBtn.Background = Brushes.Transparent;
                _tab30DaysText.Foreground = CapsuleTheme.Brush(p.Text2);
                _tab30DaysText.FontWeight = FontWeights.Medium;

                _chartTitleText.Text = "📅  " + LocalizationService.T("screentime.last7days", "Last 7 days");
                _weeklyAvgText.Text = string.Format(LocalizationService.T("screentime.avg_fmt", "avg {0}"), ScreenTimeService.FormatDuration(_service.GetWeeklyAverageSeconds()));

                _monthGrid.Visibility = Visibility.Collapsed;
                _weekGrid.Visibility = Visibility.Visible;
                PopulateWeekChart();
            }
        }

        private Grid Build30DaysHeatmapView()
        {
            var p = CapsuleTheme.Current;
            var root = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _monthHeatmapTilesGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(_monthHeatmapTilesGrid, 0);
            root.Children.Add(_monthHeatmapTilesGrid);

            var footerDock = new DockPanel
            {
                Margin = new Thickness(2, 4, 2, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            // GitHub Legend: Less ▢ ▢ ▢ ▢ ▢ More
            var legendStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            legendStack.Children.Add(new TextBlock
            {
                Text = "Less",
                FontSize = 9.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            });

            Color accent = p.Accent;
            Color[] legendColors = new[]
            {
                p.IsLight ? Color.FromArgb(35, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255),
                Color.FromArgb(85, accent.R, accent.G, accent.B),
                Color.FromArgb(145, accent.R, accent.G, accent.B),
                Color.FromArgb(200, accent.R, accent.G, accent.B),
                Color.FromArgb(255, accent.R, accent.G, accent.B)
            };

            foreach (var col in legendColors)
            {
                legendStack.Children.Add(new Border
                {
                    Width = 9,
                    Height = 9,
                    CornerRadius = new CornerRadius(2),
                    Background = new SolidColorBrush(col),
                    Margin = new Thickness(1.5, 0, 1.5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
            }

            legendStack.Children.Add(new TextBlock
            {
                Text = "More",
                FontSize = 9.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(3, 0, 0, 0)
            });

            DockPanel.SetDock(legendStack, Dock.Right);
            footerDock.Children.Add(legendStack);

            _monthDetailText = new TextBlock
            {
                Text = LocalizationService.T("screentime.click_inspect", "Click any square to inspect"),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            footerDock.Children.Add(_monthDetailText);

            Grid.SetRow(footerDock, 1);
            root.Children.Add(footerDock);

            return root;
        }

        private void PopulateMonthHeatmap()
        {
            if (_monthHeatmapTilesGrid == null) return;
            _monthHeatmapTilesGrid.Children.Clear();
            _monthHeatmapTilesGrid.RowDefinitions.Clear();
            _monthHeatmapTilesGrid.ColumnDefinitions.Clear();

            var days = _service.GetRolling30Days();
            var p = CapsuleTheme.Current;
            Color accent = p.Accent;

            const int cols = 10;
            const int rows = 3;

            for (int c = 0; c < cols; c++)
            {
                _monthHeatmapTilesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            }
            for (int r = 0; r < rows; r++)
            {
                _monthHeatmapTilesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            }

            _selectedMonthTileBorder = null;
            string targetDateStr = _selectedMonthDateStr ?? DateTime.Today.ToString("yyyy-MM-dd");
            Border targetTile = null;
            DayBarData targetData = null;
            Border todayTile = null;
            DayBarData todayData = null;

            for (int i = 0; i < days.Count; i++)
            {
                var d = days[i];
                int colIdx = i % cols;
                int rowIdx = i / cols;

                Color tileColor;
                if (d.TotalSeconds <= 0)
                {
                    tileColor = p.IsLight ? Color.FromArgb(35, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255);
                }
                else if (d.TotalSeconds < 3600)
                {
                    tileColor = Color.FromArgb(90, accent.R, accent.G, accent.B);
                }
                else if (d.TotalSeconds < 3600 * 3)
                {
                    tileColor = Color.FromArgb(150, accent.R, accent.G, accent.B);
                }
                else if (d.TotalSeconds < 3600 * 6)
                {
                    tileColor = Color.FromArgb(205, accent.R, accent.G, accent.B);
                }
                else
                {
                    tileColor = Color.FromArgb(255, accent.R, accent.G, accent.B);
                }

                var tile = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(tileColor),
                    BorderBrush = d.IsToday ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(d.IsToday ? 1.5 : 1),
                    Margin = new Thickness(2.5),
                    Cursor = Cursors.Hand,
                    ToolTip = $"{d.Date:ddd, MMM d}: {(d.TotalSeconds > 0 ? d.DurationFormatted : LocalizationService.T("screentime.no_activity", "No activity"))}"
                };

                var dayNum = new TextBlock
                {
                    Text = d.Date.Day.ToString(),
                    FontSize = 8.5,
                    FontWeight = d.IsToday ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = d.TotalSeconds >= 3600 * 3 ? Brushes.White : CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = d.IsToday ? 1.0 : 0.75
                };
                tile.Child = dayNum;

                var currentDay = d;
                var currentTile = tile;

                tile.MouseEnter += (_, _) =>
                {
                    if (_selectedMonthTileBorder != currentTile)
                    {
                        currentTile.BorderBrush = CapsuleTheme.Brush(p.Accent);
                    }
                };
                tile.MouseLeave += (_, _) =>
                {
                    if (_selectedMonthTileBorder != currentTile)
                    {
                        currentTile.BorderBrush = currentDay.IsToday ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Border);
                    }
                };

                tile.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    SelectMonthTile(currentTile, currentDay, userSelected: true);
                };

                Grid.SetColumn(tile, colIdx);
                Grid.SetRow(tile, rowIdx);
                _monthHeatmapTilesGrid.Children.Add(tile);

                string curDateStr = d.Date.ToString("yyyy-MM-dd");
                if (curDateStr == targetDateStr)
                {
                    targetTile = tile;
                    targetData = d;
                }
                if (d.IsToday)
                {
                    todayTile = tile;
                    todayData = d;
                }
            }

            if (targetTile != null && targetData != null)
            {
                SelectMonthTile(targetTile, targetData, userSelected: false);
            }
            else if (todayTile != null && todayData != null)
            {
                SelectMonthTile(todayTile, todayData, userSelected: false);
            }
        }

        private void SelectMonthTile(Border tile, DayBarData d, bool userSelected = false)
        {
            var p = CapsuleTheme.Current;
            if (userSelected)
            {
                _selectedMonthDateStr = d.Date.ToString("yyyy-MM-dd");
            }

            if (_selectedMonthTileBorder != null && _selectedMonthTileBorder != tile)
            {
                _selectedMonthTileBorder.BorderThickness = new Thickness(1);
                _selectedMonthTileBorder.BorderBrush = CapsuleTheme.Brush(p.Border);
            }

            _selectedMonthTileBorder = tile;
            tile.BorderThickness = new Thickness(2);
            tile.BorderBrush = CapsuleTheme.Brush(p.Text);

            string dateStr = d.Date.ToString("ddd, MMM d");
            string durStr = d.TotalSeconds > 0 ? d.DurationFormatted : LocalizationService.T("screentime.no_activity", "No activity");
            _monthDetailText.Text = $"📌 {dateStr}  ·  {durStr}";
            _monthDetailText.Foreground = CapsuleTheme.Brush(p.Text);
        }

        private Border BuildByAppCard()
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10)
            };

            var dock = new DockPanel();

            // Header: 📊 By app (Left) and ⟳ Refresh (Right)
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var title = new TextBlock
            {
                Text = "📊  " + LocalizationService.T("screentime.by_app", "By app"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                ToolTip = LocalizationService.T("screentime.refresh_tooltip", "Refresh App Usage"),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            _refreshBtn = new TextBlock
            {
                Text = "⟳",
                FontSize = 13.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            refreshBorder.Child = _refreshBtn;
            refreshBorder.MouseEnter += (_, _) =>
            {
                refreshBorder.Background = CapsuleTheme.Brush(p.Surface3);
                _refreshBtn.Foreground = CapsuleTheme.Brush(p.Text);
            };
            refreshBorder.MouseLeave += (_, _) =>
            {
                refreshBorder.Background = Brushes.Transparent;
                _refreshBtn.Foreground = CapsuleTheme.Brush(p.Text2);
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
            var p = CapsuleTheme.Current;
            var overlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(180, p.Bg.R, p.Bg.G, p.Bg.B)),
                Visibility = Visibility.Collapsed
            };

            var dialog = new Border
            {
                Width = 280,
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.BorderStrong),
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
                Foreground = CapsuleTheme.Brush(p.Text),
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
                    Background = _service.DailyLimitSeconds == sec ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Surface2)
                };
                chip.Child = new TextBlock
                {
                    Text = $"{h}h",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = _service.DailyLimitSeconds == sec ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text)
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
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            closeBtn.Child = new TextBlock { Text = "Cancel", FontSize = 11, Foreground = CapsuleTheme.Brush(p.Text) };
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

        private DateTime _lastFullChartRefreshUtc = DateTime.MinValue;

        private void RefreshUI()
        {
            _lastFullChartRefreshUtc = DateTime.UtcNow;
            _todayTotalText.Text = _service.TodayTotalFormatted;
            _limitLeftText.Text = _service.LimitRemainingFormatted;
            _limitLabelText.Text = $"of {_service.LimitFormatted} ";
            _appsCountText.Text = $"{_service.AppsUsedToday}";

            int avgSec = _is30DaysMode ? _service.GetMonthlyAverageSeconds() : _service.GetWeeklyAverageSeconds();
            _weeklyAvgText.Text = string.Format(LocalizationService.T("screentime.avg_fmt", "avg {0}"), ScreenTimeService.FormatDuration(avgSec));

            UpdateProgressRing(_service.LimitProgressFraction);
            if (_is30DaysMode) PopulateMonthHeatmap();
            else PopulateWeekChart();
            PopulateAppList();
        }

        private void RefreshUIIncremental()
        {
            if (_todayTotalText == null) return;

            _todayTotalText.Text = _service.TodayTotalFormatted;
            _limitLeftText.Text = _service.LimitRemainingFormatted;
            _limitLabelText.Text = $"of {_service.LimitFormatted} ";
            _appsCountText.Text = $"{_service.AppsUsedToday}";

            int avgSec = _is30DaysMode ? _service.GetMonthlyAverageSeconds() : _service.GetWeeklyAverageSeconds();
            _weeklyAvgText.Text = string.Format(LocalizationService.T("screentime.avg_fmt", "avg {0}"), ScreenTimeService.FormatDuration(avgSec));

            UpdateProgressRing(_service.LimitProgressFraction);

            // Throttle heavy chart / app-list visual tree reconstructions to every 25 seconds
            if ((DateTime.UtcNow - _lastFullChartRefreshUtc).TotalSeconds >= 25.0)
            {
                _lastFullChartRefreshUtc = DateTime.UtcNow;
                if (_is30DaysMode) PopulateMonthHeatmap();
                else PopulateWeekChart();
                PopulateAppList();
            }
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

                var p = CapsuleTheme.Current;
                if (d.TotalSeconds > 0)
                {
                    // Duration label safely above bar without clipping into header
                    var durLabel = new TextBlock
                    {
                        Text = d.DurationFormatted,
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = CapsuleTheme.Brush(p.Text),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 2)
                    };
                    contentStack.Children.Add(durLabel);

                    // Rounded Pill Bar safely scaled within available height
                    double barH = Math.Clamp(d.HeightFraction * 36.0 + 8.0, 8.0, 44.0);
                    var bar = new Border
                    {
                        Width = 24,
                        Height = barH,
                        CornerRadius = new CornerRadius(6),
                        Background = CapsuleTheme.Brush(p.Accent),
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
                        Background = CapsuleTheme.Brush(p.Surface3),
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
                    FontSize = 9.5,
                    FontWeight = d.IsToday ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = d.IsToday ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
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
            var p = CapsuleTheme.Current;

            var apps = _service.GetTopAppsToday();
            if (apps.Count == 0)
            {
                var emptyNotice = new TextBlock
                {
                    Text = LocalizationService.T("screentime.no_activity", "No app activity recorded yet today."),
                    FontSize = 11,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    Margin = new Thickness(0, 18, 0, 0),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                _appListStack.Children.Add(emptyNotice);
                return;
            }

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
                    Foreground = CapsuleTheme.Brush(p.Text),
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
                    Background = CapsuleTheme.Brush(p.Surface2),
                    CornerRadius = new CornerRadius(1.5)
                };
                Grid.SetColumnSpan(trackBg, 2);
                trackBar.Children.Add(trackBg);

                var fillBar = new Border
                {
                    Background = CapsuleTheme.Brush(p.Accent),
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
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                rightStack.Children.Add(timeText);

                var pctText = new TextBlock
                {
                    Text = app.PercentageFormatted,
                    FontSize = 10.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
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
            Dispatcher.InvokeAsync(RefreshUIIncremental);
        }
    }
}
