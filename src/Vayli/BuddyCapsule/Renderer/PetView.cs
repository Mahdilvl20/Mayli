using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.BuddyCapsule.Renderer
{
    public sealed class PetPoseMetadata
    {
        public Rect EyeQuad { get; set; } = new Rect(208, 130, 96, 45);
        public double HeadRollDegrees { get; set; }
    }

    public sealed class PetEngine
    {
        public static PetEngine Instance { get; } = new PetEngine();

        private static readonly string[] AvailablePoses =
        {
            "idle", "wave-01", "wave-02", "wave-03", "happy", "peek",
            "music-01", "music-02", "coffee-break", "charging", "sleepy", "sleeping", "low-battery"
        };

        private static readonly string[] CompanionLines =
        {
            "Ready at the top of your screen ✨",
            "Need a quick timer or file shelf? Drop anything here!",
            "Hydration check: take a sip of water 💧",
            "All systems running smoothly ⚡",
            "Listening to the beat 🎵"
        };

        private readonly Dictionary<string, PetPoseMetadata> _poseMeta = new(StringComparer.OrdinalIgnoreCase);
        private readonly Random _rng = new();

        public string CurrentPose { get; private set; } = "idle";
        public string CurrentStatusLine { get; private set; } = CompanionLines[0];
        public event Action PoseChanged;

        private PetEngine()
        {
            LoadEyeAreasJson();
        }

        private void LoadEyeAreasJson()
        {
            try
            {
                string json = EmbeddedAssetLoader.ReadResourceText("Vayli.Pet.eye-areas.json");
                if (string.IsNullOrEmpty(json)) return;
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("poses", out var poses)) return;
                foreach (var prop in poses.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("512", out var res512) &&
                        res512.TryGetProperty("eye_safe_quad", out var quad) &&
                        quad.GetArrayLength() >= 4)
                    {
                        double x1 = quad[0][0].GetDouble();
                        double y1 = quad[0][1].GetDouble();
                        double x2 = quad[2][0].GetDouble();
                        double y2 = quad[2][1].GetDouble();
                        double roll = res512.TryGetProperty("head_roll_degrees", out var rProp) ? rProp.GetDouble() : 0.0;
                        _poseMeta[prop.Name] = new PetPoseMetadata
                        {
                            EyeQuad = new Rect(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1)),
                            HeadRollDegrees = roll
                        };
                    }
                }
            }
            catch
            {
                // Fallback defaults used
            }
        }

        public PetPoseMetadata GetMetadata(string pose)
        {
            if (_poseMeta.TryGetValue(pose ?? "idle", out var m))
                return m;
            return new PetPoseMetadata();
        }

        public void SetPose(string pose, string statusLine = null)
        {
            CurrentPose = string.IsNullOrWhiteSpace(pose) ? "idle" : pose;
            if (!string.IsNullOrWhiteSpace(statusLine))
                CurrentStatusLine = statusLine;
            PoseChanged?.Invoke();
        }

        public void CycleFunPose()
        {
            string[] cycle = { "idle", "happy", "wave-01", "music-01", "coffee-break", "peek", "charging", "sleepy" };
            int idx = Array.IndexOf(cycle, CurrentPose);
            string next = cycle[(idx + 1) % cycle.Length];
            string line = CompanionLines[_rng.Next(CompanionLines.Length)];
            SetPose(next, line);
        }
    }

    /// <summary>
    /// Renders the official Vayli clay robot pet (`Vayli.Pet.body.<pose>.png` + `Vayli.Pet.props.<pose>.png`)
    /// with live #30D158 glowing eyes inside the Blender Cycles `eye_safe_quad` that blink and follow the cursor.
    /// </summary>
    public class PetView : Grid
    {
        private readonly Image _bodyImage;
        private readonly Image _propsImage;
        private readonly Canvas _eyeCanvas;
        private readonly Rectangle _leftEye;
        private readonly Rectangle _rightEye;
        private readonly DispatcherTimer _blinkTimer;
        private readonly DispatcherTimer _waveTimer;
        private readonly double _renderSize;
        private string _pose;
        private int _waveStep;
        private bool _isBlinking;

        public PetView(string initialPose = "idle", double size = 64)
        {
            _renderSize = size;
            Width = size;
            Height = size;
            ClipToBounds = false;

            _bodyImage = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform
            };
            RenderOptions.SetBitmapScalingMode(_bodyImage, BitmapScalingMode.HighQuality);

            _eyeCanvas = new Canvas
            {
                Width = size,
                Height = size,
                IsHitTestVisible = false
            };

            var eyeBrush = new SolidColorBrush(Color.FromRgb(48, 209, 88)); // #30D158 emission output from Vayli.Pet.eye-areas.json
            eyeBrush.Freeze();

            _leftEye = new Rectangle
            {
                Fill = eyeBrush,
                RadiusX = 2.5,
                RadiusY = 2.5
            };
            _rightEye = new Rectangle
            {
                Fill = eyeBrush,
                RadiusX = 2.5,
                RadiusY = 2.5
            };
            _eyeCanvas.Children.Add(_leftEye);
            _eyeCanvas.Children.Add(_rightEye);

            _propsImage = new Image
            {
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(_propsImage, BitmapScalingMode.HighQuality);

            Children.Add(_bodyImage);
            Children.Add(_eyeCanvas);
            Children.Add(_propsImage);

            SetFrame(initialPose);

            _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
            int tickCounter = 0;
            _blinkTimer.Tick += (_, _) =>
            {
                tickCounter++;
                _isBlinking = (tickCounter % 16 == 0);
                UpdateEyesFromCursor();
            };
            _blinkTimer.Start();

            _waveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _waveTimer.Tick += (_, _) =>
            {
                _waveStep++;
                string[] waveFrames = { "wave-01", "wave-02", "wave-03", "wave-02", "idle" };
                if (_waveStep < waveFrames.Length)
                {
                    SetFrame(waveFrames[_waveStep]);
                }
                else
                {
                    _waveTimer.Stop();
                    SetFrame(PetEngine.Instance.CurrentPose);
                }
            };

            MouseEnter += (_, _) => TriggerWave();
            MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                PetEngine.Instance.CycleFunPose();
                SetFrame(PetEngine.Instance.CurrentPose);
            };
        }

        public void TriggerWave()
        {
            if (_waveTimer.IsEnabled) return;
            _waveStep = 0;
            SetFrame("wave-01");
            _waveTimer.Start();
        }

        public void SetFrame(string pose)
        {
            _pose = string.IsNullOrWhiteSpace(pose) ? "idle" : pose;
            _bodyImage.Source = EmbeddedAssetLoader.LoadBitmap($"Vayli.Pet.body.{_pose}.png")
                             ?? EmbeddedAssetLoader.LoadBitmap("Vayli.Pet.body.idle.png");
            _propsImage.Source = EmbeddedAssetLoader.LoadBitmap($"Vayli.Pet.props.{_pose}.png");
            UpdateEyesFromCursor();
        }

        private void UpdateEyesFromCursor()
        {
            if (_pose == "sleeping")
            {
                _leftEye.Visibility = Visibility.Hidden;
                _rightEye.Visibility = Visibility.Hidden;
                return;
            }

            _leftEye.Visibility = Visibility.Visible;
            _rightEye.Visibility = Visibility.Visible;

            var meta = PetEngine.Instance.GetMetadata(_pose);
            double scale = _renderSize / 512.0;
            Rect quad = new Rect(
                meta.EyeQuad.X * scale,
                meta.EyeQuad.Y * scale,
                meta.EyeQuad.Width * scale,
                meta.EyeQuad.Height * scale);

            double eyeW = Math.Max(2.2, quad.Width * 0.16);
            double eyeH = _isBlinking || _pose == "sleepy"
                ? Math.Max(1.2, quad.Height * 0.12)
                : Math.Max(3.2, quad.Height * 0.42);

            double offsetX = 0;
            double offsetY = 0;
            if (NativeMethods.GetCursorPos(out var pt))
            {
                double screenW = Math.Max(1, SystemParameters.PrimaryScreenWidth);
                double screenH = Math.Max(1, SystemParameters.PrimaryScreenHeight);
                double nx = (pt.X / screenW - 0.5) * 2.0;
                double ny = (pt.Y / screenH - 0.15) * 2.0;
                offsetX = Math.Clamp(nx, -1, 1) * (quad.Width * 0.12);
                offsetY = Math.Clamp(ny, -1, 1) * (quad.Height * 0.14);
            }

            double centerY = quad.Top + (quad.Height - eyeH) * 0.5 + offsetY;
            double leftX = quad.Left + quad.Width * 0.24 - eyeW * 0.5 + offsetX;
            double rightX = quad.Left + quad.Width * 0.76 - eyeW * 0.5 + offsetX;

            _leftEye.Width = eyeW;
            _leftEye.Height = eyeH;
            _leftEye.RadiusX = eyeW * 0.45;
            _leftEye.RadiusY = eyeW * 0.45;
            Canvas.SetLeft(_leftEye, leftX);
            Canvas.SetTop(_leftEye, centerY);

            _rightEye.Width = eyeW;
            _rightEye.Height = eyeH;
            _rightEye.RadiusX = eyeW * 0.45;
            _rightEye.RadiusY = eyeW * 0.45;
            Canvas.SetLeft(_rightEye, rightX);
            Canvas.SetTop(_rightEye, centerY);
        }
    }
}
