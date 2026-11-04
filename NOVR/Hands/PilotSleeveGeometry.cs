using System;
using System.Collections.Generic;
using System.Linq;
using V = NOVR.MotionControls.SleeveVector3;
using Uv = NOVR.MotionControls.SleeveVector2;

namespace NOVR.MotionControls;

// Prepared once from the installed pilot, then reused when sleeve width changes.
// No native mesh, hand pose, texture coordinates or controller input is modified.
internal sealed class PilotSleeveGeometry
{
    internal readonly struct Skin
    {
        internal readonly int A, B, C, D;
        internal readonly float X, Y, Z, W;
        internal Skin(int a, int b, int c, int d, float x, float y, float z, float w)
        {
            A = a;
            B = b;
            C = c;
            D = d;
            X = x;
            Y = y;
            Z = z;
            W = w;
        }
        internal float Weight(int bone) => (A == bone ? X : 0) + (B == bone ? Y : 0) + (C == bone ? Z : 0) + (D == bone ? W : 0);
        internal Skin ArmOnly(int upper, int fore, int hand)
        {
            var u = Weight(upper);
            var f = Weight(fore);
            var h = Weight(hand);
            var sum = u + f + h;
            // This is a separate sleeve, not the native torso. Keeping its
            // original spine/root influences pins cloth against a rolling arm
            // and moves the twist pinch up to the shoulder.
            return sum > 0 ? new Skin(upper, fore, hand, 0, u / sum, f / sum, h / sum, 0) : this;
        }
        internal Skin Toward(int bone, float amount)
        {
            var weights = new Dictionary<int, float>();
            void Add(int b, float w)
            {
                if (w > 0)
                    weights[b] = (weights.TryGetValue(b, out var old) ? old : 0) + w;
            }
            Add(A, X * (1 - amount));
            Add(B, Y * (1 - amount));
            Add(C, Z * (1 - amount));
            Add(D, W * (1 - amount));
            Add(bone, amount);
            var ordered = weights.OrderByDescending(p => p.Value).Take(4).ToArray();
            var sum = ordered.Sum(p => p.Value);
            var ids = new int[4];
            var values = new float[4];
            for (var i = 0; i < ordered.Length; i++)
            {
                ids[i] = ordered[i].Key;
                values[i] = ordered[i].Value / sum;
            }
            return new Skin(ids[0], ids[1], ids[2], ids[3], values[0], values[1], values[2], values[3]);
        }
    }
    internal readonly struct Frame
    {
        internal readonly V Origin, X, Y, Z;
        internal Frame(V origin, V x, V y, V z)
        {
            Origin = origin;
            X = V.Normalize(x);
            Y = V.Normalize(y);
            Z = V.Normalize(z);
        }
        internal V Local(V p)
        {
            p -= Origin;
            return new V(V.Dot(p, X), V.Dot(p, Y), V.Dot(p, Z));
        }
        internal V World(V p) => Origin + X * p.X + Y * p.Y + Z * p.Z;
    }
    internal readonly V[] Original, SourceNormals;
    internal readonly Uv[] UV;
    internal readonly Skin[] Weights;
    internal readonly int[][] Triangles;
    internal readonly int[] SourceIndices, Cuff;
    internal readonly int SourceVertexCount;
    internal V[] Positions
    {
        get; private set;
    }
    internal V[] Normals
    {
        get; private set;
    }
    internal readonly float Radius;
    private readonly V[] _centers, _fitTargets;
    private readonly float[] _fitBlend;
    private readonly List<int[]> _seams;

