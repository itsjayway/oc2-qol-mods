# OC2 QoL Mods

Quality-of-life mods for Overcooked! 2 (PC, Steam), built as one BepInEx 5 (x86) plugin.

| Feature | How | Who needs the mod |
|---|---|---|
| **Spawn rotation** | On the world map, the host presses **F5** / **F6** to rotate players 2–4's kitchen starting positions. P1 stays put. Needs 3+ players. | Host only |
| **Star targets** | During a level, the top of the screen shows the 1★/2★/3★ score thresholds (and 4★ in New Game+), with ✓ on the ones you've reached. **F3** toggles it. | Each player who wants it |
| **Player stats** | The results screen shows a table per player: delivered, wrong dish, chopped, washed, binned, caught throws. | Each player who wants it; the delivery columns also need the host |

Settings: `BepInEx/config/oc2mods.spawnrotation.staroverlay.cfg` (created on first launch).

## Install (players)

Download the zip from [Releases](../../releases), extract it, and double-click `install.cmd`. See `README.txt` in the zip.

## How the stats work

The host already broadcasts per-chef stat increments to every player (`ServerMessenger.Achievement`). Unmodded games ignore stat ids they don't define. A modded host adds delivery stats on that same message using unused ids (1000, 1001), plus a handshake (1023), and checks first that this game version doesn't define those ids. No new network message types are used, so unmodded players aren't affected.

## Build (maintainers)

Needs the game installed locally. There's no .NET SDK requirement; it uses the Windows .NET Framework `csc` and references the game's own assemblies, which are never committed.

```sh
curl -sSL -o .work/bepinex.zip https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.3/BepInEx_win_x86_5.4.23.3.zip
bash build.sh      # -> out/OC2Mods.dll
bash package.sh    # -> dist/OC2-QoL-Mods-v<version>.zip (version from [BepInPlugin] in src/OC2Mods.cs)
gh release create v<version> dist/OC2-QoL-Mods-v<version>.zip
```

Never commit game DLLs, decompiled game code (`.work/`), or build output (`out/`, `dist/`).

## License

MIT. Bundles BepInEx 5.4.23.3 (MIT).
