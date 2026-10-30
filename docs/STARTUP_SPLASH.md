# VR aircraft and mod loading screen

During Blueprinter 2.0.1 startup, NOVR presents a calm **Loading aircraft & mods** card instead of an unresponsive main menu. It explains that the menu opens automatically when content is ready. There is no percentage, countdown or spinner pretending to measure progress.

The [content-readiness adapter](CONTENT_READINESS.md) owns completion: both Blueprinter's verified success event and normal root-coroutine completion are required. Its temporary offline asset-loading session does not dismiss the card. Once loading finishes, the normal menu appears and startup presentation cannot return later in the flight.

## Controls and failure behavior

- Point either controller and press Trigger for **EXIT GAME**. A separate **EXIT NOW** confirmation prevents accidental shutdown; **KEEP WAITING** returns to loading.
- The existing **VR CENTER** control and keyboard **HOME** recenter the presentation. The loading card uses a fixed readable scale and distance, independent of custom main-menu sizing.
- Keyboard **F10** toggles exit options. Escape retains the game's own binding; the loading presentation does not repurpose it.
- If the loader stops without completing, the card explains the failure and directs the pilot to exit, check `BepInEx/LogOutput.log` on the PC, then relaunch. It does not pretend that retrying a button can repair an aircraft mod.

The splash works with native menus disabled. With no supported loader or unverified readiness, NOVR retains existing menu behavior instead of creating an indefinite loading screen. Actual aircraft control and multiplayer disable startup presentation permanently.

## Rendering and input ownership

The card shares the native menu's world-space canvas, pointer and button feedback. While it is visible, vanilla and native main-menu input are suppressed. These states are restored when loading completes or the component is disabled. The full menu remains available after the transition.

A persistent, UI-only stereo base camera renders the startup card against a dark background while Blueprinter replaces menu and offline-loading scenes. It is disabled, including its pose driver, after startup. No stereo eye projection, IPD, field of view or game-camera transform is overridden. The game still performs its loading work; NOVR cannot keep Unity drawing new frames while a third-party loader blocks the main thread. SteamVR's compositor remains responsible for presenting those intervals.

If a temporary scene lacks an EventSystem, NOVR supplies one for the loading controls and yields to the next game EventSystem. The temporary system is removed when the splash closes. Mouse fallback and tracked controller pointing use the existing UI input path. While the splash owns input, only the verified Blueprinter loading screen’s transparent click-blocking GraphicRaycaster is disabled and restored afterward; arbitrary mod canvases are untouched.

## Portable validation

```sh
dotnet run --project validation/StartupSplash/NOVR.StartupSplash.Tests.csproj -c Release
```

The 28 checks compile production lifecycle state and cover early/absent loader handling, persistent loading, success-event-plus-coroutine completion, failure, confirmation cancellation, unknown loaders, VR enable/disable, temporary offline scenes, and exclusion from flight/multiplayer. The network predicate distinguishes an inactive server’s default Listening flag from an active connection, including the loader’s offline host/client shutdown sequence. They do not verify headset comfort or Unity rendering.

## Live integration probes

1. Cold-start the game with supported Blueprinter and VR enabled. Observe `startup readiness tracking enabled`, then `Startup loading splash shown; menu input suspended` in the log.
2. During preparation, inspect `NOVR Startup Loading`: its card is visible, ordinary native panels are hidden, and `MainCanvas` canvases/CanvasGroup cannot receive menu input. `NOVR Startup Presentation` is an enabled stereo Base camera with the VR UI layer only, clear color, no post processing, and unmodified stereo matrices.
3. Across the loader's temporary offline host/scene, the card remains visible and its pointer/exit confirmation and VR CENTER remain usable. Cancel exit; do not confirm it during the completion test.
4. After the adapter reports actual completion, verify `Startup loading splash released; menu input restored`, the startup camera and temporary EventSystem are disabled/removed, and main-menu interaction returns. Starting an FS-20 mission must not reactivate the splash.
5. Test the failure presentation using an isolated diagnostic state override, not a deliberately broken user mod or save. Confirm its exit route is reachable, then restore state before clean deployment.
6. Check native-menu-disabled and stock/no-Blueprinter configurations. The former should still get the supported loading splash; the latter should go straight to its normal menu. Check both eyes in the headset before claiming visual comfort validation.

The installed 0.4.7.4 cold-start probe passed 12 checks, including continuous
visibility across the actual 73.38-second Blueprinter operation, confirmation
cancellation, camera/input cleanup, and return to the interactive main menu.
The inspection capture verifies layout on the PC; both-eye comfort remains a
headset check.

Fourteen additional live checks passed using a temporary diagnostic readiness
override: failure messaging, exit cancellation, operation with native menus
disabled, input/camera release, and full restoration of the real readiness state,
menu settings and anchors. No user mod or save was broken to induce failure.
