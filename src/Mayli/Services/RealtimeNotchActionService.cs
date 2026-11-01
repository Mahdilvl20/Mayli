using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public sealed class MediaSessionSnapshot
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public bool IsPlaying { get; set; }
        public bool IsTrackSwitch { get; set; }
        public double PositionSeconds { get; set; }
        public double DurationSeconds { get; set; }
        public BitmapSource Artwork { get; set; }
        public Color BeatColor { get; set; } = Color.FromRgb(245, 245, 250);
        public string SourceApp { get; set; } = "";
    }

    /// <summary>
    /// RealtimeNotchActionService: Orchestrates real-time Windows events for the notch:
    /// 1. Real Clipboard & Screenshot events (Text, Image, Files, Screenshot saved)
    /// 2. Real Bluetooth & Audio device connection events (AirPods Pro, QCY-T13, Headsets)
    /// 3. Real Windows GSMTC Media Session + Timeline + Album Art Thumbnail + Dominant Color Extraction
    /// </summary>
    public sealed class RealtimeNotchActionService
    {
        private static readonly Lazy<RealtimeNotchActionService> _lazy = new(() => new RealtimeNotchActionService());
        public static RealtimeNotchActionService Instance => _lazy.Value;

        public event Action<NotchNotificationPayload> NotificationRequested;
        public event Action<MediaSessionSnapshot> MediaSessionChanged;

        private readonly DispatcherTimer _pollTimer;
        private readonly HashSet<string> _knownBluetoothDevices = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<FileSystemWatcher> _screenshotWatchers = new();
        private DateTime _lastClipboardTrigger = DateTime.MinValue;
        private string _lastCopiedText = null;

        // Current media state
        public bool IsMusicPlaying { get; private set; }
        public string CurrentTrack { get; private set; } = "";
        public string CurrentArtist { get; private set; } = "";
        public BitmapSource CurrentArtwork { get; private set; }
        public Color CurrentBeatColor { get; private set; } = Color.FromRgb(245, 245, 250);

        private volatile bool _gsmtcActive = false;
        private DateTime _lastGsmtcHeartbeatUtc = DateTime.MinValue;

        private RealtimeNotchActionService()
        {
            _ = AudioSpectrumService.Instance;

            StartGsmtcBackgroundWorker();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
            _pollTimer.Tick += (_, _) =>
            {
                if ((DateTime.UtcNow - _lastGsmtcHeartbeatUtc).TotalSeconds > 4.0)
                {
                    CheckFallbackMediaPlayback();
                }
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
                if (!AppPreferences.Instance.ClipboardCopiedActivity)
                    return;

                if ((DateTime.UtcNow - _lastClipboardTrigger).TotalMilliseconds < 350)
                    return;

                _lastClipboardTrigger = DateTime.UtcNow;

                int durationMs = AppPreferences.Instance.LiveActivityEventDuration switch
                {
                    "2 s" => 2000,
                    "5 s" => 5000,
                    _ => 3000
                };

                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    try
                    {
                        var data = Clipboard.GetDataObject();
                        if (data == null) return;

                        if (Clipboard.ContainsImage() || data.GetDataPresent(DataFormats.Bitmap))
                        {
                            NotificationRequested?.Invoke(new NotchNotificationPayload
                            {
                                Icon = "📋",
                                Title = "Copied",
                                Subtitle = AppPreferences.Instance.HideCopiedText ? " · Image" : " · Image",
                                DurationMs = durationMs
                            });
                            return;
                        }

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
                                    Subtitle = AppPreferences.Instance.HideCopiedText ? " · File" : $" · {desc}",
                                    DurationMs = durationMs
                                });
                                return;
                            }
                        }

                        if (Clipboard.ContainsText())
                        {
                            string text = Clipboard.GetText()?.Trim();
                            if (!string.IsNullOrEmpty(text) && text != _lastCopiedText)
                            {
                                _lastCopiedText = text;
                                string display = AppPreferences.Instance.HideCopiedText
                                    ? " · Text"
                                    : (text.Length > 20
                                        ? $" · \"{text.Substring(0, 18).Replace("\r", " ").Replace("\n", " ")}...\""
                                        : $" · \"{text.Replace("\r", " ").Replace("\n", " ")}\"");

                                NotificationRequested?.Invoke(new NotchNotificationPayload
                                {
                                    Icon = "📋",
                                    Title = "Copied",
                                    Subtitle = display,
                                    DurationMs = durationMs
                                });
                            }
                        }
                    }
                    catch { }
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
                                        DurationMs = 3500
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

                foreach (var device in activeDevices)
                {
                    if (!_knownBluetoothDevices.Contains(device))
                    {
                        _knownBluetoothDevices.Add(device);
                        Application.Current?.Dispatcher?.InvokeAsync(() =>
                        {
                            NotificationRequested?.Invoke(new NotchNotificationPayload
                            {
                                Icon = "🎧",
                                Title = device,
                                Subtitle = "  Connected",
                                DurationMs = 3800
                            });
                        });
                    }
                }

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
                                DurationMs = 2800
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

            return result;
        }

        #endregion

        #region 3. Real Windows GSMTC Media Session + Timeline + Album Cover Art + Dominant Color

        private void StartGsmtcBackgroundWorker()
        {
            var thread = new Thread(() =>
            {
                const string psScript = @"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
$asStreamMethod = ([System.IO.WindowsRuntimeStreamExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsStreamForRead' -and $_.GetParameters().Count -eq 1 })[0]
function Await($WinRtTask, $ResultType) {
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(2000) | Out-Null
    if ($netTask.IsCompleted) { return $netTask.Result }
    return $null
}
$mgrType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media, ContentType=WindowsRuntime]
$propsType = [Windows.Media.Control.GlobalSystemMediaTransportControlsSessionMediaProperties, Windows.Media, ContentType=WindowsRuntime]
$streamType = [Windows.Storage.Streams.IRandomAccessStreamWithContentType, Windows.Storage, ContentType=WindowsRuntime]
$mgr = Await ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) $mgrType
$lastThumbKey = ''
$cachedB64 = ''
while ($true) {
    try {
        if ($null -eq $mgr) {
            $mgr = Await ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) $mgrType
        }
        $session = $mgr.GetCurrentSession()
        if ($null -ne $session) {
            $srcApp = if ($session.SourceAppUserModelId) { $session.SourceAppUserModelId.Replace(""`t"", ' ').Replace(""`r"", '').Replace(""`n"", ' ') } else { '' }
            $info = $session.GetPlaybackInfo()
            $status = $info.PlaybackStatus.ToString()
            $tl = $session.GetTimelineProperties()
            $posSec = [Math]::Max(0.0, $tl.Position.TotalSeconds)
            if ($status -eq 'Playing' -and $tl.LastUpdatedTime.Year -gt 2000) {
                $elapsed = ([DateTimeOffset]::UtcNow - $tl.LastUpdatedTime.ToUniversalTime()).TotalSeconds
                if ($elapsed -gt 0 -and $elapsed -lt 300) { $posSec += $elapsed }
            }
            $durSec = [Math]::Max(0.0, $tl.EndTime.TotalSeconds)
            $posStr = $posSec.ToString('F1', [System.Globalization.CultureInfo]::InvariantCulture)
            $durStr = $durSec.ToString('F1', [System.Globalization.CultureInfo]::InvariantCulture)

            $props = Await ($session.TryGetMediaPropertiesAsync()) $propsType
            $title = if ($props.Title) { $props.Title.Replace(""`t"", ' ').Replace(""`r"", '').Replace(""`n"", ' ') } else { '' }
            $artist = if ($props.Artist) { $props.Artist.Replace(""`t"", ' ').Replace(""`r"", '').Replace(""`n"", ' ') } else { '' }
            $key = ""$title|$artist""
            $b64 = ''
            if ($key -ne $lastThumbKey -and $null -ne $props.Thumbnail) {
                $ras = Await ($props.Thumbnail.OpenReadAsync()) $streamType
                if ($null -ne $ras) {
                    $netStream = $asStreamMethod.Invoke($null, @($ras))
                    $ms = New-Object System.IO.MemoryStream
                    $netStream.CopyTo($ms)
                    $bytes = $ms.ToArray()
                    $ms.Dispose()
                    $netStream.Dispose()
                    if ($bytes.Length -gt 0) {
                        $cachedB64 = [Convert]::ToBase64String($bytes)
                        $lastThumbKey = $key
                    }
                }
                $b64 = $cachedB64
            }
            [Console]::WriteLine(""GSMTC`t$status`t$title`t$artist`t$posStr`t$durStr`t$b64`t$srcApp"")
        } else {
            [Console]::WriteLine(""GSMTC`tStopped`t`t`t0`t0`t`t"")
        }
    } catch {
        [Console]::WriteLine(""GSMTC`tError`t`t`t0`t0`t`t"")
    }
    Start-Sleep -Milliseconds 850
}
";
                string encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(psScript));

                while (true)
                {
                    Process proc = null;
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            StandardOutputEncoding = System.Text.Encoding.UTF8
                        };
                        proc = Process.Start(psi);
                        if (proc == null)
                        {
                            Thread.Sleep(3000);
                            continue;
                        }

                        while (!proc.StandardOutput.EndOfStream)
                        {
                            string line = proc.StandardOutput.ReadLine();
                            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("GSMTC\t", StringComparison.Ordinal))
                                continue;

                            _lastGsmtcHeartbeatUtc = DateTime.UtcNow;
                            _gsmtcActive = true;

                            string[] parts = line.Split('\t');
                            if (parts.Length < 6) continue;

                            string status = parts[1];
                            string title = parts[2].Trim();
                            string artist = parts[3].Trim();
                            double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double posSec);
                            double.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out double durSec);
                            string b64 = parts.Length >= 7 ? parts[6].Trim() : "";
                            string srcApp = parts.Length >= 8 ? parts[7].Trim() : "";

                            bool playing = string.Equals(status, "Playing", StringComparison.OrdinalIgnoreCase)
                                           && !string.IsNullOrWhiteSpace(title);

                            if (playing && !string.IsNullOrWhiteSpace(srcApp) && AppPreferences.Instance.IsAudioAppExcluded(srcApp))
                            {
                                playing = false;
                                title = "";
                                artist = "";
                            }

                            CleanTitleAndArtist(ref title, ref artist);
                            PublishMediaUpdate(title, artist, playing, posSec, durSec, b64, srcApp);
                        }
                    }
                    catch
                    {
                        _gsmtcActive = false;
                    }
                    finally
                    {
                        try { if (proc != null && !proc.HasExited) proc.Kill(); } catch { }
                        proc?.Dispose();
                    }
                    Thread.Sleep(2500);
                }
            })
            {
                IsBackground = true,
                Name = "MayliGsmtcWorker"
            };
            thread.Start();
        }

        private static void CleanTitleAndArtist(ref string title, ref string artist)
        {
            if (string.IsNullOrWhiteSpace(title)) return;

            if ((string.IsNullOrWhiteSpace(artist) || artist.Contains("YouTube", StringComparison.OrdinalIgnoreCase) || artist.Contains("Spotify", StringComparison.OrdinalIgnoreCase))
                && title.Contains(" - ", StringComparison.Ordinal))
            {
                int dash = title.IndexOf(" - ", StringComparison.Ordinal);
                string left = title.Substring(0, dash).Trim();
                string right = title.Substring(dash + 3).Trim();
                if (!string.IsNullOrEmpty(left) && !string.IsNullOrEmpty(right))
                {
                    artist = left;
                    title = right;
                }
            }
        }

        private void PublishMediaUpdate(string title, string artist, bool playing, double posSec, double durSec, string base64Thumb, string sourceApp = "")
        {
            if (playing && !string.IsNullOrWhiteSpace(sourceApp) && AppPreferences.Instance.IsAudioAppExcluded(sourceApp))
            {
                playing = false;
                title = "";
                artist = "";
            }

            BitmapSource decodedArt = null;
            if (!string.IsNullOrEmpty(base64Thumb))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(base64Thumb);
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 84;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    decodedArt = bmp;
                }
                catch { }
            }

            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                string cleanArtist = string.IsNullOrWhiteSpace(artist) ? "Music" : artist;
                bool trackChanged = playing && (!string.Equals(title, CurrentTrack, StringComparison.Ordinal)
                                                || !string.Equals(cleanArtist, CurrentArtist, StringComparison.Ordinal));
                bool stateChanged = playing != IsMusicPlaying || trackChanged || decodedArt != null;

                if (decodedArt != null)
                {
                    CurrentArtwork = decodedArt;
                    CurrentBeatColor = DominantColorExtractor.ExtractBeatColor(CurrentArtwork, title, cleanArtist);
                }
                else if (trackChanged)
                {
                    CurrentBeatColor = DominantColorExtractor.ExtractBeatColor(CurrentArtwork, title, cleanArtist);
                }

                IsMusicPlaying = playing;
                CurrentTrack = playing ? title : "";
                CurrentArtist = playing ? cleanArtist : "";

                MediaService.Instance.State.IsPlaying = playing;
                if (playing)
                {
                    MediaService.Instance.State.Title = CurrentTrack;
                    MediaService.Instance.State.Artist = CurrentArtist;
                    if (durSec > 1.0)
                    {
                        MediaService.Instance.State.PositionSeconds = Math.Clamp(posSec, 0.0, durSec);
                        MediaService.Instance.State.DurationSeconds = durSec;
                    }
                }

                if (stateChanged)
                {
                    MediaSessionChanged?.Invoke(new MediaSessionSnapshot
                    {
                        Title = CurrentTrack,
                        Artist = CurrentArtist,
                        IsPlaying = IsMusicPlaying,
                        IsTrackSwitch = trackChanged,
                        PositionSeconds = MediaService.Instance.State.PositionSeconds,
                        DurationSeconds = MediaService.Instance.State.DurationSeconds,
                        Artwork = CurrentArtwork,
                        BeatColor = CurrentBeatColor,
                        SourceApp = sourceApp
                    });
                }
            });
        }

        private void CheckFallbackMediaPlayback()
        {
            if (_gsmtcActive && (DateTime.UtcNow - _lastGsmtcHeartbeatUtc).TotalSeconds < 4.0)
                return;

            try
            {
                bool detected = false;
                string track = null;
                string artist = null;
                string sourceApp = "";

                if (!AppPreferences.Instance.IsAudioAppExcluded("Spotify") && !AppPreferences.Instance.IsAudioAppExcluded("Spotify.exe"))
                {
                    var spotifyProcs = Process.GetProcessesByName("Spotify");
                    foreach (var proc in spotifyProcs)
                    {
                        string title = proc.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title) &&
                            !string.Equals(title, "Spotify", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(title, "Spotify Free", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(title, "Spotify Premium", StringComparison.OrdinalIgnoreCase))
                        {
                            detected = true;
                            sourceApp = "Spotify.exe";
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

                if (!detected)
                {
                    foreach (var procName in new[] { "chrome", "msedge", "firefox", "vlc", "Music.UI", "Telegram" })
                    {
                        if (AppPreferences.Instance.IsAudioAppExcluded(procName) || AppPreferences.Instance.IsAudioAppExcluded(procName + ".exe"))
                            continue;

                        var procs = Process.GetProcessesByName(procName);
                        foreach (var proc in procs)
                        {
                            string wTitle = proc.MainWindowTitle;
                            if (!string.IsNullOrWhiteSpace(wTitle) &&
                                (wTitle.Contains("YouTube", StringComparison.OrdinalIgnoreCase) ||
                                 wTitle.Contains("SoundCloud", StringComparison.OrdinalIgnoreCase) ||
                                 wTitle.Contains("Spotify", StringComparison.OrdinalIgnoreCase)))
                            {
                                detected = true;
                                sourceApp = procName + ".exe";
                                string clean = wTitle.Replace(" - YouTube", "")
                                                     .Replace(" - Google Chrome", "")
                                                     .Replace(" - Microsoft\u200B Edge", "")
                                                     .Replace(" - Microsoft Edge", "")
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
                                    artist = "Music";
                                }
                                break;
                            }
                        }
                        if (detected) break;
                    }
                }

                PublishMediaUpdate(track ?? "", artist ?? "", detected, MediaService.Instance.State.PositionSeconds, MediaService.Instance.State.DurationSeconds, "", sourceApp);
            }
            catch { }
        }

        #endregion
    }
}
