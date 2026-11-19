using System;
using System.Collections.Generic;
using WindowsBuddy.Services;

namespace Mayli.UnitTests
{
    public class CoreAudioMicrophoneTests
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" Starting CoreAudio Microphone Unit Tests");
            Console.WriteLine("=================================================");

            RunTest("TestMockCoreAudioService_InitialState", TestMockCoreAudioService_InitialState);
            RunTest("TestMockCoreAudioService_ToggleMicrophoneMute", TestMockCoreAudioService_ToggleMicrophoneMute);
            RunTest("TestMockCoreAudioService_SetMicrophoneMute", TestMockCoreAudioService_SetMicrophoneMute);
            RunTest("TestMockCoreAudioService_VolumeUnmutesMicrophone", TestMockCoreAudioService_VolumeUnmutesMicrophone);
            RunTest("TestMockCoreAudioService_BiDirectionalSyncSimulation", TestMockCoreAudioService_BiDirectionalSyncSimulation);
            RunTest("TestMockCoreAudioService_DeviceChangeSimulation", TestMockCoreAudioService_DeviceChangeSimulation);
            RunTest("TestCoreAudioService_InstanceMockSwapping", TestCoreAudioService_InstanceMockSwapping);
            RunTest("TestLiveCoreAudioService_GracefulBehavior", TestLiveCoreAudioService_GracefulBehavior);

            Console.WriteLine("=================================================");
            Console.WriteLine($" Test Results: {_passedCount} Passed, {_failedCount} Failed");
            Console.WriteLine("=================================================");

            return _failedCount == 0 ? 0 : 1;
        }

        private static void RunTest(string testName, Action testAction)
        {
            try
            {
                testAction();
                Console.WriteLine($" [PASS] {testName}");
                _passedCount++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($" [FAIL] {testName}: {ex.Message}");
                Console.ResetColor();
                _failedCount++;
            }
        }

        private static void AssertEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new Exception($"{message}. Expected: '{expected}', Actual: '{actual}'");
            }
        }

        private static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"{message}. Expected: True, Actual: False");
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception($"{message}. Expected: False, Actual: True");
            }
        }

        private static void TestMockCoreAudioService_InitialState()
        {
            var mock = new MockCoreAudioService();

            AssertTrue(mock.IsMicrophoneAvailable, "IsMicrophoneAvailable should default to true");
            AssertEqual("Mock Microphone", mock.MicrophoneDeviceName, "MicrophoneDeviceName initial value");
            AssertFalse(mock.IsMicrophoneMuted, "IsMicrophoneMuted should default to false");
            AssertEqual(75, mock.MicrophoneVolumePercent, "MicrophoneVolumePercent initial value");
            AssertEqual("Mock Speakers", mock.CurrentDeviceName, "CurrentDeviceName initial value");
            AssertEqual(80, mock.VolumePercent, "VolumePercent initial value");
            AssertFalse(mock.IsMuted, "IsMuted should default to false");
        }

        private static void TestMockCoreAudioService_ToggleMicrophoneMute()
        {
            var mock = new MockCoreAudioService();
            bool eventFired = false;
            bool lastMuteEventValue = false;

            mock.MicrophoneMuteChanged += muted =>
            {
                eventFired = true;
                lastMuteEventValue = muted;
            };

            // First toggle: false -> true
            mock.ToggleMicrophoneMute();
            AssertTrue(mock.IsMicrophoneMuted, "IsMicrophoneMuted should be true after first toggle");
            AssertTrue(eventFired, "MicrophoneMuteChanged should have fired");
            AssertTrue(lastMuteEventValue, "Event payload should be true");

            // Second toggle: true -> false
            eventFired = false;
            mock.ToggleMicrophoneMute();
            AssertFalse(mock.IsMicrophoneMuted, "IsMicrophoneMuted should be false after second toggle");
            AssertTrue(eventFired, "MicrophoneMuteChanged should have fired");
            AssertFalse(lastMuteEventValue, "Event payload should be false");
        }

        private static void TestMockCoreAudioService_SetMicrophoneMute()
        {
            var mock = new MockCoreAudioService();
            int eventCount = 0;
            bool lastValue = false;

            mock.MicrophoneMuteChanged += muted =>
            {
                eventCount++;
                lastValue = muted;
            };

            mock.SetMicrophoneMute(true);
            AssertTrue(mock.IsMicrophoneMuted, "Should be muted");
            AssertEqual(1, eventCount, "Event should fire once");
            AssertTrue(lastValue, "Event value should be true");

            mock.SetMicrophoneMute(false);
            AssertFalse(mock.IsMicrophoneMuted, "Should be unmuted");
            AssertEqual(2, eventCount, "Event should fire twice");
            AssertFalse(lastValue, "Event value should be false");
        }

        private static void TestMockCoreAudioService_VolumeUnmutesMicrophone()
        {
            var mock = new MockCoreAudioService();
            mock.SetMicrophoneMute(true);
            AssertTrue(mock.IsMicrophoneMuted, "Prerequisite: microphone is muted");

            bool unmuteEventFired = false;
            mock.MicrophoneMuteChanged += muted =>
            {
                if (!muted) unmuteEventFired = true;
            };

            mock.SetMicrophoneVolumePercent(60);
            AssertFalse(mock.IsMicrophoneMuted, "Setting volume > 0 must unmute microphone");
            AssertEqual(60, mock.MicrophoneVolumePercent, "MicrophoneVolumePercent must be 60");
            AssertTrue(unmuteEventFired, "MicrophoneMuteChanged(false) should have fired");
        }

        private static void TestMockCoreAudioService_BiDirectionalSyncSimulation()
        {
            var mock = new MockCoreAudioService();
            bool eventFired = false;
            bool externalState = false;

            mock.MicrophoneMuteChanged += muted =>
            {
                eventFired = true;
                externalState = muted;
            };

            // Simulate external OS or hardware mute
            mock.SimulateExternalMicrophoneMute(true);
            AssertTrue(mock.IsMicrophoneMuted, "SimulateExternalMicrophoneMute should update state");
            AssertTrue(eventFired, "MicrophoneMuteChanged should fire for external mute");
            AssertTrue(externalState, "MicrophoneMuteChanged payload should be true");

            // Simulate external volume adjustment with unmute
            eventFired = false;
            mock.SimulateExternalMicrophoneVolume(42, false);
            AssertEqual(42, mock.MicrophoneVolumePercent, "Volume must update from external sync");
            AssertFalse(mock.IsMicrophoneMuted, "Mute must update from external sync");
            AssertTrue(eventFired, "MicrophoneMuteChanged must fire when state changes");
            AssertFalse(externalState, "MicrophoneMuteChanged payload must be false");
        }

        private static void TestMockCoreAudioService_DeviceChangeSimulation()
        {
            var mock = new MockCoreAudioService();
            mock.SimulateDeviceChanged("HyperX SoloCast", true);

            AssertEqual("HyperX SoloCast", mock.MicrophoneDeviceName, "Device name should update");
            AssertTrue(mock.IsMicrophoneAvailable, "Microphone should be marked available");

            mock.SimulateDeviceChanged("No Microphone", false);
            AssertFalse(mock.IsMicrophoneAvailable, "Microphone should be marked unavailable");
        }

        private static void TestCoreAudioService_InstanceMockSwapping()
        {
            // Verify default instance is available
            var defaultInstance = CoreAudioService.Instance;
            AssertTrue(defaultInstance != null, "Default CoreAudioService.Instance must not be null");

            var mock = new MockCoreAudioService();
            CoreAudioService.SetInstanceForTesting(mock);

            try
            {
                AssertTrue(ReferenceEquals(CoreAudioService.Instance, mock), "CoreAudioService.Instance must return mock when set");

                // Execute action through singleton accessor
                CoreAudioService.Instance.ToggleMicrophoneMute();
                AssertTrue(mock.IsMicrophoneMuted, "Mock state must be updated via CoreAudioService.Instance");
            }
            finally
            {
                CoreAudioService.ResetInstance();
            }

            AssertFalse(ReferenceEquals(CoreAudioService.Instance, mock), "CoreAudioService.Instance must revert after ResetInstance()");
        }

        private static void TestLiveCoreAudioService_GracefulBehavior()
        {
            // Verify real service initialization doesn't throw or crash
            var live = CoreAudioService.RealInstance;
            AssertTrue(live != null, "CoreAudioService.RealInstance must be non-null");

            // Access all microphone properties - must not throw
            bool isAvailable = live.IsMicrophoneAvailable;
            string micName = live.MicrophoneDeviceName;
            bool isMuted = live.IsMicrophoneMuted;
            int micVol = live.MicrophoneVolumePercent;

            AssertTrue(micName != null, "MicrophoneDeviceName must not be null");
            AssertTrue(micVol >= 0 && micVol <= 100, $"MicrophoneVolumePercent must be 0-100 (was {micVol})");

            // If no mic is available, device name should indicate "No Microphone"
            if (!isAvailable)
            {
                AssertEqual("No Microphone", micName, "When mic is unavailable, friendly name should be 'No Microphone'");
            }

            // Toggling mute on live service must execute without throwing even without hardware
            try
            {
                live.ToggleMicrophoneMute();
                // Toggle back to restore original state
                live.ToggleMicrophoneMute();
            }
            catch (Exception ex)
            {
                throw new Exception($"ToggleMicrophoneMute threw on live instance: {ex.Message}");
            }
        }
    }
}
