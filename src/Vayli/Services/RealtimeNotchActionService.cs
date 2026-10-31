using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services
{
    public sealed class NotchNotificationPayload
    {
        public string Icon { get; set; } = "📋";
        public string Title { get; set; } = "Copied";
        public string Subtitle { get; set; } = " · Image";
        public int DurationMs { get; set; } = 3500;
        public bool IsMediaStatus { get; set; } = false;
    }

    /// <summary>
    /// RealtimeNotchActionService: Orchestrates real-time Windows events for the notch:
    /// 1. Real Clipboard & Screenshot events (Text, Image, Files, Screenshot saved)
    /// 2. Real Bluetooth & Audio device connection events (AirPods Pro, QCY-T13, Headsets)
    /// 3. Real Music & Media playback detection (Spotify, YouTube, Chrome, Media Player)
    /// </summary>
    public sealed class RealtimeNotchActionService
    {
        private static readonly Lazy<RealtimeNotchActionService> _lazy = new(() => new RealtimeNotchActionService());
        public static RealtimeNotchActionService Instance => _lazy.Value;

        public event Action<NotchNotificationPayload> NotificationRequested;
        public event Action<string, string, bool> MediaTrackChanged; // title, artist, isPlaying

        private readonly DispatcherTimer _pollTimer;
        private readonly HashSet<string> _knownBluetoothDevices = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<FileSystemWatcher> _screenshotWatchers = new();
        private DateTime _lastClipboardTrigger = DateTime.MinValue;
        private string _lastCopiedText = null;

        // Current media state
        public bool IsMusicPlaying { get; private set; }
        public string CurrentTrack { get; private set; } = "";
        public string CurrentArtist { get; private set; } = "";
        public string CurrentSourceApp { get; private set; } = "";

        private RealtimeNotchActionService()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _pollTimer.Tick += (_, _) =>
            {
                CheckMediaPlayback();
                CheckBluetoothDevices();
            };
            _pollTimer.Start();

            InitScreenshotWatchers();
            InitKnownBluetoothDevices();
        }

        #region 1. Clipboard & Screenshot Detection (WM_CLIPBOARDUPDATE)

        public void HandleClipboardUpdate()
        {
            try
            {
                // Debounce rapid duplicate clipboard events
                if ((DateTime.UtcNow - _lastClipboardTrigger).TotalMilliseconds < 350)
                    return;

                _lastClipboardTrigger = DateTime.UtcNow;

                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    try
                    {
                        var data = Clipboard.GetDataObject();
                        if (data == null) return;

                        // Case A: Image copied (Screenshot 2: Copied · Image)
                        if (Clipboard.ContainsImage() || data.GetDataPresent(DataFormats.Bitmap))
                        {
                            NotificationRequested?.Invoke(new NotchNotificationPayload
                            {
                                Icon = "📋",
                                Title = "Copied",
                                Subtitle = " · Image",
                                DurationMs = 3500
                            });
                            return;
                        }

                        // Case B: Files copied (e.g. from Explorer)
                        if (Clipboard.ContainsFileDropList())
                        {
                            var files = Clipboard.GetFileDropList();
                            if (files.Count > 0)
                            {
                                string desc = files.Count == 1 ? Path.GetFileName(files[0]) : $"{files.Count} files";
                                NotificationRequested?.Invoke(new NotchNotificationPayload
                                {
                                    Icon = "📁",
                                    Title = "Copied",
                                    Subtitle = $" · {desc}",
                                    DurationMs = 3500
                                });
                                return;
                            }
                        }

                        // Case C: Text copied
                        if (Clipboard.ContainsText())
                        {
                            string text = Clipboard.GetText()?.Trim();
                            if (!string.IsNullOrEmpty(text) && text != _lastCopiedText)
                            {
                                _lastCopiedText = text;
                                string display = text.Length > 20
                                    ? text.Substring(0, 18).Replace("\r", " ").Replace("\n", " ") + "..."
                                    : text.Replace("\r", " ").Replace("\n", " ");

                                NotificationRequested?.Invoke(new NotchNotificationPayload
                                {
                                    Icon = "📋",
                                    Title = "Copied",
                                    Subtitle = $" · \"{display}\"",
                                    DurationMs = 3500
                                });
                            }
                        }
                    }
                    catch
                    {
                        // Ignore clipboard contention
                    }
                });
            }
            catch { }
        }

        private void InitScreenshotWatchers()
        {
            var candidates = new List<string>
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Screenshots"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive", "Pictures", "Screenshots")
            };

            foreach (var dir in candidates)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        var watcher = new FileSystemWatcher(dir)
                        {
                            Filter = "*.*",
                            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
                            EnableRaisingEvents = true
                        };
                        watcher.Created += (_, e) =>
                        {
                            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
                            if (ext is ".png" or ".jpg" or ".jpeg" or ".bmp")
                            {
                                Application.Current?.Dispatcher?.InvokeAsync(() =>
                                {
                                    NotificationRequested?.Invoke(new NotchNotificationPayload
                                    {
                                        Icon = "📸",
                                        Title = "Screenshot saved",
                                        Subtitle = "",
                                        DurationMs = 3800
                                    });
                                });
                            }
                        };
                        _screenshotWatchers.Add(watcher);
                    }
                }
                catch { }
            }
        }

        #endregion

        #region 2. Real Bluetooth & Device Connection Detection (WM_DEVICECHANGE)

        public void HandleDeviceChange(int eventCode)
        {
            // DBT_DEVICEARRIVAL = 0x8000, DBT_DEVICEREMOVECOMPLETE = 0x8004, DBT_DEVNODES_CHANGED = 0x0007
            if (eventCode is 0x8000 or 0x8004 or 0x0007)
            {
                CheckBluetoothDevices(isDirectEvent: true);
            }
        }

        private void InitKnownBluetoothDevices()
        {
            try
            {
                var current = QueryActiveBluetoothDevices();
                foreach (var d in current)
                {
                    _knownBluetoothDevices.Add(d);
                }
            }
            catch { }
        }

        private void CheckBluetoothDevices(bool isDirectEvent = false)
        {
            try
            {
                var activeDevices = QueryActiveBluetoothDevices();

                // Check for newly connected devices
                foreach (var device in activeDevices)
                {
                    if (!_knownBluetoothDevices.Contains(device))
                    {
                        _knownBluetoothDevices.Add(device);

                        // Trigger exact "AirPods Pro Connected" style banner (Screenshot 2)
                        Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            NotificationRequested?.Invoke(new NotchNotificationPayload
                            {
                                Icon = "🎧",
                                Title = device,
                                Subtitle = "  Connected",
                                DurationMs = 4000
                            });
                        });
                    }
                }

                // Check for disconnected devices
                var disconnected = _knownBluetoothDevices.Where(d => !activeDevices.Contains(d)).ToList();
                foreach (var d in disconnected)
                {
                    _knownBluetoothDevices.Remove(d);
                    if (isDirectEvent)
                    {
                        Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            NotificationRequested?.Invoke(new NotchNotificationPayload
                            {
                                Icon = "🎧",
                                Title = d,
                                Subtitle = "  Disconnected",
                                DurationMs = 3000
                            });
                        });
                    }
                }
            }
            catch { }
        }

        private static HashSet<string> QueryActiveBluetoothDevices()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using var bthKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\BTHENUM");
                if (bthKey != null)
                {
                    foreach (var subName in bthKey.GetSubKeyNames())
                    {
                        using var devSub = bthKey.OpenSubKey(subName);
                        if (devSub == null) continue;

                        foreach (var instName in devSub.GetSubKeyNames())
                        {
                            using var inst = devSub.OpenSubKey(instName);
                            if (inst == null) continue;

                            string fn = inst.GetValue("FriendlyName")?.ToString();
                            if (string.IsNullOrWhiteSpace(fn))
                                fn = inst.GetValue("DeviceDesc")?.ToString();

                            if (!string.IsNullOrWhiteSpace(fn))
                            {
                                // Filter out generic system adapters
                                if (!fn.Contains("Standard Serial", StringComparison.OrdinalIgnoreCase) &&
                                    !fn.Contains("Enumerator", StringComparison.OrdinalIgnoreCase) &&
                                    !fn.Contains("Protocol", StringComparison.OrdinalIgnoreCase) &&
                                    !fn.Contains("@", StringComparison.OrdinalIgnoreCase))
                                {
                                    result.Add(fn.Trim());
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // If empty, return currently recorded audio endpoints (like QCY-T13 or AirPods)
            return result;
        }

        #endregion

        #region 3. Real Music Playback Detection (Spotify, Chrome, YouTube, VLC)

        private void CheckMediaPlayback()
        {
            try
            {
                bool wasPlaying = IsMusicPlaying;
                string prevTrack = CurrentTrack;

                bool detected = false;
                string track = null;
                string artist = null;
                string app = null;

                // 1. Check Spotify
                var spotifyProcs = Process.GetProcessesByName("Spotify");
                if (spotifyProcs.Length > 0)
                {
                    foreach (var proc in spotifyProcs)
                    {
                        string title = proc.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title) &&
                            !string.Equals(title, "Spotify", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(title, "Spotify Free", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(title, "Spotify Premium", StringComparison.OrdinalIgnoreCase))
                        {
                            detected = true;
                            app = "Spotify";
                            int dash = title.IndexOf(" - ", StringComparison.Ordinal);
                            if (dash > 0)
                            {
                                artist = title.Substring(0, dash).Trim();
                                track = title.Substring(dash + 3).Trim();
                            }
                            else
                            {
                                track = title;
                                artist = "Spotify";
                            }
                            break;
                        }
                    }
                }

                // 2. Check Web Browsers & Media Players (Chrome, Edge, Firefox, VLC)
                if (!detected)
                {
                    foreach (var procName in new[] { "chrome", "msedge", "firefox", "vlc", "Music.UI" })
                    {
                        var procs = Process.GetProcessesByName(procName);
                        foreach (var proc in procs)
                        {
                            string wTitle = proc.MainWindowTitle;
                            if (!string.IsNullOrWhiteSpace(wTitle))
                            {
                                if (wTitle.Contains("YouTube", StringComparison.OrdinalIgnoreCase) ||
                                    wTitle.Contains("SoundCloud", StringComparison.OrdinalIgnoreCase) ||
                                    wTitle.Contains("Spotify", StringComparison.OrdinalIgnoreCase) ||
                                    wTitle.Contains("▶", StringComparison.Ordinal))
                                {
                                    detected = true;
                                    app = procName;
                                    string clean = wTitle.Replace(" - YouTube", "")
                                                         .Replace(" - Google Chrome", "")
                                                         .Replace(" - Microsoft​ Edge", "")
                                                         .Replace(" - Mozilla Firefox", "")
                                                         .Replace("▶", "").Trim();
                                    int dash = clean.IndexOf(" - ", StringComparison.Ordinal);
                                    if (dash > 0)
                                    {
                                        artist = clean.Substring(0, dash).Trim();
                                        track = clean.Substring(dash + 3).Trim();
                                    }
                                    else
                                    {
                                        track = clean;
                                        artist = "Web Audio";
                                    }
                                    break;
                                }
                            }
                        }
                        if (detected) break;
                    }
                }

                IsMusicPlaying = detected;
                if (detected)
                {
                    CurrentTrack = track ?? "Playing Track";
                    CurrentArtist = artist ?? "Music Player";
                    CurrentSourceApp = app ?? "Media";

                    // Notify on track change
                    if (!string.Equals(CurrentTrack, prevTrack, StringComparison.OrdinalIgnoreCase))
                    {
                        MediaService.Instance.State.IsPlaying = true;
                        MediaService.Instance.State.Title = CurrentTrack;
                        MediaService.Instance.State.Artist = CurrentArtist;

                        Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            MediaTrackChanged?.Invoke(CurrentTrack, CurrentArtist, true);
                            NotificationRequested?.Invoke(new NotchNotificationPayload
                            {
                                Icon = "🎵",
                                Title = CurrentTrack,
                                Subtitle = $" · {CurrentArtist}",
                                DurationMs = 4000,
                                IsMediaStatus = true
                            });
                        });
                    }
                }
                else if (wasPlaying)
                {
                    MediaService.Instance.State.IsPlaying = false;
                    Application.Current?.Dispatcher?.InvokeAsync(() =>
                    {
                        MediaTrackChanged?.Invoke("", "", false);
                    });
                }
            }
            catch { }
        }

        #endregion
    }
}
