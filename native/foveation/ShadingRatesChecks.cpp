#include "ShadingRates.h"
#include <array>
#include <cassert>
#include <cstring>
#include <iostream>
#include <string>
using namespace novr::foveation;

// Executes the same fallback state machine as production, injecting failures at
// every mutation. B=bind, S=set LUT, X=remember rejection, R=restore full rate.
struct Adapter
{
    std::string order;
    std::array<bool, 2> bind{{true, true}}, set{{true, true}};
    std::array<PixelRate, 2> actual{{PixelRate::Full, PixelRate::Full}};
    unsigned binds = 0, sets = 0, rejections = 0;
    bool restore = true;
    bool BindView() { order += 'B'; return bind.at(binds++); }
    bool SetRates(const ShadingRates& rates)
    { order += 'S'; actual.at(sets) = rates.outer; return set.at(sets++); }
    void RejectFourByFour() { order += 'X'; ++rejections; }
    bool RestoreFullRate() { order += 'R'; return restore; }
};
int main()
{
    for (unsigned profile = 0; profile < 3; ++profile)
        for (unsigned samples : {1u, 2u, 4u})
            for (bool rejected : {false, true})
            {
                const auto r = SelectShadingRates(profile, samples, rejected);
                assert(r.middle == PixelRate::Full && r.outer == PixelRate::Quarter);
                assert(r.Coarsest() == 2 && !r.Clamped());
            }
    for (unsigned profile : {3u, 4u})
    {
        auto r = SelectShadingRates(profile, 1, false);
        assert(r.middle == PixelRate::Quarter && r.outer == PixelRate::Sixteenth);
        assert(r.Coarsest() == 4 && !r.Clamped());
        Adapter accepted;
        assert(ApplyShadingRates(accepted, r) == ApplyOutcome::Applied && accepted.order == "BS");
        for (unsigned samples : {2u, 4u})
        {
            r = SelectShadingRates(profile, samples, false);
            assert(r.middle == PixelRate::Quarter && r.outer == PixelRate::Quarter && r.Coarsest() == 2);
            assert(r.Clamped() && std::strcmp(ClampReason(r.clamp), "msaa") == 0);
        }
        r = SelectShadingRates(profile, 1, false);
        Adapter fallback; fallback.set[0] = false;
        assert(ApplyShadingRates(fallback, r) == ApplyOutcome::Applied);
        assert(fallback.order == "BSXRBS" && fallback.rejections == 1);
        assert(fallback.actual[0] == PixelRate::Sixteenth && fallback.actual[1] == PixelRate::Quarter);
        assert(r.Coarsest() == 2 && std::strcmp(ClampReason(r.clamp), "driver") == 0);
        r = SelectShadingRates(profile, 1, true);
        Adapter latched;
        assert(ApplyShadingRates(latched, r) == ApplyOutcome::Applied && latched.order == "BS");
        assert(latched.actual[0] == PixelRate::Quarter && latched.rejections == 0);
    }
    auto r = SelectShadingRates(3, 1, false);
    Adapter bindFailure; bindFailure.bind[0] = false;
    assert(ApplyShadingRates(bindFailure, r) == ApplyOutcome::ViewFailed && bindFailure.order == "B");
    r = SelectShadingRates(2, 1, false);
    Adapter ordinaryFailure; ordinaryFailure.set[0] = false;
    assert(ApplyShadingRates(ordinaryFailure, r) == ApplyOutcome::RatesFailed && ordinaryFailure.order == "BS");
    r = SelectShadingRates(3, 1, false);
    Adapter restoreFailure; restoreFailure.set[0] = false; restoreFailure.restore = false;
    assert(ApplyShadingRates(restoreFailure, r) == ApplyOutcome::RestoreFailed && restoreFailure.order == "BSXR");
    r = SelectShadingRates(3, 1, false);
    Adapter retryBindFailure; retryBindFailure.set[0] = false; retryBindFailure.bind[1] = false;
    assert(ApplyShadingRates(retryBindFailure, r) == ApplyOutcome::ViewFailed && retryBindFailure.order == "BSXRB");
    r = SelectShadingRates(4, 1, false);
    Adapter retryRatesFailure; retryRatesFailure.set = {{false, false}};
    assert(ApplyShadingRates(retryRatesFailure, r) == ApplyOutcome::RatesFailed && retryRatesFailure.order == "BSXRBS");
    std::cout << "Shading-rate checks passed: unchanged old rates, MSAA cap, optional 4x4 acceptance/rejection, safe restore-and-retry order, failure boundaries, rejection latch.\n";
}
