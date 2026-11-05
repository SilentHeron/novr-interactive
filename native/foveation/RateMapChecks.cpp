#include "RateMapBuffer.h"
#include <cassert>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <limits>
#include <new>

static std::size_t allocations = 0;
void* operator new(std::size_t bytes)
{
    ++allocations;
    if (void* value = std::malloc(bytes ? bytes : 1)) return value;
    throw std::bad_alloc();
}
void operator delete(void* value) noexcept { std::free(value); }
void operator delete(void* value, std::size_t) noexcept { std::free(value); }
using namespace novr::foveation;

struct UploadAdapter
{
    RateMapBuffer cpu;
    std::vector<std::uint8_t> gpu;
    unsigned builds = 0, uploads = 0;
    RateMapUpdate Apply(const Pattern& p)
    {
        const auto change = cpu.Prepare(p);
        if (change.kind == RateMapUpdate::Recreate)
        {
            gpu.assign(cpu.Data(), cpu.Data() + cpu.Size()); ++builds;
        }
        else if (change.kind == RateMapUpdate::Upload)
        {
            assert(change.left < change.right && change.right <= change.width);
            assert(change.top < change.bottom && change.bottom <= change.height);
            // Reproduce exactly the source offset, full row pitch and rectangle
            // used by production UpdateSubresource; unchanged texels stay intact.
            for (unsigned y = change.top; y < change.bottom; ++y)
                std::memcpy(gpu.data() + std::size_t(y) * change.width + change.left,
                    cpu.Data() + std::size_t(y) * change.width + change.left,
                    change.right - change.left);
            ++uploads;
        }
        return change;
    }
};

void CheckProtectedPixels(const Pattern& p, const RateMapBuffer& buffer)
{
    for (unsigned y = 0; y < p.height; y += 7)
        for (unsigned x = 0; x < p.width; x += 7)
        {
            const float dx = 2.f * (float(x) / p.width - p.centerX);
            const float dy = 2.f * (float(y) / p.height - p.centerY);
            if (dx * dx + dy * dy <= p.innerRadius * p.innerRadius)
                assert(buffer.Data()[std::size_t(y / TileSize) * Tiles(p.width) + x / TileSize] == 0);
        }
}
int main()
{
    Pattern p{2017, 1903, 4, 0, .46f, .52f, .70f, .95f, 0};
    UploadAdapter left, right;
    assert(left.Apply(p).kind == RateMapUpdate::Recreate);
    const auto* stableAddress = left.cpu.Data();
    const auto initialAllocations = allocations;
    for (unsigned frame = 0; frame < 100; ++frame)
        assert(left.Apply(p).kind == RateMapUpdate::Unchanged);
    assert(allocations == initialAllocations && left.builds == 1 && left.uploads == 0);
    assert(left.gpu == MakePattern(p)); // Validated fixed-mask algorithm is unchanged.
    p.profile = 4; // A LUT-only change cannot allocate, rebuild or upload indices.
    const auto beforeProfile = allocations;
    assert(left.Apply(p).kind == RateMapUpdate::Unchanged);
    assert(allocations == beforeProfile && left.cpu.Data() == stableAddress);
    assert(left.builds == 1 && left.uploads == 0);

    // Full gaze excursions, asymmetric eyes, and preset changes must use the
    // same resources and heap storage while preserving the protected region.
    p.eye = 1; right.Apply(p);
    const auto* rightAddress = right.cpu.Data();
    for (unsigned frame = 0; frame < 160; ++frame)
    {
        p.eye = 0; p.centerX = float(frame % 41) / 40.f;
        p.centerY = float((frame * 17) % 41) / 40.f;
        constexpr float inner[] = {.70f, .55f, .40f, .30f, .18f};
        constexpr float outer[] = {.95f, .85f, .75f, .60f, .40f};
        p.profile = frame % 5; p.innerRadius = inner[p.profile]; p.outerRadius = outer[p.profile];
        const auto before = allocations;
        left.Apply(p);
        assert(allocations == before && left.cpu.Data() == stableAddress);
        assert(left.gpu == MakePattern(p));
        if (frame % 13 == 0) CheckProtectedPixels(p, left.cpu);
        p.eye = 1; p.centerX = 1.f - p.centerX;
        const auto beforeRight = allocations;
        right.Apply(p);
        assert(allocations == beforeRight && right.cpu.Data() == rightAddress);
        assert(right.gpu == MakePattern(p));
    }
    assert(left.builds == 1 && right.builds == 1 && left.uploads > 100 && right.uploads > 100);

    // A target resize inside the same 16-pixel tile dimensions can reuse the
    // GPU image too, but must recalculate rates against the new pixel bounds.
    p.width = 2018; p.height = 1904;
    left.Apply(p); assert(left.builds == 1 && left.gpu == MakePattern(p));
    p.width = 2048; left.Apply(p);
    assert(left.builds == 2 && left.gpu == MakePattern(p));

    const auto copy = left.gpu;
    for (float invalid : {-0.001f, 1.001f, std::numeric_limits<float>::infinity(), std::numeric_limits<float>::quiet_NaN()})
    {
        Pattern bad = p; bad.centerX = invalid;
        assert(left.Apply(bad).kind == RateMapUpdate::Invalid && left.gpu == copy);
    }
    for (float x : {0.f, .01f, .99f, 1.f})
        for (float y : {0.f, .01f, .99f, 1.f})
        {
            p.centerX = x; p.centerY = y;
            left.Apply(p); assert(left.gpu == MakePattern(p));
            CheckProtectedPixels(p, left.cpu);
        }
    Pattern broad{64, 64, 1, 0, .5f, .5f, 1.25f, 1.5f, 0};
    UploadAdapter quantized;
    quantized.Apply(broad); broad.centerX = .51f;
    assert(quantized.Apply(broad).kind == RateMapUpdate::Unchanged && quantized.uploads == 0);

    Pattern maximum{16384, 16384, 1, 0, 1.f, 0.f, .18f, .40f, 4};
    RateMapBuffer bounded;
    assert(bounded.Prepare(maximum).kind == RateMapUpdate::Recreate);
    assert(bounded.Size() == 1024 * 1024 && bounded.Size() == MakePattern(maximum).size());
    const auto expectedMaximum = MakePattern(maximum);
    assert(std::memcmp(bounded.Data(), expectedMaximum.data(), bounded.Size()) == 0);
    bounded.Reset(); assert(bounded.Size() == 0);
    assert(bounded.Prepare(maximum).kind == RateMapUpdate::Recreate);
    std::cout << "Rate-map checks passed: fixed masks unchanged, allocation-free gaze/preset updates, exact boxed uploads, independent eyes, edge protection, bounded resize.\n";
}
