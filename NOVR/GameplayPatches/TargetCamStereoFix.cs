using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NOVR.GameplayPatches;

// Both cameras belong to the flat cockpit instrument, not the headset. The
// prefab's stereo defaults can otherwise affect it before its texture is bound.
[HarmonyPatch(typeof(TargetCam), "Initialize")]
internal static class TargetCamStereoFix
{
    private static readonly AccessTools.FieldRef<TargetCam, Camera> CamField =
        AccessTools.FieldRefAccess<TargetCam, Camera>("cam");

    private static readonly AccessTools.FieldRef<TargetCam, Camera> UiCamField =
        AccessTools.FieldRefAccess<TargetCam, Camera>("UICam");

    [HarmonyPostfix]
    private static void Postfix(TargetCam __instance)
    {
        MakeInstrumentCamera(CamField(__instance));
        MakeInstrumentCamera(UiCamField(__instance));
    }

    private static void MakeInstrumentCamera(Camera? camera)
    {
        if (camera == null) return;
        camera.stereoTargetEye = StereoTargetEyeMask.None;
        var data = camera.GetComponent<UniversalAdditionalCameraData>();
        if (data != null) data.allowXRRendering = false;
    }
}
