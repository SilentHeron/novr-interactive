using NOVR.MotionControls;
using UnityEngine;
using U = UnityEngine.Object;

internal static class SleeveResourceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var side in new[] { 1, 2 })
            foreach (var operation in new[] { "vertices", "tangents", "renderer", "sleeve material", "sleeve fade" })
            {
                var source = CreateSource();
                var before = U.Created.Count;
                ResourceFault.Operation = operation;
                ResourceFault.Occurrence = side;
                var failed = false;
                try
                {
                    using var unexpected = source.Create();
                }
                catch (InvalidOperationException error) when (error.Message == "Injected " + operation + " failure")
                {
                    failed = true;
                }
                finally
                {
                    ResourceFault.Operation = null;
                }
                check(failed, $"Sleeve {side} did not exercise failure at {operation}");
                var allocated = U.Created.Skip(before).ToArray();
                check(allocated.OfType<Mesh>().Count() == side, "Failure did not reach the expected first/second sleeve mesh");
                check(allocated.Where(o => o is Mesh or GameObject or SkinnedMeshRenderer).All(o => o.DestroyCount == 1),
                    "Sleeve construction rollback leaked or double-destroyed an owned resource");
                check(source.Root.Children.Count == 0, "Failed sleeves left copied bones or renderers under the borrowed root");
                source.CheckBorrowed(check);

                // Retry on the same native pilot, tracking root and material owner.
                before = U.Created.Count;
                var recovered = source.Create();
                check(recovered.Count == 2 && recovered.Renderer(0) != null && recovered.Renderer(1) != null,
                    "Rollback prevented a complete two-sleeve retry");
                check(recovered.Renderer(0)!.bones.All(bone => !source.Source.bones.Contains(bone)),
                    "Sleeves must pose copied bones rather than the native pilot");
                check(recovered.Renderer(0)!.sharedMaterials.Single() == source.Materials.Copy,
                    "Sleeve renderer lost the borrowed material owner's returned material");
                recovered.Dispose();
                recovered.Dispose();
                check(recovered.Count == 0 && source.Root.Children.Count == 0,
                    "Disposal did not remove both renderers and every copied bone");
                check(U.Created.Skip(before).Where(o => o is Mesh or GameObject or SkinnedMeshRenderer).All(o => o.DestroyCount == 1),
                    "Successful sleeve disposal must be complete and idempotent");
                source.CheckBorrowed(check);
            }

        var baseline = CreateSource();
        using var sleeves = baseline.Create();
        var shift = new Vector3(.12f, -.08f, .04f);
        sleeves.UpdateBaseline(shift);
        var renderer = sleeves.Renderer(0)!;
        check(renderer.quality == SkinQuality.Bone4 && sleeves.Renderer(1)!.quality == SkinQuality.Bone4,
            "Private sleeves must retain all influences even under low global skinning quality");
        for (var i = 0; i < baseline.Source.bones.Length; i++)
        {
            check(Vector3.Distance(renderer.bones[i].position, baseline.Source.bones[i].position + shift) < .00001f,
                "Copied bone baseline did not preserve the native pose plus seat shift");
            check(Quaternion.Angle(renderer.bones[i].rotation, baseline.Source.bones[i].rotation) < .08f,
                "Copied bone baseline changed native orientation");
            check(renderer.bones[i].localScale.Equals(baseline.Source.bones[i].lossyScale),
                "Copied bone baseline lost native scale");
        }
        check(Vector3.Distance(renderer.transform.position, baseline.Source.transform.position + shift) < .00001f,
            "Sleeve renderer baseline lost the native renderer offset");
        var restMesh = renderer.sharedMesh;
        var restWeights = restMesh.boneWeights;
        for (var i = 0; i < restMesh.vertices.Length; i++)
        {
            var w = restWeights[i];
            check(w.weight0 >= w.weight1 && w.weight1 >= w.weight2 && w.weight2 >= w.weight3,
                "Unity sleeve mesh must receive descending bone weights");
            Vector3 Skin(int bone) => (renderer.bones[bone].localToWorldMatrix * restMesh.bindposes[bone]).MultiplyPoint3x4(restMesh.vertices[i]);
            var skinned = Skin(w.boneIndex0) * w.weight0 + Skin(w.boneIndex1) * w.weight1 +
                Skin(w.boneIndex2) * w.weight2 + Skin(w.boneIndex3) * w.weight3;
            check(Vector3.Distance(skinned, baseline.Source.transform.TransformPoint(restMesh.vertices[i]) + shift) < .00001f,
                "Adding elbow support must preserve the fitted sleeve's rest surface exactly");
        }
        var copiedPositions = renderer.bones.Select(b => b.position).ToArray();
        var copiedRotations = renderer.bones.Select(b => b.rotation).ToArray();
        var nativeRotations = baseline.Source.bones.Select(b => b.rotation).ToArray();
        foreach (var index in new[] { 1, 2, 4, 5 })
        {
            baseline.Source.bones[index].rotation = Quaternion.Euler(160, -120, 85);
            baseline.Source.bones[index].position += new Vector3(.12f, -.07f, .19f);
        }
        sleeves.UpdateBaseline(shift);
        for (var i = 0; i < baseline.Source.bones.Length; i++)
        {
            check(Vector3.Distance(renderer.bones[i].position, copiedPositions[i]) < .00001f,
                "Native head-turn/arm animation changed the visual IK reference lengths");
            check(Quaternion.Angle(renderer.bones[i].rotation, copiedRotations[i]) < .08f,
                "Native animation changed the sleeve's wrist-roll reference");
            baseline.Source.bones[i].position = baseline.BonePositions[i];
            baseline.Source.bones[i].rotation = nativeRotations[i];
        }
        sleeves.Resize(.73f);
        var vertices = renderer.sharedMesh.vertices;
        sleeves.Resize(.73f);
        check(ReferenceEquals(vertices, renderer.sharedMesh.vertices), "Unchanged thickness should not rebuild the sleeve mesh");
        baseline.CheckBorrowed(check);

        var glove = new GameObject("borrowed glove").transform;
        glove.position = baseline.Source.bones[2].position + shift;
        sleeves.Update(0, glove, tracked: false, enabled: true, collective: false, transitionProgress: 1);
        check(!renderer.enabled, "Tracking loss must hide the sleeve");
        check(renderer.localBounds.size.x > 0 && renderer.localBounds.size.y > 0 && renderer.localBounds.size.z > 0,
            "Real sleeve bounds must enclose the copied arm even while tracking is absent");
        var supportIndex = baseline.Source.bones.Length;
        check(renderer.bones.Length == baseline.Source.bones.Length + 2 && renderer.sharedMesh.bindposes.Length == renderer.bones.Length,
            "Private elbow supports must have matching owned bones and bind poses");
        foreach (var collective in new[] { false, true })
            foreach (var lift in new[] { -.10f, 0f, .10f })
            {
                glove.position = baseline.Source.bones[0].position + shift + new Vector3(-.12f, -.26f + lift, .24f);
                glove.rotation = collective ? CollectiveHandPoses.Rotation(true) : Quaternion.Euler(0, 0, 90);
                sleeves.UpdateBaseline(shift);
                sleeves.Update(0, glove, tracked: true, enabled: true, collective: collective, transitionProgress: 1);
                var upper = renderer.bones[0];
                var fore = renderer.bones[1];
                var wrist = renderer.bones[2];
                var support = renderer.bones[supportIndex];
                check(Vector3.Distance(support.position, fore.position) < .00001f &&
                    Quaternion.Angle(support.rotation, Quaternion.Slerp(upper.rotation, fore.rotation, .5f)) < .08f,
                    "Elbow support must follow the solved hinge rather than native animation");
                check(Vector3.Dot(upper.up, (fore.position - upper.position).normalized) > .999f &&
                    Vector3.Dot(fore.up, (wrist.position - fore.position).normalized) > .999f,
                    "Both arm frames must follow their solved segment axes through collective travel");
                check(Vector3.Distance(wrist.position, glove.position) < .0001f,
                    "The sleeve correction must preserve a reachable authored glove socket");
                var solved = renderer.bones.Take(3).Select(b => (b.position, b.rotation)).ToArray();
                foreach (var index in new[] { 1, 2 })
                {
                    baseline.Source.bones[index].rotation = Quaternion.Euler(175, 120, -100);
                    baseline.Source.bones[index].position += new Vector3(.08f, .11f, -.07f);
                }
                sleeves.UpdateBaseline(shift);
                sleeves.Update(0, glove, tracked: true, enabled: true, collective: collective, transitionProgress: 1);
                for (var i = 0; i < 3; i++)
                    check(Vector3.Distance(renderer.bones[i].position, solved[i].position) < .00001f &&
                        Quaternion.Angle(renderer.bones[i].rotation, solved[i].rotation) < .08f,
                        "Native head-turn animation cannot leave a stationary tracked arm twisted");
                for (var i = 0; i < baseline.Source.bones.Length; i++)
                {
                    baseline.Source.bones[i].position = baseline.BonePositions[i];
                    baseline.Source.bones[i].rotation = nativeRotations[i];
                }
            }
        var copied = renderer.bones[0].position;
        U.Destroy(baseline.Source);
        sleeves.UpdateBaseline(shift * 2);
        check(renderer.bones[0].position.Equals(copied), "Destroyed native renderer must not supply a new baseline");
    }

    private sealed record SleeveSource(Transform Root, SkinnedMeshRenderer SourceRenderer, PilotHandMaterials Materials,
        Vector3[] Vertices, Vector3[] BonePositions)
    {
        internal SkinnedMeshRenderer Source => SourceRenderer;
        internal PilotSleeves Create() => new(Root, Source, Materials, Source.sharedMesh.boneWeights, Source.sharedMesh.uv);
        internal void CheckBorrowed(Action<bool, string> check)
        {
            check(Root.gameObject.DestroyCount == 0 && Source.DestroyCount == 0 && Source.transform.gameObject.DestroyCount == 0,
                "Sleeve owner destroyed a borrowed tracking root or native pilot");
            check(Source.sharedMesh.DestroyCount == 0 && Source.sharedMaterials[0].DestroyCount == 0 && Materials.Copy.DestroyCount == 0,
                "Sleeve owner destroyed a borrowed source mesh or material");
            check(Source.sharedMesh.vertices.SequenceEqual(Vertices) && Source.bones.Select(b => b.position).SequenceEqual(BonePositions),
                "Sleeve construction, resizing, or disposal mutated native mesh/bone data");
            check(Source.quality == SkinQuality.Bone1, "Private sleeves must not change the borrowed renderer's skinning quality");
        }
    }

    private static SleeveSource CreateSource()
    {
        ResourceFault.Operation = null;
        var root = new GameObject("borrowed tracking root").transform;
        var native = new GameObject("borrowed native pilot").AddComponent<SkinnedMeshRenderer>();
        native.quality = SkinQuality.Bone1;
        native.transform.position = new Vector3(2, 3, 4);
        native.sharedMaterials = new[] { new Material() };
        var bones = new List<Transform>();
        var positions = new List<Vector3>();
        var weights = new List<BoneWeight>();
        var uv = new List<Vector2>();
        var indices = new List<int>();
        var bindposes = new List<Matrix4x4>();
        for (var side = 0; side < 2; side++)
        {
            var x = side == 0 ? -.2f : .2f;
            foreach (var (name, y) in new[] { ("upperarm", 0f), ("forearm", .3f), ("hand", .6f) })
            {
                var bone = new GameObject(name + (side == 0 ? "_L" : "_R")).transform;
                bone.SetParent(native.transform, false);
                bone.localPosition = new Vector3(x, y, 0);
                bones.Add(bone);
                bindposes.Add(Matrix4x4.TRS(bone.localPosition, Quaternion.identity, Vector3.one).inverse);
            }
            var start = positions.Count;
            for (var ring = 0; ring < 2; ring++)
                for (var segment = 0; segment < 8; segment++)
                {
                    var angle = segment * MathF.PI / 4;
                    positions.Add(new(x + .05f * MathF.Cos(angle), ring == 0 ? .05f : .58f, .05f * MathF.Sin(angle)));
                    weights.Add(new BoneWeight { boneIndex0 = side * 3 + ring, weight0 = 1 });
                    uv.Add(new(segment / 8f, ring));
                    var next = (segment + 1) % 8;
                    if (ring == 0)
                        indices.AddRange(new[] { start + segment, start + next, start + 8 + next, start + segment, start + 8 + next, start + 8 + segment });
                }
        }
        native.bones = bones.ToArray();
        native.rootBone = native.bones[0];
        native.sharedMesh = new Mesh
        {
            vertices = positions.ToArray(),
            normals = positions.Select(_ => Vector3.forward).ToArray(),
            uv = uv.ToArray(),
            boneWeights = weights.ToArray(),
            bindposes = bindposes.ToArray(),
            triangles = indices.ToArray(),
            subMeshCount = 1
        };
        return new(root, native, new PilotHandMaterials(), positions.ToArray(), bones.Select(b => b.position).ToArray());
    }
}

namespace NOVR.MotionControls
{
    // Native mesh readback, shared material creation and shader alpha evaluation
    // are boundaries. Resource ownership, mesh fitting and bounds are real code.
    internal static class PilotMeshData
    {
        internal static Vector3[]? ReadVectors(Mesh mesh, UnityEngine.Rendering.VertexAttribute attribute) =>
            attribute == UnityEngine.Rendering.VertexAttribute.Position ? mesh.vertices : mesh.normals;
        internal static int[][] Indices(Mesh mesh) => new[] { mesh.triangles };
    }
    internal sealed class PilotHandMaterials
    {
        internal readonly Material Copy = new();
        internal Material Sleeve(Material source)
        {
            ResourceFault.Check("sleeve material");
            return Copy;
        }
    }
    internal sealed class PilotSleeveFade
    {
        internal PilotSleeveFade(Material[] materials) => ResourceFault.Check("sleeve fade");
        internal bool Apply(float opacity) => opacity > 0;
    }
    internal static class TrackedControllers
    {
        internal static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
