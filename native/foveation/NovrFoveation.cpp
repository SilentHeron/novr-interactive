#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#include <windows.h>
#include <d3d11.h>
#include <dxgi.h>
#include <array>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include "FoveationPattern.h"
#include "RateMapBuffer.h"
#include "ShadingRates.h"
#include "AsyncQuery.h"
#include "ScopeLease.h"
#include "NovrFoveation.h"
#include "vendor/Unity/IUnityGraphics.h"
// NVAPI nests its SAL headers. Preserve their no-op annotations until the
// complete header is parsed when building with MinGW; upstream files stay intact.
#ifdef __MINGW32__
#define __NVAPI_EMPTY_SAL
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wunknown-attributes"
#endif
#include "vendor/nvapi/nvapi.h"
#ifdef __MINGW32__
#undef __NVAPI_EMPTY_SAL
#include "vendor/nvapi/nvapi_lite_salend.h"
#pragma clang diagnostic pop
#endif

namespace
{
using novr::foveation::Pattern;
constexpr int EventBase = 0x4E465600;
constexpr unsigned ScopeCount = 32;
SRWLOCK gate = SRWLOCK_INIT;
struct Exclusive
{
    Exclusive() { AcquireSRWLockExclusive(&gate); }
    ~Exclusive() { ReleaseSRWLockExclusive(&gate); }
};
template<class T> void Release(T*& p) { if (p) { p->Release(); p = nullptr; } }
struct Scope : novr::foveation::ScopeLease
{
    Pattern pattern{};
    ID3D11Texture2D* texture = nullptr;
    int measurement = -1;
    unsigned long long measurementSequence = 0;
    std::uint32_t frameIndex = 0;
};
std::array<Scope, ScopeCount> scopes;
Scope* active = nullptr;
ID3D11Device* pendingDevice = nullptr;
ID3D11Device* device = nullptr;
ID3D11DeviceContext* context = nullptr;
struct RateCache
{
    ID3D11Texture2D* texture = nullptr;
    ID3D11NvShadingRateResourceView* view = nullptr;
    novr::foveation::RateMapBuffer pixels;
};
// Each eye retains its own rate image, view, and CPU upload storage. Gaze moves
// update contents in queue order; fixed centers require no uploads.
std::array<RateCache, 2> rateCaches;
bool enabled = false, probed = false, capable = false, dirty = false, fatal = false;
bool diagnosticsEnabled = false, diagnosticsFailed = false;
bool fourByFourRejected = false;
struct Measurement : novr::foveation::QueryLifecycle, novr::foveation::SampleShadingInfo
{
    ID3D11Query* disjoint = nullptr;
    ID3D11Query* start = nullptr;
    ID3D11Query* end = nullptr;
    ID3D11Query* statistics = nullptr;
    unsigned eye = 0;
};
std::array<Measurement, 8> measurements;
int openMeasurement = -1;
unsigned long long nextMeasurementSequence = 0;
novr::foveation::FrameSampler frameSampler;
struct EyeMeasurement
{
    double gpuMs = -1;
    unsigned long long pixelInvocations = 0, sequence = 0;
    unsigned mode = 0, coarsestRate = 1;
    bool rateClamped = false;
};
std::array<EyeMeasurement, 2> eyeMeasurements;
HMODULE nvModule = nullptr;
bool nvInitialized = false;
decltype(&NvAPI_Initialize) nvInitialize = nullptr;
decltype(&NvAPI_Unload) nvUnload = nullptr;
decltype(&NvAPI_D3D1x_GetGraphicsCapabilities) nvCapabilities = nullptr;
decltype(&NvAPI_D3D11_CreateShadingRateResourceView) nvCreateView = nullptr;
decltype(&NvAPI_D3D11_RSSetShadingRateResourceView) nvSetView = nullptr;
decltype(&NvAPI_D3D11_RSSetViewportsPixelShadingRates) nvSetRates = nullptr;
struct Report
{
    const char* state = "waiting-for-texture";
    const char* reason = "not-initialized";
    int lastNvStatus = 0;
    HRESULT lastHr = S_OK;
    unsigned vendor = 0, width = 0, height = 0, samples = 0, eye = 0, profile = 0;
    unsigned long long probeEvents = 0, beginEvents = 0, endEvents = 0, resetEvents = 0;
    unsigned long long appliedScopes = 0, restoredScopes = 0, skippedScopes = 0;
    unsigned long long resourceBuilds = 0, resourceUpdates = 0, poolExhausted = 0, restoreFailures = 0, cancelledScopes = 0;
    unsigned long long leftApplied = 0, rightApplied = 0, leftRestored = 0, rightRestored = 0;
    unsigned long long queryCompleted = 0, queryDropped = 0, queryDisjoint = 0;
    const char* diagnosticsReason = "off";
    HRESULT queryHr = S_OK;
    float leftCenterX = .5f, leftCenterY = .5f, rightCenterX = .5f, rightCenterY = .5f;
    unsigned leftProfile = 0, rightProfile = 0, leftCoarsestRate = 1, rightCoarsestRate = 1;
    bool leftRateClamped = false, rightRateClamped = false;
    const char* leftClampReason = "none";
    const char* rightClampReason = "none";
    int fourByFourNvStatus = 0;
    unsigned long long coarseRateFallbacks = 0;
} report;

bool IsWine()
{
    HMODULE ntdll = GetModuleHandleW(L"ntdll.dll");
    return ntdll && GetProcAddress(ntdll, "wine_get_version");
}
void Unavailable(const char* reason, HRESULT hr = S_OK, int nvStatus = 0)
{
    capable = false; enabled = false; diagnosticsEnabled = false; report.state = "unavailable"; report.reason = reason;
    report.lastHr = hr; report.lastNvStatus = nvStatus;
}
void Failure(const char* reason, HRESULT hr = S_OK, int nvStatus = 0)
{
    fatal = true; enabled = false; diagnosticsEnabled = false; capable = false; report.state = "error"; report.reason = reason;
    report.lastHr = hr; report.lastNvStatus = nvStatus;
}
void ReleaseRateResources()
{
    for (auto& cache : rateCaches)
    { Release(cache.view); Release(cache.texture); cache.pixels.Reset(); }
}
// This function is ONLY invoked by render events. Restore independently of the
// latest enabled flag; an Off click cannot leave an already applied scope active.
bool Restore()
{
    if (!dirty) { active = nullptr; return true; }
    NV_D3D11_VIEWPORTS_SHADING_RATE_DESC desc{};
    desc.version = NV_D3D11_VIEWPORTS_SHADING_RATE_DESC_VER;
    desc.numViewports = 0; // NVIDIA documents this as global full-rate disable.
    const NvAPI_Status rates = nvSetRates && context ? nvSetRates(context, &desc) : NVAPI_ERROR;
    const NvAPI_Status view = nvSetView && context ? nvSetView(context, nullptr) : NVAPI_ERROR;
    const Scope* completed = active;
    active = nullptr;
    if (rates != NVAPI_OK || view != NVAPI_OK)
    {
        ++report.restoreFailures;
        Failure("full-rate-restore-failed", S_OK, rates != NVAPI_OK ? rates : view);
        // Retain dirty so subsequent End/Reset callbacks retry full-rate restore.
        return false;
    }
    dirty = false; ++report.restoredScopes;
    if (completed)
    { if (completed->pattern.eye == 0) ++report.leftRestored; else ++report.rightRestored; }
    return true;
}
// Query objects are bounded and reused. No Flush, spin loop, GPU wait, or
// GetData without DONOTFLUSH is permitted in this diagnostics path.
void FinishMeasurement(int index, bool discard)
{
    if (index < 0 || index >= static_cast<int>(measurements.size())) return;
    auto& q = measurements[index];
    if (!context || !q.TryFinish(q.sequence, discard)) return;
    context->End(q.statistics);
    context->End(q.end);
    context->End(q.disjoint);
    if (openMeasurement == index) openMeasurement = -1;
}
void FinishScopeMeasurement(const Scope& scope, bool discard)
{
    // Reset can close a query before a delayed End arrives. Its slot may already
    // be reused, so pair the index with the immutable sequence of this scope.
    if (scope.measurement >= 0
        && measurements[scope.measurement].sequence == scope.measurementSequence)
        FinishMeasurement(scope.measurement, discard);
}
void ReleaseMeasurements()
{
    FinishMeasurement(openMeasurement, true);
    for (auto& q : measurements)
    {
        Release(q.statistics); Release(q.end); Release(q.start); Release(q.disjoint);
        q = {};
    }
    openMeasurement = -1;
}
bool CreateMeasurementQueries(Measurement& q)
{
    if (q.disjoint && q.start && q.end && q.statistics) return true;
    D3D11_QUERY_DESC desc{};
    desc.Query = D3D11_QUERY_TIMESTAMP_DISJOINT;
    HRESULT hr = device->CreateQuery(&desc, &q.disjoint);
    desc.Query = D3D11_QUERY_TIMESTAMP;
    if (SUCCEEDED(hr)) hr = device->CreateQuery(&desc, &q.start);
    if (SUCCEEDED(hr)) hr = device->CreateQuery(&desc, &q.end);
    desc.Query = D3D11_QUERY_PIPELINE_STATISTICS;
    if (SUCCEEDED(hr)) hr = device->CreateQuery(&desc, &q.statistics);
    if (FAILED(hr))
    {
        Release(q.statistics); Release(q.end); Release(q.start); Release(q.disjoint);
        diagnosticsEnabled = false; diagnosticsFailed = true; report.queryHr = hr; report.diagnosticsReason = "query-creation-failed";
        return false;
    }
    return true;
}
void StartMeasurement(Scope& scope)
{
    if (!diagnosticsEnabled || !context) return;
    // Microsoft recommends TIMESTAMP_DISJOINT once per frame or less. Sample
    // alternating eyes on successive frames, keeping both scopes comparable.
    if (!frameSampler.CanSample(scope.frameIndex, scope.pattern.eye)) return;
    if (openMeasurement >= 0)
    {
        FinishMeasurement(openMeasurement, true);
        ++report.queryDropped;
        report.diagnosticsReason = "overlapping-measurement-discarded";
        return;
    }
    for (unsigned index = 0; index < measurements.size(); ++index)
    {
        auto& q = measurements[index];
        if (q.open || q.inFlight) continue;
        if (!CreateMeasurementQueries(q)) return;
        q.eye = scope.pattern.eye; q.profile = scope.pattern.profile;
        q.applied = false; q.coarsestRate = 1; q.rateClamped = false;
        if (!q.TryStart(++nextMeasurementSequence)) continue;
        frameSampler.Record(scope.frameIndex, scope.pattern.eye);
        context->Begin(q.disjoint);
        context->End(q.start);
        context->Begin(q.statistics);
        scope.measurement = openMeasurement = static_cast<int>(index);
        scope.measurementSequence = q.sequence;
        report.diagnosticsReason = "collecting";
        return;
    }
    ++report.queryDropped; report.diagnosticsReason = "query-ring-busy";
}
struct D3DQueryReader
{
    Measurement& query;
    HRESULT last = S_OK;
    novr::foveation::QueryRead Read(ID3D11Query* object, void* data, UINT size)
    {
        last = context->GetData(object, data, size, D3D11_ASYNC_GETDATA_DONOTFLUSH);
        return last == S_OK ? novr::foveation::QueryRead::Ready
             : last == S_FALSE ? novr::foveation::QueryRead::Pending : novr::foveation::QueryRead::Failed;
    }
    novr::foveation::QueryRead Disjoint(std::uint64_t& frequency, bool& disjoint)
    {
        D3D11_QUERY_DATA_TIMESTAMP_DISJOINT value{};
        const auto state = Read(query.disjoint, &value, sizeof(value));
        frequency = value.Frequency; disjoint = value.Disjoint != FALSE;
        return state;
    }
    novr::foveation::QueryRead Start(std::uint64_t& value) { return Read(query.start, &value, sizeof(value)); }
    novr::foveation::QueryRead End(std::uint64_t& value) { return Read(query.end, &value, sizeof(value)); }
    novr::foveation::QueryRead Pixels(std::uint64_t& pixels)
    {
        D3D11_QUERY_DATA_PIPELINE_STATISTICS value{};
        const auto state = Read(query.statistics, &value, sizeof(value));
        pixels = value.PSInvocations;
        return state;
    }
};
void PollMeasurements()
{
    if (!context) return;
    // At most two completed scopes per callback. Stop immediately if the oldest
    // outstanding query is not ready. This never submits/flushes pending work.
    for (unsigned attempt = 0; attempt < 2; ++attempt)
    {
        Measurement* oldest = nullptr;
        for (auto& q : measurements)
            if (q.inFlight && (!oldest || q.sequence < oldest->sequence)) oldest = &q;
        if (!oldest) return;
        auto& q = *oldest;
        D3DQueryReader reader{q};
        novr::foveation::GpuQuerySample sample;
        const auto read = novr::foveation::ReadGpuSample(reader, sample);
        if (read == novr::foveation::QueryRead::Pending) return;
        q.inFlight = false;
        if (read == novr::foveation::QueryRead::Failed)
        {
            ++report.queryDropped; diagnosticsEnabled = false; diagnosticsFailed = true;
            report.queryHr = reader.last; report.diagnosticsReason = "query-read-failed";
            continue;
        }
        if (q.discard) { ++report.queryDropped; continue; }
        if (!sample.IsValid())
        { ++report.queryDisjoint; continue; }
        auto& result = eyeMeasurements[q.eye];
        result.gpuMs = sample.Milliseconds();
        result.pixelInvocations = sample.pixels;
        result.mode = q.Mode();
        result.coarsestRate = q.coarsestRate; result.rateClamped = q.rateClamped;
        result.sequence = q.sequence;
        ++report.queryCompleted;
        if (!diagnosticsFailed) report.diagnosticsReason = diagnosticsEnabled ? "sample-completed" : "off";
    }
}
bool LoadNvApi()
{
    if (nvInitialized) return true;
    // Never load an application-local replacement or a Wine/Proton NVAPI shim.
    if (IsWine()) { Unavailable("wine-proton-unsupported"); return false; }
    wchar_t path[MAX_PATH];
    const UINT count = GetSystemDirectoryW(path, MAX_PATH);
    if (!count || count > MAX_PATH - 14) { Unavailable("system-directory-unavailable"); return false; }
    std::wcscat(path, L"\\nvapi64.dll");
    if (!nvModule) nvModule = LoadLibraryExW(path, nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
    if (!nvModule) { Unavailable("nvapi64-unavailable", HRESULT_FROM_WIN32(GetLastError())); return false; }
    using Query = void* (__cdecl*)(unsigned int);
    const auto query = reinterpret_cast<Query>(GetProcAddress(nvModule, "nvapi_QueryInterface"));
    if (!query) { Unavailable("nvapi-query-interface-unavailable"); return false; }
    // IDs are pinned to NVIDIA's official vendor/nvapi/nvapi_interface.h.
    nvInitialize = reinterpret_cast<decltype(nvInitialize)>(query(0x0150e828));
    nvUnload = reinterpret_cast<decltype(nvUnload)>(query(0xd22bdd7e));
    nvCapabilities = reinterpret_cast<decltype(nvCapabilities)>(query(0x52b1499a));
    nvCreateView = reinterpret_cast<decltype(nvCreateView)>(query(0x99ca2dff));
    nvSetView = reinterpret_cast<decltype(nvSetView)>(query(0x1b0c2f83));
    nvSetRates = reinterpret_cast<decltype(nvSetRates)>(query(0x34f7938f));
    if (!nvInitialize || !nvUnload || !nvCapabilities || !nvCreateView || !nvSetView || !nvSetRates)
    { Unavailable("nvapi-vrs-entry-points-unavailable"); return false; }
    const auto status = nvInitialize();
    if (status != NVAPI_OK) { Unavailable("nvapi-initialize-failed", S_OK, status); return false; }
    nvInitialized = true;
    return true;
}
bool EnsureDevice()
{
    if (pendingDevice != device)
    {
        if (!Restore()) return false;
        ReleaseMeasurements(); ReleaseRateResources(); Release(context); Release(device);
        device = pendingDevice;
        if (device) device->AddRef();
        probed = false; capable = false;
        fourByFourRejected = false; report.fourByFourNvStatus = 0;
    }
    if (probed) return capable && !fatal;
    probed = true;
    if (IsWine()) { Unavailable("wine-proton-unsupported"); return false; }
    if (!device) { probed = false; Unavailable("d3d11-device-unavailable"); return false; }
    IDXGIDevice* dxgiDevice = nullptr;
    IDXGIAdapter* adapter = nullptr;
    DXGI_ADAPTER_DESC desc{};
    HRESULT hr = device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));
    if (SUCCEEDED(hr)) hr = dxgiDevice->GetAdapter(&adapter);
    if (SUCCEEDED(hr)) hr = adapter->GetDesc(&desc);
    Release(adapter); Release(dxgiDevice);
    if (FAILED(hr)) { Unavailable("adapter-query-failed", hr); return false; }
    report.vendor = desc.VendorId;
    if (desc.VendorId != 0x10de) { Unavailable("nvidia-gpu-required"); return false; }
    if (!LoadNvApi()) return false;
    // V1 has the required capability and works on drivers predating V2.
    NV_D3D1x_GRAPHICS_CAPS_V1 caps{};
    const auto status = nvCapabilities(device, NV_D3D1x_GRAPHICS_CAPS_VER1,
        reinterpret_cast<NV_D3D1x_GRAPHICS_CAPS*>(&caps));
    if (status != NVAPI_OK) { Unavailable("vrs-capability-query-failed", S_OK, status); return false; }
    if (!caps.bVariablePixelRateShadingSupported) { Unavailable("hardware-vrs-unsupported"); return false; }
    device->GetImmediateContext(&context);
    if (!context || context->GetType() != D3D11_DEVICE_CONTEXT_IMMEDIATE)
    { Unavailable("immediate-context-unavailable"); return false; }
    if (fatal) return false;
    capable = true; report.state = "ready"; report.reason = "capability-confirmed";
    report.lastHr = S_OK; report.lastNvStatus = 0;
    return true;
}
Scope* FindScope(void* pointer)
{
    for (auto& scope : scopes) if (pointer == &scope) return &scope;
    return nullptr;
}
void Retire(Scope& scope, bool neverSubmitted = false)
{
    if (!scope.Retire(neverSubmitted)) return;
    Release(scope.texture); scope.measurement = -1; scope.measurementSequence = 0;
}
bool ValidateTarget(const Scope& scope)
{
    const auto& p = scope.pattern;
    ID3D11RenderTargetView* target = nullptr;
    context->OMGetRenderTargets(1, &target, nullptr);
    if (!target) { report.reason = "no-bound-color-target"; return false; }
    D3D11_RENDER_TARGET_VIEW_DESC viewDesc{}; target->GetDesc(&viewDesc);
    ID3D11Resource* resource = nullptr; target->GetResource(&resource);
    Release(target);
    // Only the exact native multipass render texture may receive coarse shading.
    ID3D11Texture2D* texture = nullptr;
    HRESULT hr = resource ? resource->QueryInterface(__uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&texture)) : E_NOINTERFACE;
    Release(resource);
    if (FAILED(hr)) { report.reason = "color-target-not-texture2d"; return false; }
    D3D11_TEXTURE2D_DESC desc{}; texture->GetDesc(&desc);
    const bool sameTexture = !scope.texture || texture == scope.texture;
    Release(texture);
    if (!sameTexture) { report.reason = "bound-target-does-not-match-xr-texture"; return false; }
    if (desc.ArraySize != 1 || (viewDesc.ViewDimension != D3D11_RTV_DIMENSION_TEXTURE2D && viewDesc.ViewDimension != D3D11_RTV_DIMENSION_TEXTURE2DMS))
    { report.reason = "array-or-other-target-layout-unsupported"; return false; }
    if (viewDesc.ViewDimension == D3D11_RTV_DIMENSION_TEXTURE2D && viewDesc.Texture2D.MipSlice != 0)
    { report.reason = "mip-target-unsupported"; return false; }
    if (desc.Width != p.width || desc.Height != p.height || desc.SampleDesc.Count != p.samples)
    { report.reason = "target-dimensions-or-msaa-mismatch"; return false; }
    UINT count = D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE;
    D3D11_VIEWPORT viewports[D3D11_VIEWPORT_AND_SCISSORRECT_OBJECT_COUNT_PER_PIPELINE]{};
    context->RSGetViewports(&count, viewports);
    if (count != 1 || viewports[0].TopLeftX != 0 || viewports[0].TopLeftY != 0
        || viewports[0].Width != float(p.width) || viewports[0].Height != float(p.height))
    { report.reason = "partial-or-multiple-viewports-unsupported"; return false; }
    return true;
}
bool EnsurePattern(const Pattern& pattern)
{
    auto& cache = rateCaches[pattern.eye];
    const auto update = cache.pixels.Prepare(pattern);
    if (update.kind == novr::foveation::RateMapUpdate::Invalid)
    { Failure("invalid-rate-pattern"); return false; }
    const bool recreate = !cache.texture || !cache.view
        || update.kind == novr::foveation::RateMapUpdate::Recreate;
    if (recreate)
    {
        Release(cache.view); Release(cache.texture);
        D3D11_TEXTURE2D_DESC desc{};
        desc.Width = update.width; desc.Height = update.height;
        desc.MipLevels = desc.ArraySize = desc.SampleDesc.Count = 1;
        desc.Format = DXGI_FORMAT_R8_UINT;
        // DEFAULT allows ordered content uploads without rebuilding the NVIDIA
        // shading-rate view. The old immutable path recreated both for any gaze.
        desc.Usage = D3D11_USAGE_DEFAULT;
        desc.BindFlags = D3D11_BIND_SHADER_RESOURCE;
        D3D11_SUBRESOURCE_DATA initial{};
        initial.pSysMem = cache.pixels.Data(); initial.SysMemPitch = desc.Width;
        const auto hr = device->CreateTexture2D(&desc, &initial, &cache.texture);
        if (FAILED(hr)) { Failure("rate-texture-creation-failed", hr); return false; }
        NV_D3D11_SHADING_RATE_RESOURCE_VIEW_DESC viewDesc{};
        viewDesc.version = NV_D3D11_SHADING_RATE_RESOURCE_VIEW_DESC_VER;
        viewDesc.Format = DXGI_FORMAT_R8_UINT;
        viewDesc.ViewDimension = NV_SRRV_DIMENSION_TEXTURE2D;
        const auto status = nvCreateView(device, cache.texture, &viewDesc, &cache.view);
        if (status != NVAPI_OK) { ReleaseRateResources(); Failure("rate-view-creation-failed", S_OK, status); return false; }
        ++report.resourceBuilds;
    }
    else if (update.kind == novr::foveation::RateMapUpdate::Upload)
    {
        // Previous scopes have already unbound this view. UpdateSubresource
        // snapshots the CPU bytes and queues an ordered copy even if earlier GPU
        // draws still reference this texture. No Map, Flush, fence, or wait.
        // The retained source image is safe to change after this call returns.
        D3D11_BOX box{update.left, update.top, 0, update.right, update.bottom, 1};
        const auto* source = cache.pixels.Data() + std::size_t(update.top) * update.width + update.left;
        context->UpdateSubresource(cache.texture, 0, &box, source, update.width, 0);
        ++report.resourceUpdates;
    }
    return true;
}

