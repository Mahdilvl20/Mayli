using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsBuddy.Tools.Clipboard
{
    public enum ClipKind
    {
        Text,
        Code,
        Email,
        Link,
        Image,
        Files,
        Html,
        Rtf,
        Svg,
        Pdf,
        Audio,
        Video,
        Archive
    }

    public enum ClipboardFilter
    {
        All,
        Text,
        Images,
        Links
    }

    public sealed class ClipboardItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public ClipKind Kind { get; set; } = ClipKind.Text;
        public string FormatType { get; set; } = string.Empty;
        public string StorageMethod { get; set; } = "sqlite_text"; // "sqlite_text", "sqlite_blob", "file"
        public string StoredFilePath { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public byte[] RawBlobData { get; set; }
        public BitmapSource Image { get; set; }
        public ImageSource Thumbnail { get; set; }
        public int ImageWidth { get; set; }
        public int ImageHeight { get; set; }
        public long FileSizeBytes { get; set; }
        public int LineCount { get; set; }
        public int WordCount { get; set; }
        public int CharCount { get; set; }
        public string LinkHost { get; set; } = string.Empty;
        public Uri LinkUri { get; set; }
        public List<string> FilePaths { get; set; } = new();
        public DateTime TimestampLocal { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public string SourceApp { get; set; } = "Windows";
        public string SourceAppExe { get; set; } = string.Empty;
        public string ImageHash { get; set; } = string.Empty;

        public string GetImageHash()
        {
            if (!string.IsNullOrEmpty(ImageHash)) return ImageHash;

            byte[] bytes = RawBlobData;
            if ((bytes == null || bytes.Length == 0) &&
                !string.IsNullOrEmpty(StoredFilePath) &&
                File.Exists(StoredFilePath))
            {
                try { bytes = File.ReadAllBytes(StoredFilePath); } catch { }
            }

            if (bytes != null && bytes.Length > 0)
            {
                ImageHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
                return ImageHash;
            }

            if (ImageWidth > 0 && ImageHeight > 0)
            {
                return $"dim_{ImageWidth}x{ImageHeight}_{FileSizeBytes}";
            }

            return string.Empty;
        }

        public string GetFullText()
        {
            if (string.Equals(StorageMethod, "file", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(StoredFilePath) &&
                File.Exists(StoredFilePath))
            {
                try
                {
                    return File.ReadAllText(StoredFilePath, System.Text.Encoding.UTF8);
                }
                catch { }
            }
            return Text ?? string.Empty;
        }

        public BitmapSource EnsureImage()
        {
            if (Image != null) return Image;

            if (RawBlobData != null && RawBlobData.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream(RawBlobData);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    Image = bmp;
                    return Image;
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(StoredFilePath) && File.Exists(StoredFilePath))
            {
                try
                {
                    using var fs = File.OpenRead(StoredFilePath);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = fs;
                    bmp.EndInit();
                    bmp.Freeze();
                    Image = bmp;
                    return Image;
                }
                catch { }
            }

            return null;
        }

        public string GetKindLabel()
        {
            return Kind switch
            {
                ClipKind.Code => "Code",
                ClipKind.Link => "Link",
                ClipKind.Image => "Image",
                ClipKind.Email => "Email",
                ClipKind.Html => "HTML",
                ClipKind.Rtf => "RTF",
                ClipKind.Svg => "SVG",
                ClipKind.Pdf => "PDF",
                ClipKind.Audio => "Audio",
                ClipKind.Video => "Video",
                ClipKind.Archive => "Archive",
                ClipKind.Files => "Files",
                _ => "Text"
            };
        }

        public string GetTimestampFormatted()
        {
            var diff = DateTime.Now - TimestampLocal;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return TimestampLocal.ToString("MMM d");
        }

        public string GetDisplayTitle()
        {
            if (Kind == ClipKind.Image)
            {
                return "Image";
            }
            if (Kind == ClipKind.Pdf)
            {
                return FilePaths.Count > 0 ? Path.GetFileName(FilePaths[0]) : "PDF Document";
            }
            if (Kind == ClipKind.Audio)
            {
                return FilePaths.Count > 0 ? Path.GetFileName(FilePaths[0]) : "Audio File";
            }
            if (Kind == ClipKind.Video)
            {
                return FilePaths.Count > 0 ? Path.GetFileName(FilePaths[0]) : "Video File";
            }
            if (Kind == ClipKind.Archive)
            {
                return FilePaths.Count > 0 ? Path.GetFileName(FilePaths[0]) : "Archive";
            }
            if (Kind == ClipKind.Files)
            {
                return FilePaths.Count > 0 ? Path.GetFileName(FilePaths[0]) : "Files";
            }

            if (string.IsNullOrWhiteSpace(Text)) return "(empty)";
            string firstLine = Text.TrimStart();
            int nl = firstLine.IndexOfAny(new[] { '\r', '\n' });
            if (nl > 0) firstLine = firstLine.Substring(0, nl);
            return firstLine.Length > 60 ? firstLine.Substring(0, 60) + "..." : firstLine;
        }

        public string GetDisplaySubtitle()
        {
            string time = GetTimestampFormatted();
            if (Kind == ClipKind.Image)
            {
                return $"{ImageWidth} × {ImageHeight} · {time}";
            }
            if (Kind == ClipKind.Code || Kind == ClipKind.Svg)
            {
                return $"{LineCount} lines · {time}";
            }
            if (Kind == ClipKind.Link)
            {
                return $"{LinkHost} · {time}";
            }
            if (Kind == ClipKind.Pdf || Kind == ClipKind.Audio || Kind == ClipKind.Video || Kind == ClipKind.Archive)
            {
                string size = FileSizeBytes > 0 ? $"{(FileSizeBytes / 1024.0 / 1024.0 >= 1 ? $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB" : $"{FileSizeBytes / 1024} KB")}" : "";
                return string.IsNullOrEmpty(size) ? time : $"{size} · {time}";
            }
            if (WordCount > 0)
            {
                return $"{WordCount} {(WordCount == 1 ? "word" : "words")} · {time}";
            }
            return $"{CharCount} chars · {time}";
        }

        public string GetFooterMeta()
        {
            if (Kind == ClipKind.Image)
            {
                string sizeStr = FileSizeBytes > 0 ? $"{FileSizeBytes / 1024} KB" : "PNG";
                return $"{ImageWidth} × {ImageHeight} · {sizeStr}";
            }
            if (Kind == ClipKind.Link)
            {
                return LinkHost ?? string.Empty;
            }
            if (Kind == ClipKind.Pdf || Kind == ClipKind.Audio || Kind == ClipKind.Video || Kind == ClipKind.Archive || Kind == ClipKind.Files)
            {
                string sizeStr = FileSizeBytes > 0 ? $"{(FileSizeBytes / (1024.0 * 1024.0) >= 1 ? $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB" : $"{FileSizeBytes / 1024} KB")}" : "";
                int count = FilePaths?.Count ?? 0;
                string countStr = count > 1 ? $"{count} files · " : "";
                return $"{countStr}{sizeStr}";
            }
            string charsStr = CharCount >= 1000 ? $"{CharCount / 1000.0:F1}k chars" : $"{CharCount} chars";
            if (LineCount > 1)
            {
                return $"{LineCount} lines · {charsStr}";
            }
            return $"{WordCount} words · {charsStr}";
        }
    }
}
