using System;
using BepInEx.Configuration;

namespace NOVR.MotionControls;

internal enum GazeCueShape
{
    Circle, Dot, Crosshair, Brackets
}

internal sealed class GazeCueSettings
{
    internal const float DefaultSize = 1f, DefaultOpacity = .65f, Diameter = 60f;
    internal const float MinSize = .5f, MaxSize = 2.5f, MinOpacity = .05f;
    internal const float DefaultVerticalDegrees = -1f, MinVerticalDegrees = -8f, MaxVerticalDegrees = 8f;
    internal const float ViewDepthMeters = 3f, CanvasScale = .001f;
    internal readonly ConfigEntry<float> Size, Opacity, VerticalDegrees;
    internal readonly ConfigEntry<GazeCueShape> Shape;
    internal readonly ConfigEntry<GazeLockPriority> LockPriority;
    internal readonly ConfigEntry<bool> FriendlyUnits;
    internal GazeCueSettings(ConfigFile config)
    {
        Size = config.Bind("VR HUD", "Look Cue Size", DefaultSize,
            new ConfigDescription("Size of the look-direction cue only. 100% keeps its original size; target markers and weapon sights are unchanged.",
                new AcceptableValueRange<float>(MinSize, MaxSize)));
        Opacity = config.Bind("VR HUD", "Look Cue Opacity", DefaultOpacity,
            new ConfigDescription("Opacity of the look-direction cue before HUD brightness and contextual fading. 65% keeps its original opacity.",
                new AcceptableValueRange<float>(MinOpacity, 1f)));
        Shape = config.Bind("VR HUD", "Look Cue Shape", GazeCueShape.Circle,
            "Look-direction symbol: hollow circle, small dot, open-centre crosshair, or corner brackets.");
        VerticalDegrees = config.Bind("VR HUD", "Look Cue Vertical Position", DefaultVerticalDegrees,
            new ConfigDescription("Cue and targeting position in degrees relative to the centre of vision. Negative moves down, positive moves up. Target selection follows the centre of the cue.",
                new AcceptableValueRange<float>(MinVerticalDegrees, MaxVerticalDegrees)));
        LockPriority = config.Bind("VR HUD", "Gaze Lock Priority", TargetCuePriority.Default,
            "What the look-direction cue and the gaze lock prefer when several units share the cue's cone. Nearest: closest to the cue. UnitsBeforeMissiles: aircraft, ground units and ships before missiles. AircraftFirst: aircraft, then other units, missiles last. The nearest angle decides within a group; a lone missile in the cone still locks.");
        FriendlyUnits = config.Bind("VR HUD", "Look Cue Friendly Units", false,
            "Show the look-direction cue on friendly units and let the gaze lock select them. Off keeps the cue and gaze lock to non-friendly units. The game's own target filters still apply.");
    }
    internal float Scale => SafeRange(Size.Value, MinSize, MaxSize, DefaultSize);
    internal float Alpha => SafeRange(Opacity.Value, MinOpacity, 1, DefaultOpacity);
    internal float VerticalAngle => SafeRange(VerticalDegrees.Value, MinVerticalDegrees, MaxVerticalDegrees, DefaultVerticalDegrees);
    internal float CanvasOffsetY => (float)Math.Tan(VerticalAngle * Math.PI / 180) * ViewDepthMeters / CanvasScale;
    internal string PositionLabel => VerticalAngle == 0 ? "Centre of vision" :
        Math.Abs(VerticalAngle).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "° " + (VerticalAngle < 0 ? "below centre" : "above centre");
    internal GazeCueShape CurrentShape => (uint)Shape.Value <= (uint)GazeCueShape.Brackets ? Shape.Value : GazeCueShape.Circle;
    internal GazeLockPriority CurrentLockPriority => TargetCuePriority.Normalize(LockPriority.Value);
    private static float SafeRange(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    internal void Reset()
    {
        Size.Value = DefaultSize;
        Opacity.Value = DefaultOpacity;
        Shape.Value = GazeCueShape.Circle;
        VerticalDegrees.Value = DefaultVerticalDegrees;
        LockPriority.Value = TargetCuePriority.Default;
        FriendlyUnits.Value = false;
    }
}
