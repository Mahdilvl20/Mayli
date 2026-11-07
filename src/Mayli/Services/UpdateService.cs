using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services
{
    public sealed class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
        public string HtmlUrl { get; set; } = "https://github.com/Mahdilvl20/Mayli/releases";
        public string DownloadUrl { get; set; } = "";
    }

    public static class UpdateService
    {
        private static string _currentVersion;
        public static string CurrentVersion
        {
            get
            {
                if (_currentVersion == null)
                {
                    try
                    {
                        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                        if (asm != null && !string.IsNullOrWhiteSpace(asm.Location) && File.Exists(asm.Location))
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
                            if (!string.IsNullOrWhiteSpace(fvi.ProductVersion))
                            {
                                string pv = fvi.ProductVersion.Split('+')[0].Trim();
                                var segs = pv.Split('.');
                                if (segs.Length == 4 && segs[3] == "0")
                                    _currentVersion = $"{segs[0]}.{segs[1]}.{segs[2]}";
                                else
                                    _currentVersion = pv;
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrWhiteSpace(_currentVersion))
                        _currentVersion = "1.1.63";
                }
                return _currentVersion;
            }
        }

        private const string RepoOwner = "Mahdilvl20";
        private const string RepoName = "Mayli";

        private static bool _hasCheckedThisSession = false;

        public static Version CurrentVersionObj => ParseVersion(CurrentVersion);

        public static Version ParseVersion(string tagOrVersion)
        {
            if (string.IsNullOrWhiteSpace(tagOrVersion))
                return new Version(1, 0, 0, 0);

            string clean = tagOrVersion.Trim().TrimStart('v', 'V');
            int dash = clean.IndexOf('-');
            if (dash > 0) clean = clean.Substring(0, dash);

            var parts = clean.Split('.');
            int p0 = parts.Length > 0 && int.TryParse(parts[0], out int val0) ? val0 : 1;
            int p1 = parts.Length > 1 && int.TryParse(parts[1], out int val1) ? val1 : 0;
            int p2 = parts.Length > 2 && int.TryParse(parts[2], out int val2) ? val2 : 0;
            int p3 = parts.Length > 3 && int.TryParse(parts[3], out int val3) ? val3 : 0;

            return new Version(p0, p1, p2, p3);
        }

        public static HttpClient CreateConfiguredClient()
        {
            var handler = new HttpClientHandler();

            // Detect system / user proxy if available (e.g. 127.0.0.1:10808)
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings");
                if (key != null)
                {
                    int proxyEnable = Convert.ToInt32(key.GetValue("ProxyEnable", 0));
                    string proxyServer = key.GetValue("ProxyServer")?.ToString();
                    if (proxyEnable == 1 && !string.IsNullOrWhiteSpace(proxyServer))
                    {
                        if (!proxyServer.StartsWith("http://") && !proxyServer.StartsWith("https://"))
                        {
                            proxyServer = "http://" + proxyServer;
                        }
                        handler.Proxy = new System.Net.WebProxy(proxyServer);
                        handler.UseProxy = true;
                    }
                }
            }
            catch { }

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mayli-App", CurrentVersion));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return client;
        }

        public static async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var result = new UpdateInfo();

            try
            {
                using var client = CreateConfiguredClient();

                // 1. Try GitHub Releases API first
                string releasesUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                try
                {
                    using var response = await client.GetAsync(releasesUrl).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() : "";
                        string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() : "";
                        string htmlUrl = root.TryGetProperty("html_url", out var htmlProp) ? htmlProp.GetString() : "";

                        if (!string.IsNullOrWhiteSpace(tagName))
                        {
                            var latestVer = ParseVersion(tagName);
                            if (latestVer > CurrentVersionObj)
                            {
                                result.HasUpdate = true;
                                result.LatestVersion = tagName.TrimStart('v', 'V');
                                result.ReleaseNotes = body ?? "";
                                result.HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? $"https://github.com/{RepoOwner}/{RepoName}/releases" : htmlUrl;
                                return result;
                            }
                        }
                    }
                }
                catch { }

                // 2. Fallback to version.json on raw.githubusercontent.com
                string rawVersionUrl = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/main/version.json";
                try
                {
                    using var response = await client.GetAsync(rawVersionUrl).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;

                        string verStr = root.TryGetProperty("version", out var verProp) ? verProp.GetString() : "";
                        string notes = root.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() : "";
                        string url = root.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : "";

                        if (!string.IsNullOrWhiteSpace(verStr))
                        {
                            var latestVer = ParseVersion(verStr);
                            if (latestVer > CurrentVersionObj)
                            {
                                result.HasUpdate = true;
                                result.LatestVersion = verStr.TrimStart('v', 'V');
                                result.ReleaseNotes = notes ?? "";
                                result.HtmlUrl = string.IsNullOrWhiteSpace(url) ? $"https://github.com/{RepoOwner}/{RepoName}/releases" : url;
                                return result;
                            }
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                LogWriter.WriteNow("update-check", $"Update check encountered error: {ex.Message}");
            }

            return result;
        }

        public static void CheckOnStartup()
        {
            if (_hasCheckedThisSession) return;
            _hasCheckedThisSession = true;

            Task.Run(async () =>
            {
                // Wait 4 seconds after boot so UI completely stabilizes first
                await Task.Delay(4000).ConfigureAwait(false);

                var info = await CheckForUpdatesAsync().ConfigureAwait(false);
                if (info.HasUpdate)
                {
                    Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        RealtimeNotchActionService.Instance.TriggerNotification(new NotchNotificationPayload
                        {
                            Icon = "🚀",
                            Title = "Update Available",
                            Subtitle = $" · v{info.LatestVersion} is ready",
                            DurationMs = 7000
                        });
                    });
                }
            });
        }
    }
}
