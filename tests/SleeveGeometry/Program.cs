using Vector3 = NOVR.MotionControls.SleeveVector3;
using Vector2 = NOVR.MotionControls.SleeveVector2;
using System.Text.Json;
using NOVR.MotionControls;
using G = NOVR.MotionControls.PilotSleeveGeometry;

var checks = 0;
void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
void CheckTopology(G geometry, float scale)
{
    var points = new Dictionary<(long, long, long), int>();
    var weld = new int[geometry.Positions.Length];
    for (var i = 0; i < weld.Length; i++)
    {
        var p = geometry.Positions[i] * scale;
        var key = ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6), (long)Math.Round(p.Z * 1e6));
        if (!points.TryGetValue(key, out var id))
        {
            id = points.Count;
            points[key] = id;
        }
        weld[i] = id;
    }
    var edges = new Dictionary<(int, int), int>();
    foreach (var t in geometry.Triangles)
        for (var i = 0; i < t.Length; i += 3)
            for (var k = 0; k < 3; k++)
            {
                var a = weld[t[i + k]];
                var b = weld[t[i + (k + 1) % 3]];
                var key = a < b ? (a, b) : (b, a);
                edges[key] = edges.TryGetValue(key, out var count) ? count + 1 : 1;
            }
    Check(edges.Values.All(v => v <= 2), "Shoulder closure must not create non-manifold edges");
    Check(edges.Values.Count(v => v == 1) == geometry.Cuff.Length, "Only the wrist cuff may remain open");
}
var verts = new List<Vector3>();
var normals = new List<Vector3>();
var uv = new List<Vector2>();
var skin = new List<G.Skin>();
var tris = new List<int>();
const int sides = 12;
var levels = new[] { -.43f, -.3f, -.18f, -.10f, -.04f, 0 };
for (var ring = 0; ring < levels.Length; ring++)
    for (var i = 0; i <= sides; i++)
    {
        var angle = i * MathF.Tau / sides;
        var normal = new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle));
        verts.Add(new Vector3(normal.X * .047f, levels[ring], normal.Z * .036f));
        normals.Add(normal);
        uv.Add(new Vector2(i / (float)sides, ring / 5f));
        // Real sleeves include torso/root weights. Those must not pin this separate
        // cloth mesh while both arm bones follow a twisting tracked wrist.
        skin.Add(ring < 2 ? new G.Skin(4, 1, 0, 0, .7f, .3f, 0, 0) : new G.Skin(5, 6, 1, 0, ring == 5 ? .5f : .7f, ring == 5 ? .3f : 0, ring == 5 ? .2f : .3f, 0));
        if (ring > 0 && i < sides)
        {
            var a = (ring - 1) * (sides + 1) + i;
            var b = ring * (sides + 1) + i;
            tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
        }
    }
verts.Add(new Vector3(10));
normals.Add(Vector3.UnitZ);
uv.Add(Vector2.Zero);
skin.Add(new G.Skin(0, 0, 0, 0, 1, 0, 0, 0));
var original = verts.ToArray();
var model = new G(original, normals.ToArray(), uv.ToArray(), skin.ToArray(), new[] { tris.ToArray() }, 4, 5, 6, new Vector3(0, -.45f, 0), new Vector3(0, -.22f, 0), new G.Frame(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), 1);
Check(model.SourceIndices.Distinct().Count() == verts.Count - 1 && !model.SourceIndices.Contains(verts.Count - 1), "Unused body vertices must not be retained");
Check(model.Cuff.Length == sides, "Welded cuff loop must ignore UV duplicate vertices");
var cuffAtDefault = model.Cuff.Select(i => model.Positions[i]).ToArray();
foreach (var thickness in new[] { .6f, .82f, 1, 1.2f, float.NaN })
{
    model.Resize(thickness);
    CheckTopology(model, 1);
    for (var i = 0; i < model.Positions.Length; i++)
    {
        Check(Finite(model.Positions[i]) && Finite(model.Normals[i]), "All fitted vertices/normals finite");
        Check(Math.Abs(model.Normals[i].Length() - 1) < 1e-4, "Normals normalized");
        var w = model.Weights[i];
        Check(Math.Abs(w.X + w.Y + w.Z + w.W - 1) < 1e-5, "Normalized skin weights");
        Check(Math.Abs(w.Weight(4) + w.Weight(5) + w.Weight(6) - 1) < 1e-5, "Extracted sleeve is independent of stationary torso/root skin weights");
        if (i < model.SourceVertexCount)
            Check(model.UV[i] == uv[model.SourceIndices[i]], "Original UV coordinates unchanged");
    }
    for (var j = 0; j < model.Cuff.Length; j++)
    {
        var i = model.Cuff[j];
        var p = model.Positions[i];
        Check(Vector3.Distance(p, cuffAtDefault[j]) < 1e-6, "Width slider must preserve glove cuff fit");
        Check(Math.Abs(p.Y + .014f) < 1e-6, "Cuff ends inside unchanged glove");
        Check(Math.Abs(p.X * p.X / (.030f * .030f) + (p.Z + .006f) * (p.Z + .006f) / (.021f * .021f) - 1) < 1e-4, "Cuff has smooth elliptical profile");
        Check(model.Weights[i].Weight(6) > .99999f, "Cuff follows wrist during pronation");
    }
    for (var ring = 0; ring < levels.Length; ring++)
    {
        var a = Array.IndexOf(model.SourceIndices, ring * (sides + 1));
        var b = Array.IndexOf(model.SourceIndices, ring * (sides + 1) + sides);
        Check(Vector3.Distance(model.Normals[a], model.Normals[b]) < 1e-5, "UV seams must share smooth normals");
    }
}
Check(original.SequenceEqual(verts), "Native source vertices unchanged");
var mixed = new G.Skin(4, 5, 6, 1, .2f, .4f, .1f, .3f).ArmOnly(4, 5, 6);
Check(Math.Abs(mixed.Weight(4) / mixed.Weight(5) - .5f) < 1e-6 && Math.Abs(mixed.Weight(6) / mixed.Weight(5) - .25f) < 1e-6,
 "Removing torso influence preserves the authored elbow/wrist blend ratios");
