# Contributor architecture

NOVR adapts the running game's cameras, controls and UI. Native aircraft logic
and cockpit displays remain authoritative. Start with [CONTRIBUTING](../CONTRIBUTING.md)
for builds and [verification](VERIFICATION.md) for executable checks and their limits.

## Startup and ownership

1. [`NOVR.Patcher`](../NOVR.Patcher/Patcher.cs) installs the XR payload before
   ordinary plugins load. Its managed upgrade path handles assemblies already
   opened by the preloader; see the [vendor boundary](XR_VENDORING.md).
2. [`NOVRPlugin`](../NOVR/NOVRPlugin.cs) first checks the graphics backend through
   [`D3D11Startup`](../NOVR/Compatibility/D3D11Startup.cs). A Steam Windows/Proton
   launch using another backend hands off once to the same executable with D3D11.
   [`StartupHandoff`](../NOVR.Patcher/StartupHandoff.cs) waits for that parent before
   copying payload or initializing plugins; the retry never changes saved Steam
   options. Flat/headless launches skip NOVR runtime creation. Normal startup then
   publishes configuration, initializes compatibility handling, applies Harmony
   patches and creates `Core`.
   - Configuration is bound with BepInEx's save-on-every-change turned off, written
     once, and afterwards saved by
     [`DeferredConfigSave`](../NOVR/Compatibility/DeferredConfigSave.cs) a second
     after the last change and when the game closes. The default rewrote the whole
     file for each of several hundred binds at every launch and on every step
     of a dragged slider.
   - [`PatchInstaller`](../NOVR/Compatibility/PatchInstaller.cs) installs each
     Harmony patch class separately. A class is skipped, and reported, when
     Harmony cannot patch it or when
     [`GameHandles`](../NOVR/Compatibility/GameHandles.cs) finds that one of its
     required `static readonly` reflection handles did not resolve. Declare a
     handle nullable (`FieldInfo?`) only if every use copes with null; a
     non-nullable handle may be used unguarded because its patch is never
     installed without it. Read a private game field that is needed more than
     once through a typed field reference (`AccessTools.FieldRefAccess<T, F>`
     in a patch class, [`GameField.Optional`](../NOVR/Compatibility/GameField.cs)
     elsewhere): `FieldInfo.GetValue` is a reflection call and boxes every
     bool, float and struct, and `Traverse` allocates on top of that. The
     assembly contracts check each typed handle against the field's real type.
   - [`StartupDiagnostics`](../NOVR/Compatibility/StartupDiagnostics.cs) collects
     what the player should know about a degraded start (a game version other
     than `SupportedGameVersion`, skipped patches, a layer NOVR uses that
     something else named). The log gets every line; the wrist menu's VR
     performance page shows the summary.
3. [`Core.Awake`](../NOVR/Core.cs) registers the persistent feature components.
   `Core.Start` creates headset/UI state and enables XR through
   [`VrTogglerManager`](../NOVR/VrTogglers/VrTogglerManager.cs). A runtime that
   is not ready is not an error: the manager records the reason,
   [`VrStartRetry`](../NOVR/VrTogglers/VrStartRetry.cs) schedules three automatic
   attempts (5, 15 and 30 seconds apart), **F11** tries again on demand, and
   [`VrStartNotice`](../NOVR/VrTogglers/VrStartNotice.cs) says so on the desktop
   window. Automatic attempts are limited because creating an OpenXR instance can
   launch the runtime's own application.
   [`XrPluginOpenXrToggler`](../NOVR/VrTogglers/XrPluginOpenXrToggler.cs) registers
   controller profiles and selects multipass before loader initialization.
4. [`VrCameraManager`](../NOVR/VrCamera/VrCameraManager.cs) discovers scene cameras
   and owns their tracked replacements. [`APIBus`](../NOVR/APIBus.cs) publishes
   camera changes; [`NOUIManager`](../NOVR/VrUi/NOUIManager.cs) attaches VR UI as
   scene/menu objects appear. Startup does not imply an aircraft or HUD is ready.

