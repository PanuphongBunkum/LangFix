namespace LangFix;

internal static class InputIntegrationTest
{
    public static int Run()
    {
        using var form = new Form
        {
            ShowInTaskbar = false,
            TopMost = true,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(20, 20),
            Size = new Size(180, 50)
        };
        using var box = new TextBox { Dock = DockStyle.Fill, Text = "xyz" };
        form.Controls.Add(box);
        form.Show();
        form.Activate();
        NativeMethods.SetForegroundWindow(form.Handle);
        box.Focus();
        box.SelectionStart = box.TextLength;
        for (var i = 0; i < 10 && NativeMethods.GetForegroundWindow() != form.Handle; i++)
        {
            Application.DoEvents();
            Thread.Sleep(20);
            NativeMethods.SetForegroundWindow(form.Handle);
        }
        if (NativeMethods.GetForegroundWindow() != form.Handle || !box.Focused) return 21;

        NativeMethods.SendBackspacesAndText(3, "abc");
        for (var i = 0; i < 10; i++)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        return box.Text == "abc" ? 0 : 20;
    }
}
