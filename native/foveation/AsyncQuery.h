#pragma once
#include <cstdint>
#include <limits>

namespace novr::foveation
{
enum class QueryRead { Ready, Pending, Failed };
struct SampleShadingInfo
{
    unsigned profile = 0, coarsestRate = 1;
    bool applied = false, rateClamped = false;
    // A clamped Extreme/Maximum sample still identifies that preset's smaller
    // protected region; the separate rate fields disclose its actual LUT cap.
    unsigned Mode() const { return applied ? profile + 1 : 0; }
};
struct FrameSampler
{
    std::uint64_t lastFrame = std::numeric_limits<std::uint64_t>::max();
    unsigned nextEye = 0;
    bool CanSample(std::uint32_t frame, unsigned eye) const
    { return lastFrame != frame && nextEye == eye; }
    void Record(std::uint32_t frame, unsigned eye) { lastFrame = frame; nextEye = 1 - eye; }
};
struct QueryLifecycle
{
    bool open = false, inFlight = false, discard = false;
    std::uint64_t sequence = 0;
    bool TryStart(std::uint64_t serial)
    {
        if (open || inFlight) return false;
        sequence = serial; open = true; discard = false;
        return true;
    }
    bool TryFinish(std::uint64_t serial, bool abort)
    {
        if (!open || serial != sequence) return false;
        discard = discard || abort; open = false; inFlight = true;
        return true;
    }
};
struct GpuQuerySample
{
    std::uint64_t frequency = 0, start = 0, end = 0, pixels = 0;
    bool disjoint = false;
    bool IsValid() const { return !disjoint && frequency != 0 && end >= start; }
    double Milliseconds() const { return IsValid() ? double(end - start) * 1000.0 / double(frequency) : -1; }
};
// The adapter provides one nonblocking read per method. Stop on the first
// pending/error result; no incomplete or disjoint sample may reach the UI.
template<class Reader> QueryRead ReadGpuSample(Reader& reader, GpuQuerySample& sample)
{
    auto result = reader.Disjoint(sample.frequency, sample.disjoint);
    if (result != QueryRead::Ready) return result;
    result = reader.Start(sample.start);
    if (result != QueryRead::Ready) return result;
    result = reader.End(sample.end);
    if (result != QueryRead::Ready) return result;
    return reader.Pixels(sample.pixels);
}
}
