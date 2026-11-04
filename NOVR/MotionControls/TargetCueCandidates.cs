using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace NOVR.MotionControls;

internal static class TargetCueCandidates
{
    private static readonly AccessTools.FieldRef<CombatHUD, List<HUDUnitMarker>> Markers =
        AccessTools.FieldRefAccess<CombatHUD, List<HUDUnitMarker>>("markers");
    private static CombatHUD? _hud;
    private static Aircraft? _aircraft;
    private static HUDUnitMarker? _candidate;
    private static float _nextCheck, _radius;

    // Share the native TargetSelect radius and exclusions. Selecting a unit
    // is not a promise of seeker/weapon lock.
    internal static bool Nearby(CombatHUD? hud)
    {
        if (hud == null || hud.aircraft == null || hud.aircraft.disabled || hud.targetDesignator == null ||
            !hud.targetDesignator.gameObject.activeInHierarchy)
        { _hud = null; _aircraft = null; _candidate = null; _nextCheck = 0; return false; }
        const float radius = 100;
        var exclusions = SceneSingleton<TargetListSelector>.i;
        if (exclusions == null) { _candidate = null; _nextCheck = 0; return false; }
        if (_hud == hud && _aircraft == hud.aircraft && _radius == radius && Time.unscaledTime < _nextCheck)
        {
            // Recheck a positive result against the current gaze and filters.
            // Newly selected or moved-away units must not leave a stale cue.
            if (_candidate == null) return false;
            if (Eligible(_candidate, hud, exclusions, radius)) return true;
        }
        _hud = hud; _aircraft = hud.aircraft; _radius = radius;
        _nextCheck = Time.unscaledTime + .08f; _candidate = null;
        foreach (var marker in Markers(hud))
            if (Eligible(marker, hud, exclusions, radius))
            { _candidate = marker; return true; }
        return false;
    }

    private static bool Eligible(HUDUnitMarker marker, CombatHUD hud, TargetListSelector exclusions, float radius)
        => marker.unit != null && !marker.selected && !marker.unit.disabled && marker.image != null &&
            marker.image.isActiveAndEnabled && marker.image.canvasRenderer.GetInheritedAlpha() > .01f &&
            !exclusions.CheckExclusions(marker.unit) &&
            Vector3.SqrMagnitude(marker.image.transform.position - hud.targetDesignator.transform.position) < radius * radius;
}
