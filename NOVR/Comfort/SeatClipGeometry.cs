using System;
using System.Collections.Generic;
using System.Numerics;

namespace NOVR.Comfort;

// Subtracts a small convex volume from individual seat triangles. Keeping the
// original triangle's barycentric coordinates lets the renderer preserve its
// own material, UV sets, normals and colours at every newly cut edge.
internal sealed class SeatClipGeometry
{
    private readonly struct Face
    {
        internal readonly int A, B, C;
        internal Face(int a, int b, int c) { A = a; B = b; C = c; }
    }

    private static readonly Vector3[] Normals;
    internal static readonly float InnerRadiusScale;
    internal static int PlaneCount => Normals.Length;
    private readonly Vector3[] _first = new Vector3[PlaneCount + 3];
    private readonly Vector3[] _second = new Vector3[PlaneCount + 3];
    private readonly Vector3[] _crossingDistances = new Vector3[PlaneCount];

    static SeatClipGeometry()
    {
        // Subdividing an octahedron gives 258 evenly distributed directions.
        // The common plane offset is the smallest face distance of that
        // inscribed mesh. Every direction therefore meets a clipping plane at
        // or before radius: the cut never extends outside the requested ball.
        var vertices = new List<Vector3>
        {
            Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY,
            -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ
        };
        var faces = new List<Face>();
        for (var x = 0; x < 2; x++)
            for (var y = 2; y < 4; y++)
                for (var z = 4; z < 6; z++)
                    faces.Add(new Face(x, y, z));
        for (var level = 0; level < 3; level++)
        {
            var midpoints = new Dictionary<long, int>();
            var next = new List<Face>(faces.Count * 4);
            int Midpoint(int a, int b)
            {
                var key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                if (midpoints.TryGetValue(key, out var index)) return index;
                index = vertices.Count;
                vertices.Add(Vector3.Normalize(vertices[a] + vertices[b]));
                midpoints.Add(key, index);
                return index;
            }
            foreach (var face in faces)
            {
                var ab = Midpoint(face.A, face.B);
                var bc = Midpoint(face.B, face.C);
                var ca = Midpoint(face.C, face.A);
                next.Add(new Face(face.A, ab, ca));
                next.Add(new Face(ab, face.B, bc));
                next.Add(new Face(ca, bc, face.C));
                next.Add(new Face(ab, bc, ca));
            }
            faces = next;
        }
        var distance = 1f;
        foreach (var face in faces)
        {
            var a = vertices[face.A];
            var normal = Vector3.Normalize(Vector3.Cross(vertices[face.B] - a, vertices[face.C] - a));
            distance = Math.Min(distance, Math.Abs(Vector3.Dot(normal, a)));
        }
        Normals = vertices.ToArray();
        InnerRadiusScale = distance * .999999f;
    }

