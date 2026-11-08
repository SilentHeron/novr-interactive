using System;
using UnityEngine;

namespace NOVR.VrUi;

internal static class WristMenuSettingsActions
{
    // Preserve the combined HUD adjustment: individual symbol sliders are
    // still available, while this control moves all existing sizes together.
    internal static void FlightHudSize(ModConfiguration config, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return;
        var amount = value - config.FlightHudScale.Value;
        config.FocusedFlightHud.Value = true;
        config.FlightHudScale.Value = value;
        // When symbols already follow Flight HUD Scale multiplicatively, the
        // additive nudge would apply the change twice.
        if (config.SymbolsFollowFlightHud.Value)
            return;
        config.MarkerScale.Value += amount;
        config.MarkerLabelScale.Value += amount;
        config.ReticleScale.Value += amount;
        config.WeaponHudScale.Value += amount;
    }
    internal static int PalmPreset(Vector3 angles, bool left)
    {
        if (!Near(angles.x, 0) || !Near(angles.y, 0))
            return -1;
        if (Near(angles.z, left ? 90 : -90))
            return 0;
        return Near(angles.z, 0) ? 1 : -1;
    }
    private static bool Near(float angle, float target) => Math.Abs(Math.IEEERemainder(angle - target, 360)) < .5;
}
