using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsBuddy.Core;

namespace WindowsBuddy.Tools.Clipboard
{
    public sealed class ClipboardService
    {
        private static readonly Lazy<ClipboardService> _lazy = new(() => new ClipboardService());
        public static ClipboardService Instance => _lazy.Value;

        private readonly List<ClipboardItem> _items = new();
        private readonly object _lock = new();

        public event Action ItemsChanged;

        public IReadOnlyList<ClipboardItem> Items
        {
            get
            {
                lock (_lock)
                {
                    return _items.ToList();
                }
            }
        }

        private ClipboardService()
        {
            SeedInitialItems();
        }

        private void SeedInitialItems()
        {
            // Seed sample items matching the user's screenshot
            var svgItem = new ClipboardItem
            {
                Kind = ClipKind.Code,
                Text = "<svg xmlns=\"http://www.w3.org/2000/svg\"\n     viewBox=\"0 0 128 128\"\n     width=\"128\" height=\"128\">\n\n  <defs>\n    <radialGradient id=\"orbGradient\"\n                    cx=\"35%\" cy=\"25%\" r=\"75%\">\n      <stop offset=\"0%\" stop-color=\"#B9F7FF\"/>\n      <stop offset=\"50%\" stop-color=\"#5B86E5\"/>\n      <stop offset=\"100%\" stop-color=\"#36096D\"/>\n    </radialGradient>\n  </defs>\n\n  <circle cx=\"64\" cy=\"64\" r=\"60\" fill=\"url(#orbGradient)\"/>\n</svg>",
                LineCount = 81,
                CharCount = 2300,
                WordCount = 142,
                SourceApp = "Google Chrome",
                SourceAppExe = "chrome.exe",
                TimestampLocal = DateTime.Now.AddMinutes(-49)
            };
            _items.Add(svgItem);

            var persianText = new ClipboardItem
            {
                Kind = ClipKind.Text,
                Text = "س",
                LineCount = 1,
                CharCount = 1,
                WordCount = 1,
                SourceApp = "Telegram",
                SourceAppExe = "telegram.exe",
                TimestampLocal = DateTime.Now.AddHours(-1)
            };
            _items.Add(persianText);

            var linkItem = new ClipboardItem
            {
                Kind = ClipKind.Link,
                Text = "https://github.com/morluto/rea",
                LinkHost = "github.com",
                LinkUri = new Uri("https://github.com/morluto/rea"),
                LineCount = 1,
                CharCount = 31,
                WordCount = 1,
                SourceApp = "Google Chrome",
                SourceAppExe = "chrome.exe",
                TimestampLocal = DateTime.Now.AddHours(-2)
            };
            _items.Add(linkItem);
        }

