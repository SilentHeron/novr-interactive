# Compact cockpit alerts — 0.4.6.9 candidate

The cockpit alert overlay places warnings below the forward sightline and recent
kill/reward notifications to the right. It has no background or frame, accepts no
pointer input, and stays fixed relative to the original cockpit viewpoint. Turning
your head or adjusting the seat does not move it. The threat scope disappears when
there are no detected threats. Notification expiry follows the game.

## Reading the scope

- **M**, red-orange: a missile already detected by the aircraft's warning system.
- **R**, amber: a detected radar emitter. Its last reported direction fades over
  four seconds; the overlay does not keep tracking it after the signal expires.
- **J**, blue: a jamming source, expiring after one second without a refresh.
- **12h** is the aircraft nose, **3h** right, **6h** behind, **9h** left. Bearings
  use aircraft axes, including roll; they do not change when you look around.
- Rings separate categories; their radii do **not** represent distance. Missile
  range is shown in the text. HIGH/LOW indicates substantial vertical separation.

Three missile rows prioritize active-lock seekers, then nearest distance. The
header counts all detected missiles and a footer reports additional threats.
This ordering is not a calculated time-to-impact estimate. For radar-guided
missiles, **NOTCH** separately labels the native evasion direction; it is not the
incoming missile bearing. The full-screen world evasion lines are hidden by
default and can be restored independently.

The quiet feed keeps the latest three native lines, with long entries ellipsized
individually. Native rank and reward messages are smaller too. Chat and mission
messages retain their original behavior. Native warning audio, detection,
countermeasures, map pings and missile map vectors continue to run.

## Controls

Open **View → Comfort & display → HUD & display → Threats & feed**:

- Compact ON/OFF switches between the new presentation and the prior overlays.
- Kill feed ON/OFF controls the compact kill feed; reward/rank notices remain.
- Smaller/Larger alerts changes the threat and notification size together.
- Alerts lower/higher moves the overlay in 2.5 cm increments.
- World notch ON/OFF restores or hides the original world evasion cues.
- Reset alerts restores the default compact layout.

The main All HUD smaller/larger buttons also resize these alerts. Close View to
see the result. Defaults are 1.4 m forward and 0.30 m below the original cockpit
viewpoint; size and height persist in the Cockpit Alerts configuration section.
There is no new render camera or render texture. Threat text updates at 10 Hz;
placement follows the cockpit every frame and before rendering.

## Validation and limits

This candidate was built and checked offline. The checks cover bearings, feed
truncation, game field/method compatibility and menu layout. No game launch or
input automation was used for this change. In-headset readability, overlap with
individual dashboards and restoration during aircraft/mission changes still need
user testing. No frame-time improvement is claimed.

The same candidate includes the previously prepared mission-start reentry guard
and stable menu-transition anchors. The installed 0.4.6.8 package is unchanged.
