using System;

namespace NOVR.MotionControls;

// Reserve the MIL shelf inside the configured end-to-end travel. The inverse
// mapping preserves the command on release/regrab without adding travel or a timer.
internal static class ThrottleDetent
{
    internal const float TravelMeters = .016f;
    internal static float CommandTravel(float totalTravel) => Math.Max(.01f, totalTravel - TravelMeters);
    internal static float Position(float command, float mil, float totalTravel) =>
        Input(command, mil, TravelMeters / CommandTravel(totalTravel)) * CommandTravel(totalTravel) / totalTravel;
    internal static float Input(float command, float mil, float width) =>
        command < mil ? command : command > mil ? command + width : mil + width * .5f;
    internal static float Output(float input, float mil, float width) =>
        !FlightControlMath.IsFinite(input) ? mil : Math.Max(0, Math.Min(1,
            input < mil ? input : input <= mil + width ? mil : input - width));
}
