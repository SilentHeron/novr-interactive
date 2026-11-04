# 0.4.8.7

- Map viewports and HUD opacity/input groups avoid unchanged native property writes. Resizing, terrain changes and restoration still take effect immediately.
- Disabled arms skip sleeve transform and bounds updates. Hand/UI depth rendering reuses each renderer's transform basis across its bones; glove poses and input are unchanged.
- Removed the unused XInput, SteamVR and OpenVR source projects. The full solution now builds on Linux without Visual Studio's C++ tools, and contributor instructions include that build command.

Focused map, hand-depth and HUD checks and game-assembly contracts passed on
Linux. A full SDK 10 solution build produced both Windows and Linux installers;
existing warnings remain in the bundled OpenXR source. Package contents and
checksum were checked. This candidate is not yet installed or headset-tested.
No measured frame-rate improvement is claimed.
