// Only Unity/rendering, device input and adjacent feature boundaries are doubled.
// Profile storage, layout transactions, proximity arbitration and grip gates use production code.
using UnityEngine;

namespace UnityEngine
{
    public partial class MonoBehaviour
    {
        public readonly Transform transform = new();
        protected static void Destroy(object value)
        {
        }
    }
    public sealed class GameObject(string name, params Type[] components)
    {
        public string Name => name;
        public Type[] Components => components;
        public int layer;
        public readonly Transform transform = new();
        public T GetComponent<T>() where T : new() => new();
    }
    public sealed class Transform
    {
        public Transform? parent;
        public void SetParent(Transform value, bool worldPositionStays) => parent = value;
        public Vector3 TransformPoint(Vector3 point) => point;
    }
    public sealed class Camera
    {
        public readonly Transform transform = new();
    }
    public sealed class Material
    {
    }
    public sealed class LineRenderer
    {
        public bool enabled;
        public Material? sharedMaterial;
        public int positionCount;
        public float startWidth, endWidth;
        public readonly GameObject gameObject = new("line");
        public void SetPosition(int index, Vector3 position)
        {
        }
    }
    public readonly record struct Color(float r, float g, float b);
    public partial struct Vector3
    {
        public static Vector3 forward => new(0, 0, 1);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 value, float scale) => new(value.x * scale, value.y * scale, value.z * scale);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static float Distance(Vector3 a, Vector3 b) => MathF.Sqrt(Dot(a - b, a - b));
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
        public static Vector3 operator *(Quaternion rotation, Vector3 direction)
        {
            var result = System.Numerics.Vector3.Transform(new(direction.x, direction.y, direction.z),
                new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            return new(result.X, result.Y, result.Z);
        }
    }
    public readonly record struct Ray(Vector3 origin, Vector3 direction)
    {
        public Vector3 GetPoint(float distance) => origin + direction * distance;
    }
    public static class Time
    {
        public static float timeScale = 1;
    }
}
public sealed class Cockpit
{
    public bool Detached; public bool IsDetached() => Detached;
}
public sealed partial class Aircraft
{
    public Cockpit? cockpit = new();
}
public sealed class ControlMapper
{
    public bool isOpen;
}
public static partial class GameManager
{
    public static bool flightControlsEnabled = true;
    public static ControlMapper? controlMapper;
}
public static class DynamicMap
{
    public static bool mapMaximized;
}
public enum CursorFlags
{
    GameMenu
}
public static class CursorManager
{
    public static bool GameMenu; public static bool GetFlag(CursorFlags flag) => GameMenu;
}
public static class SceneSingleton<T> where T : class
{
    public static T? i;
}
public sealed class RadialMenuMain
{
    public static bool InUse; public static bool IsInUse() => InUse;
}
namespace NOVR
{
    public static class NOVRHeadsetData
    {
        public static Vector3 TranslationAnchor, TranslationCalibrationOffset;
        public static Quaternion RotationCalibrationOffset = Quaternion.identity;
    }
    public static class APIBus
    {
        public static Camera? CockpitHudCamera;
    }
    public static class LayerHelper
    {
        public static int GetVrUiLayer() => 0;
    }
}
namespace NOVR.VrUi
{
    public static class WristMenu
    {
        public static bool IsOpen; public static void Close() => IsOpen = false;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    public sealed class CockpitHudWidgets
    {
        public static CockpitHudWidgets? Instance;
        public static bool Editing, LeftBusy, RightBusy;
        public static bool IsHandBusy(bool left) => left ? LeftBusy : RightBusy;
        public void Begin() => throw new InvalidOperationException("Use production layout's unified entry point in this fixture.");
        public void Finish(bool save) => throw new InvalidOperationException("HUD coordinator is checked in HudWidgetSession.");
    }
}
namespace NOVR.MotionControls
{
    public sealed partial class MotionControlSettings
    {
        public readonly BepInEx.Configuration.ConfigEntry<bool> Enabled = new(false);
        public readonly BepInEx.Configuration.ConfigEntry<float> GrabRadius = new(.15f), ThrottleTravel = new(.12f);
    }
    public sealed partial class MotionFlightController
    {
        public float Throttle = .5f;
        public Vector3 ThrottleOffset;
        public int PlacementCancels;
        public void CancelPlacement() => PlacementCancels++;
    }
    public sealed class ControllerActions
    {
        public static ControllerActions Instance = new();
        public int Resets;
        public void ResetButtons() => Resets++;
    }
    public readonly record struct ControllerSample(bool Valid, Vector3 Position, float Grip)
    {
        public Vector3 AimPosition => Position;
        public Quaternion AimRotation => Quaternion.identity;
        public int Device => 0;
    }
    public static partial class TrackedControllers
    {
        public static bool Available = true;
        public static ControllerSample Left, Right;
        public static ControllerSample Read(bool left) => left ? Left : Right;
    }
    public enum InteractionCue
    {
        Grab, Release
    }
    public static class InteractionHaptics
    {
        public static int Grabs;
        public static void Pulse(int device, float strength = 0, float duration = 0, InteractionCue cue = InteractionCue.Grab)
        {
            if (cue == InteractionCue.Grab)
                Grabs++;
        }
    }
    public static class CockpitMaterials
    {
        public static Material Opaque(string name, Color color) => new();
    }
}
