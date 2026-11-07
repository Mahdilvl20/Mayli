using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools
{
    public interface IBuddyTool
    {
        string Id { get; }
        string Title { get; }
        string IconGlyph { get; }
        FrameworkElement BuildView();
        void OnActivated();
        void InvalidateView();
    }

    public static class ToolRegistry
    {
        private static readonly List<IBuddyTool> _tools = new();
        public static IReadOnlyList<IBuddyTool> All => _tools;
        public static Action<string> OnToolNavigationRequested;

        static ToolRegistry()
        {
            CapsuleTheme.ThemeChanged += InvalidateAllViews;
        }

        public static void InvalidateAllViews()
        {
            foreach (var tool in _tools)
            {
                try { tool.InvalidateView(); } catch { }
            }
        }

        public static void RegisterDefaults()
        {
            if (_tools.Count > 0) return;

            // Exactly the 12 tools created and perfected:
            _tools.Add(new HomeTool());          // 1. Home
            _tools.Add(new SystemTool());        // 2. System
            _tools.Add(new ClipboardTool());     // 3. Clipboard
            _tools.Add(new NotesTool());         // 4. Notes
            _tools.Add(new TimerClockTool());    // 5. Timers
            _tools.Add(new FileShelfTool());     // 6. File Shelf
            _tools.Add(new WeatherTool());       // 7. Weather
            _tools.Add(new ClockTool());         // 8. Clock
            _tools.Add(new CalendarTool());      // 9. Calendar
            _tools.Add(new QuickToolsTool());    // 10. Units
            _tools.Add(new LinksTool());         // 10. Links
            _tools.Add(new MessageBannerTool()); // 11. Message
            _tools.Add(new ScreenTimeTool());    // 12. Screen Time
        }

        public static IBuddyTool GetById(string id) =>
            _tools.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase)) ?? _tools.FirstOrDefault();
    }

    /// <summary>
    /// HomeTool: The exact 6-Card Dashboard synchronized with real live services (Screen Time, System, Weather, Timers, Clipboard, Notes).
    /// </summary>
    public sealed class HomeTool : IBuddyTool
    {
        public string Id => "home";
        public string Title => LocalizationService.T("tool.home", "Home");
        public string IconGlyph => "⌂";

        // Card 1: Screen Time
        private TextBlock _stTotalText;
        private TextBlock _stAppText;
        private Image _stAppIcon;
        private Border _stAppIconBox;
        private StackPanel _stBarsPanel;

        // Card 2: System
        private CircularGauge _cpuGauge;
        private CircularGauge _ramGauge;
        private CircularGauge _diskGauge;

        // Card 3: Weather
        private TextBlock _wTempText;
        private TextBlock _wCityText;
        private TextBlock _wRangeText;
        private TextBlock _wIcon;

        // Card 4: Focus
        private TextBlock _focusTimeText;
        private TextBlock _focusStatusText;
        private Border _focusToggleBtn;
        private TextBlock _focusToggleIcon;

        // Card 5: Clipboard
        private StackPanel _clipRowsStack;

        // Card 6: Now Playing / Music Controller
        private Image _musicCardBgImage;
        private Border _musicCardGlassOverlay;
        private Image _musicCardThumbImage;
        private TextBlock _musicCardFallbackGlyph;
        private Border _musicCardThumbBorder;
        private TextBlock _musicCardTitleText;
        private TextBlock _musicCardArtistText;
        private System.Windows.Shapes.Path _musicCardPlayIconPath;
        private Border _musicCardPlayBtn;
        private Border _musicCardProgressBarFill;
        private Border _musicCardProgressBarTrack;
        private MiniEqualizerControl _musicCardMiniEq;

        private DispatcherTimer _tickTimer;

        public FrameworkElement BuildView()
        {
            var p = CapsuleTheme.Current;

            var rootGrid = new Grid
            {
                Margin = new Thickness(2, 0, 2, 2)
            };

            // 2 Rows, 3 Columns
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            rootGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // -------------------------------------------------------------
            // CARD 1: SCREEN TIME (Row 0, Col 0) - Synchronized with ScreenTimeService
            // -------------------------------------------------------------
            var screenTimeCard = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            screenTimeCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("screentime");

            var stDock = new DockPanel();

            var stHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            stHeader.Children.Add(new TextBlock
            {
                Text = "☵ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            stHeader.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("home.screentime", "SCREEN TIME"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(stHeader, Dock.Top);
            stDock.Children.Add(stHeader);

            _stBarsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0)
            };
            DockPanel.SetDock(_stBarsPanel, Dock.Bottom);
            stDock.Children.Add(_stBarsPanel);

            var stCenter = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 0, 4) };
            _stTotalText = new TextBlock
            {
                Text = WindowsBuddy.Tools.ScreenTime.ScreenTimeService.Instance.TodayTotalFormatted,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            stCenter.Children.Add(_stTotalText);

            var stAppLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            _stAppIconBox = new Border
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _stAppIcon = new Image { Width = 14, Height = 14 };
            _stAppIconBox.Child = _stAppIcon;
            stAppLine.Children.Add(_stAppIconBox);

            _stAppText = new TextBlock
            {
                Text = LocalizationService.T("home.no_apps", "No apps today"),
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 170
            };
            stAppLine.Children.Add(_stAppText);
            stCenter.Children.Add(stAppLine);
            stDock.Children.Add(stCenter);

            screenTimeCard.Child = stDock;

            // -------------------------------------------------------------
            // CARD 2: SYSTEM (Row 0, Col 1) - Synchronized with SystemMonitorService
            // -------------------------------------------------------------
            var systemCard = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            systemCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("system");

            var sysDock = new DockPanel();

            var sysHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            sysHeader.Children.Add(new TextBlock
            {
                Text = "⚙ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            sysHeader.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("home.system", "SYSTEM"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(sysHeader, Dock.Top);
            sysDock.Children.Add(sysHeader);

            var gaugesRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _cpuGauge = new CircularGauge("CPU", 1, radius: 21, thickness: 4.5);
            _ramGauge = new CircularGauge("RAM", 83, radius: 21, thickness: 5.5);
            _diskGauge = new CircularGauge("DISK", 0, radius: 21, thickness: 4.5);
            gaugesRow.Children.Add(_cpuGauge);
            gaugesRow.Children.Add(_ramGauge);
            gaugesRow.Children.Add(_diskGauge);
            sysDock.Children.Add(gaugesRow);

            systemCard.Child = sysDock;

            // -------------------------------------------------------------
            // CARD 3: WEATHER (Row 0, Col 2) - Synchronized with WeatherService
            // -------------------------------------------------------------
            var weatherCard = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            weatherCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("weather");

            var wDock = new DockPanel();
            var wHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            wHeader.Children.Add(new TextBlock
            {
                Text = "⛅ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            wHeader.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("home.weather", "WEATHER"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(wHeader, Dock.Top);
            wDock.Children.Add(wHeader);

            var wMain = new Grid { VerticalAlignment = VerticalAlignment.Center };
            wMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            wMain.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var wLeft = new StackPanel();
            _wTempText = new TextBlock
            {
                Text = "18°",
                FontSize = 26,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            wLeft.Children.Add(_wTempText);

            _wCityText = new TextBlock
            {
                Text = "Overcast · Isfahan",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 1),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 150
            };
            wLeft.Children.Add(_wCityText);

            _wRangeText = new TextBlock
            {
                Text = "H 25°  L 10°",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text3)
            };
            wLeft.Children.Add(_wRangeText);
            Grid.SetColumn(wLeft, 0);
            wMain.Children.Add(wLeft);

            _wIcon = new TextBlock
            {
                Text = "☁",
                FontSize = 32,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            Grid.SetColumn(_wIcon, 1);
            wMain.Children.Add(_wIcon);

            wDock.Children.Add(wMain);
            weatherCard.Child = wDock;

            // -------------------------------------------------------------
            // CARD 4: FOCUS (Row 1, Col 0) - Synchronized with Timers
            // -------------------------------------------------------------
            var focusCard = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            focusCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("focus");

            var fDock = new DockPanel();

            var fHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            fHeader.Children.Add(new TextBlock
            {
                Text = "⏱ ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            fHeader.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("home.focus", "FOCUS"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(fHeader, Dock.Top);
            fDock.Children.Add(fHeader);

            var fContent = new Grid { VerticalAlignment = VerticalAlignment.Center };
            fContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var fLeft = new StackPanel();
            _focusTimeText = new TextBlock
            {
                Text = "25:00",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            };
            _focusStatusText = new TextBlock
            {
                Text = "Focusing",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 0)
            };
            fLeft.Children.Add(_focusTimeText);
            fLeft.Children.Add(_focusStatusText);
            Grid.SetColumn(fLeft, 0);
            fContent.Children.Add(fLeft);

            _focusToggleBtn = new Border
            {
                Width = 36,
                Height = 36,
                CornerRadius = new CornerRadius(18),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            _focusToggleIcon = new TextBlock
            {
                Text = "▶",
                FontSize = 13,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _focusToggleBtn.Child = _focusToggleIcon;
            _focusToggleBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                TimerClockTool.ToggleRunning();
                UpdateFocusState();
            };
            Grid.SetColumn(_focusToggleBtn, 1);
            fContent.Children.Add(_focusToggleBtn);

            fDock.Children.Add(fContent);
            focusCard.Child = fDock;

            // -------------------------------------------------------------
            // CARD 5: CLIPBOARD (Row 1, Col 1) - Synchronized with ClipboardService
            // -------------------------------------------------------------
            var clipCard = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            clipCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("clipboard");

            var cDock = new DockPanel();

            var cHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            cHeader.Children.Add(new TextBlock
            {
                Text = "📄 ",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            cHeader.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("home.clipboard", "CLIPBOARD"),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(cHeader, Dock.Top);
            cDock.Children.Add(cHeader);

            _clipRowsStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            cDock.Children.Add(_clipRowsStack);

            clipCard.Child = cDock;

            // -------------------------------------------------------------
            // CARD 6: NOW PLAYING / MUSIC CONTROLLER (Frosted Glass & Controls)
            // -------------------------------------------------------------
            var nowPlayingCard = new BuddyCard
            {
                Margin = new Thickness(3),
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                ClipToBounds = true
            };
            nowPlayingCard.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("audio");

            var musicCardRootGrid = new Grid();

            // Layer 0: Frosted Glass Background with Album Artwork & Subtle Glass Gradient
            var bgClipBorder = new Border
            {
                CornerRadius = new CornerRadius(14),
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _musicCardBgImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Opacity = 0.44,
                Effect = new BlurEffect { Radius = 24, RenderingBias = RenderingBias.Performance },
                Visibility = Visibility.Collapsed
            };

            _musicCardGlassOverlay = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(170, 18, 20, 28), 0.0),
                        new GradientStop(Color.FromArgb(220, 12, 14, 20), 1.0)
                    },
                    new Point(0, 0),
                    new Point(0, 1)
                ),
                BorderThickness = new Thickness(0)
            };

            var bgGrid = new Grid();
            bgGrid.Children.Add(_musicCardBgImage);
            bgGrid.Children.Add(_musicCardGlassOverlay);
            bgClipBorder.Child = bgGrid;
            musicCardRootGrid.Children.Add(bgClipBorder);

            // Clip the card and all its frosted glass layers to exact 14px rounded corners (prevents rectangular corner bleed)
            void UpdateNowPlayingCardClip()
            {
                double w = nowPlayingCard.ActualWidth;
                double h = nowPlayingCard.ActualHeight;
                if (w > 2 && h > 2)
                {
                    var clipGeo = new RectangleGeometry(new Rect(0, 0, w, h), 14, 14);
                    clipGeo.Freeze();
                    nowPlayingCard.Clip = clipGeo;
                    bgClipBorder.Clip = clipGeo;
                    musicCardRootGrid.Clip = clipGeo;
                }
            }
            nowPlayingCard.SizeChanged += (_, _) => UpdateNowPlayingCardClip();
            nowPlayingCard.Loaded += (_, _) => UpdateNowPlayingCardClip();

            // Layer 1: Foreground Content
            var musicContentDock = new DockPanel
            {
                Margin = new Thickness(11, 8, 11, 8)
            };

            // Top Header: Icon + Title + Mini Equalizer
            var mHeader = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(mHeader, Dock.Top);

            _musicCardMiniEq = new MiniEqualizerControl
            {
                Height = 12,
                Margin = new Thickness(0, 0, 1, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_musicCardMiniEq, Dock.Right);
            mHeader.Children.Add(_musicCardMiniEq);

            var mHeaderLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            mHeaderLeft.Children.Add(new TextBlock
            {
                Text = "🎵 ",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center
            });
            mHeaderLeft.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("home.nowplaying", "NOW PLAYING"),
                FontSize = 9.0,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            mHeader.Children.Add(mHeaderLeft);
            musicContentDock.Children.Add(mHeader);

            // Bottom Section: Progress Bar + Transport Controls
            var mBottom = new StackPanel { VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 2, 0, 0) };
            DockPanel.SetDock(mBottom, Dock.Bottom);

            _musicCardProgressBarTrack = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = CapsuleTheme.Brush(Color.FromArgb(35, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 2, 0, 4)
            };
            _musicCardProgressBarFill = new Border
            {
                Height = 3,
                CornerRadius = new CornerRadius(1.5),
                Background = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            _musicCardProgressBarTrack.Child = _musicCardProgressBarFill;
            _musicCardProgressBarTrack.SizeChanged += (_, _) => RefreshMusicProgressBar();
            mBottom.Children.Add(_musicCardProgressBarTrack);

            // Transport Controls: Sleek Glassmorphic Pill Dock with Vector Icons
            var mControlsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var prevIconCanvas = new Canvas { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            prevIconCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 2.0,2.5 L 3.3,2.5 L 3.3,9.5 L 2.0,9.5 Z M 9.8,2.2 L 4.0,6.0 L 9.8,9.8 Z"),
                Fill = Brushes.White,
                Opacity = 0.90
            });
            var prevBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0),
                Child = prevIconCanvas
            };
            prevBtn.MouseEnter += (_, _) => prevBtn.Background = CapsuleTheme.Brush(Color.FromArgb(38, 255, 255, 255));
            prevBtn.MouseLeave += (_, _) => prevBtn.Background = Brushes.Transparent;
            prevBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack)) return;
                MediaService.Instance.PreviousTrack();
            };
            mControlsRow.Children.Add(prevBtn);

            var playIconCanvas = new Canvas { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _musicCardPlayIconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 3.8,2.0 L 10.2,6.0 L 3.8,10.0 Z"),
                Fill = new SolidColorBrush(Color.FromRgb(16, 18, 26))
            };
            playIconCanvas.Children.Add(_musicCardPlayIconPath);

            _musicCardPlayBtn = new Border
            {
                Width = 26,
                Height = 26,
                CornerRadius = new CornerRadius(13),
                Background = Brushes.White,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 6, 0),
                Child = playIconCanvas
            };
            _musicCardPlayBtn.MouseEnter += (_, _) => _musicCardPlayBtn.Background = new SolidColorBrush(Color.FromRgb(230, 234, 242));
            _musicCardPlayBtn.MouseLeave += (_, _) => _musicCardPlayBtn.Background = Brushes.White;
            _musicCardPlayBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack)) return;
                MediaService.Instance.TogglePlayPause();
                RefreshMusicCard();
            };
            mControlsRow.Children.Add(_musicCardPlayBtn);

            var nextIconCanvas = new Canvas { Width = 12, Height = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            nextIconCanvas.Children.Add(new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M 8.7,2.5 L 10.0,2.5 L 10.0,9.5 L 8.7,9.5 Z M 2.2,2.2 L 8.0,6.0 L 2.2,9.8 Z"),
                Fill = Brushes.White,
                Opacity = 0.90
            });
            var nextBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = nextIconCanvas
            };
            nextBtn.MouseEnter += (_, _) => nextBtn.Background = CapsuleTheme.Brush(Color.FromArgb(38, 255, 255, 255));
            nextBtn.MouseLeave += (_, _) => nextBtn.Background = Brushes.Transparent;
            nextBtn.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;
                if (string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack)) return;
                MediaService.Instance.NextTrack();
            };
            mControlsRow.Children.Add(nextBtn);

            var mControlsPill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(15),
                Padding = new Thickness(5, 2, 5, 2),
                Background = CapsuleTheme.Brush(Color.FromArgb(24, 255, 255, 255)),
                BorderBrush = CapsuleTheme.Brush(Color.FromArgb(32, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Child = mControlsRow
            };
            mBottom.Children.Add(mControlsPill);
            musicContentDock.Children.Add(mBottom);

            // Center Content: Album Thumbnail + Track Title & Artist
            var mCenterDock = new DockPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 3)
            };

            _musicCardThumbBorder = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(7),
                ClipToBounds = true,
                Background = CapsuleTheme.Brush(Color.FromArgb(40, 255, 255, 255)),
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_musicCardThumbBorder, Dock.Left);

            var thumbGrid = new Grid();
            _musicCardThumbImage = new Image
            {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _musicCardFallbackGlyph = new TextBlock
            {
                Text = "🎵",
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = CapsuleTheme.Brush(p.Text3)
            };
            thumbGrid.Children.Add(_musicCardThumbImage);
            thumbGrid.Children.Add(_musicCardFallbackGlyph);
            _musicCardThumbBorder.Child = thumbGrid;
            mCenterDock.Children.Add(_musicCardThumbBorder);

            var mInfoStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            _musicCardTitleText = new TextBlock
            {
                Text = LocalizationService.T("home.nomedia", "No Media Playing"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 145
            };
            _musicCardArtistText = new TextBlock
            {
                Text = "Windows Audio",
                FontSize = 10,
                Foreground = CapsuleTheme.Brush(p.Text2),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 1, 0, 0),
                MaxWidth = 145
            };
            mInfoStack.Children.Add(_musicCardTitleText);
            mInfoStack.Children.Add(_musicCardArtistText);
            mCenterDock.Children.Add(mInfoStack);

            musicContentDock.Children.Add(mCenterDock);
            musicCardRootGrid.Children.Add(musicContentDock);
            nowPlayingCard.Child = musicCardRootGrid;

            // Factory to get or create widget card on demand
            BuddyCard CreateCard(string wId) => wId.ToLowerInvariant() switch
            {
                "screentime" => screenTimeCard,
                "system" => systemCard,
                "weather" => weatherCard,
                "focus" => focusCard,
                "clipboard" => clipCard,
                "nowplaying" or "notes" or "music" or "audio" => nowPlayingCard,
                "clock" => BuildCompactClockCard(p),
                "calendar" => BuildCompactCalendarCard(p),
                // "nowplaying" mapped above
                "links" => BuildCompactLinksCard(p),
                "fileshelf" => BuildCompactFileShelfCard(p),
                "units" => BuildCompactUnitsCard(p),
                "fileconverter" => BuildCompactConverterCard(p),
                "message" => BuildCompactMessageCard(p),
                "water" => BuildCompactWaterCard(p),
                "stopwatch" => BuildCompactStopwatchCard(p),
                "analytics" => BuildCompactAnalyticsCard(p),
                "revenue" => BuildCompactRevenueCard(p),
                "aicoding" => BuildCompactAICodingCard(p),
                _ => null
            };

            var activeWidgets = (AppPreferences.Instance.Raw.HomeWidgets ?? new List<string> { "screentime", "system", "weather", "focus", "clipboard", "nowplaying" })
                .Select(w => string.Equals(w, "notes", StringComparison.OrdinalIgnoreCase) ? "nowplaying" : w)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .ToList();
            int slot = 0;
            foreach (var wId in activeWidgets)
            {
                var c = CreateCard(wId);
                if (c == null) continue;

                if (rootGrid.Children.Contains(c))
                    continue;

                try
                {
                    if (c.Parent is Panel pnl)
                        pnl.Children.Remove(c);
                    else if (c.Parent is ContentControl cc)
                        cc.Content = null;
                    else if (c.Parent is Decorator dec)
                        dec.Child = null;

                    var parentVisual = VisualTreeHelper.GetParent(c);
                    if (parentVisual is Panel vpnl)
                        vpnl.Children.Remove(c);
                    else if (parentVisual is Decorator vdec)
                        vdec.Child = null;
                    else if (parentVisual is ContentControl vcc)
                        vcc.Content = null;
                }
                catch { }

                if (!rootGrid.Children.Contains(c))
                {
                    int r = slot / 3;
                    int col = slot % 3;
                    Grid.SetRow(c, r);
                    Grid.SetColumn(c, col);
                    try
                    {
                        rootGrid.Children.Add(c);
                        slot++;
                    }
                    catch { }
                }
            }

            // Timer update for all synchronized components
            if (_tickTimer != null)
            {
                _tickTimer.Stop();
            }
            else
            {
                _tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _tickTimer.Tick += (_, _) =>
                {
                    if (_activeRootGrid != null && _activeRootGrid.IsVisible && _activeRootGrid.IsLoaded)
                    {
                        RefreshAllCards();
                    }
                };
            }

            _activeRootGrid = rootGrid;
            rootGrid.Loaded += (_, _) => _tickTimer?.Start();
            rootGrid.Unloaded += (_, _) => _tickTimer?.Stop();
            rootGrid.IsVisibleChanged += (_, _) =>
            {
                if (rootGrid.IsVisible && rootGrid.IsLoaded) _tickTimer?.Start();
                else _tickTimer?.Stop();
            };
            _tickTimer.Start();

            RealtimeNotchActionService.Instance.MediaSessionChanged += _ =>
            {
                try { rootGrid.Dispatcher.BeginInvoke((Action)RefreshMusicCard); } catch { }
            };

            MediaService.Instance.StateChanged += () =>
            {
                try { rootGrid.Dispatcher.BeginInvoke((Action)RefreshMusicCard); } catch { }
            };

            _lastClipSignature = null;
            RefreshAllCards();
            return rootGrid;
        }

        private Grid _activeRootGrid;
        private string _lastClipSignature;

        private void RefreshAllCards()
        {
            var p = CapsuleTheme.Current;

            // 1. Refresh System
            var t = SystemMonitorService.Current;
            _cpuGauge?.SetValue(Math.Max(1, t.CpuUsagePercent));
            _ramGauge?.SetValue(t.MemoryUsagePercent > 0 ? t.MemoryUsagePercent : 59);
            _diskGauge?.SetValue(t.DiskUsagePercent);

            // 2. Refresh Screen Time
            var stSvc = WindowsBuddy.Tools.ScreenTime.ScreenTimeService.Instance;
            if (_stTotalText != null)
            {
                _stTotalText.Text = stSvc.TodayTotalFormatted;

                var topApp = stSvc.GetTopAppsToday().FirstOrDefault();
                if (topApp != null)
                {
                    _stAppText.Text = $"{topApp.Name} · {topApp.DurationFormatted}";
                    if (!ReferenceEquals(_stAppIcon.Source, topApp.IconSource))
                    {
                        _stAppIcon.Source = topApp.IconSource;
                    }
                }

                // Render 7-day mini bars (reuse existing Borders when count matches)
                var days = stSvc.GetRolling7Days();
                if (_stBarsPanel.Children.Count != days.Count)
                {
                    _stBarsPanel.Children.Clear();
                    foreach (var d in days)
                    {
                        double h = d.IsToday ? 16 : (d.TotalSeconds > 0 ? Math.Max(4, d.HeightFraction * 14) : 2.5);
                        var bar = new Border
                        {
                            Width = 10,
                            Height = h,
                            CornerRadius = new CornerRadius(1.5),
                            Background = d.IsToday ? Brushes.White : CapsuleTheme.Brush(d.TotalSeconds > 0 ? p.Accent : p.Border),
                            Margin = new Thickness(2, 0, 2, 0),
                            VerticalAlignment = VerticalAlignment.Bottom
                        };
                        _stBarsPanel.Children.Add(bar);
                    }
                }
                else
                {
                    for (int i = 0; i < days.Count; i++)
                    {
                        var d = days[i];
                        if (_stBarsPanel.Children[i] is Border bar)
                        {
                            bar.Height = d.IsToday ? 16 : (d.TotalSeconds > 0 ? Math.Max(4, d.HeightFraction * 14) : 2.5);
                            bar.Background = d.IsToday ? Brushes.White : CapsuleTheme.Brush(d.TotalSeconds > 0 ? p.Accent : p.Border);
                        }
                    }
                }
            }

            // 3. Refresh Weather
            var w = WindowsBuddy.Tools.Weather.WeatherService.Instance.Current;
            if (w != null && _wTempText != null)
            {
                bool useF = WindowsBuddy.Tools.Weather.WeatherService.Instance.UseFahrenheit;
                int tempDisplay = (int)Math.Round(useF ? (w.Temperature * 9.0 / 5.0 + 32) : w.Temperature);
                int hiDisplay = (int)Math.Round(useF ? (w.HighTemp * 9.0 / 5.0 + 32) : w.HighTemp);
                int loDisplay = (int)Math.Round(useF ? (w.LowTemp * 9.0 / 5.0 + 32) : w.LowTemp);

                _wTempText.Text = $"{tempDisplay}°";
                _wCityText.Text = $"{w.Condition} · {w.City}";
                _wRangeText.Text = $"H {hiDisplay}°  L {loDisplay}°";
                _wIcon.Text = w.WeatherCode switch
                {
                    0 => "☀",
                    1 or 2 => "⛅",
                    3 => "☁",
                    45 or 48 => "🌫",
                    >= 51 and <= 67 => "🌧",
                    >= 71 and <= 77 => "❄",
                    >= 80 and <= 82 => "🌧",
                    >= 95 and <= 99 => "⛈",
                    _ => "☁"
                };
            }

            // 4. Refresh Focus
            UpdateFocusState();

            // 5. Refresh Clipboard (only rebuild when items or relative timestamps change)
            if (_clipRowsStack != null)
            {
                var items = WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.Items.Take(2).ToList();
                string sig = items.Count == 0
                    ? "empty"
                    : string.Join("|", items.Select(it => $"{it.GetDisplayTitle()}@{it.GetTimestampFormatted()}"));

                if (!string.Equals(sig, _lastClipSignature, StringComparison.Ordinal))
                {
                    _lastClipSignature = sig;
                    _clipRowsStack.Children.Clear();
                    if (items.Count == 0)
                    {
                        _clipRowsStack.Children.Add(CreateClipRow("Nothing copied yet", "Just now"));
                    }
                    else
                    {
                        foreach (var it in items)
                        {
                            string snippet = it.GetDisplayTitle();
                            if (string.IsNullOrWhiteSpace(snippet))
                            {
                                snippet = it.Kind == WindowsBuddy.Tools.Clipboard.ClipKind.Image ? "Image" : "Item";
                            }
                            _clipRowsStack.Children.Add(CreateClipRow(snippet, it.GetTimestampFormatted()));
                        }
                    }
                }
            }

            // 6. Refresh Now Playing / Music Controller
            RefreshMusicCard();
        }

        private void RefreshMusicCard()
        {
            if (_musicCardTitleText == null) return;

            var p = CapsuleTheme.Current;
            var rsvc = RealtimeNotchActionService.Instance;
            var msvc = MediaService.Instance;

            bool hasMedia = !string.IsNullOrWhiteSpace(rsvc.CurrentTrack);
            bool isPlaying = hasMedia && (rsvc.IsMusicPlaying || msvc.State.IsPlaying);
            string track = hasMedia ? rsvc.CurrentTrack : "";
            string artist = hasMedia ? rsvc.CurrentArtist : "";
            var artwork = hasMedia ? rsvc.CurrentArtwork : null;
            var beatColor = rsvc.CurrentBeatColor;

            _musicCardTitleText.Text = !hasMedia
                ? LocalizationService.T("home.nomedia", "No Media Playing")
                : track;

            _musicCardArtistText.Text = !hasMedia
                ? "Open a music player"
                : (string.IsNullOrWhiteSpace(artist) ? "Windows Audio" : artist);

            if (_musicCardPlayIconPath != null)
            {
                _musicCardPlayIconPath.Data = isPlaying
                    ? Geometry.Parse("M 2.8,2.2 L 4.8,2.2 L 4.8,9.8 L 2.8,9.8 Z M 7.2,2.2 L 9.2,2.2 L 9.2,9.8 L 7.2,9.8 Z")
                    : Geometry.Parse("M 3.8,2.0 L 10.2,6.0 L 3.8,10.0 Z");
            }

            if (_musicCardPlayBtn != null)
            {
                _musicCardPlayBtn.Opacity = hasMedia ? 1.0 : 0.45;
                _musicCardPlayBtn.Cursor = hasMedia ? Cursors.Hand : Cursors.Arrow;
            }

            // Cover Art - Glass Background & Thumbnail
            if (artwork != null)
            {
                if (!ReferenceEquals(_musicCardThumbImage.Source, artwork))
                {
                    _musicCardThumbImage.Source = artwork;
                    _musicCardBgImage.Source = artwork;
                }
                _musicCardThumbImage.Visibility = Visibility.Visible;
                _musicCardFallbackGlyph.Visibility = Visibility.Collapsed;
                _musicCardBgImage.Visibility = Visibility.Visible;
            }
            else
            {
                _musicCardThumbImage.Source = null;
                _musicCardBgImage.Source = null;
                _musicCardThumbImage.Visibility = Visibility.Collapsed;
                _musicCardFallbackGlyph.Visibility = Visibility.Visible;
                _musicCardBgImage.Visibility = Visibility.Collapsed;
            }

            // Mini Equalizer Visualizer
            if (_musicCardMiniEq != null)
            {
                _musicCardMiniEq.IsPlaying = isPlaying;
                if (isPlaying)
                {
                    _musicCardMiniEq.SetBeatColor(beatColor != default && beatColor != Colors.Transparent ? beatColor : p.Accent);
                    try
                    {
                        float[] bands = new float[AudioSpectrumService.Bands];
                        AudioSpectrumService.Instance.Read(bands, out bool live);
                        float[] mini4 = new float[4];
                        AudioSpectrumService.ComputeMiniBars(bands, mini4, DateTime.UtcNow.TimeOfDay.TotalSeconds * 2.0);
                        _musicCardMiniEq.SetLevels(mini4, true);
                    }
                    catch
                    {
                        _musicCardMiniEq.SetLevels(new float[] { 0.7f, 0.4f, 0.8f, 0.5f }, true);
                    }
                }
                else
                {
                    _musicCardMiniEq.DecayLevels();
                }
            }

            RefreshMusicProgressBar();
        }

        private void RefreshMusicProgressBar()
        {
            if (_musicCardProgressBarFill == null || _musicCardProgressBarTrack == null) return;
            double trackWidth = _musicCardProgressBarTrack.ActualWidth;
            if (trackWidth <= 0) trackWidth = 140;

            bool hasMedia = !string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack);
            double dur = MediaService.Instance.State.DurationSeconds;
            double pos = MediaService.Instance.State.PositionSeconds;
            if (hasMedia && dur > 0 && pos >= 0)
            {
                double fraction = Math.Clamp(pos / dur, 0.0, 1.0);
                _musicCardProgressBarFill.Width = Math.Max(0, fraction * trackWidth);
            }
            else
            {
                _musicCardProgressBarFill.Width = 0;
            }
        }

        private void UpdateFocusState()
        {
            if (_focusTimeText == null) return;
            _focusTimeText.Text = TimerClockTool.FormatTime(TimerClockTool.RemainingSeconds);
            _focusStatusText.Text = TimerClockTool.IsRunning ? "Focusing" : "Paused";
            _focusToggleIcon.Text = TimerClockTool.IsRunning ? "⏸" : "▶";
        }

        private static Border CreateClipRow(string title, string time)
        {
            var p = CapsuleTheme.Current;
            var border = new Border
            {
                Background = CapsuleTheme.Brush(p.Surface2),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 2, 0, 2),
                Cursor = Cursors.Hand
            };

            var dock = new DockPanel();
            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(new TextBlock
            {
                Text = time,
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            });
            right.Children.Add(new TextBlock
            {
                Text = "⧉",
                FontSize = 11,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            DockPanel.SetDock(right, Dock.Right);
            dock.Children.Add(right);

            var left = new StackPanel { Orientation = Orientation.Horizontal };
            left.Children.Add(new TextBlock
            {
                Text = "📄 ",
                FontSize = 9.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });
            left.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 130
            });
            dock.Children.Add(left);

            border.Child = dock;
            border.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                try { System.Windows.Clipboard.SetText(title); } catch { }
            };
            return border;
        }

        private static BuddyCard BuildCompactClockCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("clock");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "🕒 CLOCK", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("h:mm tt"), FontSize = 22, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("dddd, MMM d"), FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactCalendarCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("calendar");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "📅 CALENDAR", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("MMMM d"), FontSize = 20, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "No upcoming events", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactNowPlayingCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("audio");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "🎵 NOW PLAYING", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(RealtimeNotchActionService.Instance.CurrentTrack) ? "No Media Playing" : RealtimeNotchActionService.Instance.CurrentTrack, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(RealtimeNotchActionService.Instance.CurrentArtist) ? "Windows Audio" : RealtimeNotchActionService.Instance.CurrentArtist, FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactLinksCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("links");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "🔗 LINKS", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "Quick Bookmarks", FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "Google · GitHub · YouTube", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactFileShelfCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("fileshelf");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "📁 FILE SHELF", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "File Staging Shelf", FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "Drag & drop files here", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactUnitsCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("units");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "⇄ UNITS", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "Unit Converter", FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "Length · Weight · Temp", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactConverterCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("fileshelf");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "📄 CONVERTER", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "File Converter", FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "PNG, JPG, PDF, WEBP", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactMessageCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("message");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "💬 MESSAGE", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            string msg = WindowsBuddy.Tools.Message.MessageService.Instance.Settings?.Text;
            stack.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(msg) ? "Notch Message" : msg, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text), TextTrimming = TextTrimming.CharacterEllipsis });
            stack.Children.Add(new TextBlock { Text = "Flash message on notch", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static int _waterGlasses = 4;
        private static BuddyCard BuildCompactWaterCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "💧 WATER", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var mainGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var stack = new StackPanel();
            var glassText = new TextBlock { Text = $"{_waterGlasses} / 8 glasses", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) };
            stack.Children.Add(glassText);
            stack.Children.Add(new TextBlock { Text = "Goal: 2000 ml", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            Grid.SetColumn(stack, 0);
            mainGrid.Children.Add(stack);

            var plusBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(14),
                Background = CapsuleTheme.Brush(p.Surface2),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            plusBtn.Child = new TextBlock
            {
                Text = "+1",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            plusBtn.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                _waterGlasses = Math.Min(12, _waterGlasses + 1);
                glassText.Text = $"{_waterGlasses} / 8 glasses";
            };
            Grid.SetColumn(plusBtn, 1);
            mainGrid.Children.Add(plusBtn);

            dock.Children.Add(mainGrid);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactStopwatchCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            card.MouseLeftButtonUp += (_, _) => ToolRegistry.OnToolNavigationRequested?.Invoke("focus");
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "⏱ STOPWATCH", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "00:00.0", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "Tap to open Stopwatch", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactAnalyticsCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "📊 ANALYTICS", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "1,420 Visits", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "+18.4% today", FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(48, 209, 88)), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactRevenueCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "💵 REVENUE", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "$540.00", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "3 licenses today", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Text2), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        private static BuddyCard BuildCompactAICodingCard(ThemePalette p)
        {
            var card = new BuddyCard { Margin = new Thickness(3), Cursor = Cursors.Hand };
            var dock = new DockPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            header.Children.Add(new TextBlock { Text = "🤖 AI CODING", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text2) });
            DockPanel.SetDock(header, Dock.Top);
            dock.Children.Add(header);

            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "Claude & Codex", FontSize = 15, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Text) });
            stack.Children.Add(new TextBlock { Text = "Online · 4.2k tokens", FontSize = 10.5, Foreground = CapsuleTheme.Brush(p.Accent), Margin = new Thickness(0, 2, 0, 0) });
            dock.Children.Add(stack);
            card.Child = dock;
            return card;
        }

        public void OnActivated()
        {
            RefreshAllCards();
        }

        public void InvalidateView() { }
    }

    /// <summary>
    /// FileShelfTool: File Shelf parked files tool matching 1:1 original Vayli.
    /// </summary>
    public sealed class FileShelfTool : IBuddyTool
    {
        public string Id => "fileshelf";
        public string Title => LocalizationService.T("tool.fileshelf", "File Shelf");
        public string IconGlyph => "📁";

        private WindowsBuddy.Tools.FileShelf.FileShelfToolView _view;

        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.FileShelf.FileShelfToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }

        public void OnActivated() { }
        public void InvalidateView() => _view = null;
    }

    internal static class ToolViewLifecycleHelper
    {
        public static void DetachFromParent(FrameworkElement element)
        {
            if (element == null) return;
            try
            {
                if (element.Parent is Panel pnl)
                    pnl.Children.Remove(element);
                else if (element.Parent is Decorator dec)
                    dec.Child = null;
                else if (element.Parent is ContentControl cc)
                    cc.Content = null;

                var vParent = VisualTreeHelper.GetParent(element);
                if (vParent is Panel vpnl)
                    vpnl.Children.Remove(element);
                else if (vParent is Decorator vdec)
                    vdec.Child = null;
                else if (vParent is ContentControl vcc)
                    vcc.Content = null;
            }
            catch { }
        }
    }

    /// <summary>
    /// LinksTool: Quick Bookmarks matching Vayli 1:1 with search engines and saved bookmark cards.
    /// </summary>
    public sealed class LinksTool : IBuddyTool
    {
        public string Id => "links";
        public string Title => LocalizationService.T("tool.links", "Links");
        public string IconGlyph => "🔗";

        private WindowsBuddy.Tools.Links.LinksToolView _view;

        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.Links.LinksToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }

        public void OnActivated()
        {
            _view?.OnActivated();
        }

        public void InvalidateView() => _view = null;
    }

    /// <summary>
    /// ClipboardTool: Live clipboard history matching the exact Vayli split-column UI with code, image & link previews.
    /// </summary>
    public sealed class ClipboardTool : IBuddyTool
    {
        public string Id => "clipboard";
        public string Title => LocalizationService.T("tool.clipboard", "Clipboard");
        public string IconGlyph => "📋";

        private WindowsBuddy.Tools.Clipboard.ClipboardToolView _view;

        public static void CaptureCurrentClipboard()
        {
            WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.ScheduleCaptureDebounced(150);
        }

        public FrameworkElement BuildView()
        {
            WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.ScheduleCaptureDebounced(0);
            _view ??= new WindowsBuddy.Tools.Clipboard.ClipboardToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }

        public void OnActivated()
        {
            WindowsBuddy.Tools.Clipboard.ClipboardService.Instance.ScheduleCaptureDebounced(0);
            _view?.RefreshList();
        }

        public void InvalidateView() => _view = null;
    }

    /// <summary>
    /// NotesTool: Live notes & scratchpad matching the exact Vayli split-column UI.
    /// </summary>
    public sealed class NotesTool : IBuddyTool
    {
        public string Id => "notes";
        public string Title => LocalizationService.T("tool.notes", "Notes");
        public string IconGlyph => "📝";

        private WindowsBuddy.BuddyCapsule.UI.NotesToolView _view;

        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.BuddyCapsule.UI.NotesToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }

        public void OnActivated()
        {
            _view?.RefreshList();
        }

        public void InvalidateView() => _view = null;
    }

    /// <summary>
    /// TimerClockTool: Full Timers tool matching Vayli (Pomodoro, Countdown, Stopwatch, Hydration Water).
    /// </summary>
    public sealed class TimerClockTool : IBuddyTool
    {
        public string Id => "focus";
        public string Title => LocalizationService.T("tool.focus", "Timers");
        public string IconGlyph => "⏱";

        public static int RemainingSeconds => WindowsBuddy.Tools.Timer.TimerService.Instance.PomoRemainingSeconds;
        public static bool IsRunning => WindowsBuddy.Tools.Timer.TimerService.Instance.PomoIsRunning;
        public static void ToggleRunning() => WindowsBuddy.Tools.Timer.TimerService.Instance.TogglePomo();
        public static string FormatTime(int totalSec) => $"{totalSec / 60:D2}:{totalSec % 60:D2}";

        private WindowsBuddy.BuddyCapsule.UI.TimerToolView _view;

        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.BuddyCapsule.UI.TimerToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }

        public void OnActivated()
        {
            _view?.UpdateVisuals();
        }

        public void InvalidateView() => _view = null;
    }

    #region Supplementary Dock Tools for the 18 Dock Icons

    /// <summary>
    /// SystemTool: Real-time 4-Card hardware telemetry (Battery, Network rates, CPU/RAM/Disk, Volume & Audio Endpoint).
    /// </summary>
    public sealed class SystemTool : IBuddyTool
    {
        public string Id => "system";
        public string Title => LocalizationService.T("tool.system", "System");
        public string IconGlyph => "⚙";

        private WindowsBuddy.BuddyCapsule.UI.SystemToolView _view;

        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.BuddyCapsule.UI.SystemToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }

        public void OnActivated()
        {
            _view?.RefreshAllMetrics();
        }

        public void InvalidateView() => _view = null;
    }

    public sealed class ClockTool : IBuddyTool
    {
        public string Id => "clock";
        public string Title => LocalizationService.T("tool.clock", "Clock");
        public string IconGlyph => "🕐";
        private WindowsBuddy.Tools.Clock.ClockToolView _view;
        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.Clock.ClockToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }
        public void OnActivated()
        {
            _view?.OnActivated();
        }
        public void InvalidateView() => _view = null;
    }

    public sealed class CalendarTool : IBuddyTool
    {
        public string Id => "calendar";
        public string Title => LocalizationService.T("tool.calendar", "Calendar");
        public string IconGlyph => "📅";
        private WindowsBuddy.Tools.Calendar.CalendarToolView _view;
        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.Calendar.CalendarToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }
        public void OnActivated()
        {
            _view?.OnActivated();
        }
        public void InvalidateView() => _view = null;
    }

    public sealed class TransferTool : IBuddyTool
    {
        public string Id => "transfer";
        public string Title => LocalizationService.T("tool.maylidrop", "Transfer");
        public string IconGlyph => "⇄";
        private WindowsBuddy.Tools.FileShelf.FileShelfToolView _view;
        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.FileShelf.FileShelfToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }
        public void OnActivated() { }
        public void InvalidateView() => _view = null;
    }

    public sealed class QuickToolsTool : IBuddyTool
    {
        public string Id => "units";
        public string Title => LocalizationService.T("tool.units", "Units");
        public string IconGlyph => "📐";
        private WindowsBuddy.Tools.Units.UnitsToolView _view;
        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.Units.UnitsToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }
        public void OnActivated()
        {
            _view?.OnActivated();
        }
        public void InvalidateView() => _view = null;
    }

    /// <summary>
    /// MessageBannerTool: Notch Banner tool matching Vayli 1:1 with live preview, animations, colors and notch flash.
    /// </summary>
    public sealed class MessageBannerTool : IBuddyTool
    {
        public string Id => "message";
        public string Title => LocalizationService.T("tool.message", "Message");
        public string IconGlyph => "💬";
        private WindowsBuddy.Tools.Message.MessageToolView _view;
        public FrameworkElement BuildView()
        {
            _view ??= new WindowsBuddy.Tools.Message.MessageToolView();
            ToolViewLifecycleHelper.DetachFromParent(_view);
            return _view;
        }
        public void OnActivated() { }
        public void InvalidateView() => _view = null;
    }

    public sealed class AudioTool : IBuddyTool
    {
        public string Id => "audio";
        public string Title => "Audio";
        public string IconGlyph => "🎚";
        private BuddyCard _card;
        public FrameworkElement BuildView()
        {
            if (_card != null)
            {
                ToolViewLifecycleHelper.DetachFromParent(_card);
                return _card;
            }
            var p = CapsuleTheme.Current;
            _card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = "MEDIA & SOUND", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = CapsuleTheme.Brush(p.Accent), HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new StaticBeatBars(8, 24) { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 12) });
            stack.Children.Add(new TextBlock { Text = "AirPods Pro Connected · Volume 74%", FontSize = 11.5, Foreground = CapsuleTheme.Brush(p.Text2), HorizontalAlignment = HorizontalAlignment.Center });
            _card.Child = stack;
            return _card;
        }
        public void OnActivated() { }
        public void InvalidateView() => _card = null;
    }

    public sealed class PetCompanionTool : IBuddyTool
    {
        public string Id => "pet";
        public string Title => "Buddy";
        public string IconGlyph => "✨";
        private BuddyCard _card;
        public FrameworkElement BuildView()
        {
            if (_card != null)
            {
                ToolViewLifecycleHelper.DetachFromParent(_card);
                return _card;
            }
            var p = CapsuleTheme.Current;
            _card = new BuddyCard { Margin = new Thickness(4) };
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(16) };

            var petView = new PetView("happy", 88)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8),
                ToolTip = "Click me! / کلیک کنید تا واکنش نشون بدم"
            };
            stack.Children.Add(petView);

            stack.Children.Add(new TextBlock
            {
                Text = "Mayli Orb Companion",
                FontSize = 14.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 2)
            });

            var quoteText = new TextBlock
            {
                Text = PetEngine.Instance.CurrentStatusLine,
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Accent),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 10),
                TextAlignment = TextAlignment.Center
            };
            stack.Children.Add(quoteText);

            PetEngine.Instance.MoodChanged += _ =>
            {
                quoteText.Text = PetEngine.Instance.CurrentStatusLine;
            };

            var moodRow = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            var quickMoods = new (string Glyph, string Label, OrbMood Mood)[]
            {
                ("😊", "شاد", OrbMood.Happy),
                ("😠", "اخم", OrbMood.Angry),
                ("💖", "دلبر", OrbMood.Love),
                ("😉", "چشمک", OrbMood.Wink),
                ("😴", "خواب", OrbMood.Sleepy),
                ("🌟", "عادی", OrbMood.Idle)
            };

            foreach (var qm in quickMoods)
            {
                var captured = qm.Mood;
                var btn = new BuddyPillButton(qm.Glyph, qm.Label) { Margin = new Thickness(2) };
                btn.Clicked += () =>
                {
                    PetEngine.Instance.SetMood(captured);
                    petView.SetMood(captured);
                };
                moodRow.Children.Add(btn);
            }

            var pokeBtn = new BuddyPillButton("🐾", "قلقلک") { Margin = new Thickness(2) };
            pokeBtn.Clicked += () =>
            {
                petView.Orb.Poke();
                petView.Orb.SpawnSparkleBurst();
            };
            moodRow.Children.Add(pokeBtn);

            stack.Children.Add(moodRow);
            _card.Child = stack;
            return _card;
        }
        public void OnActivated() { }
        public void InvalidateView() => _card = null;
    }

    #endregion
}
