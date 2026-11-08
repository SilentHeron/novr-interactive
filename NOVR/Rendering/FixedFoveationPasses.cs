using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NOVR.Rendering;

internal sealed partial class FixedFoveationRuntime
{
    private static readonly PropertyInfo? NativeRenderPass = typeof(ScriptableRenderPass)
        .GetProperty("useNativeRenderPass", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly PropertyInfo? RendererMode = typeof(UniversalRenderer)
        .GetProperty("renderingModeRequested", BindingFlags.NonPublic | BindingFlags.Instance);
    private readonly Dictionary<ScriptableRenderer, bool> _forward = new();
    private FoveationPass? _reset, _begin, _end;
    private IntPtr _pending;
    private int _targetOrientation = -1;

    private void NoteTargetOrientation(bool flipped)
    {
        // One line per change lets a log confirm which target layout the
        // centre was mapped to; the orientation is stable in normal play.
        var state = flipped ? 1 : 0;
        if (_targetOrientation == state) return;
        _targetOrientation = state;
        Debug.Log(flipped
            ? "[NOVR] Foveated rendering: the eye target is an upside-down intermediate texture; mirroring the protected centre vertically."
            : "[NOVR] Foveated rendering: the eye target is drawn upright; using the projected centre directly.");
    }

    private void PreparePasses()
    {
        _reset = new FoveationPass(this, 0, 0);
        _begin = new FoveationPass(this, 249, 1);
        _end = new FoveationPass(this, 299, 2);
    }

    private static void AddPasses(ScriptableRenderer __instance, ref RenderingData renderingData)
    {
        var owner = _instance;
        if (owner == null || !owner._alive || owner._faulted || owner._native == null ||
            owner._reset == null || (owner._level == FixedFoveationLevel.Off && !owner._diagnostics && Time.frameCount > owner._resetThroughFrame))
            return;
        // Each camera starts full-rate, including instrument/map/menu cameras.
        __instance.EnqueuePass(owner._reset);
        if ((owner._level == FixedFoveationLevel.Off && !owner._diagnostics) || !owner.isActiveAndEnabled) return;
        if (renderingData.cameraData.camera != APIBus.MainCamera) return;
        __instance.EnqueuePass(owner._begin!);
        __instance.EnqueuePass(owner._end!);
    }

    private sealed class FoveationPass : ScriptableRenderPass
    {
        private readonly FixedFoveationRuntime _owner;
        private readonly int _phase;
        internal FoveationPass(FixedFoveationRuntime owner, int timing, int phase)
        {
            _owner = owner; _phase = phase; renderPassEvent = (RenderPassEvent)timing;
            if (NativeRenderPass == null) throw new MissingMemberException("URP native render-pass control.");
            NativeRenderPass.SetValue(this, false);
            if (NativeRenderPass.GetValue(this) is not false)
                throw new InvalidOperationException("Cannot safely bracket the world render pass.");
        }
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
        {
            if (_phase == 1)
                ConfigureTarget(data.cameraData.renderer.cameraColorTargetHandle, data.cameraData.renderer.cameraDepthTargetHandle);
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            try
            {
                // End stays unconditional for scopes queued before Off/teardown.
                if (_phase == 2) _owner.End(context);
                else if (_phase == 0) _owner.Issue(context, _owner._native!.ResetEvent, IntPtr.Zero);
                else _owner.Begin(context, ref data);
            }
            catch (Exception error) { _owner.Fail(error); }
        }
    }

    private bool IsForward(ScriptableRenderer renderer)
    {
        if (_forward.TryGetValue(renderer, out var forward)) return forward;
        var mode = renderer is UniversalRenderer ? RendererMode?.GetValue(renderer)?.ToString() : null;
        forward = mode == "Forward" || mode == "ForwardPlus";
        if (_forward.Count < 16) _forward.Add(renderer, forward);
        return forward;
    }

    private void Begin(ScriptableRenderContext context, ref RenderingData data)
    {
        End(context); // A missing prior End must not leak state into this eye.
        if (!_alive || !_available || _faulted || !isActiveAndEnabled || _native == null ||
            (_level == FixedFoveationLevel.Off && !_diagnostics)) return;
        try
        {
            var cameraData = data.cameraData;
            var camera = cameraData.camera;
            var xr = cameraData.xr;
            var target = cameraData.cameraTargetDescriptor;
            var viewport = xr.enabled ? xr.GetViewport(0) : default;
            var cockpit = CameraStateManager.cameraMode == CameraMode.cockpit &&
                GameManager.GetLocalAircraft(out var aircraft) && aircraft != null;
            var reason = FixedFoveationCameraPolicy.Reject(cockpit && camera != null && camera == APIBus.MainCamera,
                camera == null || camera.targetTexture != null || camera.cameraType != CameraType.Game,
                xr.enabled, xr.singlePassEnabled, cameraData.renderType == CameraRenderType.Base,
                IsForward(cameraData.renderer), xr.viewCount, xr.multipassId,
                target.dimension == TextureDimension.Tex2D, target.volumeDepth, target.width, target.height,
                target.msaaSamples, viewport.x, viewport.y, viewport.width, viewport.height,
                xr.supportsFoveatedRendering || xr.foveatedRenderingInfo != IntPtr.Zero);
            if (reason != null) { _cameraReason = reason; return; }
            var projection = xr.GetProjMatrix(0);
            // URP's own matrix setup uses this answer to flip the projection for
            // intermediate textures; the rate map must address the same rows.
            var flipped = cameraData.IsCameraProjectionMatrixFlipped();
            NoteTargetOrientation(flipped);
            if (!ResolveCenter(cameraData, projection, flipped, out var x, out var y))
            { _cameraReason = "Unsupported eye projection."; return; }
            var packet = _native.AcquireScope(target.width, target.height, target.msaaSamples, xr.multipassId, x, y,
                _level == FixedFoveationLevel.Off ? FixedFoveationLevel.Mild : CurrentRenderLevel);
            if (packet == IntPtr.Zero) { _cameraReason = "Waiting for an available render scope."; return; }
            // Ownership passes to the render queue; never free this packet on
            // the main thread once Begin may have been submitted.
            _pending = packet;
            Issue(context, _native.BeginEvent, packet);
            _cameraReason = "Waiting for both eyes to confirm shading commands.";
        }
        catch (Exception error) { Fail(error); End(context); }
    }

    private void End(ScriptableRenderContext context)
    {
        if (_pending == IntPtr.Zero || _native == null) return;
        var packet = _pending; _pending = IntPtr.Zero;
        Issue(context, _native.EndEvent, packet);
    }

    private void Issue(ScriptableRenderContext context, int eventId, IntPtr packet)
    {
        if (_native == null) return;
        var command = CommandBufferPool.Get("NOVR fixed foveation");
        try
        {
            command.IssuePluginEventAndData(_native.Callback, eventId, packet);
            context.ExecuteCommandBuffer(command);
        }
        finally { CommandBufferPool.Release(command); }
    }

    private void IssueImmediate(int eventId, IntPtr packet)
    {
        if (_native == null) return;
        var command = CommandBufferPool.Get("NOVR fixed foveation lifecycle");
        try
        {
            command.IssuePluginEventAndData(_native.Callback, eventId, packet);
            Graphics.ExecuteCommandBuffer(command);
        }
        finally { CommandBufferPool.Release(command); }
    }

    private void AfterFrame(ScriptableRenderContext context, Camera[] cameras)
    {
        if (_native == null || (_level == FixedFoveationLevel.Off && !_diagnostics && Time.frameCount > _resetThroughFrame)) return;
        try
        {
            End(context);
            Issue(context, _native.ResetEvent, IntPtr.Zero);
        }
        catch (Exception error) { Fail(error); }
    }
}