[`VrCockpitNearClip`](../NOVR/VrCamera/VrCockpitNearClip.cs) and
[`VrFarClip`](../NOVR/VrCamera/VrFarClip.cs) lease only the tracked view's clip
planes: a closer near plane in the cockpit, and a far plane extended beyond the
game's 80 km when the preference asks for more. Both adopt native rewrites as
their restore value. [`VegetationViewPatch`](../NOVR/VrCamera/VegetationViewPatch.cs)
gives the detail pass the tracked view and stereo frustum planes, but keeps the
grass/tree window biased along the native rig's forward rather than the head.

[`SpectatorCameraInput`](../NOVR/VrCamera/SpectatorCameraInput.cs) adds gated
thumbstick orbit/distance input while following a unit in VR. Native camera motion
keeps its target-scaled external distance, collision and smoothing; the tracked
child remains the stereo view. Input requires neutral after menus or target
changes and excludes the spawn map and aircraft selection. The outside-aircraft
wheel owns its menu/map actions without borrowing the aircraft's native wheel.
Native wheel blocking checks require an active/enabled owner: its child can
remain activeSelf under the inactive spectator HUD before the first spawn.
With nothing followed, the same input drives the game's free camera, still
excluding the map and selection pages. [`PilotView`](../NOVR/VrCamera/PilotView.cs)
switches a flying pilot between the cockpit and the native orbit camera with
the same guarded `SwitchState` calls as the game's own camera buttons, and
releases a spectator's followed unit into the free camera. While the pilot
flies from outside, [`ExternalFlightInput`](../NOVR/FlightControls/ExternalFlightInput.cs)
owns the thumbsticks: it writes pitch, roll, rudder and throttle in the same
pilot-input postfixes as the virtual handles, and hands only the camera part
to `SpectatorCameraInput`.
[`AircraftWalkaround`](../NOVR/VrCamera/AircraftWalkaround.cs) teleports the
player at aircraft selection by moving `NOVRHeadsetData`'s translation anchor,
the player's own offset inside the tracking space. The game camera (the
tracking origin) does not move, so the world and the interface space, which
share that local pose, keep the menu where it was. A snap turn is
`NOVRHeadsetData.TurnAbout`: heading offset and anchor change together so the
head stays put. Pulling the thumbstick back calls the game's menus to the
player through `NOVRGameplayUIBehaviour.Place`; leaving the screen restores
anchor, heading and menu placement, unless the player recentred in between.
[`MenuControllerModels`](../NOVR/VrUi/Controllers/MenuControllerModels.cs)
draws SteamVR's own render model for the detected controller on the grip pose
and labels its trigger and thumbstick with what they do on the current screen
(`ControllerCallouts`, by `ControllerHintContext`: menus, aircraft selection,
full-screen map; never flight bindings). Each button's own mesh
(`ControllerPartShape`, from the per-part files beside the model) gives the
label's line a real surface to end on and a route that does not cut through
the controller, and is drawn white while that button is pressed. Models are parsed off the main thread
and stay loaded while the feature is on. On the map in a cockpit they replace
the gloves (`PilotHandVisuals` hides while `MenuControllerModels.Active`).
[`FlightControlUiDepth`](../NOVR/FlightControls/FlightControlUiDepth.cs) gives
the HUD camera colourless depth copies of the solid stick and throttle meshes,
as `PilotHandUiDepth` does for the gloves, so widgets are cut out behind them.
Map access uses the native Maximize/Minimize lifecycle and does not require a
cached local player or aircraft. Opening locks still belong to native UI; closing
an existing map remains possible without changing the followed unit.

Folders group code by feature. Existing CLR namespaces and public type names
are deliberately retained during reorganization: integrations, reflection and
assembly contracts can depend on those names. Moving a file is not permission
to rename its runtime identity or persisted configuration keys.

## Input ordering and context

[`TrackedControllers`](../NOVR/Input/TrackedControllers.cs) caches one
validated sample per hand per frame. Availability requires active, focused XR,
tracked headset state and presence when supplied by the runtime. Each consumer
adds its own context checks; there is no single global "cockpit ready" gate.

The table gives `DefaultExecutionOrder` within the indicated Unity phase.
Ownership checks and release gates matter as much as numeric order.

