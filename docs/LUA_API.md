# Lua API

Lua XML scripts must explicitly declare `type="lua"`; there is no implicit scripting language.

Lua is executed with MoonSharp's soft sandbox. Level Lua does not automatically get unrestricted OS/file/process/network APIs.

## Global `self`

When an entity event script is executed, `self` contains that entity's XML ID.

```lua
log.info("hover: " .. self)
world.set_visible(self, false)
```

## Logging

```lua
log.info("message")
log.warn("message")
log.error("message")
```

All three go to the same diagnostics bus. `log.error` opens debugger state automatically because it emits an error entry.

## Generator API

The init script runs before generation.

### Default infinite X/Z grid

```lua
generator.grid(3)
```

Argument: streaming radius in chunks.

### Infinite axis generator

Useful for staircases, shafts, towers, elevators.

```lua
generator.axis("y", 12)
```

Valid axes: `"x"`, `"y"`, `"z"`.

The runtime chooses chunk templates deterministically as positions advance.

### Finite level

```lua
generator.finite({
  { chunk = "start", x = 0, y = 0, z = 0 },
  { chunk = "hall",  x = 1, y = 0, z = 0 },
  { chunk = "end",   x = 2, y = 0, z = 0 }
})
```

No chunks outside this list are requested.

### Fully custom generator

```lua
generator.custom(function(px, py, pz)
  local cy = math.floor(py / 16)
  local out = {}

  for y = cy - 6, cy + 6 do
    table.insert(out, {
      chunk = (y % 2 == 0) and "stairs_a" or "stairs_b",
      x = 0,
      y = y,
      z = 0
    })
  end

  return out
end)
```

The callback receives the player world position and returns desired chunk placements.

## World API

### Existence

```lua
if world.exists("door") then
  ...
end
```

### Position

```lua
local p = world.get_position("door")
log.info("x=" .. p.x)

world.set_position("door", 10, 2, -4)
```

### Visibility

```lua
world.set_visible("lamp", false)
```

### Runtime texture assignment

```lua
world.set_texture("wall", "albedo", "textures/dirty_wall.png")
```

### Destroy

```lua
world.destroy("temporary_object")
```

## UI API

The scripting API writes UI state; the renderer decides how it is presented.

### Text

```lua
ui.text("hint", "Press E to interact")
```

### Button

```lua
ui.button("leave", "Leave level")
```

### Image

```lua
ui.image("warning", "textures/ui/warning.png")
```

### Progress

```lua
ui.progress("sanity", 72, 100)
```

### Visibility

```lua
ui.visible("hint", false)
ui.visible("hint", true)
```

### Remove

```lua
ui.remove("hint")
```

## Shader API

### Enable / disable

```lua
shader.enable("flicker")
shader.disable("flicker")
```

### Attach / detach on an entity

```lua
shader.attach("door", "flicker")
shader.detach("door", "flicker")
```

The target uses the same unified entity ID namespace as `world.*`.

### Parameters

```lua
shader.set_float("flicker", "strength", 0.35)
shader.set_vec4("fog", "color", 0.8, 0.75, 0.55, 1.0)
```

### Hot reload request

```lua
shader.reload("flicker")
```

The Godot backend applies shader state through ShaderMaterial; level shaders should use .gdshader after the engine migration.

## Event API

Scripts may subscribe dynamically:

```lua
events.on("hover", "door", function(id)
  ui.text("hint", "Open " .. id)
end)

events.on("click", "door", function(id)
  log.info("clicked " .. id)
end)
```

XML event scripts and Lua subscriptions can coexist.

## Recommended event semantics

| Event | Meaning |
|---|---|
| `load` | entity instance became active |
| `unload` | entity instance is being removed |
| `hover` | mouse/raycast focus entered entity |
| `hover_end` | focus left |
| `click` | primary mouse click |
| `interact` | gameplay interact key |
| `enter` | player entered trigger |
| `exit` | player left trigger |
| `update` | per-frame/per-tick callback |
| `chunk_load` | chunk instance became active |
| `chunk_unload` | chunk instance is being removed |

## Mouse hover implementation contract

The game runtime raycasts from the camera through the cursor/crosshair. The hit's unified entity ID is emitted as:

```text
events.Emit("hover", id)
```

No model/object type branch is needed.

## Security

Do not expose `Process.Start`, arbitrary filesystem writes, unrestricted HTTP, or native library loading to level Lua unless the level is trusted.

The host game can still use all of those normal .NET capabilities internally.
