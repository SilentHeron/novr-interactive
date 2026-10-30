using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

internal sealed class RiggedGlove : IDisposable
{
    internal sealed class Bone { public string name = ""; public int parent = -1; public float[] position = Array.Empty<float>(); }
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
    private readonly Quaternion[] _open, _closed, _fs20Throttle, _fs20Stick, _throttleThumb, _stickThumb, _stickThumbPress, _fireTouch, _firePress, _switchPinch;
    private readonly int[] _fingers;
    private readonly Mesh _mesh;
    private readonly bool _left;
    private float _grip, _trigger, _thumb, _thumbContact, _indexContact;
    private float _buttonBlend;
    private bool _wasFitted;
    private const float AttachmentEntrySeconds = .12f;
    private readonly Quaternion[] _attachmentFromBones;
    private Vector3 _attachmentFromPosition;
    private Quaternion _attachmentFromRotation;
    private float _attachmentEntryTime;
    private int _attachmentKind;
    private bool _hasRenderedPose, _enteringAttachment;
    private readonly Vector3 _indexRest;
    internal static Asset Load(bool left)
    {
        var index = left ? 0 : 1;
        if (Assets[index] != null) return Assets[index]!;
        using var stream = typeof(RiggedGlove).Assembly.GetManifestResourceStream("NOVR.Glove." + (left ? "left" : "right") + ".json");
        using var reader = new StreamReader(stream ?? throw new InvalidDataException("Missing glove asset."));
        var a = JsonConvert.DeserializeObject<Asset>(reader.ReadToEnd()) ?? throw new InvalidDataException("Empty glove asset.");
        var count = a.vertices.Length / 3;
        if (count < 3 || count > 10000 || a.vertices.Length % 3 != 0 || a.bones.Length != 31 || a.triangles.Length % 3 != 0 ||
            a.uv.Length != count * 2 || a.uv.Any(v => !FlightControlMath.IsFinite(v)) || a.weights.Length != count * 4 || a.indices.Length != count * 4 || a.open.Length != 31 || a.fist.Length != 31 ||
            a.vertices.Any(v => !FlightControlMath.IsFinite(v)) || a.triangles.Any(i => i < 0 || i >= count) ||
            a.indices.Any(i => i < 0 || i >= 31) || a.weights.Any(w => !FlightControlMath.IsFinite(w) || w < 0 || w > 1)) throw new InvalidDataException("Invalid glove geometry.");
        for (var i = 0; i < 31; i++)
            if (a.bones[i].parent < -1 || a.bones[i].parent >= i || a.bones[i].position.Length != 3 || a.bones[i].position.Any(v => !FlightControlMath.IsFinite(v)) ||
                a.open[i].Length != 4 || a.fist[i].Length != 4 || a.open[i].Concat(a.fist[i]).Any(v => !FlightControlMath.IsFinite(v))) throw new InvalidDataException("Invalid glove rig.");
        for (var i = 0; i < count; i++) if (Mathf.Abs(a.weights[i*4]+a.weights[i*4+1]+a.weights[i*4+2]+a.weights[i*4+3]-1) > .001f) throw new InvalidDataException("Invalid glove skin weights.");
        return Assets[index] = a;
    }
    internal RiggedGlove(bool left, Transform parent, Material material, Rect patch)
    {
        _left = left; var asset = Load(left);
        Root = new GameObject(left ? "NOVR Left Glove" : "NOVR Right Glove").transform; Root.SetParent(parent, false);
        _bones = new Transform[asset.bones.Length]; _open = new Quaternion[_bones.Length]; _closed = new Quaternion[_bones.Length]; _fs20Throttle = new Quaternion[_bones.Length]; _fs20Stick = new Quaternion[_bones.Length]; _throttleThumb = new Quaternion[_bones.Length]; _stickThumb = new Quaternion[_bones.Length]; _fingers = new int[_bones.Length];
        _fireTouch = new Quaternion[_bones.Length]; _firePress = new Quaternion[_bones.Length];
        _stickThumbPress = new Quaternion[_bones.Length]; _switchPinch = new Quaternion[_bones.Length];
        _attachmentFromBones = new Quaternion[_bones.Length];
        var bind = new Matrix4x4[_bones.Length];
        for (var i = 0; i < _bones.Length; i++)
        {
            var b = asset.bones[i]; var t = new GameObject(b.name).transform; t.SetParent(b.parent < 0 ? Root : _bones[b.parent], false);
            t.localPosition = V(b.position, 0); _bones[i] = t; bind[i] = t.worldToLocalMatrix * Root.localToWorldMatrix;
            _open[i] = Q(asset.open[i]); _closed[i] = Q(asset.fist[i]);
            _fs20Throttle[i] = Fs20HandPoses.Joint(i, false, left, _open[i], _closed[i]);
            _fs20Stick[i] = Fs20HandPoses.Joint(i, true, left, _open[i], _closed[i]);
            _throttleThumb[i] = Fs20HandPoses.Joint(i, false, left, _open[i], _closed[i], true);
            _stickThumb[i] = Fs20HandPoses.Joint(i, true, left, _open[i], _closed[i], true);
            _switchPinch[i] = Fs20HandPoses.SwitchPinchJoint(i, left, _open[i], _closed[i]);
            if (i >= 2 && i <= 4)
            {
                _stickThumbPress[i] = Fs20HandPoses.HatPressJoint(i, left, _open[i], _closed[i]);
                _fireTouch[i] = Fs20HandPoses.FireButtonJoint(i, left, _open[i], _closed[i], false);
                _firePress[i] = Fs20HandPoses.FireButtonJoint(i, left, _open[i], _closed[i], true);
            }
            _fingers[i] = b.name.Contains("thumb") ? 1 : b.name.Contains("index") ? 2 : b.name.Contains("finger") ? 3 : 0;
        }
        for (var i = 0; i < _bones.Length; i++) _bones[i].localRotation = _open[i];
        _indexRest = Root.InverseTransformPoint(_bones[10].position);
        var vertices = new Vector3[asset.vertices.Length / 3]; var weights = new BoneWeight[vertices.Length]; var uv = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = V(asset.vertices, i * 3); var j = i * 4;
            weights[i] = new BoneWeight { boneIndex0=asset.indices[j],boneIndex1=asset.indices[j+1],boneIndex2=asset.indices[j+2],boneIndex3=asset.indices[j+3],weight0=asset.weights[j],weight1=asset.weights[j+1],weight2=asset.weights[j+2],weight3=asset.weights[j+3] };
            uv[i] = new Vector2(asset.uv[i*2],asset.uv[i*2+1]);
        }
        _mesh = new Mesh { name = "Valve slim glove / pilot material", vertices=vertices, uv=uv, boneWeights=weights, bindposes=bind, triangles=asset.triangles };
        _mesh.RecalculateNormals(); _mesh.RecalculateTangents(); _mesh.RecalculateBounds();
        Renderer = Root.gameObject.AddComponent<SkinnedMeshRenderer>(); Renderer.sharedMesh=_mesh; Renderer.bones=_bones; Renderer.rootBone=_bones[1]; Renderer.sharedMaterial=material;
        Renderer.localBounds = new Bounds(new Vector3(0,0,.06f),Vector3.one*.45f); Renderer.shadowCastingMode=ShadowCastingMode.Off; Renderer.receiveShadows=true; Renderer.updateWhenOffscreen=false;
        LayerHelper.SetLayerRecursive(Root, LayerHelper.Layers.Cockpit);
    }
    // Compute interaction points from the real tracked pose, even when the
    // rendered glove is snapped to a switch. Visual attachment never feeds back
    // into the input used to move/release that switch.
    internal Vector3 FreeTouchPoint(ControllerSample sample, bool fingertip)
    {
        var settings = ModConfiguration.Instance.MotionControls;
        var offset = settings.FreeHandOffset(_left);
        var angles = settings.FreeHandAngles(_left);
        var tip = fingertip ? _indexRest : new Vector3(0,-.019f,.043f);
        return Root.parent.TransformPoint(sample.Position + sample.Rotation * offset + sample.Rotation * Quaternion.Euler(angles) * tip);
    }
    internal void ResetAttachmentTransition()
    {
        _attachmentKind = 0;
        _hasRenderedPose = _enteringAttachment = false;
        _wasFitted = false;
        _buttonBlend = 0;
    }
    internal void Pose(ControllerSample sample, Pose? attachment, bool stick, bool collective, Pose? cockpitGrip = null, bool pointing = false, bool pinching = false)
    {
        Root.gameObject.SetActive(sample.Valid);
        if (!sample.Valid) { ResetAttachmentTransition(); return; }
        var settings=ModConfiguration.Instance.MotionControls;
        var offset=settings.FreeHandOffset(_left);
        var angles=settings.FreeHandAngles(_left);
        var trackedRotation = sample.Rotation * Quaternion.Euler(angles);
        var held = attachment.HasValue;
        var buttonContact = cockpitGrip.HasValue && pointing && !pinching && !held;
        var attachmentKind = cockpitGrip.HasValue && !buttonContact ? pinching ? 4 : 5
            : held ? stick ? 1 : collective ? 3 : 2 : 0;
        if (attachmentKind != _attachmentKind)
        {
            _attachmentKind = attachmentKind;
            _enteringAttachment = attachmentKind != 0 && _hasRenderedPose;
            if (_enteringAttachment)
            {
                // Capture the actual last rendered pose, including free-hand
                // stabilization. Tracking-local coordinates travel with the
                // aircraft, so acquisition never leaves the wrist trailing it.
                _attachmentFromPosition = Root.localPosition;
                _attachmentFromRotation = Root.localRotation;
                for (var i = 0; i < _bones.Length; i++) _attachmentFromBones[i] = _bones[i].localRotation;
                _attachmentEntryTime = Time.unscaledTime;
            }
        }
        _buttonBlend = buttonContact ? Mathf.MoveTowards(_buttonBlend, 1, Time.unscaledDeltaTime / .09f) : 0;
        var fitted = held && !collective && !cockpitGrip.HasValue && Fs20NativeCockpit.Selected;
        if (cockpitGrip.HasValue && !buttonContact)
        {
            var socket = cockpitGrip.Value;
            Root.SetPositionAndRotation(socket.position - socket.rotation * new Vector3(0,-.019f,.043f), socket.rotation);
        }
        else if (held)
        {
            // Asset axes: fingers +Z, back of hand +Y, thumb toward +/-X.
            // The socket sits between the palm and curled fingers. The old
            // 78 mm offset ran the handle through the finger pads.
            // Collective cuffs point up and inboard toward the shoulder; rolling
            // around the handle axis preserves the finger wrap as it is lifted.
            var grip = attachment!.Value;
            var rotation = grip.rotation * (stick ? Quaternion.Euler(0, 0, _left ? 90 : -90)
                : collective ? Quaternion.Euler(0, 0, _left ? 60 : -60) * Quaternion.Euler(0, _left ? -90 : 90, 0) : Quaternion.Euler(8, 0, 0));
            if (fitted)
                Root.SetPositionAndRotation(grip.position + grip.rotation * Fs20HandPoses.Wrist(stick, _left),
                    grip.rotation * (stick ? Quaternion.Euler(0, 0, _left ? 90 : -90) : Quaternion.identity));
            else Root.SetPositionAndRotation(grip.position - rotation * new Vector3(0, -.019f, .043f), rotation);
        }
        else
        {
            Root.localPosition=sample.Position + sample.Rotation * offset;
            Root.localRotation=trackedRotation;
        }
        var amount=1-Mathf.Exp(-22*Time.unscaledDeltaTime);
        var thumbPressed = sample.Primary || sample.Secondary || sample.Top || sample.Outside || sample.StickClick;
        var thumbTouch = sample.ThumbstickTouched || sample.ButtonTouched || sample.ThumbrestTouched;
        // Capacitive presence only changes visual joints. It never becomes a
        // click, acquisition action, trigger value, or a synthetic flight input.
        _thumbContact = Mathf.Lerp(_thumbContact, thumbPressed ? 1 : thumbTouch ? .85f : 0, amount);
        _indexContact = Mathf.Lerp(_indexContact, sample.Trigger > .02f ? Mathf.Lerp(.85f,1,Mathf.Clamp01(sample.Trigger)) : sample.TriggerTouched ? .85f : 0, amount);
        // Leave space for a roughly 5 cm grip instead of crushing a full fist
        // through the handle. The index moves a little with the real trigger.
        _grip=Mathf.Lerp(_grip,pinching ? .45f : cockpitGrip.HasValue ? .75f : pointing ? .75f : held ? .5f : Mathf.Max(Mathf.Clamp01(sample.Grip), sample.GripTouched ? .32f : 0),amount);
        _trigger=Mathf.Lerp(_trigger,pinching ? .35f : pointing ? 0f : cockpitGrip.HasValue ? .65f : held ? Mathf.Lerp(.48f, .62f, Mathf.Clamp01(sample.Trigger)) : Mathf.Max(Mathf.Clamp01(sample.Trigger), sample.TriggerTouched ? .2f : 0),amount);
        _thumb=Mathf.Lerp(_thumb,pinching ? .45f : pointing ? .7f : cockpitGrip.HasValue ? .85f : held ? .65f : thumbPressed ? 1 : thumbTouch ? .65f : _grip*.3f,amount);
        for (var i = 0; i < _bones.Length; i++)
        {
            var pose = pinching ? _switchPinch[i] : fitted ? (stick ? _fs20Stick[i] : _fs20Throttle[i])
                : Quaternion.Slerp(_open[i], _closed[i], _fingers[i]==1 ? _thumb : _fingers[i]==2 ? _trigger : _fingers[i]==3 ? _grip : 0);
            // Lift clear of the actuator when not touching it; settle onto the
            // fitted contact pose on touch, then the final millimeter on press.
            if (fitted && i >= 2 && i <= 4)
            {
                if (stick)
                {
                    // A rests/presses the release cap. Only actual hat contact
                    // curls toward the hat; unrelated buttons no longer make
                    // the thumb jump into that pose. Trigger still owns index.
                    var target = sample.Primary ? _firePress[i] : sample.PrimaryTouched ? _fireTouch[i]
                        : sample.StickClick ? _stickThumbPress[i] : sample.ThumbstickTouched ? _stickThumb[i] : pose;
                    pose = _wasFitted && !_enteringAttachment ? Quaternion.Slerp(_bones[i].localRotation, target, amount) : target;
                }
                else pose = Quaternion.Slerp(pose, _throttleThumb[i], _thumbContact);
            }
            if (fitted && !stick && i == 2)
                pose = Quaternion.AngleAxis(-10*Mathf.Sin(Mathf.PI*_thumbContact), Vector3.right) * pose;
            if (fitted && i == 7) pose = Quaternion.AngleAxis(-5*(1-_indexContact), Vector3.right) * pose;
            _bones[i].localRotation = pose;
        }
        _wasFitted = fitted;
        if (pinching && cockpitGrip.HasValue)
            Root.position += cockpitGrip.Value.position - (_bones[5].position + _bones[10].position) * .5f;
        if (buttonContact)
        {
            // Solve the pointing fingers first, then align the actual fingertip
            // to the cap. The wrist never occupies the button contact point.
            Root.rotation = Quaternion.Slerp(Root.rotation, cockpitGrip!.Value.rotation, _buttonBlend);
            Root.position += (cockpitGrip.Value.position - _bones[10].position) * _buttonBlend;
        }
        if (_enteringAttachment)
        {
            // Solve the current socket and fitted fingers first, then ease
            // into that moving target once. There is no smoothing or lag once
            // attached, and the button guide retains its own contact blend.
            var progress = Mathf.Clamp01((Time.unscaledTime - _attachmentEntryTime) / AttachmentEntrySeconds);
            if (progress >= 1) _enteringAttachment = false;
            else
            {
                var blend = progress * progress * (3 - 2 * progress);
                Root.localPosition = Vector3.Lerp(_attachmentFromPosition, Root.localPosition, blend);
                Root.localRotation = Quaternion.Slerp(_attachmentFromRotation, Root.localRotation, blend);
                for (var i = 0; i < _bones.Length; i++)
                    _bones[i].localRotation = Quaternion.Slerp(_attachmentFromBones[i], _bones[i].localRotation, blend);
            }
        }
        _hasRenderedPose = true;
    }
    private static Vector3 V(float[] a,int i)=>new(a[i],a[i+1],a[i+2]);
    private static Quaternion Q(float[] a)=>new Quaternion(a[0],a[1],a[2],a[3]).normalized;
    public void Dispose() { if (Root != null) Object.Destroy(Root.gameObject); if (_mesh != null) Object.Destroy(_mesh); }
}
