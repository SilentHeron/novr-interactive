# NOVR project review — 30 October 2026

Archived development record. Features, recommendations and validation results
below describe earlier builds, not the current implementation. See
[current controls](../CONTROLLER_BINDINGS.md) and [current verification](../VERIFICATION.md).

Reviewed release: **0.4.7.3**, target **PC SteamVR streaming to Steam Frame**.

0.4.7.3 is installed and packaged. The FS-20 has physical touch/flick controls, two seven-page MFDs, fitted glove poses, split gun/weapon inputs, seat and handle positioning controls, stronger feedback, short shaft fades and a targeted cover for overlapping vanilla controls. Other aircraft retain the shared console. PC dynamic foveated rendering is not active.

Release build: 0 errors (32 existing warnings); 123 core checks; 55 patch targets/fields; five UI lifecycle checks; 21-menu/138-button audit; 67 live offline FS-20 checks. Low, side and close inspection views were checked. Native-cover toggle/restore and shaft fades passed. Temporary diagnostic plugins were removed and the clean game reached its menu. These checks do not establish stereo comfort, physical haptic feel, multiplayer correctness or measured performance.

Reviewed project-owned initialization, input/interaction, cockpit geometry and lifecycle, hands/IK, MFD/HUD/camera presentation, audio/haptics, capability reporting, installer/build paths, tests and documentation. This is a project-state and implementation-priority review, not a line-by-line audit of bundled third-party XR libraries. No additional recommendations below have been implemented as part of this review.

This is a historical review of that release, not a list of open work. Source
links lead to surviving files in the current tree; retired source names remain
as text. Implementation details and validation coverage have changed since review.

## Recommended implementation order

### 1. Protect the fork during updates
*Confirmed gap · medium*

The installer still requests InfernoSuperNova/novr releases. Its update routine deletes the old plugin and patcher directories before copying the new package. Ordinary builds can also deploy directly into the detected game installation.

**Implement:** Give this fork an explicit release channel, verify its package manifest/hash, stage updates before replacing files, preserve configuration and provide rollback. Make game deployment an explicit build choice.

**Acceptance:** A failed or interrupted update leaves the previous working version usable; the installer cannot silently substitute the upstream build.

Evidence: [NOVR.Installer/InstallerConstants.cs](../../NOVR.Installer/InstallerConstants.cs); [NOVR.Installer/Services/FileSystemInstaller.cs](../../NOVR.Installer/Services/FileSystemInstaller.cs); [NOVR.Build/NOVR.Build.targets](../../NOVR.Build/NOVR.Build.targets).

### 2. Make the integrated MFDs obey cockpit depth
*Confirmed rendering limitation · large*

The MFD widgets use the separate UI layer and an overlay camera that clears depth. The foreground-hands workaround is enabled for the generic movable console, not the fixed FS-20 cluster. This limits natural occlusion between displays, housings and hands. The inspection views also show screen edges needing closer alignment with the native apertures.

**Implement:** Render the two FS-20 displays on depth-tested cockpit surfaces, with fitted bezels and clipping to the apertures. Keep helmet HUD and menus on their appropriate separate presentation paths.

**Acceptance:** Both eyes agree; a hand in front covers the display, a hand behind does not; screen pixels never spill over button labels when leaning low or sideways.

Evidence: [NOVR/VrUi/NOUIManager.cs](../../NOVR/VrUi/NOUIManager.cs); `NOVR/MotionControls/CockpitForeground.cs` (retired source); `NOVR/MotionControls/CockpitPanelHud.cs` (retired source).

### 3. Bring the live regression checks into the project
*Confirmed coverage gap · medium*

The repository has useful pure-logic and patch-compatibility checks, but the 67 live FS-20 checks used for this release are still in the local diagnostic workspace. No tracked CI workflow currently runs them or the portable checks.

**Implement:** Promote a reproducible, opt-in test runner and release manifest into the repository. Run portable checks in CI; require a locally installed game for runtime checks. Add scene-switch, reconnect, pause, recenter, handedness and multiplayer input cases. Keep diagnostic plugins out of normal packages.

**Acceptance:** A fresh checkout can reproduce the portable tests and documented local mission checks without redistributing game assets. Include a deliberate offline eject test and tests proving ordinary handle grabs cannot eject.

Evidence: [tests/Core/NOVR.Tests.csproj](../../tests/Core/NOVR.Tests.csproj); [tests/Core/PatchCompatibility.cs](../../tests/Core/PatchCompatibility.cs); [NOVR/Input/Gestures/CockpitTouchGate.cs](../../NOVR/Input/Gestures/CockpitTouchGate.cs).

### 4. Expose content readiness before mission launch
*Confirmed missing readiness check; freeze risk needs reproduction · medium*

