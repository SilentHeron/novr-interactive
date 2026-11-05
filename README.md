# NOVR Interactive

A [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR mod with virtual HOTAS controls, configurable Steam Frame / Touch bindings and a hand-following native option wheel.

**Disclaimer:**

> This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.

**A note from the maintainer:**

> Most of my contributions are AI-assisted. I’m not an experienced developer; I’m focused on creating a passable VR implementation while learning. This is an unofficial experiment intended as a stopgap, and I’m looking forward to the forthcoming official implementation from Shockfront. I’m doing my best to respect the game’s design and preserve the character of its excellent vanilla and modded aircraft.

Experimental version **0.4.8.23**. Targets Nuclear Option **0.34.6** and PC VR through OpenXR. Original cockpit displays stay in use. All aircraft share the FS-20 stick and grip; helicopters use a native collective lever instead of the plane throttle. The maintainer has tested Windows PC streaming with Steam Frame and Quest 3 through Steam Link.

## Install

Download the experimental package from [Releases](https://github.com/SilentHeron/novr-interactive/releases). The Windows and Linux installers also accept a downloaded `NOVR.zip` through **Install a downloaded NOVR package…**.

1. Close the game. In Steam, right-click Nuclear Option → **Manage → Browse local files** to open the folder containing `NuclearOption.exe`.
2. Download **BepInEx 5 for Windows x64** (`BepInEx_win_x64_5…zip`) from [BepInEx releases](https://github.com/BepInEx/BepInEx/releases). Copy the archive's contents directly into that game folder, so `winhttp.dll`, `doorstop_config.ini` and `BepInEx` sit beside `NuclearOption.exe`.
3. Extract the contents of `NOVR.zip` into that **BepInEx** folder. You should now have `BepInEx/plugins/NOVR/NOVR.dll` and `BepInEx/patchers/NOVR/NOVR.Patcher.dll`.
4. **Linux/Proton only:** add `WINEDLLOVERRIDES="winhttp=n,b" %command%` to the game's Steam launch options. Keep other options; merge an existing `WINEDLLOVERRIDES` value and use `%command%` only once.
5. Connect your headset to its PC VR runtime, then press **Play in Steam**. NOVR uses DirectX 11; a launch on another renderer restarts once automatically.

**On Linux, you still need Windows BepInEx:** Nuclear Option runs as a Windows game through Proton. BepInEx has no separate installer `.exe` to run; it loads when the game starts. The Linux BepInEx package and its `run_bepinex.sh` are for native Linux games. The optional **NOVR Linux installer** is a different tool and chooses Windows BepInEx for you.

This replaces upstream NOVR; do not install both. The installer does not change Proton launch options. DirectX 11 also applies on Linux: Proton translates it to Vulkan through DXVK. If automatic startup fails, replace any other renderer flag with `-force-d3d11`. Use `-novr-flat` for an intentional non-VR launch. [Installation troubleshooting](docs/STEAM_FRAME.md#installation-troubleshooting).

## Default controls

These buttons assume the flight stick is in your right hand. Left-handed mode moves role-based actions with the stick/throttle, using the corresponding buttons on that controller; the View menu button stays on the physical left. Custom bindings are retained. Edit them under **View → Controller bindings** or **Settings → Controls → Visual VR Controller Bindings**.

| Action | Steam Frame | Quest / Touch | Context |
| --- | --- | --- | --- |
| Guns | Right trigger | Right trigger | Stick held |
| Selected weapon release | A | A | Stick held |
| Select target nearest cue | Right thumbstick click | Right thumbstick click | Stick held |
| Unselect latest target | B | B | Stick held |
| Next weapon | Y | Y | Frame: stick held; Touch: throttle held |
| Deploy countermeasures | Left D-pad down | X | Throttle held |
| Next countermeasure type | Left D-pad right | Left thumbstick click | Throttle held |
| Wheel / air brake | Left trigger | Left trigger | Throttle held |
| Nozzle tilt / wing sweep / pusher fans | Left thumbstick up/down | Left thumbstick up/down | Throttle or collective held |
| Map / respawn | B | B | Stick hand free, or recovery view |
| VR View menu | Left View | Left Menu, or wheel → VR MENU | Either grip state for button; free hand for wheel |
| Quick wheel | Either thumbstick click | Either thumbstick click | That hand free |
| Reopen layout menu | Either thumbstick click | Either thumbstick click | Edit layout active |

Target selection and cycling act on button release; guns, weapon release, countermeasures and brakes are held actions. A white outline marks a handle within grab range (15 cm by default). Grip grabs it; **View → Flight controls** offers Hold or Toggle for each. **F7:** motion controls. **F8:** place handles. **F9:** VR settings.

For a persistent manual Ibis pusher-fan setting, turn Flight Assist off; enabled assist resumes native automatic control. For wing sweep, push the thumbstick forward to extend the wings or backward to sweep them aft.

**Quick wheel:** deflect to highlight; return to center to select. Click again to cancel. Trigger changes Options / Weapons pages. **GAME MENU** opens the game's menu; **VR MENU** opens NOVR settings, including when Steam Link consumes the Quest Menu button. Eject requires a centered stick-click hold for 1.2 seconds; release to reset, or tilt / press trigger to cancel.

**Minimap:** touch its face with a free fingertip to select it. While holding the flight stick, use the **physical right thumbstick** to pan and click to select an eligible target at the crosshair. Touch the face again to disengage and recenter on your aircraft, keeping zoom. After disengaging, center the thumbstick and release its click before normal flight bindings resume. Touch **+ / −** to zoom. In layout editing, two opposite corners adjust width and height independently without stretching the map.

**View → Edit layout:** reach a widget with your fingertip and grip to move it; throttle and stick placement uses the same edit mode. Tilt a held widget to adjust pitch/yaw; roll stays locked. Grab opposite corners with both hands to move, tilt and resize together. Labels, outlines and corner handles appear only while editing. Touch the eye to hide/show a widget, or its pencil for appearance sliders; surrounding widgets dim temporarily. Notifications and Instructions also offer **Text size** (65–150%) here and in **View → Widget appearance**. **Click either thumbstick to reopen the layout menu**, then save or cancel everything together. Each aircraft has three independent slots for its seat, handles and HUD layout. Existing layouts stay in Slot 1.

For a physical HOTAS, turn **View → Flight controls → Motion controls** off and bind your hardware in the game's control settings. HUD widgets and layout editing remain available; editing keeps virtual flight controls off.

**Wrist widgets:** in Edit layout, grip a panel with one hand and hover it over the other wrist. A white outline, haptic tick and **Release to attach** show when it's ready; release your grip to attach. Pull it away and release to place it back in the cockpit. Save the aircraft slot to keep changes. Wrist panels appear when you check them and hide when looking away or opening that hand's quick wheel. Hiding a wrist map also ends map control and recenters it. Unsaved layouts start in left, center and right groups below eye level.

**Auxiliary controls:** the aircraft's complete native nozzle, wing-sweep or fan gauge is a movable HUD frame, including its diagram. Adjust it through **View → Edit layout**. Only the fitted gauges are shown.

**Head clearance:** enabled by default with a 25 cm radius; adjust it from 5–40 cm in View settings. It cuts only eligible seat geometry near your head. Saved settings are retained when upgrading.

[Controls](docs/CONTROLLER_BINDINGS.md) · [Changes](docs/RELEASE_0.4.8.23.md)

## Build

With .NET SDK 8 and Nuclear Option installed (BepInEx is needed to run the mod):

```sh
dotnet build NOVR/NOVR.csproj -c Release -p:DeployToGame=false
dotnet build NOVR.Patcher/NOVR.Patcher.csproj -c Release -p:DeployToGame=false
```

The full solution needs SDK 10. Building does not install the mod. Optional installer executables require `-p:PublishInstallers=true`.

[Contributing](CONTRIBUTING.md) · [Code architecture](docs/ARCHITECTURE.md) · [Verification](docs/VERIFICATION.md).

## Credits and license

Maintained by SilentHeron. Forked from [NOVR](https://github.com/InfernoSuperNova/novr) by InfernoSuperNova (DeltaWing), based on [UUVR](https://github.com/Raicuparta/uuvr) by Raicuparta. Independent of the original projects and game developer.

Copyright (C) 2026 InfernoSuperNova (DeltaWing). Original UUVR/Rai Pal notice: Copyright (C) 2024 Raicuparta. Mod code is licensed under [GPL-3.0-or-later](LICENSE), without warranty. Controller artwork is separate from the code license; Valve Frame diagrams are CC BY-NC-SA 4.0. [Third-party notices](https://github.com/SilentHeron/novr-interactive/blob/main/docs/THIRD_PARTY_NOTICES.md).
