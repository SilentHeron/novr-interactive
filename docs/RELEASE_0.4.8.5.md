# 0.4.8.5

- Reduced repeated menu discovery, HUD vertex calculations and mouse-pointer allocations. Image quality and controller bindings are unchanged.
- Fixed UI recovery after loading and camera replacement, duplicate camera wrapping, and stale camera references. Pitch-compass source changes restore correctly.
- Changing maps releases map-stick control and restores ownship following; map setup avoids a redundant hierarchy scan.
- Retired unused eye-gaze diagnostics. VR Performance now shows the renderer, headset render dimensions and desktop mirror size. No PC foveated-rendering backend is included.
- Linux installation no longer runs host Wine against a guessed Proton prefix. It supplies the per-game BepInEx launch option instead; Windows setup is unchanged.
- Fixed VR-library upgrades blocked by the mod loader's own file locks. The new library is validated before replacing the pending assembly; a failed disk update preserves a usable session and reports recovery steps.
- Release packaging checks compiled versions and required files before creating an archive. Source and package checks are configured for both Windows and Linux CI.
- Removed unused compatibility code and replaced obsolete setup instructions with a current Windows/Linux guide.

Validation: 40 portable suites plus the new patcher suite, assembly contracts and
package-safety checks passed on Linux. The release package built successfully; the bundled Unity OpenXR source
still emits existing compiler warnings. A normal Linux/Proton startup verified the
XR-library upgrade and Direct3D 11 VR session. Windows runtime and headset comfort
remain unverified. No measured frame-rate improvement is claimed.
