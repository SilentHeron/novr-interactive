# PC-side dynamic foveated rendering investigation

**Status for 0.4.6.7: not implemented / not active on the tested PC path.**
Eye-gaze input is now requested through the standard OpenXR interaction profile
before session creation. Runtime permissions remain in control. No gaze
coordinates are logged. The EOTS targeting HUD continues to use head direction.

**View → Comfort & display → VR performance** reports renderer, runtime,
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

The [DXVK-NVAPI maintainer's VRS discussion](https://github.com/jp7677/dxvk-nvapi/issues/321)
remains open. Current upstream D3D11 implementation source does not provide the
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

Before enabling a backend, validate live gaze permission, per-eye focus position,
tracking loss, camera-stack exclusions, and actual GPU frame-time savings while
streaming. Frame's foveated video encoding is independent and remains unchanged.
