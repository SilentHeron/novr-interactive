# Steam Frame development notes, October 2026

Historical record. These are not current installation or control instructions.
Use the [current PC VR guide](../STEAM_FRAME.md) and [controls](../CONTROLLER_BINDINGS.md).

---

# Steam Frame PC streaming prototype

Current controls: [controller bindings](../CONTROLLER_BINDINGS.md). Since 0.4.7.10, the added MFDs and shared console are removed; original aircraft displays remain in use. The older layout notes below describe previous versions.

NOVR Motion Prototype **0.4.6.9 candidate**, targeting Nuclear Option **0.34.3**, Steam build
26272685. Nuclear Option runs on the PC; SteamVR supplies OpenXR tracking and
controller input while the headset streams the game. This is an experimental build,
compiled and checked against a local game installation. A live Steam Frame session
now starts through SteamVR/OpenXR using DirectX 11 on the Linux/Proton test host.
Live logs confirm 67 single-player missions, controller clicks through mission
selection, the airbase map and Reserve/Fly, and entry into an aircraft. Motion
flight controls, HUD comfort and the other controller bindings still need hands-on
validation. The 0.4.6.9 cockpit-alert and menu changes have offline validation only;
see [Cockpit alerts](../COCKPIT_ALERTS.md). The last installed version on the test host
is 0.4.6.8; the candidate has not been launched or installed.

## Install and start

1. Close the game. Back up `BepInEx/plugins/NOVR` and `BepInEx/patchers/NOVR`.
2. With BepInEx 5 already installed, extract `NOVR-steam-frame-experimental.zip`
   into the game's **BepInEx directory**, replacing the matching files in both folders.
3. Use SteamVR as the PC's OpenXR runtime and connect Frame through your PC streaming
   setup. Establish the VR session first, then launch Nuclear Option. On the tested
   Linux/Proton host, use the normal DirectX 11 launch, not the Vulkan launch option.
   An explicit `-force-d3d11` launch succeeded; the Vulkan launch exited during
   OpenXR initialization.
4. In a cockpit, press the throttle-hand **View** button (the thin button at the top
   of the left controller in the default layout) to open the wrist panel. Choose
   **Edit cockpit layout** to enable the controls and move the stick, throttle/collective
   and panel independently. Release grip, then reach or point and grip the item;
   move your hand, and rotate your wrist to rotate the panel. Open View again to
   **Save layout** or **Cancel layout**.
5. Use **Seat up/down/left/right/forward/back** for 2 cm adjustments. Placements and
   seat offsets save automatically for the aircraft and handedness.

The keyboard fallbacks are F6 to toggle the console, F7 to toggle motion controls,
F8 to enable motion controls and place handles, and F9
to open the wrist panel. Release both handles first; holding both thumbstick clicks for 0.8 seconds also places
the handles. The console shows a placement countdown and confirmation.
Motion flight controls remain off initially; menu pointers and the console are on.
Restore both backed-up folders and restart to revert the experimental build.

The package must include the updated patcher payload, not just `NOVR.dll`: it contains
the OpenXR controller profiles. The existing upstream installer installs upstream NOVR,
not this development branch. This package does not include BepInEx itself.

## If the headset shows a flat screen inside a black border

A desktop stream is not a VR session. NOVR can load and create its world-space menu
while OpenXR initialization fails, leaving that menu visible only in the flat game
window. Check `Player.log` for `XR_ERROR_RUNTIME_UNAVAILABLE` and SteamVR for a
connected, tracked headset before changing HUD/menu size.

On Frame, use **Streaming Status → host PC → Advanced → Stream VR without game**
to establish the VR connection first, then launch Nuclear Option. If the host is
advertised as “ready for 2D streaming only,” resolve the host's VR streaming setup
first. Restart the game after establishing the connection; this build does not retry
failed OpenXR initialization automatically.

