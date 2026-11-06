using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace WindowsBuddy.Services
{
    public sealed class WindowsNotificationPayload
    {
        public string AppName { get; set; } = "Notification";
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public BitmapSource AppIcon { get; set; }
        public string AppGlyph { get; set; } = "🔔";
        public DateTime ReceivedAt { get; set; } = DateTime.Now;
    }

    public sealed class WindowsNotificationListenerService
    {
        private static readonly Lazy<WindowsNotificationListenerService> _instance =
            new(() => new WindowsNotificationListenerService());
        public static WindowsNotificationListenerService Instance => _instance.Value;

        public event Action<WindowsNotificationPayload> NotificationReceived;

        private UserNotificationListener _listener;
        private readonly HashSet<uint> _seenNotificationIds = new();
        private readonly ConcurrentDictionary<string, BitmapSource> _cachedAppIcons = new(StringComparer.OrdinalIgnoreCase);
        private bool _isInitialized = false;
        private bool _isRunning = false;

        private WindowsNotificationListenerService()
        {
        }

        private static void Log(string msg)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mayli", "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "notifications.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ListenerService] {msg}\r\n");
            }
            catch { }
        }

        public Task StartAsync()
        {
            if (_isInitialized) return Task.CompletedTask;
            _isInitialized = true;

            // Run ENTIRELY on a dedicated MTA background task so WinRT COM NEVER blocks or deadlocks STA WPF thread
            Task.Run(async () =>
            {
                try
                {
                    Log("StartAsync background MTA worker running.");
                    _listener = UserNotificationListener.Current;
                    if (_listener == null)
                    {
                        Log("UserNotificationListener.Current is null.");
                        return;
                    }

                    var access = await _listener.RequestAccessAsync();
                    Log($"RequestAccessAsync result: {access}");
                    if (access == UserNotificationListenerAccessStatus.Allowed)
                    {
                        try
                        {
                            var existing = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
                            if (existing != null)
                            {
                                foreach (var n in existing)
                                {
                                    _seenNotificationIds.Add(n.Id);
                                }
                                Log($"Pre-populated {_seenNotificationIds.Count} existing notifications.");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log($"Pre-populate error: {ex.Message}");
                        }

                        _isRunning = true;
                        Log("Starting background MTA polling loop at 250ms interval...");
                        await BackgroundPollerLoopAsync();
                    }
                }
                catch (Exception ex)
                {
                    Log($"Background MTA worker fatal exception: {ex.Message}");
                }
            });

            return Task.CompletedTask;
        }

        private async Task BackgroundPollerLoopAsync()
        {
            int loopCount = 0;
            while (_isRunning)
            {
                loopCount++;
                try
                {
                    if (_listener != null)
                    {
                        if (loopCount <= 3 || loopCount % 40 == 0)
                        {
                            Log($"[Poller Loop #{loopCount}] Calling GetNotificationsAsync...");
                        }

                        var current = await _listener.GetNotificationsAsync(NotificationKinds.Toast);

                        if (loopCount <= 3 || loopCount % 40 == 0)
                        {
                            Log($"[Poller Loop #{loopCount}] Got notifications, count={current?.Count}");
                        }

                        if (current != null)
                        {
                            foreach (var notif in current)
                            {
                                if (!_seenNotificationIds.Contains(notif.Id))
                                {
                                    _seenNotificationIds.Add(notif.Id);
                                    Log($"*** NEW NOTIFICATION DETECTED: ID={notif.Id} ***");
                                    await ProcessNotificationAsync(notif);
                                }
                            }

                            if (_seenNotificationIds.Count > 500)
                            {
                                var currentIds = new HashSet<uint>(current.Select(c => c.Id));
                                _seenNotificationIds.RemoveWhere(id => !currentIds.Contains(id));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log($"[BackgroundPollerLoopAsync ERROR loop #{loopCount}] {ex}");
                }

                await Task.Delay(250);
            }
        }

        private async Task ProcessNotificationAsync(UserNotification notif)
        {
            try
            {
                string appName = "";
                try
                {
                    appName = notif.AppInfo?.DisplayInfo?.DisplayName ?? "";
                }
                catch { }

                var binding = notif.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric)
                           ?? notif.Notification?.Visual?.Bindings?.FirstOrDefault();

                var texts = binding?.GetTextElements()
                    .Select(t => t.Text?.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList() ?? new List<string>();

                Log($"Processing ID={notif.Id}, RawAppName='{appName}', TextsCount={texts.Count}");

                if (texts.Count == 0 && string.IsNullOrEmpty(appName))
                    return;

                // If AppName is empty or generic, check if texts[0] is the app name
                if (string.IsNullOrEmpty(appName) && texts.Count >= 2)
                {
                    string first = texts[0];
                    if (first.Equals("Telegram", StringComparison.OrdinalIgnoreCase) ||
                        first.Equals("WhatsApp", StringComparison.OrdinalIgnoreCase) ||
                        first.Equals("Discord", StringComparison.OrdinalIgnoreCase) ||
                        first.Equals("Slack", StringComparison.OrdinalIgnoreCase) ||
                        first.Equals("Chrome", StringComparison.OrdinalIgnoreCase) ||
                        first.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase) ||
                        first.Equals("Antigravity", StringComparison.OrdinalIgnoreCase))
                    {
                        appName = first;
                        texts.RemoveAt(0);
                    }
                }

                string title = "";
                string message = "";

                if (texts.Count >= 2)
                {
                    title = texts[0];
                    message = string.Join(" - ", texts.Skip(1));
                }
                else if (texts.Count == 1)
                {
                    if (texts[0].Contains(": "))
                    {
                        var parts = texts[0].Split(new[] { ": " }, 2, StringSplitOptions.None);
                        title = parts[0];
                        message = parts[1];
                    }
                    else
                    {
                        title = !string.IsNullOrEmpty(appName) ? appName : "Notification";
                        message = texts[0];
                    }
                }

                if (string.IsNullOrEmpty(appName))
                {
                    appName = !string.IsNullOrEmpty(title) ? title : "Notification";
                }

                BitmapSource iconSource = null;

                // 1. Try UWP Logo reference
                try
                {
                    var logoRef = notif.AppInfo?.DisplayInfo?.GetLogo(new Windows.Foundation.Size(32, 32));
                    if (logoRef != null)
                    {
                        using var stream = await logoRef.OpenReadAsync();
                        var netStream = stream.AsStreamForRead();
                        var bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = netStream;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        iconSource = bi;
                    }
                }
                catch { }

                // 2. Win32 executable icon extraction
                if (iconSource == null)
                {
                    iconSource = GetNativeAppIcon(appName);
                }

                string glyph = GetAppGlyph(appName);

                Log($"Parsed Notification: App='{appName}', Title='{title}', Msg='{message}', Glyph='{glyph}', HasIcon={iconSource != null}");

                var payload = new WindowsNotificationPayload
                {
                    AppName = appName,
                    Title = string.IsNullOrWhiteSpace(title) ? appName : title,
                    Message = message,
                    AppIcon = iconSource,
                    AppGlyph = glyph,
                    ReceivedAt = DateTime.Now
                };

                NotificationReceived?.Invoke(payload);
            }
            catch (Exception ex)
            {
                Log($"ProcessNotificationAsync exception: {ex.Message}");
            }
        }

        private BitmapSource GetNativeAppIcon(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return null;

            if (_cachedAppIcons.TryGetValue(appName, out var cached))
                return cached;

            try
            {
                string exePath = null;

                if (appName.Contains("Telegram", StringComparison.OrdinalIgnoreCase))
                {
                    string standardTg = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Telegram Desktop", "Telegram.exe");
                    if (File.Exists(standardTg)) exePath = standardTg;
                }

                if (string.IsNullOrEmpty(exePath))
                {
                    var procs = Process.GetProcesses();
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.ProcessName.Contains(appName, StringComparison.OrdinalIgnoreCase) ||
                                appName.Contains(p.ProcessName, StringComparison.OrdinalIgnoreCase))
                            {
                                exePath = p.MainModule?.FileName;
                                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                                    break;
                            }
                        }
                        catch { }
                    }
                }

                if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                {
                    using var icon = Icon.ExtractAssociatedIcon(exePath);
                    if (icon != null)
                    {
                        var bmp = Imaging.CreateBitmapSourceFromHIcon(
                            icon.Handle,
                            System.Windows.Int32Rect.Empty,
                            BitmapSizeOptions.FromEmptyOptions());
                        bmp.Freeze();
                        _cachedAppIcons[appName] = bmp;
                        return bmp;
                    }
                }
            }
            catch { }

            return null;
        }

        public static string GetAppGlyph(string appName)
        {
            if (string.IsNullOrEmpty(appName)) return "🔔";
            string lower = appName.ToLowerInvariant();
            if (lower.Contains("telegram")) return "✈️";
            if (lower.Contains("whatsapp")) return "💬";
            if (lower.Contains("discord")) return "🎮";
            if (lower.Contains("slack")) return "💼";
            if (lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("firefox") || lower.Contains("browser")) return "🌐";
            if (lower.Contains("mail") || lower.Contains("outlook") || lower.Contains("gmail")) return "✉️";
            if (lower.Contains("spotify") || lower.Contains("music")) return "🎵";
            if (lower.Contains("code") || lower.Contains("visual studio")) return "💻";
            if (lower.Contains("download")) return "📥";
            return "🔔";
        }

        public void TriggerTestNotification(string appName = "Telegram", string title = "Sara", string message = "Hey, check out this Dynamic Island split notification! ✨")
        {
            var payload = new WindowsNotificationPayload
            {
                AppName = appName,
                Title = title,
                Message = message,
                AppIcon = GetNativeAppIcon(appName),
                AppGlyph = GetAppGlyph(appName),
                ReceivedAt = DateTime.Now
            };
            NotificationReceived?.Invoke(payload);
        }
    }
}
