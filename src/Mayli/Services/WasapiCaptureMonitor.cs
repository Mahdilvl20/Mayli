using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Monitors active WASAPI audio capture sessions on Windows capture (microphone) endpoints
    /// so apps bypassing or delaying CapabilityAccessManager ConsentStore registry updates
    /// are detected instantaneously alongside registry tracking.
    /// </summary>
    internal sealed class WasapiCaptureMonitor : IDisposable
    {
        private const int eCapture = 1;
        private const uint DEVICE_STATE_ACTIVE = 0x00000001;
        private const int AudioSessionStateActive = 1;
        private const int CLSCTX_ALL = 23;

        private readonly object _lock = new();
        private readonly List<(IAudioSessionManager2 Mgr, SessionNotificationSink Sink)> _registeredManagers = new();
        private readonly List<(IAudioSessionControl2 Ctrl, SessionEventsSink Sink)> _registeredControls = new();
        private IMMDeviceEnumerator _enumerator;
        private DeviceNotificationSink _deviceSink;
        private bool _disposed;

        public event Action CaptureSessionsChanged;

        private static readonly Guid MMDeviceEnumeratorClsid = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

        private static IMMDeviceEnumerator CreateEnumerator()
        {
            return (IMMDeviceEnumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(MMDeviceEnumeratorClsid));
        }

        public void StartListening()
        {
            lock (_lock)
            {
                if (_disposed) return;
                try
                {
                    if (_enumerator == null)
                    {
                        _enumerator = CreateEnumerator();
                        _deviceSink = new DeviceNotificationSink(this);
                        _enumerator.RegisterEndpointNotificationCallback(_deviceSink);
                    }
                    RefreshSubscriptionsLocked();
                }
                catch
                {
                    // Audio service unavailable or COM failure on headless/CI systems
                }
            }
        }

        public IReadOnlyList<string> ScanActiveCaptureApps()
        {
            var results = new List<string>();
            var seenPids = new HashSet<uint>();
            int selfPid = Environment.ProcessId;

            try
            {
                IMMDeviceEnumerator enumerator;
                lock (_lock)
                {
                    if (_disposed) return results;
                    _enumerator ??= CreateEnumerator();
                    enumerator = _enumerator;
                }

                if (enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, out var collection) != 0 || collection == null)
                {
                    return results;
                }

                collection.GetCount(out uint devCount);
                Guid smGuid = typeof(IAudioSessionManager2).GUID;

                for (uint d = 0; d < devCount; d++)
                {
                    try
                    {
                        if (collection.Item(d, out var device) != 0 || device == null) continue;
                        if (device.Activate(ref smGuid, CLSCTX_ALL, IntPtr.Zero, out object smObj) != 0) continue;
                        if (smObj is not IAudioSessionManager2 sm) continue;

                        if (sm.GetSessionEnumerator(out var sessionEnum) != 0 || sessionEnum == null) continue;
                        sessionEnum.GetCount(out int count);

                        for (int i = 0; i < count; i++)
                        {
                            try
                            {
                                if (sessionEnum.GetSession(i, out object ctrlObj) != 0) continue;
                                if (ctrlObj is not IAudioSessionControl2 ctrl2) continue;

                                EnsureControlSubscribed(ctrl2);

                                if (ctrl2.GetState(out int state) != 0 || state != AudioSessionStateActive) continue;
                                if (ctrl2.GetProcessId(out uint pid) != 0 || pid == 0 || pid == selfPid) continue;
                                if (!seenPids.Add(pid)) continue;

                                if (IsIgnoredSystemAudioProcess((int)pid)) continue;

                                string appName = ProcessResolver.ResolveProcessIdName((int)pid);
                                if (!string.IsNullOrWhiteSpace(appName))
                                {
                                    results.Add(appName);
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return results;
        }

        private void RefreshSubscriptionsLocked()
        {
            ClearSessionSubscriptionsLocked();
            if (_enumerator == null || _disposed) return;

            try
            {
                if (_enumerator.EnumAudioEndpoints(eCapture, DEVICE_STATE_ACTIVE, out var collection) != 0 || collection == null)
                    return;

                collection.GetCount(out uint devCount);
                Guid smGuid = typeof(IAudioSessionManager2).GUID;

                for (uint d = 0; d < devCount; d++)
                {
                    try
                    {
                        if (collection.Item(d, out var device) != 0 || device == null) continue;
                        if (device.Activate(ref smGuid, CLSCTX_ALL, IntPtr.Zero, out object smObj) != 0) continue;
                        if (smObj is not IAudioSessionManager2 sm) continue;

                        var sink = new SessionNotificationSink(this);
                        if (sm.RegisterSessionNotification(sink) == 0)
                        {
                            _registeredManagers.Add((sm, sink));
                        }

                        if (sm.GetSessionEnumerator(out var sessionEnum) == 0 && sessionEnum != null)
                        {
                            sessionEnum.GetCount(out int count);
                            for (int i = 0; i < count; i++)
                            {
                                if (sessionEnum.GetSession(i, out object ctrlObj) == 0 && ctrlObj is IAudioSessionControl2 c2)
                                {
                                    EnsureControlSubscribed(c2);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void EnsureControlSubscribed(IAudioSessionControl2 ctrl2)
        {
            lock (_lock)
            {
                if (_disposed) return;
                // Limit tracked callbacks to prevent unbounded growth across long sessions
                if (_registeredControls.Count > 64)
                {
                    ClearDeadControlsLocked();
                }

                string id = null;
                try { ctrl2.GetSessionInstanceIdentifier(out id); } catch { }
                if (!string.IsNullOrEmpty(id))
                {
                    foreach (var existing in _registeredControls)
                    {
                        try
                        {
                            if (existing.Ctrl.GetSessionInstanceIdentifier(out string existingId) == 0 &&
                                string.Equals(existingId, id, StringComparison.Ordinal))
                            {
                                return;
                            }
                        }
                        catch { }
                    }
                }

                var evSink = new SessionEventsSink(this);
                try
                {
                    if (ctrl2.RegisterAudioSessionNotification(evSink) == 0)
                    {
                        _registeredControls.Add((ctrl2, evSink));
                    }
                }
                catch { }
            }
        }

        private void ClearDeadControlsLocked()
        {
            for (int i = _registeredControls.Count - 1; i >= 0; i--)
            {
                var (ctrl, sink) = _registeredControls[i];
                try
                {
                    if (ctrl.GetState(out int state) != 0 || state == 2 /* Expired */)
                    {
                        try { ctrl.UnregisterAudioSessionNotification(sink); } catch { }
                        _registeredControls.RemoveAt(i);
                    }
                }
                catch
                {
                    _registeredControls.RemoveAt(i);
                }
            }
        }

        private void ClearSessionSubscriptionsLocked()
        {
            foreach (var (ctrl, sink) in _registeredControls)
            {
                try { ctrl.UnregisterAudioSessionNotification(sink); } catch { }
            }
            _registeredControls.Clear();

            foreach (var (mgr, sink) in _registeredManagers)
            {
                try { mgr.UnregisterSessionNotification(sink); } catch { }
            }
            _registeredManagers.Clear();
        }

        private void OnDeviceTopologyChanged()
        {
            lock (_lock)
            {
                if (_disposed) return;
                RefreshSubscriptionsLocked();
            }
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            if (_disposed) return;
            try { CaptureSessionsChanged?.Invoke(); } catch { }
        }

        private static bool IsIgnoredSystemAudioProcess(int pid)
        {
            try
            {
                using var proc = Process.GetProcessById(pid);
                string name = proc.ProcessName;
                return string.Equals(name, "audiodg", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return true;
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                ClearSessionSubscriptionsLocked();
                if (_enumerator != null && _deviceSink != null)
                {
                    try { _enumerator.UnregisterEndpointNotificationCallback(_deviceSink); } catch { }
                }
                _deviceSink = null;
                _enumerator = null;
            }
        }

        private sealed class DeviceNotificationSink : IMMNotificationClient
        {
            private readonly WasapiCaptureMonitor _owner;
            public DeviceNotificationSink(WasapiCaptureMonitor owner) => _owner = owner;
            public int OnDeviceStateChanged(string pwstrDeviceId, uint dwNewState) { _owner.OnDeviceTopologyChanged(); return 0; }
            public int OnDeviceAdded(string pwstrDeviceId) { _owner.OnDeviceTopologyChanged(); return 0; }
            public int OnDeviceRemoved(string pwstrDeviceId) { _owner.OnDeviceTopologyChanged(); return 0; }
            public int OnDefaultDeviceChanged(int flow, int role, string pwstrDefaultDeviceId)
            {
                if (flow == eCapture) _owner.OnDeviceTopologyChanged();
                return 0;
            }
            public int OnPropertyValueChanged(string pwstrDeviceId, PROPERTYKEY key) => 0;
        }

        private sealed class SessionNotificationSink : IAudioSessionNotification
        {
            private readonly WasapiCaptureMonitor _owner;
            public SessionNotificationSink(WasapiCaptureMonitor owner) => _owner = owner;
            public int OnSessionCreated(object newSession)
            {
                if (newSession is IAudioSessionControl2 c2)
                {
                    _owner.EnsureControlSubscribed(c2);
                }
                _owner.RaiseChanged();
                return 0;
            }
        }

        private sealed class SessionEventsSink : IAudioSessionEvents
        {
            private readonly WasapiCaptureMonitor _owner;
            public SessionEventsSink(WasapiCaptureMonitor owner) => _owner = owner;
            public int OnDisplayNameChanged(string newDisplayName, ref Guid eventContext) => 0;
            public int OnIconPathChanged(string newIconPath, ref Guid eventContext) => 0;
            public int OnSimpleVolumeChanged(float newVolume, bool newMute, ref Guid eventContext) => 0;
            public int OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, ref Guid eventContext) => 0;
            public int OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext) => 0;
            public int OnStateChanged(int newState) { _owner.RaiseChanged(); return 0; }
            public int OnSessionDisconnected(int disconnectReason) { _owner.RaiseChanged(); return 0; }
        }

        #region COM Interop

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY { public Guid fmtid; public uint pid; }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection ppDevices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
            [PreserveSig] int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient pClient);
            [PreserveSig] int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient pClient);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint pcDevices);
            [PreserveSig] int Item(uint nDevice, out IMMDevice ppDevice);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [ComImport, Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMNotificationClient
        {
            [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, uint dwNewState);
            [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
            [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
            [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
            [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr audioSessionGuid, uint streamFlags, out IntPtr sessionControl);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr audioSessionGuid, uint streamFlags, out IntPtr audioVolume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
            [PreserveSig] int RegisterSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionNotification sessionNotification);
            [PreserveSig] int UnregisterSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionNotification sessionNotification);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int sessionCount);
            [PreserveSig] int GetSession(int sessionIndex, [MarshalAs(UnmanagedType.IUnknown)] out object session);
        }

        [ComImport, Guid("641DD20B-4D41-49CC-ABAE-17D2B5E42312"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionNotification
        {
            [PreserveSig] int OnSessionCreated([MarshalAs(UnmanagedType.IUnknown)] object newSession);
        }

        [ComImport, Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEvents
        {
            [PreserveSig] int OnDisplayNameChanged([MarshalAs(UnmanagedType.LPWStr)] string newDisplayName, ref Guid eventContext);
            [PreserveSig] int OnIconPathChanged([MarshalAs(UnmanagedType.LPWStr)] string newIconPath, ref Guid eventContext);
            [PreserveSig] int OnSimpleVolumeChanged(float newVolume, [MarshalAs(UnmanagedType.Bool)] bool newMute, ref Guid eventContext);
            [PreserveSig] int OnChannelVolumeChanged(uint channelCount, IntPtr newChannelVolumeArray, uint changedChannel, ref Guid eventContext);
            [PreserveSig] int OnGroupingParamChanged(ref Guid newGroupingParam, ref Guid eventContext);
            [PreserveSig] int OnStateChanged(int newState);
            [PreserveSig] int OnSessionDisconnected(int disconnectReason);
        }

        [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int pRetVal);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
            [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
            [PreserveSig] int GetGroupingParam(out Guid pRetVal);
            [PreserveSig] int SetGroupingParam(ref Guid @override, ref Guid eventContext);
            [PreserveSig] int RegisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents newNotifications);
            [PreserveSig] int UnregisterAudioSessionNotification([MarshalAs(UnmanagedType.Interface)] IAudioSessionEvents newNotifications);
            [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
            [PreserveSig] int GetProcessId(out uint pRetVal);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
        }

        #endregion
    }
}
