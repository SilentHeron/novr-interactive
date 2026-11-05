using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

internal static class CameraSelectionStatePatch
{
    private static readonly FieldInfo? Target = AccessTools.Field(typeof(CameraSelectionState), "target");

    [HarmonyPatch(typeof(CameraSelectionState), "UpdateOrbit")]
    private static class UpdateOrbit
    {
        [HarmonyPrefix]
        private static bool Prefix(CameraSelectionState? __instance, CameraStateManager? cam)
        {
            var pivot = cam?.cameraPivot;
            if (!XRSettings.enabled || __instance == null || pivot == null)
                return true;
            var target = Target?.GetValue(__instance) as Transform;
            if (target == null)
                return true;

            // Selection remains a tracked 3D view. Preserve its fixed relative
            // vantage instead of carrying native automatic orbit into the HMD.
            pivot.rotation = target.rotation * Quaternion.Euler(0f, 45f, 0f);
            return false;
        }
    }
}
