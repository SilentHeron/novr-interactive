# Optional fixed and gaze-centered foveation native backend

`NovrFoveation.dll` is an opt-in Windows x64 / NVIDIA / D3D11 native backend.
It starts disabled. It does not change Unity's renderer, SteamVR, OpenXR,
resolution, motion smoothing, drivers, or NVIDIA settings. Wine/Proton is
explicitly unavailable; loading or enabling this backend must not be attempted
as a Linux performance workaround.

This implements NVIDIA NVAPI variable pixel shading through Unity render-thread
callbacks. Begin binds a reusable R8_UINT rate texture and a viewport rate
lookup table; End disables variable pixel shading globally and unbinds the rate
view. There are no draw hooks, Present hooks, injected proxies, GPU waits,
texture readbacks, or shader replacements. Optional diagnostics read only small
asynchronous query results.

## Supported render scope

The managed integration must enqueue Begin immediately before the intended
opaque draw pass and End immediately after it. It must explicitly bind the
camera's actual color/depth target before Begin and exclude mirror, UI,
reflection, target cameras, shadows, post processing, and transparent passes.
Native checks the bound color target and viewport at Begin. This first backend
accepts only a full-target Texture2D at mip zero, one viewport, and 1x/2x/4x MSAA.
It rejects texture arrays, partial viewports, mismatched sizes and unsupported
sample counts. Draws inside a successful scope must keep the same target.

The rate map uses 16x16 tiles. Profiles 0 through 2 keep their masks but, since
0.4.8.33, shade the middle region at 1x1 (previously 2x1) so alpha-to-coverage
foliage and vertical edges stay sharp; only their outer region is 2x2. Profiles
3 and 4 add a coarser middle and outer rate as well as smaller protected regions:

| Profile | Name | Inner / outer radius | Center | Middle | Outer, one sample |
| --- | --- | --- | --- | --- | --- |
| 0 | Mild | .70 / .95 | 1x1 | 1x1 | 2x2 |
| 1 | Balanced | .55 / .85 | 1x1 | 1x1 | 2x2 |
| 2 | Strong | .40 / .75 | 1x1 | 1x1 | 2x2 |
| 3 | Extreme | .30 / .60 | 1x1 | 2x2 | 4x4 |
| 4 | Maximum | .18 / .40 | 1x1 | 2x2 | 4x4 |

All other lookup-table entries resolve to 1x1; there is no culling. NVIDIA
documents 4x4 for one sample per raster pixel. Extreme/Maximum therefore cap
their outer rate at 2x2 on 2x/4x MSAA targets; they retain their smaller protected
regions and 2x2 middle. The backend never changes MSAA. If the driver rejects
the optional 4x4 lookup table, Begin first restores full rate and unbinds the
view, then retries that scope with 2x2. A successful retry keeps VRS available.
That rejection is remembered for the current D3D11 device, so later scopes use
2x2 directly. Failure to bind the view, restore full rate, or apply the fallback
remains a fatal backend error. This is an application-level rate cap, not a
claim that the driver has revealed every supported shading rate.

The entire tile is full-rate if any part intersects the protected inner ellipse.
Centers use normalized top-left-origin target coordinates; the managed caller
mirrors Y when URP's intermediate target is drawn upside down. Radii use normalized
half-viewport extents, matching the managed projection-derived optical center.
Each eye has its own cached texture/view and retained CPU image. Unchanged fixed
centers produce the exact original mask and require no upload. A new gaze center
or preset updates the existing image; resources are recreated only when the
16x16-tile grid dimensions change. MSAA changes do not alter rate-image contents.
The profile selects its rate table; the supplied radii determine its pattern.
Changing only the profile or MSAA never rebuilds or uploads the region-index
image. The viewport rate table is selected separately on each applied scope.

The masks protect the supplied center, not named objects. Opaque cockpit pixels
outside that protected region can receive reduced shading along with the world.

## Updating the center from gaze

The ABI carries an immutable center per eye/scope, so eye-tracked callers use
the existing `AcquireScope` signature. The native backend accepts finite
normalized X/Y centers over the complete inclusive [0,1] image range. It rejects
out-of-range and nonfinite values. The protected ellipse clips naturally at image
edges; every tile touching the inner ellipse remains full rate there too. Gaze
tracking, freshness, projection into each eye, and fallback to the fixed optical
center remain the managed caller's responsibility. The native DLL does not open
an eye tracker or change any runtime settings.

