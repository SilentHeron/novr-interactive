# 0.4.8.25 — private test candidate

- Optional eye-tracked foveation for Steam Frame on Windows/NVIDIA/D3D11. Enable
  **Follow eye tracking** in View → VR performance → Foveated rendering.
  Missing or stale gaze uses the existing fixed centre; targeting is unchanged.
- Reuses the shading-rate textures as gaze moves, avoiding per-frame recreation.
- **Game graphics** adds tree distance, shadow quality and cloud detail using the
  game's saved settings. No automatic changes or duplicate SteamVR controls.
- One package for Windows and Linux/Proton; foveation remains Windows-only and
  defaults to Off. Eye tracking also defaults to Off.

Fixed foveation was user-tested successfully in 0.4.8.24. Eye tracking needs a
fresh Windows/Steam Frame test; additional FPS gains are not yet measured.
