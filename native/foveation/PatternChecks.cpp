#include "FoveationPattern.h"
#include <cassert>
#include <iostream>
#include <limits>

using namespace novr::foveation;
std::uint64_t MaskHash(const std::vector<std::uint8_t>& pixels)
{
    std::uint64_t hash = 14695981039346656037ULL;
    for (const auto value : pixels) { hash ^= value; hash *= 1099511628211ULL; }
    return hash;
}
int main()
{
    const Pattern mild{2017, 1903, 4, 0, .46f, .52f, .70f, .95f, 0};
    Pattern balanced = mild; balanced.innerRadius = .55f; balanced.outerRadius = .85f; balanced.profile = 1;
    Pattern strong = mild; strong.innerRadius = .40f; strong.outerRadius = .75f; strong.profile = 2;
    const auto a = MakePattern(mild), b = MakePattern(balanced), c = MakePattern(strong);
    // Golden masks from the unchanged .25 classifier, including partial edge
    // tiles and an asymmetric optical center. Rate selection must not alter them.
    assert(MaskHash(a) == 7181457753783233267ULL);
    assert(MaskHash(b) == 153532756478496121ULL);
    assert(MaskHash(c) == 18404540112613910526ULL);
    Pattern extreme = strong; extreme.profile = 3; extreme.innerRadius = .30f; extreme.outerRadius = .60f;
    Pattern maximum = strong; maximum.profile = 4; maximum.innerRadius = .18f; maximum.outerRadius = .40f;
    const auto d = MakePattern(extreme), e = MakePattern(maximum);
    assert(d.size() == c.size() && e.size() == c.size());
    assert(a.size() == size_t(Tiles(mild.width)) * Tiles(mild.height));
    assert(a.size() == b.size() && a.size() == c.size());
    unsigned full = 0, half = 0, quarter = 0;
    for (size_t i = 0; i < a.size(); ++i)
    {
        // More aggressive presets never make a tile more expensive.
        assert(a[i] <= b[i] && b[i] <= c[i] && c[i] <= d[i] && d[i] <= e[i] && e[i] <= 2);
        full += a[i] == 0; half += a[i] == 1; quarter += a[i] == 2;
    }
    assert(full && half && quarter);
    assert(std::count(c.begin(), c.end(), 0) > std::count(d.begin(), d.end(), 0));
    assert(std::count(d.begin(), d.end(), 0) > std::count(e.begin(), e.end(), 0));
    assert(std::count(d.begin(), d.end(), 2) < std::count(e.begin(), e.end(), 2));
    assert(RateAt(strong, unsigned(strong.centerX * strong.width) / TileSize,
        unsigned(strong.centerY * strong.height) / TileSize) == 0);
    // Every pixel inside the full-rate ellipse is protected, even across tiles.
    for (unsigned y = 0; y < strong.height; y += 7)
        for (unsigned x = 0; x < strong.width; x += 7)
        {
            const float dx = 2.f * (float(x) / strong.width - strong.centerX);
            const float dy = 2.f * (float(y) / strong.height - strong.centerY);
            if (dx * dx + dy * dy <= strong.innerRadius * strong.innerRadius)
                assert(RateAt(strong, x / TileSize, y / TileSize) == 0);
        }
    assert(Tiles(1) == 1 && Tiles(16) == 1 && Tiles(17) == 2);
    Pattern bad = mild; bad.samples = 8; assert(!IsValid(bad));
    bad = mild; bad.eye = 2; assert(!IsValid(bad));
    bad = mild; bad.profile = 5; assert(!IsValid(bad));
    bad = maximum; bad.innerRadius = .15f; assert(IsValid(bad));
    bad.innerRadius = .149f; assert(!IsValid(bad));
    bad = mild; bad.outerRadius = bad.innerRadius; assert(!IsValid(bad));
    bad = mild; bad.centerX = std::numeric_limits<float>::quiet_NaN(); assert(!IsValid(bad));
    bad = mild; bad.innerRadius = -1; assert(MakePattern(bad).empty());
    bad = mild; bad.width = 0; assert(MakePattern(bad).empty());
    bad = mild; bad.width = 16385; assert(MakePattern(bad).empty());
    assert(SamePattern(mild, mild));
    bad = mild; bad.eye = 1; assert(SamePattern(mild, bad));
    bad.profile = 4; bad.samples = 1; assert(SamePattern(mild, bad));
    bad.centerX = .54f; assert(!SamePattern(mild, bad));
    std::cout << "Pattern checks passed: bounded rates, protected center, asymmetric eyes, non-tile dimensions, presets, invalid input.\n";
}
