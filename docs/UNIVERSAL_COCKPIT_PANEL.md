# Universal cockpit console

From NOVR 0.4.7.0, the FS-20 Vortex uses its own [integrated native cockpit](FS20_NATIVE_COCKPIT.md). It never creates the movable console.

Other aircraft use the shared 3D console model introduced in 0.4.6.2. Its default
position, tilt and size fit the native dashboard surface in physical cockpit meters.
A one-time interior geometry check adds clearance where needed; broad dashboards
choose the area in front of the pilot. Imported cockpit scale does not alter placement.
Seat adjustment moves the view and tracked hands, leaving the stick, throttle and
console fixed. Saved panel placement is per aircraft, shared across handedness.

The stick, throttle and attached hands render after the movable console, so the
console cannot cover them. This foreground pass only runs while the shared console
is visible; the FS-20 uses normal cockpit depth.

## Displays

Each display has **< / >** keys below it. Point and trigger, or touch with trigger
or grip, to cycle pages. The seven choices are **Weapons/CM, Map, Flight,
Engine/Fuel, Systems, Tactical, Sensor**. Left defaults to Weapons/CM; right defaults
to Map. If the requested page is already on the other screen, the screens swap.

Weapons/CM and Map use the game's original live UI objects. Flight, Engine/Fuel
and Systems use the instrument widgets fitted to the current aircraft. Tactical
shows its existing radar/optical display; Sensor shows its active target or landing
camera. Tactical mirrors the native shared display and follows it into camera
mode when the game replaces its radar image; Sensor provides the full camera
feed. Unsupported instrument pages and inactive camera feeds show explicit
messages. Selecting Sensor does not activate or change the aircraft camera.

The original UI transforms, activity and layers restore when the console hides,
when leaving the cockpit, and before opening the full map. Weapon/map widgets stay
out of the forward view while another MFD page is selected. **SAFE** reflects the
native weapon-safety state. Target markers, flight-path cues and missile alerts
retain their normal projection. The green flight instruments now use a compact
forward layout; View provides **All HUD smaller / larger** and separate **Aiming & markers** controls.

## Controls

Release the flight-control grip on the hand you want to use. The default right
hand can aim a laser at the console and use its trigger. Either free hand can
interact directly by coming within 5.5 cm of a control and pressing **trigger or grip**.
Use grip to hold the motion flight stick and throttle.

Version 0.4.5.5 gives a nearby console control priority for that hand. The other
free hand can keep pointing, and both hovered controls highlight. Once one hand
holds a control, it owns console input until released. Release the buttons before
moving to another control; arriving with a held press does not activate it.
If squeezing trigger and grip together, release both before a new grab.

Hints describe the current interaction: pinch/grab instructions when close,
trigger instructions when pointing, and confirmation after an eject pull.
These hints take priority over entry messages and tracking notices for the other
hand. Result messages such as **GEAR LOCKED ON GROUND** remain visible for the
control that produced them. Point at the front of the console to use its laser controls.

| Control | Laser | Direct hand |
| --- | --- | --- |
| Gear lever | Trigger toggles gear | Hold trigger or grip on the wheel-shaped handle, then move up/down at least 3 mm |
| Radar, engine, flight assist | Trigger toggles | Trigger or grip near the toggle |
| Next WPN | Trigger cycles weapon | Trigger or grip near the key |
| HOV bezel key | Trigger toggles native hover assist | Trigger or grip near the key |
| CM bezel key | Trigger cycles countermeasure type | Trigger or grip near the key |
| + / − navigation keys | Trigger changes minimap zoom, centered on the aircraft | Trigger or grip near the key |
| < / > under each MFD | Previous/next page | Trigger or grip near the key |
| MAP | Trigger expands tactical map | Trigger or grip near the key |
| Red eject guard | Trigger lifts/closes cover | Trigger or grip near the cover |
| Yellow eject handle | After opening the cover and releasing trigger, press trigger on the handle | After opening the cover and releasing your press, hold trigger or grip on the handle and pull it toward you at least 4.5 cm |

Gear, radar, engine and flight-assist switches have a 650 ms cooldown between
requests. A press during the cooldown is ignored; release and press again to
retry. Holding cannot queue a delayed change. Gear requires only a 3 mm flick,
and an initial movement toward its existing state can be reversed within the grab.

Direct gear and eject travel are measured in physical distance,
independent of console size or tilt. Pull the handle out toward you, perpendicular
to the panel face; sideways motion and pushing away do not count as an eject pull.

There is no added hold timer. The game still performs its native canopy/seat sequence.

A completed pull shows **EJECT REQUESTED** while the game processes it, then
**EJECTION STARTED** only after the aircraft confirms it. The console reserves
input during that request so another hand cannot replace it with a different
action. If the game does not accept it, the guard closes and the hint briefly
shows **EJECT NOT CONFIRMED**. Release, reopen the guard and make another deliberate
pull to retry. It never retries automatically.

