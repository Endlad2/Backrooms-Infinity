using System.Text.Json;
using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Platform;

namespace Backrooms.Engine.Gameplay;

public sealed record OutpostTier(int Tier, float Radius, bool AllowsExtractor, string FlagItemId);

public sealed record OutpostRecord(string LevelId, string LevelName, int Tier, Vec3 Position, DateTimeOffset CreatedAt);

public sealed class OutpostService
{
    private readonly Dictionary<string, OutpostRecord> _outposts = new(StringComparer.OrdinalIgnoreCase);
    private static string SaveFile => Path.Combine(AppPaths.Root, "outposts.json");

    public IReadOnlyCollection<OutpostRecord> All => _outposts.Values;

    public static OutpostTier Tier(int tier) => tier switch
    {
        1 => new OutpostTier(1, 20f, false, "outpost_flag_1"),
        2 => new OutpostTier(2, 35f, true, "outpost_flag_2"),
        3 => new OutpostTier(3, 55f, true, "outpost_flag_3"),
        4 => new OutpostTier(4, 80f, true, "outpost_flag_4"),
        _ => new OutpostTier(1, 20f, false, "outpost_flag_1")
    };

    public OutpostService()
    {
        Load();
    }

    public bool TryPlace(string levelId, string levelName, int tier, Vec3 position, out string message)
    {
        if (_outposts.ContainsKey(levelId))
        {
            message = "На этом уровне уже есть аванпост.";
            return false;
        }
        var normalizedTier = Math.Clamp(tier, 1, 4);
        _outposts[levelId] = new OutpostRecord(levelId, levelName, normalizedTier, position, DateTimeOffset.UtcNow);
        Save();
        message = $"Аванпост {normalizedTier}-го уровня установлен.";
        return true;
    }

    public bool TryGet(string levelId, out OutpostRecord outpost) => _outposts.TryGetValue(levelId, out outpost!);

    public bool IsInside(string levelId, Vec3 position)
    {
        if (!TryGet(levelId, out var outpost)) return false;
        var d = position - outpost.Position;
        var radius = Tier(outpost.Tier).Radius;
        return d.X * d.X + d.Z * d.Z <= radius * radius;
    }

    public bool MonsterEntered(string levelId, Vec3 previous, Vec3 current) =>
        !IsInside(levelId, previous) && IsInside(levelId, current);

    public RespawnChoiceSession BeginRespawnChoice() => new(_outposts.Values.OrderBy(o => o.LevelName).ToArray());

    private void Load()
    {
        try
        {
            if (!File.Exists(SaveFile)) return;
            var rows = JsonSerializer.Deserialize<List<OutpostRecord>>(File.ReadAllText(SaveFile)) ?? new();
            foreach (var row in rows)
                _outposts[row.LevelId] = row;
        }
        catch
        {
            _outposts.Clear();
        }
    }

    private void Save()
    {
        AppPaths.EnsureCreated();
        File.WriteAllText(SaveFile, JsonSerializer.Serialize(_outposts.Values, new JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class RespawnChoiceSession
{
    private readonly IReadOnlyList<OutpostRecord> _choices;
    public IReadOnlyList<OutpostRecord> Choices => _choices;
    public DateTimeOffset Deadline { get; } = DateTimeOffset.UtcNow.AddSeconds(10);
    public OutpostRecord? Selected { get; private set; }

    public RespawnChoiceSession(IReadOnlyList<OutpostRecord> choices)
    {
        _choices = choices;
    }

    public bool Select(int index)
    {
        if (index < 0 || index >= _choices.Count || DateTimeOffset.UtcNow > Deadline)
            return false;
        Selected = _choices[index];
        return true;
    }

    public OutpostRecord? Resolve()
    {
        if (Selected is not null) return Selected;
        return DateTimeOffset.UtcNow >= Deadline ? _choices.FirstOrDefault() : null;
    }

    public int SecondsLeft => Math.Max(0, (int)Math.Ceiling((Deadline - DateTimeOffset.UtcNow).TotalSeconds));
}
