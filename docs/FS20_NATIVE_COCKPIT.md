# FS-20 Vortex: integrated VR cockpit

Version 0.4.7.5 candidate keeps the FS-20's original instrument cluster and adds
raised, labelled physical controls over its existing switch rows. There is no movable
replacement panel. Seat adjustments never move the cluster or flight handles.
Other aircraft retain the shared console.

## Physical interaction

Release the flight handle with the hand you want to use. Either hand works.
Approach a button with the pointing fingertip. Nearby, the visible hand gently
aligns with the button and guides the press inward along its axis. Press, then
withdraw to release the alignment and rearm the button. No trigger or grip is
needed. This assistance applies to FS-20 physical buttons; held throttle/stick
poses retain their existing behavior. For a switch, bring the index fingertip
close, squeeze lightly to attach a thumb/index-tip pinch, and flick up or down
about 3 mm. A 0.3-second allowance catches a squeeze begun just before contact.
Each switch flips only once per grab, then has a 650 ms cooldown across regrabs.
A grab during that cooldown is ignored until you release and grab again; it
cannot queue a later flip. Release to detach. Radar and EOTS toggles point UP when ON and DOWN when OFF;
the original gear lever retains its separate gear-position behavior.
Grab detection follows the animated cap, with extra vertical
and forward tolerance. An initial nudge toward the existing switch state does
not consume the interaction; reverse direction to flip it. A nearby switch
takes priority over an unheld flight handle or an adjacent button guide. A real Steam Frame squeeze click is accepted even
if the runtime reports a low analog squeeze value. Entering layout mode releases
an already-held flight-action control; the setup buttons stay usable.
Controller rays cannot operate these FS-20 controls.
Raised housings, chamfered caps and metal stems use cockpit lighting. The caps
reference a plain paint/normal-map region of the installed FS-20 cockpit atlas,
with restrained surface detail; white legends use a separate text material.
Touch, press, grab, switch travel and release have distinct sound/haptic cues.

| Location | Control and operation |
| --- | --- |
| Separate PREV / NEXT keys below each MFD | Touch the desired key; page name appears on the fixed nameplate above |
| Original wheel-shaped gear knob, left of the panel | Light grip and short up/down flick; the original wheel and stem follow gear state |
| Lower switch row | Touch-and-hold brake; radar flick switch; EOTS HUD flick switch |
| Two keys on each side of each MFD | Four multifunction keys, 32 × 30 mm, matching changing labels on the screen edge |
| Center of the PREV/NEXT row below the MFDs | MAP PAN: slaves physical left thumbstick to the minimap; click stick to recenter; touch again to release |
| Left side tray, below shoulder | THROTTLE POS: unlock throttle, grab/move it, release, then touch again to save and lock |
| Right side tray, below shoulder | STICK POS: independently unlock/move/save the stick |
| Left side tray, six arrow keys | Seat up/down/left/right/forward/back, one 2 cm step per deliberate touch; withdraw before pressing again |
| Striped handles beside the seat | Fresh grip, then pull UP at least 4.5 cm to eject; no hold timer |

Seat arrows require 4 cm of real hand withdrawal (or leaving sideways by 6 cm)
and a 300 ms cooldown before another touch. Seat movement itself cannot rearm
the key. Holding a key does not repeat the movement.

The position buttons follow handedness. Ground gear/weapon restrictions remain.
Unassigned original switches are decorative; engine, lights, flight assist,
weapon selection and target clearing remain available in View → Flight actions
and through the controller bindings. No decorative JETTISON switch triggers eject.
The View menu retains its separate confirmed ejection action.

## MFDs and cockpit presentation

Both integrated MFDs cycle Weapons, Map, Flight, Engine/Fuel, Systems, Tactical and
Sensor. The left starts on the live minimap, the right on Weapons. The native
widgets/camera feeds are reused; unavailable feeds report NOT FITTED. Selecting a
page already owned by the other display swaps the pages. Keys 1/2 are upper/lower
on the left edge of each display, and keys 3/4 upper/lower on its right edge.
Map offers zoom, center and heading/north orientation. Sensor offers verified
HEAD zoom, HEAD/TRACK and target clearing; Weapons offers weapon/CM selection.
Flight, Engine/Fuel, Systems and Tactical keep two page shortcuts and add MENU
and SWAP. MENU changes the four key labels to PREV, NEXT, OPEN and BACK for
choosing a fitted page; SWAP exchanges the left and right pages. Their
instruments remain readouts. Brightness remains in **View → Comfort & display →
HUD & display → Brightness**. See the [complete four-key matrix](MFD_SOFTKEYS.md).

The taller displays cover the native upper screen area and extend about 54 mm
downward over the old switch strip. Each usable screen is 191.97 × 218.70 mm,
about 3.5 times the previous lower-screen area. Their thin physical housings
bridge the measured panel step. Uniform scaling preserves map circles, while
camera feeds retain their aspect. See [display fit and depth](FS20_DISPLAY_DEPTH.md).

MAP PAN owns the physical left thumbstick while active, preventing accidental
VTOL-angle, yaw or target input. Panning pauses for menus, setup and physical
interactions using that hand. Map CENTER resets the pan offset without claiming
the stick. Changing the seat or recentering during a layout drag releases the
drag without moving its preview; a fresh grip continues placement.