| Component | Order / phase | Context and ownership |
| --- | --- | --- |
| `CockpitProfiles` | -1300 / Update | Selects the current aircraft/slot and placement state before consumers. |
| [`CockpitHudWidgets`](../NOVR/VrUi/Widgets/CockpitHudWidgets.cs) | -1260 / Update | Requires a valid cockpit/camera space to show widgets; tracked focus, unpaused input and closed menus additionally gate editing. Claims widget/corner/appearance hands before handle placement. |
| [`CockpitLayout`](../NOVR/Profiles/CockpitLayout.cs), [`NativeHandOptionWheel`](../NOVR/VrUi/RadialMenu/NativeHandOptionWheel.cs) | -1250 / Update | Layout yields a hand already editing a widget. The wheel requires a free hand, live cockpit and no layout/menu/full-map conflict; ownership persists through cancellation/neutral handoff. These equal-order components must not rely on ordering between themselves. |
| [`CockpitMapInteraction`](../NOVR/VrUi/Map/CockpitMapInteraction.cs) | -1240 / Update | Direct fingertip contacts require a visible cockpit map and free interaction context. Thumbstick reservation survives until neutral handoff; navigation is updated again by `ControllerActions` after current-frame grip sampling. |
| [`MotionFlightController`](../NOVR/FlightControls/MotionFlightController.cs) | -1200 / Update | Requires enabled motion controls, a live local pilot, attached cockpit, tracked camera/headset and enabled flight input. Menus/layout suspend grips. NOVR's one-hand wheel can coexist with the other flight hand. |
| [`HelmetNightVision`](../NOVR/Input/Gestures/HelmetNightVision.cs) | -1175 / Update | A free hand at the brow may own trigger input; held handles, wheel, editing, menus, pause and the full map exclude it. |
| [`WristMenu`](../NOVR/VrUi/ViewMenu/WristMenu.cs) | -1100 / Update | Opens View in cockpit context; outside it, the same request can open the recovery map. Respects wheel/map reservations. |
| [`ControllerActions`](../NOVR/Input/ControllerActions.cs) | -1050 / Update | Grip-bound actions respect wheel/map ownership and binding changes. Recovery-map availability deliberately does not require a surviving aircraft or held handle. Queued native actions have a separate, stricter flight context. |
| [`VrUiCursor`](../NOVR/VrUi/Input/VrUiCursor.cs) / [`ControllerUiPointer`](../NOVR/VrUi/Input/ControllerUiPointer.cs) | -1000 / Update | Runs for interactive UI, including native menus outside flight. Pointer ownership changes on actual input, not ray hover; helmet/wheel hands are excluded. Cancel captures when their page or target disappears. |
| [`PilotHandVisuals`](../NOVR/Hands/PilotHandVisuals.cs), `PilotHandUiDepth` | 10000, 11000 / LateUpdate | Render poses, arm solving and depth handling follow input sampling. They do not decide flight or contact input. |

When changing a gate, check held-input recovery, tracking loss, aircraft changes,
pause and ownership transfer. Cancellation must not synthesize a release action.
The native wheel's eject confirmation and aircraft-bound queued actions are
examples of state that must be discarded when its original context disappears.

## Coordinates and visual hands

- Controller grip and aim poses are distinct. Sampling applies headset calibration
  and seat offset; [`NOVRHeadsetData`](../NOVR/NOVRHeadsetData.cs) owns that mapping.
  Use the aim pose for menu rays and raw mapped contact points for direct input.
- Game and cockpit-UI cameras have different rig roots. Convert through those
  roots rather than treating their world positions/depth buffers as interchangeable.
  [`CockpitWidgetSpace`](../NOVR/VrUi/Widgets/CockpitWidgetSpace.cs) uses the
  pilot viewpoint's rigid pose, ignoring imported bone scale. Saved layout offsets
  stay aircraft-fixed when the player recenters or changes their seat.