The mission launch gate prevents duplicate sessions and handles failures, but checks the network manager/session state rather than third-party content readiness. This host’s recent Blueprinter startup took about 73 seconds. The private test launcher waits for content completion; that safeguard is not part of the normal menu.

**Implement:** Add a readiness adapter for supported content loaders and a clear preparing-content status. Preserve retry/failure handling and avoid interpreting a quiet log or elapsed time as proof of readiness.

**Acceptance:** Rapid launch attempts during cold startup cannot race content initialization. A failed addon produces a useful status instead of an indefinitely frozen-looking menu.

Evidence: [NOVR/VrUi/Native/Menus/NativeSinglePlayerMissionPanel.cs](../../NOVR/VrUi/Native/Menus/NativeSinglePlayerMissionPanel.cs); [NOVR/VrUi/Native/Startup/MissionLaunchGate.cs](../../NOVR/VrUi/Native/Startup/MissionLaunchGate.cs).

### 5. Calibrate each hand and improve arm reach
*Confirmed limitation; comfort requires headset feedback · medium/large*

Free-hand offsets are one mirrored pair of settings. The FS-20 grip joints are fitted, but the arm solver hides a sleeve immediately when the controller exceeds the model’s reach. The current live checks primarily exercise the right-handed layout.

**Implement:** Add a short per-controller calibration for palm orientation and fingertip reach, with individual left/right offsets. Add shoulder/reach calibration, elbow limits and a smooth sleeve transition at the reach boundary. Keep held-hand anchors fixed to the controls.

**Acceptance:** Natural upright controllers show straight wrists; fingertip contact matches the visible finger; arms do not abruptly pop in/out during ordinary reaches. Verify both handedness modes and the collective separately.

Evidence: [NOVR/FlightControls/MotionControlSettings.cs](../../NOVR/FlightControls/MotionControlSettings.cs); [NOVR/Hands/PilotHandVisuals.cs](../../NOVR/Hands/PilotHandVisuals.cs); [NOVR/Hands/Fs20HandPoses.cs](../../NOVR/Hands/Fs20HandPoses.cs).

### 6. Unify which interaction owns each hand
*Design risk, not a newly reproduced accidental-ejection bug · medium*

Flight grips, cockpit switches, layout movement, helmet gestures, wrist menus and pointers each have their own gates and ownership checks. Existing fresh-press/release guards are valuable, but coordination is spread across components.

**Implement:** Introduce one explicit hand-ownership policy and cancellation path. Define priority for held flight controls, deliberate cockpit grabs, setup and helmet gestures. Preserve the immediate physical eject pull; do not restore a hold timer.

**Acceptance:** A press has exactly one owner. Tracking loss, a menu opening or a seat/layout change releases it predictably. Crossing another control while gripping never activates that control.

Evidence: `NOVR/MotionControls/Fs20NativeCockpit.cs` (retired source); [NOVR/FlightControls/MotionFlightController.cs](../../NOVR/FlightControls/MotionFlightController.cs); [NOVR/Input/Gestures/HelmetNightVision.cs](../../NOVR/Input/Gestures/HelmetNightVision.cs).

### 7. Measure performance, then tune the expensive paths
*Measurement gap with concrete candidates · medium*

No CPU/GPU frame-time benchmark was established by the visual checks. The magnifier adds a 1024×640 world render while held. MFD discovery/reflection runs periodically, widget fitting runs every frame, and the generic console uses an extra foreground pass. The optical HUD already reuses the native sensor camera.

**Implement:** Create repeatable Frame streaming benchmarks for normal flight, busy combat, zoom and EOTS. Record CPU/GPU frame times, missed frames and allocations. Then cache discovery/reflection, update layout only when needed and add adjustable refresh/resolution budgets where measurements justify them.

**Acceptance:** Compare the same mission and graphics settings before/after. Keep text readable, eye projections unchanged and input latency stable; report measured gains instead of promising an FPS number.

Evidence: `NOVR/MotionControls/CockpitViewZoom.cs` (retired source); `NOVR/MotionControls/CockpitPanelHud.cs` (retired source); `NOVR/MotionControls/OpticalCameraLease.cs` (retired source).

### 8. Give MFD pages useful physical controls
*Feature extension · medium*

The seven pages, separate PREV/NEXT keys and left-stick minimap panning are implemented. Page availability is known, but the integrated page keys still cycle the whole list. A sensor page can currently show no active image.

**Implement:** Add contextual bezel controls: map zoom, north-up/heading-up and recenter; sensor zoom and clear lock/status indication; quick return to a preferred page. Offer skipping unavailable pages. Keep touch controls physical and avoid adding controller rays to the FS-20 cluster.

**Acceptance:** The minimap remains reachable in one simple action, common map tasks work without opening the wrist menu, and unavailable sensors do not look like a broken display.

