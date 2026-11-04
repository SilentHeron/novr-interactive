# Unity XR source boundary

`NOVR.XR.OpenXR` and `NOVR.XR.Management` contain inherited Unity XR source,
compiled as `Unity.XR.OpenXR.dll` and `Unity.XR.Management.dll`. They are kept in
the repository because NOVR initializes XR in an existing game rather than
building a Unity project with serialized XR settings.

The retained NOVR baseline `14ae40be6e769a2be583e82a770264e968b586b2` is a useful
comparison point for this fork's changes. It is **not** a pristine Unity package
revision. Earlier repository history includes XR library updates and a
`MockRuntime` dictionary compatibility fix.

## Known local changes

Relative to that baseline, the current OpenXR tree adds:

- [`Settings/NovrControllerProfiles.cs`](../NOVR.XR.OpenXR/Settings/NovrControllerProfiles.cs): runtime registration of supported controller profiles.
- [`Features/Valve/SteamFrameControllerProfile.cs`](../NOVR.XR.OpenXR/Features/Valve/SteamFrameControllerProfile.cs) and its license: Valve's profile from revision `7e842352310fc2cb458ac6b127ce1afc233173a1`, with an extension-availability guard and source notice. See [third-party notices](THIRD_PARTY_NOTICES.md).
- `ENABLE_INPUT_SYSTEM` in the OpenXR project configuration.

The XR Management source is unchanged from that baseline. Keep new adapters in
clearly named NOVR files where possible, and record further vendor edits here.
Avoid reformatting inherited files as part of an unrelated gameplay change.

## Payload and provenance limits

[`NOVR.Patcher`](../NOVR.Patcher/NOVR.Patcher.csproj) stages both built managed
assemblies under `CopyToGame/Data/Managed`. Its checked-in native payload contains
`UnityOpenXR.dll`, `openxr_loader.dll` and the subsystem manifest, used by the
Windows game on Windows and Proton. Do not replace one part independently of
the managed/native compatibility checks and a runtime startup check.

The [subsystem manifest](../NOVR.Patcher/CopyToGame/Data/UnitySubsystems/UnityOpenXR/UnitySubsystemsManifest.json)
declares version **1.17.0**. This identifies the manifest; it does not establish
the exact upstream revision of either managed source tree. Those Unity package
versions/source revisions and their original package license files are not
recorded in this checkout. Recover and record them before a vendor upgrade;
this document does not substitute for the missing original notices.

The [portable checks](VERIFICATION.md) cover NOVR's profile registration and
payload lifecycle with boundary doubles. They do not build the entire Unity XR
source or verify a native driver's ABI. Use a local runtime build, assembly
contracts and an actual SteamVR startup check when changing this boundary.
