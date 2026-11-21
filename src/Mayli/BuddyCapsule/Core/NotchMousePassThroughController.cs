using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Core
{
    /// <summary>
    /// Evaluates whether a screen coordinate falls within the interactive bounds of the notch,
    /// its satellite islands, or the privacy indicator.
    /// Pairs exact silhouette WM_NCHITTEST routing (HTCLIENT vs HTTRANSPARENT) with dynamic
    /// WS_EX_TRANSPARENT toggling over non-interactive shadow halos and margins so clicks reliably
    /// pass through to underlying processes.
    /// </summary>
    public sealed class NotchMousePassThroughController : IDisposable
    {
        private readonly CapsuleWindow _window;
        private IntPtr _hwnd;
        private bool _isClickThroughApplied;
        private DispatcherTimer _recoveryTimer;
        private bool _disposed;

        public NotchMousePassThroughController(CapsuleWindow window)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
        }

        public void AttachWindow(IntPtr hwnd)
        {
            _hwnd = hwnd;
            if (_recoveryTimer == null && _window?.Dispatcher != null)
            {
                _recoveryTimer = new DispatcherTimer(DispatcherPriority.Input, _window.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(20)
                };
                _recoveryTimer.Tick += OnRecoveryTimerTick;
            }
        }

        public IntPtr HandleNcHitTest(IntPtr lParam, out bool handled)
        {
            int x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
            int y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
            var screenPt = new Point(x, y);

            if (HitTestInteractive(screenPt))
            {
                SetWindowClickThrough(false);
                _recoveryTimer?.Stop();
                handled = true;
                return (IntPtr)1; // HTCLIENT
            }

            SetWindowClickThrough(true);
            if (_window != null && !_window.IsTucked)
            {
                _recoveryTimer?.Start();
            }

            handled = true;
            return (IntPtr)NativeMethods.HTTRANSPARENT;
        }

        public void SetWindowClickThrough(bool enable)
        {
            if (_hwnd == IntPtr.Zero || _isClickThroughApplied == enable)
            {
                if (!enable) _recoveryTimer?.Stop();
                return;
            }

            try
            {
                int exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE);
                int targetStyle = enable
                    ? (exStyle | NativeMethods.WS_EX_TRANSPARENT)
                    : (exStyle & ~NativeMethods.WS_EX_TRANSPARENT);

                if (targetStyle != exStyle)
                {
                    NativeMethods.SetWindowLong(_hwnd, NativeMethods.GWL_EXSTYLE, targetStyle);
                }

                _isClickThroughApplied = enable;
                if (!enable)
                {
                    _recoveryTimer?.Stop();
                }
            }
            catch
            {
                // Ignore Win32 handle errors during window teardown
            }
        }

        private void OnRecoveryTimerTick(object sender, EventArgs e)
        {
            if (_disposed || _hwnd == IntPtr.Zero || _window == null || _window.IsTucked || !_isClickThroughApplied)
            {
                _recoveryTimer?.Stop();
                return;
            }

            if (!NativeMethods.GetCursorPos(out var cursorPt))
            {
                return;
            }

            if (!NativeMethods.GetWindowRect(_hwnd, out var rect))
            {
                SetWindowClickThrough(false);
                return;
            }

            // Once cursor moves outside the window envelope bounds, restore normal WM_NCHITTEST and stop polling
            const int margin = 8;
            bool insideEnvelope =
                cursorPt.X >= rect.Left - margin &&
                cursorPt.X <= rect.Right + margin &&
                cursorPt.Y >= rect.Top - margin &&
                cursorPt.Y <= rect.Bottom + margin;

            if (!insideEnvelope)
            {
                SetWindowClickThrough(false);
                return;
            }

            if (HitTestInteractive(new Point(cursorPt.X, cursorPt.Y)))
            {
                SetWindowClickThrough(false);
            }
        }

        /// <summary>
        /// Evaluates whether the given screen coordinate hits an interactive region of the notch window.
        /// </summary>
        /// <param name="screenPoint">Screen coordinate (in physical pixels when PresentationSource is attached, or DIPs in headless fallback).</param>
        /// <returns>True if the point is interactive (capturing clicks); false to pass through.</returns>
        public bool HitTestInteractive(Point screenPoint)
        {
            if (_window == null) return false;
            if (_window.IsTucked) return false;

            if (PresentationSource.FromVisual(_window) != null &&
                (!_window.IsVisible || _window.Opacity <= 0.02))
            {
                return false;
            }

            double winLeft = double.IsNaN(_window.Left) ? 0 : _window.Left;
            double winTop = double.IsNaN(_window.Top) ? 0 : _window.Top;

            // 1. High-priority check: Privacy Indicator interactive target (with 2px ergonomic boundary padding)
            if (_window.PrivacyIndicator != null &&
                _window.PrivacyIndicator.Visibility == Visibility.Visible)
            {
                try
                {
                    Point pt;
                    if (PresentationSource.FromVisual(_window.PrivacyIndicator) != null)
                    {
                        pt = _window.PrivacyIndicator.PointFromScreen(screenPoint);
                    }
                    else
                    {
                        // Fallback calculation for unit test environments where HwndSource is not attached
                        double indW = _window.PrivacyIndicator.ActualWidth > 0 ? _window.PrivacyIndicator.ActualWidth : (_window.PrivacyIndicator.Width > 0 ? _window.PrivacyIndicator.Width : 26.0);
                        double indH = _window.PrivacyIndicator.ActualHeight > 0 ? _window.PrivacyIndicator.ActualHeight : (_window.PrivacyIndicator.Height > 0 ? _window.PrivacyIndicator.Height : 24.0);
                        double cardW = _window.CapsuleCard != null && _window.CapsuleCard.ActualWidth > 0 ? _window.CapsuleCard.ActualWidth : CapsuleWindow.CollapsedWidth;
                        double cardLeft = (CapsuleWindow.StableEnvelopeWidth - cardW) / 2.0;

                        double baseLeft = (winLeft > 0 && screenPoint.X > cardLeft + cardW + 40.0) ? (winLeft + cardLeft) : cardLeft;
                        double baseTop = (screenPoint.Y >= winTop && winTop > 0) ? winTop : 0;

                        double indLeft = baseLeft + cardW - 10.0 - indW;
                        double indTop = baseTop + (CapsuleWindow.CollapsedHeight - indH) / 2.0;
                        pt = new Point(screenPoint.X - indLeft, screenPoint.Y - indTop);
                    }

                    double w = _window.PrivacyIndicator.ActualWidth > 0 ? _window.PrivacyIndicator.ActualWidth : (_window.PrivacyIndicator.Width > 0 ? _window.PrivacyIndicator.Width : 26.0);
                    double h = _window.PrivacyIndicator.ActualHeight > 0 ? _window.PrivacyIndicator.ActualHeight : (_window.PrivacyIndicator.Height > 0 ? _window.PrivacyIndicator.Height : 24.0);

                    if (pt.X >= -2 && pt.X <= w + 2 &&
                        pt.Y >= -2 && pt.Y <= h + 2)
                    {
                        return true;
                    }
                }
                catch
                {
                    // Ignore visual transform exceptions during detachment
                }
            }

            // 2. Detached satellite islands (Music, Timers, HUD)
            if (HitTestVisual(_window.MusicSatelliteIsland, screenPoint)) return true;
            if (HitTestVisual(_window.LeftTimerIsland, screenPoint)) return true;
            if (HitTestVisual(_window.RightTimerIsland, screenPoint)) return true;
            if (HitTestVisual(_window.RightHudSatelliteIsland, screenPoint)) return true;
            if (HitTestVisual(_window.CapsLockSatelliteIsland, screenPoint)) return true;

            // 3. Main notch card / backdrop silhouette geometry
            if (_window.CapsuleCard != null &&
                _window.CapsuleCard.IsVisible &&
                _window.CapsuleCard.IsHitTestVisible &&
                (PresentationSource.FromVisual(_window.CapsuleCard) == null || _window.CapsuleCard.Opacity > 0.02))
            {
                try
                {
                    var backdrop = _window.NotchBackdropPath;
                    if (backdrop != null &&
                        backdrop.Data != null &&
                        PresentationSource.FromVisual(backdrop) != null)
                    {
                        Point pathPt = backdrop.PointFromScreen(screenPoint);
                        return backdrop.Data.FillContains(pathPt);
                    }

                    Point pt;
                    if (PresentationSource.FromVisual(_window.CapsuleCard) != null)
                    {
                        pt = _window.CapsuleCard.PointFromScreen(screenPoint);
                    }
                    else
                    {
                        double cardW = _window.CapsuleCard.ActualWidth > 0 ? _window.CapsuleCard.ActualWidth : CapsuleWindow.CollapsedWidth;
                        double cardLeft = (CapsuleWindow.StableEnvelopeWidth - cardW) / 2.0;
                        double cardBaseLeft = (winLeft > 0 && screenPoint.X > cardLeft + cardW + 40.0) ? (winLeft + cardLeft) : cardLeft;
                        double cardBaseTop = (screenPoint.Y >= winTop && winTop > 0) ? winTop : 0;
                        pt = new Point(screenPoint.X - cardBaseLeft, screenPoint.Y - cardBaseTop);
                    }

                    double cardWidth = _window.CapsuleCard.ActualWidth > 0 ? _window.CapsuleCard.ActualWidth : CapsuleWindow.CollapsedWidth;
                    double cardHeight = _window.CapsuleCard.ActualHeight > 0 ? _window.CapsuleCard.ActualHeight : CapsuleWindow.CollapsedHeight;

                    if (pt.X >= 0 && pt.X <= cardWidth && pt.Y >= 0 && pt.Y <= cardHeight)
                    {
                        if (backdrop?.Data != null)
                        {
                            return backdrop.Data.FillContains(pt);
                        }
                        return true;
                    }
                }
                catch
                {
                    // Ignore exceptions during teardown
                }
            }

            return false;
        }

        private static bool HitTestVisual(FrameworkElement element, Point screenPoint)
        {
            if (element == null || !element.IsVisible || !element.IsHitTestVisible || element.Opacity <= 0.05)
            {
                return false;
            }

            try
            {
                if (PresentationSource.FromVisual(element) == null) return false;
                double w = element.ActualWidth;
                double h = element.ActualHeight;
                if (w <= 0 || h <= 0) return false;

                Point pt = element.PointFromScreen(screenPoint);
                if (pt.X < 0 || pt.X > w || pt.Y < 0 || pt.Y > h)
                {
                    return false;
                }

                // Respect circular / pill corners on rounded satellite borders
                if (element is Border border && border.CornerRadius.TopLeft > 0)
                {
                    double r = Math.Min(border.CornerRadius.TopLeft, Math.Min(w, h) / 2.0);
                    if ((pt.X >= r && pt.X <= w - r) || (pt.Y >= r && pt.Y <= h - r))
                    {
                        return true;
                    }

                    double cx = pt.X < r ? r : (w - r);
                    double cy = pt.Y < r ? r : (h - r);
                    double dx = pt.X - cx;
                    double dy = pt.Y - cy;
                    return (dx * dx + dy * dy) <= (r * r);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_recoveryTimer != null)
            {
                _recoveryTimer.Stop();
                _recoveryTimer.Tick -= OnRecoveryTimerTick;
                _recoveryTimer = null;
            }
        }
    }
}