Evidence: `NOVR/MotionControls/CockpitPanelHud.cs` (retired source); `NOVR/MotionControls/Fs20PanelLayout.cs` (retired source); `NOVR/MotionControls/MfdPages.cs` (retired source).

### 9. Finish the combat HUD and feedback as one workflow
*Usability refinement; headset validation required · medium*

Marker scaling, compact threats, head-aimed EOTS, a frameless magnifier, trigger guns/A release, distinct sounds and stronger haptics are already present. Their combined behaviour in busy combat is not covered by the current cockpit screenshots.

**Implement:** Test acquisition, multiple locks, unlock/clear, gun and missile release, incoming threats and magnifier use together. Add contextual label decluttering and a clear selected-target hierarchy. Tune touch/grab/switch/detent cues individually, including an optional in-menu feedback preview.

**Acceptance:** Threat warnings remain visible without covering aim; lock state and release readiness are clear; capacitive touch never fires; sounds/haptics remain distinguishable without continuous buzzing.

Evidence: [NOVR/FlightControls/SeparateWeaponControls.cs](../../NOVR/FlightControls/SeparateWeaponControls.cs); [NOVR/VrUi/Hud/HudElementStyle.cs](../../NOVR/VrUi/Hud/HudElementStyle.cs); [NOVR/Feedback/InteractionHaptics.cs](../../NOVR/Feedback/InteractionHaptics.cs).

### 10. Make compatibility and settings easier to maintain
*Confirmed maintenance debt · medium*

The FS-20 control cover intentionally matches one known cockpit topology. Failure safely retains native geometry but is mainly logged. Some settings descriptions predate the new physical fingertip path, and native-control extraction code remains although its old constructor is no longer used.

**Implement:** Centralize aircraft geometry signatures, grip sockets, control layouts and native field access. Show a concise compatibility status when a game update disables an enhancement. Generate controls/help text from shared metadata, retire unused extraction paths and provide setup export/restore.

**Acceptance:** A changed game mesh leaves a usable cockpit with an understandable notice; documentation matches actual inputs; resetting hand calibration does not erase seat or handle placement.

Evidence: `NOVR/MotionControls/Fs20ControlCover.cs` (retired source); [NOVR/FlightControls/NativeCockpitMesh.cs](../../NOVR/FlightControls/NativeCockpitMesh.cs); [NOVR/Profiles/CockpitProfiles.cs](../../NOVR/Profiles/CockpitProfiles.cs).

### 11. Extend the native-cockpit approach to another aircraft
*Expansion after FS-20 acceptance · large per aircraft*

The integrated physical cluster, tailored handle poses and shaft fades are FS-20-specific. Other aircraft still use the shared console and existing grip poses.

**Implement:** Use the accepted FS-20 implementation as the foundation for a data-driven aircraft adapter. A sensible next candidate is the Revoker; follow it with a VTOL/helicopter cockpit that exercises nozzle and collective differences. Measure each cockpit rather than stretching the FS-20 layout.

**Acceptance:** The new aircraft has its own fitted control locations, MFD apertures, poses and geometry signature, while sharing interaction logic and preserving seat-independent placement.

Evidence: `NOVR/MotionControls/Fs20NativeCockpit.cs` (retired source); `NOVR/MotionControls/Fs20PanelLayout.cs` (retired source); [NOVR/FlightControls/NativeControlModel.cs](../../NOVR/FlightControls/NativeControlModel.cs).

### 12. Keep PC dynamic foveation as a separate research milestone
*Not implemented; backend prerequisite · research*

The clean 0.4.7.3 startup reports SteamVR/OpenXR 2.18.4, Direct3D11, Unity foveation capabilities None, eye-gaze extension enabled and no eye-foveation provider. The project reports PC DFR as NOT ACTIVE. This describes this application path, not every possible Steam Frame setup.

**Implement:** First prove a compatible PC rendering backend and live per-eye gaze. Prototype it separately, with full-resolution cockpit text and immediate full-resolution fallback on invalid tracking. Do not replace the current comfortable stereo path merely to expose a DFR toggle.

**Acceptance:** Demonstrate GPU frame-time savings in the headset with correct stereo, readable MFDs and stable gaze-loss recovery. Foveated video streaming alone does not satisfy this milestone.

Evidence: [NOVR/Compatibility/PcVrCapabilities.cs](../../NOVR/Compatibility/PcVrCapabilities.cs); [docs/PC_FOVEATION.md](../PC_FOVEATION.md).

## Boundaries for the next batch

Preserve the current A/trigger bindings, physical switch/button interaction, immediate deliberate seat-handle ejection, fixed aircraft cluster and seat-independent control placement. Do not reintroduce broad seat/cockpit masking or whole-view zoom. Get the current FS-20 headset feedback before expanding aircraft support. Preserve custom settings and unrelated mods during updates.
