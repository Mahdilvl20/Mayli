using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
using WindowsBuddy.Tools;
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
            try
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
            catch (Exception ex)
            {
                LogWriter.WriteNow("settings_error", ex.ToString());
            }
        }

        private readonly Dictionary<string, MayliSidebarItem> _navItems = new();
        private readonly StackPanel _navStack;
        private readonly ContentControl _mainContentArea;
        private readonly TextBox _searchBox;
        private readonly TextBlock _searchPlaceholder;
        private TextBlock _headerSubtitleBlock;
        private TextBlock _helpLink;
        private TextBlock _feedbackLink;
        private KeyEventHandler _activeShortcutRecorder;
        private string _activeCategory = "general";

        public SettingsWindow()
        {
            LocalizationService.ApplyToWindow(this);
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
            _headerSubtitleBlock = new TextBlock
            {
                Text = LocalizationService.T("nav.subtitle", "The Notch for Windows"),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(122, 122, 130)),
                Margin = new Thickness(0, 1, 0, 0)
            };
            titleStack.Children.Add(_headerSubtitleBlock);
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
                Text = LocalizationService.T("nav.search", "Search"),
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

            _helpLink = CreateFooterLink(LocalizationService.T("nav.help", "Help"), () => Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli#readme") { UseShellExecute = true }));
            footerLinks.Children.Add(_helpLink);
            footerLinks.Children.Add(new TextBlock { Text = "·", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 78)), Margin = new Thickness(2, 0, 2, 0) });
            _feedbackLink = CreateFooterLink(LocalizationService.T("nav.feedback", "Feedback"), () => Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli/issues") { UseShellExecute = true }));
            footerLinks.Children.Add(_feedbackLink);
            footerLinks.Children.Add(new TextBlock { Text = "·", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(70, 70, 78)), Margin = new Thickness(2, 0, 2, 0) });
            footerLinks.Children.Add(new TextBlock { Text = "v" + UpdateService.CurrentVersion, FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(104, 104, 114)), Margin = new Thickness(4, 0, 4, 0) });

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

            // Exactly the 8 categories from reference design (localized):
            AddNavItem("general", LocalizationService.T("nav.general", "General"), "⚙", Color.FromRgb(62, 56, 50));
            AddNavItem("notch", LocalizationService.T("nav.notch", "Notch"), "🖥", Color.FromRgb(44, 48, 56));
            AddNavItem("appearance", LocalizationService.T("nav.appearance", "Appearance"), "◐", Color.FromRgb(80, 60, 138));
            AddNavItem("modules", LocalizationService.T("nav.modules", "Modules"), "⊞", Color.FromRgb(33, 96, 196));
            AddSeparator();
            AddNavItem("live_activities", LocalizationService.T("nav.live_activities", "Live Activities"), "🔔", Color.FromRgb(200, 61, 45));
            AddNavItem("privacy", LocalizationService.T("nav.privacy", "Privacy"), "✋", Color.FromRgb(28, 134, 136));
            AddSeparator();
            AddNavItem("updates", LocalizationService.T("nav.updates", "Updates"), "🔄", Color.FromRgb(34, 138, 66));
            AddNavItem("about", LocalizationService.T("nav.about", "About"), "ℹ", Color.FromRgb(30, 124, 200));

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
            Panel.SetZIndex(topHeader, 999);

            var winButtons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 4, 8, 0)
            };

            topHeader.MouseLeftButtonDown += (s, e) =>
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    if (winButtons.IsAncestorOf(dep) || dep == winButtons)
                        return;
                }
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };

            Border CreateWinButton(string glyph, Action onClick, bool isClose = false)
            {
                var btn = new Border
                {
                    Width = 34,
                    Height = 28,
                    CornerRadius = new CornerRadius(6),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Focusable = false
                };
                var tb = new TextBlock
                {
                    Text = glyph,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 210)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false
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

                btn.PreviewMouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                };
                btn.MouseLeftButtonDown += (_, e) =>
                {
                    e.Handled = true;
                };
                btn.PreviewMouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    onClick?.Invoke();
                };
                return btn;
            }

            winButtons.Children.Add(CreateWinButton("—", () => WindowState = WindowState.Minimized));
            winButtons.Children.Add(CreateWinButton("✕", () => Close(), isClose: true));
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

            mainGrid.Children.Add(contentScroll);
            mainGrid.Children.Add(topHeader);

            Grid.SetColumn(mainGrid, 1);
            rootGrid.Children.Add(mainGrid);

            outerBorder.Child = rootGrid;
            Content = outerBorder;

            // Global shortcut Ctrl+K to focus search box, Escape to close
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                    return;
                }
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

        private void UpdateSidebarLocalization()
        {
            if (_navItems.TryGetValue("general", out var gi)) gi.UpdateLabel(LocalizationService.T("nav.general"));
            if (_navItems.TryGetValue("notch", out var ni)) ni.UpdateLabel(LocalizationService.T("nav.notch"));
            if (_navItems.TryGetValue("appearance", out var ai)) ai.UpdateLabel(LocalizationService.T("nav.appearance"));
            if (_navItems.TryGetValue("modules", out var mi)) mi.UpdateLabel(LocalizationService.T("nav.modules"));
            if (_navItems.TryGetValue("live_activities", out var li)) li.UpdateLabel(LocalizationService.T("nav.live_activities"));
            if (_navItems.TryGetValue("privacy", out var pi)) pi.UpdateLabel(LocalizationService.T("nav.privacy"));
            if (_navItems.TryGetValue("updates", out var ui)) ui.UpdateLabel(LocalizationService.T("nav.updates"));
            if (_navItems.TryGetValue("about", out var abi)) abi.UpdateLabel(LocalizationService.T("nav.about"));

            if (_searchPlaceholder != null)
                _searchPlaceholder.Text = LocalizationService.T("nav.search");
            if (_headerSubtitleBlock != null)
                _headerSubtitleBlock.Text = LocalizationService.T("nav.subtitle");
            if (_helpLink != null)
                _helpLink.Text = LocalizationService.T("nav.help");
            if (_feedbackLink != null)
                _feedbackLink.Text = LocalizationService.T("nav.feedback");
        }

        // =================================================================
        // PAGE 1: GENERAL (Real settings & English/Persian Vazirmatn)
        // =================================================================
        private FrameworkElement BuildGeneralPage()
        {
            // Clean up any pending shortcut key listener
            if (_activeShortcutRecorder != null)
            {
                PreviewKeyDown -= _activeShortcutRecorder;
                _activeShortcutRecorder = null;
            }

            var stack = new StackPanel();

            // Page Header
            stack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("general.title", "General"),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("general.subtitle", "Startup, shortcut, time and units"),
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
                LocalizationService.T("general.startup_title", "Start Mayli when Windows starts"),
                LocalizationService.T("general.startup_desc", "Starts silently each time you sign in. Quit applies only to this session. To have it back seconds after a restart, turn on \"Restart apps\" in Windows Settings > Accounts > Sign-in options."),
                startupToggle
            ));

            // Row 2: Open the notch (Real interactive recorder + preset cycling)
            var shortcutRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var shortcutBadge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
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
                Text = LocalizationService.T("general.change", "Change"),
                FontSize = 12.5,
                Foreground = Brushes.White
            };
            changeBtn.Child = changeTxt;
            changeBtn.MouseEnter += (_, _) => changeBtn.Background = new SolidColorBrush(Color.FromRgb(44, 44, 52));
            changeBtn.MouseLeave += (_, _) => changeBtn.Background = new SolidColorBrush(Color.FromRgb(32, 32, 38));

            string[] presets = { "Ctrl + Alt + N", "Win + Ctrl + N", "Alt + Space", "Ctrl + Shift + Space" };

            void ApplyShortcut(string sc, int mod, int vk)
            {
                AppPreferences.Instance.OpenNotchShortcut = sc;
                shortcutText.Text = sc;
                AppPreferences.Instance.Raw.HotkeyModifiers = mod;
                AppPreferences.Instance.Raw.HotkeyKey = vk;
                AppPreferences.Instance.Save();
                HotkeyService.Instance.Apply(mod, vk);
            }

            // Clicking badge cycles presets
            shortcutBadge.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                int idx = Array.IndexOf(presets, AppPreferences.Instance.OpenNotchShortcut);
                string next = presets[(idx + 1) % presets.Length];
                int mod = 3; int vk = 0x4E;
                if (next == "Win + Ctrl + N") { mod = 10; vk = 0x4E; }
                else if (next == "Alt + Space") { mod = 1; vk = 0x20; }
                else if (next == "Ctrl + Shift + Space") { mod = 6; vk = 0x20; }
                ApplyShortcut(next, mod, vk);
            };

            // Clicking "Change" activates keyboard shortcut recorder
            bool isRecording = false;
            changeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                if (isRecording)
                {
                    isRecording = false;
                    shortcutText.Text = AppPreferences.Instance.OpenNotchShortcut;
                    shortcutBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48));
                    changeTxt.Text = LocalizationService.T("general.change", "Change");
                    if (_activeShortcutRecorder != null)
                    {
                        PreviewKeyDown -= _activeShortcutRecorder;
                        _activeShortcutRecorder = null;
                    }
                    return;
                }

                isRecording = true;
                shortcutText.Text = LocalizationService.T("general.press_keys", "Press keys...");
                shortcutBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 0));
                changeTxt.Text = "✕";

                KeyEventHandler handler = null;
                handler = (s, ke) =>
                {
                    if (ke.Key == Key.Escape)
                    {
                        ke.Handled = true;
                        isRecording = false;
                        shortcutText.Text = AppPreferences.Instance.OpenNotchShortcut;
                        shortcutBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48));
                        changeTxt.Text = LocalizationService.T("general.change", "Change");
                        PreviewKeyDown -= handler;
                        _activeShortcutRecorder = null;
                        return;
                    }

                    var key = ke.Key == Key.System ? ke.SystemKey : ke.Key;
                    if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                        key == Key.LeftAlt || key == Key.RightAlt ||
                        key == Key.LeftShift || key == Key.RightShift ||
                        key == Key.LWin || key == Key.RWin)
                    {
                        return;
                    }

                    ke.Handled = true;
                    var mods = Keyboard.Modifiers;
                    int modFlags = 0;
                    var parts = new List<string>();
                    if ((mods & ModifierKeys.Control) != 0) { modFlags |= 2; parts.Add("Ctrl"); }
                    if ((mods & ModifierKeys.Alt) != 0) { modFlags |= 1; parts.Add("Alt"); }
                    if ((mods & ModifierKeys.Shift) != 0) { modFlags |= 4; parts.Add("Shift"); }
                    if ((mods & ModifierKeys.Windows) != 0) { modFlags |= 8; parts.Add("Win"); }

                    if (modFlags == 0)
                    {
                        modFlags = 3;
                        parts.Add("Ctrl");
                        parts.Add("Alt");
                    }

                    string keyStr = key.ToString();
                    parts.Add(keyStr);
                    string fullSc = string.Join(" + ", parts);
                    int vk = KeyInterop.VirtualKeyFromKey(key);

                    ApplyShortcut(fullSc, modFlags, vk);

                    isRecording = false;
                    shortcutBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(40, 40, 48));
                    changeTxt.Text = LocalizationService.T("general.change", "Change");
                    PreviewKeyDown -= handler;
                    _activeShortcutRecorder = null;
                };

                _activeShortcutRecorder = handler;
                PreviewKeyDown += handler;
            };

            shortcutRow.Children.Add(changeBtn);

            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("general.shortcut_title", "Open the notch"),
                LocalizationService.T("general.shortcut_desc", "Opens or closes the notch from anywhere"),
                shortcutRow
            ));

            // Row 3: Time format
            var timeOptions = AppPreferences.Instance.IsPersian
                ? new[] { LocalizationService.T("general.time.system"), LocalizationService.T("general.time.12h"), LocalizationService.T("general.time.24h") }
                : new[] { "System", "12-hour", "24-hour" };

            string currentTimeVal = AppPreferences.Instance.TimeFormat switch
            {
                "24-hour" => AppPreferences.Instance.IsPersian ? LocalizationService.T("general.time.24h") : "24-hour",
                "12-hour" => AppPreferences.Instance.IsPersian ? LocalizationService.T("general.time.12h") : "12-hour",
                _ => AppPreferences.Instance.IsPersian ? LocalizationService.T("general.time.system") : "System"
            };

            var timeFormatPicker = new MayliSegmentedControl(timeOptions, currentTimeVal);
            timeFormatPicker.SelectionChanged += val =>
            {
                string normalized = "System";
                if (val == "24-hour" || val == LocalizationService.T("general.time.24h"))
                    normalized = "24-hour";
                else if (val == "12-hour" || val == LocalizationService.T("general.time.12h"))
                    normalized = "12-hour";

                AppPreferences.Instance.TimeFormat = normalized;
                ClockService.Instance.Settings.Format = normalized switch
                {
                    "24-hour" => ClockFormat.TwentyFourHour,
                    "12-hour" => ClockFormat.TwelveHour,
                    _ => ClockFormat.System
                };
                ClockService.Instance.Save();
                ClockService.Instance.NotifySettingsChanged();
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("general.time_title", "Time format"),
                LocalizationService.T("general.time_desc", "Used by the notch, Home and Clock"),
                timeFormatPicker
            ));

            // Row 4: Temperature
            string currentTemp = WeatherService.Instance.UseFahrenheit ? "°F" : "°C";
            var tempPicker = new MayliSegmentedControl(new[] { "°C", "°F" }, currentTemp);
            tempPicker.SelectionChanged += val =>
            {
                WeatherService.Instance.UseFahrenheit = (val == "°F");
                AppPreferences.Instance.TemperatureUnit = val;
                AppPreferences.Instance.Save();
                WeatherService.Instance.NotifySettingsChanged();
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("general.temp_title", "Temperature"),
                LocalizationService.T("general.temp_desc", "Weather, Home and the notch"),
                tempPicker
            ));

            // Row 5: Wind speed
            var windPicker = new MayliSegmentedControl(new[] { "km/h", "mph" }, AppPreferences.Instance.WindSpeedUnit);
            windPicker.SelectionChanged += val =>
            {
                AppPreferences.Instance.WindSpeedUnit = val;
                AppPreferences.Instance.Save();
                WeatherService.Instance.NotifySettingsChanged();
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("general.wind_title", "Wind speed"),
                LocalizationService.T("general.wind_desc", "Used by Weather and Home"),
                windPicker
            ));

            // Row 6: Language (Strictly English and فارسی with Vazirmatn font)
            string currentLang = AppPreferences.Instance.IsPersian ? "فارسی" : "English";
            var langPicker = new MayliSegmentedControl(new[] { "English", "فارسی" }, currentLang);
            langPicker.SelectionChanged += val =>
            {
                string targetLang = (val == "فارسی") ? "fa" : "en";
                if (AppPreferences.Instance.Language != targetLang)
                {
                    LocalizationService.SetLanguage(targetLang);
                    LocalizationService.ApplyToWindow(this);
                    UpdateSidebarLocalization();
                    _mainContentArea.Content = BuildGeneralPage();
                }
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("general.lang_title", "Language"),
                LocalizationService.T("general.lang_desc", "Choose display language (English or Persian)"),
                langPicker
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
                Text = LocalizationService.T("notch.title", "Notch"),
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("notch.subtitle", "Shape, behavior and live preview"),
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 20)
            });

            // 2. Interactive Live Preview Card (Resting / Hover switch)
            var previewBox = new Border
            {
                Height = 148,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 44)),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 22),
                ClipToBounds = true
            };

            var previewGrid = new Grid
            {
                FlowDirection = FlowDirection.LeftToRight
            };

            // Simulated monitor top bezel line
            var monitorTopLine = new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(55, 55, 65)),
                VerticalAlignment = VerticalAlignment.Top
            };
            previewGrid.Children.Add(monitorTopLine);

            // Notch preview element container
            var notchContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };

            bool currentPreviewHover = false;

            void UpdateLivePreview(bool isHover)
            {
                currentPreviewHover = isHover;
                notchContainer.Children.Clear();
                var prefs = AppPreferences.Instance;

                string style = prefs.NotchStyle ?? "Notch";
                bool isIsland = string.Equals(style, "Island", StringComparison.OrdinalIgnoreCase);
                bool isLine = string.Equals(style, "Line", StringComparison.OrdinalIgnoreCase);

                if (isHover)
                {
                    double expW = 290;
                    double expH = 74;
                    string geoStyle = isIsland ? "Island" : (isLine ? "Line" : (prefs.NotchShape ?? "Shouldered"));
                    var geo = WindowsBuddy.BuddyCapsule.Renderer.NotchGeometryBuilder.BuildSilhouetteCore(geoStyle, expW, expH, 18, false);

                    var host = new Grid
                    {
                        Width = expW,
                        Height = expH + (isIsland ? 6 : 0)
                    };

                    host.Children.Add(new WpfPath
                    {
                        Data = geo,
                        Fill = Brushes.Black,
                        Stroke = new SolidColorBrush(Color.FromRgb(42, 42, 50)),
                        StrokeThickness = 1
                    });

                    if (prefs.EnableEdgeLight)
                    {
                        var previewEdge = new WindowsBuddy.BuddyCapsule.UI.EdgeLightWpf();
                        previewEdge.Configure(prefs.EdgeLightPattern, prefs.EdgeLightSpeed, "Always", style);
                        try
                        {
                            Color edgeCol = (prefs.EdgeLightSyncWithMusic && RealtimeNotchActionService.Instance.IsMusicPlaying)
                                ? RealtimeNotchActionService.Instance.CurrentBeatColor
                                : (Color)ColorConverter.ConvertFromString(prefs.EdgeLightColor);
                            previewEdge.SetLightColor(edgeCol);
                        }
                        catch { }
                        previewEdge.Opacity = Math.Clamp(prefs.EdgeLightIntensity / 100.0, 0.15, 1.0);
                        previewEdge.UpdateGeometry(geo, true, expW, expH);
                        host.Children.Add(previewEdge);
                    }

                    var expStack = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, isIsland ? 6 : 2, 0, 0)
                    };

                    var topHeaderMini = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    if (prefs.EnablePetBeta)
                    {
                        topHeaderMini.Children.Add(new CuteOrbPetView(OrbMood.Happy, 16)
                        {
                            Margin = new Thickness(0, 0, 6, 0)
                        });
                    }
                    topHeaderMini.Children.Add(new TextBlock
                    {
                        Text = "Mayli · Home",
                        FontSize = 10.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(210, 210, 220)),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                    expStack.Children.Add(topHeaderMini);

                    var dockMini = new Border
                    {
                        CornerRadius = new CornerRadius(11),
                        Background = new SolidColorBrush(Color.FromRgb(22, 22, 26)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(42, 42, 50)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(8, 3, 8, 3)
                    };
                    var iconsRow = new StackPanel { Orientation = Orientation.Horizontal };
                    string[] miniIcons = { "⌂", "⚙", "📋", "⏱", "⛅", "📊" };
                    for (int i = 0; i < miniIcons.Length; i++)
                    {
                        iconsRow.Children.Add(new TextBlock
                        {
                            Text = miniIcons[i],
                            FontSize = 12,
                            Foreground = i == 0 ? Brushes.White : new SolidColorBrush(Color.FromRgb(150, 150, 162)),
                            Margin = new Thickness(5, 0, 5, 0)
                        });
                    }
                    dockMini.Child = iconsRow;
                    expStack.Children.Add(dockMini);

                    host.Children.Add(expStack);
                    notchContainer.Children.Add(host);
                }
                else
                {
                    if (string.Equals(prefs.NotchVisibility, "Minimal", StringComparison.OrdinalIgnoreCase))
                    {
                        var minBorder = new Border
                        {
                            Width = 64,
                            Height = 14,
                            CornerRadius = new CornerRadius(0, 0, 6, 6),
                            Background = Brushes.Black,
                            BorderBrush = new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                            BorderThickness = new Thickness(1, 0, 1, 1),
                            Child = new TextBlock
                            {
                                Text = "⌄",
                                FontSize = 10,
                                Foreground = Brushes.White,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        };
                        notchContainer.Children.Add(minBorder);
                        return;
                    }

                    WindowsBuddy.BuddyCapsule.Renderer.NotchGeometryBuilder.GetRestingShapeProfile(
                        style,
                        prefs.NotchShape,
                        prefs.NotchScalePercent,
                        out double restW,
                        out double restH,
                        out double restR,
                        out double earW);

                    // Fit comfortably inside the preview stage
                    double previewScale = 0.85;
                    double w = Math.Round(restW * previewScale);
                    double h = Math.Round(restH * previewScale);
                    string geoStyle = isIsland ? "Island" : (isLine ? "Line" : (prefs.NotchShape ?? "Shouldered"));
                    var geo = WindowsBuddy.BuddyCapsule.Renderer.NotchGeometryBuilder.BuildSilhouetteCore(geoStyle, w, h, restR * previewScale, false);

                    var host = new Grid
                    {
                        Width = w,
                        Height = h + (isIsland ? 6 : 0)
                    };

                    host.Children.Add(new WpfPath
                    {
                        Data = geo,
                        Fill = Brushes.Black,
                        Stroke = new SolidColorBrush(Color.FromRgb(42, 42, 50)),
                        StrokeThickness = 1
                    });

                    if (prefs.EnableEdgeLight)
                    {
                        var previewEdge = new WindowsBuddy.BuddyCapsule.UI.EdgeLightWpf();
                        previewEdge.Configure(prefs.EdgeLightPattern, prefs.EdgeLightSpeed, "Always", style);
                        try
                        {
                            Color edgeCol = (prefs.EdgeLightSyncWithMusic && RealtimeNotchActionService.Instance.IsMusicPlaying)
                                ? RealtimeNotchActionService.Instance.CurrentBeatColor
                                : (Color)ColorConverter.ConvertFromString(prefs.EdgeLightColor);
                            previewEdge.SetLightColor(edgeCol);
                        }
                        catch { }
                        previewEdge.Opacity = Math.Clamp(prefs.EdgeLightIntensity / 100.0, 0.15, 1.0);
                        previewEdge.UpdateGeometry(geo, false, w, h);
                        host.Children.Add(previewEdge);
                    }

                    bool shouldShowBattery = prefs.ShowClosedBattery && DeviceService.ShouldShowBatteryIndicators;
                    bool hasRightContent = prefs.ShowClosedBluetooth || shouldShowBattery;
                    bool isCenter = string.Equals(prefs.NotchRestingLayout, "Center", StringComparison.OrdinalIgnoreCase) || !hasRightContent;
                    var restGrid = new Grid
                    {
                        Margin = new Thickness(earW * previewScale + 10, isIsland ? 5 : 0, earW * previewScale + 10, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = isCenter ? HorizontalAlignment.Center : HorizontalAlignment.Stretch
                    };
                    restGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    restGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = isCenter ? GridLength.Auto : new GridLength(1, GridUnitType.Star) });
                    restGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var leftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    if (prefs.ShowClosedDate)
                    {
                        leftSp.Children.Add(new TextBlock
                        {
                            Text = prefs.ShowClosedTime ? $"{DateTime.Now.ToString("ddd d", System.Globalization.CultureInfo.InvariantCulture)} · " : DateTime.Now.ToString("ddd d", System.Globalization.CultureInfo.InvariantCulture),
                            FontSize = 10.0,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(170, 170, 180)),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    if (prefs.ShowClosedTime)
                    {
                        if (string.Equals(prefs.ClosedClockStyle, "Analog", StringComparison.OrdinalIgnoreCase))
                        {
                            leftSp.Children.Add(new WindowsBuddy.BuddyCapsule.UI.MiniAnalogClock(13.5));
                        }
                        else
                        {
                            leftSp.Children.Add(new TextBlock
                            {
                                Text = DateTime.Now.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture),
                                FontSize = 10.5,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = Brushes.White,
                                VerticalAlignment = VerticalAlignment.Center
                            });
                        }
                    }
                    Grid.SetColumn(leftSp, 0);
                    restGrid.Children.Add(leftSp);

                    if (prefs.EnablePetBeta)
                    {
                        var miniPet = new CuteOrbPetView(OrbMood.Idle, 16)
                        {
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = isCenter ? new Thickness(6, 0, 6, 0) : new Thickness(0)
                        };
                        Grid.SetColumn(miniPet, 1);
                        restGrid.Children.Add(miniPet);
                    }

                    var rightSp = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = hasRightContent ? Visibility.Visible : Visibility.Collapsed,
                        Margin = (isCenter && hasRightContent && !prefs.EnablePetBeta) ? new Thickness(8, 0, 0, 0) : new Thickness(0)
                    };
                    if (prefs.ShowClosedBluetooth)
                    {
                        rightSp.Children.Add(new WindowsBuddy.UI.AnimateIcons.AnimateIcon(WindowsBuddy.UI.AnimateIcons.AnimateIconType.Bluetooth, 11, new SolidColorBrush(Color.FromRgb(10, 132, 255)))
                        {
                            Margin = new Thickness(0, 0, 4, 0),
                            VerticalAlignment = VerticalAlignment.Center
                        });
                    }
                    if (shouldShowBattery)
                    {
                        rightSp.Children.Add(new TextBlock { Text = "⚡", FontSize = 9.5, Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88)), Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center });
                        rightSp.Children.Add(new TextBlock { Text = $"{SystemMonitorService.Current.BatteryPercent}", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88)), VerticalAlignment = VerticalAlignment.Center });
                    }
                    Grid.SetColumn(rightSp, 2);
                    restGrid.Children.Add(rightSp);

                    host.Children.Add(restGrid);
                    notchContainer.Children.Add(host);
                }
            }

            UpdateLivePreview(false);
            previewGrid.Children.Add(notchContainer);

            // Preview switcher on top right
            string restLabel = LocalizationService.T("notch.preview.resting", "Resting");
            string hoverLabel = LocalizationService.T("notch.preview.hover", "Hover");
            var previewModeSwitch = new MayliSegmentedControl(new[] { restLabel, hoverLabel }, restLabel)
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 12, 12, 0)
            };
            previewModeSwitch.SelectionChanged += mode => UpdateLivePreview(mode == hoverLabel || mode == "Hover");
            previewGrid.Children.Add(previewModeSwitch);

            previewBox.Child = previewGrid;
            stack.Children.Add(previewBox);

            // -------------------------------------------------------------
            // SECTION: VISIBILITY (4 Visual Cards)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.visibility", "VISIBILITY")));

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
                    UpdateLivePreview(currentPreviewHover);
                };
                visCards.Add(card);
                Grid.SetColumn(card, col);
                Grid.SetRow(card, row);
                visGrid.Children.Add(card);
            }

            AddVisCard("Always", LocalizationService.T("notch.vis.always.title", "Always"), LocalizationService.T("notch.vis.always.desc", "Always on screen. Fullscreen rules below still apply."), CreateNotchIllustration("always"), 0, 0);
            AddVisCard("Smart", LocalizationService.T("notch.vis.smart.title", "Smart"), LocalizationService.T("notch.vis.smart.desc", "Full on the desktop; tucked away over maximized apps."), CreateNotchIllustration("smart"), 1, 0);
            AddVisCard("Minimal", LocalizationService.T("notch.vis.minimal.title", "Minimal"), LocalizationService.T("notch.vis.minimal.desc", "Just a small arrow at the top. Hover to peek, open on hover or click."), CreateNotchIllustration("minimal"), 0, 1);
            AddVisCard("Hidden", LocalizationService.T("notch.vis.hidden.title", "Hidden"), LocalizationService.T("notch.vis.hidden.desc", "Nothing on screen. Open it from the tray icon (or set a shortcut in General)."), CreateNotchIllustration("hidden"), 1, 1);

            stack.Children.Add(visGrid);

            // -------------------------------------------------------------
            // SECTION: STYLE (3 Visual Cards & Geometry Options)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.style", "STYLE")));

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
                    AppPreferences.Instance.NotchSilhouette = selectedKey == "Island" ? "Island" : (selectedKey == "Line" ? "Line" : "Shouldered");
                    foreach (var c in styleCards) c.IsSelected = (c.Key == selectedKey);
                    UpdateLivePreview(currentPreviewHover);
                };
                styleCards.Add(card);
                Grid.SetColumn(card, col);
                styleGrid.Children.Add(card);
            }

            AddStyleCard("Line", LocalizationService.T("notch.style.line.title", "Line"), LocalizationService.T("notch.style.line.desc", "A slim bar along the top edge"), CreateNotchIllustration("line"), 0);
            AddStyleCard("Notch", LocalizationService.T("notch.style.notch.title", "Notch"), LocalizationService.T("notch.style.notch.desc", "Grows out of the screen edge"), CreateNotchIllustration("notch"), 1);
            AddStyleCard("Island", LocalizationService.T("notch.style.island.title", "Island"), LocalizationService.T("notch.style.island.desc", "A pill floating just below the edge"), CreateNotchIllustration("island"), 2);

            stack.Children.Add(styleGrid);

            // Notch shape
            var shapeDropdown = new MayliDropdown(new[] { "Compact", "Wide", "Classic", "Ultra-Compact" }, AppPreferences.Instance.NotchShape);
            shapeDropdown.SelectionChanged += s =>
            {
                AppPreferences.Instance.NotchShape = s;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.shape.title", "Notch shape"),
                LocalizationService.T("notch.shape.desc", "Fine-tune the Notch style's silhouette"),
                shapeDropdown
            ));

            // Size
            var sizeSlider = new MayliSliderRow(60, 140, AppPreferences.Instance.NotchScalePercent, "%");
            sizeSlider.ValueChanged += v =>
            {
                AppPreferences.Instance.NotchScalePercent = (int)v;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.size.title", "Size"),
                LocalizationService.T("notch.size.desc", "Scales the resting notch"),
                sizeSlider
            ));

            // Resting layout
            string cornersLbl = LocalizationService.T("notch.layout.corners", "Corners");
            string centerLbl = LocalizationService.T("notch.layout.center", "Center");
            string curLayoutLbl = string.Equals(AppPreferences.Instance.NotchRestingLayout, "Center", StringComparison.OrdinalIgnoreCase) ? centerLbl : cornersLbl;
            var layoutPicker = new MayliSegmentedControl(new[] { cornersLbl, centerLbl }, curLayoutLbl);
            layoutPicker.SelectionChanged += l =>
            {
                AppPreferences.Instance.NotchRestingLayout = (l == centerLbl || l == "Center") ? "Center" : "Corners";
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.layout.title", "Resting layout"),
                LocalizationService.T("notch.layout.desc", "Time and battery in corners or center"),
                layoutPicker
            ));

            // Opens on
            string hoverOpenLbl = LocalizationService.T("notch.opens.hover", "Hover");
            string clickOpenLbl = LocalizationService.T("notch.opens.click", "Click");
            string curOpenLbl = string.Equals(AppPreferences.Instance.NotchOpenTrigger, "Click", StringComparison.OrdinalIgnoreCase) ? clickOpenLbl : hoverOpenLbl;
            var openOnPicker = new MayliSegmentedControl(new[] { hoverOpenLbl, clickOpenLbl }, curOpenLbl);
            openOnPicker.SelectionChanged += o =>
            {
                AppPreferences.Instance.NotchOpenTrigger = (o == clickOpenLbl || o == "Click") ? "Click" : "Hover";
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.opens.title", "Opens on"),
                LocalizationService.T("notch.opens.desc", "Hover to open at once, or click to open on purpose"),
                openOnPicker
            ));

            // Display
            var dispDropdown = new MayliDropdown(new[] { "Primary display", "Display 1", "Display 2", "All displays" }, AppPreferences.Instance.NotchDisplay);
            dispDropdown.SelectionChanged += d => AppPreferences.Instance.NotchDisplay = d;
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.display.title", "Display"),
                LocalizationService.T("notch.display.desc", "Which monitor the notch lives on"),
                dispDropdown
            ));

            // Show notch
            var showNotchToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowNotchBar);
            showNotchToggle.CheckedChanged += s => AppPreferences.Instance.ShowNotchBar = s;
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.shownotch.title", "Show notch"),
                LocalizationService.T("notch.shownotch.desc", "Hide it without quitting; the tray icon brings it back"),
                showNotchToggle
            ));

            // -------------------------------------------------------------
            // SECTION: BEHAVIOR
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.behavior", "BEHAVIOR")));

            // Hide in fullscreen
            var fullToggle = new MayliToggleSwitch(AppPreferences.Instance.HideOnFullscreen);
            fullToggle.CheckedChanged += chk => AppPreferences.Instance.HideOnFullscreen = chk;
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.fullscreen.title", "Hide in fullscreen"),
                LocalizationService.T("notch.fullscreen.desc", "Apps, videos and games on the notch's display"),
                fullToggle
            ));

            // Show events over apps
            var eventsToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowEventsOverApps);
            eventsToggle.CheckedChanged += e => AppPreferences.Instance.ShowEventsOverApps = e;
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.events.title", "Show events over apps"),
                LocalizationService.T("notch.events.desc", "When the notch is tucked away, grow it briefly for events like volume, timers and a new song"),
                eventsToggle
            ));

            // Tab animations
            var animToggle = new MayliToggleSwitch(AppPreferences.Instance.TabAnimations);
            animToggle.CheckedChanged += a => AppPreferences.Instance.TabAnimations = a;
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.tabanim.title", "Tab animations"),
                LocalizationService.T("notch.tabanim.desc", "Slide between tabs inside the open notch"),
                animToggle
            ));

            // -------------------------------------------------------------
            // SECTION: MAYLI PET (BETA)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.pet", "MAYLI PET (BETA)")));

            var petToggle = new MayliToggleSwitch(AppPreferences.Instance.EnablePetBeta);
            petToggle.CheckedChanged += p =>
            {
                AppPreferences.Instance.EnablePetBeta = p;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.pet.title", "Mayli Pet (Beta)"),
                LocalizationService.T("notch.pet.desc", "A small companion in your notch"),
                petToggle
            ));

            // -------------------------------------------------------------
            // SECTION: EDGE LIGHT
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.edgelight", "EDGE LIGHT")));

            var edgeToggle = new MayliToggleSwitch(AppPreferences.Instance.EnableEdgeLight);
            edgeToggle.CheckedChanged += el =>
            {
                AppPreferences.Instance.EnableEdgeLight = el;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.edgelight.title", "Edge light"),
                LocalizationService.T("notch.edgelight.desc", "Light that runs along the notch outline"),
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
                    UpdateLivePreview(currentPreviewHover);
                };
                edgeCards.Add(card);
                Grid.SetColumn(card, col);
                edgeCardsGrid.Children.Add(card);
            }

            AddEdgeCard("Comet", LocalizationService.T("notch.edge.comet.title", "Comet"), LocalizationService.T("notch.edge.comet.desc", "One light travels along the outline"), CreateEdgeIllustration("comet"), 0);
            AddEdgeCard("Twin comets", LocalizationService.T("notch.edge.twin.title", "Twin comets"), LocalizationService.T("notch.edge.twin.desc", "Two lights meet at the bottom, then return"), CreateEdgeIllustration("twin"), 1);
            AddEdgeCard("Breathe", LocalizationService.T("notch.edge.breathe.title", "Breathe"), LocalizationService.T("notch.edge.breathe.desc", "The whole outline glows softly"), CreateEdgeIllustration("breathe"), 2);

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

            bool matchedPreset = false;
            var swatchBorders = new List<(string Hex, Border Circle)>();

            for (int i = 0; i < lightColors.Length; i++)
            {
                string hex = lightColors[i];
                bool isSel = string.Equals(AppPreferences.Instance.EdgeLightColor, hex, StringComparison.OrdinalIgnoreCase);
                if (isSel) matchedPreset = true;

                var circle = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Margin = new Thickness(4),
                    Cursor = Cursors.Hand,
                    BorderThickness = new Thickness(isSel ? 2.5 : 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 0))
                };
                swatchBorders.Add((hex, circle));
                circle.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    AppPreferences.Instance.EdgeLightColor = hex;
                    foreach (var sb in swatchBorders)
                        sb.Circle.BorderThickness = new Thickness(string.Equals(sb.Hex, hex, StringComparison.OrdinalIgnoreCase) ? 2.5 : 0);
                    UpdateLivePreview(currentPreviewHover);
                };
                if (i < 8) swatchesRow1.Children.Add(circle);
                else swatchesRow2.Children.Add(circle);
            }

            var customBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 36)),
                BorderBrush = !matchedPreset ? new SolidColorBrush(Color.FromRgb(255, 85, 0)) : new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                BorderThickness = new Thickness(!matchedPreset ? 1.5 : 1),
                Padding = new Thickness(12, 4, 12, 4),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            customBtn.Child = new TextBlock
            {
                Text = LocalizationService.T("notch.edge.custom", "● Custom..."),
                FontSize = 11.5,
                Foreground = Brushes.White
            };
            customBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                using var dlg = new System.Windows.Forms.ColorDialog
                {
                    FullOpen = true,
                    AnyColor = true
                };
                try
                {
                    var cur = (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.EdgeLightColor);
                    dlg.Color = System.Drawing.Color.FromArgb(cur.R, cur.G, cur.B);
                }
                catch { }

                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string chosenHex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    AppPreferences.Instance.EdgeLightColor = chosenHex;
                    foreach (var sb in swatchBorders)
                        sb.Circle.BorderThickness = new Thickness(0);
                    customBtn.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 85, 0));
                    customBtn.BorderThickness = new Thickness(1.5);
                    UpdateLivePreview(currentPreviewHover);
                }
            };
            swatchesRow2.Children.Add(customBtn);

            // "Match song when playing" toggle pill button in Edge Light -> Color
            var artGrad = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            artGrad.GradientStops.Add(new GradientStop(Color.FromRgb(255, 79, 121), 0.0));
            artGrad.GradientStops.Add(new GradientStop(Color.FromRgb(191, 90, 242), 0.35));
            artGrad.GradientStops.Add(new GradientStop(Color.FromRgb(10, 132, 255), 0.7));
            artGrad.GradientStops.Add(new GradientStop(Color.FromRgb(48, 209, 88), 1.0));

            var syncMusicBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            var syncMusicSp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            syncMusicSp.Children.Add(new Border
            {
                Width = 11,
                Height = 11,
                CornerRadius = new CornerRadius(5.5),
                Background = artGrad,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            var syncMusicLabel = new TextBlock
            {
                FontSize = 11.5,
                VerticalAlignment = VerticalAlignment.Center
            };
            syncMusicSp.Children.Add(syncMusicLabel);
            syncMusicBtn.Child = syncMusicSp;

            void RefreshSyncMusicBtnVisual()
            {
                bool active = AppPreferences.Instance.EdgeLightSyncWithMusic;
                syncMusicBtn.Background = active
                    ? new SolidColorBrush(Color.FromArgb(38, 255, 85, 0))
                    : new SolidColorBrush(Color.FromRgb(30, 30, 36));
                syncMusicBtn.BorderBrush = active
                    ? new SolidColorBrush(Color.FromRgb(255, 85, 0))
                    : new SolidColorBrush(Color.FromRgb(46, 46, 54));
                syncMusicBtn.BorderThickness = new Thickness(active ? 1.5 : 1);
                string baseText = LocalizationService.T("notch.edge.sync_music", "🎵 Match song when playing");
                syncMusicLabel.Text = active ? $"{baseText} ✓" : baseText;
                syncMusicLabel.Foreground = active ? Brushes.White : new SolidColorBrush(Color.FromRgb(210, 210, 220));
                syncMusicLabel.FontWeight = active ? FontWeights.SemiBold : FontWeights.Normal;
            }

            RefreshSyncMusicBtnVisual();
            syncMusicBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AppPreferences.Instance.EdgeLightSyncWithMusic = !AppPreferences.Instance.EdgeLightSyncWithMusic;
                RefreshSyncMusicBtnVisual();
                UpdateLivePreview(currentPreviewHover);
            };
            swatchesRow2.Children.Add(syncMusicBtn);

            colorStack.Children.Add(swatchesRow1);
            colorStack.Children.Add(swatchesRow2);

            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.edge.color", "Color"),
                "",
                colorStack
            ));

            // Intensity
            var intensitySlider = new MayliSliderRow(10, 100, AppPreferences.Instance.EdgeLightIntensity, "%");
            intensitySlider.ValueChanged += v =>
            {
                AppPreferences.Instance.EdgeLightIntensity = (int)v;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.edge.intensity", "Intensity"),
                "",
                intensitySlider
            ));

            // Speed
            string slowLbl = LocalizationService.T("notch.edge.speed.slow", "Slow");
            string normLbl = LocalizationService.T("notch.edge.speed.normal", "Normal");
            string fastLbl = LocalizationService.T("notch.edge.speed.fast", "Fast");
            string curSpeedLbl = AppPreferences.Instance.EdgeLightSpeed switch
            {
                "Slow" => slowLbl,
                "Fast" => fastLbl,
                _ => normLbl
            };
            var speedPicker = new MayliSegmentedControl(new[] { slowLbl, normLbl, fastLbl }, curSpeedLbl);
            speedPicker.SelectionChanged += s =>
            {
                string canonical = "Normal";
                if (s == slowLbl || s == "Slow") canonical = "Slow";
                else if (s == fastLbl || s == "Fast") canonical = "Fast";
                AppPreferences.Instance.EdgeLightSpeed = canonical;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.edge.speed", "Speed"),
                "",
                speedPicker
            ));

            // When
            string alwaysWhenLbl = LocalizationService.T("notch.edge.when.always", "Always");
            string eventsWhenLbl = LocalizationService.T("notch.edge.when.events", "On events & hover");
            string curWhenLbl = string.Equals(AppPreferences.Instance.EdgeLightTrigger, "Always", StringComparison.OrdinalIgnoreCase)
                ? alwaysWhenLbl
                : eventsWhenLbl;
            var whenPicker = new MayliSegmentedControl(new[] { alwaysWhenLbl, eventsWhenLbl }, curWhenLbl);
            whenPicker.SelectionChanged += w =>
            {
                AppPreferences.Instance.EdgeLightTrigger = (w == alwaysWhenLbl || w == "Always")
                    ? "Always"
                    : "On events & hover";
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.edge.when.title", "When"),
                LocalizationService.T("notch.edge.when.desc", "On events & hover plays the light for a few seconds, then it fades out"),
                whenPicker
            ));

            // -------------------------------------------------------------
            // SECTION: WHEN CLOSED, SHOW
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.closed", "WHEN CLOSED, SHOW")));

            // Time
            var closedTimeToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedTime);
            closedTimeToggle.CheckedChanged += t =>
            {
                AppPreferences.Instance.ShowClosedTime = t;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.closed.time.title", "Time"),
                LocalizationService.T("notch.closed.time.desc", "The resting clock that events return to"),
                closedTimeToggle
            ));

            // Clock style
            string digLbl = LocalizationService.T("notch.closed.digital", "Digital");
            string anaLbl = LocalizationService.T("notch.closed.analog", "Analog");
            string curClockStyleLbl = string.Equals(AppPreferences.Instance.ClosedClockStyle, "Analog", StringComparison.OrdinalIgnoreCase)
                ? anaLbl
                : digLbl;
            var clockStylePicker = new MayliSegmentedControl(new[] { digLbl, anaLbl }, curClockStyleLbl);
            clockStylePicker.SelectionChanged += cs =>
            {
                AppPreferences.Instance.ClosedClockStyle = (cs == anaLbl || cs == "Analog") ? "Analog" : "Digital";
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.closed.clockstyle", "Clock style"),
                "",
                clockStylePicker
            ));

            // Date
            var closedDateToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedDate);
            closedDateToggle.CheckedChanged += d =>
            {
                AppPreferences.Instance.ShowClosedDate = d;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.closed.date.title", "Date"),
                LocalizationService.T("notch.closed.date.desc", "Day and date next to the time, e.g. Sat 3 · 9:41"),
                closedDateToggle
            ));

            // Battery
            bool hasBattery = DeviceService.HasBattery;
            var closedBatteryToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedBattery && hasBattery)
            {
                IsEnabled = hasBattery
            };
            closedBatteryToggle.CheckedChanged += b =>
            {
                AppPreferences.Instance.ShowClosedBattery = b;
                UpdateLivePreview(currentPreviewHover);
            };
            string batDesc = hasBattery
                ? LocalizationService.T("notch.closed.battery.desc", "Level and charging")
                : LocalizationService.T("notch.closed.battery.desc_pc", "Not available on Desktop PC (No battery)");
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.closed.battery.title", "Battery"),
                batDesc,
                closedBatteryToggle
            ));

            // Now playing
            var closedMediaToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedNowPlaying);
            closedMediaToggle.CheckedChanged += np => AppPreferences.Instance.ShowClosedNowPlaying = np;
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.closed.nowplaying.title", "Now playing"),
                LocalizationService.T("notch.closed.nowplaying.desc", "Compact media controls while music or video plays"),
                closedMediaToggle
            ));

            // Bluetooth
            var closedBtToggle = new MayliToggleSwitch(AppPreferences.Instance.ShowClosedBluetooth);
            closedBtToggle.CheckedChanged += bt =>
            {
                AppPreferences.Instance.ShowClosedBluetooth = bt;
                UpdateLivePreview(currentPreviewHover);
            };
            stack.Children.Add(new MayliSettingRow(
                LocalizationService.T("notch.closed.bt.title", "Bluetooth"),
                LocalizationService.T("notch.closed.bt.desc", "While a device is connected"),
                closedBtToggle
            ));

            // -------------------------------------------------------------
            // SECTION: EXCLUDED AUDIO APPS (Ignore sound per .exe)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader(LocalizationService.T("notch.sec.excluded", "EXCLUDED AUDIO APPS")));

            var excludedPanel = new StackPanel { Margin = new Thickness(0, 4, 0, 16) };

            var headerRow = new DockPanel { Margin = new Thickness(0, 4, 0, 10) };
            var textStack = new StackPanel();
            textStack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("notch.excluded.title", "Ignore audio from specific apps"),
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            textStack.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("notch.excluded.desc", "When audio plays from selected apps, the notch will not show now playing or media activities."),
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(138, 140, 153)),
                Margin = new Thickness(0, 2, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            DockPanel.SetDock(textStack, Dock.Left);
            headerRow.Children.Add(textStack);

            // Add .exe button
            var addExeBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(36, 36, 44)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 72)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 6, 12, 6),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand
            };
            var addExeTxt = new TextBlock
            {
                Text = LocalizationService.T("notch.excluded.select", "➕ Select .exe..."),
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = Brushes.White
            };
            addExeBtn.Child = addExeTxt;
            addExeBtn.MouseEnter += (_, _) => addExeBtn.Background = new SolidColorBrush(Color.FromRgb(48, 48, 58));
            addExeBtn.MouseLeave += (_, _) => addExeBtn.Background = new SolidColorBrush(Color.FromRgb(36, 36, 44));
            addExeBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                var ofd = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Select executable to ignore",
                    Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
                    CheckFileExists = true
                };
                if (ofd.ShowDialog() == true)
                {
                    AppPreferences.Instance.AddExcludedAudioApp(ofd.FileName);
                    _mainContentArea.Content = BuildNotchPage();
                }
            };
            DockPanel.SetDock(addExeBtn, Dock.Right);
            headerRow.Children.Add(addExeBtn);
            excludedPanel.Children.Add(headerRow);

            // Quick suggestions row
            var quickRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            quickRow.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("notch.excluded.quick", "Quick add: "),
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 125)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            var quickSuggestions = new[] { "chrome.exe", "msedge.exe", "Telegram.exe", "Spotify.exe", "vlc.exe", "Discord.exe", "firefox.exe" };
            foreach (var app in quickSuggestions)
            {
                bool isAlreadyExcluded = AppPreferences.Instance.IsAudioAppExcluded(app);
                var pill = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = isAlreadyExcluded ? new SolidColorBrush(Color.FromArgb(40, 255, 69, 58)) : new SolidColorBrush(Color.FromRgb(26, 26, 32)),
                    BorderBrush = isAlreadyExcluded ? new SolidColorBrush(Color.FromRgb(255, 69, 58)) : new SolidColorBrush(Color.FromRgb(50, 50, 60)),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand,
                    Opacity = isAlreadyExcluded ? 0.6 : 1.0
                };
                var pillTxt = new TextBlock
                {
                    Text = (isAlreadyExcluded ? "✓ " : "+ ") + app,
                    FontSize = 11,
                    Foreground = isAlreadyExcluded ? Brushes.LightGray : new SolidColorBrush(Color.FromRgb(200, 202, 215))
                };
                pill.Child = pillTxt;
                if (!isAlreadyExcluded)
                {
                    pill.MouseEnter += (_, _) => pill.Background = new SolidColorBrush(Color.FromRgb(38, 38, 48));
                    pill.MouseLeave += (_, _) => pill.Background = new SolidColorBrush(Color.FromRgb(26, 26, 32));
                    pill.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        AppPreferences.Instance.AddExcludedAudioApp(app);
                        _mainContentArea.Content = BuildNotchPage();
                    };
                }
                quickRow.Children.Add(pill);
            }
            excludedPanel.Children.Add(quickRow);

            // Container for excluded chips
            var chipsBox = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromRgb(20, 20, 24)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(38, 38, 46)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10)
            };

            var chipsWrap = new WrapPanel();
            var excludedList = AppPreferences.Instance.ExcludedAudioApps;
            if (excludedList == null || excludedList.Count == 0)
            {
                chipsBox.Child = new TextBlock
                {
                    Text = LocalizationService.T("notch.excluded.empty", "No apps excluded. Audio and media from all players and browsers will show on the notch."),
                    FontSize = 11.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 125)),
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(2)
                };
            }
            else
            {
                foreach (var item in excludedList)
                {
                    string captured = item;
                    var chip = new Border
                    {
                        CornerRadius = new CornerRadius(14),
                        Background = new SolidColorBrush(Color.FromRgb(32, 32, 40)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(55, 55, 68)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(10, 4, 8, 4),
                        Margin = new Thickness(0, 0, 8, 8)
                    };

                    var chipDock = new StackPanel { Orientation = Orientation.Horizontal };
                    chipDock.Children.Add(new TextBlock
                    {
                        Text = "🚫 " + captured,
                        FontSize = 11.5,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 8, 0)
                    });

                    var removeBtn = new Border
                    {
                        Width = 18,
                        Height = 18,
                        CornerRadius = new CornerRadius(9),
                        Background = new SolidColorBrush(Color.FromRgb(48, 48, 58)),
                        Cursor = Cursors.Hand,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = "✕",
                            FontSize = 9.5,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(255, 100, 100)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    removeBtn.MouseEnter += (_, _) => removeBtn.Background = new SolidColorBrush(Color.FromRgb(255, 69, 58));
                    removeBtn.MouseLeave += (_, _) => removeBtn.Background = new SolidColorBrush(Color.FromRgb(48, 48, 58));
                    removeBtn.MouseLeftButtonUp += (_, e) =>
                    {
                        e.Handled = true;
                        AppPreferences.Instance.RemoveExcludedAudioApp(captured);
                        _mainContentArea.Content = BuildNotchPage();
                    };

                    chipDock.Children.Add(removeBtn);
                    chip.Child = chipDock;
                    chipsWrap.Children.Add(chip);
                }
                chipsBox.Child = chipsWrap;
            }

            excludedPanel.Children.Add(chipsBox);
            stack.Children.Add(excludedPanel);

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
                Text = LocalizationService.T("notch.restore", "Restore defaults"),
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
                MessageBox.Show(LocalizationService.T("notch.restore_msg", "Notch settings have been restored to factory defaults."), "Mayli Settings", MessageBoxButton.OK, MessageBoxImage.Information);
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
                // Single comet beam on right/bottom outline heading left
                var comet = new WpfPath
                {
                    Data = Geometry.Parse("M 84 10 Q 78 10 75 15 Q 72 28 60 28"),
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

        private static FrameworkElement CreateThemeIllustration(string themeId)
        {
            bool isSilver = themeId == "silver";
            bool isEmber = themeId == "ember-dark";
            bool isVivid = themeId == "vivid";

            // Colors based on theme
            Color panelBg = isSilver ? Color.FromRgb(240, 240, 244) : Color.FromRgb(12, 12, 14);
            Color panelBorder = isSilver ? Color.FromRgb(210, 210, 218) : Color.FromRgb(28, 28, 34);
            Color miniCardBg = isSilver ? Color.FromRgb(255, 255, 255) : Color.FromRgb(19, 19, 23);
            Color miniCardBorder = isSilver ? Color.FromRgb(225, 225, 232) : Color.FromRgb(32, 32, 38);

            Color line1 = isSilver ? Color.FromRgb(160, 160, 170) : Color.FromRgb(90, 90, 100);
            Color line2 = isSilver ? Color.FromRgb(190, 190, 200) : Color.FromRgb(65, 65, 75);

            Color outerRingColor, midRingColor, innerRingColor, dotColor;
            if (isEmber)
            {
                outerRingColor = Color.FromRgb(255, 85, 0);
                midRingColor = Color.FromRgb(255, 136, 0);
                innerRingColor = Color.FromRgb(255, 187, 51);
                dotColor = Color.FromRgb(255, 85, 0);
            }
            else if (isVivid)
            {
                outerRingColor = Color.FromRgb(10, 132, 255);
                midRingColor = Color.FromRgb(48, 176, 255);
                innerRingColor = Color.FromRgb(104, 208, 255);
                dotColor = Color.FromRgb(10, 132, 255);
            }
            else if (isSilver)
            {
                outerRingColor = Color.FromRgb(34, 34, 38);
                midRingColor = Color.FromRgb(85, 85, 94);
                innerRingColor = Color.FromRgb(136, 136, 148);
                dotColor = Color.FromRgb(34, 34, 38);
            }
            else // Pure Black
            {
                outerRingColor = Color.FromRgb(220, 220, 230);
                midRingColor = Color.FromRgb(140, 140, 150);
                innerRingColor = Color.FromRgb(80, 80, 90);
                dotColor = Color.FromRgb(255, 255, 255);
            }

            // Main Mini Dashboard Container Box
            var panel = new Border
            {
                Width = 120,
                Height = 56,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(panelBg),
                BorderBrush = new SolidColorBrush(panelBorder),
                BorderThickness = new Thickness(1),
                SnapsToDevicePixels = true,
                ClipToBounds = true
            };

            var rootGrid = new Grid();

            // Notch at the top
            var notch = new Border
            {
                Width = 32,
                Height = 5.5,
                CornerRadius = new CornerRadius(0, 0, 3, 3),
                Background = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            rootGrid.Children.Add(notch);

            // Three mini cards container
            var cardsGrid = new Grid
            {
                Margin = new Thickness(5, 7, 5, 13),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            cardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Card 1: Left Card
            var leftCard = new Border
            {
                Background = new SolidColorBrush(miniCardBg),
                BorderBrush = new SolidColorBrush(miniCardBorder),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 0),
                Height = 26
            };
            var leftStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0) };
            leftStack.Children.Add(new Border { Width = 16, Height = 2, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(line1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) });
            leftStack.Children.Add(new Border { Width = 22, Height = 2, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(line2), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) });
            leftStack.Children.Add(new Border { Width = 11, Height = 2, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(line2), HorizontalAlignment = HorizontalAlignment.Left });
            leftCard.Child = leftStack;
            Grid.SetColumn(leftCard, 0);
            cardsGrid.Children.Add(leftCard);

            // Card 2: Center Card (3 Concentric Activity/Gauge Rings)
            var centerCard = new Border
            {
                Background = new SolidColorBrush(miniCardBg),
                BorderBrush = new SolidColorBrush(miniCardBorder),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 0),
                Height = 26
            };
            var ringCanvas = new Canvas { Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            // Outer Ring
            var outerRing = new Ellipse
            {
                Width = 22,
                Height = 22,
                Stroke = new SolidColorBrush(outerRingColor),
                StrokeThickness = 1.6
            };
            Canvas.SetLeft(outerRing, 1);
            Canvas.SetTop(outerRing, 1);
            ringCanvas.Children.Add(outerRing);

            // Mid Ring
            var midRing = new Ellipse
            {
                Width = 15,
                Height = 15,
                Stroke = new SolidColorBrush(midRingColor),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(midRing, 4.5);
            Canvas.SetTop(midRing, 4.5);
            ringCanvas.Children.Add(midRing);

            // Inner Ring
            var innerRing = new Ellipse
            {
                Width = 8,
                Height = 8,
                Stroke = new SolidColorBrush(innerRingColor),
                StrokeThickness = 1.5
            };
            Canvas.SetLeft(innerRing, 8);
            Canvas.SetTop(innerRing, 8);
            ringCanvas.Children.Add(innerRing);

            centerCard.Child = ringCanvas;
            Grid.SetColumn(centerCard, 1);
            cardsGrid.Children.Add(centerCard);

            // Card 3: Right Card (Lines + Indicator Dot)
            var rightCard = new Border
            {
                Background = new SolidColorBrush(miniCardBg),
                BorderBrush = new SolidColorBrush(miniCardBorder),
                BorderThickness = new Thickness(0.8),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(1, 0, 1, 0),
                Height = 26
            };
            var rightGrid = new Grid { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3, 0, 3, 0) };
            var rightStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Left };
            rightStack.Children.Add(new Border { Width = 16, Height = 2, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(line1), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 2) });
            rightStack.Children.Add(new Border { Width = 12, Height = 2, CornerRadius = new CornerRadius(1), Background = new SolidColorBrush(line2), HorizontalAlignment = HorizontalAlignment.Left });
            rightGrid.Children.Add(rightStack);

            var indicatorDot = new Ellipse
            {
                Width = 4,
                Height = 4,
                Fill = new SolidColorBrush(dotColor),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 1, 2, 0)
            };
            rightGrid.Children.Add(indicatorDot);

            rightCard.Child = rightGrid;
            Grid.SetColumn(rightCard, 2);
            cardsGrid.Children.Add(rightCard);

            rootGrid.Children.Add(cardsGrid);

            // Bottom Dock Row (7 mini dots, with active dot matching theme)
            var dockRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 3)
            };
            Color inactiveDot = isSilver ? Color.FromRgb(185, 185, 195) : Color.FromRgb(55, 55, 65);

            for (int i = 0; i < 7; i++)
            {
                bool isActive = (i == 2); // 3rd dot active
                dockRow.Children.Add(new Ellipse
                {
                    Width = 2.5,
                    Height = 2.5,
                    Fill = new SolidColorBrush(isActive ? dotColor : inactiveDot),
                    Margin = new Thickness(1.5, 0, 1.5, 0)
                });
            }
            rootGrid.Children.Add(dockRow);

            panel.Child = rootGrid;
            return panel;
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
                Text = "Theme, surface and notch effects",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            // -------------------------------------------------------------
            // SECTION: THEME (4 Visual Cards)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("THEME"));

            var themeGrid = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            themeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            themeGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var themeCards = new List<MayliVisualCard>();

            void AddThemeCard(string key, string title, string desc, FrameworkElement illus, int col, int row)
            {
                bool isSel = string.Equals(AppPreferences.Instance.Theme, key, StringComparison.OrdinalIgnoreCase) ||
                             (string.Equals(key, "Ember Dark", StringComparison.OrdinalIgnoreCase) &&
                              (string.IsNullOrEmpty(AppPreferences.Instance.Theme) || string.Equals(AppPreferences.Instance.Theme, "Dark", StringComparison.OrdinalIgnoreCase)));
                var card = new MayliVisualCard(key, title, desc, illus, isSel);
                card.Selected += selectedKey =>
                {
                    AppPreferences.Instance.Theme = selectedKey;
                    CapsuleTheme.SetTheme(selectedKey);
                    foreach (var c in themeCards) c.IsSelected = (c.Key == selectedKey);
                };
                themeCards.Add(card);
                Grid.SetColumn(card, col);
                Grid.SetRow(card, row);
                themeGrid.Children.Add(card);
            }

            AddThemeCard("Pure Black", "Pure Black", "Monochrome on true black", CreateThemeIllustration("pure-black"), 0, 0);
            AddThemeCard("Ember Dark", "Ember Dark", "Black with an orange accent", CreateThemeIllustration("ember-dark"), 1, 0);
            AddThemeCard("Vivid", "Vivid", "Black with a blue accent", CreateThemeIllustration("vivid"), 2, 0);
            AddThemeCard("Silver", "Silver", "Light panel; the notch stays black", CreateThemeIllustration("silver"), 0, 1);

            stack.Children.Add(themeGrid);

            // -------------------------------------------------------------
            // SECTION: SURFACE
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("SURFACE"));

            // 1. Outline Switch
            var outlineSwitch = new MayliToggleSwitch(AppPreferences.Instance.SurfaceOutline);
            var outlineRow = new MayliSettingRow("Outline", "A hairline around the notch", outlineSwitch);
            stack.Children.Add(outlineRow);

            // 2. Outline Width Segmented Control
            var widthOptions = new[] { "1 px", "2 px", "3 px" };
            var widthPicker = new MayliSegmentedControl(widthOptions, AppPreferences.Instance.SurfaceOutlineWidth);
            widthPicker.SelectionChanged += val =>
            {
                AppPreferences.Instance.SurfaceOutlineWidth = val;
            };
            var outlineWidthRow = new MayliSettingRow("Outline width", null, widthPicker);
            stack.Children.Add(outlineWidthRow);

            // 3. Tint outline from wallpaper
            var tintSwitch = new MayliToggleSwitch(AppPreferences.Instance.TintOutlineFromWallpaper);
            tintSwitch.CheckedChanged += state =>
            {
                AppPreferences.Instance.TintOutlineFromWallpaper = state;
            };
            var tintRow = new MayliSettingRow("Tint outline from wallpaper", "Picks up your wallpaper's main color", tintSwitch);
            stack.Children.Add(tintRow);

            void UpdateOutlineDependents(bool outlineOn)
            {
                outlineWidthRow.Opacity = outlineOn ? 1.0 : 0.4;
                outlineWidthRow.IsEnabled = outlineOn;
                tintRow.Opacity = outlineOn ? 1.0 : 0.4;
                tintRow.IsEnabled = outlineOn;
            }
            UpdateOutlineDependents(AppPreferences.Instance.SurfaceOutline);

            outlineSwitch.CheckedChanged += state =>
            {
                AppPreferences.Instance.SurfaceOutline = state;
                UpdateOutlineDependents(state);
            };

            // 4. Shadow
            var shadowOptions = new[] { "None", "Subtle", "Medium", "Deep" };
            var shadowPicker = new MayliSegmentedControl(shadowOptions, AppPreferences.Instance.NotchShadowDepth);
            shadowPicker.SelectionChanged += val =>
            {
                AppPreferences.Instance.NotchShadowDepth = val;
            };
            stack.Children.Add(new MayliSettingRow("Shadow", "Depth under the open notch", shadowPicker));

            // 5. Soft edge
            var softOptions = new[] { "None", "Low", "Medium", "High" };
            var softPicker = new MayliSegmentedControl(softOptions, AppPreferences.Instance.NotchSoftEdge);
            softPicker.SelectionChanged += val =>
            {
                AppPreferences.Instance.NotchSoftEdge = val;
            };
            stack.Children.Add(new MayliSettingRow("Soft edge", "Softens the notch edge; unavailable in reduced-effects mode", softPicker));

            // -------------------------------------------------------------
            // RESTORE DEFAULTS BUTTON
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
                AppPreferences.Instance.ResetAppearanceDefaults();
                CapsuleTheme.SetTheme("Ember Dark");
                _mainContentArea.Content = BuildAppearancePage();
                MessageBox.Show("Appearance settings have been restored to factory defaults.", "Mayli Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            stack.Children.Add(resetBtn);

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
                Text = "Choose what lives in your notch. Drag to reorder.",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 26)
            });

            // -------------------------------------------------------------
            // DOCK - CATEGORY HEADER
            // -------------------------------------------------------------
            stack.Children.Add(new TextBlock
            {
                Text = "DOCK",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 110)),
                Margin = new Thickness(0, 0, 0, 8)
            });

            void AddModuleRow(string id, string title, string desc, Color iconBg, string glyph, bool def)
            {
                bool isEnabled = AppPreferences.Instance.IsModuleEnabled(id, def);
                var row = new MayliModuleRow(id, title, desc, iconBg, glyph, isEnabled);
                row.Toggle.CheckedChanged += state =>
                {
                    AppPreferences.Instance.SetModuleEnabled(id, state);
                };
                stack.Children.Add(row);
            }

            // -------------------------------------------------------------
            // SECTION 1: ESSENTIALS
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("ESSENTIALS"));
            AddModuleRow("home", "Home", "Now playing, your next event and quick stats", Color.FromRgb(43, 102, 245), "⌂", true);
            AddModuleRow("system", "System", "CPU, memory, disk and battery", Color.FromRgb(0, 180, 216), "⊞", true);
            AddModuleRow("focus", "Timers", "Pomodoro, countdown, stopwatch and water", Color.FromRgb(255, 122, 0), "⏱", true);
            AddModuleRow("weather", "Weather", "Now, hourly and five-day forecast", Color.FromRgb(56, 166, 245), "⛅", true);
            AddModuleRow("clock", "Clock", "World clocks, digital or analog", Color.FromRgb(255, 170, 0), "🕒", true);
            AddModuleRow("calendar", "Calendar", "Agenda, festivals and reminders", Color.FromRgb(230, 57, 70), "📅", false);

            // -------------------------------------------------------------
            // SECTION 2: PRODUCTIVITY
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("PRODUCTIVITY"));
            AddModuleRow("clipboard", "Clipboard", "Everything you copied, searchable", Color.FromRgb(94, 92, 230), "📋", true);
            AddModuleRow("notes", "Notes", "Quick notes that stay with the notch", Color.FromRgb(255, 183, 3), "📝", true);
            AddModuleRow("fileshelf", "File Shelf", "Park files and drag them anywhere", Color.FromRgb(0, 194, 203), "📁", true);
            AddModuleRow("maylidrop", "Mayli Drop", "Share with your phone on your private network", Color.FromRgb(139, 128, 249), "📱", true);
            AddModuleRow("units", "Units", "Length, weight, temperature and more", Color.FromRgb(123, 44, 191), "⇄", true);
            AddModuleRow("links", "Links", "Bookmarks and quick web searches", Color.FromRgb(255, 89, 100), "🔗", true);
            AddModuleRow("message", "Message", "Flash your own text on the notch", Color.FromRgb(247, 37, 133), "💬", true);
            AddModuleRow("fileconverter", "File Converter", "Convert images and documents", Color.FromRgb(214, 34, 165), "📄", false);

            // -------------------------------------------------------------
            // SECTION 3: INSIGHTS
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("INSIGHTS"));
            AddModuleRow("screentime", "Screen Time", "Where your day went, app by app", Color.FromRgb(114, 9, 183), "⏳", true);
            AddModuleRow("analytics", "Analytics", "Website visitors, pages and sources", Color.FromRgb(46, 196, 182), "📊", false);
            AddModuleRow("aicoding", "AI Coding", "Claude Code and Codex usage", Color.FromRgb(90, 98, 106), "🤖", false);
            AddModuleRow("revenue", "Revenue", "Sales and license revenue at a glance", Color.FromRgb(112, 224, 0), "💵", false);

            // Reset order button
            var resetOrderBtn = new Border
            {
                CornerRadius = new CornerRadius(7),
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 9, 18, 9),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 16, 0, 12),
                Cursor = Cursors.Hand
            };
            var resetTxt = new TextBlock
            {
                Text = "Reset order",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            resetOrderBtn.Child = resetTxt;
            resetOrderBtn.MouseEnter += (_, _) => resetOrderBtn.Background = new SolidColorBrush(Color.FromRgb(40, 40, 48));
            resetOrderBtn.MouseLeave += (_, _) => resetOrderBtn.Background = new SolidColorBrush(Color.FromRgb(28, 28, 34));
            resetOrderBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                AppPreferences.Instance.ResetModulesDefaults();
                _mainContentArea.Content = BuildModulesPage();
                MessageBox.Show("Dock module order and settings have been restored to defaults.", "Mayli Modules", MessageBoxButton.OK, MessageBoxImage.Information);
            };
            stack.Children.Add(resetOrderBtn);

            var tipText = new TextBlock
            {
                Text = "Tip: drag the handle, or focus a row and press Alt+Up / Alt+Down. Changes show in the dock right away.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(110, 110, 120)),
                Margin = new Thickness(0, 0, 0, 32),
                TextWrapping = TextWrapping.Wrap
            };
            stack.Children.Add(tipText);

            // -------------------------------------------------------------
            // SECTION 4: HOME WIDGETS (Screenshots 4 & 5)
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("HOME WIDGETS"));

            var homeWidgetsList = new (string Id, string Title)[]
            {
                ("screentime", "Screen Time"),
                ("system", "System"),
                ("weather", "Weather"),
                ("focus", "Focus"),
                ("clipboard", "Clipboard"),
                ("notes", "Notes"),
                ("clock", "Clock"),
                ("calendar", "Calendar"),
                ("nowplaying", "Now Playing"),
                ("links", "Links"),
                ("fileshelf", "File Shelf"),
                ("units", "Units"),
                ("fileconverter", "File Converter"),
                ("message", "Message"),
                ("water", "Water"),
                ("stopwatch", "Stopwatch"),
                ("analytics", "Analytics"),
                ("revenue", "Revenue"),
                ("aicoding", "AI Coding")
            };

            var widgetRows = new List<MayliHomeWidgetRow>();

            void RefreshAllWidgetSubtitles()
            {
                int count = AppPreferences.Instance.GetActiveHomeWidgetCount();
                bool canEnableMore = count < 6;
                foreach (var r in widgetRows)
                {
                    r.UpdateState(r.Toggle.IsChecked, canEnableMore);
                }
            }

            foreach (var (wId, wTitle) in homeWidgetsList)
            {
                bool isWidgetActive = AppPreferences.Instance.IsHomeWidgetEnabled(wId);
                int currentActive = AppPreferences.Instance.GetActiveHomeWidgetCount();
                string initialSubtitle = isWidgetActive ? "Show on Home" : (currentActive >= 6 ? "Remove a widget to make room" : "Show on Home");

                var wRow = new MayliHomeWidgetRow(wId, wTitle, isWidgetActive, initialSubtitle);
                wRow.UpdateState(isWidgetActive, currentActive < 6);

                wRow.Toggle.CheckedChanged += isChecked =>
                {
                    if (isChecked)
                    {
                        bool success = AppPreferences.Instance.SetHomeWidgetEnabled(wId, true);
                        if (!success)
                        {
                            wRow.Toggle.IsChecked = false;
                            MessageBox.Show("You can display up to 6 widgets on Home. Remove one to add another.", "Home Widgets", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }
                    }
                    else
                    {
                        AppPreferences.Instance.SetHomeWidgetEnabled(wId, false);
                    }
                    RefreshAllWidgetSubtitles();
                };

                widgetRows.Add(wRow);
                stack.Children.Add(wRow);
            }

            return stack;
        }

        // =================================================================
        // PAGE 5: LIVE ACTIVITIES (1:1 with user's uploaded reference screenshots)
        // =================================================================
        private FrameworkElement BuildLiveActivitiesPage()
        {
            var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 40) };

            stack.Children.Add(new TextBlock
            {
                Text = "Live Activities",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            stack.Children.Add(new TextBlock
            {
                Text = "Which events may expand the notch",
                FontSize = 12.5,
                Foreground = new SolidColorBrush(Color.FromRgb(128, 128, 136)),
                Margin = new Thickness(0, 4, 0, 24)
            });

            // -------------------------------------------------------------
            // SECTION 1: MUSIC
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("MUSIC"));

            var nowPlayingRow = new MayliActivityRow(
                "Now playing",
                "Music from your player",
                Color.FromRgb(0, 122, 255),
                "▶",
                AppPreferences.Instance.ShowMediaActivity
            );
            nowPlayingRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.ShowMediaActivity = chk;
            stack.Children.Add(nowPlayingRow);

            var browserMediaRow = new MayliActivityRow(
                "Show browser media in notch",
                "Includes videos and ads reported by your browser",
                Color.FromRgb(0, 122, 255),
                "▶",
                AppPreferences.Instance.ShowBrowserMediaInNotch
            );
            browserMediaRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.ShowBrowserMediaInNotch = chk;
            stack.Children.Add(browserMediaRow);

            // Music color row with Swatches + Custom... button
            stack.Children.Add(BuildMusicColorRow());

            // -------------------------------------------------------------
            // SECTION 2: CHARGING
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("CHARGING"));

            var chargingRow = new MayliActivityRow(
                "Battery charging",
                "Plugged in, unplugged and fully charged",
                Color.FromRgb(0, 168, 150),
                "⚡",
                AppPreferences.Instance.ShowBatteryActivity
            );
            chargingRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.ShowBatteryActivity = chk;
            stack.Children.Add(chargingRow);

            // -------------------------------------------------------------
            // SECTION 3: VOLUME
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("VOLUME"));

            var volumeRow = new MayliActivityRow(
                "Volume",
                "When you change the volume",
                Color.FromRgb(0, 185, 175),
                "🔊",
                AppPreferences.Instance.VolumeActivity
            );
            volumeRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.VolumeActivity = chk;
            stack.Children.Add(volumeRow);

            // -------------------------------------------------------------
            // SECTION 4: COPIED
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("COPIED"));

            var clipRow = new MayliActivityRow(
                "Clipboard copied",
                "A short confirmation when you copy something",
                Color.FromRgb(94, 92, 230),
                "📋",
                AppPreferences.Instance.ClipboardCopiedActivity
            );
            clipRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.ClipboardCopiedActivity = chk;
            stack.Children.Add(clipRow);

            // Hide copied text (no icon, indented to align with Clipboard text)
            var hideCopiedGrid = new Grid { Margin = new Thickness(0, 8, 0, 14) };
            hideCopiedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hideCopiedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var hideTextStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(50, 0, 16, 0)
            };
            hideTextStack.Children.Add(new TextBlock
            {
                Text = "Hide copied text",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            hideTextStack.Children.Add(new TextBlock
            {
                Text = "Show \"Text\" or \"Image\" instead of what you copied",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(hideTextStack, 0);
            hideCopiedGrid.Children.Add(hideTextStack);

            var hideToggle = new MayliToggleSwitch(AppPreferences.Instance.HideCopiedText);
            hideToggle.CheckedChanged += chk => AppPreferences.Instance.HideCopiedText = chk;
            hideToggle.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(hideToggle, 1);
            hideCopiedGrid.Children.Add(hideToggle);

            stack.Children.Add(hideCopiedGrid);

            // -------------------------------------------------------------
            // SECTION 5: REMINDERS
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("REMINDERS"));

            var runningTimersRow = new MayliActivityRow(
                "Running timers",
                "Show a running Pomodoro, countdown or stopwatch on the closed notch; click it to open Timers",
                Color.FromRgb(255, 149, 0),
                "⏱",
                AppPreferences.Instance.RunningTimersActivity
            );
            runningTimersRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.RunningTimersActivity = chk;
            stack.Children.Add(runningTimersRow);

            var timersRow = new MayliActivityRow(
                "Timers",
                "Pomodoro, countdown and water reminders when they finish",
                Color.FromRgb(255, 149, 0),
                "⏱",
                AppPreferences.Instance.TimerFinishAlerts
            );
            timersRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.TimerFinishAlerts = chk;
            stack.Children.Add(timersRow);

            var calendarRow = new MayliActivityRow(
                "Calendar alerts",
                "Upcoming events and reminders that are due",
                Color.FromRgb(255, 59, 92),
                "📅",
                AppPreferences.Instance.CalendarAlerts
            );
            calendarRow.Toggle.CheckedChanged += chk => AppPreferences.Instance.CalendarAlerts = chk;
            stack.Children.Add(calendarRow);

            // Reminders Target (Segmented Control: Notch only | Notch + Windows)
            var remindersGrid = new Grid { Margin = new Thickness(0, 8, 0, 14) };
            remindersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            remindersGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var remindersTextStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            remindersTextStack.Children.Add(new TextBlock
            {
                Text = "Reminders",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            remindersTextStack.Children.Add(new TextBlock
            {
                Text = "Where upcoming reminders appear",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(remindersTextStack, 0);
            remindersGrid.Children.Add(remindersTextStack);

            var remindersSeg = new MayliSegmentedControl(
                new[] { "Notch only", "Notch + Windows" },
                AppPreferences.Instance.RemindersTarget
            );
            remindersSeg.SelectionChanged += val => AppPreferences.Instance.RemindersTarget = val;
            remindersSeg.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(remindersSeg, 1);
            remindersGrid.Children.Add(remindersSeg);

            stack.Children.Add(remindersGrid);

            // -------------------------------------------------------------
            // SECTION 6: TIMING
            // -------------------------------------------------------------
            stack.Children.Add(new MayliSectionHeader("TIMING"));

            var timingGrid = new Grid { Margin = new Thickness(0, 8, 0, 24) };
            timingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timingGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var timingTextStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            timingTextStack.Children.Add(new TextBlock
            {
                Text = "Event duration",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            timingTextStack.Children.Add(new TextBlock
            {
                Text = "How long an event stays before the notch settles",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(timingTextStack, 0);
            timingGrid.Children.Add(timingTextStack);

            var timingSeg = new MayliSegmentedControl(
                new[] { "2 s", "3 s", "5 s" },
                AppPreferences.Instance.LiveActivityEventDuration
            );
            timingSeg.SelectionChanged += val => AppPreferences.Instance.LiveActivityEventDuration = val;
            timingSeg.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(timingSeg, 1);
            timingGrid.Children.Add(timingSeg);

            stack.Children.Add(timingGrid);

            // -------------------------------------------------------------
            // RESTORE DEFAULTS BUTTON
            // -------------------------------------------------------------
            var resetBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 9, 18, 9),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 12, 0, 20),
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
                AppPreferences.Instance.ResetLiveActivitiesDefaults();
                _mainContentArea.Content = BuildLiveActivitiesPage();
                MessageBox.Show("Live Activities settings have been restored to factory defaults.", "Mayli Settings", MessageBoxButton.OK, MessageBoxImage.Information);
            };

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 20) };
            resetBtn.Margin = new Thickness(0, 0, 0, 0);
            btnRow.Children.Add(resetBtn);

            var testBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 42, 58)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 75, 110)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(18, 9, 18, 9),
                Margin = new Thickness(12, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            var testTxt = new TextBlock
            {
                Text = "🔔 Test Notch Notification",
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 180, 255))
            };
            testBtn.Child = testTxt;
            testBtn.MouseEnter += (_, _) => testBtn.Background = new SolidColorBrush(Color.FromRgb(42, 56, 78));
            testBtn.MouseLeave += (_, _) => testBtn.Background = new SolidColorBrush(Color.FromRgb(32, 42, 58));
            testBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                WindowsBuddy.Services.WindowsNotificationListenerService.Instance.TriggerTestNotification(
                    appName: "Telegram",
                    title: "Sara",
                    message: "Hey, check out this Dynamic Island split notification! ✨"
                );
            };
            btnRow.Children.Add(testBtn);

            stack.Children.Add(btnRow);

            return stack;
        }

        private FrameworkElement BuildMusicColorRow()
        {
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0)
            };
            textStack.Children.Add(new TextBlock
            {
                Text = "Music color",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            });
            textStack.Children.Add(new TextBlock
            {
                Text = "Beat color for now playing",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 140)),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(textStack, 0);
            grid.Children.Add(textStack);

            // Right container: swatches stack + custom button
            var rightContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var swatchesPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            string currentColor = AppPreferences.Instance.MusicBeatColor;
            var swatchBorders = new List<(string ColorKey, Border Border)>();

            void SelectColor(string colorKey)
            {
                AppPreferences.Instance.MusicBeatColor = colorKey;
                foreach (var item in swatchBorders)
                {
                    bool isSelected = string.Equals(item.ColorKey, colorKey, StringComparison.OrdinalIgnoreCase);
                    item.Border.BorderBrush = isSelected ? new SolidColorBrush(Color.FromRgb(255, 149, 0)) : Brushes.Transparent;
                }
            }

            // 1. Artwork gradient circle
            var artOuter = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(11),
                BorderThickness = new Thickness(2),
                BorderBrush = string.Equals(currentColor, "Artwork", StringComparison.OrdinalIgnoreCase)
                    ? new SolidColorBrush(Color.FromRgb(255, 149, 0))
                    : Brushes.Transparent,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Background = Brushes.Transparent
            };
            var artInner = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(255, 45, 85), 0.0),
                        new GradientStop(Color.FromRgb(175, 82, 222), 0.5),
                        new GradientStop(Color.FromRgb(0, 122, 255), 1.0)
                    },
                    new Point(0, 0),
                    new Point(1, 1)
                )
            };
            artOuter.Child = artInner;
            artOuter.MouseLeftButtonUp += (_, _) => SelectColor("Artwork");
            swatchBorders.Add(("Artwork", artOuter));
            swatchesPanel.Children.Add(artOuter);

            // 2. Preset color swatches
            var presetColors = new (string Name, string Hex)[]
            {
                ("White", "#FFFFFF"),
                ("Red", "#FF3B30"),
                ("Pink", "#FF2D55"),
                ("Purple", "#AF52DE"),
                ("Blue", "#007AFF"),
                ("Cyan", "#5AC8FA"),
                ("Green", "#34C759"),
                ("Yellow", "#FFCC00")
            };

            foreach (var preset in presetColors)
            {
                var colorObj = (Color)ColorConverter.ConvertFromString(preset.Hex);
                var outer = new Border
                {
                    Width = 22,
                    Height = 22,
                    CornerRadius = new CornerRadius(11),
                    BorderThickness = new Thickness(2),
                    BorderBrush = string.Equals(currentColor, preset.Hex, StringComparison.OrdinalIgnoreCase)
                        ? new SolidColorBrush(Color.FromRgb(255, 149, 0))
                        : Brushes.Transparent,
                    Margin = new Thickness(0, 0, 8, 0),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent
                };
                var inner = new Border
                {
                    Width = 14,
                    Height = 14,
                    CornerRadius = new CornerRadius(7),
                    Background = new SolidColorBrush(colorObj)
                };
                outer.Child = inner;
                string hexCapture = preset.Hex;
                outer.MouseLeftButtonUp += (_, _) => SelectColor(hexCapture);
                swatchBorders.Add((preset.Hex, outer));
                swatchesPanel.Children.Add(outer);
            }

            rightContainer.Children.Add(swatchesPanel);

            // 3. Custom... pill button below swatches
            var customBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(26, 26, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 54)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 12, 4),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Cursor = Cursors.Hand
            };
            var customStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            Color customDotColor = Color.FromRgb(255, 59, 48);
            if (!string.Equals(currentColor, "Artwork", StringComparison.OrdinalIgnoreCase))
            {
                try { customDotColor = (Color)ColorConverter.ConvertFromString(currentColor); } catch { }
            }

            var customDot = new Border
            {
                Width = 10,
                Height = 10,
                CornerRadius = new CornerRadius(5),
                Background = new SolidColorBrush(customDotColor),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            customStack.Children.Add(customDot);
            customStack.Children.Add(new TextBlock
            {
                Text = "Custom...",
                FontSize = 12,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center
            });
            customBtn.Child = customStack;

            customBtn.MouseLeftButtonUp += (_, _) =>
            {
                using var dlg = new System.Windows.Forms.ColorDialog();
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string hex = $"#{dlg.Color.R:X2}{dlg.Color.G:X2}{dlg.Color.B:X2}";
                    customDot.Background = new SolidColorBrush(Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B));
                    SelectColor(hex);
                }
            };

            rightContainer.Children.Add(customBtn);

            Grid.SetColumn(rightContainer, 1);
            grid.Children.Add(rightContainer);

            return grid;
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
            var versionText = new TextBlock
            {
                Text = $"✓ Up to date (v{UpdateService.CurrentVersion})",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88))
            };
            versionBadge.Child = versionText;
            stack.Children.Add(new MayliSettingRow(
                "Installed version",
                $"Mayli v{UpdateService.CurrentVersion} (Windows x64 Architecture).",
                versionBadge
            ));

            var checkBtn = new Border
            {
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromRgb(20, 100, 210)),
                Padding = new Thickness(16, 6, 16, 6),
                Cursor = Cursors.Hand
            };
            var checkBtnText = new TextBlock { Text = "Check Now", FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            checkBtn.Child = checkBtnText;
            checkBtn.MouseLeftButtonUp += async (_, e) =>
            {
                e.Handled = true;
                checkBtnText.Text = "Checking...";
                checkBtn.IsEnabled = false;

                try
                {
                    var info = await UpdateService.CheckForUpdatesAsync();
                    if (info.HasUpdate)
                    {
                        versionText.Text = $"🚀 Update available: v{info.LatestVersion}";
                        versionText.Foreground = new SolidColorBrush(Color.FromRgb(255, 149, 0));
                        versionBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(255, 149, 0));
                        versionBadge.Background = new SolidColorBrush(Color.FromArgb(40, 255, 149, 0));

                        checkBtnText.Text = "Download v" + info.LatestVersion + " ↗";
                        checkBtn.Background = new SolidColorBrush(Color.FromRgb(48, 209, 88));
                        checkBtn.IsEnabled = true;
                        checkBtn.MouseLeftButtonUp -= null;
                        checkBtn.MouseLeftButtonUp += (_, _) =>
                        {
                            Process.Start(new ProcessStartInfo(info.HtmlUrl) { UseShellExecute = true });
                        };

                        RealtimeNotchActionService.Instance.TriggerNotification(new NotchNotificationPayload
                        {
                            Icon = "🚀",
                            Title = "Update Available",
                            Subtitle = $" · v{info.LatestVersion} is ready",
                            DurationMs = 7000
                        });
                    }
                    else
                    {
                        checkBtnText.Text = "You're on latest!";
                        await Task.Delay(2000);
                        checkBtnText.Text = "Check Now";
                        checkBtn.IsEnabled = true;
                    }
                }
                catch
                {
                    checkBtnText.Text = "Check Now";
                    checkBtn.IsEnabled = true;
                    Process.Start(new ProcessStartInfo("https://github.com/Mahdilvl20/Mayli/releases") { UseShellExecute = true });
                }
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
                Text = $"Version {UpdateService.CurrentVersion} (Release x64) • The Notch for Windows",
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
