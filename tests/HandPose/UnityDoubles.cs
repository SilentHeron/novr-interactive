// Minimal transform/math doubles for exercising the real glove pose controller.
// They do not emulate Unity skinning, shader evaluation, or headset tracking.
#pragma warning disable CS0649
using N = System.Numerics;
namespace UnityEngine
{
    public class Object
    {
        internal static readonly List<Object> Created = new();
        internal int DestroyCount;
        public string name = "";
        protected Object() => Created.Add(this);
        public static bool operator ==(Object? a, Object? b) =>
            (ReferenceEquals(a, null) || a.DestroyCount > 0) && (ReferenceEquals(b, null) || b.DestroyCount > 0) || ReferenceEquals(a, b);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? value) => ReferenceEquals(this, value);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object value)
        {
            value.DestroyCount++;
            if (value is GameObject go)
            {
                foreach (var component in go.Components)
                    Destroy(component);
                foreach (var child in go.transform.Children.ToArray())
                    Destroy(child.gameObject);
                go.transform.parent?.Children.Remove(go.transform);
            }
        }
    }
    public class GameObject : Object
    {
        internal readonly List<Object> Components = new();
        public int layer;
        public readonly Transform transform; public bool activeSelf;
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform(this);
        }
        public void SetActive(bool active) => activeSelf = active;
        public T AddComponent<T>() where T : new()
        {
            ResourceFault.Check("renderer");
            var component = new T();
            if (component is SkinnedMeshRenderer renderer)
            {
                renderer.transform = transform;
                Components.Add(renderer);
            }
            return component;
        }
    }
    public sealed class Transform
    {
        internal readonly List<Transform> Children = new();
        public readonly GameObject gameObject; public Transform parent = null!;
        public Vector3 localPosition; public Quaternion localRotation = Quaternion.identity;
        public string name => gameObject.name;
        public Vector3 localScale = Vector3.one;
        public Vector3 lossyScale => parent == null ? localScale : Vector3.Scale(parent.lossyScale, localScale);
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 up => rotation * Vector3.up;
        public Transform(GameObject owner) => gameObject = owner;
        public void SetParent(Transform value, bool worldPositionStays)
        {
            parent?.Children.Remove(this);
            parent = value;
            parent?.Children.Add(this);
        }
        public Vector3 position
        {
            get => parent == null ? localPosition : parent.TransformPoint(localPosition); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value);
        }
        public Quaternion rotation
        {
            get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value;
        }
        public Matrix4x4 worldToLocalMatrix => localToWorldMatrix.inverse;
        public Matrix4x4 localToWorldMatrix => Matrix4x4.TRS(position, rotation, lossyScale);
        public Vector3 TransformPoint(Vector3 value) => position + rotation * value;
        public Vector3 InverseTransformPoint(Vector3 value) => Quaternion.Inverse(rotation) * (value - position);
        public Vector3 TransformDirection(Vector3 value) => rotation * value;
        public Vector3 TransformVector(Vector3 value) => rotation * Vector3.Scale(value, lossyScale);
        public Vector3 InverseTransformDirection(Vector3 value) => Quaternion.Inverse(rotation) * value;
        public void SetPositionAndRotation(Vector3 p, Quaternion r)
        {
            position = p;
            rotation = r;
        }
    }
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
        internal N.Vector3 N => new(x, y, z); internal static Vector3 From(N.Vector3 v) => new(v.X, v.Y, v.Z);
        public static Vector3 zero => default; public static Vector3 one => new(1, 1, 1);
        public static Vector3 right => new(1, 0, 0); public static Vector3 up => new(0, 1, 0); public static Vector3 forward => new(0, 0, 1);
        public static Vector3 back => new(0, 0, -1);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Scale(Vector3 a, Vector3 b) => new(a.x * b.x, a.y * b.y, a.z * b.z);
        public float sqrMagnitude => N.LengthSquared(); public float magnitude => N.Length();
        public Vector3 normalized => magnitude < 1e-12f ? zero : this / magnitude;
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.N + b.N);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.N - b.N);
        public static Vector3 operator *(Vector3 a, float b) => From(a.N * b);
        public static Vector3 operator /(Vector3 a, float b) => From(a.N / b);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * Mathf.Clamp01(t);
        public static float Dot(Vector3 a, Vector3 b) => System.Numerics.Vector3.Dot(a.N, b.N);
        public static Vector3 Cross(Vector3 a, Vector3 b) => From(System.Numerics.Vector3.Cross(a.N, b.N));
        public bool Equals(Vector3 other) => N.Equals(other.N); public override bool Equals(object? value) => value is Vector3 v && Equals(v); public override int GetHashCode() => N.GetHashCode();
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }
        private N.Quaternion N => new(x, y, z, w); private static Quaternion From(N.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
        public static Quaternion identity => new(0, 0, 0, 1); public Quaternion normalized => From(System.Numerics.Quaternion.Normalize(N));
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.N * b.N);
        public static Vector3 operator *(Quaternion q, Vector3 v) => Vector3.From(System.Numerics.Vector3.Transform(v.N, q.N));
        public static Quaternion Inverse(Quaternion q) => From(System.Numerics.Quaternion.Inverse(q.N));
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => From(System.Numerics.Quaternion.Slerp(a.N, b.N, Mathf.Clamp01(t))).normalized;
        public static Quaternion AngleAxis(float degrees, Vector3 axis) => From(System.Numerics.Quaternion.CreateFromAxisAngle(axis.normalized.N, degrees * MathF.PI / 180));
        public static Quaternion Euler(float x, float y, float z) => AngleAxis(y, Vector3.up) * AngleAxis(x, Vector3.right) * AngleAxis(z, Vector3.forward);
        public static Quaternion Euler(Vector3 value) => Euler(value.x, value.y, value.z);
        public static float Angle(Quaternion a, Quaternion b) => 2 * MathF.Acos(Math.Clamp(MathF.Abs(System.Numerics.Quaternion.Dot(a.normalized.N, b.normalized.N)), 0, 1)) * 180 / MathF.PI;
        public static Quaternion FromToRotation(Vector3 a, Vector3 b)
        {
            a = a.normalized;
            b = b.normalized;
            var dot = Math.Clamp(Vector3.Dot(a, b), -1, 1);
            if (dot > .999999f)
                return identity;
            var cross = Vector3.Cross(a, b);
            if (cross.sqrMagnitude < .000001f)
                cross = Vector3.Cross(a, MathF.Abs(a.x) < .9f ? Vector3.right : Vector3.up);
            return AngleAxis(MathF.Acos(dot) * 180 / MathF.PI, cross);
        }
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            var z = forward.normalized;
            var x = Vector3.Cross(up, z).normalized;
            var y = Vector3.Cross(z, x);
            return From(System.Numerics.Quaternion.CreateFromRotationMatrix(new System.Numerics.Matrix4x4(
                    x.x, x.y, x.z, 0, y.x, y.y, y.z, 0, z.x, z.y, z.z, 0, 0, 0, 0, 1)));
        }
    }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }
    public struct Rect
    {
    }
    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size)
        {
            this.center = center;
            this.size = size;
        }
    }
    public struct Matrix4x4
    {
        private N.Matrix4x4 _value;
        private Matrix4x4(N.Matrix4x4 value) => _value = value;
        public static Matrix4x4 identity => new(N.Matrix4x4.Identity);
        public static Matrix4x4 TRS(Vector3 p, Quaternion q, Vector3 s) => new(
            N.Matrix4x4.CreateScale(s.N) * N.Matrix4x4.CreateFromQuaternion(new(q.x, q.y, q.z, q.w)) * N.Matrix4x4.CreateTranslation(p.N));
        public Matrix4x4 inverse
        {
            get
            {
                N.Matrix4x4.Invert(_value, out var value);
                return new(value);
            }
        }
        public Vector3 MultiplyPoint3x4(Vector3 value) => Vector3.From(N.Vector3.Transform(value.N, _value));
        public Vector3 MultiplyVector(Vector3 value) => Vector3.From(N.Vector3.TransformNormal(value.N, _value));
        public float m00 => _value.M11; public float m01 => _value.M21; public float m02 => _value.M31;
        public float m10 => _value.M12; public float m11 => _value.M22; public float m12 => _value.M32;
        public float m20 => _value.M13; public float m21 => _value.M23; public float m22 => _value.M33;
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => new(b._value * a._value);
    }
    public struct BoneWeight
    {
        public int boneIndex0, boneIndex1, boneIndex2, boneIndex3; public float weight0, weight1, weight2, weight3;
    }
    public sealed class Material : Object
    {
    }
    internal static class ResourceFault
    {
        private static string? _operation;
        private static int _calls;
        internal static int Occurrence = 1;
        internal static string? Operation
        {
            get => _operation;
            set
            {
                _operation = value;
                _calls = 0;
                Occurrence = 1;
            }
        }
        internal static void Check(string operation)
        {
            if (Operation == operation && ++_calls == Occurrence)
                throw new InvalidOperationException("Injected " + operation + " failure");
        }
    }
    public sealed class Mesh : Object
    {
        public int vertexCount => vertices.Length;
        public int subMeshCount;
        public Bounds bounds = new(Vector3.zero, Vector3.one);
        internal readonly Dictionary<int, int[]> Submeshes = new();
        public void SetTriangles(int[] indices, int submesh) => Submeshes[submesh] = indices;
        private Vector3[] _vertices = [];
        public Vector3[] vertices
        {
            get => _vertices;
            set
            {
                ResourceFault.Check("vertices");
                _vertices = value;
            }
        }
        public Vector3[] normals = []; public Vector2[] uv = []; public BoneWeight[] boneWeights = []; public Matrix4x4[] bindposes = []; public int[] triangles = [];
        public void RecalculateNormals()
        {
            ResourceFault.Check("normals");
            normals = new Vector3[vertices.Length];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i];
                var b = triangles[i + 1];
                var c = triangles[i + 2];
                var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += n;
                normals[b] += n;
                normals[c] += n;
            }
            for (var i = 0; i < normals.Length; i++)
                normals[i] = normals[i].normalized;
        }
        public void RecalculateTangents()
        {
            ResourceFault.Check("tangents");
        }
        public void RecalculateBounds()
        {
        }
    }
    public enum SkinQuality { Auto = 0, Bone1 = 1, Bone2 = 2, Bone4 = 4 }
    public sealed class SkinnedMeshRenderer : Object
    {
        public Transform transform = null!;
        public bool enabled = true;
        public SkinQuality quality;
        public Material[] sharedMaterials = [];
        public Mesh sharedMesh = null!; public Transform[] bones = []; public Transform rootBone = null!; public Material sharedMaterial = null!;
        public Bounds localBounds; public Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows, updateWhenOffscreen;
    }
    public static class Time
    {
        public static float unscaledTime, unscaledDeltaTime = 1f / 90;
    }
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Abs(float a) => MathF.Abs(a); public static float Exp(float a) => MathF.Exp(a); public static float Sin(float a) => MathF.Sin(a);
        public static float Clamp01(float a) => Math.Clamp(a, 0, 1); public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode
    {
        Off
    }
    public enum VertexAttribute
    {
        Position, Normal
    }
}
namespace NOVR.MotionControls
{
    using UnityEngine;
    internal struct ControllerSample
    {
        public bool Valid, HasAimPose, Primary, Secondary, Top, Outside, StickClick, ThumbstickTouched, ButtonTouched, PrimaryTouched, ThumbrestTouched, TriggerTouched, GripTouched;
        public float Grip, Trigger; public Vector3 Position; public Quaternion Rotation, AimRotation; public ControllerHardwareProfile Profile;
    }
    internal readonly struct Pose
    {
        public readonly Vector3 position; public readonly Quaternion rotation; public Pose(Vector3 p, Quaternion r)
        {
            position = p;
            rotation = r;
        }
    }
    internal static class MotionControlVisuals
    {
        internal static bool FittedStick = true, FittedThrottle = true;
        internal static bool HasFittedCollective = true;
        internal static bool HasFittedHandle(bool stick) => stick ? FittedStick : FittedThrottle;
    }
    internal static class LayerHelper
    {
        internal enum Layers
        {
            Cockpit
        }
        internal static void SetLayerRecursive(Transform transform, Layers layer)
        {
        }
    }
    internal sealed class ModConfiguration
    {
        internal static readonly ModConfiguration Instance = new(); internal readonly Settings MotionControls = new();
        internal sealed class Settings
        {
            internal Vector3 FreeHandOffset(bool left) => new(left ? -.008f : .012f, -.021f, .025f);
            internal Vector3 FreeHandAngles(bool left) => new(5, -10, left ? 90 : -90);
        }
    }
}
