using System;
using System.Collections.Generic;
using System.Linq;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Local render-only sleeves. The native animator, pilot and remote players are never modified.
[DefaultExecutionOrder(10000)]
internal sealed class PilotHandVisuals : MonoBehaviour
{
    private static PilotHandVisuals? _instance;
    private Aircraft? _aircraft;
    private Transform? _root;
    private SkinnedMeshRenderer? _source;
    private Transform[] _sourceBones = Array.Empty<Transform>(), _copies = Array.Empty<Transform>();
    private readonly RiggedGlove?[] _gloves = new RiggedGlove?[2];
    private readonly FreeHandStabilizer[] _handStabilizers =
    {
        new(),
        new()
    };
    private readonly ArmPoseMath[] _armPoses =
    {
        new(),
        new()
    };
    private Vector3 _stabilizerSeat, _stabilizerAnchor, _stabilizerTranslation;
    private Quaternion _stabilizerRotation;
    private readonly SkinnedMeshRenderer?[] _sleeves = new SkinnedMeshRenderer?[2];
    private readonly PilotSleeveFade?[] _sleeveFades = new PilotSleeveFade?[2];
    private readonly float[] _sleeveBindRadius = new float[2];
    private const int UpperArmSlot = 0, ForearmSlot = 1, HandSlot = 2;
    private readonly int[,] _arm = new int[2, 3];
    private readonly List<Mesh> _ownedMeshes = new();
    private Material? _fallback;
    private PilotHandMaterials? _materials;
    private float _nextRetry;
    private float _sleeveThickness = -1;
    private readonly PilotSleeveGeometry?[] _sleeveGeometry = new PilotSleeveGeometry?[2];
    private int _attempts;
    internal static bool Visible(bool left) => _instance?._gloves[left ? 0 : 1]?.Root.gameObject.activeInHierarchy == true;
    internal static SkinnedMeshRenderer? GetRenderer(int index) => _instance == null ? null : index switch
    {
        0 or 1 => _instance._gloves[index]?.Renderer,
        2 or 3 => _instance._sleeves[index - 2],
        _ => null
    };
    internal static Vector3 TouchPoint(bool left, ControllerSample sample, Transform tracking, bool fingertip = false)
    {
        var glove = _instance?._gloves[left ? 0 : 1];
        return glove != null ? glove.FreeTouchPoint(sample, fingertip) : tracking.TransformPoint(sample.Position);
    }

    internal static Quaternion HandRotation(bool left, ControllerSample sample, Transform tracking) => _instance?._gloves[left ? 0 : 1]?.Root.rotation ?? tracking.rotation * sample.Rotation;
    internal static Quaternion FreeHandRotation(bool left, ControllerSample sample, Transform tracking) => tracking.rotation * FreeHandPose.Rotation(sample, ModConfiguration.Instance.MotionControls.FreeHandAngles(left));
    private void Awake() => _instance = this;
    private void LateUpdate()
    {
        if (!GameManager.GetLocalAircraft(out var aircraft) || aircraft.cockpit == null || aircraft.cockpit.IsDetached())
        {
            Release();
            return;
        }

        if (_aircraft != aircraft)
        {
            Release();
            _aircraft = aircraft;
            _attempts = 0;
            _nextRetry = 0;
        }

        GameManager.GetLocalFaction(out var faction);
        if (_materials != null && !_materials.Matches(faction))
        {
            ReleaseVisuals();
            _attempts = 0;
            _nextRetry = 0;
        }

        var settings = ModConfiguration.Instance.MotionControls;
        var camera = APIBus.MainCamera;
        var show = settings.RiggedGloves.Value && settings.Enabled.Value &&
            CameraStateManager.cameraMode == CameraMode.cockpit && camera != null &&
            camera.transform.parent != null && TrackedControllers.Available;
        if (!show)
        {
            ResetHandStabilizers();
            if (_root != null)
                _root.gameObject.SetActive(false);
            return;
        }

        if (_root == null)
        {
            if (_attempts >= 3 || Time.unscaledTime < _nextRetry)
                return;
            _attempts++;
            _nextRetry = Time.unscaledTime + 2;
            try
            {
                Create(aircraft, camera!.transform.parent!);
            }
            catch (Exception e)
            {
                ReleaseVisuals();
                Debug.LogWarning("[NOVR] Pilot hand visuals unavailable: " + e.Message);
                return;
            }
        }
        else
            _materials?.Tick(); // Defer texture read/filter/upload beyond the hand-creation frame.
        if (_root!.parent != camera!.transform.parent)
        {
            _root.SetParent(camera.transform.parent, false);
            ResetHandStabilizers();
        }

        _root.localPosition = Vector3.zero;
        _root.localRotation = Quaternion.identity;
        _root.localScale = Vector3.one;
        _root.gameObject.SetActive(true);
        ResetStabilizationAfterRecenter();

        if (_sleeveThickness != settings.SleeveThickness.Value)
            ResizeSleeves(settings.SleeveThickness.Value);
        var controls = MotionFlightController.Instance;
        var armsEnabled = settings.PilotArms.Value;
        if (armsEnabled && _source != null)
            UpdateSleeveBaseline(aircraft, _source);

        for (var side = 0; side < 2; side++)
            UpdateHand(side, controls, armsEnabled);
    }

