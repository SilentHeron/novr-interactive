# Controller bindings

NOVR uses the connected **controllers**, not the headset brand, to select its default layout. Use PC SteamVR streaming with a tracked OpenXR session before starting the game. This does not run Nuclear Option directly on a standalone headset.

The virtual stick and throttle work across aircraft. Original instrument displays remain in place; added MFDs are removed. Added cockpit switches and panels are removed. All aircraft use the refined FS-20 stick and grip; helicopters pair it with the complete native utility-helicopter collective lever. Flight actions, including gear, radar and ejection, are also available in View.

## Default right-handed layout

Flight buttons require the assigned handle held or toggle-latched. Target selection and cycling happen on button release. Guns and countermeasures are held actions.

| Action | Steam Frame | Touch / Quest |
| --- | --- | --- |
| Guns | Right trigger | Right trigger |
| Selected missile / bomb release | A | A |
| Select target nearest cue | Right thumbstick click | Right thumbstick click |
| Unselect latest target | B | B |
| Next weapon | Y on stick hand | Y on throttle hand |
| Countermeasures | Left D-pad down | X |
| Wheel / air brake | Left trigger | Left trigger |
| Next countermeasure type | Left D-pad right | Left thumbstick click |
| Clear all targets | View → Flight actions | View → Flight actions |
| Native option wheel, free hand | Thumbstick click | Thumbstick click |
| Tactical map | B with stick released | B with stick released |
| Outside view | Quick wheel → OUTSIDE VIEW | Quick wheel → OUTSIDE VIEW |
| Back to the cockpit from outside | B | B |
| View menu | Left View, or wheel → VR MENU | Physical left Menu, or wheel → VR MENU |
| Reopen menu during Edit layout | Either thumbstick click | Either thumbstick click |

The map remains available after a crash; View opens the recovery map outside a usable cockpit. F9 is the keyboard fallback. Grip, trigger pointer clicks, thumbstick yaw and throttle auxiliary axes retain their existing behavior.

With left-handed HOTAS, role-based actions follow the stick or throttle. The physical left Menu stays the menu button on Touch; its right system button is reserved by the runtime. The guide shows the actual button names for your handedness.

## Edit in the headset

**View** groups flight controls, cockpit layout, display settings and feedback.
Common pages, including **Look direction cue**, open directly from its home page.
Checkboxes switch settings on or off; drag a slider to change the value shown
beside its label. Highlighted choices show the current mode. These settings save
as you change them. Layout placement remains a separate preview with explicit
**Save** and **Cancel**; viewing a settings page does not change a saved layout.
One hand owns the menu pointer. Pointing the other hand at a menu does not take
over; press a button on that hand to switch. Trigger clicks still select the
pointed control. A press or slider drag stays with its starting hand through
release, and the last chosen hand is retained when reopening menus.

**View → Seat & handles → Head clearance** clears nearby seat geometry when it
obstructs your view. New settings start enabled at **25 cm**, adjustable from
**5–40 cm** on the same page. Saved preferences are retained. Only eligible opaque
seat meshes are affected; glass and unsupported cockpit models remain unchanged.

1. Open **View → Controller bindings** or **Settings → Controls → Visual VR Controller Bindings → OPEN**. Choose Left or Right, then select a callout beside the controller image to inspect its assignments, or use the action groups below.
2. Leave **Preset: Auto** for detection, or select Frame / Touch explicitly. Each keeps its own saved bindings.
3. Choose **Weapons & targets**, **Flight & defense**, or **Menu, map & view**.
4. Choose an action, select its hand and button, then **Save binding**. You can select the stick/throttle role or a fixed left/right hand. Unsupported buttons are disabled.
5. If the button is already used, choose another or explicitly replace the listed assignments. Cancel leaves your saved binding intact.
6. **Undo last change** on the bindings page restores the previous edit, including displaced assignments or a default reset. Each preset has its own one-step undo for the current game session.

