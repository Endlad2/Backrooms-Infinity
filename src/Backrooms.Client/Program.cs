using Backrooms.Engine.Assets;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Math;
using Backrooms.Engine.Platform;
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
        AppPaths.EnsureCreated();
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

        using var status = new ClientStatusReporter(options.LauncherHandshake);
        status.Stage("Запуск клиента и журнала");

        var diagnostics = new DiagnosticBus();
        diagnostics.EntryAdded += e =>
            status.Log(e.Severity.ToString().ToUpperInvariant(), e.Source, e.Message);

        var debugState = new DebugOverlayState(diagnostics);
        if (options.Debug)
            debugState.Open();

        try
        {
            status.Stage("Открытие архива уровня");
            var parser = new LevelParser(diagnostics);
            using var package = LevelPackage.Open(options.LevelZip, parser);

            status.Stage("Разбор level.xml и проверка ресурсов");
            var level = package.Definition;
            status.Log("INFO", "Level", $"Loaded '{level.Name}', chunks={level.Chunks.Count}, size={level.ChunkSize}.");

            status.Stage("Подготовка runtime мира");
            var world = new RuntimeWorld(diagnostics);
            var ui = new UiState();
            using IRenderer renderer = new WebGpuRendererBoundary(diagnostics);
            var generator = new GeneratorPlan();
            var lua = new LuaHost(diagnostics, world, ui, renderer, generator);

            status.Stage("Инициализация Assimp и Jolt");
            new AssimpModelImporterBoundary(diagnostics).Probe();
            new JoltPhysicsBoundary(diagnostics).Probe();

            status.Stage("Выполнение init Lua и выбор генератора");
            lua.RunInit(package);

            status.Stage("Планирование стартовых чанков");
            var templates = level.Chunks.Select(c => c.Id).ToArray();
            var placements = new ChunkPlanner().Plan(generator, templates, Vec3.Zero, level.ChunkSize);
            status.Log("INFO", "Generation", $"Initial plan contains {placements.Count} chunk placement(s); mode={generator.Mode}.");

            status.Stage("Привязка игровых событий и скриптов");
            var eventRuntime = new LevelEventRuntime(lua, package);

            status.Stage("Готово");
            status.Ready();
            var app = new SdlClientApp(options, level, diagnostics, debugState, generator, eventRuntime);
            return app.Run();
        }
        catch (Exception ex)
        {
            diagnostics.Error("Client", ex.Message, ex.ToString());
            return 1;
        }
    }
}
