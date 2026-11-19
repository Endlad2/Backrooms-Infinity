using System.Text.RegularExpressions;
using Godot;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Levels;

namespace Backrooms.Infinity.App;

public sealed class GdScriptHost
{
    private static readonly Regex ExtendsRegex = new(@"(?m)^\s*extends\s+", RegexOptions.Compiled);
    private static readonly Regex RunRegex = new(@"(?m)^\s*func\s+run\s*\(", RegexOptions.Compiled);

    private readonly DiagnosticBus _diagnostics;
    private readonly GdScriptContext _context;

    public GdScriptHost(DiagnosticBus diagnostics, GdScriptContext context)
    {
        _diagnostics = diagnostics;
        _context = context;
    }

    public void RunInit(LevelPackage package, InitScriptDefinition init)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(init.Source))
                Execute(package.ReadText(init.Source!), $"init:{init.Source}", string.Empty);
            if (!string.IsNullOrWhiteSpace(init.InlineCode))
                Execute(init.InlineCode!, "init:inline", string.Empty);

            _diagnostics.Info("GDScript", "Init script completed.");
        }
        catch (Exception ex)
        {
            _diagnostics.Error("GDScript", "Init script failed.", ex.ToString());
            throw;
        }
    }

    public void ExecuteBinding(LevelPackage package, ScriptBinding binding, string targetId)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(binding.Source))
                Execute(package.ReadText(binding.Source!), $"{binding.Event}:{binding.Source}", targetId);
            if (!string.IsNullOrWhiteSpace(binding.InlineCode))
                Execute(binding.InlineCode!, $"{binding.Event}:inline", targetId);
        }
        catch (Exception ex)
        {
            _diagnostics.Error("GDScript", $"Script event '{binding.Event}' failed for '{targetId}'.", ex.ToString());
        }
    }

    private void Execute(string source, string name, string targetId)
    {
        _diagnostics.Info("GDScript", $"Compiling/executing {name}.");
        var prepared = PrepareSource(source);
        var script = new GDScript { SourceCode = prepared };
        var reload = script.Reload();
        if (reload != Error.Ok)
            throw new InvalidDataException($"GDScript compile failed for '{name}' (Godot error: {reload}).");
        if (!script.CanInstantiate())
            throw new InvalidDataException($"GDScript '{name}' cannot be instantiated.");

        var instance = (GodotObject)script.New();
        if (!instance.HasMethod("run"))
            throw new InvalidDataException($"GDScript '{name}' must define func run(ctx), or be body-only code that can be wrapped automatically.");

        _context.CurrentTarget = targetId;
        instance.Call("run", _context);
    }

    private static string PrepareSource(string source)
    {
        var normalized = source.Replace("\r\n", "\n");
        if (RunRegex.IsMatch(normalized))
            return ExtendsRegex.IsMatch(normalized) ? normalized : "extends RefCounted\n" + normalized;

        var body = string.Join("\n", normalized.Split('\n').Select(line => "\t" + line));
        return "extends RefCounted\n\nfunc run(ctx):\n" + (string.IsNullOrWhiteSpace(body) ? "\tpass" : body) + "\n";
    }
}
