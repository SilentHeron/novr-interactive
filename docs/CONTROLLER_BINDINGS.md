# Controller bindings

NOVR uses the connected **controllers**, not the headset brand, to select its default layout. Use PC SteamVR streaming with a tracked OpenXR session before starting the game. This does not run Nuclear Option directly on a standalone headset.

The virtual stick and throttle work across aircraft. Original instrument displays remain in place; added MFDs are removed. Added cockpit switches and panels are removed. Fixed-wing aircraft use the refined FS-20 virtual handles; helicopters keep native cyclic/collective controls. Flight actions, including gear, radar and ejection, are also available in View.

## Default right-handed layout

Flight buttons require the assigned handle held or toggle-latched. Target selection and cycling happen on button release. Guns and countermeasures are held actions.

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
| Native option wheel, free hand | Thumbstick click | Thumbstick click |
| Tactical map | B with stick released | B with stick released |
| View menu | Left View | Physical left Menu |

The map remains available after a crash; View opens the recovery map outside a usable cockpit. F9 is the keyboard fallback. Grip, trigger pointer clicks, thumbstick yaw and throttle VTOL-angle axes retain their existing behavior.

With left-handed HOTAS, role-based actions follow the stick or throttle. The physical left Menu stays the menu button on Touch; its right system button is reserved by the runtime. The guide shows the actual button names for your handedness.

## Edit in the headset

1. Open **View → Controller bindings** or **Settings → Controls → Visual VR Controller Bindings → OPEN**. Choose Left or Right, then select a callout beside the controller image to inspect its assignments, or use the action groups below.
2. Leave **Preset: Auto** for detection, or select Frame / Touch explicitly. Each keeps its own saved bindings.
3. Choose **Weapons & targets**, **Flight & defense**, or **Menu & map**.
4. Choose an action, select its hand and button, then **Save binding**. You can select the stick/throttle role or a fixed left/right hand. Unsupported buttons are disabled.
5. If the button is already used, choose another or explicitly replace the listed assignments. Cancel leaves your saved binding intact.
6. **Undo last change** on the bindings page restores the previous edit, including displaced assignments or a default reset. Each preset has its own one-step undo for the current game session.

Map may share a flight button because it yields to a held handle. View requires its own button. Free-hand thumbstick click is reserved for the wheel; it remains assignable to held-handle actions. Gear, radar, flight assist, and hover assist may also be assigned; they start unassigned. Grip and flight axes stay under **View → Flight controls**, separate from button bindings. **Restore defaults** changes only the selected controller preset after confirmation.

Existing Frame custom bindings are retained. Switching between controller families does not overwrite either saved layout. Standard Touch and its Plus/Pro variants have input-profile support; new hardware still needs a real headset check. Trackpad-only Vive layouts and controller-free hand tracking are not covered by this preset.

## Hand option wheel

Release that hand’s flight handle, then click its thumbstick. The smaller game wheel sits just above the thumbstick, parallel to its top surface, and tilts with the controller. Deflect the stick to highlight an option and return it to center to select. Clicking again closes without selecting, including while an option is highlighted. Trigger switches between the native Options and Weapons pages; recenter before choosing on the new page. The opposite hand can keep flying.

## Grip and layout

The floating map has **+ / −** buttons operated by fingertip contact. Touch the map face to select it: a subtle border and centre crosshair appear. While holding the flight stick, use the **physical right thumbstick** to pan, then click it to select an eligible target under the crosshair. The map keeps the game's normal visibility and targeting rules. Touch its face again to return to normal right-stick flight bindings. The other buttons, stick tilt and twist keep working. Centre the thumbstick and release its click when taking or returning control; menus, layout editing and tracking loss cancel map selection.

In **View → Flight controls**, choose **Hold** or **Toggle** independently for stick and throttle. Toggle holds after one squeeze and releases on the next. Tracking loss, menus or moving far from a handle releases it.

**Aircraft profiles** contains three slots for the current aircraft’s seat, handles and HUD widgets. Existing layouts stay in Slot 1. Load a slot, edit, then save to the named aircraft and chosen slot. Cancel discards the preview. There are no cross-aircraft shared profiles. During a two-corner widget grab, move and tilt both hands while changing their separation to translate, rotate and resize together; roll remains locked.

For the map, horizontal and vertical corner separation change its width and height independently. This changes the visible area without stretching terrain or unit icons. The shape is saved with the aircraft's selected profile slot.

The throttle’s inboard rail shows a monochrome slider and command percentage, with IDLE / MIL / AB / MAX where applicable. Collective and hover modes use their own captions. The look-direction cue has size, shape, opacity, visibility and vertical-position settings under **Aiming & markers → Look direction cue**, also available in **Settings → HUD → VR Look Direction Cue**. Position is measured above/below the centre of vision and changes only the visible marker. Reset restores the defaults.
A brief pulse marks entry into the stick or throttle grab range; the stronger grab cue confirms attachment. MIL detent holds just below the native afterburner boundary.

Stick-limit feedback is a steady vibration that grows stronger near full pitch, roll or twist input. It uses the normal haptic-strength setting.
