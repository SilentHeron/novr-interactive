using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class HUDBoresightStateViewPositionPatch
{
    private const float HudDistance = 1000.0f;
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, Image> ProjectedPosition =
        AccessTools.FieldRefAccess<global::HUDBoresightState, Image>("projectedPosition");
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, Image> TargetPosition =
        AccessTools.FieldRefAccess<global::HUDBoresightState, Image>("targetPosition");
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, Image> Boresight =
        AccessTools.FieldRefAccess<global::HUDBoresightState, Image>("boresight");
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, Image> Line =
        AccessTools.FieldRefAccess<global::HUDBoresightState, Image>("line");
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, Vector3> GunDirectionRelative =
        AccessTools.FieldRefAccess<global::HUDBoresightState, Vector3>("gunDirectionRelative");
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, Image> TargetDesignator =
        AccessTools.FieldRefAccess<global::HUDBoresightState, Image>("targetDesignator");
    private static readonly AccessTools.FieldRef<global::HUDBoresightState, ControlsFilter> Controls =
        AccessTools.FieldRefAccess<global::HUDBoresightState, ControlsFilter>("controlsFilter");

    [HarmonyPatch(typeof(global::HUDBoresightState), nameof(global::HUDBoresightState.UpdateWeaponDisplay))]
    private static class UpdateWeaponDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::HUDBoresightState __instance, Aircraft aircraft, List<Unit> targetList)
        {
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null || aircraft == null)
                return;

            var boresight = Boresight(__instance);
            var targetDesignator = TargetDesignator(__instance);
            if (boresight == null)
                return;

            //boresight.transform.forward = cockpitHudCamera.transform.forward;
            var gunDirectionRelative = GunDirectionRelative(__instance);
            var gunDirection = aircraft.transform.TransformDirection(gunDirectionRelative);
            var boresightWorldPosition = aircraft.transform.position + gunDirection * HudDistance;
            if (TryProjectToCockpitHud(boresightWorldPosition, out var boresightHudPosition))
                boresight.transform.position = boresightHudPosition;

            if (targetDesignator != null)
            {
                targetDesignator.transform.rotation = cockpitHudCamera.transform.rotation;
                targetDesignator.color = new Color(
                    0.0f,
                    1.0f,
                    0.0f,
                    Mathf.Clamp01(Vector3.Distance(targetDesignator.transform.position, boresight.transform.position) * 0.05f - 0.1f));
            }

            var velocityVector = SceneSingleton<FlightHud>.i.velocityVector;
            if (velocityVector != null)
            {
                velocityVector.color = new Color(
                    0.0f,
                    1.0f,
                    0.0f,
                    Mathf.Clamp01(FastMath.Distance(velocityVector.transform.position, boresight.transform.position) * 0.05f - 0.1f));
            }

            if (targetList.Count <= 0)
                return;

            var target = targetList[0];
            if (!aircraft.NetworkHQ.IsTargetPositionAccurate(target, 10.0f))
                return;

            var controlsFilter = Controls(__instance);
            if (controlsFilter == null)
                return;

            controlsFilter.GetAim(target, out GlobalPosition? aimPoint, out GlobalPosition? _);
            if (!aimPoint.HasValue)
                return;

            UpdateLeadDisplay(__instance, target, aimPoint.Value);
        }
    }

    private static void UpdateLeadDisplay(global::HUDBoresightState state, Unit target, GlobalPosition aimPoint)
    {
        var boresight = Boresight(state);
        var targetPosition = TargetPosition(state);
        var projectedPosition = ProjectedPosition(state);
        var line = Line(state);
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (boresight == null || targetPosition == null || projectedPosition == null || line == null || cockpitHudCamera == null)
            return;

        if (!TryProjectToCockpitHud(target.transform.position, out var targetHudPosition) ||
            !TryProjectToCockpitHud(aimPoint.ToLocalPosition(), out var aimHudPosition))
            return;

        targetPosition.transform.position = targetHudPosition;
        targetPosition.transform.rotation = cockpitHudCamera.transform.rotation;

        var projectedHudPosition = PlayerSettings.lagPip
            ? boresight.transform.position - aimHudPosition + targetHudPosition
            : aimHudPosition;
        projectedPosition.transform.position = projectedHudPosition;
        projectedPosition.transform.rotation = cockpitHudCamera.transform.rotation;

        var targetLocalPosition = PlayerSettings.lagPip ? Vector3.zero : targetPosition.transform.localPosition;
        var projectedLocalPosition = projectedPosition.transform.localPosition;
        var delta = targetLocalPosition - projectedLocalPosition;

        line.transform.localPosition = (targetLocalPosition + projectedLocalPosition) * 0.5f;
        line.transform.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        var magnitude = delta.magnitude;
        boresight.color = FastMath.InRange(projectedPosition.transform.position, boresight.transform.position, 15.0f)
            ? Color.green
            : Color.yellow;
        if (magnitude > 14.0f)
        {
            line.enabled = true;
            magnitude -= 14.0f;
        }
        else
        {
            line.enabled = false;
        }

        line.transform.localScale = new Vector3(magnitude, 1.0f, 1.0f);
    }

    private static bool TryProjectToCockpitHud(Vector3 worldPosition, out Vector3 hudPosition) =>
        VrHudProjection.TryProjectToCockpitHud(worldPosition, out hudPosition);
}
