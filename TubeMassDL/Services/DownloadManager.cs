using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NoCloudware.UI.Core.ViewModels;
using TubeMassDL.Models;

namespace TubeMassDL.Services;

public class DownloadManager
{
    private readonly SiteDetector _siteDetector = new();
    private readonly YtdlpUpdater _updater = new();
    private CancellationTokenSource? _cts;
    private bool _isPaused;

    private readonly ConcurrentQueue<DownloadTask> _queue = new();
    private readonly ConcurrentDictionary<BaseFileItem, (CancellationTokenSource Cts, DownloadTask Task)> _active = new();
    private int _runningCount;
    private Task? _loopTask;

    public event Action<BaseFileItem, int>? ItemProgress;
    public event Action<BaseFileItem, bool>? ItemCompleted;
    public event Action? AllCompleted;

    public int MaxConcurrent { get; set; } = 3;
    public bool IsRunning => _runningCount > 0 || (_queue.Count > 0);
    public int ActiveCount => _runningCount;
    public int QueuedCount => _queue.Count;
    public bool IsPaused => _isPaused;

    public void Enqueue(IEnumerable<DownloadTask> tasks)
    {
        foreach (var t in tasks)
            _queue.Enqueue(t);
        EnsureLoopRunning();
    }

    public void Enqueue(DownloadTask task)
    {
        _queue.Enqueue(task);
        EnsureLoopRunning();
    }

    private void EnsureLoopRunning()
    {
        if (_loopTask == null || _loopTask.IsCompleted)
        {
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunLoopAsync());
        }
    }

    private async Task RunLoopAsync()
    {
        while (!_cts!.Token.IsCancellationRequested)
        {
            if (_isPaused)
            {
                await Task.Delay(200);
                continue;
            }

            if (_runningCount >= MaxConcurrent)
            {
                await Task.Delay(200);
                continue;
            }

            if (!_queue.TryDequeue(out var task))
            {
                if (_runningCount == 0)
                {
                    AllCompleted?.Invoke();
                    return;
                }
                await Task.Delay(200);
                continue;
            }

            Interlocked.Increment(ref _runningCount);
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            _active[task.Item] = (linkedCts, task);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ProcessItemAsync(task, linkedCts.Token);
                }
                finally
                {
                    _active.TryRemove(task.Item, out _);
                    Interlocked.Decrement(ref _runningCount);
                }
            });
        }
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public void Resume()
    {
        _isPaused = false;
        _cts ??= new CancellationTokenSource();
        EnsureLoopRunning();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _queue.Clear();
        foreach (var kvp in _active)
            kvp.Value.Cts.Cancel();
        _runningCount = 0;
    }

    public bool PauseAndRequeue(BaseFileItem item)
    {
        // Cancelar proceso activo
        if (_active.TryRemove(item, out var entry))
        {
            entry.Cts.Cancel();
        }

        // Quitar de la cola si está esperando
        var filtered = _queue.Where(t => t.Item != item).ToList();
        while (_queue.TryDequeue(out _)) { }
        foreach (var t in filtered) _queue.Enqueue(t);

        // Notificar al UI
        item.Status = FileStatus.Queued;
        item.Progress = 0;
        item.ProgressBarVisible = false;
        item.StatusText = "Detenido";
        ItemProgress?.Invoke(item, 0);
        return true;
    }

    private async Task ProcessItemAsync(DownloadTask task, CancellationToken ct)
    {
        var item = task.Item;
        if (ct.IsCancellationRequested) return;

        item.Progress = 0;
        item.ProgressBarVisible = true;
        item.Status = FileStatus.Processing;
        var site = _siteDetector.Detect(item.FilePath);
        item.SourceText = site.Name;

        // Wrap progress reporting so a parsing error doesn't kill the whole callback
        void ReportProgress(int p)
        {
            try
            {
                item.Progress = p;
                item.StatusText = $"{p}%";
                ItemProgress?.Invoke(item, p);
            }
            catch { }
        }

        bool success = false;

        try
        {
            if (site.IsDirectFile)
            {
                var httpDl = new HttpDownloader();
                string fileName = Path.GetFileName(new Uri(item.FilePath).AbsolutePath);
                success = await httpDl.DownloadAsync(item.FilePath, task.OutputPath, fileName,
                    new Progress<int>(ReportProgress), ct);
            }
            else
            {
                var ytdlp = new YtDlpDownloader(_updater.GetBinaryPath());
                ytdlp.ProgressUpdated += p => ReportProgress(p);
                var (ok, _, err) = await ytdlp.DownloadAsync(item.FilePath, task.OutputPath,
                    task.Format, task.AntiBlock, task.ExtractAudio, ct);
                success = ok;
                if (!ok && err != null) item.ResultMessage = err;
            }
        }
        catch (OperationCanceledException)
        {
            item.Status = FileStatus.Queued;
            item.ProgressBarVisible = false;
            item.Progress = 0;
            item.StatusText = "Pendiente";
            ItemProgress?.Invoke(item, 0);
            return;
        }
        catch (Exception ex)
        {
            item.ResultMessage = ex.Message;
            success = false;
        }

        item.ProgressBarVisible = false;
        item.Status = success ? FileStatus.Processed : FileStatus.Error;
        item.StatusText = success ? "✓" : "✗";
        if (!success && !string.IsNullOrEmpty(item.ResultMessage))
            item.StatusText = "Error: " + item.ResultMessage;
        ItemCompleted?.Invoke(item, success);
    }
}

public class DownloadTask
{
    public BaseFileItem Item { get; set; } = null!;
    public string OutputPath { get; set; } = "";
    public string Format { get; set; } = "";
    public bool AntiBlock { get; set; } = true;
    public bool ExtractAudio { get; set; }
}