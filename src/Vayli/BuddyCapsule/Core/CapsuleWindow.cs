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
    /// CapsuleWindow (`WindowsBuddy.BuddyCapsule.Core.CapsuleWindow`)
    /// Exact constants recovered by REA:
    /// CollapsedWidth = 228, CollapsedHeight = 35
    /// ExpandedWidth = 752, ExpandedHeight = 352
    /// ShadowMarginX = 24, ShadowMarginY = 36
    /// StableEnvelopeWidth = 800, StableEnvelopeHeight = 388
    /// PresenceSeconds = 0.18, CollapseSeconds = 0.07
    /// </summary>
    public sealed class CapsuleWindow : Window
    {
        public const double CollapsedWidth = 228.0;
        public const double CollapsedHeight = 35.0;
        public const double ExpandedWidth = 752.0;
        public const double ExpandedHeight = 352.0;
        public const double ShadowMarginX = 24.0;
        public const double ShadowMarginY = 36.0;
        public const double StableEnvelopeWidth = 800.0;
        public const double StableEnvelopeHeight = 388.0;
        public const double EarUnitW = 12.0;
        public const double EarUnitH = 10.0;
        public const double PresenceSeconds = 0.18;
        public const double CollapseSeconds = 0.07;
        public const int MiniBarCount = 4;
        public const double HeaderRobotSize = 28.0;

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
        private readonly Dictionary<string, BuddyPillButton> _dockButtons = new();

        private readonly TextBlock _collapsedClockText;
        private readonly TextBlock _collapsedBatteryText;
        private readonly StaticBeatBars _collapsedBeatBars;
        private readonly PetView _collapsedMiniPet;

        private readonly DispatcherTimer _morphTimer;
        private readonly DispatcherTimer _clockTimer;
        private double _morphProgress; // 0.0 = Collapsed (228x35), 1.0 = Expanded (752x352)
        private double _targetProgress;
        private string _activeToolId = "home";

        public CapsuleWindow()
        {
            ToolRegistry.RegisterDefaults();
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
                    BlurRadius = 22,
                    ShadowDepth = 6,
                    Opacity = 0.68,
                    Color = Colors.Black
                }
            };

            _notchOutlinePath = new Path
            {
                Stroke = CapsuleTheme.Brush(p.BorderStrong),
                StrokeThickness = 1.0
            };

            _edgeLight = new EdgeLightWpf();

            // 1. Collapsed Resting Content (228 x 35)
            _collapsedContent = new Grid
            {
                Margin = new Thickness(EarUnitW + 10, 2, EarUnitW + 10, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            _collapsedContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _collapsedContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _collapsedContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _collapsedMiniPet = new PetView("idle", HeaderRobotSize)
            {
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedMiniPet, 0);
            _collapsedContent.Children.Add(_collapsedMiniPet);

            _collapsedClockText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm"),
                FontSize = 12.5,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedClockText, 1);
            _collapsedContent.Children.Add(_collapsedClockText);

            _collapsedBeatBars = new StaticBeatBars(MiniBarCount, 12)
            {
                Margin = new Thickness(8, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_collapsedBeatBars, 3);
            _collapsedContent.Children.Add(_collapsedBeatBars);

            _collapsedBatteryText = new TextBlock
            {
                Text = $"{SystemMonitorService.Current.BatteryPercent}%⚡",
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_collapsedBatteryText, 4);
            _collapsedContent.Children.Add(_collapsedBatteryText);

            // 2. Expanded Content (752 x 352)
            _expandedContent = new Grid
            {
                Margin = new Thickness(EarUnitW + 14, 10, EarUnitW + 14, 14),
                Opacity = 0.0,
                Visibility = Visibility.Collapsed
            };
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _expandedContent.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top Capsule Header inside Expanded View
            var headerBar = new DockPanel { Margin = new Thickness(4, 0, 4, 8) };
            var leftBrand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            leftBrand.Children.Add(new PetView("happy", HeaderRobotSize) { Margin = new Thickness(0, 0, 8, 0) });
            leftBrand.Children.Add(new TextBlock
            {
                Text = "Vayli",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text),
                VerticalAlignment = VerticalAlignment.Center
            });
            leftBrand.Children.Add(new TextBlock
            {
                Text = "  •  WindowsBuddy Capsule",
                FontSize = 11.5,
                Foreground = CapsuleTheme.Brush(p.Text2),
                VerticalAlignment = VerticalAlignment.Center
            });

            var rightActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var settingsBtn = new BuddyPillButton("⚙", "Settings");
            settingsBtn.Clicked += () => SettingsWindow.ShowSingleton();
            var collapseBtn = new BuddyPillButton("✕", "Close");
            collapseBtn.Clicked += () => _machine.RequestClose("close-button");
            rightActions.Children.Add(settingsBtn);
            rightActions.Children.Add(collapseBtn);
            DockPanel.SetDock(rightActions, Dock.Right);

            headerBar.Children.Add(rightActions);
            headerBar.Children.Add(leftBrand);
            Grid.SetRow(headerBar, 0);
            _expandedContent.Children.Add(headerBar);

            // Center Tool Viewport
            _viewportHost = new Border
            {
                Margin = new Thickness(0, 2, 0, 8)
            };
            Grid.SetRow(_viewportHost, 1);
            _expandedContent.Children.Add(_viewportHost);

            // Bottom Navigation Dock Bar
            var navDockBorder = new Border
            {
                Background = CapsuleTheme.Brush(p.DockPill),
                BorderBrush = CapsuleTheme.Brush(p.Border),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
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
            _capsuleCard.MouseLeftButtonUp += (_, _) => _machine.Click("capsule-click");

            _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clockTimer.Tick += (_, _) =>
            {
                _collapsedClockText.Text = TimerClockTool.IsRunning
                    ? $"⏱ {TimerClockTool.FormatTime(TimerClockTool.RemainingSeconds)}"
                    : DateTime.Now.ToString("HH:mm");
                var sys = SystemMonitorService.Current;
                _collapsedBatteryText.Text = $"{sys.BatteryPercent}%{(sys.IsCharging ? "⚡" : "")}";
                _collapsedBeatBars.IsPlaying = MediaService.Instance.State.IsPlaying;
            };
            _clockTimer.Start();

            CapsuleTheme.ThemeChanged += ApplyThemeAndGeometry;
            AppPreferences.Instance.Changed += ApplyThemeAndGeometry;
            PetEngine.Instance.PoseChanged += () => _collapsedMiniPet.SetFrame(PetEngine.Instance.CurrentPose);

            NavigateToTool("home");
            UpdateMorphVisuals(0.0);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;

            // Set WS_EX_TOOLWINDOW | WS_EX_TOPMOST so it stays above taskbar/apps and out of Alt+Tab
            int exStyle = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, exStyle | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_TOPMOST);

            // Attach global hotkey (Ctrl+Alt+N) and Win32 Clipboard listener
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

            foreach (var tool in ToolRegistry.All)
            {
                string id = tool.Id;
                var btn = new BuddyPillButton(tool.IconGlyph, tool.Title, id)
                {
                    IsActive = string.Equals(id, _activeToolId, StringComparison.OrdinalIgnoreCase)
                };
                btn.Clicked += () => NavigateToTool(id);
                _dockButtons[id] = btn;
                _navTabsPanel.Children.Add(btn);
            }
        }

        public void NavigateToTool(string toolId)
        {
            var tool = ToolRegistry.GetById(toolId);
            if (tool == null) return;
            _activeToolId = tool.Id;

            foreach (var kv in _dockButtons)
                kv.Value.IsActive = string.Equals(kv.Key, _activeToolId, StringComparison.OrdinalIgnoreCase);

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
            _collapsedBatteryText.Foreground = CapsuleTheme.Brush(p.Accent);
            RebuildNavTabs();
            NavigateToTool(_activeToolId);
            UpdateMorphVisuals(_morphProgress);
        }

        public void ToggleFromExternal() => _machine.Toggle("tray-or-cli");
    }
}
