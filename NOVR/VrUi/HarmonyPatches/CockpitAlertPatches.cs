using System.Reflection;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine.UI;

namespace NOVR.VrUi.HarmonyPatches;

internal static class CockpitAlertPatches
{
    [HarmonyPatch(typeof(RadarWarning), "RadarWarning_OnRadarWarning")]
    private static class RadarDetection
    {
        [HarmonyPostfix]
        private static void Postfix(Aircraft.OnRadarWarning source, Aircraft ___aircraft)
        {
            if (source.detected) CockpitAlerts.Record(___aircraft, source.emitter, false);
        }
    }
    [HarmonyPatch(typeof(RadarWarning), "RadarWarning_OnJammed")]
    private static class JamDetection
    {
        [HarmonyPostfix]
        private static void Postfix(Unit.JamEventArgs e, Aircraft ___aircraft) => CockpitAlerts.Record(___aircraft, e.jammingUnit, true);
    }
    // Run native expiry/pooling first. Suppress only the duplicated graphics;
    // warning sounds, map pings and marker highlighting still execute normally.
    [HarmonyPatch]
    private static class RadarGraphic
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.Inner(typeof(RadarWarning), "RadarWarningIcon"), "Position");
        [HarmonyPostfix]
        private static void Postfix(bool __result, Image ___image)
        {
            if (__result && CockpitAlerts.Active && ___image != null) ___image.enabled = false;
        }
    }
    [HarmonyPatch]
    private static class JamGraphic
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.Inner(typeof(RadarWarning), "JammingIcon"), "Position");
        [HarmonyPostfix]
        private static void Postfix(bool __result, Image ___image, Text ___text)
        {
            if (!__result || !CockpitAlerts.Active) return;
            if (___image != null) ___image.enabled = false;
            if (___text != null) ___text.enabled = false;
        }
    }
}
