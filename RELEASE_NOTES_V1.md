# Hypnagogia Access v1.0.0

First public release of the screen reader accessibility mod for *Hypnagogia: Boundless Dreams*.

## Overview

This release adds direct NVDA screen reader support to parts of the game that can be difficult to access with OCR alone.

## Included Support

- menus
- dialogue
- cutscene text
- dialogue choice navigation
- many interaction prompts

## Accessibility Scope

This mod does **not** make the game fully blind playable.

*Hypnagogia: Boundless Dreams* is a colorful first-person platformer with exploration and platforming. Visual awareness is still required for navigation, movement, and environmental interaction. This release is primarily intended to improve access for **low vision players** and screen reader users who benefit from direct text output.

## Known Issue

- Mouse interaction can sometimes break screen reader output during dialogue choices.
  Workaround: switch to a controller, or visually click the intended choice.

## Not Yet Supported

- Hub world info nodes
- Secret character gallery billboard text
- Some late-game or optional content may still be inaccessible

## Installation

1. Install **BepInEx 5.4.23.5** for *Hypnagogia: Boundless Dreams* first.
2. Open the folder that contains `Hypnagogia Boundless Dreams.exe`.
3. Confirm BepInEx is already installed there. A correct install should include a `BepInEx` folder and several other BepInEx files in the same root folder as the `.exe`.
4. Download `HypnagogiaAccessMod.zip`.
5. Extract the zip into the game folder.
6. `HypnagogiaAccess.dll` should end up in `BepInEx/plugins`.
7. `nvdaControllerClient64.dll` should be in the game root folder beside the `.exe`.
8. Launch NVDA before starting the game.
9. Start the game.

## Feedback

Bug reports, compatibility notes, and accessibility feedback are welcome.
