# 0.4.8.34 — outside view, free camera, sturdier startup, less per-frame work

- New **outside view** while flying. In the cockpit, click a free thumbstick
  and pick **OUTSIDE VIEW** on the quick wheel (also **View → Flight actions →
  Outside view**). The camera moves to the game's own orbit camera around your
  aircraft and you keep it under control. It is for looking at the aircraft,
  not a second cockpit: there is no rudder and the throttle stays where you
  left it. The virtual handles stay in the cockpit, so the thumbsticks sit as
  on the game's own gamepad layout, the same for either handedness:
  - Left thumbstick is the flight stick: pull back for nose up, left and right
    to roll.
  - Right thumbstick looks around the aircraft. Hold either grip and its up
    and down move the camera closer or farther.
  - Buttons work as bound, without a handle to hold. With the default layout
    the right trigger fires guns and A releases the selected missile or bomb,
    exactly as in the cockpit; countermeasures, next weapon and the rest work
    too.
  - **B** on the stick hand returns to the cockpit (Frame and Quest; the
    mirrored button when left-handed). The quick wheel offers **COCKPIT VIEW**
    too. Thumbstick click still opens the wheel, so those two buttons are not
    flight buttons out there. The wheel opens only when the clicked stick is
    centred, so pushing hard through a turn cannot pop it open.

  A thumbstick already pushed, or a grip already squeezed, when the view
  starts or a menu closes does nothing until it has been let go once. The
  first three times, the game's message feed names the controls. With motion
  controls off (a physical HOTAS), the view and **B** work the same and the
  thumbsticks keep their spectator roles.
- **Free camera for spectators.** With nothing followed (you joined as a
  spectator, or closed the spawn map) the right stick looks and the left stick
  flies the game's free camera; hold either grip to move ten times faster.
  Before, the sticks did nothing until a unit was picked on the map. While
  following a unit, the quick wheel adds **FREE CAMERA** to let go of it.
- **Your controllers in the menus and on the map.** The VR menus (main menu,
  mission select, multiplayer, settings), the aircraft selection screen and
  the full-screen map show the controller you are holding: Steam Frame, Quest 2, Quest 3 or Quest Pro. NOVR ships no
  model of its own. It reads the one SteamVR already installed for that
  controller and places it on your tracked hand; if SteamVR's files are not
  found, nothing is shown and the menus work as before.
  - Raise a controller toward your face and it is labelled with what its
    buttons do on that screen, and nothing else. In the menus that is the
    trigger (**Select** on the hand holding the pointer, **Take the pointer**
    on the other) and the pointer hand's thumbstick (**Up / down: scroll**).
    Aircraft selection adds walking, turning and the quick wheel, below.
  - Each label's line is real geometry that runs to the button itself and
    ends with a small bead on its surface, so it stays on the button from any
    angle. NOVR reads the separate mesh SteamVR ships for every button to
    find that surface. A button the line cannot reach straight (a trigger,
    seen from above) is reached round the side of the controller: the line
    drops beside it to the trigger's depth and then runs in, instead of
    seeming to end on the top face. The label panels now stand clear of the
    whole controller instead of partly covering its head.
  - A button turns white on the model while you press it: trigger, grip,
    thumbstick click, face buttons, bumpers, Menu and View, and the Frame's
    D-pad. Turn it off with **VR Menus → Controller Press Highlight**.
  - On the full-screen map the labels are the map's: **Move map** on one
    thumbstick and **Up / down: zoom map** on the other (swapped when
    left-handed), **Click: quick wheel** on both, and **Close map** on the
    button you have bound to Map (B by default). In a cockpit the controllers
    take the place of the pilot's gloves while the map is open, and the gloves
    return when it closes. The small hint above each thumbstick steps aside
    for a raised controller's own labels.
  - The models now stay loaded through a mission (about 45 MB of texture
    memory for the pair) instead of being released when it starts, so opening
    the map does not load them again.
    Flight bindings are not shown: there is nothing to fly there. Each label
    points at the real trigger and thumbstick of that controller. Lower the
    controller and only the model remains.
  - Both can be turned off in the BepInEx config, section **VR Menus**:
    **Controller Models** and **Controller Tooltips**.
