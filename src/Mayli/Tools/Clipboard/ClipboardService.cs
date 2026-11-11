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
                        var unique = new List<ClipboardItem>();
                        var seenImageHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var seenTexts = new HashSet<string>(StringComparer.Ordinal);
                        var duplicatesToDelete = new List<string>();

                        foreach (var it in loaded)
                        {
                            if (it.Kind == ClipKind.Image)
                            {
                                string hash = it.GetImageHash();
                                if (!string.IsNullOrEmpty(hash))
                                {
                                    if (seenImageHashes.Contains(hash))
                                    {
                                        duplicatesToDelete.Add(it.Id);
                                        continue;
                                    }
                                    seenImageHashes.Add(hash);
                                }
                            }
                            else if (it.Kind != ClipKind.Files)
                            {
                                string key = $"{it.Kind}:{it.Text?.Trim()}";
                                if (seenTexts.Contains(key))
                                {
                                    duplicatesToDelete.Add(it.Id);
                                    continue;
                                }
                                seenTexts.Add(key);
                            }

                            unique.Add(it);
                        }

                        _items.AddRange(unique);

                        foreach (var id in duplicatesToDelete)
                        {
                            try { ClipboardDatabase.Instance.DeleteItem(id); } catch { }
                        }
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
            // Fresh installations start with an empty clipboard history
        }

        private System.Windows.Threading.DispatcherTimer _captureDebounceTimer;
        private readonly object _timerLock = new();
        private DateTime _internalCopyTimestampUtc = DateTime.MinValue;
        private string _lastCapturedHash = string.Empty;
        private DateTime _lastCapturedTimeUtc = DateTime.MinValue;

        public void ScheduleCaptureDebounced(int delayMs = 150)
        {
            var app = Application.Current;
            if (app == null) return;

            app.Dispatcher?.InvokeAsync(() =>
            {
                lock (_timerLock)
                {
                    if (delayMs <= 0)
                    {
                        _captureDebounceTimer?.Stop();
                        _captureDebounceTimer = null;
                        CaptureCurrentClipboard();
                        return;
                    }

                    if (_captureDebounceTimer == null)
                    {
                        _captureDebounceTimer = new System.Windows.Threading.DispatcherTimer
                        {
                            Interval = TimeSpan.FromMilliseconds(delayMs)
                        };
                        _captureDebounceTimer.Tick += (_, _) =>
                        {
                            lock (_timerLock)
                            {
                                _captureDebounceTimer?.Stop();
                                _captureDebounceTimer = null;
                            }
                            CaptureCurrentClipboard();
                        };
                    }
                    else
                    {
                        _captureDebounceTimer.Stop();
                        _captureDebounceTimer.Interval = TimeSpan.FromMilliseconds(delayMs);
                    }

                    _captureDebounceTimer.Start();
                }
            });
        }

        public void CaptureCurrentClipboard()
        {
            if ((DateTime.UtcNow - _internalCopyTimestampUtc).TotalMilliseconds < 800)
            {
                // Self-copy suppression
                return;
            }

            if (!TryCapture())
            {
                Application.Current?.Dispatcher?.InvokeAsync(async () =>
                {
                    await Task.Delay(80);
                    TryCapture();
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

            byte[] rawBytes = ClipboardDatabase.EncodeBitmapToPng(bitmap);
            if (rawBytes == null || rawBytes.Length == 0) return;

            string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(rawBytes));

            // Rapid burst suppression: if the exact same image was captured within the last 1.2 seconds, ignore
            if (string.Equals(_lastCapturedHash, hash, StringComparison.OrdinalIgnoreCase) &&
                (DateTime.UtcNow - _lastCapturedTimeUtc).TotalMilliseconds < 1200)
            {
                return;
            }

            _lastCapturedHash = hash;
            _lastCapturedTimeUtc = DateTime.UtcNow;

            lock (_lock)
            {
                var existing = _items.FirstOrDefault(i => i.Kind == ClipKind.Image &&
                    (string.Equals(i.GetImageHash(), hash, StringComparison.OrdinalIgnoreCase) ||
                     (i.FileSizeBytes == rawBytes.Length && i.ImageWidth == bitmap.PixelWidth && i.ImageHeight == bitmap.PixelHeight && AreBlobsEqual(i, rawBytes))));

                if (existing != null)
                {
                    existing.TimestampLocal = DateTime.Now;
                    existing.ImageHash = hash;
                    int idx = _items.IndexOf(existing);
                    if (idx > 0)
                    {
                        _items.RemoveAt(idx);
                        _items.Insert(0, existing);
                    }
                    ClipboardDatabase.Instance.SaveItem(existing);
                    ItemsChanged?.Invoke();
                    return;
                }

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
                    RawBlobData = rawBytes,
                    ImageHash = hash,
                    FileSizeBytes = rawBytes.Length,
                    TimestampLocal = DateTime.Now
                };

                DetectOwnerApp(item);

                // Save to SQLite / separate file per policy
                ClipboardDatabase.Instance.SaveItem(item);

                // Release large in-memory raw buffer and full bitmap to prevent memory bloat
                item.ReleaseLargeObjects();

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

        private static bool AreBlobsEqual(ClipboardItem item, byte[] newBytes)
        {
            if (item == null || newBytes == null) return false;
            if (item.RawBlobData != null && item.RawBlobData.Length == newBytes.Length)
            {
                return item.RawBlobData.AsSpan().SequenceEqual(newBytes);
            }
            if (!string.IsNullOrEmpty(item.ImageHash))
            {
                string newHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(newBytes));
                return string.Equals(item.ImageHash, newHash, StringComparison.OrdinalIgnoreCase);
            }
            if (string.Equals(item.StorageMethod, "file", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(item.StoredFilePath) && File.Exists(item.StoredFilePath))
            {
                try
                {
                    var fi = new FileInfo(item.StoredFilePath);
                    if (fi.Length != newBytes.Length) return false;
                    byte[] fileBytes = File.ReadAllBytes(item.StoredFilePath);
                    return fileBytes.AsSpan().SequenceEqual(newBytes);
                }
                catch { }
            }
            return false;
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

                string joinedPaths = string.Join(Environment.NewLine, list);
                var existing = _items.FirstOrDefault(i =>
                    (i.Kind == ClipKind.Files || i.Kind == ClipKind.Pdf || i.Kind == ClipKind.Image || i.Kind == ClipKind.Audio || i.Kind == ClipKind.Video || i.Kind == ClipKind.Archive) &&
                    string.Equals(i.Text, joinedPaths, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    existing.TimestampLocal = DateTime.Now;
                    int idx = _items.IndexOf(existing);
                    if (idx > 0)
                    {
                        _items.RemoveAt(idx);
                        _items.Insert(0, existing);
                    }
                    ClipboardDatabase.Instance.SaveItem(existing);
                    ItemsChanged?.Invoke();
                    return;
                }

                var item = new ClipboardItem
                {
                    FilePaths = list,
                    Text = joinedPaths,
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

        public void ClearAll(bool keepPinned = true)
        {
            lock (_lock)
            {
                if (keepPinned)
                {
                    _items.RemoveAll(i => !i.IsPinned);
                }
                else
                {
                    _items.Clear();
                }
                ClipboardDatabase.Instance.ClearAll(keepPinned: keepPinned);
            }
            ItemsChanged?.Invoke();
        }

        public bool CopyToClipboard(ClipboardItem item)
        {
            if (item == null) return false;
            _internalCopyTimestampUtc = DateTime.UtcNow;
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
