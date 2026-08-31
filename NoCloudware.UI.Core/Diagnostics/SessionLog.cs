using System;
using System.Collections.ObjectModel;
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

    public static ObservableCollection<LogEntry> Entries { get; } = new();

    static SessionLog()
    {
        BindingOperations.EnableCollectionSynchronization(Entries, Sync);
    }

    public static void Add(string message, string? detail = null)
    {
        Entries.Insert(0, new LogEntry { Message = message, Detail = detail });
    }

    public static void Clear()
    {
        Entries.Clear();
    }
}