`RateMapBuffer` regenerates into retained per-eye storage without heap allocation
on center/radius changes. Its optimized row/column distance calculations preserve
the original tile classifications exactly. If small gaze movement leaves those
classifications identical, the GPU upload is skipped. Otherwise, only the bounding
rectangle of changed texels is sent to `UpdateSubresource` on the immediate
context, before binding that scope's shading-rate view. The R8_UINT textures use
D3D11_USAGE_DEFAULT so their views remain reusable. The API snapshots the source
bytes before returning and orders any GPU copy after earlier uses; the CPU image
can then be reused safely. There is no Map, Flush, query wait, or fence wait in
this upload path. Driver command-buffer copies still have a cost; this is not a
claim of zero overhead or a measured eye-tracking performance improvement.

CPU rate-image storage is bounded to 1 MiB per eye at the existing maximum
16384x16384 render-target size. Gaze changes allocate neither texture/view objects
nor application upload buffers per frame. Existing fixed foveation and full-rate
restoration use the same scope boundaries as before. Mild/Balanced/Strong retain
their previous shading rates in both fixed and eye-tracked operation.

## ABI and ownership

`NovrFoveation.h` defines ABI 2 and every export. ABI 2 keeps the function
signatures but accepts profiles 0..4 and an inner radius down to .15, and adds
actual-rate/clamp reporting. Callers must check `GetApiVersion() == 2` before
using it; this prevents a managed/native version mismatch from silently losing
new profiles or their rate labels. Load this DLL explicitly from
NOVR's private Native directory. It does not require `UnityPluginLoad` or copying
anything into the game's global plugin directory. The Unity header is used for
the documented rendering-callback calling convention only.

1. Call `InitializeFromTexture` once with a D3D11 native texture, e.g. a temporary
   owned bootstrap texture. It retains the texture's device and does not operate
   on the immediate context. The bootstrap texture itself need not remain alive.
2. Submit event 0 (Probe) on Unity's rendering thread. It verifies native Windows,
   NVIDIA's adapter, system NVAPI entry points, and the hardware VRS capability.
3. Inspect `CopyReport`: `supported` is true only after a successful probe.
   `SetEnabled(1)` succeeds only when supported and no fatal failure is latched.
4. `AcquireScope` returns a native-owned immutable packet from a bounded pool.
   Its first argument may be null to avoid per-frame `GetNativeTexturePtr` sync.
   When non-null, the texture is retained and its identity must also match the
   bound RTV. With null, managed explicit target binding and native shape checks
   jointly define the intended target.
5. Submit the identical packet once to event 1 (Begin) and event 2 (End). End
   restores full rate even if disabled since submission, then retires the packet.
   A command buffer containing a packet must not be replayed.
6. Only call `CancelScope` if neither event has been submitted. An unmatched Begin
   retains its lease. The pool eventually refuses new work instead of recycling
   memory that a delayed callback might still access. End-before-Begin is a
   fatal protocol fault and also retains the queued lease.
7. Event 3 (Reset) is a render-thread safety boundary: restore full rate, preserving
   the enable flag, capability and caches. Event 4 (Shutdown) additionally disables
   and releases device/cache references. Neither event retires outstanding leases.
   The DLL must stay loaded until all issued callbacks finish; retaining its module
   until process exit avoids dangling callback addresses.

All state and report access is serialized. COM and NVAPI immediate-context calls
occur only in render events. Failed partial application attempts restore both
NVAPI states. Restore failure latches a fatal error and retries on subsequent
End/Reset events; it never claims full-rate restoration when a driver call failed.
Any fatal error requires restarting the optional feature's process to retry.

`CopyReport` returns JSON UTF-8 length excluding NUL, or the negative required
capacity including NUL. `supported` and `failed` are JSON booleans. Diagnostics
include the last HRESULT/NVAPI status, skipped scopes, and actual successful
`leftApplied`, `rightApplied`, `leftRestored`, and `rightRestored` counters.
`resourceBuilds` counts texture/view builds; `resourceUpdates` counts actual
changed-region uploads. `leftCenterX`, `leftCenterY`, `rightCenterX`, and
`rightCenterY` record the most recent successfully applied centers.
`leftProfile`/`rightProfile` identify those scopes' native profile (0..4).
`leftCoarsestRate`/`rightCoarsestRate` report their actual outer rate, 2 or 4,
initially 1 before an application. `leftRateClamped`/`rightRateClamped` and
`leftClampReason`/`rightClampReason` disclose `none`, `msaa`, or `driver`.
`fourByFourRejected` latches a driver rejection for this device, with its original
`fourByFourNvStatus`; `coarseRateFallbacks` counts attempted recovery from that
optional-rate rejection. These are last-successful-application snapshots, so
consumers must check current applied counters and profile before showing them.
`appliedScopes` counts successful NVAPI application, not measured shader savings.
`restoredScopes` also counts successful recovery from partial applications.
`hardwareValidated` and `measuredPerformanceGain` remain false in this build.

## Optional asynchronous GPU diagnostics

