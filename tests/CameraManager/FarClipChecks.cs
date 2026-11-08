using System.Reflection;
using NOVR;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.XR;

internal static class FarClipChecks
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
        var component = camera.GetComponent<VrFarClip>()!;
        void Call(string method) => typeof(VrFarClip).GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(component, null);
        var preference = ModConfiguration.Instance.VrFarClipDistance;
        preference.Value = VrFarClipPolicy.Default;
        APIBus.MainCamera = camera;
        camera.enabled = true;
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        camera.farClipPlane = 80000f;
        nativeRoot.farClipPlane = 80000f;
        XRSettings.enabled = false;
        Call("OnEnable");
        Call("LateUpdate");
        Check(camera.farClipPlane == 80000f, "Non-VR leaves the game's 80 km far plane alone.");
        XRSettings.enabled = true;
        Call("LateUpdate");
        Check(camera.farClipPlane == VrFarClipPolicy.Default && nativeRoot.farClipPlane == 80000f,
            "Only the tracked VR view extends its far plane; the native root is unchanged.");
        for (var i = 0; i < 10; i++)
            Call("LateUpdate");
        Check(camera.farClipPlane == VrFarClipPolicy.Default, "Repeated updates keep one stable lease.");
        XRSettings.enabled = false;
        Call("LateUpdate");
        Check(camera.farClipPlane == 80000f, "XR loss restores the game's value.");
        XRSettings.enabled = true;
        Call("LateUpdate");
        preference.Value = 0f;
        Call("LateUpdate");
        Check(camera.farClipPlane == 80000f, "A zero preference keeps the game's far plane.");
        preference.Value = 120000f;
        Call("LateUpdate");
        Check(camera.farClipPlane == 120000f, "A changed preference is re-applied from the game's value, not the previous lease.");
        preference.Value = 50000f;
        Call("LateUpdate");
        Check(camera.farClipPlane == 80000f, "The preference is a minimum and never shortens the game's far plane.");
        preference.Value = 5000000f;
        Call("LateUpdate");
        Check(camera.farClipPlane == VrFarClipPolicy.Maximum, "Oversized preferences are capped.");
        preference.Value = VrFarClipPolicy.Default;
        Call("LateUpdate");
        Check(camera.farClipPlane == VrFarClipPolicy.Default, "Returning to the default re-leases the default distance.");
        camera.farClipPlane = 300000f;
        Call("LateUpdate");
        Call("OnDisable");
        Check(camera.farClipPlane == 300000f, "A longer far plane supplied by the game or another mod is retained.");
        camera.farClipPlane = 80000f;
        Call("OnEnable");
        Call("LateUpdate");
        camera.farClipPlane = 90000f;
        Call("LateUpdate");
        Call("OnDisable");
        Check(camera.farClipPlane == 90000f, "A native rewrite becomes the new restore value.");
        Call("OnEnable");
        Call("LateUpdate");
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        Call("LateUpdate");
        Check(camera.farClipPlane == 90000f, "Mono views restore the game's far plane.");
        camera.stereoTargetEye = StereoTargetEyeMask.Both;
        Call("LateUpdate");
        APIBus.MainCamera = nativeRoot;
        Call("LateUpdate");
        Check(camera.farClipPlane == 90000f, "Changing the active camera restores the old camera immediately.");
        APIBus.MainCamera = camera;
        Call("LateUpdate");
        camera.enabled = false;
        Call("LateUpdate");
        Check(camera.farClipPlane == 90000f, "A disabled camera never retains a VR far-plane override.");
        camera.enabled = true;
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, 0f, -1f })
        {
            camera.farClipPlane = value;
            Call("LateUpdate");
            Call("OnDisable");
            Check(camera.farClipPlane.Equals(value), "Malformed native values are not acquired as an owned override.");
        }
        camera.farClipPlane = 80000f;
        Call("OnEnable");
        Call("LateUpdate");
        Call("OnDestroy");
        Call("OnDisable");
        Check(camera.farClipPlane == 80000f, "Destroy and disable restore idempotently.");

        Check(VrFarClipPolicy.TryExtend(80000f, 250000f, out var extended) && extended == 250000f,
            "The policy extends a shorter native far plane to the requested distance.");
        Check(!VrFarClipPolicy.TryExtend(80000f, 80000f, out _) && !VrFarClipPolicy.TryExtend(80000f, 1000f, out _),
            "Equal or shorter requests leave the game's far plane alone.");
        Check(VrFarClipPolicy.TryExtend(80000f, float.MaxValue, out var capped) && capped == VrFarClipPolicy.Maximum,
            "Requests are capped at the documented maximum.");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0f, -5f })
            Check(!VrFarClipPolicy.TryExtend(invalid, 250000f, out _) && !VrFarClipPolicy.TryExtend(80000f, invalid, out _),
                "Non-finite or non-positive values never produce a lease.");
        return checks;
    }
}
