using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Owns the local sleeve meshes and copied bones. The native pilot and the shared
// hand-material owner are borrowed; neither is destroyed or posed here.
internal sealed class PilotSleeves : IDisposable
{
    private readonly Transform _root;
    private readonly SkinnedMeshRenderer _source;
    private readonly Transform[] _sourceBones;
    private Transform[] _copies = Array.Empty<Transform>();
    private readonly SkinnedMeshRenderer?[] _sleeves = new SkinnedMeshRenderer?[2];
    private readonly PilotSleeveFade?[] _sleeveFades = new PilotSleeveFade?[2];
    private readonly PilotSleeveGeometry?[] _sleeveGeometry = new PilotSleeveGeometry?[2];
    private readonly float[] _sleeveBindRadius = new float[2];
    private readonly ArmPoseMath[] _armPoses = { new(), new() };
    private const int UpperArmSlot = 0, ForearmSlot = 1, HandSlot = 2;
    private readonly int[,] _arm = new int[2, 3];
    private readonly Vector3[,] _restPositions = new Vector3[2, 3];
    private readonly Quaternion[,] _restRotations = new Quaternion[2, 3];
    private readonly List<Mesh> _ownedMeshes = new();
    private readonly List<GameObject> _ownedObjects = new();
    private float _sleeveThickness = -1;

    internal int Count => _sleeves.Count(s => s != null);
    internal SkinnedMeshRenderer? Renderer(int side) => _sleeves[side];

