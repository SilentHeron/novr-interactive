# 0.4.8.4

- Reorganized View around flight, cockpit, visuals and feedback groups. Settings use labeled sliders, checkboxes and visible mode choices; Look direction cue and the other main settings groups open directly from home. Aircraft profiles retain explicit save/cancel behavior.
- Menu pointer ownership stays with one hand until a button press on the other hand. Slider drags remain on their original panel plane and cancel when their page closes, avoiding value jumps or stale drags.
- Increased the default physical stick yaw deadzone to 15%, independently of pitch/roll. Sustained stick-limit haptics now taper after the initial cue and rearm after withdrawing from the limit.
- Tilted the hand-attached option wheel 25° toward the pilot, retaining its hand-relative position and selection directions.
- Proton configures Rewired's legacy XInput backend before initialization rather than resetting it from an initialization callback.
- Camera changes and UI recreation no longer leave stale camera references or omit the cockpit HUD from the active camera stack. Duplicate dispatchers wait for ownership instead of throwing repeatedly.
- Turning VR off and on reinitializes the existing XR loader correctly. Setup failures do not proceed into an uninitialized backend, and background behavior restores after teardown.
- Map control now waits for the thumbstick to enter the flight-control dead zone before handing yaw back. Overlapping zoom-button catch areas choose the nearest button, and lost right-controller tracking cannot briefly reselect the map.
- Fixed the black grid covering map terrain by preserving the native grid's additive blending and world-space depth handling.
- Removed an unused camera-reflection adapter and abandoned camera setup stubs. Camera teardown retains inherited event cleanup.
- Updated portable verification and CI to cover the current controls, widgets and camera lifecycle; removed the retired MFD check reference. Added a contributor guide with build steps, code locations and testing boundaries.
