using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
    /// - Live Music Playback (Spotify / Web / Player): 308 x 45
    /// - Live Notification Banner (Copied Image/Text, Bluetooth Headset, Screenshot): 330 x 38
    /// - Expanded Multi-tool View: 752 x 352
    /// </summary>
    public sealed class CapsuleWindow : Window
    {
        public const double CollapsedWidth = 228.0;
        public const double CollapsedHeight = 35.0;
        public const double MediaCollapsedWidth = 296.0;
        public const double MediaCollapsedHeight = 45.0;
        public const double MediaWideCollapsedWidth = 380.0;
        public const double MediaWideCollapsedHeight = 75.0;
        public const double MediaWideDurationSeconds = 4.0;
        public const double BannerCollapsedWidth = 330.0;
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
        private readonly Line _notchTopBlackLine;
        private readonly EdgeLightWpf _edgeLight;
        private readonly Grid _collapsedContent;
        private readonly Grid _expandedContent;
        private readonly Border _viewportHost;
        private readonly StackPanel _navTabsPanel;
        private readonly Dictionary<string, FrameworkElement> _dockButtons = new();

        private readonly TextBlock _headerTitleText;
        private readonly TextBlock _headerSubtitleText;
        private readonly TextBlock _headerBatteryText;
        private readonly StackPanel _toolHeaderActions = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };

        // 1. Collapsed Resting Normal View (matching video 00:00-00:02: "8:51 PM" + [Green Battery] "100")
        private readonly Grid _collapsedNormalView;
        private readonly TextBlock _collapsedClockText;
        private readonly BatteryStatusGlyph _collapsedBatteryGlyph;
        private readonly TextBlock _collapsedBatteryText;

        // 2A. Collapsed Compact Live Media View (296 x 45 - Normal Music Playing)
        private readonly Grid _collapsedMediaView;
        private readonly AlbumArtThumbnail _albumArtThumb;
        private readonly TextBlock _mediaTrackText;
        private readonly TextBlock _mediaArtistText;
        private readonly MiniEqualizerControl _miniEqualizer;
        private readonly WaveformProgressBar _waveformProgress;

        // 2B. Collapsed Wide Live Media Card (380 x 75 - Track Switch / Next Song 4s Popup)
        private readonly Grid _collapsedMediaWideView;
        private readonly AlbumArtThumbnail _wideAlbumArtThumb;
        private readonly TextBlock _wideMediaTrackText;
        private readonly TextBlock _wideMediaArtistText;
        private readonly Border _wideProgressTrack;
        private readonly Border _wideProgressFill;
        private readonly TextBlock _wideRemainingText;
        private readonly MediaTransportControlsBar _wideTransportControls;
        private readonly WaveformProgressBar _wideWaveformProgress;

        // 3. Collapsed Dynamic Banner View (Copied, Screenshot, Bluetooth)
        private readonly Grid _collapsedBannerView;
        private readonly TextBlock _bannerIcon;
        private readonly TextBlock _bannerTitle;
        private readonly TextBlock _bannerSubtitle;

        // 4. Collapsed Message Banner View (Flashed from Message tool)
        private readonly Grid _collapsedMessageView;
        private readonly TextBlock _messageNotchText;
        private readonly DispatcherTimer _messageDismissTimer;
        private DispatcherTimer _messageTypeOnTimer;
        private bool _isMessageActive = false;
        private bool _isMessagePinned = false;

        private readonly DispatcherTimer _morphTimer;
        private readonly DispatcherTimer _clockTimer;
        private readonly DispatcherTimer _spectrumTimer;
        private readonly DispatcherTimer _mediaWideDismissTimer;
        private readonly DispatcherTimer _bannerDismissTimer;
        private readonly float[] _fftBands28 = new float[AudioSpectrumService.Bands];
        private readonly float[] _mappedBars40 = new float[WaveformProgressBar.CompactBarCount];
        private readonly float[] _mappedBars56 = new float[WaveformProgressBar.WideBarCount];
        private readonly float[] _miniBars4 = new float[4];
        private double _spectrumPhase;

        private double _morphProgress; // 0.0 = Collapsed, 1.0 = Expanded
        private readonly System.Diagnostics.Stopwatch _morphStopwatch = new();
        private double _morphStartProgress;
        private double _morphTargetProgress;
        private double _morphDurationSeconds = 0.22;
        private bool _isMorphingActive;
        private DateTime _lastMorphTickUtc = DateTime.UtcNow;
        private string _activeToolId = "home";

        private bool _isBannerActive = false;
        private bool _isMusicActive = false;
        private bool _isMediaWideActive = false;
        private double _mediaPosSeconds = 0.0;
        private double _mediaDurSeconds = 202.0;
        private DateTime _lastMediaTimelineSyncUtc = DateTime.UtcNow;
        private double _currentBaseCollapsedWidth = CollapsedWidth;
        private double _currentBaseCollapsedHeight = CollapsedHeight;
        private double _displayedCollapsedWidth = CollapsedWidth;
        private double _displayedCollapsedHeight = CollapsedHeight;

        private bool _isTucked = false;
        private readonly TranslateTransform _capsuleCardTranslate = new(0, 0);

        public bool IsTucked => _isTucked;

        public CapsuleWindow()
        {
            ToolRegistry.RegisterDefaults();
            ToolRegistry.OnToolNavigationRequested = id => NavigateToTool(id);
            WindowsBuddy.Tools.Weather.WeatherService.Instance.WeatherUpdated += () =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (string.Equals(_activeToolId, "weather", StringComparison.OrdinalIgnoreCase))
                    {
                        _headerSubtitleText.Text = WindowsBuddy.Tools.Weather.WeatherService.Instance.Prefs.City;
                    }
                });
            };
            _machine = new NotchStateMachine();

            Title = "Mayli Notch";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            Width = StableEnvelopeWidth;
            Height = StableEnvelopeHeight;
            Icon = EmbeddedAssetLoader.LoadBitmap("Mayli.Brand.mark-128.png");

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
                Cursor = Cursors.Hand,
                RenderTransform = _capsuleCardTranslate
            };

            var p = CapsuleTheme.Current;

            _notchBackdropPath = new Path
            {
                Fill = CapsuleTheme.Brush(p.Bg)
            };

            _notchOutlinePath = new Path
            {
                Stroke = CapsuleTheme.Brush(p.BorderStrong),
                StrokeThickness = 1.0
            };

            // Pure black 1px top line along screen bezel - ensures the top flush edge is pitch-black and blends seamlessly into monitor bezel
            _notchTopBlackLine = new Line
            {
                X1 = 0,
                Y1 = 0,
                Y2 = 0,
                Stroke = Brushes.Black,
                StrokeThickness = 1.0,
                IsHitTestVisible = false
            };

            _edgeLight = new EdgeLightWpf();

            // -------------------------------------------------------------
            // Collapsed Content Root (Contains Normal, Media & Banner views)
            // -------------------------------------------------------------
            _collapsedContent = new Grid
            {
                Margin = new Thickness(EarUnitW + 10, 2, EarUnitW + 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };

            // View 1: Normal resting collapsed view ("8:51 PM" on left, [Green Battery] "100" on right)
            _collapsedNormalView = new Grid();
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedNormalView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _collapsedClockText = new TextBlock
            {
                Text = DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedClockText, 0);
            _collapsedNormalView.Children.Add(_collapsedClockText);

            var collapsedOrb = new CuteOrbPetView(initialMood: OrbMood.Idle, size: 21)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Mayli Notch Buddy ✨ (Click to emote / کلیک کنید برای تغییر حالت)"
            };
            collapsedOrb.Clicked += () => PetEngine.Instance.CycleNextMood();
            Grid.SetColumn(collapsedOrb, 1);
            _collapsedNormalView.Children.Add(collapsedOrb);

            _collapsedBatteryGlyph = new BatteryStatusGlyph();
            _collapsedBatteryGlyph.SetBattery(SystemMonitorService.Current.BatteryPercent);
            Grid.SetColumn(_collapsedBatteryGlyph, 2);
            _collapsedNormalView.Children.Add(_collapsedBatteryGlyph);

            _collapsedBatteryText = new TextBlock
            {
                Text = $"{SystemMonitorService.Current.BatteryPercent}",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedBatteryText, 3);
            _collapsedNormalView.Children.Add(_collapsedBatteryText);
            _collapsedContent.Children.Add(_collapsedNormalView);

            // View 2A: Compact Live Media Playback View (296 x 45, matching video 00:00-00:06 & 00:11-00:19)
            _collapsedMediaView = new Grid
            {
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 1),
                RenderTransform = new TranslateTransform(0, 0)
            };
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Album Art + Track Title - Artist + 4-bar Mini Equalizer
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
            _albumArtThumb = new AlbumArtThumbnail(24.0, 6.0);
            leftInfo.Children.Add(_albumArtThumb);

            _mediaTrackText = new TextBlock
            {
                Text = "Future Calling",
                FontSize = 12.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                MaxWidth = 135,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftInfo.Children.Add(_mediaTrackText);

            leftInfo.Children.Add(new TextBlock
            {
                Text = " - ",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });

            _mediaArtistText = new TextBlock
            {
                Text = "ZOYA",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                MaxWidth = 90,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftInfo.Children.Add(_mediaArtistText);

            topRowDock.Children.Add(leftInfo);
            Grid.SetRow(topRowDock, 0);
            _collapsedMediaView.Children.Add(topRowDock);

            // Row 1: Full-width 40-bar Symmetrical Audio Spectrum
            _waveformProgress = new WaveformProgressBar(WaveformProgressBar.CompactBarCount);
            Grid.SetRow(_waveformProgress, 1);
            _collapsedMediaView.Children.Add(_waveformProgress);

            // Right-clicking compact media pill shows the 4-second Wide Music Card on demand
            _collapsedMediaView.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                ActivateMediaWideCard();
            };

            _collapsedContent.Children.Add(_collapsedMediaView);

            // View 2B: Wide Music Card on Track Switch / Next Song (380 x 75 for 4.0s, matching video 00:07-00:10)
            _collapsedMediaWideView = new Grid
            {
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 2, 2, 1),
                RenderTransform = new TranslateTransform(0, 0)
            };
            _collapsedMediaWideView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _collapsedMediaWideView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var wideTopGrid = new Grid { Margin = new Thickness(1, 1, 1, 4) };
            wideTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wideTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wideTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _wideAlbumArtThumb = new AlbumArtThumbnail(44.0, 10.0)
            {
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_wideAlbumArtThumb, 0);
            wideTopGrid.Children.Add(_wideAlbumArtThumb);

            var wideCenterStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };

            _wideMediaTrackText = new TextBlock
            {
                Text = "Nobody Else",
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            wideCenterStack.Children.Add(_wideMediaTrackText);

            _wideMediaArtistText = new TextBlock
            {
                Text = "Moeaike",
                FontSize = 11.0,
                Foreground = new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                Margin = new Thickness(0, 1, 0, 6),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            wideCenterStack.Children.Add(_wideMediaArtistText);

            // Horizontal Progress Scrubber Bar (White filled left + dark translucent track right)
            _wideProgressTrack = new Border
            {
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Background = new SolidColorBrush(Color.FromArgb(62, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 4, 0)
            };
            _wideProgressFill = new Border
            {
                Height = 3.5,
                Width = 28.0,
                CornerRadius = new CornerRadius(1.75),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _wideProgressTrack.Child = _wideProgressFill;
            _wideProgressTrack.SizeChanged += (_, _) => UpdateMediaTimelineVisuals();
            wideCenterStack.Children.Add(_wideProgressTrack);

            Grid.SetColumn(wideCenterStack, 1);
            wideTopGrid.Children.Add(wideCenterStack);

            var wideRightStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            _wideRemainingText = new TextBlock
            {
                Text = "-3:22",
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(152, 152, 160)),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 2, 4)
            };
            wideRightStack.Children.Add(_wideRemainingText);

            _wideTransportControls = new MediaTransportControlsBar(
                onPrev: () =>
                {
                    ResetMediaWideTimer();
                    MediaService.Instance.PreviousTrack();
                },
                onPlayPause: () =>
                {
                    ResetMediaWideTimer();
                    MediaService.Instance.TogglePlayPause();
                },
                onNext: () =>
                {
                    ResetMediaWideTimer();
                    MediaService.Instance.NextTrack();
                })
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };
            wideRightStack.Children.Add(_wideTransportControls);

            Grid.SetColumn(wideRightStack, 2);
            wideTopGrid.Children.Add(wideRightStack);

            Grid.SetRow(wideTopGrid, 0);
            _collapsedMediaWideView.Children.Add(wideTopGrid);

            // Row 1: Full-width 56-bar Symmetrical Audio Spectrum
            _wideWaveformProgress = new WaveformProgressBar(WaveformProgressBar.WideBarCount);
            Grid.SetRow(_wideWaveformProgress, 1);
            _collapsedMediaWideView.Children.Add(_wideWaveformProgress);

            _collapsedContent.Children.Add(_collapsedMediaWideView);

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

            // View 4: Collapsed Message Banner View (Flashed from Message tool)
            _collapsedMessageView = new Grid { Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
            var msgDock = new DockPanel();
            var msgIcon = new TextBlock
            {
                Text = "⚡",
                FontSize = 11.5,
                Foreground = new SolidColorBrush(Color.FromRgb(245, 166, 35)),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(msgIcon, Dock.Left);
            msgDock.Children.Add(msgIcon);

            _messageNotchText = new TextBlock
            {
                Text = "",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 400
            };
            msgDock.Children.Add(_messageNotchText);
            _collapsedMessageView.Children.Add(msgDock);
            _collapsedContent.Children.Add(_collapsedMessageView);

            // -------------------------------------------------------------
            // Expanded Content (752 x 352) - fixed size avoids per-frame reflow of tools during morph
            // -------------------------------------------------------------
            _expandedContent = new Grid
            {
                Width = ExpandedWidth - (EarUnitW + 12) * 2,
                Height = ExpandedHeight - 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 0, 12),
                Opacity = 0.0,
                Visibility = Visibility.Collapsed
            };
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // TOP HEADER BAR
            var headerBar = new DockPanel { Margin = new Thickness(2, 0, 2, 8) };

            var leftBrand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var headerOrb = new CuteOrbPetView(initialMood: OrbMood.Idle, size: 22)
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Mayli Companion Orb ✨ (Click to emote / کلیک کنید برای تغییر حالت)"
            };
            headerOrb.Clicked += () => PetEngine.Instance.CycleNextMood();
            leftBrand.Children.Add(headerOrb);

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

            // Right header actions: Tool Actions + Battery + Theme Switcher + Settings + Close
            var rightActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rightActions.Children.Add(_toolHeaderActions);

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
            _capsuleCard.Children.Add(_notchTopBlackLine);
            _capsuleCard.Children.Add(_edgeLight);
            _capsuleCard.Children.Add(_collapsedContent);
            _capsuleCard.Children.Add(_expandedContent);

            _envelopeRoot.Children.Add(_capsuleCard);
            Content = _envelopeRoot;

            // High-precision morph timer running at render priority
            _morphTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(10) };
            _morphTimer.Tick += OnMorphTick;

            _machine.PhaseChanged += (_, next, _) =>
            {
                if (next == NotchPhase.Opening || next == NotchPhase.Open)
                {
                    StartMorphAnimation(1.0);
                }
                else if (next == NotchPhase.Closing || next == NotchPhase.Resting)
                {
                    StartMorphAnimation(0.0);
                }
            };

            _capsuleCard.MouseEnter += (_, _) =>
            {
                if (AppPreferences.Instance.OpensOnClick) return;
                _machine.PointerMoved(true, "mouse-enter");
            };
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
                string timeStr;
                if (AppPreferences.Instance.TimeFormat == "24-hour")
                    timeStr = DateTime.Now.ToString("HH:mm");
                else if (AppPreferences.Instance.TimeFormat == "12-hour")
                    timeStr = DateTime.Now.ToString("h:mm tt");
                else
                    timeStr = DateTime.Now.ToString("t");

                if (AppPreferences.Instance.ShowClosedDate)
                    _collapsedClockText.Text = $"{DateTime.Now:ddd d} · {timeStr}";
                else
                    _collapsedClockText.Text = timeStr;

                _collapsedClockText.Visibility = AppPreferences.Instance.ShowClosedTime ? Visibility.Visible : Visibility.Collapsed;

                var sys = SystemMonitorService.Current;
                _collapsedBatteryGlyph.SetBattery(sys.BatteryPercent);
                _collapsedBatteryText.Text = $"{sys.BatteryPercent}";
                _headerBatteryText.Text = $"{sys.BatteryPercent}% ⚡";

                Visibility batVis = AppPreferences.Instance.ShowClosedBattery ? Visibility.Visible : Visibility.Collapsed;
                _collapsedBatteryGlyph.Visibility = batVis;
                _collapsedBatteryText.Visibility = batVis;

                if (_isMusicActive)
                {
                    UpdateMediaTimelineVisuals();
                }

                // Ensure notch stays on top layer above all windows when not tucked away
                if (!_isTucked)
                {
                    EnsureTopmost();
                }
            };
            _clockTimer.Start();

            // 30fps Real-time Spectrum & Equalizer Animation Timer
            _spectrumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _spectrumTimer.Tick += (_, _) =>
            {
                if (!_isMusicActive || _morphProgress > 0.4) return;
                _spectrumPhase += 0.085;

                AudioSpectrumService.Instance.Read(_fftBands28, out bool isAudioLive);
                AudioSpectrumService.ComputeMiniBars(_fftBands28, _miniBars4, _spectrumPhase);
                _miniEqualizer.SetLevels(_miniBars4, _isMusicActive);

                if (_isMediaWideActive)
                {
                    AudioSpectrumService.MapBands(_fftBands28, _mappedBars56, WaveformProgressBar.WideBarCount);
                    _wideWaveformProgress.SetSpectrumLevels(_mappedBars56, _isMusicActive, isAudioLive, _spectrumPhase);
                }
                else
                {
                    AudioSpectrumService.MapBands(_fftBands28, _mappedBars40, WaveformProgressBar.CompactBarCount);
                    _waveformProgress.SetSpectrumLevels(_mappedBars40, _isMusicActive, isAudioLive, _spectrumPhase);
                }
            };
            _spectrumTimer.Start();

            // 4-Second Wide Music Card Dismissal Timer (Track Switch / Next Song Popup)
            _mediaWideDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(MediaWideDurationSeconds) };
            _mediaWideDismissTimer.Tick += (_, _) =>
            {
                _mediaWideDismissTimer.Stop();
                if (_isMediaWideActive)
                {
                    _isMediaWideActive = false;
                    UpdateCollapsedViewState(animateCompactEntrance: true);
                }
            };

            // Notification Banner Dismissal Timer
            _bannerDismissTimer = new DispatcherTimer();
            _bannerDismissTimer.Tick += (_, _) =>
            {
                _bannerDismissTimer.Stop();
                _isBannerActive = false;
                if (_isMessagePinned)
                {
                    _isMessageActive = true;
                }
                UpdateCollapsedViewState();
            };

            // Message Banner Dismissal Timer
            _messageDismissTimer = new DispatcherTimer();
            _messageDismissTimer.Tick += (_, _) => OnMessageDismissed();

            // Hook Message Tool Flash and TakeDown
            var msgService = WindowsBuddy.Tools.Message.MessageService.Instance;
            msgService.FlashRequested += OnMessageFlashRequested;
            msgService.TakeDownRequested += OnMessageTakeDownRequested;

            // Hook Real-time Notch Action Events (Real Clipboard, Bluetooth & Media!)
            var actionService = RealtimeNotchActionService.Instance;
            actionService.NotificationRequested += OnRealNotificationRequested;
            actionService.MediaSessionChanged += OnRealMediaSessionChanged;

            CapsuleTheme.ThemeChanged += ApplyThemeAndGeometry;
            AppPreferences.Instance.Changed += ApplyThemeAndGeometry;

            // Alt+1 .. Alt+0, Alt+M, Alt+S Navigation Shortcuts
            PreviewKeyDown += (_, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                {
                    string target = (e.SystemKey != Key.None ? e.SystemKey : e.Key) switch
                    {
                        Key.D1 => "home",
                        Key.D2 => "system",
                        Key.D3 => "clipboard",
                        Key.D4 => "notes",
                        Key.D5 => "focus",
                        Key.D6 => "fileshelf",
                        Key.D7 => "weather",
                        Key.D8 => "clock",
                        Key.D9 => "units",
                        Key.D0 => "links",
                        Key.M => "message",
                        Key.S => "screentime",
                        _ => null
                    };
                    if (target != null)
                    {
                        NavigateToTool(target);
                        e.Handled = true;
                    }
                }
            };

            NavigateToTool("home");
            UpdateMorphVisuals(0.0, settled: true);
        }

        #region Real-Time Action Handlers

        private void OnRealNotificationRequested(NotchNotificationPayload payload)
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Live Activity filter checks
                if ((payload.Icon == "⚡" || payload.Icon == "🔋") && !AppPreferences.Instance.ShowBatteryActivity)
                    return;

                if (payload.Icon == "🔊" && !AppPreferences.Instance.VolumeActivity)
                    return;

                if ((payload.Icon == "📋" || payload.Icon == "📁") && !AppPreferences.Instance.ClipboardCopiedActivity)
                    return;

                if (payload.Icon == "⏱" && !AppPreferences.Instance.TimerFinishAlerts && !AppPreferences.Instance.RunningTimersActivity)
                    return;

                if (payload.Icon == "📅" && !AppPreferences.Instance.CalendarAlerts)
                    return;

                _isBannerActive = true;
                _bannerIcon.Text = payload.Icon;
                _bannerTitle.Text = payload.Title;
                _bannerSubtitle.Text = payload.Subtitle;

                // Display banner view
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;

                _currentBaseCollapsedWidth = BannerCollapsedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;

                if (_morphProgress <= 0.1)
                {
                    StartMorphAnimation(0.0);
                }

                int durationMs = AppPreferences.Instance.LiveActivityEventDuration switch
                {
                    "2 s" => 2000,
                    "5 s" => 5000,
                    _ => 3000
                };

                _bannerDismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
                _bannerDismissTimer.Stop();
                _bannerDismissTimer.Start();
            });
        }

        private void OnRealMediaSessionChanged(MediaSessionSnapshot snapshot)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (!AppPreferences.Instance.ShowMediaActivity)
                {
                    _isMusicActive = false;
                    _miniEqualizer.IsPlaying = false;
                    _wideTransportControls.SetPlaying(false);
                    return;
                }

                bool wasPlaying = _isMusicActive;
                _isMusicActive = snapshot.IsPlaying;
                string track = string.IsNullOrWhiteSpace(snapshot.Title) ? "Future Calling" : snapshot.Title;
                string artist = string.IsNullOrWhiteSpace(snapshot.Artist) ? "ZOYA" : snapshot.Artist;

                // Determine beat color based on user's Music color setting
                Color beatColor = snapshot.BeatColor;
                if (!string.Equals(AppPreferences.Instance.MusicBeatColor, "Artwork", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        beatColor = (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.MusicBeatColor);
                    }
                    catch { }
                }

                // Compact media view (296 x 45)
                _mediaTrackText.Text = track;
                _mediaArtistText.Text = artist;
                _miniEqualizer.IsPlaying = snapshot.IsPlaying;
                _miniEqualizer.SetBeatColor(beatColor);
                _waveformProgress.SetBeatColor(beatColor);
                _albumArtThumb.SetArtwork(snapshot.Artwork, artist);

                // Wide media card view (380 x 75)
                _wideMediaTrackText.Text = track;
                _wideMediaArtistText.Text = artist;
                _wideTransportControls.SetPlaying(snapshot.IsPlaying);
                _wideWaveformProgress.SetBeatColor(beatColor);
                _wideAlbumArtThumb.SetArtwork(snapshot.Artwork, artist);

                // Timeline sync for live remaining countdown (-3:22) and progress scrubber
                if (snapshot.DurationSeconds > 1.0)
                {
                    _mediaDurSeconds = snapshot.DurationSeconds;
                    _mediaPosSeconds = Math.Clamp(snapshot.PositionSeconds, 0.0, _mediaDurSeconds);
                    _lastMediaTimelineSyncUtc = DateTime.UtcNow;
                }
                else if (snapshot.IsTrackSwitch)
                {
                    _mediaDurSeconds = 202.0;
                    _mediaPosSeconds = 0.0;
                    _lastMediaTimelineSyncUtc = DateTime.UtcNow;
                }
                UpdateMediaTimelineVisuals();

                if (!_isMusicActive)
                {
                    _isMediaWideActive = false;
                    _mediaWideDismissTimer.Stop();
                }
                else if (snapshot.IsTrackSwitch)
                {
                    ActivateMediaWideCard();
                    return;
                }

                if (!_isBannerActive)
                {
                    UpdateCollapsedViewState(animateCompactEntrance: !wasPlaying && _isMusicActive && !_isMediaWideActive);
                }
            });
        }

        private void ActivateMediaWideCard()
        {
            if (!_isMusicActive) return;
            _isMediaWideActive = true;
            ResetMediaWideTimer();
            UpdateMediaTimelineVisuals();

            if (!_isBannerActive)
            {
                UpdateCollapsedViewState();
                AnimateViewEntrance(_collapsedMediaWideView, -7.0);
            }
        }

        private void ResetMediaWideTimer()
        {
            _mediaWideDismissTimer.Stop();
            _mediaWideDismissTimer.Interval = TimeSpan.FromSeconds(MediaWideDurationSeconds);
            _mediaWideDismissTimer.Start();
        }

        private void UpdateMediaTimelineVisuals()
        {
            double elapsedSinceSync = _isMusicActive
                ? Math.Max(0.0, (DateTime.UtcNow - _lastMediaTimelineSyncUtc).TotalSeconds)
                : 0.0;

            double dur = _mediaDurSeconds > 1.0 ? _mediaDurSeconds : 202.0;
            double pos = Math.Clamp(_mediaPosSeconds + elapsedSinceSync, 0.0, dur);
            double remaining = Math.Max(0.0, dur - pos);

            _wideRemainingText.Text = FormatRemaining(remaining);

            double trackWidth = _wideProgressTrack.ActualWidth > 10.0 ? _wideProgressTrack.ActualWidth : 156.0;
            double ratio = dur > 0.1 ? Math.Clamp(pos / dur, 0.04, 1.0) : 0.12;
            _wideProgressFill.Width = Math.Max(6.0, trackWidth * ratio);
        }

        private static string FormatRemaining(double remainingSeconds)
        {
            int totalSec = Math.Max(0, (int)Math.Round(remainingSeconds));
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            return $"-{mins}:{secs:D2}";
        }

        private static void AnimateViewEntrance(FrameworkElement view, double fromY)
        {
            if (view.RenderTransform is not TranslateTransform tt)
            {
                tt = new TranslateTransform(0, 0);
                view.RenderTransform = tt;
            }

            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var slideAnim = new DoubleAnimation(fromY, 0.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = ease
            };
            var fadeAnim = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = ease
            };

            tt.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            view.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void UpdateCollapsedViewState(bool animateCompactEntrance = false)
        {
            if (_isBannerActive)
            {
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = BannerCollapsedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;
            }
            else if (_isMessageActive)
            {
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Visible;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;
            }
            else if (_isMusicActive && _isMediaWideActive)
            {
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = MediaWideCollapsedWidth;
                _currentBaseCollapsedHeight = MediaWideCollapsedHeight;
            }
            else if (_isMusicActive)
            {
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = MediaCollapsedWidth;
                _currentBaseCollapsedHeight = MediaCollapsedHeight;

                if (animateCompactEntrance)
                {
                    AnimateViewEntrance(_collapsedMediaView, 5.0);
                }
            }
            else
            {
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = CollapsedWidth;
                _currentBaseCollapsedHeight = CollapsedHeight;
            }

            if (_morphProgress <= 0.1)
            {
                StartMorphAnimation(0.0);
            }
        }

        private void OnMessageFlashRequested(WindowsBuddy.Tools.Message.MessageSettings settings)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _isMessageActive = true;
                _isMessagePinned = settings.RepeatEnabled;

                string text = string.IsNullOrWhiteSpace(settings.Text) ? "Hello from Mayli!" : settings.Text;
                _messageNotchText.Text = text;
                _messageNotchText.Foreground = WindowsBuddy.Tools.Message.MessageRenderer.GetForegroundBrush(settings.ColorHex, settings.Finish);

                double estimatedWidth = Math.Clamp(text.Length * 8.2 + 80.0, 260.0, 480.0);
                _currentBaseCollapsedWidth = estimatedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;

                UpdateCollapsedViewState();

                WindowsBuddy.Tools.Message.MessageRenderer.AnimateElement(
                    _messageNotchText,
                    _messageNotchText,
                    text,
                    settings.Animation,
                    settings.Speed,
                    _messageTypeOnTimer,
                    out _messageTypeOnTimer);

                _messageDismissTimer.Stop();
                if (!settings.RepeatEnabled)
                {
                    _messageDismissTimer.Interval = settings.DurationSpan;
                    _messageDismissTimer.Start();
                }

                if (_morphProgress > 0.4)
                {
                    StartMorphAnimation(0.0);
                }
            });
        }

        private void OnMessageTakeDownRequested()
        {
            Dispatcher.InvokeAsync(() =>
            {
                _isMessageActive = false;
                _isMessagePinned = false;
                _messageDismissTimer.Stop();
                _messageTypeOnTimer?.Stop();
                UpdateCollapsedViewState();
            });
        }

        private void OnMessageDismissed()
        {
            _messageDismissTimer.Stop();
            _isMessageActive = false;
            _messageTypeOnTimer?.Stop();
            WindowsBuddy.Tools.Message.MessageService.Instance.NotifyNotchExpired();
            UpdateCollapsedViewState();
        }

        #endregion

        private static Border CreateHeaderIconButton(string glyph, string tooltip)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(3, 0, 3, 0),
                Child = new TextBlock
                {
                    Text = glyph,
                    FontSize = 13,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            border.MouseEnter += (_, _) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                if (border.Child is TextBlock tb) tb.Foreground = Brushes.White;
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
            HotkeyService.Instance.Pressed += () =>
            {
                if (_isTucked)
                {
                    SetTucked(false);
                }
                _machine.Toggle("hotkey");
            };
            HotkeyService.Instance.TuckPressed += () => ToggleTuck();

            EnsureTopmost();

            NativeMethods.AddClipboardFormatListener(hwnd);
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        }

        /// <summary>
        /// Enforces this window to be the very top window in the system Z-order above all applications.
        /// </summary>
        public void EnsureTopmost()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(
                    hwnd,
                    NativeMethods.HWND_TOPMOST,
                    0, 0, 0, 0,
                    NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// Toggles tucking the notch smoothly up out of view (or back down) via shortcut.
        /// When tucked up, it allows full, unobstructed access to whatever was underneath (browser tabs, window title bars, etc.).
        /// </summary>
        public void ToggleTuck() => SetTucked(!_isTucked);

        public void SetTucked(bool tucked)
        {
            if (_isTucked == tucked) return;
            _isTucked = tucked;

            double targetY = _isTucked ? -Math.Max(_displayedCollapsedHeight, 45.0) - 30.0 : 0.0;
            double targetOpacity = _isTucked ? 0.0 : 1.0;
            var duration = TimeSpan.FromMilliseconds(300);

            // Smooth cubic ease out on slide down (soft landing), smooth cubic ease in on slide up (clean lift)
            IEasingFunction ease = _isTucked
                ? new CubicEase { EasingMode = EasingMode.EaseIn }
                : new CubicEase { EasingMode = EasingMode.EaseOut };

            var slideAnim = new DoubleAnimation
            {
                To = targetY,
                Duration = duration,
                EasingFunction = ease
            };

            var fadeAnim = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = duration,
                EasingFunction = ease
            };

            if (_isTucked)
            {
                if (_morphProgress > 0.05)
                {
                    _machine.RequestClose("tuck");
                }

                slideAnim.Completed += (_, _) =>
                {
                    if (_isTucked)
                    {
                        _capsuleCard.IsHitTestVisible = false;
                        _capsuleCard.Visibility = Visibility.Collapsed;
                    }
                };
            }
            else
            {
                _capsuleCard.Visibility = Visibility.Visible;
                _capsuleCard.IsHitTestVisible = true;
                EnsureTopmost();
            }

            _capsuleCardTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _capsuleCard.BeginAnimation(OpacityProperty, fadeAnim);
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
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var targetScreen = System.Windows.Forms.Screen.PrimaryScreen;

                string choice = AppPreferences.Instance.NotchDisplay;
                if (string.Equals(choice, "Display 2", StringComparison.OrdinalIgnoreCase) && screens.Length > 1)
                {
                    targetScreen = screens[1];
                }
                else if (string.Equals(choice, "Display 1", StringComparison.OrdinalIgnoreCase) && screens.Length > 0)
                {
                    targetScreen = screens[0];
                }

                if (targetScreen != null)
                {
                    var bounds = targetScreen.Bounds;
                    Left = bounds.Left + (bounds.Width - StableEnvelopeWidth) / 2.0;
                    Top = bounds.Top;
                    return;
                }
            }
            catch { }

            double screenW = SystemParameters.PrimaryScreenWidth;
            Left = (screenW - StableEnvelopeWidth) / 2.0;
            Top = 0;
        }

        private void RebuildNavTabs()
        {
            _navTabsPanel.Children.Clear();
            _dockButtons.Clear();
            var p = CapsuleTheme.Current;

            // If current tool was disabled, fallback to home
            string activeCheckId = _activeToolId == "drop" || _activeToolId == "transfer" ? "maylidrop" : _activeToolId;
            if (!AppPreferences.Instance.IsModuleEnabled(activeCheckId, true) && !string.Equals(_activeToolId, "home", StringComparison.OrdinalIgnoreCase))
            {
                _activeToolId = "home";
                _viewportHost.Child = ToolRegistry.GetById("home").BuildView();
            }

            foreach (var tool in ToolRegistry.All)
            {
                string id = tool.Id;
                string checkId = id == "drop" || id == "transfer" ? "maylidrop" : id;
                if (!AppPreferences.Instance.IsModuleEnabled(checkId, true))
                    continue;

                bool isActive = string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase);

                var itemContainer = new Grid
                {
                    Width = 32,
                    Height = 30,
                    Cursor = Cursors.Hand,
                    ToolTip = tool.Id switch
                    {
                        "home" => "Home  Alt+1",
                        "system" => "System  Alt+2",
                        "clipboard" => "Clipboard  Alt+3",
                        "notes" => "Notes  Alt+4",
                        "focus" => "Timers  Alt+5",
                        "fileshelf" => "File Shelf  Alt+6",
                        "weather" => "Weather  Alt+7",
                        "clock" => "Clock  Alt+8",
                        "units" => "Units  Alt+9",
                        "links" => "Links  Alt+0",
                        "message" => "Message  Alt+M",
                        "screentime" => "Screen Time  Alt+S",
                        _ => tool.Title
                    }
                };

                var circleHighlight = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = isActive ? Brushes.White : Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                itemContainer.Children.Add(circleHighlight);

                var iconText = new TextBlock
                {
                    Text = tool.IconGlyph,
                    FontSize = 13,
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = isActive ? Brushes.Black : CapsuleTheme.Brush(p.IconMuted),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                };
                itemContainer.Children.Add(iconText);

                itemContainer.MouseEnter += (_, _) =>
                {
                    if (!string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase))
                    {
                        circleHighlight.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                        iconText.Foreground = Brushes.White;
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
                Height = 16,
                Background = CapsuleTheme.Brush(p.Border),
                Margin = new Thickness(4, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _navTabsPanel.Children.Add(divider);

            var plusBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = "Add / Customize Tools",
                Child = new TextBlock
                {
                    Text = "+",
                    FontSize = 14,
                    Foreground = CapsuleTheme.Brush(p.Text2),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            plusBtn.MouseEnter += (_, _) =>
            {
                plusBtn.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                if (plusBtn.Child is TextBlock tb) tb.Foreground = Brushes.White;
            };
            plusBtn.MouseLeave += (_, _) =>
            {
                plusBtn.Background = Brushes.Transparent;
                if (plusBtn.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Text2);
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
                "system" => "Power · Network · Audio",
                "weather" => WindowsBuddy.Tools.Weather.WeatherService.Instance.Prefs.City,
                "clock" => "Local Time & Date",
                "units" => "Conversion",
                "fileshelf" => "Parked files",
                "links" => "Quick Bookmarks",
                "message" => "Notch banner",
                "focus" => "Focus & intervals",
                "clipboard" => $"{WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.Items.Count} items",
                "notes" => $"{WindowsBuddy.Tools.Notes.NotesService.Instance.Notes.Count} notes",
                "screentime" => DateTime.Now.ToString("ddd, d MMM"),
                _ => "WindowsBuddy Capsule"
            };

            UpdateToolHeaderActions(tool.Id);
            RebuildNavTabs();

            _viewportHost.Child = tool.BuildView();
            tool.OnActivated();
        }

        private void UpdateToolHeaderActions(string toolId)
        {
            _toolHeaderActions.Children.Clear();
            if (string.Equals(toolId, "weather", StringComparison.OrdinalIgnoreCase))
            {
                var svc = WindowsBuddy.Tools.Weather.WeatherService.Instance;

                // 1. Unit toggle pill [ °C | °F ]
                var unitPill = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                    CornerRadius = new CornerRadius(12),
                    Height = 24,
                    Padding = new Thickness(2, 1, 2, 1),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var unitDock = new StackPanel { Orientation = Orientation.Horizontal };

                var cBtn = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(7, 2, 7, 2),
                    Background = !svc.UseFahrenheit ? new SolidColorBrush(Color.FromRgb(50, 50, 60)) : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = "°C",
                        FontSize = 11,
                        FontWeight = !svc.UseFahrenheit ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = !svc.UseFahrenheit ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                };

                var fBtn = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(7, 2, 7, 2),
                    Background = svc.UseFahrenheit ? new SolidColorBrush(Color.FromRgb(50, 50, 60)) : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = "°F",
                        FontSize = 11,
                        FontWeight = svc.UseFahrenheit ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = svc.UseFahrenheit ? Brushes.White : new SolidColorBrush(Color.FromRgb(142, 142, 147)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                };

                cBtn.MouseLeftButtonUp += (_, _) =>
                {
                    svc.UseFahrenheit = false;
                    UpdateToolHeaderActions("weather");
                };

                fBtn.MouseLeftButtonUp += (_, _) =>
                {
                    svc.UseFahrenheit = true;
                    UpdateToolHeaderActions("weather");
                };

                unitDock.Children.Add(cBtn);
                unitDock.Children.Add(fBtn);
                unitPill.Child = unitDock;
                _toolHeaderActions.Children.Add(unitPill);

                // 2. Change City Pill Button
                var cityBtn = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(28, 28, 34)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(46, 46, 56)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Height = 24,
                    Padding = new Thickness(8, 2, 10, 2),
                    Margin = new Thickness(6, 0, 0, 0),
                    Cursor = Cursors.Hand,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var citySp = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                citySp.Children.Add(new TextBlock
                {
                    Text = "📍",
                    FontSize = 10.5,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                });
                citySp.Children.Add(new TextBlock
                {
                    Text = "Change City",
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    VerticalAlignment = VerticalAlignment.Center
                });
                cityBtn.Child = citySp;

                cityBtn.MouseEnter += (_, _) => cityBtn.Background = new SolidColorBrush(Color.FromRgb(42, 42, 52));
                cityBtn.MouseLeave += (_, _) => cityBtn.Background = new SolidColorBrush(Color.FromRgb(28, 28, 34));
                cityBtn.MouseLeftButtonUp += (_, _) =>
                {
                    WindowsBuddy.Tools.Weather.WeatherToolView.Instance?.ShowCityPicker();
                };

                _toolHeaderActions.Children.Add(cityBtn);
            }
        }

        private void StartMorphAnimation(double targetProgress)
        {
            targetProgress = Math.Clamp(targetProgress, 0.0, 1.0);
            if (Math.Abs(_morphTargetProgress - targetProgress) < 0.001 && _isMorphingActive)
                return;

            _morphStartProgress = _morphProgress;
            _morphTargetProgress = targetProgress;

            double diff = Math.Abs(_morphTargetProgress - _morphStartProgress);
            if (diff < 0.005)
            {
                _morphProgress = _morphTargetProgress;
                _isMorphingActive = false;
                _morphStopwatch.Stop();
                _morphTimer.Stop();
                if (_morphProgress <= 0.001)
                {
                    _expandedContent.Visibility = Visibility.Collapsed;
                }
                _edgeLight.SetMorphing(false);
                UpdateMorphVisuals(_morphProgress, settled: true);
                return;
            }

            // Exactly 0.22s to open, 0.18s to close, proportional to remaining distance
            double fullDuration = _morphTargetProgress > _morphStartProgress ? 0.22 : 0.18;
            _morphDurationSeconds = Math.Max(0.06, fullDuration * diff);

            _morphStopwatch.Restart();
            _isMorphingActive = true;
            _lastMorphTickUtc = DateTime.UtcNow;
            _edgeLight.SetMorphing(true);

            if (_morphTargetProgress > 0.5)
            {
                _expandedContent.Visibility = Visibility.Visible;
            }

            _morphTimer.Start();
        }

        private void OnMorphTick(object sender, EventArgs e)
        {
            DateTime now = DateTime.UtcNow;
            double dt = Math.Clamp((now - _lastMorphTickUtc).TotalSeconds, 0.001, 0.05);
            _lastMorphTickUtc = now;

            bool morphSettled = !_isMorphingActive;
            if (_isMorphingActive)
            {
                double elapsed = _morphStopwatch.Elapsed.TotalSeconds;
                double rawT = _morphDurationSeconds > 0.001 ? Math.Clamp(elapsed / _morphDurationSeconds, 0.0, 1.0) : 1.0;

                _morphProgress = _morphStartProgress + (_morphTargetProgress - _morphStartProgress) * rawT;

                if (rawT >= 1.0)
                {
                    _morphProgress = _morphTargetProgress;
                    _isMorphingActive = false;
                    _morphStopwatch.Stop();
                    morphSettled = true;
                    if (_morphProgress <= 0.001)
                    {
                        _expandedContent.Visibility = Visibility.Collapsed;
                    }
                    _edgeLight.SetMorphing(false);
                }
            }

            // Smoothly interpolate collapsed pill width & height with time-independent exponential decay
            double dw = _currentBaseCollapsedWidth - _displayedCollapsedWidth;
            double dh = _currentBaseCollapsedHeight - _displayedCollapsedHeight;
            bool sizeSettled = Math.Abs(dw) < 0.5 && Math.Abs(dh) < 0.4;

            if (sizeSettled)
            {
                _displayedCollapsedWidth = _currentBaseCollapsedWidth;
                _displayedCollapsedHeight = _currentBaseCollapsedHeight;
            }
            else
            {
                double decay = 1.0 - Math.Exp(-22.0 * dt);
                _displayedCollapsedWidth += dw * decay;
                _displayedCollapsedHeight += dh * decay;
            }

            if (morphSettled && sizeSettled)
            {
                _morphTimer.Stop();
            }

            UpdateMorphVisuals(_morphProgress, morphSettled && sizeSettled);
        }

        private void UpdateMorphVisuals(double progress, bool settled = false)
        {
            double t = Math.Clamp(progress, 0.0, 1.0);
            // Cubic Ease Out: starts fast & responsive, settles silky smooth
            double ease = 1.0 - Math.Pow(1.0 - t, 3);

            double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.6, 1.4);
            double baseW = _displayedCollapsedWidth * scale;
            double baseH = _displayedCollapsedHeight * scale;

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
                ease);

            _notchBackdropPath.Data = geo;
            _notchOutlinePath.Data = geo;
            _notchTopBlackLine.X2 = curW;

            if (settled)
            {
                _edgeLight.UpdateGeometry(geo, t > 0.5, curW, curH);
            }

            // Collapsed content quickly fades out
            _collapsedContent.Opacity = Math.Clamp(1.0 - ease * 3.2, 0.0, 1.0);
            _collapsedContent.IsHitTestVisible = ease < 0.2;

            // Expanded content fades in smoothly during middle-to-end of expansion
            _expandedContent.Opacity = Math.Clamp((ease - 0.22) / 0.78, 0.0, 1.0);
            _expandedContent.IsHitTestVisible = ease > 0.6;
        }

        private void ApplyThemeAndGeometry()
        {
            var p = CapsuleTheme.Current;
            _notchBackdropPath.Fill = CapsuleTheme.Brush(p.Bg);

            // 1. Notch Visibility & Show Notch
            if (!AppPreferences.Instance.ShowNotchBar || string.Equals(AppPreferences.Instance.NotchVisibility, "Hidden", StringComparison.OrdinalIgnoreCase))
            {
                Visibility = Visibility.Collapsed;
            }
            else
            {
                Visibility = Visibility.Visible;
            }

            // 2. Position on selected display
            PositionAtTopCenter();

            // 3. Edge light
            if (AppPreferences.Instance.EnableEdgeLight)
            {
                _edgeLight.Visibility = Visibility.Visible;
                try
                {
                    var c = (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.EdgeLightColor);
                    _edgeLight.SetLightColor(c);
                }
                catch { }
                _edgeLight.Opacity = Math.Clamp(AppPreferences.Instance.EdgeLightIntensity / 100.0, 0.1, 1.0);
            }
            else
            {
                _edgeLight.Visibility = Visibility.Collapsed;
            }

            // 4. Soft Edge effect
            if (!string.Equals(AppPreferences.Instance.NotchSoftEdge, "None", StringComparison.OrdinalIgnoreCase))
            {
                double blurRadius = AppPreferences.Instance.NotchSoftEdge switch
                {
                    "Low" => 2.0,
                    "High" => 5.0,
                    _ => 3.0 // Medium
                };
                _notchBackdropPath.Effect = new System.Windows.Media.Effects.BlurEffect 
                { 
                    Radius = blurRadius, 
                    KernelType = KernelType.Box, 
                    RenderingBias = RenderingBias.Performance 
                };
            }
            else
            {
                _notchBackdropPath.Effect = null;
            }

            bool outlineEnabled = AppPreferences.Instance.SurfaceOutline;
            _notchOutlinePath.Visibility = outlineEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (outlineEnabled)
            {
                double strokeWidth = 1.0;
                if (AppPreferences.Instance.SurfaceOutlineWidth.Contains("2")) strokeWidth = 2.0;
                else if (AppPreferences.Instance.SurfaceOutlineWidth.Contains("3")) strokeWidth = 3.0;
                _notchOutlinePath.StrokeThickness = strokeWidth;

                if (AppPreferences.Instance.TintOutlineFromWallpaper)
                {
                    _notchOutlinePath.Stroke = CapsuleTheme.Brush(p.Accent);
                }
                else
                {
                    _notchOutlinePath.Stroke = CapsuleTheme.Brush(p.BorderStrong);
                }
            }

            // Shadow depth effect under open notch
            string shadowMode = AppPreferences.Instance.NotchShadowDepth ?? "Medium";
            if (string.Equals(shadowMode, "None", StringComparison.OrdinalIgnoreCase))
            {
                _capsuleCard.Effect = null;
            }
            else if (string.Equals(shadowMode, "Subtle", StringComparison.OrdinalIgnoreCase))
            {
                _capsuleCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 14,
                    ShadowDepth = 4,
                    Opacity = 0.25,
                    Direction = 270,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Performance
                };
            }
            else if (string.Equals(shadowMode, "Deep", StringComparison.OrdinalIgnoreCase))
            {
                _capsuleCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 38,
                    ShadowDepth = 12,
                    Opacity = 0.65,
                    Direction = 270,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Performance
                };
            }
            else // Medium
            {
                _capsuleCard.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 24,
                    ShadowDepth = 7,
                    Opacity = 0.45,
                    Direction = 270,
                    Color = Colors.Black,
                    RenderingBias = RenderingBias.Performance
                };
            }

            _headerTitleText.Foreground = CapsuleTheme.Brush(p.Text);
            _headerSubtitleText.Foreground = CapsuleTheme.Brush(p.Text2);
            _collapsedClockText.Foreground = CapsuleTheme.Brush(p.Text);

            RebuildNavTabs();
            NavigateToTool(_activeToolId);
            UpdateMorphVisuals(_morphProgress, settled: true);
        }

        public void ToggleFromExternal() => _machine.Toggle("tray-or-cli");
    }
}
