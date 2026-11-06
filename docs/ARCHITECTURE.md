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
3. [`Core.Awake`](../NOVR/Core.cs) registers the persistent feature components.
   `Core.Start` creates headset/UI state and enables XR through
   [`VrTogglerManager`](../NOVR/VrTogglers/VrTogglerManager.cs).
   [`XrPluginOpenXrToggler`](../NOVR/VrTogglers/XrPluginOpenXrToggler.cs) registers
   controller profiles and selects multipass before loader initialization.
4. [`VrCameraManager`](../NOVR/VrCamera/VrCameraManager.cs) discovers scene cameras
   and owns their tracked replacements. [`APIBus`](../NOVR/APIBus.cs) publishes
   camera changes; [`NOUIManager`](../NOVR/VrUi/NOUIManager.cs) attaches VR UI as
   scene/menu objects appear. Startup does not imply an aircraft or HUD is ready.

[`SpectatorCameraInput`](../NOVR/VrCamera/SpectatorCameraInput.cs) adds gated
thumbstick orbit/distance input while following a unit in VR. Native camera motion
keeps its target-scaled external distance, collision and smoothing; the tracked
child remains the stereo view. Input requires neutral after menus or target
changes and excludes the spawn map and aircraft selection. The outside-aircraft
wheel owns its menu/map actions without borrowing the aircraft's native wheel.
Native wheel blocking checks require an active/enabled owner: its child can
remain activeSelf under the inactive spectator HUD before the first spawn.
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
native markers by angle from the adjusted cue ray. `GazeCueRay` derives this ray
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

`NovrEyeGaze` in the OpenXR input project provides one validated tracking-space
eye/head sample per frame, including actual input-event age. The optional eye
interaction profile is registered before loader initialization and is never
required for other headsets. `FixedFoveationGaze` transforms gaze through the
tracking origin and each eye's actual view and frustum, including canted eyes;
unavailable input retains the fixed centre. Gaze
does not share gameplay targeting state. Native rate-map buffers reuse storage
and texture/view resources, updating only changed tile rectangles.

`GameGraphicsSettings` exposes only native tree, cloud and shadow setters. It
does not own parallel saved values or apply quality changes on menu creation.