    internal PilotSleeveGeometry(V[] positions, V[] normals, Uv[] uv, Skin[] skin, int[][] triangles,
        int upper, int fore, int hand, V shoulder, V elbow, Frame wrist, float metersPerUnit)
    {
        if (positions.Length == 0 || positions.Length != skin.Length || positions.Length != uv.Length ||
            !Finite(metersPerUnit) || metersPerUnit <= 0)
            throw new ArgumentException("Invalid sleeve source.");
        var kept = new List<int>();
        var map = new Dictionary<int, int>();
        var groups = new List<int[]>();
        bool Keep(int i) => i >= 0 && i < positions.Length && skin[i].Weight(upper) + skin[i].Weight(fore) > .45f && skin[i].Weight(hand) < .55f;
        int Index(int i)
        {
            if (!map.TryGetValue(i, out var value))
            {
                value = kept.Count;
                map.Add(i, value);
                kept.Add(i);
            }
            return value;
        }
        foreach (var submesh in triangles)
        {
            var selected = new List<int>();
            for (var i = 0; i + 2 < submesh.Length; i += 3)
                if (Keep(submesh[i]) && Keep(submesh[i + 1]) && Keep(submesh[i + 2]))
                {
                    selected.Add(Index(submesh[i]));
                    selected.Add(Index(submesh[i + 1]));
                    selected.Add(Index(submesh[i + 2]));
                }
            groups.Add(selected.ToArray());
        }
        if (kept.Count < 8)
            throw new ArgumentException("Pilot has no usable sleeve surface.");
        SourceVertexCount = kept.Count;
        SourceIndices = kept.ToArray();
        Original = kept.Select(i => positions[i]).ToArray();
        SourceNormals = kept.Select(i => normals.Length == positions.Length ? normals[i] : V.Zero).ToArray();
        UV = kept.Select(i => uv[i]).ToArray();
        Weights = kept.Select(i => skin[i].ArmOnly(upper, fore, hand)).ToArray();
        Triangles = groups.ToArray();
        Positions = new V[Original.Length];
        Normals = new V[Original.Length];
        _centers = new V[Original.Length];
        _fitTargets = new V[Original.Length];
        _fitBlend = new float[Original.Length];
        for (var i = 0; i < Original.Length; i++)
        {
            var wu = Weights[i].Weight(upper);
            var wf = Weights[i].Weight(fore);
            _centers[i] = (Line(shoulder, elbow, Original[i]) * wu + Line(elbow, wrist.Origin, Original[i]) * wf) / (wu + wf);
            Radius = Math.Max(Radius, Math.Min(V.Distance(Original[i], Segment(shoulder, elbow, Original[i])), V.Distance(Original[i], Segment(elbow, wrist.Origin, Original[i]))));
        }
        var weld = BuildWeld(Original, metersPerUnit);
        var boundary = BoundaryLoops(Triangles, weld);
        Cuff = boundary.Where(loop => loop.Length >= 6 && loop.Length <= 64)
            .OrderBy(loop => loop.Average(i => V.DistanceSquared(Original[i], wrist.Origin)))
            .FirstOrDefault(loop => loop.Average(i => V.Distance(Original[i], wrist.Origin)) * metersPerUnit < .12f) ?? Array.Empty<int>();
        if (Cuff.Length >= 6)
            FitCuff(wrist, hand, metersPerUnit, weld);
        var shoulderRim = boundary.Where(loop => loop.Length >= 6 && loop.Length <= 64 && loop != Cuff)
            .OrderBy(loop => loop.Average(i => V.DistanceSquared(Original[i], shoulder)))
            .FirstOrDefault(loop => loop.Average(i => V.Distance(Original[i], shoulder)) * metersPerUnit < .24f);
        if (shoulderRim != null)
        {
            var axis = V.Normalize(elbow - shoulder);
            var ordered = FitShoulder(shoulderRim, shoulder, axis, upper, metersPerUnit, weld);
            var center = shoulder + axis * (.012f / metersPerUnit);
            var first = Original.Length;
            // The tiny cap borrows a compact patch from an existing upper-arm
            // triangle instead of stretching unrelated atlas islands across it.
            var uvCenter = UV[ordered[0]];
            var largest = 0f;
            foreach (var t in Triangles)
                for (var i = 0; i < t.Length; i += 3)
                {
                    var a = t[i];
                    var b = t[i + 1];
                    var c = t[i + 2];
                    if (Weights[a].Weight(upper) < .8f || Weights[b].Weight(upper) < .8f || Weights[c].Weight(upper) < .8f)
                        continue;
                    var area = Math.Abs((UV[b].X - UV[a].X) * (UV[c].Y - UV[a].Y) - (UV[b].Y - UV[a].Y) * (UV[c].X - UV[a].X));
                    if (area > largest)
                    {
                        largest = area;
                        uvCenter = new Uv((UV[a].X + UV[b].X + UV[c].X) / 3, (UV[a].Y + UV[b].Y + UV[c].Y) / 3);
                    }
                }
            var capPositions = ordered.Select(i => _fitTargets[i]).Concat(new[] { center }).ToArray();
            var capUvs = ordered.Select((_, i) => new Uv(uvCenter.X + (float)Math.Cos(i * Math.PI * 2 / ordered.Length) * .002f,
                uvCenter.Y + (float)Math.Sin(i * Math.PI * 2 / ordered.Length) * .002f)).Concat(new[] { uvCenter }).ToArray();
            SourceIndices = SourceIndices.Concat(ordered.Select(i => SourceIndices[i])).Concat(new[] { SourceIndices[ordered[0]] }).ToArray();
            Original = Original.Concat(capPositions).ToArray();
            SourceNormals = SourceNormals.Concat(ordered.Select(i => SourceNormals[i])).Concat(new[] { axis * -1 }).ToArray();
            UV = UV.Concat(capUvs).ToArray();
            Weights = Weights.Concat(capPositions.Select(_ => new Skin(upper, 0, 0, 0, 1, 0, 0, 0))).ToArray();
            _centers = _centers.Concat(capPositions).ToArray();
            _fitTargets = _fitTargets.Concat(capPositions).ToArray();
            _fitBlend = _fitBlend.Concat(capPositions.Select(_ => 1f)).ToArray();
            Positions = new V[Original.Length];
            Normals = new V[Original.Length];
            var cap = new List<int>();
            var at = Original.Length - 1;
            for (var i = 0; i < ordered.Length; i++)
            {
                var a = first + i;
                var b = first + (i + 1) % ordered.Length;
                if (V.Dot(V.Cross(_fitTargets[a] - center, _fitTargets[b] - center), axis) > 0)
                {
                    var swap = a;
                    a = b;
                    b = swap;
                }
                cap.Add(at);
                cap.Add(a);
                cap.Add(b);
            }
            Triangles[0] = Triangles[0].Concat(cap).ToArray();
            weld = weld.Concat(ordered.Select(i => weld[i])).Concat(new[] { weld.Max() + 1 }).ToArray();
        }
        _seams = Enumerable.Range(0, Original.Length).GroupBy(i => weld[i]).Select(g => g.ToArray()).Where(g => g.Length > 1).ToList();
        Resize(.82f);
    }

