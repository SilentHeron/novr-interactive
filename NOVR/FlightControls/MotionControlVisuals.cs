using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Native aircraft handles are reused when separate meshes are available. Runtime
// primitives remain a fallback for aircraft without reusable control geometry.
internal sealed class MotionControlVisuals : IDisposable
{
    private static MotionControlVisuals? _active;
    internal static bool HasFittedHandle(bool stick) => _active != null && _active.Root != null &&
        _active.Root.gameObject.activeInHierarchy && (stick ? _active._nativeStick : _active._nativeThrottle)?.UsesFittedPose == true;
    internal static bool HasFittedCollective => _active != null && _active.Root != null &&
        _active.Root.gameObject.activeInHierarchy && _active._nativeThrottle?.UsesCollectivePose == true;
    public Transform Root { get; }
    private readonly Transform _stickBase, _stickPivot, _throttleBase, _throttleHandle;
    private readonly Transform _stickHand, _throttleHand, _rail;
    private readonly Transform _sliderParts, _collectiveParts, _collectiveJoint, _collectiveShaft;
    private readonly Transform _milMark;
    private readonly Transform _indicatorTrack, _indicatorFill, _indicatorCursor;
    private readonly TextMesh _throttleReadout;
    private readonly Transform[] _railTicks = new Transform[5];
    private readonly Renderer _stickGrip, _throttleGrip, _collectiveGrip;
    private readonly Material _baseMaterial, _idleMaterial, _hoverMaterial, _heldMaterial, _handMaterial, _textMaterial, _indicatorGrey, _indicatorWhite;
    private readonly Font _font;
    private NativeControlModel? _nativeStick, _nativeThrottle;
    private MirroredControlGeometry? _fallbackStickMirror, _fallbackThrottleMirror;
    private bool _mirrored;
    private readonly List<Renderer> _fallbackStick = new(), _fallbackThrottle = new();
    private float _lastTravel = -1;
    private bool? _lastCollective;
    private float _lastSide;
    private int _lastPercent = -1;
    private string? _lastThrottleState;
    private readonly Dictionary<Mesh, Mesh> _shadedMeshes = new();

