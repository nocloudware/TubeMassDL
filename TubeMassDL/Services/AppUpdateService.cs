using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TubeMassDL.Services;

public class AppUpdateInfo
{
    public string TagName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public bool IsNewerVersion { get; set; }
}

public class AppUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/nocloudware/TubeMassDL/releases/latest";
    private readonly HttpClient _http;

    public AppUpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TubeMassDL/2.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
    }

    public async Task<AppUpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            using var client = new HttpClient(handler);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TubeMassDL/2.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");

            using var response = await client.GetAsync(GitHubApiUrl);
            if ((int)response.StatusCode == 404) return null;
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            var release = JsonDocument.Parse(json).RootElement;

            var tagName = release.GetProperty("tag_name").GetString() ?? "0.0.0";
            var version = tagName.TrimStart('v');
            var downloadUrl = release.GetProperty("html_url").GetString() ?? "";
            var releaseNotes = release.GetProperty("body").GetString() ?? "";

            var currentVersion = Assembly.GetEntryAssembly()
                ?.GetName().Version?.ToString() ?? "0.0.0";

            return new AppUpdateInfo
            {
                TagName = tagName,
                Version = version,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes,
                IsNewerVersion = IsNewerVersion(version, currentVersion)
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking app updates: {ex.Message}");
            return null;
        }
    }

    private static bool IsNewerVersion(string v1, string v2)
    {
        try { return Version.Parse(v1) > Version.Parse(v2); }
        catch { return string.Compare(v1, v2, StringComparison.OrdinalIgnoreCase) > 0; }
    }
}
