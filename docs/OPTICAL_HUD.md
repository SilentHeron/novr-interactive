# Helmet optical targeting HUD (updated 0.4.7.1)

EOTS means **Electro-optical Targeting System**.

Open **View → EOTS HUD** to toggle the borderless head-following optical image.
It starts off. **View → EOTS setup** changes image width (12–26°) and HEAD
magnification (1–6×); defaults are 20° and 2×. The image is a mono sensor feed on
a stereo surface 3 m away. Neither eye projection nor headset FOV is changed.

- **HEAD:** turn your head to aim the optical view. The hollow central circle
  matches target selection; target/location graphics magnify their positions,
  while their glyphs keep the configured readable size.
- **Select:** press and release the configured target button while gripping its
  assigned handle. With the VTOL-style preset this is the stick-hand thumbstick
  click (physical right by default). A successful selection changes to **TRACK**.
- **TRACK:** the helmet image follows your head while the aircraft's native camera
  tracks and automatically frames the selected target or group of targets.
  Selection does not guarantee weapon lock or launch permission.
- **Unlock:** Frame B on the default stick hand removes the latest target and returns to
  HEAD; **Wrist → Flight actions → Clear targets** or the MFD CLEAR key clears the list.
  Frame Y cycles weapons. Custom bindings remain in effect.
  **EOTS setup → Look to aim** resumes HEAD without clearing existing selections.
- **Right bumper:** temporarily replaces the EOTS image with the existing forward
  magnifier. Release restores the EOTS view. These remain separate features.

This is head-directed aiming, not eye-directed aiming. The native selection
system still decides which detected units can be selected; this does not create
new ground-point locks or bypass visibility, exclusions, guidance or safety.
Aircraft without an optical camera show an unavailable message. A native landing
camera takes priority. The HUD suspends during menus, maps, layout editing,
pause, external view, ejection or loss of head tracking.

HEAD borrows the existing optical camera render and texture for that frame,
using the helmet pose. It suppresses stale native sensor annotations for that
render, then restores the camera pose, FOV, clip planes, enabled state, culling
mask and canvas visibility. Native targeting runs with its normal state. TRACK
only mirrors the native feed. No second scene camera or render texture is added;
an otherwise idle optical camera still incurs its usual rendering cost while
HEAD is enabled. Both MFD feed and HUD share the texture.

## Hollow gaze circle

The old persistent center diamond is hidden without changing the native aim
transform. **View → Comfort & display → HUD & display → Aiming & markers →
Aim cue** cycles:

- **CONTEXT** (default): the hollow circle appears near an
  available target or during target selection, then fades away.
- **ALWAYS:** keep the circle visible, brightening with acquisition feedback.
- **OFF:** hide the aim cue. Target diamonds and weapon-specific sights remain.

The translucent circle matches the native HUD color, with a faint dark outline
for contrast against bright skies and sensor imagery. It spans about 1.3° with
an empty center, about 20% smaller than the previous circle. Its maximum opacity
is 42%, further reduced by the HUD instrument brightness setting and contextual
fading. The circle sits about 0.5° lower than the targeting axis to keep the
center of the view clearer; this is a visual offset only and does not move the
selection ray or optical camera.
The optical HEAD image uses the same circle while deliberately active; its size
stays constant as optical magnification changes. OFF hides this too. The circle
does not change the target selection radius. Acquisition feedback means a
selectable candidate, not a missile lock.

The interaction is inspired by the optical HEAD mode documented on page 9 of
[VTOL VR's official manual](https://cdn.akamai.steamstatic.com/steam/apps/667970/manuals/VTOL_VR_Manual.pdf?t=1703226033).
This is an adaptation to Nuclear Option's targeting model, not feature parity.

Build/math/metadata checks pass. Camera borrowing/restoration, native overlay
composition, physical acquisition comfort and streamed frame times require a
headset test. No simulated flight inputs were used for this release.