Map may share a flight button because it yields to a held handle. View requires its own button. Free-hand thumbstick click is reserved for the wheel; it remains assignable to held-handle actions. Gear, radar, flight assist, and hover assist may also be assigned; they start unassigned. Grip and flight axes stay under **View → Flight controls**, separate from button bindings. **Restore defaults** changes only the selected controller preset after confirmation.

Existing Frame custom bindings are retained. Switching between controller families does not overwrite either saved layout. Standard Touch and its Plus/Pro variants have input-profile support; new hardware still needs a real headset check. Trackpad-only Vive layouts and controller-free hand tracking are not covered by this preset.

Nav lights may be assigned like gear and radar. **Change view** (under Menu, map & view) switches between the cockpit and the outside view; like View it works with free hands, needs a button of its own, and cannot use trigger or thumbstick click. Both start unassigned. On Steam Frame the right **Menu** button is free by default and can be chosen for any action.

## Outside view

Click a free thumbstick in the cockpit and choose **OUTSIDE VIEW**, or use **View → Flight actions → Outside view**. The camera becomes the game's orbit camera around your aircraft, in full VR, and you keep flying.

The layout is the game's own gamepad layout, with the grips where a gamepad has its bumpers. It is the same for either handedness.

| Control | Does |
| --- | --- |
| Left thumbstick | Flight stick: back for nose up, left/right to roll |
| Right thumbstick | Looks around the aircraft |
| Left / right trigger | Rudder left / right |
| Left / right grip | Throttle down / up |
| Both grips held + right thumbstick | Camera closer (up) or farther (down); throttle does not move |
| B on the stick hand | Back to the cockpit |
| Thumbstick click | Quick wheel, with **COCKPIT VIEW** |

Every other button acts as bound, without a handle to hold. Three inputs are spoken for out there: the triggers (rudder), thumbstick click and the stick hand's B (the way back), so bindings on them do not act. By default that means guns and the brake, which sit on the triggers. The weapon-release button therefore fires the selected weapon whatever it is, guns included, as the game's gamepad Fire button does; for a brake in the outside view, bind Brake to a button other than a trigger. The wheel opens only when the clicked thumbstick is centred, so pushing hard through a turn cannot pop it open. The throttle keeps its last setting when a grip is released. A thumbstick already pushed, or a trigger or grip already squeezed, when the view starts or a menu closes does nothing until it has been let go once, so the hand that was holding a virtual handle cannot move the throttle. One grip has to be held for a moment before the throttle moves, so closing both for the camera does not nudge it. The first three times, the game's message feed lists these controls.

Thumbstick flight needs motion controls on. With them off, the outside view, **B** and the wheel work the same, and the thumbsticks keep their spectator roles (right orbits, left changes distance).

## Hand option wheel

Outside a cockpit, the wheel offers **VR MENU**, **GAME MENU** and **OPEN MAP / CLOSE MAP**. It adds **COCKPIT VIEW** while you fly from the outside view, and **FREE CAMERA** while you follow a unit as a spectator; with nothing followed, the right stick looks and the left stick flies the free camera, ten times faster with a grip held. On the full-screen map, the left thumbstick moves the map and the right zooms it (swapped when left-handed), and a label above each thumbstick says so while the laser is on the map. The default **B** binding on Frame and Quest also opens/closes the map while spectating; closing keeps your current target. Map controls remain available when the controller pointer is disabled.

Release that hand’s flight handle, then click its thumbstick. The game wheel sits just above the thumbstick, tilted 25° toward the pilot for readability. It stays fixed to the controller as you move your hand. Deflect the stick to highlight an option and return it to center to select. Clicking again closes without selecting, including while an option is highlighted. Trigger switches between the native Options and Weapons pages; recenter before choosing on the new page. The opposite hand can keep flying. **GAME MENU** opens the game's menu; **VR MENU** opens NOVR settings without needing the physical Menu button, which Steam Link may consume on Quest.

