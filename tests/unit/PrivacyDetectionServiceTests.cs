using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using WindowsBuddy.Services;

namespace WindowsBuddy.Tests
{
    public static class PrivacyDetectionServiceTests
    {
        private static int _testsPassed = 0;
        private static int _testsFailed = 0;

        public static int Main(string[] args)
        {
            Console.WriteLine("============================================================");
            Console.WriteLine("Mayli Milestone 1: Privacy Detection Service Unit Test Suite");
            Console.WriteLine("============================================================");

            RunTest("Test_ProcessResolver_UnescapeRegistryPath", Test_ProcessResolver_UnescapeRegistryPath);
            RunTest("Test_ProcessResolver_ResolveNonPackagedName", Test_ProcessResolver_ResolveNonPackagedName);
            RunTest("Test_ProcessResolver_ResolvePackagedName", Test_ProcessResolver_ResolvePackagedName);
            RunTest("Test_ProcessResolver_ProcessLivenessCheck", Test_ProcessResolver_ProcessLivenessCheck);
            RunTest("Test_ProcessResolver_BrowserTitleHeuristics", Test_ProcessResolver_BrowserTitleHeuristics);
            RunTest("Test_DevicePrivacyService_MockStateInjection", Test_DevicePrivacyService_MockStateInjection);
            RunTest("Test_RegistryAccessMonitor_RealTimeEventDispatch", Test_RegistryAccessMonitor_RealTimeEventDispatch);
            RunTest("Test_DevicePrivacyService_WithRegistryMonitor", Test_DevicePrivacyService_WithRegistryMonitor);
            RunTest("Test_ProcessResolver_CrashedAppFilteredFromRegistry", Test_ProcessResolver_CrashedAppFilteredFromRegistry);

            Console.WriteLine("\n------------------------------------------------------------");
            Console.WriteLine($"Test Results: {_testsPassed} PASSED, {_testsFailed} FAILED");
            Console.WriteLine("============================================================");

            return _testsFailed == 0 ? 0 : 1;
        }

