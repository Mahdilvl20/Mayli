using System;

namespace WindowsBuddy.Services
{
    public class MockCoreAudioService : ICoreAudioService
    {
        // Playback state
        public string CurrentDeviceName { get; set; } = "Mock Speakers";
        public int VolumePercent { get; set; } = 80;
        public bool IsMuted { get; set; } = false;

        // Capture (Microphone) state
        public bool IsMicrophoneAvailable { get; set; } = true;
        public string MicrophoneDeviceName { get; set; } = "Mock Microphone";
        public bool IsMicrophoneMuted { get; set; } = false;
        public int MicrophoneVolumePercent { get; set; } = 75;

        // Events
        public event Action VolumeChanged;
        public event Action<int, bool, bool> VolumeNotification;
        public event Action<bool> MicrophoneMuteChanged;
        public event Action<int, bool, bool> MicrophoneVolumeNotification;

        // Playback methods
        public void SetVolumePercent(int percent)
        {
            VolumePercent = Math.Clamp(percent, 0, 100);
            if (VolumePercent > 0 && IsMuted)
            {
                IsMuted = false;
            }
            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(VolumePercent, IsMuted, false);
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(VolumePercent, IsMuted, false);
        }

        public void RefreshState()
        {
            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(VolumePercent, IsMuted, false);
        }

        // Capture (Microphone) methods
        public void ToggleMicrophoneMute()
        {
            SetMicrophoneMute(!IsMicrophoneMuted);
        }

        public void SetMicrophoneMute(bool muted)
        {
            IsMicrophoneMuted = muted;
            MicrophoneMuteChanged?.Invoke(IsMicrophoneMuted);
            MicrophoneVolumeNotification?.Invoke(MicrophoneVolumePercent, IsMicrophoneMuted, false);
        }

        public void SetMicrophoneVolumePercent(int percent)
        {
            MicrophoneVolumePercent = Math.Clamp(percent, 0, 100);
            if (MicrophoneVolumePercent > 0 && IsMicrophoneMuted)
            {
                IsMicrophoneMuted = false;
                MicrophoneMuteChanged?.Invoke(false);
            }
            MicrophoneVolumeNotification?.Invoke(MicrophoneVolumePercent, IsMicrophoneMuted, false);
        }

        public void RefreshMicrophoneState()
        {
            MicrophoneMuteChanged?.Invoke(IsMicrophoneMuted);
            MicrophoneVolumeNotification?.Invoke(MicrophoneVolumePercent, IsMicrophoneMuted, false);
        }

        // Test simulation helper methods
        public void SimulateExternalMicrophoneMute(bool muted)
        {
            IsMicrophoneMuted = muted;
            MicrophoneMuteChanged?.Invoke(muted);
            MicrophoneVolumeNotification?.Invoke(MicrophoneVolumePercent, muted, true);
        }

        public void SimulateExternalMicrophoneVolume(int percent, bool muted)
        {
            MicrophoneVolumePercent = Math.Clamp(percent, 0, 100);
            bool muteChanged = (IsMicrophoneMuted != muted);
            IsMicrophoneMuted = muted;
            if (muteChanged)
            {
                MicrophoneMuteChanged?.Invoke(muted);
            }
            MicrophoneVolumeNotification?.Invoke(MicrophoneVolumePercent, muted, true);
        }

        public void SimulateDeviceChanged(string deviceName, bool isAvailable)
        {
            MicrophoneDeviceName = deviceName;
            IsMicrophoneAvailable = isAvailable;
            MicrophoneMuteChanged?.Invoke(IsMicrophoneMuted);
            MicrophoneVolumeNotification?.Invoke(MicrophoneVolumePercent, IsMicrophoneMuted, true);
        }

        public void SimulateExternalPlaybackMute(bool muted)
        {
            IsMuted = muted;
            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(VolumePercent, muted, true);
        }

        public void SimulateExternalPlaybackVolume(int percent, bool muted)
        {
            VolumePercent = Math.Clamp(percent, 0, 100);
            IsMuted = muted;
            VolumeChanged?.Invoke();
            VolumeNotification?.Invoke(percent, muted, true);
        }
    }
}
