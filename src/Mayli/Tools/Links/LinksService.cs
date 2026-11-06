using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Links
{
    public sealed class LinksService
    {
        private static readonly Lazy<LinksService> _instance = new(() => new LinksService());
        public static LinksService Instance => _instance.Value;

        private const string PrefsFileName = "bookmarks.json";
        private readonly object _lock = new();

        public event Action StateChanged;

        public List<LinksBookmark> Bookmarks { get; private set; } = new();
        public List<SearchEngineItem> Engines { get; private set; } = new();
        public string SelectedEngine { get; set; } = "Google";
        public string ActiveCategory { get; set; } = "All";

        public static readonly string[] Categories = new[]
        {
            "All",
            "📌 Pinned",
            "💻 Dev",
            "💼 Work",
            "🎬 Media",
            "💬 Social",
            "🌐 General"
        };

        private LinksService()
        {
            InitEngines();
            Load();
        }

        private void InitEngines()
        {
            Engines = new List<SearchEngineItem>
            {
                new SearchEngineItem { Name = "Google", SearchUrl = "https://www.google.com/search?q={0}", Domain = "google.com", IconGlyph = "G" },
                new SearchEngineItem { Name = "DuckDuckGo", SearchUrl = "https://duckduckgo.com/?q={0}", Domain = "duckduckgo.com", IconGlyph = "🦆" },
                new SearchEngineItem { Name = "Bing", SearchUrl = "https://www.bing.com/search?q={0}", Domain = "bing.com", IconGlyph = "b" },
                new SearchEngineItem { Name = "YouTube", SearchUrl = "https://www.youtube.com/results?search_query={0}", Domain = "youtube.com", IconGlyph = "▶" },
                new SearchEngineItem { Name = "GitHub", SearchUrl = "https://github.com/search?q={0}", Domain = "github.com", IconGlyph = "🐈" },
                new SearchEngineItem { Name = "ChatGPT", SearchUrl = "https://chatgpt.com/?q={0}", Domain = "chatgpt.com", IconGlyph = "✻" },
                new SearchEngineItem { Name = "Perplexity", SearchUrl = "https://www.perplexity.ai/search?q={0}", Domain = "perplexity.ai", IconGlyph = "✳" },
                new SearchEngineItem { Name = "Wikipedia", SearchUrl = "https://en.wikipedia.org/wiki/Special:Search?search={0}", Domain = "wikipedia.org", IconGlyph = "W" }
            };
        }

        public void Load()
        {
            lock (_lock)
            {
                try
                {
                    var saved = PersistenceHelper.SafeReadJson<LinksStorageDto>(PrefsFileName);
                    if (saved != null && saved.Bookmarks != null && saved.Bookmarks.Count > 0)
                    {
                        Bookmarks = saved.Bookmarks;
                        Bookmarks.RemoveAll(b => (b.Name != null && b.Name.StartsWith("may", StringComparison.OrdinalIgnoreCase)) ||
                                                 (b.Url != null && b.Url.Contains("mayli", StringComparison.OrdinalIgnoreCase)));
                        if (Bookmarks.Count == 0)
                        {
                            InitDefaults();
                        }
                        if (!string.IsNullOrEmpty(saved.SelectedEngine))
                        {
                            SelectedEngine = saved.SelectedEngine;
                        }
                        if (!string.IsNullOrEmpty(saved.ActiveCategory))
                        {
                            ActiveCategory = saved.ActiveCategory;
                        }
                    }
                    else
                    {
                        InitDefaults();
                    }
                }
                catch
                {
                    InitDefaults();
                }
            }
        }

        private void InitDefaults()
        {
            Bookmarks = new List<LinksBookmark>
            {
                new LinksBookmark
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "GitHub",
                    Url = "https://github.com",
                    Category = "💻 Dev",
                    Pinned = true,
                    AddedAt = DateTime.Now
                }
            };
            SelectedEngine = "Google";
            ActiveCategory = "All";
            Save();
        }

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var dto = new LinksStorageDto
                    {
                        SelectedEngine = SelectedEngine,
                        ActiveCategory = ActiveCategory,
                        Bookmarks = Bookmarks
                    };
                    PersistenceHelper.SafeWriteJson(PrefsFileName, dto);
                }
                catch { }
            }
        }

        public void SetSelectedEngine(string engineName)
        {
            lock (_lock)
            {
                if (Engines.Any(e => string.Equals(e.Name, engineName, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectedEngine = engineName;
                    Save();
                    StateChanged?.Invoke();
                }
            }
        }

        public void SetActiveCategory(string category)
        {
            lock (_lock)
            {
                ActiveCategory = category;
                Save();
                StateChanged?.Invoke();
            }
        }

        public void PerformSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                var eng = Engines.FirstOrDefault(e => string.Equals(e.Name, SelectedEngine, StringComparison.OrdinalIgnoreCase)) ?? Engines[0];
                OpenUrl("https://" + eng.Domain);
                return;
            }

            var selected = Engines.FirstOrDefault(e => string.Equals(e.Name, SelectedEngine, StringComparison.OrdinalIgnoreCase)) ?? Engines[0];
            string url = selected.BuildSearchUrl(query);
            OpenUrl(url);
        }

        public void AddBookmark(string name, string url, string category = "🌐 General", bool pinned = false)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            string normalizedUrl = NormalizeUrl(url);
            string cleanName = CleanTitle(name, normalizedUrl);

            lock (_lock)
            {
                Bookmarks.Insert(0, new LinksBookmark
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = cleanName,
                    Url = normalizedUrl,
                    Category = string.IsNullOrWhiteSpace(category) ? "🌐 General" : category,
                    Pinned = pinned,
                    AddedAt = DateTime.Now
                });
                Save();
                StateChanged?.Invoke();
            }
        }

        public void UpdateBookmark(string id, string name, string url, string category, bool pinned)
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(url)) return;

            string normalizedUrl = NormalizeUrl(url);
            string cleanName = CleanTitle(name, normalizedUrl);

            lock (_lock)
            {
                var item = Bookmarks.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.Name = cleanName;
                    item.Url = normalizedUrl;
                    item.Category = string.IsNullOrWhiteSpace(category) ? "🌐 General" : category;
                    item.Pinned = pinned;
                    Save();
                    StateChanged?.Invoke();
                }
            }
        }

        public void TogglePin(string id)
        {
            lock (_lock)
            {
                var item = Bookmarks.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.Pinned = !item.Pinned;
                    Save();
                    StateChanged?.Invoke();
                }
            }
        }

        public void RecordVisit(string id)
        {
            lock (_lock)
            {
                var item = Bookmarks.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.VisitCount++;
                    Save();
                    OpenUrl(item.Url);
                    StateChanged?.Invoke();
                    return;
                }
            }
        }

        public void RemoveBookmark(string id)
        {
            lock (_lock)
            {
                var item = Bookmarks.FirstOrDefault(b => string.Equals(b.Id, id, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    Bookmarks.Remove(item);
                    Save();
                    StateChanged?.Invoke();
                }
            }
        }

        public List<LinksBookmark> GetFilteredBookmarks(string query, string category)
        {
            IEnumerable<LinksBookmark> list = Bookmarks;

            // Category filter
            if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (category.Contains("Pinned", StringComparison.OrdinalIgnoreCase))
                {
                    list = list.Where(b => b.Pinned);
                }
                else
                {
                    list = list.Where(b => string.Equals(b.Category, category, StringComparison.OrdinalIgnoreCase)
                                        || b.Category.Contains(category, StringComparison.OrdinalIgnoreCase));
                }
            }

            // Search query filter
            if (!string.IsNullOrWhiteSpace(query))
            {
                string q = query.Trim().ToLowerInvariant();
                list = list.Where(b => (b.Name != null && b.Name.ToLowerInvariant().Contains(q))
                                    || (b.Url != null && b.Url.ToLowerInvariant().Contains(q))
                                    || (b.Domain != null && b.Domain.ToLowerInvariant().Contains(q)));
            }

            // Order: Pinned first, then VisitCount desc, then AddedAt desc
            return list.OrderByDescending(b => b.Pinned)
                       .ThenByDescending(b => b.VisitCount)
                       .ThenByDescending(b => b.AddedAt)
                       .ToList();
        }

        private static string NormalizeUrl(string url)
        {
            string trimmed = url.Trim();
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return "https://" + trimmed;
            }
            return trimmed;
        }

        private static string CleanTitle(string name, string url)
        {
            if (!string.IsNullOrWhiteSpace(name)) return name.Trim();
            try
            {
                var uri = new Uri(url);
                string host = uri.Host.ToLowerInvariant();
                if (host.StartsWith("www.")) host = host.Substring(4);
                return host;
            }
            catch
            {
                return url;
            }
        }

        public static void OpenUrl(string url)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }
    }
}
