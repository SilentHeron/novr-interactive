#!/usr/bin/env python3
"""Run portable source checks, or explicitly requested local assembly contracts."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import sys
import time
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
RUN_DIRECTORY = ROOT / "tools" / "verification"
PORTABLE = (
    ("core", "tests/Core/NOVR.Tests.csproj"),
    ("patcher-payload", "tests/PatcherPayload/PatcherPayloadChecks.csproj"),
    ("graphics-startup", "tests/GraphicsStartup/GraphicsStartupChecks.csproj"),
    ("fixed-foveation", "tests/FixedFoveation/FixedFoveationChecks.csproj"),
    ("fixed-foveation-runtime", "tests/FixedFoveationRuntime/FixedFoveationRuntimeChecks.csproj"),
    ("eye-gaze", "tests/EyeGaze/EyeGazeChecks.csproj"),
    ("game-graphics", "tests/GameGraphics/GameGraphicsChecks.csproj"),
    ("ui-lifecycle", "tests/UiLifecycle/NOVR.UiLifecycle.Tests.csproj"),
    ("ui-patching", "tests/UiBehaviorPatcher/UiBehaviorPatcherChecks.csproj"),
    ("api-lifecycle", "tests/ApiBus/ApiBusChecks.csproj"),
    ("configuration-lifecycle", "tests/ModConfiguration/ModConfigurationChecks.csproj"),
    ("xr-toggle-lifecycle", "tests/XrToggler/XrTogglerChecks.csproj"),
    ("camera-manager", "tests/CameraManager/CameraManagerChecks.csproj"),
    ("spectator-input", "tests/SpectatorView/SpectatorViewChecks.csproj"),
    ("cockpit-profiles", "tests/CockpitProfile/CockpitProfileChecks.csproj"),
    ("controller-runtime", "tests/ControllerRuntime/ControllerRuntimeChecks.csproj"),
    ("controller-pointer", "tests/ControllerPointer/ControllerPointerChecks.csproj"),
    ("proton-compatibility", "tests/ProtonCompatibility/ProtonCompatibilityChecks.csproj"),
    ("controller-bindings", "tests/ControllerBinding/ControllerBindingChecks.csproj"),
    ("controller-diagram", "tests/ControllerDiagram/ControllerDiagram.csproj"),
    ("controller-diagram-ui", "tests/ControllerDiagramUi/ControllerDiagramUi.csproj"),
    ("hand-comfort", "tests/HandComfort/HandComfortChecks.csproj"),
    ("seat-clearance", "tests/SeatClearance/SeatClearance.csproj"),
    ("arm-pose", "tests/ArmPose/ArmPoseChecks.csproj"),
    ("auxiliary-flight-axis", "tests/AuxiliaryFlightAxis/AuxiliaryFlightAxis.csproj"),
    ("damage-overlay", "tests/DamageOverlay/DamageOverlay.csproj"),
    ("hand-pose", "tests/HandPose/HandPoseChecks.csproj"),
    ("hand-ui-depth", "tests/HandUiDepth/HandUiDepthChecks.csproj"),
    ("sleeve-bounds", "tests/SleeveBounds/SleeveBoundsChecks.csproj"),
    ("sleeve-geometry", "tests/SleeveGeometry/SleeveGeometryChecks.csproj"),
    ("texture-preparation", "tests/TexturePreparation/TexturePreparationChecks.csproj"),
    ("feedback-preview", "tests/FeedbackPreview/FeedbackPreviewChecks.csproj"),
    ("hand-option-wheel", "tests/HandOptionWheel/HandOptionWheel.csproj"),
    ("hud-widgets", "tests/HudWidgets/NOVR.HudWidgets.Tests.csproj"),
    ("wrist-widgets", "tests/WristWidgets/WristWidgets.csproj"),
    ("hud-widget-session", "tests/HudWidgetSession/HudWidgetSession.csproj"),
    ("hud-appearance-ui", "tests/WristMenuHudAppearance/WristMenuHudAppearance.csproj"),
    ("hud-status", "tests/HudStatus/NOVR.HudStatus.Tests.csproj"),
    ("cockpit-status-sources", "tests/CockpitStatusSource/CockpitStatusSourceChecks.csproj"),
    ("cockpit-widget-depth", "tests/CockpitWidgetDepth/CockpitWidgetDepthChecks.csproj"),
    ("cockpit-map", "tests/CockpitMap/CockpitMapChecks.csproj"),
    ("cockpit-map-input", "tests/CockpitMapInput/CockpitMapInputChecks.csproj"),
    ("grid-readout", "tests/GridAircraftReadout/GridAircraftReadout.csproj"),
    ("gaze-cue", "tests/GazeCue/GazeCue.csproj"),
    ("nozzle-hud", "tests/NozzleHud/NozzleHud.csproj"),
    ("hud-projection-lifecycle", "tests/HudProjectionLifecycle/HudProjectionLifecycleChecks.csproj"),
    ("throttle-indicator", "tests/ThrottleIndicator/ThrottleIndicator.csproj"),
    ("threat-vector", "tests/ThreatVectorLine/ThreatVectorLine.csproj"),
    ("native-menu-handoff", "tests/NativeMenuHandoff/NativeMenuHandoff.csproj"),
    ("native-binding-catalog", "tests/NativeBindingCatalog/NativeBindingCatalog.csproj"),
    ("native-settings-access", "tests/NativeSettingsAccess/NativeSettingsAccess.csproj"),
    ("ui-binding-ownership", "tests/UiBindingOwnership/UiBindingOwnership.csproj"),
    ("native-workshop", "tests/NativeWorkshop/NativeWorkshop.csproj"),
    ("mission-readiness", "tests/MissionReadiness/NOVR.MissionReadiness.Tests.csproj"),
    ("startup-splash", "tests/StartupSplash/NOVR.StartupSplash.Tests.csproj"),
    ("installer", "tests/Installer/NOVR.Installer.Tests.csproj"),
    ("installer-ui", "tests/InstallerUi/InstallerUi.csproj"),
)
TEST_ROOTS = ("tests",)


def portable_registry() -> None:
    """Check the entire registry, including during a focused verification run."""
    names = [name for name, _ in PORTABLE]
    paths = [path for _, path in PORTABLE]
    if not names or any(not name for name in names):
        raise ValueError("Portable registry must contain named suites")
    if len(names) != len(set(names)) or len(paths) != len(set(paths)):
        raise ValueError("Portable registry contains duplicate suite names or project paths")
    discovered = {
        path.relative_to(ROOT).as_posix()
        for folder in TEST_ROOTS
        for path in (ROOT / folder).rglob("*.csproj")
        if not {"bin", "obj"}.intersection(path.relative_to(ROOT).parts)
    }
    unregistered = discovered - set(paths)
    missing = set(paths) - discovered
    if unregistered or missing:
        details = []
        if unregistered:
            details.append("unregistered projects: " + ", ".join(sorted(unregistered)))
        if missing:
            details.append("missing projects: " + ", ".join(sorted(missing)))
        raise ValueError("Portable registry is incomplete; " + "; ".join(details))
    for _, relative in PORTABLE:
        portable_project(ROOT / relative)


def portable_project(path: Path) -> None:
    """Fail clearly if a portable project starts importing the game build."""
    root = ET.parse(path).getroot()
    frameworks = [item.text for item in root.iter() if item.tag == "TargetFramework"]
    if frameworks != ["net8.0"]:
        raise ValueError(f"{path.relative_to(ROOT)} must target net8.0 for portable checks")
    forbidden = {"Reference", "ProjectReference", "Import"}
    if any(item.tag in forbidden for item in root.iter()):
        raise ValueError(f"{path.relative_to(ROOT)} has assembly/project/import dependencies; review portable isolation")
    for item in root.iter():
        if item.tag not in {"Compile", "EmbeddedResource"} or "Include" not in item.attrib:
            continue
        pattern = item.attrib["Include"].replace("\\", "/")
        if "$" in pattern or ";" in pattern or Path(pattern).is_absolute():
            raise ValueError(f"{path.relative_to(ROOT)} has a nonportable source/resource include: {pattern}")
        matches = list(path.parent.glob(pattern))
        if not matches:
            raise ValueError(f"{path.relative_to(ROOT)} is missing source/resource input: {pattern}")
        for match in matches:
            if not match.resolve().is_relative_to(ROOT) or not match.is_file():
                raise ValueError(f"{path.relative_to(ROOT)} includes an external or non-file input: {pattern}")
            if match.suffix.lower() in {".dll", ".exe", ".so", ".dylib", ".bundle"}:
                raise ValueError(f"{path.relative_to(ROOT)} embeds a binary dependency: {pattern}")
    for filename in ("Directory.Build.props", "Directory.Build.targets"):
        directory = path.parent
        while True:
            candidate = directory / filename
            if candidate.exists():
                build = ET.parse(candidate).getroot()
                if build.tag != "Project" or len(build) or build.attrib:
                    raise ValueError(f"{candidate.relative_to(ROOT)} does not isolate tests from game build imports")
                break
            if directory == ROOT:
                raise ValueError(f"{path.relative_to(ROOT)} is missing an isolated {filename}")
            directory = directory.parent


def execute(command: list[str], environment: dict[str, str]) -> tuple[int, str]:
    try:
        result = subprocess.run(command, cwd=RUN_DIRECTORY, env=environment,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                text=True, encoding="utf-8", errors="replace", timeout=600)
        return result.returncode, result.stdout
    except subprocess.TimeoutExpired as error:
        output = error.stdout or b""
        if isinstance(output, bytes):
            output = output.decode("utf-8", errors="replace")
        return 124, output + "\nVerification command timed out after 600 seconds.\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=("portable", "contracts"),
                        help="portable uses source only; contracts reads explicitly supplied local assemblies")
    parser.add_argument("--dotnet", default=os.environ.get("DOTNET", "dotnet"), help=".NET executable (SDK 8 required)")
    parser.add_argument("--plugin", type=Path, help="local built NOVR.dll; required only for contracts")
    parser.add_argument("--managed", type=Path, help="local NuclearOption_Data/Managed directory; contracts only")
    parser.add_argument("--output", type=Path,
                        help="logs and summary directory (default: artifacts/verification/MODE, or portable-focused)")
    parser.add_argument("--verbose", action="store_true", help="print passing test output as well as failures")
    parser.add_argument("--list", action="store_true", help="list portable suites without running .NET")
    parser.add_argument("--suite", action="append", default=[], metavar="NAME",
                        help="run a portable suite by name; repeat to select several (default: all)")
    args = parser.parse_args()
    if args.mode == "portable" and (args.plugin or args.managed):
        parser.error("portable checks do not accept game assemblies; use contracts explicitly")
    if args.mode == "contracts" and (args.list or args.suite):
        parser.error("--list and --suite are portable-only; contracts always runs the core assembly checks")
    if args.list and args.suite:
        parser.error("use --list to discover suites, then --suite to run them")
    unknown = set(args.suite) - {name for name, _ in PORTABLE}
    if unknown:
        parser.error("unknown portable suite(s): " + ", ".join(sorted(unknown)) + "; use portable --list")
    if args.mode == "contracts" and (not args.plugin or not args.managed):
        parser.error("contracts requires both --plugin and --managed; no game paths are autodetected")
    if args.mode == "contracts" and (not args.plugin.is_file() or not args.managed.is_dir()):
        parser.error("--plugin must be an existing file and --managed an existing directory")
    if args.mode == "portable":
        portable_registry()
        if args.list:
            for name, relative in PORTABLE:
                print(f"{name:28} {relative}")
            return 0
        selected = tuple(item for item in PORTABLE if not args.suite or item[0] in args.suite)
    else:
        selected = (("assembly-contracts", dict(PORTABLE)["core"]),)
        portable_project(ROOT / selected[0][1])
    executable = shutil.which(args.dotnet)
    if executable is None:
        parser.error(f".NET executable not found: {args.dotnet}. Install SDK 8 or supply --dotnet.")
    executable = str(Path(executable).resolve())
    environment = dict(os.environ)
    environment.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
    sdk_code, sdk = execute([executable, "--version"], environment)
    sdk = sdk.strip()
    if sdk_code or not sdk.startswith("8.0."):
        parser.error("SDK 8 could not be selected by tools/verification/global.json:\n" + sdk)
    scope = "focused" if args.suite else "all" if args.mode == "portable" else "contracts"
    report_directory = "portable-focused" if args.suite else args.mode
    output = (args.output or ROOT / "artifacts" / "verification" / report_directory).resolve()
    output.mkdir(parents=True, exist_ok=True)
    summary = {"mode": args.mode, "scope": scope, "suites": [name for name, _ in selected],
               "sdk": sdk, "platform": platform.platform(),
               "started_utc": datetime.now(timezone.utc).isoformat(), "results": []}
    print(f"NOVR {args.mode} verification ({scope}) — SDK {sdk}, {platform.system()}", flush=True)
    for name, relative in selected:
        started = time.monotonic()
        print(f"Running {name}…", flush=True)
        project = str(ROOT / relative)
        restore = [executable, "restore", project, "--configfile", str(RUN_DIRECTORY / "NuGet.Config"),
                   "-p:DeployToGame=false", "--verbosity", "minimal"]
        code, transcript = execute(restore, environment)
        if code == 0:
            command = [executable, "run", "--project", project, "--configuration", "Release",
                       "--no-restore", "--no-launch-profile", "-p:DeployToGame=false"]
            if args.mode == "contracts":
                command.extend(["--", str(args.plugin.resolve()), str(args.managed.resolve())])
            code, result = execute(command, environment)
            transcript += "\n" + result
        (output / f"{name}.log").write_text(transcript, encoding="utf-8")
        seconds = round(time.monotonic() - started, 2)
        summary["results"].append({"name": name, "project": relative, "exit_code": code, "seconds": seconds})
        print(f"{'PASS' if code == 0 else 'FAIL'} {name} ({seconds:.2f}s)", flush=True)
        if code or args.verbose:
            print(transcript, flush=True)
        # Retain partial evidence if a later suite is interrupted or fails.
        (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    failures = sum(result["exit_code"] != 0 for result in summary["results"])
    summary["passed"] = failures == 0
    summary["finished_utc"] = datetime.now(timezone.utc).isoformat()
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    report = [f"# NOVR {args.mode} verification ({scope})", "", f"SDK {sdk} · {platform.system()}", "",
              "| Suite | Result | Seconds |", "| --- | --- | ---: |"]
    report.extend(f"| {item['name']} | {'PASS' if item['exit_code'] == 0 else 'FAIL'} | {item['seconds']:.2f} |"
                  for item in summary["results"])
    if args.suite:
        report += ["", "Focused run: only the selected suites executed. This is not a full portable verification."]
    report += ["", "These checks do not establish stereo comfort, rendered appearance, haptic feel, multiplayer behavior or frame-time performance.", ""]
    markdown = "\n".join(report)
    (output / "summary.md").write_text(markdown, encoding="utf-8")
    if os.environ.get("GITHUB_STEP_SUMMARY"):
        with open(os.environ["GITHUB_STEP_SUMMARY"], "a", encoding="utf-8") as file:
            file.write(markdown)
    print(f"{len(selected)-failures}/{len(selected)} suites passed. Reports: {output}")
    return 1 if failures else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, ET.ParseError) as error:
        print(f"Verification setup failed: {error}", file=sys.stderr)
        raise SystemExit(2)
