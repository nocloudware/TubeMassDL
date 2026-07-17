using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using NoCloudware.UI.Core.ViewModels;
using TubeMassDL.Models;

namespace TubeMassDL.Services;

public class LinkCollector
{
    private readonly YtdlpUpdater _updater = new();
    private readonly Dictionary<string, List<BaseFileItem>> _playlistCache = new();
    private readonly HashSet<string> _expandedPlaylists = new();
    private int _captureCount;

    public ObservableCollection<BaseFileItem> Items { get; } = new();
    public event Action? QueueUpdated;

    public LinkCollector()
    {
        Items.CollectionChanged += OnCollectionChanged;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueUpdated?.Invoke();
    }

    private BaseFileItem MakeItem(string url, string name, string source, bool isChild)
    {
        var item = new BaseFileItem
        {
            FilePath = url,
            FileName = name,
            SourceText = source,
            Status = FileStatus.Queued,
            IsSelected = true,
            StatusText = "Pendiente",
            Progress = 0,
            ProgressBarVisible = false
        };
        item.DeleteCommand = new RelayCommand(_ =>
        {
            RemoveItem(item);
        });
        item.TogglePlaylistCommand = new RelayCommand(_ =>
        {
            TogglePlaylist(item);
        });
        return item;
    }

    public void AddOrUpdate(CapturedLink link)
    {
        if (Items.Any(i => i.FilePath.Equals(link.Url, StringComparison.OrdinalIgnoreCase)))
            return;

        _captureCount++;
        var sourceName = DetectSource(link.Url);
        bool isPlaylist = link.Type == LinkType.Playlist || link.Type == LinkType.Channel;

        var item = MakeItem(link.Url,
            isPlaylist ? "📁 " + TruncateTitle(link.Url) : TruncateTitle(link.Url),
            isPlaylist ? "📁 Playlist" : sourceName,
            false);
        if (isPlaylist) item.ResultMessage = "+";
        Items.Insert(0, item);

        if (isPlaylist)
            _ = ExpandPlaylistAsync(link.Url);
    }

