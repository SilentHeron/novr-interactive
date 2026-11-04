using System;
using System.Threading;

namespace NOVR.MotionControls;

// Managed RGBA bytes and a single-channel fabric tile only: no Unity API calls.
internal static class PilotTextureEnhancement
{
    // Recolour copies only, retaining seams/folds/padding and material normal
    // maps. A neutral luminance ramp removes the old uniform's hue instead of
    // multiplying a red or green tint through an already coloured atlas.
    internal static byte[] Recolor(byte[] input, int width, int height, PilotUniformTeam team,
        bool glove, CancellationToken cancellation)
    {
        if (width < 1 || height < 1 || input.Length != (long)width * height * 4)
            throw new ArgumentException("Invalid pilot colour texture dimensions or pixel data.");
        cancellation.ThrowIfCancellationRequested();
        var output = (byte[])input.Clone();
        if (team == PilotUniformTeam.Native) return output;
        var color = PilotUniformPalette.Color(team, glove);
        var ramp = new byte[256 * 3];
        for (var value = 0; value < 256; value++)
        {
            // Build once rather than evaluating a power for every atlas texel.
            var shade = Math.Pow(value / 96.0, .75);
            // Preserve the approved glove ramp. Cloth needs a smooth shoulder:
            // the old hard cap flattened every fold brighter than ~129/255.
            shade = glove ? Math.Min(1.25, shade) : shade <= 1 ? shade
                : 1 + .36 * (1 - Math.Exp(-(shade - 1) / .36));
            ramp[value * 3] = (byte)Math.Min(255, Math.Round(color.red * shade));
            ramp[value * 3 + 1] = (byte)Math.Min(255, Math.Round(color.green * shade));
            ramp[value * 3 + 2] = (byte)Math.Min(255, Math.Round(color.blue * shade));
        }
        for (var y = 0; y < height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var at = (y * width + x) * 4;
                if (input[at + 3] == 0) continue;
                // Keep dark stitching dark and cap highlights below white so
                // both pale cloth and black cloth retain readable surface detail.
                var tone = Math.Min(255, (int)Math.Round(Luminance(input, at))) * 3;
                output[at] = ramp[tone]; output[at + 1] = ramp[tone + 1]; output[at + 2] = ramp[tone + 2];
            }
        }
        return output;
    }

    internal static byte[] ApplyNormal(byte[] input, int width, int height, byte[] fabric,
        int fabricWidth, int fabricHeight, CancellationToken cancellation)
    {
        if (width < 1 || width > 2048 || height < 1 || height > 2048 ||
            input.Length != (long)width * height * 4 || fabricWidth < 1 || fabricHeight < 1 ||
            fabric.Length != (long)fabricWidth * fabricHeight)
            throw new ArgumentException("Invalid pilot normal texture dimensions or pixel data.");
        cancellation.ThrowIfCancellationRequested();
        var tileWidth = Math.Max(1, width / 4);
        var tileHeight = Math.Max(1, height / 4);
        var tile = ResampleFabric(fabric, fabricWidth, fabricHeight, tileWidth, tileHeight, cancellation);
        var dx = new float[tile.Length]; var dy = new float[tile.Length];
        var maximumSlope = 16f;
        for (var y = 0; y < tileHeight; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var x = 0; x < tileWidth; x++)
            {
                var at = y * tileWidth + x;
                dx[at] = (tile[y * tileWidth + (x + 1) % tileWidth] - tile[y * tileWidth + (x + tileWidth - 1) % tileWidth]) * .5f;
                dy[at] = (tile[((y + 1) % tileHeight) * tileWidth + x] - tile[((y + tileHeight - 1) % tileHeight) * tileWidth + x]) * .5f;
                maximumSlope = Math.Max(maximumSlope, (float)Math.Sqrt(dx[at] * dx[at] + dy[at] * dy[at]));
            }
        }
        var output = (byte[])input.Clone();
        for (var y = 0; y < height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var x = 0; x < width; x++)
            {
                var tileAt = y % tileHeight * tileWidth + x % tileWidth;
                if (Math.Abs(dx[tileAt]) + Math.Abs(dy[tileAt]) < .00001f) continue;
                var at = (y * width + x) * 4;
                // Unity desktop UnpackNormalmapRGorAG: X is red * alpha,
                // Y is green. Alpha is a normal channel, not coverage.
                var nx = input[at] * input[at + 3] / (255.0 * 255.0) * 2 - 1;
                var ny = input[at + 1] / 255.0 * 2 - 1;
                var nz = Math.Sqrt(Math.Max(0, 1 - nx * nx - ny * ny));
                var nLength = Math.Sqrt(nx * nx + ny * ny + nz * nz);
                nx /= nLength; ny /= nLength; nz /= nLength;
                // At most 8.6 degrees of weave relief; steep native folds take
                // less detail so their tangent-space normal cannot invert.
                var strength = .15 * Math.Max(.15, nz) / maximumSlope;
                var bx = -dx[tileAt] * strength; var by = -dy[tileAt] * strength;
                var detailLength = Math.Sqrt(bx * bx + by * by + 1);
                bx /= detailLength; by /= detailLength; var bz = 1 / detailLength;
                // Reoriented normal mapping retains the original fold frame.
                // Simply adding XY would flatten or skew the authored normals.
                var tz = nz + 1;
                var projection = (-nx * bx - ny * by + tz * bz) / tz;
                var rx = nx * projection + bx;
                var ry = ny * projection + by;
                var rz = Math.Max(.001, tz * projection - bz);
                var length = Math.Sqrt(rx * rx + ry * ry + rz * rz);
                output[at] = output[at + 2] = 255;
                output[at + 1] = EncodeNormal(ry / length);
                output[at + 3] = EncodeNormal(rx / length);
            }
        }
        return output;
    }

    private static byte EncodeNormal(double value) =>
        (byte)Math.Max(0, Math.Min(255, Math.Round((value * .5 + .5) * 255)));

    internal static byte[] Apply(byte[] input, int size, byte[] fabric, int fabricWidth,
        int fabricHeight, CancellationToken cancellation) =>
        Apply(input, size, size, fabric, fabricWidth, fabricHeight, cancellation);

    internal static byte[] Apply(byte[] input, int width, int height, byte[] fabric, int fabricWidth,
        int fabricHeight, CancellationToken cancellation)
    {
        if (width < 1 || width > 2048 || height < 1 || height > 2048 ||
            input.Length != (long)width * height * 4 || fabricWidth < 1 || fabricHeight < 1 ||
            fabric.Length != (long)fabricWidth * fabricHeight)
            throw new ArgumentException("Invalid pilot texture dimensions or pixel data.");
        cancellation.ThrowIfCancellationRequested();

        // Filter once at the actual output footprint before repeating. Point sampling
        // a large weave tile at four repeats aliases threads into noisy dark/light dots.
        var tileWidth = Math.Max(1, width / 4);
        var tileHeight = Math.Max(1, height / 4);
        var tile = ResampleFabric(fabric, fabricWidth, fabricHeight, tileWidth, tileHeight, cancellation);
        double sum = 0;
        foreach (var value in tile) sum += value;
        var mean = (float)(sum / tile.Length);
        var deviation = 32f;
        foreach (var value in tile) deviation = Math.Max(deviation, Math.Abs(value - mean));
        for (var i = 0; i < tile.Length; i++) tile[i] = (tile[i] - mean) * (.055f / deviation);

        var output = new byte[input.Length];
        for (var y = 0; y < height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            var row = y * width;
            var above = Math.Min(height - 1, y + 1) * width;
            var below = Math.Max(0, y - 1) * width;
            var fabricRow = (y % tileHeight) * tileWidth;
            for (var x = 0; x < width; x++)
            {
                var at = (row + x) * 4;
                var left = (row + Math.Max(0, x - 1)) * 4;
                var right = (row + Math.Min(width - 1, x + 1)) * 4;
                var up = (above + x) * 4;
                var down = (below + x) * 4;
                var value = Luminance(input, at);
                var low = value; var high = value;
                var neighbours = Neighbour(input, at, left, value, ref low, ref high) +
                    Neighbour(input, at, right, value, ref low, ref high) +
                    Neighbour(input, at, up, value, ref low, ref high) +
                    Neighbour(input, at, down, value, ref low, ref high);
                // Strong printed edges, seams and atlas islands retain their original
                // contrast. Within cloth, never sharpen outside the local value range.
                var edge = Math.Min(1f, (high - low) / 24f);
                var contrast = Math.Max(-2f, Math.Min(2f, .2f * (value - neighbours * .25f))) * (1f - edge);
                contrast = Math.Max(low - value, Math.Min(high - value, contrast));
                var grain = tile[fabricRow + x % tileWidth] * (1f - edge);
                // A shared multiplier keeps the fabric hue and leaves black markings
                // black. Additive gray grain would wash out saturated/dark uniforms.
                var scale = 1f + grain + contrast / Math.Max(16f, value);
                var brightest = Math.Max(input[at], Math.Max(input[at + 1], input[at + 2]));
                if (brightest > 0) scale = Math.Min(scale, 255f / brightest);
                if (input[at + 3] == 0) scale = 1f;
                for (var channel = 0; channel < 3; channel++)
                    output[at + channel] = (byte)Math.Max(0, Math.Min(255,
                        (int)Math.Round(input[at + channel] * scale, MidpointRounding.ToEven)));
                output[at + 3] = input[at + 3];
            }
        }
        return output;
    }

    private static float Luminance(byte[] pixels, int at) =>
        pixels[at] * .2126f + pixels[at + 1] * .7152f + pixels[at + 2] * .0722f;

    private static float Neighbour(byte[] pixels, int at, int neighbour, float value, ref float low, ref float high)
    {
        // Do not pull invisible padding or a different coverage island into a seam.
        if (pixels[at + 3] != pixels[neighbour + 3]) return value;
        var other = Luminance(pixels, neighbour);
        low = Math.Min(low, other); high = Math.Max(high, other);
        return other;
    }

    private static float[] ResampleFabric(byte[] source, int width, int height, int targetWidth,
        int targetHeight, CancellationToken cancellation)
    {
        var horizontal = new float[targetWidth * height];
        for (var y = 0; y < height; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var x = 0; x < targetWidth; x++)
                horizontal[y * targetWidth + x] = FilterLine(source, y * width, 1, width, x, targetWidth);
        }
        var result = new float[targetWidth * targetHeight];
        for (var y = 0; y < targetHeight; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for (var x = 0; x < targetWidth; x++)
                result[y * targetWidth + x] = FilterLine(horizontal, x, targetWidth, height, y, targetHeight);
        }
        return result;
    }

    private static float FilterLine<T>(T[] source, int start, int stride, int size, int index, int targetSize)
        where T : struct, IConvertible
    {
        if (targetSize >= size)
        {
            // Wrapped, pixel-centred bilinear magnification keeps the repeat seam smooth.
            var position = (index + .5f) * size / targetSize - .5f;
            var lower = (int)Math.Floor(position);
            var blend = position - lower;
            var a = source[start + ((lower + size) % size) * stride].ToSingle(null);
            var b = source[start + ((lower + 1) % size) * stride].ToSingle(null);
            return a + (b - a) * blend;
        }
        // Exact area average also handles odd source dimensions without dropping texels.
        var from = (double)index * size / targetSize;
        var to = (double)(index + 1) * size / targetSize;
        double sum = 0;
        for (var sample = (int)from; sample < Math.Ceiling(to); sample++)
            sum += source[start + Math.Min(size - 1, sample) * stride].ToSingle(null) *
                (Math.Min(to, sample + 1) - Math.Max(from, sample));
        return (float)(sum / (to - from));
    }
}
