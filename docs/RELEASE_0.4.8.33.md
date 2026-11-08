# 0.4.8.33 — eye-tracked foveation alignment, cockpit map markers, view distance

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

Foveation changes need a native Windows/NVIDIA/Direct3D 11 playtest; the source
checks establish the mapping, not image quality. Nothing changes antialiasing,
headset resolution or SteamVR settings. One package for Windows and Linux/Proton.
