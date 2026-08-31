using System;
using System.IO;

namespace NoCloudware.UI.Core.Diagnostics;

public static class SessionLog
{
    private static readonly object Sync = new();
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TubeMassDL", "session.log");

    public static string LogFilePath => LogFile;

    static SessionLog()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!); } catch { }
    }

    public static void Add(string message, string? detail = null)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(LogFile,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{(string.IsNullOrEmpty(detail) ? "" : " | " + detail)}{Environment.NewLine}");
            }
        }
        catch { }
    }

    public static void Clear()
    {
        try { lock (Sync) { File.WriteAllText(LogFile, ""); } } catch { }
    }
}
