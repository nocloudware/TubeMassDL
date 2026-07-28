using System.IO;

namespace TubeMassDL.Services;

public static class BrowserCookieService
{
    private static readonly (string Name, string PathPattern)[] Browsers =
    {
        ("chrome",  @"Google\Chrome\User Data\Default\Cookies"),
        ("edge",    @"Microsoft\Edge\User Data\Default\Cookies"),
        ("firefox", @"Mozilla\Firefox\Profiles\*.default-release\cookies.sqlite"),
        ("firefox", @"Mozilla\Firefox\Profiles\*.default\cookies.sqlite"),
        ("brave",   @"BraveSoftware\Brave-Browser\User Data\Default\Cookies"),
        ("opera",   @"Opera Software\Opera Stable\Cookies"),
    };

    public static string? DetectAvailableBrowser()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        foreach (var (name, pattern) in Browsers)
        {
            try
            {
                string basePath = pattern.StartsWith("Mozilla")
                    ? Path.Combine(appData, pattern)
                    : Path.Combine(localAppData, pattern);

                if (pattern.Contains('*'))
                {
                    string dir = Path.GetDirectoryName(basePath)!;
                    string filePattern = Path.GetFileName(basePath);
                    if (Directory.Exists(dir) && Directory.GetFiles(dir, filePattern).Length > 0)
                        return name;
                }
                else if (File.Exists(basePath))
                {
                    return name;
                }
            }
            catch { }
        }

        return null;
    }
}
