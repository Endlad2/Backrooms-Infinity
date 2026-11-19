using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Scripting;

namespace Backrooms.Infinity.App;

public sealed class LevelScriptRuntime : ILevelScriptRuntime
{
    private readonly DiagnosticBus _diagnostics;
    private readonly LuaHost _lua;
    private readonly GdScriptHost _gdScript;

    public LevelScriptRuntime(DiagnosticBus diagnostics, LuaHost lua, GdScriptHost gdScript)
    {
        _diagnostics = diagnostics;
        _lua = lua;
        _gdScript = gdScript;
    }

    public void RunInit(LevelPackage package)
    {
        var init = package.Definition.InitScript;
        if (init is null)
        {
            _diagnostics.Info("Scripts", "No init script; default grid generator is active.");
            return;
        }

        switch (init.Type)
        {
            case LevelScriptType.Lua:
                _lua.RunInit(package, init);
                break;
            case LevelScriptType.GdScript:
                _gdScript.RunInit(package, init);
                break;
            default:
                throw new InvalidDataException($"Unsupported init script type '{init.Type}'.");
        }
    }

    public void ExecuteBinding(LevelPackage package, ScriptBinding binding, string targetId)
    {
        switch (binding.Type)
        {
            case LevelScriptType.Lua:
                _lua.ExecuteBinding(package, binding, targetId);
                break;
            case LevelScriptType.GdScript:
                _gdScript.ExecuteBinding(package, binding, targetId);
                break;
            default:
                throw new InvalidDataException($"Unsupported script type '{binding.Type}'.");
        }
    }

    public void Emit(string eventName, string targetId)
    {
        // Lua supports persistent events.on(...) callbacks. GDScript event bindings are
        // executed directly by LevelEventRuntime when their XML event fires.
        _lua.Emit(eventName, targetId);
    }
}
