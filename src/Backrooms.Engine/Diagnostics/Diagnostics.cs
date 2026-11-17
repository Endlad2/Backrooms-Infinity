using System.Collections.Concurrent;

namespace Backrooms.Engine.Diagnostics;

public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public sealed record DiagnosticEntry(
    DateTimeOffset Timestamp,
    DiagnosticSeverity Severity,
    string Source,
    string Message,
    string? Detail = null);

public sealed class DiagnosticBus
{
    private readonly ConcurrentQueue<DiagnosticEntry> _entries = new();

    public event Action<DiagnosticEntry>? EntryAdded;

    public IReadOnlyList<DiagnosticEntry> Snapshot() => _entries.ToArray();

    public void Info(string source, string message, string? detail = null) =>
        Add(new DiagnosticEntry(DateTimeOffset.Now, DiagnosticSeverity.Info, source, message, detail));

    public void Warn(string source, string message, string? detail = null) =>
        Add(new DiagnosticEntry(DateTimeOffset.Now, DiagnosticSeverity.Warning, source, message, detail));

    public void Error(string source, string message, string? detail = null) =>
        Add(new DiagnosticEntry(DateTimeOffset.Now, DiagnosticSeverity.Error, source, message, detail));

    private void Add(DiagnosticEntry entry)
    {
        _entries.Enqueue(entry);
        while (_entries.Count > 2000 && _entries.TryDequeue(out _)) { }
        EntryAdded?.Invoke(entry);
    }
}

public sealed class DebugOverlayState
{
    public bool IsOpen { get; private set; }

    public DebugOverlayState(DiagnosticBus diagnostics)
    {
        diagnostics.EntryAdded += entry =>
        {
            if (entry.Severity == DiagnosticSeverity.Error)
                IsOpen = true;
        };
    }

    public void Toggle() => IsOpen = !IsOpen;
    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
}
