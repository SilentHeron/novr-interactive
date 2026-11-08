using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrUi.HarmonyPatches;

// Native hit markers are placed with WorldToScreenPoint pixels. On the VR
// flight canvas those pixel coordinates land far from the hit. Project the
// marker along the hit direction like the unit markers; native timing,
// pooling and the forward-hemisphere check stay in charge of visibility.
[HarmonyPatch]
internal static class HitMarkerViewPositionPatch
{
    [HarmonyTargetMethod]
    private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.Inner(typeof(CombatHUD), "HitMarker"), "Position");

    [HarmonyPostfix]
    private static void Postfix(GameObject ___marker, Unit ___hitUnit, Vector3 ___hitOffset)
    {
        if (!XRSettings.enabled || ___marker == null || ___hitUnit == null || !___marker.activeSelf)
            return;
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (cockpitHudCamera == null ||
            !VrHudProjection.TryProjectToCockpitHud(___hitUnit.transform.position + ___hitOffset, out var hudPosition))
        {
            ___marker.SetActive(false);
            return;
        }
        ___marker.transform.position = hudPosition;
        ___marker.transform.rotation = cockpitHudCamera.transform.rotation;
    }
}
