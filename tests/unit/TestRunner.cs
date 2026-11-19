using System;

namespace Mayli.UnitTests
{
    public static class TestRunner
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], "CoreAudio", StringComparison.OrdinalIgnoreCase))
            {
                return CoreAudioMicrophoneTests.Main(args);
            }
            if (args.Length > 0 && string.Equals(args[0], "UI", StringComparison.OrdinalIgnoreCase))
            {
                return NotchPrivacyIndicatorUiTests.Main(args);
            }

            Console.WriteLine("=================================================");
            Console.WriteLine(" Running All Mayli Unit Test Suites");
            Console.WriteLine("=================================================");

            int audioResult = CoreAudioMicrophoneTests.Main(args);
            Console.WriteLine();
            int uiResult = NotchPrivacyIndicatorUiTests.RunTests();

            int finalResult = (audioResult == 0 && uiResult == 0) ? 0 : 1;
            Console.WriteLine();
            Console.WriteLine("=================================================");
            Console.WriteLine($" ALL UNIT TESTS OVERALL STATUS: {(finalResult == 0 ? "PASSED" : "FAILED")}");
            Console.WriteLine("=================================================");

            return finalResult;
        }
    }
}
