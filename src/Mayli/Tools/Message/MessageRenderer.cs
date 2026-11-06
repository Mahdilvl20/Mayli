using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.Message
{
    public static class MessageRenderer
    {
        public static Brush GetForegroundBrush(string colorHex, MessageFinish finish)
        {
            Color baseColor;
            try
            {
                baseColor = (Color)ColorConverter.ConvertFromString(colorHex ?? "#FFFFFF");
            }
            catch
            {
                baseColor = Colors.White;
            }

            switch (finish)
            {
                case MessageFinish.Silver:
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new(Color.FromRgb(210, 212, 222), 0.0),
                            new(Colors.White, 0.45),
                            new(Color.FromRgb(158, 160, 175), 1.0)
                        },
                        new Point(0, 0),
                        new Point(1, 1));

                case MessageFinish.Ocean:
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new(Color.FromRgb(0, 210, 255), 0.0),
                            new(Color.FromRgb(0, 114, 255), 1.0)
                        },
                        new Point(0, 0),
                        new Point(1, 1));

                case MessageFinish.Aurora:
                    return new LinearGradientBrush(
                        new GradientStopCollection
                        {
                            new(Color.FromRgb(255, 107, 129), 0.0),
                            new(Color.FromRgb(108, 92, 231), 0.5),
                            new(Color.FromRgb(0, 206, 201), 1.0)
                        },
                        new Point(0, 0),
                        new Point(1, 1));

                case MessageFinish.None:
                default:
                    return new SolidColorBrush(baseColor);
            }
        }

        public static void AnimateElement(
            FrameworkElement element,
            TextBlock textBlock,
            string fullText,
            MessageAnimationMode mode,
            double speedMultiplier,
            DispatcherTimer existingTypeOnTimer,
            out DispatcherTimer newTypeOnTimer,
            double containerWidth = 260.0,
            bool isRtl = false)
        {
            newTypeOnTimer = null;
            if (element == null) return;

            existingTypeOnTimer?.Stop();

            double speed = Math.Clamp(speedMultiplier, 0.3, 3.0);
            double baseDurationSec = 0.45 / speed;
            double w = containerWidth > 80.0 ? containerWidth : 260.0;
            double halfNotch = w / 2.0;

            // Reset transforms & effects
            element.BeginAnimation(UIElement.OpacityProperty, null);
            if (element.RenderTransform is Animatable animatable)
            {
                animatable.BeginAnimation(TranslateTransform.XProperty, null);
                animatable.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                animatable.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            }
            element.RenderTransform = null;
            element.Effect = null;
            element.Opacity = 1.0;
            if (textBlock != null)
            {
                textBlock.Text = fullText;
            }

            switch (mode)
            {
                case MessageAnimationMode.Slide:
                {
                    double elemW = element.ActualWidth > 10.0
                        ? element.ActualWidth
                        : (textBlock?.ActualWidth > 10.0 ? textBlock.ActualWidth + 24.0 : (fullText.Length * 8.5 + 24.0));
                    double elementHalfW = elemW / 2.0;

                    // Slide smoothly into the Center from completely outside the Notch edge
                    double startX = isRtl ? (halfNotch + elementHalfW + 15.0) : -(halfNotch + elementHalfW + 15.0);
                    var trans = new TranslateTransform(startX, 0.0);
                    element.RenderTransform = trans;
                    element.Opacity = 0.0;

                    var animX = new DoubleAnimation(startX, 0.0, new Duration(TimeSpan.FromSeconds(baseDurationSec)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animO = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 0.75)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    trans.BeginAnimation(TranslateTransform.XProperty, animX);
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }

                case MessageAnimationMode.Fade:
                {
                    element.Opacity = 0.0;
                    var scale = new ScaleTransform(0.85, 0.85);
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = scale;

                    var animO = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.15)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animS = new DoubleAnimation(0.85, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.15)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, animS);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, animS);
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }

                case MessageAnimationMode.TypeOn:
                {
                    if (textBlock != null && !string.IsNullOrEmpty(fullText))
                    {
                        textBlock.Text = "|";
                        int idx = 0;
                        double charIntervalMs = Math.Max(22.0, 65.0 / speed);

                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(charIntervalMs) };
                        timer.Tick += (s, e) =>
                        {
                            if (idx < fullText.Length)
                            {
                                idx++;
                                textBlock.Text = fullText.Substring(0, idx) + (idx < fullText.Length ? "|" : "");
                            }
                            else
                            {
                                textBlock.Text = fullText;
                                timer.Stop();
                            }
                        };
                        newTypeOnTimer = timer;
                        timer.Start();
                    }
                    break;
                }

                case MessageAnimationMode.Scroll:
                {
                    // Calculate element's actual half-width so it enters and exits cleanly at the true notch edges
                    double elemW = element.ActualWidth > 10.0
                        ? element.ActualWidth
                        : (textBlock?.ActualWidth > 10.0 ? textBlock.ActualWidth + 24.0 : (fullText.Length * 8.5 + 24.0));
                    double elementHalfW = elemW / 2.0;

                    // Travel continuously from right edge completely across to left edge (classic marquee)
                    double travelX = halfNotch + elementHalfW + 10.0;
                    double fromX = travelX;
                    double toX = -travelX;

                    var trans = new TranslateTransform(fromX, 0.0);
                    element.RenderTransform = trans;

                    double totalDistance = travelX * 2.0;
                    double speedPxPerSec = Math.Max(28.0, 70.0 * speed);
                    double scrollDurationSec = totalDistance / speedPxPerSec;

                    var animScroll = new DoubleAnimation(fromX, toX, new Duration(TimeSpan.FromSeconds(scrollDurationSec)))
                    {
                        RepeatBehavior = RepeatBehavior.Forever,
                        AutoReverse = false
                    };
                    trans.BeginAnimation(TranslateTransform.XProperty, animScroll);
                    break;
                }

                case MessageAnimationMode.Blur:
                {
                    var blur = new BlurEffect { Radius = 20.0 };
                    element.Effect = blur;
                    element.Opacity = 0.1;

                    var animBlur = new DoubleAnimation(20.0, 0.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.2)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animO = new DoubleAnimation(0.1, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 0.9)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    animBlur.Completed += (_, _) => { element.Effect = null; };

                    blur.BeginAnimation(BlurEffect.RadiusProperty, animBlur);
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }

                case MessageAnimationMode.Pulse:
                {
                    var scale = new ScaleTransform(0.76, 0.76);
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = scale;
                    element.Opacity = 0.15;

                    var animScale = new DoubleAnimation(0.76, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.15)))
                    {
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.45 }
                    };
                    var animO = new DoubleAnimation(0.15, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 0.7)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };

                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }
            }
        }
    }
}
