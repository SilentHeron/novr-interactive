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
- Less work that grows with the size of the mission. The cockpit map holds one
  object per unit icon and the flight HUD one per unit marker, and several
  parts of NOVR went through all of them on every frame or twice a second:
  - The pass that makes cockpit widget text respect hand depth walked every
    widget graphic on every frame. It now runs when a widget's contents can
    have changed, and four times a second otherwise.
  - Twice a second NOVR searched the whole flight HUD four times, walked it
    once more looking for the airbase overlay, and moved every map icon to the
    widget's layer again. The searches now skip the marker layer or are gone,
    the overlay is found once, and icons already moved are left alone.
  - Styled HUD graphics read the size settings once per frame instead of once
    per unit marker.
  - Private game fields read on every frame (bomb and gun sights, airbase and
    objective markers, missile warnings, map lines, the turret crosshair) go
    through typed field references instead of reflection, which also boxed
    every flag, number and vector it returned. The check NOVR runs for every
    turret that has a target, 60 times a second, allocated about 366 bytes each
    time and now allocates nothing.

  Timed in the game's own engine at the main menu on Linux/Proton, with
  hierarchies built to the game's shape for a 500-unit mission: the depth pass
  went from 108 to 3.5 microseconds on a frame where nothing changed, the
  style refresh from 29 to 19, and the twice-a-second burst from about 315 to
  about 40. Together that is roughly 0.12 ms of a frame, about 1% at 90 Hz
  (about 2% with 1,000 units). Expect steadier frame times in large battles
  rather than a higher frame rate. Method and all figures:
  [HUD and map scans](benchmarks/HUD_SCANS_0.4.8.34.md).
- New experimental **Share shadows (test)** (View → VR performance, off by
  default): the sun's shadow map is drawn once per frame and reused for the
  second eye. Not yet checked in a headset.
- Only the game's own scene container named "Canvas" is held at the origin;
  cockpit screens and other mods' objects with that default name are left alone.
- The foveation page now says outright when its settings do nothing on the
  current system, as on Linux/Proton.

Nothing in this version has been checked in a headset yet. Startup, patch installation and the no-headset retry were checked in the running game on Linux/Proton, and the scan costs were timed there at the main menu. One package for Windows and Linux/Proton. Saved layouts, bindings and settings are preserved.
