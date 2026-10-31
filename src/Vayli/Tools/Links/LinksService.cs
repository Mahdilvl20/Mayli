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
                new SearchEngineItem { Name = "YouTube", SearchUrl = "https://www.youtube.com/results?search_query={0}", Domain = "youtube.com", IconGlyph = "▶" },
                new SearchEngineItem { Name = "GitHub", SearchUrl = "https://github.com/search?q={0}", Domain = "github.com", IconGlyph = "🐈" },
                new SearchEngineItem { Name = "Stack Overflow", SearchUrl = "https://stackoverflow.com/search?q={0}", Domain = "stackoverflow.com", IconGlyph = "📚" },
                new SearchEngineItem { Name = "ChatGPT", SearchUrl = "https://chatgpt.com/?q={0}", Domain = "chatgpt.com", IconGlyph = "✻" },
                new SearchEngineItem { Name = "Perplexity", SearchUrl = "https://www.perplexity.ai/search?q={0}", Domain = "perplexity.ai", IconGlyph = "✳" }
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
                        if (!string.IsNullOrEmpty(saved.SelectedEngine))
                        {
                            SelectedEngine = saved.SelectedEngine;
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
                    Name = "vayli.in",
                    Url = "https://vayli.in",
                    Pinned = true
                },
                new LinksBookmark
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = "github.com",
                    Url = "https://github.com",
                    Pinned = true
                }
            };
            SelectedEngine = "Google";
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

        public void AddBookmark(string name, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            string normalizedUrl = url.Trim();
            if (!normalizedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !normalizedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                normalizedUrl = "https://" + normalizedUrl;
            }

            string cleanName = string.IsNullOrWhiteSpace(name) ? Path.GetFileName(normalizedUrl) : name.Trim();
            if (string.IsNullOrEmpty(cleanName))
            {
                try { cleanName = new Uri(normalizedUrl).Host; } catch { cleanName = normalizedUrl; }
            }

            lock (_lock)
            {
                Bookmarks.Add(new LinksBookmark
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = cleanName,
                    Url = normalizedUrl,
                    AddedAt = DateTime.Now
                });
                Save();
                StateChanged?.Invoke();
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
