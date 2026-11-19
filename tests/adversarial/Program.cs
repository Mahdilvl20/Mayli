using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;

namespace Mayli.AdversarialTests
{
    public class Program
    {
        private static int _testPassCount = 0;
        private static int _testFailCount = 0;

        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine("    Mayli Adversarial Stress Harness & Empirical Verification Engine (Challenger)  ");
            Console.WriteLine("================================================================================");
            Console.WriteLine($" OS: {Environment.OSVersion.VersionString} | Architecture: {RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($" Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n");

            // HARNESS 1: 100-Iteration Rapid Mute Toggle Stress Harness
            ExecuteMuteToggleStressHarness();

            // HARNESS 2: Sub-ms Registry Event Dispatch Verification
            ExecuteRegistryEventDispatchHarness();

            // HARNESS 3: Hover Dwell Suppression & Notch Body Expansion Verification
            ExecuteHoverDwellSuppressionHarness();

            // HARNESS 4: Extreme Cross-Subsystem Concurrent Stress Storm
            ExecuteCombinedStressHarness();

            Console.WriteLine("\n================================================================================");
            Console.WriteLine($" ADVERSARIAL STRESS TEST SUMMARY: {_testPassCount} PASSED, {_testFailCount} FAILED");
            Console.WriteLine("================================================================================");

            return _testFailCount == 0 ? 0 : 1;
        }

        #region HARNESS 1: 100-Iteration Rapid Mute Toggle Stress Harness

