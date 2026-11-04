# FS-20 multifunction display keys

Historical design record for the 0.4.7.x replacement MFDs. Those displays and
their added cockpit switches have been removed from the current runtime. See
[verification](VERIFICATION.md) for checks that apply to the current source.

Each FS-20 display has four physical softkeys. Read the adjacent on-screen edge
labels: their functions change with that display's page. Previous/next page
controls remain separate, and the minimap remains available as a page.

| Page | Key 1 | Key 2 | Key 3 | Key 4 |
| --- | --- | --- | --- | --- |
| Map | Zoom in | Zoom out | Center on aircraft | Heading up / north up |
| Sensor | HEAD zoom in | HEAD zoom out | HEAD / TRACK | Clear targets |
| Weapons | Previous weapon | Next weapon | Map page | Next countermeasure type |
| Flight | Map page | Engine/fuel page | MENU | SWAP |
| Engine/fuel | Flight page | Systems page | MENU | SWAP |
| Systems | Flight page | Tactical page | MENU | SWAP |
| Tactical | Map page | Sensor page | MENU | SWAP |

**Map:** CENTER resets the pan offset. It does not change which display owns the
left thumbstick. The orientation label shows the current HDG UP or NORTH UP mode;
pressing it changes to the other mode.

**Sensor:** Manual zoom is available only while the display has a verified live
HEAD feed. TRACK retains the aircraft camera's automatic framing. The mode key
offers the next action: HEAD enables head aiming or returns from tracking; TRACK
requires a selected target. CLEAR uses the existing clear-all-targets operation.
It does not fire a weapon. Landing-camera use or unavailable sensor capability
disables incompatible actions and supplies a reason.

**Weapons:** Weapon selection and countermeasure type use the existing native
input actions. Native weapon safety still depends on gear and ground conditions;
there is no new softkey that overrides it. The third key opens the map.

**Page selection:** Flight, Engine/Fuel, Systems and Tactical retain their two
page shortcuts and offer MENU and SWAP. MENU opens a page selector operated by
the same four physical keys. Their labels become PREV, NEXT, OPEN and BACK:
move the highlight through fitted pages, OPEN the highlighted page, or BACK
without changing the current page. SWAP exchanges the left and right pages.
These pages' instruments remain readouts.

**Brightness:** Open **View → Comfort & display → HUD & display → Brightness**
to adjust MFD content and HUD instruments independently from 35% to 100%.
Brightness is no longer assigned to the FS-20 MFD softkeys.

Unavailable keys are disabled with a short explanation. Page shortcuts require
the relevant display content to be fitted. State is checked again when a key is
activated, so an old label cannot authorize a stale operation after tracking,
aircraft or sensor state changes.

## Implementation checks

The retired `tools/MfdContextChecks` harness covered the production four-key
matrix, availability masks, optical modes, bounds and page helpers. It was
removed with the replacement displays and is no longer part of portable checks.
Those earlier checks did not establish physical spacing, native action delivery
or headset readability.
