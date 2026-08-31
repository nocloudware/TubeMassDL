using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Data;

namespace NoCloudware.UI.Core.Diagnostics;

public class LogEntry
{
    public DateTime Time { get; } = DateTime.Now;
    public string Message { get; init; } = "";
    public string? Detail { get; init; }
}

public static class SessionLog
{
    private static readonly object Sync = new();
    private static readonly string LogFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TubeMassDL", "session.log");

    public static ObservableCollection<LogEntry> Entries { get; } = new();
    public static string LogFilePath => LogFile;

    static SessionLog()
    {
        BindingOperations.EnableCollectionSynchronization(Entries, Sync);
        try { Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!); } catch { }
    }

    public static void Add(string message, string? detail = null)
    {
        lock (Sync)
        {
            Entries.Insert(0, new LogEntry { Message = message, Detail = detail });
            try
            {
                File.AppendAllText(LogFile,
                    $"[{DateTime.Now:HH:mm:ss}] {message}{(string.IsNullOrEmpty(detail) ? "" : " | " + detail)}{Environment.NewLine}");
            }
            catch { }
        }
    }

    public static void Clear()
    {
        lock (Sync)
        {
            Entries.Clear();
            try { File.WriteAllText(LogFile, ""); } catch { }
        }
    }

    public static string ToText()
    {
        lock (Sync)
        {
            var sb = new StringBuilder();
            foreach (var e in Entries)
            {
                sb.Append($"[{e.Time:HH:mm:ss}] {e.Message}");
                if (!string.IsNullOrEmpty(e.Detail)) sb.Append(" | ").Append(e.Detail);
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
