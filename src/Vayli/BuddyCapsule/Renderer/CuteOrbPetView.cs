using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Renderer
{
    public enum OrbMood
    {
        Idle,       // عادی / آرام
        Happy,      // شاد / خندان
        Angry,      // اخم / عصبانی
        Love,       // دلبر / عاشقانه
        Wink,       // چشمک
        Surprised,  // تعجب / شگفت‌زده
        Sleepy,     // خواب‌آلود
        Sad,        // غمگین / پکر
        Thinking    // متفکر
    }

    /// <summary>
    /// Vector-rendered, hardware-accelerated animated cute companion Orb.
    /// Based directly on the user's master SVG design with real-time vector morphing,
    /// expressive moods (frown, happy, wink, love, surprised, sleepy), cursor tracking,
    /// natural blinking, breathing physics, and particle emitters.
    /// </summary>
    public class CuteOrbPetView : Grid
    {
        private const double BaseCoordSize = 128.0;

        // Visual layers
        private readonly Canvas _rootCanvas;
        private readonly Ellipse _ambientGlow;
        private readonly Ellipse _orbBody;
        private readonly Ellipse _highlight;
        private readonly Canvas _faceCanvas;
        private readonly Canvas _particlesCanvas;

        // Face elements
        private readonly Ellipse _leftBlush;
        private readonly Ellipse _rightBlush;
        private readonly Path _leftBrow;
        private readonly Path _rightBrow;
        private readonly Canvas _leftEyeContainer;
        private readonly Canvas _rightEyeContainer;
        private readonly Path _mouthPath;

        // Specific eye shapes for instant mood morphing
        private readonly Canvas _leftNormalEyeGroup;
        private readonly Canvas _rightNormalEyeGroup;
        private readonly Path _leftHappyEye;
        private readonly Path _rightHappyEye;
        private readonly Path _leftAngryEye;
        private readonly Path _rightAngryEye;
        private readonly Path _leftHeartEye;
        private readonly Path _rightHeartEye;
        private readonly Path _leftSleepyEye;
        private readonly Path _rightSleepyEye;
        private readonly Canvas _leftSurprisedEye;
        private readonly Canvas _rightSurprisedEye;

        // Transforms
        private readonly ScaleTransform _bodySquishTransform;
        private readonly TranslateTransform _bodyFloatTransform;
        private readonly TranslateTransform _faceParallaxTransform;
        private readonly ScaleTransform _leftEyeBlinkTransform;
        private readonly ScaleTransform _rightEyeBlinkTransform;
        private readonly RotateTransform _headTiltTransform;

        // Animation Loop & Physics
        private readonly DispatcherTimer _lifeTimer;
        private readonly Random _rng = new();
        private double _breathPhase;
        private double _lookTargetX, _lookTargetY;
        private double _lookCurrentX, _lookCurrentY;
        private double _squishX = 1.0, _squishY = 1.0;
        private double _squishVelX, _squishVelY;
        private int _blinkCountdownTicks = 90;
        private int _blinkStep = -1;
        private int _particleEmitCounter = 0;

        private OrbMood _currentMood = OrbMood.Idle;
        private readonly List<ParticleItem> _particles = new();

        public OrbMood Mood
        {
            get => _currentMood;
            set => SetMood(value);
        }

        public bool FollowCursor { get; set; } = true;
        public bool EnableBreathing { get; set; } = true;
        public bool EnableParticles { get; set; } = true;
        public bool InteractiveClick { get; set; } = true;

        public event Action<OrbMood> MoodChanged;
        public event Action Clicked;

        public CuteOrbPetView(OrbMood initialMood = OrbMood.Idle, double size = 64.0)
        {
            Width = size;
            Height = size;
            ClipToBounds = false;

            var viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                Width = size,
                Height = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _rootCanvas = new Canvas
            {
                Width = BaseCoordSize,
                Height = BaseCoordSize,
                ClipToBounds = false,
                Background = Brushes.Transparent
            };

            // Head Tilt for playful winking/thinking
            _headTiltTransform = new RotateTransform(0, 64, 64);
            _rootCanvas.RenderTransform = _headTiltTransform;

            // -------------------------------------------------------------
            // 1. Ambient Glow (cx=64, cy=64, r=55)
            // -------------------------------------------------------------
            _ambientGlow = new Ellipse
            {
                Width = 110,
                Height = 110,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(_ambientGlow, 9);
            Canvas.SetTop(_ambientGlow, 9);
            UpdateGlowBrush(Color.FromRgb(99, 233, 255), 0.38);

            var glowBlur = new BlurEffect { Radius = 10, RenderingBias = RenderingBias.Performance };
            _ambientGlow.Effect = glowBlur;
            _rootCanvas.Children.Add(_ambientGlow);

            // -------------------------------------------------------------
            // 2. Orb Body (cx=64, cy=64, r=48) with Breathing & Spring Squish
            // -------------------------------------------------------------
            var orbContainer = new Canvas
            {
                Width = BaseCoordSize,
                Height = BaseCoordSize,
                ClipToBounds = false
            };

            var orbTransformGroup = new TransformGroup();
            _bodySquishTransform = new ScaleTransform(1.0, 1.0, 64, 64);
            _bodyFloatTransform = new TranslateTransform(0, 0);
            orbTransformGroup.Children.Add(_bodySquishTransform);
            orbTransformGroup.Children.Add(_bodyFloatTransform);
            orbContainer.RenderTransform = orbTransformGroup;

            _orbBody = new Ellipse
            {
                Width = 96,
                Height = 96,
                Cursor = Cursors.Hand
            };
            Canvas.SetLeft(_orbBody, 16);
            Canvas.SetTop(_orbBody, 16);
            ApplyOrbGradient(OrbMood.Idle);
            orbContainer.Children.Add(_orbBody);

            // -------------------------------------------------------------
            // 3. Specular Highlight (cx=45, cy=34, rx=23, ry=12, rot -30)
            // -------------------------------------------------------------
            _highlight = new Ellipse
            {
                Width = 46,
                Height = 24,
                Fill = new SolidColorBrush(Color.FromArgb((byte)(0.22 * 255), 255, 255, 255)),
                IsHitTestVisible = false,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform(-30)
            };
            Canvas.SetLeft(_highlight, 22);
            Canvas.SetTop(_highlight, 22);
            orbContainer.Children.Add(_highlight);

            // -------------------------------------------------------------
            // 4. Face Layer (Parallax Gaze Tracking cx=64, cy=64)
            // -------------------------------------------------------------
            _faceCanvas = new Canvas
            {
                Width = BaseCoordSize,
                Height = BaseCoordSize,
                IsHitTestVisible = false
            };
            _faceParallaxTransform = new TranslateTransform(0, 0);
            _faceCanvas.RenderTransform = _faceParallaxTransform;

            // Rosy Blushing Cheeks
            var blushBrush = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            blushBrush.GradientStops.Add(new GradientStop(Color.FromArgb(175, 255, 105, 140), 0.0));
            blushBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 105, 140), 1.0));
            blushBrush.Freeze();

            _leftBlush = new Ellipse
            {
                Width = 16,
                Height = 9,
                Fill = blushBrush,
                Opacity = 0.0
            };
            Canvas.SetLeft(_leftBlush, 27);
            Canvas.SetTop(_leftBlush, 67);
            _faceCanvas.Children.Add(_leftBlush);

            _rightBlush = new Ellipse
            {
                Width = 16,
                Height = 9,
                Fill = blushBrush,
                Opacity = 0.0
            };
            Canvas.SetLeft(_rightBlush, 85);
            Canvas.SetTop(_rightBlush, 67);
            _faceCanvas.Children.Add(_rightBlush);

            // Eyebrows (Stroke #15213A, 3.8px round cap)
            var browBrush = new SolidColorBrush(Color.FromRgb(21, 33, 58));
            browBrush.Freeze();

            _leftBrow = new Path
            {
                Stroke = browBrush,
                StrokeThickness = 3.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.0
            };
            _faceCanvas.Children.Add(_leftBrow);

            _rightBrow = new Path
            {
                Stroke = browBrush,
                StrokeThickness = 3.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.0
            };
            _faceCanvas.Children.Add(_rightBrow);

            // Eyes Containers with Blinking ScaleTransforms
            _leftEyeBlinkTransform = new ScaleTransform(1.0, 1.0, 46, 61);
            _rightEyeBlinkTransform = new ScaleTransform(1.0, 1.0, 82, 61);

            _leftEyeContainer = new Canvas
            {
                Width = BaseCoordSize,
                Height = BaseCoordSize,
                RenderTransform = _leftEyeBlinkTransform
            };
            _rightEyeContainer = new Canvas
            {
                Width = BaseCoordSize,
                Height = BaseCoordSize,
                RenderTransform = _rightEyeBlinkTransform
            };

            // Build all eye state components
            BuildEyeElements(out _leftNormalEyeGroup, out _rightNormalEyeGroup,
                             out _leftHappyEye, out _rightHappyEye,
                             out _leftAngryEye, out _rightAngryEye,
                             out _leftHeartEye, out _rightHeartEye,
                             out _leftSleepyEye, out _rightSleepyEye,
                             out _leftSurprisedEye, out _rightSurprisedEye);

            _leftEyeContainer.Children.Add(_leftNormalEyeGroup);
            _leftEyeContainer.Children.Add(_leftHappyEye);
            _leftEyeContainer.Children.Add(_leftAngryEye);
            _leftEyeContainer.Children.Add(_leftHeartEye);
            _leftEyeContainer.Children.Add(_leftSleepyEye);
            _leftEyeContainer.Children.Add(_leftSurprisedEye);

            _rightEyeContainer.Children.Add(_rightNormalEyeGroup);
            _rightEyeContainer.Children.Add(_rightHappyEye);
            _rightEyeContainer.Children.Add(_rightAngryEye);
            _rightEyeContainer.Children.Add(_rightHeartEye);
            _rightEyeContainer.Children.Add(_rightSleepyEye);
            _rightEyeContainer.Children.Add(_rightSurprisedEye);

            _faceCanvas.Children.Add(_leftEyeContainer);
            _faceCanvas.Children.Add(_rightEyeContainer);

            // Mouth Path
            _mouthPath = new Path
            {
                Stroke = browBrush,
                StrokeThickness = 3.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = 0.0
            };
            _faceCanvas.Children.Add(_mouthPath);

            orbContainer.Children.Add(_faceCanvas);
            _rootCanvas.Children.Add(orbContainer);

            // -------------------------------------------------------------
            // 5. Particles Overlay (Hearts, Sparkles, Zzz)
            // -------------------------------------------------------------
            _particlesCanvas = new Canvas
            {
                Width = BaseCoordSize,
                Height = BaseCoordSize,
                IsHitTestVisible = false
            };
            _rootCanvas.Children.Add(_particlesCanvas);

            viewbox.Child = _rootCanvas;
            Children.Add(viewbox);

            // Apply initial mood
            SetMood(initialMood);

            // -------------------------------------------------------------
            // 6. Interactive Physics & Animation Timer (~30 FPS / 33ms)
            // -------------------------------------------------------------
            _lifeTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _lifeTimer.Tick += OnLifeTimerTick;
            _lifeTimer.Start();

            // Interactions
            MouseEnter += (_, _) =>
            {
                // Perk up with curious look
                _squishVelY += 0.05;
                if (_currentMood == OrbMood.Idle)
                {
                    _leftBlush.Opacity = 0.35;
                    _rightBlush.Opacity = 0.35;
                }
            };
            MouseLeave += (_, _) =>
            {
                if (_currentMood == OrbMood.Idle)
                {
                    _leftBlush.Opacity = 0.0;
                    _rightBlush.Opacity = 0.0;
                }
            };

            MouseLeftButtonDown += (_, e) =>
            {
                if (!InteractiveClick) return;
                e.Handled = true;
                Poke();
                Clicked?.Invoke();
            };
        }

        private void BuildEyeElements(
            out Canvas leftNormal, out Canvas rightNormal,
            out Path leftHappy, out Path rightHappy,
            out Path leftAngry, out Path rightAngry,
            out Path leftHeart, out Path rightHeart,
            out Path leftSleepy, out Path rightSleepy,
            out Canvas leftSurprised, out Canvas rightSurprised)
        {
            var eyeGradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            eyeGradient.GradientStops.Add(new GradientStop(Color.FromRgb(23, 38, 61), 0.0));
            eyeGradient.GradientStops.Add(new GradientStop(Color.FromRgb(8, 13, 27), 1.0));
            eyeGradient.Freeze();

            var lineCapDarkBrush = new SolidColorBrush(Color.FromRgb(21, 33, 58));
            lineCapDarkBrush.Freeze();

            var shineBrush = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            shineBrush.Freeze();

            var secondaryShineBrush = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            secondaryShineBrush.Freeze();

            // A) Normal Eyes (ellipse cx=46, cy=61, rx=8, ry=12 and cx=82, cy=61)
            leftNormal = new Canvas { Width = BaseCoordSize, Height = BaseCoordSize };
            var le = new Ellipse { Width = 16, Height = 24, Fill = eyeGradient };
            Canvas.SetLeft(le, 38); Canvas.SetTop(le, 49);
            var ls1 = new Ellipse { Width = 5, Height = 5, Fill = shineBrush };
            Canvas.SetLeft(ls1, 40.5); Canvas.SetTop(ls1, 53.5);
            var ls2 = new Ellipse { Width = 2.4, Height = 2.4, Fill = secondaryShineBrush };
            Canvas.SetLeft(ls2, 47); Canvas.SetTop(ls2, 64);
            leftNormal.Children.Add(le);
            leftNormal.Children.Add(ls1);
            leftNormal.Children.Add(ls2);

            rightNormal = new Canvas { Width = BaseCoordSize, Height = BaseCoordSize };
            var re = new Ellipse { Width = 16, Height = 24, Fill = eyeGradient };
            Canvas.SetLeft(re, 74); Canvas.SetTop(re, 49);
            var rs1 = new Ellipse { Width = 5, Height = 5, Fill = shineBrush };
            Canvas.SetLeft(rs1, 76.5); Canvas.SetTop(rs1, 53.5);
            var rs2 = new Ellipse { Width = 2.4, Height = 2.4, Fill = secondaryShineBrush };
            Canvas.SetLeft(rs2, 83); Canvas.SetTop(rs2, 64);
            rightNormal.Children.Add(re);
            rightNormal.Children.Add(rs1);
            rightNormal.Children.Add(rs2);

            // B) Happy Eyes (Adorably curved arcs ^ ^)
            leftHappy = new Path
            {
                Data = Geometry.Parse("M 37,64 Q 46,50 55,64"),
                Stroke = lineCapDarkBrush,
                StrokeThickness = 4.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };
            rightHappy = new Path
            {
                Data = Geometry.Parse("M 73,64 Q 82,50 91,64"),
                Stroke = lineCapDarkBrush,
                StrokeThickness = 4.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };

            // C) Angry Eyes (Slanted inward with intense sharp expression \ /)
            leftAngry = new Path
            {
                Data = Geometry.Parse("M 37,56 Q 46,60 55,63 L 53,70 Q 46,72 38,68 Z"),
                Fill = eyeGradient,
                Visibility = Visibility.Collapsed
            };
            rightAngry = new Path
            {
                Data = Geometry.Parse("M 73,63 Q 82,60 91,56 L 90,68 Q 82,72 75,70 Z"),
                Fill = eyeGradient,
                Visibility = Visibility.Collapsed
            };

            // D) Heart Eyes (Romantic glowing ruby hearts ♥ ♥)
            var heartBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };
            heartBrush.GradientStops.Add(new GradientStop(Color.FromRgb(255, 46, 99), 0.0));
            heartBrush.GradientStops.Add(new GradientStop(Color.FromRgb(199, 0, 57), 1.0));
            heartBrush.Freeze();

            leftHeart = new Path
            {
                Data = Geometry.Parse("M 46,54 C 41,49 35,51 35,56 C 35,62 41,66 46,71 C 51,66 57,62 57,56 C 57,51 51,49 46,54 Z"),
                Fill = heartBrush,
                Visibility = Visibility.Collapsed
            };
            rightHeart = new Path
            {
                Data = Geometry.Parse("M 82,54 C 77,49 71,51 71,56 C 71,62 77,66 82,71 C 87,66 93,62 93,56 C 93,51 87,49 82,54 Z"),
                Fill = heartBrush,
                Visibility = Visibility.Collapsed
            };

            // E) Sleepy Eyes (Relaxed sleepy curves - -)
            leftSleepy = new Path
            {
                Data = Geometry.Parse("M 38,62 Q 46,66 54,62"),
                Stroke = lineCapDarkBrush,
                StrokeThickness = 4.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };
            rightSleepy = new Path
            {
                Data = Geometry.Parse("M 74,62 Q 82,66 90,62"),
                Stroke = lineCapDarkBrush,
                StrokeThickness = 4.2,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Visibility = Visibility.Collapsed
            };

            // F) Surprised Eyes (Extra wide and open with huge shine)
            leftSurprised = new Canvas { Width = BaseCoordSize, Height = BaseCoordSize, Visibility = Visibility.Collapsed };
            var lse = new Ellipse { Width = 20, Height = 26, Fill = eyeGradient };
            Canvas.SetLeft(lse, 36); Canvas.SetTop(lse, 48);
            var lss1 = new Ellipse { Width = 7, Height = 7, Fill = shineBrush };
            Canvas.SetLeft(lss1, 39); Canvas.SetTop(lss1, 52);
            leftSurprised.Children.Add(lse);
            leftSurprised.Children.Add(lss1);

            rightSurprised = new Canvas { Width = BaseCoordSize, Height = BaseCoordSize, Visibility = Visibility.Collapsed };
            var rse = new Ellipse { Width = 20, Height = 26, Fill = eyeGradient };
            Canvas.SetLeft(rse, 72); Canvas.SetTop(rse, 48);
            var rss1 = new Ellipse { Width = 7, Height = 7, Fill = shineBrush };
            Canvas.SetLeft(rss1, 75); Canvas.SetTop(rss1, 52);
            rightSurprised.Children.Add(rse);
            rightSurprised.Children.Add(rss1);
        }

        public void SetMood(OrbMood mood, string companionMessage = null)
        {
            _currentMood = mood;
            ApplyOrbGradient(mood);
            ApplyMoodFaceVisuals(mood);

            // Sync with PetEngine if status message given
            if (!string.IsNullOrEmpty(companionMessage))
            {
                PetEngine.Instance.SetPose(mood.ToString().ToLowerInvariant(), companionMessage);
            }

            MoodChanged?.Invoke(mood);
        }

        public void CycleNextMood()
        {
            OrbMood[] cycle =
            {
                OrbMood.Idle,
                OrbMood.Happy,
                OrbMood.Love,
                OrbMood.Wink,
                OrbMood.Surprised,
                OrbMood.Angry,
                OrbMood.Sleepy
            };
            int idx = Array.IndexOf(cycle, _currentMood);
            OrbMood next = cycle[(idx + 1) % cycle.Length];
            Poke();
            SetMood(next);
        }

        public void Poke()
        {
            // Spring squash and stretch
            _squishVelX += 0.22;
            _squishVelY -= 0.20;

            if (EnableParticles)
            {
                if (_currentMood == OrbMood.Love)
                    SpawnHeartBurst();
                else
                    SpawnSparkleBurst();
            }
        }

        private void ApplyOrbGradient(OrbMood mood)
        {
            var brush = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.35, 0.25),
                Center = new Point(0.5, 0.5),
                RadiusX = 0.75,
                RadiusY = 0.75
            };

            switch (mood)
            {
                case OrbMood.Angry:
                    // Moody heated glow with violet-crimson deep shadow
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(185, 247, 255), 0.00));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(84, 216, 232), 0.30));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(180, 80, 160), 0.70));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(80, 20, 60), 1.00));
                    UpdateGlowBrush(Color.FromRgb(255, 80, 120), 0.45);
                    break;

                case OrbMood.Love:
                    // Romantic bright cyan into magenta-purple
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(210, 248, 255), 0.00));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(100, 220, 245), 0.35));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(170, 100, 235), 0.70));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(75, 25, 95), 1.00));
                    UpdateGlowBrush(Color.FromRgb(255, 105, 210), 0.45);
                    break;

                case OrbMood.Sleepy:
                    // Calmer, softer dim blue-purple
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(160, 225, 240), 0.00));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(65, 185, 205), 0.35));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(80, 95, 200), 0.70));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(35, 26, 75), 1.00));
                    UpdateGlowBrush(Color.FromRgb(80, 180, 240), 0.25);
                    break;

                default:
                    // Standard iconic cyan-purple gradient from master SVG
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(185, 247, 255), 0.00));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(84, 216, 232), 0.35));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(100, 117, 232), 0.70));
                    brush.GradientStops.Add(new GradientStop(Color.FromRgb(48, 36, 95), 1.00));
                    UpdateGlowBrush(Color.FromRgb(99, 233, 255), 0.38);
                    break;
            }

            brush.Freeze();
            _orbBody.Fill = brush;
        }

        private void UpdateGlowBrush(Color color, double centerOpacity)
        {
            var glow = new RadialGradientBrush
            {
                Center = new Point(0.5, 0.5),
                GradientOrigin = new Point(0.5, 0.5),
                RadiusX = 0.5,
                RadiusY = 0.5
            };
            glow.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(centerOpacity * 255), color.R, color.G, color.B), 0.0));
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1.0));
            glow.Freeze();
            _ambientGlow.Fill = glow;
        }

        private void ApplyMoodFaceVisuals(OrbMood mood)
        {
            // Reset visibility
            _leftNormalEyeGroup.Visibility = Visibility.Collapsed;
            _rightNormalEyeGroup.Visibility = Visibility.Collapsed;
            _leftHappyEye.Visibility = Visibility.Collapsed;
            _rightHappyEye.Visibility = Visibility.Collapsed;
            _leftAngryEye.Visibility = Visibility.Collapsed;
            _rightAngryEye.Visibility = Visibility.Collapsed;
            _leftHeartEye.Visibility = Visibility.Collapsed;
            _rightHeartEye.Visibility = Visibility.Collapsed;
            _leftSleepyEye.Visibility = Visibility.Collapsed;
            _rightSleepyEye.Visibility = Visibility.Collapsed;
            _leftSurprisedEye.Visibility = Visibility.Collapsed;
            _rightSurprisedEye.Visibility = Visibility.Collapsed;

            _headTiltTransform.Angle = 0;

            switch (mood)
            {
                case OrbMood.Happy:
                    _leftHappyEye.Visibility = Visibility.Visible;
                    _rightHappyEye.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.88;
                    _rightBlush.Opacity = 0.88;

                    // Eyebrows raised happy
                    _leftBrow.Data = Geometry.Parse("M 34,40 Q 44,33 53,40");
                    _rightBrow.Data = Geometry.Parse("M 75,40 Q 84,33 94,40");
                    _leftBrow.Opacity = 0.85;
                    _rightBrow.Opacity = 0.85;

                    // Cute open smile
                    _mouthPath.Data = Geometry.Parse("M 56,73 Q 64,81 72,73");
                    _mouthPath.Opacity = 1.0;
                    break;

                case OrbMood.Angry: // اخم
                    _leftAngryEye.Visibility = Visibility.Visible;
                    _rightAngryEye.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.0;
                    _rightBlush.Opacity = 0.0;

                    // Sharp downward angled frown brows (\ /)
                    _leftBrow.Data = Geometry.Parse("M 33,42 Q 43,47 54,50");
                    _rightBrow.Data = Geometry.Parse("M 74,50 Q 85,47 95,42");
                    _leftBrow.Opacity = 1.0;
                    _rightBrow.Opacity = 1.0;

                    // Pouty grumpy mouth
                    _mouthPath.Data = Geometry.Parse("M 58,76 Q 64,71 70,76");
                    _mouthPath.Opacity = 1.0;
                    break;

                case OrbMood.Love:
                    _leftHeartEye.Visibility = Visibility.Visible;
                    _rightHeartEye.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.95;
                    _rightBlush.Opacity = 0.95;

                    _leftBrow.Data = Geometry.Parse("M 34,41 Q 44,34 53,41");
                    _rightBrow.Data = Geometry.Parse("M 75,41 Q 84,34 94,41");
                    _leftBrow.Opacity = 0.85;
                    _rightBrow.Opacity = 0.85;

                    // Cute kitty mouth :3
                    _mouthPath.Data = Geometry.Parse("M 56,73 Q 60,76 64,73 Q 68,76 72,73");
                    _mouthPath.Opacity = 1.0;
                    break;

                case OrbMood.Wink:
                    _leftHappyEye.Visibility = Visibility.Visible; // winking
                    _rightNormalEyeGroup.Visibility = Visibility.Visible; // wide open
                    _leftBlush.Opacity = 0.85;
                    _rightBlush.Opacity = 0.50;

                    _leftBrow.Data = Geometry.Parse("M 34,43 Q 44,38 53,43");
                    _rightBrow.Data = Geometry.Parse("M 75,39 Q 84,32 94,39");
                    _leftBrow.Opacity = 0.8;
                    _rightBrow.Opacity = 1.0;

                    _mouthPath.Data = Geometry.Parse("M 57,73 Q 64,80 72,73");
                    _mouthPath.Opacity = 1.0;
                    _headTiltTransform.Angle = -4.5; // cute head tilt
                    break;

                case OrbMood.Surprised:
                    _leftSurprisedEye.Visibility = Visibility.Visible;
                    _rightSurprisedEye.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.40;
                    _rightBlush.Opacity = 0.40;

                    // Brows raised very high
                    _leftBrow.Data = Geometry.Parse("M 34,35 Q 44,27 54,35");
                    _rightBrow.Data = Geometry.Parse("M 74,35 Q 84,27 94,35");
                    _leftBrow.Opacity = 1.0;
                    _rightBrow.Opacity = 1.0;

                    // Round 'o' mouth
                    _mouthPath.Data = Geometry.Parse("M 60,74 A 4,5 0 1 0 68,74 A 4,5 0 1 0 60,74");
                    _mouthPath.Opacity = 1.0;
                    break;

                case OrbMood.Sleepy:
                    _leftSleepyEye.Visibility = Visibility.Visible;
                    _rightSleepyEye.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.20;
                    _rightBlush.Opacity = 0.20;

                    _leftBrow.Data = Geometry.Parse("M 34,44 Q 44,42 54,44");
                    _rightBrow.Data = Geometry.Parse("M 74,44 Q 84,42 94,44");
                    _leftBrow.Opacity = 0.5;
                    _rightBrow.Opacity = 0.5;

                    _mouthPath.Data = Geometry.Parse("M 60,74 Q 64,76 68,74");
                    _mouthPath.Opacity = 0.6;
                    break;

                case OrbMood.Sad:
                    _leftNormalEyeGroup.Visibility = Visibility.Visible;
                    _rightNormalEyeGroup.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.0;
                    _rightBlush.Opacity = 0.0;

                    // Puppy inner-raised brows
                    _leftBrow.Data = Geometry.Parse("M 34,48 Q 44,43 54,39");
                    _rightBrow.Data = Geometry.Parse("M 74,39 Q 84,43 94,48");
                    _leftBrow.Opacity = 0.95;
                    _rightBrow.Opacity = 0.95;

                    _mouthPath.Data = Geometry.Parse("M 58,77 Q 64,72 70,77");
                    _mouthPath.Opacity = 0.9;
                    break;

                case OrbMood.Thinking:
                    _leftNormalEyeGroup.Visibility = Visibility.Visible;
                    _rightNormalEyeGroup.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.20;
                    _rightBlush.Opacity = 0.20;

                    // One brow up, one flat
                    _leftBrow.Data = Geometry.Parse("M 34,36 Q 44,29 54,36");
                    _rightBrow.Data = Geometry.Parse("M 75,44 Q 85,45 94,44");
                    _leftBrow.Opacity = 1.0;
                    _rightBrow.Opacity = 1.0;

                    _mouthPath.Data = Geometry.Parse("M 61,74 Q 65,73 69,75");
                    _mouthPath.Opacity = 0.8;
                    _headTiltTransform.Angle = 3.5;
                    break;

                default: // Idle
                    _leftNormalEyeGroup.Visibility = Visibility.Visible;
                    _rightNormalEyeGroup.Visibility = Visibility.Visible;
                    _leftBlush.Opacity = 0.0;
                    _rightBlush.Opacity = 0.0;

                    _leftBrow.Opacity = 0.0;
                    _rightBrow.Opacity = 0.0;

                    _mouthPath.Data = Geometry.Parse("M 60,74 Q 64,76 68,74");
                    _mouthPath.Opacity = 0.35;
                    break;
            }
        }

        private void OnLifeTimerTick(object sender, EventArgs e)
        {
            // 1. Organic Breathing & Floating Bob
            if (EnableBreathing)
            {
                double breathSpeed = (_currentMood == OrbMood.Sleepy) ? 0.03 : 0.055;
                _breathPhase += breathSpeed;

                double bobY = Math.Sin(_breathPhase) * 2.2;
                _bodyFloatTransform.Y = bobY;

                // Subtle organic pulse
                double pulse = Math.Sin(_breathPhase) * 0.018;
                _bodySquishTransform.ScaleY = _squishY + pulse;
                _bodySquishTransform.ScaleX = _squishX - pulse * 0.5;
            }
            else
            {
                _bodyFloatTransform.Y = 0;
                _bodySquishTransform.ScaleX = _squishX;
                _bodySquishTransform.ScaleY = _squishY;
            }

            // 2. Spring Physics (Elastic Squish & Poke return)
            double springK = 0.22;
            double damp = 0.78;

            _squishVelX += (1.0 - _squishX) * springK;
            _squishVelX *= damp;
            _squishX += _squishVelX;

            _squishVelY += (1.0 - _squishY) * springK;
            _squishVelY *= damp;
            _squishY += _squishVelY;

            // 3. Cursor Tracking & Parallax (Smooth gaze)
            if (FollowCursor && IsVisible && IsLoaded)
            {
                try
                {
                    if (NativeMethods.GetCursorPos(out var pt))
                    {
                        var screenCenter = PointToScreen(new Point(ActualWidth * 0.5, ActualHeight * 0.5));
                        double dx = pt.X - screenCenter.X;
                        double dy = pt.Y - screenCenter.Y;

                        // Normalize and map to spherical parallax limit (max 7px in 128-space)
                        double dist = Math.Sqrt(dx * dx + dy * dy);
                        double maxLook = 7.0;
                        if (dist > 1.0)
                        {
                            double factor = Math.Min(1.0, dist / 400.0) * maxLook;
                            _lookTargetX = (dx / dist) * factor;
                            _lookTargetY = (dy / dist) * factor;
                        }
                    }
                }
                catch
                {
                    // Fallback in case PointToScreen not ready
                }
            }
            else
            {
                _lookTargetX = 0;
                _lookTargetY = 0;
            }

            // Smooth interpolation (lerp)
            _lookCurrentX += (_lookTargetX - _lookCurrentX) * 0.18;
            _lookCurrentY += (_lookTargetY - _lookCurrentY) * 0.18;
            _faceParallaxTransform.X = _lookCurrentX;
            _faceParallaxTransform.Y = _lookCurrentY;

            // 4. Autonomous Natural Blinking
            if (_blinkStep >= 0)
            {
                _blinkStep++;
                if (_blinkStep <= 2)
                {
                    // Closing eye
                    _leftEyeBlinkTransform.ScaleY = 0.10;
                    _rightEyeBlinkTransform.ScaleY = 0.10;
                }
                else
                {
                    // Open again
                    _leftEyeBlinkTransform.ScaleY = 1.0;
                    _rightEyeBlinkTransform.ScaleY = 1.0;
                    _blinkStep = -1;
                    _blinkCountdownTicks = _rng.Next(80, 160); // 2.6s - 5.2s interval
                }
            }
            else
            {
                _blinkCountdownTicks--;
                if (_blinkCountdownTicks <= 0 && _currentMood != OrbMood.Sleepy)
                {
                    _blinkStep = 0;
                }
            }

            // 5. Update Floating Particles
            UpdateParticles();

            // Periodic mood particle spawns
            if (EnableParticles)
            {
                _particleEmitCounter++;
                if (_currentMood == OrbMood.Love && _particleEmitCounter % 32 == 0)
                {
                    SpawnHeartParticle(64 + _rng.Next(-16, 16), 55);
                }
                else if (_currentMood == OrbMood.Sleepy && _particleEmitCounter % 45 == 0)
                {
                    SpawnZzzParticle(68, 50);
                }
                else if (_currentMood == OrbMood.Happy && _particleEmitCounter % 40 == 0)
                {
                    SpawnSparkleParticle(64 + _rng.Next(-24, 24), 50 + _rng.Next(-10, 10));
                }
            }
        }

        public void SpawnHeartBurst()
        {
            for (int i = 0; i < 4; i++)
            {
                double px = 64 + _rng.Next(-20, 20);
                double py = 60 + _rng.Next(-10, 10);
                SpawnHeartParticle(px, py, burst: true);
            }
        }

        public void SpawnSparkleBurst()
        {
            for (int i = 0; i < 5; i++)
            {
                double px = 64 + _rng.Next(-26, 26);
                double py = 58 + _rng.Next(-14, 14);
                SpawnSparkleParticle(px, py);
            }
        }

        private void SpawnHeartParticle(double x, double y, bool burst = false)
        {
            var heart = new TextBlock
            {
                Text = "💖",
                FontSize = _rng.Next(11, 15),
                Opacity = 1.0,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(heart, x);
            Canvas.SetTop(heart, y);
            _particlesCanvas.Children.Add(heart);

            double vx = burst ? (_rng.NextDouble() - 0.5) * 2.5 : (_rng.NextDouble() - 0.5) * 0.8;
            double vy = burst ? -(_rng.NextDouble() * 2.2 + 1.2) : -(_rng.NextDouble() * 1.0 + 0.8);

            _particles.Add(new ParticleItem(heart, x, y, vx, vy, 0.022));
        }

        private void SpawnZzzParticle(double x, double y)
        {
            var z = new TextBlock
            {
                Text = "Z",
                FontWeight = FontWeights.Bold,
                FontSize = _rng.Next(10, 14),
                Foreground = new SolidColorBrush(Color.FromArgb(200, 185, 230, 255)),
                Opacity = 0.9,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(z, x);
            Canvas.SetTop(z, y);
            _particlesCanvas.Children.Add(z);

            _particles.Add(new ParticleItem(z, x, y, 0.4, -0.7, 0.016));
        }

        private void SpawnSparkleParticle(double x, double y)
        {
            var star = new TextBlock
            {
                Text = "✦",
                FontSize = _rng.Next(11, 15),
                Foreground = new SolidColorBrush(Color.FromRgb(255, 240, 130)),
                Opacity = 1.0,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(star, x);
            Canvas.SetTop(star, y);
            _particlesCanvas.Children.Add(star);

            double vx = (_rng.NextDouble() - 0.5) * 1.2;
            double vy = -(_rng.NextDouble() * 1.1 + 0.6);
            _particles.Add(new ParticleItem(star, x, y, vx, vy, 0.028));
        }

        private void UpdateParticles()
        {
            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                p.X += p.Vx;
                p.Y += p.Vy;
                p.Element.Opacity -= p.FadeSpeed;

                Canvas.SetLeft(p.Element, p.X);
                Canvas.SetTop(p.Element, p.Y);

                if (p.Element.Opacity <= 0)
                {
                    _particlesCanvas.Children.Remove(p.Element);
                    _particles.RemoveAt(i);
                }
            }
        }

        private sealed class ParticleItem
        {
            public FrameworkElement Element { get; }
            public double X { get; set; }
            public double Y { get; set; }
            public double Vx { get; }
            public double Vy { get; }
            public double FadeSpeed { get; }

            public ParticleItem(FrameworkElement element, double x, double y, double vx, double vy, double fadeSpeed)
            {
                Element = element;
                X = x;
                Y = y;
                Vx = vx;
                Vy = vy;
                FadeSpeed = fadeSpeed;
            }
        }
    }
}