var cuffOnly = new G.Skin(6, 1, 0, 0, .8f, .2f, 0, 0).ArmOnly(4, 5, 6);
Check(cuffOnly.Weight(6) == 1 && cuffOnly.Weight(1) == 0, "Rebased wrist still follows the unchanged glove exactly");
if (args.Length == 2)
{
    using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
    var root = doc.RootElement;
    float[] Floats(JsonElement a) => a.EnumerateArray().Select(x => x.GetSingle()).ToArray();
    Vector3 V(JsonElement a)
    {
        var f = Floats(a);
        return new(f[0], f[1], f[2]);
    }
    var input = root.GetProperty("vertices").EnumerateArray().Select(V).ToArray();
    var ns = root.GetProperty("normals").EnumerateArray().Select(V).ToArray();
    var tex = root.GetProperty("uv").EnumerateArray().Select(a => { var f = Floats(a); return new Vector2(f[0], f[1]); }).ToArray();
    var ids = root.GetProperty("boneIndices").EnumerateArray().Select(a => a.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
    var ws = root.GetProperty("weights").EnumerateArray().Select(Floats).ToArray();
    var weights = ids.Select((b, i) => new G.Skin(b[0], b[1], b[2], b[3], ws[i][0], ws[i][1], ws[i][2], ws[i][3])).ToArray();
    var tt = root.GetProperty("triangles").EnumerateArray().Select(a => a.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
    var result = new Dictionary<string, object>();
    foreach (var side in new[] { "left", "right" })
    {
        var info = root.GetProperty(side);
        var bones = info.GetProperty("bones").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        var fr = info.GetProperty("frame");
        var frame = new G.Frame(V(fr.GetProperty("origin")), V(fr.GetProperty("x")), V(fr.GetProperty("y")), V(fr.GetProperty("z")));
        var fitted = new G(input, ns, tex, weights, tt, bones[0], bones[1], bones[2], V(info.GetProperty("shoulder")), V(info.GetProperty("elbow")), frame, 100);
        fitted.Resize(.82f);
        CheckTopology(fitted, 100);
        Check(fitted.Positions.Length < 300, "Actual native sleeve compacts to under300 vertices");
        Check(fitted.Cuff.Length >= 6, "Actual cuff identified");
        Check(fitted.Weights.All(w => Math.Abs(w.Weight(bones[0]) + w.Weight(bones[1]) + w.Weight(bones[2]) - 1) < 1e-5), "Actual pilot sleeve has no remaining torso/root tether");
        foreach (var t in fitted.Triangles)
            for (var i = 0; i < t.Length; i += 3)
            {
                var cross = Vector3.Cross(fitted.Positions[t[i + 1]] - fitted.Positions[t[i]], fitted.Positions[t[i + 2]] - fitted.Positions[t[i]]);
                Check(Finite(cross) && cross.LengthSquared() > 1e-24, "Actual fitted triangles remain finite/nondegenerate");
            }
        float[] F(Vector3 v) => new[] { v.X, v.Y, v.Z };
        result[side] = new
        {
            sourceVertexCount = fitted.SourceVertexCount,
            vertices = fitted.Positions.Select(F),
            normals = fitted.Normals.Select(F),
            uv = fitted.UV.Select(v => new[] { v.X, v.Y }),
            indices = fitted.Weights.Select(w => new[] { w.A, w.B, w.C, w.D }),
            weights = fitted.Weights.Select(w => new[] { w.X, w.Y, w.Z, w.W }),
            triangles = fitted.Triangles,
            cuff = fitted.Cuff,
            sourceIndices = fitted.SourceIndices,
            original = fitted.Original.Select(F)
        };
    }
    File.WriteAllText(args[1], JsonSerializer.Serialize(result));
}
Console.WriteLine($"{checks} sleeve geometry checks passed. Pure source-linked mesh checks; no headset rendering claim.");
