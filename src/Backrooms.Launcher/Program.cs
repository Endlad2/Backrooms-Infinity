using SDL3;

namespace Backrooms.Launcher;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
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

        if (!SDL.Init(SDL.InitFlags.Video | SDL.InitFlags.Audio))
        {
            Console.Error.WriteLine($"SDL init failed: {SDL.GetError()}");
            return 3;
        }

        if (!SDL.CreateWindowAndRenderer("Backrooms", 1280, 720, 0, out var window, out var renderer))
        {
            Console.Error.WriteLine($"Window creation failed: {SDL.GetError()}");
            SDL.Quit();
            return 4;
        }

        var assetRoot = Path.Combine(AppContext.BaseDirectory, "assets", "launcher");
        var deck = new LauncherMediaDeck(assetRoot);
        var running = true;
        var lastLabel = "";

        Console.WriteLine("Backrooms launcher");
        Console.WriteLine("ENTER: start client with --level; ESC: quit.");
        Console.WriteLine("Supplied font: assets/vcrosdmonorus_vhsicons.ttf");

        while (running)
        {
            while (SDL.PollEvent(out var e))
            {
                var type = (SDL.EventType)e.Type;
                if (type == SDL.EventType.Quit)
                {
                    running = false;
                }
                else if (type == SDL.EventType.KeyDown && !e.Key.Repeat)
                {
                    var scancode = (int)e.Key.Scancode;
                    if (scancode == 41) // ESC
                    {
                        running = false;
                    }
                    else if (scancode == 40) // RETURN
                    {
                        if (string.IsNullOrWhiteSpace(options.LevelZip))
                        {
                            Console.WriteLine("No level selected. Restart launcher with --level <level.zip>.");
                        }
                        else
                        {
                            ClientLauncher.Launch(options.LevelZip, options.Nick, options.Skin);
                        }
                    }
                }
            }

            var item = deck.Current;
            var motion = deck.Motion;

            if (!string.Equals(lastLabel, item.Label, StringComparison.Ordinal))
            {
                lastLabel = item.Label;
                Console.WriteLine($"Background: {Path.GetFileName(item.ImagePath)} | ambient: {Path.GetFileName(item.AudioPath)}");
            }

            // Platform shell fallback. LauncherMediaDeck contains the requested
            // pan/zoom timing and matching image/audio paths; a renderer/audio adapter
            // can consume these without changing launcher/menu logic.
            var shade = item.Label switch
            {
                "Level 4" => (byte)20,
                "Level 8" => (byte)12,
                _ => (byte)26
            };
            SDL.SetRenderDrawColor(renderer, shade, shade, shade, 255);
            SDL.RenderClear(renderer);
            SDL.RenderPresent(renderer);
        }

        SDL.DestroyRenderer(renderer);
        SDL.DestroyWindow(window);
        SDL.Quit();
        return 0;
    }
}
