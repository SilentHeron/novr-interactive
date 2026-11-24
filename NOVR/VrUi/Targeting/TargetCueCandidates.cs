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

    // Whether anything can be selected does not depend on how it is ranked,
    // so the per-frame cue never runs the game's scoring.
    internal static bool Nearby(CombatHUD? hud) => Pick(hud, false) != null;

    // The marker a target-select tap takes.
    internal static HUDUnitMarker? Pick(CombatHUD? hud) => Pick(hud, true);

    // Both the cue and single-target selection read the current adjusted cue ray.
    // Marker positions already contain the game's known-position rules and the
    // VR projection; reading the underlying unit would bypass those rules.
    // With the Game priority the game's own score ranks the markers inside the
    // cone: threat and opportunity for the selected weapon, over range and the
    // squared distance from the designator. The caller aligns the designator
    // with the cue first. With the other priorities the configured class tier
    // wins first (so an aircraft is not lost to the missile it just launched)
    // and the angle decides within a tier.
    private static HUDUnitMarker? Pick(CombatHUD? hud, bool rank)
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
        var game = rank && priority == GazeLockPriority.Game;
        var station = game && hud.aircraft.weaponManager != null ? hud.aircraft.weaponManager.currentWeaponStation : null;
        var designator = hud.targetDesignator.gameObject;
        var bestScore = float.NegativeInfinity;
        HUDUnitMarker? scored = null;
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
            if (game)
            {
                // Highest score wins and an exact tie keeps the earlier marker.
                var score = marker.AssessPriority(hud.aircraft, designator, station);
                if (score > bestScore)
                {
                    bestScore = score;
                    scored = marker;
                }
            }
            var tier = TargetCuePriority.Tier(priority, marker.unit is Aircraft, marker.unit is Missile);
            // Exact angular ties keep the earlier marker, as before.
            if (tier > bestTier || (tier == bestTier && alignment <= bestAlignment))
                continue;
            bestTier = tier;
            bestAlignment = alignment;
            best = marker;
        }
        // A score that is not a number never wins; the nearest angle then
        // still gives the tap a target.
        return scored ?? best;
    }
}
