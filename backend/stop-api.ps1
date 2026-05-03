# Stops any running OnPoint.API so `dotnet build` / `dotnet run` can copy fresh DLLs into bin\Debug.
# Run from repo root:  pwsh -File backend/stop-api.ps1
Get-Process -Name 'OnPoint.API' -ErrorAction SilentlyContinue | Stop-Process -Force
if ($?) { Write-Host 'OnPoint.API stopped.' } else { Write-Host 'No OnPoint.API process was running.' }
