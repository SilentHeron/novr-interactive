# NOVR Interactive

A [Nuclear Option](https://store.steampowered.com/app/2168680/Nuclear_Option/) VR mod with virtual HOTAS controls, configurable Steam Frame / Touch bindings and a hand-following native option wheel.

**Disclaimer:**

> This project is an unofficial community modification and is not affiliated with, sponsored by, or endorsed by Shockfront Studios Pty Ltd. Original Nuclear Option assets, vehicle designs, audio, and code are Copyright (c) 2026 Shockfront Studios Pty Ltd. All rights reserved. Nuclear Option and Shockfront Studios are trademarks or registered trademarks of Shockfront Studios Pty Ltd. Original mod content and all other trademarks belong to their respective owners.

**A note from the maintainer:**

> Most of my contributions are AI-assisted. I’m not an experienced developer; I’m focused on creating a passable VR implementation while learning. This is an unofficial experiment intended as a stopgap, and I’m looking forward to the forthcoming official implementation from Shockfront. I’m doing my best to respect the game’s design and preserve the character of its excellent vanilla and modded aircraft.

Experimental version **0.4.8.34**. Targets Nuclear Option **0.34.6** and PC VR through OpenXR. Original cockpit displays stay in use. All aircraft share the FS-20 stick and grip; helicopters use a native collective lever instead of the plane throttle. The maintainer has tested Windows PC streaming with Steam Frame and Quest 3 through Steam Link.

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
| Map / respawn | B | B | Stick hand free, or spectating; press again to close |
| Outside view | Quick wheel → OUTSIDE VIEW | Quick wheel → OUTSIDE VIEW | In the cockpit, that hand free |
| Back to the cockpit | B | B | Outside view |
| VR View menu | Left View | Left Menu, or wheel → VR MENU | Either grip state for button; free hand for wheel |
| Quick wheel | Either thumbstick click | Either thumbstick click | That hand free |
| Reopen layout menu | Either thumbstick click | Either thumbstick click | Edit layout active |

Target selection and cycling act on button release; guns, weapon release, countermeasures and brakes are held actions. The look-direction cue is the game's own squashed diamond by default; circle, dot, crosshair and brackets remain under **View → Look direction cue**. The cue and **Select target nearest cue** prefer aircraft, ground units and ships over missiles inside the cue's cone, so an aircraft is not lost to the missile it just fired (**View → Look direction cue → Targeting**: Nearest, Units first or Aircraft first). Friendly units get no cue and no gaze lock unless **Cue friendly units** is on; the game's own target filters still apply. A white outline marks a handle within grab range (15 cm by default). Grip grabs it; **View → Flight controls** offers Hold or Toggle for each. **F7:** motion controls. **F8:** place handles. **F9:** View in the cockpit; map outside it.

For a persistent manual Ibis pusher-fan setting, turn Flight Assist off; enabled assist resumes native automatic control. For wing sweep, push the thumbstick forward to extend the wings or backward to sweep them aft.

**Quick wheel:** deflect to highlight; return to center to select. Click again to cancel. In flight, trigger changes Options / Weapons pages. **GAME MENU** opens the game's menu; **VR MENU** opens NOVR settings, including when Steam Link consumes the Quest Menu button. Eject requires a centered stick-click hold for 1.2 seconds; release to reset, or tilt / press trigger to cancel.

**Outside an aircraft:** thumbstick click opens a wheel with **VR MENU**, **GAME MENU** and **OPEN MAP / CLOSE MAP**, including while dead, waiting to spawn or spectating. After a crash that leaves your pilot alive in a detached cockpit or wrecked airframe, the same wheel adds **EJECT** (same centred 1.2-second hold); ejecting is what unlocks respawn in that case. **B** also toggles the map on Frame and Quest with the default right-handed layout (left-hand Y on Quest when mirrored). Closing the map returns to the same spectator target.

**Outside view:** pick **OUTSIDE VIEW** on the quick wheel (or **View → Flight actions**) to look at your aircraft from the game's orbit camera and keep flying. The virtual handles stay in the cockpit, so the controllers follow the game's own gamepad layout: the left stick is the flight stick (pull back for nose up), the right stick looks around the aircraft, the triggers are the rudder, and the grips are the throttle (left down, right up). Hold both grips and the right stick moves the camera closer or farther. Other buttons act as bound without a handle to hold; trigger bindings (guns and the brake by default) do not act out there, and the weapon-release button fires the selected weapon, guns included. **B** returns to the cockpit, as does **COCKPIT VIEW** on the wheel. The layout is the same for either handedness; only the cockpit button follows the stick hand. A stick, trigger or grip that is already held when the view changes does nothing until it has been let go once. With motion controls off, the sticks keep their spectator roles below.

**Spectating:** a full VR view follows the selected unit from the game’s normal external camera distance. Right stick orbits; left stick up/down moves closer/farther. Center the sticks after menus to resume camera control. With nothing followed (you joined as a spectator, or closed the spawn map), the game's free camera takes over: right stick looks, left stick flies, and either grip held moves ten times faster. While following a unit, **FREE CAMERA** on the quick wheel lets go of it; pick a unit on the map to follow one again. The spawn map and aircraft-selection pages keep their normal presentation and controls.

**Full-screen map:** point at the map, then move it with the left thumbstick and zoom with the right (swapped when left-handed). A small label above each thumbstick says which is which while the laser is on the map. Trigger selects.

**Minimap:** touch its face with a free fingertip to select it. While holding the flight stick, use the **physical right thumbstick** to pan and click to select an eligible target at the crosshair. Touch the face again to disengage and recenter on your aircraft, keeping zoom. After disengaging, center the thumbstick and release its click before normal flight bindings resume. Touch **+ / −** to zoom. In layout editing, two opposite corners adjust width and height independently without stretching the map.

**View → Edit layout:** reach a widget with your fingertip and grip to move it; throttle and stick placement uses the same edit mode. Tilt a held widget to adjust pitch/yaw; roll stays locked. Grab opposite corners with both hands to move, tilt and resize together. Labels, outlines and corner handles appear only while editing. Touch the eye to hide/show a widget, or its pencil for appearance sliders; surrounding widgets dim temporarily. Notifications and Instructions also offer **Text size** (65–150%) here and in **View → Widget appearance**. **Click either thumbstick to reopen the layout menu**, then save or cancel everything together. Each aircraft has three independent slots for its seat, handles and HUD layout. Existing layouts stay in Slot 1.

For a physical HOTAS, turn **View → Flight controls → Motion controls** off and bind your hardware in the game's control settings. HUD widgets and layout editing remain available; editing keeps virtual flight controls off.

**Wrist widgets:** in Edit layout, grip a panel with one hand and hover it over the other wrist. A white outline, haptic tick and **Release to attach** show when it's ready; release your grip to attach. Pull it away and release to place it back in the cockpit. Save the aircraft slot to keep changes. Wrist panels appear when you check them and hide when looking away or opening that hand's quick wheel. Hiding a wrist map also ends map control and recenters it. Unsaved layouts start in left, center and right groups below eye level.

**Auxiliary controls:** the aircraft's complete native nozzle, wing-sweep or fan gauge is a movable HUD frame, including its diagram. Adjust it through **View → Edit layout**. Only the fitted gauges are shown.

**Head clearance:** enabled by default with a 25 cm radius; adjust it from 5–40 cm in View settings. It cuts only eligible seat geometry near your head. Saved settings are retained when upgrading.

**Foveated rendering (experimental):** available on native Windows with compatible NVIDIA hardware and Direct3D 11. View → VR performance → Foveated rendering has a strength slider for fixed or eye-tracked use. Extreme and Maximum test smaller protected regions and coarser peripheral shading; MSAA limits the outer rate to 2×2. **Follow eye tracking** uses supported OpenXR gaze, including Steam Frame; missing tracking uses a fixed fallback. The protected centre is mapped onto the actual eye render target, which URP draws upside down on Direct3D 11, so it follows vertical gaze correctly, and fixation jitter no longer moves it. Two costly extras are opt-in through the BepInEx config, both off by default: **VR Performance → Full-Rate Foliage (Costly)** keeps trees and grass at full rate inside reduced areas, and **VR View → Game SMAA (Costly)** adds the game's own SMAA to the VR view. Foveation, eye tracking and GPU measurements default to Off. The same package works on Linux/Proton with foveation unavailable. [Details and comparison steps](docs/PC_FOVEATION.md).

**Game graphics:** View → VR performance → Game graphics adjusts the game's tree distance, shadow quality and cloud detail. Lower settings can help in scenes heavy with trees, shadows or clouds. Existing settings stay unchanged until you adjust them.

**If VR does not start:** when the headset or its runtime is not ready at launch, the desktop window says so. NOVR tries again three times over the first minute; after that, press **F11** to try again without restarting the game. Use `-novr-flat` to play on the desktop on purpose. When the game is a different version from the one this build targets, the wrist menu's VR performance page lists what could not be applied and the rest of the mod keeps working.

**Image quality:** View → VR performance → Image quality has four fixes that cost next to nothing to draw, all on by default: dithering for banded sky and haze gradients, no resting edge darkening (G-force effects still close the view in), mipmaps and anisotropic filtering for the cockpit's tactical screen, and a mipmapped copy of the cockpit map's terrain for when it is zoomed out. Switch one off if it looks wrong in your headset. **Hide distant cloud banks** is on the same page.

**Shadows shared between eyes (experimental):** View → VR performance → **Share shadows (test)** draws the sun's shadow map once per frame and reuses it for the second eye. It is off by default and has not been checked in a headset; turn it off if shadows flicker, shift or vanish in one eye.

**HUD symbol size:** target and location symbols, marker labels and reticles follow **Flight HUD Scale** (relative to its 38% default) when **Symbols Follow Flight HUD Scale** is on, in addition to their own sliders under View → HUD & markers → Symbols. The game's HMD icon size setting and its distance-based marker shrink still apply underneath.

**Distant cloud banks:** with cloudy weather (most multiplayer servers) the game rings you with large distant cloud meshes that read as a grey wall around the horizon in VR. They are hidden in VR by default; toggle **View → VR performance → Hide distant cloud banks** to compare live.

**View distance:** the game's camera stops rendering 80 km out, which a head-tracked view at altitude sees as a flat grey wall that pivots with your head. NOVR extends the tracked VR view's far plane to 250 km by default; **VR View → Far Clip Distance** in the BepInEx config is a minimum (0 keeps the game's value). The grass and tree detail window also stays centred on the aircraft's forward instead of swinging with your head.

[Controls](docs/CONTROLLER_BINDINGS.md) · [Changes](docs/RELEASE_0.4.8.34.md)

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
