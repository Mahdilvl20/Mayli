using System;
using System.Diagnostics;
using System.Windows.Threading;
using WindowsBuddy.Core;
using WindowsBuddy.Services;

namespace WindowsBuddy.BuddyCapsule.Core
{
    public enum NotchPhase
    {
        Hidden = 0,
        Resting = 1,
        Peeking = 2,
        Opening = 3,
        Open = 4,
        Closing = 5,
        Transient = 6
    }

    public enum NotchPresence
    {
        Full = 0,
        Line = 1,
        None = 2
    }

    public enum NotchVisibilityMode
    {
        Desktop = 0,
        Smart = 1,
        AlwaysVisible = 2,
        Minimal = 3,
        Hidden = 4
    }

    public enum ActivityType
    {
        RestingClock = 0,
        Media = 1,
        BluetoothConnected = 2,
        BluetoothDisconnected = 3,
        Volume = 4,
        BatteryCharging = 5,
        BatteryChargingStopped = 6,
        BatteryFull = 7,
        BatteryLow = 8,
        ClipboardCopied = 9,
        TimerRunning = 10,
        TimerComplete = 11,
        HydrationReminder = 12,
        NetworkState = 13,
        NetworkConnected = 14,
        NetworkDisconnected = 15,
        CompanionMessage = 16,
        CalendarAlert = 17,
        Clock = 18,
        Custom = 19,
        ScreenTimeLimit = 20
    }

    public sealed class NotchStateMachine
    {
        public const int InconsistencyResetMs = 2000;
        public const int CloseGraceMs = 150;
        public const int PollActiveMs = 40;
        public const int PollRestingMs = 250;
        public const int WatchdogActiveMs = 250;
        public const int WatchdogIdleMs = 500;
        public const int TransitionLogSize = 40;
        public const int ContentReadyMs = 500;
        public const int TransientGraceMs = 2000;

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly DispatcherTimer _dwellTimer;
        private readonly DispatcherTimer _graceTimer;
        private long _phaseSinceMs;

        public NotchPhase Phase { get; private set; } = NotchPhase.Resting;
        public NotchPresence Presence { get; private set; } = NotchPresence.Full;
        public bool PointerInside { get; private set; }
        public int HoverDelayMs { get; set; } = 25;
        public bool OpensOnClick { get; set; } = false;
        public Func<bool> HoldOpen { get; set; }
        public int TransitionCount { get; private set; }

        public event Action<NotchPhase, NotchPhase, string> PhaseChanged;

        public NotchStateMachine()
        {
            HoverDelayMs = Math.Clamp(AppPreferences.Instance.HoverDelayMs, 15, 45);
            OpensOnClick = AppPreferences.Instance.OpensOnClick;

            _dwellTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(HoverDelayMs) };
            _dwellTimer.Tick += (_, _) =>
            {
                _dwellTimer.Stop();
                if (PointerInside && !OpensOnClick && (Phase == NotchPhase.Resting || Phase == NotchPhase.Peeking || Phase == NotchPhase.Transient || Phase == NotchPhase.Closing))
                {
                    RequestOpen("hover-dwell");
                }
            };

            _graceTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(CloseGraceMs) };
            _graceTimer.Tick += (_, _) =>
            {
                _graceTimer.Stop();
                if (!PointerInside && !(HoldOpen?.Invoke() ?? false))
                {
                    BeginClosing("pointer-leave-grace");
                }
            };
        }

        public bool IsOpenOrOpening => Phase == NotchPhase.Open || Phase == NotchPhase.Opening;
        public bool IsResting => Phase == NotchPhase.Resting;
        public long MsInPhase => _clock.ElapsedMilliseconds - _phaseSinceMs;

        public void PointerMoved(bool isInside, string reason)
        {
            if (PointerInside == isInside) return;
            PointerInside = isInside;

            if (isInside)
            {
                _graceTimer.Stop();
                if (!OpensOnClick && !IsOpenOrOpening)
                {
                    _dwellTimer.Interval = TimeSpan.FromMilliseconds(Math.Clamp(AppPreferences.Instance.HoverDelayMs, 15, 40));
                    _dwellTimer.Start();
                }
            }
            else
            {
                _dwellTimer.Stop();
                if (IsOpenOrOpening && !(HoldOpen?.Invoke() ?? false))
                {
                    _graceTimer.Start();
                }
            }
        }

        /// <summary>
        /// Called when a hold (popup / dialog) is released: if the pointer is no longer over the notch,
        /// start the normal close grace period.
        /// </summary>
        public void ReevaluateHold()
        {
            if (!PointerInside && IsOpenOrOpening && !(HoldOpen?.Invoke() ?? false))
            {
                _graceTimer.Stop();
                _graceTimer.Start();
            }
        }

        public void Click(string reason)
        {
            if (!IsOpenOrOpening)
                RequestOpen(reason);
        }

        public void Toggle(string reason)
        {
            if (IsOpenOrOpening)
                RequestClose(reason);
            else
                RequestOpen(reason);
        }

        public void RequestOpen(string reason)
        {
            _dwellTimer.Stop();
            _graceTimer.Stop();
            if (Phase == NotchPhase.Open) return;
            Enter(NotchPhase.Opening, reason);
            Enter(NotchPhase.Open, reason + ":settled");
        }

        public void RequestClose(string reason)
        {
            _dwellTimer.Stop();
            _graceTimer.Stop();
            if (Phase == NotchPhase.Resting) return;
            BeginClosing(reason);
        }

        private void BeginClosing(string reason)
        {
            Enter(NotchPhase.Closing, reason);
            Enter(NotchPhase.Resting, reason + ":settled");
        }

        private void Enter(NotchPhase next, string reason)
        {
            if (Phase == next) return;
            var prev = Phase;
            Phase = next;
            _phaseSinceMs = _clock.ElapsedMilliseconds;
            TransitionCount++;
            PhaseChanged?.Invoke(prev, next, reason);
        }
    }
}
