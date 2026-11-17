using Backrooms.Engine.Platform;

namespace Backrooms.Launcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppPaths.EnsureCreated();
        LauncherOptions options;
        try
        {
            options = LauncherOptions.Parse(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        using var app = new LauncherApp(options);
        return app.Run();
    }
}
