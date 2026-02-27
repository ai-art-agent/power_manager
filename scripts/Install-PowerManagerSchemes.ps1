# Install-PowerManagerSchemes.ps1
# Creates three Power Manager power schemes and sets all parameters.
# Run as Administrator recommended.
# Requires: PowerShell 5.1+ (Windows).

$ErrorActionPreference = 'Stop'

# GUID подсистем (из Windows Power Settings)
$SUB_PROCESSOR  = '54533251-82be-4824-96c1-47b60b740d00'
$SUB_VIDEO      = '7516b95f-f776-4464-8c53-06167f40cc99'
$SUB_PCIEXPRESS = '501a4d13-42af-4429-9fd1-a8218c268e20'
$SUB_SLEEP      = '238c9fa8-0aad-41ed-83f4-97be242c8f20'
$SUB_DISK       = '0012ee47-9041-4b5d-9b77-535fba8b1442'

$PROCTHROTTLEMIN = '893dee8e-2bef-41e0-89c6-b55d0929964c'
$PROCTHROTTLEMAX = 'bc5038f7-23e0-4960-96da-33abaf5935ec'
$PROCTHROTTLEMIN1 = '893dee8e-2bef-41e0-89c6-b55d0929964d'
$PROCTHROTTLEMAX1 = 'bc5038f7-23e0-4960-96da-33abaf5935ed'
$VIDEONORMALLEVEL = 'aded5e82-b909-4619-9949-f5d71dac0bcb'
$VIDEODIM        = '17aaa29b-8b43-4b94-aafe-35f64daaf1ee'
$VIDEOIDLE       = '3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e'
$ASPM            = 'ee12f906-d277-404b-b6da-e5fa1a576df5'
$STANDBYIDLE     = '29f6c1db-86da-48c5-9fdb-f2b67b1f44da'
$HIBERNATEIDLE   = '9d7815a6-7ee4-497e-8888-515a05f02364'
$SYSCOOLPOL      = '94d3a615-a899-4ac5-ae2b-e4d8f634367f'
$PERFBOOSTMODE   = 'be337238-0d82-4146-a960-4f3749d470c7'
$DISKIDLE        = '6738e2c4-e8a5-4a42-b16a-e040e769756e'

$SCHEME_BALANCED = '381b4222-f694-41f0-9685-ff5bb260df2e'

# Remove previous Power Manager schemes so we don't accumulate (only our 3)
$jsonPath = Join-Path $PSScriptRoot '..\scheme-guids.json'
if (Test-Path $jsonPath) {
    try {
        $prev = Get-Content $jsonPath -Raw | ConvertFrom-Json
        foreach ($key in @('Min','Balanced','Max')) {
            $g = $prev.$key
            if ($g) {
                powercfg /delete $g 2>$null
                Write-Host "Removed previous scheme $key" -ForegroundColor Gray
            }
        }
    } catch { }
}

function Set-SchemeValue {
    param([string]$SchemeGuid, [string]$SubGuid, [string]$SettingGuid, [int]$ValueAc, [int]$ValueDc)
    powercfg /setacvalueindex $SchemeGuid $SubGuid $SettingGuid $ValueAc | Out-Null
    powercfg /setdcvalueindex $SchemeGuid $SubGuid $SettingGuid $ValueDc | Out-Null
}

function New-PowerManagerScheme {
    param([string]$BaseName, [string]$DisplayName, [string]$Description, [hashtable]$Settings)
    $out = powercfg /duplicatescheme $SCHEME_BALANCED
    $m = [regex]::Match($out, '([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})')
    $newGuid = $m.Groups[1].Value
    if (-not $newGuid) {
        Write-Warning "Could not parse GUID from: $out"
        return $null
    }
    if ($Description) {
        powercfg /changename $newGuid $DisplayName $Description
    } else {
        powercfg /changename $newGuid $DisplayName
    }
    foreach ($key in $Settings.Keys) {
        $v = $Settings[$key]
        Set-SchemeValue $newGuid $v.Sub $v.Setting $v.AC $v.DC
    }
    powercfg /setactive $newGuid | Out-Null
    return $newGuid
}

Write-Host "=== Installing Power Manager schemes ===" -ForegroundColor Cyan

# 1) Minimum (power saver): CPU max 50%, brightness 20%, all power savings
$minSettings = @{
    ProcMax    = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMAX;  AC = 50;  DC = 50 }
    ProcMax1   = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMAX1; AC = 50;  DC = 50 }
    ProcMin    = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMIN;  AC = 5;   DC = 5 }
    ProcMin1   = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMIN1; AC = 5;   DC = 5 }
    Brightness = @{ Sub = $SUB_VIDEO;     Setting = $VIDEONORMALLEVEL; AC = 20;  DC = 20 }
    PCIe       = @{ Sub = $SUB_PCIEXPRESS; Setting = $ASPM;             AC = 2;   DC = 2 }
    Cool       = @{ Sub = $SUB_PROCESSOR; Setting = $SYSCOOLPOL;        AC = 0;   DC = 0 }
    Boost      = @{ Sub = $SUB_PROCESSOR; Setting = $PERFBOOSTMODE;      AC = 0;   DC = 0 }
    DiskIdle   = @{ Sub = $SUB_DISK;     Setting = $DISKIDLE;           AC = 120; DC = 60 }
    Standby    = @{ Sub = $SUB_SLEEP;    Setting = $STANDBYIDLE;        AC = 300; DC = 180 }
}
$guidMin = New-PowerManagerScheme -BaseName 'Min' -DisplayName 'Power Manager: Minimum' -Description 'CPU max 50%, brightness 20%. Max power saving: PCIe, disk, passive cooling.' -Settings $minSettings
if ($guidMin) { Write-Host "  Created scheme Minimum: $guidMin" -ForegroundColor Green }

