#pragma warning disable CS0649
namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object value)
        {
        }
    }
    public class MonoBehaviour : Component
    {
    }
    public class Component : Object
    {
        public GameObject gameObject = null!; public Transform transform => gameObject.transform;
    }
    public class GameObject : Object
    {
        public static readonly List<GameObject> All = new();
        public readonly string name; public bool activeSelf = true; public Transform transform;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        private readonly Dictionary<Type, Component> _components = new();
        public GameObject(string name, params Type[] types)
        {
            this.name = name;
            transform = new RectTransform { gameObject = this };
            _components[typeof(RectTransform)] = transform;
            All.Add(this);
            foreach (var type in types.Where(t => t != typeof(RectTransform)))
            {
                var item = (Component)Activator.CreateInstance(type)!;
                item.gameObject = this;
                _components[type] = item;
            }
        }
        public T GetComponent<T>() => (T)(object)_components[typeof(T)];
        public void SetActive(bool active) => activeSelf = active;
        public bool Has<T>() => _components.ContainsKey(typeof(T));
    }
    public class Transform : Component
    {
        public Transform parent = null!; public readonly List<Transform> children = new();
        public void SetParent(Transform value, bool _)
        {
            parent?.children.Remove(this);
            parent = value;
            value.children.Add(this);
        }
        public T GetComponent<T>() => gameObject.GetComponent<T>();
    }
    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition, sizeDelta; public Rect rect => new(sizeDelta.x, sizeDelta.y);
    }
    public readonly record struct Rect(float x, float y, float width, float height)
    {
        public Rect(float width, float height) : this(0, 0, width, height) { }
    }
    public class Texture : Object
    {
        public string name = "";
    }
    public class Texture2D : Texture
    {
        public int width, height; public FilterMode filterMode; public TextureWrapMode wrapMode; public Texture2D(int width, int height, TextureFormat format, bool mipmap)
        {
            this.width = width;
            this.height = height;
        }
    }
    public enum TextureFormat
    {
        RGBA32
    }
    public enum FilterMode
    {
        Bilinear
    }
    public enum TextureWrapMode
    {
        Clamp
    }
    public static class ImageConversion
    {
        public static bool LoadImage(Texture2D texture, byte[] data, bool markNonReadable)
        {
            if (data.Length < 24 || data[0] != 137 || data[1] != 80)
                return false;
            texture.width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(16, 4));
            texture.height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(20, 4));
            return true;
        }
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new(); public static void LogWarning(string message) => Warnings.Add(message);
    }
    public class CanvasRenderer : Component
    {
    }
    public class Font : Object
    {
    }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => default;
        public Vector2 normalized
        {
            get
            {
                var length = MathF.Sqrt(x * x + y * y);
                return length > 0 ? new(x / length, y / length) : zero;
            }
        }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float b) => new(a.x * b, a.y * b);
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => default;
    }
    public readonly record struct Color(float r, float g, float b, float a = 1)
    {
        public static Color white => new(1, 1, 1);
    }
    public enum TextAnchor
    {
        MiddleCenter
    }
    public static class Mathf
    {
        public static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
    }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component
    {
        public bool enabled = true; public bool raycastTarget; public UnityEngine.Color color; public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
    }
    public class MaskableGraphic : Graphic
    {
        protected virtual void Awake()
        {
        }
        protected virtual void OnPopulateMesh(VertexHelper vh)
        {
        }
        protected void SetVerticesDirty()
        {
        }
    }
    public class Image : Graphic
    {
    }
    public class RawImage : Graphic
    {
        public UnityEngine.Texture? texture; public UnityEngine.Rect uvRect;
    }
    public class Text : Graphic
    {
        public UnityEngine.Font font = null!; public int fontSize; public string text = ""; public UnityEngine.TextAnchor alignment;
    }
    public class Button : UnityEngine.Component
    {
        public Graphic targetGraphic = null!; public Navigation navigation; public bool interactable = true;
        public readonly Clicked onClick = new();
        public class Clicked
        {
            private Action? _action; public void AddListener(Action action) => _action += action; public void Invoke() => _action?.Invoke();
        }
    }
    public struct Navigation
    {
        public Mode mode; public enum Mode
        {
            None
        }
    }
    public class VertexHelper
    {
        public int currentVertCount; public void Clear() => currentVertCount = 0;
        public void AddVert(UnityEngine.Vector3 point, UnityEngine.Color color, UnityEngine.Vector2 uv) => currentVertCount++;
        public void AddVert(UnityEngine.Vector2 point, UnityEngine.Color color, UnityEngine.Vector2 uv) => currentVertCount++;
        public void AddTriangle(int a, int b, int c)
        {
        }
    }
}
namespace NOVR
{
    internal static class LayerHelper
    {
        internal static void SetLayerRecursive(UnityEngine.Transform _, int layer)
        {
        }
        internal static int GetVrUiLayer() => 0;
    }
}
namespace NOVR.VrUi.Native
{
    internal static class NativeButtonFeedback
    {
        internal static void Configure(UnityEngine.UI.Button button, UnityEngine.Color color)
        {
            button.targetGraphic.color = color;
        }
        internal static void SetNormalColor(UnityEngine.UI.Button button, UnityEngine.Color color)
        {
            button.targetGraphic.color = color;
        }
    }
}
namespace NOVR.MotionControls
{
    internal static class TrackedControllers
    {
        internal static ControllerHardwareProfile Left, Right; internal static ControllerHardwareProfile Profile(bool left) => left ? Left : Right; internal static ControllerSample LeftSample, RightSample; internal static ControllerSample Read(bool left) => left ? LeftSample : RightSample;
    }
    internal struct ControllerSample
    {
        internal bool Valid, NativeFrame, Primary, Secondary, Top, Outside, StickClick, Menu, Shoulder, PrimaryTouched, SecondaryTouched, TopTouched, OutsideTouched, ThumbstickTouched, TriggerTouched, GripTouched; internal float Trigger, Grip;
    }
    internal sealed class ControllerActions
    {
        internal static ControllerActions? Instance; internal void ResetButtons()
        {
        }
    }
    internal sealed class ModConfiguration
    {
        internal static readonly ModConfiguration Instance = new(); internal readonly MotionControlsConfig MotionControls = new(); internal ControllerBindings ControllerBindings = null!;
    }
    internal sealed class MotionControlsConfig
    {
        internal readonly ValueSetting<bool> LeftHanded = new();
    }
    internal sealed class ValueSetting<T>
    {
        internal T Value = default!;
    }
}
