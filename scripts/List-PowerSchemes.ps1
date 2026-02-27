# List-PowerSchemes.ps1
# Lists current Windows power schemes (powercfg /list).
# Run: PowerShell -ExecutionPolicy Bypass -File .\List-PowerSchemes.ps1

$ErrorActionPreference = 'Stop'
Write-Host "=== Current power schemes ===" -ForegroundColor Cyan
Write-Host ""
powercfg /list
Write-Host ""
Write-Host "Active scheme (marked with *):" -ForegroundColor Yellow
powercfg /getactivescheme
