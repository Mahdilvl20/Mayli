using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsBuddy.BuddyCapsule.Theme;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tools.Timer
{
    public enum TimerMode
    {
        Pomodoro,
        Countdown,
        Alarm,
        Stopwatch,
        Water
    }

    public sealed class StopwatchLap
    {
        public int LapNumber { get; set; }
        public TimeSpan SplitTime { get; set; }
        public TimeSpan TotalTime { get; set; }
    }

    public sealed class AlarmEntry
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public int Hour { get; set; } = 8;
        public int Minute { get; set; } = 0;
        public string Title { get; set; } = "";
        public bool IsEnabled { get; set; } = true;
        public bool RepeatDaily { get; set; } = false;
        public string SoundTone { get; set; } = "Classic";
        public int SnoozeMinutes { get; set; } = 5;
        public DateTime? SnoozedUntilLocal { get; set; }
        public string LastTriggeredDateKey { get; set; } = "";

        public DateTime GetNextTargetTime(DateTime now)
        {
            if (SnoozedUntilLocal.HasValue && SnoozedUntilLocal.Value > now)
            {
                return SnoozedUntilLocal.Value;
            }

            var candidate = new DateTime(now.Year, now.Month, now.Day, Math.Clamp(Hour, 0, 23), Math.Clamp(Minute, 0, 59), 0);
            if (candidate <= now)
            {
                candidate = candidate.AddDays(1);
            }
            return candidate;
        }
    }

    public sealed class TimerPrefsModel
    {
        public int PomoWorkSeconds { get; set; } = 25 * 60;
        public int PomoBreakSeconds { get; set; } = 5 * 60;
        public int PomoLongBreakSeconds { get; set; } = 15 * 60;
        public int PomoTargetCycles { get; set; } = 4;
        public bool PomoAutoStartNext { get; set; } = true;
        public string PomoTitle { get; set; } = "";
        public int PomoSessionsToday { get; set; } = 0;
        public int PomoFocusMinutesToday { get; set; } = 0;
        public string LastStatsDate { get; set; } = "";

        public int CountTotalSeconds { get; set; } = 15 * 60;
        public string CountTitle { get; set; } = "";
        public bool CountLoop { get; set; } = false;
        public List<int> CustomCountdownPresetsSeconds { get; set; } = new() { 60, 180, 300, 600, 900, 1800, 2700, 3600 };

        public string SelectedSoundTone { get; set; } = "Classic"; // Classic, Radar, Digital, Gentle, Zen, Silent
        public int RingRepeatCount { get; set; } = 2; // 1, 2, 3, 5
        public int DefaultSnoozeMinutes { get; set; } = 5; // 0 (Off), 3, 5, 10, 15

        public List<AlarmEntry> Alarms { get; set; } = new();
        public bool ShowAlarmInNotch { get; set; } = true;

        public string StopwatchTitle { get; set; } = "";

        public int WaterGlasses { get; set; } = 0;
        public int WaterTarget { get; set; } = 8;
        public bool WaterReminderEnabled { get; set; } = false;
        public int WaterReminderIntervalMinutes { get; set; } = 60;
        public bool ShowWaterInNotch { get; set; } = false;

        public bool ShowPausedOnNotch { get; set; } = false;
    }

    public struct NotchTimerSnapshot
    {
        public bool IsActive;
        public bool IsRunning;
        public TimerMode Mode;
        public string DisplayTime;
        public string Title;
        /// <summary>
        /// Progress ratio from 0.0 to 1.0:
        /// - For Countdown / Pomodoro / Alarm / Water: decreases from 1.0 down to 0.0.
        /// - For Stopwatch: increases from 0.0 up to 1.0 every 60 seconds.
        /// - When stopped or finished: resets to 0.0.
        /// </summary>
        public double RemainingRatio;
        public Color AccentColor;
    }

    public sealed class TimerService
    {
        private static readonly Lazy<TimerService> _lazy = new(() => new TimerService());
        public static TimerService Instance => _lazy.Value;

        private const string PrefsFile = "timer_prefs.json";
        public static readonly string[] AvailableSoundTones = { "Classic", "Radar", "Digital", "Gentle", "Zen", "Silent" };

        private readonly DispatcherTimer _clock;
        private readonly Stopwatch _stopwatch = new();
        private TimeSpan _lastLapTotal = TimeSpan.Zero;
        private long _lastTickTimestamp;

        private CancellationTokenSource _soundCts;

        // Track last interacted/started mode so Notch prioritizes what the user most recently used
        public TimerMode LastActiveMode { get; set; } = TimerMode.Alarm;

        // Pomodoro State
        public int PomoWorkSeconds { get; set; } = 25 * 60;
        public int PomoBreakSeconds { get; set; } = 5 * 60;
        public int PomoLongBreakSeconds { get; set; } = 15 * 60;
        public int PomoRemainingSeconds { get; set; } = 25 * 60;
        public bool PomoIsWork { get; set; } = true;
        public int PomoCurrentCycle { get; set; } = 1;
        public int PomoTargetCycles { get; set; } = 4;
        public bool PomoIsRunning { get; set; } = false;
        public bool PomoAutoStartNext { get; set; } = true;
        public int PomoSessionsToday { get; set; } = 0;
        public int PomoFocusMinutesToday { get; set; } = 0;
        public string PomoTitle { get; set; } = "";
        private double _pomoPreciseRemaining = 25 * 60;

        // Countdown State
        public int CountTotalSeconds { get; set; } = 15 * 60;
        public int CountRemainingSeconds { get; set; } = 15 * 60;
        public bool CountIsRunning { get; set; } = false;
        public string CountTitle { get; set; } = "";
        public bool CountLoop { get; set; } = false;
        public int CountCompletedLoops { get; set; } = 0;
        public DateTime? CountTargetClockTime { get; private set; }
        public List<int> CustomCountdownPresetsSeconds { get; private set; } = new() { 60, 180, 300, 600, 900, 1800, 2700, 3600 };
        private double _countPreciseRemaining = 15 * 60;

        // Alarm & Customization State
        public List<AlarmEntry> Alarms { get; private set; } = new();
        public bool ShowAlarmInNotch { get; set; } = true;
        public int DraftAlarmHour { get; set; } = DateTime.Now.AddMinutes(30).Hour;
        public int DraftAlarmMinute { get; set; } = DateTime.Now.AddMinutes(30).Minute;
        public string DraftAlarmTitle { get; set; } = "";
        public bool DraftAlarmRepeatDaily { get; set; } = false;
        private readonly Dictionary<string, double> _alarmInitialSpanSeconds = new();

        public string SelectedSoundTone { get; set; } = "Classic";
        public int RingRepeatCount { get; set; } = 2;
        public int DefaultSnoozeMinutes { get; set; } = 5;
        public bool IsRinging { get; private set; }
        public string LastRingTitle { get; private set; } = "";

        // Stopwatch State
        public bool StopwatchIsRunning => _stopwatch.IsRunning;
        public TimeSpan StopwatchElapsed => _stopwatch.Elapsed;
        public string StopwatchTitle { get; set; } = "";
        public List<StopwatchLap> Laps { get; } = new();

        // Water State
        public int WaterGlasses { get; set; } = 0;
        public int WaterTarget { get; set; } = 8;
        public bool WaterReminderEnabled { get; set; } = false;
        public int WaterReminderIntervalMinutes { get; set; } = 60;
        public int WaterReminderRemainingSeconds { get; set; } = 60 * 60;
        public bool ShowWaterInNotch { get; set; } = false;
        private double _waterPreciseRemaining = 60 * 60;

        // General Notch Option (default false so stopped timers don't stick in Notch)
        public bool ShowPausedOnNotch { get; set; } = false;

        public event Action Tick;
        public event Action StateChanged;

        private TimerService()
        {
            LoadPrefs();

            _lastTickTimestamp = Stopwatch.GetTimestamp();
            // Adaptive timer tick: 1000ms when idle to conserve CPU, 25ms (40fps) during active countdown/stopwatch
            _clock = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000) };
            _clock.Tick += OnClockTick;
            _clock.Start();
        }

        public static string StripEmojis(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (char.IsHighSurrogate(c) && i + 1 < input.Length && char.IsLowSurrogate(input[i + 1]))
                {
                    i++;
                    continue;
                }
                if (c >= 0x2000 && c <= 0x32FF) continue;
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        private void LoadPrefs()
        {
            try
            {
                var p = PersistenceHelper.SafeReadJson<TimerPrefsModel>(PrefsFile);
                if (p != null)
                {
                    PomoWorkSeconds = Math.Clamp(p.PomoWorkSeconds, 60, 180 * 60);
                    PomoBreakSeconds = Math.Clamp(p.PomoBreakSeconds, 60, 60 * 60);
                    PomoLongBreakSeconds = Math.Clamp(p.PomoLongBreakSeconds, 60, 90 * 60);
                    PomoTargetCycles = Math.Clamp(p.PomoTargetCycles, 1, 12);
                    PomoAutoStartNext = p.PomoAutoStartNext;
                    PomoTitle = StripEmojis(p.PomoTitle ?? "");

                    string today = DateTime.Now.ToString("yyyy-MM-dd");
                    if (string.Equals(p.LastStatsDate, today, StringComparison.Ordinal))
                    {
                        PomoSessionsToday = Math.Max(0, p.PomoSessionsToday);
                        PomoFocusMinutesToday = Math.Max(0, p.PomoFocusMinutesToday);
                        WaterGlasses = Math.Clamp(p.WaterGlasses, 0, 30);
                    }

                    CountTotalSeconds = Math.Clamp(p.CountTotalSeconds, 5, 24 * 3600);
                    CountTitle = StripEmojis(p.CountTitle ?? "");
                    CountLoop = p.CountLoop;
                    if (p.CustomCountdownPresetsSeconds != null && p.CustomCountdownPresetsSeconds.Count > 0)
                    {
                        CustomCountdownPresetsSeconds = p.CustomCountdownPresetsSeconds
                            .Where(x => x >= 5 && x <= 24 * 3600)
                            .Distinct()
                            .OrderBy(x => x)
                            .ToList();
                    }

                    SelectedSoundTone = string.IsNullOrWhiteSpace(p.SelectedSoundTone) ? "Classic" : p.SelectedSoundTone;
                    RingRepeatCount = Math.Clamp(p.RingRepeatCount <= 0 ? 2 : p.RingRepeatCount, 1, 10);
                    DefaultSnoozeMinutes = Math.Clamp(p.DefaultSnoozeMinutes, 0, 30);

                    if (p.Alarms != null)
                    {
                        Alarms = p.Alarms;
                    }
                    ShowAlarmInNotch = p.ShowAlarmInNotch;

                    StopwatchTitle = StripEmojis(p.StopwatchTitle ?? "");

                    WaterTarget = Math.Clamp(p.WaterTarget, 4, 20);
                    WaterReminderEnabled = p.WaterReminderEnabled;
                    WaterReminderIntervalMinutes = Math.Clamp(p.WaterReminderIntervalMinutes, 10, 180);
                    ShowWaterInNotch = p.ShowWaterInNotch;
                    ShowPausedOnNotch = p.ShowPausedOnNotch;
                }
            }
            catch { }

            PomoRemainingSeconds = PomoWorkSeconds;
            _pomoPreciseRemaining = PomoWorkSeconds;
            CountRemainingSeconds = CountTotalSeconds;
            _countPreciseRemaining = CountTotalSeconds;
            WaterReminderRemainingSeconds = WaterReminderIntervalMinutes * 60;
            _waterPreciseRemaining = WaterReminderRemainingSeconds;

            var now = DateTime.Now;
            foreach (var al in Alarms.Where(a => a.IsEnabled))
            {
                var next = al.GetNextTargetTime(now);
                _alarmInitialSpanSeconds[al.Id] = Math.Max(60.0, (next - now).TotalSeconds);
            }
        }

        public void SavePrefs()
        {
            try
            {
                var model = new TimerPrefsModel
                {
                    PomoWorkSeconds = PomoWorkSeconds,
                    PomoBreakSeconds = PomoBreakSeconds,
                    PomoLongBreakSeconds = PomoLongBreakSeconds,
                    PomoTargetCycles = PomoTargetCycles,
                    PomoAutoStartNext = PomoAutoStartNext,
                    PomoTitle = PomoTitle ?? "",
                    PomoSessionsToday = PomoSessionsToday,
                    PomoFocusMinutesToday = PomoFocusMinutesToday,
                    LastStatsDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    CountTotalSeconds = CountTotalSeconds,
                    CountTitle = CountTitle ?? "",
                    CountLoop = CountLoop,
                    CustomCountdownPresetsSeconds = CustomCountdownPresetsSeconds,
                    SelectedSoundTone = SelectedSoundTone,
                    RingRepeatCount = RingRepeatCount,
                    DefaultSnoozeMinutes = DefaultSnoozeMinutes,
                    Alarms = Alarms,
                    ShowAlarmInNotch = ShowAlarmInNotch,
                    StopwatchTitle = StopwatchTitle ?? "",
                    WaterGlasses = WaterGlasses,
                    WaterTarget = WaterTarget,
                    WaterReminderEnabled = WaterReminderEnabled,
                    WaterReminderIntervalMinutes = WaterReminderIntervalMinutes,
                    ShowWaterInNotch = ShowWaterInNotch,
                    ShowPausedOnNotch = ShowPausedOnNotch
                };
                PersistenceHelper.SafeWriteJson(PrefsFile, model);
            }
            catch { }
        }

        /// <summary>
        /// Returns the active color for Timer UI & Notch capsules:
        /// - If "Sync with music" is enabled in Settings and music is playing, returns the song's beat color.
        /// - Otherwise returns the application's current Theme Accent color.
        /// </summary>
        public Color GetEffectiveAccentColor()
        {
            try
            {
                var prefs = AppPreferences.Instance;
                bool syncWithMusic = prefs.EdgeLightSyncWithMusic ||
                                     string.Equals(prefs.EdgeLightColor, "Artwork", StringComparison.OrdinalIgnoreCase);

                if (syncWithMusic && RealtimeNotchActionService.Instance.IsMusicPlaying)
                {
                    if (!string.Equals(prefs.MusicBeatColor, "Artwork", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            return (Color)ColorConverter.ConvertFromString(prefs.MusicBeatColor);
                        }
                        catch { }
                    }

                    Color beat = RealtimeNotchActionService.Instance.CurrentBeatColor;
                    if (beat.A > 0)
                    {
                        double lum = (0.299 * beat.R + 0.587 * beat.G + 0.114 * beat.B) / 255.0;
                        double maxC = Math.Max(beat.R, Math.Max(beat.G, beat.B));
                        double minC = Math.Min(beat.R, Math.Min(beat.G, beat.B));
                        double sat = maxC > 0 ? (maxC - minC) / maxC : 0;
                        if (lum > 0.80 || sat < 0.15)
                        {
                            return Color.FromRgb(255, 60, 90);
                        }
                        return beat;
                    }
                }
            }
            catch { }

            return CapsuleTheme.Current.Accent;
        }

        private void EnsureClockRate()
        {
            bool anyActive = StopwatchIsRunning || PomoIsRunning || CountIsRunning;
            var target = anyActive ? TimeSpan.FromMilliseconds(25) : TimeSpan.FromMilliseconds(1000);
            if (_clock.Interval != target)
            {
                _clock.Interval = target;
            }
        }

        private void OnClockTick(object sender, EventArgs e)
        {
            long nowTs = Stopwatch.GetTimestamp();
            double dt = Math.Clamp((nowTs - _lastTickTimestamp) / (double)Stopwatch.Frequency, 0.0, 0.5);
            _lastTickTimestamp = nowTs;

            EnsureClockRate();

            bool shouldNotify = StopwatchIsRunning;

            if (PomoIsRunning)
            {
                shouldNotify = true;
                _pomoPreciseRemaining -= dt;
                if (_pomoPreciseRemaining > 0.0)
                {
                    PomoRemainingSeconds = (int)Math.Ceiling(_pomoPreciseRemaining);
                }
                else
                {
                    PomoRemainingSeconds = 0;
                    _pomoPreciseRemaining = 0.0;
                    OnPomoPhaseCompleted();
                }
            }

            if (CountIsRunning)
            {
                shouldNotify = true;
                if (CountTargetClockTime.HasValue)
                {
                    double rem = (CountTargetClockTime.Value - DateTime.Now).TotalSeconds;
                    _countPreciseRemaining = Math.Max(0.0, rem);
                }
                else
                {
                    _countPreciseRemaining -= dt;
                }

                if (_countPreciseRemaining > 0.0)
                {
                    CountRemainingSeconds = (int)Math.Ceiling(_countPreciseRemaining);
                }
                else
                {
                    CountRemainingSeconds = 0;
                    _countPreciseRemaining = 0.0;
                    OnCountdownCompleted();
                }
            }

            // Check Alarms
            if (Alarms.Count > 0)
            {
                DateTime nowLocal = DateTime.Now;
                string minuteKey = nowLocal.ToString("yyyyMMdd-HHmm");
                foreach (var alarm in Alarms)
                {
                    if (!alarm.IsEnabled) continue;
                    shouldNotify = true;

                    if (alarm.SnoozedUntilLocal.HasValue)
                    {
                        if (nowLocal >= alarm.SnoozedUntilLocal.Value)
                        {
                            alarm.SnoozedUntilLocal = null;
                            alarm.LastTriggeredDateKey = minuteKey;
                            OnAlarmTriggered(alarm);
                        }
                        continue;
                    }

                    if (nowLocal.Hour == alarm.Hour &&
                        nowLocal.Minute == alarm.Minute &&
                        nowLocal.Second <= 2 &&
                        !string.Equals(alarm.LastTriggeredDateKey, minuteKey, StringComparison.Ordinal))
                    {
                        alarm.LastTriggeredDateKey = minuteKey;
                        OnAlarmTriggered(alarm);
                    }
                }
            }

            if (WaterReminderEnabled)
            {
                shouldNotify = true;
                _waterPreciseRemaining -= dt;
                if (_waterPreciseRemaining > 0.0)
                {
                    WaterReminderRemainingSeconds = (int)Math.Ceiling(_waterPreciseRemaining);
                }
                else
                {
                    OnWaterReminderTriggered();
                }
            }

            if (shouldNotify)
            {
                Tick?.Invoke();
            }
        }

        private void OnPomoPhaseCompleted()
        {
            bool wasWork = PomoIsWork;
            if (PomoIsWork)
            {
                PomoSessionsToday++;
                PomoFocusMinutesToday += Math.Max(1, PomoWorkSeconds / 60);
                if (PomoCurrentCycle >= PomoTargetCycles)
                {
                    PomoIsWork = false;
                    PomoRemainingSeconds = PomoLongBreakSeconds;
                    _pomoPreciseRemaining = PomoLongBreakSeconds;
                }
                else
                {
                    PomoIsWork = false;
                    PomoRemainingSeconds = PomoBreakSeconds;
                    _pomoPreciseRemaining = PomoBreakSeconds;
                }
            }
            else
            {
                if (PomoCurrentCycle >= PomoTargetCycles)
                {
                    PomoCurrentCycle = 1;
                }
                else
                {
                    PomoCurrentCycle++;
                }
                PomoIsWork = true;
                PomoRemainingSeconds = PomoWorkSeconds;
                _pomoPreciseRemaining = PomoWorkSeconds;
            }

            if (!PomoAutoStartNext)
            {
                PomoIsRunning = false;
            }

            SavePrefs();
            string title = wasWork
                ? LocalizationService.T("focus.alert_focus_done", "Focus session complete!")
                : LocalizationService.T("focus.alert_break_done", "Break finished!");
            string sub = !string.IsNullOrWhiteSpace(PomoTitle)
                ? $" · {PomoTitle}"
                : (PomoIsWork ? " · Time to focus" : " · Take a break");

            TriggerCompletionAlert("⏱", title, sub, SelectedSoundTone, RingRepeatCount);
            StateChanged?.Invoke();
        }

        private void OnCountdownCompleted()
        {
            string sub = !string.IsNullOrWhiteSpace(CountTitle)
                ? $" · {CountTitle}"
                : $" · {FormatDuration(CountTotalSeconds)}";

            CountTargetClockTime = null;

            if (CountLoop)
            {
                CountCompletedLoops++;
                CountRemainingSeconds = CountTotalSeconds;
                _countPreciseRemaining = CountTotalSeconds;
                CountIsRunning = true;
            }
            else
            {
                CountIsRunning = false;
                CountRemainingSeconds = CountTotalSeconds;
                _countPreciseRemaining = CountTotalSeconds;
            }

            TriggerCompletionAlert(
                "⏰",
                LocalizationService.T("focus.alert_timer_done", "Timer finished!"),
                sub,
                SelectedSoundTone,
                RingRepeatCount);

            StateChanged?.Invoke();
        }

        private void OnAlarmTriggered(AlarmEntry alarm)
        {
            if (!alarm.RepeatDaily)
            {
                alarm.IsEnabled = false;
            }
            else
            {
                var next = alarm.GetNextTargetTime(DateTime.Now.AddSeconds(5));
                _alarmInitialSpanSeconds[alarm.Id] = Math.Max(60.0, (next - DateTime.Now).TotalSeconds);
            }

            SavePrefs();

            string timeLabel = $"{alarm.Hour:D2}:{alarm.Minute:D2}";
            string sub = !string.IsNullOrWhiteSpace(alarm.Title)
                ? $" · {alarm.Title} ({timeLabel})"
                : $" · {timeLabel}";

            TriggerCompletionAlert(
                "🔔",
                LocalizationService.T("focus.alert_alarm_ring", "Alarm"),
                sub,
                string.IsNullOrWhiteSpace(alarm.SoundTone) ? SelectedSoundTone : alarm.SoundTone,
                Math.Max(2, RingRepeatCount));

            StateChanged?.Invoke();
        }

        private void OnWaterReminderTriggered()
        {
            WaterReminderRemainingSeconds = WaterReminderIntervalMinutes * 60;
            _waterPreciseRemaining = WaterReminderRemainingSeconds;

            TriggerCompletionAlert(
                "💧",
                LocalizationService.T("focus.alert_water", "Hydration Reminder"),
                $" · {WaterGlasses}/{WaterTarget} glasses today",
                SelectedSoundTone,
                1);

            StateChanged?.Invoke();
        }

        private void TriggerCompletionAlert(string icon, string title, string subtitle, string tone, int repeats)
        {
            IsRinging = true;
            LastRingTitle = !string.IsNullOrWhiteSpace(subtitle) ? $"{title}{subtitle}" : title;
            PlaySynthesizedTone(tone, repeats);

            try
            {
                RealtimeNotchActionService.Instance.TriggerNotification(new NotchNotificationPayload
                {
                    Icon = icon,
                    Title = title,
                    Subtitle = subtitle,
                    DurationMs = 4500
                });
            }
            catch { }
        }

        #region Synthesized Multi-Tone Alarm Sound Engine

        public void PlaySoundPreview(string tone = null)
        {
            PlaySynthesizedTone(tone ?? SelectedSoundTone, 1);
        }

        public void StopRingingSound()
        {
            IsRinging = false;
            try
            {
                _soundCts?.Cancel();
            }
            catch { }
            StateChanged?.Invoke();
        }

        public void SnoozeLastRing(int? customMinutes = null)
        {
            int mins = customMinutes ?? (DefaultSnoozeMinutes > 0 ? DefaultSnoozeMinutes : 5);
            StopRingingSound();
            StartCountdownInMinutes(mins, string.IsNullOrWhiteSpace(CountTitle) ? "Snooze" : CountTitle);
        }

        private void PlaySynthesizedTone(string tone, int repeats)
        {
            try
            {
                _soundCts?.Cancel();
            }
            catch { }

            if (string.Equals(tone, "Silent", StringComparison.OrdinalIgnoreCase))
            {
                IsRinging = false;
                return;
            }

            var cts = new CancellationTokenSource();
            _soundCts = cts;
            int count = Math.Clamp(repeats, 1, 10);

            Task.Run(async () =>
            {
                try
                {
                    byte[] wavBytes = BuildToneWavBytes(tone);
                    for (int i = 0; i < count; i++)
                    {
                        if (cts.Token.IsCancellationRequested) break;
                        using var ms = new MemoryStream(wavBytes);
                        using var player = new SoundPlayer(ms);
                        player.PlaySync();
                        if (i + 1 < count)
                        {
                            await Task.Delay(220, cts.Token);
                        }
                    }
                }
                catch
                {
                    try { SystemSounds.Asterisk.Play(); } catch { }
                }
                finally
                {
                    if (_soundCts == cts)
                    {
                        IsRinging = false;
                        StateChanged?.Invoke();
                    }
                }
            }, cts.Token);
        }

        private static byte[] BuildToneWavBytes(string tone)
        {
            // Define note sequences: (freqHz, durationMs, gapMs)
            (double freq, int durMs, int gapMs)[] notes = (tone ?? "Classic").ToLowerInvariant() switch
            {
                "radar" => new[]
                {
                    (880.0, 95, 45), (880.0, 95, 45), (880.0, 95, 45), (880.0, 95, 190),
                    (880.0, 95, 45), (880.0, 95, 45), (880.0, 95, 45), (880.0, 95, 80)
                },
                "digital" => new[]
                {
                    (2048.0, 70, 40), (2048.0, 70, 160),
                    (2048.0, 70, 40), (2048.0, 70, 160),
                    (2560.0, 120, 60)
                },
                "gentle" => new[]
                {
                    (349.23, 180, 20), (440.0, 180, 20), (523.25, 180, 20), (659.25, 360, 80)
                },
                "zen" => new[]
                {
                    (432.0, 720, 60), (648.0, 520, 40)
                },
                _ => new[] // Classic
                {
                    (523.25, 130, 25), (659.25, 130, 25), (783.99, 130, 25), (1046.50, 280, 110),
                    (523.25, 130, 25), (659.25, 130, 25), (783.99, 130, 25), (1046.50, 320, 50)
                }
            };

            const int sampleRate = 44100;
            int totalMs = notes.Sum(n => n.durMs + n.gapMs);
            int totalSamples = (int)((long)sampleRate * totalMs / 1000);

            using var ms = new MemoryStream(44 + totalSamples * 2);
            using var bw = new BinaryWriter(ms);

            int dataSize = totalSamples * 2;
            bw.Write(new[] { 'R', 'I', 'F', 'F' });
            bw.Write(36 + dataSize);
            bw.Write(new[] { 'W', 'A', 'V', 'E' });
            bw.Write(new[] { 'f', 'm', 't', ' ' });
            bw.Write(16);
            bw.Write((short)1); // PCM
            bw.Write((short)1); // Mono
            bw.Write(sampleRate);
            bw.Write(sampleRate * 2);
            bw.Write((short)2);
            bw.Write((short)16);
            bw.Write(new[] { 'd', 'a', 't', 'a' });
            bw.Write(dataSize);

            foreach (var (freq, durMs, gapMs) in notes)
            {
                int noteSamples = (int)((long)sampleRate * durMs / 1000);
                int gapSamples = (int)((long)sampleRate * gapMs / 1000);

                for (int i = 0; i < noteSamples; i++)
                {
                    double t = i / (double)sampleRate;
                    double progress = i / (double)Math.Max(1, noteSamples);
                    double attack = Math.Min(1.0, i / (sampleRate * 0.008));
                    double decay = Math.Exp(-3.2 * progress);
                    double env = attack * decay;

                    double wave = Math.Sin(2.0 * Math.PI * freq * t) +
                                  0.25 * Math.Sin(2.0 * Math.PI * (freq * 2.0) * t);
                    short sample = (short)Math.Clamp(wave * env * 11000.0, short.MinValue, short.MaxValue);
                    bw.Write(sample);
                }

                for (int i = 0; i < gapSamples; i++)
                {
                    bw.Write((short)0);
                }
            }

            return ms.ToArray();
        }

        #endregion

        #region Active Notch Snapshot

        public double GetPomoRemainingRatio()
        {
            if (!PomoIsRunning && PomoRemainingSeconds >= (PomoIsWork ? PomoWorkSeconds : PomoBreakSeconds))
            {
                return 0.0;
            }
            int total = PomoIsWork
                ? PomoWorkSeconds
                : (PomoCurrentCycle >= PomoTargetCycles ? PomoLongBreakSeconds : PomoBreakSeconds);
            if (total <= 0) return 0.0;
            return Math.Clamp(_pomoPreciseRemaining / total, 0.0, 1.0);
        }

        public double GetCountdownRemainingRatio()
        {
            if (!CountIsRunning && CountRemainingSeconds >= CountTotalSeconds)
            {
                return 0.0;
            }
            if (CountTotalSeconds <= 0) return 0.0;
            return Math.Clamp(_countPreciseRemaining / CountTotalSeconds, 0.0, 1.0);
        }

        public AlarmEntry GetNearestEnabledAlarm(out TimeSpan remaining, out double remainingRatio)
        {
            remaining = TimeSpan.Zero;
            remainingRatio = 0.0;
            var enabled = Alarms.Where(a => a.IsEnabled).ToList();
            if (enabled.Count == 0) return null;

            var now = DateTime.Now;
            AlarmEntry best = null;
            DateTime bestTarget = DateTime.MaxValue;

            foreach (var al in enabled)
            {
                var target = al.GetNextTargetTime(now);
                if (target < bestTarget)
                {
                    bestTarget = target;
                    best = al;
                }
            }

            if (best == null) return null;
            remaining = bestTarget > now ? (bestTarget - now) : TimeSpan.Zero;
            if (!_alarmInitialSpanSeconds.TryGetValue(best.Id, out double totalSpan) || totalSpan < remaining.TotalSeconds)
            {
                totalSpan = Math.Max(60.0, remaining.TotalSeconds);
                _alarmInitialSpanSeconds[best.Id] = totalSpan;
            }

            remainingRatio = Math.Clamp(remaining.TotalSeconds / totalSpan, 0.0, 1.0);
            return best;
        }

        public NotchTimerSnapshot GetActiveNotchTimer()
        {
            Color effectiveColor = GetEffectiveAccentColor();

            bool pomoActive = PomoIsRunning || (ShowPausedOnNotch && PomoRemainingSeconds < PomoWorkSeconds && PomoRemainingSeconds > 0);
            bool countActive = CountIsRunning || (ShowPausedOnNotch && CountRemainingSeconds < CountTotalSeconds && CountRemainingSeconds > 0);
            bool swActive = StopwatchIsRunning || (ShowPausedOnNotch && StopwatchElapsed.TotalSeconds > 0.1);
            var nearestAlarm = GetNearestEnabledAlarm(out TimeSpan alarmRem, out double alarmRatio);
            bool alarmActive = nearestAlarm != null && ShowAlarmInNotch;
            bool waterActive = WaterReminderEnabled && ShowWaterInNotch;

            TimerMode? chosen = null;

            // Prioritize LastActiveMode if it is currently running
            if (LastActiveMode == TimerMode.Countdown && CountIsRunning) chosen = TimerMode.Countdown;
            else if (LastActiveMode == TimerMode.Pomodoro && PomoIsRunning) chosen = TimerMode.Pomodoro;
            else if (LastActiveMode == TimerMode.Stopwatch && StopwatchIsRunning) chosen = TimerMode.Stopwatch;
            else if (LastActiveMode == TimerMode.Alarm && alarmActive) chosen = TimerMode.Alarm;
            else if (LastActiveMode == TimerMode.Water && waterActive) chosen = TimerMode.Water;

            // Next check any actively running timer
            if (chosen == null)
            {
                if (CountIsRunning) chosen = TimerMode.Countdown;
                else if (PomoIsRunning) chosen = TimerMode.Pomodoro;
                else if (StopwatchIsRunning) chosen = TimerMode.Stopwatch;
                else if (alarmActive) chosen = TimerMode.Alarm;
                else if (waterActive) chosen = TimerMode.Water;
                else if (countActive) chosen = TimerMode.Countdown;
                else if (pomoActive) chosen = TimerMode.Pomodoro;
                else if (swActive) chosen = TimerMode.Stopwatch;
            }

            if (chosen == null)
            {
                return new NotchTimerSnapshot
                {
                    IsActive = false,
                    IsRunning = false,
                    RemainingRatio = 0.0,
                    AccentColor = effectiveColor
                };
            }

            switch (chosen.Value)
            {
                case TimerMode.Countdown:
                {
                    string title = !string.IsNullOrWhiteSpace(CountTitle) ? CountTitle.Trim() : "";
                    return new NotchTimerSnapshot
                    {
                        IsActive = true,
                        IsRunning = CountIsRunning,
                        Mode = TimerMode.Countdown,
                        DisplayTime = FormatDuration(CountRemainingSeconds),
                        Title = title,
                        RemainingRatio = CountIsRunning ? GetCountdownRemainingRatio() : 0.0,
                        AccentColor = effectiveColor
                    };
                }
                case TimerMode.Pomodoro:
                {
                    string defaultPhase = PomoIsWork
                        ? LocalizationService.T("focus.focus", "Focus")
                        : LocalizationService.T("focus.break", "Break");
                    string title = !string.IsNullOrWhiteSpace(PomoTitle) ? PomoTitle.Trim() : defaultPhase;
                    return new NotchTimerSnapshot
                    {
                        IsActive = true,
                        IsRunning = PomoIsRunning,
                        Mode = TimerMode.Pomodoro,
                        DisplayTime = FormatDuration(PomoRemainingSeconds),
                        Title = title,
                        RemainingRatio = PomoIsRunning ? GetPomoRemainingRatio() : 0.0,
                        AccentColor = effectiveColor
                    };
                }
                case TimerMode.Alarm:
                {
                    int remSec = Math.Max(0, (int)Math.Ceiling(alarmRem.TotalSeconds));
                    string alarmTitle = !string.IsNullOrWhiteSpace(nearestAlarm.Title)
                        ? $"{nearestAlarm.Title.Trim()} ({nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2})"
                        : $"{nearestAlarm.Hour:D2}:{nearestAlarm.Minute:D2}";
                    return new NotchTimerSnapshot
                    {
                        IsActive = true,
                        IsRunning = true,
                        Mode = TimerMode.Alarm,
                        DisplayTime = FormatDuration(remSec),
                        Title = alarmTitle,
                        RemainingRatio = alarmRatio,
                        AccentColor = effectiveColor
                    };
                }
                case TimerMode.Stopwatch:
                {
                    var el = StopwatchElapsed;
                    string timeStr = el.TotalHours >= 1.0
                        ? $"{(int)el.TotalHours:D2}:{el.Minutes:D2}:{el.Seconds:D2}"
                        : $"{el.Minutes:D2}:{el.Seconds:D2}.{el.Milliseconds / 100}";
                    // Stopwatch progress bar goes from 0% (0.0) UP to 100% (1.0) over each 60-second revolution, and resets to 0 when stopped
                    double secInMin = el.TotalSeconds % 60.0;
                    double ratio = StopwatchIsRunning ? Math.Clamp(secInMin / 60.0, 0.0, 1.0) : 0.0;
                    return new NotchTimerSnapshot
                    {
                        IsActive = true,
                        IsRunning = StopwatchIsRunning,
                        Mode = TimerMode.Stopwatch,
                        DisplayTime = timeStr,
                        Title = !string.IsNullOrWhiteSpace(StopwatchTitle) ? StopwatchTitle.Trim() : "",
                        RemainingRatio = ratio,
                        AccentColor = effectiveColor
                    };
                }
                case TimerMode.Water:
                {
                    double total = Math.Max(1, WaterReminderIntervalMinutes * 60);
                    return new NotchTimerSnapshot
                    {
                        IsActive = true,
                        IsRunning = WaterReminderEnabled,
                        Mode = TimerMode.Water,
                        DisplayTime = FormatDuration(WaterReminderRemainingSeconds),
                        Title = $"{WaterGlasses}/{WaterTarget} glasses",
                        RemainingRatio = WaterReminderEnabled ? Math.Clamp(_waterPreciseRemaining / total, 0.0, 1.0) : 0.0,
                        AccentColor = effectiveColor
                    };
                }
            }

            return new NotchTimerSnapshot
            {
                IsActive = false,
                IsRunning = false,
                RemainingRatio = 0.0,
                AccentColor = effectiveColor
            };
        }

        public void ToggleActiveNotchTimer()
        {
            var snap = GetActiveNotchTimer();
            if (!snap.IsActive) return;

            switch (snap.Mode)
            {
                case TimerMode.Countdown:
                    ToggleCountdown();
                    break;
                case TimerMode.Pomodoro:
                    TogglePomo();
                    break;
                case TimerMode.Alarm:
                {
                    var nearest = GetNearestEnabledAlarm(out _, out _);
                    if (nearest != null) ToggleAlarmEnabled(nearest.Id);
                    break;
                }
                case TimerMode.Stopwatch:
                    ToggleStopwatch();
                    break;
                case TimerMode.Water:
                    ToggleWaterReminder();
                    break;
            }
        }

        public static string FormatDuration(int totalSeconds)
        {
            totalSeconds = Math.Max(0, totalSeconds);
            int h = totalSeconds / 3600;
            int m = (totalSeconds % 3600) / 60;
            int s = totalSeconds % 60;
            return h > 0 ? $"{h:D2}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
        }

        #endregion

        #region Pomodoro Actions

        public void TogglePomo()
        {
            LastActiveMode = TimerMode.Pomodoro;
            _lastTickTimestamp = Stopwatch.GetTimestamp();
            PomoIsRunning = !PomoIsRunning;
            StateChanged?.Invoke();
        }

        public void ResetPomo()
        {
            PomoIsRunning = false;
            PomoIsWork = true;
            PomoRemainingSeconds = PomoWorkSeconds;
            _pomoPreciseRemaining = PomoWorkSeconds;
            StopRingingSound();
            StateChanged?.Invoke();
        }

        public void SkipPomo()
        {
            LastActiveMode = TimerMode.Pomodoro;
            OnPomoPhaseCompleted();
        }

        public void AddPomoRemainingSeconds(int deltaSeconds)
        {
            int next = Math.Clamp(PomoRemainingSeconds + deltaSeconds, 5, 180 * 60);
            PomoRemainingSeconds = next;
            _pomoPreciseRemaining = next;
            StateChanged?.Invoke();
        }

        public void AdjustPomoWork(int deltaMinutes)
        {
            int mins = Math.Clamp((PomoWorkSeconds / 60) + deltaMinutes, 1, 120);
            PomoWorkSeconds = mins * 60;
            if (PomoIsWork && !PomoIsRunning)
            {
                PomoRemainingSeconds = PomoWorkSeconds;
                _pomoPreciseRemaining = PomoWorkSeconds;
            }
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void AdjustPomoBreak(int deltaMinutes)
        {
            int mins = Math.Clamp((PomoBreakSeconds / 60) + deltaMinutes, 1, 45);
            PomoBreakSeconds = mins * 60;
            if (!PomoIsWork && !PomoIsRunning)
            {
                PomoRemainingSeconds = PomoBreakSeconds;
                _pomoPreciseRemaining = PomoBreakSeconds;
            }
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void AdjustPomoLongBreak(int deltaMinutes)
        {
            int mins = Math.Clamp((PomoLongBreakSeconds / 60) + deltaMinutes, 5, 60);
            PomoLongBreakSeconds = mins * 60;
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void AdjustPomoCycles(int delta)
        {
            PomoTargetCycles = Math.Clamp(PomoTargetCycles + delta, 1, 12);
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void SetPomoTitle(string title)
        {
            PomoTitle = StripEmojis(title ?? "");
            SavePrefs();
            StateChanged?.Invoke();
        }

        #endregion

        #region Countdown Actions ("چند دقیقه دیگر" & "رأس ساعت مشخص")

        public void ToggleCountdown()
        {
            LastActiveMode = TimerMode.Countdown;
            _lastTickTimestamp = Stopwatch.GetTimestamp();
            if (!CountIsRunning && CountRemainingSeconds <= 0)
            {
                CountRemainingSeconds = CountTotalSeconds;
                _countPreciseRemaining = CountTotalSeconds;
            }
            CountTargetClockTime = null;
            CountIsRunning = !CountIsRunning;
            StateChanged?.Invoke();
        }

        public void ResetCountdown()
        {
            CountIsRunning = false;
            CountTargetClockTime = null;
            CountCompletedLoops = 0;
            CountRemainingSeconds = CountTotalSeconds;
            _countPreciseRemaining = CountTotalSeconds;
            StopRingingSound();
            StateChanged?.Invoke();
        }

        public void SetCountdownPreset(int totalSeconds, bool autoStart = false)
        {
            LastActiveMode = TimerMode.Countdown;
            totalSeconds = Math.Clamp(totalSeconds, 5, 24 * 3600);
            CountTargetClockTime = null;
            CountTotalSeconds = totalSeconds;
            CountRemainingSeconds = totalSeconds;
            _countPreciseRemaining = totalSeconds;
            if (autoStart)
            {
                _lastTickTimestamp = Stopwatch.GetTimestamp();
                CountIsRunning = true;
            }
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void StartCountdownInMinutes(double minutes, string optionalTitle = null)
        {
            int totalSec = Math.Clamp((int)Math.Round(minutes * 60.0), 5, 24 * 3600);
            if (!string.IsNullOrWhiteSpace(optionalTitle))
            {
                CountTitle = optionalTitle;
            }
            SetCountdownPreset(totalSec, autoStart: true);
        }

        /// <summary>
        /// Starts a countdown that rings at the exact specified clock time (HH:MM).
        /// </summary>
        public void StartCountdownUntilClockTime(int hour, int minute, string optionalTitle = null)
        {
            LastActiveMode = TimerMode.Countdown;
            var now = DateTime.Now;
            var target = new DateTime(now.Year, now.Month, now.Day, Math.Clamp(hour, 0, 23), Math.Clamp(minute, 0, 59), 0);
            if (target <= now)
            {
                target = target.AddDays(1);
            }

            int totalSec = Math.Max(5, (int)Math.Ceiling((target - now).TotalSeconds));
            CountTargetClockTime = target;
            CountTotalSeconds = totalSec;
            CountRemainingSeconds = totalSec;
            _countPreciseRemaining = (target - now).TotalSeconds;
            if (!string.IsNullOrWhiteSpace(optionalTitle))
            {
                CountTitle = optionalTitle;
            }
            _lastTickTimestamp = Stopwatch.GetTimestamp();
            CountIsRunning = true;
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void SetCountdownExactComponents(int hours, int minutes, int seconds)
        {
            int total = Math.Clamp(hours * 3600 + minutes * 60 + seconds, 5, 24 * 3600 - 1);
            CountTargetClockTime = null;
            CountTotalSeconds = total;
            CountRemainingSeconds = total;
            _countPreciseRemaining = total;
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void AdjustCountdownComponents(int deltaHours, int deltaMinutes, int deltaSeconds)
        {
            int baseSec = CountIsRunning ? CountRemainingSeconds : CountTotalSeconds;
            int next = Math.Clamp(baseSec + deltaHours * 3600 + deltaMinutes * 60 + deltaSeconds, 5, 24 * 3600 - 1);
            CountTargetClockTime = null;
            if (!CountIsRunning)
            {
                CountTotalSeconds = next;
            }
            else if (next > CountTotalSeconds)
            {
                CountTotalSeconds = next;
            }
            CountRemainingSeconds = next;
            _countPreciseRemaining = next;
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void AddCountdownRunningSeconds(int deltaSeconds)
        {
            LastActiveMode = TimerMode.Countdown;
            int next = Math.Clamp(CountRemainingSeconds + deltaSeconds, 5, 24 * 3600 - 1);
            if (CountTargetClockTime.HasValue)
            {
                CountTargetClockTime = CountTargetClockTime.Value.AddSeconds(deltaSeconds);
            }
            if (next > CountTotalSeconds)
            {
                CountTotalSeconds = next;
            }
            CountRemainingSeconds = next;
            _countPreciseRemaining = next;
            StateChanged?.Invoke();
        }

        public void AddCustomCountdownPreset(int seconds)
        {
            seconds = Math.Clamp(seconds, 5, 24 * 3600);
            if (!CustomCountdownPresetsSeconds.Contains(seconds))
            {
                CustomCountdownPresetsSeconds.Add(seconds);
                CustomCountdownPresetsSeconds.Sort();
                SavePrefs();
                StateChanged?.Invoke();
            }
        }

        public void RemoveCustomCountdownPreset(int seconds)
        {
            if (CustomCountdownPresetsSeconds.Count <= 2) return;
            if (CustomCountdownPresetsSeconds.Remove(seconds))
            {
                SavePrefs();
                StateChanged?.Invoke();
            }
        }

        public void SetCountdownTitle(string title)
        {
            CountTitle = StripEmojis(title ?? "");
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void ToggleCountdownLoop()
        {
            CountLoop = !CountLoop;
            SavePrefs();
            StateChanged?.Invoke();
        }

        #endregion

        #region Alarm & Sound Customization Actions

        public void SetDraftAlarmFromMinutesOffset(int minutesFromNow)
        {
            var target = DateTime.Now.AddMinutes(Math.Clamp(minutesFromNow, 1, 24 * 60));
            DraftAlarmHour = target.Hour;
            DraftAlarmMinute = target.Minute;
            StateChanged?.Invoke();
        }

        public void AdjustDraftAlarmTime(int deltaHours, int deltaMinutes)
        {
            int totalMins = (DraftAlarmHour * 60 + DraftAlarmMinute + deltaHours * 60 + deltaMinutes) % (24 * 60);
            if (totalMins < 0) totalMins += 24 * 60;
            DraftAlarmHour = totalMins / 60;
            DraftAlarmMinute = totalMins % 60;
            StateChanged?.Invoke();
        }

        public void SetDraftAlarmExact(int hour, int minute)
        {
            DraftAlarmHour = Math.Clamp(hour, 0, 23);
            DraftAlarmMinute = Math.Clamp(minute, 0, 59);
            StateChanged?.Invoke();
        }

        public AlarmEntry AddOrUpdateAlarm(int hour, int minute, string title = null, bool? repeatDaily = null)
        {
            LastActiveMode = TimerMode.Alarm;
            hour = Math.Clamp(hour, 0, 23);
            minute = Math.Clamp(minute, 0, 59);

            var existing = Alarms.FirstOrDefault(a => a.Hour == hour && a.Minute == minute);
            if (existing != null)
            {
                existing.IsEnabled = true;
                if (!string.IsNullOrWhiteSpace(title)) existing.Title = title.Trim();
                if (repeatDaily.HasValue) existing.RepeatDaily = repeatDaily.Value;
                existing.SoundTone = SelectedSoundTone;
                existing.SnoozeMinutes = DefaultSnoozeMinutes;
                existing.SnoozedUntilLocal = null;
                var next = existing.GetNextTargetTime(DateTime.Now);
                _alarmInitialSpanSeconds[existing.Id] = Math.Max(60.0, (next - DateTime.Now).TotalSeconds);
                SavePrefs();
                StateChanged?.Invoke();
                return existing;
            }

            var entry = new AlarmEntry
            {
                Hour = hour,
                Minute = minute,
                Title = (title ?? DraftAlarmTitle ?? "").Trim(),
                IsEnabled = true,
                RepeatDaily = repeatDaily ?? DraftAlarmRepeatDaily,
                SoundTone = SelectedSoundTone,
                SnoozeMinutes = DefaultSnoozeMinutes
            };
            var target = entry.GetNextTargetTime(DateTime.Now);
            _alarmInitialSpanSeconds[entry.Id] = Math.Max(60.0, (target - DateTime.Now).TotalSeconds);

            Alarms.Add(entry);
            Alarms = Alarms.OrderBy(a => a.Hour * 60 + a.Minute).ToList();
            SavePrefs();
            StateChanged?.Invoke();
            return entry;
        }

        public AlarmEntry AddQuickAlarmInMinutes(int minutesFromNow, string title = null)
        {
            var target = DateTime.Now.AddMinutes(Math.Clamp(minutesFromNow, 1, 24 * 60));
            DraftAlarmHour = target.Hour;
            DraftAlarmMinute = target.Minute;
            return AddOrUpdateAlarm(target.Hour, target.Minute, title ?? $"In {minutesFromNow}m", repeatDaily: false);
        }

        public void ToggleAlarmEnabled(string id)
        {
            var al = Alarms.FirstOrDefault(a => a.Id == id);
            if (al == null) return;
            al.IsEnabled = !al.IsEnabled;
            al.SnoozedUntilLocal = null;
            if (al.IsEnabled)
            {
                LastActiveMode = TimerMode.Alarm;
                var target = al.GetNextTargetTime(DateTime.Now);
                _alarmInitialSpanSeconds[al.Id] = Math.Max(60.0, (target - DateTime.Now).TotalSeconds);
            }
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void RemoveAlarm(string id)
        {
            Alarms.RemoveAll(a => a.Id == id);
            _alarmInitialSpanSeconds.Remove(id);
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void ClearAllAlarms()
        {
            Alarms.Clear();
            _alarmInitialSpanSeconds.Clear();
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void CycleSoundTone()
        {
            int idx = Array.FindIndex(AvailableSoundTones, t => string.Equals(t, SelectedSoundTone, StringComparison.OrdinalIgnoreCase));
            SelectedSoundTone = AvailableSoundTones[(idx + 1) % AvailableSoundTones.Length];
            SavePrefs();
            PlaySoundPreview(SelectedSoundTone);
            StateChanged?.Invoke();
        }

        public void SetSoundTone(string tone)
        {
            SelectedSoundTone = tone ?? "Classic";
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void CycleRingRepeatCount()
        {
            RingRepeatCount = RingRepeatCount switch
            {
                1 => 2,
                2 => 3,
                3 => 5,
                _ => 1
            };
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void SetSnoozeMinutes(int mins)
        {
            DefaultSnoozeMinutes = Math.Clamp(mins, 0, 30);
            SavePrefs();
            StateChanged?.Invoke();
        }

        #endregion

        #region Stopwatch Actions

        public void ToggleStopwatch()
        {
            LastActiveMode = TimerMode.Stopwatch;
            if (_stopwatch.IsRunning) _stopwatch.Stop();
            else _stopwatch.Start();
            StateChanged?.Invoke();
        }

        public void RecordStopwatchLap()
        {
            if (_stopwatch.Elapsed.TotalMilliseconds < 10) return;
            var total = _stopwatch.Elapsed;
            var split = total - _lastLapTotal;
            _lastLapTotal = total;

            Laps.Insert(0, new StopwatchLap
            {
                LapNumber = Laps.Count + 1,
                SplitTime = split,
                TotalTime = total
            });
            StateChanged?.Invoke();
        }

        public void ResetStopwatch()
        {
            _stopwatch.Reset();
            _lastLapTotal = TimeSpan.Zero;
            Laps.Clear();
            StateChanged?.Invoke();
        }

        public void SetStopwatchTitle(string title)
        {
            StopwatchTitle = StripEmojis(title ?? "");
            SavePrefs();
            StateChanged?.Invoke();
        }

        #endregion

        #region Water / Hydration Actions

        public void AddWaterGlass(int count = 1)
        {
            WaterGlasses = Math.Clamp(WaterGlasses + count, 0, 30);
            if (WaterReminderEnabled)
            {
                WaterReminderRemainingSeconds = WaterReminderIntervalMinutes * 60;
                _waterPreciseRemaining = WaterReminderRemainingSeconds;
            }
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void RemoveWaterGlass()
        {
            WaterGlasses = Math.Clamp(WaterGlasses - 1, 0, 30);
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void ResetWaterGlasses()
        {
            WaterGlasses = 0;
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void AdjustWaterTarget(int delta)
        {
            WaterTarget = Math.Clamp(WaterTarget + delta, 4, 20);
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void ToggleWaterReminder()
        {
            LastActiveMode = TimerMode.Water;
            WaterReminderEnabled = !WaterReminderEnabled;
            if (WaterReminderEnabled)
            {
                WaterReminderRemainingSeconds = WaterReminderIntervalMinutes * 60;
                _waterPreciseRemaining = WaterReminderRemainingSeconds;
            }
            SavePrefs();
            StateChanged?.Invoke();
        }

        public void SetWaterReminderInterval(int minutes)
        {
            WaterReminderIntervalMinutes = Math.Clamp(minutes, 15, 180);
            WaterReminderRemainingSeconds = WaterReminderIntervalMinutes * 60;
            _waterPreciseRemaining = WaterReminderRemainingSeconds;
            SavePrefs();
            StateChanged?.Invoke();
        }

        #endregion
    }
}
