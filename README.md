# NOVR Interactive

A [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR mod with virtual HOTAS controls, configurable Steam Frame / Touch bindings and a hand-following native option wheel.

**A note from the maintainer:**

> Most of my contributions are AI-assisted. I’m not an experienced developer; I’m focused on creating a passable VR implementation while learning. This is an unofficial experiment intended as a stopgap, and I’m looking forward to the forthcoming official implementation from Shockfront. I’m doing my best to respect the game’s design and preserve the character of its excellent vanilla and modded aircraft.

Experimental version **0.4.8.0**. Targets Nuclear Option **0.34.6** and PC VR through SteamVR. Original cockpit displays stay in use. Planes share the FS-20 virtual handles; helicopters keep their native controls. Quest / Touch controller support needs headset testing.

## Install

No package is published yet. Packages will be available from [Releases](https://github.com/SilentHeron/novr-interactive/releases).

1. Close the game and install BepInEx 5.x.
2. Extract `NOVR.zip` into the game's `BepInEx` folder.
3. Connect SteamVR, then launch Nuclear Option.

This replaces upstream NOVR; do not install both. On Proton, BepInEx needs the `winhttp` override set to `native,builtin`.

**F7:** motion controls. **F8:** place handles. **View / F9:** VR settings. On Steam Frame, with the stick held: **trigger** fires guns, **A** releases the selected weapon, **B** unselects the latest target and **Y** cycles weapons. Select buttons on the diagram under **View → Controller bindings** or **Settings → Controls → Visual VR Controller Bindings**.

**Free hand:** click its thumbstick to open the game’s option wheel. Deflect to highlight; return to center to select. Click again to cancel, including while highlighting. Trigger changes Options / Weapons pages. **View → Flight controls:** choose Hold or Toggle independently for stick and throttle.

**View → Edit layout:** reach a widget with your fingertip and grip to move it; throttle and stick placement uses the same edit mode. Tilt a held widget to adjust pitch/yaw; roll stays locked. Grab opposite corners with both hands to move, tilt and resize together. Labels, outlines and corner handles appear only while editing. Touch the eye to hide/show a widget, or its pencil for the backplate opacity slider; surrounding widgets dim temporarily. Reopen View to save or cancel everything together. Each aircraft has three independent slots for its seat, handles and HUD layout. Existing layouts stay in Slot 1.

[Controls](https://github.com/SilentHeron/novr-interactive/blob/main/docs/CONTROLLER_BINDINGS.md) · [Changes](https://github.com/SilentHeron/novr-interactive/blob/main/docs/RELEASE_0.4.8.0.md)

## Build

With .NET SDK 8, Nuclear Option and BepInEx installed:

```sh
dotnet build NOVR/NOVR.csproj -c Release -p:DeployToGame=false
dotnet build NOVR.Patcher/NOVR.Patcher.csproj -c Release -p:DeployToGame=false
```

The full solution needs SDK 10. [Verification instructions](https://github.com/SilentHeron/novr-interactive/blob/main/docs/VERIFICATION.md).

## Credits and license

Maintained by SilentHeron. Forked from [NOVR](https://github.com/InfernoSuperNova/novr) by InfernoSuperNova (DeltaWing), based on [UUVR](https://github.com/Raicuparta/uuvr) by Raicuparta. Independent of the original projects and game developer.

Copyright (C) 2026 InfernoSuperNova (DeltaWing). Original UUVR/Rai Pal notice: Copyright (C) 2024 Raicuparta. Licensed under [GPL-3.0-or-later](LICENSE), without warranty. [Third-party notices](https://github.com/SilentHeron/novr-interactive/blob/main/docs/THIRD_PARTY_NOTICES.md).