`SetDiagnostics(1)` enables a separate default-off measurement path. It permits
scopes while foveation is Off, giving the same opaque-pass baseline. A fixed ring
of eight reusable query sets brackets sampled valid scopes with GPU timestamps, a
TIMESTAMP_DISJOINT query, and PIPELINE_STATISTICS. The immutable packet contains
Unity's frame index. Sampling alternates eyes across successive frames, with at
most one disjoint query per frame as Microsoft recommends; both eyes therefore
receive fresh measurements every two frames in normal multipass rendering.
Query objects are created once
per occupied ring slot and reused. Ring exhaustion skips measurement; it never
waits for the GPU. Future rendering callbacks read the oldest completed queries
with `D3D11_ASYNC_GETDATA_DONOTFLUSH`. Each callback accepts at most two completed
scopes and immediately stops at the first not-ready result. There is no Flush,
retry loop, or CPU/GPU fence wait. Turning diagnostics off stops new queries and
marks open/in-flight queries for asynchronous draining and discard, so an old
session cannot publish unreported results as fresh samples after re-enabling.

Only complete, non-disjoint results with a nonzero clock frequency are published.
`leftGpuMs`/`rightGpuMs` report the bracketed opaque interval, initially -1;
`leftPixelInvocations`/`rightPixelInvocations` are hardware pipeline statistics.
These are opaque-scope costs, not total eye/frame/compositor time. Diagnostics
have their own overhead and should be enabled for a controlled comparison.
`leftSampleMode`/`rightSampleMode` capture the actually applied mode (0 Off,
1 Mild, 2 Balanced, 3 Strong, 4 Extreme, 5 Maximum);
`leftSampleCoarsestRate`/`rightSampleCoarsestRate` capture that sample's actual
outer rate (1 for Off, 2 for old profiles, 2 or 4 for new profiles), and
`leftSampleRateClamped`/`rightSampleRateClamped` disclose any cap.
`leftSequence`/`rightSequence` identify fresh
completed samples. Consumers must require both eyes to be fresh and match the
currently requested mode and actual rate before displaying an A/B comparison.

Reset closes and discards an unfinished query. Every scope stores its query's
sequence so a delayed End cannot close a query slot reused by a newer scope.
Query creation/read errors disable diagnostics, with `diagnosticsReason` and
`queryHresult`, without turning a working VRS feature into a fatal failure.
`queryCompleted`, `queryDropped`, and `queryDisjoint` expose measurement outcomes.
No GPU numbers are fabricated or inferred from the shading-rate image.

## Build and checks

The checked-in vendor sources are pinned in `vendor/sources.json` with SHA-256
hashes. Their original notices are retained. Distribute `THIRD-PARTY-NOTICES.txt`
with the DLL. NVIDIA's runtime DLL is never included in this package.

From Linux with LLVM-MinGW installed:

```sh
CXX=/path/to/x86_64-w64-mingw32-clang++ ./native/foveation/build.sh
./native/foveation/check.sh
```

The build writes `native/foveation/bin/NovrFoveation.dll`, with static C++ runtime
linking and no timestamp. The portable tests check image bounds, tile-edge protection, asymmetric centers,
preset ordering, invalid inputs, allocation-free moving-gaze updates, exact
partial-upload reconstruction, unchanged fixed masks, and the actual shared
asynchronous query reader
and lifecycle with adapter doubles. They cover pending/error early exit, invalid
clock results, aborted queries, delayed End after query-slot reuse, Extreme and
Maximum sample modes, MSAA caps, and the shared rate-application state machine's
4x4 rejection/restore/retry ordering and failure boundaries. Building and
passing these checks does not test a Windows GPU, Unity render submission,
visual quality, both-eye behavior, or performance. Those need a native Windows
NVIDIA D3D11 playtest with counters and GPU timings. The user reported successful
Windows testing and shader savings with the preceding .24/.25 builds. The new
.26 rates and fallback still need their own Windows playtest; they were compiled
and checked on Linux, not hardware-validated here.

Primary references:

- NVIDIA NVAPI D3D11 interfaces: https://docs.nvidia.com/nvapi/group__dx.html
- NVIDIA VRS overview: https://developer.nvidia.com/blog/turing-variable-rate-shading-vrworks/
- NVIDIA headers: https://github.com/NVIDIA/nvapi/tree/70d337db9186e968eab622f7e786de7e437faf3d
- Unity plugin headers: https://github.com/Unity-Technologies/NativeRenderingPlugin/tree/522254181faf188efa8b50c3e3bf6fce720b26e4

- Microsoft D3D11 query types: https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_query
- Microsoft GetData: https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-getdata

- Microsoft UpdateSubresource: https://learn.microsoft.com/en-us/windows/win32/api/d3d11/nf-d3d11-id3d11devicecontext-updatesubresource
