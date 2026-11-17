# CI and releases

## CI

`.github/workflows/ci.yml` restores and builds the solution on pushes and pull requests.

## Manual release

`.github/workflows/release.yml` uses `workflow_dispatch`.

Input:

```text
tag = v0.1.0
```

The workflow publishes launcher + client into the same directory for:

- win-x64
- linux-x64
- osx-x64
- osx-arm64

It archives each runtime and creates a GitHub Release for the supplied tag.

The workflow intentionally publishes source-built artifacts; it does not sign executables.

## Local publish

```bash
dotnet publish src/Backrooms.Launcher -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64
dotnet publish src/Backrooms.Client   -c Release -r win-x64 --self-contained false -o artifacts/publish/win-x64
```
