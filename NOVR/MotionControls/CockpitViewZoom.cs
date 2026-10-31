using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.MotionControls;

// A mono inspection image on a stereo, fixed-depth surface. Headset projection,
// eye separation and the surrounding cockpit view remain runtime-controlled.
[DefaultExecutionOrder(-1025)]
internal sealed class CockpitViewZoom : MonoBehaviour
{
    private readonly ControllerButtonGate _button = new();
    private int _bindingVersion = -1;
    private ControllerBindingProfile _bindingProfile;
    private bool _bindingLeft;
    private Aircraft? _aircraft;
    private Vector3 _calibration, _anchor;
    private Quaternion _rotation;
    private Camera? _camera;
    private UniversalAdditionalCameraData? _cameraData;
    private Camera? _sourceCamera;
    private UniversalAdditionalCameraData? _sourceData;
    private RenderTexture? _texture;
    private RawImage? _picture;
    private int _requestedWidth, _requestedSamples;
    private bool _msaaEligible;
    private float _retryAfter;
    private GameObject? _window;
    private ZoomTargetingHud? _hud;
    private InspectionAimCue? _aimCue;
    private bool _held;
    internal static bool IsActive { get; private set; }
    internal static bool AimCueVisible => IsActive && InspectionAimCue.Visible;
    private const float Distance = 3f, Width = 1.2f, Height = .75f;

