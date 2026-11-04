# Contributor architecture

NOVR adapts the running game's cameras, controls and UI. Native aircraft logic
and cockpit displays remain authoritative. Start with [CONTRIBUTING](../CONTRIBUTING.md)
for builds and [verification](VERIFICATION.md) for executable checks and their limits.

## Startup and ownership

1. [`NOVR.Patcher`](../NOVR.Patcher/Patcher.cs) installs the XR payload before
   ordinary plugins load. Its managed upgrade path handles assemblies already
   opened by the preloader; see the [vendor boundary](XR_VENDORING.md).
2. [`NOVRPlugin`](../NOVR/NOVRPlugin.cs) publishes configuration, initializes
   compatibility handling, applies Harmony patches and creates `Core`.
3. [`Core.Awake`](../NOVR/Core.cs) registers the persistent feature components.
   `Core.Start` creates headset/UI state and enables XR through
   [`VrTogglerManager`](../NOVR/VrTogglers/VrTogglerManager.cs).
   [`XrPluginOpenXrToggler`](../NOVR/VrTogglers/XrPluginOpenXrToggler.cs) registers
   controller profiles and selects multipass before loader initialization.
4. [`VrCameraManager`](../NOVR/VrCamera/VrCameraManager.cs) discovers scene cameras
   and owns their tracked replacements. [`APIBus`](../NOVR/APIBus.cs) publishes
   camera changes; [`NOUIManager`](../NOVR/VrUi/NOUIManager.cs) attaches VR UI as
   scene/menu objects appear. Startup does not imply an aircraft or HUD is ready.

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
  `PilotHandVisuals` then stabilizes free hands and solves sleeves. Snapped/smoothed
  meshes and capacitive finger animation must never feed back into contact tests,
  grip acquisition or button presses.

## Borrowed native UI

[`CockpitStatusSources`](../NOVR/VrUi/Status/CockpitStatusSources.cs),
[`CockpitMapWidget`](../NOVR/VrUi/Map/CockpitMapWidget.cs) and
[`NativeRadialMenuLease`](../NOVR/VrUi/RadialMenu/NativeRadialMenuLease.cs) borrow
live game objects rather than duplicating their events or action lists. Capture
original parents, sibling order, transforms, layers and affected visibility before
mutation. Restore surviving sources on release, invalidation, context changes
and disable/destroy; unwind partial attachment failures too. Release the map
before native full-map transitions. Do not edit shared game materials or disable
whole native HUD roots to hide one borrowed leaf.

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

**A HUD widget:** add stable ID, defaults and presentation to
[`HudWidgetCatalog`](../NOVR/VrUi/Widgets/HudWidgetCatalog.cs), then supply its
content through the appropriate source owner. Catalog metadata does not acquire
or restore native objects. Preserve saved IDs and test missing/destroyed sources:

```sh
python tools/verify.py portable --suite hud-widgets --suite cockpit-status-sources
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
