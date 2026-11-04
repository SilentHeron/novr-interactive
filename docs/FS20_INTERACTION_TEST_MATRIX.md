# FS-20 interaction test matrix

Historical record for the retired 0.4.7.5 replacement-panel experiment. The
replacement MFDs, added switches and their dedicated harnesses are no longer in
the current runtime. This document preserves that experiment's evidence and
limitations; use [verification](VERIFICATION.md) for current source checks.

This matrix records implemented coverage and remaining acceptance work for
0.4.7.5. **Final installed-build runtime evidence: pending.** The final DLL
identity belongs in the external validation report alongside its results.
An implemented probe is not a passing result, and earlier builds' results do not
establish a pass for the final DLL. Update this marker only after the final
reports, cleanup results and build identity have been checked together.

The target device path is PC SteamVR streaming to Steam Frame. Record the source
revision, DLL hash, Nuclear Option version, aircraft/loadout, controller/runtime
path, settings and report artifacts with each run. Private probes run in an
isolated offline FS-20 and restore their settings, native state and temporary
hooks. They are local development tools, not part of the public mod or portable
CI, and must not run in multiplayer.

## Implemented coverage and evidence limits

| Source | Implemented coverage | Evidence limit |
| --- | --- | --- |
| Retired `ButtonContactGuideChecks` and core checks | Production centering, raw press depth, one-shot press, withdrawal, invalid samples, cancellation and disabled-key recovery | No rendered hand, hardware tracking or entire cockpit interaction |
| Retired `SwitchGrabChecks` and [core input checks](../tests/Program.cs) | Actual cap-volume math, neighbor separation, fresh grip arming, recovery and analog/digital squeeze normalization | No real controller delivery or aircraft action |
| Retired `MfdContextChecks` | All page/key descriptors, availability combinations, map/sensor limits, fitted-page navigation and ownership | No physical contact or native dispatch |
| Private `ButtonSnapLive.cs` | Both-hand production contact/pose paths, rendered fingertip checks, brake hold, cancellation, disabled-key recovery and held-switch exclusion | Tracked samples are simulated; no motor, capacitive sensing or comfort measurement |
| Private `SoftkeyLive.cs` | Map keys, every page's labels, physical picker navigation and swaps, real widget ownership, picker artwork suppression/restoration and sensor lifecycle | Map hands match their display side; some sensor lifecycle cases call the action API directly |
| Private `MfdActionLive.cs` | All four Sensor and Weapons keys with each hand on each display; native selections, targets, camera feed, zoom limits and CLEAR recovery | Positive headset readiness is simulated; no live fire/deployment or motor/sound perception |
| Private `SwitchInteractionLive.cs` | Both hands, GEAR/RADAR/EOTS, both starting states, cap positions, natural rotations, rendered pinch, native state transitions, feedback requests and cancellation | Altitude is simulated; gear mechanics are isolated; no physical takeoff or headset comfort proof |
| Private `MfdInputLive.cs` | Native map movement/zoom/orientation, thumbstick ownership, contextual blocking and full-map release/return | Tracked input and ownership contexts are simulated; does not validate physical switch actuation |
| Private `NativeMfdVisibilityLive.cs`, `NativeDockLayerLive.cs`, `DepthTests.cs` | Real widget leases, replacement/restoration, late native children, content hierarchy, measured screen geometry, layers and rendered occlusion | Desktop rendering checks do not establish stereo readability or eye comfort |
| Private `NozzleHudLive.cs` | Native nozzle source, actual/commanded-angle rendering, rendered mesh bounds, proportional size/position, brightness and presentation release/replacement | Rendering samples substitute read-only gauge references; they do not actuate the nozzles |

All listed probes are implemented. Final runtime results remain pending. A
conditional or skipped branch needs its own evidence; passing nearby branches
does not cover it. Hardware acceptance remains separate even after automated
reports pass.

## Buttons and held interactions

Repeat physical acceptance with both hands. For MFDs, distinguish each hand on
each display from the simpler left-hand/left-display and right-hand/right-display
cases. The key/page definitions are in [MFD softkeys](MFD_SOFTKEYS.md).

