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
    /// Dynamic Island Collapsed Modes:
    /// - Resting Clock & Battery: 228 x 35
    /// - Live Music Playback (Spotify / Web / Player): 290 x 35
    /// - Live Notification Banner (Copied Image/Text, Bluetooth Headset, Screenshot): 340 x 38
    /// - Expanded Multi-tool View: 752 x 352
    /// </summary>
    public sealed class CapsuleWindow : Window
    {
        public const double CollapsedWidth = 228.0;
        public const double CollapsedHeight = 35.0;
        public const double MediaCollapsedWidth = 336.0;
        public const double MediaCollapsedHeight = 46.0;
        public const double BannerCollapsedWidth = 340.0;
        public const double BannerCollapsedHeight = 38.0;
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

        // 1. Collapsed Resting Normal View
        private readonly Grid _collapsedNormalView;
        private readonly TextBlock _collapsedClockText;
        private readonly TextBlock _collapsedBatteryText;
        private readonly StaticBeatBars _collapsedBeatBars;
        private readonly PetView _collapsedMiniPet;

        // 2. Collapsed Live Media View (Music Playing - Exact match to user screenshot)
        private readonly Grid _collapsedMediaView;
        private readonly AlbumArtThumbnail _albumArtThumb;
        private readonly TextBlock _mediaTrackText;
        private readonly TextBlock _mediaArtistText;
        private readonly MiniEqualizerControl _miniEqualizer;
        private readonly WaveformProgressBar _waveformProgress;

        // 3. Collapsed Dynamic Banner View (Copied, Screenshot, Bluetooth)
        private readonly Grid _collapsedBannerView;
        private readonly TextBlock _bannerIcon;
        private readonly TextBlock _bannerTitle;
        private readonly TextBlock _bannerSubtitle;

        private readonly DispatcherTimer _morphTimer;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _bannerDismissTimer;
        private double _morphProgress; // 0.0 = Collapsed, 1.0 = Expanded
        private double _targetProgress;
        private string _activeToolId = "home";

        private bool _isBannerActive = false;
        private bool _isMusicActive = false;
        private double _currentBaseCollapsedWidth = CollapsedWidth;
        private double _currentBaseCollapsedHeight = CollapsedHeight;

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
            // Collapsed Content Root (Contains Normal, Media & Banner views)
            // -------------------------------------------------------------
            _collapsedContent = new Grid
            {
                Margin = new Thickness(EarUnitW + 8, 2, EarUnitW + 8, 2),
                VerticalAlignment = VerticalAlignment.Center
            };

            // View 1: Normal resting collapsed view
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
                Foreground = CapsuleTheme.Brush(Color.FromRgb(48, 209, 88)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedBatteryText, 4);
            _collapsedNormalView.Children.Add(_collapsedBatteryText);
            _collapsedContent.Children.Add(_collapsedNormalView);

            // View 2: Live Media Playback View (matching the exact user screenshot)
            _collapsedMediaView = new Grid
            {
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 1)
            };
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Album Art + Track Title - Artist + Mini Equalizer (I · II)
            var topRowDock = new DockPanel { Margin = new Thickness(1, 0, 1, 1) };

            _miniEqualizer = new MiniEqualizerControl { Cursor = Cursors.Hand, ToolTip = "Play / Pause" };
            _miniEqualizer.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                MediaService.Instance.TogglePlayPause();
            };
            DockPanel.SetDock(_miniEqualizer, Dock.Right);
            topRowDock.Children.Add(_miniEqualizer);

            var leftInfo = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _albumArtThumb = new AlbumArtThumbnail();
            leftInfo.Children.Add(_albumArtThumb);

            _mediaTrackText = new TextBlock
            {
                Text = "Future Calling",
                FontSize = 11.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                MaxWidth = 150,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftInfo.Children.Add(_mediaTrackText);

            leftInfo.Children.Add(new TextBlock
            {
                Text = " - ",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });

            _mediaArtistText = new TextBlock
            {
                Text = "ZOYA",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                MaxWidth = 110,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftInfo.Children.Add(_mediaArtistText);

            topRowDock.Children.Add(leftInfo);
            Grid.SetRow(topRowDock, 0);
            _collapsedMediaView.Children.Add(topRowDock);

            // Row 1: Full-width Waveform Scrobbler
            _waveformProgress = new WaveformProgressBar();
            Grid.SetRow(_waveformProgress, 1);
            _collapsedMediaView.Children.Add(_waveformProgress);

            _collapsedContent.Children.Add(_collapsedMediaView);

            // View 3: Dynamic Banner View (Screenshot 2: Copied, Screenshot saved, AirPods Pro Connected)
            _collapsedBannerView = new Grid { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            var bannerDock = new DockPanel();

            _bannerIcon = new TextBlock
            {
                Text = "📋",
                FontSize = 13.5,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_bannerIcon, Dock.Left);
            bannerDock.Children.Add(_bannerIcon);

            var bannerTextStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _bannerTitle = new TextBlock
            {
                Text = "Copied",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            _bannerSubtitle = new TextBlock
            {
                Text = " · Image",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 220,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            bannerTextStack.Children.Add(_bannerTitle);
            bannerTextStack.Children.Add(_bannerSubtitle);
            bannerDock.Children.Add(bannerTextStack);
            _collapsedBannerView.Children.Add(bannerDock);
            _collapsedContent.Children.Add(_collapsedBannerView);

            // -------------------------------------------------------------
            // Expanded Content (752 x 352)
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

            // TOP HEADER BAR
            var headerBar = new DockPanel { Margin = new Thickness(2, 0, 2, 8) };

            var leftBrand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

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

            var themeBtn = CreateHeaderIconButton("🎨", $"Theme: {CapsuleTheme.Current.DisplayName} (Click to switch)");
            themeBtn.MouseLeftButtonUp += (_, _) =>
            {
                CapsuleTheme.CycleTheme();
                themeBtn.ToolTip = $"Theme: {CapsuleTheme.Current.DisplayName} (Click to switch)";
            };
            rightActions.Children.Add(themeBtn);

            var settingsBtn = CreateHeaderIconButton("⚙", "Settings");
            settingsBtn.MouseLeftButtonUp += (_, _) => SettingsWindow.ShowSingleton();
            rightActions.Children.Add(settingsBtn);

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

            // Bottom Navigation Dock Bar
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

            // Morph Animation timer
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

                if (_isMusicActive)
                {
                    double pos = MediaService.Instance.State.PositionSeconds;
                    double dur = Math.Max(1.0, MediaService.Instance.State.DurationSeconds);
                    _waveformProgress.SetPlayback(pos / dur, _isMusicActive);
                }
            };
            _clockTimer.Start();

            // Notification Banner Dismissal Timer
            _bannerDismissTimer = new DispatcherTimer();
            _bannerDismissTimer.Tick += (_, _) =>
            {
                _bannerDismissTimer.Stop();
                _isBannerActive = false;
                UpdateCollapsedViewState();
            };

            // Hook Real-time Notch Action Events (Real Clipboard, Bluetooth & Media!)
            var actionService = RealtimeNotchActionService.Instance;
            actionService.NotificationRequested += OnRealNotificationRequested;
            actionService.MediaTrackChanged += OnRealMediaTrackChanged;

            CapsuleTheme.ThemeChanged += ApplyThemeAndGeometry;
            AppPreferences.Instance.Changed += ApplyThemeAndGeometry;

            NavigateToTool("home");
            UpdateMorphVisuals(0.0);
        }

        #region Real-Time Action Handlers

        private void OnRealNotificationRequested(NotchNotificationPayload payload)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _isBannerActive = true;
                _bannerIcon.Text = payload.Icon;
                _bannerTitle.Text = payload.Title;
                _bannerSubtitle.Text = payload.Subtitle;

                // Display banner view
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;

                _currentBaseCollapsedWidth = BannerCollapsedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;

                if (_morphProgress <= 0.1)
                {
                    _targetProgress = 0.0;
                    _morphTimer.Start();
                }

                _bannerDismissTimer.Interval = TimeSpan.FromMilliseconds(payload.DurationMs);
                _bannerDismissTimer.Stop();
                _bannerDismissTimer.Start();
            });
        }

        private void OnRealMediaTrackChanged(string track, string artist, bool isPlaying)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _isMusicActive = isPlaying;
                _mediaTrackText.Text = string.IsNullOrWhiteSpace(track) ? "Future Calling" : track;
                _mediaArtistText.Text = string.IsNullOrWhiteSpace(artist) ? "ZOYA" : artist;
                _miniEqualizer.IsPlaying = isPlaying;
                _albumArtThumb.SetArtwork(null, string.IsNullOrWhiteSpace(artist) ? "ZOYA" : artist);
                _waveformProgress.SetPlayback(MediaService.Instance.State.PositionSeconds / Math.Max(1.0, MediaService.Instance.State.DurationSeconds), isPlaying);

                if (!_isBannerActive)
                {
                    UpdateCollapsedViewState();
                }
            });
        }

        private void UpdateCollapsedViewState()
        {
            if (_isBannerActive)
            {
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = BannerCollapsedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;
            }
            else if (_isMusicActive)
            {
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = MediaCollapsedWidth;
                _currentBaseCollapsedHeight = MediaCollapsedHeight;
            }
            else
            {
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = CollapsedWidth;
                _currentBaseCollapsedHeight = CollapsedHeight;
            }

            if (_morphProgress <= 0.1)
            {
                _targetProgress = 0.0;
                _morphTimer.Start();
            }
        }

        #endregion

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
                RealtimeNotchActionService.Instance.HandleClipboardUpdate();
                ClipboardTool.CaptureCurrentClipboard();
            }
            else if (msg == NativeMethods.WM_DEVICECHANGE)
            {
                RealtimeNotchActionService.Instance.HandleDeviceChange((int)wParam);
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

                var iconText = new TextBlock
                {
                    Text = tool.IconGlyph,
                    FontSize = 12,
                    Foreground = CapsuleTheme.Brush(isActive ? (p.IsLight ? p.Text : Colors.White) : p.IconMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemContainer.Children.Add(iconText);

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

            double baseW = _currentBaseCollapsedWidth;
            double baseH = _currentBaseCollapsedHeight;

            double curW = baseW + (ExpandedWidth - baseW) * ease;
            double curH = baseH + (ExpandedHeight - baseH) * ease;

            _capsuleCard.Width = curW;
            _capsuleCard.Height = curH;
            _capsuleCard.Cursor = t > 0.5 ? Cursors.Arrow : Cursors.Hand;

            string style = AppPreferences.Instance.NotchSilhouette;
            var geo = NotchGeometryBuilder.BuildMorphedGeometry(
                style,
                baseW,
                baseH,
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
