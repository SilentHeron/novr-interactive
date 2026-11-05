using HarmonyLib;

namespace NOVR.VrCamera;

internal static class CameraOrbitStatePatch
{
    [HarmonyPatch(typeof(CameraOrbitState), "CameraMotion")]
    private static class CameraMotionPatch
    {
        [HarmonyPrefix]
        private static void Prefix(CameraOrbitState __instance, CameraStateManager cam) =>
            SpectatorCameraInput.ApplyOrbit(__instance, cam);
    }
}
