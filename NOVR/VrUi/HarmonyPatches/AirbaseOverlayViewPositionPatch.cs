using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine.UI;
using System.Reflection;
using UnityEngine;

namespace NOVR.VrUi.HarmonyPatches;

internal static class AirbaseOverlayViewPositionPatch
{
    private static readonly FieldInfo? AirbaseMarkerField = AccessTools.Field(typeof(global::AirbaseOverlay), "airbaseMarker");
    private static readonly FieldInfo? AirbaseLabelField = AccessTools.Field(typeof(global::AirbaseOverlay), "airbaseLabel");
    private static readonly FieldInfo? NearestAirbaseField = AccessTools.Field(typeof(global::AirbaseOverlay), "nearestAirbase");
    private static readonly FieldInfo? RunwayUsageField = AccessTools.Field(typeof(global::AirbaseOverlay), "runwayUsage");
    private static readonly FieldInfo? LandingField = AccessTools.Field(typeof(global::AirbaseOverlay), "landing");
    private static readonly FieldInfo? TaxiingToRunwayField = AccessTools.Field(typeof(global::AirbaseOverlay), "taxiingToRunway");
    private static readonly FieldInfo? ReachedRunwayField = AccessTools.Field(typeof(global::AirbaseOverlay), "reachedRunway");
    private static readonly FieldInfo? RunwayBordersField = AccessTools.Field(typeof(global::AirbaseOverlay), "runwayBorders");
    private static readonly FieldInfo? GlideslopeField = AccessTools.Field(typeof(global::AirbaseOverlay), "glideslope");
    private static readonly FieldInfo? GlideslopeAimPointField = AccessTools.Field(typeof(global::AirbaseOverlay), "glideslopeAimPoint");

    [HarmonyPatch(typeof(global::AirbaseOverlay), "LateUpdate")]
    private static class LateUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(global::AirbaseOverlay __instance)
        {
            var hud = SceneSingleton<CombatHUD>.i;
            if (hud == null || __instance == null) return;
            var aircraft = hud.aircraft;
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (aircraft == null || mainCamera == null || cockpitHudCamera == null)
                return;

            HudElementStyle.Add(AirbaseMarkerField?.GetValue(__instance) as Graphic, HudElementKind.Marker);
            HudElementStyle.Add(AirbaseLabelField?.GetValue(__instance) as Graphic, HudElementKind.Label);
            HudElementStyle.Add(GlideslopeAimPointField?.GetValue(__instance) as Graphic, HudElementKind.Reticle);
            __instance.transform.rotation = cockpitHudCamera.transform.rotation;
            
