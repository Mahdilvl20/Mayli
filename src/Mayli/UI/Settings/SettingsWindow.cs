using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.UI.Settings
{
    public sealed class SettingsWindow : Window
    {
        private static SettingsWindow _openInstance;

        public static void ShowSingleton()
        {
            if (_openInstance != null && _openInstance.IsLoaded)
            {
                _openInstance.Activate();
                return;
            }
            _openInstance = new SettingsWindow();
            _openInstance.Closed += (_, _) => _openInstance = null;
            _openInstance.Show();
        }

        public SettingsWindow()
        {
            var p = CapsuleTheme.Current;
            Title = "Mayli Settings — Notch & Buddy Personalization";
            Width = 600;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = CapsuleTheme.Brush(p.Bg);
            Foreground = CapsuleTheme.Brush(p.Text);
            ResizeMode = ResizeMode.CanResize;
            Icon = EmbeddedAssetLoader.LoadBitmap("Mayli.Brand.mark-128.png");

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var root = new StackPanel { Margin = new Thickness(24) };

            // Header with App Title and Brand subtitle
            var headerStack = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            headerStack.Children.Add(new TextBlock
            {
                Text = "Mayli — The Notch for Windows",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = "Interactive Dynamic Island & Expressive Vector Orb Companion",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 0)
            });
            root.Children.Add(headerStack);

            // =========================================================
            // Section 1: Expressive Orb Companion (گوگولی مگولی)
            // =========================================================
            var orbCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 14) };
            var orbStack = new StackPanel();

            orbStack.Children.Add(new TextBlock
            {
                Text = "MAYLI BUDDY ORB & EXPRESSIONS (آیکون و حالت‌های متحرک)",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Live Interactive Orb Preview & Status Line
            var previewCenter = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 12)
            };

            var liveOrb = new CuteOrbPetView(PetEngine.Instance.CurrentMood, 92)
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                ToolTip = "Click me to emote / کلیک کنید برای تغییر حالت یا قلقلک!"
            };
            previewCenter.Children.Add(liveOrb);

            var statusLineText = new TextBlock
            {
                Text = PetEngine.Instance.CurrentStatusLine,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = CapsuleTheme.Brush(p.Text),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 2),
                TextAlignment = TextAlignment.Center
            };
            previewCenter.Children.Add(statusLineText);

            var hintText = new TextBlock
            {
                Text = "روی آیکون کلیک کنید تا واکنش نشون بده یا از دکمه‌های زیر برای تغییر احساسات استفاده کنید",
                FontSize = 10.5,
                Foreground = CapsuleTheme.Brush(p.Text3),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            previewCenter.Children.Add(hintText);
            orbStack.Children.Add(previewCenter);

            // Mood Buttons Row
            var moodsRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) };
            var moods = new (string Glyph, string Name, OrbMood Mood)[]
            {
                ("🌟", "عادی (Idle)", OrbMood.Idle),
                ("😊", "شاد (Happy)", OrbMood.Happy),
                ("😠", "اخم (Angry)", OrbMood.Angry),
                ("💖", "دلبر (Love)", OrbMood.Love),
                ("😉", "چشمک (Wink)", OrbMood.Wink),
                ("😲", "تعجب (Surprised)", OrbMood.Surprised),
                ("😴", "خواب‌آلود (Sleepy)", OrbMood.Sleepy),
                ("🥺", "مظلوم (Sad)", OrbMood.Sad),
                ("🤔", "متفکر (Thinking)", OrbMood.Thinking)
            };

            foreach (var m in moods)
            {
                var capturedMood = m.Mood;
                var pill = new BuddyPillButton(m.Glyph, m.Name)
                {
                    IsActive = (PetEngine.Instance.CurrentMood == capturedMood),
                    Margin = new Thickness(2, 3, 2, 3)
                };
                pill.Clicked += () =>
                {
                    PetEngine.Instance.SetMood(capturedMood);
                    liveOrb.SetMood(capturedMood);
                    statusLineText.Text = PetEngine.Instance.CurrentStatusLine;

                    foreach (var child in moodsRow.Children)
                        if (child is BuddyPillButton pb) pb.IsActive = (pb == pill);
                };
                moodsRow.Children.Add(pill);
            }
            orbStack.Children.Add(moodsRow);

            // Action Buttons: Poke & Particle Burst
            var actionRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 4, 0, 8)
            };

            var pokeBtn = new BuddyPillButton("🐾", "قلقلک و پرش (Poke & Squish)");
            pokeBtn.Clicked += () =>
            {
                liveOrb.Poke();
                statusLineText.Text = "قلقلکم اومد! (◕‿◕)✨";
            };
            actionRow.Children.Add(pokeBtn);

            var burstBtn = new BuddyPillButton("✨", "انفجار قلب و جرقه (Burst)");
            burstBtn.Clicked += () =>
            {
                liveOrb.SpawnHeartBurst();
                liveOrb.SpawnSparkleBurst();
                statusLineText.Text = "یک عالمه عشق و درخشش! 💖🌟";
            };
            actionRow.Children.Add(burstBtn);
            orbStack.Children.Add(actionRow);

            // Real-time listener for global mood changes
            PetEngine.Instance.MoodChanged += mood =>
            {
                liveOrb.SetMood(mood);
                statusLineText.Text = PetEngine.Instance.CurrentStatusLine;
                foreach (var child in moodsRow.Children)
                {
                    if (child is BuddyPillButton pb)
                    {
                        pb.IsActive = pb.Text.Contains(mood.ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                }
            };

            // Behavior Toggles for Orb
            var cursorCheck = new CheckBox
            {
                Content = "تعقیب هوشمند نشانگر ماوس (Gaze tracking follows cursor)",
                IsChecked = liveOrb.FollowCursor,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 4, 0, 4)
            };
            cursorCheck.Checked += (_, _) => liveOrb.FollowCursor = true;
            cursorCheck.Unchecked += (_, _) => liveOrb.FollowCursor = false;
            orbStack.Children.Add(cursorCheck);

            var breathingCheck = new CheckBox
            {
                Content = "شناور بودن و تنفس طبیعی (Organic floating & breathing)",
                IsChecked = liveOrb.EnableBreathing,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 2, 0, 4)
            };
            breathingCheck.Checked += (_, _) => liveOrb.EnableBreathing = true;
            breathingCheck.Unchecked += (_, _) => liveOrb.EnableBreathing = false;
            orbStack.Children.Add(breathingCheck);

            var particlesCheck = new CheckBox
            {
                Content = "ذرات معلق گوگولی: قلب‌ها، جرقه‌ها و Zzz (Floating emotion particles)",
                IsChecked = liveOrb.EnableParticles,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 2, 0, 4)
            };
            particlesCheck.Checked += (_, _) => liveOrb.EnableParticles = true;
            particlesCheck.Unchecked += (_, _) => liveOrb.EnableParticles = false;
            orbStack.Children.Add(particlesCheck);

            orbCard.Child = orbStack;
            root.Children.Add(orbCard);

            // =========================================================
            // Section 2: Notch Silhouette Style
            // =========================================================
            var styleCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 14) };
            var styleStack = new StackPanel();
            styleStack.Children.Add(new TextBlock
            {
                Text = "NOTCH SILHOUETTE STYLE",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var styleRow = new WrapPanel();
            foreach (var s in new[] { "Shouldered", "Island", "Compact", "Wide" })
            {
                string captured = s;
                var btn = new BuddyPillButton(null, captured)
                {
                    IsActive = string.Equals(AppPreferences.Instance.NotchSilhouette, captured, StringComparison.OrdinalIgnoreCase)
                };
                btn.Clicked += () =>
                {
                    AppPreferences.Instance.NotchSilhouette = captured;
                    foreach (var child in styleRow.Children)
                        if (child is BuddyPillButton pb) pb.IsActive = (pb == btn);
                };
                styleRow.Children.Add(btn);
            }
            styleStack.Children.Add(styleRow);
            styleCard.Child = styleStack;
            root.Children.Add(styleCard);

            // =========================================================
            // Section 3: Accent Color Swatches
            // =========================================================
            var colorCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 14) };
            var colorStack = new StackPanel();
            colorStack.Children.Add(new TextBlock
            {
                Text = "ACCENT & EDGE LIGHT COLOR",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var swatchRow = new WrapPanel();
            var swatches = new (string Name, string Hex)[]
            {
                ("Mayli Cyan", "#54D8E8"),
                ("Mayli Emerald", "#30D158"),
                ("Electric Blue", "#0A84FF"),
                ("Cyber Violet", "#BF5AF2"),
                ("Solar Amber", "#FF9F0A"),
                ("Crimson Pulse", "#FF453A")
            };
            foreach (var sw in swatches)
            {
                string hex = sw.Hex;
                var pill = new BuddyPillButton("●", sw.Name);
                pill.Clicked += () => AppPreferences.Instance.Accent = hex;
                swatchRow.Children.Add(pill);
            }
            colorStack.Children.Add(swatchRow);
            colorCard.Child = colorStack;
            root.Children.Add(colorCard);

            // =========================================================
            // Section 4: Interaction Preferences
            // =========================================================
            var behaviorCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 14) };
            var behaviorStack = new StackPanel();
            behaviorStack.Children.Add(new TextBlock
            {
                Text = "NOTCH INTERACTION",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Accent),
                Margin = new Thickness(0, 0, 0, 8)
            });

            var openModeCheck = new CheckBox
            {
                Content = "Require click to open notch (disable auto-open on hover)",
                IsChecked = AppPreferences.Instance.OpensOnClick,
                Foreground = CapsuleTheme.Brush(p.Text),
                Margin = new Thickness(0, 0, 0, 4)
            };
            openModeCheck.Checked += (_, _) => AppPreferences.Instance.OpensOnClick = true;
            openModeCheck.Unchecked += (_, _) => AppPreferences.Instance.OpensOnClick = false;
            behaviorStack.Children.Add(openModeCheck);

            behaviorCard.Child = behaviorStack;
            root.Children.Add(behaviorCard);

            scrollViewer.Content = root;
            Content = scrollViewer;
        }
    }
}
