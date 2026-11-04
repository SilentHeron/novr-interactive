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
    ("core", "tests/NOVR.Tests.csproj"),
    ("patcher-payload", "tools/PatcherPayloadChecks/PatcherPayloadChecks.csproj"),
    ("ui-lifecycle", "tests/UiLifecycle/NOVR.UiLifecycle.Tests.csproj"),
    ("ui-patching", "tools/UiBehaviorPatcherChecks/UiBehaviorPatcherChecks.csproj"),
    ("api-lifecycle", "tools/ApiBusChecks/ApiBusChecks.csproj"),
    ("configuration-lifecycle", "tools/ModConfigurationChecks/ModConfigurationChecks.csproj"),
    ("xr-toggle-lifecycle", "tools/XrTogglerChecks/XrTogglerChecks.csproj"),
    ("camera-manager", "tools/CameraManagerChecks/CameraManagerChecks.csproj"),
    ("cockpit-profiles", "tools/CockpitProfileChecks/CockpitProfileChecks.csproj"),
    ("controller-runtime", "tools/ControllerRuntimeChecks/ControllerRuntimeChecks.csproj"),
    ("controller-pointer", "tools/ControllerPointerChecks/ControllerPointerChecks.csproj"),
    ("proton-compatibility", "tools/ProtonCompatibilityChecks/ProtonCompatibilityChecks.csproj"),
    ("controller-bindings", "tools/ControllerBindingChecks/ControllerBindingChecks.csproj"),
    ("controller-diagram", "validation/ControllerDiagram/ControllerDiagram.csproj"),
    ("controller-diagram-ui", "validation/ControllerDiagramUi/ControllerDiagramUi.csproj"),
    ("hand-comfort", "tools/HandComfortChecks/HandComfortChecks.csproj"),
    ("arm-pose", "tools/ArmPoseChecks/ArmPoseChecks.csproj"),
    ("hand-pose", "tools/HandPoseChecks/HandPoseChecks.csproj"),
    ("hand-ui-depth", "tools/HandUiDepthChecks/HandUiDepthChecks.csproj"),
    ("sleeve-bounds", "tools/SleeveBoundsChecks/SleeveBoundsChecks.csproj"),
    ("sleeve-geometry", "tools/SleeveGeometryChecks/SleeveGeometryChecks.csproj"),
    ("texture-preparation", "tools/TexturePreparationChecks/TexturePreparationChecks.csproj"),
    ("feedback-preview", "tools/FeedbackPreviewChecks/FeedbackPreviewChecks.csproj"),
    ("hand-option-wheel", "validation/HandOptionWheel/HandOptionWheel.csproj"),
    ("hud-widgets", "validation/HudWidgets/NOVR.HudWidgets.Tests.csproj"),
    ("hud-appearance-ui", "validation/WristMenuHudAppearance/WristMenuHudAppearance.csproj"),
    ("hud-status", "validation/HudStatus/NOVR.HudStatus.Tests.csproj"),
    ("cockpit-status-sources", "tools/CockpitStatusSourceChecks/CockpitStatusSourceChecks.csproj"),
    ("cockpit-widget-depth", "tools/CockpitWidgetDepthChecks/CockpitWidgetDepthChecks.csproj"),
    ("cockpit-map", "tools/CockpitMapChecks/CockpitMapChecks.csproj"),
    ("cockpit-map-input", "tools/CockpitMapInputChecks/CockpitMapInputChecks.csproj"),
    ("grid-readout", "validation/GridAircraftReadout/GridAircraftReadout.csproj"),
    ("gaze-cue", "validation/GazeCue/GazeCue.csproj"),
    ("nozzle-hud", "validation/NozzleHud/NozzleHud.csproj"),
    ("hud-projection-lifecycle", "tools/HudProjectionLifecycleChecks/HudProjectionLifecycleChecks.csproj"),
    ("throttle-indicator", "validation/ThrottleIndicator/ThrottleIndicator.csproj"),
    ("threat-vector", "validation/ThreatVectorLine/ThreatVectorLine.csproj"),
    ("native-menu-handoff", "validation/NativeMenuHandoff/NativeMenuHandoff.csproj"),
    ("mission-readiness", "validation/MissionReadiness/NOVR.MissionReadiness.Tests.csproj"),
    ("startup-splash", "validation/StartupSplash/NOVR.StartupSplash.Tests.csproj"),
    ("installer", "NOVR.Installer.Tests/NOVR.Installer.Tests.csproj"),
)


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
    parser.add_argument("--output", type=Path, help="logs and summary directory (default: artifacts/verification/MODE)")
    parser.add_argument("--verbose", action="store_true", help="print passing test output as well as failures")
    args = parser.parse_args()
    if args.mode == "portable" and (args.plugin or args.managed):
        parser.error("portable checks do not accept game assemblies; use contracts explicitly")
    if args.mode == "contracts" and (not args.plugin or not args.managed):
        parser.error("contracts requires both --plugin and --managed; no game paths are autodetected")
    if args.mode == "contracts" and (not args.plugin.is_file() or not args.managed.is_dir()):
        parser.error("--plugin must be an existing file and --managed an existing directory")
    executable = shutil.which(args.dotnet)
    if executable is None:
        parser.error(f".NET executable not found: {args.dotnet}. Install SDK 8 or supply --dotnet.")
    executable = str(Path(executable).resolve())
    selected = PORTABLE if args.mode == "portable" else (("assembly-contracts", PORTABLE[0][1]),)
    for _, relative in selected:
        portable_project(ROOT / relative)
    environment = dict(os.environ)
    environment.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1", DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1")
    sdk_code, sdk = execute([executable, "--version"], environment)
    sdk = sdk.strip()
    if sdk_code or not sdk.startswith("8.0."):
        parser.error("SDK 8 could not be selected by tools/verification/global.json:\n" + sdk)
    output = (args.output or ROOT / "artifacts" / "verification" / args.mode).resolve()
    output.mkdir(parents=True, exist_ok=True)
    summary = {"mode": args.mode, "sdk": sdk, "platform": platform.platform(),
               "started_utc": datetime.now(timezone.utc).isoformat(), "results": []}
    print(f"NOVR {args.mode} verification — SDK {sdk}, {platform.system()}", flush=True)
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
    report = [f"# NOVR {args.mode} verification", "", f"SDK {sdk} · {platform.system()}", "",
              "| Suite | Result | Seconds |", "| --- | --- | ---: |"]
    report.extend(f"| {item['name']} | {'PASS' if item['exit_code'] == 0 else 'FAIL'} | {item['seconds']:.2f} |"
                  for item in summary["results"])
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
