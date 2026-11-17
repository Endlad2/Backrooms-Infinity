namespace Backrooms.Engine.Platform;

public static class AppPaths
{
    public const string AppFolderName = ".backrooms_infinity";

    public static string Root { get; } = ResolveRoot();
    public static string Levels => Path.Combine(Root, "levels");
    public static string Models => Path.Combine(Root, "models");
    public static string Settings => Path.Combine(Root, "settings");
    public static string Game => Path.Combine(Root, "game");
    public static string Assets => Path.Combine(Root, "assets");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Cache => Path.Combine(Root, "cache");
    public static string PreviewCache => Path.Combine(Cache, "previews");
    public static string Temp => Path.Combine(Root, "temp");
    public static string SettingsFile => Path.Combine(Settings, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Levels);
        Directory.CreateDirectory(Models);
        Directory.CreateDirectory(Settings);
        Directory.CreateDirectory(Game);
        Directory.CreateDirectory(Assets);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(PreviewCache);
        Directory.CreateDirectory(Temp);
    }

    public static string ResolveAsset(string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var installed = Path.Combine(Assets, normalized);
        if (File.Exists(installed))
            return installed;

        var besideBinary = Path.Combine(AppContext.BaseDirectory, "assets", normalized);
        return besideBinary;
    }

    public static string LevelZipPath(string displayName)
        => Path.Combine(Levels, MakeSafeFileName(displayName) + ".zip");

    public static string MakeSafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) || ch is '/' or '\\' ? '_' : ch).ToArray();
        var safe = new string(chars).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(safe) ? "level" : safe;
    }

    private static string ResolveRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(roaming))
                roaming = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(roaming, AppFolderName);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
            home = Environment.GetEnvironmentVariable("HOME") ?? AppContext.BaseDirectory;
        return Path.Combine(home, AppFolderName);
    }
}
