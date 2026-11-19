using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;

namespace Mayli.AdversarialTests.Challenger2
{
    public static class Program
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;
        private static readonly List<string> _findings = new();

        [STAThread]
        public static int Main(string[] args)
        {
            Console.WriteLine("================================================================================");
            Console.WriteLine(" Mayli Empirical Adversarial Challenge Test Suite (Challenger 2)");
            Console.WriteLine(" Focus: Multi-App Concurrency, Boundary Hit-Testing & Dead Process Filtering");
            Console.WriteLine("================================================================================");

            int exitCode = 0;
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                var thread = new Thread(() =>
                {
                    exitCode = RunAllSuites();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            else
            {
                exitCode = RunAllSuites();
            }

            return exitCode;
        }

        private static int RunAllSuites()
        {
            _passedCount = 0;
            _failedCount = 0;
            _findings.Clear();

            Console.WriteLine("\n>>> SUITE 1: Multi-Application Concurrency & Staggered Termination");
            RunTest("TC-ADV2-001: Simultaneous Camera (Zoom) + Mic (Discord) Dual-Dot Pill & Dual Tooltip", Test_SimultaneousCamAndMic_DualDotPillAndTooltip);
            RunTest("TC-ADV2-002: Staggered Termination: Camera Stops First, Mic Continues Seamlessly", Test_StaggeredTermination_CameraStops_MicContinues);
            RunTest("TC-ADV2-003: Staggered Termination: Mic Stops First, Camera Continues Seamlessly", Test_StaggeredTermination_MicStops_CameraContinues);
            RunTest("TC-ADV2-004: Staggered Termination: Auto-Clear Triggered on Final Access Release", Test_StaggeredTermination_AutoClearOnFinalRelease);
            RunTest("TC-ADV2-005: Empirical Inspection: Tooltip Content Cleanup on Final Device Release", Test_TooltipContentCleanupOnFinalRelease);
            RunTest("TC-ADV2-006: Mute State Preserved Across Staggered Device Transitions", Test_MuteStatePreservedAcrossStaggeredTransitions);
            RunTest("TC-ADV2-007: Rapid Access Interleaving & State Inversion Stress (10 Cycles)", Test_RapidAccessInterleavingAndFlipping);

            Console.WriteLine("\n>>> SUITE 2: Boundary Coordinates Hit-Testing (NotchMousePassThroughController)");
            RunTest("TC-ADV2-008: Hit-Test Left Boundary: 1px Inside (-1), On Boundary (-2), 1px Outside (-3)", Test_HitTest_LeftBoundary_1pxInside_OnBoundary_1pxOutside);
            RunTest("TC-ADV2-009: Hit-Test Right Boundary: 1px Inside (27), On Boundary (28), 1px Outside (29)", Test_HitTest_RightBoundary_1pxInside_OnBoundary_1pxOutside);
            RunTest("TC-ADV2-010: Hit-Test Top Boundary: 1px Inside (-1), On Boundary (-2), 1px Outside (-3)", Test_HitTest_TopBoundary_1pxInside_OnBoundary_1pxOutside);
            RunTest("TC-ADV2-011: Hit-Test Bottom Boundary: 1px Inside (25), On Boundary (26), 1px Outside (27)", Test_HitTest_BottomBoundary_1pxInside_OnBoundary_1pxOutside);
            RunTest("TC-ADV2-012: Hit-Test Dual-Dot Pill Mode Expanded Target Box Boundaries (34px Width)", Test_HitTest_DualDotPill_ExpandedHitBoxBoundaries);
            RunTest("TC-ADV2-013: Hit-Test Collapsed Indicator Passes All Clicks Through (Return False)", Test_HitTest_CollapsedIndicator_ReturnsFalse);
            RunTest("TC-ADV2-014: Hit-Test Margin Pass-Through Outside Window Silhouette", Test_HitTest_MarginPassThroughOutsideSilhouette);

            Console.WriteLine("\n>>> SUITE 3: Dead Process Filtering & Ghost Indicator Suppression");
            RunTest("TC-ADV2-015: ProcessResolver Detects Terminated/Non-Existent Process as Dead", Test_ProcessResolver_DeadProcessReturnsFalse);
            RunTest("TC-ADV2-016: Real Spawned & Violently Killed Process Filtered From Liveness Check", Test_RealSpawnedAndKilledProcess_FilteredFromRegistry);
            RunTest("TC-ADV2-017: RegistryAccessMonitor Discards Dead Process Orphaned Registry Key", Test_RegistryAccessMonitor_FiltersDeadProcessGhostIndicator);
            RunTest("TC-ADV2-018: Multiple Dead Processes Mixed With One Live Process (Only Live Reported)", Test_MultipleDeadProcessesWithOneLiveProcess_OnlyLiveReported);
            RunTest("TC-ADV2-019: Missing Packaged App Graceful Fallback Resolution", Test_MissingPackagedApp_GracefulFallback);

            Console.WriteLine("\n================================================================================");
            Console.WriteLine($" Adversarial Challenge Results: {_passedCount} PASSED, {_failedCount} FAILED");
            if (_findings.Count > 0)
            {
                Console.WriteLine("\n Empirical Findings Discovered:");
                foreach (var f in _findings)
                {
                    Console.WriteLine($"   * {f}");
                }
            }
            string verdict = _failedCount == 0 ? "CONFIRM_CORRECT" : "FAIL";
            Console.WriteLine($" Final Verdict: {verdict}");
            Console.WriteLine("================================================================================");

            return _failedCount == 0 ? 0 : 1;
        }

        private static void RunTest(string testName, Action testAction)
        {
            try
            {
                testAction();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  [PASS] {testName}");
                Console.ResetColor();
                _passedCount++;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [FAIL] {testName}: {ex.Message}");
                Console.ResetColor();
                _failedCount++;
                _findings.Add($"{testName}: {ex.Message}");
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
                throw new Exception($"Assertion failed (expected true): {message}");
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception($"Assertion failed (expected false): {message}");
            }
        }

        // =========================================================================
        // SUITE 1 IMPLEMENTATION: Concurrency & Staggered Termination
        // =========================================================================

        private static void Test_SimultaneousCamAndMic_DualDotPillAndTooltip()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "Zoom");

            AssertEqual(Visibility.Visible, indicator.Visibility, "Indicator must be Visible in dual active state");
            AssertEqual(34.0, indicator.Width, "Indicator touch target must expand to 34.0 DIPs for dual pill");
            AssertEqual(32.0, indicator.PillHost.Width, "Pill container width must be 32.0 DIPs");
            AssertEqual(16.0, indicator.PillHost.Height, "Pill container height must be 16.0 DIPs");
            AssertEqual(8.0, indicator.PillHost.CornerRadius.TopLeft, "Pill corner radius must be 8.0");

            AssertEqual(Visibility.Visible, ((FrameworkElement)indicator.CamDot.Parent).Visibility, "Camera dot container must be Visible");
            AssertEqual(Visibility.Visible, ((FrameworkElement)indicator.MicDot.Parent).Visibility, "Microphone dot container must be Visible");
            AssertEqual(6.0, indicator.CamDot.Width, "Camera dot width in pill must be 6.0 DIPs");
            AssertEqual(6.0, indicator.MicDot.Width, "Microphone dot width in pill must be 6.0 DIPs");

            string tooltip = indicator.IndicatorToolTip.Content?.ToString() ?? string.Empty;
            AssertTrue(tooltip.Contains("Camera: Zoom"), "Tooltip must list Camera app name 'Zoom'");
            AssertTrue(tooltip.Contains("Microphone: Discord"), "Tooltip must list Microphone app name 'Discord'");
            AssertTrue(tooltip.Contains("Active - Click to Mute"), "Tooltip must include click-to-mute hint");
        }

