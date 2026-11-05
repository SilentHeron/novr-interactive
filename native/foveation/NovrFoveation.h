#pragma once
#include <cstdint>
#ifdef _WIN32
#define NOVR_FFR_API extern "C" __declspec(dllexport)
#define NOVR_FFR_CALL __stdcall
#else
#define NOVR_FFR_API extern "C"
#define NOVR_FFR_CALL
#endif

// ABI 2; Windows x64 only. All integers are 32-bit. No C++ types cross this ABI.
// Non-null texture pointers must come from GetNativeTexturePtr on D3D11.
// AcquireScope accepts null: managed code must explicitly bind the intended
// camera target; native code still validates its full viewport and dimensions.
// Main-thread exports never operate on the immediate context.
// ABI 2 adds Extreme/Maximum (profiles 3/4); callers must check version 2.
// The signatures match ABI 1, but mixed binaries must fail the version check.
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationGetApiVersion();
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationInitializeFromTexture(void* nativeTexture);
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationGetEventId(int offset);
NOVR_FFR_API void* NOVR_FFR_CALL NovrFoveationGetRenderEventAndDataFunc();
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationSetEnabled(int enabled);
// Optional asynchronous GPU query diagnostics; disabled by default. Allows
// AcquireScope while shading is Off to measure a full-rate baseline.
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationSetDiagnostics(int enabled);
// Center X/Y are finite normalized top-left-origin coordinates in [0,1].
// Fixed and gaze-centered callers share this ABI; each packet captures its center.
NOVR_FFR_API void* NOVR_FFR_CALL NovrFoveationAcquireScope(void* expectedNativeTexture,
    std::uint32_t width, std::uint32_t height, std::uint32_t samples, std::uint32_t eye,
    float centerX, float centerY, float innerRadius, float outerRadius, std::uint32_t profile, std::uint32_t frameIndex);
// Call CancelScope ONLY if neither Begin nor End has been submitted to Unity.
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationCancelScope(void* packet);
// JSON; returns UTF-8 bytes excluding NUL, or -(required capacity including NUL).
NOVR_FFR_API int NOVR_FFR_CALL NovrFoveationCopyReport(char* buffer, int capacity);

// Render event offsets: 0 Probe, 1 Begin, 2 End, 3 Reset, 4 Shutdown.
// Pass the SAME immutable packet to Begin and End exactly once, in that order.
// End is mandatory even if SetEnabled(0) happens between submission and execution.
// End owns packet retirement. Do not reuse command buffers containing a packet.
// Probe, Reset and Shutdown accept null. Reset restores full-rate only, keeping
// cached resources and capability. Shutdown additionally disables and releases
// resources. Neither frees outstanding packet leases. Do not unload this DLL
// until every submitted event has executed. Keeping it loaded until exit is safest.
