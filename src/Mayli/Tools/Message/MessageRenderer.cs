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
            out DispatcherTimer newTypeOnTimer)
        {
            newTypeOnTimer = null;
            if (element == null) return;

            existingTypeOnTimer?.Stop();

            double speed = Math.Clamp(speedMultiplier, 0.3, 3.0);
            double baseDurationSec = 0.45 / speed;

            // Reset transforms & effects
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
                    var trans = new TranslateTransform(-70.0, 0.0);
                    element.RenderTransform = trans;
                    element.Opacity = 0.0;

                    var animX = new DoubleAnimation(-70.0, 0.0, new Duration(TimeSpan.FromSeconds(baseDurationSec)))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animO = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 0.7)))
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
                    var animO = new DoubleAnimation(0.0, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.2)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }

                case MessageAnimationMode.TypeOn:
                {
                    if (textBlock != null && !string.IsNullOrEmpty(fullText))
                    {
                        textBlock.Text = "";
                        int idx = 0;
                        double charIntervalMs = Math.Max(20.0, 60.0 / speed);

                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(charIntervalMs) };
                        timer.Tick += (s, e) =>
                        {
                            if (idx < fullText.Length)
                            {
                                idx++;
                                textBlock.Text = fullText.Substring(0, idx);
                            }
                            else
                            {
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
                    var trans = new TranslateTransform(80.0, 0.0);
                    element.RenderTransform = trans;

                    var animScroll = new DoubleAnimation(80.0, -80.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 4.0)))
                    {
                        RepeatBehavior = RepeatBehavior.Forever,
                        AutoReverse = true,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    trans.BeginAnimation(TranslateTransform.XProperty, animScroll);
                    break;
                }

                case MessageAnimationMode.Blur:
                {
                    var blur = new BlurEffect { Radius = 16.0 };
                    element.Effect = blur;
                    element.Opacity = 0.3;

                    var animBlur = new DoubleAnimation(16.0, 0.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.2)))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    var animO = new DoubleAnimation(0.3, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 0.9)));

                    blur.BeginAnimation(BlurEffect.RadiusProperty, animBlur);
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }

                case MessageAnimationMode.Pulse:
                {
                    var scale = new ScaleTransform(0.85, 0.85);
                    element.RenderTransformOrigin = new Point(0.5, 0.5);
                    element.RenderTransform = scale;
                    element.Opacity = 0.2;

                    var animScale = new DoubleAnimation(0.85, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 1.1)))
                    {
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
                    };
                    var animO = new DoubleAnimation(0.2, 1.0, new Duration(TimeSpan.FromSeconds(baseDurationSec * 0.7)));

                    scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
                    element.BeginAnimation(UIElement.OpacityProperty, animO);
                    break;
                }
            }
        }
    }
}
