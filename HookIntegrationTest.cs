namespace LangFix;

internal static class HookIntegrationTest
{
    public static int Run()
    {
        using var form = new Form
        {
            Text = "LangFix hook integration test",
            ShowInTaskbar = false,
            TopMost = true,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20),
            Size = new Size(320, 80)
        };
        using var box = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(box);
        form.Show();
        form.Activate();
        NativeMethods.SetForegroundWindow(form.Handle);
        box.Focus();
        Pump(100);
        if (!box.Focused) return 40;

        var directEnglish = NativeMethods.TranslateSequenceForTesting(NativeMethods.KeyboardLanguage.English, 0x4C, 0xBA, 0x59, 0x4C, 0x46, 0x55);
        var directThai = NativeMethods.TranslateSequenceForTesting(NativeMethods.KeyboardLanguage.Thai, 0x4C, 0xBA, 0x59, 0x4C, 0x46, 0x55);
        if (directEnglish != "l;ylfu" || directThai != "สวัสดี")
        {
            Console.Error.WriteLine($"Direct translation EN=[{directEnglish}] TH=[{directThai}]");
            return 45;
        }

        KeyboardService.AcceptInjectedForTesting = true;
        try
        {
            using var keyboard = new KeyboardService { AutomaticMode = true, SwitchLayoutAfterCorrection = true };
            for (var round = 1; round <= 5; round++)
            {
                box.Clear();
                NativeMethods.SwitchLayoutForText(box.Handle, "ไทย");
                Pump(100);
                if (!EnsureFocus(form, box)) return 51;
                TypeKeys(0x54, 0x45, 0x53, 0x54);
                Pump(400);
                if (box.Text != "test")
                {
                    Console.Error.WriteLine($"Round {round} Thai->English actual: [{box.Text}], layout: {NativeMethods.GetActiveKeyboardLanguage()}, decision: {KeyboardService.LastTestDecision}");
                    return 41;
                }
                if (NativeMethods.GetActiveKeyboardLanguage() != NativeMethods.KeyboardLanguage.English) return 42;

                if (!EnsureFocus(form, box)) return 52;
                TypeShortcut(NativeMethods.VkShift, NativeMethods.VkBack);
                Pump(350);
                if (box.Text != "ะำหะ" || NativeMethods.GetActiveKeyboardLanguage() != NativeMethods.KeyboardLanguage.Thai)
                {
                    Console.Error.WriteLine($"Round {round} undo Thai->English actual: [{box.Text}], layout: {NativeMethods.GetActiveKeyboardLanguage()}");
                    return 48;
                }

                box.Clear();
                NativeMethods.SwitchLayoutForText(box.Handle, "ไทย");
                Pump(100);
                if (!EnsureFocus(form, box)) return 53;
                TypeKeys(0x54, 0x45, 0x53, 0x54);
                Pump(350);
                TypeKeys((int)NativeMethods.VkSpace);
                Pump(100);
                if (box.Text != "test ") return 46;

                box.Clear();
                NativeMethods.SwitchLayoutForText(box.Handle, "english");
                Pump(100);
                if (!EnsureFocus(form, box)) return 54;
                TypeKeys(0x4C, 0xBA, 0x59, 0x4C, 0x46, 0x55);
                Pump(400);
                if (box.Text != "สวัสดี")
                {
                    Console.Error.WriteLine($"Round {round} English->Thai actual: [{box.Text}], layout: {NativeMethods.GetActiveKeyboardLanguage()}, decision: {KeyboardService.LastTestDecision}");
                    return 43;
                }
                if (NativeMethods.GetActiveKeyboardLanguage() != NativeMethods.KeyboardLanguage.Thai) return 44;

                if (round == 1)
                {
                    keyboard.Shortcut = new ShortcutDefinition(Keys.F12, Keys.Control);
                    if (!EnsureFocus(form, box)) return 55;
                    TypeShortcut(NativeMethods.VkControl, (int)Keys.F12);
                    Pump(350);
                    if (box.Text != "l;ylfu" || NativeMethods.GetActiveKeyboardLanguage() != NativeMethods.KeyboardLanguage.English)
                    {
                        Console.Error.WriteLine($"Custom shortcut undo actual: [{box.Text}], layout: {NativeMethods.GetActiveKeyboardLanguage()}");
                        return 49;
                    }

                    box.Clear();
                    keyboard.AutomaticMode = false;
                    NativeMethods.SwitchLayoutForText(box.Handle, "english");
                    Pump(100);
                    if (!EnsureFocus(form, box)) return 56;
                    TypeKeys(0x4C, 0xBA, 0x59, 0x4C, 0x46, 0x55);
                    TypeShortcut(NativeMethods.VkControl, (int)Keys.F12);
                    Pump(350);
                    if (box.Text != "สวัสดี")
                    {
                        Console.Error.WriteLine($"Custom shortcut manual correction actual: [{box.Text}]");
                        return 50;
                    }
                    keyboard.AutomaticMode = true;
                    keyboard.Shortcut = ShortcutDefinition.Default;
                }
                else
                {
                    TypeKeys((int)NativeMethods.VkSpace);
                    Pump(100);
                    if (box.Text != "สวัสดี ") return 47;
                }
            }
            return 0;
        }
        finally
        {
            KeyboardService.AcceptInjectedForTesting = false;
        }
    }

    private static void Pump(int milliseconds)
    {
        var deadline = Environment.TickCount64 + milliseconds;
        while (Environment.TickCount64 < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
    }

    private static void TypeKeys(params int[] keys)
    {
        foreach (var key in keys)
        {
            NativeMethods.SendTestVirtualKeys(key);
            Pump(35);
        }
    }

    private static bool EnsureFocus(Form form, TextBox box)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            form.Activate();
            NativeMethods.ForceForegroundForTesting(form.Handle, box.Handle);
            box.Focus();
            Pump(30);
            if (box.Focused && NativeMethods.GetFocusedInputWindow() == box.Handle) return true;
        }
        return false;
    }

    private static void TypeShortcut(int modifier, int key)
    {
        NativeMethods.SendTestKeyState(modifier, keyUp: false);
        Pump(45);
        NativeMethods.SendTestVirtualKeys(key);
        Pump(45);
        NativeMethods.SendTestKeyState(modifier, keyUp: true);
    }
}
