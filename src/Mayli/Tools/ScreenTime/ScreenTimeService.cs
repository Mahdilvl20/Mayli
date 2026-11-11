using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.ScreenTime
{
    public sealed class ScreenTimeService
    {
        private static readonly Lazy<ScreenTimeService> _lazy = new(() => new ScreenTimeService());
        public static ScreenTimeService Instance => _lazy.Value;

        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        private readonly string _storagePath;
        private readonly object _lock = new();

        private ScreenTimeHistoryModel _history = new();
        private DayScreenTimeRecord _todayRecord;
        private readonly DispatcherTimer _pollTimer;
        private DateTime _lastPollUtc = DateTime.UtcNow;
        private IntPtr _lastHwnd = IntPtr.Zero;
        private string _lastAppName = null;
        private string _lastExePath = null;

        public event Action UsageUpdated;

        public bool IsPaused
        {
            get => _history.IsTrackingPaused;
            set
            {
                _history.IsTrackingPaused = value;
                Save();
                UsageUpdated?.Invoke();
            }
        }

        public int DailyLimitSeconds
        {
            get => _history.DailyLimitSeconds > 0 ? _history.DailyLimitSeconds : 7 * 3600;
            set
            {
                _history.DailyLimitSeconds = Math.Clamp(value, 3600, 24 * 3600);
                Save();
                UsageUpdated?.Invoke();
            }
        }

        public int TodayTotalSeconds => _todayRecord?.TotalSeconds ?? 0;
        public int AppsUsedToday => _todayRecord?.AppSeconds.Count ?? 0;

        public int LimitRemainingSeconds => Math.Max(0, DailyLimitSeconds - TodayTotalSeconds);
        public double LimitProgressFraction => DailyLimitSeconds > 0
            ? Math.Clamp((double)TodayTotalSeconds / DailyLimitSeconds, 0.0, 1.0)
            : 0.0;

        public string TodayTotalFormatted => FormatDuration(TodayTotalSeconds);
        public string LimitRemainingFormatted => FormatDuration(LimitRemainingSeconds) + " left";
        public string LimitFormatted => $"{DailyLimitSeconds / 3600}h limit";

        private ScreenTimeService()
        {
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string dir = Path.Combine(appData, "Mayli", "screentime");
                Directory.CreateDirectory(dir);
                _storagePath = Path.Combine(dir, "history.json");
            }
            catch
            {
                _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screentime_history.json");
            }

            Load();
            CleanLegacyMockData();
            EnsureTodayRecord();

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _pollTimer.Tick += (_, _) => PollForegroundApp();
            _pollTimer.Start();
        }

        private void PollForegroundApp()
        {
            if (IsPaused) return;

            DateTime nowUtc = DateTime.UtcNow;
            int elapsedSec = (int)Math.Max(1, Math.Min(5, (nowUtc - _lastPollUtc).TotalSeconds));
            _lastPollUtc = nowUtc;

            // Inactivity check: if user has been idle for > 120s, skip increment
            if (GetIdleSeconds() > 120.0)
            {
                return;
            }

            EnsureTodayRecord();

            string appName = null;
            string exePath = null;

            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    if (hwnd == _lastHwnd && !string.IsNullOrWhiteSpace(_lastAppName))
                    {
                        appName = _lastAppName;
                        exePath = _lastExePath;
                    }
                    else
                    {
                        _lastHwnd = hwnd;
                        GetWindowThreadProcessId(hwnd, out uint pid);
                        if (pid != 0)
                        {
                            using var proc = Process.GetProcessById((int)pid);
                            string procName = proc.ProcessName;
                            exePath = GetProcessExePath(pid);
                            appName = ResolveFriendlyName(procName, exePath);
                            _lastAppName = appName;
                            _lastExePath = exePath;
                        }
                    }
                }
            }
            catch { }

            if (string.IsNullOrWhiteSpace(appName)) return;

            lock (_lock)
            {
                _todayRecord.TotalSeconds += elapsedSec;
                if (!_todayRecord.AppSeconds.TryGetValue(appName, out int curAppSec))
                {
                    _todayRecord.AppSeconds[appName] = elapsedSec;
                }
                else
                {
                    _todayRecord.AppSeconds[appName] = curAppSec + elapsedSec;
                }

                int hour = DateTime.Now.Hour;
                if (hour >= 0 && hour < 24)
                {
                    _todayRecord.HourlySeconds[hour] += elapsedSec;
                }

                if (!string.IsNullOrEmpty(exePath) && !_history.ExePaths.ContainsKey(appName))
                {
                    _history.ExePaths[appName] = exePath;
                }
            }

            SaveSoon();
            UsageUpdated?.Invoke();
        }

        private void EnsureTodayRecord()
        {
            string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
            if (_todayRecord == null || _todayRecord.DateString != todayStr)
            {
                lock (_lock)
                {
                    _todayRecord = _history.Days.FirstOrDefault(d => d.DateString == todayStr);
                    if (_todayRecord == null)
                    {
                        _todayRecord = new DayScreenTimeRecord
                        {
                            DateString = todayStr,
                            TotalSeconds = 0
                        };

                        _history.Days.Add(_todayRecord);
                        Save();
                    }
                }
            }
        }

        private void CleanLegacyMockData()
        {
            lock (_lock)
            {
                bool modified = false;
                string todayStr = DateTime.Today.ToString("yyyy-MM-dd");

                for (int i = _history.Days.Count - 1; i >= 0; i--)
                {
                    var day = _history.Days[i];
                    if (day.AppSeconds != null &&
                        day.AppSeconds.TryGetValue("Antigravity IDE", out int ant) && ant >= 1800 &&
                        day.AppSeconds.TryGetValue("Telegram", out int tg) && tg >= 2340 &&
                        day.AppSeconds.TryGetValue("Google Chrome", out int cr) && cr >= 1200)
                    {
                        day.AppSeconds["Antigravity IDE"] -= 1800;
                        if (day.AppSeconds["Antigravity IDE"] <= 0) day.AppSeconds.Remove("Antigravity IDE");

                        day.AppSeconds["Telegram"] -= 2340;
                        if (day.AppSeconds["Telegram"] <= 0) day.AppSeconds.Remove("Telegram");

                        day.AppSeconds["Google Chrome"] -= 1200;
                        if (day.AppSeconds["Google Chrome"] <= 0) day.AppSeconds.Remove("Google Chrome");

                        if (day.AppSeconds.TryGetValue("Mayli", out int my))
                        {
                            day.AppSeconds["Mayli"] = Math.Max(0, my - 420);
                            if (day.AppSeconds["Mayli"] == 0) day.AppSeconds.Remove("Mayli");
                        }

                        if (day.AppSeconds.TryGetValue("File Explorer", out int fe))
                        {
                            day.AppSeconds["File Explorer"] = Math.Max(0, fe - 360);
                            if (day.AppSeconds["File Explorer"] == 0) day.AppSeconds.Remove("File Explorer");
                        }

                        if (day.AppSeconds.TryGetValue("Visual Studio Code", out int vs))
                        {
                            day.AppSeconds["Visual Studio Code"] = Math.Max(0, vs - 300);
                            if (day.AppSeconds["Visual Studio Code"] == 0) day.AppSeconds.Remove("Visual Studio Code");
                        }

                        if (day.AppSeconds.TryGetValue("Windows Terminal", out int wt))
                        {
                            day.AppSeconds["Windows Terminal"] = Math.Max(0, wt - 240);
                            if (day.AppSeconds["Windows Terminal"] == 0) day.AppSeconds.Remove("Windows Terminal");
                        }

                        if (day.AppSeconds.TryGetValue("Task Manager", out int tm))
                        {
                            day.AppSeconds["Task Manager"] = Math.Max(0, tm - 180);
                            if (day.AppSeconds["Task Manager"] == 0) day.AppSeconds.Remove("Task Manager");
                        }

                        if (day.AppSeconds.TryGetValue("Settings", out int st))
                        {
                            day.AppSeconds["Settings"] = Math.Max(0, st - 180);
                            if (day.AppSeconds["Settings"] == 0) day.AppSeconds.Remove("Settings");
                        }

                        day.TotalSeconds = day.AppSeconds.Values.Sum();

                        if (day.TotalSeconds <= 0 && day.DateString != todayStr)
                        {
                            _history.Days.RemoveAt(i);
                        }

                        modified = true;
                    }
                }

                if (modified)
                {
                    _todayRecord = null;
                    Save();
                }
            }
        }

        public IReadOnlyList<AppUsageItem> GetTopAppsToday()
        {
            EnsureTodayRecord();
            var list = new List<AppUsageItem>();
            int total = Math.Max(1, _todayRecord.TotalSeconds);

            lock (_lock)
            {
                foreach (var kvp in _todayRecord.AppSeconds.OrderByDescending(p => p.Value))
                {
                    _history.ExePaths.TryGetValue(kvp.Key, out string exePath);
                    double pct = (kvp.Value / (double)total) * 100.0;
                    list.Add(new AppUsageItem
                    {
                        Name = kvp.Key,
                        Seconds = kvp.Value,
                        Percentage = pct,
                        ExePath = exePath,
                        IconSource = AppIconExtractor.GetIcon(kvp.Key, exePath)
                    });
                }
            }

            return list;
        }

        public IReadOnlyList<DayBarData> GetRolling7Days()
        {
            EnsureTodayRecord();
            var days = new List<DayBarData>();
            DateTime today = DateTime.Today;

            // Find starting Sunday of current week
            int diff = (7 + (int)today.DayOfWeek) % 7;
            DateTime sunday = today.AddDays(-diff);

            int maxSeconds = 3600; // minimum scale 1h

            lock (_lock)
            {
                for (int i = 0; i < 7; i++)
                {
                    DateTime dt = sunday.AddDays(i);
                    string dStr = dt.ToString("yyyy-MM-dd");
                    var rec = _history.Days.FirstOrDefault(d => d.DateString == dStr);
                    int sec = rec?.TotalSeconds ?? 0;
                    if (sec > maxSeconds) maxSeconds = sec;

                    days.Add(new DayBarData
                    {
                        Date = dt,
                        DayName = dt.ToString("ddd"), // "Sun", "Mon", etc.
                        TotalSeconds = sec,
                        IsToday = (dt == today)
                    });
                }
            }

            foreach (var d in days)
            {
                d.HeightFraction = maxSeconds > 0 ? (double)d.TotalSeconds / maxSeconds : 0.0;
            }

            return days;
        }

        public int GetWeeklyAverageSeconds()
        {
            var rolling = GetRolling7Days();
            var activeDays = rolling.Where(d => d.TotalSeconds > 0).ToList();
            if (activeDays.Count == 0) return 0;
            return (int)activeDays.Average(d => d.TotalSeconds);
        }

        public IReadOnlyList<DayBarData> GetRolling30Days()
        {
            EnsureTodayRecord();
            var days = new List<DayBarData>();
            DateTime today = DateTime.Today;
            DateTime startDate = today.AddDays(-29);

            int maxSeconds = 3600;

            lock (_lock)
            {
                for (int i = 0; i < 30; i++)
                {
                    DateTime dt = startDate.AddDays(i);
                    string dStr = dt.ToString("yyyy-MM-dd");
                    var rec = _history.Days.FirstOrDefault(d => d.DateString == dStr);
                    int sec = rec?.TotalSeconds ?? 0;
                    if (sec > maxSeconds) maxSeconds = sec;

                    days.Add(new DayBarData
                    {
                        Date = dt,
                        DayName = dt.ToString("ddd"),
                        TotalSeconds = sec,
                        IsToday = (dt == today)
                    });
                }
            }

            foreach (var d in days)
            {
                d.HeightFraction = maxSeconds > 0 ? (double)d.TotalSeconds / maxSeconds : 0.0;
            }

            return days;
        }

        public int GetMonthlyAverageSeconds()
        {
            var rolling = GetRolling30Days();
            var activeDays = rolling.Where(d => d.TotalSeconds > 0).ToList();
            if (activeDays.Count == 0) return 0;
            return (int)activeDays.Average(d => d.TotalSeconds);
        }

        public void Refresh()
        {
            UsageUpdated?.Invoke();
        }

        public static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0) return "0m";
            if (totalSeconds < 60) return $"{totalSeconds}s";
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            if (hours > 0)
            {
                return minutes > 0 ? $"{hours}h {minutes}m" : $"{hours}h";
            }
            return $"{minutes}m";
        }

        private static string ResolveFriendlyName(string processName, string exePath)
        {
            if (string.IsNullOrWhiteSpace(processName)) return "Unknown";

            // Known app mappings
            string lower = processName.ToLowerInvariant();
            if (lower == "telegram") return "Telegram";
            if (lower == "antigravity" || lower == "antigravity ide") return "Antigravity IDE";
            if (lower == "chrome") return "Google Chrome";
            if (lower == "msedge") return "Microsoft Edge";
            if (lower == "firefox") return "Firefox";
            if (lower == "code") return "Visual Studio Code";
            if (lower == "devenv") return "Visual Studio";
            if (lower == "windowsterminal") return "Windows Terminal";
            if (lower == "cmd" || lower == "powershell" || lower == "pwsh") return "Terminal";
            if (lower == "explorer") return "File Explorer";
            if (lower == "spotify") return "Spotify";
            if (lower == "figma") return "Figma";
            if (lower == "discord") return "Discord";
            if (lower == "slack") return "Slack";
            if (lower == "notion") return "Notion";
            if (lower == "mayli" || lower == "vayli") return "Mayli";

            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                try
                {
                    var vi = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(vi.FileDescription))
                    {
                        return vi.FileDescription.Trim();
                    }
                }
                catch { }
            }

            return char.ToUpper(processName[0]) + processName.Substring(1);
        }

        private static string GetProcessExePath(uint pid)
        {
            IntPtr hProcess = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
            if (hProcess == IntPtr.Zero) return null;

            try
            {
                var sb = new StringBuilder(1024);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(hProcess, 0, sb, ref size))
                {
                    return sb.ToString();
                }
            }
            finally
            {
                CloseHandle(hProcess);
            }
            return null;
        }

        private static double GetIdleSeconds()
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lii))
            {
                uint currentTick = (uint)Environment.TickCount;
                return (currentTick - lii.dwTime) / 1000.0;
            }
            return 0.0;
        }

        private DateTime _lastSaveUtc = DateTime.MinValue;
        private void SaveSoon()
        {
            if ((DateTime.UtcNow - _lastSaveUtc).TotalSeconds > 10.0)
            {
                _lastSaveUtc = DateTime.UtcNow;
                Save();
            }
        }

        private void Save()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_storagePath, json);
                }
                catch { }
            }
        }

        private void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_storagePath))
                    {
                        string json = File.ReadAllText(_storagePath);
                        var loaded = JsonSerializer.Deserialize<ScreenTimeHistoryModel>(json);
                        if (loaded != null)
                        {
                            _history = loaded;
                        }
                    }
                }
                catch { }
            }
        }

        #region Win32 P/Invokes
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
        #endregion
    }
}
