# Removes OC2 QoL Mods from Overcooked! 2.
#   -RemoveBepInEx    also remove BepInEx, but only if no other plugins are installed
#   -GameDir <path>   use this game folder instead of finding it through Steam
#   -Yes              don't ask for confirmation
param([string]$GameDir, [switch]$RemoveBepInEx, [switch]$Yes)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $here 'common.ps1')

try {
    if (-not $GameDir) {
        $GameDir = Find-GameDir
    }
    if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir 'Overcooked2.exe'))) {
        throw "Couldn't find Overcooked! 2. Re-run with -GameDir 'C:\path\to\Overcooked! 2'."
    }
    if (Get-Process -Name Overcooked2 -ErrorAction SilentlyContinue) {
        throw "Overcooked! 2 is running. Close it and try again."
    }

    $plugins = Join-Path $GameDir 'BepInEx\plugins'
    $targets = @()
    foreach ($f in 'OC2Mods.dll', 'OC2Mods.dll.bak') {
        $p = Join-Path $plugins $f
        if (Test-Path $p) { $targets += $p }
    }
    $config = Join-Path $GameDir 'BepInEx\config\oc2mods.spawnrotation.staroverlay.cfg'

    $removeLoader = $false
    if ($RemoveBepInEx) {
        $others = @()
        if (Test-Path $plugins) {
            $others = @(Get-ChildItem -Path $plugins -Recurse -File | Where-Object { $_.Name -notlike 'OC2Mods.dll*' })
        }
        if ($others.Count -gt 0) {
            Write-Host "Other BepInEx plugins are installed, so BepInEx will be kept:" -ForegroundColor Yellow
            $others | ForEach-Object { Write-Host "  $($_.FullName.Substring($GameDir.Length + 1))" }
        } else {
            $removeLoader = $true
        }
    }

    Write-Host "Game folder: $GameDir"
    if ($targets.Count -eq 0 -and -not $removeLoader) { Write-Host "OC2 QoL Mods isn't installed. Nothing to do."; return }
    $targets | ForEach-Object { Write-Host "Will remove $($_.Substring($GameDir.Length + 1))" }
    if ($removeLoader) {
        Write-Host "Will remove BepInEx\, winhttp.dll, doorstop_config.ini, .doorstop_version, changelog.txt (includes BepInEx\config)"
    } elseif (Test-Path $config) {
        Write-Host "Keeping settings file BepInEx\config\oc2mods.spawnrotation.staroverlay.cfg"
    }

    if (-not $Yes) {
        $answer = Read-Host "Continue? [y/N]"
        if ($answer -notmatch '^(y|yes)$') { Write-Host "Cancelled. Nothing changed."; return }
    }

    $targets | ForEach-Object { Remove-Item -LiteralPath $_ -Force }
    if ($removeLoader) {
        foreach ($f in 'winhttp.dll', 'doorstop_config.ini', '.doorstop_version', 'changelog.txt') {
            $p = Join-Path $GameDir $f
            if (Test-Path $p) { Remove-Item -LiteralPath $p -Force }
        }
        $b = Join-Path $GameDir 'BepInEx'
        if (Test-Path $b) { Remove-Item -LiteralPath $b -Recurse -Force }
    }
    Write-Host "Done."
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