        private static void RunTest(string testName, Action testAction)
        {
            Console.Write($"Running {testName}... ");
            try
            {
                testAction();
                _testsPassed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("PASSED");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                _testsFailed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("FAILED");
                Console.ResetColor();
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine($"  Stack: {ex.StackTrace}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception($"Assertion Failed: {message}");
            }
        }

        private static void Test_ProcessResolver_UnescapeRegistryPath()
        {
            // Case 1: Standard drive path with '#' delimiters
            string raw1 = @"C:#Program Files#Google#Chrome#Application#chrome.exe";
            string unescaped1 = ProcessResolver.UnescapeRegistryPath(raw1);
            Assert(unescaped1 == @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                $"Expected 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe', got '{unescaped1}'");

            // Case 2: D: drive path
            string raw2 = @"D:#Games#Discord#app-1.0.9001#Discord.exe";
            string unescaped2 = ProcessResolver.UnescapeRegistryPath(raw2);
            Assert(unescaped2 == @"D:\Games\Discord\app-1.0.9001\Discord.exe",
                $"Expected 'D:\\Games\\Discord\\app-1.0.9001\\Discord.exe', got '{unescaped2}'");

            // Case 3: Network share path
            string raw3 = @"##server#share#tool.exe";
            string unescaped3 = ProcessResolver.UnescapeRegistryPath(raw3);
            Assert(unescaped3 == @"\\server\share\tool.exe",
                $"Expected '\\\\server\\share\\tool.exe', got '{unescaped3}'");

            // Case 4: Already unescaped path
            string raw4 = @"C:\Normal\Path.exe";
            string unescaped4 = ProcessResolver.UnescapeRegistryPath(raw4);
            Assert(unescaped4 == @"C:\Normal\Path.exe",
                $"Expected 'C:\\Normal\\Path.exe', got '{unescaped4}'");

            // Case 5: Empty/null string
            Assert(ProcessResolver.UnescapeRegistryPath(null) == string.Empty, "Null path should return empty string");
            Assert(ProcessResolver.UnescapeRegistryPath("") == string.Empty, "Empty path should return empty string");
        }

        private static void Test_ProcessResolver_ResolveNonPackagedName()
        {
            // Test 1: Real existing system executable (e.g. cmd.exe)
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string cmdExe = Path.Combine(sysDir, "cmd.exe");
            if (File.Exists(cmdExe))
            {
                string rawCmd = cmdExe.Replace('\\', '#').Replace(":#", "#");
                rawCmd = cmdExe[0] + ":#" + cmdExe.Substring(3).Replace('\\', '#');

                string friendlyName = ProcessResolver.ResolveNonPackagedName(rawCmd, out string resolvedPath);
                Assert(!string.IsNullOrWhiteSpace(friendlyName), "Friendly name for cmd.exe should not be empty");
                Assert(File.Exists(resolvedPath), $"Resolved path '{resolvedPath}' should exist");
            }

            // Test 2: Known fallback mapping
            string rawChrome = @"C:#Program Files#Google#Chrome#Application#chrome.exe";
            string chromeName = ProcessResolver.ResolveNonPackagedName(rawChrome, out _);
            Assert(chromeName.Contains("Chrome", StringComparison.OrdinalIgnoreCase),
                $"Chrome name should contain 'Chrome', got '{chromeName}'");

            string rawObs = @"C:#Program Files#obs-studio#bin#64bit#obs64.exe";
            string obsName = ProcessResolver.ResolveNonPackagedName(rawObs, out _);
            Assert(obsName == "OBS Studio", $"obs64.exe should resolve to 'OBS Studio', got '{obsName}'");
        }

        private static void Test_ProcessResolver_ResolvePackagedName()
        {
            // Case 1: Windows Camera
            string camPkg = "Microsoft.WindowsCamera_8wekyb3d8bbwe";
            string camName = ProcessResolver.ResolvePackagedName(camPkg);
            Assert(camName.Contains("Camera", StringComparison.OrdinalIgnoreCase),
                $"WindowsCamera package should contain 'Camera', got '{camName}'");

            // Case 2: Skype App
            string skypePkg = "Microsoft.SkypeApp_kzf8qxf38zg5c";
            string skypeName = ProcessResolver.ResolvePackagedName(skypePkg);
            Assert(skypeName.Contains("Skype", StringComparison.OrdinalIgnoreCase),
                $"Skype package should contain 'Skype', got '{skypeName}'");

            // Case 3: Empty input
            Assert(ProcessResolver.ResolvePackagedName(null) == "Application", "Null package should return 'Application'");
        }

        private static void Test_ProcessResolver_ProcessLivenessCheck()
        {
            // Case 1: The current running test process must be alive
            string currentProcessExe = Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(currentProcessExe))
            {
                bool isAlive = ProcessResolver.IsProcessAlive(currentProcessExe);
                Assert(isAlive, "Current running process should be detected as alive");
            }

            // Case 2: A non-existent executable name should be detected as NOT alive
            ProcessResolver.AllowSimulatedProcesses = false;
            try
            {
                bool fakeAlive = ProcessResolver.IsProcessAlive(@"C:\Program Files\FakeApp987654321\GhostProcess.exe");
                Assert(!fakeAlive, "Ghost process should be detected as NOT alive");
            }
            finally
            {
                ProcessResolver.AllowSimulatedProcesses = true;
            }

            // Case 3: Mock/Test path with AllowSimulatedProcesses = true should be allowed
            bool mockAlive = ProcessResolver.IsProcessAlive(@"C:\Mock\AutomatedTestCam.exe");
            Assert(mockAlive, "Mock process should be considered alive when AllowSimulatedProcesses is true");
        }

