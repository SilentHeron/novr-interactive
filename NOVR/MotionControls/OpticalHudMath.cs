using System;

namespace NOVR.MotionControls;

internal enum OpticalHudMode { Hidden, Unavailable, Landing, Head, Track }

internal static class OpticalHudMath
{
    internal static OpticalHudMode Mode(bool enabled, bool cockpitReady, bool sensorReady, bool landing, bool tracking, int targets)
        => !enabled || !cockpitReady ? OpticalHudMode.Hidden : !sensorReady ? OpticalHudMode.Unavailable :
            landing ? OpticalHudMode.Landing : tracking && targets > 0 ? OpticalHudMode.Track : OpticalHudMode.Head;

    internal static float Bounded(float value, float minimum, float maximum, float fallback)
        => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(minimum, Math.Min(maximum, value));

    // The helmet image stays at a comfortable fixed stereo depth. Zoom only
    // changes the mono sensor projection, never the headset projection.
    internal static float Width(float degrees, float depth)
        => 2 * depth * (float)Math.Tan(Bounded(degrees, 12, 26, 20) * Math.PI / 360);

    internal static float SensorFov(float widthDegrees, float aspect, float magnification)
        => (float)(2 * Math.Atan(Math.Tan(Bounded(widthDegrees, 12, 26, 20) * Math.PI / 360) /
            Bounded(aspect, .5f, 3, 1) / Bounded(magnification, 1, 6, 2)) * 180 / Math.PI);

    // Match acquisition to the magnified image instead of selecting a unit
    // outside the small optical window with the game's full-size 100m circle.
    internal static float SelectionRadius(float verticalFov)
        => 1000 * (float)Math.Tan(Math.Min(2, verticalFov * .18f) * Math.PI / 180);
}
