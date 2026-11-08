using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace NOVR.Rendering;

// URP draws all forward opaques, including alpha-tested ones (queue 2450-2500,
// the game's alpha-to-coverage trees and grass), in one DrawObjectsPass. Coarse
// shading quantizes their coverage into blocks that shimmer. While a world
// shading scope is open for this eye, draw that pass in two halves: ordinary
// opaques under the scope, then end the scope and draw alpha-tested opaques at
// full rate. Same pass, culling, sorting and targets; only the queue filter
// differs. GPU measurements therefore cover the reduced half only.
internal sealed partial class FixedFoveationRuntime
{
    internal const int AlphaTestQueue = (int)RenderQueue.AlphaTest;
    private static readonly AccessTools.FieldRef<UniversalRenderer, DrawObjectsPass>? OpaquePass =
        TryField<UniversalRenderer, DrawObjectsPass>("m_RenderOpaqueForwardPass");
    private static readonly AccessTools.FieldRef<DrawObjectsPass, FilteringSettings>? Filtering =
        TryField<DrawObjectsPass, FilteringSettings>("m_FilteringSettings");
    private static bool _splittingFoliage;

    private static AccessTools.FieldRef<TOwner, TField>? TryField<TOwner, TField>(string name)
    {
        try { return AccessTools.FieldRefAccess<TOwner, TField>(name); }
        catch (Exception error)
        {
            Debug.LogWarning("[NOVR] Full-rate foliage unavailable (URP field " + name + "): " + error.Message);
            return null;
        }
    }

    private bool ShouldSplitFoliage(DrawObjectsPass pass, ref RenderingData data)
    {
        if (_splittingFoliage || _pending == IntPtr.Zero || OpaquePass == null || Filtering == null ||
            !ModConfiguration.Instance.FixedFoveation.FullRateFoliage.Value)
            return false;
        var camera = data.cameraData.camera;
        if (camera == null || camera != APIBus.MainCamera || data.cameraData.renderer is not UniversalRenderer renderer)
            return false;
        if (!ReferenceEquals(OpaquePass(renderer), pass))
            return false;
        var range = Filtering(pass).renderQueueRange;
        return range.lowerBound < AlphaTestQueue && range.upperBound >= AlphaTestQueue;
    }

    [HarmonyPatch(typeof(DrawObjectsPass), nameof(DrawObjectsPass.Execute))]
    private static class FoliageSplit
    {
        [HarmonyPrefix]
        private static void Prefix(DrawObjectsPass __instance, ref RenderingData renderingData, out FilteringSettings? __state)
        {
            __state = null;
            var owner = _instance;
            if (owner == null || !owner.ShouldSplitFoliage(__instance, ref renderingData))
                return;
            var settings = Filtering!(__instance);
            __state = settings;
            var reduced = settings;
            reduced.renderQueueRange = new RenderQueueRange(settings.renderQueueRange.lowerBound, AlphaTestQueue - 1);
            Filtering(__instance) = reduced;
        }

        [HarmonyPostfix]
        private static void Postfix(DrawObjectsPass __instance, ScriptableRenderContext context, ref RenderingData renderingData,
            FilteringSettings? __state)
        {
            if (__state is not { } settings)
                return;
            var owner = _instance;
            try
            {
                // Retire this eye's scope first: everything after it is full rate,
                // and the regular End pass then finds nothing left to close.
                owner?.End(context);
                var foliage = settings;
                foliage.renderQueueRange = new RenderQueueRange(AlphaTestQueue, settings.renderQueueRange.upperBound);
                Filtering!(__instance) = foliage;
                _splittingFoliage = true;
                __instance.Execute(context, ref renderingData);
            }
            catch (Exception error) { owner?.Fail(error); }
            finally
            {
                _splittingFoliage = false;
                Filtering!(__instance) = settings;
            }
        }

        // A native exception before the postfix must still restore the shared pass.
        [HarmonyFinalizer]
        private static void Finalizer(DrawObjectsPass __instance, FilteringSettings? __state)
        {
            if (__state is { } settings && Filtering != null && !_splittingFoliage)
                Filtering(__instance) = settings;
        }
    }
}