    private void FitCuff(Frame wrist, int hand, float scale, int[] weld)
    {
        var loop = Cuff.Select(i => wrist.Local(Original[i])).ToArray();
        var center = new V(loop.Average(p => p.X), loop.Average(p => p.Y), loop.Average(p => p.Z));
        var rx = loop.Max(p => Math.Abs(p.X - center.X));
        var rz = loop.Max(p => Math.Abs(p.Z - center.Z));
        if (rx * scale < .005f || rz * scale < .005f)
            return;
        var boundary = new HashSet<int>(Cuff.Select(i => weld[i]));
        var distance = SurfaceDistance(Cuff, weld, scale, .075f);
        for (var i = 0; i < Original.Length; i++)
        {
            var blend = Smooth(1 - distance[i] / .075f);
            _fitBlend[i] = blend;
            if (blend <= 0)
                continue;
            var p = wrist.Local(Original[i]);
            // Existing glove axes: native hand +Y points along the fingers, +Z
            // is dorsal. This short cloth band ends inside the unchanged glove cuff.
            var target = new V((p.X - center.X) * (.030f / scale) / rx, p.Y - center.Y - .014f / scale,
                -.006f / scale + (p.Z - center.Z) * (.021f / scale) / rz);
            if (boundary.Contains(weld[i]))
            {
                var angle = Math.Atan2((p.Z - center.Z) / rz, (p.X - center.X) / rx);
                target = new V((float)Math.Cos(angle) * .030f / scale, -.014f / scale,
                    (-.006f + (float)Math.Sin(angle) * .021f) / scale);
            }
            _fitTargets[i] = wrist.World(target);
            Weights[i] = Weights[i].Toward(hand, blend * blend);
        }
    }