struct NvRateAdapter
{
    ID3D11NvShadingRateResourceView* view;
    NvAPI_Status status = NVAPI_OK;
    bool BindView()
    {
        dirty = true; // Any partial mutation must restore both NVAPI states.
        status = nvSetView(context, view);
        return status == NVAPI_OK;
    }
    bool SetRates(const novr::foveation::ShadingRates& rates)
    {
        NV_D3D11_VIEWPORT_SHADING_RATE_DESC viewport{};
        viewport.enableVariablePixelShadingRate = true;
        for (auto& entry : viewport.shadingRateTable) entry = NV_PIXEL_X1_PER_RASTER_PIXEL;
        viewport.shadingRateTable[1] = rates.middle == novr::foveation::PixelRate::HalfHorizontal
            ? NV_PIXEL_X1_PER_2X1_RASTER_PIXELS : NV_PIXEL_X1_PER_2X2_RASTER_PIXELS;
        viewport.shadingRateTable[2] = rates.outer == novr::foveation::PixelRate::Sixteenth
            ? NV_PIXEL_X1_PER_4X4_RASTER_PIXELS : NV_PIXEL_X1_PER_2X2_RASTER_PIXELS;
        NV_D3D11_VIEWPORTS_SHADING_RATE_DESC desc{};
        desc.version = NV_D3D11_VIEWPORTS_SHADING_RATE_DESC_VER;
        desc.numViewports = 1; desc.pViewports = &viewport;
        status = nvSetRates(context, &desc);
        return status == NVAPI_OK;
    }
    void RejectFourByFour()
    {
        fourByFourRejected = true;
        report.fourByFourNvStatus = status;
        ++report.coarseRateFallbacks;
    }
    bool RestoreFullRate() { return Restore(); }
};

