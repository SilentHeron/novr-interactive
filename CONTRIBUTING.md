# Contributing

NOVR Interactive focuses on virtual HOTAS, controller bindings, the native option
wheel and adjustable HUD widgets. Keep the game's cockpit displays and aircraft
character intact. Small fixes with a clear reproduction are easier to review than
new cockpit-specific systems.

## Getting started

For source-only checks, install Python 3.10+ and .NET SDK 8, then run:

```sh
python tools/verify.py portable
```

These checks do not require Nuclear Option, Unity or a headset. See
[verification](docs/VERIFICATION.md) for logs, SDK selection and coverage limits.
Use `python tools/verify.py portable --list` to find a focused suite, then run it
with `--suite NAME` while developing.

To build the mod, install Nuclear Option locally. BepInEx 5.x for Windows x64 is
needed to run the mod on Windows or Proton; NuGet supplies its compile-time references. The supported
game version is listed in [README](README.md). Common Steam library locations
are detected; specify another installation explicitly:

```sh
dotnet build NOVR/NOVR.csproj -c Release -p:DeployToGame=false -p:NuclearOptionGameDir="/path/to/Nuclear Option"
dotnet build NOVR.Patcher/NOVR.Patcher.csproj -c Release -p:DeployToGame=false -p:NuclearOptionGameDir="/path/to/Nuclear Option"
```

SDK 8 is sufficient for these projects. SDK 10 is used for the full solution and
release packaging. To create a local package:

```sh
dotnet build NOVR.Build/NOVR.Build.csproj -c Release -p:DeployToGame=false
```

For a custom Steam library, pass the same `-p:NuclearOptionGameDir="/path/to/Nuclear Option"`
to the package command, or set `NUCLEAR_OPTION_GAME_DIR` before building.

To build all maintained projects with SDK 10:

```sh
dotnet build NuclearOptionVirtualRealityMod.sln -c Release -p:DeployToGame=false
```

The same custom-library override applies to this command.

Publishing the self-contained Linux and Windows installers is a separate opt-in:

```sh
dotnet build NOVR.Installer/NOVR.Installer.csproj -c Release -p:PublishInstallers=true
```

This writes `dist/NOVR.Installer-Linux` and `dist/NOVR.Installer-Win.exe` and does
not require the game. For a full release build, add `-p:PublishInstallers=true` to
the solution command. An ordinary Release build does not republish installers.

Output is staged in `build-output/game/BepInEx`; the package is `dist/NOVR.zip`.
Staging includes the current build's resolved assemblies and declared content;
removed dependencies or assets left in `build-output` are excluded automatically.
Its version comes from the compiled plugin and must match `NOVR/NOVRPlugin.cs`.
An optional `-p:NovrReleaseVersion=X.Y.Z` must match too; old staged metadata is
always replaced. Building does not install it. Close the game before installing a local package,
and retain a backup of the previous plugin, patcher and settings.

## Where code lives

| Area | Responsibility |
| --- | --- |
| `NOVR/Input`, `NOVR/FlightControls` | Controller samples, input ownership, bindings and virtual HOTAS |
| `NOVR/Hands`, `NOVR/Feedback`, `NOVR/Profiles` | Hand and sleeve poses, audio/haptics and per-aircraft placement |
| `NOVR/VrUi/Widgets`, `Map`, `Status` | Adjustable HUD widgets, borrowed native status UI and map interaction |
| `NOVR/VrUi/Hud`, `Targeting`, `SceneAdapters` | HUD scaling, targeting cues and native scene presentation |
| `NOVR/VrUi/ViewMenu`, `Bindings`, `Input`, `RadialMenu` | View settings, binding editor, pointers and native hand wheel |
| `NOVR/VrUi/Native` | Main/mission menus, settings, content readiness and startup splash |
| `NOVR/VrUi/HarmonyPatches` | Small adapters for native game UI behavior |
| `NOVR/VrCamera`, `NOVR/Compatibility` | Stereo/cockpit spaces and platform compatibility |
| `NOVR.Patcher`, `NOVR.XR.*` | Early XR payload installation and Unity XR integrations |
| `tests/<feature>` | Source-only suites, including `Core` and `Installer`, isolated from game references |
| `tools` | Verification runner and asset-preparation utilities |

See [architecture](docs/ARCHITECTURE.md) for startup, input ownership and the
native UI lifetime rules. Folders group features; existing CLR namespaces and
persisted configuration keys remain stable for compatibility.

Put new source-only suites in `tests/<feature>/`. Register each suite in
`tools/verify.py`, preserve the empty build-import files that isolate it from game
references, and include its linked inputs in the CI sparse checkout.

The Unity XR projects contain inherited vendor source. See the
[XR source boundary](docs/XR_VENDORING.md) before changing or updating them.

The game camera and cockpit UI camera use different coordinate spaces. Convert
through their rig roots when moving a tracked point between them; do not assume
their world positions or depth buffers match. Hand interaction uses raw tracked
contact points. Visual stabilization and snapped poses must not feed back into
contact detection.

## Making a change

Follow `.editorconfig` in maintained code. Format changed C# files with
`dotnet format whitespace . --folder --include path/to/changed.cs`; keep formatting
separate from behavior changes and leave imported `NOVR.XR.*` source in its existing style.

- Include the trigger, expected behavior and observed behavior. For VR issues,
  include the build, game version, aircraft, headset/controllers, streaming path
  and handedness. Mention whether it also occurs with stock aircraft.
- Keep native UI changes reversible. Borrowed transforms, layers, materials and
  visibility must restore when leaving the cockpit, opening the full map,
  changing aircraft or disabling VR. Do not edit shared game materials.
- Keep one owner for each controller action. Test release and neutral handoff,
  tracking loss, pause/menu transitions and both handedness settings when input
  ownership changes. Preserve existing aircraft profile slots and defaults.
- Add a focused regression for a behavioral fix using the production class.
  Use small boundary doubles where Unity or game assemblies are unavailable;
  do not copy the implementation into the test. Run the relevant checks before
  the full portable command.
- Keep per-frame paths allocation-free where practical. Cache reflection and
  component lookups, reuse collections, and dispose only resources NOVR owns.
  Read private game fields through typed field references, and do not walk the
  HUD or map hierarchy on a timer when a reference or a change test will do
  (see "Searching the HUD and map" in the architecture guide).
- Preserve Windows and Linux/Proton support. Keep OS-specific workarounds guarded;
  do not assume a particular Steam library, controller or GPU vendor. Portable CI
  runs on Windows and Linux, but it does not test either platform's live VR driver.

For a runtime build, also run the optional assembly contracts described in the
verification guide. They check that every Harmony target and cached private-field
handle exists in your installed game, that each typed field reference fits its
field's type, and that the game methods a NOVR prefix can
skip are unchanged since their NOVR copies were written
(`tests/Core/ReplacedMethodFingerprints.txt`). After a game update, review each
method the contract names against its NOVR patch, then regenerate the file by
running the contracts with `NOVR_UPDATE_FINGERPRINTS=1`. State separately what was checked in code, in a running game
and in a headset. A boundary test cannot establish haptic feel or stereo comfort.
Use offline sessions for synthetic input; remove temporary diagnostics afterward.

Keep proprietary game assemblies, extracted assets, decompiled game source,
personal settings and logs containing account information out of pull requests.
Preserve upstream credits and document the source and license of contributed
assets in [third-party notices](docs/THIRD_PARTY_NOTICES.md).
