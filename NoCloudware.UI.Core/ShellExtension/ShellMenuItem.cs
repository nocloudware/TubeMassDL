namespace NoCloudware.UI.Core.ShellExtension;

public class ShellMenuItem
{
    public string Label { get; set; } = string.Empty;
    public string Argument { get; set; } = string.Empty;
    public bool IsSeparator { get; set; }
    public bool IsDefault { get; set; }
}
