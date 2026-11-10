using System;
using NOVR.VrCamera;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.VrUi;

[DefaultExecutionOrder(50)]
public class NOUIManager : NOVRBehaviour
{
    private Camera? _cockpitHudCamera;
    private Camera? _startupCamera;
    private GameObject? _smoothedForwardReference;

    public static NOUIManager I { get; private set; } = null!;

    public Camera CockpitHudCamera
    {
        get
        {
            // Unity can destroy the native object while leaving its managed
            // wrapper alive. A null-coalescing assignment misses that case.
            if (_cockpitHudCamera == null)
            {
                var main = APIBus.MainCamera;
                RemoveUiCameraFromStack(main);
                if (_cockpitHudCamera is not null)
                    VrCameraManager.IgnoredCameras.Remove(_cockpitHudCamera);
                _cockpitHudCamera = CreateUiCamera("VrCockpitHudCamera", 100);
                var stack = main != null ? main.GetComponent<UniversalAdditionalCameraData>()?.cameraStack : null;
                if (stack != null && !stack.Contains(_cockpitHudCamera))
                    stack.Add(_cockpitHudCamera);
            }
            return _cockpitHudCamera;
        }
    }

    public GameObject CockpitHudReference
    {
        get
        {
            if (_smoothedForwardReference == null)
                _smoothedForwardReference = CreateSmoothedForwardReference();
            return _smoothedForwardReference;
        }
    }

    internal void SetStartupPresentation(bool visible)
    {
        if (visible && _startupCamera == null)
        {
            if (_startupCamera is not null)
                VrCameraManager.IgnoredCameras.Remove(_startupCamera);
            // A scene-independent stereo base camera keeps the startup card visible
            // while a content loader replaces the normal menu/mission cameras.
            // XR owns both eye projections; no FOV, eye matrix or IPD override is used.
            var pose = Create<NOVRPoseDriver>(transform);
            pose.name = "NOVR Startup Presentation";
            _startupCamera = pose.gameObject.AddComponent<Camera>();
            _startupCamera.enabled = false;
            _startupCamera.stereoTargetEye = StereoTargetEyeMask.Both;
            _startupCamera.clearFlags = CameraClearFlags.SolidColor;
            _startupCamera.backgroundColor = new Color(.018f, .025f, .035f, 1f);
            _startupCamera.depth = 1000;
            _startupCamera.nearClipPlane = .01f;
            _startupCamera.farClipPlane = 10000f;
            _startupCamera.cullingMask = 1 << (int)LayerHelper.GetVrUiLayer();
            _startupCamera.allowHDR = _startupCamera.allowMSAA = false;
            var data = pose.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderType = CameraRenderType.Base;
            data.renderPostProcessing = false;
            ConfigureUiOnly(data);
            VrCameraManager.IgnoredCameras.Add(_startupCamera);
        }
        if (_startupCamera != null)
        {
            if (_startupCamera.gameObject.activeSelf != visible)
                _startupCamera.gameObject.SetActive(visible);
            _startupCamera.enabled = visible;
        }
    }

    private GameObject CreateSmoothedForwardReference()
    {
        var go = new GameObject("SmoothedForwardReference");
        go.transform.SetParent(transform);

        var cam = CockpitHudCamera;
        go.transform.position = cam.transform.position;
        go.transform.localRotation = cam.transform.localRotation;
        return go;
    }

    private new void Awake()
    {
        base.Awake();
        APIBus.OnMainCameraChanged += OnMainCameraChanged;
    }

    private void OnDestroy()
    {
        APIBus.OnMainCameraChanged -= OnMainCameraChanged;
        RemoveUiCameraFromStack(APIBus.MainCamera);
        // Remove our wrappers even if Unity has already destroyed their native
        // camera objects. A different replacement manager keeps its singleton.
        if (_startupCamera is not null)
            VrCameraManager.IgnoredCameras.Remove(_startupCamera);
        if (_cockpitHudCamera is not null)
            VrCameraManager.IgnoredCameras.Remove(_cockpitHudCamera);
        if (ReferenceEquals(I, this))
            I = null!;
    }

