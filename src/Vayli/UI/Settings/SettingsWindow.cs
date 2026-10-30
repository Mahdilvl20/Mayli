using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WindowsBuddy.BuddyCapsule.Renderer;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.BuddyCapsule.UI;
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
            Title = "Vayli Settings — Notch Personalization";
            Width = 560;
            Height = 470;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = CapsuleTheme.Brush(p.Bg);
            Foreground = CapsuleTheme.Brush(p.Text);
            ResizeMode = ResizeMode.CanMinimize;

            var root = new StackPanel { Margin = new Thickness(22) };

            root.Children.Add(new TextBlock
            {
                Text = "Vayli — The Notch for Windows",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = CapsuleTheme.Brush(p.Text)
            });
            root.Children.Add(new TextBlock
            {
                Text = "Reconstructed from REA static binary analysis (v1.1.57.0 • WindowsBuddy)",
                FontSize = 12,
                Foreground = CapsuleTheme.Brush(p.Text2),
                Margin = new Thickness(0, 2, 0, 18)
            });

            // Section 1: Notch Silhouette Style
            var styleCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 12) };
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

            // Section 2: Accent Color Swatches
            var colorCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 12) };
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
                ("Vayli Emerald", "#30D158"),
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

            // Section 3: Interaction & Pet Companion
            var behaviorCard = new BuddyCard { Margin = new Thickness(0, 0, 0, 12) };
            var behaviorStack = new StackPanel();
            behaviorStack.Children.Add(new TextBlock
            {
                Text = "INTERACTION & PET COMPANION",
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
                Margin = new Thickness(0, 0, 0, 8)
            };
            openModeCheck.Checked += (_, _) => AppPreferences.Instance.OpensOnClick = true;
            openModeCheck.Unchecked += (_, _) => AppPreferences.Instance.OpensOnClick = false;
            behaviorStack.Children.Add(openModeCheck);

            var poseRow = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var pose in new[] { "idle", "happy", "wave-01", "music-01", "coffee-break", "charging", "sleeping" })
            {
                string captured = pose;
                var btn = new BuddyPillButton("🐾", captured);
                btn.Clicked += () => PetEngine.Instance.SetPose(captured, $"Pose changed to {captured} from Settings");
                poseRow.Children.Add(btn);
            }
            behaviorStack.Children.Add(poseRow);
            behaviorCard.Child = behaviorStack;
            root.Children.Add(behaviorCard);

            Content = root;
        }
    }
}
