using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WindowsBuddy.Tools.Clock
{
    public sealed class ClockToolView : Grid
    {
        private readonly ClockService _service = ClockService.Instance;

        // Card 1: Local Clock & Settings
        private TextBlock _localTimeText;
        private TextBlock _localDateText;
        private TextBlock _localTzText;
        private Border _digitalBtn;
        private Border _analogBtn;
        private TextBlock _timeFormatValueText;
        private Border _secondsToggleSwitch;
        private Ellipse _secondsToggleThumb;
        private Border _dateToggleSwitch;
        private Ellipse _dateToggleThumb;
        private Border _dateStyleFullBtn;
        private Border _dateStyleShortBtn;
        private Border _dateStyleCompactBtn;

        // Card 2: World Clocks
        private StackPanel _worldClocksPanel;
        private Grid _cityPickerOverlay;
        private WrapPanel _citiesWrapPanel;

        public ClockToolView()
        {
            BuildUI();

            _service.SecondTick += OnSecondTick;
            _service.SettingsChanged += OnSettingsChanged;

            Loaded += (_, _) =>
            {
                UpdateDisplay();
                UpdateSettingsUI();
            };
            Unloaded += (_, _) =>
            {
                _service.SecondTick -= OnSecondTick;
                _service.SettingsChanged -= OnSettingsChanged;
            };
        }

        private void BuildUI()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.26, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            BuildLocalClockCard();
            BuildWorldClocksCard();
            BuildCityPickerOverlay();
        }

        #region Card 1: Local Clock & Settings
        private void BuildLocalClockCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(0, 0, 4, 0)
            };

            var dock = new DockPanel();

            // 1. Top Bar: [🕒 Clock] and [This view only · Notch style  Digital  Analog]
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock
            {
                Text = "🕒 ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = "Clock",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(titleSp, Dock.Left);
            topBar.Children.Add(titleSp);

            var topActions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            topActions.Children.Add(new TextBlock
            {
                Text = "This view only · Notch style  ",
                FontSize = 10.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });

            // Segmented pill [Digital | Analog]
            var segPill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 36)),
                CornerRadius = new CornerRadius(11),
                Height = 22,
                Padding = new Thickness(1)
            };
            var segDock = new StackPanel { Orientation = Orientation.Horizontal };

            _digitalBtn = CreateSegmentButton("Digital", true, () => _service.SetDisplayMode(ClockDisplayMode.Digital));
            _analogBtn = CreateSegmentButton("Analog", false, () => _service.SetDisplayMode(ClockDisplayMode.Analog));

            segDock.Children.Add(_digitalBtn);
            segDock.Children.Add(_analogBtn);
            segPill.Child = segDock;
            topActions.Children.Add(segPill);

            DockPanel.SetDock(topActions, Dock.Right);
            topBar.Children.Add(topActions);

            DockPanel.SetDock(topBar, Dock.Top);
            dock.Children.Add(topBar);

            // 2. Settings Rows (Bottom)
            var settingsStack = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom };

            // Row 1: Time format
            var rowFormat = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };
            rowFormat.Children.Add(new TextBlock
            {
                Text = "Time format",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var formatRight = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            _timeFormatValueText = new TextBlock
            {
                Text = "12-hour (Windows)",
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            formatRight.Children.Add(_timeFormatValueText);

            var changeLink = new TextBlock
            {
                Text = "  Change",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(90, 160, 255)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            changeLink.MouseEnter += (_, _) => changeLink.TextDecorations = TextDecorations.Underline;
            changeLink.MouseLeave += (_, _) => changeLink.TextDecorations = null;
            changeLink.MouseLeftButtonUp += (_, _) => _service.ToggleFormat();
            formatRight.Children.Add(changeLink);

            DockPanel.SetDock(formatRight, Dock.Right);
            rowFormat.Children.Add(formatRight);
            settingsStack.Children.Add(rowFormat);

            // Row 2: Show seconds (left) & Show date (right)
            var rowToggles = new DockPanel { Margin = new Thickness(0, 0, 0, 7) };

            // Left: Show seconds
            var leftSecSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftSecSp.Children.Add(new TextBlock
            {
                Text = "Show seconds",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            _secondsToggleSwitch = CreateToggleSwitch(out _secondsToggleThumb, () => _service.ToggleSeconds());
            leftSecSp.Children.Add(_secondsToggleSwitch);
            DockPanel.SetDock(leftSecSp, Dock.Left);
            rowToggles.Children.Add(leftSecSp);

            // Right: Show date
            var rightDateSp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            rightDateSp.Children.Add(new TextBlock
            {
                Text = "Show date",
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            });
            _dateToggleSwitch = CreateToggleSwitch(out _dateToggleThumb, () => _service.ToggleDate());
            rightDateSp.Children.Add(_dateToggleSwitch);
            DockPanel.SetDock(rightDateSp, Dock.Right);
            rowToggles.Children.Add(rightDateSp);

            settingsStack.Children.Add(rowToggles);

            // Row 3: Date style
            var rowDateStyle = new DockPanel { Margin = new Thickness(0, 0, 0, 0) };
            rowDateStyle.Children.Add(new TextBlock
            {
                Text = "Date style",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });

            var dateStylePill = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 36)),
                CornerRadius = new CornerRadius(11),
                Height = 22,
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
            rowDateStyle.Children.Add(dateStylePill);

            settingsStack.Children.Add(rowDateStyle);

            DockPanel.SetDock(settingsStack, Dock.Bottom);
            dock.Children.Add(settingsStack);

            // 3. Center Time & Date Typography
            var centerStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 6)
            };

            _localTimeText = new TextBlock
            {
                Text = "10:25 PM",
                FontSize = 36,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            centerStack.Children.Add(_localTimeText);

            _localDateText = new TextBlock
            {
                Text = "Saturday, October 31, 2026",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(155, 164, 181)),
                Margin = new Thickness(0, 2, 0, 2)
            };
            centerStack.Children.Add(_localDateText);

            var tzOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);
            string sign = tzOffset >= TimeSpan.Zero ? "+" : "-";
            string tzLabel = $"▲ UTC{sign}{Math.Abs(tzOffset.Hours)}:{Math.Abs(tzOffset.Minutes):D2} (UTC{sign}{Math.Abs(tzOffset.Hours):D2}:{Math.Abs(tzOffset.Minutes):D2})";

            _localTzText = new TextBlock
            {
                Text = tzLabel,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            centerStack.Children.Add(_localTzText);

            dock.Children.Add(centerStack);

            card.Child = dock;
            Grid.SetColumn(card, 0);
            Children.Add(card);
        }

        private Border CreateSegmentButton(string label, bool isSelected, Action onClick)
        {
            var btn = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 1, 9, 1),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent,
                BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(255, 85, 0)) : Brushes.Transparent,
                BorderThickness = new Thickness(isSelected ? 1 : 0)
            };
            var txt = new TextBlock
            {
                Text = label,
                FontSize = 10.5,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            };
            btn.Child = txt;

            btn.MouseLeftButtonUp += (_, _) => onClick?.Invoke();
            return btn;
        }

        private Border CreateToggleSwitch(out Ellipse thumb, Action onToggle)
        {
            var sw = new Border
            {
                Width = 34,
                Height = 18,
                CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };

            var canvas = new Canvas { Width = 34, Height = 18 };
            thumb = new Ellipse
            {
                Width = 14,
                Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(142, 142, 147))
            };
            Canvas.SetLeft(thumb, 2);
            Canvas.SetTop(thumb, 2);
            canvas.Children.Add(thumb);
            sw.Child = canvas;

            sw.MouseLeftButtonUp += (_, _) => onToggle?.Invoke();
            return sw;
        }
        #endregion

        #region Card 2: World Clocks
        private void BuildWorldClocksCard()
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(16, 14, 16, 14),
                Margin = new Thickness(4, 0, 0, 0)
            };

            var dock = new DockPanel();

            // Top Header: 🌐 World clocks  + Add city
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            var titleSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(new TextBlock
            {
                Text = "🌐 ",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            titleSp.Children.Add(new TextBlock
            {
                Text = "World clocks",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(titleSp, Dock.Left);
            topBar.Children.Add(titleSp);

            var addCityBtn = new TextBlock
            {
                Text = "+ Add city",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            addCityBtn.MouseEnter += (_, _) => addCityBtn.Foreground = Brushes.White;
            addCityBtn.MouseLeave += (_, _) => addCityBtn.Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147));
            addCityBtn.MouseLeftButtonUp += (_, _) => ShowCityPickerOverlay();

            DockPanel.SetDock(addCityBtn, Dock.Right);
            topBar.Children.Add(addCityBtn);

            DockPanel.SetDock(topBar, Dock.Top);
            dock.Children.Add(topBar);

            // World Clocks Rows
            _worldClocksPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            dock.Children.Add(_worldClocksPanel);

            card.Child = dock;
            Grid.SetColumn(card, 1);
            Children.Add(card);
        }

        private void RebuildWorldClockRows()
        {
            _worldClocksPanel.Children.Clear();
            var clocks = _service.Settings.WorldClocks;

            for (int i = 0; i < clocks.Count; i++)
            {
                var city = clocks[i];
                var (cityTime, subtext) = _service.GetCityInfo(city);

                var row = new DockPanel { Margin = new Thickness(0, 4, 0, 4) };

                // Right: Time Text + optional Delete '✕'
                var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var timeText = new TextBlock
                {
                    Text = _service.FormatTime(cityTime),
                    FontSize = 13.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                };
                rightStack.Children.Add(timeText);

                if (!city.IsLocal)
                {
                    var delBtn = new Border
                    {
                        Width = 16,
                        Height = 16,
                        CornerRadius = new CornerRadius(8),
                        Background = Brushes.Transparent,
                        Cursor = Cursors.Hand,
                        Margin = new Thickness(6, 0, 0, 0),
                        Visibility = Visibility.Collapsed,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    var delTxt = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 9.5,
                        Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 170)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    delBtn.Child = delTxt;

                    string cityId = city.Id;
                    delBtn.MouseLeftButtonUp += (_, _) => _service.RemoveWorldClock(cityId);
                    delBtn.MouseEnter += (_, _) => delTxt.Foreground = new SolidColorBrush(Color.FromRgb(255, 80, 80));
                    delBtn.MouseLeave += (_, _) => delTxt.Foreground = new SolidColorBrush(Color.FromRgb(160, 160, 170));

                    rightStack.Children.Add(delBtn);

                    row.MouseEnter += (_, _) => delBtn.Visibility = Visibility.Visible;
                    row.MouseLeave += (_, _) => delBtn.Visibility = Visibility.Collapsed;
                }

                DockPanel.SetDock(rightStack, Dock.Right);
                row.Children.Add(rightStack);

                // Left: City Name and Subtext
                var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

                var cityNameText = new TextBlock
                {
                    Text = city.CityName,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                leftStack.Children.Add(cityNameText);

                var subtextBlock = new TextBlock
                {
                    Text = subtext,
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    Margin = new Thickness(0, 1, 0, 0)
                };
                leftStack.Children.Add(subtextBlock);

                row.Children.Add(leftStack);
                _worldClocksPanel.Children.Add(row);

                // Hairline divider between rows
                if (i < clocks.Count - 1)
                {
                    _worldClocksPanel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Color.FromRgb(26, 26, 34)),
                        Margin = new Thickness(0, 3, 0, 3)
                    });
                }
            }
        }
        #endregion

        #region City Picker Overlay
        private void BuildCityPickerOverlay()
        {
            _cityPickerOverlay = new Grid
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 10, 10, 14)),
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumnSpan(_cityPickerOverlay, 2);

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(22, 22, 28)),
                CornerRadius = new CornerRadius(14),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16),
                Width = 460,
                Height = 200,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dock = new DockPanel();

            // Header
            var top = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var closeBtn = new TextBlock
            {
                Text = "✕",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.MouseLeftButtonUp += (_, _) => _cityPickerOverlay.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(closeBtn, Dock.Right);
            top.Children.Add(closeBtn);

            top.Children.Add(new TextBlock
            {
                Text = "Add World Clock",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            DockPanel.SetDock(top, Dock.Top);
            dock.Children.Add(top);

            _citiesWrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = _citiesWrapPanel
            };
            dock.Children.Add(scroll);

            card.Child = dock;
            _cityPickerOverlay.Children.Add(card);

            Children.Add(_cityPickerOverlay);
        }

        private void ShowCityPickerOverlay()
        {
            _citiesWrapPanel.Children.Clear();
            var existing = _service.Settings.WorldClocks.Select(c => c.CityName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var city in ClockService.CuratedCities)
            {
                if (existing.Contains(city.CityName)) continue;

                var pill = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(32, 32, 40)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand
                };
                var sp = new StackPanel { Orientation = Orientation.Horizontal };
                sp.Children.Add(new TextBlock
                {
                    Text = city.CityName,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                });
                sp.Children.Add(new TextBlock
                {
                    Text = $" · {city.CountryOrRegion}",
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                    Margin = new Thickness(2, 0, 0, 0)
                });
                pill.Child = sp;

                pill.MouseEnter += (_, _) => pill.Background = new SolidColorBrush(Color.FromRgb(50, 50, 64));
                pill.MouseLeave += (_, _) => pill.Background = new SolidColorBrush(Color.FromRgb(32, 32, 40));

                var captured = city;
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    _service.AddWorldClock(captured);
                    _cityPickerOverlay.Visibility = Visibility.Collapsed;
                };

                _citiesWrapPanel.Children.Add(pill);
            }

            _cityPickerOverlay.Visibility = Visibility.Visible;
        }
        #endregion

        #region UI Updates
        private void OnSecondTick()
        {
            Dispatcher.Invoke(UpdateDisplay);
        }

        private void OnSettingsChanged()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateSettingsUI();
                UpdateDisplay();
            });
        }

        private void UpdateDisplay()
        {
            DateTime now = DateTime.Now;
            _localTimeText.Text = _service.FormatTime(now);

            if (_service.Settings.ShowDate)
            {
                _localDateText.Visibility = Visibility.Visible;
                _localDateText.Text = _service.FormatDate(now);
            }
            else
            {
                _localDateText.Visibility = Visibility.Collapsed;
            }

            RebuildWorldClockRows();
        }

        private void UpdateSettingsUI()
        {
            var s = _service.Settings;

            // Display Mode
            bool isDig = s.DisplayMode == ClockDisplayMode.Digital;
            ApplySegmentSelected(_digitalBtn, isDig);
            ApplySegmentSelected(_analogBtn, !isDig);

            // Time Format
            _timeFormatValueText.Text = s.Format == ClockFormat.TwelveHour ? "12-hour (Windows)" : "24-hour";

            // Seconds Toggle
            ApplyToggleState(_secondsToggleSwitch, _secondsToggleThumb, s.ShowSeconds);

            // Date Toggle
            ApplyToggleState(_dateToggleSwitch, _dateToggleThumb, s.ShowDate);

            // Date Style
            ApplySegmentSelected(_dateStyleFullBtn, s.DateFormat == ClockDateFormat.Full);
            ApplySegmentSelected(_dateStyleShortBtn, s.DateFormat == ClockDateFormat.Short);
            ApplySegmentSelected(_dateStyleCompactBtn, s.DateFormat == ClockDateFormat.Compact);
        }

        private void ApplySegmentSelected(Border btn, bool isSelected)
        {
            btn.BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(255, 85, 0)) : Brushes.Transparent;
            btn.BorderThickness = new Thickness(isSelected ? 1 : 0);
            if (btn.Child is TextBlock txt)
            {
                txt.Foreground = isSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147));
                txt.FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal;
            }
        }

        private void ApplyToggleState(Border sw, Ellipse thumb, bool isOn)
        {
            sw.Background = isOn ? new SolidColorBrush(Color.FromRgb(255, 85, 0)) : new SolidColorBrush(Color.FromRgb(46, 46, 54));
            thumb.Fill = isOn ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147));
            Canvas.SetLeft(thumb, isOn ? 18 : 2);
        }
        #endregion
    }
}