    // The caller owns and reuses output. Each three weights describe one kept
    // triangle; false returns the original triangle and true/empty removes it.
    // Coordinates must already be in metres, including any nonuniform scale.
    internal bool ClipTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 center, float radius,
        List<Vector3> output)
    {
        if (output == null) throw new ArgumentNullException(nameof(output));
        if (!Finite(a) || !Finite(b) || !Finite(c) || !Finite(center) ||
            float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0)
            throw new ArgumentException("Seat clearance requires finite metric geometry and a positive radius.");
        output.Clear();
        a -= center;
        b -= center;
        c -= center;
        var offset = radius * InnerRadiusScale;
        var innerSquared = offset * offset;
        // Convexity guarantees that a triangle inside this inner ball is
        // entirely masked; avoid scanning all planes for nearby seat padding.
        if (a.LengthSquared() <= innerSquared && b.LengthSquared() <= innerSquared && c.LengthSquared() <= innerSquared)
        {
            if (Vector3.Cross(b - a, c - a).LengthSquared() <= 1e-24f) return Original(output);
            return true;
        }
        if (DistanceSquaredToTriangle(Vector3.Zero, a, b, c) >= radius * radius)
            return Original(output);

        // A single separating plane can reject near-but-outside faces. Only
        // crossing planes can change this triangle or any convex remainder;
        // cache those distances and avoid visiting the others during clipping.
        var crossingCount = 0;
        foreach (var normal in Normals)
        {
            var distances = new Vector3(Vector3.Dot(normal, a) - offset,
                Vector3.Dot(normal, b) - offset, Vector3.Dot(normal, c) - offset);
            if (distances.X >= 0 && distances.Y >= 0 && distances.Z >= 0)
                return Original(output);
            if (distances.X > 0 || distances.Y > 0 || distances.Z > 0)
                _crossingDistances[crossingCount++] = distances;
        }
        if (crossingCount == 0)
        {
            if (Vector3.Cross(b - a, c - a).LengthSquared() <= 1e-24f) return Original(output);
            return true;
        }

        var input = _first;
        var inside = _second;
        input[0] = Vector3.UnitX;
        input[1] = Vector3.UnitY;
        input[2] = Vector3.UnitZ;
        var count = 3;
        for (var plane = 0; plane < crossingCount; plane++)
        {
            // Signed distances are linear in barycentric coordinates.
            var distances = _crossingDistances[plane];
            var insideCount = 0;
            var outsideCount = 0;
            var firstOutside = Vector3.Zero;
            var previousOutside = Vector3.Zero;
            var previous = input[count - 1];
            var previousDistance = Vector3.Dot(previous, distances);
            for (var i = 0; i < count; i++)
            {
                var current = input[i];
                var currentDistance = Vector3.Dot(current, distances);
                var previousInside = previousDistance <= 0;
                var currentInside = currentDistance <= 0;
                if (previousInside != currentInside)
                {
                    var t = previousDistance / (previousDistance - currentDistance);
                    var intersection = previous + (current - previous) * t;
                    AppendInside(inside, ref insideCount, intersection);
                    EmitOutside(intersection, ref firstOutside, ref previousOutside, ref outsideCount, a, b, c, output);
                }
                if (currentInside) AppendInside(inside, ref insideCount, current);
                else EmitOutside(current, ref firstOutside, ref previousOutside, ref outsideCount, a, b, c, output);
                previous = current;
                previousDistance = currentDistance;
            }
            if (insideCount > 1 && Vector3.DistanceSquared(inside[0], inside[insideCount - 1]) < 1e-16f)
                insideCount--;
            if (insideCount < 3)
                return Original(output);
            var swap = input;
            input = inside;
            inside = swap;
            count = insideCount;
        }

        // Tangential or degenerate contact must not rebuild or erase a face.
        var removedArea = 0f;
        for (var i = 1; i + 1 < count; i++)
            removedArea += Vector3.Cross(Position(input[i], a, b, c) - Position(input[0], a, b, c),
                Position(input[i + 1], a, b, c) - Position(input[0], a, b, c)).Length();
        if (removedArea <= 1e-12f) return Original(output);
        return true;
    }

    private static void AppendInside(Vector3[] vertices, ref int count, Vector3 value)
    {
        if (count == 0 || Vector3.DistanceSquared(vertices[count - 1], value) >= 1e-16f)
            vertices[count++] = value;
    }

    private static bool Original(List<Vector3> output)
    {
        output.Clear();
        output.Add(Vector3.UnitX);
        output.Add(Vector3.UnitY);
        output.Add(Vector3.UnitZ);
        return false;
    }

    private static void EmitOutside(Vector3 value, ref Vector3 first, ref Vector3 previous,
        ref int count, Vector3 a, Vector3 b, Vector3 c, List<Vector3> output)
    {
        if (count == 0) first = value;
        else if (count >= 2)
        {
            var origin = Position(first, a, b, c);
            if (Vector3.Cross(Position(previous, a, b, c) - origin,
                Position(value, a, b, c) - origin).LengthSquared() > 1e-24f)
            {
                output.Add(first);
                output.Add(previous);
                output.Add(value);
            }
        }
        previous = value;
        count++;
    }

    private static Vector3 Position(Vector3 weight, Vector3 a, Vector3 b, Vector3 c) =>
        a * weight.X + b * weight.Y + c * weight.Z;

    internal static float DistanceSquaredToTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a;
        var ac = c - a;
        if (Vector3.Cross(ab, ac).LengthSquared() < 1e-20f)
            return Math.Min(DistanceSquaredToSegment(point, a, b),
                Math.Min(DistanceSquaredToSegment(point, b, c), DistanceSquaredToSegment(point, c, a)));
        var ap = point - a;
        var d1 = Vector3.Dot(ab, ap);
        var d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return ap.LengthSquared();
        var bp = point - b;
        var d3 = Vector3.Dot(ab, bp);
        var d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return bp.LengthSquared();
        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0)
            return Vector3.DistanceSquared(point, a + ab * (d1 / (d1 - d3)));
        var cp = point - c;
        var d5 = Vector3.Dot(ab, cp);
        var d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return cp.LengthSquared();
        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0)
            return Vector3.DistanceSquared(point, a + ac * (d2 / (d2 - d6)));
        var va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
            return Vector3.DistanceSquared(point, b + (c - b) * ((d4 - d3) / ((d4 - d3) + d5 - d6)));
        var denominator = 1f / (va + vb + vc);
        return Vector3.DistanceSquared(point, a + ab * (vb * denominator) + ac * (vc * denominator));
    }

    private static float DistanceSquaredToSegment(Vector3 point, Vector3 a, Vector3 b)
    {
        var delta = b - a;
        var squared = delta.LengthSquared();
        var t = squared > 0 ? Math.Max(0, Math.Min(1, Vector3.Dot(point - a, delta) / squared)) : 0;
        return Vector3.DistanceSquared(point, a + delta * t);
    }

    private static bool Finite(Vector3 value) =>
        !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
        !float.IsNaN(value.Y) && !float.IsInfinity(value.Y) &&
        !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
}
