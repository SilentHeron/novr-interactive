// Lifecycle/ownership checks, not a Unity rendering emulator. In particular,
// reject duplicate CanvasGroups as the live player does; opacity must restore.
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static void Destroy(Object value)
        {
            value.Destroyed = true;
            if (value is Component component)
                component.gameObject.Components.Remove(component);
            if (value is GameObject go)
                go.transform.SetParent(null, false);
        }
    }
    public class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public readonly string name;
        public int layer;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent?.gameObject.activeInHierarchy ?? true);
        public void SetActive(bool value) => activeSelf = value;
        public Transform transform
        {
            get;
        }
        public GameObject(string name, params Type[] types)
        {
            this.name = name;
            transform = types.Contains(typeof(RectTransform)) ? new RectTransform() : new Transform();
            transform.gameObject = this;
            Components.Add(transform);
            foreach (var type in types)
                if (type != typeof(RectTransform))
                    Add(type);
        }
        private Component Add(Type type)
        {
            if (type == typeof(CanvasGroup) && GetComponent<CanvasGroup>() != null)
                throw new InvalidOperationException("Unity rejects duplicate CanvasGroups");
            var component = (Component)Activator.CreateInstance(type)!;
            component.gameObject = this;
            Components.Add(component);
            type.GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
            return component;
        }
        public T AddComponent<T>() where T : Component, new() => (T)Add(typeof(T));
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault()!;
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component => GetComponentsInChildren<T>(includeInactive).FirstOrDefault()!;
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component =>
            Components.OfType<T>().Concat(transform.Children.SelectMany(t => t.gameObject.GetComponentsInChildren<T>(includeInactive))).ToArray();
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component => gameObject.GetComponentInChildren<T>(includeInactive);
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component => gameObject.GetComponentsInChildren<T>(includeInactive);
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : Component =>
            results.AddRange(gameObject.GetComponentsInChildren<T>(includeInactive));
    }
    public class MonoBehaviour : Component
    {
        private bool _enabled = true;
        public int EnableTransitions, DisableTransitions;
        public bool enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;
                _enabled = value;
                if (value)
                    EnableTransitions++;
                else
                    DisableTransitions++;
            }
        }
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
    public class Transform : Component
    {
        internal readonly List<Transform> Children = new();
        public Transform? parent;
        public Vector3 localPosition, position, localScale = Vector3.one;
        public Quaternion localRotation;
        public int childCount => Children.Count;
        public void SetParent(Transform? value, bool preserveWorld)
        {
            parent?.Children.Remove(this);
            parent = value;
            parent?.Children.Add(this);
        }
        public void SetAsFirstSibling()
        {
            parent?.Children.Remove(this);
            parent?.Children.Insert(0, this);
        }
        public bool IsChildOf(Transform other)
        {
            for (var node = parent; node != null; node = node.parent)
                if (node == other)
                    return true;
            return false;
        }
        public Vector3 TransformPoint(Vector3 value) => value + position;
        public Vector3 InverseTransformDirection(Vector3 value) => value;
    }
    public class RectTransform : Transform
    {
        public Vector2 sizeDelta, anchorMin, anchorMax, offsetMin, offsetMax, anchoredPosition;
        public Rect rect => new() { xMax = 0, yMax = 0 };
    }
    public struct Rect
    {
        public float xMax, yMax;
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
        public static Vector2 zero => default;
        public static Vector2 one => new(1, 1);
        public static Vector2 operator *(Vector2 a, float b) => new(a.x * b, a.y * b);
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public static Vector3 one => new(1, 1, 1);
        public static Vector3 zero => default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public struct Quaternion
    {
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
    }
    public class CanvasGroup : Component
    {
        private float _alpha = 1;
        private bool _blocksRaycasts = true, _interactable = true;
        public int AlphaWrites, RaycastWrites, InteractionWrites;
        public float alpha
        {
            get => _alpha; set
            {
                _alpha = value;
                AlphaWrites++;
            }
        }
        public bool blocksRaycasts
        {
            get => _blocksRaycasts; set
            {
                _blocksRaycasts = value;
                RaycastWrites++;
            }
        }
        public bool interactable
        {
            get => _interactable; set
            {
                _interactable = value;
                InteractionWrites++;
            }
        }
    }
    public class Canvas : MonoBehaviour
    {
        public static event Action? willRenderCanvases;
        public static void Render() => willRenderCanvases?.Invoke();
    }
    public class CanvasRenderer : Component
    {
        public bool cull;
    }
    public class Camera : Component
    {
    }
    public class Font : Object
    {
    }
    public enum TextAnchor
    {
        MiddleRight
    }
    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : new() => new();
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => MathF.Max(a, b); public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
    }
    public static class Time
    {
        public static float unscaledTime, timeSinceLevelLoad;
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder : Attribute
    {
        public DefaultExecutionOrder(int order)
        {
        }
    }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Color color;
        public bool raycastTarget;
        public UnityEngine.CanvasRenderer canvasRenderer => GetComponent<UnityEngine.CanvasRenderer>();
        public void SetVerticesDirty()
        {
        }
        public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
    }
    public class Image : Graphic
    {
    }
    public class Text : Graphic
    {
        private string _text = "";
        public int TextDirties;
        public string text
        {
            get => _text; set
            {
                if (_text == value)
                    return;
                _text = value;
                TextDirties++;
            }
        }
        public UnityEngine.Font? font;
        public int fontSize;
        public UnityEngine.TextAnchor alignment;
    }
}
public class ThreatList : UnityEngine.Component
{
    public readonly Dictionary<int, ThreatItem> Items = new();
}
public class DynamicMap : UnityEngine.Component
{
}
public class FlightHud : UnityEngine.Component
{
}
namespace NOVR
{
    public sealed class Setting<T>(T value)
    {
        public T ValueField = value; public T Value => ValueField;
    }
    public sealed class ModConfiguration
    {
        public static ModConfiguration Instance { get; } = new();
        public Setting<float> HudPanelDistance = new(3), HudPanelScale = new(1);
        public Setting<bool> HudContrast = new(true), HudDeclutter = new(true);
    }
    public static class APIBus
    {
        public static UnityEngine.Camera CockpitHudCamera = new UnityEngine.GameObject("Camera").AddComponent<UnityEngine.Camera>();
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal static class CockpitHudWidgets
    {
        internal static bool MapOwned; internal static bool Owns(string id) => id == "map" && MapOwned;
    }
    internal static class CockpitStatusSources
    {
        internal static readonly HashSet<UnityEngine.Transform> Empty = new();
        internal static bool EmptiedStatusPanel(UnityEngine.Transform panel) => Empty.Contains(panel);
    }
}
