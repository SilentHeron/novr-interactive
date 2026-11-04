using System.Numerics;
using NOVR.MotionControls;

var checks = 0;
void Check(string label, Action test) { test(); checks++; Console.WriteLine("PASS " + label); }
void Assert(bool value) { if (!value) throw new Exception("Sleeve bounds assertion failed"); }
(bool valid, Vector3 center, Vector3 size) Bounds(Vector3 a, Vector3 b, Vector3 c, Matrix4x4 inverse, float radius)
{
    a = Vector3.Transform(a, inverse);
    b = Vector3.Transform(b, inverse);
    c = Vector3.Transform(c, inverse);
    // Numerics stores basis columns relative to Unity's field naming.
    var x = SleeveBoundsMath.Axis(a.X, b.X, c.X, inverse.M11, inverse.M21, inverse.M31, radius, out var cx, out var sx);
    var y = SleeveBoundsMath.Axis(a.Y, b.Y, c.Y, inverse.M12, inverse.M22, inverse.M32, radius, out var cy, out var sy);
    var z = SleeveBoundsMath.Axis(a.Z, b.Z, c.Z, inverse.M13, inverse.M23, inverse.M33, radius, out var cz, out var sz);
    return (x && y && z, new(cx, cy, cz), new(sx, sy, sz));
}
bool Contains(Vector3 p, Vector3 center, Vector3 size) =>
    Math.Abs(p.X - center.X) <= size.X / 2 + .00001f && Math.Abs(p.Y - center.Y) <= size.Y / 2 + .00001f && Math.Abs(p.Z - center.Z) <= size.Z / 2 + .00001f;

Check("Bent arm includes shoulder, elbow, wrist and clothing instead of a fixed pilot-centered box", () =>
{
    var a = new Vector3(-.2f, 1.4f, 0);
    var b = new Vector3(-.4f, 1.1f, .1f);
    var c = new Vector3(-.3f, 1.1f, .4f);
    var box = Bounds(a, b, c, Matrix4x4.Identity, .12f);
    Assert(box.valid && Contains(a, box.center, box.size) && Contains(b, box.center, box.size) && Contains(c, box.center, box.size));
    Assert(box.size.X >= .44f - .00001f && box.size.Y >= .54f - .00001f && box.size.Z >= .64f - .00001f);
});
Check("Sideways and upward reaches remain inside their own current bounds", () =>
{
    foreach (var wrist in new[] { new Vector3(.6f, .1f, .1f), new Vector3(0, .6f, 0), new Vector3(-.6f, -.1f, .2f) })
    {
        var box = Bounds(Vector3.Zero, new Vector3(.1f, .2f, .1f), wrist, Matrix4x4.Identity, .1f);
        Assert(box.valid && Contains(wrist, box.center, box.size) && box.size.Length() < 1.3f);
    }
});
Check("Root-bone rotation, mirrored scale and nonuniform scale enclose swept clothing", () =>
{
    var random = new Random(88117);
    var total = 0;
    foreach (var scale in new[] { Vector3.One, new Vector3(.01f), new Vector3(100), new Vector3(2, .6f, 1.3f), new Vector3(-1, .8f, 1.7f) })
    {
        var root = Matrix4x4.CreateScale(scale) * Matrix4x4.CreateFromYawPitchRoll(.7f, -.9f, .35f) * Matrix4x4.CreateTranslation(17, -5, 3);
        Assert(Matrix4x4.Invert(root, out var inverse));
        var a = new Vector3(.2f, 1.4f, 0);
        var b = new Vector3(.4f, 1.1f, .2f);
        var c = new Vector3(.35f, 1.1f, .45f);
        var box = Bounds(a, b, c, inverse, .14f);
        Assert(box.valid);
        for (var i = 0; i < 2500; i++)
        {
            var t = (float)random.NextDouble();
            var segment = i % 2 == 0 ? Vector3.Lerp(a, b, t) : Vector3.Lerp(b, c, t);
            var radial = Vector3.Normalize(new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f)) * .14f;
            Assert(Contains(Vector3.Transform(segment + radial, inverse), box.center, box.size));
            total++;
        }
    }
    Assert(total == 12500);
});
Check("Bounds follow a changed pose instead of retaining an older hand location", () =>
{
    var old = Bounds(Vector3.Zero, new(.2f, 0, 0), new(.4f, 0, 0), Matrix4x4.Identity, .08f);
    var next = Bounds(Vector3.Zero, new(0, .2f, 0), new(0, .4f, 0), Matrix4x4.Identity, .08f);
    Assert(next.valid && next.size.Y > old.size.Y && next.size.X < old.size.X && !Contains(new(.4f, 0, 0), next.center, next.size));
});
Check("Larger clothing increases extent without moving the solved arm center", () =>
{
    var a = Bounds(Vector3.Zero, new(.2f, .1f, 0), new(.3f, 0, .2f), Matrix4x4.Identity, .08f);
    var b = Bounds(Vector3.Zero, new(.2f, .1f, 0), new(.3f, 0, .2f), Matrix4x4.Identity, .16f);
    Assert(a.center == b.center && b.size.X > a.size.X && b.size.Y > a.size.Y && b.size.Z > a.size.Z);
});
Check("Malformed coordinates or transforms never create NaN or infinite bounds", () =>
{
    foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        Assert(!SleeveBoundsMath.Axis(bad, 0, 1, 1, 0, 0, .1f, out _, out _));
        Assert(!SleeveBoundsMath.Axis(0, 0, 1, bad, 0, 0, .1f, out _, out _));
        Assert(!SleeveBoundsMath.Axis(0, 0, 1, 1, 0, 0, bad, out _, out _));
    }
    Assert(!SleeveBoundsMath.Axis(0, 0, 1, 1, 0, 0, -.1f, out _, out _));
    Assert(!SleeveBoundsMath.Axis(-float.MaxValue, 0, float.MaxValue, 1, 0, 0, .1f, out _, out _));
    Assert(!SleeveBoundsMath.Axis(0, 0, 0, 0, 0, 0, .1f, out _, out _));
});
Console.WriteLine($"{checks} sleeve bounds checks passed, including 12,500 transformed clothing samples.");
