using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.UI
{
    /// <summary>
    /// Pure C# WPF control displaying macOS-style live microphone and camera privacy indicators
    /// positioned on the right ear shoulder of the notch capsule card.
    /// Provides hover application name tooltips, dwell-timer suppression, and instant click-to-mute routing.
    /// </summary>
    public sealed class NotchPrivacyIndicator : Grid
    {
        public static readonly Color MicrophoneOrange = Color.FromRgb(255, 149, 0); // #FF9500
        public static readonly Color CameraGreen = Color.FromRgb(48, 209, 88);       // #30D158
        public static readonly Color MutedRed = Color.FromRgb(255, 69, 58);          // #FF453A

        private readonly Border _pillHost;
        private readonly StackPanel _dotsStack;
        private readonly Grid _camContainer;
        private readonly Grid _micContainer;
        private readonly Ellipse _camDot;
        private readonly Ellipse _micDot;
        private readonly Line _micSlash;
        private readonly DropShadowEffect _camGlow;
        private readonly DropShadowEffect _micGlow;
        private readonly ScaleTransform _scaleTransform;
        private readonly TranslateTransform _translateTransform;
        private readonly ToolTip _toolTip;

        private bool _isMicrophoneInUse;
        private string _microphoneAppName = string.Empty;
        private bool _isCameraInUse;
        private string _cameraAppName = string.Empty;
        private bool _isMicrophoneMuted;
        private bool _isDisappearing;

        /// <summary>Raised when the user clicks the indicator to request microphone mute toggle.</summary>
        public event Action MuteToggleRequested;

        /// <summary>True when pointer is hovering the privacy indicator (used to suppress notch dwell timer).</summary>
        public bool IsHovered { get; private set; }

        public bool IsMicrophoneInUse => _isMicrophoneInUse;
        public string MicrophoneAppName => _microphoneAppName;
        public bool IsCameraInUse => _isCameraInUse;
        public string CameraAppName => _cameraAppName;
        public bool IsMicrophoneMuted => _isMicrophoneMuted;

        public Border PillHost => _pillHost;
        public Ellipse CamDot => _camDot;
        public Ellipse MicDot => _micDot;
        public Line MicSlash => _micSlash;
        public ToolTip IndicatorToolTip => _toolTip;

        public NotchPrivacyIndicator()
        {
            // Initial layout & properties
            Width = 26.0;
            Height = 24.0;
            HorizontalAlignment = HorizontalAlignment.Right;
            VerticalAlignment = VerticalAlignment.Center;
            Margin = new Thickness(0, 0, 10, 0);
            Background = Brushes.Transparent; // Full 26x24 touch/click hit box
            Cursor = Cursors.Hand;
            Visibility = Visibility.Collapsed;
            Opacity = 0.0;

            // Accessibility / Automation
            AutomationProperties.SetAutomationId(this, "NotchPrivacyIndicator");
            AutomationProperties.SetName(this, "Privacy Indicator");

            // Transforms for pop-in, pop-out and click bounce
            RenderTransformOrigin = new Point(0.5, 0.5);
            _scaleTransform = new ScaleTransform(1.0, 1.0);
            _translateTransform = new TranslateTransform(0.0, 0.0);
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(_scaleTransform);
            transformGroup.Children.Add(_translateTransform);
            RenderTransform = transformGroup;

            // Tooltip setup matching dark glass aesthetic
            _toolTip = new ToolTip
            {
                Background = new SolidColorBrush(Color.FromRgb(28, 28, 30)),     // #1C1C1E
                BorderBrush = new SolidColorBrush(Color.FromRgb(56, 56, 58)),    // #38383A
                BorderThickness = new Thickness(1.0),
                Foreground = Brushes.White,
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 11.5,
                FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI, sans-serif")
            };
            ToolTipService.SetInitialShowDelay(this, 40);
            ToolTipService.SetBetweenShowDelay(this, 0);
            ToolTip = _toolTip;

            // Pill container (used when both mic and cam are active)
            _pillHost = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0.0),
                CornerRadius = new CornerRadius(0.0)
            };

            // Horizontal stack of dots
            _dotsStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Camera Dot
            _camGlow = new DropShadowEffect
            {
                Color = CameraGreen,
                BlurRadius = 10.0,
                ShadowDepth = 0.0,
                Opacity = 0.90
            };
            _camDot = new Ellipse
            {
                Width = 7.5,
                Height = 7.5,
                Fill = new SolidColorBrush(CameraGreen),
                Effect = _camGlow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _camContainer = new Grid
            {
                Width = 8.0,
                Height = 8.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _camContainer.Children.Add(_camDot);

            // Microphone Dot & Muted Diagonal Slash
            _micGlow = new DropShadowEffect
            {
                Color = MicrophoneOrange,
                BlurRadius = 10.0,
                ShadowDepth = 0.0,
                Opacity = 0.90
            };
            _micDot = new Ellipse
            {
                Width = 7.5,
                Height = 7.5,
                Fill = new SolidColorBrush(MicrophoneOrange),
                Effect = _micGlow,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _micSlash = new Line
            {
                X1 = 1.5,
                Y1 = 1.5,
                X2 = 6.0,
                Y2 = 6.0,
                Stroke = Brushes.White,
                StrokeThickness = 1.5,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _micContainer = new Grid
            {
                Width = 8.0,
                Height = 8.0,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed
            };
            _micContainer.Children.Add(_micDot);
            _micContainer.Children.Add(_micSlash);

            // Assemble children
            _dotsStack.Children.Add(_camContainer);
            _dotsStack.Children.Add(_micContainer);
            _pillHost.Child = _dotsStack;
            Children.Add(_pillHost);

            StartBreathingAnimation();
        }

        private void StartBreathingAnimation()
        {
            var anim = new DoubleAnimation
            {
                From = 0.70,
                To = 1.00,
                Duration = TimeSpan.FromMilliseconds(1800),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            _camGlow.BeginAnimation(DropShadowEffect.OpacityProperty, anim);
            _micGlow.BeginAnimation(DropShadowEffect.OpacityProperty, anim);
        }

        /// <summary>
        /// Updates device access state from DevicePrivacyService event.
        /// </summary>
        public void UpdateAccessState(DeviceAccessEventArgs args)
        {
            if (args == null) return;
            UpdateAccessState(args.IsMicrophoneInUse, args.MicrophoneAppName, args.IsCameraInUse, args.CameraAppName);
        }

        /// <summary>
        /// Updates device access state.
        /// </summary>
        public void UpdateAccessState(bool isMicInUse, string micAppName, bool isCamInUse, string camAppName)
        {
            _isMicrophoneInUse = isMicInUse;
            _microphoneAppName = micAppName ?? string.Empty;
            _isCameraInUse = isCamInUse;
            _cameraAppName = camAppName ?? string.Empty;

            ApplyVisualState();
        }

        /// <summary>
        /// Updates microphone mute state from CoreAudioService event.
        /// </summary>
        public void UpdateMuteState(bool isMuted)
        {
            _isMicrophoneMuted = isMuted;
            ApplyMuteVisuals();
            UpdateToolTip();
        }

        private void ApplyVisualState()
        {
            bool anyActive = _isMicrophoneInUse || _isCameraInUse;
            bool bothActive = _isMicrophoneInUse && _isCameraInUse;

            if (!anyActive)
            {
                // Both released -> Auto-clear fade-out
                UpdateToolTip();
                TriggerDisappearanceAnimation();
                return;
            }

            _isDisappearing = false;

            if (bothActive)
            {
                // Dual-dot pill mode (32x16 container with green camera left and orange mic right)
                Width = 34.0;
                _pillHost.Width = 32.0;
                _pillHost.Height = 16.0;
                _pillHost.CornerRadius = new CornerRadius(8.0);
                _pillHost.Background = new SolidColorBrush(Color.FromArgb(178, 28, 28, 30));
                _pillHost.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
                _pillHost.BorderThickness = new Thickness(0.75);

                _camDot.Width = 6.0;
                _camDot.Height = 6.0;
                _camContainer.Width = 6.0;
                _camContainer.Height = 6.0;
                _camContainer.Margin = new Thickness(0, 0, 4.0, 0);
                _camContainer.Visibility = Visibility.Visible;

                _micDot.Width = 6.0;
                _micDot.Height = 6.0;
                _micContainer.Width = 6.0;
                _micContainer.Height = 6.0;
                _micSlash.X1 = 1.2;
                _micSlash.Y1 = 1.2;
                _micSlash.X2 = 4.8;
                _micSlash.Y2 = 4.8;
                _micContainer.Visibility = Visibility.Visible;
            }
            else
            {
                // Single dot mode
                Width = 26.0;
                _pillHost.Width = double.NaN;
                _pillHost.Height = double.NaN;
                _pillHost.CornerRadius = new CornerRadius(0.0);
                _pillHost.Background = Brushes.Transparent;
                _pillHost.BorderBrush = Brushes.Transparent;
                _pillHost.BorderThickness = new Thickness(0.0);

                if (_isCameraInUse)
                {
                    _camDot.Width = 7.5;
                    _camDot.Height = 7.5;
                    _camContainer.Width = 8.0;
                    _camContainer.Height = 8.0;
                    _camContainer.Margin = new Thickness(0);
                    _camContainer.Visibility = Visibility.Visible;
                    _micContainer.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _micDot.Width = 7.5;
                    _micDot.Height = 7.5;
                    _micContainer.Width = 8.0;
                    _micContainer.Height = 8.0;
                    _micSlash.X1 = 1.5;
                    _micSlash.Y1 = 1.5;
                    _micSlash.X2 = 6.0;
                    _micSlash.Y2 = 6.0;
                    _micContainer.Visibility = Visibility.Visible;
                    _camContainer.Visibility = Visibility.Collapsed;
                }
            }

            ApplyMuteVisuals();
            UpdateToolTip();

            if (Visibility != Visibility.Visible || Opacity < 0.95)
            {
                TriggerAppearanceAnimation();
            }
        }

        private void ApplyMuteVisuals()
        {
            if (_isMicrophoneMuted)
            {
                _micDot.Fill = new SolidColorBrush(MutedRed);
                _micGlow.Color = MutedRed;
                _micSlash.Visibility = _isMicrophoneInUse ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                _micDot.Fill = new SolidColorBrush(MicrophoneOrange);
                _micGlow.Color = MicrophoneOrange;
                _micSlash.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateToolTip()
        {
            if (_isMicrophoneInUse && _isCameraInUse)
            {
                string camApp = string.IsNullOrWhiteSpace(_cameraAppName) ? "an application" : _cameraAppName;
                string micApp = string.IsNullOrWhiteSpace(_microphoneAppName) ? "an application" : _microphoneAppName;
                string status = _isMicrophoneMuted ? "Muted - Click to Unmute" : "Active - Click to Mute";
                _toolTip.Content = $"Camera: {camApp}\nMicrophone: {micApp} ({status})";
            }
            else if (_isMicrophoneInUse)
            {
                string micApp = string.IsNullOrWhiteSpace(_microphoneAppName) ? "an application" : _microphoneAppName;
                string status = _isMicrophoneMuted ? "Status: Muted (Click to Unmute)" : "Status: Active (Click to Mute)";
                _toolTip.Content = $"Microphone in use by {micApp}\n{status}";
            }
            else if (_isCameraInUse)
            {
                string camApp = string.IsNullOrWhiteSpace(_cameraAppName) ? "an application" : _cameraAppName;
                _toolTip.Content = $"Camera in use by {camApp}";
            }
            else
            {
                _toolTip.IsOpen = false;
                _toolTip.Content = null;
            }
        }

        private void TriggerAppearanceAnimation()
        {
            Visibility = Visibility.Visible;

            var fadeAnim = new DoubleAnimation
            {
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            var scaleAnim = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(220),
                EasingFunction = new BackEase { Amplitude = 0.3, EasingMode = EasingMode.EaseOut }
            };

            BeginAnimation(OpacityProperty, fadeAnim);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        private void TriggerDisappearanceAnimation()
        {
            if (_isDisappearing || Visibility == Visibility.Collapsed) return;
            _isDisappearing = true;

            var fadeAnim = new DoubleAnimation
            {
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            var scaleAnim = new DoubleAnimation
            {
                To = 0.6,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeAnim.Completed += (_, _) =>
            {
                if (_isDisappearing)
                {
                    Visibility = Visibility.Collapsed;
                    _isDisappearing = false;
                }
            };

            BeginAnimation(OpacityProperty, fadeAnim);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
        }

        public void PlayClickBounce()
        {
            var bounceAnim = new DoubleAnimationUsingKeyFrames();
            bounceAnim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            bounceAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(70))));
            bounceAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));

            _scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounceAnim);
            _scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounceAnim);
        }

        /// <summary>
        /// Simulates click programmatically for automated unit testing.
        /// </summary>
        public void SimulateClick()
        {
            CoreAudioService.Instance.ToggleMicrophoneMute();
            MuteToggleRequested?.Invoke();
            PlayClickBounce();
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            base.OnMouseEnter(e);
            IsHovered = true;
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            IsHovered = false;
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            e.Handled = true;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            // Crucial: mark event handled to stop bubbling to _capsuleCard and prevent notch expansion
            e.Handled = true;

            CoreAudioService.Instance.ToggleMicrophoneMute();
            MuteToggleRequested?.Invoke();
            PlayClickBounce();
        }
    }
}
