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
    public sealed partial class CapsuleWindow : Window
    {
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
                _edgeLight?.Stop();
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
                        _capsLockSatelliteIsland.Visibility = Visibility.Collapsed;
                    }
                };
            }
            else
            {
                _capsuleCard.Visibility = Visibility.Visible;
                _capsuleCard.IsHitTestVisible = true;
                _edgeLight?.ResumeIfAlways();
                EnsureTopmost();
                UpdateMorphVisuals(_morphProgress);
            }

            _capsuleCardTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _capsuleCard.BeginAnimation(OpacityProperty, fadeAnim);
            _leftIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _rightIslandTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _musicSatelliteTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _rightHudTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _capsLockSatelliteIsland.CapsuleTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
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

        private System.Windows.Forms.Screen _cachedTargetScreen;
        private DateTime _lastScreenCheckUtc = DateTime.MinValue;

        private System.Windows.Forms.Screen GetTargetScreen()
        {
            try
            {
                var now = DateTime.UtcNow;
                if (_cachedTargetScreen != null && (now - _lastScreenCheckUtc).TotalSeconds < 5.0)
                {
                    return _cachedTargetScreen;
                }
                _lastScreenCheckUtc = now;

                var screens = System.Windows.Forms.Screen.AllScreens;
                var targetScreen = System.Windows.Forms.Screen.PrimaryScreen;
                string choice = AppPreferences.Instance.NotchDisplay;
                if (string.Equals(choice, "Display 2", StringComparison.OrdinalIgnoreCase) && screens.Length > 1)
                    targetScreen = screens[1];
                else if (string.Equals(choice, "Display 1", StringComparison.OrdinalIgnoreCase) && screens.Length > 0)
                    targetScreen = screens[0];

                _cachedTargetScreen = targetScreen;
                return targetScreen;
            }
            catch
            {
                return _cachedTargetScreen ?? System.Windows.Forms.Screen.PrimaryScreen;
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
            _passThroughController?.AttachWindow(hwnd);
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);

            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            Closed += (_, _) =>
            {
                try { _passThroughController?.Dispose(); } catch { }
                try { Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged; } catch { }
            };
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.InvokeAsync(ScheduleRecentering);
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
                _passThroughController?.SetWindowClickThrough(true);
                SyncAudioSpectrumServiceState();
                _edgeLight?.Stop();
                _isTuckCompletedHidden = false;
                _leftTimerIsland.IsHitTestVisible = false;
                _rightTimerIsland.IsHitTestVisible = false;
                _musicSatelliteIsland.IsHitTestVisible = false;
                _rightHudSatelliteIsland.IsHitTestVisible = false;
                _capsLockSatelliteIsland.IsHitTestVisible = false;

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
                        _capsLockSatelliteIsland.Visibility = Visibility.Collapsed;
                    }
                };
            }
            else
            {
                _isTuckCompletedHidden = false;
                _passThroughController?.SetWindowClickThrough(false);
                SyncAudioSpectrumServiceState();
                _capsuleCard.Visibility = Visibility.Visible;
                _capsuleCard.IsHitTestVisible = true;
                _edgeLight?.ResumeIfAlways();
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
                        _capsLockSatelliteIsland.BeginAnimation(OpacityProperty, null);
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
            _capsLockSatelliteIsland.CapsuleTranslate.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            _leftTimerIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _rightTimerIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _musicSatelliteIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _rightHudSatelliteIsland.BeginAnimation(OpacityProperty, fadeAnim);
            _capsLockSatelliteIsland.BeginAnimation(OpacityProperty, fadeAnim);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_NCHITTEST && _passThroughController != null)
            {
                return _passThroughController.HandleNcHitTest(lParam, out handled);
            }

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
            else if (msg == NativeMethods.WM_DISPLAYCHANGE || msg == NativeMethods.WM_DPICHANGED || msg == NativeMethods.WM_SETTINGCHANGE)
            {
                ScheduleRecentering();
            }
            return IntPtr.Zero;
        }

    }
}