    private void Update()
    {
        var settings = ModConfiguration.Instance.ControllerBindings;
        var binding = settings.Get(ControllerActionId.Zoom);
        var left = ControllerBindings.ResolveLeft(binding.Hand, ModConfiguration.Instance.MotionControls.LeftHanded.Value);
        var hand = TrackedControllers.Read(left);
        var profile = settings.EffectiveProfile;
        if (_bindingVersion != settings.ChangeVersion || _bindingProfile != profile || _bindingLeft != left)
        { _button.Reset(); _bindingVersion = settings.ChangeVersion; _bindingProfile = profile; _bindingLeft = left; }
        GameManager.GetLocalAircraft(out var aircraft);
        if (_aircraft != aircraft || _calibration != NOVRHeadsetData.TranslationCalibrationOffset ||
            _anchor != NOVRHeadsetData.TranslationAnchor || _rotation != NOVRHeadsetData.RotationCalibrationOffset)
        {
            Reset(); _aircraft = aircraft; _calibration = NOVRHeadsetData.TranslationCalibrationOffset;
            _anchor = NOVRHeadsetData.TranslationAnchor; _rotation = NOVRHeadsetData.RotationCalibrationOffset;
        }
        var available = settings.ZoomEnabled.Value && XRSettings.enabled && TrackedControllers.Available && hand.Valid &&
            aircraft != null && aircraft.cockpit != null && !aircraft.cockpit.IsDetached() &&
            CameraStateManager.cameraMode == CameraMode.cockpit && Time.timeScale > 0 && GameManager.flightControlsEnabled &&
            !WristMenu.IsOpen && !CockpitLayout.Active && !DynamicMap.mapMaximized && !CursorManager.GetFlag(CursorFlags.GameMenu) &&
            (GameManager.controlMapper == null || !GameManager.controlMapper.isOpen) &&
            (SceneSingleton<RadialMenuMain>.i == null || !RadialMenuMain.IsInUse());
        _button.Update(available, ControllerBindings.Read(hand, binding.Button));
        _held = _button.Held;
        IsActive = _held;
        if (!_held) Hide();
    }
    private void LateUpdate()
    {
        if (!_held) return;
        var main = APIBus.MainCamera; var ui = APIBus.CockpitHudCamera;
        if (main == null || ui == null) { Reset(); return; }
        EnsureView(main, ui);
        if (!ApplyQuality(main)) { Reset(); return; }
        var magnification = ModConfiguration.Instance.ControllerBindings.ViewZoomFactor.Value;
        _camera!.transform.SetPositionAndRotation(main.transform.position, main.transform.rotation);
        _camera.fieldOfView = 2 * Mathf.Atan(Height / (2 * Distance * magnification)) * Mathf.Rad2Deg;
        _camera.farClipPlane = main.farClipPlane;
        _camera.enabled = true;
        _window!.transform.SetPositionAndRotation(ui.transform.position + ui.transform.forward * Distance, ui.transform.rotation);
        _window.SetActive(true);
        _hud!.Magnification = magnification;
        _aimCue!.Magnification = magnification;
    }
    private void EnsureView(Camera main, Camera ui)
    {
        if (_camera != null) return;
        var cameraObject = new GameObject("NOVR inspection camera", typeof(Camera), typeof(UniversalAdditionalCameraData));
        cameraObject.transform.SetParent(transform, false); _camera = cameraObject.GetComponent<Camera>();
        _camera.CopyFrom(main); _camera.ResetProjectionMatrix(); _camera.stereoTargetEye = StereoTargetEyeMask.None;
        _camera.enabled = false; _camera.targetTexture = null; _camera.allowHDR = false;
        _camera.allowDynamicResolution = false; _camera.rect = new Rect(0, 0, 1, 1); _camera.aspect = Width / Height;
        _camera.nearClipPlane = .15f; _camera.depth = -50;
        // Exclude cockpit geometry and all projected UI; this is a view outside,
        // never a recursive copy of the magnifier or another native MFD feed.
        _camera.cullingMask = main.cullingMask & ~((1 << (int)LayerHelper.Layers.Cockpit) | (1 << (int)LayerHelper.GetVrUiLayer()) | (1 << 5));
        _cameraData = cameraObject.GetComponent<UniversalAdditionalCameraData>();
        _cameraData.allowXRRendering = false; _cameraData.renderType = CameraRenderType.Base;
        _cameraData.antialiasing = AntialiasingMode.None;
        _window = new GameObject("NOVR inspection window", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        _window.transform.SetParent(transform, false); _window.transform.localScale = Vector3.one * .001f;
        var canvas = _window.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = ui; canvas.overrideSorting = true; canvas.sortingOrder = 200;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.Normal | AdditionalCanvasShaderChannels.Tangent;
        _window.GetComponent<CanvasGroup>().blocksRaycasts = false;
        RectTransform Rect(string name, Vector2 size, Vector2 position)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(_window.transform, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        _picture = Rect("Magnified scene", new Vector2(Width * 1000, Height * 1000), Vector2.zero).gameObject.AddComponent<RawImage>();
        _picture.raycastTarget = false;
        var overlay = Rect("Magnified targeting HUD", new Vector2(Width * 1000, Height * 1000), Vector2.zero);
        overlay.gameObject.AddComponent<RectMask2D>();
        _hud = overlay.gameObject.AddComponent<ZoomTargetingHud>();
        _aimCue = overlay.gameObject.AddComponent<InspectionAimCue>();
        LayerHelper.SetLayerRecursive(_window.transform, LayerHelper.GetVrUiLayer());
    }
    private bool ApplyQuality(Camera main)
    {
        var settings = ModConfiguration.Instance.InspectionZoom;
        if (_sourceCamera != main)
        {
            _sourceCamera = main; _sourceData = main.GetComponent<UniversalAdditionalCameraData>();
            if (_sourceData != null) _cameraData!.volumeLayerMask = _sourceData.volumeLayerMask;
        }
        _cameraData!.renderPostProcessing = settings.SceneEffects.Value;
        // URP permits independently sampled camera textures only while native
        // MSAA is enabled. Never alter that global preference for this view.
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var eligible = pipeline != null && pipeline.msaaSampleCount > 1 &&
            _cameraData.scriptableRenderer?.supportedRenderingFeatures.msaa == true && SystemInfo.supportsStoreAndResolveAction;
        var changed = _requestedWidth != settings.Width || _requestedSamples != settings.Samples || _msaaEligible != eligible;
        var valid = _texture != null && _texture.IsCreated();
        if (!changed && (valid || Time.unscaledTime < _retryAfter)) return valid;
        _requestedWidth = settings.Width; _requestedSamples = settings.Samples; _msaaEligible = eligible;
        _retryAfter = Time.unscaledTime + 5;

        var descriptor = new RenderTextureDescriptor(settings.Width, settings.Height, RenderTextureFormat.ARGB32, 24)
        {
            msaaSamples = eligible ? settings.Samples : 1, bindMS = false,
            useMipMap = false, autoGenerateMips = false, enableRandomWrite = false, useDynamicScale = false
        };
        descriptor.msaaSamples = Mathf.Max(1, SystemInfo.GetRenderTextureSupportedMSAASampleCount(descriptor));
        var replacement = CreateTexture(descriptor);
        // Keep a working view if a driver rejects a requested quality level.
        // A failed setting is remembered, rather than retried every frame.
        if (replacement == null && !valid)
        {
            descriptor.width = 1024; descriptor.height = 640; descriptor.msaaSamples = 1;
            replacement = CreateTexture(descriptor);
        }
        if (replacement == null) return valid;
        var previous = _texture;
        _texture = replacement;
        _camera!.targetTexture = replacement; _camera.allowMSAA = replacement.antiAliasing > 1;
        _picture!.texture = replacement;
        if (previous != null) { previous.Release(); Destroy(previous); }
        Debug.Log($"[NOVR] Inspection view: {replacement.width}×{replacement.height}, {replacement.antiAliasing} sample(s); headset settings unchanged.");
        return true;
    }
    private static RenderTexture? CreateTexture(RenderTextureDescriptor descriptor)
    {
        var texture = new RenderTexture(descriptor)
        { name = "NOVR inspection view", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        try
        {
            if (texture.Create() && texture.IsCreated()) return texture;
        }
        catch (UnityException error) { Debug.LogWarning($"[NOVR] Inspection texture unavailable: {error.Message}"); }
        texture.Release(); Destroy(texture);
        return null;
    }
    private void Hide()
    { if (_camera != null) _camera.enabled = false; if (_window != null) _window.SetActive(false); }
    private void Reset() { _button.Reset(); _held = false; IsActive = false; Hide(); }
    private void OnDisable() => Reset();
    private void OnDestroy()
    {
        Reset();
        if (_camera != null) { _camera.targetTexture = null; Destroy(_camera.gameObject); }
        if (_picture != null) _picture.texture = null;
        if (_window != null) Destroy(_window);
        if (_texture != null) { _texture.Release(); Destroy(_texture); }
    }
}