- [`RiggedGlove`](../NOVR/Hands/RiggedGlove.cs) blends visual attachments;
  `PilotHandVisuals` then stabilizes free hands and updates
  [`PilotSleeves`](../NOVR/Hands/PilotSleeves.cs), which owns the copied arm bones,
  fitted meshes, sleeve solving and bounds. Immutable arm bind poses and private
  elbow support bones keep native head-turn animation and deep bends from
  twisting the cloth. Sleeve and hand-depth copies retain matching skinning
  quality. Its native pilot and hand-material
  owner are borrowed; failed creation releases the partial sleeve resources.
  Snapped/smoothed
  meshes and capacitive finger animation must never feed back into contact tests,
  grip acquisition or button presses.

[`CockpitHudWidgets.Session`](../NOVR/VrUi/Widgets/CockpitHudWidgets.Session.cs)
owns the combined HUD/seat/handle editing transaction. Aircraft or profile changes
cancel it before releasing borrowed UI. Seat calibration, recentering and temporary
input suspension release contacts without discarding the preview. Rendering and
gesture geometry remain in the main component; neither path owns profile storage.

[`CockpitHudWidgets.Wrist`](../NOVR/VrUi/Widgets/CockpitHudWidgets.Wrist.cs) maps
optional wrist attachments into that same cockpit space. It freezes a widget's
wrist frame during a grab and rebases on release. Profile data retains independent
cockpit and wrist poses; switching anchors never overwrites the cockpit placement.
[`HudWidgetWristVisibility`](../NOVR/VrUi/Widgets/HudWidgetWristVisibility.cs) owns
the glance dwell/fade. Tracking loss and the anchor hand's quick wheel override
visibility immediately. Unsaved catalog defaults form three below-eye planes;
their initial seated origin is still captured by `HudWidgetDefaultPlacement`.

`CockpitHudWidgets.WristDock` commits wrist attachment only from a deliberate
single-hand grip release. Its proximity gate requires a brief dwell and checks
both rendered and raw drag positions; context loss cancels it. The separate
preview partial draws the destination and release hint without acquiring input
or native UI. Two-hand resizing suppresses docking until a fresh grab.

## Borrowed native UI

[`TargetCueCandidates`](../NOVR/VrUi/Targeting/TargetCueCandidates.cs) ranks eligible
native markers by angle from the adjusted cue ray, after the configured class tier
(`TargetCuePriority`: units before missiles by default) and the friendly-unit
option; native `TargetListSelector` exclusions still apply first. `GazeCueRay` derives this ray
from the tracked HUD camera and the same sanitized canvas offset as the visible
symbol. The cue and VR single-target
selection share this fresh scan; visible marker positions preserve native known-
position rules. `GazeTargetSelectionPatch` delegates the chosen unit to native
`CombatHUD.SelectUnit`, retaining weapon/network notifications. Paint selection,
the full tactical map and desktop mode retain their native paths. Cue position
changes targeting alignment; size, shape and opacity change only appearance.

[`CockpitStatusSources`](../NOVR/VrUi/Status/CockpitStatusSources.cs),
[`CockpitMapWidget`](../NOVR/VrUi/Map/CockpitMapWidget.cs) and
[`NativeRadialMenuLease`](../NOVR/VrUi/RadialMenu/NativeRadialMenuLease.cs) borrow
live game objects rather than duplicating their events or action lists. Capture
original parents, sibling order, transforms, layers and affected visibility before
mutation. Restore surviving sources on release, invalidation, context changes
and disable/destroy; unwind partial attachment failures too. Release the map
before native full-map transitions. Do not edit shared game materials or disable
whole native HUD roots to hide one borrowed leaf.

`UnitMapIconPlanePatch` and `MapMarkerPlanePatch` keep native map icons, target
info and objective markers in the map frame: native code writes world rotations
that only match a flat canvas. Target info is also hidden immediately on the
minimized cockpit map, where native code would show it for its one-second refresh.
`HitMarkerViewPositionPatch` projects hit markers like unit markers instead of
screen pixels.

`NativeMessageFeedPatch` observes the native feed’s refresh into weakly keyed text
snapshots. This avoids the installed TMP StringBuilder getter returning stale
text on later reads. Native queues, filtering, delayed radio lines and expiry
remain authoritative. Compact notification visibility is separate from ownership:
opening a radial/menu/map must not reveal the original floating feed. Opening
chat restores its native message history.

