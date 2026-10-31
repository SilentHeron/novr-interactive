using HarmonyLib;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.MotionControls;

[DefaultExecutionOrder(100)]
internal sealed class OpticalTargetingHud : MonoBehaviour
{
    internal static OpticalTargetingHud? Instance { get; private set; }
    private static readonly AccessTools.FieldRef<TargetCam, Camera> CameraField = AccessTools.FieldRefAccess<TargetCam, Camera>("cam");
    private static readonly AccessTools.FieldRef<TargetCam, TargetCam.CamMode> ModeField = AccessTools.FieldRefAccess<TargetCam, TargetCam.CamMode>("currentMode");
    private readonly OpticalCameraLease _lease = new();
    private Aircraft? _aircraft;
    private Camera? _source, _lastRenderedSource;
    private RenderTexture? _lastRenderedTexture;
    private int _lastRenderedFrame = -10;
    private GameObject? _window, _crosshair;
    private Canvas? _canvas;
    private RawImage? _picture;
    private ZoomTargetingHud? _markers;
    private Text? _status;
    private bool _tracking, _wasEnabled;
    private OpticalHudMode _mode;
    private const float Depth = 3;
    internal static string ModeLabel => Instance?._tracking == true ? "TRACK" : "HEAD";
    internal static bool HeadAimActive => Instance != null && Instance.HeadAvailable();
    internal static bool AimCueVisible => Instance != null && Instance.isActiveAndEnabled &&
        Instance._crosshair != null && Instance._crosshair.activeInHierarchy &&
        Instance._picture != null && Instance._picture.enabled;
    private OpticalHudSettings Settings => ModConfiguration.Instance.OpticalHud;

    private void Awake() => Instance = this;
    private void OnEnable()
    {
        Application.onBeforeRender += BeforeRender;
        RenderPipelineManager.endCameraRendering += CameraRendered;
        RenderPipelineManager.endFrameRendering += EndFrame;
    }
    internal static void LookToAim() { if (Instance != null) Instance._tracking = false; }
    internal static bool CanLookToAim => Instance != null && Instance.isActiveAndEnabled && CockpitReady(out var aircraft) &&
        aircraft.targetCam != null && CameraField(aircraft.targetCam)?.targetTexture != null &&
        CameraField(aircraft.targetCam).gameObject.activeInHierarchy &&
        ModeField(aircraft.targetCam) != TargetCam.CamMode.landingMode;
    internal static bool CanTrackSelected => CanLookToAim && GameManager.GetLocalAircraft(out var aircraft) &&
        aircraft.weaponManager?.GetTargetList().Count > 0;
    internal static bool TryLookToAim()
    {
        if (!CanLookToAim) return false;
        var self = Instance!;
        GameManager.GetLocalAircraft(out var aircraft);
        self.RestoreCamera(); self._aircraft = aircraft;
        self.Settings.Enabled.Value = true; self._wasEnabled = true; self._tracking = false;
        return true;
    }
    internal static bool TryTrackSelected()
    {
        if (!CanTrackSelected) return false;
        var self = Instance!;
        GameManager.GetLocalAircraft(out var aircraft);
        // Return the camera to the game's target tracker; never fabricate a
        // target or keep the temporary head camera pose/FOV in TRACK mode.
        self.RestoreCamera(); self._aircraft = aircraft;
        self.Settings.Enabled.Value = true; self._wasEnabled = true; self._tracking = true;
        return true;
    }
    internal static void Toggle()
    {
        var settings = ModConfiguration.Instance.OpticalHud;
        settings.Enabled.Value = !settings.Enabled.Value; LookToAim();
    }
    internal void RestoreCamera() => _lease.Restore();
    private void EndFrame(ScriptableRenderContext context, Camera[] cameras) => RestoreCamera();
    private void CameraRendered(ScriptableRenderContext context, Camera camera)
    {
        if (camera != _source) return;
        _lastRenderedSource = camera; _lastRenderedTexture = camera.targetTexture; _lastRenderedFrame = Time.frameCount;
    }

    // The native camera is restored (often disabled) between render frames.
    // An MFD can still display its most recent verified HEAD frame; it must not
    // enable/borrow the camera itself or allocate another render texture.
    internal static bool TryGetHeadFeed(Aircraft aircraft, Camera source, out Texture? texture)
    {
        texture = null;
        var self = Instance;
        if (self == null || self._aircraft != aircraft || self._mode != OpticalHudMode.Head ||
            !self.HeadAvailable() || self._lastRenderedSource != source ||
            self._lastRenderedFrame < Time.frameCount - 1 || self._lastRenderedTexture == null ||
            source.targetTexture != self._lastRenderedTexture || !self._lastRenderedTexture.IsCreated()) return false;
        texture = self._lastRenderedTexture;
        return true;
    }

