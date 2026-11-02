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
using WindowsBuddy.UI.AnimateIcons;
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
        private readonly StackPanel _restingLeftStack;
        private readonly StackPanel _restingRightStack;
        private readonly TextBlock _collapsedDateText;
        private readonly MiniAnalogClock _collapsedAnalogClock;
        private readonly TextBlock _collapsedClockText;
        private readonly CuteOrbPetView _collapsedOrb;
        private readonly CuteOrbPetView _headerOrb;
        private readonly AnimateIcon _collapsedBluetoothIcon;
        private readonly BatteryStatusGlyph _collapsedBatteryGlyph;
        private readonly TextBlock _collapsedBatteryText;
        private readonly Grid _collapsedMinimalView;
        private bool _isMinimalModeActive = false;
        private bool _isMinimalPeekHovered = false;
        private bool _isAutoHiddenByVisibility = false;

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
        private readonly AnimateIcon _bannerAnimateIcon;
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
        private double _targetProgress;
        private string _activeToolId = "home";

        private bool _isBannerActive = false;
        private bool _isMusicActive = false;
        private bool _isMediaRetained = false;
        private bool _isMediaWideActive = false;
        private Color _currentSongBeatColor = Color.FromRgb(48, 209, 88);
        private string _lastObservedTrack = "";
        private string _lastObservedArtist = "";
        private double _mediaPosSeconds = 0.0;
        private double _mediaDurSeconds = 202.0;
        private DateTime _lastMediaTimelineSyncUtc = DateTime.UtcNow;
        private double _currentBaseCollapsedWidth = CollapsedWidth;
        private double _currentBaseCollapsedHeight = CollapsedHeight;
        private double _displayedCollapsedWidth = CollapsedWidth;
        private double _displayedCollapsedHeight = CollapsedHeight;

        private bool _isTucked = false;
        private readonly TranslateTransform _capsuleCardTranslate = new(0, 0);
        private readonly DispatcherTimer _mediaSettleExitTimer;

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

            _restingLeftStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _collapsedDateText = new TextBlock
            {
                Text = DateTime.Now.ToString("ddd d", CultureInfo.InvariantCulture),
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0),
                Visibility = Visibility.Collapsed
            };
            _restingLeftStack.Children.Add(_collapsedDateText);

            _collapsedAnalogClock = new MiniAnalogClock(15.5)
            {
                Margin = new Thickness(0, 0, 4, 0),
                Visibility = Visibility.Collapsed
            };
            _restingLeftStack.Children.Add(_collapsedAnalogClock);

            _collapsedClockText = new TextBlock
            {
                Text = DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            _restingLeftStack.Children.Add(_collapsedClockText);

            Grid.SetColumn(_restingLeftStack, 0);
            _collapsedNormalView.Children.Add(_restingLeftStack);

            _collapsedOrb = new CuteOrbPetView(initialMood: OrbMood.Idle, size: 21)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = LocalizationService.T("header.orb", "Mayli Companion Orb ✨ (Click to emote)")
            };
            _collapsedOrb.Clicked += () => PetEngine.Instance.CycleNextMood();
            Grid.SetColumn(_collapsedOrb, 1);
            _collapsedNormalView.Children.Add(_collapsedOrb);

            _restingRightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            _collapsedBluetoothIcon = new AnimateIcon(AnimateIconType.Bluetooth, size: 13, brush: new SolidColorBrush(Color.FromRgb(10, 132, 255)))
            {
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _restingRightStack.Children.Add(_collapsedBluetoothIcon);

            _collapsedBatteryGlyph = new BatteryStatusGlyph();
            _collapsedBatteryGlyph.SetBattery(SystemMonitorService.Current.BatteryPercent);
            _restingRightStack.Children.Add(_collapsedBatteryGlyph);

            _collapsedBatteryText = new TextBlock
            {
                Text = $"{SystemMonitorService.Current.BatteryPercent}",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            _restingRightStack.Children.Add(_collapsedBatteryText);

            Grid.SetColumn(_restingRightStack, 2);
            _collapsedNormalView.Children.Add(_restingRightStack);

            // Right-clicking resting notch while music plays summons the Wide Music Card on demand
            _collapsedNormalView.MouseRightButtonUp += (_, e) =>
            {
                if (_isMusicActive || _isMediaRetained)
                {
                    e.Handled = true;
                    ActivateMediaWideCard();
                }
            };

            _collapsedContent.Children.Add(_collapsedNormalView);

            // View 1B: Minimal Peek Bar View ("Minimal" or "Smart" over maximized window: sleek top chevron pill)
            _collapsedMinimalView = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var minChevron = new Path
            {
                Data = Geometry.Parse("M 0,0 L 4.5,4 L 9,0"),
                Stroke = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _collapsedMinimalView.Children.Add(minChevron);
            _collapsedContent.Children.Add(_collapsedMinimalView);

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

            _bannerAnimateIcon = new AnimateIcon(AnimateIconType.CopyCheck, size: 16, brush: Brushes.White)
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                AutoAnimateOnHover = false
            };
            DockPanel.SetDock(_bannerAnimateIcon, Dock.Left);
            bannerDock.Children.Add(_bannerAnimateIcon);

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

            _headerOrb = new CuteOrbPetView(initialMood: OrbMood.Idle, size: 22)
            {
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = LocalizationService.T("header.orb", "Mayli Companion Orb ✨ (Click to emote)")
            };
            _headerOrb.Clicked += () => PetEngine.Instance.CycleNextMood();
            leftBrand.Children.Add(_headerOrb);

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

            var themeBtn = CreateHeaderIconButton(AnimateIconType.Palette, string.Format(LocalizationService.T("header.theme_format", "Theme: {0} (Click to switch)"), CapsuleTheme.Current.DisplayName));
            themeBtn.MouseLeftButtonUp += (_, _) =>
            {
                CapsuleTheme.CycleTheme();
                themeBtn.ToolTip = string.Format(LocalizationService.T("header.theme_format", "Theme: {0} (Click to switch)"), CapsuleTheme.Current.DisplayName);
            };
            rightActions.Children.Add(themeBtn);

            var settingsBtn = CreateHeaderIconButton(AnimateIconType.Settings, LocalizationService.T("header.settings", "Settings"));
            settingsBtn.MouseLeftButtonUp += (_, _) => SettingsWindow.ShowSingleton();
            rightActions.Children.Add(settingsBtn);

            var collapseBtn = CreateHeaderIconButton(AnimateIconType.Close, LocalizationService.T("header.close", "Close (Esc)"));
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

            // Morph Animation timer
            _morphTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(14) };
            _morphTimer.Tick += OnMorphTick;

            _machine.PhaseChanged += (_, next, _) =>
            {
                if (next == NotchPhase.Opening || next == NotchPhase.Open)
                {
                    _targetProgress = 1.0;
                    _expandedContent.Visibility = Visibility.Visible;
                    _edgeLight.TriggerBurst(4.5);
                    EvaluateDynamicVisibility();
                    _morphTimer.Start();
                }
                else if (next == NotchPhase.Closing || next == NotchPhase.Resting)
                {
                    _targetProgress = 0.0;
                    EvaluateDynamicVisibility();
                    _morphTimer.Start();
                }
            };

            _capsuleCard.MouseEnter += (_, _) =>
            {
                _edgeLight.TriggerBurst(4.2);
                if (_isMinimalModeActive && AppPreferences.Instance.OpensOnClick)
                {
                    _isMinimalPeekHovered = true;
                    EvaluateDynamicVisibility();
                    return;
                }
                if (AppPreferences.Instance.OpensOnClick) return;
                _machine.PointerMoved(true, "mouse-enter");
            };
            _capsuleCard.MouseLeave += (_, _) =>
            {
                if (_isMinimalPeekHovered)
                {
                    _isMinimalPeekHovered = false;
                    EvaluateDynamicVisibility();
                }
                _machine.PointerMoved(false, "mouse-leave");
            };
            _capsuleCard.MouseLeftButtonUp += (_, _) =>
            {
                if (_morphProgress <= 0.1)
                {
                    _edgeLight.TriggerBurst(4.5);
                    _machine.Click("capsule-click");
                }
            };

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) =>
            {
                UpdateRestingClockAndStatus();
                EvaluateDynamicVisibility();

                if (_isMusicActive)
                {
                    UpdateMediaTimelineVisuals();
                }

                // Ensure notch stays on top layer above all windows when not tucked away (every 5s to avoid per-second P/Invoke overhead)
                _topmostTickCounter++;
                if (!_isTucked && !_isAutoHiddenByVisibility && (_topmostTickCounter % 5 == 0))
                {
                    EnsureTopmost();
                }
            };
            _clockTimer.Start();

            // 30fps Real-time Spectrum & Equalizer Animation Timer (active during playback and smooth decay when stopped)
            _spectrumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _spectrumTimer.Tick += (_, _) =>
            {
                if (!_isMusicActive)
                {
                    bool miniSettled = _miniEqualizer.DecayLevels();
                    bool waveSettled = _waveformProgress.DecayLevels();
                    bool wideWaveSettled = _wideWaveformProgress.DecayLevels();
                    if (miniSettled && waveSettled && wideWaveSettled)
                    {
                        _spectrumTimer.Stop();
                    }
                    return;
                }
                if (_morphProgress > 0.4) return;
                _spectrumPhase += 0.085;

                AudioSpectrumService.Instance.Read(_fftBands28, out bool isAudioLive);
                AudioSpectrumService.ComputeMiniBars(_fftBands28, _miniBars4, _spectrumPhase);
                _miniEqualizer.SetLevels(_miniBars4, true);

                if (_isMediaWideActive)
                {
                    AudioSpectrumService.MapBands(_fftBands28, _mappedBars56, WaveformProgressBar.WideBarCount);
                    _wideWaveformProgress.SetSpectrumLevels(_mappedBars56, true, isAudioLive, _spectrumPhase);
                }
                else
                {
                    AudioSpectrumService.MapBands(_fftBands28, _mappedBars40, WaveformProgressBar.CompactBarCount);
                    _waveformProgress.SetSpectrumLevels(_mappedBars40, true, isAudioLive, _spectrumPhase);
                }
            };

            // Music Settle Exit Timer (Retains media card 2.8s after music ends/pauses, then smoothly morphs to resting notch)
            _mediaSettleExitTimer = new DispatcherTimer();
            _mediaSettleExitTimer.Tick += (_, _) =>
            {
                _mediaSettleExitTimer.Stop();
                if (!_isMusicActive && _isMediaRetained)
                {
                    _isMediaRetained = false;
                    UpdateEdgeLightEffectiveColor();
                    UpdateCollapsedViewState();
                }
            };

            // Wide Music Card Dismissal Timer (Track switch / song play, then smoothly settles)
            _mediaWideDismissTimer = new DispatcherTimer();
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

            CapsuleTheme.ThemeChanged += RequestApplyThemeAndGeometry;
            AppPreferences.Instance.Changed += RequestApplyThemeAndGeometry;
            LocalizationService.LanguageChanged += () => Dispatcher.InvokeAsync(RequestApplyThemeAndGeometry);

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

            ApplyThemeAndGeometry();
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
                _bannerTitle.Text = payload.Title;
                _bannerSubtitle.Text = payload.Subtitle;

                if (payload.Title == "Copied" || payload.Icon == "📋" || payload.Icon == "📎")
                {
                    _bannerAnimateIcon.IconType = AnimateIconType.CopyCheck;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Play();
                }
                else if (payload.Icon == "🎧" || (payload.Title != null && payload.Title.Contains("Connected", StringComparison.OrdinalIgnoreCase)))
                {
                    _bannerAnimateIcon.IconType = AnimateIconType.Bluetooth;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Play();
                }
                else if (payload.Icon == "🔊" || payload.Icon == "🔈")
                {
                    _bannerAnimateIcon.IconType = AnimateIconType.Volume;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Play();
                }
                else if (payload.Icon == "⏱" || payload.Icon == "⏰")
                {
                    _bannerAnimateIcon.IconType = AnimateIconType.Timer;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Play();
                }
                else
                {
                    _bannerAnimateIcon.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Text = payload.Icon;
                }

                // Display banner view
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;

                _currentBaseCollapsedWidth = BannerCollapsedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;

                _edgeLight.TriggerBurst(4.2);
                EvaluateDynamicVisibility();

                if (_morphProgress <= 0.1)
                {
                    _targetProgress = 0.0;
                    _morphTimer.Start();
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
                if (!AppPreferences.Instance.ShowMediaActivity || (snapshot.IsPlaying && !string.IsNullOrWhiteSpace(snapshot.SourceApp) && AppPreferences.Instance.IsAudioAppExcluded(snapshot.SourceApp)))
                {
                    _isMusicActive = false;
                    _isMediaRetained = false;
                    _mediaSettleExitTimer.Stop();
                    _spectrumTimer.Stop();
                    _miniEqualizer.IsPlaying = false;
                    _wideTransportControls.SetPlaying(false);
                    _isMediaWideActive = false;
                    _mediaWideDismissTimer.Stop();
                    UpdateEdgeLightEffectiveColor();
                    UpdateCollapsedViewState();
                    return;
                }

                bool wasPlaying = _isMusicActive;
                _isMusicActive = snapshot.IsPlaying;
                if (snapshot.BeatColor.A > 0)
                {
                    _currentSongBeatColor = snapshot.BeatColor;
                }
                UpdateEdgeLightEffectiveColor();

                if (_isMusicActive)
                {
                    _isMediaRetained = true;
                    _mediaSettleExitTimer.Stop();
                    _spectrumTimer.Start();
                }
                else
                {
                    if (wasPlaying && _isMediaRetained)
                    {
                        // Music paused/stopped: smoothly decay equalizer and keep media card alive for 2.8s before morphing
                        _spectrumTimer.Start();
                        _mediaSettleExitTimer.Stop();
                        _mediaSettleExitTimer.Interval = TimeSpan.FromSeconds(2.8);
                        _mediaSettleExitTimer.Start();
                    }
                }
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

                bool trackChanged = !string.Equals(track, _lastObservedTrack, StringComparison.Ordinal) ||
                                    !string.Equals(artist, _lastObservedArtist, StringComparison.Ordinal);
                _lastObservedTrack = track;
                _lastObservedArtist = artist;

                if (!_isMusicActive)
                {
                    _isMediaWideActive = false;
                    _mediaWideDismissTimer.Stop();
                    if (!_isMediaRetained && !_isBannerActive)
                    {
                        UpdateCollapsedViewState();
                    }
                }
                else
                {
                    // Whenever a song starts playing or track switches, expand the notch to show track & progress bar, then settle
                    bool shouldExpandWide = (!wasPlaying && _isMusicActive) || snapshot.IsTrackSwitch || trackChanged;
                    if (shouldExpandWide)
                    {
                        ActivateMediaWideCard();
                        return;
                    }

                    if (!_isBannerActive)
                    {
                        UpdateCollapsedViewState();
                    }
                }
            });
        }

        private void ActivateMediaWideCard()
        {
            if (!_isMusicActive && !_isMediaRetained) return;
            _isMediaWideActive = true;
            _edgeLight.TriggerBurst(4.5);
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
            int durationSec = AppPreferences.Instance.LiveActivityEventDuration switch
            {
                "2 s" => 2,
                "5 s" => 5,
                _ => (int)MediaWideDurationSeconds
            };
            _mediaWideDismissTimer.Interval = TimeSpan.FromSeconds(durationSec);
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
            EvaluateDynamicVisibility();

            bool showMedia = (_isMusicActive || _isMediaRetained) && AppPreferences.Instance.ShowClosedNowPlaying;

            if (_isBannerActive)
            {
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
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
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Visible;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;
            }
            else if (showMedia && _isMediaWideActive)
            {
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = MediaWideCollapsedWidth;
                _currentBaseCollapsedHeight = MediaWideCollapsedHeight;
            }
            else if (_isMinimalModeActive)
            {
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedMinimalView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = 72.0;
                _currentBaseCollapsedHeight = 14.0;
            }
            else if (showMedia)
            {
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
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
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Visible;
                GetConfiguredRestingDimensions(out double rw, out double rh);
                _currentBaseCollapsedWidth = rw;
                _currentBaseCollapsedHeight = rh;
            }

            if (_morphProgress <= 0.1)
            {
                _targetProgress = 0.0;
                _morphTimer.Start();
            }
        }

        private static void GetConfiguredRestingDimensions(out double width, out double height)
        {
            NotchGeometryBuilder.GetRestingShapeProfile(
                AppPreferences.Instance.NotchStyle,
                AppPreferences.Instance.NotchShape,
                AppPreferences.Instance.NotchScalePercent,
                out width,
                out height,
                out _,
                out _);
        }

        private void UpdateRestingClockAndStatus()
        {
            var prefs = AppPreferences.Instance;
            var now = DateTime.Now;
            var culture = prefs.IsPersian ? new CultureInfo("fa-IR") : CultureInfo.InvariantCulture;

            string timeStr;
            if (prefs.TimeFormat == "24-hour")
                timeStr = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            else if (prefs.TimeFormat == "12-hour")
                timeStr = now.ToString("h:mm tt", CultureInfo.InvariantCulture);
            else
                timeStr = now.ToString("t", culture);

            bool showTime = prefs.ShowClosedTime;
            bool showDate = prefs.ShowClosedDate;
            bool isAnalog = string.Equals(prefs.ClosedClockStyle, "Analog", StringComparison.OrdinalIgnoreCase);

            if (showDate)
            {
                string datePart = now.ToString("ddd d", culture);
                _collapsedDateText.Text = (showTime && !isAnalog) ? $"{datePart} ·" : datePart;
                _collapsedDateText.Visibility = Visibility.Visible;
            }
            else
            {
                _collapsedDateText.Visibility = Visibility.Collapsed;
            }

            if (showTime)
            {
                if (isAnalog)
                {
                    _collapsedAnalogClock.UpdateTime(now);
                    _collapsedAnalogClock.Visibility = Visibility.Visible;
                    _collapsedClockText.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _collapsedClockText.Text = timeStr;
                    _collapsedClockText.Visibility = Visibility.Visible;
                    _collapsedAnalogClock.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _collapsedClockText.Visibility = Visibility.Collapsed;
                _collapsedAnalogClock.Visibility = Visibility.Collapsed;
            }

            // Pet visibility
            Visibility petVis = prefs.EnablePetBeta ? Visibility.Visible : Visibility.Collapsed;
            _collapsedOrb.Visibility = petVis;
            _headerOrb.Visibility = petVis;

            // Bluetooth indicator when closed
            bool btConnected = RealtimeNotchActionService.Instance.HasConnectedBluetoothDevice;
            _collapsedBluetoothIcon.Visibility = (prefs.ShowClosedBluetooth && btConnected)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Battery status
            var sys = SystemMonitorService.Current;
            _collapsedBatteryGlyph.SetBattery(sys.BatteryPercent);
            _collapsedBatteryText.Text = $"{sys.BatteryPercent}";
            _headerBatteryText.Text = $"{sys.BatteryPercent}% ⚡";

            Visibility batVis = prefs.ShowClosedBattery ? Visibility.Visible : Visibility.Collapsed;
            _collapsedBatteryGlyph.Visibility = batVis;
            _collapsedBatteryText.Visibility = batVis;

            // Resting layout: Corners vs Center
            bool isCenterLayout = string.Equals(prefs.NotchRestingLayout, "Center", StringComparison.OrdinalIgnoreCase);
            if (_collapsedNormalView.ColumnDefinitions.Count >= 3)
            {
                _collapsedNormalView.ColumnDefinitions[1].Width = isCenterLayout
                    ? GridLength.Auto
                    : new GridLength(1, GridUnitType.Star);
            }
            _collapsedNormalView.HorizontalAlignment = isCenterLayout
                ? HorizontalAlignment.Center
                : HorizontalAlignment.Stretch;
            _collapsedOrb.Margin = isCenterLayout ? new Thickness(8, 0, 8, 0) : new Thickness(0);
            _restingRightStack.Margin = (isCenterLayout && !prefs.EnablePetBeta) ? new Thickness(10, 0, 0, 0) : new Thickness(0);

            // Scale resting content slightly with Size slider
            double scale = Math.Clamp(prefs.NotchScalePercent / 100.0, 0.78, 1.24);
            _collapsedNormalView.LayoutTransform = Math.Abs(scale - 1.0) < 0.02
                ? Transform.Identity
                : new ScaleTransform(scale, scale);
        }

        private void EvaluateDynamicVisibility()
        {
            var prefs = AppPreferences.Instance;

            if (!prefs.ShowNotchBar)
            {
                if (IsLoaded) Visibility = Visibility.Collapsed;
                return;
            }

            if (IsLoaded && Visibility != Visibility.Visible)
            {
                Visibility = Visibility.Visible;
            }

            bool isOpenOrOpening = _targetProgress > 0.05 || _morphProgress > 0.1;
            bool hasLiveEvent = _isBannerActive || _isMessageActive || (_isMusicActive && _isMediaWideActive);
            bool eventOverridesTuck = prefs.ShowEventsOverApps && hasLiveEvent;

            InspectForegroundWindowOnNotchDisplay(out bool isFgFullscreen, out bool isFgMaximized);

            // 1. Hide in fullscreen (unless open or event overrides tuck)
            if (prefs.HideOnFullscreen && isFgFullscreen && !isOpenOrOpening && !eventOverridesTuck)
            {
                SetAutoHidden(true);
                return;
            }

            string visMode = prefs.NotchVisibility ?? "Always";

            // 2. Hidden mode: invisible when resting, but opens via shortcut/tray or live event when ShowEventsOverApps is enabled
            if (string.Equals(visMode, "Hidden", StringComparison.OrdinalIgnoreCase))
            {
                SetAutoHidden(!isOpenOrOpening && !eventOverridesTuck);
                return;
            }

            SetAutoHidden(false);

            // 3. Minimal or Smart mode
            bool wantMinimal = false;
            if (!isOpenOrOpening && !eventOverridesTuck && !_isMinimalPeekHovered)
            {
                if (string.Equals(visMode, "Minimal", StringComparison.OrdinalIgnoreCase))
                {
                    wantMinimal = true;
                }
                else if (string.Equals(visMode, "Smart", StringComparison.OrdinalIgnoreCase) && isFgMaximized)
                {
                    wantMinimal = true;
                }
            }

            if (wantMinimal != _isMinimalModeActive)
            {
                _isMinimalModeActive = wantMinimal;
                _collapsedContent.Margin = _isMinimalModeActive
                    ? new Thickness(4, 0, 4, 0)
                    : new Thickness(EarUnitW + 10, 2, EarUnitW + 10, 2);
                UpdateCollapsedViewState();
            }
        }

        private void SetAutoHidden(bool hidden)
        {
            if (_isAutoHiddenByVisibility == hidden) return;
            _isAutoHiddenByVisibility = hidden;
            if (_isTucked) return; // Manual tuck takes precedence

            if (hidden)
            {
                _capsuleCard.IsHitTestVisible = false;
                _capsuleCard.Visibility = Visibility.Collapsed;
            }
            else
            {
                _capsuleCard.Visibility = Visibility.Visible;
                _capsuleCard.IsHitTestVisible = true;
                EnsureTopmost();
            }
        }

        private void InspectForegroundWindowOnNotchDisplay(out bool isFullscreen, out bool isMaximized)
        {
            isFullscreen = false;
            isMaximized = false;
            try
            {
                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg == IntPtr.Zero || !NativeMethods.IsWindowVisible(fg) || NativeMethods.IsIconic(fg))
                    return;

                var myHwnd = new WindowInteropHelper(this).Handle;
                if (fg == myHwnd) return;

                NativeMethods.GetWindowThreadProcessId(fg, out uint fgPid);
                if (fgPid == (uint)Environment.ProcessId) return;

                var sbClass = new System.Text.StringBuilder(64);
                NativeMethods.GetClassName(fg, sbClass, sbClass.Capacity);
                string cls = sbClass.ToString();
                if (string.Equals(cls, "Progman", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(cls, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (!NativeMethods.GetWindowRect(fg, out var rect))
                    return;

                var targetScreen = GetTargetScreen();
                if (targetScreen == null) return;
                var b = targetScreen.Bounds;

                // Check if window center is on the notch's display
                int cx = (rect.Left + rect.Right) / 2;
                int cy = (rect.Top + rect.Bottom) / 2;
                if (!b.Contains(cx, cy))
                    return;

                int winW = rect.Right - rect.Left;
                int winH = rect.Bottom - rect.Top;

                // Fullscreen check: covers entire monitor bounds (including taskbar area)
                if (rect.Left <= b.Left + 2 && rect.Top <= b.Top + 2 &&
                    winW >= b.Width - 4 && winH >= b.Height - 4)
                {
                    isFullscreen = true;
                    isMaximized = true;
                    return;
                }

                if (NativeMethods.IsZoomed(fg))
                {
                    isMaximized = true;
                }
            }
            catch { }
        }

        private System.Windows.Forms.Screen GetTargetScreen()
        {
            try
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var targetScreen = System.Windows.Forms.Screen.PrimaryScreen;
                string choice = AppPreferences.Instance.NotchDisplay;
                if (string.Equals(choice, "Display 2", StringComparison.OrdinalIgnoreCase) && screens.Length > 1)
                    targetScreen = screens[1];
                else if (string.Equals(choice, "Display 1", StringComparison.OrdinalIgnoreCase) && screens.Length > 0)
                    targetScreen = screens[0];
                return targetScreen;
            }
            catch
            {
                return null;
            }
        }

        private void OnMessageFlashRequested(WindowsBuddy.Tools.Message.MessageSettings settings)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _isMessageActive = true;
                _isMessagePinned = settings.RepeatEnabled;
                _edgeLight.TriggerBurst(4.5);

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
                    _targetProgress = 0.0;
                    _morphTimer.Start();
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

        private static Border CreateHeaderIconButton(AnimateIconType iconType, string tooltip)
        {
            var p = CapsuleTheme.Current;
            var animIcon = new AnimateIcon(iconType, size: 14, brush: CapsuleTheme.Brush(p.Text2))
            {
                AutoAnimateOnHover = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var border = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                ToolTip = tooltip,
                Margin = new Thickness(3, 0, 3, 0),
                Child = animIcon
            };

            border.MouseEnter += (_, _) =>
            {
                border.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                animIcon.StrokeBrush = Brushes.White;
                animIcon.Play();
            };
            border.MouseLeave += (_, _) =>
            {
                border.Background = Brushes.Transparent;
                animIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
            };
            return border;
        }

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
                    ToolTip = LocalizationService.T("dock." + tool.Id.ToLowerInvariant(), tool.Title)
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

                AnimateIconType? animType = id.ToLowerInvariant() switch
                {
                    "home" => AnimateIconType.Home,
                    "system" => AnimateIconType.Activity,
                    "clipboard" => AnimateIconType.CopyCheck,
                    "notes" => AnimateIconType.Notes,
                    "focus" => AnimateIconType.Timer,
                    "fileshelf" => AnimateIconType.FileShelf,
                    "weather" => AnimateIconType.Weather,
                    "clock" => AnimateIconType.Clock,
                    "links" => AnimateIconType.Links,
                    "message" => AnimateIconType.Message,
                    "screentime" => AnimateIconType.Activity,
                    _ => null
                };

                AnimateIcon animIcon = null;
                TextBlock iconText = null;

                if (animType.HasValue)
                {
                    animIcon = new AnimateIcon(animType.Value, size: 14, brush: isActive ? Brushes.Black : CapsuleTheme.Brush(p.IconMuted))
                    {
                        AutoAnimateOnHover = false,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    itemContainer.Children.Add(animIcon);
                }
                else
                {
                    iconText = new TextBlock
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
                }

                itemContainer.MouseEnter += (_, _) =>
                {
                    if (!string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase))
                    {
                        circleHighlight.Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255));
                        if (iconText != null) iconText.Foreground = Brushes.White;
                        if (animIcon != null) animIcon.StrokeBrush = Brushes.White;
                    }
                    animIcon?.Play();
                };
                itemContainer.MouseLeave += (_, _) =>
                {
                    if (!string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase))
                    {
                        circleHighlight.Background = Brushes.Transparent;
                        if (iconText != null) iconText.Foreground = CapsuleTheme.Brush(p.IconMuted);
                        if (animIcon != null) animIcon.StrokeBrush = CapsuleTheme.Brush(p.IconMuted);
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
                ToolTip = LocalizationService.T("dock.customize", "Add / Customize Tools"),
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

            var culture = AppPreferences.Instance.IsPersian ? new System.Globalization.CultureInfo("fa-IR") : System.Globalization.CultureInfo.InvariantCulture;
            _headerTitleText.Text = tool.Title;
            _headerSubtitleText.Text = tool.Id switch
            {
                "home" => DateTime.Now.ToString("dddd, d MMM", culture),
                "system" => LocalizationService.T("sub.system", "Power · Network · Audio"),
                "weather" => WindowsBuddy.Tools.Weather.WeatherService.Instance.Prefs.City,
                "clock" => LocalizationService.T("sub.clock", "Local Time & Date"),
                "units" => LocalizationService.T("sub.units", "Conversion"),
                "fileshelf" => LocalizationService.T("sub.fileshelf", "Parked files"),
                "links" => LocalizationService.T("sub.links", "Quick Bookmarks"),
                "message" => LocalizationService.T("sub.message", "Notch banner"),
                "focus" => LocalizationService.T("sub.focus", "Focus & intervals"),
                "clipboard" => string.Format(LocalizationService.T("sub.clipboard_fmt", "{0} items"), WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.Items.Count),
                "notes" => string.Format(LocalizationService.T("sub.notes_fmt", "{0} notes"), WindowsBuddy.Tools.Notes.NotesService.Instance.Notes.Count),
                "screentime" => DateTime.Now.ToString("ddd, d MMM", culture),
                _ => "Mayli Capsule"
            };

            UpdateToolHeaderActions(tool.Id);
            RebuildNavTabs();

            var newView = tool.BuildView();
            _viewportHost.Child = newView;
            tool.OnActivated();

            if (AppPreferences.Instance.TabAnimations && _expandedContent.Visibility == Visibility.Visible && newView != null)
            {
                var tt = new TranslateTransform(14, 0);
                newView.RenderTransform = tt;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(14, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                newView.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            }
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

        private int _topmostTickCounter;
        private bool _themeUpdatePending;

        private void RequestApplyThemeAndGeometry()
        {
            if (_themeUpdatePending) return;
            _themeUpdatePending = true;
            Dispatcher.InvokeAsync(() =>
            {
                _themeUpdatePending = false;
                ApplyThemeAndGeometry();
            }, DispatcherPriority.Send);
        }

        private void OnMorphTick(object sender, EventArgs e)
        {
            double step = _targetProgress > _morphProgress ? 0.14 : 0.18;
            bool morphSettled = false;

            if (Math.Abs(_targetProgress - _morphProgress) <= step)
            {
                _morphProgress = _targetProgress;
                morphSettled = true;
                if (_morphProgress <= 0.001)
                {
                    _expandedContent.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                _morphProgress += Math.Sign(_targetProgress - _morphProgress) * step;
            }

            // Smoothly interpolate collapsed pill width & height (Resting <-> Music <-> Banner)
            double dw = _currentBaseCollapsedWidth - _displayedCollapsedWidth;
            double dh = _currentBaseCollapsedHeight - _displayedCollapsedHeight;
            bool sizeSettled = Math.Abs(dw) < 0.6 && Math.Abs(dh) < 0.4;

            if (sizeSettled)
            {
                _displayedCollapsedWidth = _currentBaseCollapsedWidth;
                _displayedCollapsedHeight = _currentBaseCollapsedHeight;
            }
            else
            {
                _displayedCollapsedWidth += dw * 0.28;
                _displayedCollapsedHeight += dh * 0.28;
            }

            bool allSettled = morphSettled && sizeSettled;
            _edgeLight.SetMorphing(!allSettled);

            if (allSettled)
            {
                _morphTimer.Stop();
            }

            UpdateMorphVisuals(_morphProgress);
        }

        private void UpdateMorphVisuals(double progress)
        {
            double t = Math.Clamp(progress, 0.0, 1.0);
            double ease = 1.0 - Math.Pow(1.0 - t, 3);

            double baseW = _displayedCollapsedWidth;
            double baseH = _displayedCollapsedHeight;

            double curW = baseW + (ExpandedWidth - baseW) * ease;
            double curH = baseH + (ExpandedHeight - baseH) * ease;

            _capsuleCard.Width = curW;
            _capsuleCard.Height = curH;
            _capsuleCard.Cursor = t > 0.5 ? Cursors.Arrow : Cursors.Hand;

            string notchStyle = AppPreferences.Instance.NotchStyle ?? "Notch";
            bool isIsland = string.Equals(notchStyle, "Island", StringComparison.OrdinalIgnoreCase);
            bool isLine = string.Equals(notchStyle, "Line", StringComparison.OrdinalIgnoreCase);

            string geoStyle = isIsland ? "Island" : (isLine ? "Line" : (AppPreferences.Instance.NotchShape ?? "Shouldered"));
            var geo = NotchGeometryBuilder.BuildMorphedGeometry(
                geoStyle,
                baseW,
                baseH,
                ExpandedWidth,
                ExpandedHeight,
                t);

            _notchBackdropPath.Data = geo;
            _notchOutlinePath.Data = geo;
            _notchTopBlackLine.Visibility = isIsland ? Visibility.Collapsed : Visibility.Visible;
            _notchTopBlackLine.X2 = curW;
            _edgeLight.UpdateGeometry(geo, t > 0.5, curW, curH);

            double collapsedOpacity = Math.Clamp(1.0 - t * 2.5, 0.0, 1.0);
            _collapsedContent.Opacity = collapsedOpacity;
            _collapsedContent.Visibility = collapsedOpacity <= 0.001 ? Visibility.Collapsed : Visibility.Visible;
            _collapsedContent.IsHitTestVisible = t < 0.25;

            _expandedContent.Opacity = Math.Clamp((t - 0.25) / 0.75, 0.0, 1.0);
            _expandedContent.IsHitTestVisible = t > 0.6;
        }

        private void UpdateEdgeLightEffectiveColor()
        {
            if (_edgeLight == null || !AppPreferences.Instance.EnableEdgeLight)
                return;

            bool syncWithMusic = AppPreferences.Instance.EdgeLightSyncWithMusic ||
                                 string.Equals(AppPreferences.Instance.EdgeLightColor, "Artwork", StringComparison.OrdinalIgnoreCase);

            if (syncWithMusic && (_isMusicActive || _isMediaRetained || RealtimeNotchActionService.Instance.IsMusicPlaying))
            {
                Color songColor = _currentSongBeatColor;
                if (songColor.A == 0 || (songColor.R == 48 && songColor.G == 209 && songColor.B == 88 && RealtimeNotchActionService.Instance.CurrentBeatColor.A > 0))
                {
                    songColor = RealtimeNotchActionService.Instance.CurrentBeatColor;
                }
                _edgeLight.SetLightColor(songColor);
                return;
            }

            try
            {
                var c = (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.EdgeLightColor);
                _edgeLight.SetLightColor(c);
            }
            catch
            {
                _edgeLight.SetLightColor(Colors.White);
            }
        }

        private void ApplyThemeAndGeometry()
        {
            FontFamily = LocalizationService.CurrentFontFamily;
            FlowDirection = LocalizationService.CurrentFlowDirection;
            if (_expandedContent != null)
            {
                _expandedContent.FlowDirection = LocalizationService.CurrentFlowDirection;
            }
            var p = CapsuleTheme.Current;
            _notchBackdropPath.Fill = CapsuleTheme.Brush(p.Bg);

            // Sync OpensOnClick to state machine
            _machine.OpensOnClick = AppPreferences.Instance.OpensOnClick;

            // 1. Position on selected display
            PositionAtTopCenter();

            // 2. Resting clock, layout, Pet, Battery, Bluetooth
            UpdateRestingClockAndStatus();

            // 3. Edge light
            if (AppPreferences.Instance.EnableEdgeLight)
            {
                _edgeLight.Visibility = Visibility.Visible;
                _edgeLight.Configure(
                    AppPreferences.Instance.EdgeLightPattern,
                    AppPreferences.Instance.EdgeLightSpeed,
                    AppPreferences.Instance.EdgeLightTrigger,
                    AppPreferences.Instance.NotchStyle);
                UpdateEdgeLightEffectiveColor();
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
            _collapsedDateText.Foreground = CapsuleTheme.Brush(p.Text2);

            UpdateCollapsedViewState();
            RebuildNavTabs();
            NavigateToTool(_activeToolId);
            UpdateMorphVisuals(_morphProgress);
        }

        public void ToggleFromExternal()
        {
            if (_isTucked) SetTucked(false);
            if (_isAutoHiddenByVisibility) SetAutoHidden(false);
            _machine.Toggle("tray-or-cli");
        }
    }
}