    private int[] FitShoulder(int[] rim, V shoulder, V axis, int upper, float scale, int[] weld)
    {
        var right = V.Normalize(Original[rim[0]] - shoulder - axis * V.Dot(Original[rim[0]] - shoulder, axis));
        var up = V.Cross(axis, right);
        var boundary = new HashSet<int>(rim.Select(i => weld[i]));
        var rimTargets = new Dictionary<int, V>();
        var winding = V.Dot(Original[rim[1]] - shoulder, up) >= 0 ? 1f : -1f;
        for (var i = 0; i < rim.Length; i++)
        {
            var angle = i * Math.PI * 2 / rim.Length * winding;
            rimTargets[weld[rim[i]]] = shoulder + axis * (.045f / scale) +
                (right * (float)Math.Cos(angle) + up * (float)Math.Sin(angle)) * (.060f / scale);
        }
        var distance = SurfaceDistance(rim, weld, scale, .09f);
        // The native weight cutoff is jagged and extends down the deltoid. Bring
        // its edge back toward the shoulder and blend through existing cloth.
        for (var i = 0; i < Original.Length; i++)
        {
            var blend = Smooth(1 - distance[i] / .09f);
            if (blend <= 0 || _fitBlend[i] > 0)
                continue;
            var p = Original[i] - shoulder;
            var axial = V.Dot(p, axis);
            var radial = p - axis * axial;
            var length = radial.Length();
            if (length < 1e-9f)
                continue;
            var target = shoulder + axis * (.045f / scale) + radial / length * (.060f / scale);
            if (rimTargets.TryGetValue(weld[i], out var rimTarget))
                target = rimTarget;
            else
                target += axis * Math.Max(0, axial - .045f / scale) * .6f;
            _fitTargets[i] = target;
            _fitBlend[i] = blend;
            Weights[i] = Weights[i].Toward(upper, blend * blend);
        }
        return rim;
    }
    private float[] SurfaceDistance(int[] boundary, int[] weld, float scale, float limit)
    {
        var selected = new HashSet<int>(boundary.Select(i => weld[i]));
        var distance = Enumerable.Repeat(float.PositiveInfinity, Original.Length).ToArray();
        var adjacency = new HashSet<int>[Original.Length];
        for (var i = 0; i < adjacency.Length; i++)
            adjacency[i] = new HashSet<int>();
        foreach (var tris in Triangles)
            for (var i = 0; i + 2 < tris.Length; i += 3)
                for (var edge = 0; edge < 3; edge++)
                {
                    var a = tris[i + edge];
                    var b = tris[i + (edge + 1) % 3];
                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
        foreach (var group in Enumerable.Range(0, Original.Length).GroupBy(i => weld[i]))
            foreach (var a in group)
                foreach (var b in group)
                    if (a != b)
                        adjacency[a].Add(b);
        var done = new bool[Original.Length];
        for (var i = 0; i < distance.Length; i++)
            if (selected.Contains(weld[i]))
                distance[i] = 0;
        for (var step = 0; step < Original.Length; step++)
        {
            var at = -1;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < distance.Length; i++)
                if (!done[i] && distance[i] < nearest)
                {
                    at = i;
                    nearest = distance[i];
                }
            if (at < 0 || nearest > limit)
                break;
            done[at] = true;
            foreach (var next in adjacency[at])
                distance[next] = Math.Min(distance[next], nearest + V.Distance(Original[at], Original[next]) * scale);
        }
        return distance;
    }

