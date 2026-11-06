using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        public int DurationMs { get; set; } = 5000;
        public bool IsMediaStatus { get; set; } = false;
        public BitmapSource AppIcon { get; set; }
        public string AppName { get; set; }
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
        private readonly object _btLock = new();
        private int _isBackgroundPolling = 0;
        private readonly List<FileSystemWatcher> _screenshotWatchers = new();
        private DateTime _lastClipboardTrigger = DateTime.MinValue;
        private string _lastCopiedText = null;

        // Current media & bluetooth state
        public bool IsMusicPlaying { get; private set; }
        public bool HasConnectedBluetoothDevice { get; private set; }
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

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _pollTimer.Tick += (_, _) =>
            {
                if (System.Threading.Interlocked.CompareExchange(ref _isBackgroundPolling, 1, 0) != 0)
                    return;

                Task.Run(() =>
                {
                    try
                    {
                        if ((DateTime.UtcNow - _lastGsmtcHeartbeatUtc).TotalSeconds > 4.0)
                        {
                            CheckFallbackMediaPlayback();
                        }
                        CheckBluetoothDevices();
                    }
                    catch { }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _isBackgroundPolling, 0);
                    }
                });
            };
            _pollTimer.Start();

            InitScreenshotWatchers();
            InitKeyboardHook();
            Task.Run(InitKnownBluetoothDevices);
        }

        public void TriggerNotification(NotchNotificationPayload payload)
        {
            if (payload == null) return;
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                NotificationRequested?.Invoke(payload);
            });
        }

        #region 1. Clipboard & Screenshot Detection (WM_CLIPBOARDUPDATE)

        private static DateTime _lastCutTimestamp = DateTime.MinValue;
        private static DateTime _lastCopyTimestamp = DateTime.MinValue;
        private static DateTime _lastCopiedTextTime = DateTime.MinValue;
        private static volatile bool _isCtrlDown = false;
        private static volatile bool _isShiftDown = false;
        private static NativeMethods.LowLevelKeyboardProc _llKeyboardProc;
        private static IntPtr _llKeyboardHook = IntPtr.Zero;

        private static void LogNotif(string msg)
        {
            try
            {
                string dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mayli", "logs");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "notifications.log"), $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [ActionService] {msg}\r\n");
            }
            catch { }
        }

        private void InitKeyboardHook()
        {
            try
            {
                _llKeyboardProc = LowLevelKeyboardHandler;
                IntPtr modHandle = NativeMethods.GetModuleHandle(null);
                _llKeyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _llKeyboardProc, modHandle, 0);
                int err = Marshal.GetLastWin32Error();
                LogNotif($"InitKeyboardHook: hook={_llKeyboardHook}, err={err}");
            }
            catch (Exception ex)
            {
                LogNotif($"InitKeyboardHook error: {ex}");
            }
        }

        private static IntPtr LowLevelKeyboardHandler(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = (int)wParam;
                int vkCode = Marshal.ReadInt32(lParam);

                if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
                {
                    if (vkCode == NativeMethods.VK_CONTROL || vkCode == NativeMethods.VK_LCONTROL || vkCode == NativeMethods.VK_RCONTROL)
                    {
                        _isCtrlDown = true;
                    }
                    else if (vkCode == NativeMethods.VK_SHIFT || vkCode == 0xA0 || vkCode == 0xA1)
                    {
                        _isShiftDown = true;
                    }

                    bool ctrl = _isCtrlDown ||
                                (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0 ||
                                (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) != 0 ||
                                (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) != 0;
                    bool shift = _isShiftDown || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;

                    // Sync state if physically released
                    if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) == 0 &&
                        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) == 0 &&
                        (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) == 0)
                    {
                        _isCtrlDown = false;
                        ctrl = false;
                    }

                    if ((NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) == 0)
                    {
                        _isShiftDown = false;
                        shift = false;
                    }

                    if (vkCode == NativeMethods.VK_X && ctrl)
                    {
                        _lastCutTimestamp = DateTime.UtcNow;
                        LogNotif("LowLevelKeyboard: Ctrl+X detected");
                    }
                    else if (vkCode == NativeMethods.VK_DELETE && shift)
                    {
                        _lastCutTimestamp = DateTime.UtcNow;
                        LogNotif("LowLevelKeyboard: Shift+Delete detected");
                    }
                    else if (vkCode == NativeMethods.VK_C && ctrl)
                    {
                        _lastCopyTimestamp = DateTime.UtcNow;
                        LogNotif("LowLevelKeyboard: Ctrl+C detected");
                    }
                    else if (vkCode == NativeMethods.VK_INSERT && ctrl)
                    {
                        _lastCopyTimestamp = DateTime.UtcNow;
                        LogNotif("LowLevelKeyboard: Ctrl+Insert detected");
                    }
                }
                else if (msg == NativeMethods.WM_KEYUP || msg == NativeMethods.WM_SYSKEYUP)
                {
                    if (vkCode == NativeMethods.VK_CONTROL || vkCode == NativeMethods.VK_LCONTROL || vkCode == NativeMethods.VK_RCONTROL)
                    {
                        _isCtrlDown = false;
                    }
                    else if (vkCode == NativeMethods.VK_SHIFT || vkCode == 0xA0 || vkCode == 0xA1)
                    {
                        _isShiftDown = false;
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_llKeyboardHook, nCode, wParam, lParam);
        }

        private bool CheckIfCutOperation(IDataObject data)
        {
            try
            {
                // 1. Check if Ctrl+X or Shift+Delete was recorded recently (within 2000ms)
                if ((DateTime.UtcNow - _lastCutTimestamp).TotalMilliseconds < 2000)
                {
                    _lastCutTimestamp = DateTime.MinValue;
                    return true;
                }

                // 2. Check Preferred DropEffect on clipboard (for files/folders in File Explorer)
                if (data != null && data.GetDataPresent("Preferred DropEffect"))
                {
                    var stream = data.GetData("Preferred DropEffect") as Stream;
                    if (stream != null && stream.Length >= 4)
                    {
                        byte[] bytes = new byte[4];
                        stream.Position = 0;
                        int bytesRead = stream.Read(bytes, 0, 4);
                        if (bytesRead == 4)
                        {
                            int dropEffect = BitConverter.ToInt32(bytes, 0);
                            // DROPEFFECT_MOVE = 2 (Cut / Move)
                            if ((dropEffect & 2) != 0)
                            {
                                return true;
                            }
                        }
                    }
                }

                // 3. Fallback: check live GetAsyncKeyState
                bool isCtrl = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0 ||
                              (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LCONTROL) & 0x8000) != 0 ||
                              (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RCONTROL) & 0x8000) != 0;
                bool isX = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_X) & 0x8000) != 0;
                bool isShift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) != 0;
                bool isDel = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_DELETE) & 0x8000) != 0;

                if ((isCtrl && isX) || (isShift && isDel))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        private DispatcherTimer _clipboardDebounceTimer;

        public void HandleClipboardUpdate()
        {
            try
            {
                if (!AppPreferences.Instance.ClipboardCopiedActivity)
                    return;

                Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    _clipboardDebounceTimer?.Stop();
                    _clipboardDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
                    _clipboardDebounceTimer.Tick += (s, e) =>
                    {
                        _clipboardDebounceTimer?.Stop();
                        _clipboardDebounceTimer = null;
                        ProcessClipboardContent();
                    };
                    _clipboardDebounceTimer.Start();
                });
            }
            catch (Exception ex)
            {
                LogNotif($"HandleClipboardUpdate error: {ex}");
            }
        }

        private void ProcessClipboardContent()
        {
            try
            {
                if (!AppPreferences.Instance.ClipboardCopiedActivity)
                    return;

                var data = Clipboard.GetDataObject();
                if (data == null) return;

                bool isCut = CheckIfCutOperation(data);
                string actionTitle = isCut ? "Cut" : "Copied";
                string actionIcon = isCut ? "✂️" : "📋";

                int durationMs = AppPreferences.Instance.LiveActivityEventDuration switch
                {
                    "2 s" => 2000,
                    "5 s" => 5000,
                    _ => 3000
                };

                LogNotif($"ProcessClipboardContent: isCut={isCut}, actionTitle={actionTitle}");

                if (Clipboard.ContainsImage() || data.GetDataPresent(DataFormats.Bitmap))
                {
                    NotificationRequested?.Invoke(new NotchNotificationPayload
                    {
                        Icon = actionIcon,
                        Title = actionTitle,
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
                            Icon = isCut ? "✂️" : "📁",
                            Title = actionTitle,
                            Subtitle = AppPreferences.Instance.HideCopiedText ? " · File" : $" · {desc}",
                            DurationMs = durationMs
                        });
                        return;
                    }
                }

                if (Clipboard.ContainsText())
                {
                    string text = Clipboard.GetText()?.Trim();
                    bool isSameTextAsLast = !string.IsNullOrEmpty(text) && text == _lastCopiedText;
                    bool isRapidDuplicate = isSameTextAsLast && (DateTime.UtcNow - _lastCopiedTextTime).TotalMilliseconds < 1500;

                    if (!string.IsNullOrEmpty(text) && (!isRapidDuplicate || isCut))
                    {
                        _lastCopiedText = text;
                        _lastCopiedTextTime = DateTime.UtcNow;
                        string display = AppPreferences.Instance.HideCopiedText
                            ? " · Text"
                            : (text.Length > 20
                                ? $" · \"{text.Substring(0, 18).Replace("\r", " ").Replace("\n", " ")}...\""
                                : $" · \"{text.Replace("\r", " ").Replace("\n", " ")}\"");

                        NotificationRequested?.Invoke(new NotchNotificationPayload
                        {
                            Icon = actionIcon,
                            Title = actionTitle,
                            Subtitle = display,
                            DurationMs = durationMs
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                LogNotif($"ProcessClipboardContent error: {ex}");
            }
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
                Task.Run(() => CheckBluetoothDevices(isDirectEvent: true));
            }
        }

        private void InitKnownBluetoothDevices()
        {
            try
            {
                var current = QueryActiveBluetoothDevices();
                lock (_btLock)
                {
                    foreach (var d in current)
                    {
                        _knownBluetoothDevices.Add(d);
                    }
                    HasConnectedBluetoothDevice = _knownBluetoothDevices.Count > 0;
                }
            }
            catch { }
        }

        private void CheckBluetoothDevices(bool isDirectEvent = false)
        {
            try
            {
                var activeDevices = QueryActiveBluetoothDevices();

                lock (_btLock)
                {
                    foreach (var device in activeDevices)
                    {
                        if (!_knownBluetoothDevices.Contains(device))
                        {
                            _knownBluetoothDevices.Add(device);
                            var capturedDevice = device;
                            Application.Current?.Dispatcher?.InvokeAsync(() =>
                            {
                                NotificationRequested?.Invoke(new NotchNotificationPayload
                                {
                                    Icon = "🎧",
                                    Title = capturedDevice,
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
                            var capturedDisc = d;
                            Application.Current?.Dispatcher?.InvokeAsync(() =>
                            {
                                NotificationRequested?.Invoke(new NotchNotificationPayload
                                {
                                    Icon = "🎧",
                                    Title = capturedDisc,
                                    Subtitle = "  Disconnected",
                                    DurationMs = 2800
                                });
                            });
                        }
                    }

                    HasConnectedBluetoothDevice = _knownBluetoothDevices.Count > 0;
                }
            }
            catch { }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public int dwSize;
            public int fReturnAuthenticated;
            public int fReturnRemembered;
            public int fReturnUnknown;
            public int fReturnConnected;
            public int fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO
        {
            public int dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            public int fConnected;
            public int fRemembered;
            public int fAuthenticated;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] stLastSeen;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] stLastUsed;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }

        [System.Runtime.InteropServices.DllImport("bthprops.cpl", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [System.Runtime.InteropServices.DllImport("bthprops.cpl", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [System.Runtime.InteropServices.DllImport("bthprops.cpl", SetLastError = true)]
        private static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        private static HashSet<string> QueryActiveBluetoothDevices()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var search = new BLUETOOTH_DEVICE_SEARCH_PARAMS
                {
                    dwSize = System.Runtime.InteropServices.Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                    fReturnAuthenticated = 1,
                    fReturnRemembered = 1,
                    fReturnUnknown = 0,
                    fReturnConnected = 1,
                    fIssueInquiry = 0,
                    cTimeoutMultiplier = 1,
                    hRadio = IntPtr.Zero
                };
                var info = new BLUETOOTH_DEVICE_INFO
                {
                    dwSize = System.Runtime.InteropServices.Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
                };

                IntPtr hFind = BluetoothFindFirstDevice(ref search, ref info);
                if (hFind != IntPtr.Zero)
                {
                    try
                    {
                        do
                        {
                            if (info.fConnected != 0 && !string.IsNullOrWhiteSpace(info.szName))
                            {
                                result.Add(info.szName.Trim());
                            }
                        }
                        while (BluetoothFindNextDevice(hFind, ref info));
                    }
                    finally
                    {
                        BluetoothFindDeviceClose(hFind);
                    }
                    return result;
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

            if (!playing && !IsMusicPlaying && string.IsNullOrEmpty(base64Thumb))
            {
                return;
            }

            string cleanArtist = string.IsNullOrWhiteSpace(artist) ? "Music" : artist;
            BitmapSource decodedArt = null;
            Color? preExtractedColor = null;

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
                    preExtractedColor = DominantColorExtractor.ExtractBeatColor(decodedArt, title, cleanArtist);
                }
                catch { }
            }

            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                bool trackChanged = playing && (!string.Equals(title, CurrentTrack, StringComparison.Ordinal)
                                                || !string.Equals(cleanArtist, CurrentArtist, StringComparison.Ordinal));
                bool stateChanged = playing != IsMusicPlaying || trackChanged || decodedArt != null;

                if (decodedArt != null)
                {
                    CurrentArtwork = decodedArt;
                    CurrentBeatColor = preExtractedColor ?? DominantColorExtractor.ExtractBeatColor(CurrentArtwork, title, cleanArtist);
                }
                else if (trackChanged)
                {
                    CurrentBeatColor = DominantColorExtractor.ExtractBeatColor(CurrentArtwork, title, cleanArtist);
                }

                IsMusicPlaying = playing;
                if (!string.IsNullOrWhiteSpace(title))
                {
                    CurrentTrack = title;
                    CurrentArtist = cleanArtist;
                }

                MediaService.Instance.State.IsPlaying = playing;
                if (!string.IsNullOrWhiteSpace(CurrentTrack))
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
                    try
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
                    finally
                    {
                        foreach (var proc in spotifyProcs)
                        {
                            try { proc.Dispose(); } catch { }
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
                        try
                        {
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
                        }
                        finally
                        {
                            foreach (var proc in procs)
                            {
                                try { proc.Dispose(); } catch { }
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
