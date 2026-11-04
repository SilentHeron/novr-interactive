using System;
using System.Linq;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Local render-only sleeves. The native animator, pilot and remote players are never modified.
[DefaultExecutionOrder(10000)]
internal sealed class PilotHandVisuals : MonoBehaviour
{
    private static PilotHandVisuals? _instance;
    private Aircraft? _aircraft;
    private Transform? _root;
    private PilotSleeves? _sleeves;
    private readonly RiggedGlove?[] _gloves = new RiggedGlove?[2];
    private readonly FreeHandStabilizer[] _handStabilizers =
    {
        new(),
        new()
    };
    private Vector3 _stabilizerSeat, _stabilizerAnchor, _stabilizerTranslation;
    private Quaternion _stabilizerRotation;
    private Material? _fallback;
    private PilotHandMaterials? _materials;
    private float _nextRetry;
    private int _attempts;
    internal static bool Visible(bool left) => _instance?._gloves[left ? 0 : 1]?.Root.gameObject.activeInHierarchy == true;
    internal static SkinnedMeshRenderer? GetRenderer(int index) => _instance == null ? null : index switch
    {
        0 or 1 => _instance._gloves[index]?.Renderer,
        2 or 3 => _instance._sleeves?.Renderer(index - 2),
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

        _sleeves?.Resize(settings.SleeveThickness.Value);
        var controls = MotionFlightController.Instance;
        var armsEnabled = settings.PilotArms.Value;
        if (armsEnabled && _sleeves != null)
        {
            var lean = PilotTorsoMath.Lean(PoseVector(NOVRHeadsetData.Translation - NOVRHeadsetData.TranslationAnchor));
            var torsoHead = _root.TransformPoint(
                NOVRHeadsetData.TranslationAnchor + CockpitProfiles.SeatOffset + new Vector3(lean.X, lean.Y, lean.Z));
            // Refresh the native baseline before solving on the first enabled frame too.
            _sleeves.UpdateBaseline(torsoHead - aircraft.cockpitViewPoint.position);
        }

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
        _sleeves?.Update(side, _gloves[side]!.Root, sample.Valid, armsEnabled,
            attachment.HasValue && collective, _gloves[side]!.TransitionProgress);
    }

    private void Create(Aircraft aircraft, Transform tracking)
    {
        _root = new GameObject("NOVR Pilot Hands").transform;
        _root.SetParent(tracking, false);
        // Pick the pilot nearest this cockpit seat, including multi-seat mod aircraft.
        var candidates = aircraft.pilots
            .Where(p => p != null)
            .SelectMany(p => p.GetComponentsInChildren<SkinnedMeshRenderer>(true));
        var source = candidates
            .Where(r => r.sharedMesh != null &&
                r.bones.Any(b => b != null && b.name == "hand_L") &&
                r.bones.Any(b => b != null && b.name == "head"))
            .OrderBy(r => Vector3.Distance(
                r.bones.First(b => b != null && b.name == "head").position, aircraft.cockpitViewPoint.position))
            .FirstOrDefault();
        var material = source?.sharedMaterial;
        if (material == null)
            material = _fallback = CockpitMaterials.Opaque("NOVR fallback glove", new Color(.12f, .14f, .12f));
        GameManager.GetLocalFaction(out var faction);
        _materials = new PilotHandMaterials(faction);
        var patches = new[]
        {
            new Rect(0, 0, 1, 1),
            new Rect(0, 0, 1, 1)
        };
        if (source != null)
        {
            var sourceBones = source.bones;
            var mesh = source.sharedMesh;
            var weights = mesh.boneWeights;
            var uv = PilotMeshData.ReadUv(mesh);
            for (var side = 0; side < 2; side++)
            {
                var suffix = side == 0 ? "_L" : "_R";
                var hand = Array.FindIndex(sourceBones, b => b.name == "hand" + suffix);
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
                _sleeves = new PilotSleeves(_root, source, _materials, weights, uv);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NOVR] Pilot sleeves unavailable; tracked gloves remain available: " + e.Message);
            }
        }

        for (var side = 0; side < 2; side++)
            _gloves[side] = new RiggedGlove(side == 0, _root, _materials.Glove(material, patches[side]), patches[side]);
        Debug.Log("[NOVR] Rigged Valve gloves ready; pilot material=" + material.name + ", sleeve renderers=" + (_sleeves?.Count ?? 0) + ". Controller grip/trigger pose fingers; no finger-tracking claim.");
    }

    private static ArmPoseMath.Vector PoseVector(Vector3 value) => new(value.x, value.y, value.z);
    private static float Weight(BoneWeight w, int index) => (w.boneIndex0 == index ? w.weight0 : 0) + (w.boneIndex1 == index ? w.weight1 : 0) + (w.boneIndex2 == index ? w.weight2 : 0) + (w.boneIndex3 == index ? w.weight3 : 0);
    private void ResetHandStabilizers()
    {
        _sleeves?.ResetPoses();
        for (var side = 0; side < 2; side++)
        {
            _handStabilizers[side].Reset();
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
        }

        _sleeves?.Dispose();
        _sleeves = null;
        _materials?.Dispose();
        _materials = null;
        if (_fallback != null)
            Object.Destroy(_fallback);
        _fallback = null;
        if (_root != null)
            Object.Destroy(_root.gameObject);
        _root = null;
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
