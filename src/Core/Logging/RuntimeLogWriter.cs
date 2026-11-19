using System.Text;

namespace Backrooms.Engine.Logging;

public sealed class RuntimeLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    private RuntimeLogWriter(StreamWriter writer, string path)
    {
        _writer = writer;
        Path = path;
    }

    public string Path { get; }

    public static RuntimeLogWriter Create(string component)
    {
        Platform.AppPaths.EnsureCreated();
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss-fff");
        var path = System.IO.Path.Combine(Platform.AppPaths.Logs, $"{component}-{stamp}.log");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
        return new RuntimeLogWriter(new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true }, path);
    }

    public void Write(string severity, string source, string message)
    {
        var line = $"[{DateTimeOffset.Now:O}] {severity,-7} {source}: {message}";
        lock (_gate)
            _writer.WriteLine(line);
    }

    public void Dispose()
    {
        lock (_gate)
            _writer.Dispose();
    }
}
