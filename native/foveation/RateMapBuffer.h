#pragma once
#include "FoveationPattern.h"
#include <array>
#include <cstddef>

namespace novr::foveation
{
struct RateMapUpdate
{
    enum Kind { Invalid, Unchanged, Upload, Recreate } kind = Invalid;
    std::uint32_t width = 0, height = 0;
    std::uint32_t left = 0, top = 0, right = 0, bottom = 0;
};

// One retained CPU image per eye. Center/radius updates never resize its storage.
// This is shared with the production backend so portable tests exercise the
// actual decisions to reuse, upload, and recreate rather than a duplicate model.
class RateMapBuffer
{
public:
    RateMapUpdate Prepare(const Pattern& pattern)
    {
        if (!IsValid(pattern)) return {};
        const auto width = Tiles(pattern.width), height = Tiles(pattern.height);
        RateMapUpdate result{RateMapUpdate::Unchanged, width, height, width, height, 0, 0};
        if (valid_ && SamePattern(pattern, pattern_)) return result;
        const bool recreate = !valid_ || width != width_ || height != height_;
        if (recreate) pixels_.resize(std::size_t(width) * height);

        // Compute the X contribution once per column, retaining precisely the
        // same closest-tile-point arithmetic as RateAt. No per-update heap work.
        std::array<float, 16384 / TileSize> xDistanceSquared;
        for (std::uint32_t x = 0; x < width; ++x)
        {
            const float left = float(x * TileSize) / pattern.width;
            const float right = float(std::min((x + 1) * TileSize, pattern.width)) / pattern.width;
            const float dx = 2.f * (std::clamp(pattern.centerX, left, right) - pattern.centerX);
            xDistanceSquared[x] = dx * dx;
        }
        const float innerSquared = pattern.innerRadius * pattern.innerRadius;
        const float outerSquared = pattern.outerRadius * pattern.outerRadius;
        for (std::uint32_t y = 0; y < height; ++y)
        {
            const float top = float(y * TileSize) / pattern.height;
            const float bottom = float(std::min((y + 1) * TileSize, pattern.height)) / pattern.height;
            const float dy = 2.f * (std::clamp(pattern.centerY, top, bottom) - pattern.centerY);
            const float ySquared = dy * dy;
            for (std::uint32_t x = 0; x < width; ++x)
            {
                const float radiusSquared = xDistanceSquared[x] + ySquared;
                const std::uint8_t rate = radiusSquared <= innerSquared ? 0
                    : radiusSquared <= outerSquared ? 1 : 2;
                auto& previous = pixels_[std::size_t(y) * width + x];
                if (!recreate && previous == rate) continue;
                previous = rate;
                result.left = std::min(result.left, x);
                result.top = std::min(result.top, y);
                result.right = std::max(result.right, x + 1);
                result.bottom = std::max(result.bottom, y + 1);
            }
        }
        result.kind = recreate ? RateMapUpdate::Recreate
            : result.right > result.left && result.bottom > result.top ? RateMapUpdate::Upload
            : RateMapUpdate::Unchanged;
        pattern_ = pattern; width_ = width; height_ = height; valid_ = true;
        return result;
    }
    const std::uint8_t* Data() const { return pixels_.data(); }
    std::size_t Size() const { return pixels_.size(); }
    void Reset()
    {
        std::vector<std::uint8_t>().swap(pixels_);
        pattern_ = {}; width_ = height_ = 0; valid_ = false;
    }
private:
    std::vector<std::uint8_t> pixels_;
    Pattern pattern_{};
    std::uint32_t width_ = 0, height_ = 0;
    bool valid_ = false;
};
}
