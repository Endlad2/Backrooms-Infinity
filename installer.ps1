param(
    [string]$Tag = "latest"
)

$ErrorActionPreference = "Stop"
$Repo = "Endlad2/Backrooms-Infinity"
$RootBase = if ($env:APPDATA) { $env:APPDATA } else { [Environment]::GetFolderPath("UserProfile") }
$Root = Join-Path $RootBase ".backrooms_infinity"
$Game = Join-Path $Root "game"
$Assets = Join-Path $Root "assets"
$Temp = Join-Path $Root "temp"
@("levels", "models", "settings", "game", "assets", "logs", "cache", "temp") | ForEach-Object {
    New-Item -ItemType Directory -Force -Path (Join-Path $Root $_) | Out-Null
}

if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { throw "Windows ARM64 release is not published yet." }
$arch = "win-x64"
$asset = "Backrooms-$arch.zip"
$url = if ($Tag -eq "latest") {
    "https://github.com/$Repo/releases/latest/download/$asset"
} else {
    "https://github.com/$Repo/releases/download/$Tag/$asset"
}
$archive = Join-Path $Temp $asset
Write-Host "Downloading $url"
Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing

if (Test-Path $Game) {
    Get-ChildItem -Force $Game | Remove-Item -Recurse -Force
}
Expand-Archive -Path $archive -DestinationPath $Game -Force

$BundledAssets = Join-Path $Game "assets"
if (Test-Path $BundledAssets) {
    Copy-Item (Join-Path $BundledAssets "*") $Assets -Recurse -Force
}

$Launcher = Join-Path $Game "Backrooms.Launcher.exe"
if (!(Test-Path $Launcher)) { throw "Backrooms.Launcher.exe was not found after installation." }
$Desktop = [Environment]::GetFolderPath("Desktop")
$ShortcutPath = Join-Path $Desktop "Backrooms Infinity.lnk"
$Shell = New-Object -ComObject WScript.Shell
$Shortcut = $Shell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $Launcher
$Shortcut.WorkingDirectory = $Game
$Shortcut.IconLocation = "$Launcher,0"
$Shortcut.Save()

Remove-Item $archive -Force -ErrorAction SilentlyContinue
Write-Host "Installed to $Root"
Write-Host "Shortcut: $ShortcutPath"
