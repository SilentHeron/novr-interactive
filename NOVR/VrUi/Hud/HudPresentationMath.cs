using System;

namespace NOVR.VrUi.SpecialBehavior;

internal enum AircraftLabelAffiliation
{
    Other, Friendly, Hostile
}

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
    // Flight HUD Scale defaults to .38; symbols follow its ratio to that default
    // so existing symbol sliders keep their meaning at the default HUD size.
    internal const float DefaultFlightHudScale = .38f;
    internal static float SymbolFollowFactor(bool follow, bool focusedHud, float flightHudScale) =>
        !follow || !focusedHud || float.IsNaN(flightHudScale) || float.IsInfinity(flightHudScale) || flightHudScale <= 0
            ? 1f : flightHudScale / DefaultFlightHudScale;
    // The game's jamming shake for a map icon, turned from the game's world
    // units (pixels on a flat screen) into a step inside the VR map: the two
    // in-plane parts of the game's random offset times the world size of one
    // map unit. The part toward the viewer has no meaning on a flat screen and
    // is dropped. A step is never longer than MapJamMaxMetres, whatever scale
    // the map reports, so an icon cannot leave the map for the pilot's face.
    internal const float MapJamMaxMetres = .015f;
    internal static void MapJamOffset(float noiseX, float noiseY, float worldPerMapUnit, out float x, out float y)
    {
        x = y = 0f;
        if (float.IsNaN(noiseX) || float.IsInfinity(noiseX) || float.IsNaN(noiseY) || float.IsInfinity(noiseY) ||
            float.IsNaN(worldPerMapUnit) || float.IsInfinity(worldPerMapUnit))
            return;
        var unit = Math.Abs(worldPerMapUnit);
        x = noiseX * unit;
        y = noiseY * unit;
        var length = (float)Math.Sqrt((double)x * x + (double)y * y);
        if (length <= MapJamMaxMetres)
            return;
        x *= MapJamMaxMetres / length;
        y *= MapJamMaxMetres / length;
    }
    internal static float WidgetFit(float width, float height, float userScale) =>
        Math.Min(180 * userScale / .5f / Math.Max(1, width), 350 * userScale / .5f / Math.Max(1, height));
}
