using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrUi.HarmonyPatches;

// Native map markers (selected-target info and mission objectives) write a
// world rotation of zero every update, which only matches a flat screen
// canvas. On the cockpit console map that stands the marker upright out of the
// panel, where its large red/blue sprite reads as an artifact in front of the
// pilot. Keep them in the map frame the same way UnitMapIconPlanePatch keeps
// unit icons there.
internal static class MapMarkerPlanePatch
{
    private static void AlignToMap(Transform? marker)
    {
        if (!XRSettings.enabled || marker == null)
            return;
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null || map.mapBackground == null)
            return;
        marker.rotation = map.mapBackground.transform.rotation;
    }

    [HarmonyPatch(typeof(TargetMarker), "Update")]
    private static class TargetMarkerPlane
    {
        [HarmonyPostfix]
        private static void Postfix(TargetMarker __instance) => AlignToMap(__instance.transform);
    }

    [HarmonyPatch(typeof(ObjectiveMarker), nameof(ObjectiveMarker.UpdateMarker))]
    private static class ObjectiveMarkerPlane
    {
        [HarmonyPostfix]
        private static void Postfix(ObjectiveMarker __instance) => AlignToMap(__instance.transform);
    }

    // Target info belongs to the full map. Native code shows a new marker at
    // once and hides it again on its next one-second refresh while the map is
    // minimized, so every selection flashes a large marker on the cockpit map.
    // Hide it immediately there; the full map keeps the native behaviour.
    [HarmonyPatch(typeof(TargetMarker), nameof(TargetMarker.Marker_Refresh))]
    private static class HideTargetInfoOnCockpitMap
    {
        [HarmonyPostfix]
        private static void Postfix(TargetMarker __instance)
        {
            if (XRSettings.enabled && !DynamicMap.mapMaximized)
                __instance.Show(false);
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "Minimize")]
    private static class HideTargetInfoOnMinimize
    {
        [HarmonyPostfix]
        private static void Postfix(DynamicMap __instance)
        {
            if (!XRSettings.enabled)
                return;
            var markers = __instance.mapMarkers;
            if (markers == null)
                return;
            foreach (var marker in markers)
                if (marker is TargetMarker target && target != null)
                    target.Show(false);
        }
    }
}
