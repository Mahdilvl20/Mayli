using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
            LoadPersistentItems();
        }

        private void LoadPersistentItems()
        {
            try
            {
                var loaded = ClipboardDatabase.Instance.LoadAllItems(100);
                lock (_lock)
                {
                    _items.Clear();
                    if (loaded != null && loaded.Count > 0)
                    {
                        _items.AddRange(loaded);
                    }
                    else
                    {
                        SeedInitialItems();
                        foreach (var it in _items)
                        {
                            ClipboardDatabase.Instance.SaveItem(it);
                        }
                    }
                }
            }
            catch
            {
                SeedInitialItems();
            }
        }

        private void SeedInitialItems()
        {
            var svgItem = new ClipboardItem
            {
                Kind = ClipKind.Svg,
                FormatType = "image/svg+xml",
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
                FormatType = "text/plain",
                Text = "متن نمونه در کلیپ‌برد Mayli",
                LineCount = 1,
                CharCount = 28,
                WordCount = 5,
                SourceApp = "Telegram",
                SourceAppExe = "telegram.exe",
                TimestampLocal = DateTime.Now.AddHours(-1)
            };
            _items.Add(persianText);

            var linkItem = new ClipboardItem
            {
                Kind = ClipKind.Link,
                FormatType = "text/uri-list",
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
            if (!TryCapture())
            {
                Application.Current?.Dispatcher?.InvokeAsync(async () =>
                {
                    await Task.Delay(50);
                    if (!TryCapture())
                    {
                        await Task.Delay(100);
                        TryCapture();
                    }
                });
            }
        }

        private bool TryCapture()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsImage())
                {
                    var img = System.Windows.Clipboard.GetImage();
                    if (img != null)
                    {
                        AddImageItem(img);
                        return true;
                    }
                }
                else if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var files = System.Windows.Clipboard.GetFileDropList();
                    if (files != null && files.Count > 0)
                    {
                        AddFilesItem(files);
                        return true;
                    }
                }
                else if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        AddTextItem(text);
                        return true;
                    }
                }
            }
            catch (COMException)
            {
                // Clipboard temporarily locked by another app
                return false;
            }
            catch
            {
                return true;
            }

            return true;
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
                    ClipboardDatabase.Instance.SaveItem(existing);
                    ItemsChanged?.Invoke();
                    return;
                }

                var item = new ClipboardItem
                {
                    Text = text,
                    TimestampLocal = DateTime.Now
                };

                // Classify Kind and Format
                if (DetectIsSvg(text))
                {
                    item.Kind = ClipKind.Svg;
                    item.FormatType = "image/svg+xml";
                }
                else if (IsUrl(text, out Uri uri))
                {
                    item.Kind = ClipKind.Link;
                    item.LinkUri = uri;
                    item.LinkHost = uri.Host;
                    item.FormatType = "text/uri-list";
                }
                else if (DetectIsCode(text))
                {
                    item.Kind = ClipKind.Code;
                    item.FormatType = "text/code";
                }
                else if (Regex.IsMatch(text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    item.Kind = ClipKind.Email;
                    item.FormatType = "text/email";
                }
                else
                {
                    item.Kind = ClipKind.Text;
                    item.FormatType = "text/plain";
                }

                // Compute counts
                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                item.LineCount = lines.Length;
                item.CharCount = text.Length;
                item.WordCount = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

                DetectOwnerApp(item);

                // Save to SQLite (with automatic size policy enforcement)
                ClipboardDatabase.Instance.SaveItem(item);

                _items.Insert(0, item);
                if (_items.Count > 100)
                {
                    var removed = _items[_items.Count - 1];
                    if (!removed.IsPinned)
                    {
                        _items.RemoveAt(_items.Count - 1);
                        ClipboardDatabase.Instance.DeleteItem(removed.Id);
                    }
                }
            }

            ItemsChanged?.Invoke();
        }

        public void AddImageItem(BitmapSource bitmap, string format = "image/png")
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
                    FormatType = format,
                    Image = bitmap,
                    Thumbnail = thumb,
                    ImageWidth = w,
                    ImageHeight = h,
                    RawBlobData = ClipboardDatabase.EncodeBitmapToPng(bitmap),
                    TimestampLocal = DateTime.Now
                };

                item.FileSizeBytes = item.RawBlobData.Length;

                DetectOwnerApp(item);

                // Save to SQLite / separate file per policy
                ClipboardDatabase.Instance.SaveItem(item);

                _items.Insert(0, item);
                if (_items.Count > 100)
                {
                    var removed = _items[_items.Count - 1];
                    if (!removed.IsPinned)
                    {
                        _items.RemoveAt(_items.Count - 1);
                        ClipboardDatabase.Instance.DeleteItem(removed.Id);
                    }
                }
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
                    FilePaths = list,
                    Text = string.Join(Environment.NewLine, list),
                    TimestampLocal = DateTime.Now
                };

                // Classify based on file extension
                if (list.Count == 1)
                {
                    string file = list[0];
                    string ext = Path.GetExtension(file).ToLowerInvariant();

                    try
                    {
                        if (File.Exists(file))
                        {
                            item.FileSizeBytes = new FileInfo(file).Length;
                        }
                    }
                    catch { }

                    if (ext == ".pdf")
                    {
                        item.Kind = ClipKind.Pdf;
                        item.FormatType = "application/pdf";
                    }
                    else if (ext is ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" or ".ogg" or ".wma")
                    {
                        item.Kind = ClipKind.Audio;
                        item.FormatType = "audio/" + ext.TrimStart('.');
                    }
                    else if (ext is ".mp4" or ".mkv" or ".avi" or ".mov" or ".wmv" or ".flv" or ".webm")
                    {
                        item.Kind = ClipKind.Video;
                        item.FormatType = "video/" + ext.TrimStart('.');
                    }
                    else if (ext is ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2")
                    {
                        item.Kind = ClipKind.Archive;
                        item.FormatType = "application/zip";
                    }
                    else if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".webp")
                    {
                        item.Kind = ClipKind.Image;
                        item.FormatType = "image/" + ext.TrimStart('.');
                        try
                        {
                            using var stream = File.OpenRead(file);
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.StreamSource = stream;
                            bmp.EndInit();
                            bmp.Freeze();
                            item.Image = bmp;
                            item.ImageWidth = bmp.PixelWidth;
                            item.ImageHeight = bmp.PixelHeight;
                            item.Thumbnail = CreateThumbnail(bmp, 28, 28);
                        }
                        catch { }
                    }
                    else if (ext == ".svg")
                    {
                        item.Kind = ClipKind.Svg;
                        item.FormatType = "image/svg+xml";
                        try
                        {
                            if (File.Exists(file)) item.Text = File.ReadAllText(file);
                        }
                        catch { }
                    }
                    else
                    {
                        item.Kind = ClipKind.Files;
                        item.FormatType = "files";
                    }
                }
                else
                {
                    item.Kind = ClipKind.Files;
                    item.FormatType = "files";
                    long totalSize = 0;
                    foreach (var f in list)
                    {
                        try { if (File.Exists(f)) totalSize += new FileInfo(f).Length; } catch { }
                    }
                    item.FileSizeBytes = totalSize;
                }

                DetectOwnerApp(item);

                // Save to SQLite
                ClipboardDatabase.Instance.SaveItem(item);

                _items.Insert(0, item);
                if (_items.Count > 100)
                {
                    var removed = _items[_items.Count - 1];
                    if (!removed.IsPinned)
                    {
                        _items.RemoveAt(_items.Count - 1);
                        ClipboardDatabase.Instance.DeleteItem(removed.Id);
                    }
                }
            }

            ItemsChanged?.Invoke();
        }

        public void TogglePin(ClipboardItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                item.IsPinned = !item.IsPinned;
                ClipboardDatabase.Instance.UpdatePinned(item.Id, item.IsPinned);
            }
            ItemsChanged?.Invoke();
        }

        public void DeleteItem(ClipboardItem item)
        {
            if (item == null) return;
            lock (_lock)
            {
                _items.Remove(item);
                ClipboardDatabase.Instance.DeleteItem(item.Id);
            }
            ItemsChanged?.Invoke();
        }

        public void ClearAll()
        {
            lock (_lock)
            {
                _items.RemoveAll(i => !i.IsPinned);
                ClipboardDatabase.Instance.ClearAll(keepPinned: true);
            }
            ItemsChanged?.Invoke();
        }

        public bool CopyToClipboard(ClipboardItem item)
        {
            if (item == null) return false;
            try
            {
                if (item.Kind == ClipKind.Image)
                {
                    var bmp = item.EnsureImage();
                    if (bmp != null)
                    {
                        System.Windows.Clipboard.SetImage(bmp);
                        return true;
                    }
                }

                if ((item.Kind == ClipKind.Files || item.Kind == ClipKind.Pdf ||
                     item.Kind == ClipKind.Audio || item.Kind == ClipKind.Video || item.Kind == ClipKind.Archive) &&
                    item.FilePaths.Count > 0)
                {
                    var sc = new System.Collections.Specialized.StringCollection();
                    sc.AddRange(item.FilePaths.ToArray());
                    System.Windows.Clipboard.SetFileDropList(sc);
                    return true;
                }

                string text = item.GetFullText();
                if (!string.IsNullOrEmpty(text))
                {
                    System.Windows.Clipboard.SetText(text);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool DetectIsSvg(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.TrimStart();
            return trimmed.StartsWith("<svg", StringComparison.OrdinalIgnoreCase) ||
                   (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && text.Contains("<svg"));
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
            if (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) ||
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
                if (source.PixelWidth == 0 || source.PixelHeight == 0) return source;
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
