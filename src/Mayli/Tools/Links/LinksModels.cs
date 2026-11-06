using System;
using System.Collections.Generic;

namespace WindowsBuddy.Tools.Links
{
    public sealed class LinksBookmark
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Pinned { get; set; }
        public string Category { get; set; } = "General";
        public int VisitCount { get; set; } = 0;
        public DateTime AddedAt { get; set; } = DateTime.Now;

        public string Domain
        {
            get
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(Url)) return string.Empty;
                    string normalized = Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                       Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? Url : "https://" + Url;
                    var uri = new Uri(normalized);
                    return uri.Host.ToLowerInvariant();
                }
                catch
                {
                    return Url;
                }
            }
        }
    }

    public sealed class SearchEngineItem
    {
        public string Name { get; set; } = string.Empty;
        public string SearchUrl { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string IconGlyph { get; set; } = string.Empty;

        public string BuildSearchUrl(string query)
        {
            string enc = Uri.EscapeDataString(query.Trim());
            return string.Format(SearchUrl, enc);
        }
    }

    public sealed class LinksStorageDto
    {
        public string SelectedEngine { get; set; } = "Google";
        public string ActiveCategory { get; set; } = "All";
        public List<LinksBookmark> Bookmarks { get; set; } = new();
    }
}
