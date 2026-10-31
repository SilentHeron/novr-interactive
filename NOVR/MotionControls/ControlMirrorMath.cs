using System;
using System.Numerics;

namespace NOVR.MotionControls;

// Bake a handle-space X reflection into mesh-local vertices. Keeping every
// transform proper avoids inverted grip frames and negative-scale culling.
internal readonly struct ControlMirrorMath
{
    private readonly Matrix4x4 _reflection, _normal;

    internal ControlMirrorMath(Matrix4x4 meshToHandle)
    {
        if (!Matrix4x4.Invert(meshToHandle, out var handleToMesh))
            throw new ArgumentException("Control geometry has a singular transform.");
        _reflection = meshToHandle * Matrix4x4.CreateScale(-1, 1, 1) * handleToMesh;
        if (!Matrix4x4.Invert(_reflection, out var inverse))
            throw new ArgumentException("Control reflection has a singular transform.");
        _normal = Matrix4x4.Transpose(inverse);
    }

    internal Vector3 Position(Vector3 value) => Vector3.Transform(value, _reflection);
    internal Vector3 Normal(Vector3 value) => Unit(Vector3.TransformNormal(value, _normal));
    internal Vector4 Tangent(Vector4 value)
    {
        var direction = Unit(Vector3.TransformNormal(new Vector3(value.X, value.Y, value.Z), _reflection));
        return new Vector4(direction, -value.W);
    }
    private static Vector3 Unit(Vector3 value) => value.LengthSquared() > 1e-12f ? Vector3.Normalize(value) : Vector3.Zero;

    internal static void ReverseTriangles(int[] indices)
    {
        if (indices.Length % 3 != 0) throw new ArgumentException("Control geometry is not triangulated.");
        for (var i = 0; i < indices.Length; i += 3)
        { var swap = indices[i + 1]; indices[i + 1] = indices[i + 2]; indices[i + 2] = swap; }
    }
}