void Begin(Scope* scope)
{
    ++report.beginEvents;
    if (!scope || !scope->ConsumeBegin())
    { FinishMeasurement(openMeasurement, true); Restore(); ++report.skippedScopes; Failure("invalid-or-replayed-begin-packet"); return; }
    if (active || dirty || openMeasurement >= 0)
    { FinishMeasurement(openMeasurement, true); Restore(); ++report.skippedScopes; Failure("overlapping-scope-rejected"); return; }
    if ((!enabled && !diagnosticsEnabled) || fatal || !EnsureDevice()) { ++report.skippedScopes; return; }
    const auto& p = scope->pattern;
    report.width = p.width; report.height = p.height; report.samples = p.samples;
    report.eye = p.eye; report.profile = p.profile;
    if (!ValidateTarget(*scope)) { ++report.skippedScopes; return; }
    StartMeasurement(*scope);
    if (!enabled) return; // Optional full-rate baseline uses the same exact scope.
    if (!EnsurePattern(p))
    { FinishScopeMeasurement(*scope, true); ++report.skippedScopes; return; }
    auto rates = novr::foveation::SelectShadingRates(p.profile, p.samples, fourByFourRejected);
    NvRateAdapter adapter{rateCaches[p.eye].view};
    const auto outcome = novr::foveation::ApplyShadingRates(adapter, rates);
    if (outcome != novr::foveation::ApplyOutcome::Applied)
    {
        FinishScopeMeasurement(*scope, true);
        const bool restored = Restore();
        ++report.skippedScopes;
        // Preserve a full-rate-restore failure, which is more important than the
        // optional coarse-rate rejection that triggered recovery.
        if (restored && !fatal) Failure("vrs-apply-failed", S_OK, adapter.status);
        return;
    }
    active = scope; ++report.appliedScopes;
    if (scope->measurement >= 0)
    {
        auto& sample = measurements[scope->measurement];
        sample.applied = true; sample.coarsestRate = rates.Coarsest(); sample.rateClamped = rates.Clamped();
    }
    if (p.eye == 0)
    {
        ++report.leftApplied; report.leftCenterX = p.centerX; report.leftCenterY = p.centerY;
        report.leftProfile = p.profile; report.leftCoarsestRate = rates.Coarsest();
        report.leftRateClamped = rates.Clamped(); report.leftClampReason = novr::foveation::ClampReason(rates.clamp);
    }
    else
    {
        ++report.rightApplied; report.rightCenterX = p.centerX; report.rightCenterY = p.centerY;
        report.rightProfile = p.profile; report.rightCoarsestRate = rates.Coarsest();
        report.rightRateClamped = rates.Clamped(); report.rightClampReason = novr::foveation::ClampReason(rates.clamp);
    }
    report.state = "ready"; report.reason = "scope-applied";
}
void UNITY_INTERFACE_API OnRenderEvent(int eventId, void* data)
{
    Exclusive lock;
    try
    {
        PollMeasurements();
        switch (eventId - EventBase)
        {
        case 0: ++report.probeEvents; EnsureDevice(); break;
        case 1: Begin(FindScope(data)); break;
        case 2:
        {
            ++report.endEvents;
            Scope* scope = FindScope(data);
            // Always restore on End, including malformed events, stale Off flags,
            // and skipped Begin callbacks. A late disable never suppresses End.
            if (scope) FinishScopeMeasurement(*scope, false);
            else FinishMeasurement(openMeasurement, true);
            Restore();
            if (scope && scope->state == Scope::Begun) Retire(*scope);
            else if (scope && scope->state == Scope::Queued)
            {
                // An End before its Begin is a protocol fault. Do not recycle
                // the queued packet: a delayed Begin may still hold its address.
                ++report.skippedScopes; Failure("end-without-begin");
            }
            else Failure("invalid-or-replayed-end-packet");
            break;
        }
        case 3:
            ++report.resetEvents;
            // Camera safety boundary: keep caches and capability probe intact.
            FinishMeasurement(openMeasurement, true);
            Restore();
            break;
        case 4:
            enabled = diagnosticsEnabled = false;
            FinishMeasurement(openMeasurement, true);
            if (Restore())
            {
                ReleaseMeasurements(); ReleaseRateResources(); Release(context); Release(device); Release(pendingDevice);
                probed = capable = false;
                if (nvInitialized && nvUnload && nvUnload() == NVAPI_OK) nvInitialized = false;
                if (!fatal) { report.state = "idle"; report.reason = "shutdown"; }
            }
            break;
        default: break;
        }
    }
    catch (...)
    {
        FinishMeasurement(openMeasurement, true);
        Restore(); Failure("native-render-event-exception");
    }
}
}

