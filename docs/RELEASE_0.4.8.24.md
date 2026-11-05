# 0.4.8.24 — experimental test candidate

- Optional fixed foveated rendering for supported Windows/NVIDIA/DirectX 11
  setups, with Off/Mild/Balanced/Strong in VR performance. Off by default.
- Optional GPU timings and pixel-shader counters for comparing modes. No
  performance gain is claimed until Windows hardware testing is complete.
- One package for Windows and Linux/Proton. Unsupported setups keep the existing
  renderer; layouts, bindings and graphics settings are unchanged.