        private static void Test_ProcessResolver_BrowserTitleHeuristics()
        {
            // Test detection of different meeting services from browser window titles
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Meet - weekly-standup - Google Chrome") == "Google Meet",
                "Should detect Google Meet");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("meet.google.com/abc-defg-hij - Google Chrome") == "Google Meet",
                "Should detect Google Meet from URL");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Zoom Meeting") == "Zoom",
                "Should detect Zoom");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Zoom Workplace - All Hands") == "Zoom",
                "Should detect Zoom Workplace");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Microsoft Teams - Sprint Planning") == "Microsoft Teams",
                "Should detect Microsoft Teams");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Discord | #general-voice | Modi") == "Discord",
                "Should detect Discord");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Webex Personal Room 123") == "Webex",
                "Should detect Webex");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Slack Call (3 people)") == "Slack",
                "Should detect Slack");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("WhatsApp Call") == "WhatsApp",
                "Should detect WhatsApp");

            // Non-meeting titles should return null
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("Inbox (5) - user@gmail.com - Google Chrome") == null,
                "Regular email page should return null");
            Assert(ProcessResolver.DetectMeetingServiceFromTitle("GitHub - Pull Requests - Brave") == null,
                "GitHub page should return null");
        }

        private static void Test_DevicePrivacyService_MockStateInjection()
        {
            using var service = new DevicePrivacyService();

            DeviceAccessEventArgs receivedArgs = null;
            int eventFireCount = 0;
            service.DeviceAccessChanged += (s, e) =>
            {
                receivedArgs = e;
                Interlocked.Increment(ref eventFireCount);
            };

            // 1. Inject Microphone access
            service.SetMockState(micInUse: true, micAppName: "Discord", camInUse: false, camAppName: "");
            Assert(service.IsMicrophoneInUse == true, "IsMicrophoneInUse should be true");
            Assert(service.MicrophoneAppName == "Discord", "MicrophoneAppName should be 'Discord'");
            Assert(service.IsCameraInUse == false, "IsCameraInUse should be false");
            Assert(service.IsMockActive == true, "IsMockActive should be true");
            Assert(eventFireCount >= 1, "DeviceAccessChanged event should have fired");
            Assert(receivedArgs != null && receivedArgs.IsMicrophoneInUse && receivedArgs.MicrophoneAppName == "Discord",
                "Received event args should match microphone mock state");

            // 2. Inject Camera access as well (Dual active)
            service.SetMockState(micInUse: true, micAppName: "Discord", camInUse: true, camAppName: "Windows Camera");
            Assert(service.IsMicrophoneInUse == true, "IsMicrophoneInUse should remain true");
            Assert(service.IsCameraInUse == true, "IsCameraInUse should be true");
            Assert(service.CameraAppName == "Windows Camera", "CameraAppName should be 'Windows Camera'");
            Assert(receivedArgs.IsCameraInUse && receivedArgs.IsMicrophoneInUse,
                "Both camera and microphone should be reflected in event args");

            // 3. Clear mock state
            service.ClearMockState();
            Assert(service.IsMockActive == false, "IsMockActive should be false after ClearMockState");
        }

        private static void Test_RegistryAccessMonitor_RealTimeEventDispatch()
        {
            string testRootWebcam = @"Software\Mayli\Test\ConsentStore\webcam";
            string testRootMic = @"Software\Mayli\Test\ConsentStore\microphone";

            // Clean up any previous test keys
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\Test", false); } catch { }

            using var monitor = new RegistryAccessMonitor(testRootWebcam, testRootMic, startImmediately: true);

            var eventReceived = new AutoResetEvent(false);
            RegistryAccessSnapshot latestSnapshot = null;

            monitor.AccessChanged += snapshot =>
            {
                latestSnapshot = snapshot;
                eventReceived.Set();
            };

            // Allow monitor to initialize and arm notifications
            monitor.WaitForStarted(2000);
            eventReceived.Reset();

            // Step 1: Simulate Camera Access Start via HKCU registry write
            string mockCamAppKey = $@"{testRootWebcam}\NonPackaged\C:#Mock#AutomatedTestCam.exe";
            using (var key = Registry.CurrentUser.CreateSubKey(mockCamAppKey))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
            }

            // Step 2: Verify real-time signal via RegNotifyChangeKeyValue (< 1500ms)
            var sw = Stopwatch.StartNew();
            while ((latestSnapshot == null || !latestSnapshot.IsCameraInUse) && sw.ElapsedMilliseconds < 2000)
            {
                eventReceived.WaitOne(200);
            }
            Assert(latestSnapshot != null, "Snapshot should not be null");
            Assert(latestSnapshot.IsCameraInUse == true, "IsCameraInUse should be true in snapshot");
            Assert(latestSnapshot.CameraAppName.Contains("AutomatedTestCam", StringComparison.OrdinalIgnoreCase),
                $"CameraAppName should contain 'AutomatedTestCam', got '{latestSnapshot.CameraAppName}'");

            // Step 3: Simulate Camera Access Stop
            eventReceived.Reset();
            using (var key = Registry.CurrentUser.CreateSubKey(mockCamAppKey))
            {
                key.SetValue("LastUsedTimeStop", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
            }

            sw.Restart();
            while ((latestSnapshot == null || latestSnapshot.IsCameraInUse) && sw.ElapsedMilliseconds < 2000)
            {
                eventReceived.WaitOne(200);
            }
            Assert(latestSnapshot != null && latestSnapshot.IsCameraInUse == false, "IsCameraInUse should be false after session stop");

            // Clean up test keys
            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\Test", false); } catch { }
        }

        private static void Test_DevicePrivacyService_WithRegistryMonitor()
        {
            string testRootWebcam = @"Software\Mayli\Test\DeviceServiceTest\webcam";
            string testRootMic = @"Software\Mayli\Test\DeviceServiceTest\microphone";

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\Test\DeviceServiceTest", false); } catch { }

            using var monitor = new RegistryAccessMonitor(testRootWebcam, testRootMic, startImmediately: true);
            monitor.WaitForStarted(2000);
            using var service = new DevicePrivacyService(monitor);

            var eventReceived = new AutoResetEvent(false);
            DeviceAccessEventArgs latestArgs = null;

            service.DeviceAccessChanged += (s, e) =>
            {
                latestArgs = e;
                eventReceived.Set();
            };

            // Write microphone active key
            string mockMicKey = $@"{testRootMic}\NonPackaged\C:#Mock#TestMicrophoneApp.exe";
            using (var key = Registry.CurrentUser.CreateSubKey(mockMicKey))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
            }

            bool signaled = eventReceived.WaitOne(2000);
            Assert(signaled, "DevicePrivacyService should dispatch DeviceAccessChanged upon registry write");
            Assert(service.IsMicrophoneInUse == true, "DevicePrivacyService.IsMicrophoneInUse should be true");
            Assert(latestArgs != null && latestArgs.IsMicrophoneInUse, "Event args should indicate microphone in use");

            // Write microphone stopped key
            eventReceived.Reset();
            using (var key = Registry.CurrentUser.CreateSubKey(mockMicKey))
            {
                key.SetValue("LastUsedTimeStop", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
            }

            signaled = eventReceived.WaitOne(2000);
            Assert(signaled, "DevicePrivacyService should dispatch DeviceAccessChanged upon session end");
            Assert(service.IsMicrophoneInUse == false, "DevicePrivacyService.IsMicrophoneInUse should be false");

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\Test\DeviceServiceTest", false); } catch { }
        }

        private static void Test_ProcessResolver_CrashedAppFilteredFromRegistry()
        {
            // Simulate a crashed application: LastUsedTimeStart > LastUsedTimeStop, but exe is NOT running
            string testRootWebcam = @"Software\Mayli\Test\CrashedAppTest\webcam";
            string testRootMic = @"Software\Mayli\Test\CrashedAppTest\microphone";

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\Test\CrashedAppTest", false); } catch { }

            // Write a record for a non-existent, non-mock app
            string staleKeyPath = $@"{testRootWebcam}\NonPackaged\C:#NonExistentDir999#GhostCrashedApp999.exe";
            using (var key = Registry.CurrentUser.CreateSubKey(staleKeyPath))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
            }

            // Temporarily disable simulated processes to test real process liveness verification
            ProcessResolver.AllowSimulatedProcesses = false;
            try
            {
                using var monitor = new RegistryAccessMonitor(testRootWebcam, testRootMic, startImmediately: false);
                var snapshot = monitor.ScanRegistry();

                Assert(snapshot.IsCameraInUse == false,
                    "Crashed app with no running OS process must be filtered out as INACTIVE");
                Assert(string.IsNullOrEmpty(snapshot.CameraAppName),
                    "CameraAppName should be empty because ghost app was discarded");
            }
            finally
            {
                ProcessResolver.AllowSimulatedProcesses = true;
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\Test\CrashedAppTest", false); } catch { }
            }
        }
    }
}
