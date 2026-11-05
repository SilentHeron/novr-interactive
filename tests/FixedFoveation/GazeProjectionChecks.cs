using System.Numerics;
using NOVR.Rendering;

internal static class GazeProjectionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        void Near(float expected, float actual, string label) => check(Math.Abs(expected - actual) < .00001f, label);
        var frusta = new[]
        {
            Matrix4x4.CreatePerspectiveOffCenter(-.1f, .1f, -.1f, .1f, .1f, 100),
            Matrix4x4.CreatePerspectiveOffCenter(-.08f, .12f, -.09f, .11f, .1f, 100),
            Matrix4x4.CreatePerspectiveOffCenter(-.12f, .08f, -.11f, .09f, .1f, 100),
            Matrix4x4.CreatePerspectiveOffCenter(-.12f, .16f, -.08f, .14f, .1f, 100)
        };
        foreach (var matrix in frusta)
            foreach (var horizontal in new[] { -1.4f, -.5f, -.1f, 0, .1f, .5f, 1.4f })
                foreach (var vertical in new[] { -.9f, -.15f, 0, .15f, .9f })
                {
                    var direction = Vector3.Normalize(new Vector3(horizontal, vertical, 1));
                    check(FoveationGazePolicy.TryProject(direction.X, direction.Y, direction.Z,
                        matrix.M11, matrix.M22, matrix.M31, matrix.M32, out var x, out var y), "Valid gaze projects through each asymmetric frustum.");
                    // Independent homogeneous projection. System.Numerics uses
                    // row vectors; the camera ray is -Z-forward and texture Y flips.
                    var clip = Vector4.Transform(new Vector4(direction.X, direction.Y, -direction.Z, 0), matrix);
                    Near(Math.Clamp((clip.X / clip.W + 1) / 2, 0, 1), x, "Horizontal projection agrees with perspective matrix.");
                    Near(Math.Clamp((1 - clip.Y / clip.W) / 2, 0, 1), y, "Vertical projection agrees with top-left texture origin.");
                }

        foreach (var offsetX in new[] { -.4f, 0, .4f })
            foreach (var offsetY in new[] { -.3f, 0, .3f })
            {
                check(FixedFoveationCameraPolicy.TryCenter(offsetX, offsetY, out var fixedX, out var fixedY) &&
                    FoveationGazePolicy.TryProject(0, 0, 1, 1.3f, 1.8f, offsetX, offsetY, out var x, out var y) &&
                    Math.Abs(fixedX - x) < .00001f && Math.Abs(fixedY - y) < .00001f,
                    "Looking straight ahead is exactly the existing fixed-center behavior for each eye.");
            }

        check(FoveationGazePolicy.TryProject(.2f, .1f, 1, 1, 1, 0, 0, out var right, out var up) && right > .5f && up < .5f,
            "Looking right moves right and looking up moves up in the top-left rate texture.");
        check(FoveationGazePolicy.TryProject(-.2f, -.1f, 1, 1, 1, 0, 0, out var left, out var down) && left < .5f && down > .5f,
            "Looking left/down moves the protected region left/down.");
        foreach (var (dx, dy, x, y) in new[] { (3f, 3f, 1f, 0f), (-3f, 3f, 0f, 0f), (3f, -3f, 1f, 1f), (-3f, -3f, 0f, 1f) })
        {
            check(FoveationGazePolicy.TryProject(dx, dy, 1, 1, 1, .2f, -.1f, out var actualX, out var actualY),
                "Outside-frustum gaze protects the nearest visible edge.");
            Near(x, actualX, "Horizontal edge clamps without wrapping or centering.");
            Near(y, actualY, "Vertical edge clamps without wrapping or centering.");
        }

        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            for (var parameter = 0; parameter < 7; parameter++)
            {
                var values = new[] { .1f, .2f, 1f, 1.1f, 1.2f, .1f, -.1f };
                values[parameter] = invalid;
                check(!FoveationGazePolicy.TryProject(values[0], values[1], values[2], values[3], values[4], values[5], values[6], out var x, out var y),
                    "Non-finite gaze or projection data is rejected at every input boundary.");
                Near(.5f, x, "Rejected output remains deterministic.");
                Near(.5f, y, "Rejected output remains deterministic.");
            }
        foreach (var z in new[] { -1f, 0, .049f, .05f })
            check(!FoveationGazePolicy.TryProject(0, 0, z, 1, 1, 0, 0, out _, out _),
                "Backward and nearly sideways rays cannot destabilize projection.");
        foreach (var invalidScale in new[] { 0f, -1f })
        {
            check(!FoveationGazePolicy.TryProject(0, 0, 1, invalidScale, 1, 0, 0, out _, out _), "Invalid horizontal projection scale is rejected.");
            check(!FoveationGazePolicy.TryProject(0, 0, 1, 1, invalidScale, 0, 0, out _, out _), "Invalid vertical projection scale is rejected.");
        }
        check(!FoveationGazePolicy.TryProject(float.MaxValue, 0, .1f, 2, 1, 0, 0, out _, out _),
            "Overflowing finite inputs fail closed rather than emitting invalid texture coordinates.");
    }
}