# 2) Balanced: CPU max 90%, standard balance
$balSettings = @{
    ProcMax    = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMAX;  AC = 90; DC = 90 }
    ProcMax1   = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMAX1; AC = 90; DC = 90 }
    ProcMin    = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMIN;  AC = 5;  DC = 5 }
    ProcMin1   = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMIN1; AC = 5;  DC = 5 }
    Brightness = @{ Sub = $SUB_VIDEO;     Setting = $VIDEONORMALLEVEL; AC = 50; DC = 50 }
    PCIe       = @{ Sub = $SUB_PCIEXPRESS; Setting = $ASPM;             AC = 1;  DC = 2 }
    Cool       = @{ Sub = $SUB_PROCESSOR; Setting = $SYSCOOLPOL;        AC = 1;  DC = 0 }
    Boost      = @{ Sub = $SUB_PROCESSOR; Setting = $PERFBOOSTMODE;      AC = 2;  DC = 1 }
    DiskIdle   = @{ Sub = $SUB_DISK;     Setting = $DISKIDLE;           AC = 600; DC = 300 }
    Standby    = @{ Sub = $SUB_SLEEP;    Setting = $STANDBYIDLE;        AC = 900; DC = 600 }
}
$guidBal = New-PowerManagerScheme -BaseName 'Balanced' -DisplayName 'Power Manager: Balanced' -Description 'CPU max 90%, brightness 50%. Balanced performance and power.' -Settings $balSettings
if ($guidBal) { Write-Host "  Created scheme Balanced: $guidBal" -ForegroundColor Green }

# 3) Maximum: CPU 100%, brightness 50%, power savings off
$maxSettings = @{
    ProcMax    = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMAX;  AC = 100; DC = 100 }
    ProcMax1   = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMAX1; AC = 100; DC = 100 }
    ProcMin    = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMIN;  AC = 20;  DC = 10 }
    ProcMin1   = @{ Sub = $SUB_PROCESSOR; Setting = $PROCTHROTTLEMIN1; AC = 20;  DC = 10 }
    Brightness = @{ Sub = $SUB_VIDEO;     Setting = $VIDEONORMALLEVEL; AC = 50;  DC = 50 }
    PCIe       = @{ Sub = $SUB_PCIEXPRESS; Setting = $ASPM;             AC = 0;   DC = 0 }
    Cool       = @{ Sub = $SUB_PROCESSOR; Setting = $SYSCOOLPOL;        AC = 1;   DC = 1 }
    Boost      = @{ Sub = $SUB_PROCESSOR; Setting = $PERFBOOSTMODE;      AC = 2;   DC = 2 }
    DiskIdle   = @{ Sub = $SUB_DISK;     Setting = $DISKIDLE;           AC = 0;   DC = 120 }
    Standby    = @{ Sub = $SUB_SLEEP;    Setting = $STANDBYIDLE;        AC = 0;   DC = 600 }
}
$guidMax = New-PowerManagerScheme -BaseName 'Max' -DisplayName 'Power Manager: Maximum' -Description 'CPU 100%, brightness 50%. Max performance, active cooling, no power limits.' -Settings $maxSettings
if ($guidMax) { Write-Host "  Created scheme Maximum: $guidMax" -ForegroundColor Green }

# Save GUIDs for tests and Apply-PowerScheme.ps1
$schemeGuids = @{
    Min       = $guidMin
    Balanced  = $guidBal
    Max       = $guidMax
}
$schemeGuids | ConvertTo-Json | Set-Content -Path (Join-Path $PSScriptRoot '..\scheme-guids.json') -Encoding UTF8

# Remove all other power schemes so only ours (+ system Balanced template) remain
$keepGuids = @($guidMin, $guidBal, $guidMax) + @($SCHEME_BALANCED) | Where-Object { $_ }
$listOut = powercfg /list
$allGuids = [regex]::Matches($listOut, 'Power Scheme GUID:\s*([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})') | ForEach-Object { $_.Groups[1].Value }
foreach ($g in $allGuids) {
    if ($g -notin $keepGuids) {
        powercfg /delete $g 2>$null
        Write-Host "  Deleted scheme: $g" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "Done. Schemes saved to scheme-guids.json. Switch: powercfg /setactive <GUID>" -ForegroundColor Cyan
Write-Host "Keyboard backlight on Dell is not controlled by powercfg; set manually or via Dell Quickset if needed." -ForegroundColor Yellow
Write-Host "Only Power Manager schemes + system Balanced (template) are left; others were removed." -ForegroundColor Gray
