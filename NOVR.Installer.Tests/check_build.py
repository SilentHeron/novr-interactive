#!/usr/bin/env python3
"""Optional SDK 10 build-safety checks; all outputs stay in a temporary fixture."""
from pathlib import Path
import hashlib
import re
import shutil
import subprocess
import sys
import tempfile
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
      </PropertyGroup><Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.props"/>
      <Import Project="{escape(str(repo))}/NOVR.Build/NOVR.Build.targets"/></Project>''')
    for option in ("", "-p:DeployToGame=typo", "-p:DeployToGame=true"):
        subprocess.run([dotnet, "msbuild", str(project), "-t:StageGameLayout", "-v:q"] +
                       ([option] if option else []), check=True)
        deployed = (root / "game/BepInEx/plugins/NOVR/NOVR.dll").exists()
        assert deployed == option.endswith("=true"), (option, deployed)
        print("PASS build deployment", option or "unset", deployed)

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
