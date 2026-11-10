#if CPP
using System;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.VrCamera;

public class VrCameraManager : MonoBehaviour
{
    private const string NuclearOptionMainCameraName = "Main Camera";
    private const string NuclearOptionMenuCameraName = "Menu Camera";
    private const string VrCameraChildName = "NOVR Main Camera";
    private static readonly string[] TrackedChildNames = { "cockpitRenderer", "postProcessingRenderer" };

    public static HashSet<Camera> IgnoredCameras = new();
    private static readonly System.Predicate<Camera> IsDestroyedCamera = camera => camera == null;
    private Camera[] _cameraBuffer = System.Array.Empty<Camera>();
    private int _cameraCount;

    // Cameras are recognised by object name, and reading a name allocates a
    // new string on every call. Remember each camera's role instead and ask
    // again about once a second, in case something renames a camera.
    private enum CameraRole : byte
    {
        Other, TrackedView, GameMain, GameMenu
    }
    private const int RoleRefreshFrames = 90;
    private readonly Dictionary<int, CameraRole> _roles = new();
    private int _roleAge;

    private CameraRole Role(Camera camera)
    {
        var id = camera.GetInstanceID();
        if (_roles.TryGetValue(id, out var role))
            return role;
        var name = camera.gameObject.name;
        role = name == VrCameraChildName ? CameraRole.TrackedView
            : name == NuclearOptionMainCameraName ? CameraRole.GameMain
            : name == NuclearOptionMenuCameraName ? CameraRole.GameMenu
            : CameraRole.Other;
        _roles[id] = role;
        return role;
    }

    private void Update() // Todo: Make me behave on events if possible
    {
        // Scene cameras leave managed wrappers behind after Unity destroys them.
        // Retain live owners (including disabled cameras), not every old scene.
        IgnoredCameras.RemoveWhere(IsDestroyedCamera);
        if (++_roleAge >= RoleRefreshFrames)
        {
            _roleAge = 0;
            _roles.Clear();
        }
        // Keep discovering scene/menu cameras every frame, but reuse storage
        // instead of allocating an array for every headset frame.
        var capacity = Camera.allCamerasCount;
        if (_cameraBuffer.Length < capacity)
            _cameraBuffer = new Camera[Mathf.NextPowerOfTwo(capacity)];
        var count = Camera.GetAllCameras(_cameraBuffer);
        if (count < _cameraCount)
            System.Array.Clear(_cameraBuffer, count, _cameraCount - count);
        _cameraCount = count;

        for (var i = 0; i < count; i++)
        {
            var camera = _cameraBuffer[i];
            if (camera == null)
                continue;
            var role = Role(camera);
            if (role == CameraRole.Other)
                continue;
            var gameObject = camera.gameObject;
            if (role == CameraRole.TrackedView)
            {
                // The native root is disabled once this tracked view renders,
                // so Camera.GetAllCameras never visits it again: keep the URP
                // anti-aliasing in step from the enabled tracked child instead.
                var root = camera.transform.parent != null ? camera.transform.parent.GetComponent<Camera>() : null;
                if (root != null)
                    SyncBaseCameraSettings(root, camera);
                continue;
            }
            if (role == CameraRole.GameMain)
            {
                var existingTrackedCamera = GetTrackedMainCamera(gameObject);
                if (existingTrackedCamera != null)
                {
                    EnsureTrackedMainCameraRig(camera, existingTrackedCamera);
                    continue;
                }
            }

            if (IgnoredCameras.Contains(camera))
            {
                continue;
            }

            if (role == CameraRole.GameMain)
            {
                SetUpMainCameraRig(camera);
            }
            else
            {
                HandleChildCameras(camera);
                gameObject.AddComponent<VrCamera>();
                IgnoredCameras.Add(camera);
            }
        }
    }

    public static Camera? GetTrackedMainCamera(GameObject gameCameraRoot)
    {
        var trackedCameraTransform = gameCameraRoot.transform.Find(VrCameraChildName);
        return trackedCameraTransform != null ? trackedCameraTransform.GetComponent<Camera>() : null;
    }

