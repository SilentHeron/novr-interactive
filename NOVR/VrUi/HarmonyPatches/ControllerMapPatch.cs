using HarmonyLib;
using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi.HarmonyPatches;

internal static class ControllerMapPatch
{
    [HarmonyPatch(typeof(DynamicMap), "MapControls")]
    private static class MapControls
    {
        [HarmonyPrefix]
        private static void Prefix(DynamicMap __instance, ref Vector2 ___positionOffset, System.Action? ___onMapChanged)
        {
            var cursor = VrUiCursor.I;
            if (cursor?.ControllerOverMap != true || WristMenu.IsOpen || !TrackedControllers.Available) return;
            var left = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
            var pointerHand = TrackedControllers.Read(left);
            var otherHand = TrackedControllers.Read(!left);
            if (pointerHand.Valid)
            {
                var zoom = PointerGeometry.Zoom(__instance.GetZoomLevel(), pointerHand.Axis.y, Time.unscaledDeltaTime);
                if (!Mathf.Approximately(zoom, __instance.GetZoomLevel())) __instance.SetZoomLevel(zoom);
            }
            if (otherHand.Valid)
            {
                var pan = new Vector2(FlightControlMath.Axis(otherHand.Axis.x, 0.2f, 1), FlightControlMath.Axis(otherHand.Axis.y, 0.2f, 1));
                ___positionOffset += pan * (300f * Mathf.Min(Time.unscaledDeltaTime, 0.05f) / Mathf.Max(1, __instance.GetZoomLevel()));
                if (pan.sqrMagnitude > 0) ___onMapChanged?.Invoke();
            }
        }
    }

    [HarmonyPatch(typeof(DynamicMap), "SetZoomLevel")]
    private static class Zoom
    {
        [HarmonyPrefix]
        private static bool Prefix(DynamicMap __instance, float __0, ref Vector2 ___positionOffset, Vector2 ___stationaryOffset, System.Action? ___onMapChanged)
        {
            // A console zoom is centered on the aircraft by CenterMinimizedMap;
            // desktop mouse coordinates must never become a world-space pivot.
            if (!DynamicMap.mapMaximized && UniversalCockpitPanel.IsDocked(__instance.transform))
            {
                var previous = __instance.GetZoomLevel();
                if (previous <= 0 || !FlightControlMath.IsFinite(__0)) return false;
                var zoom = Mathf.Clamp(__0, 1, 12);
                __instance.mapImage.transform.localScale *= zoom / previous;
                __instance.mapScaleCenter.localScale = Vector3.one * zoom;
                ___onMapChanged?.Invoke();
                return false;
            }
            var cursor = VrUiCursor.I;
            if (cursor?.ControllerOverMap != true) return true;
            var oldScale = __instance.GetZoomLevel();
            if (oldScale <= 0 || !FlightControlMath.IsFinite(__0)) return false;
            var image = __instance.mapImage.transform;
            var anchor = cursor.CursorPosition;
            var localAnchor = image.InverseTransformPoint(anchor);
            var newScale = Mathf.Clamp(__0, 1, 40);
            image.localScale *= newScale / oldScale;
            image.position += anchor - image.TransformPoint(localAnchor);
            __instance.mapScaleCenter.localScale = Vector3.one * newScale;
            // Preserve the point under the laser when native MapControls lays out the map.
            ___positionOffset = -___stationaryOffset - (Vector2)image.localPosition / image.localScale.x;
            ___onMapChanged?.Invoke();
            return false;
        }
    }
}
