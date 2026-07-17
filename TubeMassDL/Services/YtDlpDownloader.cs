using System.Diagnostics;
using System.IO;
using NoCloudware.UI.Core.ViewModels;
using TubeMassDL.Models;

namespace TubeMassDL.Services;

public class YtDlpDownloader
{
    private readonly string _ytdlpPath;
    private Process? _currentProcess;
    private CancellationTokenSource? _cts;

    public event Action<int>? ProgressUpdated;
    public event Action<bool, string?>? Completed;
    public event Action<string>? Log;

    public YtDlpDownloader(string ytdlpPath)
    {
        _ytdlpPath = ytdlpPath;
    }

    public async Task<(bool success, string? filePath, string? error)> DownloadAsync(
        string url, string outputPath, string format, bool antiBlock, bool extractAudio,
        CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        const int maxRetries = 3;
        int[] delays = { 30_000, 60_000, 120_000 };

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await ExecuteDownloadAsync(url, outputPath, format, antiBlock, extractAudio);
                if (result.success) return result;

                if (attempt < maxRetries)
                {
                    Log?.Invoke($"Intento {attempt} falló. Reintentando en {delays[attempt - 1] / 1000}s...");
                    await Task.Delay(delays[attempt - 1], _cts.Token);
                }
                else
                {
                    Log?.Invoke($"Todos los intentos agotados para {url}");
                    return result;
                }
            }
            catch (OperationCanceledException)
            {
                return (false, null, "Cancelado");
            }
            catch (Exception ex)
            {
                if (attempt < maxRetries)
                {
                    Log?.Invoke($"Error: {ex.Message}. Reintentando en {delays[attempt - 1] / 1000}s...");
                    await Task.Delay(delays[attempt - 1], _cts.Token);
                }
                else
                {
                    Completed?.Invoke(false, ex.Message);
                    return (false, null, ex.Message);
                }
            }
        }

        return (false, null, "Error desconocido");
    }

    private async Task<(bool success, string? filePath, string? error)> ExecuteDownloadAsync(
        string url, string outputPath, string format, bool antiBlock, bool extractAudio)
    {
        try
        {
            var args = new List<string>();

            if (format.Contains("video"))
            {
                args.Add("--extractor-args");
                args.Add("youtube:player_client=android,web");
            }

            args.Add("--cookies-from-browser"); args.Add("chrome");

            string? nodePath = GetNodePath();
            if (nodePath != null)
            {
                args.Add("--js-runtimes");
                args.Add($"node:{nodePath}");
            }

            if (antiBlock)
            {
                var rng = new Random();
                args.Add("--sleep-interval"); args.Add(rng.Next(15, 45).ToString());
                args.Add("--max-sleep-interval"); args.Add(rng.Next(45, 90).ToString());
                args.Add("--limit-rate"); args.Add("5M");
                args.Add("--wait-for-video"); args.Add("30");
                args.Add("--retries"); args.Add("5");
                args.Add("--fragment-retries"); args.Add("5");
                args.Add("--no-mtime");
            }

            args.Add("-f"); args.Add(format);

            if (extractAudio)
            {
                args.Add("--extract-audio");
                args.Add("--audio-format"); args.Add("mp3");
            }

            string safeOutput = Path.Combine(outputPath, "%(title)s.%(ext)s");
            args.Add("-o"); args.Add(safeOutput);
            args.Add("--no-playlist");
            args.Add("--progress"); args.Add("--newline");
            args.Add(url);

            var psi = new ProcessStartInfo
            {
                FileName = _ytdlpPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            string? capturedFile = null;
            string? mergedFile = null;

            var outputTask = Task.Run(async () =>
            {
                while (!_currentProcess!.HasExited && !_cts!.Token.IsCancellationRequested)
                {
                    var line = await _currentProcess.StandardOutput.ReadLineAsync(_cts.Token);
                    if (line == null) break;

                    if (line.Contains("Destination:"))
                    {
                        int idx = line.IndexOf("Destination:");
                        capturedFile = line[(idx + 12)..].Trim();
                    }
                    else if (line.Contains("[Merging formats into]"))
                    {
                        int idx = line.IndexOf("[Merging formats into]");
                        mergedFile = line[(idx + 22)..].Trim();
                    }
                    else if (line.Contains("[download]") && line.Contains('%'))
                        ParseProgress(line);
                }
            }, _cts?.Token ?? default);

            if (_currentProcess != null)
                await _currentProcess.WaitForExitAsync(_cts?.Token ?? default);
            await outputTask;

            bool ok = _currentProcess?.ExitCode == 0;
            string actualFile = mergedFile ?? capturedFile ?? "";

            Completed?.Invoke(ok, ok ? null : "Download failed");
            return (ok, ok ? actualFile : null, ok ? null : "Exit code non-zero");
        }
        catch (Exception ex)
        {
            Completed?.Invoke(false, ex.Message);
            return (false, null, ex.Message);
        }
    }

    private void ParseProgress(string line)
    {
        try
        {
            int idx = line.IndexOf('%');
            if (idx <= 0) return;
            int start = idx - 1;
            while (start > 0 && (char.IsDigit(line[start - 1]) || line[start - 1] == '.'))
                start--;
            string num = line[start..idx];
            if (float.TryParse(num, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float pct))
                ProgressUpdated?.Invoke((int)pct);
        }
        catch { }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _currentProcess?.Kill(); } catch { }
    }

    private static string? GetNodePath()
    {
        string[] paths = {
            @"C:\Program Files\nodejs\node.exe",
            @"C:\Program Files (x86)\nodejs\node.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Nodejs", "node.exe")
        };
        foreach (var p in paths)
            if (File.Exists(p)) return p;
        return null;
    }
}
