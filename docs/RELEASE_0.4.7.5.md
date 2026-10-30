# NOVR 0.4.7.5 candidate

Final build, live validation and package evidence are pending. This document
describes the candidate source; it is not an installation or test-completion
claim.

## Physical MFD controls

The FS-20 MFDs move to larger upper housings, extending about 54 mm downward
over the old switch strip. Each usable display is about 192 × 219 mm, roughly
3.5 times its previous area. Thin backing and bezels bridge the native panel
step, while map symbols and camera feeds retain their proportions.

Each FS-20 MFD gains four raised 32 × 30 mm keys, two down each side. Their
numbered caps align with changing labels on the display edges. Use a pointing
fingertip to touch, then withdraw before touching again. PREV/NEXT remains
separate below each display; MAP PAN sits in the center of that page-key row.

FS-20 buttons gain gentle fingertip alignment: approach with a pointing finger,
press straight inward along the button axis, then withdraw to release the
alignment and rearm. No grip is required. Switches, striped eject handles and
held throttle/stick grips retain their existing interactions. This behavior is
part of the candidate batch and still needs final validation.

- Map: zoom in/out, center, heading-up/north-up.
- Sensor: verified HEAD zoom, HEAD/TRACK mode, clear all targets. TRACK keeps
  native automatic framing and unavailable actions explain their prerequisites.
- Weapons: previous/next weapon, Map shortcut, next countermeasure type.
- Flight, Engine/Fuel, Systems and Tactical: page shortcuts, MENU and SWAP.
  MENU uses the four physical keys as PREV, NEXT, OPEN and BACK to choose a
  fitted page. SWAP exchanges the two displayed pages. Instruments remain
  readouts; brightness stays in View → Comfort & display → HUD & display →
  Brightness.

The existing camera feeds and native actions are reused. The new keys cannot
fire weapons or override native safety. State is checked again when a key is
activated. See [the complete key matrix](MFD_SOFTKEYS.md) and
[FS-20 interaction](FS20_NATIVE_COCKPIT.md).

Only measured redundant native details under the new housings are covered.
The original lower-side gear wheel/stem are the working VR gear control;
the duplicate added GEAR lever is removed. Decorative rotary caps remain visible. Native display artwork is
suppressed while replaced. Surrounding panel structure is preserved, and
disabling the integrated controls restores the original presentation.

## Switches and VTOL indication

Switch acquisition follows the actual animated lever cap with a bounded reach
volume and clear grab/flick hints. Intentional digital squeeze input supplements
analog grip on Steam Frame. Disabled keys can release their guided hand normally,
and entering layout mode cancels already-held flight-action controls.

The original FS-20 diegetic HUD is restored, including speed, G-force, weapon
and countermeasure information. The nozzle gauge retains its native right-side
placement and actual/commanded tilt indications. The authored instrument layout
scales as a group; weapon range/guidance widgets follow the master scale with
their own relative size trim. Native target/ammunition visibility conditions
still apply. Duplicate floating status windows remain suppressed.

## Map, layout and feedback

The integrated FS-20 now forwards its minimap pan offset. Slaved panning pauses
for menus, setup and physical interactions using the left hand. Ownership keeps
the same thumbstick from also commanding nozzle angle, yaw or target selection.
Centering does not claim the stick.

Moving the seat or recentering during a layout drag releases that drag while
keeping its preview fixed; release and grip again to continue placement.

**View → Comfort & display → Sound & haptics** adds **Try touch**, **Try grab**,
**Try switch** and **Try MIL detent**. A normal pointer press/release plays one
sample in that tracked hand, without operating an aircraft control. Sound and
haptic strength remain independently adjustable.

## Retained features and validation

Fitted controls and gloves, independent free-hand fitting, compact HUD/EOTS,
the supported-loader startup screen and fork-safe installer are retained.
Other aircraft continue to use the shared console.

Nine portable suites cover source behavior without game assemblies. Final live
checks and installer package validation must be recorded against the final
source revision and DLL; see [verification scope](VERIFICATION.md). The source is being prepared for SilentHeron/novr-interactive. Public release
and final packaged-build validation remain pending the source review.

No PC dynamic foveated rendering or measured frame-rate improvement is claimed.
Synthetic checks and inspection captures do not establish headset comfort,
physical reach, haptic feel or multiplayer flight correctness.

## Local interaction and native HUD refinement

