# Hand fit and sleeve reach

Open **View → Hands & arms**, then choose **Fit left hand** or
**Fit right hand**. Hold the hand being fitted comfortably, with its thumb up,
and use the other controller to make adjustments.

- **Position:** use the sliders to move the glove in 5 mm steps along the controller's left/right,
  up/down or forward/back axes. Start with the wrist, then check the fingertips.
- **Wrist & palm angle:** use the sliders to adjust tilt, turn and roll in 1°
  steps. The **Upright · thumbs up** preset gives the handshake pose; **Down**
  gives the horizontal-palm pose.
- Changes save automatically for that physical hand, across aircraft and stick
  handedness. Changing the left hand never changes the right hand.
- **Reset this hand**, **Reset position** and **Reset angle** remove only the new
  corrections. They restore the existing shared fit, including previous custom
  settings. The other hand is unaffected.

The visible free hand and the physical cockpit touch point use the same fit.
Menu pointers, flight input, placed control positions, and fitted stick, throttle
and collective grips keep their existing anchors. Releasing a handle returns the hand
to its calibrated free pose.

The previous `Glove Wrist Offset` and `Glove Wrist Rotation` settings remain the
baseline. New left/right corrections default to zero, so upgrading does not
replace an existing fit. Position corrections are limited to ±15 cm per axis and
angle corrections to ±180°; malformed corrections fall back to zero.

## Sleeves

Normal arm reach retains the original opaque sleeve material. Near full reach,
the render-only sleeve can extend by up to 10%, capped at 6 cm, while smoothly
fading out. The cuff remains with the glove throughout that visible transition.
The glove always follows tracking, even after the sleeve is out of reach.
An extreme fold into the shoulder also fades through the small invalid reach
range. This affects local sleeve copies only; the native pilot and cockpit are
unchanged. Unsupported material shaders retain their original appearance.

## Animation and materials

The arm solver stabilizes the elbow bend through wrist turns and shares sleeve
roll across the upper arm and forearm to reduce twisting at the elbow.
Collective cuffs face upward toward the shoulder.
Grip entry takes 120 ms; release and touch-gesture changes take 90 ms. Once
attached, hands stay fixed to the control. These visual transitions do not delay
flight input or move the tracked contact points.

The FS-20 trigger finger flexes slightly under pressure. Smoothed glove shading
removes seams at matching UV boundaries without changing the mesh shape.
Sleeves use a finer fabric tile, filtered into a copy of the native atlas at up
to 2048 pixels per side. The original markings, colors, normal maps and UV layout
are preserved. Texture preparation runs once per source atlas on a background
worker; GPU readback and upload still occur on Unity's main thread.

## Validation

From the repository root:

```sh
python tools/verify.py portable --suite hand-comfort --suite hand-pose --suite arm-pose
```

The focused checks cover fitted poses, arm geometry, correction bounds, invalid data, ordinary reach,
continuous fade through the former reach cutoff, bounded extension, and 2,001
samples across the transition. They do not prove headset comfort or shader
appearance; those require the in-game integration checks.

The installed 0.4.7.4 integration checks confirm independent fitting, fingertip
alignment, fixed held grips, and actual partial transparency with the native
sleeve shader. Baked sleeve vertices were independently compared with four-bone
world skinning before bounds containment was checked across five free reaches
and the fitted held control on each side. This matches the tested game
configuration (Auto renderer quality with FourBones global skin weights).
