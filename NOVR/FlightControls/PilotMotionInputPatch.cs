using HarmonyLib;

namespace NOVR.MotionControls;

internal static class PilotMotionInputPatch
{
    [HarmonyPatch(typeof(Rewired.Player), "GetAxisRaw", new[] { typeof(string) })]
    private static class AuxiliaryThumbstick
    {
        [HarmonyPostfix]
        private static void Postfix(Rewired.Player __instance, string __0, ref float __result)
        {
            if (__instance != GameManager.playerInput || __0 != "Custom Axis 1")
                return;
            var axis = MotionFlightController.Instance?.AuxiliaryAxis ?? 0;
            if (axis != 0)
                __result = axis;
        }
    }

    // Hook before the game's FilterInputs step, retaining flight assists and network replication.
    [HarmonyPatch(typeof(PilotPlayerState), "PlayerAxisControls")]
    private static class Axes
    {
        [HarmonyPostfix]
        private static void Postfix(Pilot ___pilot, ControlInputs ___controlInputs, float ___pilotStrength)
        {
            var motion = MotionFlightController.Instance;
            if (motion == null || !motion.SampleFor(___pilot))
            {
                // Outside view: no virtual handles, so the left thumbstick
                // flies and the triggers steer. Throttle is left alone.
                // A blacked-out pilot gets no input, as in the cockpit.
                if (___pilotStrength < 0.2f || !ExternalFlightInput.SampleFor(___pilot))
                    return;
                if (ExternalFlightInput.Flying)
                {
                    ___controlInputs.pitch = ExternalFlightInput.Pitch;
                    ___controlInputs.roll = ExternalFlightInput.Roll;
                }
                if (ExternalFlightInput.Rudder)
                    ___controlInputs.yaw = ExternalFlightInput.Yaw;
                return;
            }
            if (___pilotStrength < 0.2f)
            {
                motion.ResetGrips();
                return;
            }
            if (!motion.StickHeld)
                return;
            ___controlInputs.pitch = motion.Pitch;
            ___controlInputs.roll = motion.Roll;
            if (motion.HasYaw)
                ___controlInputs.yaw = motion.Yaw;
        }
    }

    [HarmonyPatch(typeof(PilotPlayerState), "PlayerThrottleAxis1Controls")]
    private static class Throttle
    {
        [HarmonyPostfix]
        private static void Postfix(Pilot ___pilot, ControlInputs ___controlInputs, bool ___collective,
            float ___pilotStrength, ref float ___simulatedThrottle)
        {
            var motion = MotionFlightController.Instance;
            motion?.ObserveThrottle(___pilot, ___controlInputs.throttle);
            // The outside view never touches the throttle: it stays where the
            // cockpit left it, held by the game's own accumulator.
            if (motion == null || !motion.SampleFor(___pilot))
                return;
            if (___pilotStrength < 0.2f)
            {
                motion.ResetGrips();
                return;
            }
            if (!motion.ThrottleHeld)
                return;
            ___controlInputs.throttle = motion.Throttle;
            // Seed the native accumulator so letting go keeps thrust instead of snapping back.
            ___simulatedThrottle = FlightControlMath.ToSimulatedThrottle(motion.Throttle,
                PlayerSettings.throttleUseNegative, ___collective && PlayerSettings.invertCollective);
            motion.ObserveThrottle(___pilot, motion.Throttle);
        }
    }
}