- **Walk around the aircraft at aircraft selection.** With the hand that is not
  holding the menu pointer, push the thumbstick forward to throw an aiming
  arc, and let go to teleport to where it lands. The aircraft and the menu
  stay where they are while you walk, so the menu is never in the way. To
  change something, pull the same thumbstick back and let go: the menu comes
  to you, three metres ahead of where you are looking, and stays there until
  you call it again. You can stand on any surface the arc lands on that is
  flat enough, and a circle around the aircraft is as far as you can go.
  - **Snap turn:** flick the left thumbstick left or right to turn in place,
    45 degrees a flick (**VR Menus → Snap Turn Degrees**, 15 to 90). The view
    turns about your head, so you stay on the spot you teleported to.
  - The thumbstick labels call all of this out: **Push, release: TELEPORT**
    and **Pull back: bring menu here** on the walking hand, **Left / right:
    snap turn** on the left one, **Click: quick wheel** on both.
  - Leaving the screen puts you, your heading and the menu back. Turn the
    whole thing off with **VR Menus → Walk Around Aircraft** in the BepInEx
    config.
- **Throttle feedback.** Sliding the virtual throttle now gives a light tick
  for every 5% of travel, like a notched slider, and reaching idle adds a
  second pulse right after the existing end-stop pulse, so 0% feels different
  from full power and from everywhere in between. Both follow the haptic
  strength setting.
- The main menu's rail is headed by the words **NOVR Interactive** instead of
  the red logo image, which is no longer in the package.
- The full-screen map shows what the thumbsticks do. The left stick has always
  moved the map and the right stick zoomed it, but nothing said so. While the
  laser is on the map, a small label above each thumbstick now names its job.
- **The stick and throttle hide the widgets behind them.** The cockpit map and
  the other widgets are drawn by a second camera that knows nothing about the
  cockpit, so a widget behind the virtual stick or throttle was painted over
  them. The gloves already had an answer to this (an invisible copy that
  writes depth for that camera); the solid parts of the stick, the throttle,
  its rail and marks now have the same, so a widget is cut out where a
  control is in front of it. The see-through shaft end and the captions hide
  nothing. HUD symbols that pass behind a control are cut the same way, as
  they already were behind a glove. Turn it off with **VR HUD → Stick And
  Throttle Hide Widgets Behind Them** in the BepInEx config.
- **Fixed: large red and blue shapes flickering in front of you while your
  radar is jammed.** When an enemy jams you, the game shakes every unit icon
  on the map by adding a random step of up to 2.5 to its world position each
  frame. On a flat screen that is a few pixels. In VR the map is a panel in
  the cockpit and the same number is metres, so the icons left the map and
  flew around the cockpit and through your view for as long as the jamming
  lasted; the more units on the map, the more of them. The shake now stays in
  the map's plane and in the map's own units, at most 1.5 cm a frame. This is
  the cause found in the game's code for the flicker reported during busy
  matches; it has not been seen in a headset, before or after.
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

Nothing in this version has been checked in a headset yet. That includes the outside view, the free camera, the map labels, the controller models and their labels, the walkaround teleport, snap turn and calling the menu, the controllers on the map, their 3D label lines and pressed-button highlights, the stick and throttle hiding widgets, the jammed map icon fix and the throttle ticks: their rules are covered by the source checks and they compile against the game, but how they fly, read, line up with your hands and feel is not yet known. The controller models were loaded in the running game on Linux/Proton without a headset: all eight Frame and Quest models were found in SteamVR's folders, built into controller-sized meshes with their textures and drawn by a game camera, and their thumbstick positions agree with the offsets the quick wheel already uses. Startup, patch installation and the no-headset retry were checked in the running game on Linux/Proton, and the scan costs were timed there at the main menu. The image options were exercised there too, against the real engine without a headset: the filtered screen and the map copy sample as correct mipmaps, the vignette code runs against the game's real flight profile, and dithering changes the final image. How any of them looks in a headset is not yet known. One package for Windows and Linux/Proton. Saved layouts, bindings and settings are preserved.