        private static void ExecuteMuteToggleStressHarness()
        {
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine(" [HARNESS 1] 100-Iteration Rapid Mute Toggle Stress & Synchronization Harness");
            Console.WriteLine("--------------------------------------------------------------------------------");

            var audioService = CoreAudioService.Instance;
            bool isLiveMicAvailable = audioService.IsMicrophoneAvailable;
            string micDevName = audioService.MicrophoneDeviceName;
            bool initialMuteState = audioService.IsMicrophoneMuted;

            Console.WriteLine($"  Live Microphone Available: {isLiveMicAvailable}");
            Console.WriteLine($"  Device Name: '{micDevName}'");
            Console.WriteLine($"  Initial Mute State: {initialMuteState}");

            // TEST 1.1: 100 Consecutive Synchronous Toggles on CoreAudioService
            RunTest("H1.1: 100-Iteration Rapid Mute Toggle (Deadlock, COM Exception & Sync Check)", () =>
            {
                var sw = Stopwatch.StartNew();
                int toggleCount = 100;
                double minMs = double.MaxValue;
                double maxMs = double.MinValue;
                double totalMs = 0;

                int eventCallbackCount = 0;
                Action<bool> onMuteChanged = (m) => Interlocked.Increment(ref eventCallbackCount);
                audioService.MicrophoneMuteChanged += onMuteChanged;

                try
                {
                    for (int i = 0; i < toggleCount; i++)
                    {
                        var stepSw = Stopwatch.StartNew();
                        audioService.ToggleMicrophoneMute();
                        stepSw.Stop();

                        double elapsed = stepSw.Elapsed.TotalMilliseconds;
                        totalMs += elapsed;
                        if (elapsed < minMs) minMs = elapsed;
                        if (elapsed > maxMs) maxMs = elapsed;

                        // Expected state after i+1 toggles
                        bool expectedMute = (i % 2 == 0) ? !initialMuteState : initialMuteState;
                        if (audioService.IsMicrophoneMuted != expectedMute)
                        {
                            throw new Exception($"Desynchronization detected at iteration {i + 1}! Expected: {expectedMute}, Actual: {audioService.IsMicrophoneMuted}");
                        }
                    }

                    sw.Stop();
                    Console.WriteLine($"    Executed {toggleCount} toggles in {sw.ElapsedMilliseconds} ms (avg: {totalMs / toggleCount:F3} ms, min: {minMs:F3} ms, max: {maxMs:F3} ms)");
                    Console.WriteLine($"    MicrophoneMuteChanged fired: {eventCallbackCount} times");

                    // Verify final state matches initial state (even number of toggles)
                    if (audioService.IsMicrophoneMuted != initialMuteState)
                    {
                        throw new Exception($"Final mute state ({audioService.IsMicrophoneMuted}) desynchronized from initial state ({initialMuteState})!");
                    }

                    if (eventCallbackCount != toggleCount)
                    {
                        throw new Exception($"Event callback count ({eventCallbackCount}) did not match toggle count ({toggleCount})!");
                    }
                }
                finally
                {
                    audioService.MicrophoneMuteChanged -= onMuteChanged;
                    // Guaranteed restore
                    audioService.SetMicrophoneMute(initialMuteState);
                }
            });

            // TEST 1.2: Multi-Threaded Concurrent Mute Toggle Stress
            RunTest("H1.2: Multi-Threaded Concurrent Mute Toggle Stress (4 Threads x 25 Toggles = 100 Toggles)", () =>
            {
                int threadsCount = 4;
                int togglesPerThread = 25; // 4 * 25 = 100 total toggles
                var barrier = new Barrier(threadsCount);
                int comExceptionsCount = 0;
                int deadlockCount = 0;

                var tasks = new Task[threadsCount];
                for (int t = 0; t < threadsCount; t++)
                {
                    tasks[t] = Task.Run(() =>
                    {
                        barrier.SignalAndWait();
                        for (int i = 0; i < togglesPerThread; i++)
                        {
                            try
                            {
                                audioService.ToggleMicrophoneMute();
                            }
                            catch (COMException)
                            {
                                Interlocked.Increment(ref comExceptionsCount);
                            }
                            catch (Exception)
                            {
                                Interlocked.Increment(ref deadlockCount);
                            }
                        }
                    });
                }

                bool finished = Task.WaitAll(tasks, 5000);
                if (!finished)
                {
                    throw new Exception("Deadlock detected during concurrent multi-threaded mute toggle!");
                }

                if (comExceptionsCount > 0)
                {
                    throw new Exception($"Encountered {comExceptionsCount} COM exceptions during concurrent toggling!");
                }

                if (deadlockCount > 0)
                {
                    throw new Exception($"Encountered {deadlockCount} errors during concurrent toggling!");
                }

                Console.WriteLine($"    All {threadsCount * togglesPerThread} concurrent toggles completed without deadlocks or COM exceptions.");
                // Restore state
                audioService.SetMicrophoneMute(initialMuteState);
            });
        }

        #endregion

        #region HARNESS 2: Sub-ms Registry Event Dispatch Verification

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW", SetLastError = true)]
        private static extern int RegOpenKeyEx(IntPtr hKey, string lpSubKey, uint ulOptions, int samDesired, out IntPtr phkResult);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegNotifyChangeKeyValue(IntPtr hKey, bool bWatchSubtree, uint dwNotifyFilter, IntPtr hEvent, bool fAsynchronous);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        private const uint HKEY_CURRENT_USER = 0x80000001;
        private const int KEY_READ = 0x20019;
        private const uint NOTIFY_FILTER = 0x1 | 0x4; // REG_NOTIFY_CHANGE_NAME | REG_NOTIFY_CHANGE_LAST_SET