    private static bool CockpitReady(out Aircraft aircraft)
    {
        aircraft = null!;
        if (!GameManager.GetLocalAircraft(out aircraft)) return false;
        if (!XRSettings.enabled || NOUIManager.I == null || !InputDevices.GetDeviceAtXRNode(XRNode.Head).isValid ||
            Time.timeScale <= 0 || !GameManager.flightControlsEnabled || WristMenu.IsOpen || CockpitLayout.Active ||
            CockpitViewZoom.IsActive || DynamicMap.mapMaximized || CursorManager.GetFlag(CursorFlags.GameMenu) ||
            (GameManager.controlMapper != null && GameManager.controlMapper.isOpen) ||
            (SceneSingleton<RadialMenuMain>.i != null && RadialMenuMain.IsInUse()) ||
            CameraStateManager.cameraMode != CameraMode.cockpit ||
            aircraft == null || aircraft.cockpit == null || aircraft.cockpit.IsDetached()) return false;
        var camera = APIBus.MainCamera;
        var cameraState = SceneSingleton<CameraStateManager>.i;
        if (camera == null || cameraState == null || cameraState.followingUnit != aircraft ||
            camera.transform.parent != cameraState.transform || camera.GetComponent<NOVRPoseDriver>() == null) return false;
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        return !head.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || tracked;
    }

    private bool HeadAvailable() => isActiveAndEnabled && Settings.Enabled.Value && !_tracking && CockpitReady(out var aircraft) &&
        aircraft.targetCam != null && CameraField(aircraft.targetCam) != null &&
        CameraField(aircraft.targetCam).targetTexture != null && ModeField(aircraft.targetCam) != TargetCam.CamMode.landingMode;

    internal float AcquisitionRadius(float original)
    {
        if (!HeadAvailable()) return original;
        GameManager.GetLocalAircraft(out var aircraft);
        var source = CameraField(aircraft.targetCam);
        return OpticalHudMath.SelectionRadius(FieldOfView(source.targetTexture));
    }

    // Called immediately before native target selection. The native selector
    // still applies visibility, exclusions, priority and weapon eligibility.
    internal static void AlignDesignator(CombatHUD hud)
    {
        if (!HeadAimActive || hud.targetDesignator == null) return;
        var view = APIBus.CockpitHudCamera.transform;
        hud.targetDesignator.transform.SetPositionAndRotation(view.position + view.forward * 1000, view.rotation);
    }
    internal static void SelectionFinished(CombatHUD hud, bool paint, int previousCount, Unit? previousFirst)
    {
        var instance = Instance;
        if (instance == null || !HeadAimActive || !GameManager.GetLocalAircraft(out var aircraft)) return;
        var targets = aircraft.weaponManager.GetTargetList();
        if (targets.Count == 0) return;
        if (targets.Count != previousCount || targets[0] != previousFirst) instance._tracking = true;
        else if (!paint && hud.TryGetMarker(targets[0], out var marker) && marker.image != null && marker.image.enabled &&
            Vector3.Distance(marker.image.transform.position, hud.targetDesignator.transform.position) <= instance.AcquisitionRadius(100))
            instance._tracking = true;
    }

