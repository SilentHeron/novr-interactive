using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class HUDBombingStateViewPositionPatch
{
    // Read on every frame the bombing display is up, so through field
    // references rather than reflection (which also boxed the two structs).
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Image> AlignmentBar =
        AccessTools.FieldRefAccess<global::HUDBombingState, Image>("alignmentBar");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Image> CcipPipper =
        AccessTools.FieldRefAccess<global::HUDBombingState, Image>("ccipPipper");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Image> CcipLine =
        AccessTools.FieldRefAccess<global::HUDBombingState, Image>("ccipLine");
    // dropCountdown/ccrpFallTime/ccipFallTime became TextMeshProUGUI in NO 0.34 - only the
    // transform is needed here, so treat them as Component to survive the type change.
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Component> CcipFallTime =
        AccessTools.FieldRefAccess<global::HUDBombingState, Component>("ccipFallTime");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Component> CcrpFallTime =
        AccessTools.FieldRefAccess<global::HUDBombingState, Component>("ccrpFallTime");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Component> DropCountdown =
        AccessTools.FieldRefAccess<global::HUDBombingState, Component>("dropCountdown");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Image> CcrpCircle =
        AccessTools.FieldRefAccess<global::HUDBombingState, Image>("ccrpCircle");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, GlobalPosition> AverageTargetPosition =
        AccessTools.FieldRefAccess<global::HUDBombingState, GlobalPosition>("averageTargetPosition");
    private static readonly AccessTools.FieldRef<global::HUDBombingState, Vector3> CcipImpactPointSmoothed =
        AccessTools.FieldRefAccess<global::HUDBombingState, Vector3>("ccipImpactPointSmoothed");

    [HarmonyPatch(typeof(global::HUDBombingState), nameof(global::HUDBombingState.UpdateWeaponDisplay))]
    private static class UpdateWeaponDisplayPatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::HUDBombingState __instance, Aircraft aircraft)
        {
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null || aircraft == null)
                return;

            __instance.transform.localPosition = Vector3.zero;
            __instance.transform.rotation = cockpitHudCamera.transform.rotation;

            UpdateCcrpDisplay(__instance, aircraft);
            UpdateCcipDisplay(__instance);
        }
    }

    private static void UpdateCcrpDisplay(global::HUDBombingState state, Aircraft aircraft)
    {
        var alignmentBar = AlignmentBar(state);
        var ccrpCircle = CcrpCircle(state);
        var dropCountdown = DropCountdown(state);
        var ccrpFallTime = CcrpFallTime(state);
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (alignmentBar == null || cockpitHudCamera == null || !alignmentBar.gameObject.activeSelf)
            return;

        var averageTargetPosition = AverageTargetPosition(state);
        var targetDelta = averageTargetPosition - aircraft.GlobalPosition();
        var horizontalTargetDelta = new Vector3(targetDelta.x, 0.0f, targetDelta.z);
        var verticalOffset = -targetDelta.y + Vector3.Project(horizontalTargetDelta, aircraft.transform.forward).y;
        var upperWorldPosition = averageTargetPosition.ToLocalPosition() + Vector3.up * verticalOffset;
        var lowerWorldPosition = averageTargetPosition.ToLocalPosition() + Vector3.up * verticalOffset * 0.9f;

        if (!VrHudProjection.TryProjectToCockpitHud(upperWorldPosition, out var upperHudPosition) ||
            !VrHudProjection.TryProjectToCockpitHud(lowerWorldPosition, out var lowerHudPosition))
        {
            alignmentBar.gameObject.SetActive(false);
            return;
        }

        alignmentBar.transform.position = upperHudPosition;
        alignmentBar.transform.rotation = VrHudProjection.GetRotationAlongHudSegment(upperHudPosition, lowerHudPosition, cockpitHudCamera);

        if (ccrpCircle != null)
            ccrpCircle.transform.rotation = cockpitHudCamera.transform.rotation;

        if (dropCountdown != null)
            dropCountdown.transform.rotation = cockpitHudCamera.transform.rotation;

        if (ccrpFallTime != null)
            ccrpFallTime.transform.rotation = cockpitHudCamera.transform.rotation;
    }

    private static void UpdateCcipDisplay(global::HUDBombingState state)
    {
        var ccipPipper = CcipPipper(state);
        var ccipLine = CcipLine(state);
        var ccipFallTime = CcipFallTime(state);
        var velocityVector = SceneSingleton<FlightHud>.i.velocityVector;
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (ccipPipper == null || ccipLine == null || cockpitHudCamera == null || velocityVector == null || !ccipPipper.enabled)
            return;

        var ccipImpactPointSmoothed = CcipImpactPointSmoothed(state);
        var impactWorldPosition = ccipImpactPointSmoothed + Datum.origin.position;
        if (!VrHudProjection.TryProjectToCockpitHud(impactWorldPosition, out var pipperHudPosition))
        {
            ccipPipper.enabled = false;
            ccipLine.enabled = false;
            return;
        }

        ccipPipper.transform.position = pipperHudPosition;
        ccipPipper.transform.rotation = cockpitHudCamera.transform.rotation;

        var velocityHudPosition = velocityVector.transform.position;
        var lineDirection = velocityHudPosition - pipperHudPosition;
        if (lineDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            ccipLine.enabled = false;
            return;
        }

        var normalizedLineDirection = lineDirection.normalized;
        var lineStart = pipperHudPosition + normalizedLineDirection * VrHudProjection.ReferencePixelsToHudDistance(22.0f);
        var lineEnd = velocityHudPosition - normalizedLineDirection * VrHudProjection.ReferencePixelsToHudDistance(8.0f);
        var lineVector = lineEnd - lineStart;
        if (Vector3.Dot(lineDirection, lineVector) < 0.0f)
        {
            ccipLine.enabled = false;
            return;
        }

        VrHudProjection.SetVerticalLine(ccipLine.transform, lineStart, lineEnd, cockpitHudCamera);

        if (ccipFallTime != null)
            ccipFallTime.transform.rotation = cockpitHudCamera.transform.rotation;
    }

}
