OC2 QoL Mods for Overcooked! 2 (PC, Steam)
=========================================

INSTALL
  1. Close Overcooked! 2.
  2. Extract this zip anywhere.
  3. Double-click install.cmd. It finds the game through Steam, shows what it
     will add, and asks before changing anything. If you already have BepInEx,
     it leaves BepInEx alone and only adds OC2Mods.dll.
  4. Start the game from Steam.

  Game in a custom location? In PowerShell, run:
     .\install.ps1 -GameDir "D:\Games\Overcooked! 2"

UNINSTALL
  Double-click uninstall.cmd (removes only this mod), or run
     .\uninstall.ps1 -RemoveBepInEx
  to also remove BepInEx when no other mods use it.
  Steam > Overcooked! 2 > Properties > Installed Files > "Verify integrity"
  also undoes everything.

FEATURES                                   WHO NEEDS THE MOD
  Spawn rotation (F5/F6 on world map)      host only
  Star targets during a level (F3 toggle)  each player who wants to see them
  Per-player stats on the results screen   each player who wants to see them;
                                           delivery columns also need the host

Settings: BepInEx\config\oc2mods.spawnrotation.staroverlay.cfg in the game
folder. It's created the first time you start the game with the mod.

TROUBLESHOOTING
  - Antivirus warning about winhttp.dll: that's BepInEx's loader, which is how
    every BepInEx mod starts. It comes from https://github.com/BepInEx/BepInEx.
  - Steam Deck / Linux (Proton): set the game's launch options to
       WINEDLLOVERRIDES="winhttp=n,b" %command%
  - Not working? Look at BepInEx\LogOutput.log in the game folder.

Includes BepInEx 5.4.23.3 (MIT license, see BepInEx-LICENSE.txt).
