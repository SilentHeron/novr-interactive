using UnityEngine;

public sealed class Aircraft : UnityEngine.Object
{
    public bool disabled, Ejected;
    public Cockpit? cockpit = new();
    public bool HasEjected() => Ejected;
}
public sealed class Cockpit
{
    public bool Detached;
    public bool IsDetached() => Detached;
}
public sealed class ControlMapper { public bool isOpen; }
public enum CameraMode { cockpit, orbit, chase, tv, free, selection, relative }
public sealed class Unit : UnityEngine.Object { }
public sealed class CameraStateManager { public static CameraMode cameraMode; public Unit? followingUnit; }
public static class DynamicMap { public static bool mapMaximized, AllowedToOpen = true; }
[Flags]
public enum CursorFlags { None = 0, GameMenu = 1, Map = 2, SelectionMenu = 4, Dialogue = 8, NotInGame = 16, Chat = 32, Loading = 64, EmptyScene = 128, CameraControlUI = 256 }
public static class CursorManager
{
    public static CursorFlags Flags;
    public static CursorFlags GetFlags() => Flags;
}
public static class LayerHelper { public static int GetVrUiLayer() => 28; }

namespace UnityEngine.XR
{
    public readonly record struct InputDevice(bool isValid)
    {
        public void StopHaptics() { }
    }
}
namespace NOVR
{
    public static class APIBus { public static Camera? CockpitHudCamera, MainCamera; }
    internal sealed class ModConfiguration
    {
        internal static readonly ModConfiguration Instance = new();
        internal readonly MotionSettings MotionControls = new();
        internal sealed class MotionSettings { internal readonly BoolValue LeftHanded = new(); }
        internal sealed class BoolValue { internal bool Value; }
    }
}
namespace NOVR.MotionControls
{
    internal struct ControllerSample
    {
        internal bool Valid, HasAxis, StickClick, NativeFrame;
        internal float Trigger;
        internal Vector2 Axis;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal UnityEngine.XR.InputDevice Device;
    }
    internal static class TrackedControllers
    {
        internal static bool Available = true;
        internal static ControllerSample Left, Right;
        internal static ControllerSample Read(bool left) => left ? Left : Right;
    }
    internal static class HelmetNightVision
    {
        internal static bool Owns;
        internal static bool OwnsHand(bool left) => Owns;
    }
    internal static class CockpitLayout { internal static bool Active; }
    internal enum InteractionCue { Release, Eject, Guard }
    internal static class InteractionHaptics
    {
        internal static void Pulse(UnityEngine.XR.InputDevice device, float amplitude, float duration, InteractionCue? cue = null) { }
        internal static void PulseOnly(UnityEngine.XR.InputDevice device, float amplitude, float duration) { }
    }
    // Confirmation mesh is outside this coordinator boundary; its gesture is
    // already linked from production and covered by the dedicated checks.
    internal sealed class EjectConfirmationGraphic : MonoBehaviour
    {
        internal bool raycastTarget;
        internal float Progress;
        internal void Show(Sprite? sprite) => gameObject.SetActive(true);
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal static class CockpitHudWidgets { internal static bool Editing; }
    internal static class CockpitMapInteraction
    {
        internal static bool Reserves;
        internal static bool ReservesThumbstick(bool left) => Reserves;
    }
}