| Case | Expected behavior | Implemented coverage and remaining gap |
| --- | --- | --- |
| Approach with grip/trigger released | A nearby pointing fingertip aligns gently; alignment alone causes no action | Portable guide and both-hand Map-button runtime cases; every physical key and natural approach orientation still need headset checks |
| Press, hold, retreat, press again | Raw inward movement presses once; contact never repeats; withdrawal rearms | Portable gates and both-hand Map zoom cases; PREV/NEXT, MAP PAN, position locks and seat arrows lack the same full runtime sequence |
| Lateral movement or rotation | Rendered tip remains on a bounded axis while raw tracking stays independent; moving aside releases | Portable bounds plus runtime Map-button drift/rotation; inspect wrist comfort and clipping in headset |
| Start inside or recover tracking over a button | No synthetic press; invalid/default tracking cannot count as withdrawal | Portable initialization/invalid-data checks and runtime Map-button tracking recovery |
| Press a key that disables itself | Guidance releases after valid withdrawal; other controls remain usable | Runtime Map ZOOM+ then ZOOM−; Sensor upper/lower limits and last-target CLEAR on both hands/displays, including re-enable beneath a resting finger |
| Press an already disabled key | No action, press cue or stale operation | Runtime Map limits and Sensor limits/TRACK zoom; portable availability matrix |
| Brake hold | Alignment does not brake; tracked contact starts hold, including deeper contact; retreat releases | `ButtonSnapLive` includes raw penetration to −20 mm and retreat beyond the hold threshold; physical-controller brake release remains a headset check |
| Hand already owns a switch | Nearby button cannot steal pinch ownership or activate incidentally | Actual held RADAR moved toward the neighboring brake region; both-hand/all-switch natural approaches also reject neighboring button guidance |
| Hand owns a flight grip or eject handle | Nearby interaction cannot take over that hand | Separate live conflict cases remain; switch checks do not claim this coverage |
| Page/key meaning changes beneath a resting finger | New meaning stays disarmed until withdrawal, including other-hand changes in the same frame | Runtime page replacement, held MENU→OPEN and same-frame two-hand page swaps; portable generation checks |
| Menu/tracking cancellation | Detach and release hold immediately; recovery while resting cannot create a press | Runtime GameMenu and tracking cases. Push-button layout/recenter/pause/external-view/detach/aircraft-replacement coverage remains incomplete |

## MFD actions and navigation

Both displays' four edge labels are checked against their four physical caps for
every page. A correct label alone does not establish successful native dispatch.

| Page or operation | Implemented runtime check | Remaining boundary |
| --- | --- | --- |
| Map | Physical ZOOM+/−, native bounds, CENTER without ownership change and heading/north toggle; both corresponding hands/displays | Cross-hand Map reaches remain. Separate input probe covers actual pan, zoom pivot, orientation, slave suspension, intentional throttle-grip pan and full-map return |
| Sensor | Each hand on each display physically uses zoom, HEAD/TRACK and CLEAR; actual rendered native feed; an existing live Unit supplies TRACK target; last-target disable/re-enable/withdraw/reuse | Headset-ready state is simulated. Existing target fixture is restored, not a target acquisition or live-combat test |
| Weapons | Each hand on each display physically uses native previous/next weapon, wraparound, Map shortcut and next CM selection | No fire, CM deployment or safety override; loadout must provide at least two meaningful weapon and CM choices |
| Flight, Engine/Fuel, Systems, Tactical | Left-display related-page shortcuts and MENU/BACK; MENU/SWAP descriptors; all-page labels on both displays | Full cross-hand shortcut coverage remains |
| MENU and four picker keys | Current selection, only fitted rows, white selected text; PREV/NEXT change selection, OPEN commits, BACK discards; held MENU cannot inherit OPEN and held OPEN cannot reopen | Both-hand held-contact case uses Flight; not every picker transition has every hand/display combination |
| Picker artwork suppression | Actual Flight/Engine-Fuel/Systems Graphics or Tactical feed are visible first, inactive while MENU is open, and restored by BACK; native child `activeSelf` and application Graphic `enabled` flags stay unchanged; registered TMP-generated submeshes follow their native disable/enable lifecycle; other display still renders | Final rendering evidence pending; does not rely only on a group-alpha setting |
| Nested canvas bypass | Both hands' Flight picker uses a real nested override-sorting canvas and `ignoreParentGroups` test object inside the borrowed widget; it must hide with content while picker text remains renderable | Temporary test object is destroyed afterward; actual native artwork is checked too |
| Picker restoration | BACK, current-page OPEN and context close restore prior artwork and every exact original Graphic `enabled`/`activeSelf` flag, including TMP submeshes; selecting Engine/Fuel rebuilds visible content; selecting the other display's Systems page restores both sides; dedicated PREV restores Map | Dedicated NEXT/opposite-display cycling and every external cancellation path remain separate acceptance cases |
| Select the other display's page | Physical selection swaps actual Flight/Systems ownership, leaving one dock for each widget | Does not duplicate live native sources |
| SWAP | Physical left/right swap; actual native widget ownership; map slave follows its display while pan, native zoom, configured orientation and optical state remain | Headset reach/feedback remains unmeasured |
| Availability changes with picker open | Real Engine/Fuel app removal updates fitted rows; PREV/NEXT skip it; BACK exits; exact native app array restored | Zero fitted pages and live loss of the selected page need separate runtime evidence |
| Native content lifecycle | App replacement/destruction, source suppression, exact restoration, late spawned map children and layer resets; content corners match screen geometry; actual front/behind occlusion | All final results must match the installed DLL; current-page nested content must not weaken geometry or layer assertions |

