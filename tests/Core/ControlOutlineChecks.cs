using System.Numerics;
using NOVR.MotionControls;

internal static class ControlOutlineChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        const float width = .002f;
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .000002f;
        var transforms = new[]
        {
            Matrix4x4.Identity,
            Matrix4x4.CreateScale(.025f, .05f, .025f),
            Matrix4x4.CreateScale(.68f, .81f, .55f) * Matrix4x4.CreateFromYawPitchRoll(.63f, -.42f, .17f) * Matrix4x4.CreateTranslation(.027f, -.12f, .044f)
        };
        check("Handle outline has constant physical width through native and fallback scales", () =>
        {
            var vertices = new[] { new Vector3(.1f, .2f, -.3f), new Vector3(-.4f, .3f, .7f) };
            var normals = new[] { Vector3.Normalize(new Vector3(1, 2, 3)), Vector3.UnitY };
            var original = vertices.ToArray();
            foreach (var transform in transforms)
            {
                Matrix4x4.Invert(transform, out var inverse);
                var expanded = ControlOutlineGeometry.Expand(vertices, normals, transform, width);
                for (var i = 0; i < vertices.Length; i++)
                {
                    var offset = Vector3.Transform(expanded[i], transform) - Vector3.Transform(vertices[i], transform);
                    var expected = Vector3.Normalize(Vector3.TransformNormal(normals[i], Matrix4x4.Transpose(inverse))) * width;
                    truth(Near(offset, expected));
                }
            }
            truth(vertices.SequenceEqual(original));
        });
        check("Split hard-edge and UV seam vertices share the same closed outline position", () =>
        {
            var point = new Vector3(.5f, .5f, .5f);
            var vertices = new[] { point, point, point };
            var normals = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
            foreach (var transform in transforms)
            {
                var result = ControlOutlineGeometry.Expand(vertices, normals, transform, width);
                truth(result[0] == result[1] && result[1] == result[2]);
                truth(Math.Abs(Vector3.Distance(Vector3.Transform(result[0], transform), Vector3.Transform(point, transform)) - width) < .000002f);
            }
        });
        check("Outline reflection preserves thickness and front-face winding for either hand", () =>
        {
            var vertices = new[] { new Vector3(.1f, .2f, .3f), new Vector3(.7f, .3f, -.1f), new Vector3(-.3f, .9f, .6f) };
            var normal = Vector3.Normalize(Vector3.Cross(vertices[1] - vertices[0], vertices[2] - vertices[0]));
            var normals = new[] { normal, normal, normal };
            foreach (var transform in transforms)
            {
                var mirror = new ControlMirrorMath(transform);
                var reflected = vertices.Select(mirror.Position).ToArray();
                var reflectedNormals = normals.Select(mirror.Normal).ToArray();
                var expanded = ControlOutlineGeometry.Expand(vertices, normals, transform, width);
                var expected = ControlOutlineGeometry.Expand(reflected, reflectedNormals, transform, width);
                for (var i = 0; i < vertices.Length; i++)
                    truth(Near(mirror.Position(expanded[i]), expected[i]));
                var indices = new[] { 0, 1, 2 };
                ControlMirrorMath.ReverseTriangles(indices);
                var face = Vector3.Normalize(Vector3.Cross(expected[indices[1]] - expected[indices[0]], expected[indices[2]] - expected[indices[0]]));
                truth(Vector3.Dot(face, reflectedNormals[0]) > .9999f);
            }
        });
        check("Outline rejects invalid geometry and leaves zero-length normals stable", () =>
        {
            var vertices = new[] { new Vector3(1, 2, 3) };
            truth(ControlOutlineGeometry.Expand(vertices, new[] { Vector3.Zero }, Matrix4x4.Identity, width)[0] == vertices[0]);
            var rejected = 0;
            foreach (var action in new Action[]
            {
                () => ControlOutlineGeometry.Expand(vertices, Array.Empty<Vector3>(), Matrix4x4.Identity, width),
                () => ControlOutlineGeometry.Expand(vertices, new[] { Vector3.UnitX }, Matrix4x4.CreateScale(0), width),
                () => ControlOutlineGeometry.Expand(vertices, new[] { new Vector3(float.NaN, 0, 0) }, Matrix4x4.Identity, width),
                () => ControlOutlineGeometry.Expand(vertices, new[] { Vector3.UnitX }, Matrix4x4.Identity, float.NaN)
            })
            {
                try
                {
                    action();
                }
                catch (ArgumentException) { rejected++; }
            }
            truth(rejected == 4);
            truth(vertices[0] == new Vector3(1, 2, 3));
        });
    }
}
