# Foveated rendering: experimental Windows test

The optional backend uses NVIDIA variable-rate shading with the
existing Direct3D 11 renderer. One ZIP contains the regular mod and the optional
component. Native Windows, a compatible NVIDIA GPU/driver and an eligible stereo
render path are required. Linux/Proton never loads it. Nothing switches the game
to D3D12/Vulkan or changes antialiasing, headset resolution or streaming settings.

## Controls

Open **View → VR performance → Foveated rendering**. The stepped **Strength**
slider works for both fixed and eye-tracked modes. **Off** remains the default;
existing selections are preserved. Since 0.4.8.33 the three original presets
keep their radii but shade the middle ring at full rate: only the outer edge is
coarse.

| Strength | Full-rate radius | Middle-region radius | Centre / middle / outer shading |
| --- | ---: | ---: | --- |
| Mild | 0.70 | 0.95 | 1×1 / 1×1 / 2×2 |
| Balanced | 0.55 | 0.85 | 1×1 / 1×1 / 2×2 |
| Strong | 0.40 | 0.75 | 1×1 / 1×1 / 2×2 |
| Extreme — test | 0.30 | 0.60 | 1×1 / 2×2 / up to 4×4 |
| Maximum — test | 0.18 | 0.40 | 1×1 / 2×2 / up to 4×4 |

Radii use half-viewport extents, not degrees. Full-rate protection includes the
whole 16-pixel tile touching its boundary. Extreme and Maximum reduce both the
protected area and peripheral shading frequency. 4×4 shades once per 16 raster
pixels; it requires a single-sample target (MSAA off). With 2×/4× MSAA, the new
presets use 1×1 / 2×2 / 2×2. Keep MSAA on: turning it off to unlock 4×4 removes
anti-aliasing from the whole image, which costs far more clarity than the outer
4×4 edge saves.

Why no 2×1 ring: the game's trees and grass are alpha-tested with
alpha-to-coverage, and they draw in the same world-opaque pass that foveation
reduces. Coarse shading computes their edge coverage once per block, so foliage
turns blocky and shimmers wherever it is coarse. Horizontal 2×1 shading also
smeared vertical edges such as trunks inside the still-sharp part of the lens.

With eye tracking, the full-rate and middle radii grow by 0.12 while the gaze
is tracked. Gaze input lags the eye, and without that margin the sharp region's
edge sweeps across what you are looking at during each saccade. The fixed
fallback uses the unwidened radii. A driver rejection also falls back to 2×2 without
disabling working foveation. The menu shows the actual rates and MSAA/driver
limit. Antialiasing is never changed automatically.

Geometry and image dimensions stay unchanged. The status distinguishes
capability, requested mode and successful commands for both eyes. Unsupported
combinations stay at normal quality. These two new levels are for testing
visible artifacts and comfort limits, not new recommended defaults.

Only the tracked world camera's forward opaque pass in cockpit view is bracketed.
Spectator views, separate cockpit/UI cameras, maps, reflections, shadows,
transparency and post-processing are excluded. If a modded aircraft draws opaque cockpit geometry in the main
world pass, it shares that pass's peripheral quality. Fixed foveation protects
the centre of each eye view. Looking to the side with your eyes can therefore
reveal reduced detail.

Enable **Follow eye tracking** to move that protected region with the eyes.
Steam Frame uses the optional standard OpenXR Eye Gaze Interaction feature.
Enable eye tracking on the headset and allow gaze access if the runtime requests
it. Restart after installing this version so OpenXR can register the input
feature. The menu distinguishes tracked eyes from fixed fallback and its reason.
This does not move the targeting cue or change weapon aiming.

The centre is expressed in rows of the eye texture that the world pass actually
draws into. With a camera stack or post-processing active, URP renders that pass
into an intermediate texture, and on Direct3D 11 the engine flips the projection
so the top of the image is stored in the last rows. NOVR asks URP for that
orientation on every pass and mirrors the centre to match; the log reports the
orientation once when it is first seen. Versions before 0.4.8.33 mapped an
upright image, so an eye-tracked centre moved opposite to vertical gaze and
coarse shading reached the fovea whenever the eyes left the horizontal centre.
Fixed foveation was barely affected because its centre is nearly symmetric.

