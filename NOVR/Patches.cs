using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace NOVR;

[HarmonyPatch(typeof(Camera), "set_fieldOfView")]
public static class CameraPatches
{
    [HarmonyPrefix]
    private static bool AllowNonHeadsetFov(Camera __instance)
    {
        // The runtime owns headset projection. Suppress game zoom on those
        // cameras, including repeated setters that otherwise flood Unity's log.
        // Instrument, preview and desktop cameras must retain their native FOV:
        // blocking every setter breaks target tracking and landing-camera zoom.
        if (!XRSettings.enabled || __instance.targetTexture != null ||
            __instance.cameraType is not (CameraType.Game or CameraType.VR))
            return true;

        // URP chooses XR through additional camera data. stereoTargetEye alone
        // is not sufficient to opt an URP camera out of headset rendering.
        var data = __instance.GetComponent<UniversalAdditionalCameraData>();
        // Without additional data, URP defaults to XR rendering as well.
        return data != null && !data.allowXRRendering;
    }
}
