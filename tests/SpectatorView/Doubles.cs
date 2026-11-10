using System.Reflection;
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class HarmonyPatch(Type type, string method) : Attribute
    { public Type Type => type; public string Method => method; }
    [AttributeUsage(AttributeTargets.Method)] public sealed class HarmonyPrefix : Attribute { }
    public static class AccessTools
    { public static FieldInfo? Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); }
}
namespace UnityEngine
{
    public struct Vector2(float x, float y) { public float x = x, y = y; }
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float n) => new(a.x * n, a.y * n, a.z * n);
    }
    public readonly struct Quaternion(System.Numerics.Quaternion value)
    {
        internal System.Numerics.Quaternion Value => value;
        public static Quaternion Euler(float x, float y, float z) => new(System.Numerics.Quaternion.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180));
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(a.Value * b.Value);
    }
    public sealed class Transform
    {
        public Vector3 right = new(1, 0, 0), forward = new(0, 0, 1);
        public Quaternion rotation = Quaternion.Euler(0, 0, 0);
        public bool Destroyed;
        public static bool operator ==(Transform? a, Transform? b) => ReferenceEquals(a, b) || a is null && b!.Destroyed || b is null && a!.Destroyed;
        public static bool operator !=(Transform? a, Transform? b) => !(a == b);
        public override bool Equals(object? obj) => this == obj as Transform;
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public static class Time { public static int frameCount; public static float timeScale = 1, unscaledDeltaTime = .02f; }
    public static class Debug { public static int Warnings; public static void LogWarning(string text) => Warnings++; }
    public static class Mathf
    {
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static float DeltaAngle(float from, float to) { var n = (to - from) % 360; return n > 180 ? n - 360 : n < -180 ? n + 360 : n; }
    }
}
namespace UnityEngine.XR { public static class XRSettings { public static bool enabled; } }
namespace NuclearOption.MissionEditorScripts { public static class InputFieldChecker { public static bool InsideInputField; } }
namespace NuclearOption.UI { public static class LeaderboardMenu { public static bool Open; public static bool IsOpen() => Open; } }
namespace NOVR.MotionControls
{
    public struct ControllerSample { public bool Valid, HasAxis; public UnityEngine.Vector2 Axis; public float Grip; }
    public static class TrackedControllers
    {
        public static bool Available = true, Frame;
        public static ControllerSample Left = new() { Valid = true, HasAxis = true }, Right = new() { Valid = true, HasAxis = true };
        public static ControllerSample Read(bool left) => left ? Left : Right;
        public static bool NativeFrame(bool left) => Frame;
    }
    internal static class ControllerActions { internal const ControllerButton CockpitReturnButton = ControllerButton.OutsideOrSecondary; }
    public static class NativeHandOptionWheel
    {
        public static bool IsOpen, LeftOwned, RightOwned;
        public static bool OwnsHand(bool left) => left ? LeftOwned : RightOwned;
    }
    public static class CockpitLayout { public static bool Active; }
}
namespace NOVR.VrUi { public static class WristMenu { public static bool IsOpen; } }
namespace NOVR
{
    public sealed class Setting<T>(T value) { public T Value = value; }
    public sealed class MotionControlSettings { public readonly Setting<bool> Enabled = new(true), LeftHanded = new(false); }
    public sealed class ModConfiguration
    {
        public static readonly ModConfiguration Instance = new();
        public readonly MotionControlSettings MotionControls = new();
    }
}
namespace NOVR.VrUi.SpecialBehavior { public static class CockpitHudWidgets { public static bool Editing; } }
public static class DynamicMap { public static bool mapMaximized; }
public sealed class ControlMapper { public bool isOpen; }
public enum GameState { Menu, SinglePlayer, Multiplayer }
public static class GameManager
{
    public static bool flightControlsEnabled = true;
    public static ControlMapper? controlMapper;
    public static GameState gameState = GameState.SinglePlayer;
    public static Aircraft? LocalAircraft;
    public static bool GetLocalAircraft(out Aircraft aircraft) { aircraft = LocalAircraft!; return LocalAircraft != null; }
}
public sealed class GameplayUI
{
    public readonly List<string> Messages = new();
    public void GameMessage(string message) => Messages.Add(message);
}
public sealed class PilotState { }
public sealed class Pilot
{
    public bool dead;
    public Aircraft? aircraft;
    public readonly PilotState playerState = new();
    public PilotState? currentState;
    public Pilot() => currentState = playerState;
}
public sealed class UnitPart { public bool Detached; public bool IsDetached() => Detached; }
public sealed class Aircraft : Unit
{
    public bool disabled, Ejected;
    public UnitPart? cockpit = new();
    public Pilot[]? pilots;
    public UnityEngine.Transform? cockpitViewPoint = new();
    public Aircraft() { pilots = new[] { new Pilot { aircraft = this } }; }
    public bool HasEjected() => Ejected;
}
[Flags] public enum CursorFlags { None = 0, GameMenu = 1, Map = 2, SelectionMenu = 4, CameraControlUI = 8 }
public static class CursorManager
{
    public static CursorFlags Flags;
    public static bool Menu { get => GetFlag(CursorFlags.GameMenu); set => Flags = value ? Flags | CursorFlags.GameMenu : Flags & ~CursorFlags.GameMenu; }
    public static bool GetFlag(CursorFlags flag) => (Flags & flag) != 0;
    public static CursorFlags GetFlags() => Flags;
}
public static class SceneSingleton<T> where T : class { public static T? i; }
public sealed class RadialMenuMain
{
    public static bool Open;
    public bool isActiveAndEnabled = true;
    public static bool IsInUse() => Open;
}
public abstract class CameraBaseState { public abstract void UpdateState(CameraStateManager camera); }
public class Unit { }
public enum CameraMode { cockpit, orbit, free, chase, tv, selection, relative, encyclopedia }
public sealed class CameraStateManager
{
    public static CameraMode cameraMode = CameraMode.orbit;
    public bool allowInputs = true;
    public Unit? followingUnit = new();
    public object? followingRB = new();
    public readonly UnityEngine.Transform transform = new();
    public UnityEngine.Transform? cameraPivot = new();
    public UnityEngine.Vector3 cameraVelocity;
    public readonly CameraOrbitState orbitState = new();
    public readonly CameraCockpitState cockpitState = new();
    public readonly CameraFreeState freeState = new();
    public CameraBaseState? currentState;
    public int Switches;
    // Native: following nothing hands the camera to the free state.
    public void SetFollowingUnit(Unit? unit)
    {
        followingUnit = unit;
        SwitchState(unit != null ? orbitState : freeState);
    }
    public void SwitchState(CameraBaseState state)
    {
        currentState = state;
        if (state == orbitState) cameraMode = CameraMode.orbit;
        else if (state is CameraCockpitState) cameraMode = CameraMode.cockpit;
        else if (state is CameraFreeState) cameraMode = CameraMode.free;
        else if (state is CameraChaseState) cameraMode = CameraMode.chase;
        else if (state is CameraTVState) cameraMode = CameraMode.tv;
        Switches++;
    }
}
public sealed class CameraOrbitState : CameraBaseState
{
    private float panView, tiltView, viewDistAdjust;
    public float Pan => panView; public float Tilt => tiltView; public float Distance => viewDistAdjust;
    public int Updates;
    public CameraOrbitState() { panView = 0; tiltView = 20; viewDistAdjust = 0; }
    public override void UpdateState(CameraStateManager camera) { Updates++; NOVR.VrCamera.SpectatorCameraInput.ApplyOrbit(this, camera); }
}
public sealed class CameraFreeState : CameraBaseState
{
    private float panView = 0, tiltView = 0;
    private bool trackTarget = true;
    public float Pan => panView; public float Tilt => tiltView; public bool Track => trackTarget;
    public override void UpdateState(CameraStateManager camera) { }
}
public sealed class CameraChaseState : CameraBaseState { public override void UpdateState(CameraStateManager camera) { } }
public sealed class CameraCockpitState : CameraBaseState { public override void UpdateState(CameraStateManager camera) { } }
public sealed class CameraTVState : CameraBaseState { public override void UpdateState(CameraStateManager camera) { } }
public sealed class CameraSelectionState(UnityEngine.Transform? selectedTarget)
{
    private readonly UnityEngine.Transform? target = selectedTarget;
    public UnityEngine.Transform? Target => target;
}
