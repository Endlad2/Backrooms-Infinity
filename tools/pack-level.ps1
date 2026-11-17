param(
  [string]$Source = "examples/ExampleLevel",
  [string]$Output = "ExampleLevel.brlevel.zip"
)

if (Test-Path $Output) { Remove-Item $Output -Force }
Compress-Archive -Path "$Source/*" -DestinationPath $Output
Write-Host "Packed: $Output"
