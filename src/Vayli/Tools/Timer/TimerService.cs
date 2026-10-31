using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace WindowsBuddy.Tools.Timer
{
    public enum TimerMode
    {
        Pomodoro,
        Countdown,
        Stopwatch,
        Water
    }

    public sealed class TimerService
    {
        private static readonly Lazy<TimerService> _lazy = new(() => new TimerService());
        public static TimerService Instance => _lazy.Value;

        private readonly DispatcherTimer _clock;
        private readonly Stopwatch _stopwatch = new();

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

        // Countdown State
        public int CountTotalSeconds { get; set; } = 15 * 60;
        public int CountRemainingSeconds { get; set; } = 15 * 60;
        public bool CountIsRunning { get; set; } = false;

        // Stopwatch State
        public bool StopwatchIsRunning => _stopwatch.IsRunning;
        public TimeSpan StopwatchElapsed => _stopwatch.Elapsed;

        // Water State
        public int WaterGlasses { get; set; } = 0;
        public int WaterTarget { get; set; } = 8;

        public event Action Tick;
        public event Action StateChanged;

        private TimerService()
        {
            _clock = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _clock.Tick += OnClockTick;
            _clock.Start();
        }

        private DateTime _lastSecondTick = DateTime.UtcNow;

        private void OnClockTick(object sender, EventArgs e)
        {
            bool secondElapsed = (DateTime.UtcNow - _lastSecondTick).TotalSeconds >= 1.0;
            if (secondElapsed)
            {
                _lastSecondTick = DateTime.UtcNow;

                if (PomoIsRunning)
                {
                    if (PomoRemainingSeconds > 0)
                    {
                        PomoRemainingSeconds--;
                    }
                    else
                    {
                        OnPomoPhaseCompleted();
                    }
                }

                if (CountIsRunning)
                {
                    if (CountRemainingSeconds > 0)
                    {
                        CountRemainingSeconds--;
                    }
                    else
                    {
                        CountIsRunning = false;
                    }
                }
            }

            Tick?.Invoke();
        }

        private void OnPomoPhaseCompleted()
        {
            if (PomoIsWork)
            {
                PomoSessionsToday++;
                if (PomoCurrentCycle >= PomoTargetCycles)
                {
                    // Long break
                    PomoIsWork = false;
                    PomoRemainingSeconds = PomoLongBreakSeconds;
                }
                else
                {
                    // Short break
                    PomoIsWork = false;
                    PomoRemainingSeconds = PomoBreakSeconds;
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
            }

            if (!PomoAutoStartNext)
            {
                PomoIsRunning = false;
            }

            StateChanged?.Invoke();
        }

        public void TogglePomo()
        {
            PomoIsRunning = !PomoIsRunning;
            StateChanged?.Invoke();
        }

        public void ResetPomo()
        {
            PomoIsRunning = false;
            PomoIsWork = true;
            PomoRemainingSeconds = PomoWorkSeconds;
            StateChanged?.Invoke();
        }

        public void SkipPomo()
        {
            OnPomoPhaseCompleted();
        }

        public void AdjustPomoWork(int deltaMinutes)
        {
            int mins = Math.Clamp((PomoWorkSeconds / 60) + deltaMinutes, 5, 90);
            PomoWorkSeconds = mins * 60;
            if (PomoIsWork && !PomoIsRunning)
            {
                PomoRemainingSeconds = PomoWorkSeconds;
            }
            StateChanged?.Invoke();
        }

        public void AdjustPomoBreak(int deltaMinutes)
        {
            int mins = Math.Clamp((PomoBreakSeconds / 60) + deltaMinutes, 1, 30);
            PomoBreakSeconds = mins * 60;
            if (!PomoIsWork && !PomoIsRunning)
            {
                PomoRemainingSeconds = PomoBreakSeconds;
            }
            StateChanged?.Invoke();
        }

        public void AdjustPomoLongBreak(int deltaMinutes)
        {
            int mins = Math.Clamp((PomoLongBreakSeconds / 60) + deltaMinutes, 5, 60);
            PomoLongBreakSeconds = mins * 60;
            StateChanged?.Invoke();
        }

        public void AdjustPomoCycles(int delta)
        {
            PomoTargetCycles = Math.Clamp(PomoTargetCycles + delta, 1, 12);
            StateChanged?.Invoke();
        }

        public void ToggleStopwatch()
        {
            if (_stopwatch.IsRunning) _stopwatch.Stop();
            else _stopwatch.Start();
            StateChanged?.Invoke();
        }

        public void ResetStopwatch()
        {
            _stopwatch.Reset();
            StateChanged?.Invoke();
        }

        public void AddWaterGlass()
        {
            WaterGlasses = Math.Clamp(WaterGlasses + 1, 0, 20);
            StateChanged?.Invoke();
        }

        public void RemoveWaterGlass()
        {
            WaterGlasses = Math.Clamp(WaterGlasses - 1, 0, 20);
            StateChanged?.Invoke();
        }
    }
}
