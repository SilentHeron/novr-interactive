using System.Reflection;
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
        private static readonly FieldInfo PanViewField = AccessTools.Field(typeof(CameraCockpitState), "panView");
        private static readonly FieldInfo TiltViewField = AccessTools.Field(typeof(CameraCockpitState), "tiltView");


        [HarmonyPostfix]
        private static void Postfix(CameraCockpitState __instance, CameraStateManager cam)
        {
            PanViewField.SetValue(__instance, 0.0f);
            TiltViewField.SetValue(__instance, 0.0f);
        }
    }
}
