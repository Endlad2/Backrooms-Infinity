using Backrooms.Engine.Math;

namespace Backrooms.Engine.Levels;

public enum EntityKind
{
    Primitive,
    Model
}

public sealed record TransformSpec(
    Vec3 Position,
    Vec3 Rotation,
    Vec3 Scale);

public sealed record TextureBinding(
    string Slot,
    string Source);

public sealed record ShaderBinding(
    string? Id,
    string Source,
    bool Enabled,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record ScriptBinding(
    string Event,
    string? Source,
    string? InlineCode);

public sealed record LevelEntity(
    string Id,
    EntityKind Kind,
    string? PrimitiveType,
    string? ModelSource,
    TransformSpec Transform,
    IReadOnlyList<TextureBinding> Textures,
    IReadOnlyList<ShaderBinding> Shaders,
    IReadOnlyList<ScriptBinding> Scripts,
    IReadOnlyDictionary<string, string> Properties);

public sealed record ChunkDefinition(
    string Id,
    IReadOnlyList<LevelEntity> Entities,
    IReadOnlyList<TextureTargetBinding> TextureTargets,
    IReadOnlyList<ShaderTargetBinding> ShaderTargets,
    IReadOnlyList<ScriptBinding> Scripts);

public sealed record TextureTargetBinding(
    string TargetId,
    string Slot,
    string Source);

public sealed record ShaderTargetBinding(
    string TargetId,
    string? ShaderId,
    string Source,
    bool Enabled,
    IReadOnlyDictionary<string, string> Parameters);

public sealed record InitScriptDefinition(
    string? Source,
    string? InlineCode);

public sealed record LevelDefinition(
    string Name,
    string Version,
    float ChunkSize,
    InitScriptDefinition? InitScript,
    IReadOnlyList<ChunkDefinition> Chunks,
    IReadOnlyDictionary<string, string> Metadata)
{
    public ChunkDefinition GetChunk(string id) =>
        Chunks.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.Ordinal))
        ?? throw new KeyNotFoundException($"Chunk '{id}' is not defined.");
}
