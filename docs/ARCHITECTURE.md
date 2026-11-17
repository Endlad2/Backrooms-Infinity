# Architecture

## Process model

```text
Backrooms.Launcher
  |-- SDL3 + SDL_image + SDL_ttf + SDL_mixer
  |-- levels.db.zip -> SQLite catalog
  |-- preview / level downloader
  |-- loading UI + ambient background
  |
  +-- Process.Start(Backrooms.Client, redirected stdout/stderr)
          |
          |  @@BACKROOMS_STAGE:<text>
          |  normal log lines
          |  @@BACKROOMS_READY
          v
Backrooms.Client
  |-- creates log immediately
  |-- opens/validates level ZIP + XML
  |-- creates Lua/world/render/physics boundaries
  |-- runs init Lua and plans chunks
  |-- emits READY
  +-- creates SDL game window only after startup preparation
          |
          v
Backrooms.Engine
```

The launcher remains visible during client preparation. When READY arrives, the launcher hides; when the client exits it is shown again. This avoids a blank hidden period while preserving two independent binaries.

## User-data boundary

`Backrooms.Engine.Platform.AppPaths` is the single source of truth for per-user storage. Windows uses `%APPDATA%/.backrooms_infinity`; Unix-like systems use `~/.backrooms_infinity`.

Installers unpack release files into `game/`. Runtime-downloaded level ZIPs go to `levels/`. Launcher media/font attribution are mirrored to `assets/`. Settings, logs, caches and temp files have dedicated directories.

## Engine boundaries

Game/level logic does not call SDL/WebGPU/Assimp/Jolt directly. Stable boundaries isolate native APIs:

- `IRenderer` / `WebGpuRendererBoundary`
- `AssimpModelImporterBoundary`
- `JoltPhysicsBoundary`
- runtime `UiState` + debug overlay model
- `LevelPackage` / `LevelParser`
- `LuaHost`
- `GeneratorPlan` / `ChunkPlanner`

Level Lua is a soft sandbox and does **not** automatically receive arbitrary filesystem/process/network capabilities, even though the native .NET game executable itself can use those capabilities.

## Chunk coordinates

XML positions are local to the center of a chunk. Placement converts local positions using the generator's chunk coordinate and `<chunks size="...">`.

## Entity namespace

Primitive `<object>` and imported `<model>` definitions share one global level-ID namespace. Script targets use IDs without needing to know which kind of entity owns the ID.
