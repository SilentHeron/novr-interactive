using HarmonyLib;
using UnityEngine;

namespace NOVR.VrCamera;

public class CameraCockpitStatePatch
{
    [HarmonyPatch(typeof(CameraCockpitState), nameof(CameraCockpitState.EnterState))]
    private static class EnterStatePatch
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            // Native chase/orbit exits restore a hard-coded 2 km shadow range.
            // Honour the user's saved range/cascades when returning to VR flight.
            if (UnityEngine.XR.XRSettings.enabled && PlayerSettings.graphics != null)
                PlayerSettings.graphics.SetShadowDistance(PlayerSettings.graphics.ShadowDistance);
        }
    }

    [HarmonyPatch(typeof(CameraCockpitState), "UpdateState")]
    private static class UpdateStatePatch
    {
        // Written every frame; a field reference stores the float without
        // the reflection call and boxed value that FieldInfo.SetValue needs.
        private static readonly AccessTools.FieldRef<CameraCockpitState, float> PanView =
            AccessTools.FieldRefAccess<CameraCockpitState, float>("panView");
        private static readonly AccessTools.FieldRef<CameraCockpitState, float> TiltView =
            AccessTools.FieldRefAccess<CameraCockpitState, float>("tiltView");

        [HarmonyPostfix]
        private static void Postfix(CameraCockpitState __instance, CameraStateManager cam)
        {
            PanView(__instance) = 0.0f;
            TiltView(__instance) = 0.0f;
        }
    }
}