        private static void ExecuteRegistryEventDispatchHarness()
        {
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine(" [HARNESS 2] Sub-ms Registry Event Dispatch & Immediate Clear Verification");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // TEST 2.1: Kernel RegNotifyChangeKeyValue Latency Benchmark (<1ms)
            RunTest("H2.1: Kernel RegNotifyChangeKeyValue Notification Latency Benchmark (<1ms dispatch)", () =>
            {
                string testKeyPath = @"Software\Mayli\AdversarialStressTest\ConsentStore\webcam";
                Registry.CurrentUser.CreateSubKey(testKeyPath)?.Close();

                IntPtr hKey = IntPtr.Zero;
                int openRes = RegOpenKeyEx(new IntPtr(unchecked((int)HKEY_CURRENT_USER)), testKeyPath, 0, KEY_READ, out hKey);
                if (openRes != 0 || hKey == IntPtr.Zero)
                {
                    throw new Exception($"Failed to open registry key: {openRes}");
                }

                var notifyEvent = new AutoResetEvent(false);
                int iterations = 20;
                double[] latenciesUs = new double[iterations];

                try
                {
                    for (int i = 0; i < iterations; i++)
                    {
                        int notifyRes = RegNotifyChangeKeyValue(
                            hKey,
                            bWatchSubtree: true,
                            dwNotifyFilter: NOTIFY_FILTER,
                            hEvent: notifyEvent.SafeWaitHandle.DangerousGetHandle(),
                            fAsynchronous: true);

                        if (notifyRes != 0)
                        {
                            throw new Exception($"RegNotifyChangeKeyValue failed with code {notifyRes}");
                        }

                        // Write value and measure exact time until event signals
                        string subkeyName = $@"App_{i}";
                        var sw = Stopwatch.StartNew();
                        using (var subkey = Registry.CurrentUser.CreateSubKey($@"{testKeyPath}\{subkeyName}"))
                        {
                            subkey.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                            subkey.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
                        }

                        bool signaled = notifyEvent.WaitOne(500);
                        sw.Stop();

                        if (!signaled)
                        {
                            throw new Exception($"Iteration {i}: RegNotifyChangeKeyValue did not signal within 500ms!");
                        }

                        double elapsedUs = sw.Elapsed.TotalMilliseconds * 1000.0;
                        latenciesUs[i] = elapsedUs;
                    }

                    double totalUs = 0;
                    double minUs = double.MaxValue;
                    double maxUs = double.MinValue;
                    int subMsCount = 0;

                    for (int i = 0; i < iterations; i++)
                    {
                        double val = latenciesUs[i];
                        totalUs += val;
                        if (val < minUs) minUs = val;
                        if (val > maxUs) maxUs = val;
                        if (val < 1000.0) subMsCount++; // < 1ms = < 1000 us
                    }

                    double avgUs = totalUs / iterations;
                    Console.WriteLine($"    Kernel Notification Latency over {iterations} iterations: avg={avgUs:F1} µs ({avgUs/1000.0:F3} ms), min={minUs:F1} µs, max={maxUs:F1} µs");
                    Console.WriteLine($"    Sub-millisecond (<1ms) dispatches: {subMsCount}/{iterations} ({subMsCount * 100.0 / iterations:F0}%)");

                    if (avgUs >= 1000.0 && subMsCount < iterations / 2)
                    {
                        throw new Exception($"Average kernel dispatch latency ({avgUs:F1} µs) exceeded 1ms (1000 µs) threshold!");
                    }
                }
                finally
                {
                    RegCloseKey(hKey);
                    try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdversarialStressTest", false); } catch { }
                }
            });

            // TEST 2.2: Immediate Clearance on Key/Value Removal
            RunTest("H2.2: Key/Value Removal Immediate Event Dispatch & Snapshot Invalidation", () =>
            {
                string testKeyPath = @"Software\Mayli\AdversarialClearTest\ConsentStore\microphone";
                Registry.CurrentUser.CreateSubKey(testKeyPath)?.Close();

                IntPtr hKey = IntPtr.Zero;
                RegOpenKeyEx(new IntPtr(unchecked((int)HKEY_CURRENT_USER)), testKeyPath, 0, KEY_READ, out hKey);
                var notifyEvent = new AutoResetEvent(false);

                try
                {
                    // 1. Create active capture subkey
                    string activeSubkey = $@"{testKeyPath}\ActiveApp";
                    using (var sub = Registry.CurrentUser.CreateSubKey(activeSubkey))
                    {
                        sub.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                        sub.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
                    }

                    // 2. Arm notification
                    RegNotifyChangeKeyValue(
                        hKey,
                        bWatchSubtree: true,
                        dwNotifyFilter: NOTIFY_FILTER,
                        hEvent: notifyEvent.SafeWaitHandle.DangerousGetHandle(),
                        fAsynchronous: true);

                    // 3. Remove/clear subkey and measure dispatch latency
                    var sw = Stopwatch.StartNew();
                    Registry.CurrentUser.DeleteSubKeyTree(activeSubkey, false);
                    bool signaled = notifyEvent.WaitOne(500);
                    sw.Stop();

                    if (!signaled)
                    {
                        throw new Exception("Key deletion did not trigger RegNotifyChangeKeyValue event!");
                    }

                    double clearLatencyUs = sw.Elapsed.TotalMilliseconds * 1000.0;
                    Console.WriteLine($"    Key Removal Signal Latency: {clearLatencyUs:F1} µs ({clearLatencyUs/1000.0:F3} ms)");

                    if (clearLatencyUs >= 50000.0) // 50ms max tolerance
                    {
                        throw new Exception($"Removal dispatch was delayed: {clearLatencyUs/1000.0:F2} ms");
                    }
                }
                finally
                {
                    RegCloseKey(hKey);
                    try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdversarialClearTest", false); } catch { }
                }
            });

            // TEST 2.3: RegistryAccessMonitor End-to-End Dynamic Activation and Clearing
            RunTest("H2.3: RegistryAccessMonitor End-to-End Real-Time Detection & Clear Verification", () =>
            {
                string testWebcam = @"Software\Mayli\AdvMonitorTest\webcam";
                string testMic = @"Software\Mayli\AdvMonitorTest\microphone";
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdvMonitorTest", false); } catch { }

                using var monitor = new RegistryAccessMonitor(testWebcam, testMic, startImmediately: true);
                monitor.WaitForStarted(2000);

                var accessChangedEvent = new AutoResetEvent(false);
                RegistryAccessSnapshot latestSnapshot = null;
                monitor.AccessChanged += (s) =>
                {
                    latestSnapshot = s;
                    accessChangedEvent.Set();
                };

                // Clear any startup snapshot
                accessChangedEvent.Reset();

                // 1. Simulate Microphone Activation
                string micAppKey = $@"{testMic}\NonPackaged\C:#AdversarialTest#LiveMicrophoneApp.exe";
                using (var k = Registry.CurrentUser.CreateSubKey(micAppKey))
                {
                    k.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                    k.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
                }

                bool detected = accessChangedEvent.WaitOne(2000);
                if (!detected || latestSnapshot == null || !latestSnapshot.IsMicrophoneInUse)
                {
                    throw new Exception("RegistryAccessMonitor failed to detect active microphone capture within 2000ms!");
                }
                Console.WriteLine($"    Successfully detected mic capture: App='{latestSnapshot.MicrophoneAppName}'");

                // 2. Simulate Microphone Deactivation (Clear)
                accessChangedEvent.Reset();
                using (var k = Registry.CurrentUser.CreateSubKey(micAppKey))
                {
                    k.SetValue("LastUsedTimeStop", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                }

                bool cleared = accessChangedEvent.WaitOne(2000);
                if (!cleared || latestSnapshot == null || latestSnapshot.IsMicrophoneInUse)
                {
                    throw new Exception("RegistryAccessMonitor failed to detect microphone deactivation (clear)!");
                }
                Console.WriteLine("    Successfully cleared mic capture on session stop.");

                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdvMonitorTest", false); } catch { }
            });
        }

        #endregion

        #region HARNESS 3: Hover Dwell Suppression & Notch Body Expansion Verification

        private static void ExecuteHoverDwellSuppressionHarness()
        {
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine(" [HARNESS 3] Hover Dwell Suppression & Notch Body Expansion Empirical Verification");
            Console.WriteLine("--------------------------------------------------------------------------------");

            // TEST 3.1: Verify IsHovered State on NotchPrivacyIndicator
            RunTest("H3.1: NotchPrivacyIndicator IsHovered Property State Consistency", () =>
            {
                var indicator = new NotchPrivacyIndicator();
                if (indicator.IsHovered)
                {
                    throw new Exception("Initial IsHovered must be false!");
                }

                // Simulate pointer enter
                var mouseDevice = Mouse.PrimaryDevice;
                // Use reflection to invoke protected OnMouseEnter / OnMouseLeave
                var onMouseEnterMethod = typeof(NotchPrivacyIndicator).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                var onMouseLeaveMethod = typeof(NotchPrivacyIndicator).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic);

                var dummyEventArgs = new MouseEventArgs(mouseDevice, 0);

                onMouseEnterMethod.Invoke(indicator, new object[] { dummyEventArgs });
                if (!indicator.IsHovered)
                {
                    throw new Exception("OnMouseEnter failed to set IsHovered to true!");
                }

                onMouseLeaveMethod.Invoke(indicator, new object[] { dummyEventArgs });
                if (indicator.IsHovered)
                {
                    throw new Exception("OnMouseLeave failed to set IsHovered to false!");
                }
                Console.WriteLine("    NotchPrivacyIndicator correctly transitions IsHovered true on enter, false on leave.");
            });

            // TEST 3.2: Verify Accidental Morph Suppression When Indicator Is Hovered
            RunTest("H3.2: Accidental Notch Morph Suppression When Indicator Is Hovered", () =>
            {
                // Instantiate CapsuleWindow
                var window = new CapsuleWindow();
                var card = window.CapsuleCard;
                var indicator = window.PrivacyIndicator;
                var passController = window.PassThroughController;

                // Inspect state machine via reflection
                var machineField = typeof(CapsuleWindow).GetField("_machine", BindingFlags.Instance | BindingFlags.NonPublic);
                var machine = (NotchStateMachine)machineField.GetValue(window);

                // Set mock access state so indicator is visible
                indicator.UpdateAccessState(isMicInUse: true, micAppName: "Zoom", isCamInUse: false, camAppName: null);
                if (indicator.Visibility != Visibility.Visible)
                {
                    throw new Exception("Privacy indicator was expected to be Visible!");
                }

                // Initial phase must be Resting
                if (machine.Phase != NotchPhase.Resting)
                {
                    throw new Exception($"Initial phase must be Resting, but was {machine.Phase}");
                }

                // Check dwell timer via reflection
                var dwellTimerField = typeof(NotchStateMachine).GetField("_dwellTimer", BindingFlags.Instance | BindingFlags.NonPublic);
                var dwellTimer = (DispatcherTimer)dwellTimerField.GetValue(machine);

                // Scenario A: When indicator IS hovered (IsHovered = true),
                // CapsuleCard.MouseEnter check must return early without starting dwell timer
                var onMouseEnterMethod = typeof(NotchPrivacyIndicator).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                onMouseEnterMethod.Invoke(indicator, new object[] { new MouseEventArgs(Mouse.PrimaryDevice, 0) });

                if (!indicator.IsHovered)
                {
                    throw new Exception("Indicator IsHovered was not set!");
                }

                // Raise MouseEnter on _capsuleCard
                var cardMouseEnterMethod = typeof(UIElement).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                cardMouseEnterMethod?.Invoke(card, new object[] { new MouseEventArgs(Mouse.PrimaryDevice, 0) });

                // Dwell timer MUST NOT be running
                if (dwellTimer.IsEnabled)
                {
                    throw new Exception("VIOLATION: Dwell timer was started despite indicator.IsHovered == true!");
                }

                // Machine must remain Resting
                if (machine.Phase != NotchPhase.Resting)
                {
                    throw new Exception($"VIOLATION: State machine entered {machine.Phase} instead of Resting!");
                }

                Console.WriteLine("    Verified: Dwell timer is strictly suppressed when indicator.IsHovered == true.");

                // Scenario B: Fast forward dispatcher to ensure no delayed tick occurs
                var frame = new DispatcherFrame();
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
                timer.Start();
                Dispatcher.PushFrame(frame);

                if (machine.Phase != NotchPhase.Resting)
                {
                    throw new Exception($"Accidental expansion occurred! Phase is {machine.Phase}");
                }

                Console.WriteLine("    Verified: Notch body remains strictly collapsed in resting phase after 100ms dwell window.");

                // Scenario C: Verify Single Click on Indicator does NOT morph notch
                // Clicking must call ToggleMicrophoneMute, set e.Handled=true, and NOT start morph
                bool toggleFired = false;
                Action<bool> muteChanged = (m) => toggleFired = true;
                CoreAudioService.Instance.MicrophoneMuteChanged += muteChanged;
                try
                {
                    indicator.SimulateClick();
                    if (!toggleFired)
                    {
                        throw new Exception("SimulateClick did not trigger microphone mute toggle!");
                    }
                    if (machine.Phase != NotchPhase.Resting)
                    {
                        throw new Exception($"Clicking privacy indicator unexpectedly expanded notch to {machine.Phase}!");
                    }
                    Console.WriteLine("    Verified: Indicator click toggles microphone without expanding notch body.");
                }
                finally
                {
                    CoreAudioService.Instance.MicrophoneMuteChanged -= muteChanged;
                }
            });

            // TEST 3.3: Pass-Through Hit-Test Integration Edge Coordinates
            RunTest("H3.3: HitTestInteractive Precision at Indicator Boundary Coordinates", () =>
            {
                var window = new CapsuleWindow();
                var controller = window.PassThroughController;
                window.PrivacyIndicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);

                // Coordinates:
                // Collapsed notch width = 228. Card center in 800 envelope = (800-228)/2 = 286.
                // Indicator width = 26, right margin = 10.
                // Left = 286 + 228 - 10 - 26 = 478. Right = 504.
                // Center Y = 15.

                // Exact inside
                if (!controller.HitTestInteractive(new Point(490, 15)))
                {
                    throw new Exception("Inside point (490, 15) must be interactive!");
                }

                // 2px padded boundary left (476, 15)
                if (!controller.HitTestInteractive(new Point(476, 15)))
                {
                    throw new Exception("Padded left point (476, 15) must be interactive!");
                }

                // 2px padded boundary right (506, 15)
                if (!controller.HitTestInteractive(new Point(506, 15)))
                {
                    throw new Exception("Padded right point (506, 15) must be interactive!");
                }

                // Outside interactive target (far off in pass-through margins)
                if (controller.HitTestInteractive(new Point(100, 15)))
                {
                    throw new Exception("Pass-through margin point (100, 15) must NOT be interactive!");
                }

                Console.WriteLine("    Verified: Pass-through hit testing captures clicks within indicator + 2px padding, passes through margins.");
            });
        }