`NativeVrUiRoot.Lifetime` releases discovery, callbacks and pending recenter input
when its presentation ends, while suspended panels keep their selections.
`NativePlayerSettingsAccess` owns native preference writes and apply calls; menu
rows only bind controls to it. `VirtualMouseBindingOwnership` borrows the native
UI actions, restoring each override tuple only if it still matches NOVR's change.
Module replacement and cursor disable/destroy release ownership before removing
the virtual device; unrelated external rebindings are preserved.
`UiPointerOwnership` arbitrates deliberate desktop mouse input against controller
button input before either pointer dispatches events. Idle tracking never takes
the pointer back from the mouse. Active gestures retain their release frame,
and ignored presses cannot become deferred clicks. Desktop and XR focus are
checked independently; hidden or non-VR cursors release the native UI bindings.

## Asynchronous native menus

Workshop queries in `NativeWorkshopPanel.Queries` run one at a time and coalesce
changes to the latest tab/order/page. Each query receives its own result list;
Steam mutates that list after awaiting, so it must never be the displayed list.
Only the current visible request may publish results. Selection owns its preview
cancellation source and author callback, releasing both on deselection or hide.
Accepted subscription/download operations continue when the page closes, but
completion messages belong to the original presentation and selected item. The
shared busy gate always releases, including after failures.

Multiplayer rows refresh independently of user input. Selection follows the
Steam lobby identity, including when refreshed rows use new native objects.
A missing selected server leaves JOIN disabled; it must not select another row
implicitly. Password prompts retain their original lobby and the exact entered
password, including spaces.

`NativeMultiplayerPanel` coordinates source refresh and visibility. Its
`BrowserView`, `HostSetup` and `PasswordView` partials hold their respective view
state; `LobbyData` adapts native metadata. `LobbyBrowserQuery` owns filter/sort
rules and receives search text explicitly, without referencing UI controls.

Hosting retains the mission, scene and native service context that accepted the
request. Hiding the menu does not cancel it, but a context change prevents an old
Steam completion from starting a host. Apply the selected mission only after
lobby creation succeeds. Failed requests may release only their own idle lobby;
`NativeLobbyOwnership` checks the native service and lobby identity before calling
the game's shared leave operation. Its private-field assumptions are also checked
by the optional assembly contracts. These boundaries have source-linked tests:

```sh
python tools/verify.py portable --suite native-menu-handoff --suite native-workshop
```

## Common changes

**A controller binding:** add its identity, defaults, label and collision/context
rules in [`ControllerBindingModel`](../NOVR/Input/ControllerBindingModel.cs).
Wire persistence in [`ControllerProfileBindings`](../NOVR/Input/ControllerProfileBindings.cs)
without renaming existing keys. Add dispatch/gates in `ControllerActions`; use
[`NativeControllerAction`](../NOVR/Input/NativeControllerAction.cs) for
game input intents, translating strings only at the Rewired boundary. The binding
editor consumes the shared model. Check both hardware profiles and handedness:

```sh
python tools/verify.py portable --suite controller-bindings --suite controller-runtime
```

**A native game binding:** this is separate from NOVR's controller profile editor.
[`NativeBindingCatalog`](../NOVR/VrUi/Native/Settings/NativeBindingCatalog.cs)
discovers assignable maps and whole/half-axis slots; `NativeBindingNames` owns
display names. Entries retain the original Rewired map references. The settings
panel applies edits, while `NativeBindingCaptureSession` owns delayed capture and
mapper subscriptions. Check discovery and capture together:

```sh
python tools/verify.py portable --suite native-binding-catalog --suite native-menu-handoff
```

**A HUD widget:** add stable ID, defaults and presentation to
[`HudWidgetCatalog`](../NOVR/VrUi/Widgets/HudWidgetCatalog.cs), then supply its
content through the appropriate source owner. Catalog metadata does not acquire
or restore native objects. Preserve saved IDs and test missing/destroyed sources:

```sh
python tools/verify.py portable --suite hud-widgets --suite wrist-widgets --suite hud-widget-session --suite cockpit-status-sources
```

