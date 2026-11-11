using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Timer;

namespace WindowsBuddy.BuddyCapsule.UI
{
    /// <summary>
    /// TimerToolView: High-precision Sheet / Drum Roller Wheel Timer View:
    /// 1. Pomodoro (Sheet Drum Picker, Task Chips, Focus/Break Cycles, Auto-next)
    /// 2. Countdown / Timer (Interactive Tumbler, Presets, Repeat Loop, Sound)
    /// 3. Alarm Clock (Phone-style HH:MM Drum Roller, List, Snooze, Notch)
    /// 4. Stopwatch (Sub-second live drum, Laps with Fastest/Slowest badges, Copy)
    /// 5. Hydration (Daily ml, Glass steppers, Auto-reminder countdown)
    /// </summary>
    public sealed class TimerToolView : UserControl
    {
        private TimerMode _currentMode = TimerMode.Alarm;

        // Left Sidebar Rows
        private Border _rowPomo;
        private Border _rowCountdown;
        private Border _rowAlarm;
        private Border _rowStopwatch;
        private Border _rowWater;

        private Path _iconPomo;
        private Path _iconCountdown;
        private Path _iconAlarm;
        private Path _iconStopwatch;
        private Path _iconWater;

        private TextBlock _subPomo;
        private TextBlock _subCountdown;
        private TextBlock _subAlarm;
        private TextBlock _subStopwatch;
        private TextBlock _subWater;

        private Ellipse _dotPomo;
        private Ellipse _dotCountdown;
        private Ellipse _dotAlarm;
        private Ellipse _dotStopwatch;
        private Ellipse _dotWater;

        // Center Drum Roller Wheel Controls
        private Border _drumCard;
        private TextBlock _drumLeftTop;
        private TextBlock _drumLeftActive;
        private Border _drumLeftActiveBox;
        private TextBlock _drumLeftBottom;

        private TextBlock _drumColon;

        private TextBlock _drumRightTop;
        private TextBlock _drumRightActive;
        private Border _drumRightActiveBox;
        private TextBlock _drumRightBottom;

        private TextBlock _drumSubIndicatorText;
        private StackPanel _centerQuickPillsPanel;

        // Right Panels Host
        private Grid _rightHost;
        private Grid _pomoPanel;
        private Grid _countdownPanel;
        private Grid _alarmPanel;
        private Grid _stopwatchPanel;
        private Grid _waterPanel;

        // Pomodoro Controls
        private TextBox _pomoTitleBox;
        private readonly Border[] _pomoPresetChips = new Border[4];
        private readonly Path[] _pomoPresetChipIcons = new Path[4];
        private static readonly (string Label, Geometry Icon)[] PomoPresetsList = new[]
        {
            ("Code", TimerIcons.CodeGeometry),
            ("Study", TimerIcons.BookGeometry),
            ("Focus", TimerIcons.TargetGeometry),
            ("Write", TimerIcons.PencilGeometry)
        };

        private TextBlock _cycleHeaderText;
        private TextBlock _cycleSubText;
        private StackPanel _cycleDotsPanel;
        private Border _pomoPlayBtn;
        private Path _pomoPlayIcon;
        private TextBlock _focusMinsText;
        private TextBlock _breakMinsText;
        private TextBlock _longMinsText;
        private TextBlock _cyclesText;
        private Border _autoToggleTrack;
        private Ellipse _autoToggleThumb;
        private TextBlock _completedText;

        // Countdown Controls
        private TextBox _countTitleBox;
        private TextBox _countHoursInput;
        private TextBox _countMinsInput;
        private TextBox _countSecsInput;
        private TextBox _countTargetTimeInput;
        private TextBlock _countTargetTimePreviewText;
        private Border _countPlayBtn;
        private Path _countPlayIcon;
        private Border _countLoopBtn;
        private WrapPanel _countPresetsWrap;
        private TextBlock _countSoundBtnText;
        private TextBlock _countRepeatBtnText;
        private Border _countRingBanner;

        // Alarm Controls
        private TextBox _alarmTitleBox;
        private TextBox _alarmHourInput;
        private TextBox _alarmMinInput;
        private TextBox _alarmInMinutesInput;
        private TextBlock _alarmRemainingPreviewText;
        private Border _alarmAddBtn;
        private Border _alarmDailyBtn;
        private TextBlock _alarmSoundBtnText;
        private TextBlock _alarmSnoozeBtnText;
        private TextBlock _alarmRepeatBtnText;
        private Border _alarmNotchToggleTrack;
        private Ellipse _alarmNotchToggleThumb;
        private StackPanel _alarmsListStack;
        private TextBlock _alarmsEmptyHint;

        // Stopwatch Controls
        private TextBox _swTitleBox;
        private Border _swPlayBtn;
        private Path _swPlayIcon;
        private Border _swLapBtn;
        private StackPanel _swLapsStack;
        private TextBlock _swEmptyLapsHint;

        // Water Controls
        private TextBlock _waterMlSummaryText;
        private StackPanel _waterDropsVisualPanel;
        private Border _waterAddGlassBtn;
        private TextBlock _waterGoalStepperText;
        private Border _waterReminderToggleTrack;
        private Ellipse _waterReminderToggleThumb;
        private Border _waterNotchToggleTrack;
        private Ellipse _waterNotchToggleThumb;
        private TextBlock _waterReminderStatusText;
        private Border _waterInt30Btn;
        private Border _waterInt45Btn;
        private Border _waterInt60Btn;
        private Border _waterInt90Btn;

        // Cached Accent Brush for zero-allocation 40fps rendering
        private Color _cachedAccentColor = Colors.Transparent;
        private SolidColorBrush _cachedAccentBrush;

        public TimerToolView()
        {
            var rootGrid = new Grid
            {
                Margin = new Thickness(6, 4, 6, 4),
                FlowDirection = FlowDirection.LeftToRight
            };
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(148) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // COLUMN 0: LEFT SIDEBAR (5 Mode Cards)
            // -------------------------------------------------------------
            var leftStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };

            _rowPomo = BuildModeRow(
                TimerIcons.PomodoroGeometry,
                LocalizationService.T("focus.pomodoro", "Pomodoro"),
                "25:00 • Code",
                out _subPomo,
                out _dotPomo,
                out _iconPomo,
                () => SwitchMode(TimerMode.Pomodoro));

            _rowCountdown = BuildModeRow(
                TimerIcons.HourglassGeometry,
                LocalizationService.T("focus.countdown", "Timer"),
                "01:00",
                out _subCountdown,
                out _dotCountdown,
                out _iconCountdown,
                () => SwitchMode(TimerMode.Countdown));

            _rowAlarm = BuildModeRow(
                TimerIcons.AlarmClockGeometry,
                LocalizationService.T("focus.alarm", "Alarm Clock"),
                "No active alarm",
                out _subAlarm,
                out _dotAlarm,
                out _iconAlarm,
                () => SwitchMode(TimerMode.Alarm));

            _rowStopwatch = BuildModeRow(
                TimerIcons.StopwatchGeometry,
                LocalizationService.T("focus.stopwatch", "Stopwatch"),
                "00:00.00",
                out _subStopwatch,
                out _dotStopwatch,
                out _iconStopwatch,
                () => SwitchMode(TimerMode.Stopwatch));

            _rowWater = BuildModeRow(
                TimerIcons.WaterDropGeometry,
                LocalizationService.T("focus.water", "Hydration"),
                "0 / 8 • 0 ml",
                out _subWater,
                out _dotWater,
                out _iconWater,
                () => SwitchMode(TimerMode.Water));

            leftStack.Children.Add(_rowAlarm);
            leftStack.Children.Add(_rowStopwatch);
            leftStack.Children.Add(_rowCountdown);
            leftStack.Children.Add(_rowPomo);
            leftStack.Children.Add(_rowWater);

            Grid.SetColumn(leftStack, 0);
            rootGrid.Children.Add(leftStack);

            // -------------------------------------------------------------
            // COLUMN 1: CENTER SHEET / DRUM ROLLER WHEEL + QUICK BUTTONS
            // -------------------------------------------------------------
            var centerOuterStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            centerOuterStack.Children.Add(BuildDrumWheelCard());

            _centerQuickPillsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            centerOuterStack.Children.Add(_centerQuickPillsPanel);

            Grid.SetColumn(centerOuterStack, 1);
            rootGrid.Children.Add(centerOuterStack);

            // -------------------------------------------------------------
            // COLUMN 2: DYNAMIC MODE PANELS
            // -------------------------------------------------------------
            _rightHost = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(8, 0, 0, 0)
            };

            _pomoPanel = BuildPomodoroPanel();
            _countdownPanel = BuildCountdownPanel();
            _alarmPanel = BuildAlarmPanel();
            _stopwatchPanel = BuildStopwatchPanel();
            _waterPanel = BuildWaterPanel();

            _rightHost.Children.Add(_pomoPanel);
            _rightHost.Children.Add(_countdownPanel);
            _rightHost.Children.Add(_alarmPanel);
            _rightHost.Children.Add(_stopwatchPanel);
            _rightHost.Children.Add(_waterPanel);

            Grid.SetColumn(_rightHost, 2);
            rootGrid.Children.Add(_rightHost);

            Content = rootGrid;

            TimerService.Instance.Tick += () => Dispatcher.InvokeAsync(UpdateFastTickVisuals);
            TimerService.Instance.StateChanged += () => Dispatcher.InvokeAsync(UpdateVisuals);
            RealtimeNotchActionService.Instance.MediaSessionChanged += _ => Dispatcher.InvokeAsync(UpdateVisuals);
            CapsuleTheme.ThemeChanged += () => Dispatcher.InvokeAsync(UpdateVisuals);
            AppPreferences.Instance.Changed += () => Dispatcher.InvokeAsync(UpdateVisuals);

