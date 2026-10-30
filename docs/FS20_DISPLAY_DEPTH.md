# FS-20 physical MFD surfaces

The 0.4.7.5 candidate moves the two MFD pages from the small lower apertures to
larger, taller housings over the upper instrument area. Each screen measures
191.97 × 218.70 mm, extends about 54 mm below the original upper glass and has
about 3.5 times the old display area. The cockpit rendering layer and world-space
UI retain physical depth: hands, controls and bezels can obscure the screens.
No extra display camera or render texture is allocated. Helmet HUD and the
shared console retain their own presentation paths.

`Fs20DisplayLayout` defines a 237 × 270 UI rectangle with uniform 0.00081 scale,
rotation 11.825° and measured upper-plane centers at X=±0.136, Y=0.440,
Z=0.567758 metres in `cockpit_int`. An 8 mm forward normal offset clears the
structural step below the original glass. Exact triangle clipping found the
retained structure reaches 6.146 mm forward inside the screen footprint.
Opaque backing and a narrow physical bezel bridge that gap; these are fixed
cockpit housings, not movable floating panels. Seat/head adjustments never move
them relative to the aircraft. Maps retain round symbols and camera feeds retain
their aspect rather than stretching to the taller rectangle.

Each screen has two physical keys down either side. Their changing screen-edge
labels follow the actual key positions. PREV/NEXT sits below the displays, with
MAP PAN centered between the two pairs. The lower gear/brake/radar/EOTS row
remains in place. Only measured disconnected native details under the new
housings are covered; surrounding cockpit structure is retained. The lower-side
native gear wheel/stem and rotary caps no longer need the previous softkey cover.

Borrowed game widgets live under a persistent NOVR-owned display root. That root
follows the actual cockpit world pose directly, including before rendering. It
is deliberately not a child of the aircraft: destroying an aircraft must leave
time to restore surviving game widgets or dispose widgets whose scene vanished.
Docked widget layers and layout are restored when released. Native display
artwork is suppressed while the integrated presentation replaces it and restored
when released.

PREV/NEXT skips pages not fitted to the current aircraft. Selecting the other
screen's page swaps the pages, maintaining one owner for native instruments.
The Sensor page can share the most recent verified EOTS HEAD render even though
the native camera is restored between frames. It rejects stale or replaced
textures and does not borrow, enable or render the camera itself.

Final 0.4.7.5 runtime validation is pending. Private geometry checks verify exact
cover counts and projected key/glass clearance at the design eye and sampled
10 cm lateral/forward offsets. Those measurements do not prove every possible
seat position or headset comfort. The previous 0.4.7.4 live checks covered the
former lower apertures, uniform scale, rendered occlusion, widget redocking and
verified Sensor feed reuse; they do not validate this new layout. Physical reach,
stereo readability and haptic feel still require a headset test.
