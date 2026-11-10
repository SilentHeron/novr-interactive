using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace NOVR.Rendering;

// Experimental, off by default.
//
// The game's shaders were built without single-pass stereo variants, so VR has
// to draw each eye as a separate camera pass. URP then renders the sun's shadow
// map once per pass: every shadow caster is submitted twice a frame for a map
// that is the same for both eyes to within the 6 cm between them.
//
// With the option on, the second eye of a camera keeps the first eye's shadow
// map and cascade data: the pass is still queued (it must set the shadow
// keywords and receiver constants again, because URP clears them at the start
// of every camera), but it neither clears nor redraws the map. Anything that
// does not match exactly (another camera used the pass in between, shadows
// off, no first-eye map this frame) falls back to URP's normal rendering.
internal static class SharedEyeShadows
{
    private static readonly AccessTools.FieldRef<MainLightShadowCasterPass, bool> CreateEmptyShadowmap =
        AccessTools.FieldRefAccess<MainLightShadowCasterPass, bool>("m_CreateEmptyShadowmap");

    // The first-eye map each shadow pass holds: which camera and frame it is for.
    private sealed class FirstEye
    {
        internal int Frame = -1;
        internal Camera? Camera;
    }
    private static readonly Dictionary<MainLightShadowCasterPass, FirstEye> Maps = new();
    private static MainLightShadowCasterPass? _reusing;
    private static bool _skipSlices, _announced;
    private static int _lastReuseFrame = -1000;

    internal static bool Enabled => ModConfiguration.Current?.ShareEyeShadows.Value == true;
    internal static void SetEnabled(bool enabled) => ModConfiguration.Instance.ShareEyeShadows.Value = enabled;
    // True while second-eye passes are actually being skipped.
    internal static bool Active => Time.frameCount - _lastReuseFrame < 10;

    private static bool IsSeparateEyePass(ref RenderingData data, out int eye)
    {
        var xr = data.cameraData.xr;
        eye = xr != null && xr.enabled && !xr.singlePassEnabled ? xr.multipassId : -1;
        return eye >= 0;
    }

    [HarmonyPatch(typeof(MainLightShadowCasterPass), nameof(MainLightShadowCasterPass.Setup))]
    private static class SetupPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(MainLightShadowCasterPass __instance, ref RenderingData renderingData, ref bool __result)
        {
            if (ReferenceEquals(_reusing, __instance))
                _reusing = null;
            if (!Enabled || !IsSeparateEyePass(ref renderingData, out var eye) || eye != 1 ||
                !renderingData.shadowData.supportsMainLightShadows ||
                !Maps.TryGetValue(__instance, out var first) || first.Frame != Time.frameCount ||
                !ReferenceEquals(first.Camera, renderingData.cameraData.camera))
                return true;
            // Keep the first eye's cascades, matrices and map as they are.
            CreateEmptyShadowmap(__instance) = false;
            _reusing = __instance;
            _lastReuseFrame = Time.frameCount;
            if (!_announced)
            {
                _announced = true;
                Debug.Log("[NOVR] Sharing shadow maps between eyes: the second eye reuses the first eye's sun shadow map.");
            }
            __result = true;
            return false;
        }

        [HarmonyPostfix]
        private static void Postfix(MainLightShadowCasterPass __instance, ref RenderingData renderingData, bool __result)
        {
            if (ReferenceEquals(_reusing, __instance))
                return;
            // Without shadow support the game's method returns before it
            // resets the cascade data, so an earlier first-eye map survives
            // (this is what NOVR's own interface camera does in between).
            if (!renderingData.shadowData.supportsMainLightShadows)
                return;
            if (!Maps.TryGetValue(__instance, out var first))
            {
                if (Maps.Count >= 16)
                    Maps.Clear();
                Maps[__instance] = first = new FirstEye();
            }
            if (Enabled && __result && !CreateEmptyShadowmap(__instance) &&
                IsSeparateEyePass(ref renderingData, out var eye) && eye == 0)
            {
                first.Frame = Time.frameCount;
                first.Camera = renderingData.cameraData.camera;
            }
            else
            {
                first.Frame = -1;
                first.Camera = null;
            }
        }
    }

    // URP clears the shadow map before the pass draws; a reused map must survive.
    [HarmonyPatch(typeof(MainLightShadowCasterPass), nameof(MainLightShadowCasterPass.Configure))]
    private static class ConfigurePatch
    {
        [HarmonyPostfix]
        private static void Postfix(MainLightShadowCasterPass __instance)
        {
            if (ReferenceEquals(_reusing, __instance))
                __instance.ConfigureClear(ClearFlag.None, Color.black);
        }
    }

    [HarmonyPatch(typeof(MainLightShadowCasterPass), nameof(MainLightShadowCasterPass.Execute))]
    private static class ExecutePatch
    {
        [HarmonyPrefix]
        private static void Prefix(MainLightShadowCasterPass __instance) => _skipSlices = ReferenceEquals(_reusing, __instance);

        [HarmonyFinalizer]
        private static void Finalizer(MainLightShadowCasterPass __instance)
        {
            _skipSlices = false;
            if (ReferenceEquals(_reusing, __instance))
                _reusing = null;
        }
    }

    // The cascade draws themselves. Everything else the pass does (keywords,
    // receiver constants, binding the map) still runs for the second eye.
    [HarmonyPatch]
    private static class RenderShadowSlicePatch
    {
        [HarmonyTargetMethod]
        private static MethodBase TargetMethod() => AccessTools.Method(typeof(ShadowUtils), nameof(ShadowUtils.RenderShadowSlice), new[]
        {
            typeof(CommandBuffer), typeof(ScriptableRenderContext).MakeByRefType(), typeof(ShadowSliceData).MakeByRefType(),
            typeof(ShadowDrawingSettings).MakeByRefType(), typeof(Matrix4x4), typeof(Matrix4x4)
        });

        [HarmonyPrefix]
        private static bool Prefix() => !_skipSlices;
    }
}
