using System;

namespace WindowsBuddy.Services
{
    public interface ICoreAudioService
    {
        // Playback
        string CurrentDeviceName { get; }
        int VolumePercent { get; }
        bool IsMuted { get; }
        void SetVolumePercent(int percent);
        void ToggleMute();
        void RefreshState();
        event Action VolumeChanged;
        event Action<int, bool, bool> VolumeNotification;

        // Capture (Microphone)
        bool IsMicrophoneAvailable { get; }
        string MicrophoneDeviceName { get; }
        bool IsMicrophoneMuted { get; }
        int MicrophoneVolumePercent { get; }
        void ToggleMicrophoneMute();
        void SetMicrophoneMute(bool muted);
        void SetMicrophoneVolumePercent(int percent);
        void RefreshMicrophoneState();
        event Action<bool> MicrophoneMuteChanged;
        event Action<int, bool, bool> MicrophoneVolumeNotification;
    }
}