Ejection requires a fresh press after opening the cover. Leaving the handle during
a direct pull cancels it. Pausing, menus, tracking/focus loss, recentering and
aircraft changes reset the guard. Aircraft without switchable ordinary flight assist show an explicit unavailable
hint and a centered ASSIST lever; the wrist action is disabled with an explanation.
The game still applies its own flight-input,
G-LOC, gear and radar restrictions. Gear lights show down/locked in green,
transition in amber, and otherwise go dark; they represent the game's aggregate
gear state, not separate sensors for three wheels. The switch positions follow
actual aircraft state. A radar action does nothing if that aircraft has no radar.

The native radial wheel still has no dedicated motion-controller binding. The
console and wrist Flight page supply the supported actions, including ejection.

## Left thumbstick on the MFD

Press **L STK** between the page arrows on either display. It brings **Map** onto
that MFD and marks it **SLAVED**. The physical left thumbstick pans the map;
click it to recenter. Press **SLAVED** again to return the stick to flight controls.
Changing away from Map, hiding the console or leaving the cockpit releases it.
The +/− buttons change map zoom. This first interactive page is Map; the native
Tactical and Sensor feeds remain display-only.

MFD ownership suppresses the same thumbstick's VTOL adjustment and, with a
left-handed stick, thumbstick yaw/target click. Stick-twist rudder remains available.
No map input is applied while the wrist menu, layout editor or tracking is unavailable.

## Haptic feedback

Short pulses confirm grabs, releases, panel/menu presses, lever activation,
throttle end stops and the military-power shelf. Afterburner entry uses a stronger
pulse. `Motion Controls / Interaction Haptic Strength` scales these cues; 0 turns
them off. Hardware support and perceived strength need headset testing.

## Hover assist

Version 0.4.5.7 adds a **HOV** key below CM, with a green lamp that follows the
actual native hover-assist state. It is separate from the ordinary ASSIST toggle.
Point and press trigger, or reach and press trigger/grip. You can also use
**View → Flight actions → Hover assist**. The wrist button shows the current
state, or explains **not fitted** / **ground locked** when unavailable.

Hover assist must be fitted to the aircraft and requires at least 1 m of ground
clearance. The game still applies its native input restrictions. Hover behavior,
automatic thrust-vector changes and restoration of ordinary flight assist remain
under the game’s control.

While hover assist is active, the motion handle reads **HOVER INPUT**. Its position
and percentage follow the pilot’s climb/descent command, independently of the engine power
chosen by the hover controller. Moving it forward increases the command; releasing
and regrabbing preserves that command. The ordinary readout is **COLLECTIVE** on
aircraft using collective input, or **THROTTLE** otherwise. Helicopters use lift/lower motion; jets and the VT-7 use forward/back motion.

## Position and fallback

Use **View → Edit cockpit layout** to move each handle and the console independently
with grip. Rotate your wrist to rotate the panel. It becomes an outline while editing,
so it cannot obscure handles. Reopen View to **Save layout** or **Cancel layout**.
Panel position/rotation/size save per aircraft; handles save per aircraft and handedness.
Moving only a handle leaves the panel automatically anchored.


Open **View → Cockpit setup → Console position** to adjust position in 2 cm steps,
tilt in 5-degree steps, or size in 5% steps. Adjustments preview live on the console;
its flight controls are inactive until the wrist menu closes.
**Anchor to dashboard** clears the saved panel override and restores automatic fitting. **F6** shows/hides the console;
hiding it restores the previous forward status UI and minimap. The console is
independent of F7's motion-stick/throttle enable setting.

Free-placement fallback before automatic fitting: 77 cm wide, 36 cm tall, centered 32 cm below and 65 cm forward of the
recentered seat, tilted upward 20 degrees. Settings live in
`[Universal Cockpit Panel]` in the NOVR configuration.

## Model and implementation

The original model is authored by `tools/build_cockpit_panel.py` in Blender. It
exports GLB, a Blender source file, a preview, and the mesh data embedded in NOVR.
The game needs neither Blender nor a runtime GLB importer. Geometry batches by
material and animated group; it adds no physics colliders or extra map-rendering
camera. The panel uses NOVR's projected UI layer, so it stays visible over the
underlying aircraft dashboard; it is not a replacement for the aircraft model.

