# NOVR 0.4.7.4

## Startup

Supported Blueprinter startup now shows a VR **Loading aircraft & mods** screen.
The main menu returns only after the loader reports success and finishes its
startup operation. The card stays visible during its temporary offline session.
Exit requires confirmation, and **VR CENTER** or **HOME** recenters the view.
Loading failures get an explanation and an exit route.

The splash replaces the confusing menu presentation; it cannot make Unity draw
new frames while a third-party loader blocks the main thread. See
[startup behavior](STARTUP_SPLASH.md) and [content readiness](CONTENT_READINESS.md).

## Cockpit and hands

- FS-20 MFDs fit the native lower display glass and render at cockpit depth.
  Hands, controls and bezels can occlude them naturally. Map symbols retain
  their proportions, and borrowed widgets survive a control-root rebuild.
- PREV/NEXT skip pages absent from the aircraft. The Sensor page can share a
  fresh EOTS HEAD image without creating or rendering another camera.
- **View → Comfort & display → Hands & arms** provides separate left and right
  hand fitting. Position uses 5 mm steps; wrist/palm angles use 5° steps.
  Visible fingertips and cockpit touch points stay aligned. Held controls keep
  their fitted grips.
- Local sleeves extend modestly and fade near the end of arm reach. Their
  culling bounds follow the posed arm. Native pilot and cockpit materials are
  unchanged.

See [physical displays](FS20_DISPLAY_DEPTH.md) and [hand fitting](HAND_FIT.md).

## Installation and verification

The installer follows this fork, supports downloaded packages, rejects
downgrades, verifies online package checksums, and stages updates with rollback.
It preserves user settings and other mods. See
[installer details](../NOVR.Installer/UPDATES.md).

Portable checks run without private game assemblies, including in GitHub's
Ubuntu and Windows workflow. Local assembly-contract checks remain explicit.
See [verification commands and limits](VERIFICATION.md).

No PC dynamic foveated rendering capability is claimed by this release. Headset
comfort, physical reach, and haptic feel still require a person wearing the
headset; synthetic input checks cannot establish those qualities.