    internal void Resize(float thickness)
    {
        thickness = Finite(thickness) ? Math.Max(.6f, Math.Min(1.2f, thickness)) : .82f;
        for (var i = 0; i < Original.Length; i++)
        {
            var p = _centers[i] + (Original[i] - _centers[i]) * thickness;
            Positions[i] = V.Lerp(p, _fitTargets[i], _fitBlend[i]);
        }
        Normals = CalculateNormals(Positions, Triangles);
        var surfaceNormals = (V[])Normals.Clone();
        foreach (var seam in _seams)
        {
            // Preserve authored hard folds; only join UV copies of one smooth surface.
            foreach (var at in seam)
            {
                var sum = V.Zero;
                foreach (var other in seam)
                    if ((SourceNormals[at].LengthSquared() < .1f || SourceNormals[other].LengthSquared() < .1f ||
                        V.Dot(SourceNormals[at], SourceNormals[other]) > .8f))
                        sum += surfaceNormals[other];
                if (sum.LengthSquared() > .000001f)
                    Normals[at] = V.Normalize(sum);
            }
        }
    }
    internal static V[] CalculateNormals(V[] vertices, int[][] triangles)
    {
        var normals = new V[vertices.Length];
        foreach (var tris in triangles)
            for (var i = 0; i + 2 < tris.Length; i += 3)
            {
                var a = tris[i];
                var b = tris[i + 1];
                var c = tris[i + 2];
                var cross = V.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                normals[a] += cross;
                normals[b] += cross;
                normals[c] += cross;
            }
        for (var i = 0; i < normals.Length; i++)
            normals[i] = normals[i].LengthSquared() > 1e-20f ? V.Normalize(normals[i]) : V.UnitZ;
        return normals;
    }
    private static int[] BuildWeld(V[] points, float scale)
    {
        var unique = new Dictionary<(long, long, long), int>();
        var ids = new int[points.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var p = points[i] * scale;
            var key = ((long)Math.Round(p.X * 1000000), (long)Math.Round(p.Y * 1000000), (long)Math.Round(p.Z * 1000000));
            if (!unique.TryGetValue(key, out var id))
            {
                id = unique.Count;
                unique.Add(key, id);
            }
            ids[i] = id;
        }
        return ids;
    }
    private static List<int[]> BoundaryLoops(int[][] triangles, int[] weld)
    {
        var edges = new Dictionary<(int, int), int>();
        var vertex = new Dictionary<int, int>();
        for (var i = 0; i < weld.Length; i++)
            vertex[weld[i]] = i;
        foreach (var tris in triangles)
            for (var i = 0; i + 2 < tris.Length; i += 3)
                for (var k = 0; k < 3; k++)
                {
                    var a = weld[tris[i + k]];
                    var b = weld[tris[i + (k + 1) % 3]];
                    var key = a < b ? (a, b) : (b, a);
                    edges[key] = (edges.TryGetValue(key, out var count) ? count : 0) + 1;
                }
        var graph = new Dictionary<int, HashSet<int>>();
        foreach (var edge in edges.Where(e => e.Value == 1))
        {
            var (a, b) = edge.Key;
            if (!graph.ContainsKey(a))
                graph[a] = new HashSet<int>();
            if (!graph.ContainsKey(b))
                graph[b] = new HashSet<int>();
            graph[a].Add(b);
            graph[b].Add(a);
        }
        var remaining = new HashSet<int>(graph.Keys);
        var result = new List<int[]>();
        while (remaining.Count > 0)
        {
            var queue = new Queue<int>();
            var first = remaining.First();
            remaining.Remove(first);
            queue.Enqueue(first);
            var loop = new List<int>();
            var valid = true;
            while (queue.Count > 0)
            {
                var at = queue.Dequeue();
                loop.Add(vertex[at]);
                valid &= graph[at].Count == 2;
                foreach (var next in graph[at])
                    if (remaining.Remove(next))
                        queue.Enqueue(next);
            }
            if (valid)
            {
                // Keep actual edge order for the shoulder cap, not a spatial
                // angle sort that could connect across an irregular native rim.
                var ordered = new List<int>();
                var previous = -1;
                var at = first;
                do
                {
                    ordered.Add(vertex[at]);
                    var next = graph[at].First(n => n != previous);
                    previous = at;
                    at = next;
                } while (at != first && ordered.Count <= loop.Count);
                if (at == first && ordered.Count == loop.Count)
                    result.Add(ordered.ToArray());
            }
        }
        return result;
    }
    private static V Line(V a, V b, V p)
    {
        var d = b - a;
        return a + d * (V.Dot(p - a, d) / Math.Max(1e-20f, d.LengthSquared()));
    }
    private static V Segment(V a, V b, V p)
    {
        var d = b - a;
        return a + d * Math.Max(0, Math.Min(1, V.Dot(p - a, d) / Math.Max(1e-20f, d.LengthSquared())));
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Smooth(float value)
    {
        value = Math.Max(0, Math.Min(1, value));
        return value * value * (3 - 2 * value);
    }
}

// Small managed math types keep the mesh preparation usable in Unity/net48 and
// the standalone fixture without adding a System.Numerics.Vectors dependency.
internal readonly struct SleeveVector3 : IEquatable<SleeveVector3>
{
    internal readonly float X, Y, Z;
    internal SleeveVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }
    internal SleeveVector3(float value) : this(value, value, value) { }
    internal static SleeveVector3 Zero => default;
    internal static SleeveVector3 UnitX => new(1, 0, 0);
    internal static SleeveVector3 UnitY => new(0, 1, 0);
    internal static SleeveVector3 UnitZ => new(0, 0, 1);
    internal float LengthSquared() => Dot(this, this);
    internal float Length() => (float)Math.Sqrt(LengthSquared());
    internal static SleeveVector3 Normalize(SleeveVector3 v) => v / Math.Max(1e-20f, v.Length());
    internal static float Dot(SleeveVector3 a, SleeveVector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
    internal static SleeveVector3 Cross(SleeveVector3 a, SleeveVector3 b) => new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    internal static float Distance(SleeveVector3 a, SleeveVector3 b) => (a - b).Length();
    internal static float DistanceSquared(SleeveVector3 a, SleeveVector3 b) => (a - b).LengthSquared();
    internal static SleeveVector3 Lerp(SleeveVector3 a, SleeveVector3 b, float t) => a + (b - a) * t;
    public static SleeveVector3 operator +(SleeveVector3 a, SleeveVector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static SleeveVector3 operator -(SleeveVector3 a, SleeveVector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static SleeveVector3 operator *(SleeveVector3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
    public static SleeveVector3 operator /(SleeveVector3 a, float s) => new(a.X / s, a.Y / s, a.Z / s);
    public bool Equals(SleeveVector3 v) => X == v.X && Y == v.Y && Z == v.Z;
    public override bool Equals(object? obj) => obj is SleeveVector3 v && Equals(v);
    public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode() ^ Z.GetHashCode();
}
internal readonly struct SleeveVector2 : IEquatable<SleeveVector2>
{
    internal readonly float X, Y;
    internal SleeveVector2(float x, float y)
    {
        X = x;
        Y = y;
    }
    internal static SleeveVector2 Zero => default;
    public bool Equals(SleeveVector2 v) => X == v.X && Y == v.Y;
    public override bool Equals(object? obj) => obj is SleeveVector2 v && Equals(v);
    public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
    public static bool operator ==(SleeveVector2 a, SleeveVector2 b) => a.Equals(b);
    public static bool operator !=(SleeveVector2 a, SleeveVector2 b) => !a.Equals(b);
}
