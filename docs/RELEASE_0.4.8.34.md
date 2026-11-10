# 0.4.8.34 — outside view, free camera, sturdier startup, less per-frame work

- New **outside view** while flying. In the cockpit, click a free thumbstick
  and pick **OUTSIDE VIEW** on the quick wheel (also **View → Flight actions →
  Outside view**). The camera moves to the game's own orbit camera around your
  aircraft and you keep flying, with thumbsticks instead of the virtual
  handles:
  - The stick hand's thumbstick is the flight stick: pull back for nose up,
    left and right to roll.
  - The other thumbstick looks around the aircraft.
  - Hold the throttle hand's grip and its thumbstick becomes throttle (up and
    down) and rudder (left and right). The throttle keeps its setting when you
    let go.
  - Hold the stick hand's grip and the other thumbstick moves the camera
    closer or farther.
  - Buttons work without a handle to hold: guns, weapon release,
    countermeasures, brake and the rest act as bound.
  - **B** on the stick hand returns to the cockpit (Frame and Quest; the
    mirrored button when left-handed). The quick wheel offers **COCKPIT VIEW**
    too. Thumbstick click still opens the wheel, so those two buttons are not
    flight buttons out there. The wheel opens only when the clicked stick is
    centred, so pushing hard through a turn cannot pop it open.

  A thumbstick that is already pushed when the view or its role changes does
  nothing until it has been centred once. The first three times, the game's
  message feed names the controls. With motion controls off (a physical
  HOTAS), the view and **B** work the same and the thumbsticks keep their
  spectator roles.
- **Free camera for spectators.** With nothing followed (you joined as a
  spectator, or closed the spawn map) the right stick looks and the left stick
  flies the game's free camera; hold either grip to move ten times faster.
  Before, the sticks did nothing until a unit was picked on the map. While
  following a unit, the quick wheel adds **FREE CAMERA** to let go of it.
- The full-screen map shows what the thumbsticks do. The left stick has always
  moved the map and the right stick zoomed it, but nothing said so. While the
  laser is on the map, a small label above each thumbstick now names its job.
- Two more actions can be bound under **View → Controller bindings**:
  **Change view** (Menu, map & view; works with free hands, cockpit to outside
  and back) and **Nav lights** (Flight & defense). Both start unassigned, so
  no existing layout changes. Landing gear, radar, flight assist and hover
  assist were already there, and on Steam Frame the right **Menu** button can
  be chosen for any of them.

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
- New **View → VR performance → Image quality**: four image fixes that cost next
  to nothing to draw. All are on by default and each has its own switch.
  - **Dither gradients.** The headset image has 8 bits per colour channel and
    the game dithers none of its cameras, so sky, haze and night gradients
    show bands. The VR camera now asks the render pipeline for its dithering.
  - **No edge darkening.** The game keeps a vignette at 40% at all times and
    raises it as G-force builds. In VR it now starts from none; G effects
    still close the view in completely, and night vision keeps its goggle edge.
  - **Filter cockpit screens.** The tactical screen is drawn into a texture
    with no mipmaps and with anisotropic filtering switched off, so its text
    and lines crawl as your head moves. In VR it gets both. A multisampled
    texture cannot have mipmaps, so the screen gives up its 2x multisampling
    for them. The target window gains anisotropic filtering only.
  - **Smooth cockpit map.** The map's terrain is one 4096 or 8192 pixel image
    with no mipmaps, on a panel a few hundred pixels across. While the map is
    zoomed out far enough to sparkle, it is drawn from a half-size copy that
    has them. The copy is made once per map on the GPU and uses about 21 MB of
    video memory (43 MB for the largest map). The grid and unit icons are not
    changed.

  **Hide distant cloud banks** moved to the same page.
- New experimental **Share shadows (test)** (View → VR performance, off by
  default): the sun's shadow map is drawn once per frame and reused for the
  second eye. Not yet checked in a headset.
- Only the game's own scene container named "Canvas" is held at the origin;
  cockpit screens and other mods' objects with that default name are left alone.
- The foveation page now says outright when its settings do nothing on the
  current system, as on Linux/Proton.

Nothing in this version has been checked in a headset yet. That includes the outside view, the free camera and the map labels: their rules are covered by the source checks and they compile against the game, but how they fly, read and feel is not yet known. Startup, patch installation and the no-headset retry were checked in the running game on Linux/Proton, and the scan costs were timed there at the main menu. The image options were exercised there too, against the real engine without a headset: the filtered screen and the map copy sample as correct mipmaps, the vignette code runs against the game's real flight profile, and dithering changes the final image. How any of them looks in a headset is not yet known. One package for Windows and Linux/Proton. Saved layouts, bindings and settings are preserved.