The new housings cover the upper native display artwork and old switch strip.
Native display graphics are suppressed while replaced; the surrounding cockpit
geometry and seat remain intact. Duplicate
floating status panels are hidden while the integrated displays are active.
The original diegetic flight HUD remains visible, including speed, G-force,
altitude, fuel, weapon/ammunition, countermeasure and nozzle readouts. Its native
arrangement follows Flight HUD Scale as a group. Weapon range/guidance widgets
also follow that master scale, retaining their separate size trim. Their native
target/ammunition visibility conditions still apply. The nozzle gauge stays
in its original right-side location, with actual and commanded positions.
A small transparent incoming-missile window sits just above the left upper
coaming, with a matching kill/reward feed above the right side. Each is about
21 × 8 cm at default alert scale, with a faint 12% backdrop and no border.
Missile rows retain seeker type, clock bearing, HIGH/LOW, range and native
NOTCH direction for radar-homing threats when available. The three highest
priority rows are shown with an overflow count; the window hides when empty.
The radar/jamming bearing scope stays beside the right of the cluster.
Both windows are aircraft-fixed, independent of head and seat movement.
MFDs and physical controls stay visible while moving handles,
adjusting the seat or opening the setup menu. Flight actions suspend during setup.
The original small caps, knobs and fasteners intersecting the new housings are
hidden using a private copy of the cockpit index buffer. The 48 measured cover
regions omit 1,746 of 13,292 triangles, selecting whole decorative pieces; the
panel structure, seat and displays remain. The larger MFD housings cover
two old rotary caps, four fasteners and two label quads in addition to the
existing action-row cover. The added GEAR switch is removed. The original
wheel and white stem are the working gear control: their exact 194 triangles
and cockpit material are reused, with a modest gear-state-driven hinge motion.
The installed native cockpit has no independent gear-knob animation to reuse.
These 194 source triangles are hidden only after extraction succeeds, so the
knob is never duplicated and a mismatch leaves the original knob visible.
Disabling the integrated controls restores the original mesh. A geometry mismatch
leaves the native mesh untouched. No seat/headrest masking is installed.

## Flight handles and gloves

**View → Flight controls → Stick sensitivity** (also **Settings → Controls**)
provides a draggable slider. Higher sensitivity needs less wrist tilt for full
pitch/roll, from 60° at the low end to 5° at the high end. The default remains
25° (1.00×), saved settings are retained, and twist yaw has its own response.

Free hands receive light tracking-space visual stabilization; sleeves follow
those stabilized hands. Flight and touch inputs remain raw. Acquiring a handle
or switch eases the rendered hand and fingers into their fitted pose over
120 ms, then holds the exact socket. The existing button guide retains its
90 ms alignment. Tracking loss and recentering reset the visual filters.


The FS-20 throttle's rendered copy is 68% of the former size (approximately 16 cm
across). Travel remains independently adjustable, defaulting to 12 cm. The MIL
shelf resolves from the aircraft's runtime engine registry: 90% command on FS-20.
Push through its shelf for afterburner. There are no floating throttle/stick status
captions. A small physical MIL mark remains. The bottom 4.5 cm of the metal stick
shaft and throttle arm fades smoothly to transparent, marking their floating
ends. The upper handles stay opaque. The geometry is prepared once per handle;
original cockpit meshes and shared materials are untouched.

The FS-20 has separate authored joint poses for its throttle and stick. The
throttle palm rests above the handle, fingers curl independently around its front,
and the thumb can reach the side hat. The stick hand wraps below the head with
its heel clear of the palm shelf. The cuff and sleeve align toward the forearm.
Gloves stay attached to the control while held; free hands follow tracking.

Steam Frame capacitive thumbstick, face-button, trigger and grip presence drive
visual finger motion. On the stick, touching A rests the thumb on the red release
button; pressing A moves its pad inward slightly. Thumbstick touch uses a separate
hat pose. Touch alone never becomes a button press. Controllers without capacitive
signals still animate actual presses. These are input-driven poses, not optical
hand tracking. Other aircraft retain their existing grip poses.

## Right-handed weapon bindings

- Trigger: guns, including when a missile/bomb is selected.
- A: selected non-gun weapon release. It cannot fire a selected gun.
- Frame B: unselect latest target on release while holding the stick; map when the stick is free.
- Frame Y: next weapon on release while holding the stick.
- Right stick click: target selection; clear all through wrist Flight actions or MFD CLEAR.
- Touch-compatible controllers retain B for weapon cycling and wrist actions for unselect.
- Right bumper: magnifier, regardless of grip.

Native pilot/menu/cockpit checks, station safety, reloads and launch/lock/salvo
behavior remain in the firing path. Fixed guns can fire alongside a selected
non-gun weapon without changing the selected station. Existing keyboard/HOTAS
inputs retain their ordinary behavior when no VR firing command is active.
Reapply the preset under View → Flight controls → Button bindings if needed.

## Scope and validation

The installed game's FS-20 definition is `SmallFighter1` (not `Multirole1`).
The implementation targets Nuclear Option 0.34.3 and Steam Frame PC streaming.
No proprietary game meshes, textures or assemblies are distributed. Handle meshes
and pilot sleeves are reused from the local installation; authored poses are only
joint rotations and offsets. No per-frame mesh collision solver is added.

Final 0.4.7.5 runtime and package evidence is pending. The validation plan covers
release compilation, input/asset contracts, UI lifecycle, physical softkey
interaction, guided fingertip press/release, native action delivery and menu layout. Private geometry checks
sample vertices, triangle centers and edge midpoints against the installed control meshes, including thumb
transitions. Offline mission checks use synthetic tracked inputs and inspection
cameras; physical reach, haptic feel and stereo comfort still require headset use.
