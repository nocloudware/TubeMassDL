using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NoCloudware.UI.Core.Diagnostics;
using NoCloudware.UI.Core.ViewModels;

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
        item.StatusText = "⏸";
        ItemProgress?.Invoke(item, 0);
        return true;
    }

    public void StopItem(BaseFileItem item)
    {
        string? outputPath = null;
        // Cancelar proceso activo
        if (_active.TryRemove(item, out var entry))
        {
            outputPath = entry.Task.OutputPath;
            entry.Cts.Cancel();
        }

        // Quitar de la cola si está esperando
        var filtered = _queue.Where(t => t.Item != item).ToList();
        while (_queue.TryDequeue(out _)) { }
        foreach (var t in filtered) _queue.Enqueue(t);

        // Limpiar archivos .part al detener explícitamente
        if (!string.IsNullOrEmpty(outputPath))
            YtDlpDownloader.CleanupPartFiles(outputPath, item.FilePath);

        // Marcar como error
        item.Status = FileStatus.Error;
        item.Progress = 0;
        item.ProgressBarVisible = false;
        item.StatusText = "✗";
        ItemCompleted?.Invoke(item, false);
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

        SessionLog.Add("Descargando: " + item.FilePath);

        try
        {
            string fileName = ResolveFileName(item);
            string? customName = CustomBaseName(item);

            if (FindExistingFile(task.OutputPath, customName, fileName) is { } existing)
            {
                SessionLog.Add("Ya existe (se omite): " + existing);
                item.ProgressBarVisible = false;
                item.Status = FileStatus.Processed;
                item.StatusText = "✓";
                ItemCompleted?.Invoke(item, true);
                return;
            }

            if (site.IsDirectFile)
            {
                var httpDl = new HttpDownloader();
                success = await httpDl.DownloadAsync(item.FilePath, task.OutputPath, fileName,
                    new Progress<int>(ReportProgress), ct);
            }
            else
            {
                var ytdlp = new YtDlpDownloader(_updater.GetBinaryPath());
                ytdlp.ProgressUpdated += p => ReportProgress(p);
                ytdlp.Log += msg => SessionLog.Add(msg);
                var (ok, _, err) = await ytdlp.DownloadAsync(item.FilePath, task.OutputPath,
                    task.Format, task.AntiBlock, task.ExtractAudio, customName, ct);
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

        if (success)
            SessionLog.Add("✓ " + item.FilePath);
        else
            SessionLog.Add("✗ " + item.FilePath, item.ResultMessage);

        item.ProgressBarVisible = false;
        item.Status = success ? FileStatus.Processed : FileStatus.Error;
        item.StatusText = success ? "✓" : "✗";

        if (!success && !string.IsNullOrEmpty(item.ResultMessage))
        {
            if (item.ResultMessage == "LOGIN_REQUIRED")
            {
                item.StatusText = Translations.Get("LoginRequired");
                item.ResultMessage = Translations.Get("LoginRequiredMsg");
            }
            else if (item.ResultMessage == "LOGIN_REQUIRED_NO_COOKIES")
            {
                item.StatusText = Translations.Get("CookiesNotFound");
                item.ResultMessage = Translations.Get("CookiesNotFoundMsg");
            }
            else
            {
                item.StatusText = "Error: " + item.ResultMessage;
            }
        }

        // Clean up .part files on permanent failure (not on cancel, which enables resume)
        if (!success && !string.IsNullOrEmpty(task.OutputPath))
        {
            YtDlpDownloader.CleanupPartFiles(task.OutputPath, item.FilePath);
        }

        ItemCompleted?.Invoke(item, success);
    }

    private static readonly string[] MediaExts = { ".mp4", ".avi", ".mkv", ".webm", ".mov", ".m4v", ".mp3", ".m4a", ".opus", ".wav", ".flac" };

    // Nombre base sin extensión de medios (para que el descargador agregue la real).
    private static string? CustomBaseName(BaseFileItem item)
    {
        var name = item.CustomOutputName;
        if (string.IsNullOrWhiteSpace(name)) return null;
        var ext = Path.GetExtension(name);
        return MediaExts.Contains(ext, StringComparer.OrdinalIgnoreCase) ? Path.ChangeExtension(name, null) : name;
    }

    private static string ResolveFileName(BaseFileItem item)
    {
        string uriName = Path.GetFileName(new Uri(item.FilePath).AbsolutePath);
        string? custom = CustomBaseName(item);
        return custom != null ? custom + Path.GetExtension(uriName) : uriName;
    }

    // Devuelve el archivo ya descargado en la carpeta de destino, si existe.
    // fileName: ruta exacta que escribirá el descargador directo.
    // baseName: nombre base sin extensión que usará yt-dlp (CSV/salida personalizada);
    //           si no existe la extensión exacta, busca cualquier formato de medios ya bajado.
    private static string? FindExistingFile(string outputPath, string? baseName, string fileName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath)) return null;

            string exact = Path.Combine(outputPath, fileName);
            if (File.Exists(exact)) return exact;

            if (!string.IsNullOrWhiteSpace(baseName))
            {
                return Directory.EnumerateFiles(outputPath)
                    .Where(f => MediaExts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase) &&
                                Path.GetFileNameWithoutExtension(f).Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault();
            }
            return null;
        }
        catch { return null; }
    }

    [Conditional("DEBUG")]
    public static void SelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(), "TubeMassDL_SelfTest_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "Cap 1.mp4"), "x");
            File.WriteAllText(Path.Combine(dir, "Otro.m4a"), "x");

            Debug.Assert(FindExistingFile(dir, null, "Cap 1.mp4") != null, "exacto existente no detectado");
            Debug.Assert(FindExistingFile(dir, "Cap 1", "Cap 1.webm") != null, "base por extensión distinta no detectada");
            Debug.Assert(FindExistingFile(dir, "Nada", "Nada.mp4") == null, "falso positivo");
            Debug.Assert(FindExistingFile(Path.Combine(dir, "nope"), "Cap 1", "Cap 1.mp4") == null, "carpeta inexistente");
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
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