    private void LateUpdate()
    {
        RestoreCamera();
        var ready = CockpitReady(out var aircraft);
        if (_aircraft != aircraft || _wasEnabled != Settings.Enabled.Value)
        {
            _aircraft = aircraft; _tracking = false; _lastRenderedSource = null; _lastRenderedTexture = null; _lastRenderedFrame = -10;
        }
        _wasEnabled = Settings.Enabled.Value;
        var targets = aircraft?.weaponManager?.GetTargetList().Count ?? 0;
        if (targets == 0) _tracking = false;
        var sensor = aircraft?.targetCam;
        var source = sensor != null ? CameraField(sensor) : null;
        var texture = source != null ? source.targetTexture : null;
        _mode = OpticalHudMath.Mode(Settings.Enabled.Value, ready, sensor != null && source != null && source.gameObject.activeInHierarchy && texture != null,
            sensor != null && ModeField(sensor) == TargetCam.CamMode.landingMode, _tracking, targets);
        if (_mode == OpticalHudMode.Hidden) { Hide(); return; }
        var ui = APIBus.CockpitHudCamera;
        EnsureWindow(ui); _canvas!.worldCamera = ui;
        _source = source;
        if (_mode == OpticalHudMode.Head)
        {
            // The feed renders scene geometry. The HUD camera lives in a
            // separate origin-space and must only position the display surface.
            var head = APIBus.MainCamera.transform;
            _lease.Borrow(sensor!, source!, head.position, head.rotation, FieldOfView(texture!));
        }
        var live = (_mode == OpticalHudMode.Head || _mode == OpticalHudMode.Track) && source != null && source.isActiveAndEnabled;
        _picture!.texture = live ? texture : null;
        _picture.enabled = live && _lastRenderedSource == source && _lastRenderedFrame >= Time.frameCount - 1;
        _crosshair!.SetActive(_mode == OpticalHudMode.Head && _picture.enabled &&
            TargetCueVisibility.Show(ModConfiguration.Instance.TargetCue.Value, true,
                TargetCueCandidates.Nearby(SceneSingleton<CombatHUD>.i), false));
        _status!.text = _mode == OpticalHudMode.Unavailable ? "EOTS unavailable on this aircraft" :
            _mode == OpticalHudMode.Landing ? "EOTS: landing camera in use" : !live ? "EOTS: waiting for target feed" :
            _mode == OpticalHudMode.Head ? $"HEAD  ·  {Settings.Magnification.Value:0.#}×" : "TRACK";
        _picture.color = new Color(ModConfiguration.Instance.MfdBrightness.Value, ModConfiguration.Instance.MfdBrightness.Value, ModConfiguration.Instance.MfdBrightness.Value, 1);
        var width = OpticalHudMath.Width(Settings.Width.Value, Depth);
        var aspect = texture != null ? OpticalHudMath.Bounded((float)texture.width / Mathf.Max(1, texture.height), .5f, 3, 1) : 1;
        _picture.rectTransform.sizeDelta = new Vector2(width, width / aspect) * 1000;
        ((RectTransform)_markers!.transform).sizeDelta = _picture.rectTransform.sizeDelta;
        _markers.Magnification = OpticalHudMath.Bounded(Settings.Magnification.Value, 1, 6, 2);
        _markers.gameObject.SetActive(_mode == OpticalHudMode.Head && _picture.enabled);
        _status.rectTransform.anchoredPosition = new Vector2(0, -width / aspect * 500 - 30);
        _status.rectTransform.sizeDelta = new Vector2(Mathf.Max(650, width * 1000), 45);
        _window!.SetActive(true); PlaceWindow();
    }
    private float FieldOfView(RenderTexture texture) => OpticalHudMath.SensorFov(Settings.Width.Value,
        (float)texture.width / Mathf.Max(1, texture.height), Settings.Magnification.Value);

    private void PlaceWindow()
    {
        var view = APIBus.CockpitHudCamera.transform;
        _window!.transform.SetPositionAndRotation(view.position + view.forward * Depth, view.rotation);
    }
    private void BeforeRender()
    {
        if (_window?.activeSelf != true) return;
        if (!CockpitReady(out var aircraft) || aircraft != _aircraft || !Settings.Enabled.Value) { RestoreCamera(); Hide(); return; }
        PlaceWindow();
        if (_mode == OpticalHudMode.Head)
        {
            var head = APIBus.MainCamera.transform;
            _lease.RefreshPose(head.position, head.rotation);
        }
    }
    private void EnsureWindow(Camera ui)
    {
        if (_window != null) return;
        _window = new GameObject("NOVR helmet optical view", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _window.transform.SetParent(transform, false); _window.transform.localScale = Vector3.one * .001f;
        _canvas = _window.GetComponent<Canvas>(); _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = ui; _canvas.overrideSorting = true; _canvas.sortingOrder = 190;
        _canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        _window.GetComponent<CanvasGroup>().blocksRaycasts = false;
        RectTransform Rect(string name, Transform parent, Vector2 size)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false); rect.sizeDelta = size; return rect;
        }
        _picture = Rect("Optical feed", _window.transform, Vector2.one).gameObject.AddComponent<RawImage>();
        _picture.raycastTarget = false;
        var overlay = Rect("Head-directed target markers", _window.transform, Vector2.one);
        overlay.gameObject.AddComponent<RectMask2D>();
        _markers = overlay.gameObject.AddComponent<ZoomTargetingHud>(); _markers.MarkersOnly = true;
        _crosshair = GazeRingGraphic.Create(_window.transform);
        _status = Rect("Optical mode", _window.transform, new Vector2(650, 45)).gameObject.AddComponent<Text>();
        _status.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); _status.fontSize = 23;
        _status.alignment = TextAnchor.MiddleCenter; _status.raycastTarget = false;
        _status.color = new Color(.55f, 1, .65f, .9f);
        LayerHelper.SetLayerRecursive(_window.transform, LayerHelper.GetVrUiLayer());
    }
    private void Hide()
    {
        _source = null;
        _lastRenderedSource = null; _lastRenderedTexture = null; _lastRenderedFrame = -10;
        if (_picture != null) { _picture.texture = null; _picture.enabled = false; }
        if (_window != null) _window.SetActive(false);
    }
    private void OnDisable()
    {
        Application.onBeforeRender -= BeforeRender;
        RenderPipelineManager.endCameraRendering -= CameraRendered;
        RenderPipelineManager.endFrameRendering -= EndFrame;
        RestoreCamera(); Hide(); _tracking = false;
    }
    private void OnDestroy()
    {
        OnDisable(); if (_window != null) Destroy(_window);
        if (Instance == this) Instance = null;
    }
}
