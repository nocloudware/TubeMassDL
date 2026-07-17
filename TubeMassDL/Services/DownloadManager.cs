using System.Diagnostics;
using System.IO;
using NoCloudware.UI.Core.ViewModels;
using TubeMassDL.Models;

namespace TubeMassDL.Services;

public class DownloadManager
{
    private readonly SiteDetector _siteDetector = new();
    private readonly YtdlpUpdater _updater = new();
    private CancellationTokenSource? _cts;
    private bool _isPaused;
    private bool _isRunning;

    public event Action<BaseFileItem, int>? ItemProgress;
    public event Action<BaseFileItem, bool>? ItemCompleted;
    public event Action? AllCompleted;

    public bool IsRunning => _isRunning;
    public int MaxConcurrent { get; set; } = 3;

    public async Task StartAsync(IEnumerable<BaseFileItem> items, string outputPath,
        string format, bool antiBlock, bool extractAudio = false)
    {
        if (_isRunning) return;
        _isRunning = true;
        _isPaused = false;
        _cts = new CancellationTokenSource();

        var queue = new Queue<BaseFileItem>(items);
        var active = new List<Task>();

        while (queue.Count > 0 || active.Count > 0)
        {
            if (_cts.Token.IsCancellationRequested) break;

            while (_isPaused)
            {
                await Task.Delay(200);
                if (_cts.Token.IsCancellationRequested) break;
            }

            while (queue.Count > 0 && active.Count < MaxConcurrent)
            {
                var item = queue.Dequeue();
                active.Add(ProcessItemAsync(item, outputPath, format, antiBlock, extractAudio));
            }

            if (active.Count > 0)
            {
                var done = await Task.WhenAny(active);
                active.Remove(done);
            }
        }

        _isRunning = false;
        AllCompleted?.Invoke();
    }

    private async Task ProcessItemAsync(BaseFileItem item, string outputPath,
        string format, bool antiBlock, bool extractAudio)
    {
        item.Progress = 0;
        item.ProgressBarVisible = true;
        item.Status = FileStatus.Processing;

        var site = _siteDetector.Detect(item.FilePath);
        item.SourceText = site.Name;

        var progress = new Progress<int>(p =>
        {
            item.Progress = p;
            item.StatusText = $"{p}%";
            ItemProgress?.Invoke(item, p);
        });

        bool success;
        try
        {
            if (site.IsDirectFile)
            {
                var httpDl = new HttpDownloader();
                string fileName = Path.GetFileName(new Uri(item.FilePath).AbsolutePath);
                success = await httpDl.DownloadAsync(item.FilePath, outputPath, fileName, progress, _cts!.Token);
            }
            else
            {
                var ytdlp = new YtDlpDownloader(_updater.GetBinaryPath());
                var (ok, _, err) = await ytdlp.DownloadAsync(item.FilePath, outputPath,
                    format, antiBlock, extractAudio, _cts!.Token);
                success = ok;
                if (!ok && err != null) item.ResultMessage = err;
            }
        }
        catch
        {
            success = false;
        }

        item.ProgressBarVisible = false;
        item.Status = success ? FileStatus.Processed : FileStatus.Error;
        item.StatusText = success ? "✓" : "✗";
        ItemCompleted?.Invoke(item, success);
    }

    public void Pause() { _isPaused = true; }
    public void Resume() { _isPaused = false; }
    public void Stop()
    {
        _cts?.Cancel();
        _isRunning = false;
    }
}