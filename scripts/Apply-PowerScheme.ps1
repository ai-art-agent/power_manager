# Apply-PowerScheme.ps1
# Переключает только схему электропитания. Яркость экрана не меняется.
# Usage: .\Apply-PowerScheme.ps1 -Scheme Min|Balanced|Max

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet('Min','Balanced','Max')]
    [string]$Scheme
)

$ErrorActionPreference = 'Stop'
$jsonPath = Join-Path $PSScriptRoot '..\scheme-guids.json'
if (-not (Test-Path $jsonPath)) {
    Write-Error "Run Install-PowerManagerSchemes.ps1 first to create scheme-guids.json"
}
$guids = Get-Content $jsonPath -Raw | ConvertFrom-Json
$guid = $guids.$Scheme
if (-not $guid) {
    Write-Error "Scheme '$Scheme' not found in scheme-guids.json"
}
powercfg /setactive $guid
Write-Host "Scheme: $Scheme (GUID: $guid)" -ForegroundColor Green
Write-Host "Яркость не меняется — настраивайте вручную при необходимости."
Write-Host "Подсветка клавиатуры: при необходимости переключайте вручную (Dell: Fn+F10 или Quickset)."
