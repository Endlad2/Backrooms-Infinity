using MoonSharp.Interpreter;
using Backrooms.Engine.Diagnostics;
using Backrooms.Engine.Generation;
using Backrooms.Engine.Levels;
using Backrooms.Engine.Mathematics;
using Backrooms.Engine.Rendering;
using Backrooms.Engine.Runtime;
using Backrooms.Engine.UI;

namespace Backrooms.Engine.Scripting;

public sealed class LuaHost
{
    private readonly Script _script;
    private readonly DiagnosticBus _diagnostics;
    private readonly RuntimeWorld _world;
    private readonly UiState _ui;
    private readonly IRenderer _renderer;
    private readonly GeneratorPlan _generator;
    private readonly Dictionary<(string Event, string Target), List<DynValue>> _callbacks = new();

    public LuaHost(
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

        _script = new Script(CoreModules.Preset_SoftSandbox);
        RegisterModules();
    }

    public GeneratorPlan Generator => _generator;

    public void RunInit(LevelPackage package)
    {
        var init = package.Definition.InitScript;
        if (init is null)
        {
            _diagnostics.Info("Lua", "No init script; default grid generator is active.");
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(init.Source))
                Execute(package.ReadText(init.Source!), $"init:{init.Source}");
            if (!string.IsNullOrWhiteSpace(init.InlineCode))
                Execute(init.InlineCode!, "init:inline");

            _diagnostics.Info("Lua", $"Init script completed. Generator={_generator.Mode}.");
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Lua", "Init script failed.", ex.ToString());
            throw;
        }
    }

    public void ExecuteBinding(LevelPackage package, ScriptBinding binding, string targetId)
    {
        try
        {
            _script.Globals["self"] = targetId;

            if (!string.IsNullOrWhiteSpace(binding.Source))
                Execute(package.ReadText(binding.Source!), $"{binding.Event}:{binding.Source}");
            if (!string.IsNullOrWhiteSpace(binding.InlineCode))
                Execute(binding.InlineCode!, $"{binding.Event}:inline");
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Lua", $"Script event '{binding.Event}' failed for '{targetId}'.", ex.ToString());
        }
    }

    public void Emit(string eventName, string targetId)
    {
        if (!_callbacks.TryGetValue((eventName, targetId), out var list))
            return;

        foreach (var callback in list.ToArray())
        {
            try
            {
                _script.Globals["self"] = targetId;
                _script.Call(callback, targetId);
            }
            catch (Exception ex)
            {
                _diagnostics.Error("Lua", $"Callback '{eventName}' failed for '{targetId}'.", ex.ToString());
            }
        }
    }

    private void Execute(string source, string name)
    {
        _diagnostics.Info("Lua", $"Executing {name}.");
        _script.DoString(source);
    }

    private void RegisterModules()
    {
        _script.Globals["log"] = BuildLogModule();
        _script.Globals["generator"] = BuildGeneratorModule();
        _script.Globals["world"] = BuildWorldModule();
        _script.Globals["ui"] = BuildUiModule();
        _script.Globals["shader"] = BuildShaderModule();
        _script.Globals["events"] = BuildEventsModule();
    }

    private Table BuildLogModule()
    {
        var t = new Table(_script);
        t["info"] = (Action<string>)(message => _diagnostics.Info("Lua", message));
        t["warn"] = (Action<string>)(message => _diagnostics.Warn("Lua", message));
        t["error"] = (Action<string>)(message => _diagnostics.Error("Lua", message));
        return t;
    }

    private Table BuildGeneratorModule()
    {
        var t = new Table(_script);

        t["grid"] = (Action<double>)(radius =>
        {
            _generator.Mode = GeneratorMode.Grid;
            _generator.Radius = System.Math.Max(0, (int)radius);
            _diagnostics.Info("Generator", $"Grid radius={_generator.Radius}.");
        });

        t["axis"] = (Action<string, double>)((axis, radius) =>
        {
            var normalized = axis.Trim().ToLowerInvariant();
            if (normalized is not ("x" or "y" or "z"))
                throw new ScriptRuntimeException("generator.axis axis must be x, y or z.");

            _generator.Mode = GeneratorMode.Axis;
            _generator.Axis = normalized[0];
            _generator.AxisRadius = System.Math.Max(0, (int)radius);
            _diagnostics.Info("Generator", $"Axis={_generator.Axis}, radius={_generator.AxisRadius}.");
        });

        t["finite"] = DynValue.NewCallback((_, args) =>
        {
            var table = RequireTable(args, 0, "generator.finite expects a table.");
            _generator.FixedPlacements = ParsePlacements(table);
            _generator.Mode = GeneratorMode.Finite;
            _diagnostics.Info("Generator", $"Finite placements={_generator.FixedPlacements.Count}.");
            return DynValue.Nil;
        });

        t["custom"] = DynValue.NewCallback((_, args) =>
        {
            var fn = args.Count > 0 ? args[0] : DynValue.Nil;
            if (fn.Type is not DataType.Function and not DataType.ClrFunction)
                throw new ScriptRuntimeException("generator.custom expects a function.");

            _generator.Mode = GeneratorMode.Custom;
            _generator.CustomPlanner = player =>
            {
                var result = _script.Call(fn, player.X, player.Y, player.Z);
                if (result.Type != DataType.Table)
                    throw new ScriptRuntimeException("custom generator must return a placement table.");
                return ParsePlacements(result.Table);
            };

            _diagnostics.Info("Generator", "Custom Lua generator installed.");
            return DynValue.Nil;
        });

        return t;
    }

    private Table BuildWorldModule()
    {
        var t = new Table(_script);

        t["exists"] = (Func<string, bool>)(id => _world.Resolve(id) is not null);

        t["get_position"] = DynValue.NewCallback((_, args) =>
        {
            var id = RequireString(args, 0);
            var entity = _world.Resolve(id) ?? throw new ScriptRuntimeException($"Unknown entity '{id}'.");
            var p = entity.WorldPosition;
            var result = new Table(_script);
            result["x"] = p.X;
            result["y"] = p.Y;
            result["z"] = p.Z;
            return DynValue.NewTable(result);
        });

        t["set_position"] = (Action<string, double, double, double>)((id, x, y, z) =>
        {
            var entity = _world.Resolve(id) ?? throw new ScriptRuntimeException($"Unknown entity '{id}'.");
            entity.WorldPosition = new Vec3((float)x, (float)y, (float)z);
        });

        t["set_visible"] = (Action<string, bool>)((id, visible) =>
        {
            var entity = _world.Resolve(id) ?? throw new ScriptRuntimeException($"Unknown entity '{id}'.");
            entity.Visible = visible;
        });

        t["set_texture"] = (Action<string, string, string>)((id, slot, source) =>
        {
            var entity = _world.Resolve(id) ?? throw new ScriptRuntimeException($"Unknown entity '{id}'.");
            entity.Textures[slot] = LevelPackage.Normalize(source);
        });

        t["destroy"] = (Action<string>)(id =>
        {
            var entity = _world.Resolve(id);
            if (entity is not null)
                _world.Remove(entity.Id);
        });

        return t;
    }

    private Table BuildUiModule()
    {
        var t = new Table(_script);

        t["text"] = (Action<string, string>)((id, text) => _ui.Set(new UiText(id, text)));
        t["button"] = (Action<string, string>)((id, label) => _ui.Set(new UiButton(id, label)));
        t["image"] = (Action<string, string>)((id, src) => _ui.Set(new UiImage(id, LevelPackage.Normalize(src))));
        t["progress"] = (Action<string, double, double>)((id, value, max) => _ui.Set(new UiProgress(id, value, max)));
        t["visible"] = (Action<string, bool>)((id, visible) => _ui.SetVisible(id, visible));
        t["remove"] = (Action<string>)(id => _ui.Remove(id));

        return t;
    }

    private Table BuildShaderModule()
    {
        var t = new Table(_script);

        t["enable"] = (Action<string>)(id => _renderer.SetShaderEnabled(id, true));
        t["disable"] = (Action<string>)(id => _renderer.SetShaderEnabled(id, false));
        t["reload"] = (Action<string>)(id => _renderer.ReloadShader(id));
        t["attach"] = (Action<string, string>)((target, id) =>
        {
            var entity = _world.Resolve(target) ?? throw new ScriptRuntimeException($"Unknown entity '{target}'.");
            entity.EnabledShaders.Add(id);
            _renderer.SetShaderEnabled(id, true);
        });
        t["detach"] = (Action<string, string>)((target, id) =>
        {
            var entity = _world.Resolve(target) ?? throw new ScriptRuntimeException($"Unknown entity '{target}'.");
            entity.EnabledShaders.Remove(id);
        });
        t["set_float"] = (Action<string, string, double>)((id, name, value) =>
            _renderer.SetShaderParameter(id, name, (float)value));
        t["set_vec4"] = (Action<string, string, double, double, double, double>)((id, name, x, y, z, w) =>
            _renderer.SetShaderParameter(id, name, new[] { (float)x, (float)y, (float)z, (float)w }));

        return t;
    }

    private Table BuildEventsModule()
    {
        var t = new Table(_script);
        t["on"] = DynValue.NewCallback((_, args) =>
        {
            var eventName = RequireString(args, 0);
            var target = RequireString(args, 1);
            var callback = args.Count > 2 ? args[2] : DynValue.Nil;

            if (callback.Type is not DataType.Function and not DataType.ClrFunction)
                throw new ScriptRuntimeException("events.on(event, target, callback): callback must be a function.");

            var key = (eventName, target);
            if (!_callbacks.TryGetValue(key, out var list))
                _callbacks[key] = list = new List<DynValue>();
            list.Add(callback);
            return DynValue.Nil;
        });

        return t;
    }

    private IReadOnlyList<ChunkPlacement> ParsePlacements(Table table)
    {
        var result = new List<ChunkPlacement>();

        foreach (var pair in table.Pairs)
        {
            if (pair.Value.Type != DataType.Table)
                continue;

            var row = pair.Value.Table;
            var chunk = row.Get("chunk").CastToString();
            if (string.IsNullOrWhiteSpace(chunk))
                throw new ScriptRuntimeException("Each placement needs chunk='templateId'.");

            result.Add(new ChunkPlacement(
                chunk,
                ToInt(row.Get("x")),
                ToInt(row.Get("y")),
                ToInt(row.Get("z"))));
        }

        return result;
    }

    private static int ToInt(DynValue value) =>
        value.Type == DataType.Number ? (int)value.Number : 0;

    private static string RequireString(CallbackArguments args, int index)
    {
        if (args.Count <= index || args[index].Type != DataType.String)
            throw new ScriptRuntimeException($"Argument {index + 1} must be a string.");
        return args[index].String;
    }

    private static Table RequireTable(CallbackArguments args, int index, string message)
    {
        if (args.Count <= index || args[index].Type != DataType.Table)
            throw new ScriptRuntimeException(message);
        return args[index].Table;
    }
}
