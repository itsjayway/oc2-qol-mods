# Installs OC2 QoL Mods (and BepInEx, if missing) into Overcooked! 2.
# Additive and idempotent: never overwrites an existing BepInEx install or config.
# Usage: right-click > Run with PowerShell, or double-click install.cmd.
#   -GameDir <path>   use this game folder instead of finding it through Steam
#   -Yes              don't ask for confirmation
param([string]$GameDir, [switch]$Yes)

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$payload = Join-Path $here 'BepInEx_x86'
$plugin = Join-Path $here 'OC2Mods.dll'
. (Join-Path $here 'common.ps1')


try {
    if (-not (Test-Path $plugin) -or -not (Test-Path $payload)) {
        throw "Run this from the extracted zip folder (OC2Mods.dll and BepInEx_x86\ must be next to it)."
    }
    if (-not $GameDir) { $GameDir = Find-GameDir }
    if (-not $GameDir -or -not (Test-Path (Join-Path $GameDir 'Overcooked2.exe'))) {
        throw "Couldn't find Overcooked! 2. Re-run with -GameDir 'C:\path\to\Overcooked! 2'."
    }
    if (Get-Process -Name Overcooked2 -ErrorAction SilentlyContinue) {
        throw "Overcooked! 2 is running. Close it and try again."
    }

    $bepinexCore = Join-Path $GameDir 'BepInEx\core\BepInEx.dll'
    $hasBepInEx = Test-Path $bepinexCore
    $copies = @()
    if (-not $hasBepInEx) {
        if (Test-Path (Join-Path $GameDir 'winhttp.dll')) {
            throw "The game folder has a winhttp.dll but no BepInEx. Another mod loader may be installed; not changing anything."
        }
        foreach ($f in Get-ChildItem -Path $payload -Recurse -File -Force) {
            $rel = $f.FullName.Substring($payload.Length + 1)
            $dest = Join-Path $GameDir $rel
            if (-not (Test-Path $dest)) { $copies += ,@($f.FullName, $dest) }
        }
    }
    $pluginDest = Join-Path $GameDir 'BepInEx\plugins\OC2Mods.dll'
    $pluginSame = (Test-Path $pluginDest) -and
        ((Get-Sha256 $pluginDest) -eq (Get-Sha256 $plugin))

    Write-Host "Game folder: $GameDir"
    if ($hasBepInEx) { Write-Host "BepInEx: already installed, leaving it alone" }
    else { Write-Host ("BepInEx: will add {0} files" -f $copies.Count) }
    if ($pluginSame) {
        Write-Host "OC2 QoL Mods: already up to date. Nothing to do."
        return
    } elseif (Test-Path $pluginDest) {
        Write-Host "OC2 QoL Mods: will update BepInEx\plugins\OC2Mods.dll (old copy kept as OC2Mods.dll.bak)"
    } else {
        Write-Host "OC2 QoL Mods: will add BepInEx\plugins\OC2Mods.dll"
    }

    if (-not $Yes) {
        $answer = Read-Host "Continue? [y/N]"
        if ($answer -notmatch '^(y|yes)$') { Write-Host "Cancelled. Nothing changed."; return }
    }

    foreach ($c in $copies) {
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $c[1]) | Out-Null
        Copy-Item -LiteralPath $c[0] -Destination $c[1]
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $pluginDest) | Out-Null
    if (Test-Path $pluginDest) { Copy-Item -LiteralPath $pluginDest -Destination "$pluginDest.bak" -Force }
    Copy-Item -LiteralPath $plugin -Destination $pluginDest -Force
    Write-Host "Done. Start Overcooked! 2 from Steam."
} catch [System.UnauthorizedAccessException] {
    Write-Host "Permission denied writing to the game folder. Right-click install.cmd > Run as administrator." -ForegroundColor Red
    exit 1
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
