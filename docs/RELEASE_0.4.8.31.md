# 0.4.8.31 — experimental release

Changes since 0.4.8.23:

- Optional fixed and eye-tracked foveation for native Windows, compatible NVIDIA
  hardware and Direct3D 11. Foveation, eye tracking and GPU measurements default
  to Off. Missing gaze uses a fixed fallback.
- Five strength presets, including Extreme and Maximum test levels. MSAA caps
  their outer shading at 2×2. GPU measurements cover world opaques, not FPS;
  stronger levels still need their own performance and comfort testing.
- Game graphics controls for tree distance, shadow quality and cloud detail,
  using the game's saved settings. Nothing changes until you adjust it.
- Outside-aircraft wheel with VR Menu, Game Menu and Open Map / Close Map.
  B also toggles the map, including when joining directly as a spectator.
- Full VR spectator views with right-stick orbit, left-stick distance and a
  larger notification feed. Closing the map keeps the selected spectator target.
- Removed the translucent backdrop around the VR game/join menu while keeping
  panels, buttons and background click blocking intact.

One package serves Windows and Linux/Proton; foveation stays unavailable on
Linux/Proton. It does not change antialiasing, headset resolution or SteamVR
settings. See [foveation details and comparison steps](PC_FOVEATION.md).