Linux host support remains experimental. On the local test PC, SteamVR beta and the
OpenXR runtime were already configured, but Steam was started without its
`-vrlinkforceenable` option. The host launcher was updated to retain `-pipewire`,
`PRESSURE_VESSEL_IMPORT_OPENXR_1_RUNTIMES=1`, and enable VR streaming. This is a host
configuration change, not a setting included in this ZIP. See Valve's
[platform support guidance](https://help.steampowered.com/en/faqs/view/2668-0B6E-79E0-A85B)
and the [Linux streaming issue report](https://github.com/ValveSoftware/SteamVR-for-Linux/issues/962).

Version **0.4.4.3** fixed repeat-start payload installation: identical runtime
DLLs are left untouched when Unity has already loaded them. MultiPass is now set
explicitly before OpenXR initialization, including on the first install. Neither
change replaces the need for an active VR streaming connection.

The test headset initially could not connect directly over local Wi-Fi while its
Tailscale connection was enabled. With Tailscale disabled on Frame, the direct Wi-Fi
connection and SteamVR session worked. The wireless adapter has not been independently
validated; successful home Wi-Fi streaming does not establish adapter health.

Version **0.4.4.4** fixes a menu pointer exception: Unity UI's graphic collection
supports indexed access but throws when enumerated. That exception interrupted the
controller update before the cursor/laser could be drawn. Frame detection also now
recognizes the runtime-generated layouts derived from Valve's controller profile.

Version **0.4.4.5** restores automatic mission tags before the VR menu filters
missions. Without this step, valid missions could all be hidden from Single Player
and multiplayer hosting. Both lists refresh when reopened to include missions added
later during startup. Mission header errors skip only the affected entry.

Version **0.4.4.6** uses OpenXR session focus for controller input instead of
requiring focus on the desktop mirror. Visible native VR menus also keep their
pointer active independently of the desktop cursor flag after loading transitions.
The game runs in the background while VR is enabled. Opening the runtime dashboard
still cancels VR input; the mouse fallback requires desktop focus. `Player.log` now
reports pointer state changes, tracking/focus information and controller UI presses.

Version **0.4.4.8** uses the active player-pilot state and local aircraft ownership
instead of the unused legacy `Pilot.playerControlled` field. Cockpit checks run
before querying scene menus, and the radial-menu query tolerates scene startup.
A pending handle placement survives temporary readiness/tracking loss for up to ten
seconds after its countdown, then logs a reason if it cannot complete. The wrist
panel and game log show motion-control readiness status.

Version **0.4.5.0** corrects inverted motion-stick pitch, matching the game's
negative-pitch/nose-up convention and its cockpit stick animation. Pulling back
commands nose-up; pushing forward commands nose-down. Roll is unchanged. The
wrist Flight page now includes an ejection action with a separate confirmation.
The shared 3D console adds physical controls, live weapon/status displays and the
minimap. It also adds readable countermeasure selection, minimap zoom keys, live
placement adjustments and a weapon SAFE indicator. An offline 22-check exercise
covers console actions, HUD restoration and the ejection camera handoff; physical
controller reach and stereo comfort still need headset testing. See [the universal cockpit console guide](../UNIVERSAL_COCKPIT_PANEL.md).

Version **0.4.5.1** adds optional tap-to-latch flight grips, separately for stick
and throttle. Hold-to-grip remains the default. Open **View → Flight controls**
for grip mode, sensitivity, deadzone, throttle travel and yaw settings. **Controls
help** shows the bindings currently in use, including Frame/Touch fallback and
handedness. Missing tracking and failed handle placement have visible feedback.

Historical layout (superseded by the current VTOL-style preset above): version **0.4.5.2** added wheel/air-brake input and a throttle-layout page:
**View → Flight controls → Button bindings**. Braking uses D-pad down on native
Frame, or the throttle-hand thumbstick click on Touch fallback. Hold the throttle
(or latch it) while using that button. The optional **Trigger: brakes** layout
moves the four throttle actions together to keep their buttons distinct:

| Throttle action | Native Frame, trigger-brake layout | Touch fallback, trigger-brake layout |
| --- | --- | --- |
| Brake, hold | Left trigger | Left trigger |
| Countermeasures, hold | Left shoulder | Left X |
| Select target, release | Left D-pad down | Left Y |
| Next countermeasure, release | Left D-pad right | Left thumbstick click |

**Trigger: CMs** restores the default table below. Custom bindings survive an
upgrade; an added brake binding starts unassigned if its default button is already
used by a custom throttle action. The wrist page shows the active bindings and warns
about shared buttons. Neither layout changes the flight stick, map or wrist controls.

Version **0.4.5.3** makes the stick/throttle solid and shaded, moves their captions
clear of the handles, and adds readable backplates. Captions now respect cockpit
occlusion. Throttle markings follow the
selected travel distance. The throttle readout shows **BRAKES ON** in amber while
the game's brake input is applied, then **BRAKES RELEASED**. It also shows native
keyboard/HOTAS braking while motion models are visible. These are command indicators;
the flight controls still use the same bindings and grip modes.

Version **0.4.5.4** restores the game’s normal target-camera zoom, zoom-out after
losing a target, and wide landing-camera view. The instrument magnification now
uses the same field of view as the rendered camera. This does not change headset
zoom or add a new control binding.

## Shared 3D console

The console appears automatically in the cockpit. Its layout and placement are
shared by every aircraft. Release a flight-control grip, then use the laser and
trigger, or reach to a nearby control and use trigger or grip. The gear lever supports grab-and-move;
ejection requires lifting the red guard and a separate deliberate pull/hold.
Weapon/countermeasure status and the minimap move to its displays.

Use **View → Cockpit setup → Console position** to adjust its position, tilt and
size. **F6** shows/hides it and restores the previous HUD layout when hidden.
Version 0.4.5.5 keeps the other free hand’s pointer visible when one hand is near
the console. Nearby controls take priority for their own hand; hints follow the
control being used, including eject progress. Release a held control before
operating another. Version 0.4.5.6 keeps direct gear/eject travel consistent at
all console sizes. Ejection feedback distinguishes a request from the aircraft’s
confirmation. If it is not accepted, release, reopen the guard and pull again;
there is no automatic retry. Console interactions and display fitting still
require in-headset validation.

Version 0.4.5.7 adds **HOV** below the CM key. Its lamp follows native hover assist.
The same action is available from **View → Flight actions → Hover assist**. It
requires a compatible aircraft and at least 1 m ground clearance. During hover,
**HOVER INPUT** shows the pilot’s command rather than automatic engine power;
letting go and regrabbing preserves that command. The normal caption is
**COLLECTIVE** or **THROTTLE**, depending on the aircraft.

Version 0.4.5.8 gives helicopters such as the SAH-46 a hinged collective: **grip
and lift to increase, lower to decrease**. Jets and the VT-7 retain forward/back
throttle motion. **View → Flight controls** shows the appropriate travel direction
and adjustment labels. Both layouts use the existing throttle-hand buttons and
saved per-aircraft/handedness positions. Use **Place handles** again if the new
lift range needs more clearance around your chair.

Version 0.4.5.9 reduces handle captions by 35% and moves them nearer the pilot
to avoid sidewall and console overlap at nominal placement. The collective
caption stays above the lift range. Saved seat and handle placements are retained.

## Default bindings

The VTOL-style preset uses the following right-handed Frame layout.
Combat/flight buttons work only with the corresponding handle held or latched.
Twist the stick for rudder in flight and native wheel steering on the ground;
30° twist gives full input. Horizontal thumbstick yaw remains optional. Turn both
yaw options off under View → Flight controls when using pedals.

| Action | Frame | Touch fallback |
| --- | --- | --- |
| Guns only | Right trigger | Right trigger |
| Selected non-gun weapon release | Right A | Right A |
| Next weapon, stick held | Right Y | Right B |
| Wheel/air brake | Left trigger | Left trigger |
| Deploy countermeasures | Left D-pad down | Left X |
| Select target at head-directed cue, stick held | Right thumbstick click | Right thumbstick click |
| Unselect latest target, stick held | Right B | Wrist Flight actions |
| Clear all targets | Wrist Flight actions / MFD CLEAR | Wrist Flight actions / MFD CLEAR |
| Next CM type | Left D-pad right | Left Y |
| Inspection magnifier, grip optional | Physical right bumper, hold | Shoulder button if fitted |
| Map, stick released | Right B | Right B |
| Wrist panel / recovery map after crash | Left View | Left menu |

After a crash, B can open the aircraft-selection map even if a flight grip was
latched. View opens that map outside a usable cockpit; F9 is the tracked-controller
fallback and keyboard M remains the native map shortcut.

Weapon/target/CM-type changes and unlocks act on release; fire, brake and CM
deployment act while held. Grip release, menus and tracking loss cancel input.
Selection requests the game's normal target selection; weapon lock still depends
on the weapon and its native restrictions. Unlock removes the newest target first.

Right bumper remains physical right when handedness is mirrored. It displays a
mono magnified forward image at a fixed 3 m depth, while preserving the normal
stereo view outside the window. It does not change XR projection or the sensor FOV.
The earlier whole-view zoom was removed after uncomfortable headset convergence.
Release hides the image. Its extra camera runs only while held. Inspection zoom
quality is separate from the headset: default 1280×800 with 2× MSAA and scene
effects off. Change it under View → Comfort & display → HUD & display → Inspection
zoom, or Settings → Graphics. Four resolution levels span 1024×640 to 2048×1280;
MSAA can be Off/2×/4×/8×. Main Anti-Aliasing must be enabled for zoom MSAA, and
unsupported sample counts fall back automatically. Higher zoom quality costs
additional GPU time only while held.

Use **View → Flight controls → Button bindings → VTOL-style controls** to reapply
the preset. Original stock bindings migrate once; other custom bindings survive.
Earlier throttle presets remain available. Right bumper is reserved for inspection
while enabled, so custom actions assigned to that button are suppressed.

## Wrist panel and cockpit profiles

The panel follows the throttle hand and faces the headset. It holds still during a
trigger press. **Flight actions** and **Cockpit setup** switch between pages;
subpages have an explicit Back button. The wrist panel provides:

- Motion enable, delayed handle placement, view recentering, handedness and seat adjustment.
- Gear, radar, engine, flight assist, weapon/countermeasure cycling, target clearing and map access.
- HUD size, depth, backplate contrast, declutter and reset controls.

Flight actions close the panel and enter the game's existing input path on the next
frame. The game retains its G-LOC, weapon safety, gear/radar altitude restrictions and
multiplayer command handling. For landing gear, choose **View → Flight actions →
Toggle gear**. For ejection, choose **Eject...**, then **Confirm eject**. **Cancel**
returns to the flight page; closing the panel, losing tracking/focus, or changing
aircraft clears the confirmation. Ejection uses the game's normal input handling.
The native radial quick wheel does not yet have a motion-controller binding;
the wrist panel provides the listed actions instead.

Profiles use the aircraft definition's stable key, with separate left/right-handed
layouts. The config section's hash avoids invalid configuration characters; its
comments identify the aircraft. Default positions in `[Motion Controls]` seed new
profiles. Reset aircraft setup returns that profile's seat to zero and handles to the
configured defaults. Recenter adjusts tracking; it does not overwrite saved profiles.

## HUD readability

`[VR HUD]` includes:

| Setting | Default | Range / effect |
| --- | --- | --- |
| Status Panel Scale | 1.15 | 0.8–1.6; status panels only |
| Status Panel Distance | 4 m | 2–10 m; preserves angular size while changing stereo depth |
| Status Panel Backplates | true | Dark backgrounds over bright terrain |
| Declutter Status Panels | false | Fades the secondary lower-left panel to 15% |
| Head Follow Response | 10 | Existing head-follow smoothing; 0 follows immediately |

World target markers, boresight and flight-path symbology are excluded from status
panel scaling/depth adjustments. Weapon and countermeasure panels remain visible
when decluttering. The new backplates and panel spacing still need visual evaluation
in each cockpit and both eyes.

## Native Frame profile and fallback

The fork registers Valve's
[`XR_VALVE_frame_controller_interaction` profile](https://github.com/ValveSoftware/Unity/blob/7e842352310fc2cb458ac6b127ce1afc233173a1/com.valvesoftware.openxr.utils/Documentation~/index.md)
before initializing OpenXR. The profile exposes the additional shoulder/face buttons
and the native aim pose. If the runtime does not enable that extension, the profile is
skipped and the existing Touch-compatible profile remains available. Both controllers are now detected with native Steam Frame bindings in the live
SteamVR streaming session; physical button behavior still needs hands-on validation.
The BepInEx log reports each controller and whether NOVR selected native Steam Frame
bindings or the generic fallback. See [third-party notices](../THIRD_PARTY_NOTICES.md) for the Valve source and license.

## Verification and live test pass

Automated checks cover grip/trigger transitions, tracking recovery, input curves,
throttle handoff, pointer surface geometry, HUD smoothing and map zoom rates. An
optional metadata check verifies Harmony methods, explicit overloads and injected
fields against the locally installed game and Rewired assemblies. It does not run
Unity, load OpenXR hardware, or prove the patches execute correctly in game.

Before using this as a regular flight setup, check a local mission:

1. Verify the default binding table with native Frame input and, if available, Touch
   emulation. Test left-handed mode and the View/menu fallback.
2. Click a main-menu button, scroll, use a dropdown and drag a scrollbar. Hold trigger,
   open the runtime dashboard, then return: no click or weapon fire should occur.
3. Select map icons, pan and zoom while pointing at the map. Zoom should retain the
   location under the laser. Confirm depth alignment in both eyes.
4. Fire, cycle weapons, select/clear targets and deploy/cycle countermeasures. Check
   wrist gear/radar/engine/assist actions and native game restrictions.
5. Save different seat/handle placements in two aircraft; change aircraft and restart
   the game. Test recentering, pause, ejection and controller disconnection.
6. Check status panel depth, contrast and clutter settings against bright terrain.
   Aim/world markers must retain alignment during head motion and aircraft maneuvers.

Right-click map orders/waypoint construction and text entry still use the existing
input methods. Aircraft-specific collective/auto-hover, multiplayer, streaming latency,
render performance and physical Steam Frame behavior remain unverified. Streaming
quality, eye tracking and foveation are controlled by the runtime and are not configured
by this change.

The 0.4.6.1 update also fixes recentered controller-position mapping. Recenter from
View while seated facing forward, then check the hand markers against your controllers.
The update preserves saved cockpit placements. The stock VTOL target binding moves
from the throttle click to the stick click; custom throttle layouts are preserved.

### New cockpit controls in 0.4.6.1

With the throttle gripped, left thumbstick forward/back commands VTOL angle when
fitted. Push through the MIL shelf for afterburner on equipped jets. Press **L STK**
on either MFD to route the physical left thumbstick to its Map page instead; click
that stick to recenter the map and press **SLAVED** to return to flight controls.
The right stick click still selects a target while gripping the flight stick.

The console now fits the aircraft dashboard by default. Seat adjustments leave
all three controls fixed. Use **Edit cockpit layout** for individual placement or
**Console setup → Anchor to dashboard** to restore its automatic position.
Brief haptic cues accompany physical interactions. Tune `Interaction Haptic
Strength` in the NOVR configuration if needed.

## Pilot hands, helmet gesture and interaction feedback — 0.4.6.1

Rigged gloves use Valve's BSD-licensed SteamVR slim meshes and reference poses.
They keep proper UV seams and normal details, with a tint sampled from the installed
pilot. The pilot's own sleeve geometry/materials remain local. A private texture
copy (up to 2048 square) adds mild sharpening and subtle fabric detail once on
aircraft entry; the original atlas is untouched. Native pilot animations and other players are untouched.
Grip curls three fingers and thumb; trigger curls index. This is controller-driven
posing, not optical finger tracking. The two gloves total 9,798 triangles.

Sleeves default to 82% of native thickness with unchanged lengths and wrist targets.
View → Comfort & display → Hands & arms includes Slimmer/Wider
sleeves, Gloves/Sleeves toggles, wrist-angle and hand-offset adjustments. Sleeves hide beyond their reachable envelope. Config
`Glove Wrist Offset` / `Glove Wrist Rotation` adjust visuals only; interaction points
continue using the runtime's grip pose. These values need a headset fit check.

Bring either free hand to the brow, then make a fresh trigger press to toggle native
night vision. A small cue identifies the gesture area. Held-trigger arrival and
tracking recovery cannot toggle it; menus, map, layout and occupied hands suppress it.

`Interaction Sound Volume` (default 0.25) controls original spatial grip/release,
button, detent, guard and helmet cues. View → Comfort & display → Sound & haptics has independent volume and strength buttons.
Zero mutes sound independently of `Interaction Haptic Strength`. Six pooled voices
and seven pre-generated clips bound the audio cost; the aircraft mixer is reused
where available.

Runtime controller reads are shared per hand per frame, with immediate invalidation
on calibration/seat changes. Panel state and MFD labels only refresh when changed.
Pilot geometry reads and glove setup happen on aircraft entry, not every frame.
No global game/SteamVR quality or streaming settings are changed. See NOVR-validation-0.4.6.1.txt
for final checks; no headset FPS or stereo-comfort claim is made.

## Final comfort adjustments

- Either hand can operate menu and cockpit-panel pointers. Aim at a control and
  make a fresh trigger press to take the pointer. Its owning hand retains a drag
  through release; switching hands, entering a menu or recovering tracking cannot
  turn a held trigger into a new click. Direct panel touch still works with either
  free hand. Release a latched flight handle before using that hand on the panel.
- Default throttle/collective travel is now 12 cm (previously 25 cm). Existing
  untouched 22/25 cm defaults migrate once; custom travel is preserved. Flight controls
  has Shorter/Longer travel buttons with 1 cm steps. The MIL shelf is included in the total.
- Free hands default to palms vertical, thumbs up. **Hands & arms → Hand alignment**
  has upright/down presets, wrist tilt and palm roll in 5-degree steps, and visual
  wrist offsets in 5 mm steps. Former stock zero/25-degree wrist settings migrate
  once; custom angles are retained. Held hands instead use fixed handle sockets.
  Controller hit positions and flight inputs remain unchanged.
- VT-7 dashboard fitting starts 25% larger, then checks surrounding clearance.
  Existing manually placed VT-7 panels enlarge once (within the scale limit) and
  move slightly toward the pilot to allow for the thicker scaled panel. Other
  aircraft scales are unchanged. Edit layout and Console setup remain available.
- The diagnostic input helper has been removed. The final game restart loads only
  the production mod; no automatic aircraft switches or controller tests run.

## 0.4.6.2 — steadier menus and direct MFD selection

The View menu opens about 72 cm in front of you and stays there while you point.
Either controller can operate it. Losing the usual menu hand does not close it
while the other hand can still point. Close and reopen View to center it again.
If you prefer the older hand-following behavior, use **View → Comfort & display → Menu: STEADY / FOLLOW HAND**. The adjacent **Bring menu
forward** button places the menu again using the selected mode.

The opaque console hides while View is open so it cannot cover menu buttons.
**Console setup** retains an outline preview for position, tilt and scale changes;
close View to inspect the actual displays. Layout editing keeps its existing
transparent preview and Save/Cancel behavior. The menu laser uses the game's
available particle shader and a finer tip.

Each page has a clear title, motion/backplate/declutter toggles show their state,
and **Controls help** separates stick-hand and throttle-hand bindings into two
columns. **View → Comfort & display → Hands & arms** holds hand and sleeve settings.
Resetting the seat and handles requires **Reset seat & handles… → Reset seat &
handles**; **Keep my setup** returns without changing saved placement. This reset
applies only to the current aircraft and handedness; panel placement is retained.

Touch or point at the **PAGES** title above either MFD, then trigger/grip a page.
The picker highlights the current page and marks unavailable aircraft instruments
**NOT FITTED**. Selecting the other MFD's existing page swaps the two native
widgets rather than duplicating them. **BACK**, or pressing the title again,
closes the picker without changing the page. Only one picker opens at a time.
The existing previous/next bezel buttons remain available.

Page choices use the rectangles actually drawn on the display for direct-touch
and ray hit testing. The ray dot follows the actual hit on the button. Existing
fresh-press gates apply: holding a trigger while entering a menu, moving between
choices or recovering tracking cannot activate another choice. Opening View,
hiding the console, leaving the cockpit or entering layout editing closes pickers.

Validation for this pass: **93 automated checks**, **46 resolved patch targets**,
and **23 live scripted usability checks** on a stationary VT-7. Rendered previews
were inspected for clipping, occlusion, label alignment and pointer color. The
scripted checks supplied Steam Frame-style inputs/focus without a wearer; they do
not prove headset comfort or streamed performance. Test inputs and their temporary
plugin were removed after the run, and original user settings were restored.

## 0.4.6.3 — compact travel, attached hands and organized View menu

Throttle and helicopter collective now default to **12 cm end to end**. The 1.6 cm
MIL shelf is reserved *inside* that distance on afterburning jets. Release/regrab
preserves the command. Change travel in **View → Flight controls**, in 1 cm steps.
Reset response restores 12 cm. Untouched former 22/25 cm defaults migrate once.

Free hands use a handshake pose: palms vertical, thumbs upward. **View → Comfort &
display → Hands & arms → Hand alignment** provides upright/down presets, mirrored
palm roll, wrist tilt and visual offsets. These settings affect free-hand visuals.

Holding or latching a flight handle attaches the existing skinned glove to that
handle's moving grip socket. Partial finger curls leave room for the handle; the
index still reacts to the trigger. Stick, sliding throttle and collective use
separate wrist orientations. The collective cuff points upward and inward toward
the shoulder. Sleeve IK ends at the attached wrist. Release, menu
entry, tracking loss and control disable use the existing grip-reset rules; no
controller pose or flight input is replaced by the visual attachment. Generic
sockets do not guarantee a perfect finger fit on every workshop handle model.

View now opens Home. Back returns to the actual previous page, Home returns to the
root, and Close stays in the same footer position everywhere. Pages are grouped:

- **Flight actions**: weapons, targets, gear, radar, engine, map, assists and eject.
- **Cockpit setup**: seat, console, individual layout editing, handle placement and handedness.
- **Flight controls**: grip modes, response, travel, yaw, MIL detent and button bindings.
- **Comfort & display**: hands/arms, hand alignment, HUD, sounds/haptics and menu following.
- **Controller guide**: current bindings by hand. Recenter view is also on Home.

Layout Save/Cancel is available on Home after reopening View. Reset seat/handles
and menu ejection retain their confirmation pages; leaving them clears the pending
confirmation. No simulated-input helper is included or running. This release's
hand geometry was inspected offline on representative grips; wearer alignment and
native aircraft fit still need headset testing.

## 0.4.6.4 — display brightness and clearer targeting feedback

Open **View → Comfort & display → HUD & display → Brightness**. MFD content and
HUD flight instruments adjust independently from 35–100%, in 10-point steps.
Day restores both to 100%; Night sets MFD content to 45% and HUD instruments to
50%. New settings default to 100%, preserving the existing appearance until changed.
HUD dimming covers flight instruments, compass and pitch ladder. World target
markers, the flight-path marker and forward missile alerts retain native brightness.
MFD page headers/choices, the Weapons readout, its SAFE label, and physical console
warning lamps are outside the content dimmer. Native systems-page instruments
and their built-in warnings dim with that page. Close View to inspect the result.

The Weapons page adds a compact **N SELECTED · STATUS** line. The count comes from
the game's selected-target list. Status prioritizes NO WEAPON, SAFETY ON, EMPTY,
RELOADING and NOT READY. Otherwise it mirrors a currently visible missile/laser
HUD cue such as OUT OF RANGE, NOT LASED or SHOOT, or shows LOADED. LOADED describes
ammunition/reload availability only. Selection, LOADED and native SHOOT are not
proof of seeker lock or guaranteed launch success; normal game restrictions remain.
Hidden or mismatched weapon cues are ignored. Readout refresh is limited to 10 Hz.

When neither hand is pressing, the menu pointer prefers a usable button, scroll
control or expanded map over the other hand's blank background. It stays with its
current hand when priorities tie. Held clicks/drags keep ownership through release;
a trigger already held on entry cannot create a new click when priority changes.

The engine's independent UI opacity multipliers preserve native per-widget colors
and fades; selected controls opt out of the parent dimmer. This follows the
[Unity Canvas Group documentation](https://docs.unity3d.com/2022.3/Documentation/Manual/class-CanvasGroup.html).
Brightness perception and third-party instrument behavior still need headset checks.

## 0.4.6.5 — collective arm alignment and deferred sleeve detail

The held collective glove keeps its cuff pointing upward and inward toward the
shoulder. The sleeve now chooses its elbow bend from that cuff direction too,
instead of using the downward bend of a freely tracked hand. The wrist remains
attached to the moving handle, arm lengths stay fixed, and sleeves still hide when
the hand is outside their reachable envelope. This is a visual change; collective
input, saved placements and the 12 cm default travel are unchanged.

Hands initially use the original pilot sleeve atlas. Sharpening and the same
restrained fabric pattern are prepared on one background CPU worker, then applied
to the copied sleeve materials when ready. Resolution remains capped at 2048 square.
Both sleeves share one result for each source atlas. Leaving an aircraft cancels
its work; late results cannot be applied to another cockpit. A failed enhancement
keeps the original texture visible. No original game asset is changed or exported.

Capture, filtering and upload occur in separate stages. Only managed pixel data
crosses to the worker; Unity texture operations remain on its main thread. The
intermediate readback texture no longer uploads pixels that will never be rendered.
This follows Unity's [ReadPixels guidance](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Texture2D.ReadPixels.html).
Capture, texture decoding and the final upload can still cause a pause; in-game
frame-time improvement has not been measured for this release. The log separates
capture, background filter and upload timings for a future normal cockpit entry.

Validation: 106 automated checks pass, including texture color/alpha preservation,
fabric repetition, cancellation, serialized background jobs, stale-result rejection
and failure recovery. An offline collective study checks mirrored low/mid/high
poses using representative arm lengths. It is not a native pilot or headset fit
check. No simulated input, mission changes or flight actions are needed for these
checks.

## 0.4.6.6 — dashboard replacement, aiming HUD and grip fit

The console fits the nearby coplanar dashboard skin as well as the native screen.
Its housing and MFD apertures can widen independently of height; lettering and
physical switches retain their proportions. Automatic placement checks the wider
footprint against cockpit geometry. Seat position and flight-control anchors are
unchanged. Existing manual placements are preserved: use **View → Cockpit setup →
Console position → Anchor to dashboard** to apply the new fit to a saved manual layout.
The bounded automatic fit is not a handcrafted fascia for every aircraft; tight
or unusual dashboards still need manual adjustment.

While the VR console is visible, native tactical/sensor screen surfaces stop
rendering. For a combined cockpit mesh, only matching display material submeshes
are removed from a temporary render copy; surrounding cockpit geometry remains.
When the console is hidden, edited, disabled or left behind, native presentation
restores. No native aircraft, instrument logic, camera sensor state or assets are
destroyed. No game meshes are exported or included in the package.

If neither MFD shows Tactical, the native tactical display camera and canvas are
suspended, its display animation update is skipped, and the native MFD manager
refreshes only visible docked instruments. Tactical resumes its shared display
feed; Sensor retains the game's active target/landing camera. Radar/optical scan
events and sensor cameras remain available. This reduces redundant rendering and
instrument work; no measured Steam Frame FPS gain is claimed.

**View → Comfort & display → HUD & display → All HUD smaller/larger** now adjusts
flight instruments, target/location symbols, labels, reticles and range readouts.
**Aiming & markers** offers separate controls. Defaults are symbols 45%, labels
55%, reticles 45%, weapon readouts 50%. Aiming points keep their world alignment;
lead and bombing lines retain their lengths while their thickness changes. Range
and guidance widgets are compact readouts; weapon limits and lock logic do not
change. Missile warning brightness remains independent.

The right-bumper magnifier has no labelled border. Native targeting graphics are
copied as render-only meshes, clipped to the image: world anchors follow the zoom,
glyph size stays constant, and weapon readouts occupy a compact right-hand slot.
The image remains mono on a 3 m stereo surface, with unchanged XR eye projection.
There is still an extra scene camera while held, plus HUD mesh-copy work.

Native stick grip centers sit farther below the button head. Sliding throttle
palms rest on the upper grip; collective grips keep their cylindrical center.
The glove socket moved from 78 mm to 43 mm along the fingers, toward the space
between palm and curled fingers. This reduces penetration in the offline
representative-handle study; exact native mesh contact and comfort still need a
wearer check. Held hands remain attached; free-hand calibration is unchanged.

Validation: 111 automated checks, 48 declared Harmony targets and seven private
field contracts pass against the installed assemblies. The new checks exercise
anchor preservation, zoom glyph sizing, world-line projection, readout fitting
and proportional panel widening. Seventeen menu pages and 115 page buttons pass
a static layout audit. No simulated game input or flight-action tests were run.
The prior 32-aircraft clearance audit predates this larger fit and does not
validate the new dashboard footprints. In-headset rendering, restoration,
clipping, hand comfort and streaming performance remain to be tested.

## Proton mission-menu freeze workaround (0.4.6.8)

A local frozen-session capture showed the game main thread waiting inside Wine's
`XInputGetState`, on its controller-list critical section, after the mission menu
loaded. NOVR now disables Rewired's optional legacy XInput backend when running
under Wine/Proton, using Rewired's initialization event and supported configuration
setter. This targets the observed wait; it is not a general cure for all freezes.

OpenXR motion-controller polling and haptics remain separate. The workaround does
not change Steam Input, graphics settings, saved game bindings or other mods.
Native Windows is unaffected. Other Rewired input backends remain available, but
Xbox-style gamepads may require alternate bindings. Set **Compatibility → Disable
Legacy XInput on Proton** to false in NOVR's config and restart to opt out.

Version 0.4.6.8 retains the [EOTS HUD and contextual aiming cue](../OPTICAL_HUD.md).
PC dynamic foveated rendering is still not active; see [capability limits](../PC_FOVEATION.md).
The build and 118 automated checks pass, including the installed Rewired API
contract. Actual mission-menu recovery and controller behavior require a user test.


Version 0.4.7.2 physical FS-20 controls, handle poses and setup buttons are described
in [FS20_NATIVE_COCKPIT.md](../FS20_NATIVE_COCKPIT.md). Capacitive A touch rests the
thumb on the red release button; A press depresses it. Trigger still operates guns.
