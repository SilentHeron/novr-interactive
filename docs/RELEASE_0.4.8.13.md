# 0.4.8.13

- Added **GAME MENU** and **VR MENU** to the free-hand option wheel on every aircraft, including Quest through Steam Link. No X+Y chord is needed.
- Quest / Touch defaults now use the throttle-hand thumbstick click to cycle countermeasure types while holding the throttle. Custom layouts are retained.
- Fixed grabbed HUD panels tilting backward or refusing to rotate from some wrist angles. One- and two-hand adjustment follow physical wrist rotation while keeping panel roll locked.
- Steam launches on Windows and Proton automatically retry once with Direct3D 11 if Unity starts on another graphics backend. The replacement waits for the original process to exit before initializing NOVR; Steam launch settings are unchanged.
- Replaced handle hover tint with a white outline and increased default grab reach from 12 to 15 cm, preserving custom distances.
- Added default controller tables and simple minimap interaction instructions to the project page.

Existing cockpit profiles and SteamVR quality settings are unchanged. The same package supports Windows and Linux/Proton.