    private void SetUpMainCameraRig(Camera rootCamera)
    {
        HandleChildCameras(rootCamera);

        var existingTrackedCamera = GetTrackedMainCamera(rootCamera.gameObject);
        if (existingTrackedCamera != null)
        {
            EnsureTrackedMainCameraRig(rootCamera, existingTrackedCamera);
            return;
        }

        var trackedCameraObject = new GameObject(VrCameraChildName);
        trackedCameraObject.transform.SetParent(rootCamera.transform, false);
        trackedCameraObject.tag = rootCamera.tag;

        var trackedCamera = trackedCameraObject.AddComponent<Camera>();
        trackedCamera.CopyFrom(rootCamera);
        var universalAdditionalCameraData = trackedCameraObject.AddComponent<UniversalAdditionalCameraData>();
        universalAdditionalCameraData.renderType = CameraRenderType.Base;
        universalAdditionalCameraData.allowXRRendering = true;
        var rootUniversalAdditionalCameraData = rootCamera.GetComponent<UniversalAdditionalCameraData>();

        if (universalAdditionalCameraData != null && rootUniversalAdditionalCameraData != null)
            universalAdditionalCameraData.cameraStack.AddRange(rootUniversalAdditionalCameraData.cameraStack);
        SyncBaseCameraSettings(rootCamera, trackedCamera);


        var rootAudioListener = rootCamera.GetComponent<AudioListener>();
        if (rootAudioListener != null)
        {
            var trackedAudioListener = trackedCameraObject.AddComponent<AudioListener>();
            trackedAudioListener.enabled = rootAudioListener.enabled;
            rootAudioListener.enabled = false;
        }

        rootCamera.tag = "Untagged";
        rootCamera.enabled = false;
        ReparentTrackedChildren(rootCamera.transform, trackedCameraObject.transform);

        trackedCameraObject.AddComponent<VrCamera>();
        trackedCameraObject.AddComponent<VrCockpitNearClip>();
        trackedCameraObject.AddComponent<VrFarClip>();

        IgnoredCameras.Add(rootCamera);
        IgnoredCameras.Add(trackedCamera);
    }

    private static void EnsureTrackedMainCameraRig(Camera rootCamera, Camera trackedCamera)
    {
        if (rootCamera.enabled)
        {
            rootCamera.enabled = false;
        }

        if (rootCamera.CompareTag("MainCamera"))
        {
            rootCamera.tag = "Untagged";
        }

        if (!trackedCamera.enabled)
        {
            trackedCamera.enabled = true;
        }

        if (!trackedCamera.CompareTag("MainCamera"))
        {
            trackedCamera.tag = "MainCamera";
        }

        if (trackedCamera.GetComponent<VrCamera>() == null)
        {
            trackedCamera.gameObject.AddComponent<VrCamera>();
        }
        if (trackedCamera.GetComponent<VrCockpitNearClip>() == null)
            trackedCamera.gameObject.AddComponent<VrCockpitNearClip>();
        if (trackedCamera.GetComponent<VrFarClip>() == null)
            trackedCamera.gameObject.AddComponent<VrFarClip>();
        SyncBaseCameraSettings(rootCamera, trackedCamera);

        IgnoredCameras.Add(rootCamera);
        IgnoredCameras.Add(trackedCamera);
    }

    // Camera.CopyFrom copies no URP data. The stock flight scene's base camera
    // asks for SMAA (Medium), which URP applies for the whole camera stack on
    // top of the pipeline's MSAA; a fresh tracked camera would default to none.
    // The stack's base camera owns these settings, so mirror them every update
    // (cheap assignments) and honour the VR preferences live.
    internal static void SyncBaseCameraSettings(Camera rootCamera, Camera trackedCamera)
    {
        var root = rootCamera.GetComponent<UniversalAdditionalCameraData>();
        var tracked = trackedCamera.GetComponent<UniversalAdditionalCameraData>();
        if (root == null || tracked == null)
            return;
        var mode = ModConfiguration.Instance.VrGameAntialiasing.Value ? root.antialiasing : AntialiasingMode.None;
        if (tracked.antialiasing != mode)
            tracked.antialiasing = mode;
        if (tracked.antialiasingQuality != root.antialiasingQuality)
            tracked.antialiasingQuality = root.antialiasingQuality;
        // Dithering is also the base camera's to decide: URP applies it in
        // the stack's post-processing pass (the game's post-processing overlay
        // camera here). No stock camera asks for it, and the headset image is
        // 8 bits per channel, so smooth sky and haze gradients show bands.
        var dithering = root.dithering || ModConfiguration.Instance.ImageQuality.DitherGradients.Value;
        if (tracked.dithering != dithering)
            tracked.dithering = dithering;
    }

    private void HandleChildCameras(Camera parentCamera)
    {
        foreach (var child in parentCamera.GetComponentsInChildren<Camera>())
        {
            if (child != parentCamera && !IgnoredCameras.Contains(child) && child.GetComponent<StereoCamera>() == null)
                child.gameObject.AddComponent<StereoCamera>();
        }
    }

    private static void ReparentTrackedChildren(Transform rootCameraTransform, Transform trackedCameraTransform)
    {
        foreach (var childName in TrackedChildNames)
        {
            var child = rootCameraTransform.Find(childName);
            if (child != null)
            {
                child.SetParent(trackedCameraTransform, false);
            }
        }
    }
}
