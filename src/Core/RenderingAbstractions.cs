namespace Backrooms.Engine.Rendering;

public interface IRenderer : IDisposable
{
    string BackendName { get; }
    void SetShaderEnabled(string shaderId, bool enabled);
    void SetShaderParameter(string shaderId, string name, object value);
    void ReloadShader(string shaderId);
}
