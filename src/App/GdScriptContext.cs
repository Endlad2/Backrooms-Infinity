using System.Text.Json;
using Godot;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Rendering;
using Backrooms.Engine.Runtime;
using Backrooms.Engine.UI;

namespace Backrooms.Infinity.App;

/// <summary>
/// Stable bridge exposed to runtime GDScript level scripts.
/// Keep this API intentionally small; Lua remains the preferred scripting language.
/// </summary>
public sealed partial class GdScriptContext : RefCounted
{
    private readonly DiagnosticBus _diagnostics;
    private readonly RuntimeWorld _world;
    private readonly UiState _ui;
    private readonly IRenderer _renderer;
    private readonly GeneratorPlan _generator;

    public string CurrentTarget { get; set; } = string.Empty;

    public GdScriptContext(
        DiagnosticBus diagnostics,
        RuntimeWorld world,
        UiState ui,
        IRenderer renderer,
        GeneratorPlan generator)
    {
        _diagnostics = diagnostics;
        _world = world;
        _ui = ui;
        _renderer = renderer;
        _generator = generator;
    }

    public void LogInfo(string message) => _diagnostics.Info("GDScript", message);
    public void LogWarn(string message) => _diagnostics.Warn("GDScript", message);
    public void LogError(string message) => _diagnostics.Error("GDScript", message);

    public void GeneratorGrid(int radius)
    {
        _generator.Mode = GeneratorMode.Grid;
        _generator.Radius = System.Math.Max(0, radius);
        _diagnostics.Info("Generator", $"GDScript grid radius={_generator.Radius}.");
    }

    public void GeneratorAxis(string axis, int radius)
    {
        var normalized = axis.Trim().ToLowerInvariant();
        if (normalized is not ("x" or "y" or "z"))
            throw new ArgumentException("GeneratorAxis axis must be x, y or z.", nameof(axis));

        _generator.Mode = GeneratorMode.Axis;
        _generator.Axis = normalized[0];
        _generator.AxisRadius = System.Math.Max(0, radius);
        _diagnostics.Info("Generator", $"GDScript axis={_generator.Axis}, radius={_generator.AxisRadius}.");
    }

    /// <summary>
    /// Sets a finite generator from JSON. Expected shape:
    /// [{"chunk":"templateId","x":0,"y":0,"z":0}, ...]
    /// Using JSON here keeps the bridge stable across Godot Variant marshalling changes.
    /// </summary>
    public void GeneratorFiniteJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("GeneratorFiniteJson expects a JSON array.", nameof(json));

        var placements = new List<ChunkPlacement>();
        foreach (var row in doc.RootElement.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Every finite placement must be a JSON object.", nameof(json));

            var chunk = row.TryGetProperty("chunk", out var c) ? c.GetString() : null;
            if (string.IsNullOrWhiteSpace(chunk))
                throw new ArgumentException("Every finite placement needs a non-empty 'chunk'.", nameof(json));

            placements.Add(new ChunkPlacement(
                chunk,
                ReadInt(row, "x"),
                ReadInt(row, "y"),
                ReadInt(row, "z")));
        }

        _generator.FixedPlacements = placements;
        _generator.Mode = GeneratorMode.Finite;
        _diagnostics.Info("Generator", $"GDScript finite placements={placements.Count}.");
    }

    public bool WorldExists(string id) => _world.Resolve(id) is not null;

    public Vector3 WorldGetPosition(string id)
    {
        var entity = ResolveRequired(id);
        return new Vector3(entity.WorldPosition.X, entity.WorldPosition.Y, entity.WorldPosition.Z);
    }

    public void WorldSetPosition(string id, float x, float y, float z) =>
        ResolveRequired(id).WorldPosition = new Vec3(x, y, z);

    public void WorldSetVisible(string id, bool visible) => ResolveRequired(id).Visible = visible;

    public void WorldSetTexture(string id, string slot, string source) =>
        ResolveRequired(id).Textures[slot] = Backrooms.Engine.Levels.LevelPackage.Normalize(source);

    public void WorldDestroy(string id)
    {
        var entity = _world.Resolve(id);
        if (entity is not null)
            _world.Remove(entity.Id);
    }

    public void UiText(string id, string text) => _ui.Set(new UiText(id, text));
    public void UiButton(string id, string label) => _ui.Set(new UiButton(id, label));
    public void UiImage(string id, string source) => _ui.Set(new UiImage(id, Backrooms.Engine.Levels.LevelPackage.Normalize(source)));
    public void UiProgress(string id, double value, double max) => _ui.Set(new UiProgress(id, value, max));
    public void UiVisible(string id, bool visible) => _ui.SetVisible(id, visible);
    public void UiRemove(string id) => _ui.Remove(id);

    public void ShaderEnable(string id) => _renderer.SetShaderEnabled(id, true);
    public void ShaderDisable(string id) => _renderer.SetShaderEnabled(id, false);
    public void ShaderReload(string id) => _renderer.ReloadShader(id);

    public void ShaderAttach(string target, string shaderId)
    {
        var entity = ResolveRequired(target);
        entity.EnabledShaders.Add(shaderId);
        _renderer.SetShaderEnabled(shaderId, true);
    }

    public void ShaderDetach(string target, string shaderId) => ResolveRequired(target).EnabledShaders.Remove(shaderId);
    public void ShaderSetFloat(string id, string name, float value) => _renderer.SetShaderParameter(id, name, value);
    public void ShaderSetVec4(string id, string name, float x, float y, float z, float w) =>
        _renderer.SetShaderParameter(id, name, new[] { x, y, z, w });

    private RuntimeEntity ResolveRequired(string id) =>
        _world.Resolve(id) ?? throw new KeyNotFoundException($"Unknown entity '{id}'.");

    private static int ReadInt(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.TryGetInt32(out var n) ? n : 0;
}
