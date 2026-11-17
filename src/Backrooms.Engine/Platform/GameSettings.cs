using System.Text.Json;

namespace Backrooms.Engine.Platform;

public sealed record GameSettings(
    string Nickname = "wanderer",
    int Width = 1280,
    int Height = 720,
    bool Fullscreen = false,
    float MasterVolume = 0.75f)
{
    public static GameSettings Load()
    {
        AppPaths.EnsureCreated();
        if (!File.Exists(AppPaths.SettingsFile))
        {
            var defaults = new GameSettings();
            defaults.Save();
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(AppPaths.SettingsFile))
                   ?? new GameSettings();
        }
        catch
        {
            return new GameSettings();
        }
    }

    public void Save()
    {
        AppPaths.EnsureCreated();
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(AppPaths.SettingsFile, json);
    }
}
