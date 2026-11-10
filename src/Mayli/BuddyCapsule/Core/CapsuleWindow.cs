using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        public const double BannerCollapsedHeight = 42.0;
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
        private readonly DockPanel _bannerTextStack;

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

        // 5D. Medium-Priority System Volume & Screen Brightness HUD
        private enum NotchHudKind
        {
            Volume = 0,
            Brightness = 1
        }

        // Mode A (when Music IS playing): Circular right-side detached capsule with circular ring + AnimateIcon
        private readonly Border _rightHudSatelliteIsland;
        private readonly Path _rightHudRingTrack;
        private readonly Path _rightHudRingArc;
        private readonly AnimateIcon _rightHudIcon;
        private readonly TranslateTransform _rightHudTranslate = new(0, 0);
        private readonly ScaleTransform _rightHudScale = new(0.8, 0.8);

        // Mode B (when NO Music is playing): Inside default resting notch with AnimateIcon + horizontal line progress bar
        private readonly Grid _collapsedHudView;
        private readonly AnimateIcon _collapsedHudIcon;
        private readonly Grid _collapsedHudBarHost;
        private readonly Border _collapsedHudBarTrack;
        private readonly Border _collapsedHudBarFill;
        private readonly TranslateTransform _collapsedHudTranslate = new(0, 0);

        private readonly DispatcherTimer _hudDismissTimer;
        private bool _isHudActive = false;
        private NotchHudKind _activeHudKind = NotchHudKind.Volume;
        private bool _hudIsMuted = false;
        private double _hudTargetRatio = 0.0;
        private double _hudDisplayedRatio = 0.0;
        private double _lastRenderedHudRatio = -1.0;
        private double _rightHudProgress = 0.0;
        private double _rightHudTarget = 0.0;

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
        private bool _isOpenedByHotkey = false;
        private readonly TranslateTransform _capsuleCardTranslate = new(0, 0);
        private readonly DispatcherTimer _mediaSettleExitTimer;
        private readonly DispatcherTimer _dynamicVisibilityTimer;

        public static CapsuleWindow Current { get; private set; }
        public bool IsTucked => _isTucked;

        public static void ToggleWindow()
        {
            Current?.Dispatcher?.InvokeAsync(() => Current.ToggleFromExternal());
        }

        public static void ToggleTuckWindow()
        {
            Current?.Dispatcher?.InvokeAsync(() => Current.ToggleTuck());
        }

        public CapsuleWindow()
        {
            Current = this;
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
                ClipToBounds = false,
                FlowDirection = FlowDirection.LeftToRight
            };

            // View 1: Normal resting collapsed view ("8:51 PM" on left, [Green Battery] "100" on right)
            _collapsedNormalView = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };
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
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Thickness(2, 0, 2, 0)
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

            // View 1D: Medium-Priority Volume & Brightness HUD View (Icon + Linear Progress Bar inside Default Notch)
            _collapsedHudView = new Grid
            {
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(2, 0, 4, 0),
                FlowDirection = FlowDirection.LeftToRight,
                RenderTransform = _collapsedHudTranslate
            };
            _collapsedHudView.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedHudView.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _collapsedHudIcon = new AnimateIcon(AnimateIconType.Volume, size: 16, brush: Brushes.White)
            {
                Margin = new Thickness(2, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                AutoAnimateOnHover = false,
                IsHitTestVisible = false
            };
            Grid.SetColumn(_collapsedHudIcon, 0);
            _collapsedHudView.Children.Add(_collapsedHudIcon);

            _collapsedHudBarHost = new Grid
            {
                Height = 14.0,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FlowDirection = FlowDirection.LeftToRight
            };
            _collapsedHudBarHost.SizeChanged += (_, _) => UpdateHudProgressGeometry();

            _collapsedHudBarTrack = new Border
            {
                Height = 5.5,
                CornerRadius = new CornerRadius(2.75),
                Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center
            };
            _collapsedHudBarFill = new Border
            {
                Height = 5.5,
                Width = 0.0,
                CornerRadius = new CornerRadius(2.75),
                Background = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            _collapsedHudBarHost.Children.Add(_collapsedHudBarTrack);
            _collapsedHudBarHost.Children.Add(_collapsedHudBarFill);
            Grid.SetColumn(_collapsedHudBarHost, 1);
            _collapsedHudView.Children.Add(_collapsedHudBarHost);

            _collapsedContent.Children.Add(_collapsedHudView);

            // View 2A: Compact Live Media Playback View (312 x 50, perfectly fitted inside margins)
            _collapsedMediaView = new Grid
            {
                Visibility = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = false,
                RenderTransform = new TranslateTransform(0, 0)
            };
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _collapsedMediaView.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Row 0: Album Art (22x22) | Text | Mini Equalizer
            var compactTopGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 1)
            };
            compactTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            compactTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            compactTopGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _albumArtThumb = new AlbumArtThumbnail(22.0, 5.5)
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 8, 1)
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
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            _mediaSepRun = new Run(" – ")
            {
                FontSize = 11.5,
                FontWeight = FontWeights.Normal,
                Foreground = CapsuleTheme.Brush(p.Text3)
            };
            _mediaArtistRun = new Run("NOTSOBAD")
            {
                FontSize = 11.5,
                FontWeight = FontWeights.Normal,
                Foreground = CapsuleTheme.Brush(p.Text2)
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
                Margin = new Thickness(14, 1, 14, 3)
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
                Foreground = CapsuleTheme.Brush(p.Text),
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
                Foreground = CapsuleTheme.Brush(p.Text2),
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
                Foreground = CapsuleTheme.Brush(p.Text3),
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
                Background = CapsuleTheme.Brush(p.Surface2)
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

            _bannerTextStack = new DockPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                LastChildFill = true,
                ClipToBounds = true,
                MaxWidth = 270
            };

            _bannerAppName = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 0, 1, 0),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_bannerAppName, Dock.Left);
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
            DockPanel.SetDock(_bannerTitle, Dock.Left);
            _bannerTextStack.Children.Add(_bannerTitle);

            _bannerSubtitle = new TextBlock
            {
                Text = " · Image",
                FontSize = 12.0,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
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
            var headerBar = new DockPanel { Margin = new Thickness(2, 2, 2, 12), VerticalAlignment = VerticalAlignment.Center };

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
                    e.Handled = true;
                    OnDockWheelDelta(e.Delta);
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
            _navDockBorder.PreviewMouseWheel += (s, e) =>
            {
                if (e.Delta != 0)
                {
                    e.Handled = true;
                    OnDockWheelDelta(e.Delta);
                }
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

            var leftIslandGrid = new Grid
            {
                Width = 36,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
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

            // 5D. Detached Right Circular HUD Satellite Capsule (when Volume/Brightness changes while Music IS playing)
            var rightHudTransformGroup = new TransformGroup();
            rightHudTransformGroup.Children.Add(_rightHudScale);
            rightHudTransformGroup.Children.Add(_rightHudTranslate);

            var rightHudGrid = new Grid
            {
                Width = 36,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _rightHudRingTrack = new Path
            {
                Data = new EllipseGeometry(new Point(18, 18), 16.2, 16.2),
                Stroke = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)),
                StrokeThickness = 2.6
            };
            _rightHudRingArc = new Path
            {
                Stroke = Brushes.White,
                StrokeThickness = 2.6,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            _rightHudIcon = new AnimateIcon(AnimateIconType.Volume, size: 15, brush: Brushes.White)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                AutoAnimateOnHover = false,
                IsHitTestVisible = false
            };
            rightHudGrid.Children.Add(_rightHudRingTrack);
            rightHudGrid.Children.Add(_rightHudRingArc);
            rightHudGrid.Children.Add(_rightHudIcon);

            var rightHudViewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Child = rightHudGrid
            };

            _rightHudSatelliteIsland = new Border
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
                RenderTransform = rightHudTransformGroup,
                Cursor = Cursors.Hand,
                Visibility = Visibility.Collapsed,
                Child = rightHudViewbox
            };
            _rightHudSatelliteIsland.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                NavigateToTool("system");
                _edgeLight.TriggerBurst(4.5);
                _machine.Click("right-hud-island");
            };
            _rightHudSatelliteIsland.MouseRightButtonUp += (_, e) =>
            {
                if (_activeHudKind == NotchHudKind.Volume)
                {
                    e.Handled = true;
                    CoreAudioService.Instance.ToggleMute();
                }
            };

            // Add detached islands before _capsuleCard so they emerge smoothly from behind the main notch
            _envelopeRoot.Children.Add(_musicSatelliteIsland);
            _envelopeRoot.Children.Add(_leftTimerIsland);
            _envelopeRoot.Children.Add(_rightTimerIsland);
            _envelopeRoot.Children.Add(_rightHudSatelliteIsland);
            _envelopeRoot.Children.Add(_capsuleCard);
            Content = _envelopeRoot;

            // Morph Animation timer (Render priority for 60fps smooth transitions)
            _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _morphTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(12) };
            _morphTimer.Tick += OnMorphTick;

            // Keep the notch open while a dropdown/context menu or modal dialog from a tool is showing
            NotchInteractionGuard.Register();
            _machine.HoldOpen = () => _isOpenedByHotkey || NotchInteractionGuard.IsHeld;
            NotchInteractionGuard.Released += () => Dispatcher.BeginInvoke(new Action(() => _machine.ReevaluateHold()));

            _machine.PhaseChanged += (_, next, _) =>
            {
                if (next == NotchPhase.Closing || next == NotchPhase.Resting)
                {
                    _isOpenedByHotkey = false;
                }

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
                        AudioSpectrumService.Instance.SetActive(false);
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

            // System Volume & Screen Brightness HUD Auto-Dismiss Timer
            _hudDismissTimer = new DispatcherTimer();
            _hudDismissTimer.Tick += (_, _) =>
            {
                _hudDismissTimer.Stop();
                DismissHudNotification();
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

            // Hook System Volume & Screen Brightness Changes (Medium-Priority Live Notch HUD)
            CoreAudioService.Instance.VolumeNotification += (pct, muted, _) =>
                Dispatcher.InvokeAsync(() => OnSystemVolumeChanged(pct, muted));
            ScreenBrightnessService.Instance.BrightnessChanged += pct =>
                Dispatcher.InvokeAsync(() => OnScreenBrightnessChanged(pct));

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

            // Escape to close expanded Notch (Tool switching is strictly mouse-click driven per design)
            PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    if (_morphProgress > 0.05 || _machine.IsOpenOrOpening)
                    {
                        _isOpenedByHotkey = false;
                        _machine.RequestClose("escape-key");
                        e.Handled = true;
                        return;
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

                if ((payload.Icon == "🌐" || payload.Icon == "⌨️") && !AppPreferences.Instance.KeyboardLanguageActivity)
                    return;

                if (payload.Icon == "⏱" && !AppPreferences.Instance.TimerFinishAlerts && !AppPreferences.Instance.RunningTimersActivity)
                    return;

                if (payload.Icon == "📅" && !AppPreferences.Instance.CalendarAlerts)
                    return;

                _isBannerActive = true;
                string titleText = (payload.Title ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
                string subText = (payload.Subtitle ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
                string appText = (payload.AppName ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();

                var bannerPalette = CapsuleTheme.Current;
                _bannerTitle.Foreground = CapsuleTheme.Brush(bannerPalette.Text);
                _bannerSubtitle.Foreground = CapsuleTheme.Brush(bannerPalette.Text2);
                _bannerAppName.Foreground = CapsuleTheme.Brush(bannerPalette.Text2);
                _bannerIcon.Foreground = CapsuleTheme.Brush(bannerPalette.Text);
                _bannerAppIconBorder.Background = CapsuleTheme.Brush(bannerPalette.Surface2);

                _bannerTitle.Text = titleText;
                _bannerSubtitle.Text = subText;
                _bannerSubtitle.Visibility = string.IsNullOrWhiteSpace(subText) ? Visibility.Collapsed : Visibility.Visible;
                _bannerSubtitle.Margin = (!string.IsNullOrWhiteSpace(titleText) && !string.IsNullOrWhiteSpace(subText) && !char.IsWhiteSpace(subText[0]) && subText[0] != '·')
                    ? new Thickness(5, 0, 0, 0)
                    : new Thickness(0);

                if (!string.IsNullOrWhiteSpace(appText) && !string.Equals(appText, titleText, StringComparison.OrdinalIgnoreCase))
                {
                    _bannerAppName.Text = appText + " · ";
                    _bannerAppName.Visibility = Visibility.Visible;
                }
                else
                {
                    _bannerAppName.Text = "";
                    _bannerAppName.Visibility = Visibility.Collapsed;
                }

                // Ensure collapsed content root is fully visible and opaque
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedContent.Opacity = 1.0;

                // Display banner view before icon play so elements are in the visual presentation tree
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.CutScissors;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Title == "Copied" || payload.Icon == "📋" || payload.Icon == "📎" || payload.Icon == "📁")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = (payload.Title == "Cut") ? AnimateIconType.CutScissors : AnimateIconType.CopyCheck;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🎧" || (payload.Title != null && payload.Title.Contains("Connected", StringComparison.OrdinalIgnoreCase)))
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(Color.FromRgb(10, 132, 255));
                    _bannerAnimateIcon.IconType = AnimateIconType.Bluetooth;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🔊" || payload.Icon == "🔈")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.Volume;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "🌐" || payload.Icon == "⌨️")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.Globe;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else if (payload.Icon == "⏱" || payload.Icon == "⏰")
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerAnimateIcon.IconType = AnimateIconType.Timer;
                    _bannerAnimateIcon.Visibility = Visibility.Visible;
                    _bannerAnimateIcon.Play(forceRestart: true);
                }
                else
                {
                    _bannerAppIconBorder.Visibility = Visibility.Collapsed;
                    _bannerAnimateIcon.Visibility = Visibility.Collapsed;
                    _bannerIcon.Visibility = Visibility.Visible;
                    _bannerIcon.Foreground = CapsuleTheme.Brush(CapsuleTheme.Current.Text);
                    _bannerIcon.Text = !string.IsNullOrEmpty(payload.Icon) ? payload.Icon : "🔔";
                }

                // PRIORITY HANDLING: Notification (5s) > Timer > Music
                // 1. Music Satellite Island: if music notch is currently active, album art detaches smoothly to the left!
                bool isMusicPlaying = (_isMusicActive || _isMediaRetained) && AppPreferences.Instance.ShowClosedNowPlaying;
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

                // Size ALL notification/Bluetooth/copy/cut banners dynamically to match whichever notch is active right now
                ApplyBannerDimensionsAndLayout(isMusicPlaying, isTimerActive, timerSnap);

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

            var pSat = CapsuleTheme.Current;
            Color beatColor = _currentSongBeatColor.A > 0 ? _currentSongBeatColor : Color.FromRgb(255, 60, 90);
            if (pSat.IsLight)
            {
                double lum = (0.299 * beatColor.R + 0.587 * beatColor.G + 0.114 * beatColor.B) / 255.0;
                double maxC = Math.Max(beatColor.R, Math.Max(beatColor.G, beatColor.B));
                double minC = Math.Min(beatColor.R, Math.Min(beatColor.G, beatColor.B));
                double sat = maxC > 0 ? (maxC - minC) / maxC : 0;
                if (lum > 0.72 || sat < 0.22) beatColor = pSat.Accent;
            }
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
                    AudioSpectrumService.Instance.SetActive(false);
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
                    AudioSpectrumService.Instance.SetActive(true);
                    _spectrumTimer.Start();
                }
                else
                {
                    if (wasPlaying && _isMediaRetained)
                    {
                        // Music paused/stopped: smoothly decay equalizer and keep media card alive for 3.5s before morphing
                        AudioSpectrumService.Instance.SetActive(true);
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
                var mediaPalette = CapsuleTheme.Current;
                _mediaTrackRun.Foreground = CapsuleTheme.Brush(mediaPalette.Text);
                _mediaSepRun.Foreground = CapsuleTheme.Brush(mediaPalette.Text3);
                _mediaArtistRun.Foreground = CapsuleTheme.Brush(mediaPalette.Text2);
                _wideMediaTrackText.Foreground = CapsuleTheme.Brush(mediaPalette.Text);
                _wideRemainingText.Foreground = CapsuleTheme.Brush(mediaPalette.Text2);
                _wideMediaArtistText.Foreground = CapsuleTheme.Brush(mediaPalette.Text3);

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

                // Timeline sync for live song time and progress scrubber
                if (snapshot.DurationSeconds > 1.0)
                {
                    _mediaDurSeconds = snapshot.DurationSeconds;
                    _mediaPosSeconds = Math.Clamp(snapshot.PositionSeconds, 0.0, _mediaDurSeconds);
                    _lastMediaTimelineSyncUtc = DateTime.UtcNow;
                }
                else
                {
                    _mediaDurSeconds = 0.0;
                    _mediaPosSeconds = Math.Max(0.0, snapshot.PositionSeconds);
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

            double currentPos = Math.Max(0.0, _mediaPosSeconds + elapsedSinceSync);
            string remStr;
            double ratio;

            if (_mediaDurSeconds > 1.0)
            {
                currentPos = Math.Clamp(currentPos, 0.0, _mediaDurSeconds);
                double remaining = Math.Max(0.0, _mediaDurSeconds - currentPos);
                remStr = FormatRemaining(remaining);
                ratio = Math.Clamp(currentPos / _mediaDurSeconds, 0.0, 1.0);
            }
            else
            {
                remStr = FormatRemaining(currentPos);
                ratio = 0.0;
            }

            if (!string.Equals(_wideRemainingText.Text, remStr, StringComparison.Ordinal))
            {
                _wideRemainingText.Text = remStr;
            }

            double trackWidth = _wideProgressTrack.ActualWidth > 10.0 ? _wideProgressTrack.ActualWidth : 200.0;
            double fillW = _mediaDurSeconds > 1.0 ? Math.Clamp(trackWidth * ratio, 3.0, trackWidth) : 0.0;
            _wideProgressFill.Width = fillW;

            double thumbX = Math.Clamp(fillW - 3.5, 0.0, Math.Max(0.0, trackWidth - 7.0));
            _wideProgressThumbTranslate.X = thumbX;
        }

        private void UpdateWideScrubberTheme(Color beatColor, bool isCustom)
        {
            bool isLight = CapsuleTheme.Current.IsLight;
            Color cStart, cMid, cEnd;
            if (isLight)
            {
                cStart = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.85, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.85, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.85, 0, 255));
                cMid = beatColor;
                cEnd = Color.FromRgb(
                    (byte)Math.Clamp(beatColor.R * 0.65, 0, 255),
                    (byte)Math.Clamp(beatColor.G * 0.65, 0, 255),
                    (byte)Math.Clamp(beatColor.B * 0.65, 0, 255));
            }
            else if (isCustom)
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
            return $"{mins}:{secs:D2}";
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
                    else if (_collapsedHudView.Visibility == Visibility.Visible)
                    {
                        AnimateViewEntrance(_collapsedHudView, 5.0);
                        _collapsedHudIcon?.Play(forceRestart: true);
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

        private void OnSystemVolumeChanged(int percent, bool isMuted)
        {
            if (!AppPreferences.Instance.VolumeActivity) return;
            TriggerHudNotification(NotchHudKind.Volume, percent, isMuted);
        }

        private void OnScreenBrightnessChanged(int percent)
        {
            if (!AppPreferences.Instance.BrightnessActivity) return;
            TriggerHudNotification(NotchHudKind.Brightness, percent, false);
        }

        private void TriggerHudNotification(NotchHudKind kind, int percent, bool isMuted)
        {
            if (_isTucked || _morphProgress > 0.15 || _machine.IsOpenOrOpening)
                return;

            bool wasActiveSameKind = _isHudActive && _activeHudKind == kind;
            bool prevMuted = _hudIsMuted;

            _isHudActive = true;
            _activeHudKind = kind;
            _hudIsMuted = (kind == NotchHudKind.Volume) && isMuted;
            _hudTargetRatio = _hudIsMuted ? 0.0 : Math.Clamp(percent / 100.0, 0.0, 1.0);

            if (!wasActiveSameKind)
            {
                _hudDisplayedRatio = _hudTargetRatio;
            }

            UpdateHudStylingAndIcons();
            UpdateHudProgressGeometry();

            bool showMedia = (_isMusicActive || _isMediaRetained) && AppPreferences.Instance.ShowClosedNowPlaying;
            UpdateCollapsedViewState();

            if (!_isBannerActive && !_isMessageActive)
            {
                if (showMedia)
                {
                    _rightHudIcon?.Play(forceRestart: !wasActiveSameKind || prevMuted != _hudIsMuted);
                }
                else
                {
                    if (!wasActiveSameKind)
                    {
                        AnimateViewEntrance(_collapsedHudView, -3.0);
                    }
                    _collapsedHudIcon?.Play(forceRestart: !wasActiveSameKind || prevMuted != _hudIsMuted);
                }
            }

            _edgeLight.TriggerBurst(3.2);

            if (_morphProgress <= 0.1 && !_machine.IsOpenOrOpening)
            {
                _targetProgress = 0.0;
                _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                _morphTimer.Start();
            }

            int durationMs = AppPreferences.Instance.LiveActivityEventDuration switch
            {
                "2 s" => 1800,
                "5 s" => 3500,
                _ => 2200
            };
            _hudDismissTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _hudDismissTimer.Stop();
            _hudDismissTimer.Start();
        }

        private void DismissHudNotification()
        {
            if (!_isHudActive) return;
            bool wasCenterHud = _collapsedHudView.Visibility == Visibility.Visible;

            _isHudActive = false;
            _rightHudTarget = 0.0;
            UpdateCollapsedViewState();

            if (wasCenterHud)
            {
                if (_collapsedTimerView.Visibility == Visibility.Visible)
                {
                    AnimateViewEntrance(_collapsedTimerView, 4.0);
                }
                else if (_collapsedNormalView.Visibility == Visibility.Visible)
                {
                    AnimateViewEntrance(_collapsedNormalView, 4.0);
                }
                else if (_collapsedMinimalView.Visibility == Visibility.Visible)
                {
                    AnimateViewEntrance(_collapsedMinimalView, 4.0);
                }
            }

            if (_morphProgress <= 0.1 && !_machine.IsOpenOrOpening)
            {
                _lastMorphTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                _morphTimer.Start();
            }
        }

        private void UpdateHudStylingAndIcons()
        {
            var p = CapsuleTheme.Current;
            Color iconColor;
            Color fillColor;
            Color trackColor = p.IsLight
                ? Color.FromArgb(40, 0, 0, 0)
                : Color.FromArgb(42, 255, 255, 255);
            Color ringTrackColor = p.IsLight
                ? Color.FromArgb(34, 0, 0, 0)
                : Color.FromArgb(36, 255, 255, 255);

            AnimateIconType desiredIconType;
            if (_activeHudKind == NotchHudKind.Brightness)
            {
                desiredIconType = AnimateIconType.Brightness;
                iconColor = p.IsLight ? Color.FromRgb(218, 138, 0) : Color.FromRgb(255, 214, 10);
                fillColor = p.IsLight ? Color.FromRgb(230, 149, 0) : Color.FromRgb(255, 214, 10);
            }
            else
            {
                desiredIconType = (_hudIsMuted || _hudTargetRatio <= 0.002)
                    ? AnimateIconType.VolumeMute
                    : AnimateIconType.Volume;

                if (_hudIsMuted)
                {
                    iconColor = Color.FromRgb(255, 69, 58);
                    fillColor = Color.FromRgb(255, 69, 58);
                }
                else
                {
                    iconColor = p.IsLight ? Color.FromRgb(28, 28, 34) : Color.FromRgb(250, 250, 254);
                    fillColor = p.IsLight ? Color.FromRgb(28, 28, 34) : Color.FromRgb(250, 250, 254);
                }
            }

            var iconBrush = CapsuleTheme.Brush(iconColor);
            var fillBrush = CapsuleTheme.Brush(fillColor);
            var trackBrush = CapsuleTheme.Brush(trackColor);
            var ringTrackBrush = CapsuleTheme.Brush(ringTrackColor);

            if (_collapsedHudIcon != null)
            {
                if (_collapsedHudIcon.IconType != desiredIconType)
                    _collapsedHudIcon.IconType = desiredIconType;
                _collapsedHudIcon.StrokeBrush = iconBrush;
            }
            if (_rightHudIcon != null)
            {
                if (_rightHudIcon.IconType != desiredIconType)
                    _rightHudIcon.IconType = desiredIconType;
                _rightHudIcon.StrokeBrush = iconBrush;
            }

            if (_collapsedHudBarTrack != null) _collapsedHudBarTrack.Background = trackBrush;
            if (_collapsedHudBarFill != null) _collapsedHudBarFill.Background = fillBrush;
            if (_rightHudRingTrack != null) _rightHudRingTrack.Stroke = ringTrackBrush;
            if (_rightHudRingArc != null) _rightHudRingArc.Stroke = fillBrush;
        }

        private void UpdateHudProgressGeometry()
        {
            _lastRenderedHudRatio = _hudDisplayedRatio;
            double ratio = Math.Clamp(_hudDisplayedRatio, 0.0, 1.0);

            if (_rightHudRingArc != null)
            {
                DrawTimerRingArc(_rightHudRingArc, new Point(18, 18), 16.2, ratio);
            }

            if (_collapsedHudBarHost != null && _collapsedHudBarFill != null)
            {
                double trackW = _collapsedHudBarHost.ActualWidth;
                if (trackW > 1.0)
                {
                    double fillW = ratio <= 0.002 ? 0.0 : Math.Clamp(trackW * ratio, 3.5, trackW);
                    _collapsedHudBarFill.Width = fillW;
                    _collapsedHudBarFill.Visibility = fillW <= 0.1 ? Visibility.Collapsed : Visibility.Visible;
                }
            }
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
            double prevRightHudTarget = _rightHudTarget;
            _splitIslandsTarget = 0.0;
            _musicSatelliteTarget = 0.0;
            _rightHudTarget = 0.0;

            if (_isBannerActive)
            {
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedHudView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Visible;
                ApplyBannerDimensionsAndLayout(showMedia, showTimer, timerSnap);
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
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                if (_isHudActive) _rightHudTarget = 1.0;
            }
            else if (_isHudActive && !showMedia)
            {
                // Medium-priority HUD inside Default Resting Notch (No Music playing — overrides Timer & Minimal)
                _collapsedContent.Visibility = Visibility.Visible;
                _collapsedMinimalView.Visibility = Visibility.Collapsed;
                _collapsedBannerView.Visibility = Visibility.Collapsed;
                _collapsedMediaView.Visibility = Visibility.Collapsed;
                _collapsedMediaWideView.Visibility = Visibility.Collapsed;
                _collapsedMessageView.Visibility = Visibility.Collapsed;
                _collapsedNormalView.Visibility = Visibility.Collapsed;
                _collapsedTimerView.Visibility = Visibility.Collapsed;
                _collapsedHudView.Visibility = Visibility.Visible;

                GetConfiguredRestingDimensions(out double rw, out double rh);
                _currentBaseCollapsedWidth = rw;
                _currentBaseCollapsedHeight = rh;

                double hudScale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.78, 1.24);
                double iconPx = Math.Round(16.0 * hudScale);
                if (Math.Abs(_collapsedHudIcon.Width - iconPx) > 0.4)
                {
                    _collapsedHudIcon.Width = iconPx;
                    _collapsedHudIcon.Height = iconPx;
                    _collapsedHudIcon.Margin = new Thickness(2, 0, Math.Round(9.0 * hudScale), 0);
                }
                double barH = Math.Round(Math.Clamp(5.5 * hudScale, 4.5, 7.5), 1);
                if (Math.Abs(_collapsedHudBarTrack.Height - barH) > 0.2)
                {
                    _collapsedHudBarTrack.Height = barH;
                    _collapsedHudBarTrack.CornerRadius = new CornerRadius(barH / 2.0);
                    _collapsedHudBarFill.Height = barH;
                    _collapsedHudBarFill.CornerRadius = new CornerRadius(barH / 2.0);
                }
                UpdateHudProgressGeometry();
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
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                if (_isHudActive) _rightHudTarget = 1.0;

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
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                _collapsedHudView.Visibility = Visibility.Collapsed;
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
                             Math.Abs(prevMusicSatTarget - _musicSatelliteTarget) > 0.01 ||
                             Math.Abs(prevRightHudTarget - _rightHudTarget) > 0.01;

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

        private void ApplyBannerDimensionsAndLayout(bool isMusicPlaying, bool isTimerActive, WindowsBuddy.Tools.Timer.NotchTimerSnapshot timerSnap)
        {
            double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
            double baseH = AppPreferences.Instance.BannerFontSize switch
            {
                "Large" => 44.0,
                "Extra Large" => 48.0,
                "Small" => 38.0,
                _ => 41.5
            };
            double activeW = Math.Round((BannerCollapsedWidth + 30.0) * scale);
            double activeH = Math.Round(baseH * scale);
            _currentBaseCollapsedWidth = activeW;
            _currentBaseCollapsedHeight = activeH;
            UpdateBannerVisualScale(activeW, activeH);
        }

        private static double MeasureBannerTextWidth(TextBlock tb, double fontSize, FontWeight weight)
        {
            if (tb == null || string.IsNullOrEmpty(tb.Text)) return 0.0;
            double ppd = 1.0;
            try { ppd = VisualTreeHelper.GetDpi(tb).PixelsPerDip; } catch { }
            var ft = new FormattedText(
                tb.Text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                new Typeface(tb.FontFamily, tb.FontStyle, weight, tb.FontStretch),
                fontSize,
                Brushes.White,
                ppd);
            return Math.Ceiling(ft.WidthIncludingTrailingWhitespace) + 0.5;
        }

        private void UpdateBannerVisualScale(double bannerW, double bannerH)
        {
            double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
            double fontMultiplier = AppPreferences.Instance.BannerFontSize switch
            {
                "Small" => 1.05,
                "Large" => 1.36,
                "Extra Large" => 1.55,
                _ => 1.22 // "Medium" (Default) - visibly enlarged for readability
            };

            bool isWide = bannerH >= 60.0;
            bool isMedium = bannerH >= 40.0 && !isWide;

            double titleSize = isWide
                ? Math.Round(16.5 * scale * fontMultiplier, 1)
                : (isMedium ? Math.Round(14.5 * scale * fontMultiplier, 1) : Math.Round(Math.Clamp(12.5 * scale * fontMultiplier, 11.5, 18.0), 1));
            double subSize = isWide
                ? Math.Round(15.0 * scale * fontMultiplier, 1)
                : (isMedium ? Math.Round(13.5 * scale * fontMultiplier, 1) : Math.Round(Math.Clamp(11.8 * scale * fontMultiplier, 11.0, 17.0), 1));
            double appSize = Math.Max(10.5, Math.Round(titleSize - 1.0, 1));

            double iconScaleFactor = Math.Min(1.35, Math.Max(0.95, fontMultiplier));
            double iconSize = isWide
                ? Math.Round(24.0 * scale * iconScaleFactor)
                : (isMedium ? Math.Round(20.0 * scale * iconScaleFactor) : Math.Round(Math.Clamp((bannerH - 12.0) * iconScaleFactor, 14.0, 20.0 * scale)));
            double appIconSize = isWide
                ? Math.Round(30.0 * scale * iconScaleFactor)
                : (isMedium ? Math.Round(24.0 * scale * iconScaleFactor) : Math.Round(Math.Clamp((bannerH - 9.0) * iconScaleFactor, 16.0, 24.0 * scale)));
            double emojiSize = isWide
                ? Math.Round(20.0 * scale * iconScaleFactor)
                : (isMedium ? Math.Round(16.5 * scale * iconScaleFactor) : Math.Round(Math.Clamp((bannerH - 14.0) * iconScaleFactor, 13.0, 18.0 * scale)));
            double iconMarginR = isWide ? 9.0 : 7.0;

            if (_bannerTitle != null && Math.Abs(_bannerTitle.FontSize - titleSize) > 0.3)
                _bannerTitle.FontSize = titleSize;
            if (_bannerSubtitle != null && Math.Abs(_bannerSubtitle.FontSize - subSize) > 0.3)
                _bannerSubtitle.FontSize = subSize;
            if (_bannerAppName != null && Math.Abs(_bannerAppName.FontSize - appSize) > 0.3)
                _bannerAppName.FontSize = appSize;

            if (_bannerAnimateIcon != null && (Math.Abs(_bannerAnimateIcon.Width - iconSize) > 0.4 || Math.Abs(_bannerAnimateIcon.Margin.Right - iconMarginR) > 0.2))
            {
                _bannerAnimateIcon.Width = iconSize;
                _bannerAnimateIcon.Height = iconSize;
                _bannerAnimateIcon.Margin = new Thickness(0, 0, iconMarginR, 0);
            }
            if (_bannerIcon != null && (Math.Abs(_bannerIcon.FontSize - emojiSize) > 0.4 || Math.Abs(_bannerIcon.Margin.Right - iconMarginR) > 0.2))
            {
                _bannerIcon.FontSize = emojiSize;
                _bannerIcon.Margin = new Thickness(0, 0, iconMarginR, 0);
            }
            if (_bannerAppIconBorder != null && (Math.Abs(_bannerAppIconBorder.Width - appIconSize) > 0.4 || Math.Abs(_bannerAppIconBorder.Margin.Right - iconMarginR) > 0.2))
            {
                _bannerAppIconBorder.Width = appIconSize;
                _bannerAppIconBorder.Height = appIconSize;
                _bannerAppIconBorder.CornerRadius = new CornerRadius(Math.Round(appIconSize * 0.25, 1));
                _bannerAppImage.Width = appIconSize;
                _bannerAppImage.Height = appIconSize;
                _bannerAppIconBorder.Margin = new Thickness(0, 0, iconMarginR, 0);
            }

            if (_bannerTextStack == null) return;

            bool isIslandOrLine = string.Equals(AppPreferences.Instance.NotchStyle, "Island", StringComparison.OrdinalIgnoreCase) ||
                                  string.Equals(AppPreferences.Instance.NotchStyle, "Line", StringComparison.OrdinalIgnoreCase);
            double innerW = Math.Max(40.0, bannerW - (isIslandOrLine ? 14.0 : 32.0));

            double iconFootprint = 0.0;
            if (_bannerAppIconBorder != null && _bannerAppIconBorder.Visibility == Visibility.Visible)
                iconFootprint = appIconSize + iconMarginR;
            else if (_bannerAnimateIcon != null && _bannerAnimateIcon.Visibility == Visibility.Visible)
                iconFootprint = iconSize + iconMarginR;
            else if (_bannerIcon != null && _bannerIcon.Visibility == Visibility.Visible)
                iconFootprint = emojiSize + 4.0 + iconMarginR;

            double textBudgetW = Math.Max(52.0, innerW - iconFootprint);
            _bannerTextStack.MaxWidth = textBudgetW;

            bool hasApp = _bannerAppName != null && _bannerAppName.Visibility == Visibility.Visible && !string.IsNullOrEmpty(_bannerAppName.Text);
            bool hasSub = _bannerSubtitle != null && _bannerSubtitle.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(_bannerSubtitle.Text);

            double appMargin = hasApp ? _bannerAppName.Margin.Right : 0.0;
            double measuredAppW = hasApp ? MeasureBannerTextWidth(_bannerAppName, appSize, FontWeights.SemiBold) : 0.0;
            double measuredTitleW = MeasureBannerTextWidth(_bannerTitle, titleSize, FontWeights.SemiBold);
            double subMargin = hasSub ? _bannerSubtitle.Margin.Left : 0.0;
            double measuredSubW = hasSub ? MeasureBannerTextWidth(_bannerSubtitle, subSize, FontWeights.Normal) : 0.0;
            double totalNeeded = (hasApp ? measuredAppW + appMargin : 0.0) + measuredTitleW + (hasSub ? measuredSubW + subMargin : 0.0);

            if (totalNeeded <= textBudgetW)
            {
                if (hasApp) _bannerAppName.MaxWidth = textBudgetW;
                if (_bannerTitle != null) _bannerTitle.MaxWidth = textBudgetW;
                return;
            }

            double appUsedW = 0.0;
            if (hasApp)
            {
                double reserveForRest = hasSub ? 90.0 : 65.0;
                double maxAppShare = Math.Max(28.0, Math.Min(Math.Round(textBudgetW * (hasSub ? 0.36 : 0.42)), textBudgetW - reserveForRest));
                _bannerAppName.MaxWidth = maxAppShare;
                appUsedW = Math.Min(measuredAppW, maxAppShare) + appMargin;
            }

            double remAfterApp = Math.Max(36.0, textBudgetW - appUsedW);
            if (!hasSub)
            {
                if (_bannerTitle != null) _bannerTitle.MaxWidth = remAfterApp;
            }
            else
            {
                double subTotalW = measuredSubW + subMargin;
                if (measuredTitleW + subTotalW <= remAfterApp)
                {
                    _bannerTitle.MaxWidth = remAfterApp;
                }
                else if (subTotalW <= remAfterApp * 0.48)
                {
                    _bannerTitle.MaxWidth = Math.Max(36.0, remAfterApp - subTotalW);
                }
                else if (measuredTitleW <= remAfterApp * 0.45)
                {
                    _bannerTitle.MaxWidth = remAfterApp;
                }
                else
                {
                    _bannerTitle.MaxWidth = Math.Max(36.0, Math.Round(remAfterApp * 0.48));
                }
            }
        }

        private void GetActiveNotchBaseDimensions(bool isMusicPlaying, bool isTimerActive, WindowsBuddy.Tools.Timer.NotchTimerSnapshot timerSnap, out double width, out double height)
        {
            double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
            bool isIsland = string.Equals(AppPreferences.Instance.NotchStyle, "Island", StringComparison.OrdinalIgnoreCase);

            if (isMusicPlaying)
            {
                if (_isMediaWideActive)
                {
                    double targetW = isIsland ? 360.0 : MediaWideCollapsedWidth;
                    double targetH = isIsland ? 66.0 : MediaWideCollapsedHeight;
                    width = Math.Round(targetW * scale);
                    height = Math.Round(targetH * scale);
                }
                else
                {
                    double targetW = isIsland ? 280.0 : MediaCollapsedWidth;
                    double targetH = isIsland ? 38.0 : MediaCollapsedHeight;
                    width = Math.Round(targetW * scale);
                    height = Math.Round(targetH * scale);
                }
            }
            else if (isTimerActive)
            {
                GetConfiguredRestingDimensions(out double rw, out double rh);
                width = !string.IsNullOrWhiteSpace(timerSnap.Title) ? Math.Max(rw, 248.0) : rw;
                height = rh;
            }
            else
            {
                GetConfiguredRestingDimensions(out width, out height);
            }
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
            _collapsedNormalView.VerticalAlignment = VerticalAlignment.Center;
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
            bool hasLiveEvent = _isBannerActive || _isMessageActive || _isHudActive || (_isMusicActive && _isMediaWideActive);
            bool eventOverridesTuck = prefs.ShowEventsOverApps && hasLiveEvent;

            string visMode = prefs.NotchVisibility ?? "Always";
            bool isSmartMode = string.Equals(visMode, "Smart", StringComparison.OrdinalIgnoreCase);

            InspectWindowUnderNotch(isSmartMode, out bool isFgFullscreen, out bool isOccupiedByApp, out bool isFgMaximized);

            // Dynamically adapt timer rate: 160ms for Smart mode, 320ms for Always/Hidden mode
            var desiredRate = isSmartMode ? TimeSpan.FromMilliseconds(160) : TimeSpan.FromMilliseconds(450);
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
                // Auto-hide the notch when an application covers the notch zone or is maximized,
                // UNLESS the user is hovering at the top edge, or the notch is expanded, or an active event is showing
                bool shouldHideInSmart = isOccupiedByApp && !isOpenOrOpening && !eventOverridesTuck && !_isSmartPeekHovered && !_capsuleCard.IsMouseOver;
                if (shouldHideInSmart != _lastSmartDesiredHide)
                {
                    _lastSmartDesiredHide = shouldHideInSmart;
                    _smartDebounceTicks = 0;
                }
                else
                {
                    _smartDebounceTicks++;
                }

                // Debounce 2 ticks before triggering slide animation to completely prevent any fluttering/jumping
                if (_smartDebounceTicks >= 2)
                {
                    SetAutoHidden(shouldHideInSmart);
                }

                if (shouldHideInSmart && _isAutoHiddenByVisibility)
                {
                    return;
                }
            }
            else
            {
                _lastSmartDesiredHide = false;
                _smartDebounceTicks = 0;
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
                UpdateCollapsedViewState();
                UpdateMorphVisuals(_morphProgress);
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
                        _rightHudSatelliteIsland.Visibility = Visibility.Collapsed;
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
            _rightHudTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
        }

                private bool _lastSmartDesiredHide = false;
        private int _smartDebounceTicks = 2;

        private void InspectWindowUnderNotch(bool isSmartMode, out bool isFullscreen, out bool isOccupiedByApp, out bool isMaximized)
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

                // Inspect Foreground Window
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
                                    isOccupiedByApp = true;
                                }
                                else if (rect.Top <= notchBottom + 16 && rect.Bottom > notchTop &&
                                         rect.Left < notchRight && rect.Right > notchLeft)
                                {
                                    isOccupiedByApp = true;
                                }
                            }
                        }
                    }
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
            HotkeyService.Instance.Pressed += () => ToggleFromExternal();
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
        private DateTime _lastToggleTuckTime = DateTime.MinValue;

        public void ToggleTuck()
        {
            if ((DateTime.UtcNow - _lastToggleTuckTime).TotalMilliseconds < 350) return;
            _lastToggleTuckTime = DateTime.UtcNow;
            SetTucked(!_isTucked);
        }

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
                _rightHudSatelliteIsland.IsHitTestVisible = false;

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
                        _rightHudSatelliteIsland.Visibility = Visibility.Collapsed;
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
                        _rightHudSatelliteIsland.BeginAnimation(OpacityProperty, null);
                        UpdateMorphVisuals(_morphProgress);
                    }
                };
            }

            _capsuleCardTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _capsuleCard.BeginAnimation(OpacityProperty, fadeAnim);
            _leftIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _rightIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _musicSatelliteTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _rightHudTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _leftTimerIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _rightTimerIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _musicSatelliteIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _rightHudSatelliteIsland.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_MOUSEHWHEEL = 0x020E;
            if (msg == WM_MOUSEHWHEEL && _navDockBorder != null && _navDockBorder.IsVisible)
            {
                short delta = (short)((wParam.ToInt64() >> 16) & 0xffff);
                if (delta != 0)
                {
                    OnDockWheelDelta(-delta);
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

        private int _accumulatedDockWheelDelta = 0;
        private DateTime _lastDockWheelNavTime = DateTime.MinValue;

        private void OnDockWheelDelta(int delta)
        {
            _accumulatedDockWheelDelta += delta;
            if (_navScrollViewer != null && _navScrollViewer.ScrollableWidth > 0)
            {
                _navScrollViewer.ScrollToHorizontalOffset(_navScrollViewer.HorizontalOffset - (delta * 0.5));
            }

            if (Math.Abs(_accumulatedDockWheelDelta) >= 80 || (DateTime.UtcNow - _lastDockWheelNavTime).TotalMilliseconds > 160)
            {
                int direction = _accumulatedDockWheelDelta < 0 ? 1 : -1;
                _accumulatedDockWheelDelta = 0;
                _lastDockWheelNavTime = DateTime.UtcNow;
                HandleDockNavigationScroll(direction);
            }
        }

        private void HandleDockNavigationScroll(int direction)
        {
            var enabledTools = ToolRegistry.All
                .Where(t => AppPreferences.Instance.IsModuleEnabled(t.Id, true))
                .ToList();

            if (enabledTools.Count <= 1) return;

            int curIdx = enabledTools.FindIndex(t => string.Equals(t.Id, _activeToolId, StringComparison.OrdinalIgnoreCase));
            if (curIdx < 0) curIdx = 0;

            int nextIdx = curIdx + direction;
            if (nextIdx < 0) nextIdx = enabledTools.Count - 1;
            else if (nextIdx >= enabledTools.Count) nextIdx = 0;

            NavigateToTool(enabledTools[nextIdx].Id);

            if (_dockButtons.TryGetValue(enabledTools[nextIdx].Id, out var activeBtn))
            {
                activeBtn.BringIntoView();
            }
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

            var plusBtn = BuddyGlassButton.CreateHeader("+", LocalizationService.T("dock.customize", "Add / Customize Tools"), 28);
            plusBtn.Clicked += () => SettingsWindow.ShowSingleton();
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
                    CornerRadius = new CornerRadius(13),
                    Height = 26,
                    Padding = new Thickness(2),
                    Margin = new Thickness(0, 0, 8, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    ClipToBounds = true
                };
                var unitDock = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var cBtn = BuddyGlassButton.CreatePill(null, "°C", () =>
                {
                    svc.UseFahrenheit = false;
                    UpdateToolHeaderActions("weather");
                }, height: 20, style: !svc.UseFahrenheit ? BuddyGlassButtonStyle.Accent : BuddyGlassButtonStyle.Ghost);
                cBtn.Padding = new Thickness(8, 1, 8, 1);
                cBtn.Margin = new Thickness(0, 0, 2, 0);

                var fBtn = BuddyGlassButton.CreatePill(null, "°F", () =>
                {
                    svc.UseFahrenheit = true;
                    UpdateToolHeaderActions("weather");
                }, height: 20, style: svc.UseFahrenheit ? BuddyGlassButtonStyle.Accent : BuddyGlassButtonStyle.Ghost);
                fBtn.Padding = new Thickness(8, 1, 8, 1);

                unitDock.Children.Add(cBtn);
                unitDock.Children.Add(fBtn);
                unitPill.Child = unitDock;
                _toolHeaderActions.Children.Add(unitPill);

                // 2. Change City Pill Button
                var cityBtn = BuddyGlassButton.CreatePill("📍", "Change City", () =>
                {
                    WindowsBuddy.Tools.Weather.WeatherToolView.Instance?.ShowCityPicker();
                }, height: 26, style: BuddyGlassButtonStyle.GlassPill);
                cityBtn.Margin = new Thickness(4, 0, 0, 0);
                cityBtn.VerticalAlignment = VerticalAlignment.Center;

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

            // Smoothly interpolate detached right HUD capsule (0.0 <-> 1.0) over ~240ms
            double rightHudDiff = _rightHudTarget - _rightHudProgress;
            double rightHudStep = dt / 0.24;
            bool rightHudSettled = Math.Abs(rightHudDiff) <= rightHudStep;
            if (rightHudSettled)
            {
                _rightHudProgress = _rightHudTarget;
            }
            else
            {
                _rightHudProgress += Math.Sign(rightHudDiff) * rightHudStep;
            }

            // Smoothly interpolate displayed Volume/Brightness level (0.0 <-> 1.0) for fluid progress bar & ring motion
            double hudValDiff = _hudTargetRatio - _hudDisplayedRatio;
            bool hudValSettled = Math.Abs(hudValDiff) < 0.002;
            if (hudValSettled)
            {
                _hudDisplayedRatio = _hudTargetRatio;
            }
            else
            {
                double lerp = Math.Clamp(dt * 18.0, 0.14, 0.45);
                _hudDisplayedRatio += hudValDiff * lerp;
            }
            if (!hudValSettled || Math.Abs(_hudDisplayedRatio - _lastRenderedHudRatio) > 0.002)
            {
                UpdateHudProgressGeometry();
            }

            bool allSettled = morphSettled && sizeSettled && splitSettled && musicSettled && rightHudSettled && hudValSettled;
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
            // For Notch/Shouldered: side clearance of 44.0 protects against shoulders and corners without clipping content
            double sideClearance = (isIsland || isLine) ? 20.0 : 44.0;
            double topClearance = isIsland ? islandGap : (isLine ? 3.0 : 6.5);
            double bottomClearance = (isIsland || isLine) ? 4.0 : 7.0;

            double collapsedInnerWidth = _isBannerActive
                ? Math.Max(40.0, baseW - ((isIsland || isLine) ? 16.0 : 36.0))
                : Math.Max(40.0, baseW - sideClearance);

            if (_isBannerActive)
            {
                // When banner is active: center it perfectly across the entire vertical height of the notch
                double bannerInnerH = Math.Max(16.0, baseH);
                if (double.IsNaN(_collapsedContent.Width) || Math.Abs(_collapsedContent.Width - collapsedInnerWidth) > 0.4)
                {
                    _collapsedContent.Width = collapsedInnerWidth;
                }
                if (double.IsNaN(_collapsedContent.Height) || Math.Abs(_collapsedContent.Height - bannerInnerH) > 0.4)
                {
                    _collapsedContent.Height = bannerInnerH;
                }
                _collapsedContent.HorizontalAlignment = HorizontalAlignment.Center;
                _collapsedContent.VerticalAlignment = VerticalAlignment.Center;
                _collapsedContent.Margin = new Thickness(0, isIsland ? islandGap : 0, 0, 0);

                UpdateBannerVisualScale(baseW, baseH);
            }
            else if (_collapsedMediaView.Visibility == Visibility.Visible || _collapsedMediaWideView.Visibility == Visibility.Visible)
            {
                double collapsedInnerHeight = isIsland
                    ? Math.Max(28.0, baseH)
                    : Math.Max(38.0, baseH - topClearance);

                if (double.IsNaN(_collapsedContent.Width) || Math.Abs(_collapsedContent.Width - collapsedInnerWidth) > 0.4)
                {
                    _collapsedContent.Width = collapsedInnerWidth;
                }
                if (double.IsNaN(_collapsedContent.Height) || Math.Abs(_collapsedContent.Height - collapsedInnerHeight) > 0.4)
                {
                    _collapsedContent.Height = collapsedInnerHeight;
                }
                _collapsedContent.HorizontalAlignment = HorizontalAlignment.Center;
                _collapsedContent.VerticalAlignment = isIsland ? VerticalAlignment.Center : VerticalAlignment.Top;
                _collapsedContent.Margin = new Thickness(0, isIsland ? islandGap : topClearance, 0, 0);
            }
            else
            {
                // Resting default normal view (Clock, Date, Pet Orb, Battery) or HUD / Timer
                double collapsedInnerHeight = Math.Max(16.0, baseH);
                if (double.IsNaN(_collapsedContent.Width) || Math.Abs(_collapsedContent.Width - collapsedInnerWidth) > 0.4)
                {
                    _collapsedContent.Width = collapsedInnerWidth;
                }
                if (double.IsNaN(_collapsedContent.Height) || Math.Abs(_collapsedContent.Height - collapsedInnerHeight) > 0.4)
                {
                    _collapsedContent.Height = collapsedInnerHeight;
                }
                _collapsedContent.HorizontalAlignment = HorizontalAlignment.Center;
                _collapsedContent.VerticalAlignment = VerticalAlignment.Center;
                _collapsedContent.Margin = new Thickness(0, isIsland ? islandGap : 0, 0, 0);
            }

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
                _albumArtThumb.Margin = new Thickness(2, 2, 8, 1);
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
                _albumArtThumb.Margin = new Thickness(4, 2, 8, 1);
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

                // Smooth cubic ease-out emergence from behind the center notch to the left
                double musicEase = 1.0 - Math.Pow(1.0 - musicFactor, 3);
                double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
                double gap = 9.0;
                double satH = 34.0;
                double satW = 58.0;
                double satTop = isIsland ? (islandGap + 2.0) : (isLine ? 3.0 : 4.5);

                _musicSatelliteIsland.Width = satW;
                _musicSatelliteIsland.Height = satH;
                _musicSatelliteIsland.CornerRadius = new CornerRadius(satH / 2.0);
                _musicSatelliteIsland.Margin = new Thickness(0, satTop, 0, 0);

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
                double leftW = satW * scale;
                double startLeftX = -(baseW / 2.0 - leftW / 2.0 - 8.0);
                double fullLeftX = -(baseW / 2.0 + (gap * scale) + leftW / 2.0);
                _musicSatelliteTranslate.X = startLeftX + (fullLeftX - startLeftX) * musicEase;

                double satScale = (0.85 + 0.15 * musicEase) * scale;
                _musicSatelliteScale.ScaleX = satScale;
                _musicSatelliteScale.ScaleY = satScale;

                if (!_isTucked)
                {
                    double satOpacity = Math.Clamp(musicEase * 1.15, 0.0, 1.0);
                    _musicSatelliteIsland.Opacity = satOpacity;
                    _musicSatelliteIsland.IsHitTestVisible = musicFactor > 0.6;
                }
            }

            // Update Right Detached Circular Volume / Brightness HUD Capsule (when Music is playing)
            double rightHudFactor = _rightHudProgress * Math.Clamp(1.0 - t * 3.0, 0.0, 1.0);
            if (rightHudFactor <= 0.005 || _isTuckCompletedHidden || _isAutoHiddenByVisibility)
            {
                _rightHudSatelliteIsland.Visibility = Visibility.Collapsed;
            }
            else
            {
                _rightHudSatelliteIsland.Visibility = Visibility.Visible;

                double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
                double satH = 36.0;
                double satTop = isIsland ? (islandGap + 1.0) : (isLine ? 3.0 : 4.5);

                _rightHudSatelliteIsland.Width = satH;
                _rightHudSatelliteIsland.Height = satH;
                _rightHudSatelliteIsland.CornerRadius = new CornerRadius(satH / 2.0);
                _rightHudSatelliteIsland.Margin = new Thickness(0, satTop, 0, 0);

                double hudEase = 1.0 - Math.Pow(1.0 - rightHudFactor, 3);
                double gap = 8.0 * scale;
                double hudW = satH * scale;

                double startRightX = +(baseW / 2.0 - hudW / 2.0 - 8.0);
                double fullRightX = +(baseW / 2.0 + gap + hudW / 2.0);
                _rightHudTranslate.X = startRightX + (fullRightX - startRightX) * hudEase;

                double hudIslandScale = (0.78 + 0.22 * hudEase) * scale;
                _rightHudScale.ScaleX = hudIslandScale;
                _rightHudScale.ScaleY = hudIslandScale;

                if (!_isTucked)
                {
                    double hudOpacity = Math.Clamp(hudEase * 1.15, 0.0, 1.0);
                    _rightHudSatelliteIsland.Opacity = hudOpacity;
                    _rightHudSatelliteIsland.IsHitTestVisible = rightHudFactor > 0.6;
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
                double scale = Math.Clamp(AppPreferences.Instance.NotchScalePercent / 100.0, 0.75, 1.30);
                double satH = 36.0;
                double satTop = isIsland ? (islandGap + 1.0) : (isLine ? 3.0 : 4.5);

                _leftTimerIsland.Width = satH;
                _leftTimerIsland.Height = satH;
                _leftTimerIsland.CornerRadius = new CornerRadius(satH / 2.0);
                _leftTimerIsland.Margin = new Thickness(0, satTop, 0, 0);

                _rightTimerIsland.Height = satH;
                _rightTimerIsland.CornerRadius = new CornerRadius(satH / 2.0);
                _rightTimerIsland.Margin = new Thickness(0, satTop, 0, 0);

                // If music satellite occupies the left slot during notification, hide left timer ring.
                // If Volume/Brightness HUD occupies the right slot during music, temporarily yield right timer digits without overlap.
                _leftTimerIsland.Visibility = musicFactor > 0.005 ? Visibility.Collapsed : Visibility.Visible;
                _rightTimerIsland.Visibility = rightHudFactor > 0.12 ? Visibility.Collapsed : Visibility.Visible;

                // Smooth cubic ease-out emergence from behind the center music notch
                double splitEase = 1.0 - Math.Pow(1.0 - splitFactor, 3);
                double gap = 8.0 * scale;
                double leftW = satH * scale;
                double rightW = (_rightTimerIsland.Width > 20.0 ? _rightTimerIsland.Width : 68.0) * scale;

                double startLeftX = -(baseW / 2.0 - leftW / 2.0 - 8.0);
                double fullLeftX = -(baseW / 2.0 + gap + leftW / 2.0);

                double startRightX = +(baseW / 2.0 - rightW / 2.0 - 8.0);
                double fullRightX = +(baseW / 2.0 + gap + rightW / 2.0);

                _leftIslandTranslate.X = startLeftX + (fullLeftX - startLeftX) * splitEase;
                _rightIslandTranslate.X = startRightX + (fullRightX - startRightX) * splitEase;

                double islandScale = (0.78 + 0.22 * splitEase) * scale;
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
            if (_rightHudSatelliteIsland != null)
            {
                _rightHudSatelliteIsland.Background = CapsuleTheme.Brush(p.Bg);
                _rightHudSatelliteIsland.BorderBrush = outlineBrush;
                _rightHudSatelliteIsland.BorderThickness = outlineThickness;
                UpdateHudStylingAndIcons();
            }

            // Compact & Wide Media player reactive styling
            if (_mediaTrackRun != null) _mediaTrackRun.Foreground = CapsuleTheme.Brush(p.Text);
            if (_mediaSepRun != null) _mediaSepRun.Foreground = CapsuleTheme.Brush(p.Text3);
            if (_mediaArtistRun != null) _mediaArtistRun.Foreground = CapsuleTheme.Brush(p.Text2);
            if (_wideMediaTrackText != null) _wideMediaTrackText.Foreground = CapsuleTheme.Brush(p.Text);
            if (_wideRemainingText != null) _wideRemainingText.Foreground = CapsuleTheme.Brush(p.Text2);
            if (_wideMediaArtistText != null) _wideMediaArtistText.Foreground = CapsuleTheme.Brush(p.Text3);
            if (_wideProgressTrack != null)
            {
                _wideProgressTrack.Background = CapsuleTheme.Brush(p.IsLight ? Color.FromArgb(35, 0, 0, 0) : Color.FromArgb(45, 255, 255, 255));
            }
            if (_wideProgressThumb != null)
            {
                _wideProgressThumb.Fill = CapsuleTheme.Brush(p.IsLight ? p.Accent : Colors.White);
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
                _edgeLight.Stop();
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
                string outlineW = AppPreferences.Instance.SurfaceOutlineWidth ?? "";
                if (outlineW.Contains("2")) strokeWidth = 2.0;
                else if (outlineW.Contains("3")) strokeWidth = 3.0;
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
            if (_collapsedBatteryText != null) _collapsedBatteryText.Foreground = CapsuleTheme.Brush(p.Text);
            _bannerTitle.Foreground = CapsuleTheme.Brush(p.Text);
            _bannerSubtitle.Foreground = CapsuleTheme.Brush(p.Text2);
            _bannerAppName.Foreground = CapsuleTheme.Brush(p.Text2);
            _bannerIcon.Foreground = CapsuleTheme.Brush(p.Text);
            _bannerAppIconBorder.Background = CapsuleTheme.Brush(p.Surface2);
            if (_bannerAnimateIcon != null && _bannerAnimateIcon.IconType != AnimateIconType.Bluetooth)
            {
                _bannerAnimateIcon.StrokeBrush = CapsuleTheme.Brush(p.Text);
            }
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

        private DateTime _lastToggleWindowTime = DateTime.MinValue;

        public void ToggleFromExternal()
        {
            if ((DateTime.UtcNow - _lastToggleWindowTime).TotalMilliseconds < 350) return;
            _lastToggleWindowTime = DateTime.UtcNow;

            if (_isOpenedByHotkey || _machine.IsOpenOrOpening || _morphProgress > 0.1)
            {
                _isOpenedByHotkey = false;
                _machine.RequestClose("hotkey-toggle");
            }
            else
            {
                _isOpenedByHotkey = true;
                if (_isTucked) SetTucked(false);
                if (_isAutoHiddenByVisibility) SetAutoHidden(false);
                _machine.RequestOpen("hotkey-toggle");
            }
        }
    }
}