    private void Start()
    {
        I = this;
        Create<UIBehaviorPatcher>(transform);
        UIBehaviorPatcher.DoPatching();
        Create<VrUiCursor>(transform);
        Create<NativeVrUiRoot>(transform);
        gameObject.AddComponent<SpecialBehavior.CockpitAlerts>();
        ConfigureUiCameras();
        // The main camera may already have been published before this manager
        // was created. Do not wait for another scene/camera change to attach UI.
        OnMainCameraChanged(null, APIBus.MainCamera);
    }

    protected override void OnSettingChanged()
    {
        base.OnSettingChanged();
        ConfigureUiCameras();
    }

    private void Update()
    {
        ConfigureUiCameras();
    }

    private void LateUpdate() => UpdateSmoothedPosition();

    private void UpdateSmoothedPosition()
    {
        var smoothedForwardReference = CockpitHudReference;
        var cam = CockpitHudCamera;
        smoothedForwardReference.transform.position = cam.transform.position;
        var response = ModConfiguration.Instance.HmdFollowResponse.Value;
        var blend = response <= 0f ? 1f : MotionControls.FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, response);
        smoothedForwardReference.transform.rotation = Quaternion.Slerp(smoothedForwardReference.transform.rotation, cam.transform.rotation, blend);
    }



    private Camera CreateUiCamera(string cameraName, float depth)
    {
        var poseDriver = Create<NOVRPoseDriver>(transform);
        poseDriver.name = cameraName;

        var camera = poseDriver.gameObject.AddComponent<Camera>();
        var additionalCameraData = poseDriver.gameObject.AddComponent<UniversalAdditionalCameraData>();
        VrCameraManager.IgnoredCameras.Add(camera);


        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        camera.targetTexture = null;
        camera.clearFlags = CameraClearFlags.Depth;
        camera.backgroundColor = Color.clear;
        camera.depth = depth;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.cullingMask = 1 << (int)LayerHelper.GetVrUiLayer();
        additionalCameraData.renderType = CameraRenderType.Overlay;
        ConfigureUiOnly(additionalCameraData);

        return camera;
    }

    // The game's pipeline asks every camera for a depth texture and an opaque
    // colour copy, and for shadows. Each stacked camera then resolves the
    // multisampled depth buffer again, once per eye. This camera draws only
    // interface graphics, which sample neither and cast or receive no shadow.
    private static void ConfigureUiOnly(UniversalAdditionalCameraData data)
    {
        data.requiresDepthOption = CameraOverrideOption.Off;
        data.requiresColorOption = CameraOverrideOption.Off;
        data.renderShadows = false;
    }

    private void OnMainCameraChanged(Camera? previous, Camera? newCam)
    {
        if (previous != newCam)
            RemoveUiCameraFromStack(previous);
        if (newCam == null)
        {
            return;
        }

        var cockpitHudCamera = CockpitHudCamera;
        var cameraStack = newCam.gameObject.GetComponent<UniversalAdditionalCameraData>()?.cameraStack;
        if (cameraStack != null && !cameraStack.Contains(cockpitHudCamera))
        {
            cameraStack.Add(cockpitHudCamera);
        }
    }

    private void RemoveUiCameraFromStack(Camera? camera)
    {
        if (camera == null || _cockpitHudCamera is null)
            return;
        camera.GetComponent<UniversalAdditionalCameraData>()?.cameraStack?.Remove(_cockpitHudCamera);
    }

    private void ConfigureUiCameras()
    {
        ConfigureUiCamera(CockpitHudCamera);
    }

    private static void ConfigureUiCamera(Camera camera)
    {

        camera.clearFlags = CameraClearFlags.Depth;
        camera.backgroundColor = Color.clear;
        camera.targetTexture = null;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 10000f;
        camera.rect = new Rect(0f, 0f, 1f, 1f);
    }
}
