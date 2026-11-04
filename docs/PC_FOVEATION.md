# PC-side dynamic foveated rendering investigation

**Current status: NOVR has no active PC-side dynamic foveated rendering backend.**
The support page reports runtime capabilities; it does not enable a rendering
implementation. Steam Frame's foveated streaming is a separate runtime function.
The technical evidence below records the October 2026 investigation and is not
a claim about every later runtime or driver version. EOTS was removed in 0.4.8.0.

**View → VR performance** reports renderer, runtime,
eye-tracking sample availability and the DFR limitation. Receiving eye tracking
is necessary for gaze-driven rendering, but is not proof that rendering work is
reduced. No nonfunctional DFR toggle or automatic graphics-backend switch is added.

## Evidence from this host, 29 October 2026

- NVIDIA RTX 4080; Nuclear Option uses Unity 2022.3.62 and Direct3D 11 through
  Proton/DXVK, with NOVR's existing multipass stereo path.
- A read-only extension enumeration of the PC's native SteamVR OpenXR loader
  succeeded: 41 extensions, including XR_EXT_eye_gaze_interaction v2.
- That native Linux enumeration did not advertise XR_FB_foveation,
  XR_META_foveation_eye_tracked or XR_VARJO_quad_views. It is not a Windows
  WineOpenXR enumeration, nor proof of live eye samples inside the running game.
  The in-game support page checks the actual application runtime after restart.
- The installed Unity engine exposes foveation level/flags and URP foveation
  plumbing, but these controls need a supporting provider and graphics path.
  Setting the flags alone is not an implementation.
- The PC has no installed Valve fdm_injection OpenXR/Vulkan layer in the standard
  system/user paths or SteamVR install. Reports about this layer on Steam Frame
  standalone do not establish availability on this x86 PC.

[Valve's Unity integration](https://github.com/ValveSoftware/Unity/blob/main/com.valvesoftware.openxr.utils/Documentation~/index.md)
requires Vulkan/URP for its supported foveation settings. Its requirements and
standalone results do not imply PC-streaming support.

At the time of that investigation, the [DXVK-NVAPI maintainer's VRS discussion](https://github.com/jp7677/dxvk-nvapi/issues/321)
was open. The inspected D3D11 implementation source did not provide the
shading-rate entry points used by the usual NVIDIA D3D11 foveation approach;
entry-point names appearing in a binary's interface table are not proof of support.

## What would unblock an implementation

A supported renderer/provider combination is required. Candidate paths are a
Vulkan shading-rate integration with the required extensions enabled at device
creation, a compatible driver/API-layer solution for this PC, or a custom stereo
multi-resolution renderer. The latter must preserve asymmetric eye projections,
full-resolution HUD/MFD text, transparency and post-processing, and restore full
resolution immediately when gaze is invalid. None is shipped as a working backend
in this build. Native Windows NVIDIA VRS is a separate compatibility target.

## Follow-up on 3 November 2026

The live SteamVR 2.19.1 session still reported Direct3D 11, eye-gaze support,
no Unity foveation capabilities and no active PC rendering backend. The game's
build settings also include Vulkan and Direct3D 12; the observed process had
neither forcing argument, so that session does not establish that either
alternative failed. The installed NVIDIA driver exposes Vulkan attachment
fragment shading rate. Those capabilities justify a separate native rendering
prototype, but do not establish support inside the game's active device.

The current investigation includes Direct3D 12 shading-rate commands through
Unity's native rendering interface and a patched DXVK fallback. Neither is
part of the release package. Compare enabled/disabled GPU frame time at fixed
resolution, refresh rate, scene and camera pose before making a performance
claim. Preserve full-rate cockpit overlays and test gaze loss explicitly.

Before enabling a backend, validate live gaze permission, per-eye focus position,
tracking loss, camera-stack exclusions, and actual GPU frame-time savings while
streaming. Frame's foveated video encoding is independent and remains unchanged.
