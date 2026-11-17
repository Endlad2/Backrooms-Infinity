using Backrooms.Engine.Diagnostics;

namespace Backrooms.Engine.Assets;

public sealed class AssimpModelImporterBoundary
{
    private readonly DiagnosticBus _diagnostics;

    public AssimpModelImporterBoundary(DiagnosticBus diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public bool CanLoad(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".obj", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".fbx", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".gltf", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".glb", StringComparison.OrdinalIgnoreCase);
    }

    public void Probe()
    {
        var type = Type.GetType("Silk.NET.Assimp.Assimp, Silk.NET.Assimp");
        _diagnostics.Info("Assimp", type is null
            ? "Silk.NET.Assimp binding not loaded yet."
            : $"Assimp binding available: {type.Assembly.GetName().Version}.");
    }
}

public sealed class JoltPhysicsBoundary
{
    private readonly DiagnosticBus _diagnostics;

    public JoltPhysicsBoundary(DiagnosticBus diagnostics)
    {
        _diagnostics = diagnostics;
    }

    public void Probe()
    {
        var type = Type.GetType("JoltPhysicsSharp.JoltApi, JoltPhysicsSharp")
                   ?? Type.GetType("JoltPhysicsSharp.Jolt, JoltPhysicsSharp");
        _diagnostics.Info("Jolt", type is null
            ? "JoltPhysicsSharp assembly not activated yet."
            : $"Jolt binding available: {type.Assembly.GetName().Version}.");
    }
}
