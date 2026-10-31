using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace NOVR.VrUi.HarmonyPatches;

internal static class ThreatItemVectorLineRotationSafePatch
{
    private static readonly FieldInfo MissileIconTransformField = AccessTools.Field(typeof(global::ThreatItem), "missileIconTransform");
    private static readonly FieldInfo PlayerAircraftIconTransformField = AccessTools.Field(typeof(global::ThreatItem), "playerAircraftIconTransform");
    private static readonly FieldInfo VectorLineField = AccessTools.Field(typeof(global::ThreatItem), "vectorLine");

    [HarmonyPatch(typeof(global::ThreatItem), "AlignVectorLine")]
    private static class AlignVectorLinePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ThreatItem __instance)
        {
            var missileIconTransform = MissileIconTransformField.GetValue(__instance) as Transform;
            var playerAircraftIconTransform = PlayerAircraftIconTransformField.GetValue(__instance) as Transform;
            var vectorLine = VectorLineField.GetValue(__instance) as GameObject;
            if (vectorLine == null)
                return false;

            var map = SceneSingleton<DynamicMap>.i;
            if (missileIconTransform == null || playerAircraftIconTransform == null || map == null || map.iconLayer == null)
            {
                vectorLine.SetActive(false);
                return false;
            }

            var iconLayer = map.iconLayer.transform;
            var playerLocalPosition = iconLayer.InverseTransformPoint(playerAircraftIconTransform.position);
            var missileLocalPosition = iconLayer.InverseTransformPoint(missileIconTransform.position);
            var localDelta = missileLocalPosition - playerLocalPosition;
            localDelta.z = 0;
            // Both endpoints already use icon-layer coordinates. Dividing their
            // distance by world scale again makes a small VR map's red line huge.
            // Keep the native 2-pixel width through map zoom, using map UI units
            // rather than metres (the entire widget may be scaled and rotated).
            var pixel = iconLayer.InverseTransformVector(map.transform.TransformVector(Vector3.right)).magnitude;
            var length = localDelta.magnitude;
            if (!Finite(playerLocalPosition.x) || !Finite(playerLocalPosition.y) || !Finite(playerLocalPosition.z) ||
                !Finite(length) || !Finite(pixel) || pixel <= 0)
            {
                vectorLine.SetActive(false);
                return false;
            }

            vectorLine.SetActive(true);
            vectorLine.transform.localPosition = playerLocalPosition;
            vectorLine.transform.localEulerAngles = new Vector3(
                0.0f,
                0.0f,
                -Mathf.Atan2(localDelta.x, localDelta.y) * Mathf.Rad2Deg);
            vectorLine.transform.localScale = new Vector3(pixel, length + pixel, 1);
            return false;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
