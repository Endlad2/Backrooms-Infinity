using SDL3;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Scripting;

namespace Backrooms.Client;

public sealed class SdlClientApp
{
    private readonly ClientOptions _options;
    private readonly LevelDefinition _level;
    private readonly DiagnosticBus _diagnostics;
    private readonly DebugOverlayState _debug;
    private readonly GeneratorPlan _generator;
    private readonly LevelEventRuntime _events;

    public SdlClientApp(
        ClientOptions options,
        LevelDefinition level,
        DiagnosticBus diagnostics,
        DebugOverlayState debug,
        GeneratorPlan generator,
        LevelEventRuntime events)
    {
        _options = options;
        _level = level;
        _diagnostics = diagnostics;
        _debug = debug;
        _generator = generator;
        _events = events;
    }

    public int Run()
    {
        if (!SDL.Init(SDL.InitFlags.Video))
        {
            _diagnostics.Error("SDL3", $"SDL_Init failed: {SDL.GetError()}");
            return 3;
        }

        if (!SDL.CreateWindowAndRenderer(
                $"{_level.Name} — {_options.Nick}",
                _options.Width,
                _options.Height,
                0,
                out var window,
                out var renderer))
        {
            _diagnostics.Error("SDL3", $"Window creation failed: {SDL.GetError()}");
            SDL.Quit();
            return 4;
        }

        _diagnostics.Info("Client",
            $"Client started. Generator={_generator.Mode}; F3 toggles diagnostics.");

        var running = true;
        while (running)
        {
            while (SDL.PollEvent(out var e))
            {
                var type = (SDL.EventType)e.Type;
                if (type == SDL.EventType.Quit)
                {
                    running = false;
                }
                else if (type == SDL.EventType.KeyDown)
                {
                    // SDL physical scancode 60 = F3.
                    if ((int)e.Key.Scancode == 60 && !e.Key.Repeat)
                    {
                        _debug.Toggle();
                        _diagnostics.Info("Debugger", _debug.IsOpen ? "Opened with F3." : "Closed with F3.");
                    }
                }
            }

            if (_debug.IsOpen)
                SDL.SetRenderDrawColor(renderer, 45, 18, 18, 255);
            else
                SDL.SetRenderDrawColor(renderer, 8, 10, 11, 255);

            SDL.RenderClear(renderer);
            SDL.RenderPresent(renderer);
        }

        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);
        SDL.Quit();
        return 0;
    }
}
