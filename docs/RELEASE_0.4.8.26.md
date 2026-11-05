# 0.4.8.26 — private test candidate

- A stepped foveation strength slider adds **Extreme** and **Maximum** for fixed
  and eye-tracked use. Existing Off/Mild/Balanced/Strong values are unchanged.
- The test levels shrink the protected region and request 2×2 middle / 4×4 outer
  shading. MSAA or a driver limit caps the outer rate at 2×2; the menu reports it.
- Missing or delayed gaze restores Strong's larger protected region. GPU timing
  labels reflect that fallback and reject mixed profiles/rates.
- No automatic antialiasing, resolution, SteamVR or graphics-setting changes.

The new levels need Windows headset testing. The earlier 0.4.8.25 benchmark
showed reduced shader work, but does not predict their performance or comfort.
