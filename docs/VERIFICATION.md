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

The command runs all eleven suites and returns a nonzero exit code if any suite
fails. Read individual logs and JSON/Markdown summaries under
`artifacts/verification/portable/`; use `--verbose` to print passing output too.

| Suite | What it covers | Current checks |
| --- | --- | ---: |
| Core | Flight-control math, fresh-press gates, input ownership, detent/ejection rules, HUD presentation, texture preparation, sound generation and other portable behavior | 144 |
| UI lifecycle | Duplicate-open/close handling and mission-launch ownership using offline doubles | 5 |
| Hand comfort | Per-hand correction bounds, invalid data and continuous bounded sleeve reach | 27 |
| Arm pose | Elbow singularities, mirrored collective alignment, grip transitions, repeated wrist rotation and smooth reach | 24 |
| Hand pose | Actual glove controller with transform doubles: grip/gesture transitions, finger isolation, raw contact independence, tracking resets and seam normals on both hands at four frame rates | 10,096 assertions |
| Sleeve bounds | Animated arm bounds and transformed clothing samples | 6 |
| Feedback preview | Actual preview component and haptic dispatch with boundary doubles: one-shot hand routing, cancellation, disabled output and shared cues | 21 |
| MFD context | Four-key page/action matrix, destination availability, optical capability, numeric bounds, short state labels and physical page-picker state | 31 |
| Mission readiness | Content-load state, completion recognition, late attachment and recovery using boundary doubles | 20 |
| Startup splash | Loading-state priority, honest progress wording, slow-load help and readiness transitions | 28 |
| Installer | Fork release selection, package validation, replacement/rollback and settings preservation using temporary directories and mock HTTP | 52 |

Counts describe the current source and may increase. The runner trusts each
suite's exit status; it does not turn a fixed count or an expected log string
into a pass. These are executable check programs, so the runner uses `dotnet run`
rather than `dotnet test`.

## GitHub Actions

`.github/workflows/portable-checks.yml` runs the same command on Ubuntu and
Windows for pushes, pull requests and manual dispatch. Its sparse checkout
includes only the source needed by these suites, excluding the repository's
Unity/native binaries. It uploads verification logs, not build binaries or game
files. Jobs have read-only repository permission and never deploy the mod.

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

The [FS-20 interaction test matrix](FS20_INTERACTION_TEST_MATRIX.md) lists button,
MFD and switch acceptance cases, existing source/private-probe coverage and the
runtime or headset checks still required. It separates test implementation from
passed evidence and feedback dispatch from physical sensation.

Portable checks and assembly contracts do not prove stereo appearance, rendered
occlusion, interaction comfort, haptic feel, multiplayer behavior or frame-time
performance. Verify those in the running game with a clearly recorded build,
aircraft, settings and device path. Keep synthetic interaction tests isolated
from online sessions, restore their settings afterward and remove temporary
diagnostic plugins before a normal play session.
