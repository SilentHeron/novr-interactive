using System;
using System.Collections.Generic;
using System.Numerics;

namespace NOVR.MotionControls;

// Expand in metres in the handle frame, even for small, unevenly scaled
// primitives. Welding split normals keeps the silhouette closed at UV seams.
internal static class ControlOutlineGeometry
{
    internal static Vector3[] Expand(Vector3[] vertices, Vector3[] normals, Matrix4x4 localToHandle, float width)
    {
        if (vertices.Length != normals.Length || width <= 0 || float.IsNaN(width) || float.IsInfinity(width) ||
            !Matrix4x4.Invert(localToHandle, out var inverse))
            throw new ArgumentException("Invalid control outline geometry.");
        var normalMatrix = Matrix4x4.Transpose(inverse);
        var welded = new Dictionary<Vector3, Vector3>();
        for (var i = 0; i < vertices.Length; i++)
        {
            var normal = Vector3.TransformNormal(normals[i], normalMatrix);
            if (!Finite(vertices[i]) || !Finite(normal))
                throw new ArgumentException("Control outline contains non-finite geometry.");
            if (normal.LengthSquared() > 0.00000001f)
                normal = Vector3.Normalize(normal);
            welded.TryGetValue(vertices[i], out var sum);
            welded[vertices[i]] = sum + normal;
        }
        var expanded = new Vector3[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            var normal = welded[vertices[i]];
            var offset = normal.LengthSquared() > 0.00000001f ? Vector3.Normalize(normal) * width : Vector3.Zero;
            expanded[i] = vertices[i] + Vector3.TransformNormal(offset, inverse);
            if (!Finite(expanded[i]))
                throw new ArgumentException("Control outline contains non-finite geometry.");
        }
        return expanded;
    }

    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
        !float.IsNaN(value.Y) && !float.IsInfinity(value.Y) &&
        !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
}