    public MotionControlVisuals(Transform trackingRoot)
    {
        Root = Child("NOVR Motion Flight Controls", trackingRoot);
        try
        {
            _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            // GUI/Text Shader ignores depth. UI/Default supports the same alpha
            // font atlas with an explicit per-material depth test.
            var textShader = Shader.Find("UI/Default");
            if (textShader == null) throw new InvalidOperationException("No depth-tested caption shader is available.");
            _textMaterial = new Material(textShader) { name = "NOVR flight caption", color = Color.white };
            _textMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.LessEqual);
            RefreshFontTexture(_font);
            Font.textureRebuilt += RefreshFontTexture;
            _baseMaterial = CockpitMaterials.Opaque("NOVR flight control", new Color(0.12f, 0.15f, 0.19f));
            _idleMaterial = CockpitMaterials.Opaque("NOVR flight control", new Color(0.35f, 0.65f, 0.8f));
            _hoverMaterial = CockpitMaterials.Opaque("NOVR flight control", new Color(1f, 0.75f, 0.18f));
            _heldMaterial = CockpitMaterials.Opaque("NOVR flight control", new Color(0.2f, 1f, 0.5f));
            _handMaterial = CockpitMaterials.Opaque("NOVR flight control", new Color(0.85f, 0.9f, 1f));
            _indicatorGrey = CockpitMaterials.Opaque("NOVR throttle rail grey", new Color(.25f, .25f, .25f));
            _indicatorWhite = CockpitMaterials.Opaque("NOVR throttle rail white", new Color(.9f, .9f, .9f));

            _stickBase = Child("Stick", Root);
            Shape("Base", PrimitiveType.Cylinder, _stickBase, Vector3.zero, new Vector3(0.12f, 0.015f, 0.12f), _baseMaterial);
            _stickPivot = Child("Pivot", _stickBase);
            Shape("Shaft", PrimitiveType.Cylinder, _stickPivot, new Vector3(0, 0.05f, 0), new Vector3(0.025f, 0.05f, 0.025f), _idleMaterial);
            _stickGrip = Shape("Grip", PrimitiveType.Capsule, _stickPivot, new Vector3(0, 0.12f, 0), new Vector3(0.05f, 0.04f, 0.05f), _idleMaterial);
            Shape("Top", PrimitiveType.Sphere, _stickPivot, new Vector3(0, 0.155f, 0), new Vector3(0.025f, 0.012f, 0.025f), _baseMaterial);

            _throttleBase = Child("Throttle", Root);
            _rail = Shape("Rail", PrimitiveType.Cube, _throttleBase, new Vector3(0, -0.05f, 0), new Vector3(0.1f, 0.02f, 0.3f), _baseMaterial).transform;
            _throttleHandle = Child("Handle", _throttleBase);
            _sliderParts = Child("Sliding throttle", _throttleHandle);
            Shape("Post", PrimitiveType.Cube, _sliderParts, new Vector3(0, -0.025f, 0), new Vector3(0.025f, 0.05f, 0.025f), _idleMaterial);
            _throttleGrip = Shape("Grip", PrimitiveType.Cube, _sliderParts, Vector3.zero, new Vector3(0.1f, 0.045f, 0.05f), _idleMaterial);
            _collectiveParts = Child("Collective grip", _throttleHandle);
            _collectiveGrip = Shape("Grip", PrimitiveType.Capsule, _collectiveParts, Vector3.zero,
                new Vector3(.045f, .055f, .045f), _idleMaterial);
            _collectiveGrip.transform.localRotation = Quaternion.Euler(90, 0, 0);
            Shape("Grip head", PrimitiveType.Cube, _collectiveParts, new Vector3(0, .012f, .047f),
                new Vector3(.065f, .035f, .035f), _baseMaterial);
            _collectiveJoint = Shape("Collective hinge", PrimitiveType.Cylinder, _throttleBase, Vector3.zero,
                new Vector3(.065f, .04f, .065f), _baseMaterial).transform;
            _collectiveJoint.localRotation = Quaternion.Euler(0, 0, 90);
            _collectiveShaft = Shape("Collective arm", PrimitiveType.Cylinder, _throttleBase, Vector3.zero,
                new Vector3(.023f, .14f, .023f), _baseMaterial).transform;
            for (var i = 0; i < _railTicks.Length; i++)
                _railTicks[i] = Shape("Travel marker " + i, PrimitiveType.Cube, _throttleBase,
                    Vector3.zero, new Vector3(i % 2 == 0 ? .02f : .013f, .003f, .003f), _indicatorWhite).transform;

            _milMark = Child("MIL detent gate", _throttleBase);
            Shape("Detent notch", PrimitiveType.Cube, _milMark, Vector3.zero, new Vector3(.025f, .004f, .007f), _indicatorWhite);
            var milLabel = Label("MIL", _milMark, new Vector3(0, .0022f, -.011f));
            milLabel.characterSize = .0011f; milLabel.transform.localRotation = Quaternion.Euler(90,0,0);
            _indicatorTrack = Shape("Throttle slider rail", PrimitiveType.Cube, _throttleBase, Vector3.zero,
                Vector3.one, _indicatorGrey).transform;
            _indicatorFill = Shape("Throttle slider fill", PrimitiveType.Cube, _throttleBase, Vector3.zero,
                Vector3.one, _indicatorWhite).transform;
            _indicatorCursor = Shape("Throttle slider cursor", PrimitiveType.Cube, _throttleBase, Vector3.zero,
                new Vector3(.019f, .004f, .008f), _indicatorWhite).transform;
            _throttleReadout = Label("", _throttleBase, Vector3.zero);
            _throttleReadout.characterSize = .00165f;
            _throttleReadout.lineSpacing = 1.1f;
            _stickHand = Shape("Stick Hand", PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one * 0.025f, _handMaterial).transform;
            _throttleHand = Shape("Throttle Hand", PrimitiveType.Sphere, Root, Vector3.zero, Vector3.one * 0.025f, _handMaterial).transform;
            _fallbackStickMirror = new MirroredControlGeometry(_stickBase, _stickBase.GetComponentsInChildren<MeshFilter>(true));
            // Indicators choose the inboard side explicitly and must not be
            // reflected again by the geometry-only handedness mirror.
            var fallbackGeometry = new List<MeshFilter>();
            foreach (var group in new[] { _rail, _sliderParts, _collectiveParts, _collectiveJoint, _collectiveShaft })
                fallbackGeometry.AddRange(group.GetComponentsInChildren<MeshFilter>(true));
            _fallbackThrottleMirror = new MirroredControlGeometry(_throttleBase, fallbackGeometry);
        }
        catch { Dispose(); throw; }
    }

    internal void UseAircraftModels(Aircraft aircraft)
    {
        _fallbackStick.AddRange(_stickPivot.GetComponentsInChildren<Renderer>(true));
        _fallbackStick.Add(_stickBase.Find("Base").GetComponent<Renderer>());
        foreach (var group in new[] { _rail, _sliderParts, _collectiveParts, _collectiveJoint, _collectiveShaft })
            _fallbackThrottle.AddRange(group.GetComponentsInChildren<Renderer>(true));
        _nativeStick = NativeControlModel.TryCreate(aircraft, true, _stickPivot);
        _nativeThrottle = NativeControlModel.TryCreate(aircraft, false, _throttleHandle);
        if (_nativeStick != null) foreach (var renderer in _fallbackStick) renderer.enabled = false;
        if (_nativeThrottle != null) foreach (var renderer in _fallbackThrottle) renderer.enabled = false;
        SetMirrored(ModConfiguration.Instance.MotionControls.LeftHanded.Value);
        _active = this;
    }
    internal void SetVisible(bool visible)
    {
        Root.gameObject.SetActive(visible);
        _nativeStick?.SetVisible(visible); _nativeThrottle?.SetVisible(visible);
    }

    internal Pose GripPose(bool stick) => stick
        ? new Pose(_stickPivot.TransformPoint(Vector3.up * .108f), _stickPivot.rotation)
        : new Pose(_throttleHandle.position, _throttleHandle.rotation);

    private void SetMirrored(bool mirrored)
    {
        if (_mirrored == mirrored) return;
        _mirrored = mirrored;
        _fallbackStickMirror?.SetMirrored(mirrored); _fallbackThrottleMirror?.SetMirrored(mirrored);
        _nativeStick?.SetMirrored(mirrored); _nativeThrottle?.SetMirrored(mirrored);
    }

    public void Update(Vector3 stick, Vector3 throttle, float travel, float angle, float pitch, float roll,
        float thrust, bool stickHeld, bool throttleHeld, bool stickNear, bool throttleNear,
        bool stickTracked, Vector3 stickHand, bool throttleTracked, Vector3 throttleHand,
        bool stickLatched = false, bool throttleLatched = false, bool stickTapMode = false, bool throttleTapMode = false,
        bool brakesApplied = false, string throttleTitle = "THROTTLE", bool collective = false, float yawDegrees = 0f, float milPower = -1f,
        float actualMilPower = -1f)
    {
        SetMirrored(ModConfiguration.Instance.MotionControls.LeftHanded.Value);
        _stickBase.localPosition = stick - Vector3.up * 0.12f;
        var stickRotation = Quaternion.Euler(pitch * angle, 0f, -roll * angle) * Quaternion.Euler(0f, yawDegrees, 0f);
        // Swing the complete grip/socket assembly around a point 4 cm farther
        // down the shaft. The neutral grip and saved layout stay in place, and
        // the fitted glove travels with the handle throughout the arc.
        var pivotDrop = Vector3.up * .04f;
        _stickPivot.localPosition = stickRotation * pivotDrop - pivotDrop;
        _stickPivot.localRotation = stickRotation;
        _throttleBase.localPosition = throttle;
        var side = ThrottleIndicatorMath.InboardSide(stick.x, throttle.x);
        if (_lastTravel != travel || _lastCollective != collective || _lastSide != side)
        {
            _lastTravel = travel;
            _lastCollective = collective;
            _lastSide = side;
            _rail.gameObject.SetActive(!collective);
            _sliderParts.gameObject.SetActive(!collective);
            _collectiveParts.gameObject.SetActive(collective);
            _collectiveJoint.gameObject.SetActive(collective);
            _collectiveShaft.gameObject.SetActive(collective);
            _rail.localScale = new Vector3(0.1f, 0.02f, travel + 0.05f);
            for (var i = 0; i < _railTicks.Length; i++)
            {
                var marker = FlightControlMath.ThrottleOffset(collective, i * .25f, travel);
                _railTicks[i].localPosition = collective
                    ? new Vector3(side * ThrottleIndicatorMath.InboardOffset, marker.height, marker.forward)
                    : new Vector3(side * ThrottleIndicatorMath.InboardOffset, -.016f, marker.forward);
                _railTicks[i].localScale = collective ? new Vector3(i % 2 == 0 ? .02f : .013f, .003f, .012f)
                    : new Vector3(i % 2 == 0 ? .02f : .013f, .003f, .003f);
            }
        }
        var offset = FlightControlMath.ThrottleOffset(collective, milPower > 0 && milPower < 1 ? ThrottleDetent.Position(thrust, milPower, travel) : thrust, travel);
        _throttleHandle.localPosition = new Vector3(0, offset.height, offset.forward);
        _throttleHandle.localRotation = Quaternion.identity;
        if (collective)
        {
            var length = FlightControlMath.CollectiveArmLength(travel);
            var neutralArm = new Vector3(0, FlightControlMath.CollectiveGripHeight, FlightControlMath.CollectiveNeutralForward(travel));
            var pivot = -neutralArm;
            var arm = _throttleHandle.localPosition - pivot;
            _collectiveJoint.localPosition = pivot;
            _collectiveShaft.localPosition = pivot + arm * .5f;
            _collectiveShaft.localRotation = Quaternion.FromToRotation(Vector3.up, arm);
            _collectiveShaft.localScale = new Vector3(.023f, length * .5f, .023f);
            _throttleHandle.localRotation = Quaternion.FromToRotation(neutralArm, arm);
        }
        _nativeStick?.Highlight(stickHeld || CockpitLayout.Held(CockpitLayout.Element.Stick) ? 2 : stickNear ? 1 : 0);
        _nativeThrottle?.Highlight(throttleHeld || CockpitLayout.Held(CockpitLayout.Element.Throttle) ? 2 : throttleNear ? 1 : 0);
        _stickGrip.sharedMaterial = stickHeld ? _heldMaterial : stickNear ? _hoverMaterial : _idleMaterial;
        _throttleGrip.sharedMaterial = throttleHeld ? _heldMaterial : throttleNear ? _hoverMaterial : _idleMaterial;
        _collectiveGrip.sharedMaterial = _throttleGrip.sharedMaterial;
        _stickHand.gameObject.SetActive(stickTracked && !PilotHandVisuals.Visible(ModConfiguration.Instance.MotionControls.LeftHanded.Value));
        _stickHand.localPosition = stickHand;
        _throttleHand.gameObject.SetActive(throttleTracked && !PilotHandVisuals.Visible(!ModConfiguration.Instance.MotionControls.LeftHanded.Value));
        _throttleHand.localPosition = throttleHand;
        _milMark.gameObject.SetActive(!collective && milPower > 0 && milPower < 1);
        _milMark.localPosition = new Vector3(side * ThrottleIndicatorMath.InboardOffset, -.013f, (ThrottleDetent.Position(milPower, milPower, travel) - .5f) * travel);
        UpdateThrottleIndicator(travel, thrust, offset.height, offset.forward, side, collective,
            actualMilPower > 0 ? actualMilPower : milPower, throttleTitle == "HOVER INPUT");
    }

    private void UpdateThrottleIndicator(float travel, float thrust, float height, float forward, float side,
        bool collective, float militaryPower, bool hover)
    {
        var lateral = side * ThrottleIndicatorMath.InboardOffset;
        var idle = FlightControlMath.ThrottleOffset(collective, 0, travel);
        var full = FlightControlMath.ThrottleOffset(collective, 1, travel);
        var start = new Vector3(lateral, collective ? idle.height : -.020f, idle.forward);
        var end = new Vector3(lateral, collective ? full.height : -.020f, full.forward);
        var cursor = new Vector3(lateral, collective ? height : -.016f, forward);
        var axis = end - start;
        _indicatorTrack.localPosition = (start + end) * .5f;
        _indicatorTrack.localRotation = Quaternion.FromToRotation(Vector3.forward, axis);
        _indicatorTrack.localScale = new Vector3(.008f, .004f, axis.magnitude + .008f);
        var fillStart = start + (collective ? Vector3.back : Vector3.up) * .0025f;
        var fillEnd = cursor + (collective ? Vector3.back * .0025f : Vector3.zero);
        var fillAxis = fillEnd - fillStart;
        _indicatorFill.gameObject.SetActive(ThrottleIndicatorMath.Command(thrust) > .001f);
        _indicatorFill.localPosition = (fillStart + fillEnd) * .5f;
        _indicatorFill.localRotation = fillAxis.sqrMagnitude > .0000001f ? Quaternion.FromToRotation(Vector3.forward, fillAxis) : Quaternion.identity;
        _indicatorFill.localScale = new Vector3(.003f, .003f, Mathf.Max(.001f, fillAxis.magnitude));
        _indicatorCursor.localPosition = cursor;
        _indicatorCursor.localRotation = _indicatorTrack.localRotation;
        // Keep the larger two-line readout beside the cursor, with extra inboard
        // clearance from the rail, broad housing and palm in either handedness.
        _throttleReadout.transform.localPosition = cursor + new Vector3(side * .052f, .008f, -.018f);
        _throttleReadout.transform.localRotation = Quaternion.Euler(collective ? 15f : 60f, -side * 15f, 0);
        var percent = ThrottleIndicatorMath.Percent(thrust);
        var state = ThrottleIndicatorMath.State(thrust, militaryPower, collective, hover);
        if (percent != _lastPercent || state != _lastThrottleState)
        {
            _lastPercent = percent; _lastThrottleState = state;
            _throttleReadout.text = percent.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%\n" + state;
        }
    }

    private static Transform Child(string name, Transform parent)
    {
        var child = new GameObject(name);
        child.layer = (int)LayerHelper.Layers.Cockpit;
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private Renderer Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var shape = GameObject.CreatePrimitive(type);
        shape.name = name;
        shape.layer = (int)LayerHelper.Layers.Cockpit;
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = position;
        shape.transform.localScale = scale;
        var collider = shape.GetComponent<Collider>();
        collider.enabled = false;
        Object.Destroy(collider);
        var filter = shape.GetComponent<MeshFilter>();
        var original = filter.sharedMesh;
        if (!_shadedMeshes.TryGetValue(original, out var shaded))
        {
            // Unity primitives share their source meshes with the rest of the game.
            // Shade our own copy and reuse it across matching shapes.
            shaded = Object.Instantiate(original);
            shaded.name = "NOVR shaded " + original.name;
            var normals = shaded.normals;
            var light = new Vector3(-.3f, .6f, -.8f).normalized;
            var colors = new Color[normals.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                var value = .58f + .42f * Mathf.Max(0, Vector3.Dot(normals[i], light));
                colors[i] = new Color(value, value, value, 1);
            }
            shaded.colors = colors;
            _shadedMeshes.Add(original, shaded);
        }
        filter.sharedMesh = shaded;
        var renderer = shape.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void RefreshFontTexture(Font font)
    {
        if (font != _font || _textMaterial == null) return;
        var texture = _font.material.mainTexture;
        _textMaterial.mainTexture = texture;
        // Dynamic fonts are alpha-only: supply white RGB without altering alpha.
        var alphaOnly = texture is Texture2D atlas && atlas.format == TextureFormat.Alpha8;
        _textMaterial.SetVector("_TextureSampleAdd", alphaOnly ? new Vector4(1, 1, 1, 0) : Vector4.zero);
    }

    private TextMesh Label(string value, Transform parent, Vector3 position)
    {
        var text = Child("Label", parent).gameObject.AddComponent<TextMesh>();
        text.text = value;
        text.font = _font;
        text.GetComponent<MeshRenderer>().sharedMaterial = _textMaterial;
        text.fontSize = 40;
        text.characterSize = 0.0048f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.color = Color.white;
        text.transform.localPosition = position;
        text.transform.localRotation = Quaternion.Euler(35f, 0f, 0f);
        return text;
    }

    public void Dispose()
    {
        if (_active == this) _active = null;
        _fallbackStickMirror?.Dispose(); _fallbackThrottleMirror?.Dispose();
        _nativeStick?.Dispose(); _nativeThrottle?.Dispose();
        Font.textureRebuilt -= RefreshFontTexture;
        if (Root != null) Object.Destroy(Root.gameObject);
        foreach (var mesh in _shadedMeshes.Values) Object.Destroy(mesh);
        _shadedMeshes.Clear();
        Object.Destroy(_baseMaterial);
        Object.Destroy(_idleMaterial);
        Object.Destroy(_hoverMaterial);
        Object.Destroy(_heldMaterial);
        Object.Destroy(_handMaterial);
        Object.Destroy(_textMaterial);
        Object.Destroy(_indicatorGrey);
        Object.Destroy(_indicatorWhite);
    }
}
