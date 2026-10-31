# 0.4.7.10

- Separate Steam Frame and Touch / Quest presets, selected from connected controllers. Existing Frame bindings are preserved; each preset saves its own changes.
- **View → Controller bindings** edits each action's button and hand, with actual controller labels, conflict replacement, cancel and per-preset reset. Gear, radar, flight assist, hover assist and EOTS can also be assigned.
- Touch defaults: trigger guns, A weapon release, B unselect/map, stick click select, X countermeasures, Y next weapon, throttle trigger brake, throttle thumbstick click zoom, physical left Menu for View. Frame keeps its current layout and right-bumper zoom.
- Standard Touch, Touch Plus and Touch Pro input support; right system button remains reserved. Tracking/profile changes require a fresh press before actions resume.
- Removed replacement MFDs and shared console. Original aircraft displays retain their normal owners and updates. Existing FS-20 switches, seat controls and handle locks remain.
- KR-67 Ifrit: direct grip on its original gear wheel and striped ejection handles, with hand poses, sounds, haptics and retrigger protection. No cockpit geometry or display changes. The original gear mesh is static; no new animation is added.

Validation: package builds; 166 core/assembly checks pass against Nuclear Option 0.34.6, including 66 native patch targets. Controller bindings pass 376 focused assertions and input sampling/profile registration pass 74. Native geometry/contact checks pass for FS-20 restoration and KR-67 anchors. No new game session was launched; Touch hardware, menu usability and Ifrit reach need headset testing.
