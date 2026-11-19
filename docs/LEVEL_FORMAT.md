# Level ZIP / XML format

## ZIP layout

```text
MyLevel.zip
├─ level.xml
├─ models/
│  ├─ office.glb
│  └─ door.fbx
├─ textures/
│  ├─ wall.png
│  └─ carpet.png
├─ shaders/
│  └─ flicker.wgsl
└─ scripts/
   ├─ init.lua
   └─ door.lua
```

`level.xml` is required. Folders are conventional but paths are relative to ZIP root.

Paths containing `..` are rejected.

## Minimal level

```xml
<?xml version="1.0" encoding="utf-8"?>
<level name="Endless Offices" version="1">
  <chunks size="16">
    <chunk id="office">
      <object id="floor" type="cube">
        <transform position="0 -1 0" scale="16 1 16" />
      </object>
    </chunk>
  </chunks>
</level>
```

Without an init script, the engine uses the default infinite X/Z grid generator.

## Root `<level>`

Attributes:

| Attribute | Required | Meaning |
|---|---:|---|
| `name` | yes | Display name |
| `version` | no | Level-format/content version; default `1` |

Optional children:

- `<meta>`
- `<script class="init">`
- `<chunks>`

## Metadata

```xml
<meta>
  <entry key="author" value="name" />
  <entry key="description">A strange office level.</entry>
</meta>
```

## Init script

Runs before chunk generation is planned.

External:

```xml
<script class="init" src="scripts/init.lua" />
```

Inline:

```xml
<script class="init"><![CDATA[
  log.info("init")
  generator.grid(3)
]]></script>
```

If both `src` and inline code are present, `src` executes first and inline code second.

## `<chunks>`

```xml
<chunks size="16">
  ...
</chunks>
```

`size` is the grid cell spacing in world units. A chunk's XML entity positions are always relative to the center of that chunk.

The size does not constrain geometry. A mesh may extend outside the nominal cell.

## `<chunk>`

```xml
<chunk id="office">
  ...
</chunk>
```

The `id` is a template ID used by generators. The same chunk template may be instantiated at many coordinates.

Allowed children:

- `<object>`
- `<model>`
- `<texture target="...">`
- `<shader target="...">`
- `<script>`

## Primitive object

```xml
<object id="wall" type="cube">
  <transform
    position="0 1.5 -8"
    rotation="0 0 0"
    scale="16 3 0.2" />

  <texture slot="albedo" src="textures/wall.png" />

  <shader id="flicker"
          src="shaders/flicker.wgsl"
          enabled="true">
    <param name="strength" value="0.2" />
  </shader>

  <script event="hover" src="scripts/wall_hover.lua" />
</object>
```

Supported primitive names in the parser:

`cube`, `sphere`, `plane`, `capsule`, `cylinder`, `cone`, `quad`.

Transform may also be written directly:

```xml
<object id="x" type="cube"
        position="0 0 0"
        rotation="0 90 0"
        scale="1 1 1" />
```

## Model

```xml
<model id="office_model" src="models/office.glb">
  <transform position="0 0 0" />
  <texture slot="albedo" src="textures/override.png" />
  <script event="click" src="scripts/click.lua" />
</model>
```

Accepted extensions:

- `.obj`
- `.fbx`
- `.gltf`
- `.glb`

The parser verifies the file exists in the ZIP.

## One ID namespace

Models and primitives share a single namespace.

Valid:

```xml
<object id="table" ... />
<model id="chair" ... />
```

Invalid:

```xml
<object id="thing" ... />
<model id="thing" ... />
```

This allows APIs such as:

```lua
world.set_visible("thing", false)
```

without asking the script whether `thing` was an imported model or engine primitive.

## Texture bindings

As child of an entity:

```xml
<model id="door" src="models/door.glb">
  <texture slot="albedo" src="textures/door.png" />
  <texture slot="normal" src="textures/door_n.png" />
</model>
```

Or at chunk level by ID:

```xml
<texture target="door" slot="albedo" src="textures/door.png" />
```

The parser validates the target ID.

Recommended slots:

- `albedo`
- `normal`
- `roughness`
- `metallic`
- `emissive`
- `ao`
- custom renderer-defined slots

## Shader bindings

Entity child:

```xml
<shader id="wet_floor" src="shaders/wet.wgsl" enabled="true">
  <param name="roughness" value="0.1" />
</shader>
```

Chunk target form:

```xml
<shader target="floor"
        id="wet_floor"
        src="shaders/wet.wgsl"
        enabled="true" />
```

Shader files are WGSL by default. The renderer boundary is designed for runtime shader loading/reloading.

## Scripts

External:

```xml
<script event="hover" src="scripts/hover.lua" />
```

Inline:

```xml
<script event="click"><![CDATA[
  log.info("clicked " .. self)
  world.set_visible(self, false)
]]></script>
```

Common event names reserved by convention:

- `load`
- `unload`
- `hover`
- `hover_end`
- `click`
- `interact`
- `enter`
- `exit`
- `update`
- `chunk_load`
- `chunk_unload`

Event names are strings so new engine events can be added without changing the XML schema.

## Properties

```xml
<object id="door" type="cube">
  <properties>
    <property name="locked" value="true" />
    <property name="key" value="blue" />
  </properties>
</object>
```

Properties are preserved as strings for gameplay systems/scripts.

## Chunk-local coordinates

For:

```xml
<chunks size="16">
```

and entity:

```xml
<transform position="2 1 -3" />
```

placed in chunk coordinate `(10, 0, -4)`:

```text
chunk center = (160, 0, -64)
world        = (162, 1, -67)
```

This rule also works for Y-axis generators.

## Inventory items and decomposition

A level may extend/override the runtime item registry at root level:

```xml
<items>
  <item id="office_chair" name="Офисный стул" kind="Furniture"
        maxStack="1" placeable="true">
    <decompose>
      <material id="metal" amount="2" />
      <material id="plastic" amount="3" />
    </decompose>
    <properties>
      <property name="loot_group" value="office" />
    </properties>
  </item>
</items>
```

`kind` maps to the engine `ItemKind` values (`Material`, `Tool`, `Weapon`, `Furniture`, `Consumable`, `Backpack`, `OutpostFlag`, `Misc`). Backpack definitions may set `backpackCapacity`.

## Monster declarations

Root-level monster metadata feeds the lightweight runtime:

```xml
<monsters>
  <monster id="hound" name="Гончая" hostile="true"
           walkSpeed="2.8" chaseSpeed="5.6"
           detectionRadius="20" maxAlive="2" />
</monsters>
```

An optional `model="models/hound.glb"` path is validated against the ZIP. This schema deliberately keeps spawn/AI data separate from a concrete rendering backend.
