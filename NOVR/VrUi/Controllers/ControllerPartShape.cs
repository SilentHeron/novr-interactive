using System;
using System.Collections.Generic;
using System.IO;

namespace NOVR.VrUi.Controllers;

// One button, stick or trigger of the shown controller as SteamVR ships it: a
// mesh of its own beside the whole model, in the model's coordinates. Held
// here in grip space with Unity's axes, the space the labels are laid out in,
// so a label's line can end on the real surface of the part it names instead
// of on a flat panel in front of the controller. No Unity types: read off the
// main thread and checked without the engine.
internal sealed class ControllerPartShape
{
    private const long MaxFileBytes = 8L * 1024 * 1024;
    private readonly float[] _positions;
    private readonly int[] _triangles;
    // x, y, z per vertex in grip space, and three vertex numbers per triangle
    // in the file's winding. Read-only: the shape is shared between frames.
    internal float[] Positions => _positions;
    internal int[] Triangles => _triangles;
    // The middle of the part (the mean of its vertices) and how far its
    // furthest vertex is from there.
    internal readonly float CentreX, CentreY, CentreZ, Radius;

    private ControllerPartShape(float[] positions, int[] triangles, float x, float y, float z, float radius)
    {
        _positions = positions;
        _triangles = triangles;
        CentreX = x;
        CentreY = y;
        CentreZ = z;
        Radius = radius;
    }