        private static void Test_StaggeredTermination_CameraStops_MicContinues()
        {
            var indicator = new NotchPrivacyIndicator();
            // Start simultaneous
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "Zoom");

            // Step 1: Camera terminates, mic remains active
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);

            AssertEqual(Visibility.Visible, indicator.Visibility, "Indicator must remain Visible when mic continues");
            AssertEqual(26.0, indicator.Width, "Width must collapse back to single dot 26.0 DIPs");
            AssertEqual(Visibility.Collapsed, ((FrameworkElement)indicator.CamDot.Parent).Visibility, "Camera dot container must be Collapsed");
            AssertEqual(Visibility.Visible, ((FrameworkElement)indicator.MicDot.Parent).Visibility, "Mic dot container must remain Visible");
            AssertEqual(7.5, indicator.MicDot.Width, "Mic dot must return to standard 7.5 DIPs");

            string tooltip = indicator.IndicatorToolTip.Content?.ToString() ?? string.Empty;
            AssertTrue(tooltip.Contains("Discord"), "Tooltip must display Discord");
            AssertFalse(tooltip.Contains("Zoom"), "Tooltip must no longer display Zoom");
            AssertFalse(tooltip.Contains("Camera:"), "Tooltip must not contain Camera section");
        }

        private static void Test_StaggeredTermination_MicStops_CameraContinues()
        {
            var indicator = new NotchPrivacyIndicator();
            // Start simultaneous
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "Zoom");

            // Step 1: Mic terminates, camera remains active
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: true, camAppName: "Zoom");

            AssertEqual(Visibility.Visible, indicator.Visibility, "Indicator must remain Visible when camera continues");
            AssertEqual(26.0, indicator.Width, "Width must collapse back to single dot 26.0 DIPs");
            AssertEqual(Visibility.Collapsed, ((FrameworkElement)indicator.MicDot.Parent).Visibility, "Mic dot container must be Collapsed");
            AssertEqual(Visibility.Visible, ((FrameworkElement)indicator.CamDot.Parent).Visibility, "Camera dot container must remain Visible");
            AssertEqual(7.5, indicator.CamDot.Width, "Camera dot must return to standard 7.5 DIPs");

            string tooltip = indicator.IndicatorToolTip.Content?.ToString() ?? string.Empty;
            AssertTrue(tooltip.Contains("Zoom"), "Tooltip must display Zoom");
            AssertFalse(tooltip.Contains("Discord"), "Tooltip must no longer display Discord");
            AssertFalse(tooltip.Contains("Microphone:"), "Tooltip must not contain Microphone section");
        }

        private static void Test_StaggeredTermination_AutoClearOnFinalRelease()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "Zoom");

            // Staggered step 1: Camera releases
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);
            AssertTrue(indicator.IsMicrophoneInUse, "Mic in use true");
            AssertFalse(indicator.IsCameraInUse, "Camera in use false");

            // Staggered step 2: Mic releases
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertFalse(indicator.IsMicrophoneInUse, "Mic in use false");
            AssertFalse(indicator.IsCameraInUse, "Camera in use false");
            AssertEqual(string.Empty, indicator.MicrophoneAppName, "MicrophoneAppName should be cleared");
            AssertEqual(string.Empty, indicator.CameraAppName, "CameraAppName should be cleared");
        }

        private static void Test_TooltipContentCleanupOnFinalRelease()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);
            string activeTooltip = indicator.IndicatorToolTip.Content?.ToString() ?? string.Empty;
            AssertTrue(activeTooltip.Contains("Discord"), "Active tooltip must show Discord");

            // All devices release access
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);

            // EMPIRICAL OBSERVATION:
            // In NotchPrivacyIndicator.ApplyVisualState():
            // if (!anyActive) { TriggerDisappearanceAnimation(); return; }
            // Because line 257 returns early before calling UpdateToolTip(), indicator.IndicatorToolTip.Content
            // is NOT cleared to null upon device release, retaining stale application text.
            object currentContent = indicator.IndicatorToolTip.Content;
            if (currentContent != null)
            {
                throw new Exception($"Stale tooltip retained upon final device release! Expected null, but Content was: '{currentContent}'");
            }
        }

        private static void Test_MuteStatePreservedAcrossStaggeredTransitions()
        {
            var indicator = new NotchPrivacyIndicator();
            // 1. Both active
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "Zoom");

            // 2. User mutes microphone
            indicator.UpdateMuteState(true);
            AssertTrue(indicator.IsMicrophoneMuted, "Indicator must be muted");
            AssertEqual(Visibility.Visible, indicator.MicSlash.Visibility, "Slash line must be visible in dual pill");

            // 3. Camera terminates while mic continues muted
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);
            AssertTrue(indicator.IsMicrophoneMuted, "Microphone mute state must survive camera release");
            AssertEqual(Visibility.Visible, indicator.MicSlash.Visibility, "Slash line must remain visible in single dot mode");

            var fill = indicator.MicDot.Fill as System.Windows.Media.SolidColorBrush;
            AssertEqual(NotchPrivacyIndicator.MutedRed, fill.Color, "Dot must retain warning muted red color");

            string tooltip = indicator.IndicatorToolTip.Content?.ToString() ?? string.Empty;
            AssertTrue(tooltip.Contains("Status: Muted (Click to Unmute)"), "Tooltip must show muted status");
        }

        private static void Test_RapidAccessInterleavingAndFlipping()
        {
            var indicator = new NotchPrivacyIndicator();
            // Rapid state flips across 10 iterations
            for (int i = 0; i < 10; i++)
            {
                indicator.UpdateAccessState(true, "Discord", false, null); // Mic only
                indicator.UpdateAccessState(true, "Discord", true, "Zoom"); // Both
                indicator.UpdateAccessState(false, null, true, "Zoom"); // Cam only
                indicator.UpdateAccessState(false, null, false, null); // None
            }

            // Settle on final state: Both active
            indicator.UpdateAccessState(true, "Discord", true, "Zoom");
            AssertEqual(Visibility.Visible, indicator.Visibility, "Must be Visible after rapid interleaving");
            AssertEqual(34.0, indicator.Width, "Must cleanly settle on 34.0 pill width");
            AssertEqual(6.0, indicator.CamDot.Width, "Camera dot must be 6.0");
            AssertEqual(6.0, indicator.MicDot.Width, "Mic dot must be 6.0");
        }

        // =========================================================================
        // SUITE 2 IMPLEMENTATION: Boundary Coordinates Hit-Testing
        // =========================================================================

        private static void Test_HitTest_LeftBoundary_1pxInside_OnBoundary_1pxOutside()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            window.PrivacyIndicator.UpdateAccessState(true, "Discord", false, null);

            // Isolate indicator target box
            window.CapsuleCard.IsHitTestVisible = false;

            // Indicator geometry:
            // Collapsed notch width = 228, centered in 800 window -> left offset = 286.
            // Indicator right margin = 10, width = 26.
            // indLeft = 286 + 228 - 10 - 26 = 478.0.
            // Padded left boundary is indLeft - 2 = 476.0.
            // Y center = (35 - 24) / 2 + 12 = 17.5.

            // 1. 1px Inside padded left boundary: X = 477.0
            bool inside = controller.HitTestInteractive(new Point(477.0, 17.5));
            AssertTrue(inside, "1px inside left boundary (X=477, pt.X=-1) must hit");

            // 2. Exactly on padded left boundary: X = 476.0
            bool onBoundary = controller.HitTestInteractive(new Point(476.0, 17.5));
            AssertTrue(onBoundary, "Exactly on left boundary (X=476, pt.X=-2) must hit");

            // 3. 1px Outside padded left boundary: X = 475.0
            bool outside = controller.HitTestInteractive(new Point(475.0, 17.5));
            AssertFalse(outside, "1px outside left boundary (X=475, pt.X=-3) must NOT hit indicator target box");
        }

        private static void Test_HitTest_RightBoundary_1pxInside_OnBoundary_1pxOutside()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            window.PrivacyIndicator.UpdateAccessState(true, "Discord", false, null);
            window.CapsuleCard.IsHitTestVisible = false;

            // indLeft = 478.0, indW = 26.0.
            // Padded right boundary is indLeft + indW + 2 = 478 + 26 + 2 = 506.0.

            // 1. 1px Inside padded right boundary: X = 505.0
            bool inside = controller.HitTestInteractive(new Point(505.0, 17.5));
            AssertTrue(inside, "1px inside right boundary (X=505, pt.X=27) must hit");

            // 2. Exactly on padded right boundary: X = 506.0
            bool onBoundary = controller.HitTestInteractive(new Point(506.0, 17.5));
            AssertTrue(onBoundary, "Exactly on right boundary (X=506, pt.X=28) must hit");

            // 3. 1px Outside padded right boundary: X = 507.0
            bool outside = controller.HitTestInteractive(new Point(507.0, 17.5));
            AssertFalse(outside, "1px outside right boundary (X=507, pt.X=29) must NOT hit indicator target box");
        }

        private static void Test_HitTest_TopBoundary_1pxInside_OnBoundary_1pxOutside()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            window.PrivacyIndicator.UpdateAccessState(true, "Discord", false, null);
            window.CapsuleCard.IsHitTestVisible = false;

            // indTop = (35 - 24) / 2 = 5.5.
            // Padded top boundary is indTop - 2 = 3.5.
            // X center = 491.0.

            // 1. 1px Inside padded top boundary: Y = 4.5
            bool inside = controller.HitTestInteractive(new Point(491.0, 4.5));
            AssertTrue(inside, "1px inside top boundary (Y=4.5, pt.Y=-1) must hit");

            // 2. Exactly on padded top boundary: Y = 3.5
            bool onBoundary = controller.HitTestInteractive(new Point(491.0, 3.5));
            AssertTrue(onBoundary, "Exactly on top boundary (Y=3.5, pt.Y=-2) must hit");

            // 3. 1px Outside padded top boundary: Y = 2.5
            bool outside = controller.HitTestInteractive(new Point(491.0, 2.5));
            AssertFalse(outside, "1px outside top boundary (Y=2.5, pt.Y=-3) must NOT hit indicator target box");
        }

        private static void Test_HitTest_BottomBoundary_1pxInside_OnBoundary_1pxOutside()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            window.PrivacyIndicator.UpdateAccessState(true, "Discord", false, null);
            window.CapsuleCard.IsHitTestVisible = false;

            // indTop = 5.5, indH = 24.0.
            // Padded bottom boundary is indTop + indH + 2 = 5.5 + 24 + 2 = 31.5.

            // 1. 1px Inside padded bottom boundary: Y = 30.5
            bool inside = controller.HitTestInteractive(new Point(491.0, 30.5));
            AssertTrue(inside, "1px inside bottom boundary (Y=30.5, pt.Y=25) must hit");

            // 2. Exactly on padded bottom boundary: Y = 31.5
            bool onBoundary = controller.HitTestInteractive(new Point(491.0, 31.5));
            AssertTrue(onBoundary, "Exactly on bottom boundary (Y=31.5, pt.Y=26) must hit");

            // 3. 1px Outside padded bottom boundary: Y = 32.5
            bool outside = controller.HitTestInteractive(new Point(491.0, 32.5));
            AssertFalse(outside, "1px outside bottom boundary (Y=32.5, pt.Y=27) must NOT hit indicator target box");
        }

        private static void Test_HitTest_DualDotPill_ExpandedHitBoxBoundaries()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            // Both active -> dual dot pill (width = 34.0)
            window.PrivacyIndicator.UpdateAccessState(true, "Discord", true, "Zoom");
            window.CapsuleCard.IsHitTestVisible = false;

            // indW = 34.0.
            // indLeft = 286 + 228 - 10 - 34 = 470.0.
            // Padded left = 470 - 2 = 468.0.
            // Padded right = 470 + 34 + 2 = 506.0.

            // Left boundary
            AssertTrue(controller.HitTestInteractive(new Point(469.0, 17.5)), "Dual-pill 1px inside left must hit");
            AssertTrue(controller.HitTestInteractive(new Point(468.0, 17.5)), "Dual-pill on left boundary must hit");
            AssertFalse(controller.HitTestInteractive(new Point(467.0, 17.5)), "Dual-pill 1px outside left must NOT hit target box");

            // Right boundary
            AssertTrue(controller.HitTestInteractive(new Point(505.0, 17.5)), "Dual-pill 1px inside right must hit");
            AssertTrue(controller.HitTestInteractive(new Point(506.0, 17.5)), "Dual-pill on right boundary must hit");
            AssertFalse(controller.HitTestInteractive(new Point(507.0, 17.5)), "Dual-pill 1px outside right must NOT hit target box");
        }

        private static void Test_HitTest_CollapsedIndicator_ReturnsFalse()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            window.PrivacyIndicator.Visibility = Visibility.Collapsed;
            window.CapsuleCard.IsHitTestVisible = false;

            // Center of where the indicator was:
            bool hit = controller.HitTestInteractive(new Point(491.0, 17.5));
            AssertFalse(hit, "Collapsed indicator must return false for hit testing to allow pass-through");
        }

        private static void Test_HitTest_MarginPassThroughOutsideSilhouette()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);
            window.PrivacyIndicator.UpdateAccessState(true, "Discord", false, null);

            // Points far outside the notch silhouette (in the transparent envelope margin):
            // Window envelope is 800x388. Card is centered at 286..514.
            // Point X=100, Y=15 is in the transparent margin to the left of the notch.
            bool hitLeftMargin = controller.HitTestInteractive(new Point(100.0, 15.0));
            AssertFalse(hitLeftMargin, "Left transparent margin (100, 15) must pass through (false)");

            // Point X=700, Y=15 is in the transparent margin to the right of the notch.
            bool hitRightMargin = controller.HitTestInteractive(new Point(700.0, 15.0));
            AssertFalse(hitRightMargin, "Right transparent margin (700, 15) must pass through (false)");

            // Point outside window envelope bounds entirely
            bool hitNegative = controller.HitTestInteractive(new Point(-20.0, -20.0));
            AssertFalse(hitNegative, "Negative coordinates (-20, -20) must pass through (false)");
        }

        // =========================================================================
        // SUITE 3 IMPLEMENTATION: Dead Process Filtering & Ghost Suppression
        // =========================================================================

        private static void Test_ProcessResolver_DeadProcessReturnsFalse()
        {
            ProcessResolver.AllowSimulatedProcesses = false;
            try
            {
                // Non-existent PID/exe
                bool alive = ProcessResolver.IsProcessAlive(@"C:\Program Files\FakeApp_DeadPID_99999\NonExistentApp.exe");
                AssertFalse(alive, "ProcessResolver must report non-existent process as dead");
            }
            finally
            {
                ProcessResolver.AllowSimulatedProcesses = true;
            }
        }

        private static void Test_RealSpawnedAndKilledProcess_FilteredFromRegistry()
        {
            // Create a uniquely named dummy exe in Temp directory
            string tempDir = Path.GetTempPath();
            string uniqueExeName = $"DeadProcTest_{Guid.NewGuid():N}.exe";
            string uniqueExePath = Path.Combine(tempDir, uniqueExeName);

            // Copy cmd.exe to create a real runnable PE with this unique name
            string sysCmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            File.Copy(sysCmd, uniqueExePath, overwrite: true);

            Process childProc = null;
            try
            {
                // 1. Start child process
                var psi = new ProcessStartInfo
                {
                    FileName = uniqueExePath,
                    Arguments = "/c \"timeout /t 30 /nobreak\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                childProc = Process.Start(psi);
                AssertTrue(childProc != null && !childProc.HasExited, "Child process must successfully start");

                // 2. While running, ProcessResolver should detect it alive
                ProcessResolver.AllowSimulatedProcesses = false;
                bool aliveWhileRunning = ProcessResolver.IsProcessAlive(uniqueExePath);
                AssertTrue(aliveWhileRunning, "ProcessResolver must detect uniquely spawned process while alive");

                // 3. Violently kill the child process
                childProc.Kill();
                childProc.WaitForExit(3000);
                AssertTrue(childProc.HasExited, "Child process must have terminated");

                // Allow OS process table to settle
                Thread.Sleep(50);

                // 4. Now verify ProcessResolver detects it as DEAD
                bool aliveAfterKill = ProcessResolver.IsProcessAlive(uniqueExePath);
                AssertFalse(aliveAfterKill, "ProcessResolver must detect terminated child process as dead");
            }
            finally
            {
                ProcessResolver.AllowSimulatedProcesses = true;
                if (childProc != null)
                {
                    try { childProc.Dispose(); } catch { }
                }
                try { File.Delete(uniqueExePath); } catch { }
            }
        }

        private static void Test_RegistryAccessMonitor_FiltersDeadProcessGhostIndicator()
        {
            string testRootWebcam = @"Software\Mayli\AdversarialTest2\DeadProc\webcam";
            string testRootMic = @"Software\Mayli\AdversarialTest2\DeadProc\microphone";

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdversarialTest2", false); } catch { }

            // Write an orphaned registry entry for a killed/dead app
            string killedAppKey = $@"{testRootWebcam}\NonPackaged\C:#Program Files#DeadConference#DeadZoomMeeting.exe";
            using (var key = Registry.CurrentUser.CreateSubKey(killedAppKey))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord); // Looks active in registry!
            }

            ProcessResolver.AllowSimulatedProcesses = false;
            try
            {
                using var monitor = new RegistryAccessMonitor(testRootWebcam, testRootMic, startImmediately: false);
                var snapshot = monitor.ScanRegistry();

                AssertFalse(snapshot.IsCameraInUse, "Dead process must be filtered out: IsCameraInUse must be false");
                AssertEqual(string.Empty, snapshot.CameraAppName, "CameraAppName must be empty (no ghost indicator)");
                AssertEqual(0, snapshot.AllCameraApps.Count, "AllCameraApps must have 0 entries");
            }
            finally
            {
                ProcessResolver.AllowSimulatedProcesses = true;
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdversarialTest2", false); } catch { }
            }
        }

        private static void Test_MultipleDeadProcessesWithOneLiveProcess_OnlyLiveReported()
        {
            string testRootWebcam = @"Software\Mayli\AdversarialTest2\MixedProc\webcam";
            string testRootMic = @"Software\Mayli\AdversarialTest2\MixedProc\microphone";

            try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdversarialTest2", false); } catch { }

            // 1. Two dead processes
            string deadKey1 = $@"{testRootMic}\NonPackaged\C:#Apps#GhostRecorder#Ghost1.exe";
            using (var key = Registry.CurrentUser.CreateSubKey(deadKey1))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
            }
            string deadKey2 = $@"{testRootMic}\NonPackaged\C:#Apps#GhostMeeting#Ghost2.exe";
            using (var key = Registry.CurrentUser.CreateSubKey(deadKey2))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
            }

            // 2. One live process (the current running test process)
            string currentExe = Process.GetCurrentProcess().MainModule?.FileName;
            AssertTrue(!string.IsNullOrWhiteSpace(currentExe), "Current process main module path must be available");
            string encodedLivePath = currentExe.Replace('\\', '#').Replace(":#", "#");
            encodedLivePath = currentExe[0] + ":#" + currentExe.Substring(3).Replace('\\', '#');

            string liveKey = $@"{testRootMic}\NonPackaged\{encodedLivePath}";
            using (var key = Registry.CurrentUser.CreateSubKey(liveKey))
            {
                key.SetValue("LastUsedTimeStart", DateTime.UtcNow.ToFileTime(), RegistryValueKind.QWord);
                key.SetValue("LastUsedTimeStop", 0L, RegistryValueKind.QWord);
            }

            ProcessResolver.AllowSimulatedProcesses = false;
            try
            {
                using var monitor = new RegistryAccessMonitor(testRootWebcam, testRootMic, startImmediately: false);
                var snapshot = monitor.ScanRegistry();

                AssertTrue(snapshot.IsMicrophoneInUse, "Microphone must be in use by live process");
                AssertEqual(1, snapshot.AllMicrophoneApps.Count, "Exactly 1 live app must be reported; 2 dead apps must be filtered out");
                AssertFalse(snapshot.MicrophoneAppName.Contains("Ghost1"), "Ghost1 must NOT appear");
                AssertFalse(snapshot.MicrophoneAppName.Contains("Ghost2"), "Ghost2 must NOT appear");
            }
            finally
            {
                ProcessResolver.AllowSimulatedProcesses = true;
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Mayli\AdversarialTest2", false); } catch { }
            }
        }

        private static void Test_MissingPackagedApp_GracefulFallback()
        {
            // Non-installed package family name
            string fakePkg = "NonExistentVendor.GhostCameraApp_1234567890abc";
            string resolved = ProcessResolver.ResolvePackagedName(fakePkg);
            AssertFalse(string.IsNullOrWhiteSpace(resolved), "Fallback name must not be empty");
            AssertTrue(resolved.Contains("Ghost Camera App") || resolved.Contains("GhostCameraApp"),
                $"Fallback must safely extract package family identity without throwing, got '{resolved}'");
        }
    }
}
