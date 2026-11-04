using System;
using System.Reflection;
using HarmonyLib;

namespace NOVR.MotionControls;

internal static class MilitaryPower
{
    private static readonly FieldInfo? TurbojetNozzles = AccessTools.Field(typeof(Turbojet), "nozzles");
    private static readonly FieldInfo? TurbofanNozzles = AccessTools.Field(typeof(Turbofan), "nozzles");
    private static readonly FieldInfo? NozzleBurners = AccessTools.Field(typeof(JetNozzle), "afterburners");
    private static readonly FieldInfo? GaugeAfterburner = AccessTools.Field(typeof(ThrottleGauge), "afterburner");
    private static readonly FieldInfo? GaugeRegions = AccessTools.Field(typeof(ThrottleGauge), "throttleRegions");

    // Read the actual nozzle threshold, not a guessed universal percentage.
    // Ducted engines disable their afterburners and helicopters use a collective.
    internal static float Find(Aircraft aircraft) => Find(aircraft, out _);
    internal static float Find(Aircraft aircraft, out float afterburnerStart)
    {
        var result = 1f;
        // Aircraft parts become separate physics roots at runtime. The engine
        // registry remains authoritative after that reparenting.
        foreach (var state in aircraft.engineStates)
        {
            // Both native jet engine families pass the pilot's normalized
            // throttle to their afterburners. DuctedThrustSystem does not.
            var field = state is Turbojet ? TurbojetNozzles : state is Turbofan ? TurbofanNozzles : null;
            if (field?.GetValue(state) is not JetNozzle[] nozzles)
                continue;
            foreach (var nozzle in nozzles)
            {
                if (nozzle == null || NozzleBurners?.GetValue(nozzle) is not Array burners)
                    continue;
                foreach (var burner in burners)
                {
                    if (burner == null)
                        continue;
                    var type = burner.GetType();
                    if (AccessTools.Field(type, "throttleStart")?.GetValue(burner) is float start &&
                        AccessTools.Field(type, "throttleEnd")?.GetValue(burner) is float end &&
                        AccessTools.Field(type, "thrust")?.GetValue(burner) is float thrust &&
                        FlightControlMath.IsFinite(start) && FlightControlMath.IsFinite(end) && FlightControlMath.IsFinite(thrust) &&
                        start > 0 && start < 1 && end > start && thrust > 0)
                        result = Math.Min(result, start);
                }
            }
        }
        if (result < 1f)
        {
            // The aircraft's authored native gauge may enter AB before the
            // nozzle's thrust ramp. Respect the earlier boundary so the shelf
            // is dry in both engine behavior and the original cockpit display.
            var extras = aircraft.GetAircraftParameters().HUDExtras;
            if (extras != null)
                foreach (var gauge in extras.GetComponentsInChildren<ThrottleGauge>(true))
                {
                    if (GaugeAfterburner?.GetValue(gauge) is not true ||
                        GaugeRegions?.GetValue(gauge) is not Array regions || regions.Length == 0)
                        continue;
                    // Native ThrottleGauge itself uses the final region's start
                    // for its afterburner boundary, independent of display names.
                    var region = regions.GetValue(regions.Length - 1);
                    if (region != null && AccessTools.Field(region.GetType(), "start")?.GetValue(region) is float start &&
                        FlightControlMath.IsFinite(start) && start > 0 && start < 1)
                        result = Math.Min(result, start);
                }
        }
        afterburnerStart = result;
        return ThrottleDetent.MilitaryCommand(result);
    }
}
