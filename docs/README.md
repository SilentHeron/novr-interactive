# Documentation

For the current build:

- [Controller bindings and cockpit layout](CONTROLLER_BINDINGS.md)
- [Hand fit and sleeve reach](HAND_FIT.md)
- [PC VR setup and performance on Windows and Linux/Proton](STEAM_FRAME.md)
- [Building and contributing](../CONTRIBUTING.md)
- [Code architecture and extension points](ARCHITECTURE.md)
- [Reproducible verification](VERIFICATION.md)
- [Third-party credits and licenses](THIRD_PARTY_NOTICES.md)

The root [README](../README.md) links the latest release notes. Each
`RELEASE_*.md` records that particular version; it is not a cumulative manual.

Archived [design notes](history/) for the replacement console, MFDs, physical
switches, EOTS and inspection zoom describe retired experiments. Those systems were removed by
0.4.8.0. Keep those notes as history, but use the current control guide when
reporting bugs or testing a build. Cockpit widgets now have per-aircraft saved
positions, sizes, visibility and backplate opacity rather than fixed placements.

[PC foveated rendering](PC_FOVEATION.md) is optional and experimental, with
fixed and eye-tracked modes on supported native Windows/NVIDIA/Direct3D 11
systems. It defaults to Off and stays unavailable on Linux/Proton.
