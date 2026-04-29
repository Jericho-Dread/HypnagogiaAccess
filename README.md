# Hypnagogia Accessibility Mod

Accessibility mod for *Hypnagogia: Boundless Dreams* with NVDA screen reader support.

## Requirements

- A Windows copy of *Hypnagogia: Boundless Dreams*
- [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases)
- NVDA if you want speech output

## What to Download

For a public release, I recommend shipping **the mod only**, not a full BepInEx install.

Release zip contents:

- `BepInEx/plugins/HypnagogiaAccess.dll`
- `BepInEx/plugins/nvdaControllerClient64.dll`

Why:

- It keeps the release smaller and cleaner.
- Users can reuse the same BepInEx install for multiple mods.
- It avoids redistributing extra loader files unless you explicitly want a bundled package.

If you want, you can still make a separate "full package" later for players who want the easiest setup.

## Install Guide

1. Install **BepInEx 5.4.23.5** for the game first.
2. Open your game folder.
3. Extract the mod zip into the game folder.
4. Make sure these files end up here:

```text
Hypnagogia Boundless Dreams/
  BepInEx/
    plugins/
      HypnagogiaAccess.dll
      nvdaControllerClient64.dll
```

5. Start NVDA.
6. Launch the game.

## First Launch Check

If the mod loaded correctly:

- the game should start normally
- `BepInEx` should already be installed and active
- NVDA should begin reading supported dialogue, menus, and interaction prompts

If nothing is read:

- confirm NVDA is running
- confirm both mod files are inside `BepInEx/plugins`
- confirm you installed **BepInEx 5.4.23.5**

## Recommended Release Notes

Good short release instructions:

1. Install BepInEx 5.4.23.5 for *Hypnagogia: Boundless Dreams*.
2. Extract this zip into the game folder.
3. Launch NVDA before starting the game.

## Building From Source

This repository is set up for source release without bundling commercial game binaries.

It does **not** need to include:

- game DLLs
- decompiled game source
- build outputs
- release zips
- machine-specific logs

### Build Requirements

- .NET SDK
- A local copy of *Hypnagogia: Boundless Dreams*
- BepInEx installed into the game
- `nvdaControllerClient64.dll` placed in a local `externals/` folder

### Build Command

```powershell
dotnet build HypnagogiaAccess.csproj
```

If your game is installed somewhere else:

```powershell
dotnet build HypnagogiaAccess.csproj `
  /p:GameDir="D:\SteamLibrary\steamapps\common\Hypnagogia Boundless Dreams"
```
