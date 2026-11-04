# Steam Frame fork installation and updates

The configured release channel is **SilentHeron/novr-interactive**; publication is pending. This installer follows that channel only. It does not fall back to the original NOVR repository when the fork has no release. The channel appears in the installer window and progress text.

Close Nuclear Option before applying an update. The installer does not stop a running game automatically.

## Downloaded and local packages

Opening the installer is offline; network access begins only when Install or Repair / Update is chosen. Online updates use the latest published release on the fork. Supported assets are `NOVR-steam-frame-<version>.zip` (preferred) and `NOVR.zip`; unrelated ZIP files are never selected. Tags support three or four numeric components, with an optional `v` prefix. The installer will not downgrade a newer installed version.

An online package must have a GitHub SHA-256 asset digest or a matching `<package>.zip.sha256` release asset. The entire download is checked before installation. A failed or canceled download never replaces an installed directory. Checksums establish correspondence to the selected release; they are not a separate publisher signature.

If no release has been published, **Install a downloaded NOVR package…** accepts a locally provided ZIP with `plugins/NOVR/version.txt`. This validates its structure and embedded version. Local selection does not claim publisher authentication or verify an absent release checksum; use the provided package and its checksum from your trusted download source.

The package must contain nonempty `plugins/NOVR/NOVR.dll` and `patchers/NOVR/NOVR.Patcher.dll`. Its embedded version, when present, must match the selected release. Absolute paths, traversal, duplicate/case-alias paths, symbolic links, and another mod's plugin/patcher paths are rejected. Expanded contents are capped at 1 GiB and 10,000 entries.

Only `plugins/NOVR` and `patchers/NOVR` are installed. Package documentation is not copied into the game, and package defaults cannot overwrite `BepInEx/config`. Other mods remain untouched.

At startup, NOVR's patcher installs its runtime payload. BepInEx opens managed
assemblies before initializing patchers, so updates to the two NOVR-supplied XR
assemblies use its supported replacement hook. Validation finishes before the
old reader is released. A blocked disk replacement keeps the updated in-memory
assembly usable and logs an offline recovery instruction. These are complete
XR-library replacements; an upgrade cannot preserve earlier third-party preloader
edits to those same two assemblies.

## Update recovery

Install, update and removal share an exclusive `.novr-install.lock` in the game directory, outside the BepInEx tree that may be removed. An additional legacy lock check rejects an already-running older updater. Full BepInEx removal closes that inner handle before deleting its tree so Windows can finish removing it; the game-root lock stays held. Older versions do not honor the root lock, so they must not run concurrently with removal.

The installer validates the ZIP and stages both owned directories inside `BepInEx/.novr-update`. It writes a recovery journal before moving the old directories aside. Both replacements must succeed before the transaction is marked committed; errors or cancellation restore the previous pair. If the installer was interrupted, a subsequent install attempt recovers the previous transaction first. A failed recovery retains its backup directory and reports its location instead of deleting the old files.

The two directory replacements are not one indivisible filesystem operation, which is why the game must be closed. Process-interruption recovery is covered by tests; power loss, disk corruption and antivirus/file-lock behavior on a real Windows installation have not been tested. Initial BepInEx bootstrap installation is separate from the NOVR directory transaction.

## Building a release

Ordinary builds stage local output and **do not copy into the detected game**. Intentional development deployment requires `-p:DeployToGame=true`; any other value leaves the game untouched.

The installer project targets .NET 10. To compile without also generating both distribution executables:

```sh
dotnet build NOVR.Installer/NOVR.Installer.csproj -c Release -p:IsPublishingInstaller=true
```

The release build project emits `NOVR.zip.sha256` beside `NOVR.zip`. It reads the compiled plugin's release version, checks it against `NOVR/NOVRPlugin.cs`, and writes fresh metadata for local installation. An optional `-p:NovrReleaseVersion=<release version>` must match; stale binaries, missing payloads and conflicting versions are rejected before packaging. Upload the ZIP and its checksum together to this fork's release. Runtime compilation still requires the game's private reference assemblies.

The installer pins `Tmds.DBus.Protocol` to the compatible 0.21.3 backport for [the upstream D-Bus advisory](https://github.com/advisories/GHSA-xrw6-gwf8-vvr9); the runtime game plugin does not use this dependency.

## Verification

Run the portable installer checks without game files or network access:

```sh
dotnet run --project NOVR.Installer.Tests/NOVR.Installer.Tests.csproj -c Release
```

An optional ZIP argument validates a real release package without installing it. Tests cover four-part versions, malformed archives, settings/other-mod preservation, forced second-swap failure, cancellation, interrupted-update recovery, concurrent updates, mocked release selection and checksums. Temporary fixture files are removed on completion.

With SDK 10 and Python available, `python3 NOVR.Installer.Tests/check_build.py /path/to/dotnet` also verifies deployment settings, compiled release identity, stale metadata/binary rejection, missing payloads and generated checksums in temporary directories. It compiles tiny fake plugin fixtures, without building or deploying the runtime plugin.