        public void CaptureCurrentClipboard()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsImage())
                {
                    var img = System.Windows.Clipboard.GetImage();
                    if (img != null)
                    {
                        AddImageItem(img);
                    }
                }
                else if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        AddTextItem(text);
                    }
                }
                else if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var files = System.Windows.Clipboard.GetFileDropList();
                    if (files != null && files.Count > 0)
                    {
                        AddFilesItem(files);
                    }
                }
            }
            catch { }
        }

        public void AddTextItem(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return;
            string text = rawText.Trim();

            lock (_lock)
            {
                var existing = _items.FirstOrDefault(i => i.Kind != ClipKind.Image && string.Equals(i.Text, text, StringComparison.Ordinal));
                if (existing != null)
                {
                    existing.TimestampLocal = DateTime.Now;
                    _items.Remove(existing);
                    _items.Insert(0, existing);
                    ItemsChanged?.Invoke();
                    return;
                }

                var item = new ClipboardItem
                {
                    Text = text,
                    TimestampLocal = DateTime.Now
                };

                // Classify Kind
                if (IsUrl(text, out Uri uri))
                {
                    item.Kind = ClipKind.Link;
                    item.LinkUri = uri;
                    item.LinkHost = uri.Host;
                }
                else if (DetectIsCode(text))
                {
                    item.Kind = ClipKind.Code;
                }
                else if (Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    item.Kind = ClipKind.Email;
                }
                else
                {
                    item.Kind = ClipKind.Text;
                }

                // Compute counts
                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                item.LineCount = lines.Length;
                item.CharCount = text.Length;
                item.WordCount = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                DetectOwnerApp(item);

                _items.Insert(0, item);
                if (_items.Count > 100) _items.RemoveAt(_items.Count - 1);
            }

            ItemsChanged?.Invoke();
        }

        public void AddImageItem(BitmapSource bitmap)
        {
            if (bitmap == null) return;

            lock (_lock)
            {
                int w = bitmap.PixelWidth;
                int h = bitmap.PixelHeight;

                var thumb = CreateThumbnail(bitmap, 28, 28);
                var item = new ClipboardItem
                {
                    Kind = ClipKind.Image,
                    Image = bitmap,
                    Thumbnail = thumb,
                    ImageWidth = w,
                    ImageHeight = h,
                    FileSizeBytes = (long)(w * h * 4 * 0.7), // estimate
                    TimestampLocal = DateTime.Now
                };

                DetectOwnerApp(item);

                _items.Insert(0, item);
                if (_items.Count > 100) _items.RemoveAt(_items.Count - 1);
            }

            ItemsChanged?.Invoke();
        }

        public void AddFilesItem(System.Collections.Specialized.StringCollection files)
        {
            lock (_lock)
            {
                var list = new List<string>();
                foreach (var f in files)
                {
                    if (!string.IsNullOrEmpty(f)) list.Add(f);
                }
                if (list.Count == 0) return;

                var item = new ClipboardItem
                {
                    Kind = ClipKind.Files,
                    FilePaths = list,
                    Text = string.Join(Environment.NewLine, list),
                    TimestampLocal = DateTime.Now
                };

                DetectOwnerApp(item);
                _items.Insert(0, item);
                if (_items.Count > 100) _items.RemoveAt(_items.Count - 1);
            }

            ItemsChanged?.Invoke();
        }

        public void TogglePin(ClipboardItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                item.IsPinned = !item.IsPinned;
            }
            ItemsChanged?.Invoke();
        }

        public void DeleteItem(ClipboardItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                _items.Remove(item);
            }
            ItemsChanged?.Invoke();
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _items.RemoveAll(i => !i.IsPinned);
            }
            ItemsChanged?.Invoke();
        }

        public bool CopyToClipboard(ClipboardItem item)
        {
            if (item == null) return false;
            try
            {
                if (item.Kind == ClipKind.Image && item.Image != null)
                {
                    System.Windows.Clipboard.SetImage(item.Image);
                    return true;
                }
                if (item.Kind == ClipKind.Files && item.FilePaths.Count > 0)
                {
                    var sc = new System.Collections.Specialized.StringCollection();
                    sc.AddRange(item.FilePaths.ToArray());
                    System.Windows.Clipboard.SetFileDropList(sc);
                    return true;
                }
                if (!string.IsNullOrEmpty(item.Text))
                {
                    System.Windows.Clipboard.SetText(item.Text);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool IsUrl(string text, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (text.Contains('\n') || text.Contains('\r')) return false;
            if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return Uri.TryCreate(text, UriKind.Absolute, out uri);
        }

        private static bool DetectIsCode(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.TrimStart();
            if (trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("<div", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("{") ||
                trimmed.StartsWith("[") ||
                trimmed.StartsWith("import ") ||
                trimmed.StartsWith("export ") ||
                trimmed.StartsWith("const ") ||
                trimmed.StartsWith("var ") ||
                trimmed.StartsWith("let ") ||
                trimmed.StartsWith("function ") ||
                trimmed.StartsWith("class ") ||
                trimmed.StartsWith("public ") ||
                trimmed.StartsWith("private ") ||
                trimmed.StartsWith("protected ") ||
                trimmed.StartsWith("#include") ||
                trimmed.StartsWith("def ") ||
                trimmed.StartsWith("npm ") ||
                trimmed.StartsWith("git "))
            {
                return true;
            }

            if (text.Contains("function(") || text.Contains("=>") || text.Contains("</") || text.Contains("();") || text.Contains("xmlns="))
            {
                return true;
            }

            return false;
        }

        private static void DetectOwnerApp(ClipboardItem item)
        {
            try
            {
                IntPtr hwnd = NativeMethods.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
                    if (pid > 0)
                    {
                        using var proc = Process.GetProcessById((int)pid);
                        string name = proc.ProcessName.ToLowerInvariant();
                        item.SourceAppExe = name + ".exe";
                        item.SourceApp = name switch
                        {
                            "chrome" => "Google Chrome",
                            "msedge" => "Microsoft Edge",
                            "firefox" => "Firefox",
                            "code" => "Visual Studio Code",
                            "devenv" => "Visual Studio",
                            "telegram" => "Telegram",
                            "discord" => "Discord",
                            "notepad" => "Notepad",
                            "explorer" => "File Explorer",
                            _ => proc.ProcessName
                        };
                    }
                }
            }
            catch { }
        }

        private static ImageSource CreateThumbnail(BitmapSource source, int width, int height)
        {
            try
            {
                double scaleX = (double)width / source.PixelWidth;
                double scaleY = (double)height / source.PixelHeight;
                double scale = Math.Max(scaleX, scaleY);

                var transformed = new TransformedBitmap(source, new ScaleTransform(scale, scale));
                var cropped = new CroppedBitmap(transformed, new Int32Rect(
                    Math.Max(0, (transformed.PixelWidth - width) / 2),
                    Math.Max(0, (transformed.PixelHeight - height) / 2),
                    Math.Min(width, transformed.PixelWidth),
                    Math.Min(height, transformed.PixelHeight)));
                cropped.Freeze();
                return cropped;
            }
            catch
            {
                return source;
            }
        }
    }
}
