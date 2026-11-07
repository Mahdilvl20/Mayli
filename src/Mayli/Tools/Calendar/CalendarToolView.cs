using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Calendar
{
    public sealed class CalendarToolView : UserControl
    {
        public const double GridW = 728.0;
        public const double GridH = 242.0;

        private static readonly FontFamily VazirFont = new FontFamily("pack://application:,,,/Resources/#Vazir, pack://application:,,,/Resources/#Vazirmatn, Vazir, Vazirmatn, Segoe UI");

        private readonly CalendarService _service = CalendarService.Instance;

        private Grid _rootGrid;

        // Left Column (Calendar Grid)
        private Border _leftCard;
        private TextBlock _monthTitleText;
        private TextBlock _monthSubtitleText;
        private Border _btnPrevMonth;
        private Border _btnNextMonth;
        private Border _btnToday;
        private TextBlock _btnTodayText;
        private Border _switchJalali;
        private TextBlock _switchJalaliText;
        private Border _switchGregorian;
        private TextBlock _switchGregorianText;

        private UniformGrid _weekdaysHeaderGrid;
        private UniformGrid _daysGrid;
        private TextBlock _sourceStatusText;
        private Border _btnPrevYear;
        private Border _btnNextYear;
        private TextBlock _btnPrevYearText;
        private TextBlock _btnNextYearText;

        // Right Column (Day Details & Events)
        private Border _rightCard;
        private TextBlock _heroDayText;
        private TextBlock _heroWeekdayText;
        private TextBlock _heroPrimaryDateText;
        private TextBlock _heroSecondaryDateText;
        private TextBlock _heroHijriDateText;
        private Border _holidayBadge;
        private TextBlock _holidayBadgeText;

        private StackPanel _eventsListPanel;
        private ScrollViewer _eventsScrollViewer;
        private Border _emptyEventsBox;
        private TextBlock _emptyEventsText;
        private Border _copyDateBtn;
        private TextBlock _copyDateBtnText;

        public CalendarToolView()
        {
            FontFamily = VazirFont;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            FlowDirection = LocalizationService.CurrentFlowDirection;

            BuildUI();

            _service.StateChanged += () => Dispatcher.InvokeAsync(RefreshAll);
            _service.EventsUpdated += () => Dispatcher.InvokeAsync(RefreshAll);
            _service.DateSelected += _ => Dispatcher.InvokeAsync(RefreshDayDetails);
            CapsuleTheme.ThemeChanged += () => Dispatcher.InvokeAsync(ApplyTheme);

            Loaded += (_, _) =>
            {
                RefreshAll();
                _ = _service.PrefetchMonthEventsAsync(_service.DisplayYear, _service.DisplayMonth, _service.CurrentCalendarType);
            };
        }

        public void OnActivated()
        {
            RefreshAll();
        }

        private void BuildUI()
        {
            var p = CapsuleTheme.Current;

            _rootGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                FlowDirection = LocalizationService.CurrentFlowDirection
            };

            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(420) });
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) }); // Spacing
            _rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // LEFT COLUMN: CALENDAR GRID & MONTH NAV
            // -------------------------------------------------------------
            _leftCard = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 8, 10, 6)
            };

            // Allow mouse wheel to navigate months
            _leftCard.MouseWheel += (s, e) =>
            {
                e.Handled = true;
                if (e.Delta > 0) _service.PreviousMonth();
                else if (e.Delta < 0) _service.NextMonth();
            };

            var leftDock = new DockPanel();

            // Top Header: Month title, dual subtitle, nav buttons, and calendar switch
            var topBar = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };

            // Segmented Calendar Switch on the right: [ شمسی ] [ میلادی ]
            var switchContainer = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(13),
                Padding = new Thickness(2),
                Height = 26,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            var switchStack = new StackPanel { Orientation = Orientation.Horizontal };

            _switchJalaliText = new TextBlock
            {
                Text = LocalizationService.T("cal.shamsi", "Solar"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = VazirFont,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _switchJalali = new Border
            {
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                Child = _switchJalaliText
            };
            _switchJalali.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _service.SwitchCalendarType(CalendarType.Jalali);
            };
            switchStack.Children.Add(_switchJalali);

            _switchGregorianText = new TextBlock
            {
                Text = LocalizationService.T("cal.gregorian", "Gregorian"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _switchGregorian = new Border
            {
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(8, 0, 8, 0),
                Cursor = Cursors.Hand,
                Child = _switchGregorianText
            };
            _switchGregorian.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _service.SwitchCalendarType(CalendarType.Gregorian);
            };
            switchStack.Children.Add(_switchGregorian);

            switchContainer.Child = switchStack;
            DockPanel.SetDock(switchContainer, Dock.Right);
            topBar.Children.Add(switchContainer);

            // Left cluster: Nav Arrows + Month Title + Subtitle
            var leftHeaderCluster = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = LocalizationService.CurrentFlowDirection
            };

            var titleStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            _monthTitleText = new TextBlock
            {
                Text = "آبان ۱۴۰۵",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            titleStack.Children.Add(_monthTitleText);

            _monthSubtitleText = new TextBlock
            {
                Text = "Oct - Nov 2026",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 1, 0, 0)
            };
            titleStack.Children.Add(_monthSubtitleText);
            leftHeaderCluster.Children.Add(titleStack);

            // Nav buttons: ‹ Today ›
            var navBtnsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnPrevMonth = CreateMiniCircleButton("‹", 24, "Previous Month", () => _service.PreviousMonth());
            navBtnsRow.Children.Add(_btnPrevMonth);

            _btnTodayText = new TextBlock
            {
                Text = LocalizationService.T("cal.today", "Today"),
                FontSize = 10.0,
                FontWeight = FontWeights.SemiBold,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _btnToday = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Height = 24,
                Padding = new Thickness(8, 0, 8, 0),
                Margin = new Thickness(4, 0, 4, 0),
                Cursor = Cursors.Hand,
                ToolTip = "Jump to Today",
                Child = _btnTodayText
            };
            _btnToday.MouseEnter += (_, _) => _btnToday.Background = CapsuleTheme.Brush(p.ControlFillHover);
            _btnToday.MouseLeave += (_, _) => _btnToday.Background = CapsuleTheme.Brush(p.Surface2);
            _btnToday.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _service.GoToToday();
            };
            navBtnsRow.Children.Add(_btnToday);

            _btnNextMonth = CreateMiniCircleButton("›", 24, "Next Month", () => _service.NextMonth());
            navBtnsRow.Children.Add(_btnNextMonth);

            leftHeaderCluster.Children.Add(navBtnsRow);
            topBar.Children.Add(leftHeaderCluster);

            DockPanel.SetDock(topBar, Dock.Top);
            leftDock.Children.Add(topBar);

            // Bottom Bar: Source indicator + Year steppers
            var bottomBar = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };

            var yearSteppers = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnPrevYearText = new TextBlock { Text = "« -1Y", FontSize = 9.5, Foreground = CapsuleTheme.Brush(p.Text2) };
            _btnPrevYear = new Border
            {
                Padding = new Thickness(5, 2, 5, 2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Child = _btnPrevYearText
            };
            _btnPrevYear.MouseLeftButtonUp += (_, e) => { e.Handled = true; _service.AdjustYear(-1); };
            yearSteppers.Children.Add(_btnPrevYear);

            _btnNextYearText = new TextBlock { Text = "+1Y »", FontSize = 9.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(4, 0, 0, 0) };
            _btnNextYear = new Border
            {
                Padding = new Thickness(5, 2, 5, 2),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                Child = _btnNextYearText
            };
            _btnNextYear.MouseLeftButtonUp += (_, e) => { e.Handled = true; _service.AdjustYear(+1); };
            yearSteppers.Children.Add(_btnNextYear);

            DockPanel.SetDock(yearSteppers, Dock.Right);
            bottomBar.Children.Add(yearSteppers);

            _sourceStatusText = new TextBlock
            {
                Text = "● holidayapi.ir",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.75
            };
            bottomBar.Children.Add(_sourceStatusText);

            DockPanel.SetDock(bottomBar, Dock.Bottom);
            leftDock.Children.Add(bottomBar);

            // Center Matrix: Weekdays Header + 7x6 Day Cells
            var matrixStack = new StackPanel();

            _weekdaysHeaderGrid = new UniformGrid
            {
                Columns = 7,
                Rows = 1,
                Margin = new Thickness(0, 0, 0, 2),
                FlowDirection = FlowDirection.LeftToRight
            };
            matrixStack.Children.Add(_weekdaysHeaderGrid);

            _daysGrid = new UniformGrid
            {
                Columns = 7,
                Rows = 6,
                FlowDirection = FlowDirection.LeftToRight
            };
            matrixStack.Children.Add(_daysGrid);

            leftDock.Children.Add(matrixStack);
            _leftCard.Child = leftDock;

            Grid.SetColumn(_leftCard, 0);
            _rootGrid.Children.Add(_leftCard);

            // -------------------------------------------------------------
            // RIGHT COLUMN: SELECTED DAY DETAIL & EVENTS SHEET
            // -------------------------------------------------------------
            _rightCard = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 8, 10, 8)
            };

            var rightDock = new DockPanel();

            // Bottom Actions: Copy Date Button
            _copyDateBtnText = new TextBlock
            {
                Text = "📋 " + LocalizationService.T("cal.copy_date", "Copy Date"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _copyDateBtn = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Height = 28,
                Cursor = Cursors.Hand,
                ToolTip = "Copy formatted date to clipboard",
                Child = _copyDateBtnText
            };
            _copyDateBtn.MouseEnter += (_, _) => _copyDateBtn.Background = CapsuleTheme.Brush(p.ControlFillHover);
            _copyDateBtn.MouseLeave += (_, _) => _copyDateBtn.Background = CapsuleTheme.Brush(p.Surface2);
            _copyDateBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                CopyCurrentDateToClipboard();
            };
            DockPanel.SetDock(_copyDateBtn, Dock.Bottom);
            rightDock.Children.Add(_copyDateBtn);

            // Hero Date Header (Big Day Number + Weekday + Dates)
            var heroDateRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };

            _holidayBadgeText = new TextBlock
            {
                Text = "تعطیل رسمی",
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                FontFamily = VazirFont,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            };
            _holidayBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(235, 75, 75)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 2, 6, 2),
                VerticalAlignment = VerticalAlignment.Top,
                Child = _holidayBadgeText
            };
            DockPanel.SetDock(_holidayBadge, Dock.Right);
            heroDateRow.Children.Add(_holidayBadge);

            var heroStack = new StackPanel { Orientation = Orientation.Horizontal };

            _heroDayText = new TextBlock
            {
                Text = "۱۵",
                FontSize = 32,
                FontWeight = FontWeights.Bold,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -4, 10, 0)
            };
            heroStack.Children.Add(_heroDayText);

            var heroTexts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _heroWeekdayText = new TextBlock
            {
                Text = "جمعه",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            heroTexts.Children.Add(_heroWeekdayText);

            _heroPrimaryDateText = new TextBlock
            {
                Text = "آبان ۱۴۰۵",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 1, 0, 0)
            };
            heroTexts.Children.Add(_heroPrimaryDateText);

            _heroSecondaryDateText = new TextBlock
            {
                Text = "6 November 2026",
                FontSize = 9.5,
                FontFamily = LocalizationService.LatinFontFamily,
                Foreground = CapsuleTheme.Brush(p.Text2)
            };
            heroTexts.Children.Add(_heroSecondaryDateText);

            _heroHijriDateText = new TextBlock
            {
                Text = "۲۶ ربیع‌الثانی ۱۴۴۸",
                FontSize = 9.0,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Opacity = 0.85
            };
            heroTexts.Children.Add(_heroHijriDateText);

            heroStack.Children.Add(heroTexts);
            heroDateRow.Children.Add(heroStack);

            DockPanel.SetDock(heroDateRow, Dock.Top);
            rightDock.Children.Add(heroDateRow);

            // Divider line
            var separator = new Border
            {
                Height = 1,
                Background = CapsuleTheme.Brush(p.Border),
                Margin = new Thickness(0, 0, 0, 6)
            };
            DockPanel.SetDock(separator, Dock.Top);
            rightDock.Children.Add(separator);

            // Events List Header
            var eventsHeader = new TextBlock
            {
                Text = LocalizationService.T("cal.events_title", "Events & Holidays"),
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 0, 4)
            };
            DockPanel.SetDock(eventsHeader, Dock.Top);
            rightDock.Children.Add(eventsHeader);

            // Scrollable events container
            _eventsListPanel = new StackPanel();

            _emptyEventsText = new TextBlock
            {
                Text = LocalizationService.T("cal.no_events", "No special events for this day"),
                FontSize = 10.0,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Opacity = 0.75
            };
            _emptyEventsBox = new Border
            {
                Padding = new Thickness(8, 20, 8, 20),
                Child = _emptyEventsText
            };

            _eventsScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = _eventsListPanel
            };
            rightDock.Children.Add(_eventsScrollViewer);

            _rightCard.Child = rightDock;

            Grid.SetColumn(_rightCard, 2);
            _rootGrid.Children.Add(_rightCard);

            Content = _rootGrid;
        }

        #region Refresh & UI Binding

        private void RefreshAll()
        {
            ApplyTheme();
            RefreshHeader();
            RefreshWeekdaysHeader();
            RefreshDaysGrid();
            RefreshDayDetails();
        }

        private void RefreshHeader()
        {
            bool isJalali = (_service.CurrentCalendarType == CalendarType.Jalali);
            _monthTitleText.Text = _service.GetDisplayHeaderTitle();
            _monthSubtitleText.Text = _service.GetDisplayHeaderSubtitle();

            _monthTitleText.FontFamily = isJalali ? VazirFont : LocalizationService.LatinFontFamily;
            _monthSubtitleText.FontFamily = isJalali ? LocalizationService.LatinFontFamily : VazirFont;

            var p = CapsuleTheme.Current;

            if (isJalali)
            {
                _switchJalali.Background = CapsuleTheme.Brush(p.Accent);
                _switchJalaliText.Foreground = CapsuleTheme.Brush(p.AccentFg);

                _switchGregorian.Background = Brushes.Transparent;
                _switchGregorianText.Foreground = CapsuleTheme.Brush(p.Text2);
            }
            else
            {
                _switchGregorian.Background = CapsuleTheme.Brush(p.Accent);
                _switchGregorianText.Foreground = CapsuleTheme.Brush(p.AccentFg);

                _switchJalali.Background = Brushes.Transparent;
                _switchJalaliText.Foreground = CapsuleTheme.Brush(p.Text2);
            }

            _btnPrevYearText.Text = isJalali ? $"« {CalendarService.ToPersianDigits((_service.DisplayYear - 1).ToString())}" : $"« {_service.DisplayYear - 1}";
            _btnNextYearText.Text = isJalali ? $"{CalendarService.ToPersianDigits((_service.DisplayYear + 1).ToString())} »" : $"{_service.DisplayYear + 1} »";
            _btnPrevYearText.FontFamily = isJalali ? VazirFont : LocalizationService.LatinFontFamily;
            _btnNextYearText.FontFamily = isJalali ? VazirFont : LocalizationService.LatinFontFamily;
        }

        private void RefreshWeekdaysHeader()
        {
            _weekdaysHeaderGrid.Children.Clear();
            var p = CapsuleTheme.Current;
            bool isJalali = (_service.CurrentCalendarType == CalendarType.Jalali);
            _weekdaysHeaderGrid.FlowDirection = isJalali ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            string[] names = isJalali
                ? new[] { "ش", "ی", "د", "س", "چ", "پ", "ج" }
                : new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };

            for (int i = 0; i < 7; i++)
            {
                bool isWeekend = isJalali ? (i == 6) : (i == 6);
                var tb = new TextBlock
                {
                    Text = names[i],
                    FontSize = 11.0,
                    FontWeight = FontWeights.Bold,
                    FontFamily = isJalali ? VazirFont : LocalizationService.LatinFontFamily,
                    Foreground = isWeekend ? new SolidColorBrush(Color.FromRgb(245, 90, 90)) : CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                _weekdaysHeaderGrid.Children.Add(tb);
            }
        }

        private void RefreshDaysGrid()
        {
            _daysGrid.Children.Clear();
            var p = CapsuleTheme.Current;
            var cells = _service.GetCurrentMonthCells();
            bool isJalali = (_service.CurrentCalendarType == CalendarType.Jalali);
            _daysGrid.FlowDirection = isJalali ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

            foreach (var cell in cells)
            {
                var cellBorder = BuildDayCell(cell, isJalali, p);
                _daysGrid.Children.Add(cellBorder);
            }
        }

        private Border BuildDayCell(CalendarCellData cell, bool isJalali, ThemePalette p)
        {
            var cellBorder = new Border
            {
                Height = 26,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(1.0, 0.5, 1.0, 0.5),
                Cursor = Cursors.Hand
            };

            // Background & Border styling
            if (cell.IsSelected)
            {
                cellBorder.Background = CapsuleTheme.Brush(p.Accent);
                cellBorder.BorderBrush = CapsuleTheme.Brush(p.Accent);
                cellBorder.BorderThickness = new Thickness(1);
            }
            else if (cell.IsToday)
            {
                cellBorder.Background = CapsuleTheme.Brush(p.Surface2);
                cellBorder.BorderBrush = CapsuleTheme.Brush(p.Accent);
                cellBorder.BorderThickness = new Thickness(1.5);
            }
            else
            {
                cellBorder.Background = Brushes.Transparent;
                cellBorder.BorderBrush = Brushes.Transparent;
                cellBorder.BorderThickness = new Thickness(1);
            }

            var cellGrid = new Grid();

            // Text Stack: Primary day (large) + Secondary day (small)
            var textStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string primaryStr = isJalali ? CalendarService.ToPersianDigits(cell.PrimaryDay.ToString()) : cell.PrimaryDay.ToString();
            string secondaryStr = isJalali ? cell.SecondaryDay.ToString() : CalendarService.ToPersianDigits(cell.SecondaryDay.ToString());

            Brush primaryFg;
            if (cell.IsSelected)
            {
                primaryFg = CapsuleTheme.Brush(p.AccentFg);
            }
            else if (!cell.IsCurrentMonth)
            {
                primaryFg = CapsuleTheme.Brush(p.Text2);
                cellBorder.Opacity = 0.30;
            }
            else if (cell.IsHoliday)
            {
                primaryFg = new SolidColorBrush(Color.FromRgb(245, 80, 80));
            }
            else
            {
                primaryFg = CapsuleTheme.Brush(p.Text);
            }

            var primaryTb = new TextBlock
            {
                Text = primaryStr,
                FontSize = 13.5,
                FontWeight = (cell.IsToday || cell.IsSelected) ? FontWeights.Bold : FontWeights.SemiBold,
                FontFamily = isJalali ? VazirFont : LocalizationService.LatinFontFamily,
                Foreground = primaryFg,
                VerticalAlignment = VerticalAlignment.Center
            };
            textStack.Children.Add(primaryTb);

            // Small secondary date marker
            if (cell.IsCurrentMonth)
            {
                var secondaryTb = new TextBlock
                {
                    Text = secondaryStr,
                    FontSize = 9.0,
                    FontFamily = isJalali ? LocalizationService.LatinFontFamily : VazirFont,
                    Foreground = cell.IsSelected ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text2),
                    Opacity = cell.IsSelected ? 0.80 : 0.50,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(3.5, 0, 0, 0.5)
                };
                textStack.Children.Add(secondaryTb);
            }

            cellGrid.Children.Add(textStack);

            // Event or Holiday Dot indicator at bottom
            if (cell.IsCurrentMonth && (cell.IsHoliday || cell.EventCount > 0))
            {
                var dot = new Ellipse
                {
                    Width = 4.0,
                    Height = 4.0,
                    Fill = cell.IsSelected
                        ? CapsuleTheme.Brush(p.AccentFg)
                        : (cell.IsHoliday ? new SolidColorBrush(Color.FromRgb(245, 80, 80)) : CapsuleTheme.Brush(p.Accent)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 1.0)
                };
                cellGrid.Children.Add(dot);
            }

            cellBorder.Child = cellGrid;

            // Hover state
            if (!cell.IsSelected)
            {
                cellBorder.MouseEnter += (_, _) => cellBorder.Background = CapsuleTheme.Brush(p.ControlFillHover);
                cellBorder.MouseLeave += (_, _) => cellBorder.Background = cell.IsToday ? CapsuleTheme.Brush(p.Surface2) : Brushes.Transparent;
            }

            cellBorder.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _service.SelectDate(cell.Date);
            };

            return cellBorder;
        }

        private void RefreshDayDetails()
        {
            var p = CapsuleTheme.Current;
            DateTime date = _service.SelectedDate;
            bool isJalali = (_service.CurrentCalendarType == CalendarType.Jalali);
            bool isPersian = isJalali;
            _eventsListPanel.FlowDirection = isJalali ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            bool isToday = (date.Date == DateTime.Today);

            int primaryDay = isJalali ? new PersianCalendar().GetDayOfMonth(date) : date.Day;
            _heroDayText.Text = isPersian || isJalali ? CalendarService.ToPersianDigits(primaryDay.ToString()) : primaryDay.ToString();
            _heroDayText.FontFamily = isJalali || isPersian ? VazirFont : LocalizationService.LatinFontFamily;

            string weekday = _service.GetWeekdayName(date, isPersian);
            _heroWeekdayText.Text = isToday ? $"{weekday} ({LocalizationService.T("cal.today", "Today")})" : weekday;
            _heroWeekdayText.FontFamily = VazirFont;

            if (isJalali)
            {
                // Primary line: Month & Year (the day is already the big hero number!)
                _heroPrimaryDateText.Text = _service.GetJalaliMonthYear(date, isPersian || isJalali);
                _heroPrimaryDateText.FontFamily = VazirFont;
                // Secondary line: Alternate Gregorian full date
                _heroSecondaryDateText.Text = _service.GetFullGregorianDate(date);
                _heroSecondaryDateText.FontFamily = LocalizationService.LatinFontFamily;
            }
            else
            {
                // Primary line: Gregorian Month & Year
                _heroPrimaryDateText.Text = _service.GetGregorianMonthYear(date);
                _heroPrimaryDateText.FontFamily = LocalizationService.LatinFontFamily;
                // Secondary line: Alternate Jalali full date
                _heroSecondaryDateText.Text = _service.GetFullJalaliDate(date, isPersian);
                _heroSecondaryDateText.FontFamily = VazirFont;
            }

            string hijri = _service.GetHijriDate(date, isPersian || isJalali);
            _heroHijriDateText.Text = string.IsNullOrEmpty(hijri) ? "" : hijri;
            _heroHijriDateText.FontFamily = VazirFont;

            var payload = _service.GetSelectedDayPayload();
            bool isHoliday = (date.DayOfWeek == DayOfWeek.Friday) || (payload?.IsHoliday ?? false);

            if (isHoliday)
            {
                _holidayBadge.Background = new SolidColorBrush(Color.FromRgb(235, 75, 75));
                _holidayBadgeText.Text = "🔴 " + LocalizationService.T("cal.holiday", "Holiday");
                _holidayBadge.Visibility = Visibility.Visible;
            }
            else
            {
                _holidayBadge.Background = new SolidColorBrush(Color.FromArgb(50, 48, 209, 88));
                _holidayBadgeText.Text = "💼 " + LocalizationService.T("cal.working_day", "Working Day");
                _holidayBadge.Visibility = Visibility.Visible;
            }

            // Populate Events List
            _eventsListPanel.Children.Clear();

            if (payload != null && payload.Events != null && payload.Events.Count > 0)
            {
                foreach (var ev in payload.Events)
                {
                    _eventsListPanel.Children.Add(BuildEventCard(ev, p));
                }
            }
            else
            {
                _eventsListPanel.Children.Add(_emptyEventsBox);
            }
        }

        private Border BuildEventCard(CalendarEventItem ev, ThemePalette p)
        {
            var card = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = ev.IsHoliday ? new SolidColorBrush(Color.FromArgb(90, 235, 75, 75)) : CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 5, 8, 5),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var stack = new StackPanel();

            var topRow = new DockPanel();

            var badge = new TextBlock
            {
                Text = ev.IsHoliday ? "🔴 تعطیل" : "📌 مناسبت",
                FontSize = 8.5,
                FontWeight = FontWeights.Bold,
                FontFamily = VazirFont,
                Foreground = ev.IsHoliday ? new SolidColorBrush(Color.FromRgb(245, 90, 90)) : CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(badge, Dock.Right);
            topRow.Children.Add(badge);

            var descTb = new TextBlock
            {
                Text = ev.Description,
                FontSize = 10.5,
                FontWeight = FontWeights.Medium,
                FontFamily = VazirFont,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            topRow.Children.Add(descTb);
            stack.Children.Add(topRow);

            if (!string.IsNullOrWhiteSpace(ev.AdditionalDescription))
            {
                var addTb = new TextBlock
                {
                    Text = ev.AdditionalDescription,
                    FontSize = 9.0,
                    FontFamily = VazirFont,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    Margin = new Thickness(0, 2, 0, 0),
                    Opacity = 0.85
                };
                stack.Children.Add(addTb);
            }

            card.Child = stack;
            return card;
        }

        private void CopyCurrentDateToClipboard()
        {
            try
            {
                DateTime d = _service.SelectedDate;
                string jDate = _service.GetFullJalaliDate(d, true);
                string gDate = _service.GetFullGregorianDate(d);
                string textToCopy = $"{jDate}  ·  {gDate}";

                System.Windows.Clipboard.SetText(textToCopy);

                _copyDateBtnText.Text = "✓ " + LocalizationService.T("cal.copied_hint", "Date copied");
                DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    _copyDateBtnText.Text = "📋 " + LocalizationService.T("cal.copy_date", "Copy Date");
                };
                timer.Start();
            }
            catch { }
        }

        private void ApplyTheme()
        {
            var p = CapsuleTheme.Current;
            _leftCard.Background = CapsuleTheme.Brush(p.Surface);
            _leftCard.BorderBrush = CapsuleTheme.Brush(p.Border);
            _rightCard.Background = CapsuleTheme.Brush(p.Surface);
            _rightCard.BorderBrush = CapsuleTheme.Brush(p.Border);

            _monthTitleText.Foreground = CapsuleTheme.Brush(p.Text);
            _monthSubtitleText.Foreground = CapsuleTheme.Brush(p.Text2);
            _heroWeekdayText.Foreground = CapsuleTheme.Brush(p.Text);
            _heroPrimaryDateText.Foreground = CapsuleTheme.Brush(p.Text);
            _heroSecondaryDateText.Foreground = CapsuleTheme.Brush(p.Text2);
            _heroHijriDateText.Foreground = CapsuleTheme.Brush(p.Text2);
            _heroDayText.Foreground = CapsuleTheme.Brush(p.Accent);

            _emptyEventsText.Foreground = CapsuleTheme.Brush(p.Text2);
            _copyDateBtn.Background = CapsuleTheme.Brush(p.Surface2);
            _copyDateBtn.BorderBrush = CapsuleTheme.Brush(p.Border);
            _copyDateBtnText.Foreground = CapsuleTheme.Brush(p.Text);
        }

        private static Border CreateMiniCircleButton(string glyph, double size, string toolTip, Action onClick)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size / 2),
                Background = CapsuleTheme.Brush(p.Surface2),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = toolTip,
                Child = new TextBlock
                {
                    Text = glyph,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = CapsuleTheme.Brush(p.Text),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -1, 0, 0)
                }
            };
            border.MouseEnter += (_, _) => border.Background = CapsuleTheme.Brush(p.ControlFillHover);
            border.MouseLeave += (_, _) => border.Background = CapsuleTheme.Brush(p.Surface2);
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                onClick();
            };
            return border;
        }

        #endregion
    }
}
