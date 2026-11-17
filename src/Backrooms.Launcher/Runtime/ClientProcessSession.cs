using Backrooms.Engine.Platform;
using System.Diagnostics;

namespace Backrooms.Launcher.Runtime;

public sealed class ClientProcessSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly CancellationTokenSource _cts = new();
    private Task _stdoutPump = Task.CompletedTask;
    private Task _stderrPump = Task.CompletedTask;
    private bool _pumpsStarted;

    private ClientProcessSession(Process process) => _process = process;

    public event Action<string>? StageChanged;
    public event Action<string>? LogLine;
    public event Action? Ready;

    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public static ClientProcessSession Start(string levelZip, string nick, string? skin, int width, int height, bool fullscreen)
    {
        var psi = BuildStartInfo(levelZip, nick, skin, width, height, fullscreen);
        var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("Failed to start Backrooms.Client.");
        return new ClientProcessSession(process);
    }

    // Subscribe to events before calling BeginPumps so no early startup stage/READY marker is lost.
    public void BeginPumps()
    {
        if (_pumpsStarted) return;
        _pumpsStarted = true;
        _stdoutPump = PumpAsync(_process.StandardOutput, isError: false, _cts.Token);
        _stderrPump = PumpAsync(_process.StandardError, isError: true, _cts.Token);
    }

    public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        BeginPumps();
        await _process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(_stdoutPump, _stderrPump);
        return _process.ExitCode;
    }

    private async Task PumpAsync(StreamReader reader, bool isError, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;

                if (!isError && line.StartsWith(StartupProtocol.StagePrefix, StringComparison.Ordinal))
                    StageChanged?.Invoke(line[StartupProtocol.StagePrefix.Length..].Trim());
                else if (!isError && string.Equals(line, StartupProtocol.ReadyMarker, StringComparison.Ordinal))
                    Ready?.Invoke();
                else
                    LogLine?.Invoke((isError ? "ERR " : string.Empty) + line);
            }
        }
        catch (OperationCanceledException) { }
    }

    private static ProcessStartInfo BuildStartInfo(string levelZip, string nick, string? skin, int width, int height, bool fullscreen)
    {
        var baseDir = AppContext.BaseDirectory;
        var exeName = OperatingSystem.IsWindows() ? "Backrooms.Client.exe" : "Backrooms.Client";
        var exe = Path.Combine(baseDir, exeName);
        var dll = Path.Combine(baseDir, "Backrooms.Client.dll");
        ProcessStartInfo psi;

        if (File.Exists(exe))
            psi = new ProcessStartInfo(exe);
        else if (File.Exists(dll))
        {
            psi = new ProcessStartInfo("dotnet");
            psi.ArgumentList.Add(dll);
        }
        else
        {
            var project = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Backrooms.Client", "Backrooms.Client.csproj"));
            psi = new ProcessStartInfo("dotnet");
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(project);
            psi.ArgumentList.Add("--");
        }

        psi.UseShellExecute = false;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.CreateNoWindow = true;
        psi.WorkingDirectory = baseDir;

        psi.ArgumentList.Add("--level");
        psi.ArgumentList.Add(Path.GetFullPath(levelZip));
        psi.ArgumentList.Add("--nick");
        psi.ArgumentList.Add(nick);
        psi.ArgumentList.Add("--width");
        psi.ArgumentList.Add(width.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--height");
        psi.ArgumentList.Add(height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        psi.ArgumentList.Add("--launcher-handshake");
        if (fullscreen) psi.ArgumentList.Add("--fullscreen");
        if (!string.IsNullOrWhiteSpace(skin))
        {
            psi.ArgumentList.Add("--skin");
            psi.ArgumentList.Add(skin);
        }
        return psi;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (!_process.HasExited)
        {
            try { _process.Kill(entireProcessTree: true); } catch { }
        }
        try { await Task.WhenAll(_stdoutPump, _stderrPump); } catch { }
        _process.Dispose();
        _cts.Dispose();
    }
}
