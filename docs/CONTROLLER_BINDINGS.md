# Controller bindings

NOVR uses the connected **controllers**, not the headset brand, to select its default layout. Use PC SteamVR streaming with a tracked OpenXR session before starting the game. This does not run Nuclear Option directly on a standalone headset.

The virtual stick and throttle work across aircraft. Original instrument displays remain in place; added MFDs are removed. Existing FS-20 switches remain optional conveniences. Flight actions, including gear, radar and ejection, are also available in View.

## Default right-handed layout

Flight buttons require the assigned handle held or tap-latched. Target selection and cycling happen on button release. Guns and countermeasures are held actions.

| Action | Steam Frame | Touch / Quest |
| --- | --- | --- |
| Guns | Right trigger | Right trigger |
| Selected missile / bomb release | A | A |
| Select target under gaze cue | Right thumbstick click | Right thumbstick click |
| Unselect latest target | B | B |
| Next weapon | Y on stick hand | Y on throttle hand |
| Countermeasures | Left D-pad down | X |
| Wheel / air brake | Left trigger | Left trigger |
| Next countermeasure type | Left D-pad right | View → Flight actions |
| Clear all targets | View → Flight actions | View → Flight actions |
| Inspection zoom, hold | Physical right bumper | Throttle thumbstick click |
| Tactical map | B with stick released | B with stick released |
| View menu | Left View | Physical left Menu |

Zoom works with the handles held or released. The map remains available after a crash; View opens the recovery map outside a usable cockpit. F9 is the keyboard fallback. Grip, trigger pointer clicks, thumbstick yaw and throttle VTOL-angle axes retain their existing behavior.

With left-handed HOTAS, role-based actions follow the stick or throttle. The physical left Menu stays the menu button on Touch; its right system button is reserved by the runtime. Steam Frame zoom remains on the physical right bumper. The guide shows the actual button names for your handedness.

## Edit in the headset

1. Open **View → Controller bindings**.
2. Leave **Preset: Auto** for detection, or select Frame / Touch explicitly. Each keeps its own saved bindings.
3. Choose **Weapons & targets**, **Flight & defense**, or **Menu, map & zoom**.
4. Choose an action, select its hand and button, then **Save binding**. You can select the stick/throttle role or a fixed left/right hand. Unsupported buttons are disabled.
5. If the button is already used, choose another or explicitly replace the listed assignments. Cancel leaves your saved binding intact.
6. **Undo last change** on the bindings page restores the previous edit, including displaced assignments or a default reset. Each preset has its own one-step undo for the current game session.

Map may share a flight button because it yields to a held handle. View and zoom require their own buttons. Gear, radar, flight assist, hover assist and EOTS may also be assigned; they start unassigned. Grip and flight axes stay under **View → Flight controls**, separate from button bindings. **Restore defaults** changes only the selected controller preset after confirmation.

Existing Frame custom bindings are retained. Switching between controller families does not overwrite either saved layout. Standard Touch and its Plus/Pro variants have input-profile support; new hardware still needs a real headset check. Trackpad-only Vive layouts and controller-free hand tracking are not covered by this preset.
