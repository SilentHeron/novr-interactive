using System.Numerics;
using NOVR.MotionControls;

internal static class ControlMirrorChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        static Vector3 Mirror(Vector3 value) => new(-value.X, value.Y, value.Z);
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .00002f;
        var transforms = new[]
        {
            Matrix4x4.Identity,
            Matrix4x4.CreateScale(.025f, .05f, .025f) * Matrix4x4.CreateTranslation(0, .05f, 0),
            Matrix4x4.CreateScale(.68f, .81f, .55f) * Matrix4x4.CreateFromYawPitchRoll(.63f, -.42f, .17f) * Matrix4x4.CreateTranslation(.027f, -.12f, .044f),
            Matrix4x4.CreateScale(-.8f, 1.2f, .5f) * Matrix4x4.CreateRotationZ(.37f) * Matrix4x4.CreateTranslation(-.04f, .02f, -.1f)
        };
        check("Control geometry reflects around the grip frame even with offsets, rotations and unequal scales", () =>
        {
            foreach (var transform in transforms)
            {
                var mirror = new ControlMirrorMath(transform);
                foreach (var vertex in new[] { Vector3.Zero, new Vector3(.23f, .71f, -.41f), new Vector3(-.8f, -.12f, .37f) })
                {
                    var expected = Mirror(Vector3.Transform(vertex, transform));
                    truth(Near(Vector3.Transform(mirror.Position(vertex), transform), expected));
                    truth(Near(mirror.Position(mirror.Position(vertex)), vertex));
                }
            }
        });
        check("Mirrored triangle winding agrees with transformed normals and tangent parity", () =>
        {
            var points = new[] { new Vector3(.1f, .2f, .3f), new Vector3(.7f, .3f, -.1f), new Vector3(-.3f, .9f, .6f) };
            var normal = Vector3.Normalize(Vector3.Cross(points[1] - points[0], points[2] - points[0]));
            var tangent = Vector3.Normalize(points[1] - points[0]);
            foreach (var transform in transforms)
            {
                var mirror = new ControlMirrorMath(transform);
                var reflected = points.Select(mirror.Position).ToArray();
                var indices = new[] { 0, 1, 2 }; ControlMirrorMath.ReverseTriangles(indices);
                var face = Vector3.Normalize(Vector3.Cross(reflected[indices[1]] - reflected[indices[0]], reflected[indices[2]] - reflected[indices[0]]));
                var reflectedNormal = mirror.Normal(normal);
                truth(Vector3.Dot(face, reflectedNormal) > .9999f);
                foreach (var handedness in new[] { -1f, 1f })
                {
                    var reflectedTangent = mirror.Tangent(new Vector4(tangent, handedness));
                    truth(reflectedTangent.W == -handedness);
                    truth(Math.Abs(Vector3.Dot(reflectedNormal, new Vector3(reflectedTangent.X, reflectedTangent.Y, reflectedTangent.Z))) < .00001f);
                }
                ControlMirrorMath.ReverseTriangles(indices);
                truth(indices.SequenceEqual(new[] { 0, 1, 2 }));
                truth(Near(points[0], new Vector3(.1f, .2f, .3f)));
            }
        });
        check("Mirrored handle geometry follows ordinary aircraft-axis animation with a proper grip rotation", () =>
        {
            var native = new ControlMirrorMath(Matrix4x4.CreateTranslation(.04f, .108f, -.02f));
            var point = new Vector3(.02f, .03f, .01f);
            var grip = Quaternion.CreateFromYawPitchRoll(.3f, -.2f, .1f);
            var anchor = new Vector3(-.22f, -.36f, .32f);
            var meshToGrip = Matrix4x4.CreateTranslation(.04f, .108f, -.02f);
            var actual = Vector3.Transform(Vector3.Transform(native.Position(point), meshToGrip), grip) + anchor;
            var expected = Vector3.Transform(Mirror(Vector3.Transform(point, meshToGrip)), grip) + anchor;
            truth(Near(actual, expected));
            truth(Math.Abs(Matrix4x4.CreateFromQuaternion(grip).GetDeterminant() - 1f) < .00001f);
        });
    }
}
