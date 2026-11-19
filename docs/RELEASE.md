# CI, catalog and releases

## CI

`.github/workflows/ci.yml` performs repository validation, restores/builds `Backrooms.Infinity.csproj` with .NET 8 and asks the Godot 4.7.2 .NET editor to import the project in CI. Godot's own `--headless --editor` switch is used only by CI automation; the shipped game has no headless gameplay mode.

## Catalog update

`.github/workflows/update-levels-db.yml` is manual (`workflow_dispatch`) and accepts:

```text
name
preview
download_link
```

It validates the URLs, UPSERTs the row in the SQLite `levels.db`, repacks `levels.db.zip`, regenerates `data/catalog.json`, and commits the updated catalog.

## Manual release

`.github/workflows/release.yml` is manual and accepts a Git tag such as `v0.1.0`. It builds the single Godot project and exports platform packages. There are no Launcher/Client artifacts.

The release also attaches `installer.ps1` and `installer.sh`.
