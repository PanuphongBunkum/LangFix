using System.Text.Json;

namespace LangFix;

internal sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 5;
    public bool Enabled { get; set; } = true;
    public bool AutomaticMode { get; set; } = true;
    public bool StartWithWindows { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool SwitchLayoutAfterCorrection { get; set; } = true;
    public int ShortcutKey { get; set; } = (int)Keys.Back;
    public int ShortcutModifiers { get; set; } = (int)Keys.Shift;
    public bool CheckForUpdatesAutomatically { get; set; } = true;
    public DateTimeOffset LastUpdateCheckUtc { get; set; } = DateTimeOffset.MinValue;

    public ShortcutDefinition GetShortcut() => ShortcutDefinition.FromSettings(ShortcutKey, ShortcutModifiers);

    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LangFix");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
                if (settings.SettingsVersion < 4)
                {
                    settings.SettingsVersion = 4;
                    settings.AutomaticMode = true;
                    settings.SwitchLayoutAfterCorrection = true;
                    settings.ShortcutKey = (int)Keys.Back;
                    settings.ShortcutModifiers = (int)Keys.Shift;
                }
                if (settings.SettingsVersion < 5)
                {
                    settings.SettingsVersion = 5;
                    settings.CheckForUpdatesAutomatically = true;
                }
                settings.Save();
                return settings;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
        }
        return new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, FilePath, overwrite: true);
    }
}
