using System;

namespace WindowsBuddy.Tools.Notes
{
    public sealed class NoteItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsPinned { get; set; }
        public string ColorTag { get; set; } = "#FF5722";

        public string GetDisplayTitle()
        {
            if (!string.IsNullOrWhiteSpace(Title)) return Title;
            if (!string.IsNullOrWhiteSpace(Content))
            {
                string firstLine = Content.TrimStart();
                int nl = firstLine.IndexOfAny(new[] { '\r', '\n' });
                if (nl > 0) firstLine = firstLine.Substring(0, nl);
                return firstLine.Length > 40 ? firstLine.Substring(0, 40) + "..." : firstLine;
            }
            return "Untitled note";
        }

        public string GetDisplayPreview()
        {
            if (string.IsNullOrWhiteSpace(Content)) return GetFormattedTime();
            string trimmed = Content.Trim();
            int nl = trimmed.IndexOfAny(new[] { '\r', '\n' });
            if (nl > 0 && nl + 1 < trimmed.Length)
            {
                string rest = trimmed.Substring(nl + 1).TrimStart();
                int nl2 = rest.IndexOfAny(new[] { '\r', '\n' });
                if (nl2 > 0) rest = rest.Substring(0, nl2);
                return rest.Length > 35 ? rest.Substring(0, 35) + "..." : rest;
            }
            return GetFormattedTime();
        }

        public int GetWordCount()
        {
            if (string.IsNullOrWhiteSpace(Content)) return 0;
            return Content.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        public string GetFormattedTime()
        {
            var diff = DateTime.Now - UpdatedAt;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return UpdatedAt.ToString("MMM d");
        }
    }
}
