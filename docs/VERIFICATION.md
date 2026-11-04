# Reproducible verification

The portable suites run with **.NET SDK 8** and **Python 3.10 or newer**. They
compile selected production source without a game installation, Unity assemblies,
a headset, or a packaged NOVR DLL:

```sh
python tools/verify.py portable
```

If .NET is not on `PATH`, supply its executable explicitly:

```sh
python tools/verify.py portable --dotnet /path/to/dotnet
```

`tools/verification/global.json` selects a stable SDK 8 feature band even when
newer SDKs are installed. Restore uses only the public NuGet feed and the package
versions declared in each test project. No game paths are autodetected. Each
project has isolated build imports; the runner rejects assembly references,
project references or imports that would silently pull in the game build.

The command runs every suite registered in `tools/verify.py` and returns a
nonzero exit code if any fails. Read individual logs and JSON/Markdown summaries
under `artifacts/verification/portable/`; use `--verbose` to print passing output.
The runner also checks that linked source and embedded resources are present and
inside this repository. It rejects missing resource globs and binary dependencies.

| Area | Coverage |
| --- | --- |
| Core and lifecycle | Flight-control math, input ownership, detents, ejection rules, complete configuration publication, XR enable/disable and retry, camera discovery/ownership, API event subscriptions, canvas-camera rebinding, paused/reentrant UI attachment and UI open/close ownership |
| Controllers and menus | Steam Frame/Touch input mapping, Proton controller compatibility, pointer ownership and slider drag capture, binding validation and persistence, controller artwork/callouts/activity, View settings, binding editor, hand option wheel and native-menu handoff |
| Hands and feedback | Glove poses and finger transitions, tracking resets, elbow/wrist alignment, sleeve reach/bounds/geometry, texture preparation, UI-depth restoration and feedback dispatch |
| HUD widgets | Per-aircraft layout slots, migration, saved placement, corner acquisition, resizing/rotation, appearance controls, borrowing/restoring native status sources and depth handling |
| Map and flight cues | Minimap borrowing, terrain changes/tint/restoration, map interaction, redundant coordinate suppression, gaze candidates, nozzle/collective visibility, threat-vector bounds, delayed pitch-ladder setup and native runway endpoints |
| Startup and installer | Mission-content readiness, splash progress, release selection, package validation, replacement/rollback and settings preservation |

These are executable check programs, so the runner uses `dotnet run` rather than
`dotnet test`. It trusts exit status, not a fixed assertion count or expected log
string. Some suites exercise pure math; others compile production components
against small Unity/game boundary doubles. Profile suites use real temporary
BepInEx config files. None of those doubles supplies a rendered game scene.

The runner's own registry and isolation checks require only Python:

```sh
python -m unittest discover -s tools/verification -p "test_*.py"
```

## GitHub Actions

`.github/workflows/portable-checks.yml` runs the same command on Ubuntu and
Windows for pushes, pull requests and manual dispatch. Its sparse checkout
includes the source and controller/glove artwork needed by these suites,
excluding the repository's Unity/native binaries. It runs the Python isolation
checks before the portable suites. It uploads verification logs, not build
binaries or game files. Jobs have read-only repository permission and never
deploy the mod.

A passing Linux run does not prove Windows passed; both matrix results need to
be checked in GitHub Actions. Local test output cannot establish that a hosted
workflow ran successfully.

## Optional local assembly contracts

After building the runtime locally against your installed game, explicitly opt
into its compatibility checks:

```sh
python tools/verify.py contracts \
  --plugin /path/to/built/NOVR.dll \
  --managed /path/to/NuclearOption_Data/Managed
```

This reads the supplied assemblies with Mono.Cecil. It runs the core checks
plus the embedded asset and patch/field compatibility checks. Reports are kept
separately under `artifacts/verification/contracts/`. It does not launch the
game, install a package, send game input, or upload local assemblies. Do not add
private game files or hardcoded installation paths to CI to make this check run.

The optional installer build/deployment-safety check in
`NOVR.Installer.Tests/check_build.py` is separate and requires SDK 10. It is not
part of the SDK 8 portable workflow.

## Runtime evidence still needed

The [FS-20 interaction matrix](FS20_INTERACTION_TEST_MATRIX.md) is a historical
record for the retired 0.4.7.5 replacement-panel experiment, not a current runtime
acceptance checklist. Its private probes and removed MFD harness are not run by
the portable command.

Portable checks and assembly contracts do not prove stereo appearance, rendered
occlusion, interaction comfort, haptic feel, multiplayer behavior or frame-time
performance. Verify those in the running game with a clearly recorded build,
aircraft, settings and device path. Keep synthetic interaction tests isolated
from online sessions, restore their settings afterward and remove temporary
diagnostic plugins before a normal play session.
