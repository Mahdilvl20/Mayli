using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        private static readonly string[] AvailablePoses =
        {
            "idle", "happy", "angry", "frown", "love", "wink", "surprised", "sleepy", "sad", "thinking",
            "wave-01", "wave-02", "wave-03", "peek", "music-01", "music-02", "coffee-break", "charging", "sleeping"
        };

        private static readonly Dictionary<OrbMood, string[]> MoodLines = new()
        {
            {
                OrbMood.Idle, new[]
                {
                    "آماده در بالای صفحه شما ✨",
                    "Ready at the top of your screen ✨",
                    "All systems running smoothly ⚡",
                    "یک روز پرانرژی و متمرکز داشته باشید 🚀"
                }
            },
            {
                OrbMood.Happy, new[]
                {
                    "وایلی خیلی خوشحاله! همه چی عالی پیش میره 🌟",
                    "Vayli is super happy today! Keep going!",
                    "لبخند بزن، امروز روز توئه! 😊✨",
                    "Awesome work! You are rocking it 🎯"
                }
            },
            {
                OrbMood.Angry, new[]
                {
                    "وایلی اخم کرده! کی اذیتش کرده؟! 😠",
                    "Hmph! Grumpy Vayli is not amused 😤",
                    "اخم وایلی رو ببین! گوگولی ولی عصبانی 💢",
                    "Don't poke too hard, I'm meditating! ⚡"
                }
            },
            {
                OrbMood.Love, new[]
                {
                    "وایلی عاشقتونه! گوگولی مگولی 💖",
                    "Vayli loves you! Keep being awesome ✨",
                    "یک عالمه عشق و انرژی مثبت براتون! 🌸",
                    "Best companion ever! (◕‿◕)♡"
                }
            },
            {
                OrbMood.Wink, new[]
                {
                    "یه چشمک گوگولی برای انرژی امروز 😉",
                    "Wink! Let's get things done together ✨",
                    "همه چی تحت کنترله دوست من! 😎"
                }
            },
            {
                OrbMood.Surprised, new[]
                {
                    "وای! این دیگه چی بود؟! 😲",
                    "Woah! That was unexpected and exciting!",
                    "دیدم چیکار کردی! فوق‌العاده بود ⚡"
                }
            },
            {
                OrbMood.Sleepy, new[]
                {
                    "وایلی خوابش میاد... هیسسس 😴",
                    "Feeling cozy and sleepy... Zzz 💤",
                    "شاید وقت یه استراحت کوتاه و نوشیدن آبه 💧"
                }
            },
            {
                OrbMood.Sad, new[]
                {
                    "یه کم پکر شدم... بغلم کن 🥺",
                    "Feeling a bit blue today... but we got this 💙"
                }
            },
            {
                OrbMood.Thinking, new[]
                {
                    "دارم عمیقاً فکر می‌کنم... 🤔",
                    "Deep in thought, processing the universe 🌌"
                }
            }
        };

        public static PetEngine Instance { get; } = new PetEngine();

        private readonly Dictionary<string, PetPoseMetadata> _poseMeta = new(StringComparer.OrdinalIgnoreCase);
        private readonly Random _rng = new();

        public OrbMood CurrentMood { get; private set; } = OrbMood.Idle;
        public string CurrentPose { get; private set; } = "idle";
        public string CurrentStatusLine { get; private set; } = "آماده در بالای صفحه شما ✨";

        public event Action<OrbMood> MoodChanged;
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

        public void SetMood(OrbMood mood, string statusLine = null)
        {
            CurrentMood = mood;
            CurrentPose = mood.ToString().ToLowerInvariant();

            if (!string.IsNullOrWhiteSpace(statusLine))
            {
                CurrentStatusLine = statusLine;
            }
            else
            {
                CurrentStatusLine = PickRandomLine(mood);
            }

            MoodChanged?.Invoke(mood);
            PoseChanged?.Invoke();
        }

        public void SetPose(string pose, string statusLine = null)
        {
            CurrentPose = string.IsNullOrWhiteSpace(pose) ? "idle" : pose;
            CurrentMood = ParseMood(CurrentPose);

            if (!string.IsNullOrWhiteSpace(statusLine))
            {
                CurrentStatusLine = statusLine;
            }
            else
            {
                CurrentStatusLine = PickRandomLine(CurrentMood);
            }

            MoodChanged?.Invoke(CurrentMood);
            PoseChanged?.Invoke();
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
            int idx = Array.IndexOf(cycle, CurrentMood);
            OrbMood next = cycle[(idx + 1) % cycle.Length];
            SetMood(next);
        }

        public void CycleFunPose() => CycleNextMood();

        private string PickRandomLine(OrbMood mood)
        {
            if (MoodLines.TryGetValue(mood, out var lines) && lines.Length > 0)
            {
                return lines[_rng.Next(lines.Length)];
            }
            return "Vayli is with you ✨";
        }

        public static OrbMood ParseMood(string poseName)
        {
            if (string.IsNullOrWhiteSpace(poseName)) return OrbMood.Idle;
            string lower = poseName.Trim().ToLowerInvariant();
            return lower switch
            {
                "happy" => OrbMood.Happy,
                "angry" or "frown" or "grumpy" => OrbMood.Angry,
                "love" or "heart" or "romantic" => OrbMood.Love,
                "wink" or "winking" => OrbMood.Wink,
                "surprised" or "shocked" or "curious" => OrbMood.Surprised,
                "sleepy" or "sleeping" or "charging" or "low-battery" => OrbMood.Sleepy,
                "sad" => OrbMood.Sad,
                "thinking" or "coffee-break" => OrbMood.Thinking,
                "music-01" or "music-02" or "wave-01" or "wave-02" or "wave-03" => OrbMood.Happy,
                _ => OrbMood.Idle
            };
        }
    }

    /// <summary>
    /// Renders the vector-animated interactive companion Pet (the glowing expressive Orb).
    /// Fully animatable with moods (frown, happy, wink, love, surprised, sleepy),
    /// mouse gaze tracking, organic breathing and bounce physics, and particle effects.
    /// </summary>
    public class PetView : Grid
    {
        public CuteOrbPetView Orb { get; }

        public PetView(string initialPose = "idle", double size = 64)
        {
            Width = size;
            Height = size;
            ClipToBounds = false;

            var mood = PetEngine.ParseMood(initialPose);
            Orb = new CuteOrbPetView(mood, size)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Sync with global engine
            PetEngine.Instance.MoodChanged += onEngineMoodChanged;

            Orb.Clicked += () =>
            {
                PetEngine.Instance.CycleNextMood();
            };

            Children.Add(Orb);
        }

        private void onEngineMoodChanged(OrbMood mood)
        {
            Orb.SetMood(mood);
        }

        public void TriggerWave()
        {
            Orb.Poke();
            Orb.SetMood(OrbMood.Wink);
        }

        public void SetFrame(string pose)
        {
            var mood = PetEngine.ParseMood(pose);
            Orb.SetMood(mood);
        }

        public void SetMood(OrbMood mood)
        {
            Orb.SetMood(mood);
        }
    }
}
