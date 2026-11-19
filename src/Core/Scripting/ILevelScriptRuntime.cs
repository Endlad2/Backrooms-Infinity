using Backrooms.Engine.Levels;

namespace Backrooms.Engine.Scripting;

public interface ILevelScriptRuntime
{
    void ExecuteBinding(LevelPackage package, ScriptBinding binding, string targetId);
    void Emit(string eventName, string targetId);
}
