#pragma once
#include <cstdint>

namespace novr::foveation
{
enum class PixelRate { Full, HalfHorizontal, Quarter, Sixteenth };
enum class RateClamp { None, Msaa, Driver };
struct ShadingRates
{
    // Mild/Balanced/Strong keep their region radii but shade the middle ring
    // at full rate: 2x1 smeared vertical edges and the game's alpha-to-coverage
    // foliage (trees, grass) inside the sharp lens area. Only the outer edge
    // is coarse for these presets.
    PixelRate middle = PixelRate::Full;
    PixelRate outer = PixelRate::Quarter;
    RateClamp clamp = RateClamp::None;
    unsigned Coarsest() const { return outer == PixelRate::Sixteenth ? 4 : 2; }
    bool Clamped() const { return clamp != RateClamp::None; }
};
inline const char* ClampReason(RateClamp clamp)
{
    return clamp == RateClamp::Msaa ? "msaa" : clamp == RateClamp::Driver ? "driver" : "none";
}
inline ShadingRates SelectShadingRates(std::uint32_t profile, std::uint32_t samples, bool fourByFourRejected)
{
    if (profile < 3) return {}; // Mild/Balanced/Strong: 1x1 / 1x1 / 2x2.
    // NVIDIA documents 4x4 at one sample per raster pixel. The public sample
    // order structs likewise provide 4x4 X1 only, but 2x2 X1/X2/X4.
    if (samples != 1) return {PixelRate::Quarter, PixelRate::Quarter, RateClamp::Msaa};
    if (fourByFourRejected) return {PixelRate::Quarter, PixelRate::Quarter, RateClamp::Driver};
    return {PixelRate::Quarter, PixelRate::Sixteenth, RateClamp::None};
}
enum class ApplyOutcome { Applied, ViewFailed, RatesFailed, RestoreFailed };
// The production NVAPI adapter and portable failure-injection tests share this
// exact ordering. Only rejection of the optional 4x4 LUT gets a fallback retry;
// view failures or failed full-rate restoration cannot be hidden as a clamp.
template<class Adapter> ApplyOutcome ApplyShadingRates(Adapter& adapter, ShadingRates& rates)
{
    if (!adapter.BindView()) return ApplyOutcome::ViewFailed;
    if (adapter.SetRates(rates)) return ApplyOutcome::Applied;
    if (rates.outer != PixelRate::Sixteenth) return ApplyOutcome::RatesFailed;
    adapter.RejectFourByFour();
    if (!adapter.RestoreFullRate()) return ApplyOutcome::RestoreFailed;
    rates.outer = PixelRate::Quarter; rates.clamp = RateClamp::Driver;
    if (!adapter.BindView()) return ApplyOutcome::ViewFailed;
    return adapter.SetRates(rates) ? ApplyOutcome::Applied : ApplyOutcome::RatesFailed;
}
}
