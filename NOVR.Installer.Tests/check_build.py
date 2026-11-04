#!/usr/bin/env python3
"""Optional SDK 10 build-safety checks; all outputs stay in a temporary fixture."""
from pathlib import Path
import hashlib
import re
import shutil
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
import zipfile
from xml.sax.saxutils import escape

repo = Path(__file__).resolve().parent.parent
dotnet = str(sys.argv[1]) if len(sys.argv) > 1 else "dotnet"
declared = re.search(r'\[BepInPlugin\(\s*"deltawing\.novr"\s*,\s*"[^"]*"\s*,\s*"([^"]+)"',
                     (repo / "NOVR/NOVRPlugin.cs").read_text()).group(1)

with tempfile.TemporaryDirectory(prefix="novr-build-check-") as directory:
    root = Path(directory)
    source = root / "source"
    source.mkdir()
    (source / "NOVR.dll").write_text("fixture")
    project = root / "check.proj"
    project.write_text(f'''<Project><PropertyGroup>
      <NuclearOptionGameDirResolved>{escape(str(root))}/game</NuclearOptionGameDirResolved>
      <GameLayoutOutputDir>{escape(str(root))}/layout</GameLayoutOutputDir>
      <TargetDir>{escape(str(source))}/</TargetDir><GameDeployPath>BepInEx/plugins/NOVR</GameDeployPath>
      <TargetPath>{escape(str(source))}/NOVR.dll</TargetPath><TargetFileName>NOVR.dll</TargetFileName>
      </PropertyGroup><Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.props"/>
      <Target Name="GetCopyToOutputDirectoryItems" />
      <Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.targets"/></Project>''')
    for option in ("", "-p:DeployToGame=typo", "-p:DeployToGame=true"):
        subprocess.run([dotnet, "msbuild", str(project), "-t:StageGameLayout", "-v:q"] +
                       ([option] if option else []), check=True)
        deployed = (root / "game/BepInEx/plugins/NOVR/NOVR.dll").exists()
        assert deployed == option.endswith("=true"), (option, deployed)
        print("PASS build deployment", option or "unset", deployed)

    # Exercise the real cross-publication target's opt-in and recursion guards.
    # Replace only its child-process launches with a command record: this fixture
    # must never publish a real installer or depend on host runtime packs.
    installer = ET.parse(repo / "NOVR.Installer/NOVR.Installer.csproj")
    publish_default = installer.find("PropertyGroup/PublishInstallers")
    publish_target = installer.find("Target[@Name='PublishInstallersToDist']")
    assert publish_default is not None and publish_target is not None
    assert publish_default.text == "false"
    for task in publish_target.findall("Exec"):
        command = task.attrib["Command"]
        assert "-p:IsPublishingInstaller=true" in command and "--no-restore" in command
        task.tag = "WriteLinesToFile"
        task.attrib.clear()
        task.attrib.update(File="$(InstallerPublishIntermediateDir)/commands.txt", Lines=command, Overwrite="false")
    installer_publish = root / "installer-publish"
    for runtime, filename in (("linux-x64", "NOVR.Installer"), ("win-x64", "NOVR.Installer.exe")):
        artifact = installer_publish / runtime / filename
        artifact.parent.mkdir(parents=True)
        artifact.write_text(runtime)
    installer_dist = root / "installer-dist"
    installer_project = root / "installer-check.proj"
    installer_project.write_text(f'''<Project><PropertyGroup>
      <Configuration>Release</Configuration>
      <InstallerDistDir>{escape(str(installer_dist))}</InstallerDistDir>
      <InstallerPublishIntermediateDir>{escape(str(installer_publish))}</InstallerPublishIntermediateDir>
      {ET.tostring(publish_default, encoding="unicode")}</PropertyGroup>
      <Target Name="Build" />{ET.tostring(publish_target, encoding="unicode")}</Project>''')
    publish_cases = (([], False), (["-p:PublishInstallers=false"], False),
                     (["-p:PublishInstallers=typo"], False), (["-p:PublishInstallers=true"], True),
                     (["-p:PublishInstallers=true", "-p:Configuration=Debug"], False),
                     (["-p:PublishInstallers=true", "-p:IsPublishingInstaller=true"], False))
    for options, expected in publish_cases:
        shutil.rmtree(installer_dist, ignore_errors=True)
        commands = installer_publish / "commands.txt"
        commands.unlink(missing_ok=True)
        subprocess.run([dotnet, "msbuild", str(installer_project), "-t:Build", "-v:q", *options], check=True)
        assert commands.exists() == expected, options
        assert installer_dist.exists() == expected, options
        if expected:
            lines = commands.read_text(encoding="utf-8-sig").splitlines()
            assert len(lines) == 2 and "-r linux-x64" in lines[0] and "-r win-x64" in lines[1], lines
            assert (installer_dist / "NOVR.Installer-Linux").read_text() == "linux-x64"
            assert (installer_dist / "NOVR.Installer-Win.exe").read_text() == "win-x64"
    print("PASS installer cross-publication requires explicit Release opt-in and cannot recurse")

    # Real managed metadata with an external attribute, without any game assets.
    fake = root / "fake"
    (fake / "BepInEx").mkdir(parents=True)
    (fake / "NOVR").mkdir()
    (fake / "BepInEx/BepInEx.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>''')
    (fake / "BepInEx/Attribute.cs").write_text('''namespace BepInEx {
      public sealed class BepInPlugin : System.Attribute {
        public BepInPlugin(string guid, string name, string version) {} } }''')
    (fake / "NOVR/NOVR.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
      <ItemGroup><ProjectReference Include="../BepInEx/BepInEx.csproj"/></ItemGroup></Project>''')

    package_source = root / "package-source"
    plugin = package_source / "plugins/NOVR/NOVR.dll"
    patcher = package_source / "patchers/NOVR/NOVR.Patcher.dll"
    metadata = plugin.with_name("version.txt")
    plugin.parent.mkdir(parents=True)
    patcher.parent.mkdir(parents=True)
    patcher.write_bytes(b"fixture-patcher")

    def compile_plugin(version):
        (fake / "NOVR/Plugin.cs").write_text('''namespace NOVR {
          [BepInEx.BepInPlugin("deltawing.novr", "NOVR Interactive", "''' + version + '''")]
          public sealed class NOVRPlugin {} }''')
        subprocess.run([dotnet, "build", str(fake / "NOVR/NOVR.csproj"), "-c", "Release", "-v:q"], check=True)
        shutil.copyfile(fake / "NOVR/bin/Release/net10.0/NOVR.dll", plugin)

    compile_plugin(declared)

    # Exercise the real staging target with SDK-resolved references/content and
    # the patcher's actual generated-payload target, without proprietary files.
    staged_project = fake / "Staging"
    staged_project.mkdir()
    (staged_project / "Class.cs").write_text("public sealed class StagingFixture {}")
    (staged_project / "Assets").mkdir()
    (staged_project / "Assets/current.txt").write_text("current asset")
    (fake / "linked notice.txt").write_text("linked notice")
    shared_output = root / "shared-output"
    shared_output.mkdir()
    (shared_output / "Removed.Plugin.dll").write_text("stale removed assembly")
    (shared_output / "Assets").mkdir()
    (shared_output / "Assets/removed.txt").write_text("stale removed asset")
    generated = root / "generated/plugins"
    generated.mkdir(parents=True)
    for name in ("Management", "OpenXR"):
        (generated / f"Unity.XR.{name}.dll").write_text("current " + name)
    payload_target = ET.parse(repo / "NOVR.Patcher/NOVR.Patcher.csproj").find(
        "Target[@Name='CopyXrAssembliesToPatcherPayload']")
    assert payload_target is not None
    payload_target_xml = ET.tostring(payload_target, encoding="unicode")
    staged_layout = root / "staged-game/BepInEx/patchers/NOVR"

    def stage_current(include_dependency=True):
        dependency = ('<ProjectReference Include="../BepInEx/BepInEx.csproj" />'
                      '<Content Include="Assets/current.txt" CopyToOutputDirectory="Always" />'
                      if include_dependency else "")
        (staged_project / "Staging.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><SkipNuclearOptionReferences>true</SkipNuclearOptionReferences></PropertyGroup>
          <Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.props" />
          <PropertyGroup><TargetFramework>net10.0</TargetFramework><DebugType>none</DebugType>
            <SkipNuclearOptionReferences>true</SkipNuclearOptionReferences>
            <OutputPath>{escape(str(shared_output))}/</OutputPath><AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
            <BuildOutputDir>{escape(str(root))}/generated</BuildOutputDir>
            <GameLayoutOutputDir>{escape(str(root))}/staged-game</GameLayoutOutputDir>
            <GameDeployPath>BepInEx/patchers/NOVR</GameDeployPath></PropertyGroup>
          <ItemGroup>{dependency}<None Include="../linked notice.txt" Link="ThirdParty/NOTICE.txt"
            CopyToOutputDirectory="PreserveNewest" /></ItemGroup>
          <Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.targets" />
          <Target Name="FixtureXrCopyLocal" BeforeTargets="StageGameLayout"><ItemGroup>
            <ReferenceCopyLocalPaths Include="{escape(str(generated))}/Unity.XR.Management.dll" />
          </ItemGroup></Target>
          {payload_target_xml}</Project>''')
        subprocess.run([dotnet, "build", str(staged_project / "Staging.csproj"), "-c", "Release", "-v:q"], check=True)

    stage_current()
    assert (staged_layout / "Staging.dll").is_file()
    assert (staged_layout / "BepInEx.dll").is_file()
    assert (staged_layout / "Assets/current.txt").read_text() == "current asset"
    assert (staged_layout / "ThirdParty/NOTICE.txt").read_text() == "linked notice"
    for name in ("Management", "OpenXR"):
        assert (staged_layout / f"CopyToGame/Data/Managed/Unity.XR.{name}.dll").read_text() == "current " + name
    assert not (staged_layout / "Unity.XR.Management.dll").exists(), "XR belongs only in the patcher payload"
    assert not (staged_layout / "Removed.Plugin.dll").exists()
    assert not (staged_layout / "Assets/removed.txt").exists()
    stage_current(False)
    assert not (staged_layout / "BepInEx.dll").exists()
    assert not (staged_layout / "Assets/current.txt").exists()
    assert (shared_output / "Removed.Plugin.dll").exists(), "fixture must retain stray build output"
    assert not (staged_layout / "Removed.Plugin.dll").exists()
    print("PASS current outputs, linked content and generated XR payload stage; removed files stay excluded")

    # Invalid/missing game paths fail before dependency builds. BepInEx is a
    # runtime install requirement, not a required compile-time directory.
    requires_game = ET.parse(repo / "NOVR/NOVR.csproj").findtext("PropertyGroup/RequiresNuclearOptionGameReferences")
    assert requires_game == "true"
    prerequisite = root / "prerequisite"
    prerequisite.mkdir()
    (prerequisite / "Class.cs").write_text("public sealed class PrerequisiteFixture {}")
    (prerequisite / "must-not-build.proj").write_text('''<Project><Target Name="Build">
      <Error Text="DEPENDENCY_BUILT_BEFORE_GAME_VALIDATION" /></Target></Project>''')
    managed = root / "prerequisite-game/NuclearOption_Data/Managed"

    def check_game_reference_prerequisite(required=True, dependency=False):
        reference = '<ItemGroup><ProjectReference Include="must-not-build.proj" /></ItemGroup>' if dependency else ""
        (prerequisite / "Check.csproj").write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup><NuclearOptionGameDir>{escape(str(managed.parent.parent))}</NuclearOptionGameDir>
            <SkipNuclearOptionReferences>true</SkipNuclearOptionReferences></PropertyGroup>
          <Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.props" />
          <PropertyGroup><TargetFramework>net10.0</TargetFramework>
            <RequiresNuclearOptionGameReferences>{str(required).lower()}</RequiresNuclearOptionGameReferences>
            <SkipNuclearOptionReferences>true</SkipNuclearOptionReferences></PropertyGroup>
          {reference}<Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.targets" /></Project>''')
        # Build target (without restore) isolates ordering before references; the
        # missing-game guard must run even before SDK assets are available.
        return subprocess.run([dotnet, "msbuild", str(prerequisite / "Check.csproj"), "-t:Build", "-v:q"],
                              capture_output=True, text=True)

    for partial in (False, True):
        if partial:
            managed.mkdir(parents=True)
            (managed / "Assembly-CSharp.dll").write_text("assembly presence fixture")
        result = check_game_reference_prerequisite(dependency=True)
        assert result.returncode != 0
        assert "Cannot build NOVR: required Nuclear Option assemblies" in result.stdout, result.stdout + result.stderr
        assert "-p:NuclearOptionGameDir=" in result.stdout
        assert "DEPENDENCY_BUILT_BEFORE_GAME_VALIDATION" not in result.stdout
    (managed / "UnityEngine.CoreModule.dll").write_text("assembly presence fixture")
    check_game_reference_prerequisite()
    subprocess.run([dotnet, "build", str(prerequisite / "Check.csproj"), "-v:q"], check=True)
    assert not (managed.parent.parent / "BepInEx").exists()
    shutil.rmtree(managed.parent.parent)
    check_game_reference_prerequisite(required=False)
    subprocess.run([dotnet, "build", str(prerequisite / "Check.csproj"), "-v:q"], check=True)
    print("PASS early actionable game reference errors; installed BepInEx and source-only game references not required")

    package = root / "dist/NOVR.zip"
    args = [dotnet, "msbuild", str(repo / "NOVR.Build/NOVR.Build.csproj"), "-t:CreateReleaseZip",
            "-p:Configuration=Release", f"-p:ReleasePackageSourceDir={package_source}",
            f"-p:ReleasePackageDistDir={package.parent}", f"-p:ReleasePackagePath={package}",
            f"-p:ReleasePackageStagingDir={root / 'staging'}",
            f"-p:PackageValidationBuildDir={root / 'validation-build'}", "-v:q"]

    def package_ok(version=None):
        subprocess.run(args + ([] if version is None else [f"-p:NovrReleaseVersion={version}"]), check=True)
        checksum = package.with_name(package.name + ".sha256").read_text().strip().split()[0]
        assert checksum.lower() == hashlib.sha256(package.read_bytes()).hexdigest()
        with zipfile.ZipFile(package) as archive:
            assert archive.read("plugins/NOVR/version.txt").decode("utf-8-sig").strip() == declared
            assert archive.read("plugins/NOVR/NOVR.dll") == plugin.read_bytes()
            assert archive.read("patchers/NOVR/NOVR.Patcher.dll") == patcher.read_bytes()

    def package_rejected(name, version=None):
        before_zip = package.read_bytes()
        before_metadata = metadata.read_bytes()
        result = subprocess.run(args + ([] if version is None else [f"-p:NovrReleaseVersion={version}"]),
                                capture_output=True, text=True)
        assert result.returncode != 0, name
        assert "Cannot package NOVR:" in result.stdout, result.stdout + result.stderr
        assert package.read_bytes() == before_zip, name + " changed previous package"
        assert metadata.read_bytes() == before_metadata, name + " wrote metadata before validation"
        print("PASS reject", name)

    metadata.write_text("0.0.1\n")
    package_ok()
    print("PASS omitted version derives compiled release and replaces stale metadata")
    metadata.unlink()
    package_ok()
    print("PASS clean staging gets version metadata and matching ZIP checksum")
    package_ok(declared)
    print("PASS matching explicit version")
    package_rejected("different explicit release", "0.0.1")
    package_rejected("malformed explicit release", "0.4")
    package_rejected("overflowing explicit release", "999999999999999999999999.0.0")
    patcher.unlink()
    package_rejected("missing patcher")
    patcher.write_bytes(b"")
    package_rejected("empty patcher")
    patcher.write_bytes(b"fixture-patcher")
    compiled = plugin.read_bytes()
    plugin.unlink()
    package_rejected("missing plugin")
    plugin.write_bytes(b"not-an-assembly")
    package_rejected("invalid plugin metadata")
    plugin.write_bytes(compiled)
    compile_plugin("0.0.1")
    package_rejected("stale compiled plugin despite current version.txt")
    package_rejected("stale compiled plugin even with matching stale override", "0.0.1")
    print("PASS release build safety; all writes stayed in", root)
