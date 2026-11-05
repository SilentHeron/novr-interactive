// Boundary doubles exercise the production lifecycle/coordinates/material
// configuration, not Unity skinning, GPU output or actual URP pass scheduling.
using N = System.Numerics;
namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> Created = new();
        internal bool Dead;
        public string name = "";
        public Object() => Created.Add(this);
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || (a is null || a.Dead) && (b is null || b.Dead);
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
        public GameObject gameObject = null!; public Transform transform => gameObject.transform;
    }
    public class MonoBehaviour : Component
    {
    }
    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public readonly Transform transform;
        public int layer;
        public bool activeSelf = true;
        public bool activeInHierarchy => !Dead && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this };
            Components.Add(transform);
        }
        public void SetActive(bool value) => activeSelf = value;
        public T AddComponent<T>() where T : Component, new()
        {
            var value = new T { gameObject = this };
            Components.Add(value);
            return value;
        }
    }
    public sealed class Transform : Component
    {
        internal readonly List<Transform> Children = new();
        public Transform? parent;
        public Vector3 localPosition, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public Vector3 position
        {
            get => TransformPoint(Vector3.zero); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value);
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
        public Vector3 lossyScale => new(TransformVector(Vector3.right).magnitude, TransformVector(Vector3.up).magnitude, TransformVector(Vector3.forward).magnitude);
        public void SetParent(Transform? next, bool worldPositionStays)
        {
            parent?.Children.Remove(this);
            parent = next;
            next?.Children.Add(this);
        }
        public Vector3 TransformPoint(Vector3 p)
        {
            var result = localPosition + localRotation * (p * localScale);
            return parent == null ? result : parent.TransformPoint(result);
        }
        public Vector3 InverseTransformPoint(Vector3 p)
        {
            if (parent != null)
                p = parent.InverseTransformPoint(p);
            return (Quaternion.Inverse(localRotation) * (p - localPosition)) / localScale;
        }
        public Vector3 TransformVector(Vector3 p)
        {
            var result = localRotation * (p * localScale);
            return parent == null ? result : parent.TransformVector(result);
        }
        public Vector3 InverseTransformVector(Vector3 p)
        {
            if (parent != null)
                p = parent.InverseTransformVector(p);
            return (Quaternion.Inverse(localRotation) * p) / localScale;
        }
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        internal N.Vector3 N => new(x, y, z); internal static Vector3 From(N.Vector3 v) => new(v.X, v.Y, v.Z);
        public static Vector3 zero => new(0, 0, 0); public static Vector3 one => new(1, 1, 1);
        public static Vector3 right => new(1, 0, 0); public static Vector3 up => new(0, 1, 0); public static Vector3 forward => new(0, 0, 1);
        public float sqrMagnitude => N.LengthSquared(); public float magnitude => N.Length();
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.N + b.N);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.N - b.N);
        public static Vector3 operator *(Vector3 a, float b) => From(a.N * b);
        public static Vector3 operator *(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 operator /(Vector3 a, Vector3 b) => new(a.x / b.x, a.y / b.y, a.z / b.z);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        private N.Quaternion N => new(x, y, z, w); private static Quaternion From(N.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static Quaternion identity => new(0, 0, 0, 1);
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.N * b.N);
        public static Vector3 operator *(Quaternion q, Vector3 v) => Vector3.From(System.Numerics.Vector3.Transform(v.N, q.N));
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.N));
        public static Quaternion Euler(float x, float y, float z) => From(System.Numerics.Quaternion.CreateFromYawPitchRoll(y * MathF.PI / 180, x * MathF.PI / 180, z * MathF.PI / 180));
    }
    public readonly record struct Bounds(Vector3 center, Vector3 size);
    public readonly record struct Color(float r, float g, float b, float a)
    {
        public static Color white => new(1, 1, 1, 1);
    }
    public sealed class Camera : MonoBehaviour
    {
    }
    public sealed class Mesh : Object
    {
        public int subMeshCount = 1;
    }
    public sealed class Texture2D : Object
    {
        public static readonly Texture2D whiteTexture = new();
    }
    public sealed class Shader : Object
    {
        internal static bool Available = true, Forward = true;
        internal static readonly HashSet<string> MissingProperties = new();
        public static Shader? Find(string name) => Available ? new Shader { name = name } : null;
    }
    public sealed class Material : Object
    {
        public readonly Shader shader;
        public int renderQueue;
        internal readonly Dictionary<string, float> Floats = new();
        internal readonly Dictionary<string, Color> Colors = new();
        internal readonly Dictionary<string, Texture2D> Textures = new();
        internal readonly HashSet<string> Keywords = new();
        internal readonly Dictionary<string, bool> Passes = new();
        public Material(Shader shader) => this.shader = shader;
        public bool HasProperty(string property) => !Shader.MissingProperties.Contains(property);
        public int FindPass(string name) => name == "ForwardLit" && Shader.Forward ? 0 : -1;
        public float GetFloat(string property) => Floats.GetValueOrDefault(property);
        public void SetFloat(string property, float value) => Floats[property] = value;
        public void SetColor(string property, Color value) => Colors[property] = value;
        public void SetTexture(string property, Texture2D value) => Textures[property] = value;
        public void EnableKeyword(string keyword) => Keywords.Add(keyword);
        public void DisableKeyword(string keyword) => Keywords.Remove(keyword);
        public void SetShaderPassEnabled(string name, bool enabled) => Passes[name] = enabled;
    }
    public enum SkinQuality { Auto = 0, Bone1 = 1, Bone2 = 2, Bone4 = 4 }
    public sealed class SkinnedMeshRenderer : Component
    {
        public bool enabled = true, receiveShadows, updateWhenOffscreen, allowOcclusionWhenDynamic = true;
        public SkinQuality quality;
        public Mesh sharedMesh = null!;
        public Transform[] bones = Array.Empty<Transform>(); public Transform rootBone = null!;
        public Material[] sharedMaterials = Array.Empty<Material>();
        public void GetSharedMaterials(List<Material> results)
        {
            results.Clear();
            results.AddRange(sharedMaterials);
        }
        public Bounds localBounds;
        public Rendering.ShadowCastingMode shadowCastingMode;
        public Rendering.LightProbeUsage lightProbeUsage;
        public Rendering.ReflectionProbeUsage reflectionProbeUsage;
    }
    public static class Mathf
    {
        public static float Abs(float value) => MathF.Abs(value);
    }
    public static class Debug
    {
        public static int Warnings; public static void LogWarning(string value) => Warnings++;
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order => order;
    }
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode
    {
        Off, On
    }
    public enum LightProbeUsage
    {
        Off, BlendProbes
    }
    public enum ReflectionProbeUsage
    {
        Off, BlendProbes
    }
    public enum RenderQueue
    {
        Geometry = 2000, Transparent = 3000
    }
    public enum BlendMode
    {
        Zero = 0, One = 1, SrcAlpha = 5, OneMinusSrcAlpha = 10
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
    public static CameraMode cameraMode = CameraMode.cockpit;
}
namespace NOVR
{
    internal static class APIBus
    {
        internal static UnityEngine.Camera? MainCamera, CockpitHudCamera;
    }
    internal static class LayerHelper
    {
        internal static int GetVrUiLayer() => 30;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal sealed class CockpitHudWidgets
    {
        internal static CockpitHudWidgets? Instance = new(); internal bool Available = true;
    }
}
namespace NOVR.MotionControls
{
    internal static class PilotHandVisuals
    {
        internal static readonly UnityEngine.SkinnedMeshRenderer?[] Renderers = new UnityEngine.SkinnedMeshRenderer?[4];
        internal static UnityEngine.SkinnedMeshRenderer? GetRenderer(int index) => Renderers[index];
    }
}