Selecting **Eject** shows a red eject symbol in the center of the wheel. Keep the thumbstick centered and hold a fresh stick-click for **1.2 seconds** to confirm. The ring expands and fills as vibration builds on that hand. Releasing early resets the hold; tilting the stick or pressing trigger cancels. Tracking loss, a menu or leaving the cockpit also cancels. Other wheel actions still select on return to center.

## Grip and layout

The floating map has **+ / −** buttons operated by fingertip contact. Touch the map face to select it: a subtle border and centre crosshair appear. While holding the flight stick, use the **physical right thumbstick** to pan, then click it to select an eligible target under the crosshair. The map keeps the game's normal visibility and targeting rules. Touch its face again to return to normal right-stick flight bindings and immediately recenter on your aircraft; zoom is retained. The other buttons, stick tilt and twist keep working. Centre the thumbstick and release its click when taking or returning control; menus, layout editing and tracking loss cancel map selection.

In **View → Flight controls**, choose **Hold** or **Toggle** independently for stick and throttle. Toggle holds after one squeeze and releases on the next. Tracking loss, menus or moving far from a handle releases it.

**Aircraft profiles** contains three slots for the current aircraft’s seat, handles and HUD widgets. Existing layouts stay in Slot 1. Load a slot, edit, then save to the named aircraft and chosen slot. Cancel discards the preview. There are no cross-aircraft shared profiles. During a two-corner widget grab, move and tilt both hands while changing their separation to translate, rotate and resize together; roll remains locked.

During **Edit layout**, click either thumbstick to reopen **Save / Cancel / Continue moving** directly. This replaces the wheel while editing, works while holding a widget, and keeps your unsaved positions. It also works with virtual motion controls off.

For the map, horizontal and vertical corner separation change its width and height independently. This changes the visible area without stretching terrain or unit icons. The shape is saved with the aircraft's selected profile slot.

The throttle’s inboard rail shows a monochrome slider and command percentage, with IDLE / MIL / AB / MAX where applicable. Collective and hover modes use their own captions. The look-direction cue has size, shape, opacity, visibility and vertical-position settings under **View → Look direction cue**, also available in **Settings → HUD → VR Look Direction Cue**. Position is measured above/below the centre of vision and adjusts both the cue and the target-selection ray. Reset restores the defaults. In Near targets mode, already-selected targets do not activate the cue; it appears only when another selectable target is nearby.

In VR cockpit view, a target-select click chooses the eligible marker closest to
the centre of the cue within the acquisition cone. Threat and range priority
cannot pull the selection toward a different off-centre target. Moving the cue
up or down moves the targeting ray with it; size, shape and opacity do not change aim.
Native target filters, paint selection and tactical-map selection are unchanged.

A white outline and brief pulse mark entry into the stick or throttle grab range; the stronger grab cue confirms attachment and the outline disappears. Default grab reach is 15 cm (formerly 12 cm); custom distances are retained. MIL detent holds just below the native afterburner boundary.

MIL detection covers the game's turbojet and turbofan engines, including mods based on those engine types. Engines without an afterburner have no MIL notch. Helicopter collective travel stays continuous: lift to increase the command and lower to decrease it. The complete lever pivots beneath and behind its grip; the saved grip position and configured lift distance remain unchanged.

Stick-limit feedback briefly grows stronger near full pitch, roll or twist input, then stops completely within 0.45 seconds. Holding a tight turn stays silent. Release the grip or ease below 70% travel for a moment to rearm the cue. It uses the normal haptic-strength setting.

Physical stick twist now defaults to a separate **15% yaw deadzone** (4.5° at the default 30° yaw range). Pitch/roll retain their 6% default; the horizontal thumbstick keeps its own existing deadzone. Adjust twist deadzone under **View → Flight controls**.

While holding the throttle or collective, move its thumbstick up/down to adjust the aircraft’s auxiliary control: nozzle tilt, wing sweep or pusher fans. On the Ibis, turn Flight Assist off to retain a manual fan setting; enabled assist resumes native automatic control. Aircraft without an auxiliary system ignore this input.
