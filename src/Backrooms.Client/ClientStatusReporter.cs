using Backrooms.Engine.Platform;
using Backrooms.Engine.Logging;

namespace Backrooms.Client;

public sealed class ClientStatusReporter : IDisposable
{
    private readonly RuntimeLogWriter _log;
    private readonly bool _launcherHandshake;

    public ClientStatusReporter(bool launcherHandshake)
    {
        _launcherHandshake = launcherHandshake;
        _log = RuntimeLogWriter.Create("client");
        Log("INFO", "Client", $"Log file: {_log.Path}");
    }

    public void Stage(string stage)
    {
        Log("INFO", "Stage", stage);
        if (_launcherHandshake)
        {
            Console.Out.WriteLine(StartupProtocol.StagePrefix + stage);
            Console.Out.Flush();
        }
    }

    public void Ready()
    {
        Log("INFO", "Client", "Startup complete; creating/showing game window.");
        if (_launcherHandshake)
        {
            Console.Out.WriteLine(StartupProtocol.ReadyMarker);
            Console.Out.Flush();
        }
    }

    public void Log(string severity, string source, string message)
    {
        _log.Write(severity, source, message);
        Console.Out.WriteLine($"[{DateTimeOffset.Now:HH:mm:ss}] {severity,-7} {source}: {message}");
        Console.Out.Flush();
    }

    public void Dispose() => _log.Dispose();
}