    internal static ControllerPartShape? From(ObjMesh mesh, RenderModelInfo info)
    {
        var count = mesh.VertexCount;
        if (count < 3 || mesh.Triangles.Length < 3)
            return null;
        var positions = new float[count * 3];
        double sx = 0, sy = 0, sz = 0;
        for (var i = 0; i < count; i++)
        {
            info.ToGrip(mesh.Positions[i * 3], mesh.Positions[i * 3 + 1], mesh.Positions[i * 3 + 2], out var x, out var y, out var z);
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || float.IsNaN(z) || float.IsInfinity(z))
                return null;
            positions[i * 3] = x;
            positions[i * 3 + 1] = y;
            positions[i * 3 + 2] = z;
            sx += x;
            sy += y;
            sz += z;
        }
        var cx = (float)(sx / count);
        var cy = (float)(sy / count);
        var cz = (float)(sz / count);
        var radius = 0d;
        for (var i = 0; i < count; i++)
        {
            double dx = positions[i * 3] - cx, dy = positions[i * 3 + 1] - cy, dz = positions[i * 3 + 2] - cz;
            radius = Math.Max(radius, dx * dx + dy * dy + dz * dz);
        }
        return new ControllerPartShape(positions, (int[])mesh.Triangles.Clone(), cx, cy, cz, (float)Math.Sqrt(radius));
    }

    // The parts of a model that have a mesh file beside it, by part name. The
    // body is left out: nothing is labelled "body". A part whose file is
    // missing or cannot be read is simply absent, and its label then points
    // at the part's origin from the model's description.
    internal static Dictionary<string, ControllerPartShape> LoadAll(string directory, RenderModelInfo info)
    {
        var result = new Dictionary<string, ControllerPartShape>();
        foreach (var part in info.Parts)
        {
            var file = part.File;
            // A plain file name beside the model, never a path out of it.
            if (part.Name == "body" || string.IsNullOrEmpty(file) || file != Path.GetFileName(file) ||
                !file.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                continue;
            try
            {
                var path = Path.Combine(directory, file);
                if (!File.Exists(path) || new FileInfo(path).Length > MaxFileBytes)
                    continue;
                ObjMesh mesh;
                using (var reader = new StreamReader(path))
                    mesh = ObjMesh.Parse(reader);
                var shape = From(mesh, info);
                if (shape != null)
                    result[part.Name] = shape;
            }
            catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException || e is InvalidDataException)
            {
                // The whole model still shows; only this line is less exact.
            }
        }
        return result;
    }

    // Where a line drawn from (fx, fy, fz) to this part ends: the first point
    // of the part's surface on the way to its middle, so the line stops on
    // the face of the button that is turned toward the label. A part whose
    // middle is hollow, so that the line would pass through without touching
    // it, gets the vertex nearest its middle instead.
    internal void Target(float fx, float fy, float fz, out float x, out float y, out float z)
    {
        x = CentreX;
        y = CentreY;
        z = CentreZ;
        if (float.IsNaN(fx) || float.IsInfinity(fx) || float.IsNaN(fy) || float.IsInfinity(fy) || float.IsNaN(fz) || float.IsInfinity(fz))
            return;
        double dx = CentreX - fx, dy = CentreY - fy, dz = CentreZ - fz;
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (length < 1e-6)
            return;
        dx /= length;
        dy /= length;
        dz /= length;
        var best = FirstHit(fx, fy, fz, dx, dy, dz, length + Radius);
        if (!double.IsInfinity(best))
        {
            x = (float)(fx + dx * best);
            y = (float)(fy + dy * best);
            z = (float)(fz + dz * best);
            return;
        }
        var nearest = double.PositiveInfinity;
        for (var i = 0; i + 2 < _positions.Length; i += 3)
        {
            double vx = _positions[i] - CentreX, vy = _positions[i + 1] - CentreY, vz = _positions[i + 2] - CentreZ;
            var distance = vx * vx + vy * vy + vz * vz;
            if (distance >= nearest)
                continue;
            nearest = distance;
            x = _positions[i];
            y = _positions[i + 1];
            z = _positions[i + 2];
        }
    }

    // True when this shape lies across the straight line between two points,
    // other than within `margin` of its far end (where the line is allowed to
    // arrive on a surface).
    internal bool Blocks(float fx, float fy, float fz, float tx, float ty, float tz, float margin)
    {
        double dx = tx - fx, dy = ty - fy, dz = tz - fz;
        var length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (double.IsNaN(length) || double.IsInfinity(length) || length - margin <= 1e-6)
            return false;
        return !double.IsInfinity(FirstHit(fx, fy, fz, dx / length, dy / length, dz / length, length - margin));
    }

    // The line from a label to the part it names, as two or three points
    // (x, y, z each) in grip space. Straight when nothing is in the way. A
    // part on the far side of the controller (a trigger, seen from above) is
    // reached round the side instead: the line first drops beside the
    // controller to the part's own depth, then runs in to it, so it never
    // appears to end on the face it would otherwise pass through. `whole` is
    // the whole controller; (nx, ny, nz) points from the controller toward
    // the label plane.
    internal static float[] Leader(ControllerPartShape part, ControllerPartShape? whole, float fx, float fy, float fz, float nx, float ny, float nz)
    {
        const float margin = .004f;
        part.Target(fx, fy, fz, out var x, out var y, out var z);
        if (whole == null || !whole.Blocks(fx, fy, fz, x, y, z, margin))
            return new[] { fx, fy, fz, x, y, z };
        var normal = Math.Sqrt((double)nx * nx + (double)ny * ny + (double)nz * nz);
        if (double.IsNaN(normal) || normal < 1e-6)
            return new[] { fx, fy, fz, x, y, z };
        // Round the side: straight down from the label to the part's depth.
        var depth = (float)(((part.CentreX - fx) * (double)nx + (part.CentreY - fy) * (double)ny + (part.CentreZ - fz) * (double)nz) / (normal * normal));
        float ex = fx + nx * depth, ey = fy + ny * depth, ez = fz + nz * depth;
        part.Target(ex, ey, ez, out var sx, out var sy, out var sz);
        if (!whole.Blocks(ex, ey, ez, sx, sy, sz, margin) && !whole.Blocks(fx, fy, fz, ex, ey, ez, 0f))
            return new[] { fx, fy, fz, ex, ey, ez, sx, sy, sz };
        // Over the top: along the label plane to the point above the part,
        // then straight down onto it.
        var across = (float)(((fx - part.CentreX) * (double)nx + (fy - part.CentreY) * (double)ny + (fz - part.CentreZ) * (double)nz) / (normal * normal));
        float ax = part.CentreX + nx * across, ay = part.CentreY + ny * across, az = part.CentreZ + nz * across;
        part.Target(ax, ay, az, out var ox, out var oy, out var oz);
        if (!whole.Blocks(ax, ay, az, ox, oy, oz, margin))
            return new[] { fx, fy, fz, ax, ay, az, ox, oy, oz };
        // Nothing reaches it cleanly: keep the side route, which at least
        // shows which side of the controller the part is on.
        return new[] { fx, fy, fz, ex, ey, ez, sx, sy, sz };
    }

    // Distance along a ray (unit direction) to the first triangle it meets
    // within `limit`, or infinity.
    private double FirstHit(double fx, double fy, double fz, double dx, double dy, double dz, double limit)
    {
        var best = double.PositiveInfinity;
        for (var i = 0; i + 2 < _triangles.Length; i += 3)
        {
            int a = _triangles[i] * 3, b = _triangles[i + 1] * 3, c = _triangles[i + 2] * 3;
            double ax = _positions[a], ay = _positions[a + 1], az = _positions[a + 2];
            double e1x = _positions[b] - ax, e1y = _positions[b + 1] - ay, e1z = _positions[b + 2] - az;
            double e2x = _positions[c] - ax, e2y = _positions[c + 1] - ay, e2z = _positions[c + 2] - az;
            // Möller-Trumbore, both faces.
            double px = dy * e2z - dz * e2y, py = dz * e2x - dx * e2z, pz = dx * e2y - dy * e2x;
            var det = e1x * px + e1y * py + e1z * pz;
            if (det > -1e-18 && det < 1e-18)
                continue;
            var inv = 1 / det;
            double tx = fx - ax, ty = fy - ay, tz = fz - az;
            var u = (tx * px + ty * py + tz * pz) * inv;
            if (u < 0 || u > 1)
                continue;
            double qx = ty * e1z - tz * e1y, qy = tz * e1x - tx * e1z, qz = tx * e1y - ty * e1x;
            var v = (dx * qx + dy * qy + dz * qz) * inv;
            if (v < 0 || u + v > 1)
                continue;
            var t = (e2x * qx + e2y * qy + e2z * qz) * inv;
            if (t > 1e-7 && t <= limit && t < best)
                best = t;
        }
        return best;
    }
}