The design takes general inspiration from the grouped cockpit controls in the
[USAF F-16A/B flight manual](https://www.aahs-online.org/resources/e-library/fm/USAF-F16A_B-Flight-Manual.pdf)
and the striped emergency handles described in
[NASA's F-111 cockpit reference](https://www.nasa.gov/image-article/f-111-advanced-fighter-technology-integration-2/).
It is an original, simplified universal layout, not a replica of either aircraft.
No Nuclear Option or military reference model/texture is included in the panel. The separately attributed Valve gloves are bundled under their own license. Direct trigger interaction and
contextual hints follow the approach described in the
[VTOL VR manual](https://cdn.steamstatic.com/steam/apps/667970/manuals/VTOL_VR_Manual.pdf?t=1649333292).

## Validation status

76 automated checks pass, including pitch polarity, diagonal stick input,
guarded-ejection transitions, embedded mesh integrity and 45 Harmony target
metadata checks against Nuclear Option 0.34.3. The game builds with no errors.
22 live checks in a disposable offline FS-12 mission verify initialization, widget
docking/restoration (including fonts and anchors), map zoom/expand/restore, native
weapon and countermeasure selection, radar/engine/assist switches, the ground gear
restriction, pause cancellation and guarded ejection. Ejection also verifies the
camera handoff to the pilot's helmet point after native asynchronous setup.
For 0.4.5.5, 32 further checks drive the production panel with simulated controller
poses and button values. They cover both hands, direct trigger/grip input, native
weapon/CM/engine/assist actions, the visible pointer, control hints, tracking
recovery, handedness, scaled/tilted positioning, map transitions, the ground gear
restriction and a full direct-trigger guarded eject pull. The previous build
reproduced six interaction failures; all six pass with these changes.
For 0.4.5.6, 38 additional controller-driven checks exercise fixed physical gear
and eject travel across five size/tilt combinations, reject sideways motion and
pushes away from the pilot, verify pending/unconfirmed/accepted feedback, protect a request from a
simultaneous other-hand press, and complete native ejection. The preceding build
failed 15 assertions in this sequence; the updated build passes all 38. These
checks wait for an established cockpit before beginning the interactions. The
32 earlier controller-interaction checks were also repeated and passed on 0.4.5.6.
For 0.4.5.7, 81 checks across the SAH-46 (31), VT-7 (30) and FS-12 (20) verify
widget docking/restoration, map transitions, available/unavailable hover and
ordinary-assist controls, native hover activation through direct, laser and wrist
input, actual-state lamps, stable command readouts, release/regrab and all four
negative-range/inverted-collective setting combinations. Full-scale captions fit
their backplates and the wrist capability labels fit their buttons. These tests
use simulated hand and altitude conditions with the aircraft held stationary;
they validate input and UI behavior, not hover flight handling. The prior 18
flight-grip/menu and 32 controller-console checks also passed again on 0.4.5.7.
The 81 cross-aircraft hover/widget checks were repeated and passed on 0.4.5.8,
with lift-driven collective input in the SAH-46 and forward throttle input in the
VT-7 and FS-12. See [motion controls](MOTION_CONTROLS.md) for the 152 additional
collective/throttle placement and interaction checks.
Live fixed-camera captures verify the model, readable weapon/countermeasure
widgets and minimized map. These captures use a
temporary diagnostic camera; they do not establish stereo comfort or physical
controller reach. In-headset placement, hand interaction and flight feel still
require hands-on validation. The studio preview has blank screens; the displays
populate in the game.

If the embedded model cannot load, NOVR keeps the original HUD and reports one
error instead of retrying every frame. Toggle F6 off/on to retry after resolving
the problem. No diagnostic tool is included in the release package.

## 0.4.6.0 validation

76 automated checks pass, including 45 native patch-target checks. In-game
checks using simulated controller input passed: 53 in the VT-7 for layout,
native handle restoration, MFD pages and focused HUD; 50 in the SAH-46 for
the same core behavior before the final lower-default adjustment; and 32 in
the VT-7 for configuration migration, grip-bound native actions, target selection,
unlock/clear and the replacement inspection magnifier. The latter verify both
actual OpenXR eye projections remain unchanged while the image is visible.
The helicopter's final lower default is compiled; its ergonomics remain for
headset testing. Captures are inspection views, not stereo comfort validation.

## Dashboard clearance audit — 0.4.6.1

The installed set contains 32 aircraft definitions, including workshop aircraft.
A conservative panel-body/interior triangle test found clear automatic placements
for 26. It excludes exterior and simplified LOD shells that are not rendered in
the cockpit, plus the original handles replaced by NOVR. This is geometry analysis,
not a claim that every cockpit was flown or inspected in stereo.

The MiG-29's small angled display leaves a tight intersection with the cockpit tub
after the bounded adjustment. Use Edit layout to bring it farther toward you or
move it toward the center; a roughly 6 cm additional forward clearance cleared the
body envelope in the scan. Five definitions lack a usable frontal display plane:
Aryx F-16M, Aryx Interceptor, blacknight2u F-117A, EW-25, and P_Trisurface1.
They use the free-placement fallback; save a custom per-aircraft placement.

Wide multi-seat dashboards shift the fitted center toward the pilot. Narrow or
recessed screens receive a small forward clearance, with a modest size reduction
when needed. The model retains the same control layout. Native levers, canopy
motion, personal reach and larger manual scale settings still need a headset check.

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
