using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using WindowsBuddy.BuddyCapsule.Core;
using WindowsBuddy.BuddyCapsule.UI;
using WindowsBuddy.Services;

namespace Mayli.UnitTests
{
    public class NotchPrivacyIndicatorUiTests
    {
        private static int _passedCount = 0;
        private static int _failedCount = 0;

        [STAThread]
        public static int Main(string[] args)
        {
            return RunTests();
        }

        public static int RunTests()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" Starting Notch Privacy Indicator UI Unit Tests");
            Console.WriteLine("=================================================");

            int exitCode = 0;
            // Ensure tests execute in STA thread
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                var thread = new Thread(() =>
                {
                    exitCode = ExecuteAll();
                });
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                thread.Join();
            }
            else
            {
                exitCode = ExecuteAll();
            }

            return exitCode;
        }

        private static int ExecuteAll()
        {
            _passedCount = 0;
            _failedCount = 0;

            RunTest("Test_InitialState_CollapsedAndZeroOpacity", Test_InitialState_CollapsedAndZeroOpacity);
            RunTest("Test_MicrophoneOnly_OrangeGlowAndDropShadow", Test_MicrophoneOnly_OrangeGlowAndDropShadow);
            RunTest("Test_CameraOnly_GreenGlow", Test_CameraOnly_GreenGlow);
            RunTest("Test_DualDotPill_BothActive", Test_DualDotPill_BothActive);
            RunTest("Test_MicrophoneMute_SlashLineAndWarningColorShift", Test_MicrophoneMute_SlashLineAndWarningColorShift);
            RunTest("Test_ClickToMute_InvokesCoreAudioToggleAndEvent", Test_ClickToMute_InvokesCoreAudioToggleAndEvent);
            RunTest("Test_HoverDwellSuppression_IsHovered", Test_HoverDwellSuppression_IsHovered);
            RunTest("Test_AutoClear_TriggeredWhenAccessEnds", Test_AutoClear_TriggeredWhenAccessEnds);
            RunTest("Test_AutoClear_ClearsToolTipContent", Test_AutoClear_ClearsToolTipContent);
            RunTest("Test_PassThroughHitTesting_BoundaryPadding", Test_PassThroughHitTesting_BoundaryPadding);
            RunTest("Test_CapsuleWindow_ComponentsIntegration", Test_CapsuleWindow_ComponentsIntegration);
            RunTest("Test_EdgeLightAudioReactive_DefaultOnAndBeatPulseModulation", Test_EdgeLightAudioReactive_DefaultOnAndBeatPulseModulation);
            RunTest("Test_EdgeLightBreathe_OutwardOnlyClipExcludesInterior", Test_EdgeLightBreathe_OutwardOnlyClipExcludesInterior);
            RunTest("Test_CapsLockSatellite_LowestPriorityAndTwoPhaseShrinkMorph", Test_CapsLockSatellite_LowestPriorityAndTwoPhaseShrinkMorph);

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
                throw new Exception($"Assertion failed: {message}");
            }
        }

        private static void AssertFalse(bool condition, string message)
        {
            if (condition)
            {
                throw new Exception($"Assertion failed (expected false): {message}");
            }
        }

        private static void AssertNotNull(object obj, string message)
        {
            if (obj == null)
            {
                throw new Exception($"Assertion failed (expected not null): {message}");
            }
        }

        private static void Test_InitialState_CollapsedAndZeroOpacity()
        {
            var indicator = new NotchPrivacyIndicator();
            AssertEqual(Visibility.Collapsed, indicator.Visibility, "Initial visibility must be Collapsed");
            AssertEqual(0.0, indicator.Opacity, "Initial opacity must be 0.0");
            AssertEqual(26.0, indicator.Width, "Initial touch target width must be 26.0");
            AssertEqual(24.0, indicator.Height, "Initial touch target height must be 24.0");
            AssertFalse(indicator.IsMicrophoneInUse, "Mic must initially be inactive");
            AssertFalse(indicator.IsCameraInUse, "Cam must initially be inactive");
            AssertFalse(indicator.IsHovered, "IsHovered must initially be false");
            AssertEqual("NotchPrivacyIndicator", AutomationProperties.GetAutomationId(indicator), "AutomationId check");
            AssertEqual("Privacy Indicator", AutomationProperties.GetName(indicator), "AutomationName check");
        }

        private static void Test_MicrophoneOnly_OrangeGlowAndDropShadow()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);

            AssertEqual(Visibility.Visible, indicator.Visibility, "Indicator must be visible when mic is active");
            AssertEqual(Visibility.Visible, indicator.MicDot.Visibility, "Mic dot must be visible");
            AssertEqual(Visibility.Collapsed, ((FrameworkElement)indicator.CamDot.Parent).Visibility, "Cam dot container must be collapsed");

            var fill = indicator.MicDot.Fill as SolidColorBrush;
            AssertNotNull(fill, "Mic dot fill must be SolidColorBrush");
            AssertEqual(NotchPrivacyIndicator.MicrophoneOrange, fill.Color, "Mic dot color must be #FF9500");

            var glow = indicator.MicDot.Effect as DropShadowEffect;
            AssertNotNull(glow, "Mic glow must be DropShadowEffect");
            AssertEqual(NotchPrivacyIndicator.MicrophoneOrange, glow.Color, "Mic glow color must be #FF9500");
            AssertEqual(10.0, glow.BlurRadius, "Mic glow blur radius must be 10.0");
            AssertEqual(0.90, glow.Opacity, "Mic glow opacity must be 0.90");

            AssertEqual(Visibility.Collapsed, indicator.MicSlash.Visibility, "Mic slash must be collapsed when unmuted");
            AssertNotNull(indicator.IndicatorToolTip.Content, "Tooltip content must not be null");
            string toolTipText = indicator.IndicatorToolTip.Content.ToString();
            AssertTrue(toolTipText.Contains("Discord"), "Tooltip must display app name Discord");
            AssertTrue(toolTipText.Contains("Click to Mute"), "Tooltip must display click to mute action");
        }

        private static void Test_CameraOnly_GreenGlow()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: true, camAppName: "Windows Camera");

            AssertEqual(Visibility.Visible, indicator.Visibility, "Indicator must be visible when camera is active");
            AssertEqual(Visibility.Visible, indicator.CamDot.Visibility, "Cam dot must be visible");
            AssertEqual(Visibility.Collapsed, ((FrameworkElement)indicator.MicDot.Parent).Visibility, "Mic dot container must be collapsed");

            var fill = indicator.CamDot.Fill as SolidColorBrush;
            AssertNotNull(fill, "Cam dot fill must be SolidColorBrush");
            AssertEqual(NotchPrivacyIndicator.CameraGreen, fill.Color, "Cam dot color must be #30D158");

            var glow = indicator.CamDot.Effect as DropShadowEffect;
            AssertNotNull(glow, "Cam glow must be DropShadowEffect");
            AssertEqual(NotchPrivacyIndicator.CameraGreen, glow.Color, "Cam glow color must be #30D158");

            string toolTipText = indicator.IndicatorToolTip.Content?.ToString() ?? "";
            AssertTrue(toolTipText.Contains("Windows Camera"), "Tooltip must display app name Windows Camera");
        }

        private static void Test_DualDotPill_BothActive()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Google Meet", isCamInUse: true, camAppName: "OBS Camera");

            AssertEqual(Visibility.Visible, indicator.Visibility, "Indicator must be visible when both active");
            AssertEqual(34.0, indicator.Width, "Hit box width expands to 34.0 in dual-dot mode");
            AssertEqual(32.0, indicator.PillHost.Width, "Pill container width must be 32.0");
            AssertEqual(16.0, indicator.PillHost.Height, "Pill container height must be 16.0");
            AssertEqual(8.0, indicator.PillHost.CornerRadius.TopLeft, "Pill container corner radius must be 8.0");

            AssertEqual(6.0, indicator.CamDot.Width, "Cam dot width in dual-dot pill must be 6.0");
            AssertEqual(6.0, indicator.MicDot.Width, "Mic dot width in dual-dot pill must be 6.0");

            string toolTipText = indicator.IndicatorToolTip.Content?.ToString() ?? "";
            AssertTrue(toolTipText.Contains("OBS Camera"), "Dual tooltip must contain camera app name");
            AssertTrue(toolTipText.Contains("Google Meet"), "Dual tooltip must contain mic app name");
            AssertTrue(toolTipText.Contains("Click to Mute"), "Dual tooltip must contain click to mute action");
        }

        private static void Test_MicrophoneMute_SlashLineAndWarningColorShift()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Zoom", isCamInUse: false, camAppName: null);

            // Mute
            indicator.UpdateMuteState(true);
            AssertTrue(indicator.IsMicrophoneMuted, "IsMicrophoneMuted must be true");
            AssertEqual(Visibility.Visible, indicator.MicSlash.Visibility, "Slash line must be visible when muted");
            AssertEqual(1.5, indicator.MicSlash.X1, "Slash X1 must be 1.5");
            AssertEqual(1.5, indicator.MicSlash.Y1, "Slash Y1 must be 1.5");
            AssertEqual(6.0, indicator.MicSlash.X2, "Slash X2 must be 6.0");
            AssertEqual(6.0, indicator.MicSlash.Y2, "Slash Y2 must be 6.0");

            var fill = indicator.MicDot.Fill as SolidColorBrush;
            AssertNotNull(fill, "Mic dot fill must not be null");
            AssertEqual(NotchPrivacyIndicator.MutedRed, fill.Color, "Muted color must shift to warning red #FF453A");

            string mutedToolTip = indicator.IndicatorToolTip.Content?.ToString() ?? "";
            AssertTrue(mutedToolTip.Contains("Muted"), "Tooltip must state Muted");
            AssertTrue(mutedToolTip.Contains("Click to Unmute"), "Tooltip must indicate Click to Unmute");

            // Unmute
            indicator.UpdateMuteState(false);
            AssertFalse(indicator.IsMicrophoneMuted, "IsMicrophoneMuted must be false");
            AssertEqual(Visibility.Collapsed, indicator.MicSlash.Visibility, "Slash line must collapse when unmuted");
            var unmutedFill = indicator.MicDot.Fill as SolidColorBrush;
            AssertEqual(NotchPrivacyIndicator.MicrophoneOrange, unmutedFill.Color, "Color must revert to #FF9500");
        }

        private static void Test_ClickToMute_InvokesCoreAudioToggleAndEvent()
        {
            var mock = new MockCoreAudioService();
            CoreAudioService.SetInstanceForTesting(mock);
            try
            {
                var indicator = new NotchPrivacyIndicator();
                indicator.UpdateAccessState(isMicInUse: true, micAppName: "Telegram", isCamInUse: false, camAppName: null);

                bool burstFired = false;
                indicator.MuteToggleRequested += () => burstFired = true;

                AssertFalse(mock.IsMicrophoneMuted, "Initial mute state should be false");

                // Simulate single click
                indicator.SimulateClick();

                AssertTrue(mock.IsMicrophoneMuted, "Single click must toggle mock microphone to muted");
                AssertTrue(burstFired, "Single click must raise MuteToggleRequested for edge light burst");

                // Second click
                burstFired = false;
                indicator.SimulateClick();

                AssertFalse(mock.IsMicrophoneMuted, "Second click must toggle mock microphone to unmuted");
                AssertTrue(burstFired, "Second click must raise MuteToggleRequested again");
            }
            finally
            {
                CoreAudioService.ResetInstance();
            }
        }

        private static void Test_HoverDwellSuppression_IsHovered()
        {
            var indicator = new NotchPrivacyIndicator();
            AssertFalse(indicator.IsHovered, "IsHovered must initially be false");
        }

        private static void Test_AutoClear_TriggeredWhenAccessEnds()
        {
            var indicator = new NotchPrivacyIndicator();
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Meet", isCamInUse: false, camAppName: null);
            AssertEqual(Visibility.Visible, indicator.Visibility, "Must be visible when mic is active");

            // Stop access
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertFalse(indicator.IsMicrophoneInUse, "Mic must be marked inactive");
            AssertFalse(indicator.IsCameraInUse, "Cam must be marked inactive");
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Tooltip content must be cleared to null when device access ends");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Tooltip must not be open");
        }

        private static void Test_AutoClear_ClearsToolTipContent()
        {
            var indicator = new NotchPrivacyIndicator();

            // Scenario 1: Mic-only release -> null
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);
            AssertNotNull(indicator.IndicatorToolTip.Content, "Mic active: tooltip must not be null");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Mic release: tooltip content must be null");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Mic release: tooltip must not be open");

            // Scenario 2: Camera-only release -> null
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: true, camAppName: "Windows Camera");
            AssertNotNull(indicator.IndicatorToolTip.Content, "Camera active: tooltip must not be null");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Camera release: tooltip content must be null");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Camera release: tooltip must not be open");

            // Scenario 3: Dual-active simultaneous release -> null
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "OBS");
            AssertNotNull(indicator.IndicatorToolTip.Content, "Dual active: tooltip must not be null");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Dual simultaneous release: tooltip content must be null");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Dual simultaneous release: tooltip must not be open");

            // Scenario 4: Staggered release (Dual -> Cam releases -> Mic releases -> null)
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "OBS");
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);
            AssertNotNull(indicator.IndicatorToolTip.Content, "Staggered step 1: mic still active, tooltip not null");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Staggered step 2: all released, tooltip content must be null");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Staggered step 2: tooltip must not be open");

            // Scenario 5: Inverted staggered release (Dual -> Mic releases -> Cam releases -> null)
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: true, camAppName: "OBS");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: true, camAppName: "OBS");
            AssertNotNull(indicator.IndicatorToolTip.Content, "Inverted step 1: cam still active, tooltip not null");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Inverted step 2: all released, tooltip content must be null");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Inverted step 2: tooltip must not be open");

            // Scenario 6: Muted mic release -> null
            indicator.UpdateAccessState(isMicInUse: true, micAppName: "Zoom", isCamInUse: false, camAppName: null);
            indicator.UpdateMuteState(true);
            AssertNotNull(indicator.IndicatorToolTip.Content, "Muted mic active: tooltip not null");
            indicator.UpdateAccessState(isMicInUse: false, micAppName: null, isCamInUse: false, camAppName: null);
            AssertEqual(null, indicator.IndicatorToolTip.Content, "Muted mic release: tooltip content must be null");
            AssertFalse(indicator.IndicatorToolTip.IsOpen, "Muted mic release: tooltip must not be open");
        }

        private static void Test_PassThroughHitTesting_BoundaryPadding()
        {
            var window = new CapsuleWindow();
            var controller = new NotchMousePassThroughController(window);

            // Make indicator visible
            window.PrivacyIndicator.UpdateAccessState(isMicInUse: true, micAppName: "Discord", isCamInUse: false, camAppName: null);

            // Coordinates in the right shoulder area:
            // Collapsed notch width = 228, centered in 800 window.
            // Left offset = (800 - 228)/2 = 286.
            // Indicator right margin = 10, width = 26.
            // Indicator X = 286 + 228 - 10 - 26 = 478 to 504.
            // Height = 35. Center Y = (35 - 24)/2 = 5.5 to 29.5.

            // 1. Center of indicator
            var insidePt = new Point(490, 15);
            bool hitsInside = controller.HitTestInteractive(insidePt);
            AssertTrue(hitsInside, "Click inside privacy indicator hit box must return true");

            // 2. 2px boundary padding (TC-T1-083)
            var paddedPt = new Point(476, 15); // 2px left of indicator X=478
            bool hitsPadded = controller.HitTestInteractive(paddedPt);
            AssertTrue(hitsPadded, "Point within 2px boundary padding must return true");

            // 3. Clear indicator -> should not hit indicator
            window.PrivacyIndicator.Visibility = Visibility.Collapsed;
            var outsideNotch = new Point(235, 15); // far outside notch
            bool hitsOutside = controller.HitTestInteractive(outsideNotch);
            AssertFalse(hitsOutside, "Point far outside notch must return false for pass-through");
        }

        private static void Test_CapsuleWindow_ComponentsIntegration()
        {
            var window = new CapsuleWindow();
            AssertNotNull(window.PrivacyIndicator, "CapsuleWindow must expose PrivacyIndicator");
            AssertNotNull(window.CapsuleCard, "CapsuleWindow must expose CapsuleCard");
            AssertNotNull(window.PassThroughController, "CapsuleWindow must expose PassThroughController");
            AssertTrue(window.CapsuleCard.Children.Contains(window.PrivacyIndicator), "CapsuleCard children must contain PrivacyIndicator");
        }

        private static void Test_EdgeLightAudioReactive_DefaultOnAndBeatPulseModulation()
        {
            var freshData = new WindowsBuddy.Services.AppPreferencesModel();
            AssertTrue(freshData.EdgeLightAudioReactive, "EdgeLightAudioReactive default must be enabled (true)");

            var edgeLight = new EdgeLightWpf();
            edgeLight.Configure("Breathe", "Normal", "Always", "Notch", audioReactive: true);
            edgeLight.UpdateGeometry(null, isExpanded: false, width: 228.0, height: 35.0);

            double baselineGlow = edgeLight.CurrentGlowThickness;
            AssertTrue(edgeLight.IsAudioReactiveEnabled, "Audio reactive flag should be active after Configure");

            // Simulate sudden bass beat
            edgeLight.ApplyAudioEnergy(0.90f, 0.45f, 0.75f, isLive: true);
            edgeLight.UpdateDashOffsets();

            AssertTrue(edgeLight.CurrentAudioPulse > 0.35, "Bass onset must produce clear positive audio pulse");
            AssertTrue(edgeLight.CurrentGlowThickness > baselineGlow + 0.5, "Breathe glow thickness must swell on beat impulse");
            AssertTrue(edgeLight.CurrentContainerOpacity >= 0.45, "Container opacity must boost with audio beat");

            // Test Comet & Twin comets patterns with audio active without geometry errors
            edgeLight.Configure("Twin comets", "Normal", "Always", "Notch", audioReactive: true);
            edgeLight.ApplyAudioEnergy(0.80f, 0.50f, 0.70f, isLive: true);
            edgeLight.UpdateDashOffsets();
            AssertTrue(edgeLight.CurrentAudioPulse > 0.25, "Twin comets must track audio pulse");

            edgeLight.Configure("Comet", "Normal", "Always", "Island", audioReactive: true);
            edgeLight.ApplyAudioEnergy(0.85f, 0.40f, 0.72f, isLive: true);
            edgeLight.UpdateDashOffsets();
            AssertTrue(edgeLight.CurrentAudioPulse > 0.25, "Comet must track audio pulse");

            // Test silence decay & clean restoration of idle pattern
            for (int i = 0; i < 16; i++)
            {
                edgeLight.ApplyAudioEnergy(0f, 0f, 0f, isLive: false);
            }
            edgeLight.UpdateDashOffsets();
            AssertTrue(edgeLight.CurrentAudioPulse < 0.08, "Audio pulse must decay smoothly on silence");

            edgeLight.Configure("Breathe", "Normal", "Always", "Notch", audioReactive: true);
            for (int i = 0; i < 20; i++) edgeLight.ApplyAudioEnergy(0f, 0f, 0f, isLive: false);
            edgeLight.UpdateDashOffsets();
            AssertTrue(Math.Abs(edgeLight.CurrentGlowThickness - baselineGlow) < 0.1, "Idle Breathe glow thickness must restore cleanly when silent");

            // Test turning off resets reactive modulation cleanly
            edgeLight.Configure("Breathe", "Normal", "Always", "Notch", audioReactive: false);
            edgeLight.ApplyAudioEnergy(0.95f, 0.80f, 0.90f, isLive: true);
            edgeLight.UpdateDashOffsets();
            AssertFalse(edgeLight.IsAudioReactiveEnabled, "Audio reactive flag must turn off cleanly");
            AssertTrue(Math.Abs(edgeLight.CurrentAudioPulse) < 0.001, "Audio pulse must remain zero when feature is toggled off");
            AssertTrue(Math.Abs(edgeLight.CurrentGlowThickness - baselineGlow) < 0.05, "Glow thickness must stay at baseline when audio reactive is disabled");
        }

        private static void Test_EdgeLightBreathe_OutwardOnlyClipExcludesInterior()
        {
            var edgeLight = new EdgeLightWpf();
            var notchGeo = WindowsBuddy.BuddyCapsule.Renderer.NotchGeometryBuilder.BuildSilhouetteCore("Shouldered", 228.0, 35.0, 15.0, isOutline: false);
            edgeLight.Configure("Breathe", "Normal", "Always", "Notch", audioReactive: false);
            edgeLight.UpdateGeometry(notchGeo, isExpanded: false, width: 228.0, height: 35.0);

            var clip = edgeLight.CurrentOutwardClip;
            AssertNotNull(clip, "Outward clip must be set on EdgeLight container");
            AssertFalse(clip.FillContains(new Point(114.0, 18.0)), "Notch interior center must be strictly excluded from EdgeLight glow");
            AssertFalse(clip.FillContains(new Point(114.0, 32.0)), "Notch interior near bottom edge must be strictly excluded from EdgeLight glow");
            AssertFalse(clip.FillContains(new Point(16.0, 18.0)), "Notch interior near left wall must be strictly excluded from EdgeLight glow");
            AssertTrue(clip.FillContains(new Point(114.0, 41.0)), "Exterior directly below notch bottom edge must remain visible for outward glow");
            AssertTrue(clip.FillContains(new Point(6.0, 22.0)), "Exterior outside left wall must remain visible for outward glow");
            AssertTrue(clip.FillContains(new Point(222.0, 22.0)), "Exterior outside right wall must remain visible for outward glow");

            var islandGeo = WindowsBuddy.BuddyCapsule.Renderer.NotchGeometryBuilder.BuildSilhouetteCore("Island", 220.0, 34.0, 17.0, isOutline: false);
            edgeLight.Configure("Breathe", "Normal", "Always", "Island", audioReactive: false);
            edgeLight.UpdateGeometry(islandGeo, isExpanded: false, width: 220.0, height: 34.0);
            var islandClip = edgeLight.CurrentOutwardClip;
            AssertNotNull(islandClip, "Island outward clip must be set");
            AssertFalse(islandClip.FillContains(new Point(110.0, 22.0)), "Island interior must be strictly excluded from EdgeLight glow");
            AssertTrue(islandClip.FillContains(new Point(110.0, 2.0)), "Island top exterior margin must remain visible for outward glow");
        }

        private static void Test_CapsLockSatellite_LowestPriorityAndTwoPhaseShrinkMorph()
        {
            var window = new CapsuleWindow();
            try
            {
                var capsIsland = window.CapsLockSatelliteIsland;
                AssertNotNull(capsIsland, "CapsLockSatelliteIsland must be constructed");

                double GetCapsTranslateX()
                {
                    foreach (var tr in ((TransformGroup)capsIsland.RenderTransform).Children)
                    {
                        if (tr is TranslateTransform tt) return tt.X;
                    }
                    return 0.0;
                }

                // 1. Even with NO music playing (idle main notch), Caps Lock ON opens as "Caps Lock ON" pill on the right
                window.SetCapsLockStateForTest(isOn: true, expandPill: true, immediate: true);
                AssertTrue(capsIsland.Visibility == Visibility.Visible, "CapsLock capsule must be visible even when notch is idle and nothing is playing");
                AssertTrue(capsIsland.Width > 120.0, "Initial CapsLock phase must expand as a roomy pill for text");
                AssertTrue(string.Equals(window.CapsLockDisplayText, "Caps Lock ON", StringComparison.Ordinal), "Expanded pill must display 'Caps Lock ON'");
                AssertTrue(GetCapsTranslateX() > 90.0, "CapsLock capsule must be positioned to the right of the main notch");

                // 2. After delay, shrinks smoothly into compact enlarged circle (40x40) with centered 'A'
                window.SetCapsLockStateForTest(isOn: true, expandPill: false, immediate: true);
                AssertTrue(capsIsland.Visibility == Visibility.Visible, "CapsLock shrunk circle must remain visible while Caps Lock is ON");
                AssertTrue(Math.Abs(capsIsland.Width - 40.0) < 0.5 && Math.Abs(capsIsland.Height - 40.0) < 0.5, "Shrunk CapsLock capsule must become a 40x40 circle");
                AssertTrue(Math.Abs(capsIsland.CornerRadius.TopLeft - 20.0) < 0.5, "Shrunk CapsLock capsule must be fully circular");
                AssertTrue(string.Equals(window.CapsLockCircleGlyphText, "A", StringComparison.Ordinal), "Shrunk circle center must show 'A'");
                double idleRightX = GetCapsTranslateX();

                // 3. Lowest-priority outermost slot: when 3-capsule Split Timer is open, CapsLock stays visible AND shifts right beside RightTimer without overlap
                window.SetCapsLockStateForTest(isOn: true, expandPill: false, immediate: true, rightTimerOccupied: true);
                AssertTrue(capsIsland.Visibility == Visibility.Visible, "CapsLock capsule must remain visible alongside 3 open timer capsules");
                double withTimerRightX = GetCapsTranslateX();
                AssertTrue(withTimerRightX > idleRightX + 45.0, "CapsLock capsule must shift cleanly to the outermost right slot next to RightTimerIsland");

                // 4. When Volume/Brightness HUD opens on right slot, CapsLock stays visible and shifts cleanly beside RightHudSatelliteIsland
                window.SetCapsLockStateForTest(isOn: true, expandPill: false, immediate: true, rightHudOccupied: true);
                AssertTrue(capsIsland.Visibility == Visibility.Visible, "CapsLock capsule must remain visible alongside right HUD satellite");
                double withHudRightX = GetCapsTranslateX();
                AssertTrue(withHudRightX > idleRightX + 25.0, "CapsLock capsule must shift cleanly next to RightHudSatelliteIsland");

                // 5. Shifts smoothly back next to main notch once higher-priority islands close
                window.SetCapsLockStateForTest(isOn: true, expandPill: false, immediate: true, rightHudOccupied: false, rightTimerOccupied: false);
                AssertTrue(capsIsland.Visibility == Visibility.Visible, "CapsLock capsule must stay visible");
                AssertTrue(Math.Abs(GetCapsTranslateX() - idleRightX) < 1.0, "CapsLock capsule must slide back beside main notch once right slot clears");

                // 6. Collapses when Caps Lock turns OFF
                window.SetCapsLockStateForTest(isOn: false, expandPill: false, immediate: true);
                AssertTrue(capsIsland.Visibility == Visibility.Collapsed, "CapsLock capsule must collapse when Caps Lock turns OFF");
            }
            finally
            {
                window.Close();
            }
        }
    }
}
