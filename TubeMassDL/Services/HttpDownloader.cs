using System.IO;
using System.Net.Http;

namespace TubeMassDL.Services;

public class HttpDownloader
{
    private readonly HttpClient _client;
    private CancellationTokenSource? _cts;
    private bool _isPaused;
    private readonly SemaphoreSlim _pauseLock = new(1, 1);

    public HttpDownloader()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    }

    public async Task<bool> DownloadAsync(string url, string outputPath, string fileName,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            Directory.CreateDirectory(outputPath);
            string fullPath = Path.Combine(outputPath, SanitizeFileName(fileName));

            using var response = await _client.GetAsync(url,
                HttpCompletionOption.ResponseHeadersRead, _cts.Token);
            response.EnsureSuccessStatusCode();

            long total = response.Content.Headers.ContentLength ?? -1;
            using var stream = await response.Content.ReadAsStreamAsync(_cts.Token);
            using var file = File.Create(fullPath);

            var buffer = new byte[81920];
            long read = 0;
            int bytes;
            while ((bytes = await stream.ReadAsync(buffer, 0, buffer.Length, _cts.Token)) > 0)
            {
                await _pauseLock.WaitAsync(_cts.Token);
                try
                {
                    while (_isPaused && !_cts.Token.IsCancellationRequested)
                    {
                        _pauseLock.Release();
                        await Task.Delay(200, _cts.Token);
                        await _pauseLock.WaitAsync(_cts.Token);
                    }
                }
                finally { _pauseLock.Release(); }

                await file.WriteAsync(buffer, 0, bytes, _cts.Token);
                read += bytes;
                if (total > 0) progress?.Report((int)(read * 100 / total));
            }
            progress?.Report(100);
            return true;
        }
        catch { return false; }
    }

    public void Pause() => _isPaused = true;
    public void Resume() => _isPaused = false;
    public void Stop() => _cts?.Cancel();

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}