namespace LangFix;

internal static class LayoutIntegrationTest
{
    public static int Run()
    {
        using var form = new Form
        {
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-2000, -2000),
            Size = new Size(10, 10)
        };
        form.Show();
        _ = form.Handle;

        NativeMethods.SwitchLayoutForText(form.Handle, "สวัสดี");
        PumpMessages();
        if (!InputLanguage.CurrentInputLanguage.Culture.Name.StartsWith("th", StringComparison.OrdinalIgnoreCase)) return 30;

        NativeMethods.SwitchLayoutForText(form.Handle, "hello");
        PumpMessages();
        if (!InputLanguage.CurrentInputLanguage.Culture.Name.StartsWith("en", StringComparison.OrdinalIgnoreCase)) return 31;
        return 0;
    }

    private static void PumpMessages()
    {
        for (var i = 0; i < 20; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }
    }
}
