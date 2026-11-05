#include "AsyncQuery.h"
#include "ScopeLease.h"
#include <array>
#include <cassert>
#include <cmath>
#include <iostream>
using namespace novr::foveation;

// An adapter double for the exact reader used by the Windows GetData path.
struct Reader
{
    std::array<QueryRead, 4> states;
    unsigned calls = 0;
    QueryRead Disjoint(std::uint64_t& frequency, bool& disjoint)
    { frequency = 1000000; disjoint = false; return states[calls++]; }
    QueryRead Start(std::uint64_t& value) { value = 1000; return states[calls++]; }
    QueryRead End(std::uint64_t& value) { value = 3500; return states[calls++]; }
    QueryRead Pixels(std::uint64_t& value) { value = 123456; return states[calls++]; }
};
int main()
{
    constexpr auto ready = QueryRead::Ready, pending = QueryRead::Pending, failed = QueryRead::Failed;
    for (unsigned stop = 0; stop < 4; ++stop)
        for (const auto state : {pending, failed})
        {
            Reader reader{{ready,ready,ready,ready}}; reader.states[stop] = state;
            GpuQuerySample sample;
            assert(ReadGpuSample(reader, sample) == state);
            assert(reader.calls == stop + 1); // No polling loop or later reads.
        }
    Reader reader{{ready,ready,ready,ready}};
    GpuQuerySample sample;
    assert(ReadGpuSample(reader, sample) == ready && reader.calls == 4);
    assert(sample.IsValid() && std::abs(sample.Milliseconds() - 2.5) < 1e-9 && sample.pixels == 123456);
    sample.disjoint = true; assert(!sample.IsValid() && sample.Milliseconds() == -1);
    sample.disjoint = false; sample.frequency = 0; assert(!sample.IsValid());
    sample.frequency = 1000000; sample.end = 999; assert(!sample.IsValid());
    ScopeLease lease;
    assert(!lease.Retire(false) && lease.state == ScopeLease::Free);
    assert(lease.Acquire() && !lease.Acquire());
    assert(!lease.Retire(false) && lease.state == ScopeLease::Queued); // End before Begin retains the lease.
    assert(lease.Retire(true) && lease.state == ScopeLease::Free); // Explicit never-submitted cancel.
    assert(lease.Acquire() && lease.ConsumeBegin() && !lease.ConsumeBegin());
    assert(!lease.Retire(true) && lease.state == ScopeLease::Begun); // Never cancel an executing packet.
    assert(lease.Retire(false) && lease.state == ScopeLease::Free);
    FrameSampler cadence;
    assert(cadence.CanSample(0, 0) && !cadence.CanSample(0, 1));
    cadence.Record(0, 0);
    assert(!cadence.CanSample(0, 0) && !cadence.CanSample(0, 1));
    assert(!cadence.CanSample(1, 0) && cadence.CanSample(1, 1));
    cadence.Record(1, 1);
    assert(!cadence.CanSample(1, 0) && !cadence.CanSample(1, 1));
    assert(cadence.CanSample(2, 0));
    QueryLifecycle query;
    assert(query.TryStart(1));
    assert(!query.TryStart(2)); // Cannot overlap an open statistics query.
    assert(!query.TryFinish(2, false)); // A stale or unrelated End cannot close it.
    assert(query.TryFinish(1, true) && query.inFlight && query.discard);
    assert(!query.TryStart(2)); // Cannot reuse before asynchronous completion.
    query.inFlight = false;
    assert(query.TryStart(2) && !query.discard);
    assert(!query.TryFinish(1, false) && query.open); // Delayed End after Reset/reuse.
    assert(query.TryFinish(2, false) && !query.discard);
    query.inFlight = false;
    assert(query.TryStart(3));
    query.discard = true; // Main-thread diagnostics disable while GPU scope is open.
    assert(query.TryFinish(3, false) && query.inFlight && query.discard);
    for (unsigned profile = 0; profile <= 4; ++profile)
    {
        SampleShadingInfo shading; shading.profile = profile;
        assert(shading.Mode() == 0 && shading.coarsestRate == 1 && !shading.rateClamped);
        shading.applied = true; shading.coarsestRate = profile < 3 ? 2 : 4;
        assert(shading.Mode() == profile + 1);
        if (profile >= 3)
        {
            shading.coarsestRate = 2; shading.rateClamped = true;
            // Modes 4/5 retain their smaller masks even when 4x4 is unavailable.
            assert(shading.Mode() == profile + 1 && shading.coarsestRate == 2 && shading.rateClamped);
        }
    }
    std::cout << "Query checks passed: pending/error short-circuit, completed timing, disjoint rejection, abort, delayed End, bounded reuse, one-query-per-frame eye cadence, packet lease retirement, disable discard preservation.\n";
}
