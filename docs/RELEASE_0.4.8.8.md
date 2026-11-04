# 0.4.8.8

- Helicopters share the complete native utility-helicopter collective, including its bent shaft and grip. The hand sits near the buttons with an upright wrist, supported palm and relaxed thumb. Saved placement and configured vertical travel are retained.
- MIL detents now recognize afterburning turbofans as well as turbojets and their subclasses. Late-initializing engines are rechecked while the throttle is released; held controls keep their existing mapping. Dry engines and helicopter collectives stay continuous.
- Sleeve roll is shared across the arm, and the separate sleeve mesh no longer retains torso weights that caused it to pinch during wrist twists. Glove poses and the solved wrist and elbow positions are preserved.
- BDF now uses green sleeves and blue gloves. PALA retains black sleeves and red gloves; fabric detail and glove seams are preserved.
- Stick-limit feedback builds briefly with deflection, then stops within 0.45 seconds. Holding near the limit stays silent until the grip is released or the stick returns below 70% travel for a moment.

Focused control, hand/arm, texture and installed-game contract checks passed.
The full solution built both Windows and Linux installers. Collective poses
were reviewed against the installed native geometry in offline renders;
headset comfort, haptic feel and Windows runtime still need testing.
