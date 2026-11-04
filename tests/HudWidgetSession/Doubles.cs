// Session/profile methods are production source. These boundaries supply
// in-memory config, Unity lifetime signals and observable rendering/contact I/O.
using System.Reflection;
using NOVR.MotionControls;
using UnityEngine;

internal static class Effects
{
    internal static readonly List<string> Events = new();
}
namespace BepInEx.Configuration
{
    public sealed class ConfigEntry<T>(T value)
    {
        public T Value = value;
        public object DefaultValue = value!;
    }
    public sealed class ConfigFile
    {
        private readonly Dictionary<(string, string), object> _entries = new();
        public int Saves;
        public ConfigEntry<T> Bind<T>(string section, string key, T value, string description)
        {
            if (!_entries.TryGetValue((section, key), out var entry))
                _entries[(section, key)] = entry = new ConfigEntry<T>(value);
            return (ConfigEntry<T>)entry;
        }
        public void Save() => Saves++;
    }
}
namespace UnityEngine
{
    public class MonoBehaviour
    {
        protected static void Destroy(GameObject value)
        {
            Effects.Events.Add("destroy:" + value.Name);
            value.Destroyed = true;
        }
    }
    public sealed class GameObject(string name)
    {
        public string Name = name; public bool Destroyed;
    }
    public sealed class RectTransform(string name)
    {
        public GameObject gameObject = new(name);
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order { get; } = order;
    }
    public readonly record struct Vector2(float x, float y);
    public struct Vector3(float x, float y, float z) : IEquatable<Vector3>
    {
        public float x = x, y = y, z = z;
        public static Vector3 zero => default;
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static bool operator ==(Vector3 a, Vector3 b) => a.Equals(b);
        public static bool operator !=(Vector3 a, Vector3 b) => !a.Equals(b);
        public bool Equals(Vector3 value) => x == value.x && y == value.y && z == value.z;
        public override bool Equals(object? value) => value is Vector3 other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(x, y, z);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
    public static class Time
    {
        public static float timeScale = 1;
    }
    public static class Application
    {
        public static event Action? onBeforeRender;
        internal static int Subscribers => onBeforeRender?.GetInvocationList().Length ?? 0;
        internal static void Clear() => onBeforeRender = null;
    }
}
namespace UnityEngine.XR
{
    public static class XRSettings
    {
        public static bool enabled = true;
    }
}
public enum CameraMode
{
    cockpit, external
}
public static class CameraStateManager
{
    public static CameraMode cameraMode;
}
public sealed class AircraftDefinition
{
    public string jsonKey = "SmallFighter1", name = "FS20", unitName = "FS-20";
}
public sealed class Aircraft
{
    public AircraftDefinition? definition = new(); public bool Lost, Collective;
}
public sealed class ControlMapper
{
    public bool isOpen;
}
public static class GameManager
{
    public static Aircraft? Current;
    public static bool flightControlsEnabled = true;
    public static ControlMapper? controlMapper;
    public static bool GetLocalAircraft(out Aircraft aircraft)
    {
        aircraft = Current!;
        return aircraft != null;
    }
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
    public sealed class ModConfiguration
    {
        public static ModConfiguration Instance = new();
        public readonly BepInEx.Configuration.ConfigFile Config = new();
        public readonly MotionControlSettings MotionControls = new();
    }
    public static class NOVRHeadsetData
    {
        public static Vector3 TranslationAnchor, TranslationCalibrationOffset;
        public static Quaternion RotationCalibrationOffset = Quaternion.identity;
    }
}
namespace NOVR.VrUi
{
    public static class WristMenu
    {
        public static bool IsOpen;
    }
}
namespace NOVR.MotionControls
{
    public sealed class MotionControlSettings
    {
        public readonly BepInEx.Configuration.ConfigEntry<bool> LeftHanded = new(false);
        public readonly BepInEx.Configuration.ConfigEntry<Vector3> StickPosition = new(new(.22f, -.36f, .32f));
        public readonly BepInEx.Configuration.ConfigEntry<Vector3> ThrottlePosition = new(new(-.24f, -.36f, .32f));
    }
    public sealed class MotionFlightController
    {
        public static MotionFlightController Instance = new();
        public int Resets, PlacementCancels;
        public void ResetGrips() => Resets++;
        public void CancelPlacement() => PlacementCancels++;
        public static bool UsesLiftCollective(Aircraft aircraft) => aircraft.Collective;
    }
    public sealed class ControllerActions
    {
        public static ControllerActions Instance = new();
        public int Resets;
        public void ResetButtons() => Resets++;
    }
    public static class TrackedControllers
    {
        public static bool Available = true;
        public static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
    public sealed class CockpitLayout
    {
        public static CockpitLayout? Instance = new();
        public static Vector3? StickPreview => Instance?.Active == true ? Instance.Stick : null;
        public static Vector3? ThrottlePreview => Instance?.Active == true ? Instance.Throttle : null;
        public bool Active, RejectBegin;
        public Vector3 Stick, Throttle;
        private Aircraft? _aircraft;
        public bool CanSaveUnified => Active && _aircraft == GameManager.Current;
        public void Finish(bool save) => FinishUnified(save);
        public bool BeginUnified()
        {
            if (RejectBegin)
                return false;
            Stick = CockpitProfiles.StickPosition;
            Throttle = CockpitProfiles.ThrottlePosition;
            _aircraft = GameManager.Current;
            Active = true;
            return true;
        }
        public void FinishUnified(bool save)
        {
            if (Active)
                Effects.Events.Add("finish-handles");
            Active = false;
        }
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    public static class CockpitStatusSources
    {
        public static void ReleaseAll() => Effects.Events.Add("release-status");
    }
    public static class CockpitMapWidget
    {
        public static void ReleaseAll() => Effects.Events.Add("release-map");
    }
    public static class CockpitAlerts
    {
        public static bool LostCockpit(Aircraft? aircraft) => aircraft?.Lost == true;
        public static void ReleaseWidgetParents() => Effects.Events.Add("release-alerts");
    }
    internal sealed partial class CockpitHudWidgets : MonoBehaviour
    {
        internal static CockpitHudWidgets? Instance
        {
            get; private set;
        }
        private sealed class Contact
        {
            internal readonly ControllerButtonGate Gate = new();
            internal string? Id;
            internal int Corner = -1;
        }
        internal sealed class Appearance(string id)
        {
            internal bool SliderHeld, Disposed;
            internal void ReleaseHands() => SliderHeld = false;
            internal void Dispose()
            {
                Disposed = true;
                Effects.Events.Add("dispose:" + id);
            }
        }
        private sealed class Widget(string id)
        {
            internal readonly RectTransform Root = new(id);
            internal readonly Appearance Appearance = new(id);
        }
        private readonly Dictionary<string, Widget> _widgets = new();
        private readonly Contact[] _hands = { new(), new() };
        private readonly string?[] _hover = new string?[2];
        private readonly Quaternion[] _handRotations = new Quaternion[2];
        private readonly bool[] _appearanceBusy = new bool[2];
        private string? _menuAppearanceFocus, _resizing;
        internal bool ReadyBoundary = true;
        internal CockpitHudWidgets()
        {
            Instance = this;
            _profiles = new HudWidgetProfiles(ModConfiguration.Instance.Config);
            Application.onBeforeRender += Place;
        }
        private bool Ready() => ReadyBoundary;
        private void Place() => Effects.Events.Add("draw:" + _editing);
        private void CreateWidget(string id)
        {
            _widgets.Add(id, new Widget(id));
            Effects.Events.Add("create:" + id);
        }
        internal void RefreshForCheck() => RefreshContext();
        internal bool InputReadyForCheck() => CanUpdateEdit();
        internal void DisableForCheck() => OnDisable();
        internal HudWidgetPose PoseForCheck(string id) => _poses[id];
        internal void PoseFromGesture(string id, HudWidgetPose pose) => _poses[id] = pose;
        internal int WidgetCount => _widgets.Count;
        internal Appearance AppearanceForCheck(string id) => _widgets[id].Appearance;
        internal bool ContactsClear => _hands.All(h => h.Id == null && h.Corner == -1 && !h.Gate.Held) &&
            _hover.All(h => h == null) && _resizing == null && _handRotations.All(q => q == Quaternion.identity) && !_appearanceBusy.Any(v => v) &&
            _widgets.Values.All(w => !w.Appearance.SliderHeld);
        internal void HoldForCheck(int handCount, bool opacity = false)
        {
            for (var side = 0; side < handCount; side++)
            {
                _hands[side].Id = _hover[side] = "map";
                _hands[side].Corner = side == 0 ? 0 : 3;
                _hands[side].Gate.Update(true, 0);
                _hands[side].Gate.Update(true, 1);
                _handRotations[side] = new Quaternion(0, 1, 0, 0);
                _appearanceBusy[side] = opacity;
            }
            _resizing = handCount == 2 ? "map" : null;
            _widgets["map"].Appearance.SliderHeld = opacity;
        }
        internal bool FocusCleared => _menuAppearanceFocus == null;
        internal bool ContinueGripForCheck(int side)
        {
            _hands[side].Gate.Update(true, 1);
            return _hands[side].Gate.Held;
        }
    }
}
