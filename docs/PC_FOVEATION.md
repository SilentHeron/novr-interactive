# PC foveation experiment: discontinued

The private PC-side foveated-rendering experiment was stopped on 3 November 2026
after testing introduced stutter without a clear visible benefit. NOVR has no
active PC foveation backend, and this is not an active implementation goal.

The experimental rendering plugins, gaze helper and forced graphics backend were
removed from the test installation. They were never part of the release package.
The regular Direct3D 11 renderer remains the baseline on Windows and Linux/Proton.
See [PC VR setup and performance](STEAM_FRAME.md) for current guidance.

The investigation verified capabilities, gaze samples and command insertion.
It did **not** prove that Unity retained the shading-rate state for the intended
draws, or establish a measured GPU frame-time improvement. The
[historical investigation](history/PC_FOVEATION.md) is retained as a record, not
as a recommended setup or roadmap.

Steam Frame's foveated video streaming is independent of PC scene rendering.
