// Minimal transform/math doubles for exercising the real glove pose controller.
// They do not emulate Unity skinning, shader evaluation, or headset tracking.
#pragma warning disable CS0649
using N = System.Numerics;
namespace UnityEngine
{
    public class Object { public string name = ""; public static void Destroy(Object value) { } }
    public class GameObject : Object
    {
        public readonly Transform transform; public bool activeSelf;
        public GameObject(string name) { this.name = name; transform = new Transform(this); }
        public void SetActive(bool active) => activeSelf = active;
        public T AddComponent<T>() where T : new() => new();
    }
    public sealed class Transform
    {
        public readonly GameObject gameObject; public Transform parent = null!;
        public Vector3 localPosition; public Quaternion localRotation = Quaternion.identity;
        public Transform(GameObject owner) => gameObject = owner;
        public void SetParent(Transform value, bool worldPositionStays) => parent = value;
        public Vector3 position { get => parent == null ? localPosition : parent.TransformPoint(localPosition); set => localPosition = parent == null ? value : parent.InverseTransformPoint(value); }
        public Quaternion rotation { get => parent == null ? localRotation : parent.rotation * localRotation; set => localRotation = parent == null ? value : Quaternion.Inverse(parent.rotation) * value; }
        public Matrix4x4 worldToLocalMatrix => default; public Matrix4x4 localToWorldMatrix => default;
        public Vector3 TransformPoint(Vector3 value) => position + rotation * value;
        public Vector3 InverseTransformPoint(Vector3 value) => Quaternion.Inverse(rotation) * (value - position);
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
    }
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        internal N.Vector3 N => new(x, y, z); internal static Vector3 From(N.Vector3 v) => new(v.X, v.Y, v.Z);
        public static Vector3 zero => default; public static Vector3 one => new(1, 1, 1);
        public static Vector3 right => new(1, 0, 0); public static Vector3 up => new(0, 1, 0); public static Vector3 forward => new(0, 0, 1);
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
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
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
    }
    public struct Vector2 { public Vector2(float x, float y) { } }
    public struct Rect { }
    public struct Bounds { public Bounds(Vector3 center, Vector3 size) { } }
    public struct Matrix4x4 { public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b) => default; }
    public struct BoneWeight { public int boneIndex0, boneIndex1, boneIndex2, boneIndex3; public float weight0, weight1, weight2, weight3; }
    public sealed class Material : Object { }
    public sealed class Mesh : Object
    {
        public Vector3[] vertices = [], normals = []; public Vector2[] uv = []; public BoneWeight[] boneWeights = []; public Matrix4x4[] bindposes = []; public int[] triangles = [];
        public void RecalculateNormals()
        {
            normals = new Vector3[vertices.Length];
            for (var i = 0; i < triangles.Length; i += 3)
            {
                var a = triangles[i]; var b = triangles[i + 1]; var c = triangles[i + 2]; var n = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += n; normals[b] += n; normals[c] += n;
            }
            for (var i = 0; i < normals.Length; i++) normals[i] = normals[i].normalized;
        }
        public void RecalculateTangents() { } public void RecalculateBounds() { }
    }
    public sealed class SkinnedMeshRenderer
    {
        public Mesh sharedMesh = null!; public Transform[] bones = []; public Transform rootBone = null!; public Material sharedMaterial = null!;
        public Bounds localBounds; public Rendering.ShadowCastingMode shadowCastingMode; public bool receiveShadows, updateWhenOffscreen;
    }
    public static class Time { public static float unscaledTime, unscaledDeltaTime = 1f / 90; }
    public static class Mathf
    {
        public const float PI = MathF.PI;
        public static float Abs(float a) => MathF.Abs(a); public static float Exp(float a) => MathF.Exp(a); public static float Sin(float a) => MathF.Sin(a);
        public static float Clamp01(float a) => Math.Clamp(a, 0, 1); public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    }
}
namespace UnityEngine.Rendering { public enum ShadowCastingMode { Off } }
namespace NOVR.MotionControls
{
    using UnityEngine;
    internal struct ControllerSample
    {
        public bool Valid, HasAimPose, Primary, Secondary, Top, Outside, StickClick, ThumbstickTouched, ButtonTouched, PrimaryTouched, ThumbrestTouched, TriggerTouched, GripTouched;
        public float Grip, Trigger; public Vector3 Position; public Quaternion Rotation, AimRotation; public ControllerHardwareProfile Profile;
    }
    internal readonly struct Pose { public readonly Vector3 position; public readonly Quaternion rotation; public Pose(Vector3 p, Quaternion r) { position = p; rotation = r; } }
    internal static class MotionControlVisuals
    {
        internal static bool FittedStick = true, FittedThrottle = true;
        internal static bool HasFittedCollective = true;
        internal static bool HasFittedHandle(bool stick) => stick ? FittedStick : FittedThrottle;
    }
    internal static class LayerHelper { internal enum Layers { Cockpit } internal static void SetLayerRecursive(Transform transform, Layers layer) { } }
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
