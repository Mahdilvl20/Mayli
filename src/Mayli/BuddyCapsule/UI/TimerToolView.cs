using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.Tools.Timer;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.UI
{
    public sealed class TimerToolView : UserControl
    {
        public const double GridW = 728.0;
        public const double GridH = 242.0;

        private readonly Grid _rootGrid;

        // Mode Rows (Column 0)
        private readonly Border _rowPomo;
        private readonly Border _rowCountdown;
        private readonly Border _rowStopwatch;
        private readonly Border _rowWater;
        private readonly TextBlock _subPomo;
        private readonly TextBlock _subCountdown;
        private readonly TextBlock _subStopwatch;
        private readonly TextBlock _subWater;

        // Center Ring (Column 1)
        private readonly Canvas _ringCanvas;
        private readonly System.Windows.Shapes.Path _ringTrack;
        private readonly System.Windows.Shapes.Path _ringProgress;
        private readonly TextBlock _centerTimeText;
        private readonly TextBlock _centerPhaseText;

        // Right Controls (Column 2)
        private readonly TextBlock _cycleHeaderText;
        private readonly StackPanel _cycleDotsPanel;
        private readonly TextBlock _cycleSubText;

        private readonly Border _resetBtn;
        private readonly Border _playBtn;
        private readonly TextBlock _playIcon;
        private readonly Border _skipBtn;

        private readonly TextBlock _focusMinsText;
        private readonly TextBlock _breakMinsText;
        private readonly TextBlock _longMinsText;
        private readonly TextBlock _cyclesText;

        private readonly Border _autoToggleTrack;
        private readonly Ellipse _autoToggleThumb;
        private readonly TextBlock _completedText;

        private TimerMode _currentMode = TimerMode.Pomodoro;

        public TimerToolView()
        {
            FlowDirection = LocalizationService.CurrentFlowDirection;
            Background = Brushes.Transparent;

            _rootGrid = new Grid();
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(176) }); // Col 0: Left Modes
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) }); // Col 1: Center Ring
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Col 2: Right Controls

            // -------------------------------------------------------------
            // COLUMN 0: LEFT MODES (Pomodoro, Countdown, Stopwatch, Water)
            // -------------------------------------------------------------
            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

            _rowPomo = CreateModeRow("⏱", "Pomodoro", "25:00 · Focus · 1 of 4", out _subPomo, () => SwitchMode(TimerMode.Pomodoro));
            _rowCountdown = CreateModeRow("⌛", "Countdown", "15:00", out _subCountdown, () => SwitchMode(TimerMode.Countdown));
            _rowStopwatch = CreateModeRow("⏱", "Stopwatch", "00:00:00.00", out _subStopwatch, () => SwitchMode(TimerMode.Stopwatch));
            _rowWater = CreateModeRow("💧", "Water", "0 / 8", out _subWater, () => SwitchMode(TimerMode.Water));

            leftStack.Children.Add(_rowPomo);
            leftStack.Children.Add(_rowCountdown);
            leftStack.Children.Add(_rowStopwatch);
            leftStack.Children.Add(_rowWater);

            Grid.SetColumn(leftStack, 0);
            _rootGrid.Children.Add(leftStack);

            // -------------------------------------------------------------
            // COLUMN 1: CENTER CIRCULAR RING (25:00 + Focus)
            // -------------------------------------------------------------
            var centerContainer = new Grid { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };

            _ringCanvas = new Canvas
            {
                Width = 152,
                Height = 152,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _ringTrack = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                StrokeThickness = 6.0
            };
            _ringProgress = new System.Windows.Shapes.Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange #FF5722
                StrokeThickness = 6.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };

            _ringCanvas.Children.Add(_ringTrack);
            _ringCanvas.Children.Add(_ringProgress);
            centerContainer.Children.Add(_ringCanvas);

            var centerTextStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _centerTimeText = new TextBlock
            {
                Text = "25:00",
                FontSize = 32.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _centerPhaseText = new TextBlock
            {
                Text = "Focus",
                FontSize = 12.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            centerTextStack.Children.Add(_centerTimeText);
            centerTextStack.Children.Add(_centerPhaseText);
            centerContainer.Children.Add(centerTextStack);

            Grid.SetColumn(centerContainer, 1);
            _rootGrid.Children.Add(centerContainer);

            // -------------------------------------------------------------
            // COLUMN 2: RIGHT CONTROLS, STEPPERS & TOGGLE
            // -------------------------------------------------------------
            var rightStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };

            // Section 1: Cycle Header + 4 Dots + Subtitle
            var cycleHeaderStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };

            var topRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _cycleHeaderText = new TextBlock
            {
                Text = "Focus 1 of 4",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(_cycleHeaderText);

            _cycleDotsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            topRow.Children.Add(_cycleDotsPanel);
            cycleHeaderStack.Children.Add(topRow);

            _cycleSubText = new TextBlock
            {
                Text = "Break 05:00 next",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            cycleHeaderStack.Children.Add(_cycleSubText);
            rightStack.Children.Add(cycleHeaderStack);

            // Section 2: Transport Controls (Reset, Play/Pause, Skip)
            var transportRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };

            _resetBtn = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                Cursor = Cursors.Hand,
                ToolTip = "Reset",
                Child = new TextBlock
                {
                    Text = "↻",
                    FontSize = 14,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _resetBtn.MouseEnter += (_, _) => _resetBtn.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            _resetBtn.MouseLeave += (_, _) => _resetBtn.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            _resetBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.ResetPomo();
            };
            transportRow.Children.Add(_resetBtn);

            _playIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 15,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 0, 0)
            };
            _playBtn = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(22),
                Background = new SolidColorBrush(Color.FromRgb(230, 74, 25)), // Orange #E64A19
                Cursor = Cursors.Hand,
                ToolTip = "Start / Pause",
                Margin = new Thickness(14, 0, 14, 0),
                Child = _playIcon
            };
            _playBtn.MouseEnter += (_, _) => _playBtn.Background = new SolidColorBrush(Color.FromRgb(255, 90, 40));
            _playBtn.MouseLeave += (_, _) => _playBtn.Background = new SolidColorBrush(Color.FromRgb(230, 74, 25));
            _playBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.TogglePomo();
            };
            transportRow.Children.Add(_playBtn);

            _skipBtn = new Border
            {
                Width = 32,
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                Cursor = Cursors.Hand,
                ToolTip = "Skip to Next Phase",
                Child = new TextBlock
                {
                    Text = "⏭",
                    FontSize = 13,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _skipBtn.MouseEnter += (_, _) => _skipBtn.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
            _skipBtn.MouseLeave += (_, _) => _skipBtn.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            _skipBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.SkipPomo();
            };
            transportRow.Children.Add(_skipBtn);

            rightStack.Children.Add(transportRow);

            // Section 3: Steppers (Focus 25m, Break 5m, Long 15m, Cycles 4)
            var steppersGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            steppersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            steppersGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            steppersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            steppersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var focusStepper = CreateStepper("Focus 25m", out _focusMinsText, delta => TimerService.Instance.AdjustPomoWork(delta));
            Grid.SetRow(focusStepper, 0);
            Grid.SetColumn(focusStepper, 0);
            steppersGrid.Children.Add(focusStepper);

            var breakStepper = CreateStepper("Break 5m", out _breakMinsText, delta => TimerService.Instance.AdjustPomoBreak(delta));
            Grid.SetRow(breakStepper, 0);
            Grid.SetColumn(breakStepper, 1);
            steppersGrid.Children.Add(breakStepper);

            var longStepper = CreateStepper("Long 15m", out _longMinsText, delta => TimerService.Instance.AdjustPomoLongBreak(delta));
            Grid.SetRow(longStepper, 1);
            Grid.SetColumn(longStepper, 0);
            steppersGrid.Children.Add(longStepper);

            var cyclesStepper = CreateStepper("Cycles 4", out _cyclesText, delta => TimerService.Instance.AdjustPomoCycles(delta));
            Grid.SetRow(cyclesStepper, 1);
            Grid.SetColumn(cyclesStepper, 1);
            steppersGrid.Children.Add(cyclesStepper);

            rightStack.Children.Add(steppersGrid);

            // Section 4: Auto-start next toggle & Completed today
            var toggleRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3),
                Cursor = Cursors.Hand
            };
            toggleRow.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("focus.auto_start", "Auto-start next"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });

            _autoToggleTrack = new Border
            {
                Width = 34,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(255, 87, 34)), // Orange #FF5722
                VerticalAlignment = VerticalAlignment.Center
            };
            var toggleCanvas = new Canvas { Width = 34, Height = 18 };
            _autoToggleThumb = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = Brushes.White
            };
            Canvas.SetLeft(_autoToggleThumb, 18);
            Canvas.SetTop(_autoToggleThumb, 2);
            toggleCanvas.Children.Add(_autoToggleThumb);
            _autoToggleTrack.Child = toggleCanvas;
            toggleRow.Children.Add(_autoToggleTrack);

            toggleRow.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.PomoAutoStartNext = !TimerService.Instance.PomoAutoStartNext;
                UpdateToggleVisual();
            };
            rightStack.Children.Add(toggleRow);

            _completedText = new TextBlock
            {
                Text = "0 completed today",
                FontSize = 11.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            rightStack.Children.Add(_completedText);

            Grid.SetColumn(rightStack, 2);
            _rootGrid.Children.Add(rightStack);

            Content = _rootGrid;

            TimerService.Instance.Tick += () => Dispatcher.InvokeAsync(UpdateVisuals);
            TimerService.Instance.StateChanged += () => Dispatcher.InvokeAsync(UpdateVisuals);

            DrawCircularRingTrack();
            SwitchMode(TimerMode.Pomodoro);
            UpdateVisuals();
        }

        private Border CreateModeRow(string icon, string title, string sub, out TextBlock subBlock, Action onClick)
        {
            var border = new Border
            {
                Height = 44,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();
            var iconBlock = new TextBlock
            {
                Text = icon,
                FontSize = 13.0,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            DockPanel.SetDock(iconBlock, Dock.Left);
            dock.Children.Add(iconBlock);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            stack.Children.Add(titleBlock);

            subBlock = new TextBlock
            {
                Text = sub,
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 1, 0, 0)
            };
            stack.Children.Add(subBlock);

            dock.Children.Add(stack);
            border.Child = dock;

            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };

            return border;
        }

        private Border CreateStepper(string initialText, out TextBlock labelBlock, Action<int> onDelta)
        {
            var pill = new Border
            {
                Width = 142,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                Margin = new Thickness(3)
            };

            var dock = new DockPanel();

            var minus = new Border
            {
                Width = 24,
                Height = 26,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "−",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            minus.MouseEnter += (_, _) => { if (minus.Child is TextBlock tb) tb.Foreground = Brushes.White; };
            minus.MouseLeave += (_, _) => { if (minus.Child is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)); };
            minus.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onDelta(-1);
            };
            DockPanel.SetDock(minus, Dock.Left);
            dock.Children.Add(minus);

            var plus = new Border
            {
                Width = 24,
                Height = 26,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "+",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            plus.MouseEnter += (_, _) => { if (plus.Child is TextBlock tb) tb.Foreground = Brushes.White; };
            plus.MouseLeave += (_, _) => { if (plus.Child is TextBlock tb) tb.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)); };
            plus.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onDelta(+1);
            };
            DockPanel.SetDock(plus, Dock.Right);
            dock.Children.Add(plus);

            labelBlock = new TextBlock
            {
                Text = initialText,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            dock.Children.Add(labelBlock);

            pill.Child = dock;
            return pill;
        }

        private void SwitchMode(TimerMode mode)
        {
            _currentMode = mode;

            _rowPomo.Background = new SolidColorBrush(mode == TimerMode.Pomodoro ? Color.FromRgb(38, 38, 44) : Colors.Transparent);
            _rowCountdown.Background = new SolidColorBrush(mode == TimerMode.Countdown ? Color.FromRgb(38, 38, 44) : Colors.Transparent);
            _rowStopwatch.Background = new SolidColorBrush(mode == TimerMode.Stopwatch ? Color.FromRgb(38, 38, 44) : Colors.Transparent);
            _rowWater.Background = new SolidColorBrush(mode == TimerMode.Water ? Color.FromRgb(38, 38, 44) : Colors.Transparent);

            UpdateVisuals();
        }

        public void UpdateVisuals()
        {
            var s = TimerService.Instance;

            // Update Left Subtitles
            int pMins = s.PomoRemainingSeconds / 60;
            int pSecs = s.PomoRemainingSeconds % 60;
            string pPhase = s.PomoIsWork ? LocalizationService.T("focus.focus", "Focus") : LocalizationService.T("focus.break", "Break");
            _subPomo.Text = $"{pMins:D2}:{pSecs:D2} · {pPhase} · {s.PomoCurrentCycle} of {s.PomoTargetCycles}";

            int cMins = s.CountRemainingSeconds / 60;
            int cSecs = s.CountRemainingSeconds % 60;
            _subCountdown.Text = $"{cMins:D2}:{cSecs:D2}";

            var sw = s.StopwatchElapsed;
            _subStopwatch.Text = $"{sw.Hours:D2}:{sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 10:D2}";

            _subWater.Text = $"{s.WaterGlasses} / {s.WaterTarget}";

            // Center Ring Time & Phase
            _centerTimeText.Text = $"{pMins:D2}:{pSecs:D2}";
            _centerPhaseText.Text = pPhase;

            // Draw Progress Arc
            int totalSecs = s.PomoIsWork ? s.PomoWorkSeconds : s.PomoBreakSeconds;
            double fraction = totalSecs > 0 ? (double)(totalSecs - s.PomoRemainingSeconds) / totalSecs : 0.0;
            DrawCircularProgressArc(Math.Clamp(fraction, 0.0, 1.0));

            // Right Header & Dots
            _cycleHeaderText.Text = string.Format(LocalizationService.T("focus.cycle_header_fmt", "{0} {1} of {2}"), pPhase, s.PomoCurrentCycle, s.PomoTargetCycles);
            int nextBreakMins = s.PomoBreakSeconds / 60;
            int nextBreakSecs = s.PomoBreakSeconds % 60;
            _cycleSubText.Text = string.Format(LocalizationService.T("focus.next_break_fmt", "Break {0} next"), $"{nextBreakMins:D2}:{nextBreakSecs:D2}");

            UpdateCycleDots(s.PomoCurrentCycle, s.PomoTargetCycles);

            // Transport Buttons
            _playIcon.Text = s.PomoIsRunning ? "⏸" : "▶";

            // Stepper Texts
            _focusMinsText.Text = string.Format(LocalizationService.T("focus.focus_min_fmt", "Focus {0}m"), s.PomoWorkSeconds / 60);
            _breakMinsText.Text = string.Format(LocalizationService.T("focus.break_min_fmt", "Break {0}m"), s.PomoBreakSeconds / 60);
            _longMinsText.Text = string.Format(LocalizationService.T("focus.long_min_fmt", "Long {0}m"), s.PomoLongBreakSeconds / 60);
            _cyclesText.Text = string.Format(LocalizationService.T("focus.cycles_fmt", "Cycles {0}"), s.PomoTargetCycles);

            // Auto-start toggle & Completed
            UpdateToggleVisual();
            _completedText.Text = string.Format(LocalizationService.T("focus.completed_today_fmt", "{0} completed today"), s.PomoSessionsToday);
        }

        private void UpdateCycleDots(int current, int total)
        {
            _cycleDotsPanel.Children.Clear();
            for (int i = 1; i <= Math.Min(total, 8); i++)
            {
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Margin = new Thickness(3, 0, 3, 0),
                    Fill = new SolidColorBrush(i <= current ? Color.FromRgb(255, 87, 34) : Color.FromArgb(50, 255, 255, 255))
                };
                _cycleDotsPanel.Children.Add(dot);
            }
        }

        private void UpdateToggleVisual()
        {
            bool on = TimerService.Instance.PomoAutoStartNext;
            _autoToggleTrack.Background = new SolidColorBrush(on ? Color.FromRgb(255, 87, 34) : Color.FromArgb(40, 255, 255, 255));
            Canvas.SetLeft(_autoToggleThumb, on ? 18 : 2);
        }

        private void DrawCircularRingTrack()
        {
            double r = 70.0;
            var center = new Point(76, 76);
            var geo = new EllipseGeometry(center, r, r);
            _ringTrack.Data = geo;
        }

        private void DrawCircularProgressArc(double fraction)
        {
            if (fraction <= 0.001)
            {
                _ringProgress.Data = null;
                return;
            }

            double r = 70.0;
            var center = new Point(76, 76);
            double angle = fraction * 359.99;
            double startAngle = -90.0;
            double endAngle = startAngle + angle;

            Point startPt = PointOnCircle(center, r, startAngle);
            Point endPt = PointOnCircle(center, r, endAngle);
            bool isLargeArc = angle > 180.0;

            var fig = new PathFigure
            {
                StartPoint = startPt,
                IsClosed = false
            };
            fig.Segments.Add(new ArcSegment(
                endPt,
                new Size(r, r),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            _ringProgress.Data = new PathGeometry(new[] { fig });
        }

        private static Point PointOnCircle(Point center, double radius, double angleDegrees)
        {
            double rad = angleDegrees * Math.PI / 180.0;
            return new Point(
                center.X + radius * Math.Cos(rad),
                center.Y + radius * Math.Sin(rad));
        }
    }
}