    private void ResetStabilizationAfterRecenter()
    {
        if (_stabilizerSeat != CockpitProfiles.SeatOffset || _stabilizerAnchor != NOVRHeadsetData.TranslationAnchor ||
            _stabilizerTranslation != NOVRHeadsetData.TranslationCalibrationOffset ||
            _stabilizerRotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            ResetHandStabilizers();
            _stabilizerSeat = CockpitProfiles.SeatOffset;
            _stabilizerAnchor = NOVRHeadsetData.TranslationAnchor;
            _stabilizerTranslation = NOVRHeadsetData.TranslationCalibrationOffset;
            _stabilizerRotation = NOVRHeadsetData.RotationCalibrationOffset;
        }
    }

    private void UpdateSleeveBaseline(Aircraft aircraft, SkinnedMeshRenderer source)
    {
        var lean = PilotTorsoMath.Lean(PoseVector(NOVRHeadsetData.Translation - NOVRHeadsetData.TranslationAnchor));
        var torsoHead = _root!.TransformPoint(
            NOVRHeadsetData.TranslationAnchor + CockpitProfiles.SeatOffset + new Vector3(lean.X, lean.Y, lean.Z));
        // Refresh the native baseline before solving on the first enabled
        // frame too. Hidden sleeves need no bone/renderer transform writes.
        var shift = torsoHead - aircraft.cockpitViewPoint.position;
        for (var i = 0; i < _copies.Length; i++)
        {
            _copies[i].position = _sourceBones[i].position + shift;
            _copies[i].rotation = _sourceBones[i].rotation;
            _copies[i].localScale = _sourceBones[i].lossyScale;
        }

        foreach (var sleeve in _sleeves)
            if (sleeve != null)
            {
                sleeve.transform.position = source.transform.position + shift;
                sleeve.transform.rotation = source.transform.rotation;
                sleeve.transform.localScale = source.transform.lossyScale;
            }
    }

    private void UpdateHand(int side, MotionFlightController? controls, bool armsEnabled)
    {
        var left = side == 0;
        var sample = TrackedControllers.Read(left);
        Pose? attachment = null;
        var stick = false;
        var collective = false;
        if (controls != null && controls.TryGetHeldHandPose(left, out var gripPose, out stick, out collective))
            attachment = gripPose;
        Pose? cornerPinch = null;
        var pointing = false;
        if (CockpitHudWidgets.TryGetCornerHandPose(left, out var corner, out var cornerRotation))
        {
            // Corner targets are the midpoint of the posed thumb/index
            // tips. The glove's existing attachment blend seats them at
            // the live corner without changing raw tracking or hit tests.
            attachment = null;
            stick = collective = pointing = false;
            cornerPinch = new Pose(corner, cornerRotation);
        }
        else if (!attachment.HasValue && (CockpitHudWidgets.AppearancePointing(left) || CockpitMapInteraction.Pointing(left)))
        {
            // Inline sliders use direct fingertip contact. Extend the
            // index without anchoring or moving the tracked wrist.
            pointing = true;
        }

        _gloves[side]?.Pose(sample, attachment, stick, collective, cornerPinch, pointing);
        // Render only, after raw tracking has posed the glove. Contact and
        // grip sockets stay exact; input hit testing still uses the sample.
        if (_gloves[side] != null)
        {
            var freelyTracked = sample.Valid && !attachment.HasValue && !cornerPinch.HasValue &&
                !pointing && !_gloves[side]!.IsTransitioning;
            _handStabilizers[side].Apply(_gloves[side]!.Root, freelyTracked, Time.unscaledDeltaTime);
        }
        if (_sleeves[side] != null)
        {
            var glove = _gloves[side]!.Root;
            var opacity = armsEnabled && sample.Valid ? SolveArm(side, glove, attachment.HasValue && collective) : 0;
            if (opacity <= 0)
                _armPoses[side].Reset();
            if (armsEnabled)
                UpdateSleeveBounds(side);
            _sleeves[side]!.enabled = _sleeveFades[side]?.Apply(opacity) ?? opacity > 0;
        }
    }

