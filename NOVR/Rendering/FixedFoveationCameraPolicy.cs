using System;

namespace NOVR.Rendering;

// No guessed support: reject render layouts or providers we cannot bracket.
internal static class FixedFoveationCameraPolicy
{
    internal static string? Reject(bool trackedWorldCamera, bool offscreen, bool xr, bool singlePass,
        bool baseCamera, bool forward, int viewCount, int eye, bool texture2D, int depth,
        int width, int height, int samples, float x, float y, float viewportWidth, float viewportHeight,
        bool providerFoveation)
    {
        if (!trackedWorldCamera || offscreen || !baseCamera) return "Waiting for the outside-world camera.";
        if (!xr || singlePass || viewCount != 1 || eye < 0 || eye > 1) return "Unsupported stereo layout.";
        if (!forward) return "Unsupported rendering path.";
        if (providerFoveation) return "Runtime foveation already owns this view.";
        if (!texture2D || depth != 1 || width < 32 || height < 32 || width > 16384 || height > 16384 ||
            (samples != 1 && samples != 2 && samples != 4)) return "Unsupported eye texture.";
        if (!Finite(x) || !Finite(y) || !Finite(viewportWidth) || !Finite(viewportHeight) ||
            Math.Abs(x) > .01f || Math.Abs(y) > .01f ||
            Math.Abs(viewportWidth - width) > .01f || Math.Abs(viewportHeight - height) > .01f)
            return "Unsupported cropped eye viewport.";
        return null;
    }

    internal static bool TryCenter(float projectionOffsetX, float projectionOffsetY, out float x, out float y)
    {
        // Forward (-Z) projected into each asymmetric eye frustum; D3D textures
        // use top-left Y. Fixed foveation follows view direction, not the HUD cue.
        x = (1 - projectionOffsetX) * .5f;
        y = (1 + projectionOffsetY) * .5f;
        return Finite(x) && Finite(y) && x >= .15f && x <= .85f && y >= .15f && y <= .85f;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