    private async Task ExpandPlaylistAsync(string playlistUrl)
    {
        try
        {
            var ytdlpPath = _updater.GetBinaryPath();
            if (!File.Exists(ytdlpPath)) return;

            var psi = new ProcessStartInfo
            {
                FileName = ytdlpPath,
                Arguments = $"--flat-playlist --dump-json --no-warnings --ignore-errors \"{playlistUrl}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return;

            string json = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            if (string.IsNullOrWhiteSpace(json)) return;

            var children = new List<BaseFileItem>();
            var lines = json.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < lines.Length; i++)
            {
                try
                {
                    using var doc2 = JsonDocument.Parse(lines[i]);
                    var entry = doc2.RootElement;

                    var url = entry.TryGetProperty("url", out var u) ? u.GetString() : null;
                    var title = entry.TryGetProperty("title", out var t) ? t.GetString() : "";

                    if (string.IsNullOrEmpty(url)) continue;

                    bool isLast = (i == lines.Length - 1);
                    string treeChar = isLast ? "└── " : "├── ";
                    var childName = $"{treeChar}🎬 {(title?.Length > 70 ? title[..67] + "..." : title ?? TruncateTitle(url))}";

                    children.Add(MakeItem(url, childName, DetectSource(url), true));
                }
                catch { }
            }

            _playlistCache[playlistUrl] = children;
            // Collapsed by default: don't insert children into Items

            var parentIndex = -1;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].FilePath == playlistUrl && Items[i].SourceText == "📁 Playlist")
                { parentIndex = i; break; }
            }

            if (parentIndex >= 0)
            {
                Items[parentIndex].FileName = $"📁 {TruncateTitle(playlistUrl)} ({children.Count})";
                Items[parentIndex].ResultMessage = "+";
            }
        }
        catch { }
    }

    public void TogglePlaylist(BaseFileItem item)
    {
        if (item.SourceText != "📁 Playlist") return;
        try { File.AppendAllText("toggle.log", $"[{DateTime.Now:HH:mm:ss}] Toggle: {item.FilePath}, expanded={_expandedPlaylists.Contains(item.FilePath)}\n"); } catch { }

        if (_expandedPlaylists.Contains(item.FilePath))
        {
            // Collapse
            if (_playlistCache.TryGetValue(item.FilePath, out var children))
            {
                foreach (var child in children) Items.Remove(child);
                _expandedPlaylists.Remove(item.FilePath);
                item.ResultMessage = "+";
                try { File.AppendAllText("toggle.log", $"  → Collapsed, removed {children.Count}\n"); } catch { }
            }
        }
        else
        {
            // Expand - populate cache if needed
            if (!_playlistCache.ContainsKey(item.FilePath))
            {
                var pName = TruncateTitle(item.FilePath);
                var dummy = MakeItem(item.FilePath + "#dummy", "  ⚠️ Sin conexión a yt-dlp", "Error", true);
                _playlistCache[item.FilePath] = new List<BaseFileItem> { dummy };
                Items[Items.IndexOf(item)].FileName = $"📁 {pName} (1)";
                try { File.AppendAllText("toggle.log", $"  → Dummy created\n"); } catch { }
            }

            if (_playlistCache.TryGetValue(item.FilePath, out var children))
            {
                try { File.AppendAllText("toggle.log", $"  → Cache found, count={children.Count}\n"); } catch { }
                var idx = Items.IndexOf(item);
                if (idx >= 0)
                {
                    for (int j = 0; j < children.Count; j++)
                        Items.Insert(idx + 1 + j, children[j]);
                }
                _expandedPlaylists.Add(item.FilePath);
                item.ResultMessage = "−";
            }
        }
    }

    public void RefreshTreeIcons()
    {
        foreach (var item in Items)
        {
            if (item.SourceText == "📁 Playlist")
                item.ResultMessage = _expandedPlaylists.Contains(item.FilePath) ? "−" : "+";
        }
    }

    public bool IsPlaylistExpanded(string url) => _expandedPlaylists.Contains(url);

    public IEnumerable<BaseFileItem> GetSelectedItems() =>
        Items.Where(i => i.IsSelected && i.Status is FileStatus.Queued or FileStatus.Error);

    public IEnumerable<BaseFileItem> GetItemsByStatus(FileStatus status) =>
        Items.Where(i => i.Status == status);

    public int CountByStatus(FileStatus status) =>
        Items.Count(i => i.Status == status);

    public void ClearCompleted()
    {
        var toRemove = Items.Where(i => i.Status is FileStatus.Processed or FileStatus.Error).ToList();
        foreach (var item in toRemove) Items.Remove(item);
    }

    public void ClearAll()
    {
        Items.Clear();
        _playlistCache.Clear();
        _expandedPlaylists.Clear();
    }

    public void RemoveItem(BaseFileItem item)
    {
        if (item.SourceText == "📁 Playlist")
        {
            if (_playlistCache.TryGetValue(item.FilePath, out var children))
            {
                foreach (var child in children) Items.Remove(child);
                _playlistCache.Remove(item.FilePath);
            }
            _expandedPlaylists.Remove(item.FilePath);
        }
        else
        {
            foreach (var kvp in _playlistCache)
            {
                if (kvp.Value.Contains(item))
                {
                    kvp.Value.Remove(item);
                    Items.Remove(item);
                    // Update parent count
                    var parent = Items.FirstOrDefault(i => i.FilePath == kvp.Key && i.SourceText == "📁 Playlist");
                    if (parent != null)
                        parent.FileName = $"📁 {TruncateTitle(kvp.Key)} ({kvp.Value.Count})";
                    return;
                }
            }
        }
        Items.Remove(item);
    }

    private static string TruncateTitle(string url)
    {
        if (url.Length <= 60) return url;
        return url[..57] + "...";
    }

    private static string DetectSource(string url)
    {
        var lower = url.ToLowerInvariant();
        if (lower.Contains("youtube.com") || lower.Contains("youtu.be")) return "YouTube";
        if (lower.Contains("vimeo.com")) return "Vimeo";
        if (lower.Contains("tiktok.com")) return "TikTok";
        if (lower.Contains("instagram.com")) return "Instagram";
        if (lower.Contains("facebook.com") || lower.Contains("fb.watch")) return "Facebook";
        if (lower.Contains("twitter.com") || lower.Contains("x.com")) return "X";
        if (lower.Contains("twitch.tv")) return "Twitch";
        if (lower.Contains("dailymotion.com")) return "Dailymotion";
        try
        {
            var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            if (new[] { ".mp4", ".avi", ".mkv", ".zip", ".pdf", ".exe", ".msi" }.Contains(ext)) return "Directo";
        }
        catch { }
        return "Web";
    }

    private class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
    }
}