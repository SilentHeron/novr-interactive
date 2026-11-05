// Boundary doubles only. The production auxiliary property, component discovery
// and Rewired postfix run unchanged; no native physics or controller SDK runs.
namespace UnityEngine
{
    public readonly record struct Vector2(float x, float y);
    public static class Mathf
    {
        public static float Abs(float value) => MathF.Abs(value);
        public static float Sign(float value) => value >= 0 ? 1 : -1;
    }
}
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static System.Reflection.FieldInfo? Field(Type type, string name) => type.GetField(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method) { }
        public HarmonyPatch(Type type, string method, Type[] arguments) { }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute { }
}
namespace Rewired { public sealed class Player { } }
public static class GameManager { public static Rewired.Player playerInput = new(); }
public static class PlayerSettings { public static bool throttleUseNegative, invertCollective; }
public sealed class Pilot { }
public sealed class PilotPlayerState { }
public sealed class ControlInputs { public float pitch, roll, yaw, throttle; }
public sealed class Aircraft
{
    public object[] Components = Array.Empty<object>();
    public bool Hover;
    public bool FlightAssist = true;
    public bool IsAutoHoverEnabled() => Hover;
    public T? GetComponentInChildren<T>(bool includeInactive) where T : class => Components.OfType<T>().FirstOrDefault();
}
public class TiltWingController
{
    private bool autoTilt = true;
    public bool Automatic => autoTilt;
}
public sealed class ModdedTiltWing : TiltWingController { }
public sealed class DuctedThrustSystem { }
public sealed class SwivelDuctSystem { }
public sealed class DuctedFan { }
public sealed class SwingWingController { }
public sealed class CustomAircraftSystem { }

namespace NOVR.VrUi.SpecialBehavior
{
    internal static class CockpitMapInteraction
    {
        internal static bool ReserveLeft, ReserveRight;
        internal static bool ReservesThumbstick(bool left) => left ? ReserveLeft : ReserveRight;
    }
}

namespace NOVR.MotionControls
{
    internal sealed class BoolSetting { internal bool Value; }
    internal sealed class MotionControlSettings { internal BoolSetting LeftHanded = new(); }
    internal struct ControllerSample
    {
        internal bool Valid, HasAxis;
        internal UnityEngine.Vector2 Axis;
    }
    // Rest-of-coordinator state supplied to the source-linked auxiliary partial.
    // Physical tracking/handle latch behavior has its own source-linked suites.
    public sealed partial class MotionFlightController
    {
        internal static MotionFlightController? Instance;
        internal bool FlightActive = true, ThrottleHeld = true, StickHeld = false, HasYaw = false;
        internal float Pitch = 0, Roll = 0, Yaw = 0, Throttle = 0;
        private readonly MotionControlSettings Settings = new();
        private ControllerSample _throttleHand = new() { Valid = true, HasAxis = true, Axis = new(0, .8f) };
        private Aircraft? _aircraft;
        internal void Configure(Aircraft aircraft) { _aircraft = aircraft; ConfigureAuxiliaryControls(aircraft); }
        internal void SetHand(float y, bool valid = true, bool hasAxis = true) =>
            _throttleHand = new() { Valid = valid, HasAxis = hasAxis, Axis = new(0, y) };
        internal void SetLeftHanded(bool left) => Settings.LeftHanded.Value = left;
        internal bool SampleFor(Pilot pilot) => FlightActive;
        internal void ResetGrips() { StickHeld = false; ThrottleHeld = false; }
        internal void ObserveThrottle(Pilot pilot, float command) { }
    }
}
