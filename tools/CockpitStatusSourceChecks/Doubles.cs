// Bounded Unity hierarchy/lifecycle doubles; run the actual source adapter.
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
            if (value == null) return;
            if (value is GameObject go)
            {
                foreach (var child in go.transform.Children.ToArray()) Destroy(child.gameObject);
                foreach (var component in go.Components) component.Dead = true;
                go.transform.SetParent(null, false);
            }
            value!.Dead = true;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public string name => gameObject.name;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : Component
        {
            void Visit(Transform root)
            {
                if (!includeInactive && !root.gameObject.activeInHierarchy) return;
                results.AddRange(root.gameObject.Components.OfType<T>().Where(x => !x.Dead));
                foreach (var child in root.Children) Visit(child);
            }
            Visit(transform);
        }
    }
    public class MonoBehaviour : Component { }
    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public readonly RectTransform transform;
        public string name;
        public int layer;
        public bool activeSelf = true, FailNextCanvasGroup;
        public bool activeInHierarchy => !Dead && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name, params Type[] types)
        {
            this.name = name; transform = new RectTransform { gameObject = this }; Components.Add(transform);
            foreach (var type in types)
                if (type != typeof(RectTransform)) Add(type);
        }
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault(x => !x.Dead)!;
        public T AddComponent<T>() where T : Component, new() => (T)Add(typeof(T));
        private Component Add(Type type)
        {
            if (type == typeof(CanvasGroup) && GetComponent<CanvasGroup>() != null)
                throw new InvalidOperationException("Unity rejects duplicate CanvasGroups");
            if (type == typeof(CanvasGroup) && FailNextCanvasGroup)
            { FailNextCanvasGroup = false; throw new InvalidOperationException("synthetic attach failure"); }
            var component = (Component)Activator.CreateInstance(type)!; component.gameObject = this; Components.Add(component);
            if (component is UnityEngine.UI.Graphic && GetComponent<CanvasRenderer>() == null) AddComponent<CanvasRenderer>();
            type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }
        public void SetActive(bool active) => activeSelf = active;
    }
    public class Transform : Component
    {
        internal readonly List<Transform> Children = new();
        public Transform parent = null!;
        public Vector3 localPosition, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public int childCount => Children.Count;
        public Transform GetChild(int index) => Children[index];
        public Transform? Find(string name) => Children.FirstOrDefault(child => child.name == name);
        public void SetParent(Transform? next, bool worldPositionStays)
        { parent?.Children.Remove(this); parent = next!; next?.Children.Add(this); }
        public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
        public void SetSiblingIndex(int index)
        {
            if (parent == null) return;
            parent.Children.Remove(this); parent.Children.Insert(Math.Clamp(index, 0, parent.Children.Count), this);
        }
        public bool IsChildOf(Transform ancestor)
        { for (var node = parent; node != null; node = node.parent) if (node == ancestor) return true; return false; }
        private Vector3 Offset => this is RectTransform rect && parent is RectTransform p
            ? (Vector3)(p.rect.min + p.rect.size * rect.anchorMin) : Vector3.zero;
        public Vector3 TransformPoint(Vector3 point)
        {
            var result = localPosition + Offset + point * localScale;
            return parent == null ? result : parent.TransformPoint(result);
        }
        public Vector3 InverseTransformPoint(Vector3 point)
        {
            if (parent != null) point = parent.InverseTransformPoint(point);
            return (point - localPosition - Offset) / localScale;
        }
    }
    public sealed class RectTransform : Transform
    {
        public Vector2 anchoredPosition;
        public Vector2 anchorMin = new(.5f, .5f), anchorMax = new(.5f, .5f), pivot = new(.5f, .5f), sizeDelta = new(100, 100);
        public Rect rect => new(-pivot * sizeDelta, sizeDelta);
        public void GetWorldCorners(Vector3[] corners)
        {
            var min = rect.min; var max = min + rect.size;
            corners[0] = TransformPoint(new Vector3(min.x, min.y, 0));
            corners[1] = TransformPoint(new Vector3(min.x, max.y, 0));
            corners[2] = TransformPoint(new Vector3(max.x, max.y, 0));
            corners[3] = TransformPoint(new Vector3(max.x, min.y, 0));
        }
    }
    public sealed class CanvasGroup : Component
    { public float alpha = 1; public bool interactable = true, blocksRaycasts = true; }
    public sealed class CanvasRenderer : Component { public bool cull; }
    public sealed class Canvas : Component
    { public static event Action? willRenderCanvases; public static void Render() => willRenderCanvases?.Invoke(); }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a) => new(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new(a.x * b.x, a.y * b.y);
        public static implicit operator Vector3(Vector2 a) => new(a.x, a.y, 0);
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float f) => new(a.x * f, a.y * f, a.z * f);
        public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 operator /(Vector3 a, Vector3 b) => new(a.x / b.x, a.y / b.y, a.z / b.z);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    { public static Quaternion identity => new(0, 0, 0, 1); }
    public readonly record struct Rect(Vector2 min, Vector2 size)
    {
        public Vector2 center => min + size * new Vector2(.5f, .5f);
        public float width => size.x; public float height => size.y;
        public float xMax => min.x + size.x; public float yMax => min.y + size.y;
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
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a, b); }
    public readonly record struct Color(float r, float g, float b, float a);
    public sealed class Font : Object { }
    public enum TextAnchor { MiddleRight }
    public static class Resources { public static T GetBuiltinResource<T>(string name) where T : new() => new(); }
    public static class Time { public static float unscaledTime; }
    public static class Debug { public static int Warnings; public static void LogWarning(string message) => Warnings++; }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder(int order) : Attribute { public int Order => order; }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component
    {
        public bool enabled = true, raycastTarget = true;
        public UnityEngine.Color color;
        public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
        public UnityEngine.CanvasRenderer canvasRenderer => GetComponent<UnityEngine.CanvasRenderer>();
        public void SetVerticesDirty() { }
    }
    public sealed class Image : Graphic { }
    public sealed class Text : Graphic
    {
        public string text = "";
        public UnityEngine.Font? font;
        public int fontSize;
        public UnityEngine.TextAnchor alignment;
    }
}
namespace TMPro
{
    public class TMP_Text : UnityEngine.UI.Graphic
    {
        public string text = ""; public object? font, fontSharedMaterial; public int fontSize, maxVisibleLines;
        public bool richText, enableWordWrapping; public TextOverflowModes overflowMode; public TextAlignmentOptions alignment;
    }
    public sealed class TextMeshProUGUI : TMP_Text { }
    public enum TextOverflowModes { Ellipsis }
    public enum TextAlignmentOptions { TopLeft }
}
namespace HarmonyLib
{ public static class AccessTools { public static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!; } }
namespace UnityEngine.XR { public static class XRSettings { public static bool enabled = true; } }
public sealed class Aircraft : UnityEngine.Object { }
public sealed class FlightHud : UnityEngine.MonoBehaviour { public UnityEngine.Transform statusAnchor = null!; }
public sealed class StatusDisplay : UnityEngine.MonoBehaviour { public bool enabled = true; }
public sealed class AircraftActionsReport : UnityEngine.MonoBehaviour
{
    private TMPro.TextMeshProUGUI actionsText = null!; private UnityEngine.UI.Image background = null!;
    internal void SetFields(TMPro.TextMeshProUGUI text, UnityEngine.UI.Image image) { actionsText = text; background = image; }
    internal void NativeReport(string text, bool backplate) { actionsText.text = text; background.enabled = backplate; }
}
public sealed class AirbaseOverlay : UnityEngine.MonoBehaviour
{
    private TMPro.TextMeshProUGUI airbaseLabel = null!;
    internal void SetLabel(TMPro.TextMeshProUGUI text) => airbaseLabel = text;
    internal void NativeLabel(string text) => airbaseLabel.text = text;
}
public static class SceneSingleton<T> { public static T? i; }
public static class GameManager
{
    public static Aircraft? Aircraft;
    public static bool GetLocalAircraft(out Aircraft aircraft) { aircraft = Aircraft!; return aircraft != null; }
}
namespace NOVR.VrUi.SpecialBehavior
{
    // Legacy docking's Unity world transform is outside this bounded hierarchy
    // check; verify the adapter restores it before capturing the lease.
    internal sealed class NOVRStatusDisplayBehavior : UnityEngine.MonoBehaviour
    {
        internal int Prepares;
        internal void PrepareForWidget() { Prepares++; transform.localScale = UnityEngine.Vector3.one; }
    }
    internal sealed class CockpitHudWidgets
    {
        internal static CockpitHudWidgets? Instance = new();
        internal bool Available = true;
        internal static bool Visible = true;
        internal static bool IsVisible(string id) => Instance?.Available == true && Visible;
        internal static Aircraft? CurrentAircraft;
        internal static readonly Dictionary<string, UnityEngine.RectTransform> Slots = new();
        internal static readonly Dictionary<string, bool> Content = new();
        internal static bool TryGetContent(string id, out UnityEngine.RectTransform content) => Slots.TryGetValue(id, out content!);
        internal static void SetHasContent(string id, bool visible) => Content[id] = visible;
    }
    internal static class CockpitAlerts { internal static bool InRecovery; }
}
