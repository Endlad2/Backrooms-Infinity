# Backrooms — C# runtime/level-pack foundation

A source repository for a moddable Backrooms game built around:

- C# / .NET 10
- SDL3 platform shell
- WebGPU boundary through Silk.NET.WebGPU
- Assimp boundary through Silk.NET.Assimp
- JoltPhysicsSharp boundary
- ImGui.NET debug-panel model
- Lua through MoonSharp
- ZIP level packs with XML + `models/`, `textures/`, `shaders/`, `scripts/`

The repository intentionally keeps third-party APIs behind small engine boundaries so game code and AI-assisted edits do not spread SDL/WebGPU/Assimp/Jolt calls throughout the project.

## Projects

`Backrooms.Launcher` and `Backrooms.Client` are separate executable projects. `Backrooms.Engine` is the shared class library.

The launcher starts the client and passes the level ZIP, nickname, optional skin, and settings through command-line arguments.

## Important implementation status

The level ZIP format, XML parser/validation, Lua sandbox/API, generator plans, diagnostics bus, F3 debugger state, client/launcher process split, CI/release workflows, and supplied assets are implemented as source.

The actual 3D WebGPU draw pipeline, ImGui SDL/WebGPU backend, Assimp mesh upload, Jolt body creation, and launcher image/audio compositor are intentionally isolated behind boundary classes and are not presented as finished production rendering code. The current SDL shell opens and runs, while those backend integrations are the next implementation layer. This keeps the architecture honest and prevents low-level binding code from contaminating the level/runtime APIs.

## Build

Install the .NET 10 SDK, then:

```bash
dotnet restore Backrooms.sln
dotnet build Backrooms.sln -c Release
```

Run the client:

```bash
dotnet run --project src/Backrooms.Client -- \
  --level examples/ExampleLevel.brlevel.zip \
  --nick wanderer
```

Run the launcher:

```bash
dotnet run --project src/Backrooms.Launcher -- \
  --level examples/ExampleLevel.brlevel.zip \
  --nick wanderer
```

The client also accepts the level ZIP as the first positional argument.

## Level pack

A level is a ZIP containing `level.xml` and optional `models/`, `textures/`, `shaders/`, `scripts/`.

See:

- `docs/LEVEL_FORMAT.md`
- `docs/LUA_API.md`
- `docs/ARCHITECTURE.md`
- `docs/DEBUGGER.md`
- `examples/ExampleLevel/`

## Coordinate rule

Every entity position in XML is local to the **center of its chunk**.

For a chunk placed at grid coordinate `(cx, cy, cz)` and `chunks size="16"`:

```text
worldPosition = (cx * 16, cy * 16, cz * 16) + localPosition
```

## Entity IDs

`<object>` and `<model>` share one global XML ID namespace. This is invalid:

```xml
<object id="door" ... />
<model id="door" ... />
```

Scripts never need to care whether an ID points to a primitive or imported model.

## Default generation

Without `<script class="init">`, generation defaults to an infinite X/Z grid.

The init Lua script can select:

- grid generation;
- finite explicit placements;
- infinite generation on X, Y or Z (for stairs/elevators/towers);
- fully custom Lua placement planning.

## Debugger

Errors from the level parser, Lua execution, engine subsystems, and backend probes are pushed to one diagnostics bus. An error automatically opens debugger state; F3 toggles it manually.

The `ImGuiDebugPanel` is already separated from the diagnostics model so it can be drawn by the final SDL/WebGPU ImGui backend without changing engine logic.

## Assets supplied with this project

The launcher asset directory contains the three PNGs and three matching MP3 files supplied with the request. The VCR OSD Mono Russian/VHS icon font is copied into both launcher and client output assets.

Before public redistribution, verify that you have redistribution rights for all supplied media/font assets.

## License

Repository source: PolyForm Noncommercial License 1.0.0.

See `LICENSE`.
