using Microsoft.Win32;

namespace LangFix;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LangFix";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{Application.ExecutablePath}\" --startup");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
