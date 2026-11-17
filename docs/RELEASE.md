# CI, catalog and releases

## CI

`.github/workflows/ci.yml` checks the SQLite schema in `levels.db.zip`, restores and builds the .NET 10 solution. Checkout uses full history because the launcher build generates contributor counts from Git history.

## Catalog update

`.github/workflows/update-levels-db.yml` is manual (`workflow_dispatch`) with inputs:

```text
name
preview
download_link
```

It UPSERTs the row into the root `levels.db.zip` and pushes the result to `main`.

## Manual release

`.github/workflows/release.yml` is manual and accepts:

```text
tag = v0.1.0
```

It publishes launcher + client into one **self-contained** directory for:

- win-x64
- linux-x64
- osx-x64
- osx-arm64

Each runtime becomes `Backrooms-<rid>.zip`. The workflow also attaches `installer.ps1` and `installer.sh`, then creates the GitHub Release for the supplied tag.

Self-contained artifacts are intentional: the installers can put `game/` on a clean machine without a preinstalled .NET runtime.
