// Unity/game boundaries only; projection, camera selection and ownership use
// production source. System.Numerics supplies the matrix operations.
using N = System.Numerics;
namespace UnityEngine
{
    internal class Object
    {
        internal static readonly List<Object> Created = new();
        internal bool Dead;
        internal string name = "";
        protected Object() => Created.Add(this);
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || (a is null || a.Dead) && (b is null || b.Dead);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        internal static void Destroy(Object? value)
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
    internal class Component : Object
    {
        internal GameObject gameObject = null!;
        internal Transform transform => gameObject.transform;
    }
    internal class MonoBehaviour : Component
    {
    }
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class DefaultExecutionOrder : Attribute
    {
        internal DefaultExecutionOrder(int order)
        {
        }
    }
    internal sealed class GameObject : Object
    {
        internal readonly Transform transform;
        internal readonly List<Component> Components = new();
        internal bool activeSelf = true;
        internal bool activeInHierarchy => !Dead && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        internal GameObject(string name, params Type[] types)
        {
            this.name = name;
            transform = types.Contains(typeof(RectTransform)) ? new RectTransform() : new Transform();
            transform.gameObject = this;
            Components.Add(transform);
            foreach (var type in types.Where(type => type != typeof(RectTransform)))
            {
                var component = (Component)Activator.CreateInstance(type)!;
                component.gameObject = this;
                Components.Add(component);
            }
        }
        internal void SetActive(bool value) => activeSelf = value;
        internal T GetComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault()!;
    }
    internal class Transform : Component
    {
        internal Transform? parent;
        internal readonly List<Transform> Children = new();
        internal Vector3 localPosition;
        internal Vector3 localScale = Vector3.one;
        internal Quaternion localRotation = Quaternion.identity;
        internal Matrix4x4 localToWorldMatrix => (parent?.localToWorldMatrix ?? Matrix4x4.identity) * Matrix4x4.TRS(localPosition, localRotation, localScale);
        internal Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;
        internal void SetParent(Transform? next, bool worldPositionStays)
        {
            parent?.Children.Remove(this);
            parent = next;
            next?.Children.Add(this);
        }
    }
    internal sealed class RectTransform : Transform
    {
        internal Vector2 pivot, sizeDelta;
    }
    internal readonly record struct Vector2(float x, float y);
    internal readonly record struct Vector3(float x, float y, float z)
    {
        internal static Vector3 zero => default;
        internal static Vector3 one => new(1, 1, 1);
        internal N.Vector3 Value => new(x, y, z);
    }
    internal struct Vector4(float x, float y, float z, float w)
    {
        internal float x = x, y = y, z = z, w = w;
    }
    internal readonly record struct Quaternion(float x, float y, float z, float w)
    {
        internal static Quaternion identity => new(0, 0, 0, 1);
        internal N.Quaternion Value => new(x, y, z, w);
        internal static Quaternion Euler(float x, float y, float z)
        {
            var q = N.Quaternion.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180);
            return new(q.X, q.Y, q.Z, q.W);
        }
    }
    internal readonly struct Matrix4x4(N.Matrix4x4 value)
    {
        private readonly N.Matrix4x4 _value = value;
        internal static Matrix4x4 identity => new(N.Matrix4x4.Identity);
        internal float determinant => _value.GetDeterminant();
        internal Matrix4x4 inverse => N.Matrix4x4.Invert(_value, out var inverse) ? new(inverse) : default;
        internal Vector3 MultiplyPoint(Vector3 point)
        {
            var p = N.Vector4.Transform(new N.Vector4(point.Value, 1), _value);
            return new(p.X / p.W, p.Y / p.W, p.Z / p.W);
        }
        internal Vector3 MultiplyVector(Vector3 point)
        {
            var p = N.Vector3.TransformNormal(point.Value, _value);
            return new(p.X, p.Y, p.Z);
        }
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new(b._value * a._value);
        internal static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 scale) => new(N.Matrix4x4.CreateScale(scale.Value) * N.Matrix4x4.CreateFromQuaternion(q.Value) * N.Matrix4x4.CreateTranslation(p.Value));
        internal static Matrix4x4 Perspective(float left, float right, float bottom, float top) => new(N.Matrix4x4.CreatePerspectiveOffCenter(left, right, bottom, top, .01f, 100));
    }
    internal readonly record struct Color(float r, float g, float b, float a);
    internal sealed class Material : Object
    {
        internal Object shader = new Sprite();
        internal readonly Dictionary<string, int> Values = new();
        internal Material()
        {
        }
        internal Material(Material source)
        {
            shader = source.shader;
            CopyPropertiesFromMaterial(source);
        }
        internal void CopyPropertiesFromMaterial(Material source)
        {
            Values.Clear();
            foreach (var pair in source.Values)
                Values.Add(pair.Key, pair.Value);
        }
        internal void SetInt(string name, int value) => Values[name] = value;
    }
    internal sealed class Sprite : Object
    {
    }
    internal sealed class Camera : Component
    {
        internal enum StereoscopicEye
        {
            Left, Right
        }
        internal bool isActiveAndEnabled = true, stereoEnabled = true;
        internal float nearClipPlane = .01f;
        internal readonly Matrix4x4[] Projection = new Matrix4x4[2], View = new Matrix4x4[2];
        internal Matrix4x4 GetStereoProjectionMatrix(StereoscopicEye eye) => Projection[(int)eye];
        internal Matrix4x4 GetStereoViewMatrix(StereoscopicEye eye) => View[(int)eye];
    }
    internal enum RenderMode
    {
        WorldSpace
    }
    internal sealed class Canvas : Component
    {
        internal bool isActiveAndEnabled = true, overrideSorting;
        internal int sortingOrder;
        internal Camera? worldCamera;
        internal RenderMode renderMode;
    }
    internal sealed class CanvasRenderer : Component
    {
    }
    internal static class Mathf
    {
        internal static float Abs(float value) => MathF.Abs(value);
        internal static float Min(float a, float b) => MathF.Min(a, b);
        internal static float Max(float a, float b) => MathF.Max(a, b);
    }
}
namespace UnityEngine.Rendering
{
    internal enum CompareFunction
    {
        Always = 8
    }
    internal readonly struct ScriptableRenderContext
    {
    }
    internal static class RenderPipelineManager
    {
        internal static event Action<ScriptableRenderContext, UnityEngine.Camera>? beginCameraRendering;
        internal static int Subscribers => beginCameraRendering?.GetInvocationList().Length ?? 0;
        internal static void Render(UnityEngine.Camera camera) => beginCameraRendering?.Invoke(default, camera);
    }
}
namespace UnityEngine.UI
{
    internal sealed class Image : UnityEngine.Component
    {
        internal enum Type
        {
            Simple, Sliced
        }
        internal enum FillMethod
        {
            Radial360
        }
        internal UnityEngine.Sprite? sprite;
        internal UnityEngine.Sprite? overrideSprite => sprite;
        internal UnityEngine.Material material = new();
        internal UnityEngine.Color color;
        internal bool enabled = true, raycastTarget = true, maskable = true, preserveAspect, fillCenter = true, fillClockwise;
        internal float fillAmount;
        internal int fillOrigin;
        internal Type type;
        internal FillMethod fillMethod;
        internal UnityEngine.Canvas? canvas;
        internal bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
}
namespace UnityEngine.XR
{
    internal static class XRSettings
    {
        internal static bool enabled = true;
    }
}
internal sealed class GameplayUI
{
    internal UnityEngine.UI.Image? hurt;
}
internal static class SceneSingleton<T> where T : class
{
    internal static T? i;
}
namespace NOVR
{
    internal static class APIBus
    {
        internal static UnityEngine.Camera? MainCamera, CockpitHudCamera;
    }
    internal static class LayerHelper
    {
        internal static int Assignments;
        internal static int GetVrUiLayer() => 23;
        internal static void SetLayerRecursive(UnityEngine.Transform transform, int layer) => Assignments++;
    }
}
namespace NOVR.MotionControls
{
    internal static class TrackedControllers
    {
        internal static bool Available = true;
    }
}
namespace NOVR.VrUi
{
    internal sealed class NOUIManager
    {
        internal static NOUIManager? I = new();
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal sealed class HudGraphicVisibilityLease
    {
        internal static readonly List<HudGraphicVisibilityLease> All = new();
        internal readonly UnityEngine.UI.Image Source;
        internal bool Hidden;
        internal HudGraphicVisibilityLease(UnityEngine.UI.Image source, bool children = false)
        {
            Source = source;
            All.Add(this);
        }
        internal void Apply() => Hidden = true;
        internal void Restore() => Hidden = false;
        internal void Release() => Restore();
    }
}
