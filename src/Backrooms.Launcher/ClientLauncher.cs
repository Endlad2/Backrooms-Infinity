using System.Diagnostics;

namespace Backrooms.Launcher;

public static class ClientLauncher
{
    public static bool Launch(string levelZip, string nick, string? skin)
    {
        var baseDir = AppContext.BaseDirectory;
        var exeName = OperatingSystem.IsWindows() ? "Backrooms.Client.exe" : "Backrooms.Client";
        var exe = Path.Combine(baseDir, exeName);
        var dll = Path.Combine(baseDir, "Backrooms.Client.dll");

        ProcessStartInfo psi;

        if (File.Exists(exe))
        {
            psi = new ProcessStartInfo(exe) { UseShellExecute = false };
        }
        else if (File.Exists(dll))
        {
            psi = new ProcessStartInfo("dotnet") { UseShellExecute = false };
            psi.ArgumentList.Add(dll);
        }
        else
        {
            var repoClient = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "Backrooms.Client"));
            psi = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                WorkingDirectory = repoClient
            };
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--");
        }

        psi.ArgumentList.Add("--level");
        psi.ArgumentList.Add(Path.GetFullPath(levelZip));
        psi.ArgumentList.Add("--nick");
        psi.ArgumentList.Add(nick);

        if (!string.IsNullOrWhiteSpace(skin))
        {
            psi.ArgumentList.Add("--skin");
            psi.ArgumentList.Add(skin);
        }

        return Process.Start(psi) is not null;
    }
}
