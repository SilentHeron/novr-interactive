# 0.4.8.22

- Fix controllers being hidden when a streaming runtime reports the headset is
  unworn despite a focused VR session and valid tracking. Presence no longer
  blocks controller input; focus and tracking checks still apply.
- Clarify Proton installation: use BepInEx 5 Windows x64 beside the game
  executable, extract NOVR into BepInEx, and launch the game through Steam.

The controller regression reproduces the reported WiVRn startup state using
runtime doubles. Confirmation on the reporter's WiVRn/xrizer setup is pending.
