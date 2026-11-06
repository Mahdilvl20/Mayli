using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Clock
{
    public sealed class ClockToolView : Grid
    {
        private readonly ClockService _service = ClockService.Instance;

        // Card 1: Local Clock & Personalization
        private Border _cardLocal;
        private Border _digitalBtn;
        private Border _analogBtn;
        private Border _dualBtn;

        // Digital View Container
        private StackPanel _digitalClockContainer;
        private TextBlock _localTimeText;
        private TextBlock _localSecText;
        private Border _localAmPmBadge;
        private TextBlock _localAmPmText;
        private TextBlock _localDateText;
        private TextBlock _localTzBadge;

        // Analog View Container
        private StackPanel _analogClockContainer;
        private AnalogClockControl _analogLargeClock;
        private TextBlock _analogSubTimeText;
        private TextBlock _analogSubDateText;

        // Dual View Container
        private Grid _dualClockContainer;
        private AnalogClockControl _dualAnalogClock;
        private TextBlock _dualDigitalTimeText;
        private TextBlock _dualDateText;
        private TextBlock _dualTzText;

        // Settings Controls
        private Border _timeFormat12Btn;
        private Border _timeFormat24Btn;
        private Border _secondsToggleSwitch;
        private Ellipse _secondsToggleThumb;
        private Border _dateToggleSwitch;
        private Ellipse _dateToggleThumb;
        private Border _dateStyleFullBtn;
        private Border _dateStyleShortBtn;
        private Border _dateStyleCompactBtn;
        private Border _hourlyChimeToggleSwitch;
        private Ellipse _hourlyChimeToggleThumb;

        // Card 2: World Clocks & Time Converter
        private StackPanel _worldClocksPanel;
        private TextBlock _worldClocksCountText;
        private Border _converterToggleBtn;
        private TextBlock _converterToggleText;
        private Grid _converterSliderPanel;
        private Slider _timeSlider;
        private TextBlock _sliderValueText;
        private DateTime? _simulatedTime = null;

        // Add City Overlay
        private Grid _cityPickerOverlay;
        private TextBox _citySearchInput;
        private WrapPanel _citiesWrapPanel;

        private bool _isSubscribed;

        public ClockToolView()
        {
            FlowDirection = LocalizationService.CurrentFlowDirection;
            BuildUI();

            SubscribeEvents();

            Loaded += (_, _) =>
            {
                SubscribeEvents();
                UpdateSettingsUI();
                RebuildWorldClockRows();
                UpdateDisplay();
            };
            Unloaded += (_, _) =>
            {
                UnsubscribeEvents();
            };
        }

        public void OnActivated()
        {
            SubscribeEvents();
            UpdateSettingsUI();
            RebuildWorldClockRows();
            UpdateDisplay();
        }

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;
            _service.SecondTick += OnSecondTick;
            _service.SettingsChanged += OnSettingsChanged;
            _isSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;
            _service.SecondTick -= OnSecondTick;
            _service.SettingsChanged -= OnSettingsChanged;
            _isSubscribed = false;
        }

        private void BuildUI()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.18, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            BuildLocalClockCard();
            BuildWorldClocksCard();
            BuildCityPickerOverlay();
        }

        #region Card 1: Local Clock & Personalization
        private void BuildLocalClockCard()
        {
            var p = CapsuleTheme.Current;
            _cardLocal = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 4, 0),
                ClipToBounds = true
            };

            var dock = new DockPanel();

            // 1. Top Bar: [🕒 Clock] and [Mode Segment: Digital | Analog | Dual]
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock
            {
                Text = "🕒 ",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("clock.clock", "Clock"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(titleSp, Dock.Left);
            topBar.Children.Add(titleSp);

            // Segmented pill [Digital | Analog | Dual]
            var segPill = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Height = 21,
                Padding = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var segDock = new StackPanel { Orientation = Orientation.Horizontal };

            _digitalBtn = CreateSegmentButton("Digital", true, () => _service.SetDisplayMode(ClockDisplayMode.Digital));
            _analogBtn = CreateSegmentButton("Analog", false, () => _service.SetDisplayMode(ClockDisplayMode.Analog));
            _dualBtn = CreateSegmentButton("Dual", false, () => _service.SetDisplayMode(ClockDisplayMode.Dual));

            segDock.Children.Add(_digitalBtn);
            segDock.Children.Add(_analogBtn);
            segDock.Children.Add(_dualBtn);
            segPill.Child = segDock;

            DockPanel.SetDock(segPill, Dock.Right);
            topBar.Children.Add(segPill);

            DockPanel.SetDock(topBar, Dock.Top);
            dock.Children.Add(topBar);

            // 2. Settings Stack (Bottom) - Compact 2 Rows
            var settingsStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 2, 0, 0) };

            // Row 1: Time format (12h vs 24h) [Left] & Date Style (Full | Short | Compact) [Right]
            var row1 = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var formatPill = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Height = 20,
                Padding = new Thickness(1)
            };
            var formatDock = new StackPanel { Orientation = Orientation.Horizontal };
            _timeFormat12Btn = CreateSegmentButton("12-Hour", true, () => _service.SetFormat(ClockFormat.TwelveHour));
            _timeFormat24Btn = CreateSegmentButton("24-Hour", false, () => _service.SetFormat(ClockFormat.TwentyFourHour));
            formatDock.Children.Add(_timeFormat12Btn);
            formatDock.Children.Add(_timeFormat24Btn);
            formatPill.Child = formatDock;
            DockPanel.SetDock(formatPill, Dock.Left);
            row1.Children.Add(formatPill);

            var dateStylePill = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Height = 20,
                Padding = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var dateStyleDock = new StackPanel { Orientation = Orientation.Horizontal };
            _dateStyleFullBtn = CreateSegmentButton("Full", true, () => _service.SetDateFormat(ClockDateFormat.Full));
            _dateStyleShortBtn = CreateSegmentButton("Short", false, () => _service.SetDateFormat(ClockDateFormat.Short));
            _dateStyleCompactBtn = CreateSegmentButton("Compact", false, () => _service.SetDateFormat(ClockDateFormat.Compact));
            dateStyleDock.Children.Add(_dateStyleFullBtn);
            dateStyleDock.Children.Add(_dateStyleShortBtn);
            dateStyleDock.Children.Add(_dateStyleCompactBtn);
            dateStylePill.Child = dateStyleDock;
            DockPanel.SetDock(dateStylePill, Dock.Right);
            row1.Children.Add(dateStylePill);

            settingsStack.Children.Add(row1);

            // Row 2: Toggles [Seconds] [Date] [Hourly Chime]
            var row2 = new DockPanel { Margin = new Thickness(0, 0, 0, 0) };

            var secSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            secSp.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("clock.show_seconds", "Seconds"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            _secondsToggleSwitch = CreateToggleSwitch(out _secondsToggleThumb, () => _service.ToggleSeconds());
            secSp.Children.Add(_secondsToggleSwitch);
            DockPanel.SetDock(secSp, Dock.Left);
            row2.Children.Add(secSp);

            var chimeSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            chimeSp.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("clock.hourly_chime", "Chime 🔔"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            _hourlyChimeToggleSwitch = CreateToggleSwitch(out _hourlyChimeToggleThumb, () => _service.ToggleHourlyChime());
            chimeSp.Children.Add(_hourlyChimeToggleSwitch);
            DockPanel.SetDock(chimeSp, Dock.Right);
            row2.Children.Add(chimeSp);

            var dateToggleSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            dateToggleSp.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("clock.show_date", "Date"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            });
            _dateToggleSwitch = CreateToggleSwitch(out _dateToggleThumb, () => _service.ToggleDate());
            dateToggleSp.Children.Add(_dateToggleSwitch);
            row2.Children.Add(dateToggleSp);

            settingsStack.Children.Add(row2);

            DockPanel.SetDock(settingsStack, Dock.Bottom);
            dock.Children.Add(settingsStack);

            // 3. Center Clock Faces Viewport
            var centerViewport = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };

            // View A: Digital View
            _digitalClockContainer = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 4)
            };

            var timeRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };
            _localTimeText = new TextBlock
            {
                Text = "10:25",
                FontSize = 34,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            timeRow.Children.Add(_localTimeText);

            _localSecText = new TextBlock
            {
                Text = ":42",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(2, 0, 4, 5)
            };
            timeRow.Children.Add(_localSecText);

            _localAmPmBadge = new Border
            {
                CornerRadius = new CornerRadius(5),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 1.5, 5, 1.5),
                Margin = new Thickness(4, 0, 0, 7),
                VerticalAlignment = VerticalAlignment.Bottom
            };
            _localAmPmText = new TextBlock
            {
                Text = "PM",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent)
            };
            _localAmPmBadge.Child = _localAmPmText;
            timeRow.Children.Add(_localAmPmBadge);

            _digitalClockContainer.Children.Add(timeRow);

            _localDateText = new TextBlock
            {
                Text = "Friday, November 6, 2026",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 3)
            };
            _digitalClockContainer.Children.Add(_localDateText);

            var metaSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            _localTzBadge = new TextBlock
            {
                Text = "📍 Local (UTC+3:30)",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 0)
            };
            metaSp.Children.Add(_localTzBadge);
            _digitalClockContainer.Children.Add(metaSp);

            centerViewport.Children.Add(_digitalClockContainer);

            // View B: Analog View (Size 96px perfectly fits without overflowing)
            _analogClockContainer = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 4),
                Visibility = Visibility.Collapsed
            };
            _analogLargeClock = new AnalogClockControl(96.0, true);
            _analogClockContainer.Children.Add(_analogLargeClock);

            _analogSubTimeText = new TextBlock
            {
                Text = "10:25:42 PM",
                FontSize = 12.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 1)
            };
            _analogClockContainer.Children.Add(_analogSubTimeText);

            _analogSubDateText = new TextBlock
            {
                Text = "Friday, November 6, 2026",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _analogClockContainer.Children.Add(_analogSubDateText);

            centerViewport.Children.Add(_analogClockContainer);

            // View C: Dual View (Side-by-Side Analog + Digital)
            _dualClockContainer = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 4),
                Visibility = Visibility.Collapsed
            };
            _dualClockContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _dualClockContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            _dualClockContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _dualAnalogClock = new AnalogClockControl(84.0, true);
            Grid.SetColumn(_dualAnalogClock, 0);
            _dualClockContainer.Children.Add(_dualAnalogClock);

            var dualRight = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _dualDigitalTimeText = new TextBlock
            {
                Text = "10:25:42 PM",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                FontFamily = new FontFamily("Consolas, Segoe UI")
            };
            dualRight.Children.Add(_dualDigitalTimeText);

            _dualDateText = new TextBlock
            {
                Text = "Friday, November 6, 2026",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 2, 0, 2)
            };
            dualRight.Children.Add(_dualDateText);

            _dualTzText = new TextBlock
            {
                Text = "📍 Local (UTC+3:30)",
                FontSize = 10.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 2)
            };
            dualRight.Children.Add(_dualTzText);

            Grid.SetColumn(dualRight, 2);
            _dualClockContainer.Children.Add(dualRight);

            centerViewport.Children.Add(_dualClockContainer);

            dock.Children.Add(centerViewport);

            _cardLocal.Child = dock;
            Grid.SetColumn(_cardLocal, 0);
            Children.Add(_cardLocal);
        }

        private Border CreateSegmentButton(string label, bool isSelected, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var btn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 1, 7, 1),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = isSelected ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 1 : 0)
            };
            var txt = new TextBlock
            {
                Text = label,
                FontSize = 10.0,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center
            };
            btn.Child = txt;

            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick?.Invoke();
                UpdateSettingsUI();
                UpdateDisplay();
            };
            return btn;
        }

        private Border CreateToggleSwitch(out Ellipse thumb, Action onToggle)
        {
            var p = CapsuleTheme.Current;
            var sw = new Border
            {
                Width = 30,
                Height = 16,
                CornerRadius = new CornerRadius(8),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            var canvas = new Canvas { Width = 30, Height = 16 };
            thumb = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = CapsuleTheme.Brush(p.Text2)
            };
            Canvas.SetLeft(thumb, 2);
            Canvas.SetTop(thumb, 2);
            canvas.Children.Add(thumb);
            sw.Child = canvas;

            sw.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onToggle?.Invoke();
                UpdateSettingsUI();
                UpdateDisplay();
            };
            return sw;
        }
        #endregion

        #region Card 2: World Clocks & Time Converter
        private void BuildWorldClocksCard()
        {
            var p = CapsuleTheme.Current;
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(4, 0, 0, 0),
                ClipToBounds = true
            };

            var dock = new DockPanel();

            // Top Bar: 🌐 World Clocks [Count] + [⏱️ Converter] + [+ Add City]
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock
            {
                Text = "🌐 ",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("clock.world_clocks", "World Clocks"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            });

            _worldClocksCountText = new TextBlock
            {
                Text = " (4)",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleSp.Children.Add(_worldClocksCountText);

            DockPanel.SetDock(titleSp, Dock.Left);
            topBar.Children.Add(titleSp);

            var topActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            // Time Converter Toggle Button
            _converterToggleBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 2, 6, 2),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _converterToggleText = new TextBlock
            {
                Text = "⏱️ Converter",
                FontSize = 10.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2)
            };
            _converterToggleBtn.Child = _converterToggleText;
            _converterToggleBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ToggleConverterPanel();
            };
            topActions.Children.Add(_converterToggleBtn);

            // Add City Button
            var addCityBtn = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(7, 2, 7, 2),
                Background = CapsuleTheme.Brush(p.Accent),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var addCityTxt = new TextBlock
            {
                Text = "+ Add City",
                FontSize = 10.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.AccentFg)
            };
            addCityBtn.Child = addCityTxt;
            addCityBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                ShowCityPickerOverlay();
            };
            topActions.Children.Add(addCityBtn);

            DockPanel.SetDock(topActions, Dock.Right);
            topBar.Children.Add(topActions);

            DockPanel.SetDock(topBar, Dock.Top);
            dock.Children.Add(topBar);

            // Interactive Time Converter Panel (Hidden by default)
            _converterSliderPanel = new Grid
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 2, 0, 6),
                Background = CapsuleTheme.Brush(p.Surface2),
                Height = 36
            };
            _converterSliderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _converterSliderPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var sliderStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
            _sliderValueText = new TextBlock
            {
                Text = "Reference: 14:00 (2:00 PM)",
                FontSize = 10.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Accent)
            };
            sliderStack.Children.Add(_sliderValueText);

            _timeSlider = new Slider
            {
                Minimum = 0,
                Maximum = 1439, // 24h * 60m
                Value = DateTime.Now.Hour * 60 + DateTime.Now.Minute,
                SmallChange = 15,
                LargeChange = 60,
                Margin = new Thickness(0, 1, 0, 0)
            };
            _timeSlider.ValueChanged += (_, e) =>
            {
                int totalMins = (int)e.NewValue;
                int h = totalMins / 60;
                int m = totalMins % 60;
                _simulatedTime = DateTime.Today.AddHours(h).AddMinutes(m);
                _sliderValueText.Text = $"Reference: {h:D2}:{m:D2} ({_service.FormatTime(_simulatedTime.Value, false)})";
                RebuildWorldClockRows();
            };
            sliderStack.Children.Add(_timeSlider);
            Grid.SetColumn(sliderStack, 0);
            _converterSliderPanel.Children.Add(sliderStack);

            var resetNowBtn = new Border
            {
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(6, 3, 6, 3),
                Background = CapsuleTheme.Brush(p.Surface3),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            resetNowBtn.Child = new TextBlock { Text = "Now", FontSize = 9.5, Foreground = CapsuleTheme.Brush(p.Text), FontWeight = FontWeights.Bold };
            resetNowBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                DateTime now = DateTime.Now;
                _timeSlider.Value = now.Hour * 60 + now.Minute;
                _simulatedTime = null;
                RebuildWorldClockRows();
            };
            Grid.SetColumn(resetNowBtn, 1);
            _converterSliderPanel.Children.Add(resetNowBtn);

            DockPanel.SetDock(_converterSliderPanel, Dock.Top);
            dock.Children.Add(_converterSliderPanel);

            // World Clocks Scrollable Stack
            _worldClocksPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _worldClocksPanel
            };
            dock.Children.Add(scroll);

            card.Child = dock;
            Grid.SetColumn(card, 1);
            Children.Add(card);
        }

        private void ToggleConverterPanel()
        {
            var p = CapsuleTheme.Current;
            if (_converterSliderPanel.Visibility == Visibility.Visible)
            {
                _converterSliderPanel.Visibility = Visibility.Collapsed;
                _simulatedTime = null;
                _converterToggleBtn.Background = CapsuleTheme.Brush(p.Surface2);
                _converterToggleText.Foreground = CapsuleTheme.Brush(p.Text2);
            }
            else
            {
                _converterSliderPanel.Visibility = Visibility.Visible;
                DateTime now = DateTime.Now;
                _timeSlider.Value = now.Hour * 60 + now.Minute;
                _converterToggleBtn.Background = CapsuleTheme.Brush(p.Accent);
                _converterToggleText.Foreground = CapsuleTheme.Brush(p.AccentFg);
            }
            RebuildWorldClockRows();
        }

        private void RebuildWorldClockRows()
        {
            var p = CapsuleTheme.Current;
            _worldClocksPanel.Children.Clear();
            var clocks = _service.Settings.WorldClocks;
            _worldClocksCountText.Text = $" ({clocks.Count})";

            for (int i = 0; i < clocks.Count; i++)
            {
                int index = i;
                var city = clocks[index];
                var (cityTime, subtext, isDay) = _service.GetCityInfo(city, _simulatedTime);

                var rowCard = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(9),
                    Padding = new Thickness(9, 5, 9, 5),
                    Margin = new Thickness(0, 2, 0, 2)
                };

                var dock = new DockPanel();

                // Right Section: Digital Time + Hover Action Buttons
                var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var timeText = new TextBlock
                {
                    Text = _service.FormatTime(cityTime, false),
                    FontSize = 13.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    FontFamily = new FontFamily("Consolas, Segoe UI"),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };
                rightStack.Children.Add(timeText);

                // Hover Actions (Move Up, Move Down, Delete)
                var actionsPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    Visibility = Visibility.Collapsed
                };

                if (index > 0 && !city.IsLocal)
                {
                    var upBtn = CreateActionButton("▲", () => _service.MoveWorldClock(index, index - 1));
                    actionsPanel.Children.Add(upBtn);
                }
                if (index < clocks.Count - 1 && !city.IsLocal)
                {
                    var downBtn = CreateActionButton("▼", () => _service.MoveWorldClock(index, index + 1));
                    actionsPanel.Children.Add(downBtn);
                }
                if (!city.IsLocal)
                {
                    string cityId = city.Id;
                    var delBtn = CreateActionButton("✕", () => _service.RemoveWorldClock(cityId), isDelete: true);
                    actionsPanel.Children.Add(delBtn);
                }

                rightStack.Children.Add(actionsPanel);

                rowCard.MouseEnter += (_, _) => actionsPanel.Visibility = Visibility.Visible;
                rowCard.MouseLeave += (_, _) => actionsPanel.Visibility = Visibility.Collapsed;

                DockPanel.SetDock(rightStack, Dock.Right);
                dock.Children.Add(rightStack);

                // Left Section: Day/Night indicator + City Name + Country & Difference
                var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                // Day / Night Sun or Moon Badge
                var dayNightBadge = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush(isDay ? Color.FromArgb(40, 255, 180, 40) : Color.FromArgb(40, 70, 100, 200)),
                    Margin = new Thickness(0, 0, 7, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var dnIcon = new TextBlock
                {
                    Text = isDay ? "☀️" : "🌙",
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                dayNightBadge.Child = dnIcon;
                leftStack.Children.Add(dayNightBadge);

                var cityDetails = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var cityNameText = new TextBlock
                {
                    Text = city.CityName,
                    FontSize = 12.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.Text)
                };
                cityDetails.Children.Add(cityNameText);

                var subtextBlock = new TextBlock
                {
                    Text = subtext,
                    FontSize = 9.5,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    Margin = new Thickness(0, 1, 0, 0)
                };
                cityDetails.Children.Add(subtextBlock);

                leftStack.Children.Add(cityDetails);
                dock.Children.Add(leftStack);

                rowCard.Child = dock;
                _worldClocksPanel.Children.Add(rowCard);
            }
        }

        private Border CreateActionButton(string glyph, Action onClick, bool isDelete = false)
        {
            var p = CapsuleTheme.Current;
            var btn = new Border
            {
                Width = 17,
                Height = 17,
                CornerRadius = new CornerRadius(8.5),
                Background = CapsuleTheme.Brush(p.Surface3),
                Cursor = Cursors.Hand,
                Margin = new Thickness(1.5, 0, 1.5, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var txt = new TextBlock
            {
                Text = glyph,
                FontSize = isDelete ? 9.0 : 8.0,
                Foreground = isDelete ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Child = txt;

            btn.MouseEnter += (_, _) => btn.Background = CapsuleTheme.Brush(p.BorderStrong);
            btn.MouseLeave += (_, _) => btn.Background = CapsuleTheme.Brush(p.Surface3);
            btn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick?.Invoke();
                RebuildWorldClockRows();
            };
            return btn;
        }
        #endregion

        #region City Picker Overlay (Searchable 80+ Cities)
        private void BuildCityPickerOverlay()
        {
            var p = CapsuleTheme.Current;
            _cityPickerOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(180, p.Bg.R, p.Bg.G, p.Bg.B)),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumnSpan(_cityPickerOverlay, 2);

            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                CornerRadius = new CornerRadius(14),
                BorderBrush = CapsuleTheme.Brush(p.BorderStrong),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14),
                Width = 460,
                Height = 260,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dock = new DockPanel();

            // Header
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            var closeBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 13,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(4)
            };
            closeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _cityPickerOverlay.Visibility = Visibility.Collapsed;
            };
            DockPanel.SetDock(closeBtn, Dock.Right);
            top.Children.Add(closeBtn);

            top.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("clock.add_world_clock", "Add World Clock"),
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);

            // Search Bar Input
            var searchBoxBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var searchIcon = new TextBlock
            {
                Text = "🔍 ",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            _citySearchInput = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = CapsuleTheme.Brush(p.Text),
                FontSize = 11.5,
                CaretBrush = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            _citySearchInput.TextChanged += (_, _) => FilterCities(_citySearchInput.Text);
            Grid.SetColumn(_citySearchInput, 1);
            searchGrid.Children.Add(_citySearchInput);

            searchBoxBorder.Child = searchGrid;
            DockPanel.SetDock(searchBoxBorder, Dock.Top);
            dock.Children.Add(searchBoxBorder);

            // Cities Wrap Panel inside ScrollViewer
            _citiesWrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _citiesWrapPanel
            };
            dock.Children.Add(scroll);

            card.Child = dock;
            _cityPickerOverlay.Children.Add(card);

            Children.Add(_cityPickerOverlay);
        }

        private void ShowCityPickerOverlay()
        {
            _citySearchInput.Text = "";
            FilterCities("");
            _cityPickerOverlay.Visibility = Visibility.Visible;
            _citySearchInput.Focus();
        }

        private void FilterCities(string query)
        {
            var p = CapsuleTheme.Current;
            _citiesWrapPanel.Children.Clear();
            var existing = _service.Settings.WorldClocks.Select(c => c.CityName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var results = _service.SearchCities(query);

            foreach (var city in results)
            {
                if (existing.Contains(city.CityName)) continue;

                var pill = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(9, 5, 9, 5),
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = $"{city.FlagOrIcon} {city.CityName}",
                    FontSize = 11.0,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = CapsuleTheme.Brush(p.Text)
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $" · {city.CountryOrRegion}",
                    FontSize = 10.0,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    Margin = new Thickness(2, 0, 0, 0)
                });
                pill.Child = sp;

                pill.MouseEnter += (_, _) => pill.Background = CapsuleTheme.Brush(p.Surface3);
                pill.MouseLeave += (_, _) => pill.Background = CapsuleTheme.Brush(p.Surface2);

                var captured = city;
                pill.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    _service.AddWorldClock(captured);
                    _cityPickerOverlay.Visibility = Visibility.Collapsed;
                    RebuildWorldClockRows();
                };

                _citiesWrapPanel.Children.Add(pill);
            }
        }
        #endregion

        #region UI Display Updates & Live Timers
        private void OnSecondTick()
        {
            if (!IsVisible || !IsLoaded) return;
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(UpdateDisplay, DispatcherPriority.Render);
                return;
            }
            UpdateDisplay();
        }

        private void OnSettingsChanged()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.InvokeAsync(OnSettingsChanged, DispatcherPriority.Input);
                return;
            }
            UpdateSettingsUI();
            RebuildWorldClockRows();
            UpdateDisplay();
        }

        private string _lastWorldClocksSig;

        private void UpdateDisplay()
        {
            DateTime now = DateTime.Now;

            // 1. Digital Time Components
            bool showSec = _service.Settings.ShowSeconds;
            bool is24 = _service.Settings.Format == ClockFormat.TwentyFourHour;

            if (is24)
            {
                _localTimeText.Text = now.ToString("HH:mm", CultureInfo.InvariantCulture);
                _localSecText.Text = showSec ? now.ToString(":ss", CultureInfo.InvariantCulture) : "";
                _localSecText.Visibility = showSec ? Visibility.Visible : Visibility.Collapsed;
                _localAmPmBadge.Visibility = Visibility.Collapsed;
            }
            else
            {
                _localTimeText.Text = now.ToString("h:mm", CultureInfo.InvariantCulture);
                _localSecText.Text = showSec ? now.ToString(":ss", CultureInfo.InvariantCulture) : "";
                _localSecText.Visibility = showSec ? Visibility.Visible : Visibility.Collapsed;
                _localAmPmBadge.Visibility = Visibility.Visible;
                _localAmPmText.Text = now.ToString("tt", CultureInfo.InvariantCulture).ToUpperInvariant();
            }

            // Date
            if (_service.Settings.ShowDate)
            {
                _localDateText.Visibility = Visibility.Visible;
                _localDateText.Text = _service.FormatDate(now);
                _dualDateText.Text = _service.FormatDate(now);
            }
            else
            {
                _localDateText.Visibility = Visibility.Collapsed;
                _dualDateText.Text = "";
            }

            // Location and Timezone
            var tzOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            string sign = tzOffset >= TimeSpan.Zero ? "+" : "-";
            string tzStr = $"📍 {TimeZoneInfo.Local.DisplayName.Split(')')[^1].Trim()} (UTC{sign}{Math.Abs(tzOffset.Hours)}:{Math.Abs(tzOffset.Minutes):D2})";
            _localTzBadge.Text = tzStr;
            _dualTzText.Text = tzStr;

            // 2. Analog Clocks (96px fits comfortably)
            _analogLargeClock.ShowSeconds = showSec;
            _analogLargeClock.UpdateTime(now);
            _analogSubTimeText.Text = _service.FormatTime(now, true);
            _analogSubDateText.Text = _service.FormatDate(now);

            // 3. Dual Clocks (84px fits comfortably)
            _dualAnalogClock.ShowSeconds = showSec;
            _dualAnalogClock.UpdateTime(now);
            _dualDigitalTimeText.Text = _service.FormatTime(now, showSec);

            // 4. World Clocks Tick Check
            if (_simulatedTime == null)
            {
                var clocks = _service.Settings.WorldClocks;
                string sig = string.Join("|", clocks.Select(c => $"{c.Id}:{_service.FormatTime(_service.GetCityInfo(c).time)}"));
                if (!string.Equals(sig, _lastWorldClocksSig, StringComparison.Ordinal))
                {
                    _lastWorldClocksSig = sig;
                    RebuildWorldClockRows();
                }
            }
        }

        private void UpdateSettingsUI()
        {
            var s = _service.Settings;

            // Mode Selector
            ApplySegmentSelected(_digitalBtn, s.DisplayMode == ClockDisplayMode.Digital);
            ApplySegmentSelected(_analogBtn, s.DisplayMode == ClockDisplayMode.Analog);
            ApplySegmentSelected(_dualBtn, s.DisplayMode == ClockDisplayMode.Dual);

            _digitalClockContainer.Visibility = s.DisplayMode == ClockDisplayMode.Digital ? Visibility.Visible : Visibility.Collapsed;
            _analogClockContainer.Visibility = s.DisplayMode == ClockDisplayMode.Analog ? Visibility.Visible : Visibility.Collapsed;
            _dualClockContainer.Visibility = s.DisplayMode == ClockDisplayMode.Dual ? Visibility.Visible : Visibility.Collapsed;

            // Format (12h vs 24h)
            ApplySegmentSelected(_timeFormat12Btn, s.Format == ClockFormat.TwelveHour);
            ApplySegmentSelected(_timeFormat24Btn, s.Format == ClockFormat.TwentyFourHour);

            // Seconds Toggle
            ApplyToggleState(_secondsToggleSwitch, _secondsToggleThumb, s.ShowSeconds);

            // Date Toggle
            ApplyToggleState(_dateToggleSwitch, _dateToggleThumb, s.ShowDate);

            // Hourly Chime Toggle
            ApplyToggleState(_hourlyChimeToggleSwitch, _hourlyChimeToggleThumb, s.HourlyChime);

            // Date Style
            ApplySegmentSelected(_dateStyleFullBtn, s.DateFormat == ClockDateFormat.Full);
            ApplySegmentSelected(_dateStyleShortBtn, s.DateFormat == ClockDateFormat.Short);
            ApplySegmentSelected(_dateStyleCompactBtn, s.DateFormat == ClockDateFormat.Compact);
        }

        private void ApplySegmentSelected(Border btn, bool isSelected)
        {
            var p = CapsuleTheme.Current;
            btn.BorderBrush = isSelected ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent;
            btn.BorderThickness = new Thickness(isSelected ? 1 : 0);
            if (btn.Child is TextBlock txt)
            {
                txt.Foreground = isSelected ? CapsuleTheme.Brush(p.Text) : CapsuleTheme.Brush(p.Text2);
                txt.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private void ApplyToggleState(Border sw, Ellipse thumb, bool isOn)
        {
            var p = CapsuleTheme.Current;
            sw.Background = isOn ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.Surface2);
            thumb.Fill = isOn ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text2);
            Canvas.SetLeft(thumb, isOn ? 16 : 2);
        }
        #endregion
    }
}
