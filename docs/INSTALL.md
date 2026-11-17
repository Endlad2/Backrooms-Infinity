# Installation

## Windows

Run `installer.ps1` from the repository/release. By default it downloads the latest platform release. To install a specific tag:

```powershell
./installer.ps1 -Tag v0.1.0
```

It installs into `%APPDATA%/.backrooms_infinity`, unpacks the release into `game/`, mirrors bundled assets into `assets/`, and creates `Backrooms Infinity.lnk` on the desktop.

## Linux/macOS

```sh
chmod +x installer.sh
./installer.sh
# or: ./installer.sh v0.1.0
```

Linux installs to `~/.backrooms_infinity` and creates a desktop `.desktop` launcher. macOS gets a desktop `.command` launcher. The release is self-contained, so users do not need to install the .NET runtime separately.
