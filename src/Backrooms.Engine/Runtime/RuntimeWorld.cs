using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Math;

namespace Backrooms.Engine.Runtime;

public sealed class RuntimeEntity
{
    public required string Id { get; init; }
    public required EntityKind Kind { get; init; }
    public required string ChunkInstanceId { get; init; }
    public Vec3 LocalPosition { get; set; }
    public Vec3 WorldPosition { get; set; }
    public Vec3 Rotation { get; set; }
    public Vec3 Scale { get; set; } = Vec3.One;
    public bool Visible { get; set; } = true;
    public Dictionary<string, string> Textures { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> EnabledShaders { get; } = new(StringComparer.Ordinal);
}

public sealed class RuntimeWorld
{
    private readonly Dictionary<string, RuntimeEntity> _entities = new(StringComparer.Ordinal);
    private readonly DiagnosticBus _diagnostics;

    public RuntimeWorld(DiagnosticBus diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public IEnumerable<RuntimeEntity> Entities => _entities.Values;

    public bool Exists(string id) => _entities.ContainsKey(id);

    public RuntimeEntity? TryGet(string id) =>
        _entities.TryGetValue(id, out var entity) ? entity : null;

    public RuntimeEntity Get(string id) =>
        TryGet(id) ?? throw new KeyNotFoundException($"Entity '{id}' does not exist.");

    public RuntimeEntity? Resolve(string id)
    {
        if (_entities.TryGetValue(id, out var direct))
            return direct;

        var suffix = "/" + id;
        return _entities.Values.FirstOrDefault(e => e.Id.EndsWith(suffix, StringComparison.Ordinal));
    }

    public void Add(RuntimeEntity entity)
    {
        if (!_entities.TryAdd(entity.Id, entity))
            throw new InvalidOperationException($"Runtime entity id '{entity.Id}' already exists.");
    }

    public bool Remove(string id)
    {
        var removed = _entities.Remove(id);
        if (!removed)
            _diagnostics.Warn("RuntimeWorld", $"Attempted to remove unknown entity '{id}'.");
        return removed;
    }

    public void Clear() => _entities.Clear();
}
