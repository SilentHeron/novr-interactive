using System;
using System.Threading;

namespace NOVR.MotionControls;

// Managed RGBA bytes and a single-channel fabric tile only: no Unity API calls.
internal static class PilotTextureEnhancement
{
    internal static byte[] Apply(byte[] input, int size, byte[] fabric, int fabricWidth,
        int fabricHeight, CancellationToken cancellation)
    {
        if (size < 1 || size > 2048 || input.Length != (long)size * size * 4 ||
            fabricWidth < 1 || fabricHeight < 1 || fabric.Length != (long)fabricWidth * fabricHeight)
            throw new ArgumentException("Invalid pilot texture dimensions or pixel data.");
        cancellation.ThrowIfCancellationRequested();
        var output = new byte[input.Length];
        for (var y = 0; y < size; y++)
        {
            cancellation.ThrowIfCancellationRequested();
            var row = y * size;
            var above = Math.Min(size - 1, y + 1) * size;
            var below = Math.Max(0, y - 1) * size;
            var fabricRow = ((y * 4) % size) * fabricHeight / size * fabricWidth;
            for (var x = 0; x < size; x++)
            {
                var at = (row + x) * 4;
                var left = (row + Math.Max(0, x - 1)) * 4;
                var right = (row + Math.Min(size - 1, x + 1)) * 4;
                var up = (above + x) * 4;
                var down = (below + x) * 4;
                var grain = (fabric[fabricRow + ((x * 4) % size) * fabricWidth / size] - 128) * .12f;
                for (var channel = 0; channel < 3; channel++)
                {
                    var value = input[at + channel];
                    var neighbours = input[left + channel] + input[right + channel] + input[up + channel] + input[down + channel];
                    var sharpened = value + .35f * (value - neighbours * .25f) + grain;
                    output[at + channel] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(sharpened, MidpointRounding.ToEven)));
                }
                output[at + 3] = input[at + 3];
            }
        }
        return output;
    }
}
