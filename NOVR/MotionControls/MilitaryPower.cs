using System;
using HarmonyLib;

namespace NOVR.MotionControls;

internal static class MilitaryPower
{
    // Read the actual nozzle threshold, not a guessed universal percentage.
    // Ducted engines disable their afterburners and helicopters use a collective.
    internal static float Find(Aircraft aircraft)
    {
        var result = 1f;
        // Aircraft parts become separate physics roots at runtime. The engine
        // registry remains authoritative after that reparenting.
        foreach (var state in aircraft.engineStates)
        {
            if (state is not Turbojet engine) continue;
            if (AccessTools.Field(typeof(Turbojet), "nozzles")?.GetValue(engine) is not JetNozzle[] nozzles) continue;
            foreach (var nozzle in nozzles)
            {
                if (nozzle == null || AccessTools.Field(typeof(JetNozzle), "afterburners")?.GetValue(nozzle) is not Array burners) continue;
                foreach (var burner in burners)
                {
                    if (burner == null) continue;
                    var type = burner.GetType();
                    if (AccessTools.Field(type, "throttleStart")?.GetValue(burner) is float start &&
                        AccessTools.Field(type, "throttleEnd")?.GetValue(burner) is float end &&
                        AccessTools.Field(type, "thrust")?.GetValue(burner) is float thrust &&
                        FlightControlMath.IsFinite(start) && FlightControlMath.IsFinite(end) &&
                        start > 0 && start < 1 && end > start && thrust > 0)
                        result = Math.Min(result, start);
                }
            }
        }
        return result;
    }
}
