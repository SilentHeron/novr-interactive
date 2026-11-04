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
        if (paint)
        {
            // CombatHUD selects before the designator's LateUpdate. Refresh it
            // now so native multi-selection uses this frame's adjusted cue ray.
            var camera = APIBus.CockpitHudCamera;
            if (camera != null && __instance.targetDesignator != null)
                GazeCueRay.AlignDesignator(__instance.targetDesignator.transform, camera.transform);
            return true;
        }
        var aircraft = __instance.aircraft;
        if (aircraft == null || aircraft.disabled || aircraft.weaponManager == null ||
            aircraft.cockpit == null || aircraft.cockpit.IsDetached())
            return false;
        var candidate = TargetCueCandidates.Nearest(__instance);
        if (candidate != null)
            __instance.SelectUnit(candidate.unit);
        // Empty gaze must not fall back to native threat/range prioritization.
        return false;
    }
}
