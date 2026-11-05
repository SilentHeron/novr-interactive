#pragma once
#include <algorithm>
#include <cmath>
#include <cstdint>
#include <vector>

namespace novr::foveation
{
constexpr std::uint32_t TileSize = 16;
struct Pattern
{
    std::uint32_t width, height, samples, eye;
    float centerX, centerY, innerRadius, outerRadius;
    std::uint32_t profile;
};
inline bool IsValid(const Pattern& p)
{
    return p.width >= 64 && p.width <= 16384 && p.height >= 64 && p.height <= 16384
        && (p.samples == 1 || p.samples == 2 || p.samples == 4)
        && p.eye <= 1 && p.profile <= 4
        && std::isfinite(p.centerX) && std::isfinite(p.centerY)
        && std::isfinite(p.innerRadius) && std::isfinite(p.outerRadius)
        && p.centerX >= 0.f && p.centerX <= 1.f && p.centerY >= 0.f && p.centerY <= 1.f
        && p.innerRadius >= 0.15f && p.innerRadius <= 1.25f
        && p.outerRadius > p.innerRadius && p.outerRadius <= 1.5f;
}
inline std::uint32_t Tiles(std::uint32_t pixels) { return (pixels + TileSize - 1) / TileSize; }
inline bool SamePattern(const Pattern& a, const Pattern& b)
{
    // MSAA, eye and profile do not change region indices. Profile-specific
    // shading rates are selected in the viewport LUT on every scope.
    return a.width == b.width && a.height == b.height && a.centerX == b.centerX
        && a.centerY == b.centerY && a.innerRadius == b.innerRadius && a.outerRadius == b.outerRadius;
}
inline std::uint8_t RateAt(const Pattern& p, std::uint32_t tx, std::uint32_t ty)
{
    // Classify by the closest point of each tile to the protected ellipse. A tile
    // that intersects the full-rate region remains entirely full rate.
    const float left = float(tx * TileSize) / p.width;
    const float right = float(std::min((tx + 1) * TileSize, p.width)) / p.width;
    const float top = float(ty * TileSize) / p.height;
    const float bottom = float(std::min((ty + 1) * TileSize, p.height)) / p.height;
    const float dx = 2.f * (std::clamp(p.centerX, left, right) - p.centerX);
    const float dy = 2.f * (std::clamp(p.centerY, top, bottom) - p.centerY);
    const float radius2 = dx * dx + dy * dy;
    return radius2 <= p.innerRadius * p.innerRadius ? 0
         : radius2 <= p.outerRadius * p.outerRadius ? 1 : 2;
}
inline std::vector<std::uint8_t> MakePattern(const Pattern& p)
{
    if (!IsValid(p)) return {};
    const auto w = Tiles(p.width), h = Tiles(p.height);
    std::vector<std::uint8_t> result(w * h);
    for (std::uint32_t y = 0; y < h; ++y)
        for (std::uint32_t x = 0; x < w; ++x) result[y * w + x] = RateAt(p, x, y);
    return result;
}
}
