using HarmonyLib;
using NOVR.MotionControls;
using UnityEngine.XR;

namespace NOVR.VrUi.HarmonyPatches;

[HarmonyPatch(typeof(CombatHUD), "TargetSelect")]
internal static class GazeTargetSelectionPatch
{
    [HarmonyPrefix]
    internal static bool Prefix(CombatHUD __instance, bool paint)
    {
        // Keep native paint/multi-selection, tactical-map and desktop behavior.
        if (!XRSettings.enabled || CameraStateManager.cameraMode != CameraMode.cockpit || DynamicMap.mapMaximized)
            return true;
        // CombatHUD selects before the designator's LateUpdate. Refresh it now
        // so native multi-selection, and the game's priority score for a tap,
        // use this frame's adjusted cue ray.
        var camera = APIBus.CockpitHudCamera;
        if (camera != null && __instance.targetDesignator != null)
            GazeCueRay.AlignDesignator(__instance.targetDesignator.transform, camera.transform);
        if (paint)
            return true;
        var aircraft = __instance.aircraft;
        if (aircraft == null || aircraft.disabled || aircraft.weaponManager == null ||
            aircraft.cockpit == null || aircraft.cockpit.IsDetached())
            return false;
        // The pick stays here rather than in the native method so the cue's
        // own rules keep applying: the friendly-units option, the live target
        // list, and an empty cue selecting nothing.
        var candidate = TargetCueCandidates.Pick(__instance);
        if (candidate != null)
            __instance.SelectUnit(candidate.unit);
        return false;
    }
}
