using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

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
        string? customName = null, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        const int maxRetries = 3;
        int[] delays = { 30_000, 60_000, 120_000 };

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                var result = await ExecuteDownloadAsync(url, outputPath, format, antiBlock, extractAudio, customName);
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
        string url, string outputPath, string format, bool antiBlock, bool extractAudio, string? customName = null)
    {
        try
        {
            var args = new List<string>();

            if (url.Contains("youtube.com") || url.Contains("youtu.be"))
            {
                args.Add("--extractor-args");
                args.Add(extractAudio
                    ? "youtube:player_client=android_music"
                    : "youtube:player_client=android,web");
            }

            // Detect available browser cookies dynamically
            var browser = BrowserCookieService.DetectAvailableBrowser();
            string? usedBrowser = browser;
            if (browser != null)
            {
                args.Add("--cookies-from-browser"); args.Add(browser);
            }

            string? nodePath = GetNodePath();
            if (nodePath != null)
            {
                args.Add("--js-runtimes");
                args.Add($"node:{nodePath}");
            }

            string? ffmpegDir = FindFfmpegDir();
            if (ffmpegDir != null)
            {
                args.Add("--ffmpeg-location");
                args.Add(ffmpegDir);
            }

            if (antiBlock)
            {
                var rng = new Random();
                args.Add("--sleep-interval"); args.Add(rng.Next(5, 15).ToString());
                args.Add("--max-sleep-interval"); args.Add(rng.Next(15, 30).ToString());
                args.Add("--limit-rate"); args.Add("5M");
                args.Add("--wait-for-video"); args.Add("5");
                args.Add("--retries"); args.Add("3");
                args.Add("--fragment-retries"); args.Add("3");
                args.Add("--no-mtime");
            }

            args.Add("-f"); args.Add(format);

            if (extractAudio)
            {
                args.Add("--extract-audio");
                string audioFmt = format.Contains("m4a") ? "m4a" :
                                  format.Contains("opus") ? "opus" :
                                  format.Contains("mp3") ? "mp3" :
                                  format.Contains("wav") ? "wav" : "m4a";
                args.Add("--audio-format"); args.Add(audioFmt);
            }
            else
            {
                // For video: recode to requested container if the direct codec isn't available
                string videoExt = GetRequestedVideoExt(format);
                if (!string.IsNullOrEmpty(videoExt) && videoExt != "mp4")
                {
                    args.Add("--recode-video"); args.Add(videoExt);
                }
            }

            args.Add("--ignore-errors");

            string safeOutput = string.IsNullOrEmpty(customName)
                ? Path.Combine(outputPath, "%(title)s.%(ext)s")
                : Path.Combine(outputPath, customName.Replace("%", "") + ".%(ext)s");
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

            // Use a controlled PATH so yt-dlp never scans the user's full PATH
            // (which may contain untrusted junctions like Codex's -> WinError 448).
            psi.Environment["PATH"] = BuildControlledPath(_ytdlpPath, nodePath, ffmpegDir);

            _currentProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

            // Kill process if cancellation is requested
            _cts?.Token.Register(() =>
            {
                try { if (_currentProcess != null && !_currentProcess.HasExited) _currentProcess.Kill(); } catch { }
            });

            _currentProcess.Start();
            Log?.Invoke($"yt-dlp iniciado (pid {_currentProcess.Id}): {url}");

            string? capturedFile = null;
            string? mergedFile = null;
            string stderrLog = "";

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

            var stderrTask = Task.Run(async () =>
            {
                var sb = new System.Text.StringBuilder();
                while (!_currentProcess!.HasExited && !_cts!.Token.IsCancellationRequested)
                {
                    var line = await _currentProcess.StandardError.ReadLineAsync(_cts.Token);
                    if (line == null) break;
                    sb.AppendLine(line);
                }
                stderrLog = sb.ToString();
            }, _cts?.Token ?? default);

            if (_currentProcess != null)
                await _currentProcess.WaitForExitAsync(_cts?.Token ?? default);
            await Task.WhenAll(outputTask, stderrTask);
            Log?.Invoke($"yt-dlp finalizó (exit {_currentProcess?.ExitCode})");

            bool ok = _currentProcess?.ExitCode == 0;
            string actualFile = mergedFile ?? capturedFile ?? "";

            string? errorMsg = null;
            if (!ok)
            {
                errorMsg = DetectLoginError(stderrLog, usedBrowser);
                if (errorMsg == null)
                    errorMsg = GetStderrError(stderrLog) ?? "Exit code non-zero";
                if (!string.IsNullOrWhiteSpace(stderrLog))
                    Log?.Invoke(stderrLog.Trim());
            }

            Completed?.Invoke(ok, ok ? null : errorMsg);
            return (ok, ok ? actualFile : null, ok ? null : errorMsg);
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

    private static string? GetStderrError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr)) return null;

        var lines = stderr.Split('\n');
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.Length == 0) continue;
            if (line.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0)
                return line.Length > 300 ? line[..300] : line;
        }
        var last = lines[^1].Trim();
        return last.Length > 300 ? last[..300] : last;
    }

    private static string? DetectLoginError(string stderr, string? usedBrowser)
    {
        if (string.IsNullOrEmpty(stderr)) return null;

        string lower = stderr.ToLowerInvariant();

        bool isLoginError = lower.Contains("sign in") ||
                            lower.Contains("sign in required") ||
                            lower.Contains("login required") ||
                            lower.Contains("private video") ||
                            lower.Contains("this video is private") ||
                            lower.Contains("http error 4") ||
                            lower.Contains("confirm your identity") ||
                            lower.Contains("confirm you are not a bot");

        if (!isLoginError) return null;

        if (usedBrowser == null)
            return "LOGIN_REQUIRED_NO_COOKIES";

        return "LOGIN_REQUIRED";
    }

    public static void CleanupPartFiles(string outputPath, string url)
    {
        try
        {
            if (!Directory.Exists(outputPath)) return;
            foreach (var f in Directory.GetFiles(outputPath, "*.part"))
            {
                try { File.Delete(f); } catch { }
            }
            foreach (var f in Directory.GetFiles(outputPath, "*.ytdl"))
            {
                try { File.Delete(f); } catch { }
            }
        }
        catch { }
    }

    private static string GetRequestedVideoExt(string format)
    {
        // Extract video extension from yt-dlp format string, e.g. "bestvideo[ext=webm]+bestaudio/best" -> "webm"
        var match = Regex.Match(format, @"\[ext=(\w+)\]");
        return match.Success ? match.Groups[1].Value : "";
    }

    public static string? GetNodePath()
    {
        string[] paths = {
            @"C:\Program Files\nodejs\node.exe",
            @"C:\Program Files (x86)\nodejs\node.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Nodejs", "node.exe")
        };
        foreach (var p in paths)
        {
            try { if (File.Exists(p)) return p; } catch { }
        }
        return null;
    }

    private static string? FindFfmpegDir()
    {
        var candidates = new List<string>();
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(path))
            candidates.AddRange(path.Split(';', StringSplitOptions.RemoveEmptyEntries));
        candidates.Add(Path.GetDirectoryName(Environment.ProcessPath) ?? "");

        foreach (var dir in candidates)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            try
            {
                if (File.Exists(Path.Combine(dir.Trim(), "ffmpeg.exe")))
                    return dir.Trim();
            }
            catch { }
        }
        return null;
    }

    public static string BuildControlledPath(string ytdlpPath, string? nodePath, string? ffmpegDir)
    {
        var dirs = new List<string>();
        string? ytDir = Path.GetDirectoryName(ytdlpPath);
        if (!string.IsNullOrEmpty(ytDir)) dirs.Add(ytDir);
        if (!string.IsNullOrEmpty(nodePath)) dirs.Add(Path.GetDirectoryName(nodePath) ?? "");
        if (!string.IsNullOrEmpty(ffmpegDir)) dirs.Add(ffmpegDir);
        string sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        dirs.Add(sys);
        string? sysRoot = Path.GetDirectoryName(sys);
        if (!string.IsNullOrEmpty(sysRoot)) dirs.Add(sysRoot);
        return string.Join(";", dirs.Where(d => !string.IsNullOrWhiteSpace(d)).Distinct());
    }
}
