namespace TubeMassDL.Models;

public class CapturedLink
{
    public string Url { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LinkType Type { get; set; } = LinkType.Unknown;
}