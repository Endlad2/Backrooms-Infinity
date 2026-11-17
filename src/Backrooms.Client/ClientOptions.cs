namespace Backrooms.Client;

public sealed record ClientOptions(
    string LevelZip,
    string Nick,
    string? Skin,
    int Width,
    int Height,
    bool Fullscreen,
    bool Debug)
{
    public static ClientOptions Parse(string[] args)
    {
        string? level = null;
        var nick = "wanderer";
        string? skin = null;
        var width = 1280;
        var height = 720;
        var fullscreen = false;
        var debug = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            if (!arg.StartsWith("--", StringComparison.Ordinal) && level is null)
            {
                level = arg;
                continue;
            }

            string Next()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value after {arg}.");
                return args[++i];
            }

            switch (arg)
            {
                case "--level": level = Next(); break;
                case "--nick": nick = Next(); break;
                case "--skin": skin = Next(); break;
                case "--width": width = int.Parse(Next()); break;
                case "--height": height = int.Parse(Next()); break;
                case "--fullscreen": fullscreen = true; break;
                case "--debug": debug = true; break;
                case "--help":
                case "-h":
                    throw new HelpRequestedException();
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        if (string.IsNullOrWhiteSpace(level))
            throw new ArgumentException("A level ZIP is required. Use --level <path.zip>.");

        return new ClientOptions(Path.GetFullPath(level), nick, skin, width, height, fullscreen, debug);
    }

    public static string HelpText => """
Backrooms.Client
  --level <path.zip>    Required level pack
  --nick <name>         Player nickname
  --skin <path|id>      Reserved for future skin system
  --width <px>          Window width (default 1280)
  --height <px>         Window height (default 720)
  --fullscreen          Fullscreen mode
  --debug               Open debugger on startup
  F3                    Toggle debugger while running
""";
}

public sealed class HelpRequestedException : Exception { }