Untracked, unfocused, invalid or older-than-100ms gaze falls back immediately to
the existing fixed centre. Extreme/Maximum require a sample no older than 50ms
and fall back to **Strong**, restoring its larger protected region and gentler
shading. This is input sample age, not measured end-to-end latency. Input
exceptions use the same fallback. Both
eyes share one sample per frame, projected into their separate asymmetric
frusta and eye view angles. Tracking-origin rotation is accounted for and
translation is excluded, so aircraft attitude and seat adjustments do not offset
it. Moving gaze updates reusable per-eye rate textures;
it does not create textures/views or allocate the rate image each frame.

## Compare before keeping it enabled

1. Keep the same mission, seat, view, headset resolution, refresh rate and quality
   settings. Let loading/shader compilation settle.
2. Enable **Measure GPU cost** and observe Off, then Mild, then Off again.
   Compare several seconds in each mode, not individual samples. Measurements
   identify the selected mode and require fresh data from both eyes.
3. The two GPU times and pixel-shader invocation counts cover each eye's world
   opaque pass, not the entire frame. Also compare SteamVR's application GPU frame
   time, missed frames and head-motion stability. CPU-limited missions may barely
   benefit even if pixel-shader work decreases.
4. Check peripheral terrain, native instrument text, HUD, shadows and both eyes.
   If artifacts or stutter appear, use Off. Disable measurements after comparing.
5. For eye tracking, hold your head still and look around with your eyes. Confirm
   the menu reports tracked eyes and detail follows them. Use Maximum for this
   check: look well above and below the centre and confirm the sharp region
   moves the same way as your eyes, not the opposite way. Also check fast eye
   movements, blinks, loss of focus and turning eye tracking off. Allow each mode
   to settle before comparing timings. Extreme/Maximum are optional test steps
   beyond Strong; compare both steady fixation and fast eye movements. Keep MSAA
   unchanged between trials and record the actual applied rates shown in the
   menu. Fallback measurements are labelled Strong, not the selected test level.

Measurements use asynchronous D3D11 queries without waiting for the GPU or
flushing commands to obtain a result. Default Off with measurements disabled has
no steady-state foveation render events. Do not combine this experiment with
another injected foveated-rendering tool. Provider-owned foveation is rejected
when the active XR view reports it.

## Validation status

The user tested fixed and eye-tracked 0.4.8.25 on Windows/RTX 4080/Steam Frame.
The [first-pass benchmark](benchmarks/FOVEATION_0.4.8.25.md) reports lower world
opaque GPU time and shader invocations. Total GPU time varied enough that small
differences between presets cannot be ranked reliably. These measurements do
not validate the new Extreme/Maximum levels or the new 4×4 driver path.
Native compile checks, source tests and package checks do not establish
eye-tracking latency, image quality or additional GPU savings. The package
remains experimental.

The earlier eye-tracked D3D12 experiment was stopped after stuttering and no
proven performance improvement. It remains removed. See the
[historical investigation](history/PC_FOVEATION.md). Steam Frame's foveated video
streaming remains independent of this PC-rendering feature.

## Build

Build the native component with `native/foveation/build.sh`, then build the
solution with `-p:IncludeFixedFoveationNative=true`. A missing native payload or
notice fails packaging. Standard contributor builds can omit that flag and the
feature reports that its optional component is not included. Release downloads
use the same combined ZIP for Windows and Linux; no platform selection is needed.
See [native implementation](../native/foveation/README.md) for its ABI, pinned
headers, source checks and ownership rules.

Steam Frame's standard eye-gaze integration is documented by
[Valve](https://partner.steamgames.com/doc/steamhardware/steamframe/engines/unity).
