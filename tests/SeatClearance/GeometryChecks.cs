using System.Diagnostics;
using System.Numerics;
using NOVR.Comfort;

internal static partial class Program
{
    static partial void RunGeometryChecks(ref int passed)
    {
        var checkedCount = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checkedCount++;
        }
        var clipper = new SeatClipGeometry();
        var output = new List<Vector3>(4096);
        var origin = Vector3.Zero;
        var a = new Vector3(-2, -1, 0);
        var b = new Vector3(2, -1, 0);
        var c = new Vector3(0, 2, 0);
        const float radius = .14f;
        Check(SeatClipGeometry.PlaneCount == 258, "The intended bounded near-sphere has 258 clipping planes.");
        Check(SeatClipGeometry.InnerRadiusScale > .98f && SeatClipGeometry.InnerRadiusScale < 1,
            "The cut stays within two percent of the requested radius.");
        Check(!clipper.ClipTriangle(a, b, c, new Vector3(0, 0, 2), radius, output), "Distant triangles remain unchanged.");
        Check(output.SequenceEqual(new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ }), "Unchanged faces keep their original vertex weights.");
        Check(clipper.ClipTriangle(a * .01f, b * .01f, c * .01f, origin, radius, output) && output.Count == 0,
            "A face fully inside head clearance disappears.");
        Check(clipper.ClipTriangle(a, b, c, origin, radius, output) && output.Count > 3,
            "A large face crossing the head is split instead of disappearing wholesale.");
        ValidateOutput(a, b, c, output, Check);
        var expectedArea = 6f - MathF.PI * radius * radius;
        var visibleArea = Area(a, b, c, output);
        Check(visibleArea >= expectedArea - .000005f && visibleArea < expectedArea + .003f,
            "Only the small local cut is removed from a large seat triangle.");
        Check(!Contains(output, new Vector3(1f / 3)), "The center of an intersected seat face is removed.");
        Check(Contains(output, new Vector3(.46f, .46f, .08f)), "A distant part of the same seat face survives.");
        foreach (var point in new[] { a, b, c })
            Check(Contains(output, Barycentric(point, a, b, c)), "The original far triangle corners remain visible.");

        var baseline = output.ToArray();
        var translation = new Vector3(3, -4, 1);
        Check(clipper.ClipTriangle(a + translation, b + translation, c + translation, translation, radius, output),
            "Translating both the head and seat preserves the cut.");
        Check(output.Count == baseline.Length && output.Zip(baseline).All(pair => Vector3.DistanceSquared(pair.First, pair.Second) < 1e-10f),
            "The cut uses seat-relative coordinates, not global position.");
        Check(clipper.ClipTriangle(a, c, b, origin, radius, output), "Reversed input winding still clips.");
        ValidateOutput(a, c, b, output, Check);
        Check(Math.Abs(Area(a, c, b, output) - visibleArea) < .00001f, "Winding changes do not change the masked surface.");

        Check(!clipper.ClipTriangle(a, a, a, origin, radius, output), "A distant zero-area face remains untouched.");
        Check(!clipper.ClipTriangle(origin, origin, origin, origin, radius, output), "A zero-area face at the head remains safe.");
        Check(!clipper.ClipTriangle(-Vector3.UnitX, origin, Vector3.UnitX, origin, radius, output), "Collinear triangles do not create invalid geometry.");
        Check(!clipper.ClipTriangle(a + Vector3.UnitZ * radius, b + Vector3.UnitZ * radius, c + Vector3.UnitZ * radius,
            origin, radius, output), "Tangency at the maximum radius leaves the seat unchanged.");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0, -.1f })
        {
            var failed = false;
            try { clipper.ClipTriangle(a, b, c, origin, invalid, output); }
            catch (ArgumentException) { failed = true; }
            Check(failed, "Invalid radius fails before modifying geometry.");
        }
        var invalidVertexRejected = false;
        try { clipper.ClipTriangle(new Vector3(float.NaN, 0, 0), b, c, origin, radius, output); }
        catch (ArgumentException) { invalidVertexRejected = true; }
        Check(invalidVertexRejected, "Nonfinite mesh data is rejected.");

        var random = new Random(75041);
        // Arbitrarily oriented large triangles test the entire spherical bound,
        // including head crossings where every original vertex is far away.
        for (var plane = 0; plane < 48; plane++)
        {
            var direction = Vector3.Normalize(new Vector3((float)random.NextDouble() * 2 - 1,
                (float)random.NextDouble() * 2 - 1, (float)random.NextDouble() * 2 - 1));
            var u = Vector3.Normalize(Vector3.Cross(direction, Math.Abs(direction.Y) < .9f ? Vector3.UnitY : Vector3.UnitZ));
            var v = Vector3.Cross(direction, u);
            var z = (float)random.NextDouble() * radius * .95f;
            var p = direction * z;
            var pa = p - u * .5f - v * .5f;
            var pb = p + u * .5f - v * .5f;
            var pc = p + v;
            Check(clipper.ClipTriangle(pa, pb, pc, origin, radius, output), "Intersected arbitrary planes are clipped.");
            ValidateOutput(pa, pb, pc, output, Check);
            for (var sample = 0; sample < 48; sample++)
            {
                var angle = sample * MathF.PI / 24;
                var radial = u * MathF.Cos(angle) + v * MathF.Sin(angle);
                var innerPlanar = MathF.Sqrt(MathF.Max(0, MathF.Pow(radius * SeatClipGeometry.InnerRadiusScale * .99f, 2) - z * z));
                var inner = p + radial * innerPlanar;
                var outerPlanar = MathF.Sqrt(radius * radius - z * z) + .0005f;
                var outer = p + radial * outerPlanar;
                Check(!Contains(output, Barycentric(inner, pa, pb, pc)), "The guaranteed inner clearance is fully removed.");
                Check(Contains(output, Barycentric(outer, pa, pb, pc)), "Seat surface beyond the requested radius always survives.");
            }
        }

        var farA = new Vector3(-.25f, -.8f, .3f);
        var farB = new Vector3(.25f, -.8f, .3f);
        var farC = new Vector3(0, -.7f, .35f);
        for (var i = 0; i < 20; i++) clipper.ClipTriangle(a, b, c, origin, radius, output);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++) clipper.ClipTriangle(a, b, c, origin, radius, output);
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Repeated clipping allocates no managed memory after buffers warm up.");
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
            for (var face = 0; face < 4000; face++)
                clipper.ClipTriangle(farA, farB, farC, origin, radius, output);
        watch.Stop();
        Console.WriteLine($"Seat geometry: radius guarantee {SeatClipGeometry.InnerRadiusScale:P2}; 4,000 far faces {watch.Elapsed.TotalMilliseconds / 100:F3} ms/update.");
        BenchmarkIntersections(clipper, output, radius);
        passed += checkedCount;
    }

    private static void BenchmarkIntersections(SeatClipGeometry clipper, List<Vector3> output, float radius)
    {
        // A seat-sized patch includes fully removed, split and unaffected
        // triangles. Report timings without binding correctness to CPU speed.
        var patch = new List<Vector3>(600);
        for (var row = 0; row < 5; row++)
            for (var col = 0; col < 20; col++)
            {
                var a = new Vector3(-.2f + col * .02f, -.15f + row * .06f, 0);
                var b = a + new Vector3(.02f, 0, 0);
                var c = a + new Vector3(0, .06f, 0);
                var d = a + new Vector3(.02f, .06f, 0);
                patch.AddRange(new[] { a, b, c, b, d, c });
            }
        var farA = new Vector3(-.25f, -.8f, .3f);
        var farB = new Vector3(.25f, -.8f, .3f);
        var farC = new Vector3(0, -.7f, .35f);
        foreach (var includeFar in new[] { false, true })
        {
            var samples = new double[200];
            for (var iteration = -20; iteration < samples.Length; iteration++)
            {
                var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < patch.Count; i += 3)
                    clipper.ClipTriangle(patch[i], patch[i + 1], patch[i + 2], Vector3.Zero, radius, output);
                if (includeFar)
                    for (var face = 0; face < 3800; face++)
                        clipper.ClipTriangle(farA, farB, farC, Vector3.Zero, radius, output);
                if (iteration >= 0)
                    samples[iteration] = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            }
            Array.Sort(samples);
            Console.WriteLine($"Seat geometry: {(includeFar ? "4,000 faces including" : "patch of")} 200 near-head faces, p50 {samples[99]:F3} ms / p95 {samples[189]:F3} ms ({samples.Length} updates).");
        }
    }

    private static Vector3 Position(Vector3 barycentric, Vector3 a, Vector3 b, Vector3 c) =>
        a * barycentric.X + b * barycentric.Y + c * barycentric.Z;

    private static Vector3 Barycentric(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        var ab = b - a;
        var ac = c - a;
        var delta = point - a;
        var d00 = Vector3.Dot(ab, ab);
        var d01 = Vector3.Dot(ab, ac);
        var d11 = Vector3.Dot(ac, ac);
        var d20 = Vector3.Dot(delta, ab);
        var d21 = Vector3.Dot(delta, ac);
        var denominator = d00 * d11 - d01 * d01;
        var y = (d11 * d20 - d01 * d21) / denominator;
        var z = (d00 * d21 - d01 * d20) / denominator;
        return new Vector3(1 - y - z, y, z);
    }

    private static bool Contains(List<Vector3> output, Vector3 point)
    {
        for (var i = 0; i < output.Count; i += 3)
        {
            var weights = Barycentric(point, output[i], output[i + 1], output[i + 2]);
            if (weights.X >= -.00001f && weights.Y >= -.00001f && weights.Z >= -.00001f) return true;
        }
        return false;
    }

    private static float Area(Vector3 a, Vector3 b, Vector3 c, List<Vector3> output)
    {
        var area = 0f;
        for (var i = 0; i < output.Count; i += 3)
            area += Vector3.Cross(Position(output[i + 1], a, b, c) - Position(output[i], a, b, c),
                Position(output[i + 2], a, b, c) - Position(output[i], a, b, c)).Length() * .5f;
        return area;
    }

    private static void ValidateOutput(Vector3 a, Vector3 b, Vector3 c, List<Vector3> output, Action<bool, string> check)
    {
        check(output.Count % 3 == 0, "Output consists of complete triangles.");
        check(output.All(weight => float.IsFinite(weight.X) && float.IsFinite(weight.Y) && float.IsFinite(weight.Z) &&
            weight.X >= -.000001f && weight.Y >= -.000001f && weight.Z >= -.000001f &&
            Math.Abs(weight.X + weight.Y + weight.Z - 1) < .000002f), "All interpolated vertices stay on the source face.");
        var originalNormal = Vector3.Cross(b - a, c - a);
        var winding = true;
        for (var i = 0; i < output.Count; i += 3)
            winding &= Vector3.Dot(originalNormal, Vector3.Cross(Position(output[i + 1], a, b, c) - Position(output[i], a, b, c),
                Position(output[i + 2], a, b, c) - Position(output[i], a, b, c))) > 0;
        check(winding, "Split triangles preserve the seat's original front-face winding.");
    }
}
