# 0.4.8.34 — sturdier startup, VR start recovery, less per-frame work

- Starting up no longer depends on everything matching. Each VR adjustment is
  installed on its own, so a game update that renames one target disables that
  adjustment instead of leaving the game half patched and NOVR unstarted. An
  adjustment whose private game fields are missing is skipped before it can
  throw inside the game's own code. A different game version and any skipped
  adjustments are listed on the wrist menu's VR performance page.
- A headset that is not ready at launch no longer needs a game restart. The
  desktop window says VR has not started, NOVR tries again three times over the
  first minute, and **F11** tries again after that.
- Fewer settings writes. The whole settings file was rewritten for every
  setting bound at launch and on every step of a dragged slider; it is now
  written once at launch, a second after the last change, and on exit. Three
  test launches showed no measurable change in time to the main menu, so expect
  smoother menu sliders rather than a faster start.
- NOVR's interface is attached to game screens that are still loading by
  waiting for them (up to 30 seconds) instead of trying on three consecutive
  frames, which only worked while frames during loading were slow.
- Less per-frame work. Unit markers are placed without reflection and read both
  camera poses once per frame instead of once per marker; styled HUD graphics
  share one update; cameras are no longer looked up by name every frame; and
  NOVR's interface camera no longer asks for a depth copy, colour copy or
  shadows for each eye.
- New experimental **Share shadows (test)** (View → VR performance, off by
  default): the sun's shadow map is drawn once per frame and reused for the
  second eye. Not yet checked in a headset.
- Only the game's own scene container named "Canvas" is held at the origin;
  cockpit screens and other mods' objects with that default name are left alone.
- The foveation page now says outright when its settings do nothing on the
  current system, as on Linux/Proton.

Nothing in this version has been checked in a headset yet. Startup, patch installation and the no-headset retry were checked in the running game on Linux/Proton. One package for Windows and Linux/Proton. Saved layouts, bindings and settings are preserved.
