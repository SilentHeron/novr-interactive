using System.Reflection;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.XR;

internal static class NearClipChecks
{
    internal static int Run(Camera camera, Camera nativeRoot)
    {
        var checks = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            checks++;
        }
        var component = camera.GetComponent<VrCockpitNearClip>()!;
        void Call(string method) => typeof(VrCockpitNearClip).GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null);
        APIBus.MainCamera = camera;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        XRSettings.enabled = false;
        Call("OnEnable");
        Call("LateUpdate");
        Check(camera.nearClipPlane == .2f, "Non-VR leaves the native 20 cm near plane alone.");
        XRSettings.enabled = true;
        Call("LateUpdate");
        Check(camera.nearClipPlane == .03f && nativeRoot.nearClipPlane == .2f,
            "Only tracked VR cockpit view moves its near plane inside close side glass; original root remains unchanged.");
        for (var i = 0; i < 10; i++)
            Call("LateUpdate");
        CameraStateManager.cameraMode = CameraMode.external;
        Call("LateUpdate");
        Check(camera.nearClipPlane == .2f, "External view restores the captured native value, not the clamped value.");
        CameraStateManager.cameraMode = CameraMode.cockpit;
        Call("LateUpdate");
        XRSettings.enabled = false;
        Call("LateUpdate");
        Check(camera.nearClipPlane == .2f, "XR loss restores the native value.");
        XRSettings.enabled = true;
        camera.nearClipPlane = .01f;
        Call("LateUpdate");
        Call("OnDisable");
        Check(camera.nearClipPlane == .01f, "A smaller near plane supplied by the game or another mod is retained.");
        camera.nearClipPlane = .2f;
        Call("OnEnable");
        Call("LateUpdate");
        camera.nearClipPlane = .1f;
        Call("LateUpdate");
        Call("OnDisable");
        Check(camera.nearClipPlane == .1f, "A native rewrite while in the cockpit becomes the new restore value.");
        Call("OnEnable");
        Call("LateUpdate");
        camera.nearClipPlane = .4f;
        Call("OnDisable");
        Check(camera.nearClipPlane == .4f, "Disabling never overwrites a later change by another owner.");
        Call("OnEnable");
        Call("LateUpdate");
        APIBus.MainCamera = nativeRoot;
        Call("LateUpdate");
        Check(camera.nearClipPlane == .4f, "Changing the active camera restores the old camera immediately.");
        APIBus.MainCamera = camera;
        Call("LateUpdate");
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        Call("LateUpdate");
        Check(camera.nearClipPlane == .4f, "Mono view restores the original near plane.");
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        Call("LateUpdate");
        camera.enabled = false;
        Call("LateUpdate");
        Check(camera.nearClipPlane == .4f, "Disabled camera never retains a VR near-plane override.");
        camera.enabled = true;
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, 0f, -1f })
        {
            camera.nearClipPlane = value;
            Call("LateUpdate");
            Call("OnDisable");
            Check(camera.nearClipPlane.Equals(value), "Malformed native values are not acquired as an owned override.");
        }
        camera.nearClipPlane = .2f;
        Call("LateUpdate");
        Call("OnDestroy");
        Call("OnDisable");
        Check(camera.nearClipPlane == .2f, "Destroy and disable restore idempotently.");
        return checks;
    }
}
