# NOVR Interactive

A [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR mod with motion controls, an interactive FS-20 cockpit and configurable MFDs.

**A note from the maintainer:**

> my contributions to this are mostly ai slop, I am not an experienced dev and I am just focused on creating a passable VR implementation as a stopgap until Shockfront releases their own, which I'm sure will be infinitely better and more faithful to the community and developer vision of the game. I am doing my best to not impinge on the design of any of the excellent vanilla & modded aircraft.

Experimental version **0.4.7.5**. Targets Nuclear Option **0.34.3** and Steam Frame PC streaming through SteamVR. Other aircraft use a shared cockpit panel.

## Install

No package is published yet. Packages will be available from [Releases](https://github.com/SilentHeron/novr-interactive/releases).

1. Close the game and install BepInEx 5.x.
2. Extract `NOVR.zip` into the game's `BepInEx` folder.
3. Connect SteamVR, then launch Nuclear Option.

This replaces upstream NOVR; do not install both. On Proton, BepInEx needs the `winhttp` override set to `native,builtin`.

**F7:** motion controls. **F8:** place handles. **View / F9:** VR settings. With the stick held, **trigger** fires guns, **A** releases the selected weapon and **B** cycles weapons. **Right bumper:** zoom.

[Controls](https://github.com/SilentHeron/novr-interactive/blob/main/docs/STEAM_FRAME.md) · [FS-20 cockpit](https://github.com/SilentHeron/novr-interactive/blob/main/docs/FS20_NATIVE_COCKPIT.md) · [Changes](https://github.com/SilentHeron/novr-interactive/blob/main/docs/RELEASE_0.4.7.5.md)

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
