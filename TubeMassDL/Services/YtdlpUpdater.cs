using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

namespace TubeMassDL.Services;

public class YtdlpUpdater
{
    private readonly string _ytdlpPath;
    private readonly HttpClient _http;

    public YtdlpUpdater()
    {
        _ytdlpPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TubeMassDL/2.0");
    }

    public string GetBinaryPath() => _ytdlpPath;

    public async Task<(bool success, string version)> CheckAndUpdateAsync()
    {
        try
        {
            string? current = File.Exists(_ytdlpPath) ? await GetVersionAsync() : null;
            string? latest = await GetLatestVersionAsync();

            if (latest == null && current != null)
                return (true, current);
            if (latest == null && current == null)
            {
                bool dl = await DownloadLatestAsync();
                return dl ? (true, await GetVersionAsync() ?? "?") : (false, "");
            }
            if (current == latest)
                return (true, current!);

            if (await DownloadLatestAsync())
                return (true, await GetVersionAsync() ?? latest!);
            return (false, current ?? "");
        }
        catch { return (false, ""); }
    }

    private async Task<string?> GetVersionAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ytdlpPath,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var proc = Process.Start(psi);
            if (proc == null) return null;
            string ver = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return ver.Trim();
        }
        catch { return null; }
    }

    private async Task<string?> GetLatestVersionAsync()
    {
        try
        {
            var resp = await _http.GetAsync(
                "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest");
            if (!resp.IsSuccessStatusCode) return null;
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
        }
        catch { return null; }
    }

    private async Task<bool> DownloadLatestAsync()
    {
        try
        {
            string temp = _ytdlpPath + ".tmp";
            using var resp = await _http.GetAsync(
                "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe",
                HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            using var src = await resp.Content.ReadAsStreamAsync();
            using var dst = File.Create(temp);
            await src.CopyToAsync(dst);
            dst.Close();
            if (File.Exists(_ytdlpPath)) File.Delete(_ytdlpPath);
            File.Move(temp, _ytdlpPath);
            return true;
        }
        catch { return false; }
    }
}