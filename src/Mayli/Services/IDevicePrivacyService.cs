using System;

namespace WindowsBuddy.Services
{
    /// <summary>
    /// Event arguments containing live microphone and webcam access status.
    /// </summary>
    public class DeviceAccessEventArgs : EventArgs
    {
        public bool IsMicrophoneInUse { get; init; }
        public string MicrophoneAppName { get; init; } = string.Empty;
        public bool IsCameraInUse { get; init; }
        public string CameraAppName { get; init; } = string.Empty;
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service contract for detecting live microphone and webcam access by applications.
    /// </summary>
    public interface IDevicePrivacyService : IDisposable
    {
        /// <summary>
        /// Gets whether any application is currently capturing audio from the microphone.
        /// </summary>
        bool IsMicrophoneInUse { get; }

        /// <summary>
        /// Gets the friendly name of the application currently capturing audio.
        /// Empty string if no application is capturing audio.
        /// </summary>
        string MicrophoneAppName { get; }

        /// <summary>
        /// Gets whether any application is currently capturing video from the webcam.
        /// </summary>
        bool IsCameraInUse { get; }

        /// <summary>
        /// Gets the friendly name of the application currently capturing video.
        /// Empty string if no application is capturing video.
        /// </summary>
        string CameraAppName { get; }

        /// <summary>
        /// Fired when microphone or camera access state changes.
        /// </summary>
        event EventHandler<DeviceAccessEventArgs> DeviceAccessChanged;

        /// <summary>
        /// Forces an immediate refresh of device access states from the registry.
        /// </summary>
        void Refresh();

        /// <summary>
        /// Injects mock state for automated testing without requiring real hardware.
        /// Pass null for any parameter to leave it unchanged or unmocked.
        /// </summary>
        void SetMockState(bool? micInUse, string micAppName, bool? camInUse, string camAppName);

        /// <summary>
        /// Clears mock state and restores live registry monitoring.
        /// </summary>
        void ClearMockState();

        /// <summary>
        /// Gets whether mock state override is currently active.
        /// </summary>
        bool IsMockActive { get; }
    }
}