**Hand fit:** adjust [`Fs20HandPoses`](../NOVR/Hands/Fs20HandPoses.cs),
[`CollectiveHandPoses`](../NOVR/Hands/CollectiveHandPoses.cs) or
[`FreeHandPose`](../NOVR/Hands/FreeHandPose.cs) at the relevant boundary.
Keep grip sockets, mirroring and finger transitions coherent; use
[`ArmPoseMath`](../NOVR/Hands/ArmPoseMath.cs) for arm geometry changes.
Read [hand fit](HAND_FIT.md), then check the shared source before rendered review:

```sh
python tools/verify.py portable --suite hand-pose --suite arm-pose --suite hand-comfort
```

These focused checks do not establish rendered fit, headset comfort or hardware
behavior. Run full verification before submission and report runtime evidence
separately. Prefer a small owner/helper with an explicit lifetime over another
cross-feature framework.

## Rendering cost

The game's shaders contain no single-pass stereo variants (checked across every
compiled variant in the player data), so VR draws each eye as a separate camera
pass; `NOVR.Patcher` and `XrPluginOpenXrToggler` force that mode. Work that is
the same for both eyes is therefore the main thing a mod can still remove:

- NOVR's interface camera (`NOUIManager.ConfigureUiOnly`) asks for no depth
  texture, colour copy or shadows. The game's pipeline enables all three for
  every camera, and each stacked camera otherwise resolves the multisampled
  depth buffer again for each eye.
- [`SharedEyeShadows`](../NOVR/Rendering/SharedEyeShadows.cs) (experimental, off
  by default) keeps the first eye's sun shadow map for the second eye. The pass
  still runs, because URP resets the shadow keywords for every camera, but it
  neither clears nor redraws the map. It falls back to normal rendering unless
  the same pass rendered the same camera's first eye in the same frame.
- `HUDUnitMarkerViewPositionPatch` runs for every unit marker on every frame. It
  reads private fields through field-reference delegates and captures both
  camera poses once per `CombatHUD.UpdateMarkers` call
  (`VrHudProjection.HudView`). Capture a view inside the call that uses it; the
  head pose changes several times a frame.
- `HudElementStyleDriver` refreshes all styled HUD graphics from one callback.

## Searching the HUD and map

The flight HUD's first child (`CombatHUD.iconLayer`) holds one object per unit
marker and the cockpit map holds one per unit icon, so anything that walks
either hierarchy costs time in proportion to the size of the mission.

- Nothing walks a hierarchy every frame.
  [`CockpitWidgetDepthMaterials`](../NOVR/VrUi/Widgets/CockpitWidgetDepthMaterials.cs)
  walks a widget's graphics only when its canvas became (un)available or
  `Transform.hierarchyCount` changed, plus once every `RescanSeconds` for the
  changes that test cannot see (a component added to an existing object, or
  one object replacing another within a frame). A deactivated graphic keeps
  its widget, so toggling an icon needs no scan.
- Periodic searches of the HUD go through
  [`HudHierarchy`](../NOVR/VrUi/Shared/HudHierarchy.cs), which leaves the marker
  layer out. Prefer not searching at all: the weapon widget is read from
  `CombatHUD.weaponState`, readable panels register themselves in
  `HudReadability.Panels`, and the airbase overlay is found once per HUD.
- [`BorrowedLayers`](../NOVR/VrUi/Shared/BorrowedLayers.cs) moves a borrowed
  widget's objects to its slot's layer. Objects it has already moved are
  skipped; one pass in `VerifyEvery` re-checks them all.
- `Turret.FixedUpdate` runs at the physics rate for every turret with a target.
  Its prefix must decide the common case (not under manual control) from one
  field read and allocate nothing.

## Image quality

[`ImageQualitySettings`](../NOVR/Rendering/ImageQualitySettings.cs) holds four
options that cost next to nothing to draw. Each undoes itself when VR stops or
its option is switched off.

- **Dithering** belongs to the base camera of a URP camera stack
  (`VrCameraManager.SyncBaseCameraSettings`); the game's post-processing
  overlay camera applies it in its uber pass.
