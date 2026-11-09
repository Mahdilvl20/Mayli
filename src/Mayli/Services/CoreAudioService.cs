using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WindowsBuddy.Services
{
    public sealed class CoreAudioService
    {
        private static readonly Lazy<CoreAudioService> _lazy = new(() => new CoreAudioService());
        public static CoreAudioService Instance => _lazy.Value;

        private static readonly Guid _selfEventContext = new("A48B73E1-9F42-4C8B-B114-92A76E123456");

        private readonly object _syncLock = new();
        private IMMDeviceEnumerator _enumerator;
        private IAudioEndpointVolume _endpointVolume;
        private IAudioEndpointVolumeCallback _volumeCallback;
        private IMMNotificationClient _deviceNotificationClient;
        private string _deviceName = "Speakers";
        private bool _isMuted;
        private int _volumePercent = 100;

        public event Action VolumeChanged;
        public event Action<int, bool, bool> VolumeNotification;

        public string CurrentDeviceName => _deviceName;
        public int VolumePercent => _volumePercent;
        public bool IsMuted => _isMuted;

        private CoreAudioService()
        {
            Initialize();
        }

        private void Initialize()
        {
            lock (_syncLock)
            {
                try
                {
                    if (_enumerator == null)
                    {
                        _enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                        _deviceNotificationClient ??= new DeviceNotificationClient(this);
                        try
                        {
                            _enumerator.RegisterEndpointNotificationCallback(_deviceNotificationClient);
                        }
                        catch { }
                    }

                    BindDefaultEndpointLocked();
                }
                catch (Exception ex)
                {
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"Initialize EXCEPTION: {ex}");
                    _deviceName = "Realtek(R) Audio";
                    _volumePercent = 100;
                    _isMuted = false;
                }
            }
        }

        private void BindDefaultEndpointLocked()
        {
            UnregisterEndpointCallbackLocked();

            IMMDevice device = null;
            // Try eConsole (0) first, fallback to eMultimedia (1)
            int hr = _enumerator.GetDefaultAudioEndpoint(0, 0, out device);
            if (hr != 0 || device == null)
            {
                hr = _enumerator.GetDefaultAudioEndpoint(0, 1, out device);
            }

            WindowsBuddy.Core.LogWriter.WriteNow("audio", $"GetDefaultAudioEndpoint hr=0x{hr:X8}, device null={device == null}");

            if (device != null)
            {
                // Query Device Friendly Name
                try
                {
                    if (device.OpenPropertyStore(0, out var store) == 0 && store != null)
                    {
                        var pkey = new PROPERTYKEY
                        {
                            fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                            pid = 14
                        };
                        if (store.GetValue(ref pkey, out var pv) == 0 && pv.pwszVal != IntPtr.Zero)
                        {
                            _deviceName = Marshal.PtrToStringUni(pv.pwszVal);
                        }
                    }
                }
                catch (Exception ex)
                {
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"PropStore error: {ex}");
                }

                var volumeGuid = typeof(IAudioEndpointVolume).GUID;
                int actHr = device.Activate(ref volumeGuid, 23, IntPtr.Zero, out var ep);
                WindowsBuddy.Core.LogWriter.WriteNow("audio", $"Activate hr=0x{actHr:X8}, ep type={(ep != null ? ep.GetType().FullName : "null")}");

                if (ep is IAudioEndpointVolume vol)
                {
                    _endpointVolume = vol;
                    _volumeCallback ??= new AudioVolumeCallback(this);
                    try
                    {
                        int regHr = _endpointVolume.RegisterControlChangeNotify(_volumeCallback);
                        WindowsBuddy.Core.LogWriter.WriteNow("audio", $"RegisterControlChangeNotify hr=0x{regHr:X8}");
                    }
                    catch (Exception ex)
                    {
                        WindowsBuddy.Core.LogWriter.WriteNow("audio", $"RegisterControlChangeNotify EXCEPTION: {ex}");
                    }

                    RefreshStateLocked();
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"Successfully initialized endpoint volume. CurrentVol={_volumePercent}%, Muted={_isMuted}, Dev='{_deviceName}'");
                }
                else
                {
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", "ep is NOT IAudioEndpointVolume!");
                }
            }
        }

        private void UnregisterEndpointCallbackLocked()
        {
            if (_endpointVolume != null && _volumeCallback != null)
            {
                try { _endpointVolume.UnregisterControlChangeNotify(_volumeCallback); }
                catch { }
            }
            _endpointVolume = null;
        }

        private void RefreshStateLocked()
        {
            if (_endpointVolume == null) return;
            try
            {
                if (_endpointVolume.GetMasterVolumeLevelScalar(out float level) == 0)
                {
                    _volumePercent = (int)Math.Clamp(Math.Round(level * 100.0), 0, 100);
                }
                if (_endpointVolume.GetMute(out bool mute) == 0)
                {
                    _isMuted = mute;
                }
            }
            catch { }
        }

        public void RefreshState()
        {
            lock (_syncLock)
            {
                if (_endpointVolume == null)
                {
                    Initialize();
                }
                RefreshStateLocked();
            }
        }

        public void SetVolumePercent(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            _volumePercent = percent;

            IAudioEndpointVolume ep;
            lock (_syncLock)
            {
                if (_endpointVolume == null)
                {
                    Initialize();
                }
                ep = _endpointVolume;
            }

            if (ep != null)
            {
                try
                {
                    float scalar = percent / 100.0f;
                    var ctx = _selfEventContext;
                    int hr = ep.SetMasterVolumeLevelScalar(scalar, ref ctx);
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"SetMasterVolumeLevelScalar({scalar:F2}) returned hr=0x{hr:X8}");
                    if (_isMuted && percent > 0)
                    {
                        ep.SetMute(false, ref ctx);
                        _isMuted = false;
                    }
                }
                catch (Exception ex)
                {
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"SetVolumePercent EXCEPTION: {ex}");
                }
            }
            else
            {
                WindowsBuddy.Core.LogWriter.WriteNow("audio", "SetVolumePercent called but _endpointVolume is NULL!");
            }

            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(_volumePercent, _isMuted, false);
        }

        public void ToggleMute()
        {
            IAudioEndpointVolume ep;
            lock (_syncLock)
            {
                if (_endpointVolume == null)
                {
                    Initialize();
                }
                ep = _endpointVolume;
            }

            _isMuted = !_isMuted;
            if (ep != null)
            {
                try
                {
                    var ctx = _selfEventContext;
                    int hr = ep.SetMute(_isMuted, ref ctx);
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"SetMute({_isMuted}) returned hr=0x{hr:X8}");
                }
                catch (Exception ex)
                {
                    WindowsBuddy.Core.LogWriter.WriteNow("audio", $"ToggleMute EXCEPTION: {ex}");
                }
            }

            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(_volumePercent, _isMuted, false);
        }

        private void HandleExternalVolumeNotify(IntPtr pNotifyData)
        {
            if (pNotifyData == IntPtr.Zero) return;
            try
            {
                var data = Marshal.PtrToStructure<AUDIO_VOLUME_NOTIFICATION_DATA>(pNotifyData);
                if (data.guidEventContext == _selfEventContext)
                    return;

                int pct = (int)Math.Clamp(Math.Round(data.fMasterVolume * 100.0), 0, 100);
                bool muted = data.bMuted;
                _volumePercent = pct;
                _isMuted = muted;

                VolumeChanged?.Invoke();
                VolumeNotification?.Invoke(pct, muted, true);
            }
            catch { }
        }

        private void HandleDefaultDeviceChangedAsync()
        {
            Task.Run(() =>
            {
                lock (_syncLock)
                {
                    try
                    {
                        if (_enumerator != null)
                        {
                            BindDefaultEndpointLocked();
                        }
                    }
                    catch { }
                }
                try { VolumeChanged?.Invoke(); } catch { }
            });
        }

        private sealed class AudioVolumeCallback : IAudioEndpointVolumeCallback
        {
            private readonly CoreAudioService _owner;
            public AudioVolumeCallback(CoreAudioService owner) => _owner = owner;

            public int OnNotify(IntPtr pNotify)
            {
                _owner.HandleExternalVolumeNotify(pNotify);
                return 0;
            }
        }

        private sealed class DeviceNotificationClient : IMMNotificationClient
        {
            private readonly CoreAudioService _owner;
            public DeviceNotificationClient(CoreAudioService owner) => _owner = owner;

            public int OnDeviceStateChanged(string pwstrDeviceId, uint dwNewState) => 0;
            public int OnDeviceAdded(string pwstrDeviceId) => 0;
            public int OnDeviceRemoved(string pwstrDeviceId) => 0;

            public int OnDefaultDeviceChanged(int flow, int role, string pwstrDefaultDeviceId)
            {
                if (flow == 0 && (role == 0 || role == 1))
                {
                    _owner.HandleDefaultDeviceChangedAsync();
                }
                return 0;
            }

            public int OnPropertyValueChanged(string pwstrDeviceId, PROPERTYKEY key) => 0;
        }

        #region COM Declarations

        [ComImport]
        [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient pClient);
            [PreserveSig] int UnregisterEndpointNotificationCallback([MarshalAs(UnmanagedType.Interface)] IMMNotificationClient pClient);
        }

        [ComImport]
        [Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMNotificationClient
        {
            [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, uint dwNewState);
            [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
            [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
            [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
            [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, PROPERTYKEY key);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate([In] ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
            [PreserveSig] int GetState(out uint pdwState);
        }

        [ComImport]
        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
            [PreserveSig] int GetValue([In] ref PROPERTYKEY key, out PROPVARIANT pv);
            [PreserveSig] int SetValue([In] ref PROPERTYKEY key, [In] ref PROPVARIANT pv);
            [PreserveSig] int Commit();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PROPVARIANT
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(2)] public ushort wReserved1;
            [FieldOffset(4)] public ushort wReserved2;
            [FieldOffset(6)] public ushort wReserved3;
            [FieldOffset(8)] public IntPtr pwszVal;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AUDIO_VOLUME_NOTIFICATION_DATA
        {
            public Guid guidEventContext;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bMuted;
            public float fMasterVolume;
            public uint nChannels;
        }

        [ComImport]
        [Guid("657804FA-D6AD-4496-8A60-352752AF4F89")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolumeCallback
        {
            [PreserveSig] int OnNotify(IntPtr pNotifyData);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] IAudioEndpointVolumeCallback pNotify);
            [PreserveSig] int UnregisterControlChangeNotify([MarshalAs(UnmanagedType.Interface)] IAudioEndpointVolumeCallback pNotify);
            [PreserveSig] int GetChannelCount(out uint pnChannelCount);
            [PreserveSig] int SetMasterVolumeLevel(float fLevelDB, [In] ref Guid pguidEventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float fLevel, [In] ref Guid pguidEventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float pfLevelDB);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float pfLevel);
            [PreserveSig] int SetChannelVolumeLevel(uint nChannel, float fLevelDB, [In] ref Guid pguidEventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, [In] ref Guid pguidEventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, [In] ref Guid pguidEventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
            [PreserveSig] int GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);
            [PreserveSig] int VolumeStepUp([In] ref Guid pguidEventContext);
            [PreserveSig] int VolumeStepDown([In] ref Guid pguidEventContext);
            [PreserveSig] int QueryHardwareSupport(out uint pdwHardwareSupportMask);
            [PreserveSig] int GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
        }

        #endregion
    }
}
