using System;

namespace NOVR.VrUi.SpecialBehavior;

internal enum AircraftLabelAffiliation { Other, Friendly, Hostile }

internal static class HudPresentationMath
{
    // Apply to native aircraft name/type/range text only. Unknown/non-aircraft
    // targets retain the normal label scale, and flat-screen text is untouched.
    internal static float AircraftLabelScale(float userScale, AircraftLabelAffiliation affiliation, bool vr) =>
        !vr ? 1 : userScale * (affiliation switch
        {
            AircraftLabelAffiliation.Friendly => .65f,
            AircraftLabelAffiliation.Hostile => .8f,
            _ => 1f
        });
    internal static float ScaleCoordinate(float coordinate, float anchor, float scale) => anchor + (coordinate - anchor) * scale;
    internal static float WidgetFit(float width, float height, float userScale) =>
        Math.Min(180 * userScale / .5f / Math.Max(1, width), 350 * userScale / .5f / Math.Max(1, height));
}
