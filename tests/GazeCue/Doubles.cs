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
        public Quaternion rotation = Quaternion.identity;
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 TransformDirection(Vector3 direction) => rotation * direction;
        public void SetParent(Transform value, bool worldPositionStays) => parent = value;
        public void SetPositionAndRotation(Vector3 value, Quaternion orientation)
        {
            position = value;
            rotation = orientation;
        }
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
        public static Vector3 zero => default;
        public static Vector3 forward => new(0, 0, 1);
        public float sqrMagnitude => SqrMagnitude(this);
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this / magnitude : zero;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
        public static Vector3 operator /(Vector3 a, float b) => new(a.x / b, a.y / b, a.z / b);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static float SqrMagnitude(Vector3 value) => value.x * value.x + value.y * value.y + value.z * value.z;
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
        public static Quaternion Euler(float pitch, float yaw, float roll)
        {
            var value = System.Numerics.Quaternion.CreateFromYawPitchRoll(yaw * MathF.PI / 180, pitch * MathF.PI / 180, roll * MathF.PI / 180);
            return new(value.X, value.Y, value.Z, value.W);
        }
        public static Vector3 operator *(Quaternion rotation, Vector3 point)
        {
            var value = System.Numerics.Vector3.Transform(new(point.x, point.y, point.z), new System.Numerics.Quaternion(rotation.x, rotation.y, rotation.z, rotation.w));
            return new(value.X, value.Y, value.Z);
        }
    }
    public class Camera : Component
    {
    }
    public class MonoBehaviour : Component
    {
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order => order;
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
        public static float Sqrt(float value) => MathF.Sqrt(value);
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
