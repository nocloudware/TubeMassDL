using System;
using System.Linq;
using Microsoft.Win32;

namespace NoCloudware.UI.Core.ShellExtension;

public static class ShellExtensionRegistrar
{
    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    public static void Register(IShellExtensionConfig config, string guid)
    {
        if (!config.IsEnabled) return;

        Unregister(config, guid);

        foreach (var ext in config.SupportedExtensions)
        {
            var cleanExt = ext.StartsWith('.') ? ext : $".{ext}";
            var basePath = $@"Software\Classes\SystemFileAssociations\{cleanExt}\shell\{guid}";

            using var rootKey = Registry.CurrentUser.CreateSubKey(basePath);
            rootKey.SetValue("MUIVerb", config.MenuTitle);
            rootKey.SetValue("Icon", $"\"{config.ExecutablePath}\",0");
            rootKey.SetValue("SubCommands", string.Empty);

            int order = 1;
            foreach (var item in config.MenuItems)
            {
                if (item.IsSeparator) continue;

                var itemPath = $@"{basePath}\shell\{order:D2}_{item.Label.Replace(" ", "_")}";
                using var itemKey = Registry.CurrentUser.CreateSubKey(itemPath);
                itemKey.SetValue("MUIVerb", item.Label);

                using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{itemPath}\command");
                cmdKey.SetValue("", $"\"{config.ExecutablePath}\" {item.Argument} \"%1\"");
                order++;
            }
        }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public static void Unregister(IShellExtensionConfig config, string guid)
    {
        if (!config.IsEnabled) return;

        foreach (var ext in config.SupportedExtensions)
        {
            var cleanExt = ext.StartsWith('.') ? ext : $".{ext}";
            var path = $@"Software\Classes\SystemFileAssociations\{cleanExt}\shell";
            try
            {
                using var shellKey = Registry.CurrentUser.OpenSubKey(path, writable: true);
                shellKey?.DeleteSubKeyTree(guid, throwOnMissingSubKey: false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unregistering {ext}\\{guid}: {ex.Message}");
            }
        }

        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
    }

    public static bool IsInstalled(IShellExtensionConfig config, string guid)
    {
        if (!config.SupportedExtensions.Any()) return false;

        var ext = config.SupportedExtensions[0];
        var cleanExt = ext.StartsWith('.') ? ext : $".{ext}";
        var path = $@"Software\Classes\SystemFileAssociations\{cleanExt}\shell\{guid}";
        using var key = Registry.CurrentUser.OpenSubKey(path);
        return key != null;
    }
}
