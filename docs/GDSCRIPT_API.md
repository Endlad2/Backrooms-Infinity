# Runtime GDScript level API

GDScript is supported for level scripts, but **Lua remains the default/recommended language** for level logic. Use GDScript only when a level genuinely needs Godot-specific runtime behavior.

Every XML script must declare its language explicitly:

```xml
<script type="lua" src="scripts/door.lua" event="click" />
<script type="gdscript" src="scripts/special.gd" event="click" />
```

The old form without `type` is rejected by the parser.

## Runtime execution

GDScript is compiled from the level ZIP at runtime through Godot's `GDScript` resource API. External `.gd` sources and inline code are both supported.

For short inline/body scripts, write statements directly:

```xml
<script type="gdscript" event="click"><![CDATA[
ctx.LogInfo("clicked " + ctx.CurrentTarget)
ctx.WorldSetVisible(ctx.CurrentTarget, false)
]]></script>
```

The engine wraps body-only code into `extends RefCounted` + `func run(ctx)` automatically.

For a full `.gd` source, implement `run(ctx)`:

```gdscript
extends RefCounted

func run(ctx):
    ctx.LogInfo("runtime gdscript")
```

A source with `func run(ctx)` but no `extends` automatically gets `extends RefCounted` prepended.

## Context API

The context is a C# Godot object. User-defined C# methods are callable from GDScript with their declared names.

### Diagnostics

```gdscript
ctx.LogInfo("message")
ctx.LogWarn("message")
ctx.LogError("message")
```

Errors go through the same diagnostics bus and can open the F3 debugger.

### Generator

```gdscript
ctx.GeneratorGrid(3)
ctx.GeneratorAxis("y", 12)
```

Finite layouts use JSON to avoid brittle cross-language Variant marshalling:

```gdscript
ctx.GeneratorFiniteJson(JSON.stringify([
    {"chunk": "start", "x": 0, "y": 0, "z": 0},
    {"chunk": "hall",  "x": 1, "y": 0, "z": 0}
]))
```

For fully custom dynamic generators, prefer Lua's `generator.custom(...)` API. This is intentional: Lua remains the stable content scripting layer, while GDScript is the escape hatch for Godot-specific cases.

### Current entity

For entity event scripts:

```gdscript
var id = ctx.CurrentTarget
```

Init scripts receive an empty target.

### World

```gdscript
ctx.WorldExists("door")
var p = ctx.WorldGetPosition("door")
ctx.WorldSetPosition("door", 1.0, 2.0, 3.0)
ctx.WorldSetVisible("door", false)
ctx.WorldSetTexture("door", "albedo", "textures/door.png")
ctx.WorldDestroy("door")
```

### UI

```gdscript
ctx.UiText("hint", "Press E")
ctx.UiButton("leave", "Leave")
ctx.UiImage("warning", "textures/ui/warning.png")
ctx.UiProgress("sanity", 75.0, 100.0)
ctx.UiVisible("hint", false)
ctx.UiRemove("hint")
```

### Shaders

```gdscript
ctx.ShaderEnable("flicker")
ctx.ShaderDisable("flicker")
ctx.ShaderReload("flicker")
ctx.ShaderAttach("door", "flicker")
ctx.ShaderDetach("door", "flicker")
ctx.ShaderSetFloat("flicker", "strength", 0.35)
ctx.ShaderSetVec4("fog", "color", 0.8, 0.75, 0.55, 1.0)
```

## Security warning

Unlike MoonSharp Lua's soft sandbox, runtime GDScript executes inside Godot and can reach Godot APIs that may expose filesystem, OS and other powerful functionality. Therefore a ZIP containing `type="gdscript"` must be treated as executable/trusted content. Do not enable GDScript in arbitrary third-party levels without a trust policy or signature/approval layer.
