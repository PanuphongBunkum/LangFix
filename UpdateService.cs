using System.Net.Http.Json;

namespace LangFix;

internal static class UpdateService
{
    public const string RepoPath = "/PanuphongBunkum/LangFix/";
    public const string WebsiteUrl = "https://github.com/PanuphongBunkum/LangFix/releases/latest";
    public const string FeedbackUrl = "https://github.com/PanuphongBunkum/LangFix/issues/new/choose";
    private const string ManifestUrl = "https://github.com/PanuphongBunkum/LangFix/releases/latest/download/langfix-version.json";
    private static readonly HttpClient Client = CreateClient();

    internal sealed record UpdateManifest(string Version, string? DownloadUrl, string? ReleaseNotes);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"LangFix/{CurrentVersion}");
        return client;
    }

    public static async Task<UpdateManifest> GetLatestAsync()
    {
        var manifest = await Client.GetFromJsonAsync<UpdateManifest>(ManifestUrl).ConfigureAwait(false);
        if (manifest is null || !Version.TryParse(manifest.Version, out _))
            throw new InvalidDataException("ข้อมูลเวอร์ชันไม่ถูกต้อง");
        return manifest;
    }

    public static string CurrentVersion =>
        typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    internal static bool IsNewerVersion(string current, string latest) =>
        Version.TryParse(current, out var currentVersion)
        && Version.TryParse(latest, out var latestVersion)
        && latestVersion > currentVersion;

    internal static string ResolveSafeDownloadUrl(string? candidate)
    {
        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith(RepoPath, StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsoluteUri;
        }
        return WebsiteUrl;
    }
}