int NOVR_FFR_CALL NovrFoveationGetApiVersion() { return 2; }
int NOVR_FFR_CALL NovrFoveationGetEventId(int offset) { return offset >= 0 && offset < 5 ? EventBase + offset : -1; }
void* NOVR_FFR_CALL NovrFoveationGetRenderEventAndDataFunc() { return reinterpret_cast<void*>(&OnRenderEvent); }
int NOVR_FFR_CALL NovrFoveationInitializeFromTexture(void* nativeTexture)
{
    Exclusive lock;
    if (IsWine()) { Unavailable("wine-proton-unsupported"); return 0; }
    if (!nativeTexture || fatal) return 0;
    ID3D11Resource* resource = nullptr;
    const auto hr = static_cast<IUnknown*>(nativeTexture)->QueryInterface(__uuidof(ID3D11Resource), reinterpret_cast<void**>(&resource));
    if (FAILED(hr)) { Unavailable("native-texture-is-not-d3d11", hr); return 0; }
    ID3D11Device* next = nullptr; resource->GetDevice(&next); Release(resource);
    Release(pendingDevice); pendingDevice = next;
    if (!next) { Unavailable("texture-device-unavailable"); return 0; }
    if (!probed) { report.state = "waiting-for-probe"; report.reason = "texture-device-received"; }
    return 1;
}
int NOVR_FFR_CALL NovrFoveationSetEnabled(int value)
{
    Exclusive lock;
    enabled = value != 0 && capable && !fatal && !IsWine();
    return enabled ? 1 : 0;
}
int NOVR_FFR_CALL NovrFoveationSetDiagnostics(int value)
{
    Exclusive lock;
    diagnosticsEnabled = value != 0 && capable && !fatal && !diagnosticsFailed && !IsWine();
    if (!diagnosticsEnabled)
    {
        // Main-thread disable only marks ownership state. Render events drain
        // the GPU queries; results from the old session must never be published
        // as fresh samples after diagnostics is enabled again.
        for (auto& query : measurements)
            if (query.open || query.inFlight) query.discard = true;
    }
    if (!diagnosticsFailed) report.diagnosticsReason = diagnosticsEnabled ? "waiting-for-scope" : "off";
    return diagnosticsEnabled ? 1 : 0;
}
void* NOVR_FFR_CALL NovrFoveationAcquireScope(void* nativeTexture,
    std::uint32_t width, std::uint32_t height, std::uint32_t samples, std::uint32_t eye,
    float centerX, float centerY, float innerRadius, float outerRadius, std::uint32_t profile, std::uint32_t frameIndex)
{
    Exclusive lock;
    const Pattern p{width, height, samples, eye, centerX, centerY, innerRadius, outerRadius, profile};
    if ((!enabled && !diagnosticsEnabled) || fatal || !novr::foveation::IsValid(p)) return nullptr;
    for (auto& scope : scopes)
    {
        if (scope.state != Scope::Free) continue;
        ID3D11Texture2D* texture = nullptr;
        const auto hr = nativeTexture ? static_cast<IUnknown*>(nativeTexture)->QueryInterface(__uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&texture)) : S_OK;
        if (FAILED(hr)) { report.reason = "scope-texture-is-not-d3d11-texture2d"; report.lastHr = hr; return nullptr; }
        scope.texture = texture; scope.pattern = p; scope.frameIndex = frameIndex; scope.Acquire();
        return &scope;
    }
    ++report.poolExhausted; report.reason = "scope-pool-exhausted"; return nullptr;
}
int NOVR_FFR_CALL NovrFoveationCancelScope(void* packet)
{
    Exclusive lock;
    Scope* scope = FindScope(packet);
    if (!scope || scope->state != Scope::Queued) return 0;
    Retire(*scope, true); ++report.cancelledScopes; return 1;
}
int NOVR_FFR_CALL NovrFoveationCopyReport(char* buffer, int capacity)
{
    Exclusive lock;
    unsigned leased = 0;
    for (const auto& scope : scopes) if (scope.state != Scope::Free) ++leased;
    char text[4096];
    const int size = std::snprintf(text, sizeof(text),
        "{\"abi\":2,\"backend\":\"windows-nvidia-d3d11\",\"state\":\"%s\",\"reason\":\"%s\","
        "\"enabled\":%d,\"capable\":%d,\"scopeActive\":%d,\"fatal\":%d,\"wine\":%d,\"supported\":%s,\"failed\":%s,"
        "\"nvStatus\":%d,\"hresult\":%ld,\"vendorId\":%u,\"width\":%u,\"height\":%u,\"samples\":%u,\"eye\":%u,\"profile\":%u,"
        "\"probeEvents\":%llu,\"beginEvents\":%llu,\"endEvents\":%llu,\"resetEvents\":%llu,"
        "\"appliedScopes\":%llu,\"restoredScopes\":%llu,\"skippedScopes\":%llu,\"resourceBuilds\":%llu,"
        "\"resourceUpdates\":%llu,\"leftCenterX\":%.6f,\"leftCenterY\":%.6f,\"rightCenterX\":%.6f,\"rightCenterY\":%.6f,"
        "\"poolExhausted\":%llu,\"leasedPackets\":%u,\"restoreFailures\":%llu,\"cancelledScopes\":%llu,"
        "\"leftApplied\":%llu,\"rightApplied\":%llu,\"leftRestored\":%llu,\"rightRestored\":%llu,"
        "\"diagnosticsEnabled\":%s,\"diagnosticsReason\":\"%s\",\"queryHresult\":%ld,"
        "\"queryCompleted\":%llu,\"queryDropped\":%llu,\"queryDisjoint\":%llu,"
        "\"leftGpuMs\":%.6f,\"rightGpuMs\":%.6f,\"leftPixelInvocations\":%llu,\"rightPixelInvocations\":%llu,"
        "\"leftSampleMode\":%u,\"rightSampleMode\":%u,\"leftSequence\":%llu,\"rightSequence\":%llu,"
        "\"leftProfile\":%u,\"rightProfile\":%u,\"leftCoarsestRate\":%u,\"rightCoarsestRate\":%u,"
        "\"leftRateClamped\":%s,\"rightRateClamped\":%s,\"leftClampReason\":\"%s\",\"rightClampReason\":\"%s\","
        "\"fourByFourRejected\":%s,\"fourByFourNvStatus\":%d,\"coarseRateFallbacks\":%llu,"
        "\"leftSampleCoarsestRate\":%u,\"rightSampleCoarsestRate\":%u,\"leftSampleRateClamped\":%s,\"rightSampleRateClamped\":%s,"
        "\"hardwareValidated\":false,\"measuredPerformanceGain\":false}",
        report.state, report.reason, enabled, capable, dirty, fatal, IsWine(), capable && !fatal ? "true" : "false", fatal ? "true" : "false",
        report.lastNvStatus, report.lastHr, report.vendor, report.width, report.height, report.samples, report.eye, report.profile,
        report.probeEvents, report.beginEvents, report.endEvents, report.resetEvents,
        report.appliedScopes, report.restoredScopes, report.skippedScopes, report.resourceBuilds,
        report.resourceUpdates, report.leftCenterX, report.leftCenterY, report.rightCenterX, report.rightCenterY,
        report.poolExhausted, leased, report.restoreFailures, report.cancelledScopes,
        report.leftApplied, report.rightApplied, report.leftRestored, report.rightRestored,
        diagnosticsEnabled ? "true" : "false", report.diagnosticsReason, report.queryHr,
        report.queryCompleted, report.queryDropped, report.queryDisjoint,
        eyeMeasurements[0].gpuMs, eyeMeasurements[1].gpuMs,
        eyeMeasurements[0].pixelInvocations, eyeMeasurements[1].pixelInvocations,
        eyeMeasurements[0].mode, eyeMeasurements[1].mode,
        eyeMeasurements[0].sequence, eyeMeasurements[1].sequence,
        report.leftProfile, report.rightProfile, report.leftCoarsestRate, report.rightCoarsestRate,
        report.leftRateClamped ? "true" : "false", report.rightRateClamped ? "true" : "false",
        report.leftClampReason, report.rightClampReason, fourByFourRejected ? "true" : "false",
        report.fourByFourNvStatus, report.coarseRateFallbacks,
        eyeMeasurements[0].coarsestRate, eyeMeasurements[1].coarsestRate,
        eyeMeasurements[0].rateClamped ? "true" : "false", eyeMeasurements[1].rateClamped ? "true" : "false");
    if (size < 0 || size >= static_cast<int>(sizeof(text))) return 0;
    if (!buffer || capacity <= size) return -(size + 1);
    std::memcpy(buffer, text, static_cast<size_t>(size) + 1);
    return size;
}
