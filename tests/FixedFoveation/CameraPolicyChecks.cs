using NOVR.Rendering;

internal static class CameraPolicyChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var eye = new Eye();
        foreach (var index in new[] { 0, 1 })
            foreach (var samples in new[] { 1, 2, 4 })
                check((eye with
                {
                    Index = index,
                    Samples = samples
                }).Reject() == null,
                    "Each multipass eye accepts supported MSAA without changing the target.");

        var rejected = new (Eye view, string label)[]
        {
            (eye with { TrackedWorldCamera = false }, "HUD, menu and untracked cameras"),
            (eye with { Offscreen = true }, "offscreen and mirror render targets"),
            (eye with { BaseCamera = false }, "overlay cameras"),
            (eye with { Xr = false }, "flat or XR-disabled rendering"),
            (eye with { SinglePass = true }, "single-pass rendering"),
            (eye with { ViewCount = 2 }, "multi-view rendering"),
            (eye with { ViewCount = 0 }, "absent views"),
            (eye with { Index = -1 }, "missing eye index"),
            (eye with { Index = 2 }, "unknown eye index"),
            (eye with { Forward = false }, "deferred rendering"),
            (eye with { ProviderFoveation = true }, "existing runtime foveation ownership"),
            (eye with { Texture2D = false }, "array, cubemap or other non-2D textures"),
            (eye with { Depth = 0 }, "missing texture depth"),
            (eye with { Depth = 2 }, "layered textures"),
            (eye with { Width = 31 }, "undersized eye width"),
            (eye with { Height = 31 }, "undersized eye height"),
            (eye with { Width = 16385 }, "oversized eye width"),
            (eye with { Height = 16385 }, "oversized eye height"),
            (eye with { Samples = 0 }, "unknown MSAA count"),
            (eye with { Samples = 3 }, "nonstandard MSAA count"),
            (eye with { Samples = 8 }, "unsupported 8x MSAA"),
            (eye with { X = 1 }, "horizontal cropped viewport origin"),
            (eye with { Y = -1 }, "vertical cropped viewport origin"),
            (eye with { ViewportWidth = 1999 }, "cropped viewport width"),
            (eye with { ViewportHeight = 2001 }, "oversized viewport height")
        };
        foreach (var (view, label) in rejected)
            check(!string.IsNullOrWhiteSpace(view.Reject()), "Fail closed with a visible reason for " + label + ".");
        check((eye with
        {
            ProviderFoveation = true
        }).Reject()!.Contains("already owns"),
            "Provider-owned foveation is reported so a second backend does not take ownership.");
        foreach (var size in new[] { 32, 16384 })
            check((eye with
            {
                Width = size,
                Height = size
            }).Reject() == null,
                "Documented eye texture size limits are inclusive.");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            check((eye with
            {
                X = invalid
            }).Reject() != null, "Invalid viewport X is rejected.");
            check((eye with
            {
                Y = invalid
            }).Reject() != null, "Invalid viewport Y is rejected.");
            check((eye with
            {
                ViewportWidth = invalid
            }).Reject() != null, "Invalid viewport width is rejected.");
            check((eye with
            {
                ViewportHeight = invalid
            }).Reject() != null, "Invalid viewport height is rejected.");
        }

        check(FixedFoveationCameraPolicy.TryCenter(0, 0, out var centreX, out var centreY) &&
              centreX == .5f && centreY == .5f, "A symmetric projection centres fixed foveation.");
        check(FixedFoveationCameraPolicy.TryCenter(.2f, .1f, out var leftX, out var leftY) &&
              Math.Abs(leftX - .4f) < .00001f && Math.Abs(leftY - .55f) < .00001f,
            "Asymmetric projection offsets determine the optical centre, including D3D top-left Y.");
        check(FixedFoveationCameraPolicy.TryCenter(-.2f, -.1f, out var rightX, out var rightY) &&
              Math.Abs(rightX - .6f) < .00001f && Math.Abs(rightY - .45f) < .00001f,
            "The opposite eye keeps its own asymmetric optical centre.");
        check(Math.Abs(leftX + rightX - 1) < .00001f && Math.Abs(leftY + rightY - 1) < .00001f,
            "Mirrored frusta produce mirrored centres rather than one shared HUD position.");
        foreach (var offset in new[] { -.6f, .6f })
            check(FixedFoveationCameraPolicy.TryCenter(offset, offset, out _, out _),
                "Conservative off-centre frusta remain supported.");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -2f, 2f, -.71f, .71f })
        {
            check(!FixedFoveationCameraPolicy.TryCenter(invalid, 0, out _, out _),
                "Non-finite or extreme horizontal projection offsets fail closed.");
            check(!FixedFoveationCameraPolicy.TryCenter(0, invalid, out _, out _),
                "Non-finite or extreme vertical projection offsets fail closed.");
        }
    }

    private sealed record Eye
    {
        internal bool TrackedWorldCamera { get; init; } = true;
        internal bool Offscreen
        {
            get; init;
        }
        internal bool Xr { get; init; } = true;
        internal bool SinglePass
        {
            get; init;
        }
        internal bool BaseCamera { get; init; } = true;
        internal bool Forward { get; init; } = true;
        internal int ViewCount { get; init; } = 1;
        internal int Index
        {
            get; init;
        }
        internal bool Texture2D { get; init; } = true;
        internal int Depth { get; init; } = 1;
        internal int Width { get; init; } = 2000;
        internal int Height { get; init; } = 2000;
        internal int Samples { get; init; } = 1;
        internal float X
        {
            get; init;
        }
        internal float Y
        {
            get; init;
        }
        internal float? ViewportWidth
        {
            get; init;
        }
        internal float? ViewportHeight
        {
            get; init;
        }
        internal bool ProviderFoveation
        {
            get; init;
        }

        internal string? Reject() => FixedFoveationCameraPolicy.Reject(TrackedWorldCamera, Offscreen,
            Xr, SinglePass, BaseCamera, Forward, ViewCount, Index, Texture2D, Depth, Width, Height,
            Samples, X, Y, ViewportWidth ?? Width, ViewportHeight ?? Height, ProviderFoveation);
    }
}
