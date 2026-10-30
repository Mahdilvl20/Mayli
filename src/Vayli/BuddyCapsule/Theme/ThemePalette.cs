using System;
using System.Windows.Media;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Theme
{
    public sealed class ThemePalette
    {
        public string Id { get; set; } = "obsidian-dark";
        public string DisplayName { get; set; } = "Obsidian Dark";
        public bool IsLight { get; set; } = false;
        public bool UseIconTiles { get; set; } = true;

        public Color Bg { get; set; } = Color.FromRgb(10, 10, 12);
        public Color Surface { get; set; } = Color.FromRgb(19, 19, 23);
        public Color Surface2 { get; set; } = Color.FromRgb(27, 27, 33);
        public Color Surface3 { get; set; } = Color.FromRgb(36, 36, 44);
        public Color Overlay { get; set; } = Color.FromArgb(230, 14, 14, 18);
        public Color Border { get; set; } = Color.FromArgb(38, 255, 255, 255);
        public Color BorderStrong { get; set; } = Color.FromArgb(72, 255, 255, 255);

        public Color Text { get; set; } = Color.FromRgb(245, 245, 247);
        public Color Text2 { get; set; } = Color.FromRgb(174, 174, 182);
        public Color Text3 { get; set; } = Color.FromRgb(118, 118, 128);
        public Color TextDisabled { get; set; } = Color.FromRgb(78, 78, 86);

        public Color Icon { get; set; } = Color.FromRgb(240, 240, 245);
        public Color IconMuted { get; set; } = Color.FromRgb(142, 142, 152);

        public Color Accent { get; set; } = Color.FromRgb(48, 209, 88);   // #30D158 Vayli signature emerald
        public Color AccentFg { get; set; } = Color.FromRgb(8, 20, 12);
        public Color AccentSoft { get; set; } = Color.FromArgb(42, 48, 209, 88);
        public Color AccentStrong { get; set; } = Color.FromRgb(56, 229, 98);

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

        public Color DockPill { get; set; } = Color.FromRgb(28, 28, 35);
        public Color ControlFill { get; set; } = Color.FromArgb(24, 255, 255, 255);
        public Color ControlFillHover { get; set; } = Color.FromArgb(42, 255, 255, 255);
        public Color PlayInk { get; set; } = Color.FromRgb(48, 209, 88);
        public Color SunInk { get; set; } = Color.FromRgb(255, 214, 10);

        public ThemePalette Clone() => (ThemePalette)MemberwiseClone();
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
            var palette = new ThemePalette();
            string hex = AppPreferences.Instance.Accent;
            if (!string.IsNullOrWhiteSpace(hex))
            {
                try
                {
                    var converted = (Color)ColorConverter.ConvertFromString(hex);
                    palette.Accent = converted;
                    palette.AccentSoft = Color.FromArgb(46, converted.R, converted.G, converted.B);
                }
                catch
                {
                    // Keep default emerald
                }
            }
            return palette;
        }

        public static SolidColorBrush Brush(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