            SwitchMode(TimerService.Instance.LastActiveMode);
            UpdateVisuals();
        }

        private SolidColorBrush GetAccentBrush()
        {
            Color c = TimerService.Instance.GetEffectiveAccentColor();
            if (_cachedAccentBrush == null || _cachedAccentColor != c)
            {
                _cachedAccentColor = c;
                _cachedAccentBrush = new SolidColorBrush(c);
                _cachedAccentBrush.Freeze();
            }
            return _cachedAccentBrush;
        }

        #region Drum Wheel Card Builder & Wheel Handlers

        private Border BuildDrumWheelCard()
        {
            var p = CapsuleTheme.Current;
            _drumCard = new Border
            {
                Width = 168,
                Height = 176,
                CornerRadius = new CornerRadius(8),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                Cursor = Cursors.Hand,
                ToolTip = "Scroll: Adjust time · Click: Start/Pause · Right-click: Stop & Reset"
            };

            _drumCard.MouseWheel += (s, e) =>
            {
                e.Handled = true;
                AdjustTumblerLeft(e.Delta > 0 ? +1 : -1);
            };
            _drumCard.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ToggleCurrentModePrimaryAction();
            };
            _drumCard.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                ResetCurrentModeAction();
            };

            var cardStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var tumblersRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // --- LEFT TUMBLER ---
            var leftTumbler = new StackPanel
            {
                Width = 56,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftTumbler.MouseWheel += (s, e) =>
            {
                e.Handled = true;
                AdjustTumblerLeft(e.Delta > 0 ? +1 : -1);
            };

            _drumLeftTop = new TextBlock
            {
                Text = "01",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text2),
                Opacity = 0.38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _drumLeftTop.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AdjustTumblerLeft(-1);
            };

            _drumLeftActive = new TextBlock
            {
                Text = "02",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _drumLeftActiveBox = new Border
            {
                Width = 56,
                Height = 46,
                CornerRadius = new CornerRadius(5),
                BorderBrush = GetAccentBrush(),
                BorderThickness = new Thickness(1.5),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 2),
                Child = _drumLeftActive
            };
            _drumLeftActiveBox.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ToggleCurrentModePrimaryAction();
            };

            _drumLeftBottom = new TextBlock
            {
                Text = "03",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text2),
                Opacity = 0.38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _drumLeftBottom.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AdjustTumblerLeft(+1);
            };

            leftTumbler.Children.Add(_drumLeftTop);
            leftTumbler.Children.Add(_drumLeftActiveBox);
            leftTumbler.Children.Add(_drumLeftBottom);
            tumblersRow.Children.Add(leftTumbler);

            // --- COLON SEPARATOR ---
            _drumColon = new TextBlock
            {
                Text = ":",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(6, -4, 6, 0)
            };
            tumblersRow.Children.Add(_drumColon);

            // --- RIGHT TUMBLER ---
            var rightTumbler = new StackPanel
            {
                Width = 56,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightTumbler.MouseWheel += (s, e) =>
            {
                e.Handled = true;
                AdjustTumblerRight(e.Delta > 0 ? +1 : -1);
            };

            _drumRightTop = new TextBlock
            {
                Text = "29",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text2),
                Opacity = 0.38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _drumRightTop.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AdjustTumblerRight(-1);
            };

            _drumRightActive = new TextBlock
            {
                Text = "30",
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _drumRightActiveBox = new Border
            {
                Width = 56,
                Height = 46,
                CornerRadius = new CornerRadius(5),
                BorderBrush = GetAccentBrush(),
                BorderThickness = new Thickness(1.5),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 2),
                Child = _drumRightActive
            };
            _drumRightActiveBox.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ToggleCurrentModePrimaryAction();
            };

            _drumRightBottom = new TextBlock
            {
                Text = "31",
                FontSize = 22,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text2),
                Opacity = 0.38,
                HorizontalAlignment = HorizontalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 2, 0, 2)
            };
            _drumRightBottom.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AdjustTumblerRight(+1);
            };

            rightTumbler.Children.Add(_drumRightTop);
            rightTumbler.Children.Add(_drumRightActiveBox);
            rightTumbler.Children.Add(_drumRightBottom);
            tumblersRow.Children.Add(rightTumbler);

            cardStack.Children.Add(tumblersRow);

            // --- SUB-INDICATOR (e.g. Code • Focus 1/4) ---
            _drumSubIndicatorText = new TextBlock
            {
                Text = "Code • Focus 1/4",
                FontFamily = LocalizationService.CurrentFontFamily,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetAccentBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 150
            };
            cardStack.Children.Add(_drumSubIndicatorText);

            _drumCard.Child = cardStack;
            return _drumCard;
        }

        private void AdjustTumblerLeft(int delta)
        {
            var s = TimerService.Instance;
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    s.AddPomoRemainingSeconds(delta * 60);
                    if (!s.PomoIsRunning)
                    {
                        s.PomoWorkSeconds = Math.Clamp(s.PomoRemainingSeconds, 60, 180 * 60);
                        s.SavePrefs();
                    }
                    break;
                case TimerMode.Countdown:
                    s.AdjustCountdownComponents(0, delta, 0);
                    break;
                case TimerMode.Alarm:
                    s.AdjustDraftAlarmTime(delta, 0);
                    break;
                case TimerMode.Stopwatch:
                    break;
                case TimerMode.Water:
                    if (delta > 0) s.AddWaterGlass(1);
                    else s.RemoveWaterGlass();
                    break;
            }
            UpdateVisuals();
        }

        private void AdjustTumblerRight(int delta)
        {
            var s = TimerService.Instance;
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    s.AddPomoRemainingSeconds(delta);
                    if (!s.PomoIsRunning)
                    {
                        s.PomoWorkSeconds = Math.Clamp(s.PomoRemainingSeconds, 5, 180 * 60);
                        s.SavePrefs();
                    }
                    break;
                case TimerMode.Countdown:
                    s.AdjustCountdownComponents(0, 0, delta);
                    break;
                case TimerMode.Alarm:
                    s.AdjustDraftAlarmTime(0, delta);
                    break;
                case TimerMode.Stopwatch:
                    break;
                case TimerMode.Water:
                    s.AdjustWaterTarget(delta);
                    break;
            }
            UpdateVisuals();
        }

        private void ToggleCurrentModePrimaryAction()
        {
            var s = TimerService.Instance;
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    s.TogglePomo();
                    break;
                case TimerMode.Countdown:
                    s.ToggleCountdown();
                    break;
                case TimerMode.Alarm:
                {
                    var nearest = s.GetNearestEnabledAlarm(out _, out _);
                    if (nearest != null) s.ToggleAlarmEnabled(nearest.Id);
                    else CommitDraftAlarm();
                    break;
                }
                case TimerMode.Stopwatch:
                    s.ToggleStopwatch();
                    break;
                case TimerMode.Water:
                    s.AddWaterGlass(1);
                    break;
            }
        }

        private void ResetCurrentModeAction()
        {
            var s = TimerService.Instance;
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    s.ResetPomo();
                    break;
                case TimerMode.Countdown:
                    s.ResetCountdown();
                    break;
                case TimerMode.Stopwatch:
                    s.ResetStopwatch();
                    break;
            }
        }

        #endregion

        #region Panel Builders

        private Grid BuildPomodoroPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // 1. Task Bar (Full-width bar with task title and chevron)
            var taskBar = BuildTaskBar(
                TimerService.Instance.PomoTitle,
                out _pomoTitleBox,
                text => TimerService.Instance.SetPomoTitle(text));
            stack.Children.Add(taskBar);

            // 2. Preset Chips: [ 💻 Code ] [ 📖 Study ] [ 🎯 Focus ] [ ✍ Write ]
            var presetChipsRow = BuildPomodoroPresetChips();
            stack.Children.Add(presetChipsRow);

            // 3. Header & Transport Controls Row
            var headerAndTransportDock = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };

            // Transport buttons on the right: [ ↺ ] [ ▶ ] [ ⏭ ]
            var transportRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var resetBtn = TimerIcons.CreateCircleButton(TimerIcons.ResetGeometry, 34, "Stop & Reset", () => TimerService.Instance.ResetPomo(), out _);
            transportRow.Children.Add(resetBtn);

            _pomoPlayIcon = new Path
            {
                Data = TimerIcons.PlayGeometry,
                Fill = CapsuleTheme.Brush(p.AccentFg),
                Width = 15,
                Height = 15,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pomoPlayBtn = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = GetAccentBrush(),
                Cursor = Cursors.Hand,
                ToolTip = "Start / Pause",
                Margin = new Thickness(8, 0, 8, 0),
                Child = _pomoPlayIcon
            };
            _pomoPlayBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.TogglePomo();
            };
            transportRow.Children.Add(_pomoPlayBtn);

            var skipBtn = TimerIcons.CreateCircleButton(TimerIcons.SkipGeometry, 34, "Skip Phase", () => TimerService.Instance.SkipPomo(), out _);
            transportRow.Children.Add(skipBtn);

            DockPanel.SetDock(transportRow, Dock.Right);
            headerAndTransportDock.Children.Add(transportRow);

            // Info on the left: "Focus 1 of 4" + Dots + "Break 05:00 next"
            var cycleInfoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var topCycleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _cycleHeaderText = new TextBlock
            {
                Text = "Focus 1 of 4",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            topCycleRow.Children.Add(_cycleHeaderText);

            _cycleDotsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            topCycleRow.Children.Add(_cycleDotsPanel);
            cycleInfoStack.Children.Add(topCycleRow);

            _cycleSubText = new TextBlock
            {
                Text = "Break 05:00 next",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 0)
            };
            cycleInfoStack.Children.Add(_cycleSubText);
            headerAndTransportDock.Children.Add(cycleInfoStack);

            stack.Children.Add(headerAndTransportDock);

            // 4. 2x2 Steppers Grid: Focus 25m, Break 5m, Long 15m, Cycles 4
            var steppersGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 4, 0, 6) };
            steppersGrid.Children.Add(CreateStepperPill("Focus 25m", out _focusMinsText,
                () => TimerService.Instance.AdjustPomoWork(-5),
                () => TimerService.Instance.AdjustPomoWork(+5)));
            steppersGrid.Children.Add(CreateStepperPill("Break 5m", out _breakMinsText,
                () => TimerService.Instance.AdjustPomoBreak(-1),
                () => TimerService.Instance.AdjustPomoBreak(+1)));
            steppersGrid.Children.Add(CreateStepperPill("Long 15m", out _longMinsText,
                () => TimerService.Instance.AdjustPomoLongBreak(-5),
                () => TimerService.Instance.AdjustPomoLongBreak(+5)));
            steppersGrid.Children.Add(CreateStepperPill("Cycles 4", out _cyclesText,
                () => TimerService.Instance.AdjustPomoCycles(-1),
                () => TimerService.Instance.AdjustPomoCycles(+1)));
            stack.Children.Add(steppersGrid);

            // 5. Bottom row: Auto-next toggle + "0 done (0m)"
            var bottomBar = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };
            var autoToggle = CreateMiniToggle(
                LocalizationService.T("focus.auto", "Auto-next"),
                out _autoToggleTrack,
                out _autoToggleThumb,
                () =>
                {
                    TimerService.Instance.PomoAutoStartNext = !TimerService.Instance.PomoAutoStartNext;
                    TimerService.Instance.SavePrefs();
                    UpdateVisuals();
                });
            DockPanel.SetDock(autoToggle, Dock.Left);
            bottomBar.Children.Add(autoToggle);

            _completedText = new TextBlock
            {
                Text = "0 done (0m)",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            bottomBar.Children.Add(_completedText);
            stack.Children.Add(bottomBar);

            grid.Children.Add(stack);
            return grid;
        }

        private Border BuildTaskBar(string initialTitle, out TextBox tbOut, Action<string> onTitleChanged)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 32,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var dock = new DockPanel();

            var chevron = new Path
            {
                Data = TimerIcons.ChevronDownGeometry,
                Stroke = CapsuleTheme.Brush(p.Text2),
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 10,
                Height = 10,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            DockPanel.SetDock(chevron, Dock.Right);
            dock.Children.Add(chevron);

            var editIcon = new Path
            {
                Data = TimerIcons.PencilGeometry,
                Stroke = CapsuleTheme.Brush(p.Text2),
                StrokeThickness = 1.5,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 12,
                Height = 12,
                Stretch = Stretch.Uniform,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            DockPanel.SetDock(editIcon, Dock.Left);
            dock.Children.Add(editIcon);

            string cleanInit = TimerService.StripEmojis(initialTitle ?? "");
            if (string.IsNullOrWhiteSpace(cleanInit)) cleanInit = "Code";

            var tb = new TextBox
            {
                Text = cleanInit,
                FontFamily = LocalizationService.CurrentFontFamily,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                MaxLength = 34
            };
            tb.TextChanged += (_, _) =>
            {
                onTitleChanged(tb.Text);
            };

            dock.Children.Add(tb);
            border.Child = dock;
            tbOut = tb;
            return border;
        }

        private StackPanel BuildPomodoroPresetChips()
        {
            var p = CapsuleTheme.Current;
            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 4)
            };

            for (int i = 0; i < PomoPresetsList.Length; i++)
            {
                int index = i;
                var preset = PomoPresetsList[i];
                var chip = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Height = 26,
                    Padding = new Thickness(9, 0, 9, 0),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = Cursors.Hand
                };

                var chipStack = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                var iconPath = new Path
                {
                    Data = preset.Icon,
                    Stroke = CapsuleTheme.Brush(p.Text2),
                    StrokeThickness = 1.6,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Width = 12,
                    Height = 12,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 5, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                _pomoPresetChipIcons[index] = iconPath;
                chipStack.Children.Add(iconPath);

                var tb = new TextBlock
                {
                    Text = preset.Label,
                    FontFamily = LocalizationService.CurrentFontFamily,
                    FontSize = 11,
                    FontWeight = FontWeights.Medium,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    VerticalAlignment = VerticalAlignment.Center
                };
                chipStack.Children.Add(tb);

                chip.Child = chipStack;
                chip.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    if (_pomoTitleBox != null) _pomoTitleBox.Text = preset.Label;
                    TimerService.Instance.SetPomoTitle(preset.Label);
                    UpdateVisuals();
                };
                _pomoPresetChips[index] = chip;
                stack.Children.Add(chip);
            }

            return stack;
        }

        private Grid BuildCountdownPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // 1. Task Bar
            stack.Children.Add(BuildTitleInputBar(
                LocalizationService.T("focus.count_title_placeholder", "Timer label (e.g. Tea, Workout, Exam)..."),
                TimerService.Instance.CountTitle,
                out _countTitleBox,
                text => TimerService.Instance.SetCountdownTitle(text),
                new[] { "Coffee", "Cook", "Pill", "Workout", "Rest" }));

            // 2. Direct Controls + Transport Controls
            var mainControlRow = new DockPanel { Margin = new Thickness(0, 3, 0, 4) };

            var rightControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var stopBtn = TimerIcons.CreateCircleButton(TimerIcons.ResetGeometry, 34, "Stop & Reset", () => TimerService.Instance.ResetCountdown(), out _);
            rightControls.Children.Add(stopBtn);

            _countPlayIcon = new Path
            {
                Data = TimerIcons.PlayGeometry,
                Fill = CapsuleTheme.Brush(p.AccentFg),
                Width = 15,
                Height = 15,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _countPlayBtn = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = GetAccentBrush(),
                Cursor = Cursors.Hand,
                ToolTip = "Start / Pause Timer",
                Margin = new Thickness(8, 0, 8, 0),
                Child = _countPlayIcon
            };
            _countPlayBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CommitTypedCountdownIfEdited();
                TimerService.Instance.ToggleCountdown();
            };
            rightControls.Children.Add(_countPlayBtn);

            _countLoopBtn = TimerIcons.CreateCircleButton(TimerIcons.LoopGeometry, 34, "Auto-Repeat (Loop)", () => TimerService.Instance.ToggleCountdownLoop(), out _);
            rightControls.Children.Add(_countLoopBtn);

            DockPanel.SetDock(rightControls, Dock.Right);
            mainControlRow.Children.Add(rightControls);

            // Left: Editable H : M : S Boxes
            var hmsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            hmsRow.Children.Add(CreateEditableUnitBox("H", "00", out _countHoursInput,
                delta => TimerService.Instance.AdjustCountdownComponents(delta, 0, 0),
                CommitTypedCountdownIfEdited));
            hmsRow.Children.Add(CreateEditableUnitBox("M", "15", out _countMinsInput,
                delta => TimerService.Instance.AdjustCountdownComponents(0, delta, 0),
                CommitTypedCountdownIfEdited));
            hmsRow.Children.Add(CreateEditableUnitBox("S", "00", out _countSecsInput,
                delta => TimerService.Instance.AdjustCountdownComponents(0, 0, delta * 5),
                CommitTypedCountdownIfEdited));

            // "Ring at HH:MM" direct box
            var atTimeBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = "Type exact clock time (HH:MM) and press Enter to ring at that time!"
            };
            var atTimeStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            atTimeStack.Children.Add(new TextBlock
            {
                Text = "At HH:MM",
                FontSize = 8.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _countTargetTimeInput = new TextBox
            {
                Text = DateTime.Now.AddMinutes(15).ToString("HH:mm"),
                Width = 44,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _countTargetTimeInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    StartCountdownFromTypedClockTime();
                }
            };
            atTimeStack.Children.Add(_countTargetTimeInput);
            atTimeBorder.Child = atTimeStack;
            hmsRow.Children.Add(atTimeBorder);

            mainControlRow.Children.Add(hmsRow);
            stack.Children.Add(mainControlRow);

            // 3. Preset Duration Chips
            var presetsHeaderRow = new DockPanel { Margin = new Thickness(0, 1, 0, 2) };
            _countTargetTimePreviewText = new TextBlock
            {
                Text = "Rings at --:--",
                FontSize = 10.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_countTargetTimePreviewText, Dock.Left);
            presetsHeaderRow.Children.Add(_countTargetTimePreviewText);

            var savePresetBtn = CreateQuickChip("+ Save Preset", () =>
            {
                CommitTypedCountdownIfEdited();
                TimerService.Instance.AddCustomCountdownPreset(TimerService.Instance.CountTotalSeconds);
            });
            savePresetBtn.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(savePresetBtn, Dock.Right);
            presetsHeaderRow.Children.Add(savePresetBtn);
            stack.Children.Add(presetsHeaderRow);

            _countPresetsWrap = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 4)
            };
            stack.Children.Add(_countPresetsWrap);

            // 4. Sound & Ring Customization Bar
            var customRow = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };

            var soundControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var soundCycleBtn = CreateActionPill(TimerIcons.BellGeometry, "Sound: Classic", out _countSoundBtnText, () => TimerService.Instance.CycleSoundTone());
            soundControls.Children.Add(soundCycleBtn);

            var testSoundBtn = CreateQuickChip("Test", () => TimerService.Instance.PlaySoundPreview());
            soundControls.Children.Add(testSoundBtn);

            var repeatCycleBtn = CreateActionPill(TimerIcons.LoopGeometry, "Ring: 2x", out _countRepeatBtnText, () => TimerService.Instance.CycleRingRepeatCount());
            soundControls.Children.Add(repeatCycleBtn);

            DockPanel.SetDock(soundControls, Dock.Left);
            customRow.Children.Add(soundControls);

            _countRingBanner = new Border
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            var ringBtns = new StackPanel { Orientation = Orientation.Horizontal };
            ringBtns.Children.Add(CreateQuickChip("Snooze", () => TimerService.Instance.SnoozeLastRing()));
            ringBtns.Children.Add(CreateQuickChip("Stop", () => TimerService.Instance.StopRingingSound()));
            _countRingBanner.Child = ringBtns;
            customRow.Children.Add(_countRingBanner);

            stack.Children.Add(customRow);

            grid.Children.Add(stack);
            return grid;
        }

        private Grid BuildAlarmPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // 1. Alarm Title Input + Quick Tags
            stack.Children.Add(BuildTitleInputBar(
                LocalizationService.T("focus.alarm_title_placeholder", "Alarm label (e.g. Wake up, Meeting, Call)..."),
                TimerService.Instance.DraftAlarmTitle,
                out _alarmTitleBox,
                text => TimerService.Instance.DraftAlarmTitle = text ?? "",
                new[] { "Wake up", "Meeting", "Pill", "Lunch", "Call" }));

            // 2. Picker Row
            var pickerDock = new DockPanel { Margin = new Thickness(0, 3, 0, 4) };

            var rightAddActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            TextBlock dailyBtnText = null;
            _alarmDailyBtn = CreateActionPill(
                TimerIcons.LoopGeometry,
                "Once",
                out dailyBtnText,
                () =>
                {
                    TimerService.Instance.DraftAlarmRepeatDaily = !TimerService.Instance.DraftAlarmRepeatDaily;
                    if (dailyBtnText != null)
                        dailyBtnText.Text = TimerService.Instance.DraftAlarmRepeatDaily ? "Daily" : "Once";
                    UpdateVisuals();
                });
            rightAddActions.Children.Add(_alarmDailyBtn);

            _alarmAddBtn = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = GetAccentBrush(),
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Set / Add Alarm",
                Child = new TextBlock
                {
                    Text = "+ Set Alarm",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.AccentFg),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            _alarmAddBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CommitDraftAlarm();
            };
            rightAddActions.Children.Add(_alarmAddBtn);

            DockPanel.SetDock(rightAddActions, Dock.Right);
            pickerDock.Children.Add(rightAddActions);

            var leftPickers = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            leftPickers.Children.Add(CreateEditableUnitBox("Hour", $"{TimerService.Instance.DraftAlarmHour:D2}", out _alarmHourInput,
                delta => TimerService.Instance.AdjustDraftAlarmTime(delta, 0),
                SyncTypedDraftAlarmTime));

            leftPickers.Children.Add(new TextBlock
            {
                Text = ":",
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0, 1, 0)
            });

            leftPickers.Children.Add(CreateEditableUnitBox("Min", $"{TimerService.Instance.DraftAlarmMinute:D2}", out _alarmMinInput,
                delta => TimerService.Instance.AdjustDraftAlarmTime(0, delta),
                SyncTypedDraftAlarmTime));

            var inMinBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Type how many minutes from now and press Enter!"
            };
            var inMinStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inMinStack.Children.Add(new TextBlock
            {
                Text = "In +Min",
                FontSize = 8.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _alarmInMinutesInput = new TextBox
            {
                Text = "30",
                Width = 34,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextAlignment = TextAlignment.Center
            };
            _alarmInMinutesInput.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && int.TryParse(_alarmInMinutesInput.Text.Trim(), out int mins) && mins > 0)
                {
                    e.Handled = true;
                    TimerService.Instance.AddQuickAlarmInMinutes(mins, _alarmTitleBox.Text);
                }
            };
            inMinStack.Children.Add(_alarmInMinutesInput);
            inMinBorder.Child = inMinStack;
            leftPickers.Children.Add(inMinBorder);

            pickerDock.Children.Add(leftPickers);
            stack.Children.Add(pickerDock);

            // 3. Quick Chips
            var quickInRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 1, 0, 4),
                FlowDirection = FlowDirection.LeftToRight
            };
            foreach (var (label, mins) in new[] { ("+5m", 5), ("+10m", 10), ("+15m", 15), ("+20m", 20), ("+30m", 30), ("+45m", 45), ("+1h", 60), ("+2h", 120) })
            {
                int m = mins;
                quickInRow.Children.Add(CreateQuickChip(label, () =>
                {
                    _alarmInMinutesInput.Text = m.ToString();
                    TimerService.Instance.AddQuickAlarmInMinutes(m, _alarmTitleBox.Text);
                }));
            }
            stack.Children.Add(quickInRow);

            // 4. Customization Row
            var customRow = new DockPanel { Margin = new Thickness(0, 1, 0, 4) };
            var customLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            customLeft.Children.Add(CreateActionPill(TimerIcons.BellGeometry, "Classic", out _alarmSoundBtnText, () => TimerService.Instance.CycleSoundTone()));
            customLeft.Children.Add(CreateQuickChip("Test", () => TimerService.Instance.PlaySoundPreview()));
            customLeft.Children.Add(CreateActionPill(TimerIcons.MoonGeometry, "5m", out _alarmSnoozeBtnText, () =>
            {
                int nextSnooze = TimerService.Instance.DefaultSnoozeMinutes switch
                {
                    0 => 3,
                    3 => 5,
                    5 => 10,
                    10 => 15,
                    _ => 0
                };
                TimerService.Instance.SetSnoozeMinutes(nextSnooze);
            }));
            customLeft.Children.Add(CreateActionPill(TimerIcons.LoopGeometry, "2x", out _alarmRepeatBtnText, () => TimerService.Instance.CycleRingRepeatCount()));

            DockPanel.SetDock(customLeft, Dock.Left);
            customRow.Children.Add(customLeft);

            var notchToggle = CreateMiniToggle(
                "Notch",
                out _alarmNotchToggleTrack,
                out _alarmNotchToggleThumb,
                () =>
                {
                    TimerService.Instance.ShowAlarmInNotch = !TimerService.Instance.ShowAlarmInNotch;
                    TimerService.Instance.SavePrefs();
                    UpdateVisuals();
                });
            notchToggle.HorizontalAlignment = HorizontalAlignment.Right;
            customRow.Children.Add(notchToggle);
            stack.Children.Add(customRow);

            // 5. Saved Alarms List
            var alarmsBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 5, 8, 5),
                Height = 68
            };
            var alarmsOuter = new Grid();
            _alarmsEmptyHint = new TextBlock
            {
                Text = "No alarms set — pick a clock time (HH:MM) or click +5m..+2h above",
                FontSize = 10.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            alarmsOuter.Children.Add(_alarmsEmptyHint);

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _alarmsListStack = new StackPanel();
            scroll.Content = _alarmsListStack;
            alarmsOuter.Children.Add(scroll);
            alarmsBorder.Child = alarmsOuter;
            stack.Children.Add(alarmsBorder);

            _alarmRemainingPreviewText = new TextBlock
            {
                Text = "",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(2, 2, 0, 0)
            };
            stack.Children.Add(_alarmRemainingPreviewText);

            grid.Children.Add(stack);
            return grid;
        }

        private Grid BuildStopwatchPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            stack.Children.Add(BuildTitleInputBar(
                LocalizationService.T("focus.sw_title_placeholder", "Stopwatch label (shown in Notch)..."),
                TimerService.Instance.StopwatchTitle,
                out _swTitleBox,
                text => TimerService.Instance.SetStopwatchTitle(text),
                new[] { "Run", "Sprint", "Task", "Speedrun" }));

            var controlsRow = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };

            var leftActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var resetSwBtn = TimerIcons.CreateCircleButton(TimerIcons.ResetGeometry, 34, "Stop & Reset", () => TimerService.Instance.ResetStopwatch(), out _);
            leftActions.Children.Add(resetSwBtn);

            _swPlayIcon = new Path
            {
                Data = TimerIcons.PlayGeometry,
                Fill = CapsuleTheme.Brush(p.AccentFg),
                Width = 15,
                Height = 15,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _swPlayBtn = new Border
            {
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = GetAccentBrush(),
                Cursor = Cursors.Hand,
                ToolTip = "Start / Stop Stopwatch",
                Margin = new Thickness(8, 0, 8, 0),
                Child = _swPlayIcon
            };
            _swPlayBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.ToggleStopwatch();
            };
            leftActions.Children.Add(_swPlayBtn);

            _swLapBtn = TimerIcons.CreateCircleButton(TimerIcons.FlagGeometry, 34, "Record Lap", () => TimerService.Instance.RecordStopwatchLap(), out _);
            leftActions.Children.Add(_swLapBtn);

            DockPanel.SetDock(leftActions, Dock.Left);
            controlsRow.Children.Add(leftActions);

            var copyLapsBtn = CreateActionPill(TimerIcons.CopyGeometry, "Copy Laps", out _, CopyStopwatchLapsToClipboard);
            copyLapsBtn.HorizontalAlignment = HorizontalAlignment.Right;
            controlsRow.Children.Add(copyLapsBtn);

            stack.Children.Add(controlsRow);

            var lapsContainer = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 6, 8, 6),
                Height = 110
            };

            var lapsGrid = new Grid();
            _swEmptyLapsHint = new TextBlock
            {
                Text = LocalizationService.T("focus.no_laps", "No laps recorded yet — press Lap while running"),
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            lapsGrid.Children.Add(_swEmptyLapsHint);

            var lapsScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            _swLapsStack = new StackPanel();
            lapsScroll.Content = _swLapsStack;
            lapsGrid.Children.Add(lapsScroll);

            lapsContainer.Child = lapsGrid;
            stack.Children.Add(lapsContainer);

            grid.Children.Add(stack);
            return grid;
        }

        private Grid BuildWaterPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var topCard = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            var topStack = new StackPanel();
            var summaryDock = new DockPanel();
            _waterMlSummaryText = new TextBlock
            {
                Text = "0 ml / 2000 ml (0%)",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_waterMlSummaryText, Dock.Left);
            summaryDock.Children.Add(_waterMlSummaryText);

            var resetWaterBtn = new TextBlock
            {
                Text = LocalizationService.T("focus.reset", "Reset"),
                FontSize = 10.5,
                Foreground = GetAccentBrush(),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            resetWaterBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.ResetWaterGlasses();
            };
            summaryDock.Children.Add(resetWaterBtn);
            topStack.Children.Add(summaryDock);

            _waterDropsVisualPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            topStack.Children.Add(_waterDropsVisualPanel);
            topCard.Child = topStack;
            stack.Children.Add(topCard);

            var actionsRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actionsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var minusGlassBtn = TimerIcons.CreateCircleButton(TimerIcons.MinusGeometry, 34, "-1 Glass", () => TimerService.Instance.RemoveWaterGlass(), out _);
            Grid.SetColumn(minusGlassBtn, 0);
            actionsRow.Children.Add(minusGlassBtn);

            var addStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dropIcon = new Path
            {
                Data = TimerIcons.WaterDropGeometry,
                Fill = CapsuleTheme.Brush(p.AccentFg),
                Width = 13,
                Height = 13,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            addStack.Children.Add(dropIcon);
            addStack.Children.Add(new TextBlock
            {
                Text = "+1 Glass (250ml)",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                VerticalAlignment = VerticalAlignment.Center
            });

            _waterAddGlassBtn = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = GetAccentBrush(),
                Padding = new Thickness(14, 0, 14, 0),
                Margin = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                Child = addStack
            };
            _waterAddGlassBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.AddWaterGlass(1);
            };
            Grid.SetColumn(_waterAddGlassBtn, 1);
            actionsRow.Children.Add(_waterAddGlassBtn);

            var goalStepper = CreateStepperPill("Goal: 8", out _waterGoalStepperText,
                () => TimerService.Instance.AdjustWaterTarget(-1),
                () => TimerService.Instance.AdjustWaterTarget(+1));
            Grid.SetColumn(goalStepper, 2);
            actionsRow.Children.Add(goalStepper);
            stack.Children.Add(actionsRow);

            var reminderCard = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 7, 10, 7)
            };
            var remStack = new StackPanel();

            var remTopDock = new DockPanel();
            var remToggle = CreateMiniToggle(
                LocalizationService.T("focus.water_auto_reminder", "Auto Hydration Timer"),
                out _waterReminderToggleTrack,
                out _waterReminderToggleThumb,
                () => TimerService.Instance.ToggleWaterReminder());
            DockPanel.SetDock(remToggle, Dock.Left);
            remTopDock.Children.Add(remToggle);

            _waterReminderStatusText = new TextBlock
            {
                Text = "Off",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetAccentBrush(),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            remTopDock.Children.Add(_waterReminderStatusText);
            remStack.Children.Add(remTopDock);

            var remBottomDock = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var intervalsRow = new StackPanel { Orientation = Orientation.Horizontal };
            _waterInt30Btn = CreateQuickChip("30m", () => TimerService.Instance.SetWaterReminderInterval(30));
            _waterInt45Btn = CreateQuickChip("45m", () => TimerService.Instance.SetWaterReminderInterval(45));
            _waterInt60Btn = CreateQuickChip("60m", () => TimerService.Instance.SetWaterReminderInterval(60));
            _waterInt90Btn = CreateQuickChip("90m", () => TimerService.Instance.SetWaterReminderInterval(90));
            intervalsRow.Children.Add(_waterInt30Btn);
            intervalsRow.Children.Add(_waterInt45Btn);
            intervalsRow.Children.Add(_waterInt60Btn);
            intervalsRow.Children.Add(_waterInt90Btn);
            DockPanel.SetDock(intervalsRow, Dock.Left);
            remBottomDock.Children.Add(intervalsRow);

            var waterNotchToggle = CreateMiniToggle(
                LocalizationService.T("focus.show_in_notch", "Show in Notch"),
                out _waterNotchToggleTrack,
                out _waterNotchToggleThumb,
                () =>
                {
                    TimerService.Instance.ShowWaterInNotch = !TimerService.Instance.ShowWaterInNotch;
                    TimerService.Instance.SavePrefs();
                    UpdateVisuals();
                });
            waterNotchToggle.HorizontalAlignment = HorizontalAlignment.Right;
            remBottomDock.Children.Add(waterNotchToggle);

            remStack.Children.Add(remBottomDock);
            reminderCard.Child = remStack;
            stack.Children.Add(reminderCard);

            grid.Children.Add(stack);
            return grid;
        }

        #endregion

        #region Input & Helper Builders

        private void CommitTypedCountdownIfEdited()
        {
            if (_countHoursInput == null || _countMinsInput == null || _countSecsInput == null) return;
            int h = int.TryParse(_countHoursInput.Text.Trim(), out int ph) ? Math.Clamp(ph, 0, 23) : 0;
            int m = int.TryParse(_countMinsInput.Text.Trim(), out int pm) ? Math.Clamp(pm, 0, 999) : 0;
            int s = int.TryParse(_countSecsInput.Text.Trim(), out int ps) ? Math.Clamp(ps, 0, 59) : 0;
            int total = Math.Clamp(h * 3600 + m * 60 + s, 5, 24 * 3600 - 1);
            if (total != TimerService.Instance.CountTotalSeconds && !TimerService.Instance.CountIsRunning)
            {
                TimerService.Instance.SetCountdownExactComponents(h, m, s);
            }
        }

        private void StartCountdownFromTypedClockTime()
        {
            if (_countTargetTimeInput == null) return;
            string raw = _countTargetTimeInput.Text.Trim();
            var parts = raw.Split(':', '.', ' ');
            if (parts.Length >= 2 &&
                int.TryParse(parts[0], out int h) &&
                int.TryParse(parts[1], out int m))
            {
                TimerService.Instance.StartCountdownUntilClockTime(h, m, _countTitleBox?.Text);
            }
            else if (int.TryParse(raw, out int minsFromNow) && minsFromNow > 0)
            {
                TimerService.Instance.StartCountdownInMinutes(minsFromNow, _countTitleBox?.Text);
            }
        }

        private void SyncTypedDraftAlarmTime()
        {
            if (_alarmHourInput == null || _alarmMinInput == null) return;
            int h = int.TryParse(_alarmHourInput.Text.Trim(), out int ph) ? Math.Clamp(ph, 0, 23) : TimerService.Instance.DraftAlarmHour;
            int m = int.TryParse(_alarmMinInput.Text.Trim(), out int pm) ? Math.Clamp(pm, 0, 59) : TimerService.Instance.DraftAlarmMinute;
            TimerService.Instance.SetDraftAlarmExact(h, m);
        }

        private void CommitDraftAlarm()
        {
            SyncTypedDraftAlarmTime();
            TimerService.Instance.AddOrUpdateAlarm(
                TimerService.Instance.DraftAlarmHour,
                TimerService.Instance.DraftAlarmMinute,
                _alarmTitleBox?.Text,
                TimerService.Instance.DraftAlarmRepeatDaily);
        }

        private static StackPanel BuildTitleInputBar(
            string placeholder,
            string initialValue,
            out TextBox textBoxOut,
            Action<string> onChanged,
            string[] quickTags)
        {
            var p = CapsuleTheme.Current;
            var container = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };

            var inputBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 6, 3)
            };

            var dock = new DockPanel();

            var clearBtn = new Border
            {
                Width = 16,
                Height = 16,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = "Clear title",
                Child = new Path
                {
                    Data = TimerIcons.CloseGeometry,
                    Stroke = CapsuleTheme.Brush(p.Text2),
                    StrokeThickness = 1.6,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Width = 9,
                    Height = 9,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            DockPanel.SetDock(clearBtn, Dock.Right);
            dock.Children.Add(clearBtn);

            var tbGrid = new Grid();
            var hint = new TextBlock
            {
                Text = placeholder,
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(initialValue) ? Visibility.Visible : Visibility.Collapsed
            };

            var tb = new TextBox
            {
                Text = initialValue ?? "",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                MaxLength = 34
            };

            tb.TextChanged += (_, _) =>
            {
                hint.Visibility = string.IsNullOrEmpty(tb.Text) ? Visibility.Visible : Visibility.Collapsed;
                onChanged(tb.Text);
            };
            clearBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                tb.Text = "";
            };

            tbGrid.Children.Add(hint);
            tbGrid.Children.Add(tb);
            dock.Children.Add(tbGrid);
            inputBorder.Child = dock;
            container.Children.Add(inputBorder);

            var tagsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 3, 0, 0)
            };
            foreach (var tag in quickTags)
            {
                string tVal = tag;
                var chip = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(5),
                    Padding = new Thickness(6, 1.5, 6, 1.5),
                    Margin = new Thickness(0, 0, 4, 0),
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = tVal,
                        FontSize = 9.5,
                        Foreground = CapsuleTheme.Brush(p.Text)
                    }
                };
                chip.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    tb.Text = tVal;
                };
                tagsRow.Children.Add(chip);
            }
            container.Children.Add(tagsRow);

            textBoxOut = tb;
            return container;
        }

        private static Border CreateEditableUnitBox(
            string unitLabel,
            string initialText,
            out TextBox inputOut,
            Action<int> onStepDelta,
            Action onCommitText)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4, 2, 4, 2),
                Margin = new Thickness(0, 0, 4, 0)
            };
            border.MouseWheel += (_, e) =>
            {
                e.Handled = true;
                onStepDelta(e.Delta > 0 ? +1 : -1);
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var minus = new TextBlock
            {
                Text = "−",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Padding = new Thickness(3, 0, 3, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            minus.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onStepDelta(-1);
            };

            var centerCol = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var tb = new TextBox
            {
                Text = initialText,
                Width = 26,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                CaretBrush = CapsuleTheme.Brush(p.Text),
                FontSize = 12.0,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            tb.LostFocus += (_, _) => onCommitText?.Invoke();
            tb.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    onCommitText?.Invoke();
                }
            };
            centerCol.Children.Add(tb);
            centerCol.Children.Add(new TextBlock
            {
                Text = unitLabel,
                FontSize = 8.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            });

            var plus = new TextBlock
            {
                Text = "+",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Padding = new Thickness(3, 0, 3, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            plus.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onStepDelta(+1);
            };

            stack.Children.Add(minus);
            stack.Children.Add(centerCol);
            stack.Children.Add(plus);
            border.Child = stack;
            inputOut = tb;
            return border;
        }

        private static Border BuildModeRow(
            Geometry iconGeom,
            string title,
            string subtitle,
            out TextBlock subBlockOut,
            out Ellipse runningDotOut,
            out Path iconPathOut,
            Action onClick)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 1.5, 0, 1.5),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();

            var iconPath = new Path
            {
                Data = iconGeom,
                Stroke = CapsuleTheme.Brush(p.Text2),
                StrokeThickness = 1.6,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconPathOut = iconPath;

            var iconBorder = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = iconPath
            };
            DockPanel.SetDock(iconBorder, Dock.Left);
            dock.Children.Add(iconBorder);

            runningDotOut = new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(48, 209, 88)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0),
                Visibility = Visibility.Collapsed
            };
            DockPanel.SetDock(runningDotOut, Dock.Right);
            dock.Children.Add(runningDotOut);

            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });

            subBlockOut = new TextBlock
            {
                Text = subtitle,
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0)
            };
            textStack.Children.Add(subBlockOut);
            dock.Children.Add(textStack);

            border.Child = dock;
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return border;
        }

        private static Border CreateStepperPill(string initialLabel, out TextBlock labelOut, Action onMinus, Action onPlus)
        {
            var p = CapsuleTheme.Current;
            var pill = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Height = 30,
                Margin = new Thickness(2.5),
                Padding = new Thickness(8, 0, 8, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var minusBtn = new TextBlock
            {
                Text = "—",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            minusBtn.MouseEnter += (_, _) => minusBtn.Foreground = CapsuleTheme.Brush(p.Text);
            minusBtn.MouseLeave += (_, _) => minusBtn.Foreground = CapsuleTheme.Brush(p.Text2);
            minusBtn.MouseLeftButtonUp += (_, e) => { e.Handled = true; onMinus(); };
            Grid.SetColumn(minusBtn, 0);
            grid.Children.Add(minusBtn);

            var label = new TextBlock
            {
                Text = initialLabel,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            labelOut = label;
            Grid.SetColumn(label, 1);
            grid.Children.Add(label);

            var plusBtn = new TextBlock
            {
                Text = "+",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            plusBtn.MouseEnter += (_, _) => plusBtn.Foreground = CapsuleTheme.Brush(p.Text);
            plusBtn.MouseLeave += (_, _) => plusBtn.Foreground = CapsuleTheme.Brush(p.Text2);
            plusBtn.MouseLeftButtonUp += (_, e) => { e.Handled = true; onPlus(); };
            Grid.SetColumn(plusBtn, 2);
            grid.Children.Add(plusBtn);

            pill.Child = grid;
            return pill;
        }

        private static Border CreateQuickChip(string label, Action onClick, Action onRightClick = null)
        {
            var p = CapsuleTheme.Current;
            var chip = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(0, 0, 4, 2),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 10,
                    FontWeight = FontWeights.Medium,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            chip.MouseEnter += (_, _) => chip.Opacity = 0.8;
            chip.MouseLeave += (_, _) => chip.Opacity = 1.0;
            chip.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            if (onRightClick != null)
            {
                chip.MouseRightButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    onRightClick();
                };
            }
            return chip;
        }

        private static Border CreateQuickActionButton(string label, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var btn = new Border
            {
                Width = 48,
                Height = 28,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(3, 0, 3, 0),
                Cursor = Cursors.Hand
            };
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Child = tb;
            btn.MouseEnter += (_, _) =>
            {
                btn.Background = CapsuleTheme.Brush(p.Surface2);
            };
            btn.MouseLeave += (_, _) =>
            {
                btn.Background = CapsuleTheme.Brush(p.Surface);
            };
            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return btn;
        }

        private static Border CreateActionPill(Geometry iconGeom, string label, out TextBlock textOut, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var pill = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(7, 0, 7, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (iconGeom != null)
            {
                var iconPath = new Path
                {
                    Data = iconGeom,
                    Stroke = CapsuleTheme.Brush(p.Text2),
                    StrokeThickness = 1.6,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    Width = 11,
                    Height = 11,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                stack.Children.Add(iconPath);
            }

            var tb = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            textOut = tb;
            stack.Children.Add(tb);

            pill.Child = stack;
            pill.MouseEnter += (_, _) => pill.Background = CapsuleTheme.Brush(p.Surface2);
            pill.MouseLeave += (_, _) => pill.Background = CapsuleTheme.Brush(p.Surface);
            pill.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return pill;
        }

        private static Border CreateActionPill(string label, out TextBlock textOut, Action onClick)
        {
            return CreateActionPill(null, label, out textOut, onClick);
        }

        private static StackPanel CreateMiniToggle(string label, out Border trackOut, out Ellipse thumbOut, Action onToggle)
        {
            var p = CapsuleTheme.Current;
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            if (!string.IsNullOrEmpty(label))
            {
                row.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 10.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 6, 0)
                });
            }

            trackOut = new Border
            {
                Width = 32,
                Height = 17,
                CornerRadius = new CornerRadius(8.5),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };
            var canvas = new Canvas { Width = 32, Height = 17 };
            thumbOut = new Ellipse
            {
                Width = 13,
                Height = 13,
                Fill = CapsuleTheme.Brush(p.AccentFg)
            };
            Canvas.SetLeft(thumbOut, 2);
            Canvas.SetTop(thumbOut, 1);
            canvas.Children.Add(thumbOut);
            trackOut.Child = canvas;
            row.Children.Add(trackOut);

            row.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onToggle();
            };
            return row;
        }

        private void CopyStopwatchLapsToClipboard()
        {
            var laps = TimerService.Instance.Laps;
            if (laps.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var lap in laps.OrderBy(l => l.LapNumber))
            {
                sb.AppendLine($"Lap #{lap.LapNumber}: +{FormatLapSpan(lap.SplitTime)} (Total: {FormatLapSpan(lap.TotalTime)})");
            }
            try
            {
                Clipboard.SetText(sb.ToString().TrimEnd());
            }
            catch { }
        }

        #endregion

        #region Mode Switching & Live Visual Updates

        public void SwitchMode(TimerMode mode)
        {
            _currentMode = mode;
            TimerService.Instance.LastActiveMode = mode;

            SetRowActiveStyle(_rowPomo, _iconPomo, mode == TimerMode.Pomodoro);
            SetRowActiveStyle(_rowCountdown, _iconCountdown, mode == TimerMode.Countdown);
            SetRowActiveStyle(_rowAlarm, _iconAlarm, mode == TimerMode.Alarm);
            SetRowActiveStyle(_rowStopwatch, _iconStopwatch, mode == TimerMode.Stopwatch);
            SetRowActiveStyle(_rowWater, _iconWater, mode == TimerMode.Water);

            _pomoPanel.Visibility = mode == TimerMode.Pomodoro ? Visibility.Visible : Visibility.Collapsed;
            _countdownPanel.Visibility = mode == TimerMode.Countdown ? Visibility.Visible : Visibility.Collapsed;
            _alarmPanel.Visibility = mode == TimerMode.Alarm ? Visibility.Visible : Visibility.Collapsed;
            _stopwatchPanel.Visibility = mode == TimerMode.Stopwatch ? Visibility.Visible : Visibility.Collapsed;
            _waterPanel.Visibility = mode == TimerMode.Water ? Visibility.Visible : Visibility.Collapsed;

            RebuildCenterQuickPills();
            UpdateVisuals();
        }

        private void SetRowActiveStyle(Border row, Path iconPath, bool active)
        {
            if (row == null) return;
            var p = CapsuleTheme.Current;
            var accentBrush = GetAccentBrush();

            row.Background = active ? CapsuleTheme.Brush(p.Surface) : Brushes.Transparent;
            row.BorderBrush = active ? CapsuleTheme.Brush(p.Border) : Brushes.Transparent;

            if (iconPath != null)
            {
                iconPath.Stroke = active ? accentBrush : CapsuleTheme.Brush(p.Text2);
            }
        }

        private void RebuildCenterQuickPills()
        {
            _centerQuickPillsPanel.Children.Clear();
            var s = TimerService.Instance;

            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("- 1m", () => { s.AddPomoRemainingSeconds(-60); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 1m", () => { s.AddPomoRemainingSeconds(+60); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 5m", () => { s.AddPomoRemainingSeconds(+300); UpdateVisuals(); }));
                    break;
                case TimerMode.Countdown:
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("- 1m", () => { s.AddCountdownRunningSeconds(-60); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 1m", () => { s.AddCountdownRunningSeconds(+60); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 5m", () => { s.AddCountdownRunningSeconds(+300); UpdateVisuals(); }));
                    break;
                case TimerMode.Alarm:
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("- 10m", () => { s.AdjustDraftAlarmTime(0, -10); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 15m", () => { s.AdjustDraftAlarmTime(0, +15); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 30m", () => { s.AdjustDraftAlarmTime(0, +30); UpdateVisuals(); }));
                    break;
                case TimerMode.Stopwatch:
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("Reset", () => { s.ResetStopwatch(); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("Lap", () => { s.RecordStopwatchLap(); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("Copy", () => CopyStopwatchLapsToClipboard()));
                    break;
                case TimerMode.Water:
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("- 250ml", () => { s.RemoveWaterGlass(); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 250ml", () => { s.AddWaterGlass(1); UpdateVisuals(); }));
                    _centerQuickPillsPanel.Children.Add(CreateQuickActionButton("+ 500ml", () => { s.AddWaterGlass(2); UpdateVisuals(); }));
                    break;
            }
        }

        /// <summary>
        /// High-frequency (40fps) lightweight update for live drum roller digits and active border styling.
        /// </summary>
        private void UpdateFastTickVisuals()
        {
            var s = TimerService.Instance;
            var accentBrush = GetAccentBrush();
            _drumLeftActiveBox.BorderBrush = accentBrush;
            _drumRightActiveBox.BorderBrush = accentBrush;
            _drumSubIndicatorText.Foreground = accentBrush;

            // 1. Update Sidebar Subtitles with bullet •
            string pPhase = s.PomoIsWork
                ? LocalizationService.T("focus.focus", "Focus")
                : LocalizationService.T("focus.break", "Break");
            string pSub = $"{TimerService.FormatDuration(s.PomoRemainingSeconds)} • {(!string.IsNullOrWhiteSpace(s.PomoTitle) ? s.PomoTitle : pPhase)}";
            if (_subPomo.Text != pSub) _subPomo.Text = pSub;

            string cSub = TimerService.FormatDuration(s.CountRemainingSeconds);
            if (!string.IsNullOrWhiteSpace(s.CountTitle)) cSub += $" • {s.CountTitle}";
            if (_subCountdown.Text != cSub) _subCountdown.Text = cSub;

            var nearestAlarm = s.GetNearestEnabledAlarm(out TimeSpan alarmRem, out _);
            string aSub = nearestAlarm != null
                ? $"{nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2} • in {(int)alarmRem.TotalHours}h {alarmRem.Minutes}m"
                : "No active alarm";
            if (_subAlarm.Text != aSub) _subAlarm.Text = aSub;

            var sw = s.StopwatchElapsed;
            string swSub = sw.TotalHours >= 1.0
                ? $"{(int)sw.TotalHours:D2}:{sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 10:D2}"
                : $"{sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 10:D2}";
            if (_subStopwatch.Text != swSub) _subStopwatch.Text = swSub;

            string wSub = $"{s.WaterGlasses} / {s.WaterTarget} • {s.WaterGlasses * 250} ml";
            if (_subWater.Text != wSub) _subWater.Text = wSub;

            // 2. Update Drum Roller Tumblers
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                {
                    int rem = s.PomoRemainingSeconds;
                    int m = rem / 60;
                    int sec = rem % 60;

                    _drumLeftTop.Text = $"{((m - 1 + 100) % 100):D2}";
                    _drumLeftActive.Text = $"{m:D2}";
                    _drumLeftBottom.Text = $"{((m + 1) % 100):D2}";

                    _drumRightTop.Text = $"{((sec - 1 + 60) % 60):D2}";
                    _drumRightActive.Text = $"{sec:D2}";
                    _drumRightBottom.Text = $"{((sec + 1) % 60):D2}";

                    string tName = !string.IsNullOrWhiteSpace(s.PomoTitle) ? TimerService.StripEmojis(s.PomoTitle) : "Code";
                    if (string.IsNullOrWhiteSpace(tName)) tName = "Code";
                    _drumSubIndicatorText.Text = $"{tName} • {pPhase} {s.PomoCurrentCycle}/{s.PomoTargetCycles}";
                    break;
                }
                case TimerMode.Countdown:
                {
                    int rem = s.CountRemainingSeconds;
                    int m = rem / 60;
                    int sec = rem % 60;

                    _drumLeftTop.Text = $"{((m - 1 + 100) % 100):D2}";
                    _drumLeftActive.Text = $"{m:D2}";
                    _drumLeftBottom.Text = $"{((m + 1) % 100):D2}";

                    _drumRightTop.Text = $"{((sec - 1 + 60) % 60):D2}";
                    _drumRightActive.Text = $"{sec:D2}";
                    _drumRightBottom.Text = $"{((sec + 1) % 60):D2}";

                    var ringAt = DateTime.Now.AddSeconds(rem);
                    string cTitle = !string.IsNullOrWhiteSpace(s.CountTitle) ? TimerService.StripEmojis(s.CountTitle) : "Countdown";
                    _drumSubIndicatorText.Text = s.CountIsRunning
                        ? $"Rings at {ringAt:HH:mm}"
                        : $"Ready • {cTitle}";
                    break;
                }
                case TimerMode.Alarm:
                {
                    int h = s.DraftAlarmHour;
                    int m = s.DraftAlarmMinute;

                    _drumLeftTop.Text = $"{((h - 1 + 24) % 24):D2}";
                    _drumLeftActive.Text = $"{h:D2}";
                    _drumLeftBottom.Text = $"{((h + 1) % 24):D2}";

                    _drumRightTop.Text = $"{((m - 1 + 60) % 60):D2}";
                    _drumRightActive.Text = $"{m:D2}";
                    _drumRightBottom.Text = $"{((m + 1) % 60):D2}";

                    if (nearestAlarm != null)
                    {
                        _drumSubIndicatorText.Text = $"{nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2} • in {(int)alarmRem.TotalHours}h {alarmRem.Minutes}m";
                    }
                    else
                    {
                        _drumSubIndicatorText.Text = "Alarm Clock • Ready";
                    }
                    break;
                }
                case TimerMode.Stopwatch:
                {
                    int m = sw.Minutes;
                    int sec = sw.Seconds;

                    _drumLeftTop.Text = $"{((m - 1 + 100) % 100):D2}";
                    _drumLeftActive.Text = $"{m:D2}";
                    _drumLeftBottom.Text = $"{((m + 1) % 100):D2}";

                    _drumRightTop.Text = $"{((sec - 1 + 60) % 60):D2}";
                    _drumRightActive.Text = $"{sec:D2}";
                    _drumRightBottom.Text = $"{((sec + 1) % 60):D2}";

                    _drumSubIndicatorText.Text = $".{sw.Milliseconds / 10:D2} ms • {(s.Laps.Count > 0 ? $"Lap #{s.Laps.Count + 1}" : (s.StopwatchIsRunning ? "Running" : "Ready"))}";
                    break;
                }
                case TimerMode.Water:
                {
                    int g = s.WaterGlasses;
                    int t = s.WaterTarget;

                    _drumLeftTop.Text = $"{Math.Max(0, g - 1):D2}";
                    _drumLeftActive.Text = $"{g:D2}";
                    _drumLeftBottom.Text = $"{(g + 1):D2}";

                    _drumRightTop.Text = $"{Math.Max(1, t - 1):D2}";
                    _drumRightActive.Text = $"{t:D2}";
                    _drumRightBottom.Text = $"{(t + 1):D2}";

                    _drumSubIndicatorText.Text = $"{g * 250} ml • Target {t * 250} ml";
                    break;
                }
            }
        }

        public void UpdateVisuals()
        {
            var s = TimerService.Instance;
            var accentBrush = GetAccentBrush();
            Color accentColor = s.GetEffectiveAccentColor();

            // Sync Accent Colors on Action Buttons & Dots
            _pomoPlayBtn.Background = accentBrush;
            _countPlayBtn.Background = accentBrush;
            _alarmAddBtn.Background = accentBrush;
            _swPlayBtn.Background = accentBrush;
            _waterAddGlassBtn.Background = accentBrush;
            _dotPomo.Fill = accentBrush;
            _dotCountdown.Fill = accentBrush;
            _dotAlarm.Fill = accentBrush;
            _dotStopwatch.Fill = accentBrush;
            _dotWater.Fill = accentBrush;

            // Ensure high-contrast text and icons inside action buttons (never white-on-white)
            double btnLum = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
            var btnFgBrush = btnLum > 0.60 ? Brushes.Black : Brushes.White;

            if (_pomoPlayIcon != null) _pomoPlayIcon.Fill = btnFgBrush;
            if (_countPlayIcon != null) _countPlayIcon.Fill = btnFgBrush;
            if (_swPlayIcon != null) _swPlayIcon.Fill = btnFgBrush;
            if (_alarmAddBtn.Child is TextBlock alarmTb) alarmTb.Foreground = btnFgBrush;
            if (_waterAddGlassBtn.Child is StackPanel waterSp)
            {
                foreach (var child in waterSp.Children)
                {
                    if (child is Path pIcon) pIcon.Fill = btnFgBrush;
                    else if (child is TextBlock wTb) wTb.Foreground = btnFgBrush;
                }
            }

            _dotPomo.Visibility = s.PomoIsRunning ? Visibility.Visible : Visibility.Collapsed;
            _dotCountdown.Visibility = s.CountIsRunning ? Visibility.Visible : Visibility.Collapsed;
            _dotAlarm.Visibility = s.Alarms.Any(a => a.IsEnabled) ? Visibility.Visible : Visibility.Collapsed;
            _dotStopwatch.Visibility = s.StopwatchIsRunning ? Visibility.Visible : Visibility.Collapsed;
            _dotWater.Visibility = s.WaterReminderEnabled ? Visibility.Visible : Visibility.Collapsed;

            _subWater.Text = $"{s.WaterGlasses} / {s.WaterTarget} • {s.WaterGlasses * 250} ml";

            UpdateFastTickVisuals();

            // Pomodoro Controls
            string pPhase = s.PomoIsWork
                ? LocalizationService.T("focus.focus", "Focus")
                : LocalizationService.T("focus.break", "Break");
            _cycleHeaderText.Text = string.Format(LocalizationService.T("focus.cycle_header_fmt", "{0} {1} of {2}"), pPhase, s.PomoCurrentCycle, s.PomoTargetCycles);
            int nextBreakMins = s.PomoBreakSeconds / 60;
            int nextBreakSecs = s.PomoBreakSeconds % 60;
            _cycleSubText.Text = string.Format(LocalizationService.T("focus.next_break_fmt", "Break {0} next"), $"{nextBreakMins:D2}:{nextBreakSecs:D2}");
            UpdateCycleDots(s.PomoCurrentCycle, s.PomoTargetCycles, accentColor);

            _pomoPlayIcon.Data = s.PomoIsRunning ? TimerIcons.PauseGeometry : TimerIcons.PlayGeometry;
            _focusMinsText.Text = $"Focus {s.PomoWorkSeconds / 60}m";
            _breakMinsText.Text = $"Break {s.PomoBreakSeconds / 60}m";
            _longMinsText.Text = $"Long {s.PomoLongBreakSeconds / 60}m";
            _cyclesText.Text = $"Cycles {s.PomoTargetCycles}";
            UpdateMiniToggleVisual(_autoToggleTrack, _autoToggleThumb, s.PomoAutoStartNext, accentColor);
            _completedText.Text = $"{s.PomoSessionsToday} done ({s.PomoFocusMinutesToday}m)";

            // Update Pomo Presets Active Highlight
            string curTitle = s.PomoTitle ?? "";
            for (int i = 0; i < PomoPresetsList.Length; i++)
            {
                if (_pomoPresetChips[i] != null)
                {
                    bool isSelected = string.Equals(curTitle, PomoPresetsList[i].Label, StringComparison.OrdinalIgnoreCase);
                    _pomoPresetChips[i].BorderBrush = isSelected ? accentBrush : CapsuleTheme.Brush(CapsuleTheme.Current.Border);
                    _pomoPresetChips[i].Background = isSelected ? CapsuleTheme.Brush(CapsuleTheme.Current.Surface2) : CapsuleTheme.Brush(CapsuleTheme.Current.Surface);
                    if (_pomoPresetChipIcons[i] != null)
                    {
                        _pomoPresetChipIcons[i].Stroke = isSelected ? accentBrush : CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
                    }
                }
            }

            // Countdown Controls
            int baseSec = s.CountIsRunning ? s.CountRemainingSeconds : s.CountTotalSeconds;
            int ch = baseSec / 3600;
            int cm = (baseSec % 3600) / 60;
            int cs = baseSec % 60;
            if (!_countHoursInput.IsKeyboardFocused) _countHoursInput.Text = $"{ch:D2}";
            if (!_countMinsInput.IsKeyboardFocused) _countMinsInput.Text = $"{cm:D2}";
            if (!_countSecsInput.IsKeyboardFocused) _countSecsInput.Text = $"{cs:D2}";
            if (!_countTargetTimeInput.IsKeyboardFocused)
            {
                var target = s.CountTargetClockTime ?? DateTime.Now.AddSeconds(baseSec);
                _countTargetTimeInput.Text = target.ToString("HH:mm");
            }
            _countPlayIcon.Data = s.CountIsRunning ? TimerIcons.PauseGeometry : TimerIcons.PlayGeometry;
            _countLoopBtn.Background = s.CountLoop ? accentBrush : CapsuleTheme.Brush(CapsuleTheme.Current.Surface);
            _countSoundBtnText.Text = $"Sound: {s.SelectedSoundTone}";
            _countRepeatBtnText.Text = $"{s.RingRepeatCount}x";
            _countRingBanner.Visibility = s.IsRinging ? Visibility.Visible : Visibility.Collapsed;
            RebuildCountdownPresets();

            // Alarm Controls
            if (!_alarmHourInput.IsKeyboardFocused) _alarmHourInput.Text = $"{s.DraftAlarmHour:D2}";
            if (!_alarmMinInput.IsKeyboardFocused) _alarmMinInput.Text = $"{s.DraftAlarmMinute:D2}";
            _alarmSoundBtnText.Text = $"Sound: {s.SelectedSoundTone}";
            _alarmSnoozeBtnText.Text = s.DefaultSnoozeMinutes > 0 ? $"{s.DefaultSnoozeMinutes}m" : "Off";
            _alarmRepeatBtnText.Text = s.DraftAlarmRepeatDaily ? "Daily" : "Once";
            UpdateMiniToggleVisual(_alarmNotchToggleTrack, _alarmNotchToggleThumb, s.ShowAlarmInNotch, accentColor);

            var draftTarget = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, s.DraftAlarmHour, s.DraftAlarmMinute, 0);
            if (draftTarget <= DateTime.Now) draftTarget = draftTarget.AddDays(1);
            var spanUntilDraft = draftTarget - DateTime.Now;
            _alarmRemainingPreviewText.Text = $"Selected time ({s.DraftAlarmHour:D2}:{s.DraftAlarmMinute:D2}) is in {(int)spanUntilDraft.TotalHours}h {spanUntilDraft.Minutes}m";
            RebuildAlarmsList(accentColor);

            // Stopwatch Controls
            _swPlayIcon.Data = s.StopwatchIsRunning ? TimerIcons.PauseGeometry : TimerIcons.PlayGeometry;
            RebuildStopwatchLapsList();

            // Water Controls
            int currentMl = s.WaterGlasses * 250;
            int targetMl = s.WaterTarget * 250;
            int waterPct = targetMl > 0 ? (int)Math.Round((double)currentMl / targetMl * 100.0) : 0;
            _waterMlSummaryText.Text = $"{currentMl} ml / {targetMl} ml ({waterPct}%)";
            _waterGoalStepperText.Text = $"Goal: {s.WaterTarget} ({targetMl}ml)";

            _waterDropsVisualPanel.Children.Clear();
            int maxDrops = Math.Min(s.WaterTarget, 14);
            for (int i = 1; i <= maxDrops; i++)
            {
                bool isDrank = i <= s.WaterGlasses;
                var drop = new Path
                {
                    Data = TimerIcons.WaterDropGeometry,
                    Fill = isDrank ? accentBrush : Brushes.Transparent,
                    Stroke = isDrank ? Brushes.Transparent : CapsuleTheme.Brush(CapsuleTheme.Current.Text2),
                    StrokeThickness = isDrank ? 0 : 1.4,
                    Width = 12,
                    Height = 12,
                    Stretch = Stretch.Uniform,
                    Opacity = isDrank ? 1.0 : 0.35,
                    Margin = new Thickness(2, 0, 2, 0)
                };
                _waterDropsVisualPanel.Children.Add(drop);
            }

            UpdateMiniToggleVisual(_waterReminderToggleTrack, _waterReminderToggleThumb, s.WaterReminderEnabled, accentColor);
            UpdateMiniToggleVisual(_waterNotchToggleTrack, _waterNotchToggleThumb, s.ShowWaterInNotch, accentColor);
            _waterReminderStatusText.Foreground = accentBrush;
            _waterReminderStatusText.Text = s.WaterReminderEnabled
                ? TimerService.FormatDuration(s.WaterReminderRemainingSeconds)
                : "Off";

            HighlightIntervalChip(_waterInt30Btn, s.WaterReminderIntervalMinutes == 30, accentColor);
            HighlightIntervalChip(_waterInt45Btn, s.WaterReminderIntervalMinutes == 45, accentColor);
            HighlightIntervalChip(_waterInt60Btn, s.WaterReminderIntervalMinutes == 60, accentColor);
            HighlightIntervalChip(_waterInt90Btn, s.WaterReminderIntervalMinutes == 90, accentColor);
        }

        private void RebuildCountdownPresets()
        {
            _countPresetsWrap.Children.Clear();
            var s = TimerService.Instance;
            foreach (int sec in s.CustomCountdownPresetsSeconds)
            {
                int capturedSec = sec;
                string label = capturedSec >= 3600 && capturedSec % 3600 == 0
                    ? $"{capturedSec / 3600}h"
                    : (capturedSec >= 60 && capturedSec % 60 == 0
                        ? $"{capturedSec / 60}m"
                        : $"{capturedSec}s");

                var chip = CreateQuickChip(
                    label,
                    () => s.SetCountdownPreset(capturedSec, autoStart: true),
                    () => s.RemoveCustomCountdownPreset(capturedSec));
                chip.ToolTip = "Left-click: Start · Right-click: Remove preset";
                _countPresetsWrap.Children.Add(chip);
            }
        }

        private void RebuildAlarmsList(Color accentColor)
        {
            var p = CapsuleTheme.Current;
            var alarms = TimerService.Instance.Alarms;
            _alarmsEmptyHint.Visibility = alarms.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            _alarmsListStack.Children.Clear();
            if (alarms.Count == 0) return;

            var now = DateTime.Now;
            foreach (var al in alarms)
            {
                var captured = al;
                var row = new DockPanel { Margin = new Thickness(2, 2, 2, 2) };

                var delBtn = new Border
                {
                    Width = 18,
                    Height = 18,
                    Background = Brushes.Transparent,
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(6, 0, 0, 0),
                    ToolTip = "Delete alarm",
                    Child = new Path
                    {
                        Data = TimerIcons.CloseGeometry,
                        Stroke = CapsuleTheme.Brush(p.Text2),
                        StrokeThickness = 1.6,
                        StrokeStartLineCap = PenLineCap.Round,
                        StrokeEndLineCap = PenLineCap.Round,
                        Width = 9,
                        Height = 9,
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                delBtn.MouseEnter += (_, _) =>
                {
                    if (delBtn.Child is Path path) path.Stroke = CapsuleTheme.Brush(p.Text);
                };
                delBtn.MouseLeave += (_, _) =>
                {
                    if (delBtn.Child is Path path) path.Stroke = CapsuleTheme.Brush(p.Text2);
                };
                delBtn.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    TimerService.Instance.RemoveAlarm(captured.Id);
                };
                DockPanel.SetDock(delBtn, Dock.Right);
                row.Children.Add(delBtn);

                var toggle = CreateMiniToggle("", out var trk, out var thm, () => TimerService.Instance.ToggleAlarmEnabled(captured.Id));
                UpdateMiniToggleVisual(trk, thm, captured.IsEnabled, accentColor);
                DockPanel.SetDock(toggle, Dock.Right);
                row.Children.Add(toggle);

                var rem = captured.GetNextTargetTime(now) - now;
                string remStr = captured.IsEnabled
                    ? $"in {(int)rem.TotalHours}h {rem.Minutes}m"
                    : "Off";

                var leftInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                leftInfo.Children.Add(new TextBlock
                {
                    Text = $"{captured.Hour:D2}:{captured.Minute:D2}",
                    FontSize = 12,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas, Segoe UI"),
                    Foreground = captured.IsEnabled ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2),
                    Margin = new Thickness(0, 0, 6, 0)
                });
                leftInfo.Children.Add(new TextBlock
                {
                    Text = !string.IsNullOrWhiteSpace(captured.Title) ? $"{captured.Title} • {remStr}" : remStr,
                    FontSize = 10.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center
                });
                row.Children.Add(leftInfo);

                _alarmsListStack.Children.Add(row);
            }
        }

        private static void HighlightIntervalChip(Border chip, bool active, Color accentColor)
        {
            if (chip == null) return;
            var p = CapsuleTheme.Current;
            chip.Background = active ? new SolidColorBrush(accentColor) : CapsuleTheme.Brush(p.Surface);
            if (chip.Child is TextBlock tb)
            {
                tb.Foreground = active ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text);
            }
        }

        private void RebuildStopwatchLapsList()
        {
            var p = CapsuleTheme.Current;
            var laps = TimerService.Instance.Laps;
            _swEmptyLapsHint.Visibility = laps.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (_swLapsStack.Children.Count == laps.Count) return;

            _swLapsStack.Children.Clear();
            if (laps.Count == 0) return;

            int fastestLapNum = -1;
            int slowestLapNum = -1;
            if (laps.Count >= 2)
            {
                fastestLapNum = laps.OrderBy(l => l.SplitTime).First().LapNumber;
                slowestLapNum = laps.OrderByDescending(l => l.SplitTime).First().LapNumber;
            }

            foreach (var lap in laps)
            {
                var row = new DockPanel { Margin = new Thickness(2, 2, 2, 2) };

                Brush rowBrush = CapsuleTheme.Brush(p.Text);
                string badge = "";
                if (lap.LapNumber == fastestLapNum)
                {
                    rowBrush = new SolidColorBrush(Color.FromRgb(48, 209, 88));
                    badge = " (Best)";
                }
                else if (lap.LapNumber == slowestLapNum)
                {
                    rowBrush = new SolidColorBrush(Color.FromRgb(255, 69, 58));
                    badge = " (Slow)";
                }

                var numBlock = new TextBlock
                {
                    Text = $"Lap #{lap.LapNumber}{badge}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = rowBrush,
                    Width = 75
                };
                DockPanel.SetDock(numBlock, Dock.Left);
                row.Children.Add(numBlock);

                var totalBlock = new TextBlock
                {
                    Text = FormatLapSpan(lap.TotalTime),
                    FontSize = 11,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Right
                };
                DockPanel.SetDock(totalBlock, Dock.Right);
                row.Children.Add(totalBlock);

                var splitBlock = new TextBlock
                {
                    Text = $"+{FormatLapSpan(lap.SplitTime)}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = rowBrush,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                row.Children.Add(splitBlock);

                _swLapsStack.Children.Add(row);
            }
        }

        private static string FormatLapSpan(TimeSpan ts)
        {
            return ts.TotalHours >= 1.0
                ? $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}"
                : $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds / 10:D2}";
        }

        private void UpdateCycleDots(int current, int total, Color activeColor)
        {
            var p = CapsuleTheme.Current;
            _cycleDotsPanel.Children.Clear();
            for (int i = 1; i <= Math.Min(total, 8); i++)
            {
                bool isCompletedOrActive = i <= current;
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Margin = new Thickness(3, 0, 3, 0),
                    Fill = isCompletedOrActive ? new SolidColorBrush(activeColor) : Brushes.Transparent,
                    Stroke = isCompletedOrActive ? new SolidColorBrush(activeColor) : CapsuleTheme.Brush(p.Border),
                    StrokeThickness = 1.2
                };
                _cycleDotsPanel.Children.Add(dot);
            }
        }

        private static void UpdateMiniToggleVisual(Border track, Ellipse thumb, bool on, Color activeColor)
        {
            if (track == null || thumb == null) return;
            var p = CapsuleTheme.Current;
            track.Background = on ? new SolidColorBrush(activeColor) : CapsuleTheme.Brush(p.Surface2);
            thumb.Fill = on ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text2);
            Canvas.SetLeft(thumb, on ? 16 : 2);
        }

        #endregion
    }
}
