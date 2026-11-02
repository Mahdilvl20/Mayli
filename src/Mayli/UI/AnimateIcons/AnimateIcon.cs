using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace WindowsBuddy.UI.AnimateIcons
{
    /// <summary>
    /// Highly optimized, hardware-accelerated animated vector icon control
    /// inspired by @animateicons/react (AnimateIcons.in / Lucide / Hugeicons).
    /// Zero CPU at idle, no background timers, purely GPU composition animated via direct BeginAnimation.
    /// </summary>
    public sealed class AnimateIcon : Viewbox
    {
        public static readonly DependencyProperty IconTypeProperty =
            DependencyProperty.Register(nameof(IconType), typeof(AnimateIconType), typeof(AnimateIcon),
                new PropertyMetadata(AnimateIconType.Settings, OnVisualPropertyChanged));

        public static readonly DependencyProperty StrokeBrushProperty =
            DependencyProperty.Register(nameof(StrokeBrush), typeof(Brush), typeof(AnimateIcon),
                new PropertyMetadata(Brushes.White, OnBrushPropertyChanged));

        public static readonly DependencyProperty StrokeThicknessProperty =
            DependencyProperty.Register(nameof(StrokeThickness), typeof(double), typeof(AnimateIcon),
                new PropertyMetadata(2.0, OnVisualPropertyChanged));

        public static readonly DependencyProperty AutoAnimateOnHoverProperty =
            DependencyProperty.Register(nameof(AutoAnimateOnHover), typeof(bool), typeof(AnimateIcon),
                new PropertyMetadata(true));

        public AnimateIconType IconType
        {
            get => (AnimateIconType)GetValue(IconTypeProperty);
            set => SetValue(IconTypeProperty, value);
        }

        public Brush StrokeBrush
        {
            get => (Brush)GetValue(StrokeBrushProperty);
            set => SetValue(StrokeBrushProperty, value);
        }

        public double StrokeThickness
        {
            get => (double)GetValue(StrokeThicknessProperty);
            set => SetValue(StrokeThicknessProperty, value);
        }

        public bool AutoAnimateOnHover
        {
            get => (bool)GetValue(AutoAnimateOnHoverProperty);
            set => SetValue(AutoAnimateOnHoverProperty, value);
        }

        private readonly Canvas _canvas;
        private Action _playAction;
        private bool _isPlaying;

        public AnimateIcon()
        {
            Width = 24;
            Height = 24;
            Stretch = Stretch.Uniform;
            IsHitTestVisible = true;

            _canvas = new Canvas
            {
                Width = 24,
                Height = 24,
                Background = Brushes.Transparent,
                IsHitTestVisible = true
            };
            Child = _canvas;

            MouseEnter += (_, _) =>
            {
                if (AutoAnimateOnHover)
                {
                    Play();
                }
            };

            BuildIcon();
        }

        public AnimateIcon(AnimateIconType type, double size = 24, Brush brush = null) : this()
        {
            Width = size;
            Height = size;
            IconType = type;
            if (brush != null) StrokeBrush = brush;
        }

        private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AnimateIcon icon)
            {
                icon.BuildIcon();
            }
        }

        private static void OnBrushPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AnimateIcon icon)
            {
                icon.UpdateBrushes();
            }
        }

        public void Play()
        {
            if (_isPlaying) return;
            _isPlaying = true;
            try
            {
                _playAction?.Invoke();
            }
            catch
            {
                _isPlaying = false;
            }
        }

        private void UpdateBrushes()
        {
            Brush b = StrokeBrush ?? Brushes.White;
            foreach (UIElement elem in _canvas.Children)
            {
                if (elem is Shape shape)
                {
                    if (shape.Tag as string == "fillOnly")
                    {
                        shape.Fill = b;
                    }
                    else
                    {
                        shape.Stroke = b;
                    }
                }
            }
        }

        private Path CreatePath(string geometryData, bool fillOnly = false)
        {
            Brush b = StrokeBrush ?? Brushes.White;
            var path = new Path
            {
                Data = Geometry.Parse(geometryData),
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Tag = fillOnly ? "fillOnly" : "stroke",
                IsHitTestVisible = false
            };

            if (fillOnly)
            {
                path.Fill = b;
                path.Stroke = null;
            }
            else
            {
                path.Stroke = b;
                path.StrokeThickness = StrokeThickness;
                path.Fill = null;
            }

            return path;
        }

        private void BuildIcon()
        {
            _canvas.Children.Clear();
            _playAction = null;
            _isPlaying = false;

            switch (IconType)
            {
                case AnimateIconType.CopyCheck:
                    BuildCopyCheck();
                    break;
                case AnimateIconType.Settings:
                    BuildSettings();
                    break;
                case AnimateIconType.Palette:
                    BuildPalette();
                    break;
                case AnimateIconType.Close:
                    BuildClose();
                    break;
                case AnimateIconType.Bell:
                    BuildBell();
                    break;
                case AnimateIconType.Home:
                    BuildHome();
                    break;
                case AnimateIconType.Clock:
                    BuildClock();
                    break;
                case AnimateIconType.Timer:
                    BuildTimer();
                    break;
                case AnimateIconType.Activity:
                    BuildActivity();
                    break;
                case AnimateIconType.Bluetooth:
                    BuildBluetooth();
                    break;
                case AnimateIconType.Volume:
                    BuildVolume();
                    break;
                case AnimateIconType.Sparkles:
                    BuildSparkles();
                    break;
                case AnimateIconType.Message:
                    BuildMessage();
                    break;
                case AnimateIconType.Notes:
                    BuildNotes();
                    break;
                case AnimateIconType.FileShelf:
                    BuildFileShelf();
                    break;
                case AnimateIconType.Links:
                    BuildLinks();
                    break;
                case AnimateIconType.Weather:
                    BuildWeather();
                    break;
            }
        }

        #region Icon Builders & Keyframe Animations

        private void BuildCopyCheck()
        {
            var backCard = CreatePath("M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2");
            var backTrans = new TranslateTransform(0, 0);
            backCard.RenderTransform = backTrans;
            _canvas.Children.Add(backCard);

            var frontCard = CreatePath("M10 8h10a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H10a2 2 0 0 1-2-2V10a2 2 0 0 1 2-2z");
            var frontTrans = new TranslateTransform(0, 0);
            frontCard.RenderTransform = frontTrans;
            _canvas.Children.Add(frontCard);

            var check = CreatePath("M12 15l2 2 4-4");
            var checkScale = new ScaleTransform(1, 1, 15, 15);
            check.RenderTransform = checkScale;
            _canvas.Children.Add(check);

            _playAction = () =>
            {
                var bxAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                bxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                bxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-2.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                bxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
                bxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))));
                bxAnim.Completed += (_, _) => _isPlaying = false;

                var fxAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                fxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                fxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(2.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                fxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
                fxAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))));

                var ckScale = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                ckScale.KeyFrames.Add(new SplineDoubleKeyFrame(0.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(60))));
                ckScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
                ckScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(550))));

                var opAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                opAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(40))));
                opAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));

                backTrans.BeginAnimation(TranslateTransform.XProperty, bxAnim);
                backTrans.BeginAnimation(TranslateTransform.YProperty, bxAnim);
                frontTrans.BeginAnimation(TranslateTransform.XProperty, fxAnim);
                frontTrans.BeginAnimation(TranslateTransform.YProperty, fxAnim);
                checkScale.BeginAnimation(ScaleTransform.ScaleXProperty, ckScale);
                checkScale.BeginAnimation(ScaleTransform.ScaleYProperty, ckScale);
                check.BeginAnimation(UIElement.OpacityProperty, opAnim);
            };
        }

        private void BuildSettings()
        {
            var gear = CreatePath("M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915");
            var gearRotate = new RotateTransform(0, 12, 12);
            var gearScale = new ScaleTransform(1, 1, 12, 12);
            var gearGroup = new TransformGroup();
            gearGroup.Children.Add(gearRotate);
            gearGroup.Children.Add(gearScale);
            gear.RenderTransform = gearGroup;
            _canvas.Children.Add(gear);

            var circle = CreatePath("M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z");
            var circleScale = new ScaleTransform(1, 1, 12, 12);
            circle.RenderTransform = circleScale;
            _canvas.Children.Add(circle);

            _playAction = () =>
            {
                var rotAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(38, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(540))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800))));
                rotAnim.Completed += (_, _) => _isPlaying = false;

                var scAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                scAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));
                scAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(800))));

                var cScale = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                cScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                cScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.28, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
                cScale.KeyFrames.Add(new SplineDoubleKeyFrame(0.90, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
                cScale.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(750))));

                gearRotate.BeginAnimation(RotateTransform.AngleProperty, rotAnim);
                gearScale.BeginAnimation(ScaleTransform.ScaleXProperty, scAnim);
                gearScale.BeginAnimation(ScaleTransform.ScaleYProperty, scAnim);
                circleScale.BeginAnimation(ScaleTransform.ScaleXProperty, cScale);
                circleScale.BeginAnimation(ScaleTransform.ScaleYProperty, cScale);
            };
        }

        private void BuildPalette()
        {
            var palette = CreatePath("M12 22a1 1 0 0 1 0-20 10 9 0 0 1 10 9 5 5 0 0 1-5 5h-2.25a1.75 1.75 0 0 0-1.4 2.8l.3.4a1.75 1.75 0 0 1-1.4 2.8z");
            var rot = new RotateTransform(0, 12, 12);
            palette.RenderTransform = rot;
            _canvas.Children.Add(palette);

            var d1 = CreatePath("M6.5 12.5m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);
            var d2 = CreatePath("M8.5 7.5m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);
            var d3 = CreatePath("M13.5 6.5m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);
            var d4 = CreatePath("M17.5 10.5m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);

            var s1 = new ScaleTransform(1, 1, 6.5, 12.5); d1.RenderTransform = s1;
            var s2 = new ScaleTransform(1, 1, 8.5, 7.5); d2.RenderTransform = s2;
            var s3 = new ScaleTransform(1, 1, 13.5, 6.5); d3.RenderTransform = s3;
            var s4 = new ScaleTransform(1, 1, 17.5, 10.5); d4.RenderTransform = s4;

            _canvas.Children.Add(d1);
            _canvas.Children.Add(d2);
            _canvas.Children.Add(d3);
            _canvas.Children.Add(d4);

            _playAction = () =>
            {
                var rotAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(560))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(750))));
                rotAnim.Completed += (_, _) => _isPlaying = false;

                rot.BeginAnimation(RotateTransform.AngleProperty, rotAnim);

                ScaleTransform[] dots = { s1, s2, s3, s4 };
                for (int i = 0; i < dots.Length; i++)
                {
                    int delay = i * 60;
                    var dAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 80))));
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 220))));
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 380))));
                    dots[i].BeginAnimation(ScaleTransform.ScaleXProperty, dAnim);
                    dots[i].BeginAnimation(ScaleTransform.ScaleYProperty, dAnim);
                }
            };
        }

        private void BuildClose()
        {
            var l1 = CreatePath("M18 6L6 18");
            var l2 = CreatePath("M6 6L18 18");

            var tg = new TransformGroup();
            var rot = new RotateTransform(0, 12, 12);
            var scale = new ScaleTransform(1, 1, 12, 12);
            tg.Children.Add(rot);
            tg.Children.Add(scale);

            l1.RenderTransform = tg;
            l2.RenderTransform = tg;

            _canvas.Children.Add(l1);
            _canvas.Children.Add(l2);

            _playAction = () =>
            {
                var rotAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(90, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420)), new KeySpline(0.2, 0, 0.2, 1.0)));
                rotAnim.Completed += (_, _) => _isPlaying = false;

                var scAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                scAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                scAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                scAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));

                rot.BeginAnimation(RotateTransform.AngleProperty, rotAnim);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, scAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, scAnim);
            };
        }

        private void BuildBell()
        {
            var body = CreatePath("M3.262 15.326A1 1 0 0 0 4 17h16a1 1 0 0 0 .74-1.673C19.41 13.956 18 12.499 18 8A6 6 0 0 0 6 8c0 4.499-1.411 5.956-2.738 7.326");
            var clapper = CreatePath("M10.268 21a2 2 0 0 0 3.464 0");

            var bodyRot = new RotateTransform(0, 12, 2);
            body.RenderTransform = bodyRot;

            var clapTrans = new TranslateTransform(0, 0);
            clapper.RenderTransform = clapTrans;

            _canvas.Children.Add(body);
            _canvas.Children.Add(clapper);

            _playAction = () =>
            {
                var rotAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-7, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(680))));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(840))));
                rotAnim.Completed += (_, _) => _isPlaying = false;

                var clapAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                clapAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                clapAnim.KeyFrames.Add(new SplineDoubleKeyFrame(2.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
                clapAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-1.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
                clapAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.9, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520))));
                clapAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(680))));
                clapAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(840))));

                bodyRot.BeginAnimation(RotateTransform.AngleProperty, rotAnim);
                clapTrans.BeginAnimation(TranslateTransform.XProperty, clapAnim);
            };
        }

        private void BuildHome()
        {
            var house = CreatePath("M3 10a2 2 0 0 1 .709-1.528l7-6a2 2 0 0 1 2.582 0l7 6A2 2 0 0 1 21 10v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z");
            var door = CreatePath("M15 21v-8a1 1 0 0 0-1-1h-4a1 1 0 0 0-1 1v8");

            var scale = new ScaleTransform(1, 1, 12, 21);
            house.RenderTransform = scale;
            door.RenderTransform = scale;

            _canvas.Children.Add(house);
            _canvas.Children.Add(door);

            _playAction = () =>
            {
                var sy = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(0.76, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))));
                sy.Completed += (_, _) => _isPlaying = false;

                var sx = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(600))));

                scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            };
        }

        private void BuildClock()
        {
            var dial = CreatePath("M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z");
            var hour = CreatePath("M12 12l4 2");
            var minute = CreatePath("M12 6v6");

            var minRot = new RotateTransform(0, 12, 12);
            minute.RenderTransform = minRot;

            _canvas.Children.Add(dial);
            _canvas.Children.Add(hour);
            _canvas.Children.Add(minute);

            _playAction = () =>
            {
                var rotAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rotAnim.KeyFrames.Add(new SplineDoubleKeyFrame(360, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700)), new KeySpline(0.2, 0.0, 0.2, 1.0)));
                rotAnim.Completed += (_, _) => _isPlaying = false;

                minRot.BeginAnimation(RotateTransform.AngleProperty, rotAnim);
            };
        }

        private void BuildTimer()
        {
            var body = CreatePath("M12 6a8 8 0 1 0 0 16 8 8 0 0 0 0-16z");
            var button = CreatePath("M10 2h4");
            var needle = CreatePath("M12 14l3-3");

            var btnTrans = new TranslateTransform(0, 0);
            button.RenderTransform = btnTrans;

            var needleRot = new RotateTransform(0, 12, 14);
            needle.RenderTransform = needleRot;

            _canvas.Children.Add(body);
            _canvas.Children.Add(button);
            _canvas.Children.Add(needle);

            _playAction = () =>
            {
                var bAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                bAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                bAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(140))));
                bAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(280))));

                var nAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                nAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
                nAnim.KeyFrames.Add(new SplineDoubleKeyFrame(360, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(720)), new KeySpline(0.2, 0.0, 0.2, 1.0)));
                nAnim.Completed += (_, _) => _isPlaying = false;

                btnTrans.BeginAnimation(TranslateTransform.YProperty, bAnim);
                needleRot.BeginAnimation(RotateTransform.AngleProperty, nAnim);
            };
        }

        private void BuildActivity()
        {
            var ecg = CreatePath("M2 12h2.49a2 2 0 0 0 1.92-1.46l2.35-8.36a.25.25 0 0 1 .48 0l5.52 19.64a.25.25 0 0 0 .48 0l2.35-8.36A2 2 0 0 1 19.52 12H22");
            var scale = new ScaleTransform(1, 1, 12, 12);
            ecg.RenderTransform = scale;
            _canvas.Children.Add(ecg);

            _playAction = () =>
            {
                var sAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.18, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.92, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(680))));
                sAnim.Completed += (_, _) => _isPlaying = false;

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, sAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, sAnim);
            };
        }

        private void BuildBluetooth()
        {
            var bt = CreatePath("M7 7l10 10-5 5V2l5 5L7 17");
            var scale = new ScaleTransform(1, 1, 12, 12);
            bt.RenderTransform = scale;
            _canvas.Children.Add(bt);

            _playAction = () =>
            {
                var sAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.22, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.94, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))));
                sAnim.Completed += (_, _) => _isPlaying = false;

                scale.BeginAnimation(ScaleTransform.ScaleXProperty, sAnim);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, sAnim);
            };
        }

        private void BuildVolume()
        {
            var horn = CreatePath("M11 4.702a.705.705 0 0 0-1.203-.498L6.413 7.587A1.4 1.4 0 0 1 5.416 8H3a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2.416a1.4 1.4 0 0 1 .997.413l3.383 3.384A.705.705 0 0 0 11 19.298z");
            var w1 = CreatePath("M16 9a5 5 0 0 1 0 6");
            var w2 = CreatePath("M19.364 18.364a9 9 0 0 0 0-12.728");

            var hornScale = new ScaleTransform(1, 1, 6.5, 12);
            horn.RenderTransform = hornScale;

            var t1 = new TranslateTransform(0, 0); w1.RenderTransform = t1;
            var t2 = new TranslateTransform(0, 0); w2.RenderTransform = t2;

            _canvas.Children.Add(horn);
            _canvas.Children.Add(w1);
            _canvas.Children.Add(w2);

            _playAction = () =>
            {
                var hAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                hAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                hAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.15, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(160))));
                hAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340))));
                hAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(520))));

                var w1Anim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                w1Anim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(40))));
                w1Anim.KeyFrames.Add(new SplineDoubleKeyFrame(1.8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
                w1Anim.KeyFrames.Add(new SplineDoubleKeyFrame(-0.4, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(400))));
                w1Anim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(560))));

                var w2Anim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                w2Anim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
                w2Anim.KeyFrames.Add(new SplineDoubleKeyFrame(2.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300))));
                w2Anim.KeyFrames.Add(new SplineDoubleKeyFrame(-0.3, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(480))));
                w2Anim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(640))));
                w2Anim.Completed += (_, _) => _isPlaying = false;

                hornScale.BeginAnimation(ScaleTransform.ScaleXProperty, hAnim);
                hornScale.BeginAnimation(ScaleTransform.ScaleYProperty, hAnim);
                t1.BeginAnimation(TranslateTransform.XProperty, w1Anim);
                t2.BeginAnimation(TranslateTransform.XProperty, w2Anim);
            };
        }

        private void BuildSparkles()
        {
            var mainStar = CreatePath("M9.937 15.5A2 2 0 0 0 8.5 14.063l-6.135-1.582a.5.5 0 0 1 0-.962L8.5 9.936A2 2 0 0 0 9.937 8.5l1.582-6.135a.5.5 0 0 1 .963 0L14.063 8.5A2 2 0 0 0 15.5 9.937l6.135 1.581a.5.5 0 0 1 0 .964L15.5 14.063a2 2 0 0 0-1.437 1.437l-1.582 6.135a.5.5 0 0 1-.963 0z");
            var smallStar = CreatePath("M20 3v4M22 5h-4");

            var tg = new TransformGroup();
            var rot = new RotateTransform(0, 12, 12);
            var sc = new ScaleTransform(1, 1, 12, 12);
            tg.Children.Add(rot);
            tg.Children.Add(sc);
            mainStar.RenderTransform = tg;

            _canvas.Children.Add(mainStar);
            _canvas.Children.Add(smallStar);

            _playAction = () =>
            {
                var rAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(45, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650))));
                rAnim.Completed += (_, _) => _isPlaying = false;

                var sAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.25, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(320))));
                sAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650))));

                rot.BeginAnimation(RotateTransform.AngleProperty, rAnim);
                sc.BeginAnimation(ScaleTransform.ScaleXProperty, sAnim);
                sc.BeginAnimation(ScaleTransform.ScaleYProperty, sAnim);
            };
        }

        private void BuildMessage()
        {
            var bubble = CreatePath("M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z");
            var scale = new ScaleTransform(1, 1, 4, 21); // Tail origin
            bubble.RenderTransform = scale;
            _canvas.Children.Add(bubble);

            // 3 Message dots
            var d1 = CreatePath("M8 10m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);
            var d2 = CreatePath("M12 10m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);
            var d3 = CreatePath("M16 10m-1 0a1 1 0 1 0 2 0 1 1 0 1 0-2 0", true);

            var s1 = new ScaleTransform(1, 1, 8, 10); d1.RenderTransform = s1;
            var s2 = new ScaleTransform(1, 1, 12, 10); d2.RenderTransform = s2;
            var s3 = new ScaleTransform(1, 1, 16, 10); d3.RenderTransform = s3;

            _canvas.Children.Add(d1);
            _canvas.Children.Add(d2);
            _canvas.Children.Add(d3);

            _playAction = () =>
            {
                var sy = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(0.78, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.14, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))));
                sy.Completed += (_, _) => _isPlaying = false;

                var sx = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.12, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(0.95, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))));

                scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);

                ScaleTransform[] dots = { s1, s2, s3 };
                for (int i = 0; i < dots.Length; i++)
                {
                    int delay = i * 70;
                    var dAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay))));
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.2, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 80))));
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 200))));
                    dAnim.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(delay + 360))));
                    dots[i].BeginAnimation(ScaleTransform.ScaleXProperty, dAnim);
                    dots[i].BeginAnimation(ScaleTransform.ScaleYProperty, dAnim);
                }
            };
        }

        private void BuildNotes()
        {
            var pad = CreatePath("M16 3H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8z");
            var fold = CreatePath("M15 3v5h5");
            var line1 = CreatePath("M9 13h6");
            var line2 = CreatePath("M9 17h4");

            var rot = new RotateTransform(0, 12, 12);
            pad.RenderTransform = rot;
            fold.RenderTransform = rot;
            line1.RenderTransform = rot;
            line2.RenderTransform = rot;

            _canvas.Children.Add(pad);
            _canvas.Children.Add(fold);
            _canvas.Children.Add(line1);
            _canvas.Children.Add(line2);

            _playAction = () =>
            {
                var rAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-8, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(420))));
                rAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650))));
                rAnim.Completed += (_, _) => _isPlaying = false;

                rot.BeginAnimation(RotateTransform.AngleProperty, rAnim);
            };
        }

        private void BuildFileShelf()
        {
            var folder = CreatePath("M4 20h16a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-7.93a2 2 0 0 1-1.66-.9l-.82-1.2A2 2 0 0 0 7.93 3H4a2 2 0 0 0-2 2v13a2 2 0 0 0 2 2z");
            var scale = new ScaleTransform(1, 1, 12, 20);
            folder.RenderTransform = scale;
            _canvas.Children.Add(folder);

            _playAction = () =>
            {
                var sy = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(0.82, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.10, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))));
                sy.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))));
                sy.Completed += (_, _) => _isPlaying = false;

                var sx = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.08, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180))));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(0.96, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(380))));
                sx.KeyFrames.Add(new SplineDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(580))));

                scale.BeginAnimation(ScaleTransform.ScaleYProperty, sy);
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
            };
        }

        private void BuildLinks()
        {
            var l1 = CreatePath("M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71");
            var l2 = CreatePath("M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71");

            var t1 = new TranslateTransform(0, 0); l1.RenderTransform = t1;
            var t2 = new TranslateTransform(0, 0); l2.RenderTransform = t2;

            _canvas.Children.Add(l1);
            _canvas.Children.Add(l2);

            _playAction = () =>
            {
                var a1 = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                a1.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                a1.KeyFrames.Add(new SplineDoubleKeyFrame(1.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                a1.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450))));
                a1.Completed += (_, _) => _isPlaying = false;

                var a2 = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                a2.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                a2.KeyFrames.Add(new SplineDoubleKeyFrame(-1.6, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(200))));
                a2.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(450))));

                t1.BeginAnimation(TranslateTransform.XProperty, a1);
                t1.BeginAnimation(TranslateTransform.YProperty, a1);
                t2.BeginAnimation(TranslateTransform.XProperty, a2);
                t2.BeginAnimation(TranslateTransform.YProperty, a2);
            };
        }

        private void BuildWeather()
        {
            var cloud = CreatePath("M17.5 19H9a7 7 0 1 1 6.71-9h1.79a4.5 4.5 0 1 1 0 9z");
            var trans = new TranslateTransform(0, 0);
            cloud.RenderTransform = trans;
            _canvas.Children.Add(cloud);

            _playAction = () =>
            {
                var yAnim = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
                yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
                yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(-2.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(250))));
                yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0.5, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(500))));
                yAnim.KeyFrames.Add(new SplineDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(700))));
                yAnim.Completed += (_, _) => _isPlaying = false;

                trans.BeginAnimation(TranslateTransform.YProperty, yAnim);
            };
        }

        #endregion
    }
}
