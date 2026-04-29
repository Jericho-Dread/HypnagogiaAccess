# Hypnagogia Accessibility Mod

Accessibility mod for *Hypnagogia: Boundless Dreams* with NVDA screen reader support.

## Overview

This mod adds direct screen reader support to parts of *Hypnagogia: Boundless Dreams* that are otherwise difficult to access with OCR alone.

Current support includes:

- menus
- dialogue
- cutscene text
- many interaction prompts
- dialogue choice navigation

## Accessibility Scope

This mod does **not** make the game fully blind playable.

*Hypnagogia: Boundless Dreams* is a colorful first-person platformer with exploration and platforming. Visual awareness is still required for navigation, movement, and environmental interaction. Some areas are dark and some rely heavily on visual information. At its current stage, the mod is primarily intended to improve access for **low vision players** and screen reader users who benefit from direct text output.

## Requirements

- Windows
- *Hypnagogia: Boundless Dreams*
- [BepInEx 5.4.23.5](https://github.com/BepInEx/BepInEx/releases)
- NVDA for speech output

## Installation

1. Install **BepInEx 5.4.23.5** for *Hypnagogia: Boundless Dreams*.
2. Download the latest `HypnagogiaAccessMod.zip` release from the GitHub releases page.
3. Extract the zip into the game folder.
4. Confirm these files are present:

```text
Hypnagogia Boundless Dreams/
  BepInEx/
    plugins/
      HypnagogiaAccess.dll
      nvdaControllerClient64.dll
```

5. Start NVDA.
6. Launch the game.

## Known Issue

- Mouse interaction can sometimes break screen reader output during dialogue choices.
  Workaround: switch to a controller, or visually click the intended choice.

## Not Yet Supported

- Hub world info nodes
- Secret character gallery billboard text
- Some late-game or optional content may still be inaccessible

## Source Repository

This repository contains source code only.

It does **not** include:

- commercial game binaries
- decompiled game source
- packaged releases
- build outputs
- machine-specific logs

## Building From Source

### Requirements

- .NET SDK
- A local copy of *Hypnagogia: Boundless Dreams*
- BepInEx installed into the game
- `nvdaControllerClient64.dll` placed in a local `externals/` folder

### Build

```powershell
dotnet build HypnagogiaAccess.csproj
```

If the game is installed in a different location:

```powershell
dotnet build HypnagogiaAccess.csproj `
  /p:GameDir="D:\SteamLibrary\steamapps\common\Hypnagogia Boundless Dreams"
```
