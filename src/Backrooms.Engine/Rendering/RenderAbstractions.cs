using Backrooms.Engine.Diagnostics;

namespace Backrooms.Engine.Rendering;

public interface IRenderer : IDisposable
{
    string BackendName { get; }
    void SetShaderEnabled(string shaderId, bool enabled);
    void SetShaderParameter(string shaderId, string name, object value);
    void ReloadShader(string shaderId);
}

public sealed class WebGpuRendererBoundary : IRenderer
{
    private readonly DiagnosticBus _diagnostics;
    private readonly Dictionary<string, bool> _shaderStates = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, object>> _parameters = new(StringComparer.Ordinal);

    public string BackendName => "WebGPU (Silk.NET boundary)";

    public WebGpuRendererBoundary(DiagnosticBus diagnostics)
    {
        _diagnostics = diagnostics;

        // Keep Silk.NET behind this boundary. The game/runtime layer never references it directly.
        var apiType = Type.GetType("Silk.NET.WebGPU.WebGPU, Silk.NET.WebGPU");
        if (apiType is null)
            _diagnostics.Warn("Renderer", "Silk.NET.WebGPU assembly was not discovered yet.");
        else
            _diagnostics.Info("Renderer", $"WebGPU binding loaded: {apiType.Assembly.GetName().Version}.");
    }

    public void SetShaderEnabled(string shaderId, bool enabled)
    {
        _shaderStates[shaderId] = enabled;
        _diagnostics.Info("Shader", $"{shaderId}: {(enabled ? "enabled" : "disabled")}.");
    }

    public void SetShaderParameter(string shaderId, string name, object value)
    {
        if (!_parameters.TryGetValue(shaderId, out var values))
            _parameters[shaderId] = values = new Dictionary<string, object>(StringComparer.Ordinal);
        values[name] = value;
    }

    public void ReloadShader(string shaderId) =>
        _diagnostics.Info("Shader", $"Reload requested for '{shaderId}'.");

    public void Dispose() { }
}
