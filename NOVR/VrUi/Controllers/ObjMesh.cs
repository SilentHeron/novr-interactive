using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NOVR.VrUi.Controllers;

// Wavefront OBJ geometry as flat arrays, in the file's own coordinates and
// winding. Only what a textured, unlit controller needs: positions, texture
// coordinates and triangles. No Unity types, so it can be read off the main
// thread and checked without the engine.
internal sealed class ObjMesh
{
    internal readonly float[] Positions, Uvs;
    internal readonly int[] Triangles;
    internal int VertexCount => Positions.Length / 3;

    private ObjMesh(float[] positions, float[] uvs, int[] triangles)
    {
        Positions = positions;
        Uvs = uvs;
        Triangles = triangles;
    }

    internal static ObjMesh Parse(TextReader reader, int maxVertices = 400_000)
    {
        var points = new List<float>();
        var coords = new List<float>();
        var positions = new List<float>();
        var uvs = new List<float>();
        var triangles = new List<int>();
        var lookup = new Dictionary<long, int>();
        var corners = new List<int>();
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length < 2)
                continue;
            if (line[0] == 'v' && line[1] == ' ')
            {
                var p = Numbers(line, 2, 3);
                points.Add(p[0]);
                points.Add(p[1]);
                points.Add(p[2]);
            }
            else if (line[0] == 'v' && line[1] == 't' && line.Length > 2 && line[2] == ' ')
            {
                var t = Numbers(line, 3, 2);
                coords.Add(t[0]);
                coords.Add(t[1]);
            }
            else if (line[0] == 'f' && line[1] == ' ')
            {
                corners.Clear();
                foreach (var corner in line.Substring(2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = corner.Split('/');
                    var point = Index(parts[0], points.Count / 3);
                    var coord = parts.Length > 1 && parts[1].Length > 0 ? Index(parts[1], coords.Count / 2) : -1;
                    var key = ((long)point << 32) | (uint)(coord + 1);
                    if (!lookup.TryGetValue(key, out var vertex))
                    {
                        vertex = positions.Count / 3;
                        if (vertex >= maxVertices)
                            throw new InvalidDataException("Model has too many vertices.");
                        lookup.Add(key, vertex);
                        positions.Add(points[point * 3]);
                        positions.Add(points[point * 3 + 1]);
                        positions.Add(points[point * 3 + 2]);
                        uvs.Add(coord >= 0 ? coords[coord * 2] : 0);
                        uvs.Add(coord >= 0 ? coords[coord * 2 + 1] : 0);
                    }
                    corners.Add(vertex);
                }
                // Polygons are fanned from their first corner.
                for (var i = 2; i < corners.Count; i++)
                {
                    triangles.Add(corners[0]);
                    triangles.Add(corners[i - 1]);
                    triangles.Add(corners[i]);
                }
            }
        }
        if (triangles.Count == 0)
            throw new InvalidDataException("Model has no faces.");
        return new ObjMesh(positions.ToArray(), uvs.ToArray(), triangles.ToArray());
    }

    private static int Index(string text, int count)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value == 0)
            throw new InvalidDataException("Bad face index.");
        // One-based, or negative to count back from the latest element.
        var index = value > 0 ? value - 1 : count + value;
        if (index < 0 || index >= count)
            throw new InvalidDataException("Face index is out of range.");
        return index;
    }

    private static float[] Numbers(string line, int start, int count)
    {
        var result = new float[count];
        var parts = line.Substring(start).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < count)
            throw new InvalidDataException("Incomplete vertex line.");
        for (var i = 0; i < count; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]) ||
                float.IsNaN(result[i]) || float.IsInfinity(result[i]))
                throw new InvalidDataException("Bad vertex number.");
        return result;
    }
}