The picker test recognizes only actual `TMP_SubMeshUI` children registered in
their `TextMeshProUGUI` owner's submesh array as generated-text lifecycle objects.
The installed text library disables those submeshes when their owner is hidden
and enables them when it returns. The test requires that hidden state and exact
original flags after showing the page; it does not exempt other native graphics
from preservation or restoration checks.

`MfdActionLive` covers four hand/display combinations for each of Weapons and
Sensor. It blocks and records unexpected fire, CM deployment and unrelated
controls as failures. It checks ammunition and firing-state preservation and
restores original target ordering, marker selection, weapon/CM choice, optical
settings, camera state and temporary native target UI. A missing reflected API
or preparation error produces a failed report instead of a stuck preparation
status. Preconditions such as pre-existing manual target markers are reported;
those branches are not silently treated as passes.

## Flick switches

The acquisition matrix is **2 hands × 3 switches × 2 starting states**: twelve
families. Each samples the cap center, lateral/vertical/front margins, neutral
and modestly rotated raw-controller orientations, and outside-volume rejection.
The native-action matrix adds both requested directions for each hand/switch.

| Case | Implemented runtime check | Remaining boundary |
| --- | --- | --- |
| Actual moving cap | Hover and locked pinch use the animated actuator cap; cap-center and offsets are sampled in both states | Real reach/comfort still needs headset acceptance |
| Reach volume | Inside samples include ±32 mm lateral, ±35 mm vertical and 65 mm forward; outside samples use ±49 mm lateral and 100 mm forward | Portable math checks cover exact finite/boundary behavior: ±40/45 mm ellipse, −20..75 mm depth and no more than 4 mm behind plate |
| Fresh grip | One edge owns the switch, requests one grab haptic/audio cue, renders thumb/index midpoint at the cap and does not toggle | Hardware signal delivery and perceived cue strength are not measured |
| Held grip before approach | Entering while gripping does not acquire; release/regrip succeeds | Real Steam Frame digital-grip/analog behavior still needs acceptance |
| Natural wrist orientation | Neutral controller and two modest rotations, each at center and lateral offset; no neighboring fingertip guide or action steals the hand | Broader body/reach variation and hand clipping remain visual acceptance |
| Flick up/down | 11 mm produces no action; 12.5 mm crosses the 12 mm threshold and changes actual native/configured state once; reverse movement in the same grip cannot toggle again; same-state flick is a no-op | Exact 12 mm is not a separate runtime or portable flick-boundary case; the runtime samples straddle it |
| Ground gear guard | Simulated altitude 0 rejects upward flick with the ground-lock message and no native gear request; altitude 15 allows native input processing | No physical takeoff or mechanical extension/retraction test |
| Native action and feedback | Gear changes native boolean and Retracting/Extending state; radar changes native activation; EOTS changes configured state; one switch haptic/audio request; pinch follows changed cap | Logical/native state, not all downstream aircraft mechanics |
| Release/distance/tracking/menu | Release or retreat detaches; held return cannot re-grab; tracking loss and GameMenu cancel, and held recovery requires a fresh edge | Pause, external view, cockpit detach, aircraft replacement and recenter while held remain explicit gaps |
| Layout Begin/Finish(false) | Both hands holding RADAR release on real layout entry; no motion/fresh grip can toggle in layout; held exit cannot reacquire; fresh grip works afterward | Live eject-owner cancellation and other switch/layout combinations remain gaps |
| Competing hand | For all three switches, the second hand cannot steal a left-owned pinch; first release cannot transfer to an already-held right grip; fresh right regrip works | Reverse owner order is not a separate runtime case |

