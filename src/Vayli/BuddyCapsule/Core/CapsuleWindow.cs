using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;
using WindowsBuddy.Tools;
using WindowsBuddy.UI.Settings;

namespace WindowsBuddy.BuddyCapsule.Core
{
    /// <summary>
    /// CapsuleWindow: Exact notch window architecture matching the Vayli design references.
    /// CollapsedWidth = 228, CollapsedHeight = 35 (Can expand to 360x42 for wide notification/greeting banner)
    /// ExpandedWidth = 752, ExpandedHeight = 352
    /// </summary>
    public sealed class CapsuleWindow : Window
    {
        public const double CollapsedWidth = 228.0;
        public const double CollapsedHeight = 35.0;
        public const double BannerCollapsedWidth = 340.0;
        public const double ExpandedWidth = 752.0;
        public const double ExpandedHeight = 352.0;
        public const double StableEnvelopeWidth = 800.0;
        public const double StableEnvelopeHeight = 388.0;
        public const double EarUnitW = 12.0;
        public const double EarUnitH = 10.0;
        public const double HeaderRobotSize = 22.0;

        private readonly NotchStateMachine _machine;
        private readonly Grid _envelopeRoot;
        private readonly Grid _capsuleCard;
        private readonly Path _notchBackdropPath;
        private readonly Path _notchOutlinePath;
        private readonly EdgeLightWpf _edgeLight;
        private readonly Grid _collapsedContent;
        private readonly Grid _expandedContent;
        private readonly Border _viewportHost;
        private readonly StackPanel _navTabsPanel;
        private readonly Dictionary<string, FrameworkElement> _dockButtons = new();

        private readonly TextBlock _headerTitleText;
        private readonly TextBlock _headerSubtitleText;
        private readonly TextBlock _headerBatteryText;

        // Collapsed Banner & Normal elements
        private readonly Grid _collapsedNormalView;
        private readonly Grid _collapsedBannerView;
        private readonly TextBlock _bannerIcon;
        private readonly TextBlock _bannerTitle;
        private readonly TextBlock _bannerSubtitle;
        private readonly TextBlock _collapsedClockText;
        private readonly TextBlock _collapsedBatteryText;
        private readonly StaticBeatBars _collapsedBeatBars;
        private readonly PetView _collapsedMiniPet;

        private readonly DispatcherTimer _morphTimer;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _bannerCycleTimer;
        private double _morphProgress; // 0.0 = Collapsed, 1.0 = Expanded
        private double _targetProgress;
        private string _activeToolId = "home";
        private int _bannerMode = 0; // 0=Normal, 1=GoodMorning, 2=Copied, 3=AirPods, 4=CoffeeBreak, 5=Coding

        public CapsuleWindow()
        {
            ToolRegistry.RegisterDefaults();
            ToolRegistry.OnToolNavigationRequested = id => NavigateToTool(id);
            _machine = new NotchStateMachine();

            Title = "Vayli Notch";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = StableEnvelopeWidth;
            Height = StableEnvelopeHeight;

            PositionAtTopCenter();

            _envelopeRoot = new Grid
            {
                Width = StableEnvelopeWidth,
                Height = StableEnvelopeHeight,
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _capsuleCard = new Grid
            {
                Width = CollapsedWidth,
                Height = CollapsedHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand
            };

            var p = CapsuleTheme.Current;

            _notchBackdropPath = new Path
            {
                Fill = CapsuleTheme.Brush(p.Bg),
                Effect = new DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 6,
                    Opacity = 0.72,
                    Color = Colors.Black
                }
            };

            _notchOutlinePath = new Path
            {
                Stroke = CapsuleTheme.Brush(p.BorderStrong),
                StrokeThickness = 1.0
            };

            _edgeLight = new EdgeLightWpf();

            // -------------------------------------------------------------
            // 1. Collapsed Resting Content (228 x 35) & Notch Notifications
            // -------------------------------------------------------------
            _collapsedContent = new Grid
            {
                Margin = new Thickness(EarUnitW + 8, 2, EarUnitW + 8, 2),
                VerticalAlignment = VerticalAlignment.Center
            };

            // Normal collapsed view
            _collapsedNormalView = new Grid();
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _collapsedMiniPet = new PetView("idle", HeaderRobotSize)
            {
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedMiniPet, 0);
            _collapsedNormalView.Children.Add(_collapsedMiniPet);

            _collapsedClockText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedClockText, 1);
            _collapsedNormalView.Children.Add(_collapsedClockText);

