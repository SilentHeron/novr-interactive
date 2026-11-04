using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NOVR.MotionControls;

internal static class TargetCueCandidates
{
    // Native 100-unit chord radius on the VR HUD's 1000-unit projection sphere:
    // dot > 1 - (100 / 1000)^2 / 2, or a roughly 5.73-degree half-cone.
    private const float MinimumAlignment = .995f;
    private static readonly AccessTools.FieldRef<CombatHUD, List<HUDUnitMarker>> Markers =
        AccessTools.FieldRefAccess<CombatHUD, List<HUDUnitMarker>>("markers");

    internal static bool Nearby(CombatHUD? hud) => Nearest(hud) != null;

    // Both the cue and single-target selection read the current adjusted cue ray.
    // Native threat/range priority must not override an explicit gaze choice.
    // Marker positions already contain the game's known-position rules and the
    // VR projection; reading the underlying unit would bypass those rules.
    internal static HUDUnitMarker? Nearest(CombatHUD? hud)
    {
        var camera = APIBus.CockpitHudCamera;
        var exclusions = SceneSingleton<TargetListSelector>.i;
        if (camera == null || exclusions == null || hud == null || hud.aircraft == null ||
            hud.aircraft.disabled || hud.targetDesignator == null || !hud.targetDesignator.gameObject.activeInHierarchy)
            return null;
        var selected = hud.aircraft.weaponManager != null
            ? hud.aircraft.weaponManager.GetTargetList() : hud.GetTargetList();
        if (selected == null)
            return null;

        var origin = camera.transform.position;
        var forward = GazeCueRay.Direction(camera.transform);
        var bestAlignment = MinimumAlignment;
        HUDUnitMarker? best = null;
        foreach (var marker in Markers(hud))
        {
            if (marker == null || marker.unit == null || marker.selected || selected.Contains(marker.unit) ||
                marker.unit.disabled || marker.image == null || !marker.image.isActiveAndEnabled ||
                !(marker.image.canvasRenderer.GetInheritedAlpha() > .01f) || exclusions.CheckExclusions(marker.unit))
                continue;
            var direction = marker.image.transform.position - origin;
            var distanceSquared = direction.sqrMagnitude;
            var along = Vector3.Dot(direction, forward);
            if (!(distanceSquared > 0) || float.IsInfinity(distanceSquared) || !(along > 0) || float.IsInfinity(along))
                continue;
            var alignment = along / Mathf.Sqrt(distanceSquared);
            if (alignment <= bestAlignment)
                continue;
            bestAlignment = alignment;
            best = marker;
        }
        return best;
    }
}