    internal PilotSleeves(Transform root, SkinnedMeshRenderer source, PilotHandMaterials materials,
        BoneWeight[] weights, Vector2[] uv)
    {
        _root = root;
        _source = source;
        _sourceBones = source.bones;
        for (var side = 0; side < 2; side++)
        {
            var suffix = side == 0 ? "_L" : "_R";
            _arm[side, UpperArmSlot] = Array.FindIndex(_sourceBones, b => b.name == "upperarm" + suffix);
            _arm[side, ForearmSlot] = Array.FindIndex(_sourceBones, b => b.name == "forearm" + suffix);
            _arm[side, HandSlot] = Array.FindIndex(_sourceBones, b => b.name == "hand" + suffix);
        }
        try
        {
            Create(source.sharedMesh, weights, uv, materials);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal void ResetPoses()
    {
        foreach (var pose in _armPoses)
            pose.Reset();
    }

    internal void Update(int side, Transform glove, bool tracked, bool enabled,
        bool collective, float transitionProgress)
    {
        var sleeve = _sleeves[side];
        if (sleeve == null)
            return;
        var opacity = enabled && tracked ? SolveArm(side, glove, collective, transitionProgress) : 0;
        if (opacity <= 0)
            _armPoses[side].Reset();
        if (enabled)
            UpdateSleeveBounds(side);
        sleeve.enabled = _sleeveFades[side]?.Apply(opacity) ?? opacity > 0;
    }

    private void Create(Mesh source, BoneWeight[] weights, Vector2[] uv, PilotHandMaterials materials)
    {
        if (source.vertexCount > 20000 || weights.Length != source.vertexCount || uv.Length != source.vertexCount)
            return;
        var vertices = PilotMeshData.ReadVectors(source, VertexAttribute.Position);
        if (vertices == null)
            return;
        var normals = PilotMeshData.ReadVectors(source, VertexAttribute.Normal) ?? Array.Empty<Vector3>();
        var indices = PilotMeshData.Indices(source);
        var points = Array.ConvertAll(vertices, SleeveVector);
        var sourceNormals = Array.ConvertAll(normals, SleeveVector);
        var coordinates = Array.ConvertAll(uv, v => new SleeveVector2(v.x, v.y));
        var skin = Array.ConvertAll(weights, w => new PilotSleeveGeometry.Skin(w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3, w.weight0, w.weight1, w.weight2, w.weight3));
        var scale = _source.transform.lossyScale;
        var metersPerUnit = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        _sleeveThickness = -1;
        _copies = new Transform[_sourceBones.Length + 2];
        for (var i = 0; i < _copies.Length; i++)
        {
            _copies[i] = CreateChild(i < _sourceBones.Length ? "Sleeve " + _sourceBones[i].name :
                i == _sourceBones.Length ? "Sleeve left elbow support" : "Sleeve right elbow support").transform;
        }
        var bindposes = new Matrix4x4[_copies.Length];
        Array.Copy(source.bindposes, bindposes, source.bindposes.Length);
        bindposes[_sourceBones.Length] = bindposes[_sourceBones.Length + 1] = Matrix4x4.identity;

        for (var side = 0; side < 2; side++)
        {
            var upper = _arm[side, UpperArmSlot];
            var fore = _arm[side, ForearmSlot];
            var hand = _arm[side, HandSlot];
            if (upper < 0 || fore < 0 || hand < 0)
                continue;
            for (var joint = 0; joint < 3; joint++)
            {
                var rest = source.bindposes[_arm[side, joint]].inverse;
                _restPositions[side, joint] = rest.MultiplyPoint3x4(Vector3.zero);
                _restRotations[side, joint] = Quaternion.LookRotation(rest.MultiplyVector(Vector3.forward), rest.MultiplyVector(Vector3.up));
            }
            var support = _sourceBones.Length + side;
            bindposes[support] = Matrix4x4.TRS(_restPositions[side, ForearmSlot],
                Quaternion.Slerp(_restRotations[side, UpperArmSlot], _restRotations[side, ForearmSlot], .5f), Vector3.one).inverse;
            var wrist = source.bindposes[hand].inverse;
            var frame = new PilotSleeveGeometry.Frame(SleeveVector(wrist.MultiplyPoint3x4(Vector3.zero)), SleeveVector(wrist.MultiplyVector(Vector3.right)), SleeveVector(wrist.MultiplyVector(Vector3.up)), SleeveVector(wrist.MultiplyVector(Vector3.forward)));
            var geometry = new PilotSleeveGeometry(points, sourceNormals, coordinates, skin, indices, upper, fore, hand, SleeveVector(source.bindposes[upper].inverse.MultiplyPoint3x4(Vector3.zero)), SleeveVector(source.bindposes[fore].inverse.MultiplyPoint3x4(Vector3.zero)), frame, metersPerUnit);
            _sleeveGeometry[side] = geometry;
            _sleeveBindRadius[side] = geometry.Radius;
            var mesh = new Mesh();
            _ownedMeshes.Add(mesh);
            mesh.name = "Fitted pilot sleeve";
            mesh.vertices = Array.ConvertAll(geometry.Positions, UnityVector);
            mesh.normals = Array.ConvertAll(geometry.Normals, UnityVector);
            mesh.uv = Array.ConvertAll(geometry.UV, v => new Vector2(v.X, v.Y));
            mesh.boneWeights = Array.ConvertAll(geometry.Weights, original =>
            {
                var w = original.SupportElbow(upper, fore, hand, support);
                return new BoneWeight { boneIndex0 = w.A, boneIndex1 = w.B, boneIndex2 = w.C, boneIndex3 = w.D, weight0 = w.X, weight1 = w.Y, weight2 = w.Z, weight3 = w.W };
            });
            mesh.bindposes = bindposes;
            mesh.subMeshCount = geometry.Triangles.Length;
            for (var sub = 0; sub < geometry.Triangles.Length; sub++)
                mesh.SetTriangles(geometry.Triangles[sub], sub);
            mesh.RecalculateTangents();
            var go = CreateChild(side == 0 ? "Pilot left sleeve" : "Pilot right sleeve");
            go.layer = (int)LayerHelper.Layers.Cockpit;
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.quality = SkinQuality.Bone4;
            renderer.sharedMesh = mesh;
            renderer.bones = _copies;
            renderer.rootBone = _copies[0];
            renderer.sharedMaterials = Array.ConvertAll(_source.sharedMaterials, m => materials.Sleeve(m));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.localBounds = source.bounds;
            renderer.updateWhenOffscreen = false;
            _sleeves[side] = renderer;
            _sleeveFades[side] = new PilotSleeveFade(renderer.sharedMaterials);
        }
    }

    internal void Resize(float thickness)
    {
        if (_sleeveThickness == thickness)
            return;
        _sleeveThickness = thickness;
        for (var side = 0; side < 2; side++)
        {
            var renderer = _sleeves[side];
            var geometry = _sleeveGeometry[side];
            if (renderer == null || geometry == null)
                continue;
            geometry.Resize(thickness);
            var mesh = renderer.sharedMesh;
            mesh.vertices = Array.ConvertAll(geometry.Positions, UnityVector);
            mesh.normals = Array.ConvertAll(geometry.Normals, UnityVector);
            mesh.RecalculateTangents();
        }
    }

    internal void UpdateBaseline(Vector3 shift)
    {
        // The aircraft can outlive the native pilot renderer during teardown.
        if (_source == null)
            return;
        for (var i = 0; i < _sourceBones.Length; i++)
        {
            _copies[i].position = _sourceBones[i].position + shift;
            _copies[i].rotation = _sourceBones[i].rotation;
            _copies[i].localScale = _sourceBones[i].lossyScale;
        }

        // Keep the native shoulder anchor, but solve from an immutable arm
        // reference. Native seated/head-turn animations must not become new
        // wrist-roll origins or change the IK lengths on the next frame.
        for (var side = 0; side < 2; side++)
        {
            var upper = _arm[side, UpperArmSlot];
            if (upper < 0 || _sleeves[side] == null)
                continue;
            var shoulder = _copies[upper].position;
            for (var joint = 0; joint < 3; joint++)
            {
                var copy = _copies[_arm[side, joint]];
                copy.position = shoulder + _source.transform.TransformVector(_restPositions[side, joint] - _restPositions[side, UpperArmSlot]);
                copy.rotation = _source.transform.rotation * _restRotations[side, joint];
                copy.localScale = _source.transform.lossyScale;
            }
            UpdateElbowSupport(side);
        }

        foreach (var sleeve in _sleeves)
            if (sleeve != null)
            {
                sleeve.transform.position = _source.transform.position + shift;
                sleeve.transform.rotation = _source.transform.rotation;
                sleeve.transform.localScale = _source.transform.lossyScale;
            }
    }

    private static SleeveVector3 SleeveVector(Vector3 value) => new(value.x, value.y, value.z);
    private static Vector3 UnityVector(SleeveVector3 value) => new(value.X, value.Y, value.Z);
    private void UpdateSleeveBounds(int side)
    {
        var sleeve = _sleeves[side];
        if (sleeve == null || _source == null)
            return;
        var u = _arm[side, UpperArmSlot];
        var f = _arm[side, ForearmSlot];
        var h = _arm[side, HandSlot];
        if (u < 0 || f < 0 || h < 0)
            return;
        // Skinned bounds use the root-bone space (Unity's inspector uses
        // actualRootBone.localToWorldMatrix), which can differ from the renderer.
        var space = sleeve.rootBone != null ? sleeve.rootBone : sleeve.transform;
        var matrix = space.worldToLocalMatrix;
        var a = matrix.MultiplyPoint3x4(_copies[u].position);
        var b = matrix.MultiplyPoint3x4(_copies[f].position);
        var c = matrix.MultiplyPoint3x4(_copies[h].position);
        var scale = _source.transform.lossyScale;
        var largest = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        // Include resized clothing, the solver's bounded extension and seam margin.
        var radius = Mathf.Max(.06f, _sleeveBindRadius[side] * largest * Mathf.Max(1, _sleeveThickness) * (1 + ArmReachMath.MaximumStretchRatio + .02f) + .02f);
        if (!SleeveBoundsMath.Axis(a.x, b.x, c.x, matrix.m00, matrix.m01, matrix.m02, radius, out var x, out var sx) || !SleeveBoundsMath.Axis(a.y, b.y, c.y, matrix.m10, matrix.m11, matrix.m12, radius, out var y, out var sy) || !SleeveBoundsMath.Axis(a.z, b.z, c.z, matrix.m20, matrix.m21, matrix.m22, radius, out var z, out var sz))
            return;
        sleeve.localBounds = new Bounds(new Vector3(x, y, z), new Vector3(sx, sy, sz));
    }

    private float SolveArm(int side, Transform glove, bool collective, float transitionProgress)
    {
        var upperIndex = _arm[side, UpperArmSlot];
        var forearmIndex = _arm[side, ForearmSlot];
        var handIndex = _arm[side, HandSlot];
        if (upperIndex < 0 || forearmIndex < 0 || handIndex < 0)
            return 0;
        var upper = _copies[upperIndex];
        var forearm = _copies[forearmIndex];
        var hand = _copies[handIndex];
        var shoulder = upper.position;
        var target = glove.position;
        var upperLength = Vector3.Distance(upper.position, forearm.position);
        var forearmLength = Vector3.Distance(forearm.position, hand.position);
        var delta = target - shoulder;
        var distance = delta.magnitude;
        var reach = ArmReachMath.Evaluate(upperLength, forearmLength, distance);
        if (!reach.Valid || reach.Opacity <= 0 || !TrackedControllers.Finite(target))
            return 0;
        var direction = delta / distance;
        target = shoulder + direction * reach.Distance;
        distance = reach.Distance;
        upperLength *= reach.Scale;
        forearmLength *= reach.Scale;
        upper.localScale *= reach.Scale;
        forearm.localScale *= reach.Scale;
        var localDirection = _root!.InverseTransformDirection(direction);
        var localCuff = _root.InverseTransformDirection(glove.TransformDirection(Vector3.back));
        var bendLocal = _armPoses[side].Bend(
            side == 0, PoseVector(localDirection), PoseVector(localCuff), collective, transitionProgress);
        var bend = _root.TransformDirection(new Vector3(bendLocal.X, bendLocal.Y, bendLocal.Z));
        if (!ArmReachMath.Triangle(upperLength, forearmLength, distance, out var along, out var height))
            return 0;
        var elbow = shoulder + direction * along + bend * height;
        if (!TrackedControllers.Finite(elbow))
            return 0;
        var dorsalLocal = ArmPoseMath.HingeDorsal(side == 0, PoseVector(localDirection), bendLocal);
        var dorsal = _root.TransformDirection(new Vector3(dorsalLocal.X, dorsalLocal.Y, dorsalLocal.Z));
        var upperDirection = (elbow - shoulder).normalized;
        var foreDirection = (target - elbow).normalized;
        upper.rotation = Quaternion.LookRotation(dorsal, upperDirection);
        forearm.position = elbow;
        forearm.rotation = Quaternion.LookRotation(dorsal, foreDirection);
        // The selected pilot rig uses +Y along each arm bone and +Z dorsally.
        // Share cloth roll across both segments: rolling only the forearm
        // blended opposing rotations through the elbow and collapsed its mesh.
        // Axial rotation leaves the solved elbow/wrist positions unchanged.
        // Equal angles also remain continuous through +/-180 and full turns;
        // fractional upper-arm angles would introduce a new wraparound snap.
        var roll = _armPoses[side].ForearmRoll(
            PoseVector(_root.InverseTransformDirection(foreDirection)),
            PoseVector(_root.InverseTransformDirection(forearm.forward)),
            PoseVector(_root.InverseTransformDirection(glove.up)));
        upper.rotation = Quaternion.AngleAxis(roll, upperDirection) * upper.rotation;
        forearm.rotation = Quaternion.AngleAxis(roll, foreDirection) * forearm.rotation;
        hand.SetPositionAndRotation(target, Quaternion.LookRotation(glove.up, glove.forward));
        UpdateElbowSupport(side);
        return reach.Opacity;
    }

    private void UpdateElbowSupport(int side)
    {
        var upper = _copies[_arm[side, UpperArmSlot]];
        var forearm = _copies[_arm[side, ForearmSlot]];
        var support = _copies[_sourceBones.Length + side];
        support.SetPositionAndRotation(forearm.position, Quaternion.Slerp(upper.rotation, forearm.rotation, .5f));
        support.localScale = forearm.localScale;
    }

    private static ArmPoseMath.Vector PoseVector(Vector3 value) => new(value.x, value.y, value.z);

    private GameObject CreateChild(string name)
    {
        var child = new GameObject(name);
        _ownedObjects.Add(child);
        child.transform.SetParent(_root, false);
        return child;
    }

    public void Dispose()
    {
        ResetPoses();
        for (var side = 0; side < 2; side++)
        {
            if (_sleeves[side] != null)
                _sleeves[side]!.enabled = false;
            _sleeves[side] = null;
            _sleeveFades[side] = null;
            _sleeveGeometry[side] = null;
        }
        foreach (var mesh in _ownedMeshes)
            Object.Destroy(mesh);
        _ownedMeshes.Clear();
        foreach (var child in _ownedObjects)
            Object.Destroy(child);
        _ownedObjects.Clear();
        _copies = Array.Empty<Transform>();
        _sleeveThickness = -1;
    }
}
