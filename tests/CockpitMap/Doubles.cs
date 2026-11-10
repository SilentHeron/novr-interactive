// Bounded Unity hierarchy/lifecycle doubles; run the actual map adapter.
// These checks do not simulate rendering, native game events or a VR headset.
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        internal bool Dead;
        public static bool operator ==(Object? a, Object? b) =>
            ReferenceEquals(a, b) || (a is null || a.Dead) && (b is null || b.Dead);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object? value)
        {
            if (value == null)
                return;
            if (value is GameObject go)
            {
                foreach (var child in go.transform.Children.ToArray())
                    Destroy(child.gameObject);
                foreach (var component in go.Components)
                    component.Dead = true;
                go.transform.SetParent(null, false);
            }
            value!.Dead = true;
        }
    }
    public class Component : Object
    {
        internal int ChildrenQueries;
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : Component
        {
            ChildrenQueries++;
            void Visit(Transform root)
            {
                if (!includeInactive && !root.gameObject.activeInHierarchy)
                    return;
                results.AddRange(root.gameObject.Components.OfType<T>().Where(x => !x.Dead));
                foreach (var child in root.Children)
                    Visit(child);
            }
            Visit(transform);
        }
    }
    public class MonoBehaviour : Component
    {
    }
    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public readonly RectTransform transform;
        public string name;
        // Reads are counted: on the real map each one is an engine call per icon.
        internal int LayerReads;
        private int _layer;
        public int layer
        {
            get
            {
                LayerReads++;
                return _layer;
            }
            set => _layer = value;
        }
        public bool activeSelf = true, FailNextCanvasGroup;
        public bool activeInHierarchy => !Dead && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name, params Type[] components)
        {
            this.name = name;
            transform = new RectTransform { gameObject = this };
            Components.Add(transform);
            foreach (var type in components)
                if (type != typeof(RectTransform))
                {
                    var component = (Component)Activator.CreateInstance(type)!;
                    component.gameObject = this;
                    Components.Add(component);
                }
        }
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault(x => !x.Dead)!;
        public T AddComponent<T>() where T : Component, new()
        {
            if (typeof(T) == typeof(CanvasGroup) && FailNextCanvasGroup)
            {
                FailNextCanvasGroup = false;
                throw new InvalidOperationException("synthetic attach failure");
            }
            var component = new T { gameObject = this };
            Components.Add(component);
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Transform : Component
    {
        internal readonly List<Transform> Children = new();
        public Transform parent = null!;
        private Vector3 _localPosition, _localScale = Vector3.one;
        public int PositionWrites, ScaleWrites;
        public Vector3 localPosition
        {
            get => _localPosition; set
            {
                _localPosition = value;
                PositionWrites++;
            }
        }
        public Vector3 localScale
        {
            get => _localScale; set
            {
                _localScale = value;
                ScaleWrites++;
            }
        }
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 position
        {
            get => TransformPoint(Vector3.zero); set => localPosition = (parent == null ? value : parent.InverseTransformPoint(value)) - Offset;
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
        public Vector3 forward => rotation * new Vector3(0, 0, 1);
        public int childCount => Children.Count;
        public Transform? Find(string name) => Children.FirstOrDefault(c => c.gameObject.name == name);
        public Transform GetChild(int index) => Children[index];
        public void SetParent(Transform? next, bool worldPositionStays)
        {
            parent?.Children.Remove(this);
            parent = next!;
            next?.Children.Add(this);
        }
        public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
        public void SetAsLastSibling()
        {
            if (parent != null)
                SetSiblingIndex(parent.Children.Count - 1);
        }
        public void SetSiblingIndex(int index)
        {
            if (parent == null)
                return;
            parent.Children.Remove(this);
            parent.Children.Insert(Math.Clamp(index, 0, parent.Children.Count), this);
        }
        public bool IsChildOf(Transform ancestor)
        {
            for (var node = parent; node != null; node = node.parent)
                if (node == ancestor)
                    return true;
            return false;
        }
        private Vector3 Offset => this is RectTransform rect && parent is RectTransform p
            ? (Vector3)(p.rect.min + p.rect.size * rect.anchorMin) : Vector3.zero;
        public Vector3 TransformPoint(Vector3 point)
        {
            var result = localPosition + Offset + localRotation * (point * localScale);
            return parent == null ? result : parent.TransformPoint(result);
        }
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            if (parent != null)
                point = parent.InverseTransformPoint(point);
            return (Quaternion.Inverse(localRotation) * (point - localPosition - Offset)) / localScale;
        }
        public Vector3 TransformVector(Vector3 vector)
        {
            var result = localRotation * (vector * localScale);
            return parent == null ? result : parent.TransformVector(result);
        }
        public Vector3 InverseTransformVector(Vector3 vector)
        {
            if (parent != null)
                vector = parent.InverseTransformVector(vector);
            return (Quaternion.Inverse(localRotation) * vector) / localScale;
        }
    }
    public sealed class RectTransform : Transform
    {
        public Vector2 anchorMin = new(.5f, .5f), anchorMax = new(.5f, .5f), pivot = new(.5f, .5f);
        private Vector2 _sizeDelta = new(100, 100);
        public int SizeWrites;
        public Vector2 sizeDelta
        {
            get => _sizeDelta; set
            {
                _sizeDelta = value;
                SizeWrites++;
            }
        }
        public Rect rect => new(-pivot * sizeDelta, sizeDelta);
        public Vector2 anchoredPosition
        {
            get => new(localPosition.x, localPosition.y); set => localPosition = new(value.x, value.y, localPosition.z);
        }
        public void GetWorldCorners(Vector3[] corners)
        {
            var min = rect.min;
            var max = min + rect.size;
            corners[0] = TransformPoint(new Vector3(min.x, min.y, 0));
            corners[1] = TransformPoint(new Vector3(min.x, max.y, 0));
            corners[2] = TransformPoint(new Vector3(max.x, max.y, 0));
            corners[3] = TransformPoint(new Vector3(max.x, min.y, 0));
        }
    }
    public sealed class CanvasGroup : Component
    {
        public float alpha = 1; public bool interactable = true, blocksRaycasts = true;
    }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => new(0, 0);
        public static Vector2 one => new(1, 1);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float b) => new(a.x * b, a.y * b);
        public static Vector2 operator /(Vector2 a, float b) => new(a.x / b, a.y / b);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static implicit operator Vector3(Vector2 a) => new(a.x, a.y, 0);
    }
    public record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 forward => new(0, 0, 1);
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static explicit operator Vector2(Vector3 a) => new(a.x, a.y);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 operator /(Vector3 a, Vector3 b) => new(a.x / b.x, a.y / b.y, a.z / b.z);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
        private System.Numerics.Quaternion N => new(x, y, z, w);
        private static Quaternion From(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.N * b.N);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            var p = System.Numerics.Vector3.Transform(new(v.x, v.y, v.z), q.N);
            return new(p.X, p.Y, p.Z);
        }
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.N));
        public static Quaternion Euler(float x, float y, float z) => From(System.Numerics.Quaternion.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180));
        public static Quaternion LookRotation(Vector3 forward, Vector3 up) => Euler(0, MathF.Atan2(forward.x, forward.z) * 180 / MathF.PI, 0);
        public Vector3 eulerAngles
        {
            get
            {
                var f = this * new Vector3(0, 0, 1);
                return new(0, MathF.Atan2(f.x, f.z) * 180 / MathF.PI, 0);
            }
        }
    }
    public readonly record struct Rect(Vector2 min, Vector2 size)
    {
        public Vector2 center => min + size * new Vector2(.5f, .5f); public float width => size.x; public float height => size.y;
        public float xMin => min.x; public float yMin => min.y; public float xMax => min.x + size.x; public float yMax => min.y + size.y;
        public bool Contains(Vector2 p) => p.x >= xMin && p.y >= yMin && p.x < xMax && p.y < yMax;
    }
    public struct Bounds(Vector3 center, Vector3 size)
    {
        private Vector3 _min = center - size * .5f, _max = center + size * .5f;
        public readonly Vector3 center => (_min + _max) * .5f;
        public readonly Vector3 size => _max - _min;
        public void Encapsulate(Vector3 p)
        {
            _min = new(Math.Min(_min.x, p.x), Math.Min(_min.y, p.y), Math.Min(_min.z, p.z));
            _max = new(Math.Max(_max.x, p.x), Math.Max(_max.y, p.y), Math.Max(_max.z, p.z));
        }
    }
    public static class Mathf
    {
        public static float Abs(float value) => Math.Abs(value);
        public static float Min(float a, float b) => Math.Min(a, b); public static float Clamp01(float v) => Math.Clamp(v, 0, 1);
        public static float Clamp(float v, float min, float max) => Math.Clamp(v, min, max);
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < .00001f;
    }
    public sealed class Camera : MonoBehaviour
    {
    }
    public sealed class Material : Object
    {
    }
    public sealed class Sprite : Object
    {
    }
    public readonly record struct Color(float r, float g, float b, float a)
    {
        public static Color white => new(1, 1, 1, 1); public static Color Lerp(Color x, Color y, float t) => new(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
    }
    public static class Time
    {
        public static float unscaledTime, unscaledDeltaTime = .02f, timeScale = 1;
    }
    public static class Cursor
    {
        public static bool visible;
    }
    public sealed class CanvasRenderer : Component
    {
    }
    public static class Debug
    {
        public static int Warnings; public static void LogWarning(string message) => Warnings++;
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order => order;
    }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component
    {
        private bool _enabled = true;
        public int EnabledWrites;
        public bool enabled
        {
            get => _enabled; set
            {
                _enabled = value;
                EnabledWrites++;
            }
        }
        public bool raycastTarget = true;
    }
    public sealed class Image : Graphic
    {
        public UnityEngine.Material material = null!; public UnityEngine.Color color; public UnityEngine.Sprite? sprite; public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
    }
    public sealed class Text : Graphic
    {
        public string text = "";
    }
}
namespace TMPro
{
    public sealed class TMP_Text : UnityEngine.UI.Graphic
    {
        public string text = "";
    }
}
namespace UnityEngine.XR
{
    public static class XRSettings
    {
        public static bool enabled = true;
    }
}
public class Unit : UnityEngine.Object
{
    public readonly UnityEngine.Transform transform = new UnityEngine.GameObject("unit").transform;
}
public sealed class Aircraft : Unit
{
    public bool disabled; public WeaponManager? weaponManager = new();
}
public sealed class WeaponManager
{
    public readonly List<Unit> Targets = new(); public List<Unit> GetTargetList() => Targets;
}
public sealed class FlightHud : UnityEngine.MonoBehaviour
{
}
public static class SceneSingleton<T>
{
    public static T? i;
}
public static class GameManager
{
    public static bool flightControlsEnabled = true;
    public static ControlMapper? controlMapper;
    public static Aircraft? Aircraft;
    public static bool GetLocalAircraft(out Aircraft aircraft)
    {
        aircraft = Aircraft!;
        return aircraft != null;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal static class CockpitHudWidgets
    {
        internal static Aircraft? CurrentAircraft;
        internal static bool Editing, Visible = true;
        internal static readonly Dictionary<string, UnityEngine.RectTransform> Slots = new();
        internal static readonly Dictionary<string, bool> Content = new();
        internal static bool TryGetContent(string id, out UnityEngine.RectTransform content) => Slots.TryGetValue(id, out content!);
        internal static void SetHasContent(string id, bool visible) => Content[id] = visible;
        internal static bool IsVisible(string id) => Visible;
    }
}

public sealed class DynamicMap : UnityEngine.MonoBehaviour
{
    public static bool mapMaximized;
    public UnityEngine.GameObject mapImage = null!;
    public UnityEngine.GameObject viewIndicator = null!;
    public UnityEngine.UI.Image mapBackground = null!;
    public UnityEngine.Transform mapScaleCenter = null!, mapScaleProxy = null!;
    public float mapDisplayFactor = 1;
    public List<MapIcon> mapIcons = new(), selectedIcons = new();
    public int Changes;
    public float GetZoomLevel() => mapScaleCenter.localScale.x;
    public void SetZoomLevel(float zoom)
    {
        // Model the narrow production Harmony ownership hook; the native mouse
        // anchoring body must never run while the floating map is borrowed.
        if (!NOVR.VrUi.SpecialBehavior.CockpitMapNavigation.TrySetZoom(this, zoom))
            throw new Exception("Unowned desktop zoom");
        Changes++;
    }
    public void Maximize()
    {
    }
}
public class MapIcon : UnityEngine.MonoBehaviour
{
    public UnityEngine.UI.Image iconImage = null!;
}
public sealed class UnitMapIcon : MapIcon
{
    public Unit unit = null!;
}
public sealed class CombatHUD
{
    public Aircraft? aircraft;
    public HashSet<Unit> Markers = new();
    public List<Unit> Selected = new();
    public bool RefuseSelection;
    public bool MarkerExists(Unit unit) => Markers.Contains(unit);
    public void SelectUnit(Unit unit)
    {
        if (RefuseSelection)
            return;
        Selected.Add(unit);
        aircraft!.weaponManager!.Targets.Add(unit);
    }
}
public sealed class TargetListSelector
{
    public HashSet<Unit> Excluded = new();
    public bool CheckExclusions(Unit unit) => Excluded.Contains(unit);
}
public sealed class CameraStateManager : UnityEngine.MonoBehaviour
{
    public static CameraMode cameraMode = CameraMode.cockpit;
}
namespace NOVR
{
    internal static class APIBus
    {
        internal static UnityEngine.Camera? MainCamera, CockpitHudCamera;
    }
}
public static class GlobalPositions
{
    public static UnityEngine.Vector3 Origin;
    public static UnityEngine.Vector3 ToGlobalPosition(this UnityEngine.Vector3 p) => p + Origin;
    public static UnityEngine.Vector3 GlobalPosition(this Unit unit) => unit.transform.position.ToGlobalPosition();
    public static UnityEngine.Vector3 AsVector3(this UnityEngine.Vector3 p) => p;
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch(Type type, string method) : Attribute
    {
        public Type Type => type; public string Method => method;
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute
    {
    }
}
