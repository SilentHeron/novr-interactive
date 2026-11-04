"""Checks for the source-only runner; no .NET or game installation required."""
import importlib.util
import contextlib
import io
import json
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
        verify.portable_registry()


class RegistryFailureChecks(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.project = self.root / "tests/Example/Example.csproj"
        self.project.parent.mkdir(parents=True)
        self.project.write_text('<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup></Project>')
        for filename in ("Directory.Build.props", "Directory.Build.targets"):
            (self.root / "tests" / filename).write_text("<Project />")
        for attribute, value in (("ROOT", self.root), ("PORTABLE", (("example", "tests/Example/Example.csproj"),))):
            replacement = patch.object(verify, attribute, value)
            replacement.start()
            self.addCleanup(replacement.stop)

    def test_new_project_must_be_registered(self):
        extra = self.root / "tests/Extra.csproj"
        extra.write_text("<Project />")
        with self.assertRaisesRegex(ValueError, "unregistered projects: tests/Extra.csproj"):
            verify.portable_registry()

    def test_removed_project_must_be_removed_from_registry(self):
        self.project.unlink()
        with self.assertRaisesRegex(ValueError, "missing projects: tests/Example/Example.csproj"):
            verify.portable_registry()

    def test_duplicate_names_and_paths_are_rejected(self):
        for extra in (("example", "tests/Other.csproj"), ("other", "tests/Example/Example.csproj")):
            with self.subTest(extra=extra), patch.object(verify, "PORTABLE", (*verify.PORTABLE, extra)):
                with self.assertRaisesRegex(ValueError, "duplicate suite names or project paths"):
                    verify.portable_registry()

    def test_empty_registry_does_not_claim_a_passing_run(self):
        with patch.object(verify, "PORTABLE", ()):
            with self.assertRaisesRegex(ValueError, "must contain named suites"):
                verify.portable_registry()

    def test_generated_projects_are_not_suites(self):
        for directory in ("bin", "obj"):
            generated = self.project.parent / directory / "generated.csproj"
            generated.parent.mkdir()
            generated.write_text("<Project />")
        verify.portable_registry()

    def test_registry_preflight_checks_isolation(self):
        self.project.write_text('<Project><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><Reference Include="Game.dll" /></ItemGroup></Project>')
        with self.assertRaisesRegex(ValueError, "assembly/project/import"):
            verify.portable_registry()


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

    def test_contracts_rejects_portable_selection(self):
        for selection in (("--suite", "core"), ("--list",)):
            with self.subTest(selection=selection):
                self.assert_setup_rejected(["contracts", *selection], "portable-only")

    def test_unknown_suite_fails_before_dotnet(self):
        with patch.object(verify, "execute") as execute:
            self.assert_setup_rejected(["portable", "--suite", "does-not-exist"], "unknown portable suite(s)")
            execute.assert_not_called()

    def test_listing_and_selection_are_not_combined(self):
        self.assert_setup_rejected(["portable", "--list", "--suite", "core"], "use --list to discover")

    def test_list_requires_no_dotnet_or_artifact_directory(self):
        output = io.StringIO()
        with patch.object(sys, "argv", ["verify.py", "portable", "--list"]), \
                patch.object(verify.shutil, "which") as which, \
                patch.object(verify, "execute") as execute, contextlib.redirect_stdout(output):
            self.assertEqual(0, verify.main())
        self.assertIn("core", output.getvalue())
        self.assertIn(dict(verify.PORTABLE)["core"], output.getvalue())
        which.assert_not_called()
        execute.assert_not_called()

    def test_focused_run_still_requires_complete_registry(self):
        with patch.object(sys, "argv", ["verify.py", "portable", "--suite", "core"]), \
                patch.object(verify, "portable_registry", side_effect=ValueError("unregistered projects: extra.csproj")), \
                patch.object(verify, "execute") as execute:
            with self.assertRaisesRegex(ValueError, "unregistered projects"):
                verify.main()
            execute.assert_not_called()

    def test_focused_run_executes_only_selected_suites_once_and_reports_its_scope(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            registry = (("first", "tests/First.csproj"), ("second", "tests/Second.csproj"),
                        ("unselected", "tests/Other.csproj"))
            arguments = ["verify.py", "portable", "--suite", "second", "--suite", "first", "--suite", "second"]
            with patch.object(sys, "argv", arguments), patch.object(verify, "ROOT", root), \
                    patch.object(verify, "PORTABLE", registry), patch.object(verify, "portable_registry") as preflight, \
                    patch.object(verify.shutil, "which", return_value=str(root / "dotnet")), \
                    patch.object(verify, "execute", side_effect=[(0, "8.0.100"), (0, ""), (0, "first passed"), (0, ""), (1, "second failed")]) as execute, \
                    patch.dict(verify.os.environ, {"GITHUB_STEP_SUMMARY": ""}), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, verify.main())
            preflight.assert_called_once_with()
            commands = [call.args[0] for call in execute.call_args_list]
            self.assertEqual(5, len(commands))
            self.assertEqual(str(root / "tests/First.csproj"), commands[1][2])
            self.assertEqual(str(root / "tests/Second.csproj"), commands[3][2])
            report = root / "artifacts/verification/portable-focused"
            summary = json.loads((report / "summary.json").read_text())
            self.assertEqual("focused", summary["scope"])
            self.assertEqual(["first", "second"], summary["suites"])
            self.assertFalse(summary["passed"])
            self.assertIn("not a full portable verification", (report / "summary.md").read_text())
            self.assertFalse((root / "artifacts/verification/portable").exists())

    def test_contracts_passes_explicit_assemblies_to_core_without_portable_selection(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            plugin = root / "NOVR.dll"
            plugin.write_text("fixture")
            managed = root / "Managed"
            managed.mkdir()
            arguments = ["verify.py", "contracts", "--plugin", str(plugin), "--managed", str(managed)]
            with patch.object(sys, "argv", arguments), patch.object(verify, "ROOT", root), \
                    patch.object(verify, "PORTABLE", (("other", "tests/Other.csproj"), ("core", "tests/Core.csproj"))), \
                    patch.object(verify, "portable_registry") as registry, patch.object(verify, "portable_project") as isolation, \
                    patch.object(verify.shutil, "which", return_value=str(root / "dotnet")), \
                    patch.object(verify, "execute", side_effect=[(0, "8.0.100"), (0, "restored"), (0, "contracts passed")]) as execute, \
                    patch.dict(verify.os.environ, {"GITHUB_STEP_SUMMARY": ""}), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(0, verify.main())
            registry.assert_not_called()
            isolation.assert_called_once_with(root / "tests/Core.csproj")
            run = execute.call_args_list[-1].args[0]
            self.assertEqual(["--", str(plugin), str(managed)], run[-3:])
            summary = json.loads((root / "artifacts/verification/contracts/summary.json").read_text())
            self.assertEqual("contracts", summary["scope"])
            self.assertEqual(["assembly-contracts"], summary["suites"])

    def test_restore_failure_does_not_run_suite_and_is_reported(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            output = root / "custom-output"
            with patch.object(sys, "argv", ["verify.py", "portable", "--output", str(output)]), \
                    patch.object(verify, "ROOT", root), patch.object(verify, "PORTABLE", (("core", "tests/Core.csproj"),)), \
                    patch.object(verify, "portable_registry"), \
                    patch.object(verify.shutil, "which", return_value=str(root / "dotnet")), \
                    patch.object(verify, "execute", side_effect=[(0, "8.0.100"), (1, "restore failed")]) as execute, \
                    patch.dict(verify.os.environ, {"GITHUB_STEP_SUMMARY": ""}), contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1, verify.main())
            self.assertEqual(2, execute.call_count)
            summary = json.loads((output / "summary.json").read_text())
            self.assertFalse(summary["passed"])
            self.assertEqual("all", summary["scope"])
            self.assertEqual(1, summary["results"][0]["exit_code"])
            self.assertEqual("restore failed", (output / "core.log").read_text())

    def test_incompatible_sdk_stops_before_restore(self):
        with patch.object(verify, "portable_registry"), \
                patch.object(verify.shutil, "which", return_value=str(Path.cwd() / "dotnet")), \
                patch.object(verify, "execute", return_value=(0, "10.0.100")) as execute:
            self.assert_setup_rejected(["portable"], "SDK 8 could not be selected")
            self.assertEqual(1, execute.call_count)
            self.assertEqual("--version", execute.call_args.args[0][-1])


if __name__ == "__main__":
    unittest.main()
