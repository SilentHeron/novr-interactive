using System.Reflection;
using Q = System.Numerics.Quaternion;
using V = System.Numerics.Vector3;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPrefix : Attribute
    {
    }
    // FieldRef and FieldRefAccess come from tests/Shared/FieldRefDouble.cs.
    public static partial class AccessTools
    {
    }
}

namespace UnityEngine
{
    public struct Vector3(float x, float y, float z)
    {
        public float x = x, y = y, z = z;
        public float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static implicit operator V(Vector3 a) => new(a.x, a.y, a.z);
        public static implicit operator Vector3(V a) => new(a.X, a.Y, a.Z);
    }
    public static class Mathf
    {
        public const float Rad2Deg = 180 / MathF.PI;
        public static float Atan2(float x, float y) => MathF.Atan2(x, y);
    }
    public sealed class Transform
    {
        public Transform? parent;
        public Vector3 localPosition;
        public Vector3 localScale = new(1, 1, 1);
        public Q rotation = Q.Identity;
        public Vector3 localEulerAngles
        {
            set => rotation = Q.CreateFromAxisAngle(V.UnitZ, value.z / Mathf.Rad2Deg);
        }
        public Vector3 position => TransformPoint(default);
        public Vector3 TransformVector(Vector3 v)
        {
            Vector3 result = V.Transform((V)v * (V)localScale, rotation);
            return parent != null ? parent.TransformVector(result) : result;
        }
        public Vector3 InverseTransformVector(Vector3 v)
        {
            var relative = parent != null ? parent.InverseTransformVector(v) : v;
            return V.Transform(relative, Q.Inverse(rotation)) / (V)localScale;
        }
        public Vector3 TransformPoint(Vector3 v)
        {
            Vector3 result = V.Transform((V)v * (V)localScale, rotation) + (V)localPosition;
            return parent != null ? parent.TransformPoint(result) : result;
        }
        public Vector3 InverseTransformPoint(Vector3 v)
        {
            var relative = parent != null ? parent.InverseTransformPoint(v) : v;
            return V.Transform((V)relative - (V)localPosition, Q.Inverse(rotation)) / (V)localScale;
        }
    }
    public sealed class GameObject
    {
        public readonly Transform transform = new();
        public bool activeSelf;
        public void SetActive(bool active) => activeSelf = active;
    }
}

public static class SceneSingleton<T>
{
    public static T i = default!;
}
public sealed class DynamicMap
{
    public readonly UnityEngine.Transform transform = new();
    public UnityEngine.GameObject iconLayer = new();
}
public sealed class ThreatItem
{
    private readonly UnityEngine.Transform? missileIconTransform, playerAircraftIconTransform;
    private readonly UnityEngine.GameObject? vectorLine;
    public ThreatItem(UnityEngine.Transform? missile, UnityEngine.Transform? player, UnityEngine.GameObject? line)
    {
        missileIconTransform = missile;
        playerAircraftIconTransform = player;
        vectorLine = line;
    }
}
