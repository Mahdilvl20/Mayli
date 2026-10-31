using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.Tools.ScreenTime
{
    public static class AppIconExtractor
    {
        private static readonly ConcurrentDictionary<string, ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

        public static ImageSource GetIcon(string appName, string exePath = null)
        {
            if (string.IsNullOrWhiteSpace(appName))
            {
                return GetFallbackIcon("?");
            }

            if (_iconCache.TryGetValue(appName, out var cached))
            {
                return cached;
            }

            // 1. Try extracting from exe path
            if (!string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath))
            {
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        var src = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        _iconCache[appName] = src;
                        return src;
                    }
                }
                catch { }
            }

            // 2. Try looking in Windows known app paths if exePath is empty
            string guessedPath = GuessAppPath(appName);
            if (!string.IsNullOrEmpty(guessedPath) && File.Exists(guessedPath))
            {
                try
                {
                    using var icon = Icon.ExtractAssociatedIcon(guessedPath);
                    if (icon != null)
                    {
                        var src = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze();
                        _iconCache[appName] = src;
                        return src;
                    }
                }
                catch { }
            }

            // 3. Fallback styled monogram icon
            var fallback = GetFallbackIcon(appName);
            _iconCache[appName] = fallback;
            return fallback;
        }

        private static string GuessAppPath(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("chrome"))
                return @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            if (lower.Contains("telegram"))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Telegram Desktop\Telegram.exe");
            if (lower.Contains("antigravity"))
                return Environment.ProcessPath;
            if (lower.Contains("vayli"))
                return Environment.ProcessPath;
            if (lower.Contains("explorer"))
                return @"C:\Windows\explorer.exe";
            if (lower.Contains("code") || lower.Contains("visual studio code"))
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Microsoft VS Code\Code.exe");

            return null;
        }

        private static ImageSource GetFallbackIcon(string name)
        {
            int size = 32;
            var drawingVisual = new DrawingVisual();
            using (var dc = drawingVisual.RenderOpen())
            {
                // Generate a consistent vibrant color based on name hash
                int hash = Math.Abs(name.GetHashCode());
                byte r = (byte)(60 + (hash % 140));
                byte g = (byte)(70 + ((hash / 3) % 130));
                byte b = (byte)(110 + ((hash / 7) % 140));
                var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));

                dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, size, size), 8, 8);

                string initial = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant();
                var formattedText = new FormattedText(
                    initial,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                    16,
                    System.Windows.Media.Brushes.White,
                    VisualTreeHelper.GetDpi(drawingVisual).PixelsPerDip);

                double x = (size - formattedText.Width) / 2.0;
                double y = (size - formattedText.Height) / 2.0;
                dc.DrawText(formattedText, new System.Windows.Point(x, y));
            }

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(drawingVisual);
            rtb.Freeze();
            return rtb;
        }
    }
}
