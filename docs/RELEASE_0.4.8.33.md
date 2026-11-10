# 0.4.8.33 — eye-tracked foveation alignment, cockpit map markers, view distance

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
- Foveation keeps its 0.4.8.32 cost. An earlier test build of this version
  shaded the middle ring at full rate, widened the sharp region for eye tracking
  and fast eye movements, scaled it by each eye's field of view, drew foliage at
  full rate and added the game's SMAA. Together they made frame rates much worse,
  so the presets are back to 1×1 / 2×1 / 2×2 with their original radii. Two
  extras stay as opt-ins in the BepInEx config, both off by default and renamed
  so a saved "on" from the test build does not carry over:
  **VR Performance → Full-Rate Foliage (Costly)** keeps trees and grass sharp
  inside reduced areas, and **VR View → Game SMAA (Costly)** adds the game
  camera's post-process anti-aliasing on top of MSAA.
- Eye-tracked foveation holds its centre through fixation jitter (one-tile
  deadband), so the edge of the sharp region no longer shimmers.
- With **Measure GPU cost** on, the readout adds a whole-frame line: smoothed
  frame interval and FPS, plus the app's total GPU time when the runtime reports it.
- The look-direction cue now defaults to the game's own squashed diamond, drawn
  to the vanilla target designator's proportions in the HUD colour. A saved
  circle, the previous default, switches once; other chosen shapes are kept, and
  the circle stays available under **View → Look direction cue**.
- Eye-tracked foveation now protects where you look. URP renders the world pass
  into an intermediate texture that Direct3D 11 stores upside down; the
  protected centre was mapped onto an upright image, so it moved opposite to
  vertical gaze and coarse shading reached the fovea, which looked like heavy
  aliasing. The centre now follows URP's own target-orientation answer. Fixed
  foveation shifts only slightly (its centre is nearly symmetric).
- Selected-target info and objective markers on the cockpit map stay in the map
  plane. Native code set a world rotation every frame, so on the tilted console
  map a large red (hostile) or blue (friendly) marker stood upright in front of
  the pilot and flashed with each target selection. Target info is now also
  hidden immediately on the minimized map, as native code does one second later.
- Hit markers are projected along the hit direction instead of flat-screen
  pixel coordinates.
- Cycling weapons no longer flashes an oversized targeting symbol. The game
  builds a new native-size weapon widget on every switch, and NOVR resized it
  only on its next half-second scan; the widget is now sized as it is created.
- Crashes that leave the pilot alive in an intact cockpit no longer strand you.
  When the cockpit detaches or the airframe is destroyed, the thumbstick wheel
  now adds **EJECT** (same centred 1.2-second hold) beside Game Menu, VR Menu
  and Map; native respawn stays locked until the pilot ejects. If the game's
  own flight wheel is unavailable, the same menu wheel opens instead of nothing.
- The look-direction cue and gaze lock no longer appear on or select friendly
  units unless **Cue friendly units** is enabled (View → Look direction cue →
  Targeting). The game's own target filters still apply.
- HUD symbols now follow the Flight HUD size. Target/location symbols, marker
  labels and reticles were sized only by their own sliders while instruments and
  weapon readouts followed **Flight HUD Scale**; the combined wrist slider only
  nudged them additively. **Symbols Follow Flight HUD Scale** (on by default,
  View → HUD & markers → Symbols) scales them with Flight HUD Scale relative to
  its 38% default, so nothing changes at the default size. The game's own
  per-distance marker shrink and HMD icon size setting still apply underneath.
- New **Gaze lock priority**: inside the cue's cone, aircraft, ground units and
  ships now win over missiles by default (Units first); Aircraft first and
  Nearest are also available. The nearest angle still decides within a class, and
  a lone missile in the cone still locks.
- The tracked VR view's far plane is extended from the game's 80 km to 250 km
  (**VR View → Far Clip Distance**, a minimum; 0 keeps the game's value). At
  altitude the 80 km cut shows as a flat grey wall that pivots with head turns.
  The grass/tree detail window is centred along the aircraft's forward instead
  of the head, so its edge no longer swings when looking around.
- Distant cloud banks are hidden in VR by default (**View → VR performance →
  Hide distant cloud banks**, applies live). With weather coverage above 10%,
  as on most multiplayer servers, the game rings you with about 300 cloud meshes
  of 1.2 km and up, 10-40 km out at the cloud height; in the headset that ring
  reads as a grey wall around the horizon. Nearby clouds and the cloud layer stay.

Foveation changes need a native Windows/NVIDIA/Direct3D 11 playtest; the source
checks establish the mapping, not image quality. Nothing changes antialiasing
unless you opt in to Game SMAA, and nothing changes MSAA, headset resolution or
SteamVR settings. One package for Windows and Linux/Proton.
