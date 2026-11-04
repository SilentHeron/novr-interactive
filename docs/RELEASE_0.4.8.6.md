# 0.4.8.6

- Threat alerts update existing rows and pins instead of rebuilding unchanged content every refresh. Threat ordering, expiry and notch details are unchanged.
- Released redundant glove and fabric source textures after conversion. Failed hand-material initialization now releases partial allocations before retrying.
- Install and uninstall operations share a lock, preventing a second installer from removing files during an update. Full BepInEx removal closes its legacy lock before deleting that folder on Windows.
- Removed an unused asset-editing library, class database, old OpenVR action payloads and obsolete installer files. The mod continues to use OpenXR through SteamVR on Windows and Linux/Proton.

Validation: focused alert, material, texture, installer and XR-payload checks,
verification-runner tests and game-assembly contracts passed on Linux. The package
and both Windows/Linux installer executables built; package contents and checksum
were checked. Windows execution remains unverified. This candidate has not been
installed or tested in a headset; no frame-rate improvement is claimed.
