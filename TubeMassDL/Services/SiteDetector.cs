using System.IO;
using System.Text.RegularExpressions;
using TubeMassDL.Models;

namespace TubeMassDL.Services;

public class SiteDetector
{
    private static readonly (string pattern, string name)[] Sites =
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

    public SiteInfo Detect(string url)
    {
        try
        {
            var uri = new Uri(url);
            string ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();
            if (DirectExtensions.Contains(ext))
                return new SiteInfo { Name = "Directo", SupportsYtDlp = false, IsDirectFile = true };

            foreach (var (pattern, name) in Sites)
            {
                if (Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
                    return new SiteInfo { Name = name, SupportsYtDlp = true, IsDirectFile = false };
            }
        }
        catch { }

        return new SiteInfo { Name = "Web", SupportsYtDlp = true, IsDirectFile = false };
    }
}