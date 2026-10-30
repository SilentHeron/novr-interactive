using HarmonyLib;
using NuclearOption.Networking;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrUi.HarmonyPatches;

// Native icons write world XY rotations. Keep their headings in the map plane,
// including when the minimized map is mounted on the tilted 3D console.
[HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.UpdateIcon))]
internal static class UnitMapIconPlanePatch
{
    [HarmonyPostfix]
    private static void Postfix(UnitMapIcon __instance, Transform mapTransform, bool ___orient, TrackingInfo ___trackingInfo)
    {
        if (!XRSettings.enabled || __instance.iconImage == null || mapTransform == null) return;
        var icon = __instance.iconImage.transform;
        if (icon != __instance.transform) __instance.transform.localRotation = Quaternion.identity;
        if (___orient)
        {
            // Preserve the last known heading for contacts that are not observed.
            if (__instance.unit != null && (___trackingInfo == null || ___trackingInfo.Observed()))
                icon.rotation = mapTransform.rotation * Quaternion.Euler(0, 0, -__instance.unit.transform.eulerAngles.y);
        }
        else
        {
            var map = SceneSingleton<DynamicMap>.i;
            if (map?.mapBackground != null) icon.rotation = map.mapBackground.transform.rotation;
        }
    }
}

[HarmonyPatch(typeof(AirbaseMapIcon), nameof(AirbaseMapIcon.UpdateIcon))]
internal static class AirbaseMapIconPlanePatch
{
    [HarmonyPostfix]
    private static void Postfix(AirbaseMapIcon __instance)
    {
        if (!XRSettings.enabled || __instance.iconImage == null) return;
        var map = SceneSingleton<DynamicMap>.i;
        if (map?.mapBackground == null) return;
        if (__instance.iconImage.transform != __instance.transform) __instance.transform.localRotation = Quaternion.identity;
        __instance.iconImage.transform.rotation = map.mapBackground.transform.rotation;
    }
}
