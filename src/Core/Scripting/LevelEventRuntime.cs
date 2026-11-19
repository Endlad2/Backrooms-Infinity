using Backrooms.Engine.Levels;

namespace Backrooms.Engine.Scripting;

public sealed class LevelEventRuntime
{
    private readonly LuaHost _lua;
    private readonly LevelPackage _package;
    private readonly Dictionary<(string Event, string EntityId), List<ScriptBinding>> _bindings = new();

    public LevelEventRuntime(LuaHost lua, LevelPackage package)
    {
        _lua = lua;
        _package = package;

        foreach (var chunk in package.Definition.Chunks)
        {
            foreach (var entity in chunk.Entities)
            {
                foreach (var binding in entity.Scripts)
                {
                    var key = (binding.Event, entity.Id);
                    if (!_bindings.TryGetValue(key, out var list))
                        _bindings[key] = list = new List<ScriptBinding>();
                    list.Add(binding);
                }
            }
        }
    }

    public void Emit(string eventName, string entityId)
    {
        _lua.Emit(eventName, entityId);

        if (_bindings.TryGetValue((eventName, entityId), out var list))
        {
            foreach (var binding in list)
                _lua.ExecuteBinding(_package, binding, entityId);
        }
    }
}