    private void Create(Aircraft aircraft, Transform tracking)
    {
        _root = new GameObject("NOVR Pilot Hands").transform;
        _root.SetParent(tracking, false);
        // Pick the pilot nearest this cockpit seat, including multi-seat mod aircraft.
        var candidates = aircraft.pilots
            .Where(p => p != null)
            .SelectMany(p => p.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        _source = candidates
            .Where(r => r.sharedMesh != null &&
                r.bones.Any(b => b != null && b.name == "hand_L") &&
                r.bones.Any(b => b != null && b.name == "head"))
            .OrderBy(r => Vector3.Distance(
                r.bones.First(b => b != null && b.name == "head").position, aircraft.cockpitViewPoint.position))
            .FirstOrDefault();
        var material = _source?.sharedMaterial;
        if (material == null)
            material = _fallback = CockpitMaterials.Opaque("NOVR fallback glove", new Color(.12f, .14f, .12f));
        GameManager.GetLocalFaction(out var faction);
        _materials = new PilotHandMaterials(faction);
        var patches = new[]
        {
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1)
        };
        if (_source != null)
        {
            _sourceBones = _source.bones;
            var mesh = _source.sharedMesh;
            var weights = mesh.boneWeights;
            var uv = PilotMeshData.ReadUv(mesh);
            for (var side = 0; side < 2; side++)
            {
                var suffix = side == 0 ? "_L" : "_R";
                _arm[side, UpperArmSlot] = Array.FindIndex(_sourceBones, b => b.name == "upperarm" + suffix);
                _arm[side, ForearmSlot] = Array.FindIndex(_sourceBones, b => b.name == "forearm" + suffix);
                _arm[side, HandSlot] = Array.FindIndex(_sourceBones, b => b.name == "hand" + suffix);
                var hand = _arm[side, HandSlot];
                var min = new Vector2(1, 1);
                var max = Vector2.zero;
                var found = false;
                for (var i = 0; i < uv.Length; i++)
                    if (Weight(weights[i], hand) > .8f)
                    {
                        min = Vector2.Min(min, uv[i]);
                        max = Vector2.Max(max, uv[i]);
                        found = true;
                    }

                if (found)
                {
                    var margin = (max - min) * .12f;
                    patches[side] = Rect.MinMaxRect(min.x + margin.x, min.y + margin.y, max.x - margin.x, max.y - margin.y);
                }
            }

            try
            {
                CreateSleeves(mesh, weights, uv);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NOVR] Pilot sleeves unavailable; tracked gloves remain available: " + e.Message);
            }
        }

