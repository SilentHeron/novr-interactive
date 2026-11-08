using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.GameplayPatches;

// The game rings the camera with ~300 distant cloud meshes (1.2-1.6 km each,
// larger in heavy weather) 10-40 km out at the cloud height, whenever the
// weather's coverage is above 10%. Multiplayer servers usually run such
// weather; most single-player missions do not. In the headset that ring reads
// as a grey wall around the horizon, so VR can hide it. The native coverage
// rule still runs first; this only overrides the result while in VR.
[HarmonyPatch(typeof(CloudLayer), "UpdateWeatherSets")]
internal static class DistantCloudBanks
{
    private static readonly AccessTools.FieldRef<CloudLayer, ParticleSystem> DistantClouds =
        AccessTools.FieldRefAccess<CloudLayer, ParticleSystem>("distantCloudSystem");
    private static readonly MethodInfo? NativeUpdate = AccessTools.Method(typeof(CloudLayer), "UpdateWeatherSets");

    internal static bool HideInVr => ModConfiguration.Instance.HideDistantCloudsInVr.Value;

    [HarmonyPostfix]
    private static void Postfix(CloudLayer __instance)
    {
        if (!XRSettings.enabled || !HideInVr)
            return;
        var system = DistantClouds(__instance);
        if (system != null && system.gameObject.activeSelf)
            system.gameObject.SetActive(false);
    }

    // Apply a menu change now instead of on the next slow weather update.
    // Re-running the native method restores its own coverage decision first.
    internal static void SetHideInVr(bool hide)
    {
        ModConfiguration.Instance.HideDistantCloudsInVr.Value = hide;
        var layer = UnityEngine.Object.FindObjectOfType<CloudLayer>();
        if (layer == null || NativeUpdate == null)
            return;
        try { NativeUpdate.Invoke(layer, null); }
        catch (Exception error) { Debug.LogWarning("[NOVR] Could not refresh distant clouds: " + error.Message); }
    }
}
