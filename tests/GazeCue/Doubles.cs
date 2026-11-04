using BepInEx.Configuration;
using NOVR.MotionControls;
namespace NOVR
{
    public class ModConfiguration
    {
        public static ModConfiguration Instance = null!;
        internal readonly GazeCueSettings LookCue;
        public readonly ConfigEntry<float> InstrumentBrightness;
        public ModConfiguration(ConfigFile config)
        {
            Instance = this;
            LookCue = new(config);
            InstrumentBrightness = config.Bind("VR HUD", "Instrument Brightness", 1f);
        }
    }
}
namespace UnityEngine
{
    public class Component
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
    }
    public class Transform : Component
    {
        public Transform? parent;
        public Vector3 position;
        public void SetParent(Transform value, bool worldPositionStays) => parent = value;
    }
    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition, sizeDelta;
        public Rect rect => new(sizeDelta.x, sizeDelta.y);
    }
    public class GameObject
    {
        private readonly List<Component> _components = new();
        public Transform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public void SetActive(bool value) => activeSelf = value;
        public GameObject(string name, params Type[] components)
        {
            transform = new RectTransform { gameObject = this };
            _components.Add(transform);
        }
        public T AddComponent<T>() where T : Component, new()
        {
            var component = new T { gameObject = this };
            _components.Add(component);
            return component;
        }
        public T GetComponent<T>() where T : Component => _components.OfType<T>().First();
    }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 one => new(1, 1);
        public static Vector2 zero => new(0, 0);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator *(Vector2 a, float v) => new(a.x * v, a.y * v);
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static float SqrMagnitude(Vector3 value) => value.x * value.x + value.y * value.y + value.z * value.z;
    }
    public static class Time
    {
        public static float unscaledTime;
    }
    public class CanvasRenderer
    {
        public float Alpha = 1;
        public float GetInheritedAlpha() => Alpha;
    }
    public readonly record struct Rect(float width, float height)
    {
        public Vector2 center => Vector2.zero;
    }
    public record struct Color(float r, float g, float b, float a = 1);
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Cos(float a) => MathF.Cos(a);
        public static float Sin(float a) => MathF.Sin(a);
    }
}
namespace UnityEngine.UI
{
    public class Image : MaskableGraphic
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
        public readonly UnityEngine.CanvasRenderer canvasRenderer = new();
    }
    public class MaskableGraphic : UnityEngine.Component
    {
        public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
        public UnityEngine.Color color;
        public bool raycastTarget = true;
        public int DirtyCount;
        protected void SetVerticesDirty() => DirtyCount++;
        protected virtual void OnPopulateMesh(VertexHelper helper)
        {
        }
        public VertexHelper Mesh()
        {
            var helper = new VertexHelper();
            OnPopulateMesh(helper);
            return helper;
        }
    }
    public class VertexHelper
    {
        public readonly List<(UnityEngine.Vector2 p, UnityEngine.Color color)> Vertices = new();
        public readonly List<(int a, int b, int c)> Triangles = new();
        public int currentVertCount => Vertices.Count;
        public void Clear()
        {
            Vertices.Clear();
            Triangles.Clear();
        }
        public void AddVert(UnityEngine.Vector2 position, UnityEngine.Color color, UnityEngine.Vector2 uv) => Vertices.Add((position, color));
        public void AddTriangle(int a, int b, int c) => Triangles.Add((a, b, c));
    }
}
namespace NuclearOption.UIStyleSystem
{
    public class ColorTheme
    {
        public UnityEngine.Color AllClear = new(.1f, .8f, .3f);
    }
    public class ThemeManager
    {
        public static ThemeManager? Active;
        public ColorTheme ColorTheme = new();
    }
}
