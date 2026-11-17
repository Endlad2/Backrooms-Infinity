using Backrooms.Engine.Assets;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Rendering;
using Backrooms.Engine.Runtime;
using Backrooms.Engine.Scripting;
using Backrooms.Engine.UI;

namespace Backrooms.Client;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ClientOptions options;
        try
        {
            options = ClientOptions.Parse(args);
        }
        catch (HelpRequestedException)
        {
            Console.WriteLine(ClientOptions.HelpText);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(ClientOptions.HelpText);
            return 2;
        }

        var diagnostics = new DiagnosticBus();
        diagnostics.EntryAdded += e =>
            Console.WriteLine($"[{e.Timestamp:HH:mm:ss}] {e.Severity,-7} {e.Source}: {e.Message}");

        var debugState = new DebugOverlayState(diagnostics);
        if (options.Debug)
            debugState.Open();

        try
        {
            var parser = new LevelParser(diagnostics);
            using var package = LevelPackage.Open(options.LevelZip, parser);

            var world = new RuntimeWorld(diagnostics);
            var ui = new UiState();
            using IRenderer renderer = new WebGpuRendererBoundary(diagnostics);
            var generator = new GeneratorPlan();
            var lua = new LuaHost(diagnostics, world, ui, renderer, generator);

            new AssimpModelImporterBoundary(diagnostics).Probe();
            new JoltPhysicsBoundary(diagnostics).Probe();

            lua.RunInit(package);

            var eventRuntime = new LevelEventRuntime(lua, package);
            var app = new SdlClientApp(options, package.Definition, diagnostics, debugState, generator, eventRuntime);
            return app.Run();
        }
        catch (Exception ex)
        {
            diagnostics.Error("Client", ex.Message, ex.ToString());
            return 1;
        }
    }
}
