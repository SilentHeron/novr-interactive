"""Checks for the source-only runner; no .NET or game installation required."""
import importlib.util
import contextlib
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch


SPEC = importlib.util.spec_from_file_location("novr_verify", Path(__file__).parents[1] / "verify.py")
verify = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(verify)


class PortableRegistryChecks(unittest.TestCase):
    def test_registered_projects_are_present_and_isolated(self):
        names = [name for name, _ in verify.PORTABLE]
        paths = [path for _, path in verify.PORTABLE]
        self.assertEqual(len(names), len(set(names)))
        self.assertEqual(len(paths), len(set(paths)))
        for name, path in verify.PORTABLE:
            with self.subTest(suite=name):
                verify.portable_project(verify.ROOT / path)

    def test_source_check_projects_are_registered(self):
        discovered = {
            path.relative_to(verify.ROOT).as_posix()
            for folder in ("tests", "tools", "validation", "NOVR.Installer.Tests")
            for path in (verify.ROOT / folder).rglob("*.csproj")
            if not {"bin", "obj"}.intersection(path.parts)
        }
        self.assertEqual(discovered, {path for _, path in verify.PORTABLE})


class PortableIsolationChecks(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name).resolve()
        self.project = self.root / "checks" / "Checks.csproj"
        self.project.parent.mkdir()
        self.root_patch = patch.object(verify, "ROOT", self.root)
        self.root_patch.start()
        self.addCleanup(self.temporary.cleanup)
        self.addCleanup(self.root_patch.stop)
        for name in ("Directory.Build.props", "Directory.Build.targets"):
            (self.project.parent / name).write_text("<Project />", encoding="utf-8")
        (self.root / "Production.cs").write_text("// linked production source", encoding="utf-8")
        self.write_project()

    def write_project(self, items="", framework="net8.0"):
        self.project.write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            f"<TargetFramework>{framework}</TargetFramework></PropertyGroup>"
            '<ItemGroup><Compile Include="../Production.cs" />'
            f"{items}</ItemGroup></Project>", encoding="utf-8")

    def test_source_and_public_packages_are_allowed(self):
        self.write_project('<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />')
        verify.portable_project(self.project)

    def test_wrong_framework_is_rejected(self):
        self.write_project(framework="net10.0")
        with self.assertRaisesRegex(ValueError, "must target net8.0"):
            verify.portable_project(self.project)

    def test_build_dependencies_are_rejected(self):
        for tag in ("Reference", "ProjectReference", "Import"):
            with self.subTest(tag=tag):
                self.write_project(f'<{tag} Include="game" />')
                with self.assertRaisesRegex(ValueError, "assembly/project/import"):
                    verify.portable_project(self.project)

    def test_build_imports_must_be_isolated(self):
        for name in ("Directory.Build.props", "Directory.Build.targets"):
            with self.subTest(file=name):
                path = self.project.parent / name
                path.write_text('<Project><Import Project="game.props" /></Project>', encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "does not isolate"):
                    verify.portable_project(self.project)
                path.write_text("<Project />", encoding="utf-8")

    def test_nearest_empty_import_isolation_can_be_inherited(self):
        for name in ("Directory.Build.props", "Directory.Build.targets"):
            (self.project.parent / name).unlink()
            (self.root / name).write_text("<Project />", encoding="utf-8")
        verify.portable_project(self.project)

    def test_missing_build_isolation_is_rejected(self):
        (self.project.parent / "Directory.Build.targets").unlink()
        with self.assertRaisesRegex(ValueError, "missing an isolated"):
            verify.portable_project(self.project)

    def test_missing_linked_source_is_rejected(self):
        (self.root / "Production.cs").unlink()
        with self.assertRaisesRegex(ValueError, "missing source/resource"):
            verify.portable_project(self.project)

    def test_missing_resource_glob_is_rejected(self):
        self.write_project('<EmbeddedResource Include="../art/*.png" />')
        with self.assertRaisesRegex(ValueError, "missing source/resource"):
            verify.portable_project(self.project)

    def test_resource_glob_and_windows_relative_paths_are_allowed(self):
        (self.root / "art").mkdir()
        (self.root / "art" / "diagram.png").write_bytes(b"test resource")
        self.write_project('<EmbeddedResource Include="..\\art\\*.png" />')
        verify.portable_project(self.project)

    def test_external_source_is_rejected(self):
        with tempfile.NamedTemporaryFile(suffix=".cs") as source:
            self.write_project(f'<Compile Include="{Path(source.name).as_posix()}" />')
            with self.assertRaisesRegex(ValueError, "nonportable"):
                verify.portable_project(self.project)

    def test_dynamic_include_is_rejected(self):
        self.write_project('<Compile Include="$(GamePath)/Source.cs" />')
        with self.assertRaisesRegex(ValueError, "nonportable"):
            verify.portable_project(self.project)

    def test_binary_resource_is_rejected(self):
        (self.root / "Game.dll").write_bytes(b"not a redistributable test resource")
        self.write_project('<EmbeddedResource Include="../Game.dll" />')
        with self.assertRaisesRegex(ValueError, "binary dependency"):
            verify.portable_project(self.project)


class RunnerModeChecks(unittest.TestCase):
    def assert_setup_rejected(self, arguments, message):
        output = io.StringIO()
        with patch.object(sys, "argv", ["verify.py", *arguments]), contextlib.redirect_stderr(output):
            with self.assertRaises(SystemExit) as error:
                verify.main()
        self.assertEqual(2, error.exception.code)
        self.assertIn(message, output.getvalue())

    def test_portable_rejects_game_assembly_arguments(self):
        for argument in ("--plugin", "--managed"):
            with self.subTest(argument=argument):
                self.assert_setup_rejected(["portable", argument, "unused"], "do not accept game assemblies")

    def test_contracts_requires_explicit_paths(self):
        self.assert_setup_rejected(["contracts"], "requires both --plugin and --managed")

    def test_incompatible_sdk_stops_before_restore(self):
        with patch.object(verify, "PORTABLE", ()), \
                patch.object(verify.shutil, "which", return_value=str(Path.cwd() / "dotnet")), \
                patch.object(verify, "execute", return_value=(0, "10.0.100")) as execute:
            self.assert_setup_rejected(["portable"], "SDK 8 could not be selected")
            self.assertEqual(1, execute.call_count)
            self.assertEqual("--version", execute.call_args.args[0][-1])


if __name__ == "__main__":
    unittest.main()
