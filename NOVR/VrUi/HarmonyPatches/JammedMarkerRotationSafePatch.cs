using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class JammedMarkerRotationSafePatch
{
    private static readonly AccessTools.FieldRef<global::JammedMarker, GameObject> VectorLine =
        AccessTools.FieldRefAccess<global::JammedMarker, GameObject>("vectorLine");
    private static readonly AccessTools.FieldRef<global::JammedMarker, Image> VectorLineImage =
        AccessTools.FieldRefAccess<global::JammedMarker, Image>("vectorLineImage");
    private static readonly AccessTools.FieldRef<global::JammedMarker, Radar> MarkerRadar =
        AccessTools.FieldRefAccess<global::JammedMarker, Radar>("radar");
    private static readonly AccessTools.FieldRef<global::JammedMarker, Unit> MarkerUnit =
        AccessTools.FieldRefAccess<global::JammedMarker, Unit>("unit");
    private static readonly AccessTools.FieldRef<global::JammedMarker, Unit> JammedBy =
        AccessTools.FieldRefAccess<global::JammedMarker, Unit>("jammedBy");
    private static readonly AccessTools.FieldRef<global::JammedMarker, UnitMapIcon> MarkerIcon =
        AccessTools.FieldRefAccess<global::JammedMarker, UnitMapIcon>("mapIcon");
    private static readonly AccessTools.FieldRef<global::JammedMarker, UnitMapIcon> JammedByIcon =
        AccessTools.FieldRefAccess<global::JammedMarker, UnitMapIcon>("jammedByIcon");

    [HarmonyPatch(typeof(global::JammedMarker), "Update")]
    private static class UpdatePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::JammedMarker __instance)
        {
            var unit = MarkerUnit(__instance);
            var mapIcon = MarkerIcon(__instance);
            var radar = MarkerRadar(__instance);
            var jammedBy = JammedBy(__instance);
            if (unit == null || mapIcon == null || unit.disabled || radar == null || jammedBy == null || jammedBy.disabled || !radar.IsJammed())
                return true;

            var map = SceneSingleton<DynamicMap>.i;
            var iconLayer = map.iconLayer.transform;
            var jammedLocalPosition = iconLayer.InverseTransformPoint(mapIcon.transform.position);

            __instance.transform.localPosition = jammedLocalPosition;
            __instance.transform.localScale = Vector3.one / map.mapImage.transform.localScale.x;

            var vectorLineImage = VectorLineImage(__instance);
            var vectorLine = VectorLine(__instance);
            var jammedByIcon = JammedByIcon(__instance);
            if (jammedByIcon == null || vectorLineImage == null || vectorLine == null)
            {
                if (vectorLineImage != null)
                    vectorLineImage.enabled = false;
                return false;
            }

            var jammerLocalPosition = iconLayer.InverseTransformPoint(jammedByIcon.transform.position);
            var localDelta = jammerLocalPosition - jammedLocalPosition;
            vectorLineImage.enabled = true;
            vectorLine.transform.localPosition = jammedLocalPosition;
            vectorLine.transform.localEulerAngles = new Vector3(
                0.0f,
                0.0f,
                -Mathf.Atan2(localDelta.x, localDelta.y) * Mathf.Rad2Deg);
            vectorLine.transform.localScale = new Vector3(1.0f, localDelta.magnitude, 1.0f);
            return false;
        }
    }
}
