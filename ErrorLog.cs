namespace LangFix;

internal static class ErrorLog
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LangFix", "error.log");

    private const long MaxLogBytes = 1_048_576; // 1 MB

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (File.Exists(FilePath) && new FileInfo(FilePath).Length >= MaxLogBytes)
                File.Move(FilePath, FilePath + ".old", overwrite: true);
            File.AppendAllText(FilePath, $"[{DateTimeOffset.Now:O}] {exception}\r\n\r\n");
        }
        catch { }
    }
}
