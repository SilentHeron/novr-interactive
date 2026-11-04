using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

internal sealed class RiggedGlove : IDisposable
{
    internal sealed class Bone
    {
        public string name = "";
        public int parent = -1;
        public float[] position = Array.Empty<float>();
    }

    internal sealed class Asset
    {
        public float[] vertices = Array.Empty<float>(), weights = Array.Empty<float>(), uv = Array.Empty<float>();
        public int[] triangles = Array.Empty<int>(), indices = Array.Empty<int>();
        public Bone[] bones = Array.Empty<Bone>();
        public float[][] open = Array.Empty<float[]>(), fist = Array.Empty<float[]>();
    }

    private static readonly Asset?[] Assets = new Asset?[2];
    internal readonly Transform Root;
    internal readonly SkinnedMeshRenderer Renderer;
    private readonly Transform[] _bones;
    private readonly Quaternion[] _open, _closed, _fs20Throttle, _fs20Stick, _throttleThumb, _stickThumb, _stickThumbPress, _stickTrigger, _fireTouch, _firePress, _cornerPinch;
    private readonly Quaternion[] _collective, _collectiveThumb, _collectiveTrigger;
    private enum FingerGroup
    {
        None,
        Thumb,
        Index,
        Grasping
    }

    private readonly FingerGroup[] _fingers;
    private readonly Mesh _mesh;
    private bool _disposed;
    private readonly bool _left;
    private float _grip, _trigger, _thumb, _thumbContact, _indexContact, _triggerPressure;
    private bool _wasFittedStick;
    private readonly Quaternion[] _animatedStickThumb = new Quaternion[3];
    private const float AttachmentEntrySeconds = .12f, AttachmentReleaseSeconds = .09f;
    private readonly Quaternion[] _attachmentFromBones;
    private Vector3 _attachmentFromPosition;
    private Quaternion _attachmentFromRotation;
    private float _attachmentEntryTime, _attachmentTransitionSeconds;
    private enum AttachmentKind
    {
        Free,
        Stick,
        Throttle,
        Collective,
        CornerPinch,
        Pointing
    }

    private AttachmentKind _attachmentKind;
    private bool _hasRenderedPose, _transitioning;
    private Vector3 _lastTrackedPosition;
    private Quaternion _lastTrackedRotation;
    internal bool IsTransitioning => _transitioning;
    internal float TransitionProgress { get; private set; } = 1;

    private readonly Vector3 _indexRest;
    internal static Asset Load(bool left)
    {
        var index = left ? 0 : 1;
        if (Assets[index] != null)
            return Assets[index]!;
        using var stream = typeof(RiggedGlove).Assembly.GetManifestResourceStream("NOVR.Glove." + (left ? "left" : "right") + ".json");
        using var reader = new StreamReader(stream ?? throw new InvalidDataException("Missing glove asset."));
        var a = JsonConvert.DeserializeObject<Asset>(reader.ReadToEnd()) ?? throw new InvalidDataException("Empty glove asset.");
        var count = a.vertices.Length / 3;
        if (count < 3 || count > 10000 || a.vertices.Length % 3 != 0 || a.bones.Length != GloveBones.Count || a.triangles.Length % 3 != 0 || a.uv.Length != count * 2 || a.uv.Any(v => !FlightControlMath.IsFinite(v)) || a.weights.Length != count * 4 || a.indices.Length != count * 4 || a.open.Length != GloveBones.Count || a.fist.Length != GloveBones.Count || a.vertices.Any(v => !FlightControlMath.IsFinite(v)) || a.triangles.Any(i => i < 0 || i >= count) || a.indices.Any(i => i < 0 || i >= GloveBones.Count) || a.weights.Any(w => !FlightControlMath.IsFinite(w) || w < 0 || w > 1))
            throw new InvalidDataException("Invalid glove geometry.");
        for (var i = 0; i < GloveBones.Count; i++)
            if (a.bones[i].parent < -1 || a.bones[i].parent >= i || a.bones[i].position.Length != 3 || a.bones[i].position.Any(v => !FlightControlMath.IsFinite(v)) || a.open[i].Length != 4 || a.fist[i].Length != 4 || a.open[i].Concat(a.fist[i]).Any(v => !FlightControlMath.IsFinite(v)))
                throw new InvalidDataException("Invalid glove rig.");
        for (var i = 0; i < count; i++)
            if (Mathf.Abs(a.weights[i * 4] + a.weights[i * 4 + 1] + a.weights[i * 4 + 2] + a.weights[i * 4 + 3] - 1) > .001f)
                throw new InvalidDataException("Invalid glove skin weights.");
        return Assets[index] = a;
    }

