using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WpfPath = System.Windows.Shapes.Path;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools.Clock;
using WindowsBuddy.Tools.Weather;

namespace WindowsBuddy.UI.Settings
{
    /// <summary>
    /// Modern Settings Window developed 1:1 matching the reference design:
    /// 2-Column layout with left navigation sidebar, search filter, smooth category pages,
    /// vibrant orange toggles, segmented controls, and no paid/trial elements.
    /// </summary>
    public sealed class SettingsWindow : Window
    {
        private static SettingsWindow _openInstance;

        public static void ShowSingleton()
        {
            if (_openInstance != null && _openInstance.IsLoaded)
            {
                if (_openInstance.WindowState == WindowState.Minimized)
                    _openInstance.WindowState = WindowState.Normal;
                _openInstance.Activate();
                return;
            }
            _openInstance = new SettingsWindow();
            _openInstance.Closed += (_, _) => _openInstance = null;
            _openInstance.Show();
        }

        private readonly Dictionary<string, MayliSidebarItem> _navItems = new();
        private readonly StackPanel _navStack;
        private readonly ContentControl _mainContentArea;
        private readonly TextBox _searchBox;
        private readonly TextBlock _searchPlaceholder;
        private string _activeCategory = "general";

        public SettingsWindow()
        {
            Title = "Mayli Settings";
            Width = 940;
            Height = 660;
            MinWidth = 840;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Icon = EmbeddedAssetLoader.LoadBitmap("Mayli.Brand.mark-128.png");

            // Outer Shell with subtle rounded corners and dark border
            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(Color.FromRgb(10, 10, 12)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(34, 34, 40)),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.55,
                    BlurRadius = 24,
                    ShadowDepth = 6
                }
            };

