using System.Net.Http.Json;

namespace LangFix;

internal static class UpdateService
{
    public const string WebsiteUrl = "https://langfix-th-en-download.panuphongoat.chatgpt.site";
    public const string FeedbackUrl = WebsiteUrl + "/#feedback";
    private const string ManifestUrl = WebsiteUrl + "/langfix-version.json";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(12) };

    internal sealed record UpdateManifest(string Version, string? DownloadUrl, string? ReleaseNotes);

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
            && uri.Host.Equals(new Uri(WebsiteUrl).Host, StringComparison.OrdinalIgnoreCase))
        {
            return uri.AbsoluteUri;
        }
        return WebsiteUrl;
    }
}
