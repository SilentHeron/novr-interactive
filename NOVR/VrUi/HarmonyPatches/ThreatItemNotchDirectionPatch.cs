using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class ThreatItemNotchDirectionPatch
{
    private const float NotchIndicatorDistance = 1000.0f;
    private static readonly AccessTools.FieldRef<global::ThreatItem, global::Missile> ThreatMissile =
        AccessTools.FieldRefAccess<global::ThreatItem, global::Missile>("missile");
    private static readonly AccessTools.FieldRef<global::ThreatItem, GameObject> NotchLine =
        AccessTools.FieldRefAccess<global::ThreatItem, GameObject>("notchLine");
    private static readonly AccessTools.FieldRef<global::ThreatItem, GameObject> NotchIndicator =
        AccessTools.FieldRefAccess<global::ThreatItem, GameObject>("notchIndicator");
    private static readonly AccessTools.FieldRef<global::ThreatItem, Image> NotchIndicatorBox =
        AccessTools.FieldRefAccess<global::ThreatItem, Image>("notchIndicatorBox");
    private static readonly AccessTools.FieldRef<global::ThreatItem, Text> NotchIndicatorLabel =
        AccessTools.FieldRefAccess<global::ThreatItem, Text>("notchIndicatorLabel");
    private static readonly AccessTools.FieldRef<global::ThreatItem, Transform> PlayerAircraftIconTransform =
        AccessTools.FieldRefAccess<global::ThreatItem, Transform>("playerAircraftIconTransform");
    private static readonly HashSet<GameObject> CompactHiddenIndicators = new();

    [HarmonyPatch(typeof(global::CombatHUD), nameof(global::CombatHUD.ShowNotchIndicator))]
    private static class PrepareNotchIndicator
    {
        [HarmonyPostfix]
        private static void Postfix(GameObject __result)
        {
            if (__result == null)
                return;
            // These are instantiated after the flight canvas is converted to VR.
            // Parenting alone retains the prefab's original HUD layer, which the
            // VR HUD camera does not render.
            LayerHelper.SetLayerRecursive(__result.transform, LayerHelper.GetVrUiLayer());
            // Keep the game's hollow, sliced box and its authored label below
            // it. Treat this compact widget as one symbol: scaling both drawn
            // meshes around the shared origin preserves its proportions, label
            // spacing and evasion bearing, without altering the RectTransforms.
            HudElementStyle.Add(__result.GetComponent<Image>(), HudElementKind.Marker);
            HudElementStyle.Add(__result.GetComponentInChildren<Text>(), HudElementKind.Marker, __result.transform);
        }
    }

    [HarmonyPatch(typeof(global::ThreatItem), "AlignNotchLine")]
    private static class AlignNotchLinePatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ThreatItem __instance)
        {
            var notchLine = NotchLine(__instance);
            var playerAircraftIconTransform = PlayerAircraftIconTransform(__instance);
            if (notchLine == null || playerAircraftIconTransform == null || !TryGetNotchDirection(__instance, out var notchDirection))
                return false;

            var flattenedNotchDirection = new Vector3(notchDirection.x, 0.0f, notchDirection.z);
            if (flattenedNotchDirection.sqrMagnitude <= Mathf.Epsilon)
                return false;

            var notchYawDegrees = Quaternion.LookRotation(flattenedNotchDirection, Vector3.up).eulerAngles.y;
            notchLine.transform.position = playerAircraftIconTransform.position;
            notchLine.transform.eulerAngles = new Vector3(
                0.0f,
                0.0f,
                SceneSingleton<DynamicMap>.i.mapImage.transform.eulerAngles.z - notchYawDegrees);

            return false;
        }
    }

    [HarmonyPatch(typeof(global::ThreatItem), "AlignNotchIndicator")]
    private static class AlignNotchIndicatorPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(global::ThreatItem __instance)
        {
            var notchIndicator = NotchIndicator(__instance);
            if (notchIndicator == null)
                return false;
            var compact = SpecialBehavior.CockpitAlerts.Active && !ModConfiguration.Instance.CockpitAlerts.WorldNotch.Value;
            if (compact)
            {
                CompactHiddenIndicators.Add(notchIndicator);
                notchIndicator.SetActive(false);
            }
            else if (CompactHiddenIndicators.Remove(notchIndicator))
                notchIndicator.SetActive(true);
            if (compact || !TryGetNotchDirection(__instance, out var notchDirection))
                return false;

            var missile = ThreatMissile(__instance);
            var aircraft = SceneSingleton<CombatHUD>.i.aircraft;
            var worldPosition = aircraft.GlobalPosition().ToLocalPosition() + notchDirection * NotchIndicatorDistance;
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null ||
                !VrHudProjection.TryProjectDirectionToCockpitHud(worldPosition, out var hudWorldPosition))
                return false;
            var mainCameraLocalNotchDirection = mainCamera.transform.InverseTransformDirection(notchDirection);
            var hudNotchDirection = cockpitHudCamera.transform.TransformDirection(mainCameraLocalNotchDirection);

            // Keep the evasion bearing relative to the HUD camera, including
            // head translation / recentering, rather than the UI world's origin.
            notchIndicator.transform.position = hudWorldPosition;
            var indicatorUp = Vector3.ProjectOnPlane(hudNotchDirection, cockpitHudCamera.transform.forward);
            if (indicatorUp.sqrMagnitude <= Mathf.Epsilon)
                indicatorUp = cockpitHudCamera.transform.up;

            indicatorUp = Vector3.Cross(-cockpitHudCamera.transform.forward, indicatorUp).normalized;
            if (Vector3.Dot(indicatorUp, -cockpitHudCamera.transform.up) > 0.0f)
                indicatorUp *= -1.0f;

            notchIndicator.transform.rotation = Quaternion.LookRotation(
                cockpitHudCamera.transform.forward,
                indicatorUp);

            var distance = FastMath.Distance(aircraft.transform.position, missile.transform.position);
            var color = Color.green;
            if (missile.seekerMode == Missile.SeekerMode.activeLock)
                color = Color.Lerp(Color.yellow, Color.red, Mathf.Sin(Time.timeSinceLevelLoad * 20f) + 0.5f);
            else if (missile.seekerMode == Missile.SeekerMode.activeSearch)
                color = Color.Lerp(Color.green, Color.yellow, Mathf.Sin(Time.timeSinceLevelLoad * 10f) + 0.5f);

            var notchIndicatorBox = NotchIndicatorBox(__instance);
            if (notchIndicatorBox != null)
            {
                // Do not replace the native rectangle with full-height lines:
                // those alias badly in the headset and lose the notch-box cue.
                notchIndicatorBox.color = color;
                notchIndicatorBox.raycastTarget = false;
            }

            var notchIndicatorLabel = NotchIndicatorLabel(__instance);
            if (notchIndicatorLabel != null)
            {
                notchIndicatorLabel.text = $"[{missile.GetSeekerType()}] {UnitConverter.DistanceReading(distance)}";
                notchIndicatorLabel.color = color;
                notchIndicatorLabel.raycastTarget = false;
            }

            return false;
        }
    }

    internal static bool TryGetNotchDirection(global::ThreatItem threatItem, out Vector3 notchDirection)
    {
        notchDirection = Vector3.zero;

        var missile = ThreatMissile(threatItem);
        var aircraft = SceneSingleton<CombatHUD>.i?.aircraft;
        var mainCamera = APIBus.MainCamera;
        if (missile == null || aircraft == null || mainCamera == null)
            return false;

        var evasionPointToTarget = aircraft.GlobalPosition() - missile.GetEvasionPoint();
        if (evasionPointToTarget.sqrMagnitude <= Mathf.Epsilon)
            return false;

        notchDirection = Vector3.ProjectOnPlane(mainCamera.transform.forward, evasionPointToTarget.normalized);
        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            notchDirection = Vector3.ProjectOnPlane(aircraft.transform.forward, evasionPointToTarget.normalized);
        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            notchDirection = Vector3.Cross(evasionPointToTarget.normalized, Vector3.up);
        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            notchDirection = Vector3.Cross(evasionPointToTarget.normalized, Vector3.right);

        if (notchDirection.sqrMagnitude <= Mathf.Epsilon)
            return false;

        notchDirection.Normalize();
        return true;
    }

    internal static void RestoreCompactIndicators()
    {
        foreach (var indicator in CompactHiddenIndicators)
            if (indicator != null)
                indicator.SetActive(true);
        CompactHiddenIndicators.Clear();
    }

    [HarmonyPatch(typeof(global::ThreatItem), "OnDestroy")]
    private static class ForgetDestroyedThreat
    {
        [HarmonyPrefix]
        private static void Prefix(global::ThreatItem __instance)
        {
            var indicator = NotchIndicator(__instance);
            if (indicator != null)
                CompactHiddenIndicators.Remove(indicator);
        }
    }
}
