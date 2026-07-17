using System;

namespace TubeMassDL.Models
{
    public class VideoInfo
    {
        public string Url { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public int Duration { get; set; }
        public string Thumbnail { get; set; } = string.Empty;
        public DownloadStatus Status { get; set; } = DownloadStatus.Pending;
        public int Progress { get; set; }
        public string Extension { get; set; } = "";

        public string DurationFormatted => $"{Duration / 60:D2}:{Duration % 60:D2}";
    }

    public enum DownloadStatus
    {
        Pending,
        Loading,
        Downloading,
        Completed,
        Error,
        Cancelled
    }
}
