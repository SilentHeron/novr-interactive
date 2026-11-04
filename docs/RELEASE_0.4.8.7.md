# 0.4.8.7

- Map viewports and HUD opacity/input groups avoid unchanged native property writes. Resizing, terrain changes and restoration still take effect immediately.
- Disabled arms skip sleeve transform and bounds updates. Hand/UI depth rendering reuses each renderer's transform basis across its bones; glove poses and input are unchanged.
- Removed the unused XInput, SteamVR and OpenVR source projects. The full solution now builds on Linux without Visual Studio's C++ tools, and contributor instructions include that build command.
- Headset samples no longer use reflection or box pose values. Calibration and the existing update, late-update and before-render sampling phases are unchanged; unused render-event subscriptions were removed.
- Closing or hiding settings now cancels controller-binding capture, including capture that was waiting to start. Reopening preserves the selected tab and clears the listening prompt.
- Closed native menus skip repeated button-tree scans; reopening still discovers buttons immediately.
- Packages stage current build outputs and declared assets only, excluding stale files left by removed dependencies. Missing game assemblies produce an actionable build error. Setup instructions specify Windows x64 BepInEx on both Windows and Proton.

Focused map, hand-depth, HUD, headset-lifecycle, menu and packaging checks passed
on Linux, as did game-assembly contracts. A full SDK 10 solution build produced
both Windows and Linux installers; existing warnings remain in the bundled XR
source. Package contents and checksum were checked.

Installed and started through SteamVR on Linux/Proton with Direct3D 11 at 90 Hz.
Content loading completed and the native menu returned. Windows runtime and
headset comfort remain unverified. A live flight still showed substantial
rendering delay and intermittent streaming resets; reducing per-game resolution
is being tested separately. No measured frame-rate improvement is claimed.