- [`RestingVignette`](../NOVR/Rendering/RestingVignette.cs) rescales the
  flight profile's vignette so the game's resting 0.4 becomes zero and full
  blackout stays full. It reads the value back each frame instead of patching
  `GLOC`, and follows whichever profile the volume is using without making
  the volume instantiate one.
- [`CockpitScreenFilter`](../NOVR/Rendering/CockpitScreenFilter.cs) gives the
  tactical screen's render texture mipmaps. Two engine facts shape it, both
  measured in the running game: a multisampled render texture gets no mipmap
  levels, and automatic mipmap generation averages 8-bit sRGB values as plain
  numbers. So the screen becomes single-sampled half-float (linear) colour.
  The target window, which shows the outside world, only gains anisotropic
  filtering: a float target would pass highlights the 8-bit one clamps.
- [`TerrainDetail`](../NOVR/VrUi/Map/TerrainDetail.cs) draws the cockpit map's
  terrain from a half-size mipmapped copy while the original is being shrunk
  (`MinificationSwitch`, measured as eye pixels per texel through the headset
  camera). `Image.overrideSprite` does the swap, so the game's own sprite
  reference never changes. The copy's levels are built by repeated bilinear
  halving on the GPU, not by the device's mipmap generation, for the same
  sRGB reason.

A change to any of these needs the engine, not only the doubles: the unit
suites record what each engine call was asked to do, and cannot tell whether
a mipmap level holds the right pixels.

## Optional foveated rendering

`Rendering/FixedFoveationRuntime` probes native Windows/NVIDIA/D3D11 eligibility
before explicitly loading the optional native payload. The native probe must
also confirm VRS hardware support. `FixedFoveationPasses` brackets only the tracked
world camera's forward opaque draw pass; all other camera and pass boundaries
restore full rate. Native scope packets survive asynchronous render submission
and are retired by End. Off with measurements disabled dispatches no steady-state
render events. The DLL remains loaded until process exit so queued callbacks
cannot outlive their code. Never infer performance gains from capability or
command counters; the optional GPU queries and headset comparisons are separate
validation. See [the experiment](PC_FOVEATION.md).

`FixedFoveationPasses` asks URP whether the bracketed colour target is Y-flipped
(`CameraData.IsCameraProjectionMatrixFlipped`), the same answer URP uses to flip
its own projection. The opaque pass draws into an intermediate texture whenever
a camera stack or post-processing is active, and Direct3D stores that upside
down, so the centre is mirrored onto target rows before it reaches the native
rate map. A fixed centre barely moves; an eye-tracked centre would otherwise
travel opposite to vertical gaze.

`FixedFoveationFoliage` patches URP's `DrawObjectsPass.Execute` for the main
camera's forward opaque pass while a scope is open and the costly
**Full-Rate Foliage** opt-in is on (off by default): it narrows the pass to
queues below 2450, ends the scope, and re-executes the pass for 2450-2500 at
full rate, restoring the shared filter in a finalizer. Radii are the presets
from `FixedFoveationPolicy.Radii(level)` for fixed and eye-tracked centres
alike; `FoveationGazeStabilizer` is a pure helper.

`NovrEyeGaze` in the OpenXR input project provides one validated tracking-space
eye/head sample per frame, including actual input-event age. The optional eye
interaction profile is registered before loader initialization and is never
required for other headsets. `FixedFoveationGaze` transforms gaze through the
tracking origin and each eye's actual view and frustum, including canted eyes;
unavailable input retains the fixed centre. Gaze
does not share gameplay targeting state. Native rate-map buffers reuse storage
and texture/view resources, updating only changed tile rectangles.

`HudElementStyle` rescales drawn vertices per element kind: symbols, labels and
reticles use their own sliders times `HudPresentationMath.SymbolFollowFactor`
(Flight HUD Scale over its .38 default when the follow option is on); weapon
readouts multiply Flight HUD Scale directly. Native per-distance marker scaling
remains in the transforms underneath.

`GameGraphicsSettings` exposes only native tree, cloud and shadow setters. It
does not own parallel saved values or apply quality changes on menu creation.
