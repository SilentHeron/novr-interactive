using System;
using BepInEx.Configuration;

namespace NOVR.MotionControls;

internal enum GazeCueShape { Circle, Dot, Crosshair, Brackets }

internal sealed class GazeCueSettings
{
    internal const float DefaultSize = 1f, DefaultOpacity = .65f, Diameter = 60f;
    internal const float MinSize = .5f, MaxSize = 2.5f, MinOpacity = .05f;
    internal readonly ConfigEntry<float> Size, Opacity;
    internal readonly ConfigEntry<GazeCueShape> Shape;
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
    }
    internal float Scale => SafeRange(Size.Value, MinSize, MaxSize, DefaultSize);
    internal float Alpha => SafeRange(Opacity.Value, MinOpacity, 1, DefaultOpacity);
    internal GazeCueShape CurrentShape => (uint)Shape.Value <= (uint)GazeCueShape.Brackets ? Shape.Value : GazeCueShape.Circle;
    private static float SafeRange(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    internal void Reset() { Size.Value = DefaultSize; Opacity.Value = DefaultOpacity; Shape.Value = GazeCueShape.Circle; }
}
