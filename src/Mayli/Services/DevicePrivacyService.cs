using System;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Thread-safe service for detecting live microphone and camera access across the system.
    /// Manages real-time registry monitoring via RegistryAccessMonitor, maintains current device states,
    /// dispatches DeviceAccessChanged events, and supports non-elevated mock simulation for automated testing.
    /// </summary>
    public sealed class DevicePrivacyService : IDevicePrivacyService
    {
        private static readonly Lazy<DevicePrivacyService> _lazy = new(() => new DevicePrivacyService());
        public static DevicePrivacyService Instance => _lazy.Value;

        private readonly object _lock = new();
        private readonly RegistryAccessMonitor _monitor;
        private readonly bool _ownsMonitor;

        private bool _isMicInUse;
        private string _micAppName = string.Empty;
        private bool _isCamInUse;
        private string _camAppName = string.Empty;

        // Mock state support for automated testing
        private bool _isMockActive;
        private bool? _mockMicInUse;
        private string _mockMicAppName;
        private bool? _mockCamInUse;
        private string _mockCamAppName;

        private bool _disposed;

        public bool IsMicrophoneInUse
        {
            get
            {
                lock (_lock)
                {
                    return _isMockActive && _mockMicInUse.HasValue ? _mockMicInUse.Value : _isMicInUse;
                }
            }
        }

        public string MicrophoneAppName
        {
            get
            {
                lock (_lock)
                {
                    if (_isMockActive && _mockMicAppName != null)
                        return _mockMicAppName;
                    return _micAppName;
                }
            }
        }

        public bool IsCameraInUse
        {
            get
            {
                lock (_lock)
                {
                    return _isMockActive && _mockCamInUse.HasValue ? _mockCamInUse.Value : _isCamInUse;
                }
            }
        }

        public string CameraAppName
        {
            get
            {
                lock (_lock)
                {
                    if (_isMockActive && _mockCamAppName != null)
                        return _mockCamAppName;
                    return _camAppName;
                }
            }
        }

        public bool IsMockActive
        {
            get
            {
                lock (_lock)
                {
                    return _isMockActive;
                }
            }
        }

        public event EventHandler<DeviceAccessEventArgs> DeviceAccessChanged;

        /// <summary>
        /// Creates a new DevicePrivacyService.
        /// An optional custom RegistryAccessMonitor can be supplied for unit testing.
        /// </summary>
        public DevicePrivacyService(RegistryAccessMonitor monitor = null)
        {
            if (monitor != null)
            {
                _monitor = monitor;
                _ownsMonitor = false;
            }
            else
            {
                _monitor = new RegistryAccessMonitor();
                _ownsMonitor = true;
            }

            _monitor.AccessChanged += OnRegistryAccessChanged;

            // Perform initial snapshot
            var initialSnapshot = _monitor.ScanRegistry();
            ApplySnapshot(initialSnapshot, forceNotify: false);
        }

        /// <summary>
        /// Forces an immediate refresh of device access states from the registry.
        /// </summary>
        public void Refresh()
        {
            _monitor.TriggerRefresh();
            var snapshot = _monitor.ScanRegistry();
            ApplySnapshot(snapshot, forceNotify: true);
        }

        /// <summary>
        /// Waits until the underlying registry monitor has armed its event listeners.
        /// Useful in automated test scripts to ensure synchronous coordination.
        /// </summary>
        public bool WaitForStarted(int timeoutMs = 3000)
        {
            return _monitor.WaitForStarted(timeoutMs);
        }

        /// <summary>
        /// Injects mock state for automated testing without requiring real hardware.
        /// </summary>
        public void SetMockState(bool? micInUse, string micAppName, bool? camInUse, string camAppName)
        {
            DeviceAccessEventArgs eventArgsToFire = null;

            lock (_lock)
            {
                bool prevEffectiveMic = IsMicrophoneInUse;
                string prevEffectiveMicName = MicrophoneAppName;
                bool prevEffectiveCam = IsCameraInUse;
                string prevEffectiveCamName = CameraAppName;

                if (micInUse.HasValue) _mockMicInUse = micInUse.Value;
                if (micAppName != null) _mockMicAppName = micAppName;
                if (camInUse.HasValue) _mockCamInUse = camInUse.Value;
                if (camAppName != null) _mockCamAppName = camAppName;

                _isMockActive = _mockMicInUse.HasValue || _mockCamInUse.HasValue ||
                                _mockMicAppName != null || _mockCamAppName != null;

                bool newEffectiveMic = IsMicrophoneInUse;
                string newEffectiveMicName = MicrophoneAppName;
                bool newEffectiveCam = IsCameraInUse;
                string newEffectiveCamName = CameraAppName;

                if (prevEffectiveMic != newEffectiveMic ||
                    !string.Equals(prevEffectiveMicName, newEffectiveMicName, StringComparison.Ordinal) ||
                    prevEffectiveCam != newEffectiveCam ||
                    !string.Equals(prevEffectiveCamName, newEffectiveCamName, StringComparison.Ordinal))
                {
                    eventArgsToFire = new DeviceAccessEventArgs
                    {
                        IsMicrophoneInUse = newEffectiveMic,
                        MicrophoneAppName = newEffectiveMicName,
                        IsCameraInUse = newEffectiveCam,
                        CameraAppName = newEffectiveCamName,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }

            if (eventArgsToFire != null)
            {
                DeviceAccessChanged?.Invoke(this, eventArgsToFire);
            }
        }

        /// <summary>
        /// Clears all mock state overrides and restores live registry monitoring.
        /// </summary>
        public void ClearMockState()
        {
            DeviceAccessEventArgs eventArgsToFire = null;

            lock (_lock)
            {
                bool prevEffectiveMic = IsMicrophoneInUse;
                string prevEffectiveMicName = MicrophoneAppName;
                bool prevEffectiveCam = IsCameraInUse;
                string prevEffectiveCamName = CameraAppName;

                _isMockActive = false;
                _mockMicInUse = null;
                _mockMicAppName = null;
                _mockCamCamInUse = null;
                _mockCamAppName = null;

                bool newEffectiveMic = _isMicInUse;
                string newEffectiveMicName = _micAppName;
                bool newEffectiveCam = _isCamInUse;
                string newEffectiveCamName = _camAppName;

                if (prevEffectiveMic != newEffectiveMic ||
                    !string.Equals(prevEffectiveMicName, newEffectiveMicName, StringComparison.Ordinal) ||
                    prevEffectiveCam != newEffectiveCam ||
                    !string.Equals(prevEffectiveCamName, newEffectiveCamName, StringComparison.Ordinal))
                {
                    eventArgsToFire = new DeviceAccessEventArgs
                    {
                        IsMicrophoneInUse = newEffectiveMic,
                        MicrophoneAppName = newEffectiveMicName,
                        IsCameraInUse = newEffectiveCam,
                        CameraAppName = newEffectiveCamName,
                        Timestamp = DateTime.UtcNow
                    };
                }
            }

            if (eventArgsToFire != null)
            {
                DeviceAccessChanged?.Invoke(this, eventArgsToFire);
            }
        }

        private bool? _mockCamCamInUse
        {
            get => _mockCamInUse;
            set => _mockCamInUse = value;
        }

        private void OnRegistryAccessChanged(RegistryAccessSnapshot snapshot)
        {
            ApplySnapshot(snapshot, forceNotify: false);
        }

        private void ApplySnapshot(RegistryAccessSnapshot snapshot, bool forceNotify)
        {
            if (snapshot == null) return;

            DeviceAccessEventArgs eventArgsToFire = null;

            lock (_lock)
            {
                bool prevEffectiveMic = IsMicrophoneInUse;
                string prevEffectiveMicName = MicrophoneAppName;
                bool prevEffectiveCam = IsCameraInUse;
                string prevEffectiveCamName = CameraAppName;

                _isMicInUse = snapshot.IsMicrophoneInUse;
                _micAppName = snapshot.MicrophoneAppName ?? string.Empty;
                _isCamInUse = snapshot.IsCameraInUse;
                _camAppName = snapshot.CameraAppName ?? string.Empty;

                bool newEffectiveMic = IsMicrophoneInUse;
                string newEffectiveMicName = MicrophoneAppName;
                bool newEffectiveCam = IsCameraInUse;
                string newEffectiveCamName = CameraAppName;

                if (forceNotify ||
                    prevEffectiveMic != newEffectiveMic ||
                    !string.Equals(prevEffectiveMicName, newEffectiveMicName, StringComparison.Ordinal) ||
                    prevEffectiveCam != newEffectiveCam ||
                    !string.Equals(prevEffectiveCamName, newEffectiveCamName, StringComparison.Ordinal))
                {
                    eventArgsToFire = new DeviceAccessEventArgs
                    {
                        IsMicrophoneInUse = newEffectiveMic,
                        MicrophoneAppName = newEffectiveMicName,
                        IsCameraInUse = newEffectiveCam,
                        CameraAppName = newEffectiveCamName,
                        Timestamp = snapshot.Timestamp
                    };
                }
            }

            if (eventArgsToFire != null)
            {
                try
                {
                    DeviceAccessChanged?.Invoke(this, eventArgsToFire);
                }
                catch
                {
                    // Prevent subscriber exceptions from crashing service
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _monitor.AccessChanged -= OnRegistryAccessChanged;
            if (_ownsMonitor)
            {
                _monitor.Dispose();
            }
        }
    }
}
