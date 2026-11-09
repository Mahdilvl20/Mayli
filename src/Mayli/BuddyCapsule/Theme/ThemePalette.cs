using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Theme
{
    public sealed class ThemePalette
    {
        public string Id { get; set; } = "pure-black";
        public string DisplayName { get; set; } = "Pure Black";
        public bool IsLight { get; set; } = false;
        public bool UseIconTiles { get; set; } = true;

        public Color Bg { get; set; } = Color.FromRgb(0, 0, 0);
        public Color Surface { get; set; } = Color.FromRgb(14, 14, 16);
        public Color Surface2 { get; set; } = Color.FromRgb(22, 22, 26);
        public Color Surface3 { get; set; } = Color.FromRgb(32, 32, 38);
        public Color Overlay { get; set; } = Color.FromArgb(230, 10, 10, 12);
        public Color Border { get; set; } = Color.FromArgb(36, 255, 255, 255);
        public Color BorderStrong { get; set; } = Color.FromArgb(68, 255, 255, 255);

        public Color Text { get; set; } = Color.FromRgb(250, 250, 252);
        public Color Text2 { get; set; } = Color.FromRgb(142, 142, 147);
        public Color Text3 { get; set; } = Color.FromRgb(99, 99, 102);
        public Color TextDisabled { get; set; } = Color.FromRgb(65, 65, 70);

        public Color Icon { get; set; } = Color.FromRgb(240, 240, 245);
        public Color IconMuted { get; set; } = Color.FromRgb(135, 135, 145);

        public Color Accent { get; set; } = Color.FromRgb(255, 255, 255); // Pure Black monochrome
        public Color AccentFg { get; set; } = Color.FromRgb(10, 10, 12);
        public Color AccentSoft { get; set; } = Color.FromArgb(36, 255, 255, 255);
        public Color AccentStrong { get; set; } = Color.FromRgb(255, 255, 255);

        public Color CScreen { get; set; } = Color.FromRgb(94, 92, 230);
        public Color CSystem { get; set; } = Color.FromRgb(10, 132, 255);
        public Color CWeather { get; set; } = Color.FromRgb(255, 159, 10);
        public Color CFocus { get; set; } = Color.FromRgb(191, 90, 242);
        public Color CClipboard { get; set; } = Color.FromRgb(100, 210, 255);
        public Color CNotes { get; set; } = Color.FromRgb(255, 214, 10);

        public Color Success { get; set; } = Color.FromRgb(48, 209, 88);
        public Color Warning { get; set; } = Color.FromRgb(255, 159, 10);
        public Color Danger { get; set; } = Color.FromRgb(255, 69, 58);
        public Color Info { get; set; } = Color.FromRgb(10, 132, 255);

        public Color DockPill { get; set; } = Color.FromRgb(18, 18, 22);
        public Color ControlFill { get; set; } = Color.FromArgb(20, 255, 255, 255);
        public Color ControlFillHover { get; set; } = Color.FromArgb(42, 255, 255, 255);
        public Color PlayInk { get; set; } = Color.FromRgb(255, 255, 255);
        public Color SunInk { get; set; } = Color.FromRgb(255, 214, 10);

        public ThemePalette Clone() => (ThemePalette)MemberwiseClone();

        #region 4 Official Preset Looks (Screenshot 5: Pure Black, Ember Dark, Vivid, Silver)

        public static readonly ThemePalette PureBlack = new ThemePalette
        {
            Id = "pure-black",
            DisplayName = "Pure Black",
            IsLight = false,
            Bg = Color.FromRgb(0, 0, 0),
            Surface = Color.FromRgb(14, 14, 16),
            Surface2 = Color.FromRgb(22, 22, 26),
            Surface3 = Color.FromRgb(32, 32, 38),
            Overlay = Color.FromArgb(235, 8, 8, 10),
            Border = Color.FromArgb(32, 255, 255, 255),
            BorderStrong = Color.FromArgb(64, 255, 255, 255),
            Text = Color.FromRgb(252, 252, 254),
            Text2 = Color.FromRgb(142, 142, 147),
            Text3 = Color.FromRgb(99, 99, 102),
            Accent = Color.FromRgb(255, 255, 255),
            AccentFg = Color.FromRgb(0, 0, 0),
            AccentSoft = Color.FromArgb(32, 255, 255, 255),
            AccentStrong = Color.FromRgb(255, 255, 255),
            DockPill = Color.FromRgb(16, 16, 18),
            ControlFill = Color.FromArgb(20, 255, 255, 255),
            ControlFillHover = Color.FromArgb(40, 255, 255, 255)
        };

        public static readonly ThemePalette EmberDark = new ThemePalette
        {
            Id = "ember-dark",
            DisplayName = "Ember Dark",
            IsLight = false,
            Bg = Color.FromRgb(13, 14, 17),
            Surface = Color.FromRgb(20, 21, 26),
            Surface2 = Color.FromRgb(28, 30, 37),
            Surface3 = Color.FromRgb(38, 40, 50),
            Overlay = Color.FromArgb(235, 12, 13, 16),
            Border = Color.FromArgb(36, 255, 255, 255),
            BorderStrong = Color.FromArgb(80, 255, 122, 0),
            Text = Color.FromRgb(252, 252, 254),
            Text2 = Color.FromRgb(150, 150, 160),
            Text3 = Color.FromRgb(105, 105, 115),
            Accent = Color.FromRgb(255, 122, 0), // Ember Orange #FF7A00
            AccentFg = Color.FromRgb(20, 8, 0),
            AccentSoft = Color.FromArgb(50, 255, 122, 0),
            AccentStrong = Color.FromRgb(255, 145, 36),
            DockPill = Color.FromRgb(22, 23, 28),
            ControlFill = Color.FromArgb(24, 255, 255, 255),
            ControlFillHover = Color.FromArgb(50, 255, 122, 0)
        };

        public static readonly ThemePalette Vivid = new ThemePalette
        {
            Id = "vivid",
            DisplayName = "Vivid",
            IsLight = false,
            Bg = Color.FromRgb(10, 13, 20),
            Surface = Color.FromRgb(16, 21, 32),
            Surface2 = Color.FromRgb(24, 32, 48),
            Surface3 = Color.FromRgb(32, 44, 66),
            Overlay = Color.FromArgb(235, 8, 12, 18),
            Border = Color.FromArgb(40, 255, 255, 255),
            BorderStrong = Color.FromArgb(80, 10, 132, 255),
            Text = Color.FromRgb(252, 252, 254),
            Text2 = Color.FromRgb(140, 160, 185),
            Text3 = Color.FromRgb(95, 115, 140),
            Accent = Color.FromRgb(10, 132, 255), // Electric Blue #0A84FF
            AccentFg = Color.FromRgb(2, 14, 30),
            AccentSoft = Color.FromArgb(50, 10, 132, 255),
            AccentStrong = Color.FromRgb(56, 158, 255),
            DockPill = Color.FromRgb(18, 24, 38),
            ControlFill = Color.FromArgb(24, 255, 255, 255),
            ControlFillHover = Color.FromArgb(50, 10, 132, 255)
        };

        public static readonly ThemePalette Silver = new ThemePalette
        {
            Id = "silver",
            DisplayName = "Silver",
            IsLight = true,
            Bg = Color.FromRgb(238, 240, 244),
            Surface = Color.FromRgb(255, 255, 255),
            Surface2 = Color.FromRgb(244, 245, 248),
            Surface3 = Color.FromRgb(230, 232, 238),
            Overlay = Color.FromArgb(240, 248, 249, 252),
            Border = Color.FromArgb(35, 0, 0, 0),
            BorderStrong = Color.FromArgb(65, 0, 0, 0),
            Text = Color.FromRgb(24, 24, 28),
            Text2 = Color.FromRgb(68, 71, 80),
            Text3 = Color.FromRgb(102, 106, 116),
            TextDisabled = Color.FromRgb(170, 172, 180),
            Icon = Color.FromRgb(28, 28, 32),
            IconMuted = Color.FromRgb(85, 88, 98),
            Accent = Color.FromRgb(0, 113, 227), // Apple System Blue #0071E3
            AccentFg = Color.FromRgb(255, 255, 255),
            AccentSoft = Color.FromArgb(28, 0, 113, 227),
            AccentStrong = Color.FromRgb(0, 90, 190),
            DockPill = Color.FromRgb(242, 243, 246),
            ControlFill = Color.FromArgb(18, 0, 0, 0),
            ControlFillHover = Color.FromArgb(32, 0, 0, 0)
        };

        public static readonly IReadOnlyList<ThemePalette> AllPresets = new[]
        {
            PureBlack,
            EmberDark,
            Vivid,
            Silver
        };

        #endregion
    }

    public static class CapsuleTheme
    {
        public static ThemePalette Current { get; private set; } = BuildFromPreferences();
        public static event Action ThemeChanged;

        static CapsuleTheme()
        {
            AppPreferences.Instance.Changed += () =>
            {
                Current = BuildFromPreferences();
                ThemeChanged?.Invoke();
            };
        }

        public static ThemePalette BuildFromPreferences()
        {
            string themeName = AppPreferences.Instance.Theme;
            var preset = ThemePalette.AllPresets.FirstOrDefault(p =>
                string.Equals(p.DisplayName, themeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Id, themeName, StringComparison.OrdinalIgnoreCase));

            if (preset != null)
                return preset.Clone();

            // Default to Pure Black if not set or legacy
            return ThemePalette.PureBlack.Clone();
        }

        public static void CycleTheme()
        {
            var presets = ThemePalette.AllPresets;
            int currentIndex = -1;
            for (int i = 0; i < presets.Count; i++)
            {
                if (string.Equals(presets[i].Id, Current.Id, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = (currentIndex + 1) % presets.Count;
            var nextTheme = presets[nextIndex];
            AppPreferences.Instance.Theme = nextTheme.DisplayName;
            Current = nextTheme.Clone();
            ThemeChanged?.Invoke();
        }

        public static void SetTheme(string idOrName)
        {
            var preset = ThemePalette.AllPresets.FirstOrDefault(p =>
                string.Equals(p.DisplayName, idOrName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Id, idOrName, StringComparison.OrdinalIgnoreCase));

            if (preset != null)
            {
                AppPreferences.Instance.Theme = preset.DisplayName;
                Current = preset.Clone();
                ThemeChanged?.Invoke();
            }
        }

        private static readonly Dictionary<Color, SolidColorBrush> _brushCache = new();

        public static SolidColorBrush Brush(Color c)
        {
            lock (_brushCache)
            {
                if (_brushCache.TryGetValue(c, out var cached)) return cached;
                var b = new SolidColorBrush(c);
                b.Freeze();
                _brushCache[c] = b;
                return b;
            }
        }
    }
}