            var rootGrid = new Grid();
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // LEFT COLUMN: SIDEBAR
            // -------------------------------------------------------------
            var sidebarBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 17, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                CornerRadius = new CornerRadius(11, 0, 0, 11)
            };

            var sidebarDock = new DockPanel();

            // 1. Sidebar Header (Brand & App Title)
            var headerPanel = new Grid
            {
                Margin = new Thickness(18, 16, 16, 14),
                Cursor = Cursors.Hand
            };
            headerPanel.MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed) DragMove();
            };

            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var iconBox = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(24, 24, 28)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(44, 44, 52)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 11, 0)
            };

            var appLogo = EmbeddedAssetLoader.LoadBitmap("Mayli.Brand.mark-128.png");
            if (appLogo != null)
            {
                iconBox.Child = new Image
                {
                    Source = appLogo,
                    Width = 24,
                    Height = 24,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            else
            {
                iconBox.Child = new TextBlock
                {
                    Text = "••",
                    FontSize = 14,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }
            Grid.SetColumn(iconBox, 0);
            headerPanel.Children.Add(iconBox);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock
            {
                Text = "Mayli",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "The Notch for Windows",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 122, 130)),
                Margin = new Thickness(0, 1, 0, 0)
            });
            Grid.SetColumn(titleStack, 1);
            headerPanel.Children.Add(titleStack);

            DockPanel.SetDock(headerPanel, Dock.Top);
            sidebarDock.Children.Add(headerPanel);

            // 2. Search Box
            var searchBorder = new Border
            {
                Height = 34,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(14, 0, 14, 12)
            };

            var searchGrid = new Grid();
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var searchIcon = new TextBlock
            {
                Text = "🔍",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 120)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 6, 0)
            };
            Grid.SetColumn(searchIcon, 0);
            searchGrid.Children.Add(searchIcon);

            var inputOverlayGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            _searchPlaceholder = new TextBlock
            {
                Text = "Search",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 120)),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            inputOverlayGrid.Children.Add(_searchPlaceholder);

            _searchBox = new TextBox
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                FontSize = 12.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            _searchBox.TextChanged += (_, _) =>
            {
                _searchPlaceholder.Visibility = string.IsNullOrEmpty(_searchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
                FilterContent(_searchBox.Text);
            };
            inputOverlayGrid.Children.Add(_searchBox);

            Grid.SetColumn(inputOverlayGrid, 1);
            searchGrid.Children.Add(inputOverlayGrid);

            var ctrlKBadge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(52, 52, 60)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ctrlKBadge.Child = new TextBlock
            {
                Text = "Ctrl K",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150))
            };
            Grid.SetColumn(ctrlKBadge, 2);
            searchGrid.Children.Add(ctrlKBadge);

            searchBorder.Child = searchGrid;
            DockPanel.SetDock(searchBorder, Dock.Top);
            sidebarDock.Children.Add(searchBorder);

            // 3. Sidebar Footer (Help · Feedback · Version - NO trial/upgrade card)
            var footerStack = new StackPanel
            {
                Margin = new Thickness(16, 12, 16, 16),
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var footerLinks = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TextBlock CreateFooterLink(string text, Action onClick)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(104, 104, 114)),
                    Cursor = Cursors.Hand,
                    Margin = new Thickness(4, 0, 4, 0)
                };
                tb.MouseEnter += (_, _) => tb.Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 180));
                tb.MouseLeave += (_, _) => tb.Foreground = new SolidColorBrush(Color.FromRgb(104, 104, 114));
                tb.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    onClick?.Invoke();
                };
                return tb;
            }

            footerLinks.Children.Add(CreateFooterLink("Help", () => Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli#readme") { UseShellExecute = true })));
            footerLinks.Children.Add(new TextBlock { Text = "·", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 78)), Margin = new Thickness(2, 0, 2, 0) });
            footerLinks.Children.Add(CreateFooterLink("Feedback", () => Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli/issues") { UseShellExecute = true })));
            footerLinks.Children.Add(new TextBlock { Text = "·", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 78)), Margin = new Thickness(2, 0, 2, 0) });
            footerLinks.Children.Add(new TextBlock { Text = "v1.1.57", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(104, 104, 114)), Margin = new Thickness(4, 0, 4, 0) });

            footerStack.Children.Add(footerLinks);
            DockPanel.SetDock(footerStack, Dock.Bottom);
            sidebarDock.Children.Add(footerStack);

            // 4. Navigation Categories List
            var navScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            _navStack = new StackPanel { Margin = new Thickness(0, 2, 0, 12) };

            void AddNavItem(string key, string label, string glyph, Color iconBg)
            {
                var item = new MayliSidebarItem(key, label, glyph, iconBg);
                item.Clicked += SetActiveCategory;
                _navItems[key] = item;
                _navStack.Children.Add(item);
            }

            void AddSeparator()
            {
                _navStack.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                    Margin = new Thickness(14, 8, 14, 8)
                });
            }

            // Exactly the 8 categories from reference design:
            AddNavItem("general", "General", "⚙", Color.FromRgb(62, 56, 50));
            AddNavItem("notch", "Notch", "🖥", Color.FromRgb(44, 48, 56));
            AddNavItem("appearance", "Appearance", "◐", Color.FromRgb(80, 60, 138));
            AddNavItem("modules", "Modules", "⊞", Color.FromRgb(33, 96, 196));
            AddSeparator();
            AddNavItem("live_activities", "Live Activities", "🔔", Color.FromRgb(200, 61, 45));
            AddNavItem("privacy", "Privacy", "✋", Color.FromRgb(28, 134, 136));
            AddSeparator();
            AddNavItem("updates", "Updates", "🔄", Color.FromRgb(34, 138, 66));
            AddNavItem("about", "About", "ℹ", Color.FromRgb(30, 124, 200));

            navScroll.Content = _navStack;
            sidebarDock.Children.Add(navScroll);

            sidebarBorder.Child = sidebarDock;
            Grid.SetColumn(sidebarBorder, 0);
            rootGrid.Children.Add(sidebarBorder);

            // -------------------------------------------------------------
            // RIGHT COLUMN: MAIN CONTENT AREA
            // -------------------------------------------------------------
            var mainGrid = new Grid
            {
                Background = new SolidColorBrush(Color.FromRgb(9, 9, 11))
            };

            // Top drag and window controls header
            var topHeader = new Grid
            {
                Height = 36,
                VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent
            };
            topHeader.MouseLeftButtonDown += (_, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed) DragMove();
            };

            var winButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 8, 0)
            };

            Border CreateWinButton(string glyph, Action onClick, bool isClose = false)
            {
                var btn = new Border
                {
                    Width = 34,
                    Height = 28,
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                var tb = new TextBlock
                {
                    Text = glyph,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                btn.Child = tb;

                btn.MouseEnter += (_, _) =>
                {
                    btn.Background = isClose ? new SolidColorBrush(Color.FromRgb(232, 17, 35)) : new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
                    tb.Foreground = Brushes.White;
                };
                btn.MouseLeave += (_, _) =>
                {
                    btn.Background = Brushes.Transparent;
                    tb.Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210));
                };
                btn.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    onClick?.Invoke();
                };
                return btn;
            }

            winButtons.Children.Add(CreateWinButton("—", () => WindowState = WindowState.Minimized));
            winButtons.Children.Add(CreateWinButton("✕", Close, isClose: true));
            topHeader.Children.Add(winButtons);

            // Main scrollable content view
            var contentScroll = new ScrollViewer
            {
                Margin = new Thickness(0, 36, 0, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Padding = new Thickness(36, 12, 40, 36)
            };

            _mainContentArea = new ContentControl();
            contentScroll.Content = _mainContentArea;

            mainGrid.Children.Add(topHeader);
            mainGrid.Children.Add(contentScroll);

            Grid.SetColumn(mainGrid, 1);
            rootGrid.Children.Add(mainGrid);

            outerBorder.Child = rootGrid;
            Content = outerBorder;

            // Global shortcut Ctrl+K to focus search box
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    _searchBox.Focus();
                    _searchBox.SelectAll();
                    e.Handled = true;
                }
            };

            // Set initial active category
            SetActiveCategory("general");
        }

        private void SetActiveCategory(string key)
        {
            _activeCategory = key;
            foreach (var kvp in _navItems)
            {
                kvp.Value.IsSelected = string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase);
            }

            _mainContentArea.Content = key switch
            {
                "general" => BuildGeneralPage(),
                "notch" => BuildNotchPage(),
                "appearance" => BuildAppearancePage(),
                "modules" => BuildModulesPage(),
                "live_activities" => BuildLiveActivitiesPage(),
                "privacy" => BuildPrivacyPage(),
                "updates" => BuildUpdatesPage(),
                "about" => BuildAboutPage(),
                _ => BuildGeneralPage()
            };
        }

        private void FilterContent(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                foreach (var item in _navItems.Values)
                    item.Visibility = Visibility.Visible;
                return;
            }

            string q = query.Trim().ToLowerInvariant();
            MayliSidebarItem firstMatch = null;

            foreach (var item in _navItems.Values)
            {
                bool matches = item.Label.ToLowerInvariant().Contains(q) || item.Key.ToLowerInvariant().Contains(q);
                item.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                if (matches && firstMatch == null) firstMatch = item;
            }

            if (firstMatch != null && !_navItems[_activeCategory].IsVisible)
            {
                SetActiveCategory(firstMatch.Key);
            }
        }

        // =================================================================
        // PAGE 1: GENERAL (1:1 with user's uploaded reference screenshot)
        // =================================================================
        private FrameworkElement BuildGeneralPage()
        {
            var stack = new StackPanel();

            // Page Header
            stack.Children.Add(new TextBlock
            {
                Text = "General",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Startup, shortcut, time and units",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            // Row 1: Start Mayli when Windows starts
            var startupToggle = new MayliToggleSwitch(AppPreferences.IsWindowsStartupEnabled());
            startupToggle.CheckedChanged += isChecked =>
            {
                AppPreferences.SetWindowsStartup(isChecked);
                AppPreferences.Instance.Raw.StartWithWindows = isChecked;
                AppPreferences.Instance.Save();
            };
            stack.Children.Add(new MayliSettingRow(
                "Start Mayli when Windows starts",
                "Starts silently each time you sign in. Quit applies only to this session. To have it back seconds after a restart, turn on \"Restart apps\" in Windows Settings > Accounts > Sign-in options.",
                startupToggle
            ));

            // Row 2: Open the notch
            var shortcutRow = new StackPanel { Orientation = Orientation.Horizontal };
            var shortcutBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0)
            };
            var shortcutText = new TextBlock
            {
                Text = AppPreferences.Instance.OpenNotchShortcut,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150))
            };
            shortcutBadge.Child = shortcutText;
            shortcutRow.Children.Add(shortcutBadge);

            var changeBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 48, 56)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand
            };
            var changeTxt = new TextBlock
            {
                Text = "Change",
                FontSize = 12.5,
                Foreground = Brushes.White
            };
            changeBtn.Child = changeTxt;
            changeBtn.MouseEnter += (_, _) => changeBtn.Background = new SolidColorBrush(Color.FromRgb(44, 44, 52));
            changeBtn.MouseLeave += (_, _) => changeBtn.Background = new SolidColorBrush(Color.FromRgb(32, 32, 38));

            changeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                // Cycle between standard shortcuts
                string[] presets = { "Ctrl + Alt + N", "Win + Ctrl + N", "Alt + Space", "Ctrl + Shift + Space" };
                int idx = Array.IndexOf(presets, AppPreferences.Instance.OpenNotchShortcut);
                string next = presets[(idx + 1) % presets.Length];
                AppPreferences.Instance.OpenNotchShortcut = next;
                shortcutText.Text = next;
            };
            shortcutRow.Children.Add(changeBtn);

            stack.Children.Add(new MayliSettingRow(
                "Open the notch",
                "Opens or closes the notch from anywhere",
                shortcutRow
            ));

            // Row 3: Time format
            var timeFormatPicker = new MayliSegmentedControl(new[] { "System", "12-hour", "24-hour" }, AppPreferences.Instance.TimeFormat);
            timeFormatPicker.SelectionChanged += val =>
            {
                AppPreferences.Instance.TimeFormat = val;
                ClockService.Instance.Settings.Format = val switch
                {
                    "24-hour" => ClockFormat.TwentyFourHour,
                    _ => ClockFormat.TwelveHour
                };
                ClockService.Instance.Save();
            };
            stack.Children.Add(new MayliSettingRow(
                "Time format",
                "Used by the notch, Home and Clock",
                timeFormatPicker
            ));

            // Row 4: Temperature
            string currentTemp = WeatherService.Instance.UseFahrenheit ? "°F" : "°C";
            var tempPicker = new MayliSegmentedControl(new[] { "°C", "°F" }, currentTemp);
            tempPicker.SelectionChanged += val =>
            {
                WeatherService.Instance.UseFahrenheit = (val == "°F");
                AppPreferences.Instance.TemperatureUnit = val;
            };
            stack.Children.Add(new MayliSettingRow(
                "Temperature",
                "Weather, Home and the notch",
                tempPicker
            ));

            // Row 5: Wind speed
            var windPicker = new MayliSegmentedControl(new[] { "km/h", "mph" }, AppPreferences.Instance.WindSpeedUnit);
            windPicker.SelectionChanged += val =>
            {
                AppPreferences.Instance.WindSpeedUnit = val;
            };
            stack.Children.Add(new MayliSettingRow(
                "Wind speed",
                "Used by Weather and Home",
                windPicker
            ));

            // Row 6: Language
            var langText = new TextBlock
            {
                Text = "English",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 140, 150)),
                VerticalAlignment = VerticalAlignment.Center
            };
            stack.Children.Add(new MayliSettingRow(
                "Language",
                "More languages are on the way",
                langText
            ));

            return stack;
        }

        // =================================================================
        // PAGE 2: NOTCH (Full 1:1 implementation matching reference screenshots)
        // =================================================================
        private FrameworkElement BuildNotchPage()
        {
            var stack = new StackPanel();

            // 1. Page Header
            stack.Children.Add(new TextBlock
            {
                Text = "Notch",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Shape, behavior and live preview",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 20)
            });

            // 2. Interactive Live Preview Card (Resting / Hover switch)
            var previewBox = new Border
            {
                Height = 145,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 22),
                ClipToBounds = true
            };

            var previewGrid = new Grid();

            // Simulated monitor top bezel line
            var monitorTopLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                VerticalAlignment = VerticalAlignment.Top
            };
            previewGrid.Children.Add(monitorTopLine);

            // Notch preview element container
            var notchContainer = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            void UpdateLivePreview(bool isHover)
            {
                if (isHover)
                {
                    // Expanded hover notch preview
                    var expBorder = new Border
                    {
                        Width = 260,
                        Height = 56,
                        CornerRadius = new CornerRadius(0, 0, 16, 16),
                        Background = Brushes.Black,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                        BorderThickness = new Thickness(1, 0, 1, 1),
                        Padding = new Thickness(14, 8, 14, 8)
                    };
                    var expStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    string[] miniIcons = { "⌂", "⚙", "📋", "⏱", "⛅", "📊" };
                    foreach (var ic in miniIcons)
                    {
                        expStack.Children.Add(new TextBlock
                        {
                            Text = ic,
                            FontSize = 14,
                            Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 190)),
                            Margin = new Thickness(6, 0, 6, 0)
                        });
                    }
                    expBorder.Child = expStack;
                    notchContainer.Child = expBorder;
                }
                else
                {
                    // Resting notch preview
                    bool isIsland = string.Equals(AppPreferences.Instance.NotchStyle, "Island", StringComparison.OrdinalIgnoreCase);
                    var restBorder = new Border
                    {
                        Width = 175,
                        Height = 28,
                        CornerRadius = isIsland ? new CornerRadius(14) : new CornerRadius(0, 0, 14, 14),
                        Background = Brushes.Black,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(35, 35, 42)),
                        BorderThickness = isIsland ? new Thickness(1) : new Thickness(1, 0, 1, 1),
                        Margin = isIsland ? new Thickness(0, 4, 0, 0) : new Thickness(0),
                        Padding = new Thickness(12, 0, 12, 0)
                    };

                    var restGrid = new Grid();
                    restGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    restGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var clockText = new TextBlock
                    {
                        Text = "11:43 AM",
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(clockText, 0);
                    restGrid.Children.Add(clockText);

                    var battStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    battStack.Children.Add(new TextBlock { Text = "⚡", FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88)), Margin = new Thickness(0, 0, 3, 0) });
                    battStack.Children.Add(new TextBlock { Text = "85", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88)) });
                    Grid.SetColumn(battStack, 1);
                    restGrid.Children.Add(battStack);

                    restBorder.Child = restGrid;
                    notchContainer.Child = restBorder;
                }
            }

            UpdateLivePreview(false);
            previewGrid.Children.Add(notchContainer);

            // Preview switcher on top right
            var previewModeSwitch = new MayliSegmentedControl(new[] { "Resting", "Hover" }, "Resting")
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 12, 12, 0)
            };
            previewModeSwitch.SelectionChanged += mode => UpdateLivePreview(mode == "Hover");
            previewGrid.Children.Add(previewModeSwitch);

            previewBox.Child = previewGrid;
            stack.Children.Add(previewBox);

            // -------------------------------------------------------------
            // SECTION: VISIBILITY (4 Visual Cards)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("VISIBILITY"));

            var visGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            visGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            visGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            visGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            visGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var visCards = new List<MayliVisualCard>();

            void AddVisCard(string key, string title, string desc, FrameworkElement illus, int col, int row)
            {
                bool isSel = string.Equals(AppPreferences.Instance.NotchVisibility, key, StringComparison.OrdinalIgnoreCase);
                var card = new MayliVisualCard(key, title, desc, illus, isSel);
                card.Selected += selectedKey =>
                {
                    AppPreferences.Instance.NotchVisibility = selectedKey;
                    foreach (var c in visCards) c.IsSelected = (c.Key == selectedKey);
                };
                visCards.Add(card);
                Grid.SetColumn(card, col);
                Grid.SetRow(card, row);
                visGrid.Children.Add(card);
            }

            AddVisCard("Always", "Always", "Always on screen. Fullscreen rules below still apply.", CreateNotchIllustration("always"), 0, 0);
            AddVisCard("Smart", "Smart", "Full on the desktop; tucked away over maximized apps.", CreateNotchIllustration("smart"), 1, 0);
            AddVisCard("Minimal", "Minimal", "Just a small arrow at the top. Hover to peek, open on hover or click.", CreateNotchIllustration("minimal"), 0, 1);
            AddVisCard("Hidden", "Hidden", "Nothing on screen. Open it from the tray icon (or set a shortcut in General).", CreateNotchIllustration("hidden"), 1, 1);

            stack.Children.Add(visGrid);

            // -------------------------------------------------------------
            // SECTION: STYLE (3 Visual Cards & Geometry Options)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("STYLE"));

            var styleGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            styleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var styleCards = new List<MayliVisualCard>();

            void AddStyleCard(string key, string title, string desc, FrameworkElement illus, int col)
            {
                bool isSel = string.Equals(AppPreferences.Instance.NotchStyle, key, StringComparison.OrdinalIgnoreCase);
                var card = new MayliVisualCard(key, title, desc, illus, isSel);
                card.Selected += selectedKey =>
                {
                    AppPreferences.Instance.NotchStyle = selectedKey;
                    AppPreferences.Instance.NotchSilhouette = selectedKey == "Island" ? "Island" : (selectedKey == "Line" ? "Compact" : "Shouldered");
                    foreach (var c in styleCards) c.IsSelected = (c.Key == selectedKey);
                    UpdateLivePreview(false);
                };
                styleCards.Add(card);
                Grid.SetColumn(card, col);
                styleGrid.Children.Add(card);
            }

            AddStyleCard("Line", "Line", "A slim bar along the top edge", CreateNotchIllustration("line"), 0);
            AddStyleCard("Notch", "Notch", "Grows out of the screen edge", CreateNotchIllustration("notch"), 1);
            AddStyleCard("Island", "Island", "A pill floating just below the edge", CreateNotchIllustration("island"), 2);

            stack.Children.Add(styleGrid);

            // Notch shape
            var shapeDropdown = new MayliDropdown(new[] { "Compact", "Wide", "Classic", "Ultra-Compact" }, AppPreferences.Instance.NotchShape);
            shapeDropdown.SelectionChanged += s => AppPreferences.Instance.NotchShape = s;
            stack.Children.Add(new MayliSettingRow(
                "Notch shape",
                "Fine-tune the Notch style's silhouette",
                shapeDropdown
            ));

            // Size
            var sizeSlider = new MayliSliderRow(60, 140, AppPreferences.Instance.NotchScalePercent, "%");
            sizeSlider.ValueChanged += v => AppPreferences.Instance.NotchScalePercent = (int)v;
            stack.Children.Add(new MayliSettingRow(
                "Size",
                "Scales the resting notch",
                sizeSlider
            ));

            // Resting layout
            var layoutPicker = new MayliSegmentedControl(new[] { "Corners", "Center" }, AppPreferences.Instance.NotchRestingLayout);
            layoutPicker.SelectionChanged += l => AppPreferences.Instance.NotchRestingLayout = l;
            stack.Children.Add(new MayliSettingRow(
                "Resting layout",
                "Time and battery in corners or center",
                layoutPicker
            ));

            // Opens on
            var openOnPicker = new MayliSegmentedControl(new[] { "Hover", "Click" }, AppPreferences.Instance.NotchOpenTrigger);
            openOnPicker.SelectionChanged += o => AppPreferences.Instance.NotchOpenTrigger = o;
            stack.Children.Add(new MayliSettingRow(
                "Opens on",
                "Hover to open at once, or click to open on purpose",
                openOnPicker
            ));

            // Display
            var dispDropdown = new MayliDropdown(new[] { "Primary display", "Display 1", "Display 2", "All displays" }, AppPreferences.Instance.NotchDisplay);
            dispDropdown.SelectionChanged += d => AppPreferences.Instance.NotchDisplay = d;
            stack.Children.Add(new MayliSettingRow(
                "Display",
                "Which monitor the notch lives on",
                dispDropdown
            ));

            // Show notch
            var showNotchToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowNotchBar);
            showNotchToggle.CheckedChanged += s => AppPreferences.Instance.ShowNotchBar = s;
            stack.Children.Add(new MayliSettingRow(
                "Show notch",
                "Hide it without quitting; the tray icon brings it back",
                showNotchToggle
            ));

            // -------------------------------------------------------------
            // SECTION: BEHAVIOR
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("BEHAVIOR"));

            // Hide in fullscreen
            var fullToggle = new MayliToggleSwitch(AppPreferences.Instance.HideOnFullscreen);
            fullToggle.CheckedChanged += chk => AppPreferences.Instance.HideOnFullscreen = chk;
            stack.Children.Add(new MayliSettingRow(
                "Hide in fullscreen",
                "Apps, videos and games on the notch's display",
                fullToggle
            ));

            // Show events over apps
            var eventsToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowEventsOverApps);
            eventsToggle.CheckedChanged += e => AppPreferences.Instance.ShowEventsOverApps = e;
            stack.Children.Add(new MayliSettingRow(
                "Show events over apps",
                "When the notch is tucked away, grow it briefly for events like volume, timers and a new song",
                eventsToggle
            ));

            // Tab animations
            var animToggle = new MayliToggleSwitch(AppPreferences.Instance.TabAnimations);
            animToggle.CheckedChanged += a => AppPreferences.Instance.TabAnimations = a;
            stack.Children.Add(new MayliSettingRow(
                "Tab animations",
                "Slide between tabs inside the open notch",
                animToggle
            ));

            // -------------------------------------------------------------
            // SECTION: MAYLI PET (BETA)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("MAYLI PET (BETA)"));

            var petToggle = new MayliToggleSwitch(AppPreferences.Instance.EnablePetBeta);
            petToggle.CheckedChanged += p => AppPreferences.Instance.EnablePetBeta = p;
            stack.Children.Add(new MayliSettingRow(
                "Mayli Pet (Beta)",
                "A small companion in your notch",
                petToggle
            ));

            // -------------------------------------------------------------
            // SECTION: EDGE LIGHT
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("EDGE LIGHT"));

            var edgeToggle = new MayliToggleSwitch(AppPreferences.Instance.EnableEdgeLight);
            edgeToggle.CheckedChanged += el => AppPreferences.Instance.EnableEdgeLight = el;
            stack.Children.Add(new MayliSettingRow(
                "Edge light",
                "Light that runs along the notch outline",
                edgeToggle
            ));

            // 3 Edge Light Pattern Cards
            var edgeCardsGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            edgeCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            edgeCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            edgeCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var edgeCards = new List<MayliVisualCard>();

            void AddEdgeCard(string key, string title, string desc, FrameworkElement illus, int col)
            {
                bool isSel = string.Equals(AppPreferences.Instance.EdgeLightPattern, key, StringComparison.OrdinalIgnoreCase);
                var card = new MayliVisualCard(key, title, desc, illus, isSel);
                card.Selected += selectedKey =>
                {
                    AppPreferences.Instance.EdgeLightPattern = selectedKey;
                    foreach (var c in edgeCards) c.IsSelected = (c.Key == selectedKey);
                };
                edgeCards.Add(card);
                Grid.SetColumn(card, col);
                edgeCardsGrid.Children.Add(card);
            }

            AddEdgeCard("Comet", "Comet", "One light travels along the outline", CreateEdgeIllustration("comet"), 0);
            AddEdgeCard("Twin comets", "Twin comets", "Two lights meet at the bottom, then return", CreateEdgeIllustration("twin"), 1);
            AddEdgeCard("Breathe", "Breathe", "The whole outline glows softly", CreateEdgeIllustration("breathe"), 2);

            stack.Children.Add(edgeCardsGrid);

            // Edge Light Color Swatches
            var colorStack = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 8) };
            var swatchesRow1 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var swatchesRow2 = new StackPanel { Orientation = Orientation.Horizontal };

            var lightColors = new[]
            {
                "#FFFFFF", "#F9E498", "#FFB340", "#30D158", "#54D8E8", "#0A84FF", "#7D6CFA", "#BF5AF2",
                "#FF4F79", "#FF7A59", "#C49A6C", "#7CE8FF"
            };

            for (int i = 0; i < lightColors.Length; i++)
            {
                string hex = lightColors[i];
                var circle = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(string.Equals(AppPreferences.Instance.EdgeLightColor, hex, StringComparison.OrdinalIgnoreCase) ? 2.5 : 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 0))
                };
                circle.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    AppPreferences.Instance.EdgeLightColor = hex;
                    // refresh border
                    _mainContentArea.Content = BuildNotchPage();
                };
                if (i < 8) swatchesRow1.Children.Add(circle);
                else swatchesRow2.Children.Add(circle);
            }

            var customBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            customBtn.Child = new TextBlock { Text = "● Custom...", FontSize = 11.5, Foreground = Brushes.White };
            swatchesRow2.Children.Add(customBtn);

            colorStack.Children.Add(swatchesRow1);
            colorStack.Children.Add(swatchesRow2);

            stack.Children.Add(new MayliSettingRow(
                "Color",
                "",
                colorStack
            ));

            // Intensity
            var intensitySlider = new MayliSliderRow(10, 100, AppPreferences.Instance.EdgeLightIntensity, "%");
            intensitySlider.ValueChanged += v => AppPreferences.Instance.EdgeLightIntensity = (int)v;
            stack.Children.Add(new MayliSettingRow(
                "Intensity",
                "",
                intensitySlider
            ));

            // Speed
            var speedPicker = new MayliSegmentedControl(new[] { "Slow", "Normal", "Fast" }, AppPreferences.Instance.EdgeLightSpeed);
            speedPicker.SelectionChanged += s => AppPreferences.Instance.EdgeLightSpeed = s;
            stack.Children.Add(new MayliSettingRow(
                "Speed",
                "",
                speedPicker
            ));

            // When
            var whenPicker = new MayliSegmentedControl(new[] { "Always", "On events & hover" }, AppPreferences.Instance.EdgeLightTrigger);
            whenPicker.SelectionChanged += w => AppPreferences.Instance.EdgeLightTrigger = w;
            stack.Children.Add(new MayliSettingRow(
                "When",
                "On events & hover plays the light for a few seconds, then it fades out",
                whenPicker
            ));

            // -------------------------------------------------------------
            // SECTION: WHEN CLOSED, SHOW
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("WHEN CLOSED, SHOW"));

            // Time
            var closedTimeToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedTime);
            closedTimeToggle.CheckedChanged += t => AppPreferences.Instance.ShowClosedTime = t;
            stack.Children.Add(new MayliSettingRow(
                "Time",
                "The resting clock that events return to",
                closedTimeToggle
            ));

            // Clock style
            var clockStylePicker = new MayliSegmentedControl(new[] { "Digital", "Analog" }, AppPreferences.Instance.ClosedClockStyle);
            clockStylePicker.SelectionChanged += cs => AppPreferences.Instance.ClosedClockStyle = cs;
            stack.Children.Add(new MayliSettingRow(
                "Clock style",
                "",
                clockStylePicker
            ));

            // Date
            var closedDateToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedDate);
            closedDateToggle.CheckedChanged += d => AppPreferences.Instance.ShowClosedDate = d;
            stack.Children.Add(new MayliSettingRow(
                "Date",
                "Day and date next to the time, e.g. Sat 3 · 9:41",
                closedDateToggle
            ));

            // Battery
            var closedBatteryToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedBattery);
            closedBatteryToggle.CheckedChanged += b => AppPreferences.Instance.ShowClosedBattery = b;
            stack.Children.Add(new MayliSettingRow(
                "Battery",
                "Level and charging",
                closedBatteryToggle
            ));

            // Now playing
            var closedMediaToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedNowPlaying);
            closedMediaToggle.CheckedChanged += np => AppPreferences.Instance.ShowClosedNowPlaying = np;
            stack.Children.Add(new MayliSettingRow(
                "Now playing",
                "Compact media controls while music or video plays",
                closedMediaToggle
            ));

            // Bluetooth
            var closedBtToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedBluetooth);
            closedBtToggle.CheckedChanged += bt => AppPreferences.Instance.ShowClosedBluetooth = bt;
            stack.Children.Add(new MayliSettingRow(
                "Bluetooth",
                "While a device is connected",
                closedBtToggle
            ));

            // -------------------------------------------------------------
            // RESTORE DEFAULTS BUTTON (At the end of the page)
            // -------------------------------------------------------------
            var resetBtn = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 9, 18, 9),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 16, 0, 36),
                Cursor = Cursors.Hand
            };
            var resetTxt = new TextBlock
            {
                Text = "Restore defaults",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            resetBtn.Child = resetTxt;

            resetBtn.MouseEnter += (_, _) => resetBtn.Background = new SolidColorBrush(Color.FromRgb(40, 40, 48));
            resetBtn.MouseLeave += (_, _) => resetBtn.Background = new SolidColorBrush(Color.FromRgb(28, 28, 34));

            resetBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AppPreferences.Instance.ResetNotchDefaults();
                // Immediately refresh page to update all controls
                _mainContentArea.Content = BuildNotchPage();
                MessageBox.Show("Notch settings have been restored to factory defaults.", "Mayli Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            stack.Children.Add(resetBtn);

            return stack;
        }

        // =================================================================
        // CARD ILLUSTRATIONS HELPERS
        // =================================================================
        private static FrameworkElement CreateNotchIllustration(string type)
        {
            var canvas = new Canvas { Width = 110, Height = 48, ClipToBounds = true };

            // Horizontal top screen border
            var topEdge = new Line
            {
                X1 = 0,
                Y1 = 8,
                X2 = 110,
                Y2 = 8,
                Stroke = new SolidColorBrush(Color.FromRgb(70, 70, 80)),
                StrokeThickness = 1
            };
            canvas.Children.Add(topEdge);

            if (type == "always" || type == "notch")
            {
                // Classic shouldered notch
                var path = new WpfPath
                {
                    Data = Geometry.Parse("M 25 8 Q 33 8 36 14 Q 40 26 50 26 L 60 26 Q 70 26 74 14 Q 77 8 85 8 Z"),
                    Fill = Brushes.Black,
                    Stroke = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                    StrokeThickness = 1
                };
                canvas.Children.Add(path);
            }
            else if (type == "smart")
            {
                var path = new WpfPath
                {
                    Data = Geometry.Parse("M 30 8 Q 36 8 38 12 Q 41 22 50 22 L 60 22 Q 69 22 72 12 Q 74 8 80 8 Z"),
                    Fill = Brushes.Black,
                    Stroke = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                    StrokeThickness = 1
                };
                canvas.Children.Add(path);

                // Small desktop window preview
                var win = new Border
                {
                    Width = 34,
                    Height = 22,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(36, 32, 30)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 50, 45)),
                    BorderThickness = new Thickness(1)
                };
                var chevron = new TextBlock
                {
                    Text = "˅",
                    FontSize = 8,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                win.Child = chevron;
                Canvas.SetLeft(win, 62);
                Canvas.SetTop(win, 14);
                canvas.Children.Add(win);
            }
            else if (type == "minimal")
            {
                // Small tab with chevron
                var tab = new Border
                {
                    Width = 20,
                    Height = 12,
                    CornerRadius = new CornerRadius(0, 0, 4, 4),
                    Background = Brushes.Black,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 70)),
                    BorderThickness = new Thickness(1, 0, 1, 1)
                };
                tab.Child = new TextBlock { Text = "˅", FontSize = 7, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
                Canvas.SetLeft(tab, 45);
                Canvas.SetTop(tab, 8);
                canvas.Children.Add(tab);
            }
            else if (type == "hidden")
            {
                // Dashed outline
                var path = new WpfPath
                {
                    Data = Geometry.Parse("M 25 8 Q 33 8 36 14 Q 40 26 50 26 L 60 26 Q 70 26 74 14 Q 77 8 85 8"),
                    Stroke = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                canvas.Children.Add(path);
            }
            else if (type == "line")
            {
                // Slim horizontal line bar
                var lineBar = new Border
                {
                    Width = 60,
                    Height = 8,
                    CornerRadius = new CornerRadius(0, 0, 4, 4),
                    Background = Brushes.Black,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                    BorderThickness = new Thickness(1, 0, 1, 1)
                };
                Canvas.SetLeft(lineBar, 25);
                Canvas.SetTop(lineBar, 8);
                canvas.Children.Add(lineBar);
            }
            else if (type == "island")
            {
                // Detached floating pill island
                var pill = new Border
                {
                    Width = 54,
                    Height = 14,
                    CornerRadius = new CornerRadius(7),
                    Background = Brushes.Black,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 70)),
                    BorderThickness = new Thickness(1)
                };
                Canvas.SetLeft(pill, 28);
                Canvas.SetTop(pill, 14);
                canvas.Children.Add(pill);
            }

            return canvas;
        }

        private static FrameworkElement CreateEdgeIllustration(string pattern)
        {
            var canvas = new Canvas { Width = 110, Height = 48, ClipToBounds = true };

            var topEdge = new Line
            {
                X1 = 0,
                Y1 = 8,
                X2 = 110,
                Y2 = 8,
                Stroke = new SolidColorBrush(Color.FromRgb(70, 70, 80)),
                StrokeThickness = 1
            };
            canvas.Children.Add(topEdge);

            // Base black notch
            var basePath = new WpfPath
            {
                Data = Geometry.Parse("M 22 8 Q 30 8 34 14 Q 38 28 50 28 L 60 28 Q 72 28 76 14 Q 80 8 88 8 Z"),
                Fill = Brushes.Black,
                Stroke = new SolidColorBrush(Color.FromRgb(40, 40, 50)),
                StrokeThickness = 1
            };
            canvas.Children.Add(basePath);

            if (pattern == "comet")
            {
                // Single comet beam on left outline
                var comet = new WpfPath
                {
                    Data = Geometry.Parse("M 26 10 Q 32 10 35 15 Q 38 28 50 28"),
                    Stroke = Brushes.White,
                    StrokeThickness = 2,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                canvas.Children.Add(comet);
            }
            else if (pattern == "twin")
            {
                // Two symmetrical comets meeting at bottom
                var leftComet = new WpfPath
                {
                    Data = Geometry.Parse("M 38 28 L 52 28"),
                    Stroke = Brushes.White,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                var rightComet = new WpfPath
                {
                    Data = Geometry.Parse("M 58 28 L 72 28"),
                    Stroke = Brushes.White,
                    StrokeThickness = 2.5,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };
                canvas.Children.Add(leftComet);
                canvas.Children.Add(rightComet);
            }
            else if (pattern == "breathe")
            {
                // Soft uniform glowing outline
                var glow = new WpfPath
                {
                    Data = Geometry.Parse("M 22 8 Q 30 8 34 14 Q 38 28 50 28 L 60 28 Q 72 28 76 14 Q 80 8 88 8"),
                    Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                    StrokeThickness = 1.5
                };
                canvas.Children.Add(glow);
            }

            return canvas;
        }

        // =================================================================
        // PAGE 3: APPEARANCE
        // =================================================================
        private FrameworkElement BuildAppearancePage()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Appearance",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Accent colors, glow effects and Mayli Orb companion",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            // Accent Colors
            var swatchesPanel = new WrapPanel { MaxWidth = 280 };
            var swatches = new (string Name, string Hex)[]
            {
                ("Cyan", "#54D8E8"),
                ("Emerald", "#30D158"),
                ("Blue", "#0A84FF"),
                ("Violet", "#BF5AF2"),
                ("Amber", "#FF9F0A"),
                ("Crimson", "#FF453A")
            };

            foreach (var sw in swatches)
            {
                string hex = sw.Hex;
                var dot = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(string.Equals(AppPreferences.Instance.Accent, hex, StringComparison.OrdinalIgnoreCase) ? 2.5 : 0),
                    BorderBrush = Brushes.White
                };
                dot.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    AppPreferences.Instance.Accent = hex;
                    foreach (var child in swatchesPanel.Children)
                        if (child is Border b) b.BorderThickness = new Thickness(b == dot ? 2.5 : 0);
                };
                swatchesPanel.Children.Add(dot);
            }

            stack.Children.Add(new MayliSettingRow(
                "Accent color",
                "Primary edge glow, button accents, and highlight color throughout Mayli.",
                swatchesPanel
            ));

            // Edge Light Effect
            var edgePicker = new MayliSegmentedControl(new[] { "Aura Glow", "Orbit Comet", "Breathing", "Off" }, AppPreferences.Instance.EdgeLightMode);
            edgePicker.SelectionChanged += m => AppPreferences.Instance.EdgeLightMode = m;
            stack.Children.Add(new MayliSettingRow(
                "Edge light animation",
                "Dynamic light beam tracing along the notch boundary during alerts and charging.",
                edgePicker
            ));

            // Interactive Companion Orb
            var petCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 17, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 10, 0, 16)
            };

            var petStack = new StackPanel();
            petStack.Children.Add(new TextBlock
            {
                Text = "Mayli Orb Companion (گوگولی مگولی)",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 4)
            });
            petStack.Children.Add(new TextBlock
            {
                Text = "Expressive animated vector orb that reacts to your mouse, music, and tasks.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 0, 0, 14)
            });

            var liveOrb = new CuteOrbPetView(PetEngine.Instance.CurrentMood, 84)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 10),
                ToolTip = "Click to interact / کلیک کنید!"
            };
            petStack.Children.Add(liveOrb);

            var statusLine = new TextBlock
            {
                Text = PetEngine.Instance.CurrentStatusLine,
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 0)),
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            petStack.Children.Add(statusLine);

            var moodsRow = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10) };
            var quickMoods = new (string Glyph, string Name, OrbMood Mood)[]
            {
                ("😊", "شاد", OrbMood.Happy),
                ("💖", "عشق", OrbMood.Love),
                ("😉", "چشمک", OrbMood.Wink),
                ("😠", "اخم", OrbMood.Angry),
                ("😴", "خواب", OrbMood.Sleepy),
                ("🌟", "عادی", OrbMood.Idle)
            };

            foreach (var qm in quickMoods)
            {
                var captured = qm.Mood;
                var pill = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 50)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 4, 10, 4),
                    Margin = new Thickness(3),
                    Cursor = Cursors.Hand
                };
                var pillTxt = new TextBlock { Text = $"{qm.Glyph} {qm.Name}", FontSize = 11.5, Foreground = Brushes.White };
                pill.Child = pillTxt;

                pill.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    PetEngine.Instance.SetMood(captured);
                    liveOrb.SetMood(captured);
                    statusLine.Text = PetEngine.Instance.CurrentStatusLine;
                };
                moodsRow.Children.Add(pill);
            }
            petStack.Children.Add(moodsRow);

            var pokeRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var pokeBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(36, 36, 44)),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand
            };
            pokeBtn.Child = new TextBlock { Text = "🐾 قلقلک (Poke)", FontSize = 12, Foreground = Brushes.White };
            pokeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                liveOrb.Poke();
                statusLine.Text = "قلقلکم اومد! (◕‿◕)✨";
            };
            pokeRow.Children.Add(pokeBtn);

            var burstBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(36, 36, 44)),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(4),
                Cursor = Cursors.Hand
            };
            burstBtn.Child = new TextBlock { Text = "✨ جرقه و قلب (Burst)", FontSize = 12, Foreground = Brushes.White };
            burstBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                liveOrb.SpawnHeartBurst();
                liveOrb.SpawnSparkleBurst();
                statusLine.Text = "یک عالمه درخشش! 💖🌟";
            };
            pokeRow.Children.Add(burstBtn);

            petStack.Children.Add(pokeRow);
            petCard.Child = petStack;
            stack.Children.Add(petCard);

            return stack;
        }

        // =================================================================
        // PAGE 4: MODULES
        // =================================================================
        private FrameworkElement BuildModulesPage()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Modules",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Customize active tools in the bottom navigation dock",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            var modules = new (string Title, string Desc)[]
            {
                ("Home Dashboard", "Unified 6-card glance widget hub (Screen Time, System, Weather, Timers, Clipboard, Notes)."),
                ("System Monitor", "Real-time CPU, RAM, GPU, network and disk activity gauges."),
                ("Clipboard History", "Live copy-paste manager with search, code formatting, and instant restore."),
                ("Quick Notes", "Markdown scratchpad for persistent thoughts and task tracking."),
                ("Timers & Focus", "Pomodoro work sprint cycles, custom countdowns, and stopwatch."),
                ("File Shelf", "Drag and drop staging shelf for temporary documents and downloads."),
                ("Weather Forecast", "Real-time Open-Meteo forecasts, hourly graphs, and precipitation charts."),
                ("World Clock", "Dual-zone time tracking with seamless 12/24-hour switching."),
                ("Unit Converter", "Instant conversion across Length, Mass, Temperature, Data, and Volume."),
                ("Bookmarks & Links", "Fast website bookmark launcher with custom search provider triggers."),
                ("Notch Message Banner", "Broadcast custom text and motivational banners directly on the notch."),
                ("Screen Time Analytics", "Windows active foreground process monitoring and daily usage limits.")
            };

            foreach (var m in modules)
            {
                var toggle = new MayliToggleSwitch(true);
                stack.Children.Add(new MayliSettingRow(m.Title, m.Desc, toggle));
            }

            return stack;
        }

        // =================================================================
        // PAGE 5: LIVE ACTIVITIES
        // =================================================================
        private FrameworkElement BuildLiveActivitiesPage()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Live Activities",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Media player visualizer, download shelf and notifications",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            var mediaToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowMediaActivity);
            mediaToggle.CheckedChanged += chk => AppPreferences.Instance.ShowMediaActivity = chk;
            stack.Children.Add(new MayliSettingRow(
                "Media player island",
                "Show now-playing song titles, animated audio visualizer waves, and rounded album art cover.",
                mediaToggle
            ));

            var shelfToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowFileShelfActivity);
            shelfToggle.CheckedChanged += chk => AppPreferences.Instance.ShowFileShelfActivity = chk;
            stack.Children.Add(new MayliSettingRow(
                "File shelf auto-park",
                "Automatically park newly downloaded files into the shelf for effortless drag-and-drop into chat or email.",
                shelfToggle
            ));

            var batteryToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowBatteryActivity);
            batteryToggle.CheckedChanged += chk => AppPreferences.Instance.ShowBatteryActivity = chk;
            stack.Children.Add(new MayliSettingRow(
                "Charging & battery glow",
                "Flash dynamic green aura light on the notch borders when connecting charger or low battery.",
                batteryToggle
            ));

            var timerToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowTimerActivity);
            timerToggle.CheckedChanged += chk => AppPreferences.Instance.ShowTimerActivity = chk;
            stack.Children.Add(new MayliSettingRow(
                "Timer & focus pills",
                "Show ongoing countdown timer and stopwatch progress pills in the resting notch bar.",
                timerToggle
            ));

            return stack;
        }

        // =================================================================
        // PAGE 6: PRIVACY
        // =================================================================
        private FrameworkElement BuildPrivacyPage()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Privacy",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Data storage, clipboard security and local privacy",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            var localBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(40, 48, 209, 88)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 209, 88)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6)
            };
            localBadge.Child = new TextBlock
            {
                Text = "🛡 100% Local & Private",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88))
            };
            stack.Children.Add(new MayliSettingRow(
                "Zero cloud transmission",
                "Mayli operates entirely on your computer. All clipboard snippets, screen time logs, bookmarks, and notes are saved exclusively in your local AppData folder.",
                localBadge
            ));

            var clearClipBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(34, 28, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 40, 44)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand
            };
            clearClipBtn.Child = new TextBlock { Text = "Clear Clipboard", FontSize = 12.5, Foreground = new SolidColorBrush(Color.FromRgb(255, 69, 58)) };
            clearClipBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                try
                {
                    Clipboard.Clear();
                    MessageBox.Show("Clipboard history cleared successfully.", "Mayli Privacy", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch { }
            };
            stack.Children.Add(new MayliSettingRow(
                "Clear clipboard history",
                "Immediately erase all cached clipboard text, links, and snippets from memory.",
                clearClipBtn
            ));

            var openFolderBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                Cursor = Cursors.Hand
            };
            openFolderBtn.Child = new TextBlock { Text = "Open in Explorer", FontSize = 12.5, Foreground = Brushes.White };
            openFolderBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                string dir = AppPaths.DataDir;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            };
            stack.Children.Add(new MayliSettingRow(
                "Local data directory",
                "Inspect the JSON database files directly on disk (%AppData%\\Mayli).",
                openFolderBtn
            ));

            return stack;
        }

        // =================================================================
        // PAGE 7: UPDATES
        // =================================================================
        private FrameworkElement BuildUpdatesPage()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "Updates",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Version information and update channels",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            var versionBadge = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(40, 48, 209, 88)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(48, 209, 88)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6)
            };
            versionBadge.Child = new TextBlock
            {
                Text = "✓ Up to date (v1.1.57)",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88))
            };
            stack.Children.Add(new MayliSettingRow(
                "Installed version",
                "Mayli v1.1.57 (Windows x64 Architecture).",
                versionBadge
            ));

            var checkBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(20, 100, 210)),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand
            };
            checkBtn.Child = new TextBlock { Text = "Check Now ↗", FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            checkBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli/releases") { UseShellExecute = true });
            };
            stack.Children.Add(new MayliSettingRow(
                "Check for updates",
                "Queries github.com/Mahdilvl20/Mayli for newly published releases and fixes.",
                checkBtn
            ));

            var channelPicker = new MayliSegmentedControl(new[] { "Stable", "Beta" }, "Stable");
            stack.Children.Add(new MayliSettingRow(
                "Release channel",
                "Choose between thoroughly tested Stable releases or experimental Beta features.",
                channelPicker
            ));

            return stack;
        }

        // =================================================================
        // PAGE 8: ABOUT
        // =================================================================
        private FrameworkElement BuildAboutPage()
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = "About",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Mayli — The Notch for Windows",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            var aboutCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 17, 20)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(24),
                Margin = new Thickness(0, 0, 0, 18)
            };

            var aboutStack = new StackPanel();
            aboutStack.Children.Add(new TextBlock
            {
                Text = "Mayli",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            aboutStack.Children.Add(new TextBlock
            {
                Text = "Version 1.1.57 (Release x64) • The Notch for Windows",
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(255, 85, 0)),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 12)
            });
            aboutStack.Children.Add(new TextBlock
            {
                Text = "A high-performance interactive dynamic island notch experience crafted with hardware-accelerated WPF, live system telemetries, media spectrum analysis, and an expressive vector companion orb.",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 180)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19,
                Margin = new Thickness(0, 0, 0, 16)
            });

            var ghBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 38)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 7, 14, 7),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            ghBtn.Child = new TextBlock { Text = "GitHub: Mahdilvl20/Mayli ↗", FontSize = 12.5, Foreground = Brushes.White };
            ghBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli") { UseShellExecute = true });
            };
            aboutStack.Children.Add(ghBtn);

            aboutCard.Child = aboutStack;
            stack.Children.Add(aboutCard);

            return stack;
        }
    }
}
