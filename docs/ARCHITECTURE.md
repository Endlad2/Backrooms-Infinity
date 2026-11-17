# Architecture

## Goals

The project uses dependency boundaries. Game/level code talks to engine interfaces, not directly to SDL3, WebGPU, Assimp, Jolt or ImGui.

```text
Backrooms.Launcher.exe
        |
        +---- launches ----> Backrooms.Client.exe
                               |
                               v
                       Backrooms.Engine
                   /       |      |      \
              Levels     Lua    Runtime  Diagnostics
                 |         |       |         |
              Assimp    UI/API   World     ImGui panel
                         |
                       IRenderer
                         |
                      WebGPU
```

## Executables

### Backrooms.Launcher

Responsibilities:

- main menu process;
- background media playlist;
- player nickname/settings;
- level selection path;
- starts the client through `Process.Start`;
- never loads level scripts itself.

### Backrooms.Client

Responsibilities:

- opens the level ZIP;
- validates/parses XML;
- creates Lua sandbox;
- executes init script before chunk generation;
- owns runtime world, rendering, physics, UI and event dispatch;
- owns debugger state.

## Backrooms.Engine boundaries

### Levels

`LevelPackage` never extracts arbitrary ZIP entries. Assets are normalized and `..` traversal is rejected.

`LevelParser` validates:

- root `<level>`;
- level name;
- `<chunks size>`;
- chunk IDs;
- global object/model ID uniqueness;
- model extensions;
- referenced asset existence;
- target IDs for chunk-level texture/shader bindings.

### Runtime

`RuntimeWorld` holds runtime entities. Template IDs are converted to instance IDs such as:

```text
room_a@12,0,-7/door
```

Lua can still resolve `door` when only one matching runtime entity is relevant.

### Generation

`GeneratorPlan` contains only data/strategy. `ChunkPlanner` does not know XML or SDL.

This makes it possible to replace generation without rewriting streaming/rendering.

### Scripting

MoonSharp uses `CoreModules.Preset_SoftSandbox`; level scripts do not get arbitrary file/process/network access by default.

The engine itself can still launch processes and use network APIs because the client is a normal .NET desktop process. That power is deliberately **not** exposed automatically to untrusted level Lua.

### Rendering

`IRenderer` is the stable game-facing API.

`WebGpuRendererBoundary` is the only place intended to know Silk.NET.WebGPU. A production renderer should add:

- adapter/device/surface creation;
- SDL3 native-window -> WebGPU surface bridge;
- swapchain/surface configuration;
- vertex/index/uniform buffers;
- texture upload;
- WGSL pipeline cache;
- render/compute passes;
- shader hot reload.

### Assets / Assimp

`AssimpModelImporterBoundary` is the designated place for OBJ/FBX/glTF/GLB decoding and conversion to the engine mesh representation.

### Physics / Jolt

`JoltPhysicsBoundary` is the designated place for world creation, body/shape handles, raycasts, triggers and character controller integration.

### UI / ImGui

Gameplay UI state is data (`UiState`). Debug UI has `ImGuiDebugPanel`, so scripts and gameplay never depend on ImGui directly.
