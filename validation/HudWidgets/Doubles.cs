using N = System.Numerics;

namespace UnityEngine;

public struct Vector2
{
    public float x, y;
    public Vector2(float x, float y) { this.x = x; this.y = y; }
}

public struct Vector3
{
    public float x, y, z;
    public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    public static Vector3 zero => default;
    internal N.Vector3 Numerics => new(x, y, z);
    internal static Vector3 From(N.Vector3 value) => new(value.X, value.Y, value.Z);
    public float magnitude => Numerics.Length();
    public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Numerics + b.Numerics);
    public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.Numerics - b.Numerics);
    public static Vector3 operator *(Vector3 value, float factor) => From(value.Numerics * factor);
}

public struct Quaternion
{
    private readonly N.Quaternion _value;
    private Quaternion(N.Quaternion value) => _value = value;
    public static Quaternion identity => new(N.Quaternion.Identity);
    public static Quaternion Inverse(Quaternion value) => new(N.Quaternion.Inverse(value._value));
    public static Vector3 operator *(Quaternion rotation, Vector3 point) => Vector3.From(N.Vector3.Transform(point.Numerics, rotation._value));
    internal static Quaternion Euler(float pitch, float yaw, float roll) =>
        new(N.Quaternion.CreateFromYawPitchRoll(yaw * MathF.PI / 180, pitch * MathF.PI / 180, roll * MathF.PI / 180));
}
