namespace LangFix;

internal static class ErrorLog
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LangFix", "error.log");

    public static void Write(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.AppendAllText(FilePath, $"[{DateTimeOffset.Now:O}] {exception}\r\n\r\n");
        }
        catch { }
    }
}