The fixture discovers every loaded `LandingGear` belonging to the local aircraft
by its native owner, including separately parented parts, and records the actual
count. The inspected FS-20 fixture has **three gear components and three
hinges**. It requires a nonempty inventory and every hinge before any mutation;
an absent hinge cannot turn an empty check into a pass. Only the local owner's
mechanical gear listeners are isolated, with a final hinge-position/rotation
check. Logical gear state and synthetic altitude are restored before removing
the hook. This proves native control dispatch while keeping wheel mechanics
stationary, not flight or gear animation.

The suite also restores radar/EOTS settings, inputs, queued actions, feedback,
controller samples, physics and temporary hooks. It checks no neighboring
button, brake, flight-handle, weapon or eject action was requested and preserves
target selection and the seated pilot. The cap change does not alter the native
ground safeguard, eject behavior or flight-handle bindings.

## Native nozzle readout

The implemented nozzle probe first verifies an actual FS-20 nozzle system is
bound and its angle drives the native gauge. Rendering samples then substitute
only the gauge's read-only system/input references, leaving real flight inputs
and nozzle actuation untouched.

| Case | Implemented runtime check | Remaining boundary |
| --- | --- | --- |
| Duplicate HUD suppression | Gauge is borrowed beside the cockpit outside the hidden duplicate manager; visible graphics, compact placement, nonblocking raycasts and recovery of faded graphics | Stereo readability |
| Actual versus commanded angle | Native source binding, 0/45/90-degree icon/label samples and an intentionally mismatched actual/command sample | Real controller-driven nozzle actuation; aircraft attitude is not used as a substitute |
| Rendered size and placement | CanvasRenderer mesh vertices are measured in HUD-center coordinates at default scale; actual artwork must fit the compact right-HUD bounds. The 0/45/90-degree samples stay bounded with a fixed source scale | Screenshot and mesh bounds do not establish stereo readability |
| Proportional scale/brightness | Changing HUD scale from .38 to .22 scales measured artwork width/height and right-side anchor position by the same ratio; instrument brightness follows its setting. Disabling focused HUD uses the native unscaled convention (1/.38 relative to default) | Readability while maneuvering and preferred headset size remain user acceptance |
| Release/replacement | Exact parent/sibling/RectTransform and fade restoration; unfitted source, pooled return, replacement and deferred anchor cleanup | Wider aircraft/scene transitions need their own evidence |

## Hardware acceptance and final evidence

Automated feedback checks count actual haptic/audio dispatch requests and reject
repeat requests from held contacts. Portable feedback checks cover device-hand
routing, disabled output and cancellation. Neither demonstrates that Steam Frame
vibrated, that a sound was audible at the selected volume, or that the feedback
felt satisfying.

The headset pass must confirm both hands' touch/grab/press/flick/release cues,
readable labels/pickers, natural reach, hand fit, depth/occlusion and stereo
comfort. Check actual SteamVR grip-pressed input alongside analog squeeze;
finger presence or capacitive touch alone must not grab a switch. Confirm flight
handle and eject ownership boundaries without treating the safe automated
switch fixture as proof of those interactions.

Before updating the pending marker, associate final reports with the installed
DLL, verify every suite completed and restoration succeeded, retain conditional
branches and gaps as limitations, and remove temporary diagnostics. Record
assertion counts from those final reports rather than copying earlier totals.
See [reproducible verification](VERIFICATION.md) for portable and assembly checks.