            _collapsedBeatBars = new StaticBeatBars(4, 12)
            {
                Margin = new Thickness(8, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_collapsedBeatBars, 3);
            _collapsedNormalView.Children.Add(_collapsedBeatBars);

            _collapsedBatteryText = new TextBlock
            {
                Text = $"{SystemMonitorService.Current.BatteryPercent}%⚡",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(Color.FromRgb(48, 209, 88)), // Signature Green
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedBatteryText, 4);
            _collapsedNormalView.Children.Add(_collapsedBatteryText);
            _collapsedContent.Children.Add(_collapsedNormalView);

            // Dynamic Banner view (Screenshot 2: Copied, Screenshot, AirPods / Screenshot 6: Good morning)
            _collapsedBannerView = new Grid { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            var bannerDock = new DockPanel();

            _bannerIcon = new TextBlock
            {
                Text = "📋",
                FontSize = 13,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_bannerIcon, Dock.Left);
            bannerDock.Children.Add(_bannerIcon);

            var bannerTextStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _bannerTitle = new TextBlock
            {
                Text = "Copied",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            _bannerSubtitle = new TextBlock
            {
                Text = " · Image",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            bannerTextStack.Children.Add(_bannerTitle);
            bannerTextStack.Children.Add(_bannerSubtitle);
            bannerDock.Children.Add(bannerTextStack);
            _collapsedBannerView.Children.Add(bannerDock);
            _collapsedContent.Children.Add(_collapsedBannerView);

            // -------------------------------------------------------------
            // 2. Expanded Content (752 x 352)
            // -------------------------------------------------------------
            _expandedContent = new Grid
            {
                Margin = new Thickness(EarUnitW + 12, 10, EarUnitW + 12, 12),
                Opacity = 0.0,
                Visibility = Visibility.Collapsed
            };
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // TOP HEADER BAR (Exact layout from Screenshot 1 & 3 & 4)
            var headerBar = new DockPanel { Margin = new Thickness(2, 0, 2, 8) };

            // Left brand & tool title
            var leftBrand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Squircle Robot Head Icon
            var squircleIcon = new Border
            {
                Width = 22,
                Height = 22,
                CornerRadius = new CornerRadius(6),
                Background = CapsuleTheme.Brush(p.IsLight ? Color.FromRgb(28, 28, 32) : Color.FromRgb(250, 250, 252)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "••",
                    FontSize = 11,
                    FontWeight = FontWeights.ExtraBold,
                    Foreground = CapsuleTheme.Brush(p.IsLight ? Colors.White : Colors.Black),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, -2, 0, 0)
                }
            };
            leftBrand.Children.Add(squircleIcon);

            _headerTitleText = new TextBlock
            {
                Text = "Home",
                FontSize = 13.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftBrand.Children.Add(_headerTitleText);

            leftBrand.Children.Add(new TextBlock
            {
                Text = "  |  ",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text3),
                VerticalAlignment = VerticalAlignment.Center
            });

            _headerSubtitleText = new TextBlock
            {
                Text = DateTime.Now.ToString("dddd, d MMM"),
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            };
            leftBrand.Children.Add(_headerSubtitleText);

            // Right header actions: Battery + Theme Switcher + Settings + Close
            var rightActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            // Battery badge
            _headerBatteryText = new TextBlock
            {
                Text = $"{SystemMonitorService.Current.BatteryPercent}% ⚡",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(Color.FromRgb(48, 209, 88)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            rightActions.Children.Add(_headerBatteryText);

            // Theme Switcher Button (Screenshot 5: Cycles Pure Black -> Ember Dark -> Vivid -> Silver)
            var themeBtn = CreateHeaderIconButton("🎨", $"Theme: {CapsuleTheme.Current.DisplayName} (Click to switch)");
            themeBtn.MouseLeftButtonUp += (_, _) =>
            {
                CapsuleTheme.CycleTheme();
                themeBtn.ToolTip = $"Theme: {CapsuleTheme.Current.DisplayName} (Click to switch)";
            };
            rightActions.Children.Add(themeBtn);

            // Settings Button
            var settingsBtn = CreateHeaderIconButton("⚙", "Settings");
            settingsBtn.MouseLeftButtonUp += (_, _) => SettingsWindow.ShowSingleton();
            rightActions.Children.Add(settingsBtn);

            // Close Button
            var collapseBtn = CreateHeaderIconButton("✕", "Close Notch");
            collapseBtn.MouseLeftButtonUp += (_, _) => _machine.RequestClose("close-button");
            rightActions.Children.Add(collapseBtn);

            DockPanel.SetDock(rightActions, Dock.Right);
            headerBar.Children.Add(rightActions);
            headerBar.Children.Add(leftBrand);
            Grid.SetRow(headerBar, 0);
            _expandedContent.Children.Add(headerBar);

            // Center Tool Viewport
            _viewportHost = new Border
            {
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(_viewportHost, 1);
            _expandedContent.Children.Add(_viewportHost);

            // Bottom Navigation Dock Bar (Screenshot 1: 18 tool icons with active circle highlight & dot)
            var navDockBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.DockPill),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(6, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _navTabsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            RebuildNavTabs();
            navDockBorder.Child = _navTabsPanel;
            Grid.SetRow(navDockBorder, 2);
            _expandedContent.Children.Add(navDockBorder);

            _capsuleCard.Children.Add(_notchBackdropPath);
            _capsuleCard.Children.Add(_notchOutlinePath);
            _capsuleCard.Children.Add(_edgeLight);
            _capsuleCard.Children.Add(_collapsedContent);
            _capsuleCard.Children.Add(_expandedContent);

            _envelopeRoot.Children.Add(_capsuleCard);
            Content = _envelopeRoot;

            // Wire state machine transitions & morph animation
            _morphTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(14) };
            _morphTimer.Tick += OnMorphTick;

            _machine.PhaseChanged += (_, next, _) =>
            {
                if (next == NotchPhase.Opening || next == NotchPhase.Open)
                {
                    _targetProgress = 1.0;
                    _expandedContent.Visibility = Visibility.Visible;
                    _morphTimer.Start();
                }
                else if (next == NotchPhase.Closing || next == NotchPhase.Resting)
                {
                    _targetProgress = 0.0;
                    _morphTimer.Start();
                }
            };

            _capsuleCard.MouseEnter += (_, _) => _machine.PointerMoved(true, "mouse-enter");
            _capsuleCard.MouseLeave += (_, _) => _machine.PointerMoved(false, "mouse-leave");
            _capsuleCard.MouseLeftButtonUp += (_, _) =>
            {
                if (_morphProgress <= 0.1)
                {
                    // Right clicking or clicking collapsed notch toggles banner notifications or opens
                    _machine.Click("capsule-click");
                }
            };

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) =>
            {
                _collapsedClockText.Text = DateTime.Now.ToString("HH:mm");
                var sys = SystemMonitorService.Current;
                string batStr = $"{sys.BatteryPercent}%⚡";
                _collapsedBatteryText.Text = batStr;
                _headerBatteryText.Text = batStr;
            };
            _clockTimer.Start();

            // Periodic banner simulation when collapsed (Screenshots 2 & 6: Good morning, Copied, AirPods Pro)
            _bannerCycleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            _bannerCycleTimer.Tick += (_, _) =>
            {
                if (_morphProgress > 0.1) return;
                _bannerMode = (_bannerMode + 1) % 6;
                ShowBannerNotificationMode(_bannerMode);
            };
            _bannerCycleTimer.Start();

            CapsuleTheme.ThemeChanged += ApplyThemeAndGeometry;
            AppPreferences.Instance.Changed += ApplyThemeAndGeometry;

            NavigateToTool("home");
            UpdateMorphVisuals(0.0);
        }

        private void ShowBannerNotificationMode(int mode)
        {
            var p = CapsuleTheme.Current;
            switch (mode)
            {
                case 1: // Screenshot 6: Good morning, Alex!
                    _collapsedNormalView.Visibility = Visibility.Collapsed;
                    _collapsedBannerView.Visibility = Visibility.Visible;
                    _bannerIcon.Text = "🤖";
                    _bannerTitle.Text = "Good morning, Alex!";
                    _bannerSubtitle.Text = "";
                    break;
                case 2: // Screenshot 2: Copied · Image
                    _collapsedNormalView.Visibility = Visibility.Collapsed;
                    _collapsedBannerView.Visibility = Visibility.Visible;
                    _bannerIcon.Text = "📋";
                    _bannerTitle.Text = "Copied";
                    _bannerSubtitle.Text = " · Image";
                    break;
                case 3: // Screenshot 2: Screenshot saved
                    _collapsedNormalView.Visibility = Visibility.Collapsed;
                    _collapsedBannerView.Visibility = Visibility.Visible;
                    _bannerIcon.Text = "📸";
                    _bannerTitle.Text = "Screenshot saved";
                    _bannerSubtitle.Text = "";
                    break;
                case 4: // Screenshot 2: AirPods Pro Connected
                    _collapsedNormalView.Visibility = Visibility.Collapsed;
                    _collapsedBannerView.Visibility = Visibility.Visible;
                    _bannerIcon.Text = "🎧";
                    _bannerTitle.Text = "AirPods Pro";
                    _bannerSubtitle.Text = "  Connected";
                    break;
                default: // Normal clock & battery
                    _collapsedBannerView.Visibility = Visibility.Collapsed;
                    _collapsedNormalView.Visibility = Visibility.Visible;
                    break;
            }
        }

        private static Border CreateHeaderIconButton(string glyph, string tooltip)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(2, 0, 2, 0),
                Child = new TextBlock
                {
                    Text = glyph,
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            border.MouseEnter += (_, _) =>
            {
                border.Background = CapsuleTheme.Brush(CapsuleTheme.Current.ControlFillHover);
                if (border.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
            };
            border.MouseLeave += (_, _) =>
            {
                border.Background = Brushes.Transparent;
                if (border.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
            };
            return border;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST);

            HotkeyService.Instance.AttachWindow(hwnd);
            HotkeyService.Instance.Pressed += () => _machine.Toggle("hotkey");

            NativeMethods.AddClipboardFormatListener(hwnd);
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_CLIPBOARDUPDATE && AppPreferences.Instance.Raw.ClipboardRecording)
            {
                ClipboardTool.CaptureCurrentClipboard();
                ShowBannerNotificationMode(2); // Show "Copied" banner!
            }
            else if (msg == NativeMethods.WM_DISPLAYCHANGE || msg == NativeMethods.WM_DPICHANGED)
            {
                PositionAtTopCenter();
            }
            return IntPtr.Zero;
        }

        private void PositionAtTopCenter()
        {
            double screenW = SystemParameters.PrimaryScreenWidth;
            Left = (screenW - StableEnvelopeWidth) / 2.0;
            Top = 0;
        }

        private void RebuildNavTabs()
        {
            _navTabsPanel.Children.Clear();
            _dockButtons.Clear();
            var p = CapsuleTheme.Current;

            foreach (var tool in ToolRegistry.All)
            {
                string id = tool.Id;
                bool isActive = string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase);

                var itemContainer = new Grid
                {
                    Width = 30,
                    Height = 28,
                    Cursor = Cursors.Hand,
                    ToolTip = tool.Title
                };

                // Active circular highlight
                var circleHighlight = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = CapsuleTheme.Brush(isActive ? (p.IsLight ? Color.FromArgb(40, 0, 0, 0) : Color.FromArgb(60, 255, 255, 255)) : Colors.Transparent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemContainer.Children.Add(circleHighlight);

                // Icon Glyph
                var iconText = new TextBlock
                {
                    Text = tool.IconGlyph,
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(isActive ? (p.IsLight ? p.Text : Colors.White) : p.IconMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemContainer.Children.Add(iconText);

                // Tiny white dot indicator right beneath icon (Screenshot 1)
                var activeDot = new Ellipse
                {
                    Width = 3,
                    Height = 3,
                    Fill = CapsuleTheme.Brush(p.Accent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 1),
                    Visibility = isActive ? Visibility.Visible : Visibility.Collapsed
                };
                itemContainer.Children.Add(activeDot);

                itemContainer.MouseEnter += (_, _) =>
                {
                    if (!string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase))
                    {
                        circleHighlight.Background = CapsuleTheme.Brush(p.ControlFillHover);
                        iconText.Foreground = CapsuleTheme.Brush(p.Text);
                    }
                };
                itemContainer.MouseLeave += (_, _) =>
                {
                    if (!string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase))
                    {
                        circleHighlight.Background = Brushes.Transparent;
                        iconText.Foreground = CapsuleTheme.Brush(p.IconMuted);
                    }
                };

                itemContainer.MouseLeftButtonUp += (_, e) =>
                {
                    e.Handled = true;
                    NavigateToTool(id);
                };

                _dockButtons[id] = itemContainer;
                _navTabsPanel.Children.Add(itemContainer);
            }

            // Divider and '+' customize button matching Screenshot 1
            var divider = new Border
            {
                Width = 1,
                Height = 14,
                Background = CapsuleTheme.Brush(p.Border),
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _navTabsPanel.Children.Add(divider);

            var plusBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Cursor = Cursors.Hand,
                ToolTip = "Add / Customize Tools",
                Child = new TextBlock
                {
                    Text = "+",
                    FontSize = 13,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            plusBtn.MouseLeftButtonUp += (_, _) => SettingsWindow.ShowSingleton();
            _navTabsPanel.Children.Add(plusBtn);
        }

        public void NavigateToTool(string toolId)
        {
            var tool = ToolRegistry.GetById(toolId);
            if (tool == null) return;
            _activeToolId = tool.Id;

            // Update Header text based on selected tool (Screenshot 1, 3, 4)
            _headerTitleText.Text = tool.Title;
            _headerSubtitleText.Text = tool.Id switch
            {
                "home" => DateTime.Now.ToString("dddd, d MMM"),
                "weather" => "San Francisco",
                "fileshelf" => "Parked files",
                "links" => "Quick Bookmarks",
                "focus" => "Focus & Pomodoro",
                "clipboard" => "Clipboard History",
                "notes" => "Scratchpad",
                "screentime" => "Activity tracker",
                _ => "WindowsBuddy Capsule"
            };

            RebuildNavTabs();

            _viewportHost.Child = tool.BuildView();
            tool.OnActivated();
        }

        private void OnMorphTick(object sender, EventArgs e)
        {
            double step = _targetProgress > _morphProgress ? 0.14 : 0.18;
            if (Math.Abs(_targetProgress - _morphProgress) <= step)
            {
                _morphProgress = _targetProgress;
                _morphTimer.Stop();
                if (_morphProgress <= 0.001)
                {
                    _expandedContent.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _morphProgress += Math.Sign(_targetProgress - _morphProgress) * step;
            }

            UpdateMorphVisuals(_morphProgress);
        }

        private void UpdateMorphVisuals(double progress)
        {
            double t = Math.Clamp(progress, 0.0, 1.0);
            double ease = 1.0 - Math.Pow(1.0 - t, 3);

            double curW = CollapsedWidth + (ExpandedWidth - CollapsedWidth) * ease;
            double curH = CollapsedHeight + (ExpandedHeight - CollapsedHeight) * ease;

            _capsuleCard.Width = curW;
            _capsuleCard.Height = curH;
            _capsuleCard.Cursor = t > 0.5 ? Cursors.Arrow : Cursors.Hand;

            string style = AppPreferences.Instance.NotchSilhouette;
            var geo = NotchGeometryBuilder.BuildMorphedGeometry(
                style,
                CollapsedWidth,
                CollapsedHeight,
                ExpandedWidth,
                ExpandedHeight,
                t);

            _notchBackdropPath.Data = geo;
            _notchOutlinePath.Data = geo;
            _edgeLight.UpdateGeometry(geo, t > 0.5);

            _collapsedContent.Opacity = Math.Clamp(1.0 - t * 2.5, 0.0, 1.0);
            _collapsedContent.IsHitTestVisible = t < 0.25;

            _expandedContent.Opacity = Math.Clamp((t - 0.25) / 0.75, 0.0, 1.0);
            _expandedContent.IsHitTestVisible = t > 0.6;
        }

        private void ApplyThemeAndGeometry()
        {
            var p = CapsuleTheme.Current;
            _notchBackdropPath.Fill = CapsuleTheme.Brush(p.Bg);
            _notchOutlinePath.Stroke = CapsuleTheme.Brush(p.BorderStrong);
            _headerTitleText.Foreground = CapsuleTheme.Brush(p.Text);
            _headerSubtitleText.Foreground = CapsuleTheme.Brush(p.Text2);
            _collapsedClockText.Foreground = CapsuleTheme.Brush(p.Text);

            RebuildNavTabs();
            NavigateToTool(_activeToolId);
            UpdateMorphVisuals(_morphProgress);
        }

        public void ToggleFromExternal() => _machine.Toggle("tray-or-cli");
    }
}
