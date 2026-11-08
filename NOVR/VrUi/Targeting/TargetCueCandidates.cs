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
    // Inside the cone the configured class tier wins first (so an aircraft is
    // not lost to the missile it just launched); angle decides within a tier.
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

        var settings = ModConfiguration.Instance.LookCue;
        var priority = settings.CurrentLockPriority;
        var allowFriendly = settings.FriendlyUnits.Value;
        var origin = camera.transform.position;
        var forward = GazeCueRay.Direction(camera.transform);
        var bestAlignment = MinimumAlignment;
        var bestTier = int.MaxValue;
        HUDUnitMarker? best = null;
        foreach (var marker in Markers(hud))
        {
            if (marker == null || marker.unit == null || marker.selected || selected.Contains(marker.unit) ||
                marker.unit.disabled || marker.image == null || !marker.image.isActiveAndEnabled ||
                !(marker.image.canvasRenderer.GetInheritedAlpha() > .01f) || exclusions.CheckExclusions(marker.unit))
                continue;
            if (!TargetCuePriority.Eligible(allowFriendly, DynamicMap.GetFactionMode(marker.unit.NetworkHQ) == FactionMode.Friendly))
                continue;
            var direction = marker.image.transform.position - origin;
            var distanceSquared = direction.sqrMagnitude;
            var along = Vector3.Dot(direction, forward);
            if (!(distanceSquared > 0) || float.IsInfinity(distanceSquared) || !(along > 0) || float.IsInfinity(along))
                continue;
            var alignment = along / Mathf.Sqrt(distanceSquared);
            if (alignment <= MinimumAlignment)
                continue;
            var tier = TargetCuePriority.Tier(priority, marker.unit is Aircraft, marker.unit is Missile);
            // Exact angular ties keep the earlier marker, as before.
            if (tier > bestTier || (tier == bestTier && alignment <= bestAlignment))
                continue;
            bestTier = tier;
            bestAlignment = alignment;
            best = marker;
        }
        return best;
    }
}
