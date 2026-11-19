param(
    [string]$Tag = "latest",
    [string]$Repository = "Endlad2/Backrooms-Infinity"
)

$ErrorActionPreference = "Stop"
$root = Join-Path $env:APPDATA ".backrooms_infinity"
$game = Join-Path $root "game"
$assets = Join-Path $root "assets"
$temp = Join-Path ([System.IO.Path]::GetTempPath()) ("backrooms-infinity-" + [guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $root,$game,$assets,$temp | Out-Null

if ($Tag -eq "latest") {
    $release = Invoke-RestMethod -Headers @{"User-Agent"="Backrooms-Infinity-Installer"} -Uri "https://api.github.com/repos/$Repository/releases/latest"
    $Tag = $release.tag_name
}
if ([string]::IsNullOrWhiteSpace($Tag)) { throw "Release tag could not be resolved." }

$base = "https://github.com/$Repository/releases/download/$Tag"
$gameZip = Join-Path $temp "game.zip"
$assetZip = Join-Path $temp "assets.zip"
Write-Host "Installing Backrooms Infinity $Tag..."
Invoke-WebRequest -UseBasicParsing -Uri "$base/Backrooms-win-x64.zip" -OutFile $gameZip
Invoke-WebRequest -UseBasicParsing -Uri "$base/Backrooms-user-assets.zip" -OutFile $assetZip

if (Test-Path $game) { Remove-Item -Recurse -Force $game }
New-Item -ItemType Directory -Force -Path $game | Out-Null
Expand-Archive -Path $gameZip -DestinationPath $game -Force
Expand-Archive -Path $assetZip -DestinationPath $root -Force

$exe = Join-Path $game "Backrooms-Infinity.exe"
if (!(Test-Path $exe)) { throw "Installed executable not found: $exe" }
$desktop = [Environment]::GetFolderPath("Desktop")
$linkPath = Join-Path $desktop "Backrooms Infinity.lnk"
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($linkPath)
$link.TargetPath = $exe
$link.WorkingDirectory = $game
$link.IconLocation = $exe
$link.Description = "Backrooms Infinity"
$link.Save()

Remove-Item -Recurse -Force $temp -ErrorAction SilentlyContinue
Write-Host "Installed to $root"
Write-Host "Desktop shortcut: $linkPath"