        for (var side = 0; side < 2; side++)
            _gloves[side] = new RiggedGlove(side == 0, _root, _materials.Glove(material, patches[side]), patches[side]);
        Debug.Log("[NOVR] Rigged Valve gloves ready; pilot material=" + material.name + ", sleeve renderers=" + _sleeves.Count(s => s != null) + ". Controller grip/trigger pose fingers; no finger-tracking claim.");
    }

    private void CreateSleeves(Mesh source, BoneWeight[] weights, Vector2[] uv)
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
        var scale = _source!.transform.lossyScale;
        var metersPerUnit = Mathf.Max(Mathf.Abs(scale.x), Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
        _sleeveThickness = -1;
        _copies = new Transform[_sourceBones.Length];
        for (var i = 0; i < _copies.Length; i++)
        {
            _copies[i] = new GameObject("Sleeve " + _sourceBones[i].name).transform;
            _copies[i].SetParent(_root, false);
        }

        for (var side = 0; side < 2; side++)
        {
            var upper = _arm[side, UpperArmSlot];
            var fore = _arm[side, ForearmSlot];
            var hand = _arm[side, HandSlot];
            if (upper < 0 || fore < 0 || hand < 0)
                continue;
            var wrist = source.bindposes[hand].inverse;
            var frame = new PilotSleeveGeometry.Frame(SleeveVector(wrist.MultiplyPoint3x4(Vector3.zero)), SleeveVector(wrist.MultiplyVector(Vector3.right)), SleeveVector(wrist.MultiplyVector(Vector3.up)), SleeveVector(wrist.MultiplyVector(Vector3.forward)));
            var geometry = new PilotSleeveGeometry(points, sourceNormals, coordinates, skin, indices, upper, fore, hand, SleeveVector(source.bindposes[upper].inverse.MultiplyPoint3x4(Vector3.zero)), SleeveVector(source.bindposes[fore].inverse.MultiplyPoint3x4(Vector3.zero)), frame, metersPerUnit);
            _sleeveGeometry[side] = geometry;
            _sleeveBindRadius[side] = geometry.Radius;
            var mesh = new Mesh
            {
                name = "Fitted pilot sleeve",
                vertices = Array.ConvertAll(geometry.Positions, UnityVector),
                normals = Array.ConvertAll(geometry.Normals, UnityVector),
                uv = Array.ConvertAll(geometry.UV, v => new Vector2(v.X, v.Y)),
                boneWeights = Array.ConvertAll(geometry.Weights, w => new BoneWeight { boneIndex0 = w.A, boneIndex1 = w.B, boneIndex2 = w.C, boneIndex3 = w.D, weight0 = w.X, weight1 = w.Y, weight2 = w.Z, weight3 = w.W }),
                bindposes = source.bindposes,
                subMeshCount = geometry.Triangles.Length
            };
            _ownedMeshes.Add(mesh);
            for (var sub = 0; sub < geometry.Triangles.Length; sub++)
                mesh.SetTriangles(geometry.Triangles[sub], sub);
            mesh.RecalculateTangents();
            var go = new GameObject(side == 0 ? "Pilot left sleeve" : "Pilot right sleeve");
            go.transform.SetParent(_root, false);
            go.layer = (int)LayerHelper.Layers.Cockpit;
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = _copies;
            renderer.rootBone = _copies[0];
            renderer.sharedMaterials = Array.ConvertAll(_source.sharedMaterials, m => _materials!.Sleeve(m));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.localBounds = source.bounds;
            renderer.updateWhenOffscreen = false;
            _sleeves[side] = renderer;
            _sleeveFades[side] = new PilotSleeveFade(renderer.sharedMaterials);
        }
    }

    private void ResizeSleeves(float thickness)
    {
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

    private float SolveArm(int side, Transform glove, bool collective)
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
            side == 0, PoseVector(localDirection), PoseVector(localCuff), collective, _gloves[side]!.TransitionProgress);
        var bend = _root.TransformDirection(new Vector3(bendLocal.X, bendLocal.Y, bendLocal.Z));
        if (!ArmReachMath.Triangle(upperLength, forearmLength, distance, out var along, out var height))
            return 0;
        var elbow = shoulder + direction * along + bend * height;
        if (!TrackedControllers.Finite(elbow))
            return 0;
        var upperSwing = Quaternion.FromToRotation(forearm.position - shoulder, elbow - shoulder);
        var foreSwing = Quaternion.FromToRotation(hand.position - forearm.position, target - elbow);
        upper.rotation = upperSwing * upper.rotation;
        forearm.position = elbow;
        forearm.rotation = foreSwing * forearm.rotation;
        // The selected pilot rig uses +Y along each arm bone and +Z dorsally.
        // Share cloth roll across both segments: rolling only the forearm
        // blended opposing rotations through the elbow and collapsed its mesh.
        // Axial rotation leaves the solved elbow/wrist positions unchanged.
        // Equal angles also remain continuous through +/-180 and full turns;
        // fractional upper-arm angles would introduce a new wraparound snap.
        var foreDirection = (target - elbow).normalized;
        var roll = _armPoses[side].ForearmRoll(
            PoseVector(_root.InverseTransformDirection(foreDirection)),
            PoseVector(_root.InverseTransformDirection(forearm.forward)),
            PoseVector(_root.InverseTransformDirection(glove.up)));
        upper.rotation = Quaternion.AngleAxis(roll, (elbow - shoulder).normalized) * upper.rotation;
        forearm.rotation = Quaternion.AngleAxis(roll, foreDirection) * forearm.rotation;
        hand.SetPositionAndRotation(target, Quaternion.LookRotation(glove.up, glove.forward));
        return reach.Opacity;
    }

    private static ArmPoseMath.Vector PoseVector(Vector3 value) => new(value.x, value.y, value.z);
    private static float Weight(BoneWeight w, int index) => (w.boneIndex0 == index ? w.weight0 : 0) + (w.boneIndex1 == index ? w.weight1 : 0) + (w.boneIndex2 == index ? w.weight2 : 0) + (w.boneIndex3 == index ? w.weight3 : 0);
    private void ResetHandStabilizers()
    {
        for (var side = 0; side < 2; side++)
        {
            _handStabilizers[side].Reset();
            _armPoses[side].Reset();
            _gloves[side]?.ResetAttachmentTransition();
        }
    }

    private sealed class FreeHandStabilizer
    {
        private bool _ready;
        private Vector3 _position, _previousPosition;
        private Quaternion _rotation, _previousRotation;
        internal void Reset() => _ready = false;
        internal void Apply(Transform hand, bool free, float elapsed)
        {
            if (!free)
            {
                Reset();
                return;
            }

            var position = hand.localPosition;
            var rotation = hand.localRotation;
            if (!TrackedControllers.Finite(position) || !FlightControlMath.IsFinite(rotation.x) || !FlightControlMath.IsFinite(rotation.y) || !FlightControlMath.IsFinite(rotation.z) || !FlightControlMath.IsFinite(rotation.w) || Quaternion.Dot(rotation, rotation) < .5f)
            {
                Reset();
                return;
            }

            var distance = _ready ? Vector3.Distance(position, _previousPosition) : 0;
            var angle = _ready ? Quaternion.Angle(rotation, _previousRotation) : 0;
            if (!_ready || !FlightControlMath.IsFinite(elapsed) || elapsed <= 0 || elapsed > .1f || distance > .18f || angle > 55f)
            {
                _position = _previousPosition = position;
                _rotation = _previousRotation = rotation;
                _ready = true;
                return;
            }

            // Tracking-space filtering never trails a moving aircraft. Small
            // resting jitter uses 22 ms; a deliberate reach/turn uses only 6 ms.
            var motion = Mathf.Max(Mathf.InverseLerp(.08f, .65f, distance / elapsed), Mathf.InverseLerp(15f, 150f, angle / elapsed));
            var weight = 1 - Mathf.Exp(-elapsed / Mathf.Lerp(.022f, .006f, motion));
            _position = Vector3.Lerp(_position, position, weight);
            _rotation = Quaternion.Slerp(_rotation, rotation, weight);
            _previousPosition = position;
            _previousRotation = rotation;
            hand.localPosition = _position;
            hand.localRotation = _rotation;
        }
    }

    private void ReleaseVisuals()
    {
        ResetHandStabilizers();
        for (var i = 0; i < 2; i++)
        {
            _gloves[i]?.Dispose();
            _gloves[i] = null;
            _sleeves[i] = null;
            _sleeveFades[i] = null;
            _sleeveGeometry[i] = null;
        }

        foreach (var mesh in _ownedMeshes)
            Object.Destroy(mesh);
        _ownedMeshes.Clear();
        _materials?.Dispose();
        _materials = null;
        if (_fallback != null)
            Object.Destroy(_fallback);
        _fallback = null;
        if (_root != null)
            Object.Destroy(_root.gameObject);
        _root = null;
        _source = null;
        _copies = Array.Empty<Transform>();
        _sourceBones = Array.Empty<Transform>();
        _sleeveThickness = -1;
    }

    private void Release()
    {
        ReleaseVisuals();
        _aircraft = null;
    }

    private void OnDisable()
    {
        ResetHandStabilizers();
        if (_root != null)
            _root.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        Release();
        if (_instance == this)
            _instance = null;
    }
}