            UpdateAirbaseMarker(__instance, aircraft);
            UpdateRunwayBorders(__instance);
            UpdateGlideslope(__instance, aircraft);
        }
    }

    private static void UpdateAirbaseMarker(global::AirbaseOverlay overlay, Aircraft aircraft)
    {
        var airbaseMarker = AirbaseMarkerField?.GetValue(overlay) as Behaviour;
        var airbaseLabel = AirbaseLabelField?.GetValue(overlay) as Behaviour;
        var nearestAirbase = NearestAirbaseField?.GetValue(overlay) as Airbase;
        var runwayUsage = GetRunwayUsage(overlay);
        var landing = LandingField?.GetValue(overlay) is true;
        var taxiingToRunway = TaxiingToRunwayField?.GetValue(overlay) is true;
        var reachedRunway = ReachedRunwayField?.GetValue(overlay) is true;
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (airbaseMarker == null || airbaseLabel == null || nearestAirbase == null || cockpitHudCamera == null || !airbaseMarker.enabled)
            return;

        if (landing || aircraft.radarAlt < 1.0f && !taxiingToRunway)
            return;

        var center = nearestAirbase.center;
        var pilots = aircraft.pilots;
        if (center == null || pilots == null || pilots.Length == 0) return;
        var pilot = pilots[0];
        if (pilot == null || pilot.flightInfo == null) return;
        var markerWorldPosition = center.position;
        if (!pilot.flightInfo.HasTakenOff)
        {
            if (!taxiingToRunway || reachedRunway || !runwayUsage.HasValue)
                return;

            if (runwayUsage.Value.Runway == null) return;
            var start = runwayUsage.Value.GetStart();
            if (start == null) return;
            // Match native taxi guidance: the entrance, not the far endpoint.
            markerWorldPosition = start.position;
        }

        var offScreen = NavigationHudProjection.PinToEdge(markerWorldPosition, out var markerHudPosition, out _);
        if (offScreen)
        {
            airbaseMarker.transform.position = markerHudPosition;
            airbaseLabel.transform.position = airbaseLabel is TMPro.TMP_Text label
                ? NavigationHudProjection.LabelPosition(markerHudPosition, label)
                : markerHudPosition - cockpitHudCamera.transform.up * 20;
        }
        else
        {
            airbaseMarker.transform.position = markerHudPosition;
            airbaseLabel.transform.position = markerHudPosition - Vector3.up * 20.0f;
        }

        var rotation = Quaternion.LookRotation(airbaseMarker.transform.position - cockpitHudCamera.transform.position);
        
        airbaseMarker.transform.rotation = rotation;
        airbaseLabel.transform.rotation = offScreen ? cockpitHudCamera.transform.rotation : rotation;
    }

    private static void UpdateRunwayBorders(global::AirbaseOverlay overlay)
    {
        var runwayUsage = GetRunwayUsage(overlay);
        var landing = LandingField?.GetValue(overlay) is true;
        var runwayBorders = GetBehaviours(RunwayBordersField?.GetValue(overlay));
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (!landing || !runwayUsage.HasValue || runwayBorders == null || runwayBorders.Length < 4 || cockpitHudCamera == null)
            return;

        var runway = runwayUsage.Value.Runway;
        if (runway == null)
            return;

        var width = runway.GetWidth();
        var start = runway.Start;
        var end = runway.End;
        if (start == null || end == null) return;
        var corners = new[]
        {
            start.position - 0.5f * width * start.right,
            start.position + 0.5f * width * start.right,
            end.position + 0.5f * width * end.right,
            end.position - 0.5f * width * end.right
        };

        var hudCorners = new Vector3[4];
        for (var i = 0; i < corners.Length; i++)
        {
            if (!VrHudProjection.TryProjectToCockpitHud(corners[i], out hudCorners[i]))
            {
                SetRunwayBordersEnabled(runwayBorders, false);
                return;
            }
        }

        SetRunwayBordersEnabled(runwayBorders, true);
        for (var i = 0; i < runwayBorders.Length && i < 4; i++)
        {
            var nextIndex = (i + 1) % 4;
            var border = runwayBorders[i];
            if (border != null)
                VrHudProjection.SetVerticalLine(border.transform, hudCorners[i], hudCorners[nextIndex], cockpitHudCamera);
        }
    }

    private static void UpdateGlideslope(global::AirbaseOverlay overlay, Aircraft aircraft)
    {
        var runwayUsage = GetRunwayUsage(overlay);
        var glideslope = GlideslopeField?.GetValue(overlay) as Behaviour;
        var glideslopeAimPoint = GlideslopeAimPointField?.GetValue(overlay) as Behaviour;
        var cockpitHudCamera = APIBus.CockpitHudCamera;
        if (!runwayUsage.HasValue || runwayUsage.Value.Runway == null || glideslope == null || glideslopeAimPoint == null ||
            cockpitHudCamera == null || aircraft.rb == null || !glideslope.enabled)
            return;

        var runway = runwayUsage.Value.Runway;
        if (runway.Start == null || runway.End == null) return;
        // Native touchdown is inset from the runway start and respects reversal
        // and floating-origin coordinates; GetEnd points at the far threshold.
        var touchdownPosition = runwayUsage.Value.GetTouchdownPoint().ToLocalPosition();
        var distanceToTouchdown = FastMath.Distance(aircraft.transform.position, touchdownPosition);
        var runwayVelocity = runwayUsage.Value.Runway.GetVelocity();
        var closingSpeed = Vector3.Dot(aircraft.rb.velocity - runwayVelocity, (touchdownPosition - aircraft.transform.position).normalized);
        var timeToTouchdown = distanceToTouchdown / closingSpeed;
        var aimPointWorldPosition = runwayUsage.Value.GetGlideslopeAimpoint(
            aircraft,
            distanceToTouchdown * 0.9f,
            timeToTouchdown * 0.9f);

        if (!VrHudProjection.TryProjectToCockpitHud(touchdownPosition, out var touchdownHudPosition) ||
            !VrHudProjection.TryProjectToCockpitHud(aimPointWorldPosition, out var aimPointHudPosition))
        {
            glideslope.enabled = false;
            glideslopeAimPoint.enabled = false;
            return;
        }

        VrHudProjection.SetVerticalLine(glideslope.transform, touchdownHudPosition, aimPointHudPosition, cockpitHudCamera, -8.0f);
        glideslopeAimPoint.transform.position = aimPointHudPosition;
        glideslopeAimPoint.transform.rotation = cockpitHudCamera.transform.rotation;
    }

    private static Airbase.Runway.RunwayUsage? GetRunwayUsage(global::AirbaseOverlay overlay) =>
        RunwayUsageField?.GetValue(overlay) as Airbase.Runway.RunwayUsage?;

    // Image[] is already covariant with Behaviour[]. Borrow the native array;
    // allocating a reflection copy every LateUpdate adds avoidable HUD garbage.
    private static Behaviour?[]? GetBehaviours(object? value) => value as Behaviour?[];

    private static void SetRunwayBordersEnabled(Behaviour?[] runwayBorders, bool enabled)
    {
        foreach (var runwayBorder in runwayBorders)
        {
            if (runwayBorder != null)
                runwayBorder.enabled = enabled;
        }
    }

}
