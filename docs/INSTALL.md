# Installation

Backrooms Infinity is a single Godot .NET application. There are no separate launcher/client executables.

## From source

Requirements:

- Godot 4.7.2 .NET
- .NET 8 SDK
- Git (only needed for normal repository work)

```bash
dotnet restore Backrooms.Infinity.csproj
dotnet build Backrooms.Infinity.csproj -c Debug
```

Open `project.godot` in the Godot .NET editor and run `Main.tscn`.

## User data

Windows: `%APPDATA%/.backrooms_infinity/`

Linux/macOS: `~/.backrooms_infinity/`

The game creates `levels/`, `models/`, `settings/`, `game/`, `assets/`, `logs/`, `cache/` and `temp/` beneath that root.

## Release install

`installer.ps1` and `installer.sh` download a tagged GitHub release, unpack the one Godot export into `.backrooms_infinity/game/`, copy the bundled menu/font assets to `.backrooms_infinity/assets/`, and create a desktop shortcut/launcher.

There is no game-specific `--headless` command-line mode. F3 opens the in-game debugger.
