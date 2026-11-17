# Backrooms Infinity

A moddable Backrooms game/runtime built with C# / .NET 10, SDL3, WebGPU (Silk.NET), Assimp, JoltPhysicsSharp, ImGui.NET and Lua/MoonSharp.

The repository is licensed under **PolyForm Noncommercial 1.0.0**. Launcher and client are separate executable projects; low-level third-party APIs stay behind engine boundaries so level/game logic remains small and AI-friendly.

## Repository layout

- `src/Backrooms.Launcher` — menu, animated backgrounds/ambience, online level catalog, downloads, settings, authors, client startup/loading screen.
- `src/Backrooms.Client` — level ZIP loading, validation, Lua/init execution, generation planning and game window.
- `src/Backrooms.Engine` — level format, Lua API, generation, diagnostics, rendering/model/physics boundaries.
- `levels.db.zip` — ZIP containing SQLite `levels.db`, table `levels(download_link, preview, name)`.
- `.github/workflows/update-levels-db.yml` — manual catalog updater.
- `.github/workflows/release.yml` — manual tagged release.
- `installer.ps1` / `installer.sh` — install latest (or selected tag) release into the per-user game directory.
- `docs/` — level/Lua/API/architecture/release documentation.

## User data directory

Windows:

```text
%APPDATA%/.backrooms_infinity/
```

Linux/macOS:

```text
~/.backrooms_infinity/
```

Layout:

```text
.backrooms_infinity/
├── levels/       downloaded level ZIPs: {display name}.zip
├── models/       user/runtime model storage
├── settings/     settings.json
├── game/         unpacked GitHub release contents
├── assets/       launcher PNG/MP3, VHS font and FONT-COPYRIGHT.txt
├── logs/         launcher/client logs
├── cache/        levels.db.zip and preview cache
└── temp/         temporary files
```

`AppPaths.ResolveAsset()` prefers `~/.backrooms_infinity/assets` / `%APPDATA%/.../assets`, then falls back to assets beside the executable for development builds.

## Launcher

The main menu uses the bundled VHS font for all text and centers four buttons:

- **Одиночная игра**
- **Сетевая игра** — currently shows “not implemented”.
- **Настройки**
- **Авторы**

The background continuously pans/zooms through the supplied images and plays the matching ambience through SDL_mixer.

Singleplayer downloads:

```text
https://raw.githubusercontent.com/Endlad2/Backrooms-Infinity/main/levels.db.zip
```

while displaying a loading indicator. Each catalog row becomes a preview card; the level name is rendered in the lower-right corner. Already downloaded levels show green **Готов** in the upper-left corner. Preview images are cached.

Clicking a card downloads an absent level to `levels/{name}.zip`, then starts the client. The client immediately opens its log and performs ZIP/XML/Lua/generation preparation without creating the game window. Structured startup stages are streamed over redirected stdout/stderr to the still-visible launcher. The launcher shows the level name, current stage and a Minecraft-Bedrock-inspired loading panel over the same moving background/ambience. When the client emits its READY marker and creates the game window, the launcher hides until the client exits.

## Contributors / “Авторы”

`tools/Backrooms.ContributorsGen` is a build-time tool. Before launcher compilation it runs `git shortlog`, calculates every contributor's commit count, sorts descending, serializes the result to JSON and embeds that JSON into generated `Contributors.g.cs`. No runtime GitHub request is required by the Authors screen.

CI/release checkout uses full Git history (`fetch-depth: 0`) so the counts include repository history.

## Build

Install .NET 10 SDK:

```bash
dotnet restore Backrooms.sln
dotnet build Backrooms.sln -c Release
```

Example direct client run:

```bash
dotnet run --project src/Backrooms.Client -- \
  --level examples/ExampleLevel.brlevel.zip \
  --nick wanderer
```

Example launcher run:

```bash
dotnet run --project src/Backrooms.Launcher
```

### Bundled catalog level

The repository ships `levels/Level-0.zip`, an infinite 64x64-chunk Level 0 pack with eight deterministic chunk templates (seven office/maze variants plus one real pit-grid template). Its catalog preview is `levels/previews/level-0.jpg`, and `levels.db.zip` points to both files through `raw.githubusercontent.com` URLs.

## Level packs

A level is a ZIP containing `level.xml` and optional `models/`, `textures/`, `shaders/`, `scripts/`. Object/model IDs share one namespace. Entity positions are relative to the **center of the chunk**. `<script class="init">` runs before generation and controls finite/grid/axis/custom generation strategies.

See `docs/LEVEL_FORMAT.md` and `docs/LUA_API.md` for the full format/API.

## Rendering status

The level format, parser, Lua sandbox/API, generation strategies, diagnostics/F3 state, process split, catalog/download flow, launcher UI/media layer, installers and CI/release/catalog workflows are implemented as source. The actual production 3D WebGPU renderer, full ImGui backend, Assimp GPU mesh upload and Jolt world integration remain isolated boundary layers intended for the next engine implementation phase; the project does not pretend those stubs are a finished 3D game renderer.

## Supplied font/media

The requested VCR OSD Mono Russian/VHS icon font and supplied launcher media are retained. Font attribution is copied to releases and installed assets as `FONT-COPYRIGHT.txt`:

```text
Author: MrManet
RUS: Daymarius
VHS icons: DENIRO_pro
```

Verify redistribution rights for all third-party/supplied media before a public release.
