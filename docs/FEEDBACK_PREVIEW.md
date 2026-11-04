# Try interaction feedback

Open **View → Sound & haptics**. Adjust sound volume and
haptic strength, then point at **Touch**, **Grab**, **Switch**, or
**MIL detent** and click with the trigger. One sample plays in the hand used
to click; you can compare the two controllers without changing handedness.

The samples use the shared touch, grab and switch cues, plus the throttle's
stronger MIL detent cue. They share the actual interaction sounds
and strength settings. Zero sound volume mutes only sound; zero haptic strength
disables only vibration. Controller capability, the game's audio mixer and pause
state still apply.

Holding, hovering, changing a setting or leaving the page does not start a sample.
An incomplete/cancelled click, tracking loss or a replaced controller discards its
pending sample. The sample buttons suppress the normal menu-click cue so it does
not mask the softer touch sound. They never grab a control, move the throttle or
operate an aircraft action. Back, Home and Close retain their normal positions.

## Verification

The standalone SDK 8 project in `tests/FeedbackPreview` compiles the production
preview button, one-shot gate, shared cue profiles, haptic dispatcher and sound
generator against small device/audio doubles. It checks hand routing, single
playback, cancellation, page/menu closure, tracking loss, device replacement,
strength settings, unsupported haptics and distinct generated samples.
From the repository root:

```sh
python tools/verify.py portable --suite feedback-preview
```

These checks cannot establish haptic feel or Unity audio mixing. Confirm cue
comfort and sound volume in Steam Frame during the next headset test.
