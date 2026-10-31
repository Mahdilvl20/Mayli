using System;
using System.Collections.Generic;
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
        Files
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
        public string Text { get; set; } = string.Empty;
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

        public string GetKindLabel()
        {
            return Kind switch
            {
                ClipKind.Code => "Code",
                ClipKind.Link => "Link",
                ClipKind.Image => "Image",
                ClipKind.Email => "Email",
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
            if (Kind == ClipKind.Files)
            {
                return FilePaths.Count > 0 ? System.IO.Path.GetFileName(FilePaths[0]) : "Files";
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
            if (Kind == ClipKind.Code)
            {
                return $"{LineCount} lines · {time}";
            }
            if (Kind == ClipKind.Link)
            {
                return $"{LinkHost} · {time}";
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
            string charsStr = CharCount >= 1000 ? $"{CharCount / 1000.0:F1}k chars" : $"{CharCount} chars";
            if (LineCount > 1)
            {
                return $"{LineCount} lines · {charsStr}";
            }
            return $"{WordCount} words · {charsStr}";
        }
    }
}