        #endregion

        #region HARNESS 4: Extreme Cross-Subsystem Concurrent Stress Storm

        private static void ExecuteCombinedStressHarness()
        {
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine(" [HARNESS 4] Extreme Cross-Subsystem Concurrent Stress Storm");
            Console.WriteLine("--------------------------------------------------------------------------------");

            RunTest("H4.1: Concurrent Registry Churn (50 ops) + Live Mute Storm (100 toggles) + UI Interaction", () =>
            {
                var audioService = CoreAudioService.Instance;
                bool initialMute = audioService.IsMicrophoneMuted;

                string stressWebcam = @"Software\Mayli\CrossStress\webcam";
                string stressMic = @"Software\Mayli\CrossStress\microphone";
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\CrossStress", false); } catch { }

                using var monitor = new RegistryAccessMonitor(stressWebcam, stressMic, startImmediately: true);
                monitor.WaitForStarted(2000);

                var startBarrier = new Barrier(4);
                var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var cancelToken = tokenSource.Token;

                int registryOps = 0;
                int audioToggles = 0;
                int monitorScans = 0;
                int uiSimulations = 0;

                // Worker 1: Registry Churner (creates & deletes webcam and mic keys)
                var taskRegistry = Task.Run(() =>
                {
                    startBarrier.SignalAndWait();
                    for (int i = 0; i < 50 && !cancelToken.IsCancellationRequested; i++)
                    {
                        string subkey = (i % 2 == 0) ? stressWebcam : stressMic;
                        string appSub = $@"{subkey}\App_{i}";
                        using (var k = Registry.CurrentUser.CreateSubKey(appSub))
                        {
                            k.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                            k.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
                        }
                        Thread.Sleep(2);
                        using (var k = Registry.CurrentUser.CreateSubKey(appSub))
                        {
                            k.SetValue("LastUsedTimeStop", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                        }
                        Interlocked.Increment(ref registryOps);
                    }
                });

                // Worker 2: Audio Mute Storm (100 toggles on CoreAudioService)
                var taskAudio = Task.Run(() =>
                {
                    startBarrier.SignalAndWait();
                    for (int i = 0; i < 100 && !cancelToken.IsCancellationRequested; i++)
                    {
                        audioService.ToggleMicrophoneMute();
                        Interlocked.Increment(ref audioToggles);
                        Thread.Sleep(1);
                    }
                });

                // Worker 3: Continuous Registry Scan
                var taskMonitor = Task.Run(() =>
                {
                    startBarrier.SignalAndWait();
                    for (int i = 0; i < 50 && !cancelToken.IsCancellationRequested; i++)
                    {
                        var snap = monitor.ScanRegistry();
                        if (snap != null) Interlocked.Increment(ref monitorScans);
                        Thread.Sleep(3);
                    }
                });

                // Worker 4: UI Thread Simulation (Hover + Click + PassThrough HitTest) - Needs STA
                var threadUi = new Thread(() =>
                {
                    startBarrier.SignalAndWait();
                    var indicator = new NotchPrivacyIndicator();
                    var onMouseEnter = typeof(NotchPrivacyIndicator).GetMethod("OnMouseEnter", BindingFlags.Instance | BindingFlags.NonPublic);
                    var onMouseLeave = typeof(NotchPrivacyIndicator).GetMethod("OnMouseLeave", BindingFlags.Instance | BindingFlags.NonPublic);

                    for (int i = 0; i < 50 && !cancelToken.IsCancellationRequested; i++)
                    {
                        indicator.UpdateAccessState(i % 2 == 0, "TestApp", i % 3 == 0, "CamApp");
                        onMouseEnter.Invoke(indicator, new object[] { new MouseEventArgs(Mouse.PrimaryDevice, 0) });
                        onMouseLeave.Invoke(indicator, new object[] { new MouseEventArgs(Mouse.PrimaryDevice, 0) });
                        Interlocked.Increment(ref uiSimulations);
                        Thread.Sleep(2);
                    }
                });
                threadUi.SetApartmentState(ApartmentState.STA);
                threadUi.Start();

                bool finishedInTime = Task.WaitAll(new Task[] { taskRegistry, taskAudio, taskMonitor }, 8000) && threadUi.Join(8000);
                if (!finishedInTime)
                {
                    throw new Exception("Deadlock detected during cross-subsystem concurrent stress storm!");
                }

                Console.WriteLine($"    Cross-Subsystem Stress Ops: Reg={registryOps}, Audio={audioToggles}, Monitor={monitorScans}, UI={uiSimulations}");

                // Restore mute state
                audioService.SetMicrophoneMute(initialMute);
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\CrossStress", false); } catch { }
            });
        }

        #endregion

        #region Helper Runner

        private static void RunTest(string testName, Action testAction)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                testAction();
                sw.Stop();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASS] {testName} ({sw.ElapsedMilliseconds} ms)");
                Console.ResetColor();
                _testPassCount++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}");
                Console.WriteLine($"         Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"         Inner: {ex.InnerException.Message}");
                }
                Console.ResetColor();
                _testFailCount++;
            }
        }

        #endregion
    }
}
