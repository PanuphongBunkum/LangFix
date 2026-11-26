using System.Threading;

namespace LangFix;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (Environment.GetCommandLineArgs().Contains("--shutdown"))
        {
            if (EventWaitHandle.TryOpenExisting("Local\\LangFix-Shutdown-8E63E520", out var shutdownEvent))
            {
                using (shutdownEvent) shutdownEvent.Set();
            }
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--self-test"))
        {
            Environment.ExitCode = SelfTests.Run();
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--input-test"))
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = InputIntegrationTest.Run();
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--layout-test"))
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = LayoutIntegrationTest.Run();
            return;
        }

        if (Environment.GetCommandLineArgs().Contains("--hook-test"))
        {
            ApplicationConfiguration.Initialize();
            Environment.ExitCode = HookIntegrationTest.Run();
            return;
        }

        using var mutex = new Mutex(true, "Local\\LangFix-TH-EN-8E63E520", out var firstInstance);
        if (!firstInstance) return;

        ApplicationConfiguration.Initialize();
        Application.Run(new LangFixContext());
    }
}
