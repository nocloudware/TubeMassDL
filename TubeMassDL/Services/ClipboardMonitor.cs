using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using TubeMassDL.Models;

namespace TubeMassDL.Services;

public class ClipboardMonitor : IDisposable
{
    private DispatcherTimer? _timer;
    private string _lastClipboardText = string.Empty;
    private bool _isActive;
    private bool _paused;

    private static readonly (string pattern, string name)[] SupportedSites =
    {
        ("youtube.com|youtu.be", "YouTube"),
        ("vimeo.com", "Vimeo"),
        ("tiktok.com", "TikTok"),
        ("instagram.com", "Instagram"),
        ("facebook.com|fb.watch", "Facebook"),
        ("twitter.com|x.com", "X"),
        ("twitch.tv", "Twitch"),
        ("dailymotion.com", "Dailymotion"),
    };

    private static readonly string[] DirectExtensions = { ".mp4", ".avi", ".mkv", ".zip", ".pdf", ".exe", ".msi" };

    public event Action<CapturedLink>? LinkCaptured;

    public bool IsActive => _isActive;
    public bool IsPaused => _paused;

    public void Start()
    {
        if (_isActive) return;
        _isActive = true;

        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(500),
            DispatcherPriority.Background,
            OnTimerTick,
            System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher);
        _timer.Start();
    }

    public void Stop()
    {
        _isActive = false;
        _timer?.Stop();
        _timer = null;
    }

    public void Pause() => _paused = true;
    public void Resume() => _paused = false;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (_paused) return;

            string text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;
            if (text == _lastClipboardText) return;

            if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return;
            if (uri.Scheme != "http" && uri.Scheme != "https") return;
            if (!IsSupportedUrl(text)) return;

            _lastClipboardText = text;

            var link = new CapturedLink
            {
                Url = text,
                Timestamp = DateTime.Now,
                Type = ClassifyLink(text)
            };
            LinkCaptured?.Invoke(link);
        }
        catch { }
    }

    private static bool IsSupportedUrl(string url)
    {
        try
        {
            var ext = Path.GetExtension(new Uri(url).AbsolutePath).ToLowerInvariant();
            if (DirectExtensions.Contains(ext)) return true;

            foreach (var (pattern, _) in SupportedSites)
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static LinkType ClassifyLink(string url)
    {
        if (url.Contains("/playlist") || url.Contains("list=")) return LinkType.Playlist;
        if (url.Contains("/channel/") || url.Contains("/user/") || url.Contains("/@")) return LinkType.Channel;
        return LinkType.VideoLink;
    }

    public void Dispose() => Stop();
}