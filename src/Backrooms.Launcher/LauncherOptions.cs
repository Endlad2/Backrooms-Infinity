namespace Backrooms.Launcher;

public sealed record LauncherOptions(string? LevelZip, string? Nick, string? Skin)
{
    public static LauncherOptions Parse(string[] args)
    {
        string? level = null;
        string? nick = null;
        string? skin = null;

        for (var i = 0; i < args.Length; i++)
        {
            string Next()
            {
                if (i + 1 >= args.Length)
                    throw new ArgumentException($"Missing value after {args[i]}.");
                return args[++i];
            }

            switch (args[i])
            {
                case "--level": level = Path.GetFullPath(Next()); break;
                case "--nick": nick = Next(); break;
                case "--skin": skin = Next(); break;
            }
        }

        return new LauncherOptions(level, nick, skin);
    }
}