The latest local candidate restores the native FS-20 diegetic HUD layout and
its flight/weapon readouts, with shared flight-HUD scaling and native nozzle
placement. The gaze circle uses the live HUD green, is about 20% smaller and
more transparent, and sits about half a degree lower. Two verified empty native
screen borders are hidden while the integrated FS-20 displays are active.
Switches use fingertip acquisition, a lighter squeeze, a
short approach allowance and a 3 mm flick; an initial movement toward their
existing state no longer consumes the grab. The original gear wheel/stem
replace the added GEAR switch, with gear-state-driven movement of the original
geometry. Switch pinch and stick-thumb poses have been refitted. Free hands
and their sleeves receive light visual stabilization. Hands ease onto handle
and switch grips over 120 ms, then hold their exact contact pose. These local
refinements include seat-button repeat protection: each touch produces one
2 cm step, and actual hand withdrawal is required before the next press.
A draggable stick sensitivity slider is available in View → Flight controls
and Settings → Controls, reusing the saved pitch/roll angle without changing yaw.
The optical system is now labelled EOTS throughout the interface. These changes
require headset review; prior runtime evidence does not validate this revision.

## Upper cockpit feeds

The FS-20 incoming missile rows now occupy a small transparent window above
the left coaming; the kill/reward feed occupies a matching window on the right.
Seeker, bearing, range and native radar-missile NOTCH directions are retained.
The missile window hides without detections, and the radar/jamming scope stays
beside the instrument cluster. Placement is aircraft-fixed and independent of
seat adjustments. These positions still need headset review.


## Recovery controls and headset view

The controller map action remains available after a crash and in spectator view.
A latched stick on a disabled aircraft no longer blocks B. View/F9 opens the
recovery map outside a usable cockpit, while retaining the wrist menu inside one.
Native map availability and menu/text-entry restrictions remain in force.

FS-20 flick switches now need 3 mm of vertical fingertip movement after grabbing.
Non-gear caps point up when on and down when off. A 650 ms per-control cooldown
and one action per grab prevent rapid accidental reversals; a squeeze attempted
during cooldown must be released before retrying. The original gear knob is kept.
The shared console uses the same cooldown and lighter physical gear travel.

Vegetation culling uses the tracked headset camera and a conservative envelope
of both eye projections, rather than the original aircraft-facing camera cache.
Native tree range, density and distance detail remain unchanged. This can render
more trees when looking sideways; no frame-rate improvement is claimed.

The main-menu VR CENTER button is attached below the menu window, sharing its
fixed pose rather than following head turns.

The current local native anti-aliasing preference is 4× MSAA. The installed
Forward URP pipeline applies its MSAA setting to XR; no global eye-tracking MSAA
disable was found. PC dynamic foveated rendering remains inactive. This batch
does not change graphics settings or claim to remove all visible aliasing.


## Inspection quality and recurring render work

The RB inspection window now has its own resolution, MSAA and scene-effects
settings in View → Comfort & display → HUD & display → Inspection zoom and in
Settings → Graphics. Defaults are 1280×800, 2× MSAA and scene effects off; higher
resolution and 4×/8× MSAA are optional. Main Anti-Aliasing must be enabled for
this URP version to use zoom MSAA. Unsupported sampling falls back, and texture
replacement preserves the old working image if allocation fails. Headset
resolution, stereo projections and native MSAA preferences are not changed.

Camera discovery reuses its buffer; map/sensor captions are formatted only
when their displayed inputs change. Hidden native-app CanvasGroups are cached
and periodically rediscovered while visibility ownership and restoration are
preserved. Zoom HUD copies cache unchanged image/text mesh channels and topology,
while positions, materials and alpha still update every draw. Dirty graphics,
text rebuilds, direct TMP uploads and SDF scale changes invalidate those caches;
unknown custom graphics keep the conservative copying path. These reduce
recurring CPU work; no measured frame-rate gain is claimed. This does not suspend the unused FS-20 tactical camera or app updates.


## Peripheral navigation arrows

Off-screen mission and airbase/location cues use a separate peripheral band,
inset within both eyes' common view and capped at 42° horizontally and 34°
vertically. Visibility is derived from the runtime stereo projections instead
of the old fixed 25° central clamp. Navigation locations visible in either eye
remain at their true bearing. Off-screen labels use inward HUD-space offsets;
combat targets and weapon cues retain their existing behavior. Projection math
was checked at side, rear and diagonal bearings with asymmetric/narrow eye
bounds; actual headset placement remains to be reviewed.
