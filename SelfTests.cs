namespace LangFix;

using System.Runtime.InteropServices;

internal static class SelfTests
{
    public static int Run()
    {
        var expectedInputSize = IntPtr.Size == 8 ? 40 : 28;
        if (Marshal.SizeOf<NativeMethods.Input>() != expectedInputSize)
        {
            Console.Error.WriteLine($"FAIL: INPUT size is {Marshal.SizeOf<NativeMethods.Input>()}; expected {expectedInputSize}");
            return 10;
        }

        var cases = new (string Input, string Expected)[]
        {
            ("l;ylfu", "สวัสดี"),
            ("สวัสดี", "l;ylfu"),
            ("hello", "้ำสสน"),
            ("ะำหะ", "test"),
            ("ภาษาไทย", "4kKkwmp"),
            ("123", "ๅ/-")
        };

        foreach (var (input, expected) in cases)
        {
            var actual = LanguageConverter.Convert(input);
            if (actual != expected)
            {
                Console.Error.WriteLine($"FAIL: {input} => {actual}; expected {expected}");
                return 1;
            }
        }

        if (!LanguageConverter.ShouldAutoConvert("l;ylfu", out var greeting) || greeting != "สวัสดี") return 2;
        if (LanguageConverter.ShouldAutoConvert("hello", out _)) return 3;
        if (!LanguageConverter.ShouldAutoConvert("้ำสสน", out var hello) || hello != "hello") return 4;
        if (KeyboardService.IsWordBoundary(';') || KeyboardService.IsWordBoundary('\'')) return 5;
        if (!KeyboardService.IsWordBoundary(' ') || !KeyboardService.IsWordBoundary('\t')) return 6;
        if (!KeyboardService.IsAutomaticBoundaryKey(NativeMethods.VkSpace) || KeyboardService.IsAutomaticBoundaryKey((uint)'A')) return 7;
        if (NativeMethods.TargetLayoutId("สวัสดี") != "0000041E") return 8;
        if (NativeMethods.TargetLayoutId("hello") != "00000409") return 9;
        if (!WordDictionaryService.BuiltInDictionariesLoaded) return 22;
        if (!WordDictionaryService.IsEnglishExact("computer")) return 23;
        if (!WordDictionaryService.IsThaiExact("ประเทศไทย")) return 24;
        var manualTest = KeyboardService.ChooseManual("test", "ะำหะ", NativeMethods.KeyboardLanguage.Thai);
        if (manualTest is null || manualTest.Value.Original != "ะำหะ" || manualTest.Value.Converted != "test") return 15;
        var autoTest = KeyboardService.ChooseAutomatic("test", "ะำหะ", NativeMethods.KeyboardLanguage.Thai);
        if (autoTest is null || autoTest.Value.Converted != "test") return 16;
        var manualGreeting = KeyboardService.ChooseManual("l;ylfu", "สวัสดี", NativeMethods.KeyboardLanguage.English);
        if (manualGreeting is null || manualGreeting.Value.Converted != "สวัสดี") return 17;
        var autoGreeting = KeyboardService.ChooseAutomatic("l;ylfu", "สวัสดี", NativeMethods.KeyboardLanguage.English);
        if (autoGreeting is null || autoGreeting.Value.Converted != "สวัสดี") return 18;
        var immediateTest = KeyboardService.ChooseImmediateAutomatic("test", "ะำหะ", NativeMethods.KeyboardLanguage.Thai);
        if (immediateTest is null || immediateTest.Value.Converted != "test") return 19;
        var immediateGreeting = KeyboardService.ChooseImmediateAutomatic("l;ylfu", "สวัสดี", NativeMethods.KeyboardLanguage.English);
        if (immediateGreeting is null || immediateGreeting.Value.Converted != "สวัสดี") return 20;
        if (KeyboardService.ChooseImmediateAutomatic("tes", "ะำห", NativeMethods.KeyboardLanguage.Thai) is not null) return 21;
        if (!ShortcutDefinition.Default.Matches(NativeMethods.VkBack, false, false, true)) return 25;
        if (ShortcutDefinition.Default.Matches(NativeMethods.VkBack, true, false, true)) return 26;
        if (!UpdateService.IsNewerVersion("0.3.0", "0.4.0")) return 27;
        if (UpdateService.IsNewerVersion("0.4.0", "0.4.0")) return 28;
        if (WordDictionaryService.RightLangDictionariesLoaded)
        {
            if (!WordDictionaryService.IsEnglishKnown("correct")) return 11;
            if (!WordDictionaryService.IsThaiKnown("สวัสดี")) return 12;
            if (!LanguageConverter.ShouldAutoConvert("แนพพำแะ", out var correct) || correct != "correct") return 13;
            var wrongThai = LanguageConverter.Convert("ประเทศไทย");
            if (!LanguageConverter.ShouldAutoConvert(wrongThai, out var thailand) || thailand != "ประเทศไทย") return 14;
        }
        return 0;
    }
}
