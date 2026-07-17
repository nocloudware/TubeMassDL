using System.Collections.Generic;

namespace NoCloudware.UI.Core.ShellExtension;

public interface IShellExtensionConfig
{
    bool IsEnabled { get; }
    string MenuTitle { get; }
    string MenuIcon { get; }
    string[] SupportedExtensions { get; }
    string ExecutablePath { get; }
    List<ShellMenuItem> MenuItems { get; }
}
