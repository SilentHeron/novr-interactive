# NOVR Interactive

A [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR mod with virtual HOTAS controls, configurable Steam Frame / Touch bindings and selected physical cockpit interactions.

**A note from the maintainer:**

> Most of my contributions are AI-assisted. I’m not an experienced developer; I’m focused on creating a passable VR implementation while learning. This is an unofficial experiment intended as a stopgap, and I’m looking forward to the forthcoming official implementation from Shockfront. I’m doing my best to respect the game’s design and preserve the character of its excellent vanilla and modded aircraft.

Experimental version **0.4.7.12**. Targets Nuclear Option **0.34.6** and PC VR through SteamVR. Original cockpit displays stay in use; Quest / Touch controller support needs headset testing.

## Install

No package is published yet. Packages will be available from [Releases](https://github.com/SilentHeron/novr-interactive/releases).

1. Close the game and install BepInEx 5.x.
2. Extract `NOVR.zip` into the game's `BepInEx` folder.
3. Connect SteamVR, then launch Nuclear Option.

This replaces upstream NOVR; do not install both. On Proton, BepInEx needs the `winhttp` override set to `native,builtin`.

**F7:** motion controls. **F8:** place handles. **View / F9:** VR settings. On Steam Frame, with the stick held: **trigger** fires guns, **A** releases the selected weapon, **B** unselects the latest target and **Y** cycles weapons. **Right bumper:** zoom. Change bindings under **View → Controller bindings**.

**View → Edit layout:** grip any widget, throttle or stick to move it. Grab opposite widget corners with both hands to resize; angles stay locked. Labels, outlines and corner handles appear only while editing. Reopen View to save or cancel everything together. Layouts save per aircraft, with an optional shared HUD default.

[Controls](https://github.com/SilentHeron/novr-interactive/blob/main/docs/CONTROLLER_BINDINGS.md) · [FS-20 cockpit](https://github.com/SilentHeron/novr-interactive/blob/main/docs/FS20_NATIVE_COCKPIT.md) · [Changes](https://github.com/SilentHeron/novr-interactive/blob/main/docs/RELEASE_0.4.7.12.md)

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
