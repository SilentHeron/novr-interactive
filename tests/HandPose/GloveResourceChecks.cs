using NOVR.MotionControls;
using UnityEngine;
using U = UnityEngine.Object;

internal static class GloveResourceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var left in new[] { false, true })
            foreach (var operation in new[] { "vertices", "normals", "tangents", "renderer" })
            {
                U.Created.Clear();
                var parent = new GameObject("borrowed tracking root").transform;
                var material = new Material();
                ResourceFault.Operation = operation;
                var failed = false;
                try
                {
                    using var unexpected = new RiggedGlove(left, parent, material, default);
                }
                catch (InvalidOperationException error) when (error.Message == "Injected " + operation + " failure")
                {
                    failed = true;
                }
                finally
                {
                    ResourceFault.Operation = null;
                }
                check(failed, "Glove resource failure was not exercised: " + operation);
                var meshes = U.Created.OfType<Mesh>().ToArray();
                check(meshes.Length == 1, "Failure did not reach the owned mesh allocation");
                check(meshes.All(mesh => mesh.DestroyCount == 1), "Failed glove construction leaked its mesh at " + operation);
                check(U.Created.OfType<GameObject>().Where(go => go != parent.gameObject).All(go => go.DestroyCount == 1),
                    "Failed glove construction leaked its hierarchy at " + operation);
                check(parent.Children.Count == 0 && parent.gameObject.DestroyCount == 0 && material.DestroyCount == 0,
                    "Rollback modified the borrowed tracking root or material");

                // A retry must succeed using the same borrowed inputs and cached rig.
                var recovered = new RiggedGlove(left, parent, material, default);
                var mesh = recovered.Renderer.sharedMesh;
                check(mesh.DestroyCount == 0 && recovered.Root.gameObject.DestroyCount == 0,
                    "Successful retry disposed a live glove");
                check(ReferenceEquals(recovered.Renderer.sharedMaterial, material), "Retry changed the borrowed material");
                recovered.Dispose();
                recovered.Dispose();
                check(mesh.DestroyCount == 1 && recovered.Root.gameObject.DestroyCount == 1 && parent.Children.Count == 0,
                    "Successful glove disposal was not complete and idempotent");
                check(parent.gameObject.DestroyCount == 0 && material.DestroyCount == 0,
                    "Normal disposal destroyed borrowed resources");
            }
    }
}
