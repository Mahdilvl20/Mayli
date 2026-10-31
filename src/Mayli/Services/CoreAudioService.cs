using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace WindowsBuddy.Services
{
    public sealed class CoreAudioService
    {
        private static readonly Lazy<CoreAudioService> _lazy = new(() => new CoreAudioService());
        public static CoreAudioService Instance => _lazy.Value;

        private IAudioEndpointVolume _endpointVolume;
        private string _deviceName = "Speakers";
        private bool _isMuted;
        private int _volumePercent = 100;

        public event Action VolumeChanged;

        public string CurrentDeviceName => _deviceName;
        public int VolumePercent => _volumePercent;
        public bool IsMuted => _isMuted;

        private CoreAudioService()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out var device) == 0 && device != null)
                {
                    // Query Device Friendly Name
                    try
                    {
                        var storeGuid = new Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99");
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
                    catch { }

                    var volumeGuid = typeof(IAudioEndpointVolume).GUID;
                    if (device.Activate(ref volumeGuid, 23, IntPtr.Zero, out var ep) == 0 && ep is IAudioEndpointVolume vol)
                    {
                        _endpointVolume = vol;
                        RefreshState();
                    }
                }
            }
            catch
            {
                _deviceName = "Realtek(R) Audio";
                _volumePercent = 100;
                _isMuted = false;
            }
        }

        public void RefreshState()
        {
            if (_endpointVolume == null) return;
            try
            {
                if (_endpointVolume.GetMasterVolumeLevelScalar(out float level) == 0)
                {
                    _volumePercent = (int)Math.Round(level * 100.0);
                }
                if (_endpointVolume.GetMute(out bool mute) == 0)
                {
                    _isMuted = mute;
                }
            }
            catch { }
        }

        public void SetVolumePercent(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            _volumePercent = percent;

            if (_endpointVolume != null)
            {
                try
                {
                    float scalar = percent / 100.0f;
                    var ctx = Guid.Empty;
                    _endpointVolume.SetMasterVolumeLevelScalar(scalar, ref ctx);
                    if (_isMuted && percent > 0)
                    {
                        _endpointVolume.SetMute(false, ref ctx);
                        _isMuted = false;
                    }
                }
                catch { }
            }

            VolumeChanged?.Invoke();
        }

        public void ToggleMute()
        {
            _isMuted = !_isMuted;
            if (_endpointVolume != null)
            {
                try
                {
                    var ctx = Guid.Empty;
                    _endpointVolume.SetMute(_isMuted, ref ctx);
                }
                catch { }
            }
            VolumeChanged?.Invoke();
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
            int EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate(ref Guid iid, uint dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            int OpenPropertyStore(uint stgmAccess, out IPropertyStore ppProperties);
        }

        [ComImport]
        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            int GetCount(out uint count);
            int GetAt(uint iProp, out PROPERTYKEY pkey);
            int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
            int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
            int Commit();
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
            [FieldOffset(0)] public short vt;
            [FieldOffset(8)] public IntPtr pwszVal;
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, ref Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, ref Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
            int SetChannelVolumeLevel(uint nChannel, float fLevelDB, ref Guid pguidEventContext);
            int SetChannelVolumeLevelScalar(uint nChannel, float fLevel, ref Guid pguidEventContext);
            int GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
            int GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid pguidEventContext);
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
        }

        #endregion
    }
}