    internal RiggedGlove(bool left, Transform parent, Material material, Rect patch)
    {
        _left = left;
        var asset = Load(left);
        try
        {
            Root = new GameObject(left ? "NOVR Left Glove" : "NOVR Right Glove").transform;
            Root.SetParent(parent, false);
            _bones = new Transform[asset.bones.Length];
            _open = new Quaternion[_bones.Length];
            _closed = new Quaternion[_bones.Length];
            _fs20Throttle = new Quaternion[_bones.Length];
            _fs20Stick = new Quaternion[_bones.Length];
            _throttleThumb = new Quaternion[_bones.Length];
            _stickThumb = new Quaternion[_bones.Length];
            _fingers = new FingerGroup[_bones.Length];
            _fireTouch = new Quaternion[_bones.Length];
            _firePress = new Quaternion[_bones.Length];
            _stickTrigger = new Quaternion[_bones.Length];
            _stickThumbPress = new Quaternion[_bones.Length];
            _cornerPinch = new Quaternion[_bones.Length];
            _attachmentFromBones = new Quaternion[_bones.Length];
            _collective = new Quaternion[_bones.Length];
            _collectiveThumb = new Quaternion[_bones.Length];
            _collectiveTrigger = new Quaternion[_bones.Length];
            var bind = new Matrix4x4[_bones.Length];
            for (var i = 0; i < _bones.Length; i++)
            {
                var b = asset.bones[i];
                var t = new GameObject(b.name).transform;
                t.SetParent(b.parent < 0 ? Root : _bones[b.parent], false);
                t.localPosition = V(b.position, 0);
                _bones[i] = t;
                bind[i] = t.worldToLocalMatrix * Root.localToWorldMatrix;
                _open[i] = Q(asset.open[i]);
                _closed[i] = Q(asset.fist[i]);
                _fs20Throttle[i] = Fs20HandPoses.Joint(i, false, left, _open[i], _closed[i]);
                _fs20Stick[i] = Fs20HandPoses.Joint(i, true, left, _open[i], _closed[i]);
                _stickTrigger[i] = Fs20HandPoses.Joint(i, true, left, _open[i], _closed[i], triggerPressure: 1);
                _throttleThumb[i] = Fs20HandPoses.Joint(i, false, left, _open[i], _closed[i], true);
                _stickThumb[i] = Fs20HandPoses.Joint(i, true, left, _open[i], _closed[i], true);
                _cornerPinch[i] = Fs20HandPoses.CornerPinchJoint(i, left, _open[i], _closed[i]);
                _collective[i] = CollectiveHandPoses.Joint(i, left, _open[i], _closed[i]);
                _collectiveThumb[i] = CollectiveHandPoses.Joint(i, left, _open[i], _closed[i], thumbContact: true);
                _collectiveTrigger[i] = CollectiveHandPoses.Joint(i, left, _open[i], _closed[i], trigger: true);
                if (GloveBones.IsThumbJoint(i))
                {
                    _stickThumbPress[i] = Fs20HandPoses.HatPressJoint(i, left, _open[i], _closed[i]);
                    _fireTouch[i] = Fs20HandPoses.FireButtonJoint(i, left, _open[i], _closed[i], false);
                    _firePress[i] = Fs20HandPoses.FireButtonJoint(i, left, _open[i], _closed[i], true);
                }

                _fingers[i] = b.name.Contains("thumb") ? FingerGroup.Thumb :
                    b.name.Contains("index") ? FingerGroup.Index :
                    b.name.Contains("finger") ? FingerGroup.Grasping : FingerGroup.None;
            }

            for (var i = 0; i < _bones.Length; i++)
                _bones[i].localRotation = _open[i];
            _indexRest = Root.InverseTransformPoint(_bones[GloveBones.IndexTip].position);
            var vertices = new Vector3[asset.vertices.Length / 3];
            var weights = new BoneWeight[vertices.Length];
            var uv = new Vector2[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i] = V(asset.vertices, i * 3);
                var j = i * 4;
                weights[i] = new BoneWeight
                {
                    boneIndex0 = asset.indices[j],
                    boneIndex1 = asset.indices[j + 1],
                    boneIndex2 = asset.indices[j + 2],
                    boneIndex3 = asset.indices[j + 3],
                    weight0 = asset.weights[j],
                    weight1 = asset.weights[j + 1],
                    weight2 = asset.weights[j + 2],
                    weight3 = asset.weights[j + 3]
                };
                uv[i] = new Vector2(asset.uv[i * 2], asset.uv[i * 2 + 1]);
            }

            // Retain ownership before any upload can throw. Object-initializer
            // assignments would leave a failed mesh outside this owner's cleanup.
            _mesh = new Mesh();
            _mesh.name = "Valve slim glove / pilot material";
            _mesh.vertices = vertices;
            _mesh.uv = uv;
            _mesh.boneWeights = weights;
            _mesh.bindposes = bind;
            _mesh.triangles = asset.triangles;
            _mesh.RecalculateNormals();
            SmoothSkinSeams(vertices, weights);
            _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();
            Renderer = Root.gameObject.AddComponent<SkinnedMeshRenderer>();
            Renderer.sharedMesh = _mesh;
            Renderer.bones = _bones;
            Renderer.rootBone = _bones[GloveBones.Wrist];
            Renderer.sharedMaterial = material;
            Renderer.localBounds = new Bounds(new Vector3(0, 0, .06f), Vector3.one * .45f);
            Renderer.shadowCastingMode = ShadowCastingMode.Off;
            Renderer.receiveShadows = true;
            Renderer.updateWhenOffscreen = false;
            LayerHelper.SetLayerRecursive(Root, LayerHelper.Layers.Cockpit);
        }
        catch
        {
            // A failed constructor is never assigned to PilotHandVisuals.
            // Its hierarchy and standalone mesh must be released here.
            Dispose();
            throw;
        }
    }

    private Quaternion BaseJointPose(int bone, bool pinching, bool fittedCollective, bool fitted, bool stick)
    {
        if (pinching)
            return _cornerPinch[bone];
        if (fittedCollective)
            return _collective[bone];
        if (fitted)
            return stick ? _fs20Stick[bone] : _fs20Throttle[bone];
        var curl = _fingers[bone] switch
        {
            FingerGroup.Thumb => _thumb,
            FingerGroup.Index => _trigger,
            FingerGroup.Grasping => _grip,
            _ => 0
        };
        return Quaternion.Slerp(_open[bone], _closed[bone], curl);
    }

    // Compute interaction points from the real tracked pose, even when the
    // rendered glove is pinching a widget corner. Visual attachment never feeds
    // back into the input used to move or release that corner.
    internal Vector3 FreeTouchPoint(ControllerSample sample, bool fingertip)
    {
        var settings = ModConfiguration.Instance.MotionControls;
        var offset = settings.FreeHandOffset(_left);
        var angles = settings.FreeHandAngles(_left);
        var tip = fingertip ? _indexRest : new Vector3(0, -.019f, .043f);
        return Root.parent.TransformPoint(sample.Position + sample.Rotation * offset + FreeHandPose.Rotation(sample, angles) * tip);
    }

    internal void ResetAttachmentTransition()
    {
        _attachmentKind = AttachmentKind.Free;
        _hasRenderedPose = _transitioning = false;
        _wasFittedStick = false;
        TransitionProgress = 1;
    }

    private static AttachmentKind ChooseAttachment(
        bool pinching, bool held, bool stick, bool collective, bool pointing)
    {
        // Direct widget contact takes priority over handles; free gestures come last.
        if (pinching)
            return AttachmentKind.CornerPinch;
        if (held)
            return stick ? AttachmentKind.Stick : collective ? AttachmentKind.Collective : AttachmentKind.Throttle;
        return pointing ? AttachmentKind.Pointing : AttachmentKind.Free;
    }

    internal void Pose(ControllerSample sample, Pose? attachment, bool stick, bool collective, Pose? cornerPinch = null, bool pointing = false)
    {
        Root.gameObject.SetActive(sample.Valid);
        if (!sample.Valid)
        {
            ResetAttachmentTransition();
            return;
        }

        var settings = ModConfiguration.Instance.MotionControls;
        var offset = settings.FreeHandOffset(_left);
        var angles = settings.FreeHandAngles(_left);
        var trackedRotation = FreeHandPose.Rotation(sample, angles);
        // Do not animate through a tracking teleport or stale-frame recovery.
        // Seat/recenter changes also reset this state through PilotHandVisuals.
        if (_hasRenderedPose && ((sample.Position - _lastTrackedPosition).sqrMagnitude > .18f * .18f || Quaternion.Angle(sample.Rotation, _lastTrackedRotation) > 55 || Time.unscaledDeltaTime > .1f))
            ResetAttachmentTransition();
        var held = attachment.HasValue;
        var pinching = cornerPinch.HasValue;
        var attachmentKind = ChooseAttachment(pinching, held, stick, collective, pointing);
        if (attachmentKind != _attachmentKind)
        {
            _attachmentKind = attachmentKind;
            _transitioning = _hasRenderedPose;
            var attaching = attachmentKind is AttachmentKind.Stick or AttachmentKind.Throttle or
                AttachmentKind.Collective or AttachmentKind.CornerPinch;
            _attachmentTransitionSeconds = attaching ? AttachmentEntrySeconds : AttachmentReleaseSeconds;
            if (_transitioning)
            {
                // Capture the actual last rendered pose, including free-hand
                // stabilization. Tracking-local coordinates travel with the
                // aircraft, so acquisition/release never leaves the wrist trailing it.
                _attachmentFromPosition = Root.localPosition;
                _attachmentFromRotation = Root.localRotation;
                for (var i = 0; i < _bones.Length; i++)
                    _attachmentFromBones[i] = _bones[i].localRotation;
                _attachmentEntryTime = Time.unscaledTime;
            }
        }

        var fitted = held && !collective && !pinching && MotionControlVisuals.HasFittedHandle(stick);
        var fittedCollective = held && collective && !stick && !pinching && MotionControlVisuals.HasFittedCollective;
        if (cornerPinch.HasValue)
        {
            var socket = cornerPinch.Value;
            Root.SetPositionAndRotation(socket.position - socket.rotation * new Vector3(0, -.019f, .043f), socket.rotation);
        }
        else if (held)
        {
            // Asset axes: fingers +Z, back of hand +Y, thumb toward +/-X.
            // The socket sits between the palm and curled fingers. The old
            // 78 mm offset ran the handle through the finger pads.
            // Collective cuffs point up and inboard toward the shoulder; rolling
            // around the handle axis preserves the finger wrap as it is lifted.
            var grip = attachment!.Value;
            var rotation = grip.rotation * (stick ? Quaternion.Euler(0, 0, _left ? 90 : -90) : collective ? Quaternion.Euler(0, 0, _left ? 60 : -60) * Quaternion.Euler(0, _left ? -90 : 90, 0) : Quaternion.Euler(8, 0, 0));
            if (fittedCollective)
                Root.SetPositionAndRotation(grip.position + grip.rotation * CollectiveHandPoses.Wrist(_left), grip.rotation * CollectiveHandPoses.Rotation(_left));
            else if (fitted)
                Root.SetPositionAndRotation(grip.position + grip.rotation * Fs20HandPoses.Wrist(stick, _left), grip.rotation * (stick ? Fs20HandPoses.StickRotation(_left) : Quaternion.identity));
            else
                Root.SetPositionAndRotation(grip.position - rotation * new Vector3(0, -.019f, .043f), rotation);
        }
        else
        {
            Root.localPosition = sample.Position + sample.Rotation * offset;
            Root.localRotation = trackedRotation;
        }

        var amount = 1 - Mathf.Exp(-22 * Time.unscaledDeltaTime);
        var thumbPressed = sample.Primary || sample.Secondary || sample.Top || sample.Outside || sample.StickClick;
        var thumbTouch = sample.ThumbstickTouched || sample.ButtonTouched || sample.ThumbrestTouched;
        // Capacitive presence only changes visual joints. It never becomes a
        // click, acquisition action, trigger value, or a synthetic flight input.
        _thumbContact = Mathf.Lerp(_thumbContact, thumbPressed ? 1 : thumbTouch ? .85f : 0, amount);
        _triggerPressure = Mathf.Lerp(_triggerPressure, Mathf.Clamp01(sample.Trigger), amount);
        _indexContact = Mathf.Lerp(_indexContact, sample.Trigger > .02f ? Mathf.Lerp(.85f, 1, Mathf.Clamp01(sample.Trigger)) : sample.TriggerTouched ? .85f : 0, amount);
        // Leave space for a roughly 5 cm grip instead of crushing a full fist
        // through the handle. The index moves a little with the real trigger.
        _grip = Mathf.Lerp(_grip, pinching ? .45f : pointing ? .75f : held ? .5f : Mathf.Max(Mathf.Clamp01(sample.Grip), sample.GripTouched ? .32f : 0), amount);
        _trigger = Mathf.Lerp(_trigger, pinching ? .35f : pointing ? 0f : held ? Mathf.Lerp(.48f, .62f, Mathf.Clamp01(sample.Trigger)) : Mathf.Max(Mathf.Clamp01(sample.Trigger), sample.TriggerTouched ? .2f : 0), amount);
        _thumb = Mathf.Lerp(_thumb, pinching ? .45f : pointing ? .7f : held ? .65f : thumbPressed ? 1 : thumbTouch ? .65f : _grip * .3f, amount);
        for (var i = 0; i < _bones.Length; i++)
        {
            var pose = BaseJointPose(i, pinching, fittedCollective, fitted, stick);
            if (fittedCollective && i == GloveBones.ThumbDistal)
                pose = Quaternion.Slerp(pose, _collectiveThumb[i], _thumbContact);
            if (fittedCollective && GloveBones.IsIndexJoint(i))
                pose = Quaternion.Slerp(pose, _collectiveTrigger[i], _triggerPressure);
            // Lift clear of the actuator when not touching it; settle onto the
            // fitted contact pose on touch, then the final millimeter on press.
            if (fitted && GloveBones.IsThumbJoint(i))
            {
                if (stick)
                {
                    // A rests/presses the release cap. Only actual hat contact
                    // curls toward the hat; unrelated buttons no longer make
                    // the thumb jump into that pose. Trigger still owns index.
                    var target = sample.Primary ? _firePress[i] : sample.PrimaryTouched ? _fireTouch[i] : sample.StickClick ? _stickThumbPress[i] : sample.ThumbstickTouched ? _stickThumb[i] : pose;
                    // Keep the thumb's own animation separate from the final
                    // attachment blend, so changing targets mid-grab cannot
                    // restart it from an already blended finger pose.
                    pose = _wasFittedStick ? Quaternion.Slerp(_animatedStickThumb[i - GloveBones.ThumbMetacarpal], target, amount) : target;
                    _animatedStickThumb[i - GloveBones.ThumbMetacarpal] = pose;
                }
                else
                    pose = Quaternion.Slerp(pose, _throttleThumb[i], _thumbContact);
            }

            if (fitted && !stick && i == GloveBones.ThumbMetacarpal)
                pose = Quaternion.AngleAxis(-10 * Mathf.Sin(Mathf.PI * _thumbContact), Vector3.right) * pose;
            if (fitted && stick && GloveBones.IsIndexJoint(i))
                pose = Quaternion.Slerp(pose, _stickTrigger[i], _triggerPressure);
            if (fitted && i == GloveBones.IndexProximal)
                pose = Quaternion.AngleAxis(-(stick ? 1f : 5f) * (1 - _indexContact), Vector3.right) * pose;
            _bones[i].localRotation = pose;
        }

        _wasFittedStick = fitted && stick;
        if (cornerPinch.HasValue)
            Root.position += cornerPinch.Value.position - (_bones[GloveBones.ThumbTip].position + _bones[GloveBones.IndexTip].position) * .5f;
        if (_transitioning)
        {
            // Solve the live target first, then ease from the last visible
            // wrist and joints. This also covers release and pointing/pinch
            // changes. The fitted socket is exact after entry.
            var progress = Mathf.Clamp01((Time.unscaledTime - _attachmentEntryTime) / _attachmentTransitionSeconds);
            if (progress >= 1)
            {
                _transitioning = false;
                TransitionProgress = 1;
            }
            else
            {
                var blend = progress * progress * (3 - 2 * progress);
                TransitionProgress = blend;
                Root.localPosition = Vector3.Lerp(_attachmentFromPosition, Root.localPosition, blend);
                Root.localRotation = Quaternion.Slerp(_attachmentFromRotation, Root.localRotation, blend);
                for (var i = 0; i < _bones.Length; i++)
                    _bones[i].localRotation = Quaternion.Slerp(_attachmentFromBones[i], _bones[i].localRotation, blend);
            }
        }

        _lastTrackedPosition = sample.Position;
        _lastTrackedRotation = sample.Rotation;
        _hasRenderedPose = true;
    }

    private void SmoothSkinSeams(Vector3[] vertices, BoneWeight[] weights)
    {
        // The Valve mesh splits vertices at UV islands. RecalculateNormals
        // treats those as hard edges, leaving visible lighting seams. Join only
        // coincident vertices that deform identically and form a smooth surface;
        // keep cuffs/hard folds, UVs and tangent handedness intact.
        var groups = new Dictionary<Vector3, List<int>>();
        for (var i = 0; i < vertices.Length; i++)
        {
            if (!groups.TryGetValue(vertices[i], out var group))
                groups.Add(vertices[i], group = new List<int>());
            group.Add(i);
        }

        var normals = _mesh.normals;
        foreach (var group in groups.Values)
        {
            if (group.Count < 2)
                continue;
            var smooth = true;
            var sum = Vector3.zero;
            for (var i = 0; i < group.Count && smooth; i++)
            {
                var index = group[i];
                sum += normals[index];
                for (var j = 0; j < i; j++)
                    if (!weights[index].Equals(weights[group[j]]) || Vector3.Dot(normals[index], normals[group[j]]) < .5f)
                    {
                        smooth = false;
                        break;
                    }
            }

            if (!smooth || sum.sqrMagnitude < .000001f)
                continue;
            var normal = sum.normalized;
            foreach (var index in group)
                normals[index] = normal;
        }

        _mesh.normals = normals;
    }

    private static Vector3 V(float[] a, int i) => new(a[i], a[i + 1], a[i + 2]);
    private static Quaternion Q(float[] a) => new Quaternion(a[0], a[1], a[2], a[3]).normalized;
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        if (Root != null)
            Object.Destroy(Root.gameObject);
        if (_mesh != null)
            Object.Destroy(_mesh);
    }
}
