using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
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
        public const double MediaCollapsedWidth = 348.0;
        public const double MediaCollapsedHeight = 52.0;
        public const double MediaWideCollapsedWidth = 398.0;
        public const double MediaWideCollapsedHeight = 86.0;
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
        private readonly Border _navDockBorder;
        private readonly ScrollViewer _navScrollViewer;
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

        // 2A. Collapsed Compact Live Media View (Responsive Width x 54 - Normal Music Playing)
        private readonly Grid _collapsedMediaView;
        private readonly AlbumArtThumbnail _albumArtThumb;
        private readonly TextBlock _mediaCombinedText;
        private readonly Run _mediaTrackRun;
        private readonly Run _mediaSepRun;
        private readonly Run _mediaArtistRun;
        private readonly MiniEqualizerControl _miniEqualizer;
        private readonly WaveformProgressBar _waveformProgress;

        // 2B. Collapsed Wide Live Media Card (398 x 90 - Track Switch / Play 4s Popup)
        private readonly Grid _collapsedMediaWideView;
        private readonly AlbumArtThumbnail _wideAlbumArtThumb;
        private readonly TextBlock _wideMediaTrackText;
        private readonly TextBlock _wideMediaArtistText;
        private readonly Grid _wideScrubberHost;
        private readonly Border _wideProgressTrack;
        private readonly Border _wideProgressFill;
        private readonly Ellipse _wideProgressThumb;
        private readonly TranslateTransform _wideProgressThumbTranslate = new(0, 0);
        private readonly TextBlock _wideRemainingText;
        private readonly WaveformProgressBar _wideWaveformProgress;

        // 3. Collapsed Dynamic Banner View (Copied, Screenshot, Bluetooth, Windows Notifications)
        private readonly Grid _collapsedBannerView;
        private readonly TranslateTransform _bannerTranslate = new(0, 0);
        private readonly ScaleTransform _bannerScale = new(1, 1);
        private int _bannerSequenceId = 0;
        private readonly AnimateIcon _bannerAnimateIcon;
        private readonly Border _bannerAppIconBorder;
        private readonly Image _bannerAppImage;
        private readonly TextBlock _bannerAppName;
        private readonly TextBlock _bannerIcon;
        private readonly TextBlock _bannerTitle;
        private readonly TextBlock _bannerSubtitle;
        private readonly StackPanel _bannerTextStack;

        // 4. Collapsed Message Banner View (Flashed from Message tool)
        private readonly Grid _collapsedMessageView;
        private readonly StackPanel _messageNotchStack;
        private readonly TextBlock _messageNotchText;
        private readonly DispatcherTimer _messageDismissTimer;
        private DispatcherTimer _messageTypeOnTimer;
        private bool _isMessageActive = false;
        private bool _isMessagePinned = false;

        // 5A. Collapsed Standalone Timer View (when NO music is playing: Left = Clock Icon, Center = Title, Right = Timer/Stopwatch digits)
        private readonly Grid _collapsedTimerView;
        private readonly Path _collapsedTimerLeftRingTrack;
        private readonly Path _collapsedTimerLeftRingArc;
        private readonly Path _collapsedTimerClockIcon;
        private readonly TextBlock _collapsedTimerTitleText;
        private readonly TextBlock _collapsedTimerDigitsText;

        // 5B. Detached Split Dynamic Island Capsules (when Music IS playing + Timer is active)
        // Left Capsule: Clock Icon + Circular Progress Ring around capsule perimeter (depletes with timer)
        // Right Capsule: Timer / Stopwatch countdown or elapsed digits
        private readonly Border _leftTimerIsland;
        private readonly Path _leftIslandRingTrack;
        private readonly Path _leftIslandRingArc;
        private readonly Path _leftIslandClockIcon;
        private readonly TranslateTransform _leftIslandTranslate = new(0, 0);
        private readonly ScaleTransform _leftIslandScale = new(0.8, 0.8);

        private readonly Border _rightTimerIsland;
        private readonly TextBlock _rightIslandTimeText;
        private readonly TranslateTransform _rightIslandTranslate = new(0, 0);
        private readonly ScaleTransform _rightIslandScale = new(0.8, 0.8);

        // 5C. Detached Music Satellite Capsule (when Notification arrives while Music IS playing)
        // Album Art Image (circular) + pulsating beat ring + mini equalizer
        private readonly Border _musicSatelliteIsland;
        private readonly Grid _musicSatelliteArtGrid;
        private readonly Image _musicSatelliteArtworkImage;
        private readonly TextBlock _musicSatelliteGlyph;
        private readonly Ellipse _musicSatelliteBeatRing;
        private readonly MiniEqualizerControl _musicSatelliteEqualizer;
        private readonly TranslateTransform _musicSatelliteTranslate = new(0, 0);
        private readonly ScaleTransform _musicSatelliteScale = new(0.8, 0.8);
        private double _musicSatelliteProgress = 0.0;
        private double _musicSatelliteTarget = 0.0;
        private BitmapSource _currentArtworkSource;

        private double _splitIslandsProgress = 0.0;
        private double _splitIslandsTarget = 0.0;
        private bool _lastTimerActiveState = false;
        private Color _cachedTimerAccentColor = Colors.Transparent;
        private SolidColorBrush _cachedTimerAccentBrush;
        private long _lastMorphTickTimestamp;

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
        private bool _isTuckCompletedHidden = false;
        private bool _isSmartPeekHovered = false;
        private readonly TranslateTransform _capsuleCardTranslate = new(0, 0);
        private readonly DispatcherTimer _mediaSettleExitTimer;
        private readonly DispatcherTimer _dynamicVisibilityTimer;

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
                HorizontalAlignment = HorizontalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
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
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                FlowDirection = FlowDirection.LeftToRight
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

            // View 1C: Standalone Active Timer / Stopwatch View (when NO music is playing: Left = Clock Icon, Center = Title, Right = Digits)
            _collapsedTimerView = new Grid
            {
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FlowDirection = FlowDirection.LeftToRight
            };
            _collapsedTimerView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedTimerView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _collapsedTimerView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var timerLeftIconHost = new Grid
            {
                Width = 22,
                Height = 22,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _collapsedTimerLeftRingTrack = new Path
            {
                Data = new EllipseGeometry(new Point(11, 11), 9.5, 9.5),
                Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                StrokeThickness = 2.0
            };
            _collapsedTimerLeftRingArc = new Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 159, 10)),
                StrokeThickness = 2.0,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _collapsedTimerClockIcon = new Path
            {
                Data = Geometry.Parse("M 11,5.5 A 5.5,5.5 0 1 1 10.99,5.5 M 11,7.8 L 11,11 L 13.3,12.3"),
                Stroke = new SolidColorBrush(Color.FromRgb(255, 159, 10)),
                StrokeThickness = 1.4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            timerLeftIconHost.Children.Add(_collapsedTimerLeftRingTrack);
            timerLeftIconHost.Children.Add(_collapsedTimerLeftRingArc);
            timerLeftIconHost.Children.Add(_collapsedTimerClockIcon);
            Grid.SetColumn(timerLeftIconHost, 0);
            _collapsedTimerView.Children.Add(timerLeftIconHost);

            _collapsedTimerTitleText = new TextBlock
            {
                Text = "",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 120,
                Margin = new Thickness(6, 0, 6, 0)
            };
            Grid.SetColumn(_collapsedTimerTitleText, 1);
            _collapsedTimerView.Children.Add(_collapsedTimerTitleText);

            _collapsedTimerDigitsText = new TextBlock
            {
                Text = "25:00",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 159, 10)),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedTimerDigitsText, 2);
            _collapsedTimerView.Children.Add(_collapsedTimerDigitsText);

            _collapsedTimerView.MouseLeftButtonUp += (_, e) =>
            {
                if (_morphProgress <= 0.1)
                {
                    e.Handled = true;
                    NavigateToTool("focus");
                    _edgeLight.TriggerBurst(4.5);
                    _machine.Click("timer-notch-click");
                }
            };
            _collapsedTimerView.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                WindowsBuddy.Tools.Timer.TimerService.Instance.ToggleActiveNotchTimer();
            };

            _collapsedContent.Children.Add(_collapsedTimerView);

            // View 2A: Compact Live Media Playback View (312 x 50, perfectly fitted inside margins)
            _collapsedMediaView = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                RenderTransform = new TranslateTransform(0, 0)
            };
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Album Art (22x22) | Text | Mini Equalizer
            var compactTopGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 2)
            };
            compactTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            compactTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            compactTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _albumArtThumb = new AlbumArtThumbnail(22.0, 5.5)
            {
                Margin = new Thickness(2, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_albumArtThumb, 0);
            compactTopGrid.Children.Add(_albumArtThumb);

            _mediaCombinedText = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left
            };
            _mediaTrackRun = new Run("Grounds for Hope")
            {
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            };
            _mediaSepRun = new Run(" – ")
            {
                FontSize = 11.5,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 205))
            };
            _mediaArtistRun = new Run("NOTSOBAD")
            {
                FontSize = 11.5,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 210, 225))
            };
            _mediaCombinedText.Inlines.Add(_mediaTrackRun);
            _mediaCombinedText.Inlines.Add(_mediaSepRun);
            _mediaCombinedText.Inlines.Add(_mediaArtistRun);
            Grid.SetColumn(_mediaCombinedText, 1);
            compactTopGrid.Children.Add(_mediaCombinedText);

            _miniEqualizer = new MiniEqualizerControl(prominent: false)
            {
                Cursor = Cursors.Hand,
                ToolTip = "Play / Pause",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0)
            };
            _miniEqualizer.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                MediaService.Instance.TogglePlayPause();
            };
            Grid.SetColumn(_miniEqualizer, 2);
            compactTopGrid.Children.Add(_miniEqualizer);

            Grid.SetRow(compactTopGrid, 0);
            _collapsedMediaView.Children.Add(compactTopGrid);

            // Row 1: Symmetrical Audio Spectrum (safe inset strictly within the notch flat bottom baseline)
            _waveformProgress = new WaveformProgressBar(WaveformProgressBar.CompactBarCount)
            {
                Height = 8.0,
                Margin = new Thickness(14, 1, 14, 5)
            };
            Grid.SetRow(_waveformProgress, 1);
            _collapsedMediaView.Children.Add(_waveformProgress);

            // Right-clicking compact media pill shows the Wide Music Card on demand
            _collapsedMediaView.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                ActivateMediaWideCard();
            };

            _collapsedContent.Children.Add(_collapsedMediaView);

            // View 2B: Wide Music Card on Track Switch / Play (392 x 84)
            _collapsedMediaWideView = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                RenderTransform = new TranslateTransform(0, 0)
            };
            _collapsedMediaWideView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _collapsedMediaWideView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var wideTopGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 3)
            };
            wideTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            wideTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _wideAlbumArtThumb = new AlbumArtThumbnail(38.0, 8.0)
            {
                Margin = new Thickness(4, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_wideAlbumArtThumb, 0);
            wideTopGrid.Children.Add(_wideAlbumArtThumb);

            var wideRightGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            wideRightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            wideRightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            wideRightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Line 1: Track Title on left (*) + Remaining Time on right (46px fixed)
            var wideTitleRow = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            wideTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wideTitleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46.0, GridUnitType.Pixel) });

            _wideMediaTrackText = new TextBlock
            {
                Text = "Grounds for Hope",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_wideMediaTrackText, 0);
            wideTitleRow.Children.Add(_wideMediaTrackText);

            _wideRemainingText = new TextBlock
            {
                Text = "-2:55",
                FontSize = 11.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(210, 220, 235)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(_wideRemainingText, 1);
            wideTitleRow.Children.Add(_wideRemainingText);

            Grid.SetRow(wideTitleRow, 0);
            wideRightGrid.Children.Add(wideTitleRow);

            // Line 2: Artist subtitle
            _wideMediaArtistText = new TextBlock
            {
                Text = "NOTSOBAD",
                FontSize = 11.5,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(165, 175, 192)),
                Margin = new Thickness(0, 2, 0, 4),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                MaxWidth = 240.0
            };
            Grid.SetRow(_wideMediaArtistText, 1);
            wideRightGrid.Children.Add(_wideMediaArtistText);

            // Line 3: Scrubber bar
            _wideScrubberHost = new Grid
            {
                Height = 6.0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = false,
                Margin = new Thickness(0, 2, 8, 2)
            };

            _wideProgressTrack = new Border
            {
                Height = 3.5,
                CornerRadius = new CornerRadius(1.75),
                Background = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            _wideProgressTrack.SizeChanged += (_, _) => UpdateMediaTimelineVisuals();
            _wideScrubberHost.Children.Add(_wideProgressTrack);

            _wideProgressFill = new Border
            {
                Height = 3.5,
                Width = 24.0,
                CornerRadius = new CornerRadius(1.75),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            _wideScrubberHost.Children.Add(_wideProgressFill);

            _wideProgressThumb = new Ellipse
            {
                Width = 8.0,
                Height = 8.0,
                Fill = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false,
                RenderTransform = _wideProgressThumbTranslate
            };
            _wideScrubberHost.Children.Add(_wideProgressThumb);

            Grid.SetRow(_wideScrubberHost, 2);
            wideRightGrid.Children.Add(_wideScrubberHost);

            Grid.SetColumn(wideRightGrid, 1);
            wideTopGrid.Children.Add(wideRightGrid);

            Grid.SetRow(wideTopGrid, 0);
            _collapsedMediaWideView.Children.Add(wideTopGrid);

            // Row 1: Spectrum
            _wideWaveformProgress = new WaveformProgressBar(WaveformProgressBar.WideBarCount)
            {
                Height = 9.5,
                Margin = new Thickness(16, 2, 16, 6)
            };
            Grid.SetRow(_wideWaveformProgress, 1);
            _collapsedMediaWideView.Children.Add(_wideWaveformProgress);

            _collapsedContent.Children.Add(_collapsedMediaWideView);

            // View 3: Dynamic Banner View (Copied, Cut, Screenshot saved, AirPods Connected, Windows Notifications)
            _collapsedBannerView = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };
            var bannerGroup = new TransformGroup();
            bannerGroup.Children.Add(_bannerTranslate);
            bannerGroup.Children.Add(_bannerScale);
            _collapsedBannerView.RenderTransform = bannerGroup;
            var bannerContainer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _bannerAnimateIcon = new AnimateIcon(AnimateIconType.CopyCheck, size: 16, brush: Brushes.White)
            {
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                AutoAnimateOnHover = false
            };
            bannerContainer.Children.Add(_bannerAnimateIcon);

            _bannerAppIconBorder = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            };
            _bannerAppImage = new Image
            {
                Width = 24,
                Height = 24,
                Stretch = Stretch.UniformToFill
            };
            _bannerAppIconBorder.Child = _bannerAppImage;
            bannerContainer.Children.Add(_bannerAppIconBorder);

            _bannerIcon = new TextBlock
            {
                Text = "📋",
                FontSize = 13.5,
                Margin = new Thickness(0, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            bannerContainer.Children.Add(_bannerIcon);

            _bannerTextStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 270
            };

            _bannerAppName = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(152, 152, 160)),
                Margin = new Thickness(0, 0, 5, 0),
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            _bannerTextStack.Children.Add(_bannerAppName);

            _bannerTitle = new TextBlock
            {
                Text = "Copied",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 200
            };
            _bannerTextStack.Children.Add(_bannerTitle);

            _bannerSubtitle = new TextBlock
            {
                Text = " · Image",
                FontSize = 12.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                MaxWidth = 220,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 0)
            };
            _bannerTextStack.Children.Add(_bannerSubtitle);

            bannerContainer.Children.Add(_bannerTextStack);
            _collapsedBannerView.Children.Add(bannerContainer);
            _collapsedContent.Children.Add(_collapsedBannerView);

            // View 4: Collapsed Message Banner View (Flashed from Message tool)
            _collapsedMessageView = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true,
                FlowDirection = FlowDirection.LeftToRight
            };
            _collapsedMessageView.SizeChanged += (s, e) =>
            {
                if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
                {
                    double radius = e.NewSize.Height / 2.0;
                    _collapsedMessageView.Clip = new RectangleGeometry(
                        new Rect(0, 0, e.NewSize.Width, e.NewSize.Height),
                        radius,
                        radius);
                }
            };

            _messageNotchStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.LeftToRight
            };
            var msgIcon = new AnimateIcon(AnimateIconType.Sparkles, size: 14, brush: new SolidColorBrush(Color.FromRgb(245, 166, 35)))
            {
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center,
                AutoAnimateOnHover = false
            };
            _messageNotchStack.Children.Add(msgIcon);

            _messageNotchText = new TextBlock
            {
                Text = "",
                FontSize = 12.0,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 440
            };
            _messageNotchStack.Children.Add(_messageNotchText);
            _collapsedMessageView.Children.Add(_messageNotchStack);

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
                ClipToBounds = true,
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
            _navTabsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _navScrollViewer = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.HorizontalOnly,
                Focusable = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Content = _navTabsPanel
            };

            // Enable mouse-wheel and trackpad horizontal scrolling
            _navScrollViewer.PreviewMouseWheel += (s, e) =>
            {
                if (e.Delta != 0)
                {
                    double targetOffset = _navScrollViewer.HorizontalOffset - (e.Delta * 0.6);
                    _navScrollViewer.ScrollToHorizontalOffset(targetOffset);
                    e.Handled = true;
                }
            };

            _navDockBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.DockPill),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(17),
                Padding = new Thickness(6, 4, 6, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 660,
                Child = _navScrollViewer
            };
            RebuildNavTabs();
            Grid.SetRow(_navDockBorder, 2);
            _expandedContent.Children.Add(_navDockBorder);

            _capsuleCard.Children.Add(_notchBackdropPath);
            _capsuleCard.Children.Add(_notchOutlinePath);
            _capsuleCard.Children.Add(_notchTopBlackLine);
            _capsuleCard.Children.Add(_edgeLight);
            _capsuleCard.Children.Add(_collapsedContent);
            _capsuleCard.Children.Add(_collapsedMessageView);
            _capsuleCard.Children.Add(_expandedContent);

            // -------------------------------------------------------------
            // Detached Split Dynamic Island Capsules (when Music is playing + Timer is active)
            // Left Capsule: Circular capsule with Clock Icon + Depleting Circular Progress Bar around its rounded perimeter
            // Right Capsule: Pill capsule with Timer / Stopwatch digits
            // -------------------------------------------------------------
            var leftTransformGroup = new TransformGroup();
            leftTransformGroup.Children.Add(_leftIslandScale);
            leftTransformGroup.Children.Add(_leftIslandTranslate);

            var leftIslandGrid = new Grid { Width = 36, Height = 36 };
            _leftIslandRingTrack = new Path
            {
                Data = new EllipseGeometry(new Point(18, 18), 16.2, 16.2),
                Stroke = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)),
                StrokeThickness = 2.6
            };
            _leftIslandRingArc = new Path
            {
                Stroke = new SolidColorBrush(Color.FromRgb(255, 159, 10)),
                StrokeThickness = 2.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _leftIslandClockIcon = new Path
            {
                Data = Geometry.Parse("M 18,10.5 A 7.5,7.5 0 1 1 17.99,10.5 M 18,13.5 L 18,18 L 21.2,19.8"),
                Stroke = Brushes.White,
                StrokeThickness = 1.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round
            };
            leftIslandGrid.Children.Add(_leftIslandRingTrack);
            leftIslandGrid.Children.Add(_leftIslandRingArc);
            leftIslandGrid.Children.Add(_leftIslandClockIcon);

            _leftTimerIsland = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = CapsuleTheme.Brush(p.Bg),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4.5, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = leftTransformGroup,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                ToolTip = "Click: Open Timers · Right-click: Pause / Resume",
                Child = leftIslandGrid
            };
            _leftTimerIsland.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                NavigateToTool("focus");
                _edgeLight.TriggerBurst(4.5);
                _machine.Click("left-timer-island");
            };
            _leftTimerIsland.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                WindowsBuddy.Tools.Timer.TimerService.Instance.ToggleActiveNotchTimer();
            };

            var rightTransformGroup = new TransformGroup();
            rightTransformGroup.Children.Add(_rightIslandScale);
            rightTransformGroup.Children.Add(_rightIslandTranslate);

            _rightIslandTimeText = new TextBlock
            {
                Text = "25:00",
                FontSize = 13.0,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas, Segoe UI"),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 159, 10)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _rightTimerIsland = new Border
            {
                Height = 36,
                Width = 68,
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(10, 0, 10, 0),
                Background = CapsuleTheme.Brush(p.Bg),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 4.5, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = rightTransformGroup,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                ToolTip = "Click: Open Timers · Right-click: Pause / Resume",
                Child = _rightIslandTimeText
            };
            _rightTimerIsland.SizeChanged += (_, _) =>
            {
                if (_splitIslandsProgress > 0.005)
                {
                    UpdateMorphVisuals(_morphProgress);
                }
            };
            _rightTimerIsland.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                NavigateToTool("focus");
                _edgeLight.TriggerBurst(4.5);
                _machine.Click("right-timer-island");
            };
            _rightTimerIsland.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                WindowsBuddy.Tools.Timer.TimerService.Instance.ToggleActiveNotchTimer();
            };

            // 5C. Detached Music Satellite Capsule (when Notification arrives while Music IS playing)
            var musicSatTransformGroup = new TransformGroup();
            musicSatTransformGroup.Children.Add(_musicSatelliteScale);
            musicSatTransformGroup.Children.Add(_musicSatelliteTranslate);

            var musicSatPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _musicSatelliteArtGrid = new Grid
            {
                Width = 24,
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };

            _musicSatelliteBeatRing = new Ellipse
            {
                Width = 26,
                Height = 26,
                Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                StrokeThickness = 1.2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _musicSatelliteArtworkImage = new Image
            {
                Width = 24,
                Height = 24,
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Clip = new EllipseGeometry(new Point(12, 12), 12, 12)
            };

            _musicSatelliteGlyph = new TextBlock
            {
                Text = "🎵",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };

            _musicSatelliteArtGrid.Children.Add(_musicSatelliteBeatRing);
            _musicSatelliteArtGrid.Children.Add(_musicSatelliteArtworkImage);
            _musicSatelliteArtGrid.Children.Add(_musicSatelliteGlyph);

            _musicSatelliteEqualizer = new MiniEqualizerControl
            {
                Cursor = Cursors.Hand,
                ToolTip = "Music Visualizer",
                Margin = new Thickness(4, 0, 7, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            musicSatPanel.Children.Add(_musicSatelliteArtGrid);
            musicSatPanel.Children.Add(_musicSatelliteEqualizer);

            _musicSatelliteIsland = new Border
            {
                Width = 58,
                Height = 34,
                CornerRadius = new CornerRadius(17),
                Background = CapsuleTheme.Brush(p.Bg),
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 5, 0, 0),
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = musicSatTransformGroup,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                ToolTip = "Music Playing · Click: Play / Pause · Right-click: Next track",
                Child = musicSatPanel
            };
            _musicSatelliteIsland.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                MediaService.Instance.TogglePlayPause();
                _edgeLight.TriggerBurst(4.5);
            };
            _musicSatelliteIsland.MouseRightButtonUp += (_, e) =>
            {
                e.Handled = true;
                MediaService.Instance.NextTrack();
                _edgeLight.TriggerBurst(4.5);
            };

            // Add detached islands before _capsuleCard so they emerge smoothly from behind the main notch
            _envelopeRoot.Children.Add(_musicSatelliteIsland);
            _envelopeRoot.Children.Add(_leftTimerIsland);
            _envelopeRoot.Children.Add(_rightTimerIsland);
            _envelopeRoot.Children.Add(_capsuleCard);
            Content = _envelopeRoot;

            // Morph Animation timer (Render priority for 60fps smooth transitions)
            _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _morphTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(12) };
            _morphTimer.Tick += OnMorphTick;

            // Keep the notch open while a dropdown/context menu or modal dialog from a tool is showing
            NotchInteractionGuard.Register();
            _machine.HoldOpen = () => NotchInteractionGuard.IsHeld;
            NotchInteractionGuard.Released += () => Dispatcher.BeginInvoke(new Action(() => _machine.ReevaluateHold()));

            _machine.PhaseChanged += (_, next, _) =>
            {
                if (next == NotchPhase.Opening || next == NotchPhase.Open)
                {
                    if (_viewportHost.Child == null)
                    {
                        NavigateToTool(_activeToolId);
                    }
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
                if (_isSmartPeekHovered)
                {
                    _isSmartPeekHovered = false;
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

            // Snappy 120ms dynamic visibility evaluation for intelligent app obstruction detection and top-edge hover
            _dynamicVisibilityTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _dynamicVisibilityTimer.Tick += (_, _) => EvaluateDynamicVisibility();
            _dynamicVisibilityTimer.Start();

            // 30fps Real-time Spectrum & Equalizer Animation Timer (active during playback and smooth decay when stopped)
            _spectrumTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _spectrumTimer.Tick += (_, _) =>
            {
                if (!_isMusicActive)
                {
                    bool miniSettled = _miniEqualizer.DecayLevels();
                    _musicSatelliteEqualizer?.DecayLevels();
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
                _musicSatelliteEqualizer?.SetLevels(_miniBars4, true);

                if (_isMediaWideActive)
                {
                    AudioSpectrumService.MapBands(_fftBands28, _mappedBars56, WaveformProgressBar.WideBarCount);
                    _wideWaveformProgress.SetSpectrumLevels(_mappedBars56, true, isAudioLive, _spectrumPhase);
                    UpdateMediaTimelineVisuals();
                }
                else
                {
                    AudioSpectrumService.MapBands(_fftBands28, _mappedBars40, WaveformProgressBar.CompactBarCount);
                    _waveformProgress.SetSpectrumLevels(_mappedBars40, true, isAudioLive, _spectrumPhase);
                }
            };

            // Startup memory compaction: trims initial JIT/assembly load working set after 6 seconds
            var startupTrimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6.0) };
            startupTrimTimer.Tick += (_, _) =>
            {
                startupTrimTimer.Stop();
                TrimWorkingSet();
            };
            startupTrimTimer.Start();

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
                    var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                    var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease };
                    fadeOut.Completed += (_, _) =>
                    {
                        _isMediaWideActive = false;
                        _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                        _collapsedMediaWideView.BeginAnimation(OpacityProperty, null);
                        _collapsedMediaWideView.Opacity = 1.0;
                        UpdateCollapsedViewState(animateCompactEntrance: true);
                    };
                    _collapsedMediaWideView.BeginAnimation(OpacityProperty, fadeOut);
                }
            };

            // Notification Banner Dismissal Timer
            _bannerDismissTimer = new DispatcherTimer();
            _bannerDismissTimer.Tick += (_, _) =>
            {
                _bannerDismissTimer.Stop();
                AnimateBannerExit();
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

            // Hook Real Windows Notifications (Toasts from all apps: Telegram, WhatsApp, Chrome, Discord, System)
            var winNotifService = WindowsNotificationListenerService.Instance;
            winNotifService.NotificationReceived += OnWindowsNotificationReceived;
            Loaded += async (_, _) =>
            {
                _ = Dispatcher.InvokeAsync(() =>
                {
                    if (_viewportHost.Child == null)
                    {
                        NavigateToTool(_activeToolId);
                    }
                }, DispatcherPriority.Background);

                try
                {
                    await winNotifService.StartAsync();
                }
                catch { }
            };

            // Hook Live Timer / Stopwatch / Pomodoro Events for Standalone & Split Dynamic Island Capsules
            var timerService = WindowsBuddy.Tools.Timer.TimerService.Instance;
            timerService.Tick += () => Dispatcher.InvokeAsync(OnTimerServiceUpdated);
            timerService.StateChanged += () => Dispatcher.InvokeAsync(OnTimerServiceUpdated);

            CapsuleTheme.ThemeChanged += () => Dispatcher.InvokeAsync(() =>
            {
                RequestApplyThemeAndGeometry();
                RebuildNavTabs();
                if (_machine.Phase == NotchPhase.Open)
                {
                    NavigateToTool(_activeToolId);
                }
            });
            AppPreferences.Instance.Changed += RequestApplyThemeAndGeometry;
            LocalizationService.LanguageChanged += () => Dispatcher.InvokeAsync(() =>
            {
                RequestApplyThemeAndGeometry();
                RebuildNavTabs();
                UpdateToolHeaderActions(_activeToolId);
                if (_machine.Phase == NotchPhase.Open)
                {
                    NavigateToTool(_activeToolId);
                }
            });

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
                        Key.D9 => "calendar",
                        Key.D0 => "units",
                        Key.C => "calendar",
                        Key.U => "units",
                        Key.L => "links",
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

        private static void LogNotif(string msg)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mayli", "logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "notifications.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CapsuleWindow] {msg}\r\n");
            }
            catch { }
        }

        private void OnWindowsNotificationReceived(WindowsNotificationPayload winNotif)
        {
            LogNotif($"OnWindowsNotificationReceived: App='{winNotif.AppName}', Title='{winNotif.Title}', Msg='{winNotif.Message}', HasIcon={winNotif.AppIcon != null}");
            Dispatcher.InvokeAsync(() =>
            {
                var payload = new NotchNotificationPayload
                {
                    AppName = winNotif.AppName,
                    Title = winNotif.Title,
                    Subtitle = winNotif.Message,
                    AppIcon = winNotif.AppIcon,
                    Icon = winNotif.AppGlyph,
                    DurationMs = 5000 // 5 seconds display on the notch
                };
                OnRealNotificationRequested(payload);
            });
        }

        private void OnRealNotificationRequested(NotchNotificationPayload payload)
        {
            LogNotif($"OnRealNotificationRequested: App='{payload.AppName}', Title='{payload.Title}', Icon='{payload.Icon}'");
            Dispatcher.InvokeAsync(() =>
            {
                // Live Activity filter checks
                if ((payload.Icon == "⚡" || payload.Icon == "🔋") && !AppPreferences.Instance.ShowBatteryActivity)
                    return;

                if (payload.Icon == "🔊" && !AppPreferences.Instance.VolumeActivity)
                    return;

                if ((payload.Icon == "📋" || payload.Icon == "📁" || payload.Icon == "✂️") && !AppPreferences.Instance.ClipboardCopiedActivity)
                    return;

                if (payload.Icon == "⏱" && !AppPreferences.Instance.TimerFinishAlerts && !AppPreferences.Instance.RunningTimersActivity)
                    return;

                if (payload.Icon == "📅" && !AppPreferences.Instance.CalendarAlerts)
                    return;

                _isBannerActive = true;
                _bannerTitle.Text = payload.Title ?? "";
                _bannerSubtitle.Text = payload.Subtitle ?? "";

                if (!string.IsNullOrWhiteSpace(payload.AppName))
                {
                    _bannerAppName.Text = payload.AppName + " · ";
                    _bannerAppName.Visibility = Visibility.Visible;
                }
                else
                {
                    _bannerAppName.Visibility = Visibility.Collapsed;
                }

                // Ensure collapsed content root is fully visible and opaque
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedContent.Opacity = 1.0;

                // Display banner view before icon play so elements are in the visual presentation tree
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;
                AnimateBannerEntrance();

                if (payload.AppIcon != null)
                {
                    _bannerAppImage.Source = payload.AppIcon;
                    _bannerAppIconBorder.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                }
                else if (payload.Title == "Cut" || payload.Icon == "✂️")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.IconType = AnimateIconType.CutScissors;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Title == "Copied" || payload.Icon == "📋" || payload.Icon == "📎" || payload.Icon == "📁")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.IconType = (payload.Title == "Cut") ? AnimateIconType.CutScissors : AnimateIconType.CopyCheck;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🎧" || (payload.Title != null && payload.Title.Contains("Connected", StringComparison.OrdinalIgnoreCase)))
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.IconType = AnimateIconType.Bluetooth;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🔊" || payload.Icon == "🔈")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.IconType = AnimateIconType.Volume;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "⏱" || payload.Icon == "⏰")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.IconType = AnimateIconType.Timer;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Text = !string.IsNullOrEmpty(payload.Icon) ? payload.Icon : "🔔";
                }

                // PRIORITY HANDLING: Notification (5s) > Timer > Music
                // 1. Music Satellite Island: if music is currently active/playing, album art detaches smoothly to the left!
                bool isMusicPlaying = _isMusicActive || _isMediaRetained || RealtimeNotchActionService.Instance.IsMusicPlaying;
                if (isMusicPlaying)
                {
                    UpdateMusicSatelliteArtwork();
                    _musicSatelliteTarget = 1.0;
                }
                else
                {
                    _musicSatelliteTarget = 0.0;
                }

                // 2. Timer Priority: if timer is active, timer digits detach to the right island so timer is never lost!
                var timerSnap = WindowsBuddy.Tools.Timer.TimerService.Instance.GetActiveNotchTimer();
                bool isTimerActive = timerSnap.IsActive && AppPreferences.Instance.RunningTimersActivity;
                if (isTimerActive)
                {
                    UpdateTimerNotchVisuals(timerSnap);
                    _splitIslandsTarget = 1.0;
                }
                else
                {
                    _splitIslandsTarget = 0.0;
                }

                bool isCopyOrCut = payload.Title == "Copied" || payload.Title == "Cut" || payload.Icon == "📋" || payload.Icon == "✂️" || payload.Icon == "📁";
                if (isCopyOrCut)
                {
                    GetConfiguredRestingDimensions(out double rw, out double rh);
                    if (isMusicPlaying)
                    {
                        double satW = Math.Round(rh * 1.68);
                        double gap = 9.0;
                        double copyMainW = Math.Max(152.0, rw - satW - gap);
                        _currentBaseCollapsedWidth = copyMainW;
                        _currentBaseCollapsedHeight = rh;
                        _bannerSubtitle.MaxWidth = Math.Max(40.0, copyMainW - 96.0);
                    }
                    else
                    {
                        _currentBaseCollapsedWidth = rw;
                        _currentBaseCollapsedHeight = rh;
                        _bannerSubtitle.MaxWidth = Math.Max(60.0, rw - 110.0);
                    }
                }
                else
                {
                    bool hasRichNotif = payload.AppIcon != null || !string.IsNullOrWhiteSpace(payload.AppName);
                    _currentBaseCollapsedWidth = hasRichNotif ? 340.0 : BannerCollapsedWidth;
                    _currentBaseCollapsedHeight = hasRichNotif ? 44.0 : BannerCollapsedHeight;
                    _bannerSubtitle.MaxWidth = 220.0;
                }

                _edgeLight.TriggerBurst(4.2);
                EvaluateDynamicVisibility();

                if (_morphProgress <= 0.1)
                {
                    _targetProgress = 0.0;
                    _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                    _morphTimer.Start();
                }

                int durationMs = payload.DurationMs > 0 ? payload.DurationMs : 5000;
                _bannerDismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
                _bannerDismissTimer.Stop();
                _bannerDismissTimer.Start();
            });
        }

        private void UpdateMusicSatelliteArtwork()
        {
            if (_currentArtworkSource != null)
            {
                _musicSatelliteArtworkImage.Source = _currentArtworkSource;
                _musicSatelliteArtworkImage.Visibility = Visibility.Visible;
                _musicSatelliteGlyph.Visibility = Visibility.Collapsed;
            }
            else
            {
                _musicSatelliteArtworkImage.Source = null;
                _musicSatelliteArtworkImage.Visibility = Visibility.Collapsed;
                _musicSatelliteGlyph.Visibility = Visibility.Visible;
            }

            Color beatColor = _currentSongBeatColor.A > 0 ? _currentSongBeatColor : Color.FromRgb(255, 60, 90);
            _musicSatelliteBeatRing.Stroke = new SolidColorBrush(Color.FromArgb(160, beatColor.R, beatColor.G, beatColor.B));
            _musicSatelliteEqualizer?.SetBeatColor(beatColor);
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
                    if (_musicSatelliteEqualizer != null) _musicSatelliteEqualizer.IsPlaying = false;
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
                        // Music paused/stopped: smoothly decay equalizer and keep media card alive for 3.5s before morphing
                        _spectrumTimer.Start();
                        _mediaSettleExitTimer.Stop();
                        _mediaSettleExitTimer.Interval = TimeSpan.FromSeconds(3.5);
                        _mediaSettleExitTimer.Start();
                    }
                }
                string track = !string.IsNullOrWhiteSpace(snapshot.Title)
                    ? snapshot.Title
                    : (!string.IsNullOrWhiteSpace(_lastObservedTrack) ? _lastObservedTrack : "");
                string artist = !string.IsNullOrWhiteSpace(snapshot.Artist)
                    ? snapshot.Artist
                    : (!string.IsNullOrWhiteSpace(_lastObservedArtist) ? _lastObservedArtist : "");

                // Determine beat color based on user's Music color setting
                Color beatColor = snapshot.BeatColor;
                bool isCustomBeatColor = !string.Equals(AppPreferences.Instance.MusicBeatColor, "Artwork", StringComparison.OrdinalIgnoreCase);
                if (isCustomBeatColor)
                {
                    try
                    {
                        beatColor = (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.MusicBeatColor);
                    }
                    catch { isCustomBeatColor = false; }
                }

                // Compact media view (312 x 50)
                _mediaTrackRun.Text = track;
                bool hasArtist = !string.IsNullOrWhiteSpace(artist);
                _mediaSepRun.Text = hasArtist ? " – " : string.Empty;
                _mediaArtistRun.Text = hasArtist ? artist : string.Empty;

                _miniEqualizer.IsPlaying = snapshot.IsPlaying;
                _miniEqualizer.SetBeatColor(beatColor);
                if (_musicSatelliteEqualizer != null)
                {
                    _musicSatelliteEqualizer.IsPlaying = snapshot.IsPlaying;
                    _musicSatelliteEqualizer.SetBeatColor(beatColor);
                }
                _waveformProgress.SetBeatColor(beatColor, isCustomBeatColor);
                _currentArtworkSource = snapshot.Artwork;
                _albumArtThumb.SetArtwork(snapshot.Artwork, artist);

                // Wide media card view (392 x 84)
                _wideMediaTrackText.Text = track;
                _wideMediaArtistText.Text = hasArtist ? artist : "Playing Now";
                _wideWaveformProgress.SetBeatColor(beatColor, isCustomBeatColor);
                _wideAlbumArtThumb.SetArtwork(snapshot.Artwork, artist);
                UpdateWideScrubberTheme(beatColor, isCustomBeatColor);

                if (_musicSatelliteTarget > 0.005)
                {
                    UpdateMusicSatelliteArtwork();
                }

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
                    bool wasWide = _isMediaWideActive;
                    _isMediaWideActive = false;
                    _mediaWideDismissTimer.Stop();
                    if ((wasWide || !_isMediaRetained) && !_isBannerActive)
                    {
                        UpdateCollapsedViewState(animateCompactEntrance: wasWide && _isMediaRetained);
                    }
                }
                else
                {
                    // Whenever a song starts playing or track switches, expand the notch to show track & progress bar, then settle
                    bool shouldExpandWide = (!wasPlaying && _isMusicActive) || (snapshot.IsTrackSwitch && trackChanged);
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
                AnimateViewEntrance(_collapsedMediaWideView, 0.0);
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

            string remStr = FormatRemaining(remaining);
            if (!string.Equals(_wideRemainingText.Text, remStr, StringComparison.Ordinal))
            {
                _wideRemainingText.Text = remStr;
            }

            double trackWidth = _wideProgressTrack.ActualWidth > 10.0 ? _wideProgressTrack.ActualWidth : 200.0;
            double ratio = dur > 0.1 ? Math.Clamp(pos / dur, 0.0, 1.0) : 0.12;
            double fillW = Math.Clamp(trackWidth * ratio, 3.0, trackWidth);
            _wideProgressFill.Width = fillW;

            double thumbX = Math.Clamp(fillW - 3.5, 0.0, Math.Max(0.0, trackWidth - 7.0));
            _wideProgressThumbTranslate.X = thumbX;
        }

        private void UpdateWideScrubberTheme(Color beatColor, bool isCustom)
        {
            Color cStart, cMid, cEnd;
            if (isCustom)
            {
                cStart = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.72 + 255 * 0.28, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.72 + 255 * 0.28, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.72 + 255 * 0.28, 0, 255));
                cMid = beatColor;
                cEnd = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.50 + 255 * 0.50, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.55 + 255 * 0.45, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.60 + 255 * 0.40, 0, 255));
            }
            else
            {
                cStart = Color.FromRgb(
                    (byte)Math.Clamp(214 * 0.68 + beatColor.R * 0.32, 0, 255),
                    (byte)Math.Clamp(146 * 0.68 + beatColor.G * 0.32, 0, 255),
                    (byte)Math.Clamp(255 * 0.72 + beatColor.B * 0.28, 0, 255));
                cMid = Color.FromRgb(
                    (byte)Math.Clamp(156 * 0.68 + beatColor.R * 0.32, 0, 255),
                    (byte)Math.Clamp(132 * 0.68 + beatColor.G * 0.32, 0, 255),
                    (byte)Math.Clamp(255 * 0.75 + beatColor.B * 0.25, 0, 255));
                cEnd = Color.FromRgb(
                    (byte)Math.Clamp(136 * 0.68 + beatColor.R * 0.32, 0, 255),
                    (byte)Math.Clamp(198 * 0.68 + beatColor.G * 0.32, 0, 255),
                    (byte)Math.Clamp(255 * 0.78 + beatColor.B * 0.22, 0, 255));
            }

            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            grad.GradientStops.Add(new GradientStop(cStart, 0.0));
            grad.GradientStops.Add(new GradientStop(cMid, 0.52));
            grad.GradientStops.Add(new GradientStop(cEnd, 1.0));
            grad.Freeze();
            _wideProgressFill.Background = grad;
        }

        private static string FormatRemaining(double remainingSeconds)
        {
            int totalSec = Math.Max(0, (int)Math.Round(remainingSeconds));
            int mins = totalSec / 60;
            int secs = totalSec % 60;
            return $"-{mins}:{secs:D2}";
        }

        private static void AnimateViewEntrance(FrameworkElement view, double fromY = 0.0)
        {
            view.Opacity = 0.0;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = ease
            };

            if (view.RenderTransform is TranslateTransform tt && Math.Abs(fromY) > 0.01)
            {
                double safeY = Math.Clamp(fromY, -1.5, 1.5);
                var slideAnim = new DoubleAnimation(safeY, 0.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = ease
                };
                tt.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            }
            view.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void AnimateBannerEntrance()
        {
            _bannerSequenceId++;
            _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _collapsedBannerView.BeginAnimation(OpacityProperty, null);

            var cubicEase = new CubicEase { EasingMode = EasingMode.EaseOut };
            var backEase = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.25 };

            var slideAnim = new DoubleAnimation(-8.0, 0.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = cubicEase };
            var scaleAnim = new DoubleAnimation(0.92, 1.0, TimeSpan.FromMilliseconds(280)) { EasingFunction = backEase };
            var fadeAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = cubicEase };

            _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _collapsedBannerView.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private void AnimateBannerExit()
        {
            if (!_isBannerActive) return;

            int currentSeq = _bannerSequenceId;
            var ease = new CubicEase { EasingMode = EasingMode.EaseIn };

            var slideAnim = new DoubleAnimation(0.0, -7.0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
            var scaleAnim = new DoubleAnimation(1.0, 0.93, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
            var fadeAnim = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease };

            fadeAnim.Completed += (_, _) =>
            {
                if (_bannerSequenceId == currentSeq && _isBannerActive)
                {
                    _isBannerActive = false;
                    _musicSatelliteTarget = 0.0;
                    if (_isMessagePinned) _isMessageActive = true;

                    _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, null);
                    _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    _collapsedBannerView.BeginAnimation(OpacityProperty, null);
                    _bannerTranslate.Y = 0;
                    _bannerScale.ScaleX = 1.0;
                    _bannerScale.ScaleY = 1.0;
                    _collapsedBannerView.Opacity = 1.0;

                    UpdateCollapsedViewState();

                    if (_collapsedNormalView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedNormalView, 5.0);
                    }
                    else if (_collapsedMediaView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedMediaView, 5.0);
                    }
                    else if (_collapsedTimerView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedTimerView, 5.0);
                    }

                    _morphTimer.Start();
                }
            };

            _bannerTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _bannerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
            _collapsedBannerView.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private Color GetEffectiveTimerColor()
        {
            bool syncWithMusic = AppPreferences.Instance.EdgeLightSyncWithMusic ||
                                 string.Equals(AppPreferences.Instance.EdgeLightColor, "Artwork", StringComparison.OrdinalIgnoreCase);

            if (syncWithMusic && (_isMusicActive || _isMediaRetained || RealtimeNotchActionService.Instance.IsMusicPlaying))
            {
                if (!string.Equals(AppPreferences.Instance.MusicBeatColor, "Artwork", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        return (Color)ColorConverter.ConvertFromString(AppPreferences.Instance.MusicBeatColor);
                    }
                    catch { }
                }

                Color songColor = _currentSongBeatColor;
                if (songColor.A == 0 || (songColor.R == 48 && songColor.G == 209 && songColor.B == 88 && RealtimeNotchActionService.Instance.CurrentBeatColor.A > 0))
                {
                    songColor = RealtimeNotchActionService.Instance.CurrentBeatColor;
                }
                if (songColor.A > 0)
                {
                    return songColor;
                }
            }

            return CapsuleTheme.Current.Accent;
        }

        private void OnTimerServiceUpdated()
        {
            var snap = WindowsBuddy.Tools.Timer.TimerService.Instance.GetActiveNotchTimer();
            bool isActive = snap.IsActive && AppPreferences.Instance.RunningTimersActivity;

            UpdateTimerNotchVisuals(snap);

            if (isActive != _lastTimerActiveState)
            {
                _lastTimerActiveState = isActive;
                UpdateCollapsedViewState();
            }
            else if (isActive && _collapsedTimerView.Visibility == Visibility.Visible)
            {
                // Adjust width if title appeared/disappeared while timer is active
                GetConfiguredRestingDimensions(out double rw, out double rh);
                double desiredW = !string.IsNullOrWhiteSpace(snap.Title) ? Math.Max(rw, 248.0) : rw;
                if (Math.Abs(_currentBaseCollapsedWidth - desiredW) > 1.0)
                {
                    _currentBaseCollapsedWidth = desiredW;
                    _currentBaseCollapsedHeight = rh;
                    if (_morphProgress <= 0.1)
                    {
                        _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                        _morphTimer.Start();
                    }
                }
            }
        }

        private void UpdateTimerNotchVisuals(WindowsBuddy.Tools.Timer.NotchTimerSnapshot snap)
        {
            if (!snap.IsActive)
            {
                // Completely reset progress bars when timer stops or finishes so no stale arc remains
                _collapsedTimerLeftRingArc.Data = null;
                _collapsedTimerLeftRingArc.Visibility = Visibility.Collapsed;
                _leftIslandRingArc.Data = null;
                _leftIslandRingArc.Visibility = Visibility.Collapsed;
                return;
            }

            Color effectiveColor = GetEffectiveTimerColor();
            if (_cachedTimerAccentBrush == null || _cachedTimerAccentColor != effectiveColor)
            {
                _cachedTimerAccentColor = effectiveColor;
                _cachedTimerAccentBrush = new SolidColorBrush(effectiveColor);
                _cachedTimerAccentBrush.Freeze();

                _collapsedTimerClockIcon.Stroke = _cachedTimerAccentBrush;
                _collapsedTimerLeftRingArc.Stroke = _cachedTimerAccentBrush;
                _collapsedTimerDigitsText.Foreground = _cachedTimerAccentBrush;
                _leftIslandRingArc.Stroke = _cachedTimerAccentBrush;
                _leftIslandClockIcon.Stroke = _cachedTimerAccentBrush;
                _rightIslandTimeText.Foreground = _cachedTimerAccentBrush;
            }

            string displayDigits = snap.IsRunning ? snap.DisplayTime : $"❚❚ {snap.DisplayTime}";
            double ratio = snap.IsRunning ? snap.RemainingRatio : 0.0;

            // 1. Update Standalone Notch Timer View (when NO music is playing)
            DrawTimerRingArc(_collapsedTimerLeftRingArc, new Point(11, 11), 9.5, ratio);

            string wantTitle = !string.IsNullOrWhiteSpace(snap.Title) ? snap.Title : "";
            if (_collapsedTimerTitleText.Text != wantTitle)
            {
                _collapsedTimerTitleText.Text = wantTitle;
                _collapsedTimerTitleText.Visibility = string.IsNullOrEmpty(wantTitle) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (_collapsedTimerDigitsText.Text != displayDigits)
            {
                _collapsedTimerDigitsText.Text = displayDigits;
            }

            // 2. Update Detached Split Dynamic Island Capsules (when Music IS playing)
            DrawTimerRingArc(_leftIslandRingArc, new Point(18, 18), 16.2, ratio);

            if (_rightIslandTimeText.Text != displayDigits)
            {
                _rightIslandTimeText.Text = displayDigits;
                double stableWidth = displayDigits.Length <= 5 ? 66.0 : (displayDigits.Length <= 7 ? 76.0 : 88.0);
                if (Math.Abs(_rightTimerIsland.Width - stableWidth) > 0.5)
                {
                    _rightTimerIsland.Width = stableWidth;
                }
            }
        }

        private static void DrawTimerRingArc(Path targetPath, Point center, double radius, double ratio)
        {
            ratio = Math.Clamp(ratio, 0.0, 1.0);
            if (ratio <= 0.002)
            {
                targetPath.Data = null;
                targetPath.Visibility = Visibility.Collapsed;
                return;
            }

            if (targetPath.Visibility != Visibility.Visible)
            {
                targetPath.Visibility = Visibility.Visible;
            }

            double angle = Math.Min(ratio * 359.99, 359.92);
            double startAngle = -90.0;
            double endAngle = startAngle + angle;

            double startRad = startAngle * Math.PI / 180.0;
            double endRad = endAngle * Math.PI / 180.0;

            var startPt = new Point(center.X + radius * Math.Cos(startRad), center.Y + radius * Math.Sin(startRad));
            var endPt = new Point(center.X + radius * Math.Cos(endRad), center.Y + radius * Math.Sin(endRad));
            bool isLargeArc = angle > 180.0;

            if (targetPath.Data is PathGeometry existingPg &&
                existingPg.Figures.Count == 1 &&
                existingPg.Figures[0].Segments.Count == 1 &&
                existingPg.Figures[0].Segments[0] is ArcSegment existingArc)
            {
                existingPg.Figures[0].StartPoint = startPt;
                existingArc.Point = endPt;
                existingArc.Size = new Size(radius, radius);
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
                new Size(radius, radius),
                0,
                isLargeArc,
                SweepDirection.Clockwise,
                true));

            targetPath.Data = new PathGeometry(new[] { fig });
        }

        private void UpdateCollapsedViewState(bool animateCompactEntrance = false)
        {
            EvaluateDynamicVisibility();
            bool wasMediaVisible = _collapsedMediaView.Visibility == Visibility.Visible ||
                                   _collapsedMediaWideView.Visibility == Visibility.Visible;

            bool showMedia = (_isMusicActive || _isMediaRetained) && AppPreferences.Instance.ShowClosedNowPlaying;
            var timerSnap = WindowsBuddy.Tools.Timer.TimerService.Instance.GetActiveNotchTimer();
            bool showTimer = timerSnap.IsActive && AppPreferences.Instance.RunningTimersActivity;
            _lastTimerActiveState = showTimer;

            if (showTimer)
            {
                UpdateTimerNotchVisuals(timerSnap);
            }

            double prevSplitTarget = _splitIslandsTarget;
            double prevMusicSatTarget = _musicSatelliteTarget;
            _splitIslandsTarget = 0.0;
            _musicSatelliteTarget = 0.0;

            if (_isBannerActive)
            {
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;
                bool isCopyOrCut = _bannerTitle.Text == "Copied" || _bannerTitle.Text == "Cut" || _bannerIcon.Text == "📋" || _bannerIcon.Text == "✂️" || _bannerIcon.Text == "📁";
                if (isCopyOrCut)
                {
                    GetConfiguredRestingDimensions(out double rw, out double rh);
                    if (showMedia)
                    {
                        double satW = Math.Round(rh * 1.68);
                        double gap = 9.0;
                        double copyMainW = Math.Max(152.0, rw - satW - gap);
                        _currentBaseCollapsedWidth = copyMainW;
                        _currentBaseCollapsedHeight = rh;
                        _bannerSubtitle.MaxWidth = Math.Max(40.0, copyMainW - 96.0);
                    }
                    else
                    {
                        _currentBaseCollapsedWidth = rw;
                        _currentBaseCollapsedHeight = rh;
                        _bannerSubtitle.MaxWidth = Math.Max(60.0, rw - 110.0);
                    }
                }
                else
                {
                    bool hasRich = _bannerAppIconBorder.Visibility == Visibility.Visible || _bannerAppName.Visibility == Visibility.Visible;
                    _currentBaseCollapsedWidth = hasRich ? 340.0 : BannerCollapsedWidth;
                    _currentBaseCollapsedHeight = hasRich ? 44.0 : BannerCollapsedHeight;
                    _bannerSubtitle.MaxWidth = 220.0;
                }
                if (showMedia)
                {
                    UpdateMusicSatelliteArtwork();
                    _musicSatelliteTarget = 1.0;
                }
                if (showTimer) _splitIslandsTarget = 1.0;
            }
            else if (_isMessageActive)
            {
                _collapsedContent.Visibility = Visibility.Collapsed;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Visible;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;
            }
            else if (showMedia && _isMediaWideActive)
            {
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Visible;
                _collapsedMediaWideView.LayoutTransform = Transform.Identity;
                double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
                bool isIsland = string.Equals(AppPreferences.Instance.NotchStyle, "Island", StringComparison.OrdinalIgnoreCase);
                double targetW = isIsland ? 360.0 : MediaWideCollapsedWidth;
                double targetH = isIsland ? 66.0 : MediaWideCollapsedHeight;
                _currentBaseCollapsedWidth = Math.Round(targetW * scale);
                _currentBaseCollapsedHeight = Math.Round(targetH * scale);
                if (showTimer) _splitIslandsTarget = 1.0;
            }
            else if (_isMinimalModeActive)
            {
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedMinimalView.Visibility = Visibility.Visible;
                _currentBaseCollapsedWidth = 72.0;
                _currentBaseCollapsedHeight = 14.0;
            }
            else if (showMedia)
            {
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Visible;
                _collapsedMediaView.LayoutTransform = Transform.Identity;
                double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
                bool isIsland = string.Equals(AppPreferences.Instance.NotchStyle, "Island", StringComparison.OrdinalIgnoreCase);
                double targetW = isIsland ? 280.0 : MediaCollapsedWidth;
                double targetH = isIsland ? 38.0 : MediaCollapsedHeight;
                _currentBaseCollapsedWidth = Math.Round(targetW * scale);
                _currentBaseCollapsedHeight = Math.Round(targetH * scale);
                if (showTimer) _splitIslandsTarget = 1.0;

                if (animateCompactEntrance)
                {
                    AnimateViewEntrance(_collapsedMediaView, 5.0);
                }
            }
            else if (showTimer)
            {
                // Standalone Timer Mode (No Music): Left = Clock Icon, Center = Title, Right = Timer/Stopwatch Digits
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Visible;
                GetConfiguredRestingDimensions(out double rw, out double rh);
                _currentBaseCollapsedWidth = !string.IsNullOrWhiteSpace(timerSnap.Title) ? Math.Max(rw, 248.0) : rw;
                _currentBaseCollapsedHeight = rh;
            }
            else
            {
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Visible;
                GetConfiguredRestingDimensions(out double rw, out double rh);
                _currentBaseCollapsedWidth = rw;
                _currentBaseCollapsedHeight = rh;
                if (wasMediaVisible)
                {
                    AnimateViewEntrance(_collapsedNormalView, -4.0);
                    _edgeLight.TriggerBurst(3.5);
                }
            }

            bool needMorph = Math.Abs(_currentBaseCollapsedWidth - _displayedCollapsedWidth) > 0.5 ||
                             Math.Abs(_currentBaseCollapsedHeight - _displayedCollapsedHeight) > 0.5 ||
                             Math.Abs(prevSplitTarget - _splitIslandsTarget) > 0.01 ||
                             Math.Abs(prevMusicSatTarget - _musicSatelliteTarget) > 0.01;

            if (needMorph)
            {
                if (_morphProgress <= 0.1 && !_machine.IsOpenOrOpening) _targetProgress = 0.0;
                _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
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

            bool shouldShowBattery = prefs.ShowClosedBattery && DeviceService.ShouldShowBatteryIndicators;
            Visibility batVis = shouldShowBattery ? Visibility.Visible : Visibility.Collapsed;
            _collapsedBatteryGlyph.Visibility = batVis;
            _collapsedBatteryText.Visibility = batVis;
            _headerBatteryText.Visibility = DeviceService.ShouldShowBatteryIndicators ? Visibility.Visible : Visibility.Collapsed;

            // Resting layout: Corners vs Center (if device has no right-side content like battery on PC, auto-center clock)
            bool hasRightContent = batVis == Visibility.Visible || _collapsedBluetoothIcon.Visibility == Visibility.Visible;
            _restingRightStack.Visibility = hasRightContent ? Visibility.Visible : Visibility.Collapsed;
            bool isCenterLayout = string.Equals(prefs.NotchRestingLayout, "Center", StringComparison.OrdinalIgnoreCase) || !hasRightContent;
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
            _restingRightStack.Margin = (isCenterLayout && hasRightContent && !prefs.EnablePetBeta) ? new Thickness(10, 0, 0, 0) : new Thickness(0);

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

            string visMode = prefs.NotchVisibility ?? "Always";
            bool isSmartMode = string.Equals(visMode, "Smart", StringComparison.OrdinalIgnoreCase);

            InspectWindowUnderNotch(isSmartMode, out bool isFgFullscreen, out bool isOccupiedByApp, out bool isFgMaximized);

            // Dynamically adapt timer rate: 160ms for Smart mode, 320ms for Always/Hidden mode
            var desiredRate = isSmartMode ? TimeSpan.FromMilliseconds(160) : TimeSpan.FromMilliseconds(320);
            if (_dynamicVisibilityTimer.Interval != desiredRate)
            {
                _dynamicVisibilityTimer.Interval = desiredRate;
            }

            // 1. Hide in fullscreen (unless open or event overrides tuck)
            if (prefs.HideOnFullscreen && isFgFullscreen && !isOpenOrOpening && !eventOverridesTuck)
            {
                SetAutoHidden(true);
                return;
            }

            // 2. Hidden mode: invisible when resting, but opens via shortcut/tray or live event when ShowEventsOverApps is enabled
            if (string.Equals(visMode, "Hidden", StringComparison.OrdinalIgnoreCase))
            {
                SetAutoHidden(!isOpenOrOpening && !eventOverridesTuck);
                return;
            }

            // 3. Smart mode: Detect if important UI (tabs, title bar, address bar, buttons) is underneath
            var targetScreen = GetTargetScreen();
            if (targetScreen != null)
            {
                var b = targetScreen.Bounds;
                double notchW = Math.Max(220.0, _displayedCollapsedWidth + 24.0);
                int notchLeft = b.Left + (int)((b.Width - notchW) / 2.0);
                int notchRight = notchLeft + (int)notchW;

                if (NativeMethods.GetCursorPos(out var curPt))
                {
                    // If mouse cursor reaches top 4px above the notch, trigger peek reveal
                    if (curPt.Y <= b.Top + 4 && curPt.X >= notchLeft - 30 && curPt.X <= notchRight + 30)
                    {
                        _isSmartPeekHovered = true;
                    }
                    else if (_isSmartPeekHovered && (curPt.Y > b.Top + Math.Max(80.0, _displayedCollapsedHeight + 25.0) || curPt.X < notchLeft - 60 || curPt.X > notchRight + 60))
                    {
                        _isSmartPeekHovered = false;
                    }
                }
            }

            if (string.Equals(visMode, "Smart", StringComparison.OrdinalIgnoreCase))
            {
                // When an application window with important UI is underneath the notch:
                // Auto-hide the notch so tabs/titlebar/search are completely unobstructed,
                // UNLESS the user is hovering at the top edge, or the notch is expanded, or an active event is showing
                bool shouldHideInSmart = isOccupiedByApp && !isOpenOrOpening && !eventOverridesTuck && !_isSmartPeekHovered && !_capsuleCard.IsMouseOver;
                SetAutoHidden(shouldHideInSmart);
                if (shouldHideInSmart)
                {
                    return;
                }
            }
            else
            {
                SetAutoHidden(false);
            }

            // 4. Minimal mode
            bool wantMinimal = false;
            if (!isOpenOrOpening && !eventOverridesTuck && !_isMinimalPeekHovered)
            {
                if (string.Equals(visMode, "Minimal", StringComparison.OrdinalIgnoreCase))
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

            double targetY = hidden ? -Math.Max(_displayedCollapsedHeight, 40.0) - 20.0 : 0.0;
            double targetOpacity = hidden ? 0.0 : 1.0;
            var duration = TimeSpan.FromMilliseconds(220);
            IEasingFunction ease = hidden
                ? new CubicEase { EasingMode = EasingMode.EaseIn }
                : new CubicEase { EasingMode = EasingMode.EaseOut };

            var slideAnim = new DoubleAnimation { To = targetY, Duration = duration, EasingFunction = ease };
            var fadeAnim = new DoubleAnimation { To = targetOpacity, Duration = duration, EasingFunction = ease };

            if (hidden)
            {
                _capsuleCard.IsHitTestVisible = false;
                slideAnim.Completed += (_, _) =>
                {
                    if (_isAutoHiddenByVisibility)
                    {
                        _capsuleCard.Visibility = Visibility.Collapsed;
                        _leftTimerIsland.Visibility = Visibility.Collapsed;
                        _rightTimerIsland.Visibility = Visibility.Collapsed;
                        _musicSatelliteIsland.Visibility = Visibility.Collapsed;
                    }
                };
            }
            else
            {
                _capsuleCard.Visibility = Visibility.Visible;
                _capsuleCard.IsHitTestVisible = true;
                EnsureTopmost();
                UpdateMorphVisuals(_morphProgress);
            }

            _capsuleCardTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _capsuleCard.BeginAnimation(OpacityProperty, fadeAnim);
            _leftIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _rightIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _musicSatelliteTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }

        private int _smartTextClearTicks = 10;

        private bool DetectTextUnderNotch(int notchLeft, int notchTop, int notchRight, int notchBottom)
        {
            try
            {
                // 1. Check if active typing caret is near or under the notch
                var gui = new NativeMethods.GUITHREADINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.GUITHREADINFO)) };
                if (NativeMethods.GetGUIThreadInfo(0, ref gui) && gui.hwndCaret != IntPtr.Zero)
                {
                    var pt = new NativeMethods.POINT { X = gui.rcCaret.Left, Y = gui.rcCaret.Top };
                    if (NativeMethods.ClientToScreen(gui.hwndCaret, ref pt))
                    {
                        if (pt.Y <= notchBottom + 24 && pt.Y >= notchTop - 10 &&
                            pt.X >= notchLeft - 36 && pt.X <= notchRight + 36)
                        {
                            _smartTextClearTicks = 0;
                            return true;
                        }
                    }
                }

                // 2. Sample underlying window pixels directly under the notch (WDA_EXCLUDEFROMCAPTURE excludes the notch itself)
                int sampleW = Math.Clamp(notchRight - notchLeft - 24, 120, 220);
                int sampleH = 22;
                int sampleX = notchLeft + ((notchRight - notchLeft) - sampleW) / 2;
                int sampleY = notchTop + 5;

                IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
                if (screenDc == IntPtr.Zero) return false;
                try
                {
                    IntPtr memDc = NativeMethods.CreateCompatibleDC(screenDc);
                    if (memDc == IntPtr.Zero) return false;
                    try
                    {
                        var bmi = new NativeMethods.BITMAPINFO();
                        bmi.bmiHeader.biSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.BITMAPINFOHEADER));
                        bmi.bmiHeader.biWidth = sampleW;
                        bmi.bmiHeader.biHeight = -sampleH; // top-down
                        bmi.bmiHeader.biPlanes = 1;
                        bmi.bmiHeader.biBitCount = 32;
                        bmi.bmiHeader.biCompression = 0;

                        IntPtr hDib = NativeMethods.CreateDIBSection(memDc, ref bmi, 0, out IntPtr ppvBits, IntPtr.Zero, 0);
                        if (hDib == IntPtr.Zero || ppvBits == IntPtr.Zero) return false;
                        try
                        {
                            IntPtr oldBmp = NativeMethods.SelectObject(memDc, hDib);
                            bool ok = false;
                            if (_isAutoHiddenByVisibility)
                            {
                                // Notch is already retracted off-screen: sample screen directly
                                ok = NativeMethods.BitBlt(memDc, 0, 0, sampleW, sampleH, screenDc, sampleX, sampleY, NativeMethods.SRCCOPY);
                            }
                            else
                            {
                                // Notch is visible on-screen: sample directly from the foreground app DC underneath to avoid sampling the notch itself
                                IntPtr myHwnd = new WindowInteropHelper(this).Handle;
                                IntPtr fg = NativeMethods.GetForegroundWindow();
                                if (fg != IntPtr.Zero && fg != myHwnd && NativeMethods.GetWindowRect(fg, out var fgRect))
                                {
                                    if (sampleX >= fgRect.Left && sampleY >= fgRect.Top && sampleX + sampleW <= fgRect.Right)
                                    {
                                        IntPtr fgDc = NativeMethods.GetDC(fg);
                                        if (fgDc != IntPtr.Zero)
                                        {
                                            try
                                            {
                                                int srcX = sampleX - fgRect.Left;
                                                int srcY = sampleY - fgRect.Top;
                                                ok = NativeMethods.BitBlt(memDc, 0, 0, sampleW, sampleH, fgDc, srcX, srcY, NativeMethods.SRCCOPY);
                                            }
                                            finally
                                            {
                                                NativeMethods.ReleaseDC(fg, fgDc);
                                            }
                                        }
                                    }
                                }
                            }
                            NativeMethods.SelectObject(memDc, oldBmp);
                            if (!ok) return false;

                            // Scan 5 horizontal lines for high-frequency glyph/text contrast edges
                            int[] scanRows = { 3, 7, 11, 15, 19 };
                            int glyphEdges = 0;
                            foreach (int r in scanRows)
                            {
                                if (r >= sampleH) continue;
                                int rowOffset = r * sampleW * 4;
                                int prevLum = -1;
                                int prevDir = 0;
                                for (int x = 2; x < sampleW - 2; x += 2)
                                {
                                    int px = System.Runtime.InteropServices.Marshal.ReadInt32(ppvBits, rowOffset + x * 4);
                                    int b = px & 0xFF;
                                    int g = (px >> 8) & 0xFF;
                                    int red = (px >> 16) & 0xFF;
                                    int lum = (red * 2 + g * 5 + b) >> 3;

                                    if (prevLum >= 0)
                                    {
                                        int diff = lum - prevLum;
                                        if (Math.Abs(diff) >= 20)
                                        {
                                            int dir = diff > 0 ? 1 : -1;
                                            if (prevDir != 0 && dir != prevDir)
                                            {
                                                glyphEdges += 2; // Alternating stroke (background -> glyph -> background)
                                            }
                                            else
                                            {
                                                glyphEdges += 1;
                                            }
                                            prevDir = dir;
                                        }
                                    }
                                    prevLum = lum;
                                }
                            }

                            if (glyphEdges >= 9)
                            {
                                _smartTextClearTicks = 0;
                                return true;
                            }
                        }
                        finally
                        {
                            NativeMethods.DeleteObject(hDib);
                        }
                    }
                    finally
                    {
                        NativeMethods.DeleteDC(memDc);
                    }
                }
                finally
                {
                    NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
            catch { }

            if (_smartTextClearTicks < 3)
            {
                _smartTextClearTicks++;
                return true;
            }
            return false;
        }

        private void InspectWindowUnderNotch(bool checkTextUnderNotch, out bool isFullscreen, out bool isOccupiedByApp, out bool isMaximized)
        {
            isFullscreen = false;
            isOccupiedByApp = false;
            isMaximized = false;
            try
            {
                var targetScreen = GetTargetScreen();
                if (targetScreen == null) return;
                var b = targetScreen.Bounds;

                double notchW = Math.Max(220.0, _displayedCollapsedWidth + 24.0);
                double notchH = Math.Max(36.0, _displayedCollapsedHeight + 12.0);
                int notchLeft = b.Left + (int)((b.Width - notchW) / 2.0);
                int notchRight = notchLeft + (int)notchW;
                int notchTop = b.Top;
                int notchBottom = b.Top + (int)notchH;

                var myHwnd = new WindowInteropHelper(this).Handle;
                bool hasAppWindowInNotchZone = false;

                // 1. Inspect Foreground Window
                IntPtr fg = NativeMethods.GetForegroundWindow();
                if (fg != IntPtr.Zero && NativeMethods.IsWindowVisible(fg) && !NativeMethods.IsIconic(fg) && fg != myHwnd)
                {
                    NativeMethods.GetWindowThreadProcessId(fg, out uint fgPid);
                    if (fgPid != (uint)Environment.ProcessId)
                    {
                        var sbClass = new System.Text.StringBuilder(64);
                        NativeMethods.GetClassName(fg, sbClass, sbClass.Capacity);
                        string cls = sbClass.ToString();

                        bool isDesktop = string.Equals(cls, "Progman", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(cls, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(cls, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(cls, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);

                        if (!isDesktop && NativeMethods.GetWindowRect(fg, out var rect))
                        {
                            int winW = rect.Right - rect.Left;
                            int winH = rect.Bottom - rect.Top;

                            // Fullscreen check: covers entire monitor bounds (games, full-screen video, PPT)
                            if (rect.Left <= b.Left + 2 && rect.Top <= b.Top + 2 &&
                                winW >= b.Width - 4 && winH >= b.Height - 4)
                            {
                                isFullscreen = true;
                                isMaximized = true;
                                isOccupiedByApp = true;
                                return;
                            }

                            int cx = (rect.Left + rect.Right) / 2;
                            int cy = (rect.Top + rect.Bottom) / 2;
                            if (b.Contains(cx, cy) || b.Contains(rect.Left + 10, rect.Top + 10))
                            {
                                if (NativeMethods.IsZoomed(fg) || (rect.Left <= b.Left + 4 && rect.Top <= b.Top + 4 && winW >= b.Width - 10))
                                {
                                    isMaximized = true;
                                    hasAppWindowInNotchZone = true;
                                }
                                else if (rect.Top <= notchBottom + 16 && rect.Bottom > notchTop &&
                                         rect.Left < notchRight && rect.Right > notchLeft)
                                {
                                    hasAppWindowInNotchZone = true;
                                }
                            }
                        }
                    }
                }

                // 2. Also check if a background window is directly under the notch
                if (!hasAppWindowInNotchZone)
                {
                    var testPt = new NativeMethods.POINT
                    {
                        X = (notchLeft + notchRight) / 2,
                        Y = notchTop + 16
                    };
                    IntPtr wndUnder = NativeMethods.WindowFromPoint(testPt);
                    if (wndUnder != IntPtr.Zero && wndUnder != myHwnd)
                    {
                        IntPtr rootUnder = NativeMethods.GetAncestor(wndUnder, NativeMethods.GA_ROOT);
                        if (rootUnder == IntPtr.Zero) rootUnder = wndUnder;

                        if (rootUnder != myHwnd)
                        {
                            NativeMethods.GetWindowThreadProcessId(rootUnder, out uint uPid);
                            if (uPid != (uint)Environment.ProcessId)
                            {
                                var sbUnder = new System.Text.StringBuilder(64);
                                NativeMethods.GetClassName(rootUnder, sbUnder, sbUnder.Capacity);
                                string clsUnder = sbUnder.ToString();
                                bool isDesktopUnder = string.Equals(clsUnder, "Progman", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(clsUnder, "WorkerW", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(clsUnder, "Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                                                     string.Equals(clsUnder, "Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase);

                                if (!isDesktopUnder && NativeMethods.IsWindowVisible(rootUnder))
                                {
                                    hasAppWindowInNotchZone = true;
                                }
                            }
                        }
                    }
                }

                if (hasAppWindowInNotchZone && checkTextUnderNotch)
                {
                    isOccupiedByApp = DetectTextUnderNotch(notchLeft, notchTop, notchRight, notchBottom);
                }
                else
                {
                    _smartTextClearTicks = 10;
                    isOccupiedByApp = false;
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

                bool isScroll = settings.Animation == WindowsBuddy.Tools.Message.MessageAnimationMode.Scroll;
                double estimatedWidth = isScroll ? 280.0 : Math.Clamp(text.Length * 8.6 + 90.0, 240.0, 520.0);
                _currentBaseCollapsedWidth = estimatedWidth;
                _currentBaseCollapsedHeight = BannerCollapsedHeight;

                _messageNotchText.TextTrimming = isScroll ? TextTrimming.None : TextTrimming.CharacterEllipsis;
                _messageNotchText.MaxWidth = isScroll ? 3000.0 : 440.0;

                UpdateCollapsedViewState();
                _morphTimer.Start();

                bool isRtl = LocalizationService.IsRtl(text) || AppPreferences.Instance.IsPersian;

                Action triggerAnimation = () =>
                {
                    WindowsBuddy.Tools.Message.MessageRenderer.AnimateElement(
                        _messageNotchStack,
                        _messageNotchText,
                        text,
                        settings.Animation,
                        settings.Speed,
                        _messageTypeOnTimer,
                        out _messageTypeOnTimer,
                        containerWidth: estimatedWidth,
                        isRtl: isRtl);
                };

                _messageDismissTimer.Stop();
                if (!settings.RepeatEnabled)
                {
                    _messageDismissTimer.Interval = settings.DurationSpan;
                    _messageDismissTimer.Start();
                }

                if (_morphProgress > 0.3)
                {
                    _targetProgress = 0.0;
                    _morphTimer.Start();

                    var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
                    delayTimer.Tick += (s, e) =>
                    {
                        delayTimer.Stop();
                        triggerAnimation();
                    };
                    delayTimer.Start();
                }
                else
                {
                    triggerAnimation();
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
                _messageNotchStack.BeginAnimation(UIElement.OpacityProperty, null);
                _messageNotchStack.RenderTransform = null;
                _messageNotchStack.Effect = null;
                _messageNotchStack.Opacity = 1.0;
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

            CapsuleTheme.ThemeChanged += () =>
            {
                animIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
            };

            border.MouseEnter += (_, _) =>
            {
                var cur = CapsuleTheme.Current;
                border.Background = CapsuleTheme.Brush(cur.ControlFillHover);
                animIcon.StrokeBrush = CapsuleTheme.Brush(cur.Text);
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

            CapsuleTheme.ThemeChanged += () =>
            {
                if (border.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text2);
            };

            border.MouseEnter += (_, _) =>
            {
                var cur = CapsuleTheme.Current;
                border.Background = CapsuleTheme.Brush(cur.ControlFillHover);
                if (border.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(cur.Text);
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
            try { NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_NONE); } catch { }

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
            PositionAtTopCenter();

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
                _isTuckCompletedHidden = false;
                _leftTimerIsland.IsHitTestVisible = false;
                _rightTimerIsland.IsHitTestVisible = false;
                _musicSatelliteIsland.IsHitTestVisible = false;

                if (_morphProgress > 0.05)
                {
                    _machine.RequestClose("tuck");
                }

                slideAnim.Completed += (_, _) =>
                {
                    if (_isTucked)
                    {
                        _isTuckCompletedHidden = true;
                        _capsuleCard.IsHitTestVisible = false;
                        _capsuleCard.Visibility = Visibility.Collapsed;
                        _leftTimerIsland.Visibility = Visibility.Collapsed;
                        _rightTimerIsland.Visibility = Visibility.Collapsed;
                        _musicSatelliteIsland.Visibility = Visibility.Collapsed;
                    }
                };
            }
            else
            {
                _isTuckCompletedHidden = false;
                _capsuleCard.Visibility = Visibility.Visible;
                _capsuleCard.IsHitTestVisible = true;
                UpdateMorphVisuals(_morphProgress);
                EnsureTopmost();

                fadeAnim.Completed += (_, _) =>
                {
                    if (!_isTucked)
                    {
                        // Release animation hold on island opacity so UpdateMorphVisuals can control it freely
                        _leftTimerIsland.BeginAnimation(OpacityProperty, null);
                        _rightTimerIsland.BeginAnimation(OpacityProperty, null);
                        _musicSatelliteIsland.BeginAnimation(OpacityProperty, null);
                        UpdateMorphVisuals(_morphProgress);
                    }
                };
            }

            _capsuleCardTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _capsuleCard.BeginAnimation(OpacityProperty, fadeAnim);
            _leftIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _rightIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _musicSatelliteTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _leftTimerIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _rightTimerIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _musicSatelliteIsland.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (msg == WM_MOUSEHWHEEL && _navScrollViewer != null && _navScrollViewer.IsVisible)
            {
                short delta = (short)((wParam.ToInt64() >> 16) & 0xffff);
                if (delta != 0)
                {
                    _navScrollViewer.ScrollToHorizontalOffset(_navScrollViewer.HorizontalOffset + (delta * 0.5));
                    handled = true;
                }
            }
            else if (msg == NativeMethods.WM_CLIPBOARDUPDATE && AppPreferences.Instance.Raw.ClipboardRecording)
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
                    if (targetScreen.Primary)
                    {
                        double pWidth = SystemParameters.PrimaryScreenWidth;
                        Left = (pWidth - StableEnvelopeWidth) / 2.0;
                        Top = 0;
                        return;
                    }

                    var bounds = targetScreen.Bounds;
                    double scaleX = 1.0;
                    double scaleY = 1.0;

                    try
                    {
                        var pt = new NativeMethods.POINT
                        {
                            X = bounds.Left + bounds.Width / 2,
                            Y = bounds.Top + bounds.Height / 2
                        };
                        IntPtr hMon = NativeMethods.MonitorFromPoint(pt, 2);
                        if (hMon != IntPtr.Zero && NativeMethods.GetDpiForMonitor(hMon, 0, out uint dpiX, out uint dpiY) == 0 && dpiX > 0)
                        {
                            scaleX = dpiX / 96.0;
                            scaleY = dpiY / 96.0;
                        }
                    }
                    catch { }

                    if (scaleX <= 0.1) scaleX = 1.0;
                    if (scaleY <= 0.1) scaleY = 1.0;

                    double dipLeft = bounds.Left / scaleX;
                    double dipTop = bounds.Top / scaleY;
                    double dipWidth = bounds.Width / scaleX;

                    Left = dipLeft + (dipWidth - StableEnvelopeWidth) / 2.0;
                    Top = dipTop;
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
            if (!AppPreferences.Instance.IsModuleEnabled(_activeToolId, true) && !string.Equals(_activeToolId, "home", StringComparison.OrdinalIgnoreCase))
            {
                _activeToolId = "home";
                _viewportHost.Child = ToolRegistry.GetById("home").BuildView();
            }

            foreach (var tool in ToolRegistry.All)
            {
                string id = tool.Id;
                if (!AppPreferences.Instance.IsModuleEnabled(id, true))
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
                    Background = isActive ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent,
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
                    "calendar" => AnimateIconType.Calendar,
                    "links" => AnimateIconType.Links,
                    "message" => AnimateIconType.Message,
                    "screentime" => AnimateIconType.Activity,
                    _ => null
                };

                AnimateIcon animIcon = null;
                TextBlock iconText = null;

                if (animType.HasValue)
                {
                    animIcon = new AnimateIcon(animType.Value, size: 14, brush: isActive ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.IconMuted))
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
                        Foreground = isActive ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.IconMuted),
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
                        circleHighlight.Background = CapsuleTheme.Brush(p.ControlFillHover);
                        if (iconText != null) iconText.Foreground = CapsuleTheme.Brush(p.Text);
                        if (animIcon != null) animIcon.StrokeBrush = CapsuleTheme.Brush(p.Text);
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
                plusBtn.Background = CapsuleTheme.Brush(p.ControlFillHover);
                if (plusBtn.Child is TextBlock tb) tb.Foreground = CapsuleTheme.Brush(p.Text);
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
                "calendar" => LocalizationService.T("sub.calendar", "Solar & Gregorian · Holidays & Events"),
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

            if (_dockButtons.TryGetValue(tool.Id, out var activeBtn))
            {
                activeBtn.BringIntoView();
            }

            var newView = tool.BuildView();
            _viewportHost.Child = newView;
            tool.OnActivated();

            if (AppPreferences.Instance.TabAnimations && _expandedContent.Visibility == Visibility.Visible && newView != null)
            {
                var tt = new TranslateTransform(0, -6);
                newView.RenderTransform = tt;
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
                tt.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-6, 0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
                newView.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
            }
        }

        private void UpdateToolHeaderActions(string toolId)
        {
            _toolHeaderActions.Children.Clear();
            if (string.Equals(toolId, "weather", StringComparison.OrdinalIgnoreCase))
            {
                var p = CapsuleTheme.Current;
                var svc = WindowsBuddy.Tools.Weather.WeatherService.Instance;

                // 1. Unit toggle pill [ °C | °F ]
                var unitPill = new Border
                {
                    Background = CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
                    BorderThickness = new Thickness(1),
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
                    Background = !svc.UseFahrenheit ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = "°C",
                        FontSize = 11,
                        FontWeight = !svc.UseFahrenheit ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = !svc.UseFahrenheit ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text2),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center
                    }
                };

                var fBtn = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(7, 2, 7, 2),
                    Background = svc.UseFahrenheit ? CapsuleTheme.Brush(p.Accent) : Brushes.Transparent,
                    Cursor = Cursors.Hand,
                    Child = new TextBlock
                    {
                        Text = "°F",
                        FontSize = 11,
                        FontWeight = svc.UseFahrenheit ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = svc.UseFahrenheit ? CapsuleTheme.Brush(p.AccentFg) : CapsuleTheme.Brush(p.Text2),
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
                    Background = CapsuleTheme.Brush(p.Surface2),
                    BorderBrush = CapsuleTheme.Brush(p.Border),
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
                    Foreground = CapsuleTheme.Brush(p.Text),
                    VerticalAlignment = VerticalAlignment.Center
                });
                cityBtn.Child = citySp;

                cityBtn.MouseEnter += (_, _) => cityBtn.Background = CapsuleTheme.Brush(p.Surface3);
                cityBtn.MouseLeave += (_, _) => cityBtn.Background = CapsuleTheme.Brush(p.Surface2);
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
            long nowTs = System.Diagnostics.Stopwatch.GetTimestamp();
            double rawDt = (nowTs - _lastMorphTickTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
            double dt = rawDt > 0.05 ? 0.014 : Math.Clamp(rawDt, 0.004, 0.035);
            _lastMorphTickTimestamp = nowTs;

            double step = (_targetProgress > _morphProgress ? 4.2 : 5.2) * dt;
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
            // Freeze base width/height during open/close morph so baseW does not drift while interpolating to ExpandedWidth!
            bool sizeSettled = true;
            if (_morphProgress <= 0.01)
            {
                double dw = _currentBaseCollapsedWidth - _displayedCollapsedWidth;
                double dh = _currentBaseCollapsedHeight - _displayedCollapsedHeight;
                sizeSettled = Math.Abs(dw) < 0.5 && Math.Abs(dh) < 0.4;

                if (sizeSettled)
                {
                    _displayedCollapsedWidth = _currentBaseCollapsedWidth;
                    _displayedCollapsedHeight = _currentBaseCollapsedHeight;
                }
                else
                {
                    double lerpFactor = Math.Clamp(dt * 8.5, 0.08, 0.22);
                    _displayedCollapsedWidth += dw * lerpFactor;
                    _displayedCollapsedHeight += dh * lerpFactor;
                }
            }

            // Smoothly interpolate detached split Dynamic Island timer capsules (0.0 <-> 1.0) over ~300ms
            double splitDiff = _splitIslandsTarget - _splitIslandsProgress;
            double splitStep = dt / 0.30;
            bool splitSettled = Math.Abs(splitDiff) <= splitStep;
            if (splitSettled)
            {
                _splitIslandsProgress = _splitIslandsTarget;
            }
            else
            {
                _splitIslandsProgress += Math.Sign(splitDiff) * splitStep;
            }

            // Smoothly interpolate detached music satellite capsule (0.0 <-> 1.0) over ~300ms
            double musicDiff = _musicSatelliteTarget - _musicSatelliteProgress;
            double musicStep = dt / 0.30;
            bool musicSettled = Math.Abs(musicDiff) <= musicStep;
            if (musicSettled)
            {
                _musicSatelliteProgress = _musicSatelliteTarget;
            }
            else
            {
                _musicSatelliteProgress += Math.Sign(musicDiff) * musicStep;
            }

            bool allSettled = morphSettled && sizeSettled && splitSettled && musicSettled;
            _edgeLight.SetMorphing(!morphSettled);

            if (allSettled)
            {
                _morphTimer.Stop();
                if (_morphProgress <= 0.01 && _targetProgress <= 0.01)
                {
                    TrimWorkingSet();
                }
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

            string notchStyle = AppPreferences.Instance.NotchStyle ?? "Notch";
            bool isIsland = string.Equals(notchStyle, "Island", StringComparison.OrdinalIgnoreCase);
            bool isLine = string.Equals(notchStyle, "Line", StringComparison.OrdinalIgnoreCase);
            double islandGap = isIsland ? NotchGeometryBuilder.IslandGap : 0.0;

            _capsuleCard.Width = curW;
            _capsuleCard.Height = curH + islandGap;
            _capsuleCard.Cursor = t > 0.5 ? Cursors.Arrow : Cursors.Hand;

            string geoStyle = isIsland ? "Island" : (isLine ? "Line" : (AppPreferences.Instance.NotchShape ?? "Shouldered"));
            double collapsedBottomRadius = Math.Clamp(baseH * 0.215, 15.0, 19.5);
            double bottomRadius = collapsedBottomRadius + (24.0 - collapsedBottomRadius) * ease;
            var geo = NotchGeometryBuilder.BuildSilhouetteCore(geoStyle, curW, curH, bottomRadius, isOutline: false);

            _notchBackdropPath.Data = geo;
            _notchOutlinePath.Data = geo;
            _notchTopBlackLine.Visibility = isIsland ? Visibility.Collapsed : Visibility.Visible;
            _notchTopBlackLine.X2 = curW;
            _edgeLight.UpdateGeometry(geo, t > 0.5, curW, curH);

            double collapsedOpacity = Math.Clamp(1.0 - t * 2.5, 0.0, 1.0);
            _collapsedContent.Opacity = collapsedOpacity;
            _collapsedContent.Visibility = collapsedOpacity <= 0.001 ? Visibility.Collapsed : Visibility.Visible;
            _collapsedContent.IsHitTestVisible = t < 0.25;

            // Inset content comfortably inside the notch silhouette
            // For Notch/Shouldered: side clearance of 26.0 protects against shoulders without starving width
            double sideClearance = (isIsland || isLine) ? 20.0 : 26.0;
            double topClearance = isIsland ? islandGap : (isLine ? 3.0 : 6.5);
            double bottomClearance = (isIsland || isLine) ? 4.0 : 7.0;

            double collapsedInnerWidth = _isBannerActive
                ? Math.Max(40.0, baseW - ((isIsland || isLine) ? 16.0 : 36.0))
                : Math.Max(40.0, baseW - sideClearance);
            double collapsedInnerHeight = isIsland
                ? Math.Max(16.0, baseH)
                : Math.Max(16.0, baseH - topClearance - bottomClearance);

            if (Math.Abs(_collapsedContent.Width - collapsedInnerWidth) > 0.4)
            {
                _collapsedContent.Width = collapsedInnerWidth;
            }
            if (Math.Abs(_collapsedContent.Height - collapsedInnerHeight) > 0.4)
            {
                _collapsedContent.Height = collapsedInnerHeight;
            }
            _collapsedContent.HorizontalAlignment = HorizontalAlignment.Center;
            _collapsedContent.VerticalAlignment = isIsland ? VerticalAlignment.Center : VerticalAlignment.Top;
            _collapsedContent.Margin = new Thickness(0, isIsland ? islandGap : topClearance, 0, 0);

            // Confine media views strictly within inner bounds and constrain text MaxWidth to prevent grid blowouts
            _collapsedMediaView.Width = collapsedInnerWidth;
            _collapsedMediaWideView.Width = collapsedInnerWidth;

            double availableCenterW = Math.Max(40.0, collapsedInnerWidth - (isIsland ? 56.0 : 64.0));
            if (Math.Abs(_mediaCombinedText.MaxWidth - availableCenterW) > 1.0)
            {
                _mediaCombinedText.MaxWidth = availableCenterW;
            }

            double availableRightW = Math.Max(40.0, collapsedInnerWidth - (isIsland ? 56.0 : 64.0));
            double targetTrackMaxW = Math.Max(40.0, availableRightW - 48.0);
            if (Math.Abs(_wideMediaTrackText.MaxWidth - targetTrackMaxW) > 1.0)
            {
                _wideMediaTrackText.MaxWidth = targetTrackMaxW;
            }
            double targetArtistMaxW = Math.Max(40.0, availableRightW - 8.0);
            if (Math.Abs(_wideMediaArtistText.MaxWidth - targetArtistMaxW) > 1.0)
            {
                _wideMediaArtistText.MaxWidth = targetArtistMaxW;
            }

            // Responsive styling for Island vs Notch / Line
            if (isIsland)
            {
                _waveformProgress.Visibility = Visibility.Collapsed;
                _wideWaveformProgress.Visibility = Visibility.Collapsed;
                _mediaCombinedText.TextAlignment = TextAlignment.Left;
                _mediaCombinedText.HorizontalAlignment = HorizontalAlignment.Left;
                _collapsedMediaView.VerticalAlignment = VerticalAlignment.Center;
                _collapsedMediaWideView.VerticalAlignment = VerticalAlignment.Center;
                _albumArtThumb.UpdateDimensions(20.0, 5.0);
                _albumArtThumb.Margin = new Thickness(0, 0, 8, 0);
                _wideAlbumArtThumb.UpdateDimensions(36.0, 7.5);
                _wideAlbumArtThumb.Margin = new Thickness(0, 0, 10, 0);
                _miniEqualizer.Margin = new Thickness(8, 0, 0, 0);
            }
            else
            {
                _waveformProgress.Visibility = Visibility.Visible;
                _wideWaveformProgress.Visibility = Visibility.Visible;
                _mediaCombinedText.TextAlignment = TextAlignment.Left;
                _mediaCombinedText.HorizontalAlignment = HorizontalAlignment.Left;
                _collapsedMediaView.VerticalAlignment = VerticalAlignment.Center;
                _collapsedMediaWideView.VerticalAlignment = VerticalAlignment.Center;
                _albumArtThumb.UpdateDimensions(22.0, 5.5);
                _albumArtThumb.Margin = new Thickness(5, 0, 8, 0);
                _wideAlbumArtThumb.UpdateDimensions(38.0, 8.0);
                _wideAlbumArtThumb.Margin = new Thickness(4, 0, 10, 0);
                _miniEqualizer.Margin = new Thickness(8, 0, 5, 0);
            }

            _collapsedMessageView.Margin = new Thickness(0, islandGap, 0, 0);
            _expandedContent.Margin = new Thickness(0, 10 + islandGap * ease, 0, 12);

            _expandedContent.Visibility = t > 0.02 ? Visibility.Visible : Visibility.Collapsed;
            _expandedContent.Opacity = Math.Clamp((t - 0.25) / 0.75, 0.0, 1.0);
            _expandedContent.IsHitTestVisible = t > 0.6;

            // Update Detached Music Satellite Island (when Notification arrives while Music is playing)
            double musicFactor = _musicSatelliteProgress * Math.Clamp(1.0 - t * 3.0, 0.0, 1.0);
            if (musicFactor <= 0.005 || _isTuckCompletedHidden || _isAutoHiddenByVisibility)
            {
                _musicSatelliteIsland.Visibility = Visibility.Collapsed;
            }
            else
            {
                _musicSatelliteIsland.Visibility = Visibility.Visible;

                // Smooth cubic ease-out emergence from behind the center notch to the right
                double musicEase = 1.0 - Math.Pow(1.0 - musicFactor, 3);
                double gap = 9.0;
                double satH = baseH;
                double satW = Math.Round(satH * 1.68);

                _musicSatelliteIsland.Width = satW;
                _musicSatelliteIsland.Height = satH;
                _musicSatelliteIsland.CornerRadius = new CornerRadius(satH / 2.0);
                _musicSatelliteIsland.Margin = new Thickness(0, islandGap, 0, 0);

                // Update artwork / glyph sizing to fit responsive satH
                double artSize = Math.Max(16.0, satH - 10.0);
                if (_musicSatelliteArtGrid != null && Math.Abs(_musicSatelliteArtworkImage.Width - artSize) > 0.5)
                {
                    _musicSatelliteArtGrid.Width = artSize;
                    _musicSatelliteArtGrid.Height = artSize;
                    _musicSatelliteArtworkImage.Width = artSize;
                    _musicSatelliteArtworkImage.Height = artSize;
                    _musicSatelliteArtworkImage.Clip = new EllipseGeometry(new Point(artSize / 2.0, artSize / 2.0), artSize / 2.0, artSize / 2.0);
                    _musicSatelliteBeatRing.Width = artSize + 2.0;
                    _musicSatelliteBeatRing.Height = artSize + 2.0;
                }

                // Smooth cubic ease-out emergence from behind the center notch to the LEFT
                double startLeftX = -(baseW / 2.0 - satW / 2.0 - 8.0);
                double fullLeftX = -(baseW / 2.0 + gap + satW / 2.0);
                _musicSatelliteTranslate.X = startLeftX + (fullLeftX - startLeftX) * musicEase;

                double satScale = 0.85 + 0.15 * musicEase;
                _musicSatelliteScale.ScaleX = satScale;
                _musicSatelliteScale.ScaleY = satScale;

                if (!_isTucked)
                {
                    double satOpacity = Math.Clamp(musicEase * 1.15, 0.0, 1.0);
                    _musicSatelliteIsland.Opacity = satOpacity;
                    _musicSatelliteIsland.IsHitTestVisible = musicFactor > 0.6;
                }
            }

            // Update Left & Right Detached Split Timer Islands (when Music is playing + Timer is active, or Notification + Timer)
            double splitFactor = _splitIslandsProgress * Math.Clamp(1.0 - t * 3.0, 0.0, 1.0);
            if (splitFactor <= 0.005 || _isTuckCompletedHidden || _isAutoHiddenByVisibility)
            {
                _leftTimerIsland.Visibility = Visibility.Collapsed;
                _rightTimerIsland.Visibility = Visibility.Collapsed;
            }
            else
            {
                _leftTimerIsland.Height = baseH;
                _leftTimerIsland.CornerRadius = new CornerRadius(baseH / 2.0);
                _leftTimerIsland.Margin = new Thickness(0, islandGap, 0, 0);

                _rightTimerIsland.Height = baseH;
                _rightTimerIsland.CornerRadius = new CornerRadius(baseH / 2.0);
                _rightTimerIsland.Margin = new Thickness(0, islandGap, 0, 0);

                // If music satellite is occupying the left slot during notification, hide left timer ring so only right timer digits show
                _leftTimerIsland.Visibility = musicFactor > 0.005 ? Visibility.Collapsed : Visibility.Visible;
                _rightTimerIsland.Visibility = Visibility.Visible;

                // Smooth cubic ease-out emergence from behind the center music notch
                double splitEase = 1.0 - Math.Pow(1.0 - splitFactor, 3);
                double gap = 8.0;
                double leftW = 36.0;
                double rightW = _rightTimerIsland.Width > 20.0 ? _rightTimerIsland.Width : 68.0;

                double startLeftX = -(baseW / 2.0 - leftW / 2.0 - 8.0);
                double fullLeftX = -(baseW / 2.0 + gap + leftW / 2.0);

                double startRightX = +(baseW / 2.0 - rightW / 2.0 - 8.0);
                double fullRightX = +(baseW / 2.0 + gap + rightW / 2.0);

                _leftIslandTranslate.X = startLeftX + (fullLeftX - startLeftX) * splitEase;
                _rightIslandTranslate.X = startRightX + (fullRightX - startRightX) * splitEase;

                double islandScale = 0.78 + 0.22 * splitEase;
                _leftIslandScale.ScaleX = islandScale;
                _leftIslandScale.ScaleY = islandScale;
                _rightIslandScale.ScaleX = islandScale;
                _rightIslandScale.ScaleY = islandScale;

                if (!_isTucked)
                {
                    double islandOpacity = Math.Clamp(splitEase * 1.15, 0.0, 1.0);
                    _leftTimerIsland.Opacity = islandOpacity;
                    _rightTimerIsland.Opacity = islandOpacity;
                    _leftTimerIsland.IsHitTestVisible = splitFactor > 0.6;
                    _rightTimerIsland.IsHitTestVisible = splitFactor > 0.6;
                }
            }
        }

        private void UpdateEdgeLightEffectiveColor()
        {
            if (_leftTimerIsland != null)
            {
                UpdateTimerNotchVisuals(WindowsBuddy.Tools.Timer.TimerService.Instance.GetActiveNotchTimer());
            }

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
            // The capsule outer window and envelope root MUST remain strictly LeftToRight
            // so layout centering and morph transforms are never inverted horizontally when Persian is selected.
            FlowDirection = FlowDirection.LeftToRight;
            if (_envelopeRoot != null)
            {
                _envelopeRoot.FlowDirection = FlowDirection.LeftToRight;
            }
            if (_expandedContent != null)
            {
                _expandedContent.FlowDirection = LocalizationService.CurrentFlowDirection;
            }
            var p = CapsuleTheme.Current;
            _notchBackdropPath.Fill = CapsuleTheme.Brush(p.Bg);
            bool outlineEnabled = AppPreferences.Instance.SurfaceOutline;
            var outlineBrush = outlineEnabled
                ? (AppPreferences.Instance.TintOutlineFromWallpaper ? CapsuleTheme.Brush(p.Accent) : CapsuleTheme.Brush(p.BorderStrong))
                : Brushes.Transparent;
            var outlineThickness = outlineEnabled ? new Thickness(1.0) : new Thickness(0);

            if (_musicSatelliteIsland != null)
            {
                _musicSatelliteIsland.Background = CapsuleTheme.Brush(p.Bg);
                _musicSatelliteIsland.BorderBrush = outlineBrush;
                _musicSatelliteIsland.BorderThickness = outlineThickness;
            }
            if (_leftTimerIsland != null)
            {
                _leftTimerIsland.Background = CapsuleTheme.Brush(p.Bg);
                _leftTimerIsland.BorderBrush = outlineBrush;
                _leftTimerIsland.BorderThickness = outlineThickness;
            }
            if (_rightTimerIsland != null)
            {
                _rightTimerIsland.Background = CapsuleTheme.Brush(p.Bg);
                _rightTimerIsland.BorderBrush = outlineBrush;
                _rightTimerIsland.BorderThickness = outlineThickness;
            }

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

            outlineEnabled = AppPreferences.Instance.SurfaceOutline;
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
            if (_navDockBorder != null)
            {
                _navDockBorder.Background = CapsuleTheme.Brush(p.DockPill);
                _navDockBorder.BorderBrush = CapsuleTheme.Brush(p.Border);
            }

            UpdateCollapsedViewState();
            UpdateMorphVisuals(_morphProgress);
        }

        public static void TrimWorkingSet()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Optimized, false, false);
                using var proc = System.Diagnostics.Process.GetCurrentProcess();
                NativeMethods.SetProcessWorkingSetSize(proc.Handle, new IntPtr(-1), new IntPtr(-1));
            }
            catch { }
        }

        public void ToggleFromExternal()
        {
            if (_isTucked) SetTucked(false);
            if (_isAutoHiddenByVisibility) SetAutoHidden(false);
            _machine.Toggle("tray-or-cli");
        }
    }
}
