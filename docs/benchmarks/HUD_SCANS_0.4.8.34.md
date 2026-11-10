# HUD and map scans, reflection: 0.4.8.34 measurements

What the work that grows with the number of units cost before and after the
0.4.8.34 changes, timed inside the game's own Unity 2022.3.62f2 runtime.

**Read these as costs of the code, not as a frame-rate result.** Everything was
timed at the main menu on Linux/Proton with no headset, on hierarchies built
for the purpose in the game's shape: a HUD whose first child holds one object
per unit marker ahead of about 150 instrument objects, and a cockpit map
widget holding one graphic per unit icon. Real missions were not measured, and
their object counts are not known; the three sizes below are stand-ins. Each
value is the fastest of five runs of a loop of 50 to 300 calls, in
microseconds.

"NOVR" rows call the mod's own code: the 2dc034b build for "before" and the
build with these changes for "after". "Replica" rows time a copy of the old
loop beside the new code, because the old code is private to classes that need
a live aircraft.

## Work done on every frame

| | Source | 250 units | 500 units | 1,000 units |
| --- | --- | ---: | ---: | ---: |
| Widget depth pass, before | NOVR | 61.9 | 107.6 | 191.6 |
| Widget depth pass, after, nothing changed | NOVR | 3.5 | 3.5 | 3.5 |
| Widget depth pass, after, on a frame an object arrived or left | NOVR | 65.4 | 111.6 | 197.3 |
| Styled HUD graphics refresh, before | NOVR | 14.4 | 28.9 | 57.7 |
| Styled HUD graphics refresh, after | NOVR | 10.2 | 19.0 | 37.8 |

The depth pass still does its full walk when a widget's hierarchy changes size
and four times a second regardless, so its cost on those frames is unchanged.

## Work done twice a second

| | Source | 250 units | 500 units | 1,000 units |
| --- | --- | ---: | ---: | ---: |
| Map layer refresh, before | Replica | 42 | 85 | 181 |
| Map layer refresh, after | NOVR | 13 | 28 | 62 |
| Map layer refresh, after, the one full pass in ten | NOVR | 29 | 60 | 129 |
| Walk for the airbase overlay, before | Replica | 71 | 123 | 225 |
| Walk for the airbase overlay, after | | once per HUD | once per HUD | once per HUD |
| One component search of the whole HUD, before (four ran) | Replica | 12 | 27 | 56 |
| One search that leaves the marker layer out, after (two run) | Replica | 6 | 6 | 6 |
| Name search for a status panel the aircraft lacks, before | Replica | 101 | 169 | 302 |
| The same search without the marker layer, after | Replica | 35 | 35 | 35 |

## Reading a private game field

| | Time per read | Allocated per read |
| --- | ---: | ---: |
| `FieldInfo.GetValue`, bool, float or Vector3 | 0.13 to 0.15 | 32 bytes |
| `FieldInfo.GetValue`, object reference | 0.11 | 0 |
| `FieldInfo.SetValue`, float | 0.19 | 32 bytes |
| `Traverse.Create(x).Field(name).GetValue<bool>()` | 0.46 | 366 bytes |
| Typed field reference, read or write | under 0.01 | 0 |

The `Traverse` row is what the old `Turret.FixedUpdate` prefix did first for
every turret that has a target, at the game's 60 Hz physics rate: about 22 KB
of garbage and 28 microseconds per engaging turret per second.

## What it adds up to

For the 500-unit column at 90 frames a second, the every-frame rows fall from
about 137 to about 28 microseconds (counting the four periodic depth walks),
and the twice-a-second burst from about 315 to about 40. That is roughly
0.12 ms of an 11.1 ms frame, about 1%; the 1,000-unit column comes to about 2%.
The field reads are a few microseconds a frame on top.

The frame rate in VR is not expected to change from this. The earlier
[foveation benchmark](FOVEATION_0.4.8.25.md) recorded 10.4 ms of GPU time per
frame against an 8.3 ms budget at 120 Hz, so the GPU sets the frame rate on
that system. What this removes is main-thread time, a burst twice a second,
and a steady source of garbage for the collector.

## Checks made in the same runs

- `Transform.hierarchyCount`, which the depth pass now tests, counts every
  transform under the same root: it rose by one for a child or grandchild,
  fell by one the frame after `Destroy`, followed an object moved to another
  root, and ignored `SetActive`. One object destroyed and one created in the
  same frame left it unchanged, which is why the periodic walk remains.
- `GetComponentsInChildren(bool, List<T>)` replaces the list's contents.
- For each of seven size settings, changed and changed back, against nine
  kinds of styled graphic (126 cases), the per-frame change test marked a
  graphic for rebuild exactly when the scale its mesh is built with had
  changed.
