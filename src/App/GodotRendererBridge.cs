using Godot;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Rendering;

namespace Backrooms.Infinity.App;

public sealed class GodotRendererBridge : IRenderer
{
    private readonly DiagnosticBus _diagnostics;
    private readonly Dictionary<string, List<ShaderMaterial>> _materials = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _enabled = new(StringComparer.Ordinal);

    public GodotRendererBridge(DiagnosticBus diagnostics) => _diagnostics = diagnostics;
    public string BackendName => "Godot 4.7 RenderingDevice / StandardMaterial3D";

    public void Register(string id, ShaderMaterial material)
    {
        if (!_materials.TryGetValue(id, out var list)) _materials[id] = list = new List<ShaderMaterial>();
        list.Add(material);
    }

    public void SetShaderEnabled(string shaderId, bool enabled)
    {
        _enabled[shaderId] = enabled;
        if (_materials.TryGetValue(shaderId, out var list))
            foreach (var material in list) material.SetShaderParameter("enabled", enabled);
        _diagnostics.Info("Shader", $"{shaderId}: {(enabled ? "enabled" : "disabled")}");
    }

    public void SetShaderParameter(string shaderId, string name, object value)
    {
        if (!_materials.TryGetValue(shaderId, out var list)) return;
        var variant = value switch
        {
            float f => Variant.From(f),
            double d => Variant.From((float)d),
            int i => Variant.From(i),
            bool b => Variant.From(b),
            string s => Variant.From(s),
            float[] v when v.Length >= 4 => Variant.From(new Vector4(v[0], v[1], v[2], v[3])),
            _ => Variant.From(value.ToString() ?? string.Empty)
        };
        foreach (var material in list) material.SetShaderParameter(name, variant);
    }

    public void ReloadShader(string shaderId) => _diagnostics.Info("Shader", $"Reload requested for '{shaderId}'. Godot recreates shader resources when the chunk is rebuilt.");
    public void Dispose() => _materials.Clear();
}
