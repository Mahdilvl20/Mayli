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
    /// TimerToolView: Full multi-mode Timers & Phone-Style Alarm view:
    /// 1. Pomodoro (Custom title, Focus/Break/Long Break, Cycles, Auto-start, +1m/+5m)
    /// 2. Countdown (Direct editable H:M:S + "Ring in X mins" + "Ring at HH:MM", Custom saved presets, Sound & Loop)
    /// 3. Alarm (Phone-style HH:MM & "In X mins" alarm creator, Multiple alarms list, 6 Synthesized Sounds + Test, Snooze, Daily repeat)
    /// 4. Stopwatch (Sub-second 0->100% progress ring, Lap recording with Fastest/Slowest badges & Copy)
    /// 5. Water / Hydration (Daily ml progress, auto-reminder countdown, Notch toggle)
    /// </summary>
    public sealed class TimerToolView : UserControl
    {
        private TimerMode _currentMode = TimerMode.Pomodoro;

        // Left Sidebar Rows
        private Border _rowPomo;
        private Border _rowCountdown;
        private Border _rowAlarm;
        private Border _rowStopwatch;
        private Border _rowWater;

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

        // Center Circular Progress Ring
        private Path _ringTrack;
        private Path _ringProgress;
        private TextBlock _centerTimeText;
        private TextBlock _centerPhaseText;
        private TextBlock _centerSubInfoText;
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
        private TextBlock _cycleHeaderText;
        private TextBlock _cycleSubText;
        private StackPanel _cycleDotsPanel;
        private Border _pomoPlayBtn;
        private TextBlock _pomoPlayIcon;
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
        private TextBlock _countPlayIcon;
        private Border _countLoopBtn;
        private WrapPanel _countPresetsWrap;
        private TextBlock _countSoundBtnText;
        private TextBlock _countRepeatBtnText;
        private Border _countRingBanner;

        // Alarm Controls (Phone-style Clock Alarm)
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
        private TextBlock _swPlayIcon;
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
                Margin = new Thickness(4, 0, 4, 0),
                FlowDirection = FlowDirection.LeftToRight
            };
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(168) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(164) });
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
                "🍅",
                LocalizationService.T("focus.pomodoro", "Pomodoro"),
                "25:00 · Focus",
                out _subPomo,
                out _dotPomo,
                () => SwitchMode(TimerMode.Pomodoro));

            _rowCountdown = BuildModeRow(
                "⏳",
                LocalizationService.T("focus.countdown", "Timer"),
                "15:00 · Ready",
                out _subCountdown,
                out _dotCountdown,
                () => SwitchMode(TimerMode.Countdown));

            _rowAlarm = BuildModeRow(
                "⏰",
                LocalizationService.T("focus.alarm", "Alarm Clock"),
                "Set time or +min",
                out _subAlarm,
                out _dotAlarm,
                () => SwitchMode(TimerMode.Alarm));

            _rowStopwatch = BuildModeRow(
                "⏱",
                LocalizationService.T("focus.stopwatch", "Stopwatch"),
                "00:00.00",
                out _subStopwatch,
                out _dotStopwatch,
                () => SwitchMode(TimerMode.Stopwatch));

            _rowWater = BuildModeRow(
                "💧",
                LocalizationService.T("focus.water", "Hydration"),
                "0 / 8 · 0 ml",
                out _subWater,
                out _dotWater,
                () => SwitchMode(TimerMode.Water));

            leftStack.Children.Add(_rowPomo);
            leftStack.Children.Add(_rowCountdown);
            leftStack.Children.Add(_rowAlarm);
            leftStack.Children.Add(_rowStopwatch);
            leftStack.Children.Add(_rowWater);

            Grid.SetColumn(leftStack, 0);
            rootGrid.Children.Add(leftStack);

            // -------------------------------------------------------------
            // COLUMN 1: CENTER CIRCULAR PROGRESS RING + QUICK PILLS
            // -------------------------------------------------------------
            var centerOuterStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var centerContainer = new Grid
            {
                Width = 148,
                Height = 148,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = "Click: Start / Pause · Right-click: Stop & Reset · Scroll: Adjust time"
            };
            centerContainer.MouseWheel += OnCenterRingMouseWheel;
            centerContainer.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ToggleCurrentModePrimaryAction();
            };
            centerContainer.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                ResetCurrentModeAction();
            };

            var p = CapsuleTheme.Current;
            _ringTrack = new Path
            {
                Stroke = CapsuleTheme.Brush(p.Border),
                StrokeThickness = 8.5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _ringProgress = new Path
            {
                Stroke = GetAccentBrush(),
                StrokeThickness = 8.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            centerContainer.Children.Add(_ringTrack);
            centerContainer.Children.Add(_ringProgress);

            var centerTextStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _centerTimeText = new TextBlock
            {
                Text = "25:00",
                FontSize = 23,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _centerPhaseText = new TextBlock
            {
                Text = "Focus",
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetAccentBrush(),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 118,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            _centerSubInfoText = new TextBlock
            {
                Text = "Cycle 1 of 4",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            centerTextStack.Children.Add(_centerTimeText);
            centerTextStack.Children.Add(_centerPhaseText);
            centerTextStack.Children.Add(_centerSubInfoText);
            centerContainer.Children.Add(centerTextStack);

            centerOuterStack.Children.Add(centerContainer);

            _centerQuickPillsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 0),
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
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(6, 0, 0, 0)
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

            DrawCircularRingTrack();
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

        #region Panel Builders

        private Grid BuildPomodoroPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            stack.Children.Add(BuildTitleInputBar(
                LocalizationService.T("focus.pomo_title_placeholder", "Focus title (shown in Notch)..."),
                TimerService.Instance.PomoTitle,
                out _pomoTitleBox,
                text => TimerService.Instance.SetPomoTitle(text),
                new[] { "💻 Code", "📚 Study", "🎯 Focus", "✍️ Write" }));

            var headerAndTransportDock = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };

            var transportRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var resetBtn = CreateCircleIconButton("⏹", 28, "Stop & Reset", () => TimerService.Instance.ResetPomo());
            transportRow.Children.Add(resetBtn);

            _pomoPlayIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 13,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _pomoPlayBtn = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = GetAccentBrush(),
                Cursor = Cursors.Hand,
                ToolTip = "Start / Pause",
                Margin = new Thickness(6, 0, 6, 0),
                Child = _pomoPlayIcon
            };
            _pomoPlayBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.TogglePomo();
            };
            transportRow.Children.Add(_pomoPlayBtn);

            var skipBtn = CreateCircleIconButton("⏭", 28, "Skip Phase", () => TimerService.Instance.SkipPomo());
            transportRow.Children.Add(skipBtn);

            DockPanel.SetDock(transportRow, Dock.Right);
            headerAndTransportDock.Children.Add(transportRow);

            var cycleInfoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var topCycleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _cycleHeaderText = new TextBlock
            {
                Text = "Focus 1 of 4",
                FontSize = 12.0,
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

            var steppersGrid = new UniformGrid { Columns = 2, Rows = 2, Margin = new Thickness(0, 2, 0, 6) };
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

        private Grid BuildCountdownPanel()
        {
            var p = CapsuleTheme.Current;
            var grid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            // 1. Title Bar + Quick Tags
            stack.Children.Add(BuildTitleInputBar(
                LocalizationService.T("focus.count_title_placeholder", "Timer label (e.g. Tea, Workout, Exam)..."),
                TimerService.Instance.CountTitle,
                out _countTitleBox,
                text => TimerService.Instance.SetCountdownTitle(text),
                new[] { "☕ Coffee", "🍳 Cook", "💊 Pill", "🏋️ Gym", "🧘 Rest" }));

            // 2. Direct Editable H : M : S + "Ring at HH:MM" + Transport Controls
            var mainControlRow = new DockPanel { Margin = new Thickness(0, 3, 0, 4) };

            var rightControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var stopBtn = CreateCircleIconButton("⏹", 28, "Stop & Reset", () => TimerService.Instance.ResetCountdown());
            rightControls.Children.Add(stopBtn);

            _countPlayIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _countPlayBtn = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = GetAccentBrush(),
                Cursor = Cursors.Hand,
                ToolTip = "Start / Pause Timer",
                Margin = new Thickness(5, 0, 5, 0),
                Child = _countPlayIcon
            };
            _countPlayBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CommitTypedCountdownIfEdited();
                TimerService.Instance.ToggleCountdown();
            };
            rightControls.Children.Add(_countPlayBtn);

            _countLoopBtn = CreateCircleIconButton("🔁", 28, "Auto-Repeat (Loop)", () => TimerService.Instance.ToggleCountdownLoop());
            rightControls.Children.Add(_countLoopBtn);

            DockPanel.SetDock(rightControls, Dock.Right);
            mainControlRow.Children.Add(rightControls);

            // Left: Editable H : M : S Boxes + Target Clock Time ("چه ساعتی زنگ بخوره")
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

            // "Ring at HH:MM" direct box ("چه ساعتی زنگ بخوره")
            var atTimeBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(3, 0, 0, 0),
                ToolTip = "Type exact clock time (HH:MM) and press Enter to ring at that time!"
            };
            var atTimeStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            atTimeStack.Children.Add(new TextBlock
            {
                Text = "⏰ At HH:MM",
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

            // 3. Preset Duration Chips + [+ Save Custom]
            var presetsHeaderRow = new DockPanel { Margin = new Thickness(0, 1, 0, 2) };
            _countTargetTimePreviewText = new TextBlock
            {
                Text = "🔔 Rings at --:--",
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

            // 4. Sound & Ring Customization Bar + Active Ringing Controls
            var customRow = new DockPanel { Margin = new Thickness(0, 2, 0, 0) };

            var soundControls = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var soundCycleBtn = CreateActionPill("🔔 Sound: Classic", out _countSoundBtnText, () => TimerService.Instance.CycleSoundTone());
            soundControls.Children.Add(soundCycleBtn);

            var testSoundBtn = CreateQuickChip("🔊 Test", () => TimerService.Instance.PlaySoundPreview());
            soundControls.Children.Add(testSoundBtn);

            var repeatCycleBtn = CreateActionPill("🔁 Ring: 2x", out _countRepeatBtnText, () => TimerService.Instance.CycleRingRepeatCount());
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
            ringBtns.Children.Add(CreateQuickChip("💤 Snooze", () => TimerService.Instance.SnoozeLastRing()));
            ringBtns.Children.Add(CreateQuickChip("✕ Stop", () => TimerService.Instance.StopRingingSound()));
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
                new[] { "⏰ Wake", "💼 Meet", "💊 Pill", "🍳 Food", "📞 Call" }));

            // 2. Phone-style Time Picker ("چه ساعتی زنگ بخوره" HH:MM) + "چند دقیقه دیگه" (In X min)
            var pickerDock = new DockPanel { Margin = new Thickness(0, 3, 0, 4) };

            var rightAddActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            TextBlock dailyBtnText = null;
            _alarmDailyBtn = CreateActionPill(
                "🔁 Once",
                out dailyBtnText,
                () =>
                {
                    TimerService.Instance.DraftAlarmRepeatDaily = !TimerService.Instance.DraftAlarmRepeatDaily;
                    if (dailyBtnText != null)
                        dailyBtnText.Text = TimerService.Instance.DraftAlarmRepeatDaily ? "🔁 Daily" : "🔁 Once";
                    UpdateVisuals();
                });
            rightAddActions.Children.Add(_alarmDailyBtn);

            _alarmAddBtn = new Border
            {
                Height = 32,
                CornerRadius = new CornerRadius(16),
                Background = GetAccentBrush(),
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(4, 0, 0, 0),
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

            // Direct "In X mins" ("چند دقیقه دیگه زنگ بخوره") input box
            var inMinBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(5, 0, 0, 0),
                ToolTip = "Type how many minutes from now and press Enter!"
            };
            var inMinStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inMinStack.Children.Add(new TextBlock
            {
                Text = "⏳ In +Min",
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

            // 3. Quick "Ring in X minutes" chips ("چند دقیقه دیگه زنگ بخوره")
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

            // 4. Customization Row (Sound, Snooze, Repeat, Notch toggle)
            var customRow = new DockPanel { Margin = new Thickness(0, 1, 0, 4) };
            var customLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            customLeft.Children.Add(CreateActionPill("🔔 Classic", out _alarmSoundBtnText, () => TimerService.Instance.CycleSoundTone()));
            customLeft.Children.Add(CreateQuickChip("🔊", () => TimerService.Instance.PlaySoundPreview()));
            customLeft.Children.Add(CreateActionPill("💤 5m", out _alarmSnoozeBtnText, () =>
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
            customLeft.Children.Add(CreateActionPill("🔁 2x", out _alarmRepeatBtnText, () => TimerService.Instance.CycleRingRepeatCount()));

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
                CornerRadius = new CornerRadius(10),
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
                new[] { "🏃 Run", "⚡ Sprint", "🧩 Task", "🎮 Speedrun" }));

            var controlsRow = new DockPanel { Margin = new Thickness(0, 4, 0, 6) };

            var leftActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            _swPlayIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.AccentFg),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _swPlayBtn = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = GetAccentBrush(),
                Cursor = Cursors.Hand,
                ToolTip = "Start / Stop Stopwatch",
                Margin = new Thickness(0, 0, 8, 0),
                Child = _swPlayIcon
            };
            _swPlayBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                TimerService.Instance.ToggleStopwatch();
            };
            leftActions.Children.Add(_swPlayBtn);

            _swLapBtn = CreateActionPill("🏁 Lap", out _, () => TimerService.Instance.RecordStopwatchLap());
            leftActions.Children.Add(_swLapBtn);

            var resetSwBtn = CreateActionPill("⏹ Reset", out _, () => TimerService.Instance.ResetStopwatch());
            leftActions.Children.Add(resetSwBtn);

            DockPanel.SetDock(leftActions, Dock.Left);
            controlsRow.Children.Add(leftActions);

            var copyLapsBtn = CreateActionPill("📋 Copy", out _, CopyStopwatchLapsToClipboard);
            copyLapsBtn.HorizontalAlignment = HorizontalAlignment.Right;
            controlsRow.Children.Add(copyLapsBtn);

            stack.Children.Add(controlsRow);

            var lapsContainer = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
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
                CornerRadius = new CornerRadius(10),
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

            var minusGlassBtn = CreateActionPill("-1 Glass", out _, () => TimerService.Instance.RemoveWaterGlass());
            Grid.SetColumn(minusGlassBtn, 0);
            actionsRow.Children.Add(minusGlassBtn);

            _waterAddGlassBtn = new Border
            {
                Height = 30,
                CornerRadius = new CornerRadius(15),
                Background = GetAccentBrush(),
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(6, 0, 6, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = "💧 +1 Glass (250ml)",
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.AccentFg),
                    VerticalAlignment = VerticalAlignment.Center
                }
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
                CornerRadius = new CornerRadius(10),
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
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3, 6, 3)
            };

            var dock = new DockPanel();

            var clearBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0),
                ToolTip = "Clear title"
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
                    Background = CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
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
                CornerRadius = new CornerRadius(8),
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
            string icon,
            string title,
            string subtitle,
            out TextBlock subBlockOut,
            out Ellipse runningDotOut,
            Action onClick)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();

            var iconBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                Background = CapsuleTheme.Brush(p.Surface2),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = icon,
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
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
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });

            subBlockOut = new TextBlock
            {
                Text = subtitle,
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                TextTrimming = TextTrimming.CharacterEllipsis
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
                CornerRadius = new CornerRadius(8),
                Height = 27,
                Margin = new Thickness(2),
                Padding = new Thickness(6, 0, 6, 0)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var minusBtn = new TextBlock
            {
                Text = "−",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            minusBtn.MouseLeftButtonUp += (_, e) => { e.Handled = true; onMinus(); };
            Grid.SetColumn(minusBtn, 0);
            grid.Children.Add(minusBtn);

            labelOut = new TextBlock
            {
                Text = initialLabel,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelOut, 1);
            grid.Children.Add(labelOut);

            var plusBtn = new TextBlock
            {
                Text = "+",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand
            };
            plusBtn.MouseLeftButtonUp += (_, e) => { e.Handled = true; onPlus(); };
            Grid.SetColumn(plusBtn, 2);
            grid.Children.Add(plusBtn);

            pill.Child = grid;
            return pill;
        }

        private static Border CreateCircleIconButton(string glyph, double size, string tooltip, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var btn = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2.0),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Child = new TextBlock
                {
                    Text = glyph,
                    FontSize = 11.5,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btn.MouseEnter += (_, _) => btn.Opacity = 0.82;
            btn.MouseLeave += (_, _) => btn.Opacity = 1.0;
            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return btn;
        }

        private static Border CreateQuickChip(string label, Action onClick, Action onRightClick = null)
        {
            var p = CapsuleTheme.Current;
            var chip = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2.5, 7, 2.5),
                Margin = new Thickness(0, 1, 4, 2),
                Cursor = Cursors.Hand,
                Child = new TextBlock
                {
                    Text = label,
                    FontSize = 10.0,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            chip.MouseEnter += (_, _) => chip.Opacity = 0.82;
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

        private static Border CreateActionPill(string label, out TextBlock textOut, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var tb = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            textOut = tb;

            var pill = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                Child = tb
            };
            pill.MouseEnter += (_, _) => pill.Opacity = 0.82;
            pill.MouseLeave += (_, _) => pill.Opacity = 1.0;
            pill.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return pill;
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
            row.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 10.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });

            trackOut = new Border
            {
                Width = 28,
                Height = 15,
                CornerRadius = new CornerRadius(7.5),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };
            var canvas = new Canvas { Width = 28, Height = 15 };
            thumbOut = new Ellipse
            {
                Width = 11,
                Height = 11,
                Fill = CapsuleTheme.Brush(p.AccentFg)
            };
            Canvas.SetLeft(thumbOut, 2);
            Canvas.SetTop(thumbOut, 2);
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

        private void OnCenterRingMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            int delta = e.Delta > 0 ? +1 : -1;
            var s = TimerService.Instance;
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    s.AddPomoRemainingSeconds(delta * 60);
                    break;
                case TimerMode.Countdown:
                    s.AdjustCountdownComponents(0, delta, 0);
                    break;
                case TimerMode.Alarm:
                    s.AdjustDraftAlarmTime(0, delta * 5);
                    break;
                case TimerMode.Water:
                    if (delta > 0) s.AddWaterGlass(1);
                    else s.RemoveWaterGlass();
                    break;
            }
        }

        public void SwitchMode(TimerMode mode)
        {
            _currentMode = mode;
            TimerService.Instance.LastActiveMode = mode;

            var p = CapsuleTheme.Current;
            _rowPomo.Background = mode == TimerMode.Pomodoro ? CapsuleTheme.Brush(p.Surface2) : Brushes.Transparent;
            _rowCountdown.Background = mode == TimerMode.Countdown ? CapsuleTheme.Brush(p.Surface2) : Brushes.Transparent;
            _rowAlarm.Background = mode == TimerMode.Alarm ? CapsuleTheme.Brush(p.Surface2) : Brushes.Transparent;
            _rowStopwatch.Background = mode == TimerMode.Stopwatch ? CapsuleTheme.Brush(p.Surface2) : Brushes.Transparent;
            _rowWater.Background = mode == TimerMode.Water ? CapsuleTheme.Brush(p.Surface2) : Brushes.Transparent;

            _pomoPanel.Visibility = mode == TimerMode.Pomodoro ? Visibility.Visible : Visibility.Collapsed;
            _countdownPanel.Visibility = mode == TimerMode.Countdown ? Visibility.Visible : Visibility.Collapsed;
            _alarmPanel.Visibility = mode == TimerMode.Alarm ? Visibility.Visible : Visibility.Collapsed;
            _stopwatchPanel.Visibility = mode == TimerMode.Stopwatch ? Visibility.Visible : Visibility.Collapsed;
            _waterPanel.Visibility = mode == TimerMode.Water ? Visibility.Visible : Visibility.Collapsed;

            RebuildCenterQuickPills();
            UpdateVisuals();
        }

        private void RebuildCenterQuickPills()
        {
            _centerQuickPillsPanel.Children.Clear();
            var s = TimerService.Instance;

            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("-1m", () => s.AddPomoRemainingSeconds(-60)));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+1m", () => s.AddPomoRemainingSeconds(+60)));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+5m", () => s.AddPomoRemainingSeconds(+300)));
                    break;
                case TimerMode.Countdown:
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+30s", () => s.AddCountdownRunningSeconds(+30)));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+1m", () => s.AddCountdownRunningSeconds(+60)));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+5m", () => s.AddCountdownRunningSeconds(+300)));
                    break;
                case TimerMode.Alarm:
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+10m", () => s.AddQuickAlarmInMinutes(10, _alarmTitleBox?.Text)));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+30m", () => s.AddQuickAlarmInMinutes(30, _alarmTitleBox?.Text)));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+1h", () => s.AddQuickAlarmInMinutes(60, _alarmTitleBox?.Text)));
                    break;
                case TimerMode.Stopwatch:
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("🏁 Lap", () => s.RecordStopwatchLap()));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("⏹ Reset", () => s.ResetStopwatch()));
                    break;
                case TimerMode.Water:
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("-250ml", () => s.RemoveWaterGlass()));
                    _centerQuickPillsPanel.Children.Add(CreateQuickChip("+250ml", () => s.AddWaterGlass(1)));
                    break;
            }
        }

        /// <summary>
        /// High-frequency (40fps) lightweight update for silky-smooth progress ring and live digits without recreating UI trees.
        /// </summary>
        private void UpdateFastTickVisuals()
        {
            var s = TimerService.Instance;
            var accentBrush = GetAccentBrush();
            _ringProgress.Stroke = accentBrush;
            _centerPhaseText.Foreground = accentBrush;

            // 1. Update Sidebar Subtitles
            string pPhase = s.PomoIsWork
                ? LocalizationService.T("focus.focus", "Focus")
                : LocalizationService.T("focus.break", "Break");
            string pSub = $"{TimerService.FormatDuration(s.PomoRemainingSeconds)} · {(!string.IsNullOrWhiteSpace(s.PomoTitle) ? s.PomoTitle : pPhase)}";
            if (_subPomo.Text != pSub) _subPomo.Text = pSub;

            string cSub = TimerService.FormatDuration(s.CountRemainingSeconds);
            if (!string.IsNullOrWhiteSpace(s.CountTitle)) cSub += $" · {s.CountTitle}";
            if (_subCountdown.Text != cSub) _subCountdown.Text = cSub;

            var nearestAlarm = s.GetNearestEnabledAlarm(out TimeSpan alarmRem, out double alarmRatio);
            string aSub = nearestAlarm != null
                ? $"🔔 {nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2} ({TimerService.FormatDuration((int)Math.Ceiling(alarmRem.TotalSeconds))})"
                : "No active alarm";
            if (_subAlarm.Text != aSub) _subAlarm.Text = aSub;

            var sw = s.StopwatchElapsed;
            string swSub = sw.TotalHours >= 1.0
                ? $"{(int)sw.TotalHours:D2}:{sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 10:D2}"
                : $"{sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 10:D2}";
            if (_subStopwatch.Text != swSub) _subStopwatch.Text = swSub;

            // 2. Update Center Ring & Digits
            switch (_currentMode)
            {
                case TimerMode.Pomodoro:
                {
                    string tStr = TimerService.FormatDuration(s.PomoRemainingSeconds);
                    if (_centerTimeText.Text != tStr) _centerTimeText.Text = tStr;
                    _centerPhaseText.Text = !string.IsNullOrWhiteSpace(s.PomoTitle) ? s.PomoTitle : pPhase;
                    _centerSubInfoText.Text = $"{pPhase} · {s.PomoCurrentCycle}/{s.PomoTargetCycles}";
                    DrawCircularProgressArc(s.PomoIsRunning ? s.GetPomoRemainingRatio() : 0.0);
                    break;
                }
                case TimerMode.Countdown:
                {
                    string tStr = TimerService.FormatDuration(s.CountRemainingSeconds);
                    if (_centerTimeText.Text != tStr) _centerTimeText.Text = tStr;
                    _centerPhaseText.Text = !string.IsNullOrWhiteSpace(s.CountTitle) ? s.CountTitle : "Timer";
                    var ringAt = DateTime.Now.AddSeconds(s.CountRemainingSeconds);
                    _centerSubInfoText.Text = s.CountIsRunning
                        ? $"🔔 Rings at {ringAt:HH:mm}"
                        : $"Ready · {TimerService.FormatDuration(s.CountTotalSeconds)}";
                    _countTargetTimePreviewText.Text = s.CountIsRunning
                        ? $"🔔 Rings at {ringAt:HH:mm:ss}"
                        : $"🔔 Will ring at {DateTime.Now.AddSeconds(s.CountTotalSeconds):HH:mm}";
                    DrawCircularProgressArc(s.CountIsRunning ? s.GetCountdownRemainingRatio() : 0.0);
                    break;
                }
                case TimerMode.Alarm:
                {
                    if (nearestAlarm != null)
                    {
                        int remSec = Math.Max(0, (int)Math.Ceiling(alarmRem.TotalSeconds));
                        string tStr = TimerService.FormatDuration(remSec);
                        if (_centerTimeText.Text != tStr) _centerTimeText.Text = tStr;
                        _centerPhaseText.Text = !string.IsNullOrWhiteSpace(nearestAlarm.Title)
                            ? nearestAlarm.Title
                            : $"🔔 {nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2}";
                        _centerSubInfoText.Text = $"Rings at {nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2}";
                        DrawCircularProgressArc(alarmRatio);
                    }
                    else
                    {
                        string draftStr = $"{s.DraftAlarmHour:D2}:{s.DraftAlarmMinute:D2}";
                        if (_centerTimeText.Text != draftStr) _centerTimeText.Text = draftStr;
                        _centerPhaseText.Text = "Alarm Clock";
                        _centerSubInfoText.Text = "Ready to set";
                        DrawCircularProgressArc(0.0);
                    }
                    break;
                }
                case TimerMode.Stopwatch:
                {
                    string tStr = sw.TotalHours >= 1.0
                        ? $"{(int)sw.TotalHours:D2}:{sw.Minutes:D2}:{sw.Seconds:D2}"
                        : $"{sw.Minutes:D2}:{sw.Seconds:D2}.{sw.Milliseconds / 10:D2}";
                    if (_centerTimeText.Text != tStr) _centerTimeText.Text = tStr;
                    _centerPhaseText.Text = !string.IsNullOrWhiteSpace(s.StopwatchTitle) ? s.StopwatchTitle : "Stopwatch";
                    _centerSubInfoText.Text = s.Laps.Count > 0 ? $"Lap #{s.Laps.Count + 1}" : (s.StopwatchIsRunning ? "Running" : "Ready");
                    // Progress bar from 0% to 100% when running, and 0% when stopped/reset
                    double secRatio = s.StopwatchIsRunning ? Math.Clamp((sw.TotalSeconds % 60.0) / 60.0, 0.0, 1.0) : 0.0;
                    DrawCircularProgressArc(secRatio);
                    break;
                }
                case TimerMode.Water:
                {
                    string tStr = $"{s.WaterGlasses} / {s.WaterTarget}";
                    if (_centerTimeText.Text != tStr) _centerTimeText.Text = tStr;
                    _centerPhaseText.Text = $"{s.WaterGlasses * 250} ml";
                    _centerSubInfoText.Text = s.WaterReminderEnabled
                        ? $"Next in {TimerService.FormatDuration(s.WaterReminderRemainingSeconds)}"
                        : $"Goal: {s.WaterTarget * 250} ml";
                    double waterRatio = s.WaterTarget > 0 ? Math.Clamp((double)s.WaterGlasses / s.WaterTarget, 0.0, 1.0) : 0.0;
                    DrawCircularProgressArc(waterRatio);
                    break;
                }
            }
        }

        public void UpdateVisuals()
        {
            var s = TimerService.Instance;
            var accentBrush = GetAccentBrush();
            Color accentColor = s.GetEffectiveAccentColor();

            // Sync Accent Colors on Primary Action Buttons & Dots
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

            _dotPomo.Visibility = s.PomoIsRunning ? Visibility.Visible : Visibility.Collapsed;
            _dotCountdown.Visibility = s.CountIsRunning ? Visibility.Visible : Visibility.Collapsed;
            _dotAlarm.Visibility = s.Alarms.Any(a => a.IsEnabled) ? Visibility.Visible : Visibility.Collapsed;
            _dotStopwatch.Visibility = s.StopwatchIsRunning ? Visibility.Visible : Visibility.Collapsed;
            _dotWater.Visibility = s.WaterReminderEnabled ? Visibility.Visible : Visibility.Collapsed;

            _subWater.Text = $"{s.WaterGlasses} / {s.WaterTarget} · {s.WaterGlasses * 250} ml";

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

            _pomoPlayIcon.Text = s.PomoIsRunning ? "⏸" : "▶";
            _focusMinsText.Text = $"Focus {s.PomoWorkSeconds / 60}m";
            _breakMinsText.Text = $"Break {s.PomoBreakSeconds / 60}m";
            _longMinsText.Text = $"Long {s.PomoLongBreakSeconds / 60}m";
            _cyclesText.Text = $"Cycles {s.PomoTargetCycles}";
            UpdateMiniToggleVisual(_autoToggleTrack, _autoToggleThumb, s.PomoAutoStartNext, accentColor);
            _completedText.Text = $"{s.PomoSessionsToday} done ({s.PomoFocusMinutesToday}m)";

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
            _countPlayIcon.Text = s.CountIsRunning ? "⏸" : "▶";
            _countLoopBtn.Background = s.CountLoop ? accentBrush : CapsuleTheme.Brush(CapsuleTheme.Current.Surface2);
            _countSoundBtnText.Text = $"🔔 {s.SelectedSoundTone}";
            _countRepeatBtnText.Text = $"🔁 {s.RingRepeatCount}x";
            _countRingBanner.Visibility = s.IsRinging ? Visibility.Visible : Visibility.Collapsed;
            RebuildCountdownPresets();

            // Alarm Controls
            if (!_alarmHourInput.IsKeyboardFocused) _alarmHourInput.Text = $"{s.DraftAlarmHour:D2}";
            if (!_alarmMinInput.IsKeyboardFocused) _alarmMinInput.Text = $"{s.DraftAlarmMinute:D2}";
            _alarmSoundBtnText.Text = $"🔔 {s.SelectedSoundTone}";
            _alarmSnoozeBtnText.Text = s.DefaultSnoozeMinutes > 0 ? $"💤 {s.DefaultSnoozeMinutes}m" : "💤 Off";
            _alarmRepeatBtnText.Text = $"🔁 {s.RingRepeatCount}x";
            UpdateMiniToggleVisual(_alarmNotchToggleTrack, _alarmNotchToggleThumb, s.ShowAlarmInNotch, accentColor);

            var draftTarget = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, s.DraftAlarmHour, s.DraftAlarmMinute, 0);
            if (draftTarget <= DateTime.Now) draftTarget = draftTarget.AddDays(1);
            var spanUntilDraft = draftTarget - DateTime.Now;
            _alarmRemainingPreviewText.Text = $"⏳ Selected time ({s.DraftAlarmHour:D2}:{s.DraftAlarmMinute:D2}) is in {(int)spanUntilDraft.TotalHours}h {spanUntilDraft.Minutes}m · Right-click presets to delete";
            RebuildAlarmsList(accentColor);

            // Stopwatch Controls
            _swPlayIcon.Text = s.StopwatchIsRunning ? "⏸" : "▶";
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
                _waterDropsVisualPanel.Children.Add(new TextBlock
                {
                    Text = "💧",
                    FontSize = 11,
                    Opacity = i <= s.WaterGlasses ? 1.0 : 0.22,
                    Margin = new Thickness(1.5, 0, 1.5, 0)
                });
            }

            UpdateMiniToggleVisual(_waterReminderToggleTrack, _waterReminderToggleThumb, s.WaterReminderEnabled, accentColor);
            UpdateMiniToggleVisual(_waterNotchToggleTrack, _waterNotchToggleThumb, s.ShowWaterInNotch, accentColor);
            _waterReminderStatusText.Foreground = accentBrush;
            _waterReminderStatusText.Text = s.WaterReminderEnabled
                ? $"⏱ {TimerService.FormatDuration(s.WaterReminderRemainingSeconds)}"
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

                var delBtn = new TextBlock
                {
                    Text = "✕",
                    FontSize = 10,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(6, 0, 0, 0),
                    ToolTip = "Delete alarm"
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
                    Text = !string.IsNullOrWhiteSpace(captured.Title) ? $"{captured.Title} · {remStr}" : remStr,
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
            chip.Background = active ? new SolidColorBrush(accentColor) : CapsuleTheme.Brush(p.Surface2);
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
                    badge = " ⚡";
                }
                else if (lap.LapNumber == slowestLapNum)
                {
                    rowBrush = new SolidColorBrush(Color.FromRgb(255, 69, 58));
                    badge = " 🐢";
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
                var dot = new Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Margin = new Thickness(2.5, 0, 2.5, 0),
                    Fill = i <= current ? new SolidColorBrush(activeColor) : CapsuleTheme.Brush(p.Border)
                };
                _cycleDotsPanel.Children.Add(dot);
            }
        }

        private static void UpdateMiniToggleVisual(Border track, Ellipse thumb, bool on, Color activeColor)
        {
            if (track == null || thumb == null) return;
            var p = CapsuleTheme.Current;
            track.Background = on ? new SolidColorBrush(activeColor) : CapsuleTheme.Brush(p.Surface2);
            thumb.Fill = CapsuleTheme.Brush(p.AccentFg);
            Canvas.SetLeft(thumb, on ? 15 : 2);
        }

        private void DrawCircularRingTrack()
        {
            var p = CapsuleTheme.Current;
            double r = 64.0;
            var center = new Point(74, 74);
            var geo = new EllipseGeometry(center, r, r);
            _ringTrack.Data = geo;
            _ringTrack.Stroke = CapsuleTheme.Brush(p.Border);
        }

        private void DrawCircularProgressArc(double fraction)
        {
            fraction = Math.Clamp(fraction, 0.0, 1.0);
            if (fraction <= 0.002)
            {
                _ringProgress.Data = null;
                _ringProgress.Visibility = Visibility.Collapsed;
                return;
            }

            if (_ringProgress.Visibility != Visibility.Visible)
            {
                _ringProgress.Visibility = Visibility.Visible;
            }

            double r = 64.0;
            var center = new Point(74, 74);
            double angle = Math.Min(fraction * 359.99, 359.94);
            double startAngle = -90.0;
            double endAngle = startAngle + angle;

            Point startPt = PointOnCircle(center, r, startAngle);
            Point endPt = PointOnCircle(center, r, endAngle);
            bool isLargeArc = angle > 180.0;

            if (_ringProgress.Data is PathGeometry existingPg &&
                existingPg.Figures.Count == 1 &&
                existingPg.Figures[0].Segments.Count == 1 &&
                existingPg.Figures[0].Segments[0] is ArcSegment existingArc)
            {
                existingPg.Figures[0].StartPoint = startPt;
                existingArc.Point = endPt;
                existingArc.Size = new Size(r, r);
                existingArc.IsLargeArc = isLargeArc;
                return;
            }

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

        #endregion
    }
}
