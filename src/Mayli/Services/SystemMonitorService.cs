using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using WindowsBuddy.Core;

namespace WindowsBuddy.Services
{
    public sealed class SystemTelemetryData
    {
        public int BatteryPercent { get; set; } = 100;
        public bool IsCharging { get; set; } = true;
        public bool HasBattery { get; set; } = true;
        public int CpuUsagePercent { get; set; }
        public int MemoryUsagePercent { get; set; }
        public double MemoryUsedGb { get; set; }
        public double MemoryTotalGb { get; set; }
        public int DiskUsagePercent { get; set; }
        public int DiskStoragePercent { get; set; }
        public double DiskUsedGb { get; set; }
        public double DiskTotalGb { get; set; }
        public bool NetworkConnected { get; set; } = true;
        public string NetworkName { get; set; } = "Connected";
        public int MasterVolumePercent { get; set; } = 68;
        public bool IsMuted { get; set; } = false;
    }

    public static class SystemMonitorService
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out ulong lpIdleTime, out ulong lpKernelTime, out ulong lpUserTime);

        private static readonly DispatcherTimer _timer;
        private static ulong _lastIdleTime;
        private static ulong _lastKernelTime;
        private static ulong _lastUserTime;
        private static int _lastCpuPercent = 8;
        private static int _cachedDiskStoragePercent = 45;
        private static double _cachedDiskUsedGb = 0;
        private static double _cachedDiskTotalGb = 0;
        private static PerformanceCounter _diskCounter;
        private static bool _diskCounterInitialized;
        private static int _lastDiskActivityPercent = 0;
        private static DateTime _lastDiskCheckUtc = DateTime.MinValue;
        private static DateTime _lastSampleUtc = DateTime.MinValue;

        public static SystemTelemetryData Current { get; private set; }
        public static event Action<SystemTelemetryData> TelemetryChanged;

        static SystemMonitorService()
        {
            if (GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
            {
                _lastIdleTime = idle;
                _lastKernelTime = kernel;
                _lastUserTime = user;
            }

            Current = SampleTelemetryInternal(forceCpuSample: true);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
            _timer.Tick += (_, _) =>
            {
                bool hasSubscribers = TelemetryChanged != null;
                var desiredInterval = hasSubscribers ? TimeSpan.FromSeconds(1.0) : TimeSpan.FromSeconds(3.5);
                if (_timer.Interval != desiredInterval) _timer.Interval = desiredInterval;

                Current = SampleTelemetryInternal(forceCpuSample: true);
                TelemetryChanged?.Invoke(Current);
            };
            _timer.Start();
        }

        /// <summary>
        /// Returns the unified live telemetry snapshot so Home widget and System tab always display identical values.
        /// </summary>
        public static SystemTelemetryData FetchLiveTelemetry()
        {
            var now = DateTime.UtcNow;
            if (Current != null && (now - _lastSampleUtc).TotalMilliseconds < 450)
            {
                return Current;
            }

            Current = SampleTelemetryInternal(forceCpuSample: (now - _lastSampleUtc).TotalMilliseconds >= 450);
            return Current;
        }

        private static void EnsureDiskCounterAsync()
        {
            if (_diskCounterInitialized) return;
            _diskCounterInitialized = true;
            Task.Run(() =>
            {
                try
                {
                    var pc = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
                    pc.NextValue();
                    _diskCounter = pc;
                }
                catch
                {
                    _diskCounter = null;
                }
            });
        }

        private static SystemTelemetryData SampleTelemetryInternal(bool forceCpuSample)
        {
            var now = DateTime.UtcNow;
            var data = new SystemTelemetryData();

            // 1. Battery
            if (NativeMethods.GetSystemPowerStatus(out var sps))
            {
                data.HasBattery = DeviceService.HasBattery;
                data.IsCharging = sps.ACLineStatus == 1 && data.HasBattery;
                data.BatteryPercent = (data.HasBattery && sps.BatteryLifePercent <= 100) ? sps.BatteryLifePercent : 100;
            }

            // 2. RAM (GlobalMemoryStatusEx)
            var mem = new NativeMethods.MEMORYSTATUSEX();
            if (NativeMethods.GlobalMemoryStatusEx(mem))
            {
                data.MemoryUsagePercent = (int)Math.Clamp(mem.dwMemoryLoad, 1, 100);
                data.MemoryTotalGb = Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 1);
                data.MemoryUsedGb = Math.Round((mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0), 1);
            }

            // 3. System-wide CPU (GetSystemTimes)
            if (forceCpuSample && GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
            {
                ulong idleDiff = idle >= _lastIdleTime ? (idle - _lastIdleTime) : 0;
                ulong kernelDiff = kernel >= _lastKernelTime ? (kernel - _lastKernelTime) : 0;
                ulong userDiff = user >= _lastUserTime ? (user - _lastUserTime) : 0;
                ulong totalDiff = kernelDiff + userDiff;

                if (totalDiff > 0)
                {
                    double busyRatio = 1.0 - ((double)idleDiff / totalDiff);
                    _lastCpuPercent = (int)Math.Clamp(Math.Round(busyRatio * 100.0), 1, 100);
                }

                _lastIdleTime = idle;
                _lastKernelTime = kernel;
                _lastUserTime = user;
                _lastSampleUtc = now;
            }
            data.CpuUsagePercent = _lastCpuPercent;

            // 4. Real System Drive Storage & Real-time Disk Activity
            if ((now - _lastDiskCheckUtc).TotalSeconds >= 10.0 || _cachedDiskTotalGb <= 0)
            {
                _lastDiskCheckUtc = now;
                try
                {
                    string sysRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                    var drive = new DriveInfo(sysRoot);
                    if (drive.IsReady && drive.TotalSize > 0)
                    {
                        double usedRatio = (double)(drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize;
                        _cachedDiskStoragePercent = (int)Math.Clamp(Math.Round(usedRatio * 100.0), 1, 100);
                        _cachedDiskUsedGb = Math.Round((drive.TotalSize - drive.TotalFreeSpace) / (1024.0 * 1024.0 * 1024.0), 1);
                        _cachedDiskTotalGb = Math.Round(drive.TotalSize / (1024.0 * 1024.0 * 1024.0), 1);
                    }
                }
                catch { }
            }
            data.DiskStoragePercent = _cachedDiskStoragePercent;
            data.DiskUsedGb = _cachedDiskUsedGb;
            data.DiskTotalGb = _cachedDiskTotalGb;

            int liveDiskActivity = 0;
            EnsureDiskCounterAsync();

            if (_diskCounter != null)
            {
                try
                {
                    float raw = _diskCounter.NextValue();
                    liveDiskActivity = (int)Math.Clamp(Math.Round(raw), 0, 100);
                    _lastDiskActivityPercent = liveDiskActivity;
                }
                catch
                {
                    liveDiskActivity = _lastDiskActivityPercent;
                }
            }
            else
            {
                liveDiskActivity = Math.Max(0, (_lastCpuPercent / 5));
            }

            data.DiskUsagePercent = liveDiskActivity;

            // 5. Network & Audio
            data.NetworkConnected = NetworkInterface.GetIsNetworkAvailable();
            data.NetworkName = data.NetworkConnected ? "Online" : "Offline";
            data.MasterVolumePercent = CoreAudioService.Instance.VolumePercent;
            data.IsMuted = CoreAudioService.Instance.IsMuted;

            return data;
        }
    }

    public sealed class MediaTrackState
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public bool IsPlaying { get; set; } = false;
        public double PositionSeconds { get; set; } = 0;
        public double DurationSeconds { get; set; } = 0;
    }

    public sealed class MediaService
    {
        public static MediaService Instance { get; } = new MediaService();

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

        private readonly DispatcherTimer _progressTimer;
        public MediaTrackState State { get; } = new MediaTrackState();
        public event Action StateChanged;

        private MediaService()
        {
            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _progressTimer.Tick += (_, _) =>
            {
                if (State.IsPlaying && !string.IsNullOrWhiteSpace(State.Title) && State.DurationSeconds > 0)
                {
                    State.PositionSeconds = Math.Min(State.DurationSeconds, State.PositionSeconds + 1);
                    StateChanged?.Invoke();
                }
            };
            _progressTimer.Start();
        }

        public void TogglePlayPause()
        {
            bool hasSession = !string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack) ||
                              !string.IsNullOrWhiteSpace(State.Title);
            if (!hasSession)
            {
                State.IsPlaying = false;
                StateChanged?.Invoke();
                return;
            }
            try
            {
                var mgr = RealtimeNotchActionService.Instance.GsmtcManager;
                var session = mgr?.GetCurrentSession();
                if (session != null)
                {
                    Task.Run(async () =>
                    {
                        await session.TryTogglePlayPauseAsync();
                        RealtimeNotchActionService.Instance.TriggerImmediateMediaPoll();
                    });
                    State.IsPlaying = !State.IsPlaying;
                    StateChanged?.Invoke();
                    return;
                }
            }
            catch { }
            NativeMethods.keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, UIntPtr.Zero);
            State.IsPlaying = !State.IsPlaying;
            StateChanged?.Invoke();
        }

        public void NextTrack()
        {
            bool hasSession = !string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack) ||
                              !string.IsNullOrWhiteSpace(State.Title);
            if (!hasSession) return;
            try
            {
                var mgr = RealtimeNotchActionService.Instance.GsmtcManager;
                var session = mgr?.GetCurrentSession();
                if (session != null)
                {
                    Task.Run(async () =>
                    {
                        await session.TrySkipNextAsync();
                        RealtimeNotchActionService.Instance.TriggerImmediateMediaPoll();
                    });
                    State.PositionSeconds = 0;
                    StateChanged?.Invoke();
                    return;
                }
            }
            catch { }
            NativeMethods.keybd_event(VK_MEDIA_NEXT_TRACK, 0, 0, UIntPtr.Zero);
            State.PositionSeconds = 0;
            StateChanged?.Invoke();
        }

        public void PreviousTrack()
        {
            bool hasSession = !string.IsNullOrWhiteSpace(RealtimeNotchActionService.Instance.CurrentTrack) ||
                              !string.IsNullOrWhiteSpace(State.Title);
            if (!hasSession) return;
            try
            {
                var mgr = RealtimeNotchActionService.Instance.GsmtcManager;
                var session = mgr?.GetCurrentSession();
                if (session != null)
                {
                    Task.Run(async () =>
                    {
                        await session.TrySkipPreviousAsync();
                        RealtimeNotchActionService.Instance.TriggerImmediateMediaPoll();
                    });
                    State.PositionSeconds = 0;
                    StateChanged?.Invoke();
                    return;
                }
            }
            catch { }
            NativeMethods.keybd_event(VK_MEDIA_PREV_TRACK, 0, 0, UIntPtr.Zero);
            State.PositionSeconds = 0;
            StateChanged?.Invoke();
        }
    }
}
