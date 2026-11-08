using System.Reflection;
using HarmonyLib;
using NOVR;
using NOVR.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;
using Native = NOVR.Rendering.FixedFoveationNative;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    checks++;
}

foreach (var unsupported in new[]
{
    (RuntimePlatform.LinuxPlayer, false, 0x10DE, GraphicsDeviceType.Direct3D11),
    (RuntimePlatform.OSXPlayer, false, 0x10DE, GraphicsDeviceType.Direct3D11),
    (RuntimePlatform.WindowsPlayer, true, 0x10DE, GraphicsDeviceType.Direct3D11),
    (RuntimePlatform.WindowsPlayer, false, 0x1002, GraphicsDeviceType.Direct3D11),
    (RuntimePlatform.WindowsPlayer, false, 0x8086, GraphicsDeviceType.Direct3D11),
    (RuntimePlatform.WindowsPlayer, false, 0x10DE, GraphicsDeviceType.Direct3D12),
    (RuntimePlatform.WindowsPlayer, false, 0x10DE, GraphicsDeviceType.Vulkan)
})
{
    using var f = new Fixture(unsupported.Item1, unsupported.Item2, unsupported.Item3, unsupported.Item4);
    ModConfiguration.Instance.FixedFoveation.Level.Value = FixedFoveationLevel.Balanced;
    f.Tick();
    Check(!FixedFoveationRuntime.IsAvailable && FixedFoveationRuntime.Status.StartsWith("Unavailable"),
        "Unsupported configurations show a reason without claiming activity.");
    Check(Native.Constructed == 0 && Native.Initialized == 0 && Native.Events.Count == 0,
        "Linux, Wine, non-NVIDIA and non-D3D11 never construct or call the optional native component.");
    Check(Texture2D.Created == 0 && Harmony.Patches == 0,
        "Unsupported configurations do not create bootstrap textures or patch render passes.");
    var data = f.World();
    f.Render(ref data);
    RenderPipelineManager.EndFrame();
    f.Tick();
    Check(f.Renderer.Passes.Count == 0 && Native.Events.Count == 0, "Unsupported rendering stays dormant across frames.");
    FixedFoveationRuntime.SetLevel(3);
    Check(ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Balanced,
        "An unavailable mode cannot overwrite the existing preference.");
    FixedFoveationRuntime.SetLevel(0);
    Check(ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Off,
        "Off stays available even when the native component is unsupported.");
    FixedFoveationRuntime.SetDiagnostics(true);
    Check(!ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value, "Unsupported hardware cannot enable GPU diagnostics.");
    if (unsupported.Item1 != RuntimePlatform.WindowsPlayer)
        Check(Native.WineChecks == 0, "Non-Windows platforms do not call Windows Wine detection.");
}

using (var f = new Fixture())
{
    f.Tick();
    Check(FixedFoveationRuntime.IsAvailable && Native.Events.Select(e => e.id).SequenceEqual(new[] { 0 }),
        "Compatible default-Off startup issues one capability probe.");
    Check(Native.Enables.Count == 0 && !Native.Enabled && !Native.DiagnosticsEnabled,
        "Capability discovery does not enable shading reduction or optional measurement.");
    for (var frame = 0; frame < 5; frame++)
    {
        var data = f.World();
        f.Render(ref data);
        RenderPipelineManager.EndFrame();
        f.Tick();
        Check(f.Renderer.Passes.Count == 0, "Default Off does not enqueue per-camera rendering work.");
    }
    Check(Native.Events.Count == 1 && Native.AcquiredScopes.Count == 0, "Default Off does not submit per-frame native events.");
    Check(Texture2D.Created == 1 && Texture2D.Destroyed == 1 && Texture2D.NativePointerReads == 1,
        "The bootstrap texture is temporary and its native pointer is queried once.");
    Check(FixedFoveationRuntime.Status.StartsWith("Off."), "Preference Off is described as Off after a successful probe.");
    Check(XRSettings.eyeTextureResolutionScale == 1.25f && SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11,
        "The rendering preference leaves headset resolution and graphics backend untouched.");
}

using (var f = new Fixture())
{
    f.Activate();
    Check(FixedFoveationRuntime.Status.Contains("selected") && !FixedFoveationRuntime.Status.Contains("applied to both"),
        "A selected preference alone cannot claim that shading commands ran for both eyes.");
    Native.Events.Clear();
    foreach (var eye in new[] { 0, 1 })
    {
        var data = f.World(eye);
        data.cameraData.xr.Projection = new Matrix4x4 { m02 = eye == 0 ? .2f : -.2f, m12 = eye == 0 ? .1f : -.1f };
        f.Render(ref data);
        Check(f.Renderer.Passes.Select(p => (int)p.renderPassEvent).SequenceEqual(new[] { 0, 249, 299 }),
            "The outside-world scope begins before opaque rendering and ends before skybox/transparents.");
        Check(f.Renderer.Passes.All(p => !p.useNativeRenderPass), "VRS boundaries remain separate render passes.");
        Check(f.Renderer.Passes[1].ConfiguredTargets > 0, "Begin binds the current camera colour and depth targets.");
    }
    Check(Native.Events.Select(e => e.id).SequenceEqual(new[] { 3, 1, 2, 3, 1, 2 }),
        "Each eligible eye is bracketed by full-rate reset, Begin and matching End.");
    Check(Native.AcquiredScopes.Count == 2 && Native.LiveScopes.Count == 0,
        "Each eye owns a separate scope and releases it after opaque rendering.");
    var left = Native.AcquiredScopes[0];
    var right = Native.AcquiredScopes[1];
    Check(left.Eye == 0 && right.Eye == 1 && left.Samples == 4 && right.Samples == 4 && left.Width == 2000 && left.Height == 2000,
        "The native boundary receives actual eye identity, target dimensions and MSAA.");
    Check(Math.Abs(left.X - .4f) < .00001f && Math.Abs(right.X - .6f) < .00001f && left.Y > .5f && right.Y < .5f,
        "Each eye uses its own asymmetric projection centre.");
    f.Tick();
    Check(FixedFoveationRuntime.Status.Contains("both eyes") && FixedFoveationRuntime.Status.Contains("1×1 / 1×1 / 2×2"),
        "Both-eye command confirmation reports the actual shading pattern without claiming a performance gain.");
    FixedFoveationRuntime.SetLevel(3);
    f.Tick();
    var stronger = f.World();
    f.Render(ref stronger);
    Check(Native.AcquiredScopes[^1].Level == FixedFoveationLevel.Strong,
        "A strength change reaches the next world scope without rebuilding renderer settings.");
    Check(CommandBufferPool.Acquired == CommandBufferPool.Released, "Rendering returns every borrowed command buffer.");
}

using (var f = new Fixture())
{
    f.Activate();
    var data = f.World();
    f.Renderer.AddRenderPasses(ref data);
    f.Execute(f.Renderer.Passes[0], ref data);
    f.Execute(f.Renderer.Passes[1], ref data);
    var pending = Native.AcquiredScopes.Single().Packet;
    FixedFoveationRuntime.SetLevel(0);
    f.Tick();
    Check(!Native.Enabled, "Switching Off disarms the native backend before later passes run.");
    f.Execute(f.Renderer.Passes[2], ref data);
    Check(Native.Events.Any(e => e.id == 2 && e.packet == pending) && Native.LiveScopes.Count == 0,
        "A queued End still releases its matching scope after the preference changes to Off.");
    for (var frame = 0; frame < 4; frame++)
        f.Tick();
    Native.Events.Clear();
    f.Render(ref data);
    RenderPipelineManager.EndFrame();
    Check(f.Renderer.Passes.Count == 0 && Native.Events.Count == 0,
        "After bounded reset cleanup, Off returns to zero per-frame native work.");
}

using (var f = new Fixture())
{
    f.Activate();
    var otherCamera = f.World();
    otherCamera.cameraData.camera = new Camera();
    f.Render(ref otherCamera);
    Check(f.Renderer.Passes.Count == 1 && Native.AcquiredScopes.Count == 0,
        "HUD, instrument, map and other cameras receive only a full-rate reset.");
    foreach (var kind in new[] { "menu", "no aircraft", "offscreen", "overlay", "provider", "provider pointer", "single pass", "deferred", "bad projection" })
    {
        var data = f.World();
        CameraStateManager.cameraMode = CameraMode.cockpit;
        GameManager.HasAircraft = true;
        APIBus.MainCamera.targetTexture = null;
        switch (kind)
        {
            case "menu":
                CameraStateManager.cameraMode = CameraMode.menu;
                break;
            case "no aircraft":
                GameManager.HasAircraft = false;
                break;
            case "offscreen":
                APIBus.MainCamera.targetTexture = new object();
                break;
            case "overlay":
                data.cameraData.renderType = CameraRenderType.Overlay;
                break;
            case "provider":
                data.cameraData.xr.supportsFoveatedRendering = true;
                break;
            case "provider pointer":
                data.cameraData.xr.foveatedRenderingInfo = (IntPtr)19;
                break;
            case "single pass":
                data.cameraData.xr.singlePassEnabled = true;
                break;
            case "deferred":
                data.cameraData.renderer = new UniversalRenderer { renderingModeRequested = RenderingMode.Deferred };
                break;
            case "bad projection":
                data.cameraData.xr.Projection = new Matrix4x4 { m02 = float.NaN };
                break;
        }
        f.Render(ref data);
        Check(Native.AcquiredScopes.Count == 0 && Native.LiveScopes.Count == 0,
            "The actual runtime excludes " + kind + " from shading scopes.");
    }
}

using (var f = new Fixture())
{
    f.Activate();
    var data = f.World();
    f.Renderer.AddRenderPasses(ref data);
    f.Execute(f.Renderer.Passes[1], ref data);
    Check(Native.LiveScopes.Count == 1, "The fixture leaves a submitted world scope outstanding.");
    RenderPipelineManager.EndFrame();
    Check(Native.LiveScopes.Count == 0 && Native.Events[^1].id == 3,
        "The frame-end guard retires a missing End and restores full-rate rendering.");
    f.Execute(f.Renderer.Passes[1], ref data);
    f.Disable();
    Check(Native.LiveScopes.Count == 0 && !Native.Enabled && Native.Events[^1].id == 4,
        "Teardown ends queued ownership before shutting down the native backend.");
    Check(!FixedFoveationRuntime.IsAvailable && RenderPipelineManager.Subscribers == 0 && Harmony.Unpatches == 1,
        "Teardown clears availability and removes its managed render hooks.");
    f.Reenable();
    f.Tick();
    f.Tick();
    Check(FixedFoveationRuntime.IsAvailable && Native.Constructed == 2 && RenderPipelineManager.Subscribers == 1 && Harmony.Patches == 2,
        "Re-enabling re-probes the device and installs exactly one set of managed hooks.");
    f.Render(ref data);
    Check(Native.LiveScopes.Count == 0 && Native.Enabled, "The saved mode can resume with fresh scoped ownership after re-enable.");
}

foreach (var failure in new[] { "initialize", "enable", "acquire", "driver report", "unsupported report" })
{
    using var f = new Fixture();
    if (failure == "initialize")
        Native.RejectInitialize = true;
    f.Tick();
    if (failure != "initialize")
    {
        if (failure == "enable")
            Native.RejectEnable = true;
        FixedFoveationRuntime.SetLevel(1);
        f.Tick();
        if (failure == "acquire")
            Native.ThrowAcquire = true;
        if (failure == "driver report")
        {
            Native.Failed = true;
            Native.Reason = "driver context lost";
            f.Tick();
        }
        if (failure == "unsupported report")
        {
            Native.Supported = false;
            f.Tick();
            Check(!Native.Enabled && !FixedFoveationRuntime.IsAvailable,
                "Loss of support disarms the backend during the same update that reads the report.");
        }
        var data = f.World();
        f.Render(ref data);
    }
    Check(!FixedFoveationRuntime.IsAvailable && !Native.Enabled,
        "Native " + failure + " leaves the feature unavailable and disarmed.");
    Check(Native.LiveScopes.Count == 0, "Native failures do not strand an acquired render scope.");
    var acquired = Native.AcquiredScopes.Count;
    var later = f.World();
    f.Render(ref later);
    Check(Native.AcquiredScopes.Count == acquired, "A failed backend does not submit further shading scopes.");
}

using (var f = new Fixture())
{
    f.Activate();
    Native.RejectDiagnostics = true;
    FixedFoveationRuntime.SetDiagnostics(true);
    f.Tick();
    Check(FixedFoveationRuntime.IsAvailable && Native.Enabled && !Native.DiagnosticsEnabled,
        "A rejected diagnostic request cannot disable otherwise available VRS.");
    Check(ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value &&
          FixedFoveationRuntime.Status.Contains("GPU measurement unavailable"),
        "A rejected request keeps the preference and reports measurement unavailability.");
    var requests = Native.DiagnosticRequests.Count;
    f.Tick();
    Check(Native.DiagnosticRequests.Count == requests,
        "A rejected diagnostic request is blocked instead of retried on each update.");
    var data = f.World();
    f.Render(ref data);
    Check(Native.AcquiredScopes.Count == 1 && Native.LiveScopes.Count == 0,
        "World shading scopes remain balanced when optional GPU queries are rejected.");
}

using (var f = new Fixture())
{
    f.Activate();
    XRSettings.enabled = false;
    f.Tick();
    Check(!Native.Enabled && ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Mild,
        "Leaving VR disarms rendering while preserving the selected preference.");
    Check(FixedFoveationRuntime.Status.Contains("Inactive outside VR"), "Flat mode displays an inactive status.");
}

using (var f = new Fixture())
{
    f.Activate();
    FixedFoveationRuntime.SetDiagnostics(true);
    f.Tick();
    var data = f.World();
    f.Renderer.AddRenderPasses(ref data);
    f.Execute(f.Renderer.Passes[1], ref data);
    var outstanding = Native.AcquiredScopes.Single().Packet;
    Native.Supported = false;
    Native.Reason = "device context unavailable";
    f.Tick();
    Check(!Native.Enabled && !Native.DiagnosticsEnabled && !FixedFoveationRuntime.IsAvailable,
        "A lost device immediately disables shading and diagnostics in the same update.");
    Check(FixedFoveationRuntime.Status.Contains("device context unavailable"),
        "A lost device reports the capability reason instead of an active mode.");
    Check(ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Mild &&
          ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value,
        "Device loss preserves both saved preferences while disarming actual work.");
    f.Execute(f.Renderer.Passes[2], ref data);
    Check(Native.LiveScopes.Count == 0 && Native.Events.Any(e => e.id == 2 && e.packet == outstanding),
        "A scope submitted before device loss still receives its paired End.");
    var count = Native.AcquiredScopes.Count;
    f.Render(ref data);
    Check(Native.AcquiredScopes.Count == count && f.Renderer.Passes.All(p => (int)p.renderPassEvent == 0),
        "The first camera after support loss can only reset full-rate state, never acquire another scope.");
}

foreach (var level in new[] { FixedFoveationLevel.Off, FixedFoveationLevel.Mild })
{
    using var f = new Fixture();
    f.Tick();
    FixedFoveationRuntime.SetLevel((int)level);
    FixedFoveationRuntime.SetDiagnostics(true);
    f.Tick();
    Check(Native.DiagnosticsEnabled && Native.Enabled == (level != FixedFoveationLevel.Off),
        "The diagnostic failure fixture starts with independent measurement and shading controls.");
    Native.DiagnosticsEnabled = false;
    Native.DiagnosticsReason = "GPU query creation failed";
    f.Tick();
    Check(FixedFoveationRuntime.IsAvailable && Native.Enabled == (level != FixedFoveationLevel.Off),
        "A GPU query failure leaves supported VRS and its requested mode available.");
    Check(ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value &&
          ModConfiguration.Instance.FixedFoveation.RequestedLevel == level,
        "A query failure preserves the user's measurement and shading preferences.");
    Check(FixedFoveationRuntime.Status.Contains("GPU query creation failed") &&
          !FixedFoveationRuntime.Status.Contains("World opaque GPU"),
        "Failed measurements display the reason without stale timing values.");
    var requests = Native.DiagnosticRequests.Count;
    for (var i = 0; i < 4; i++)
        f.Tick();
    Check(Native.DiagnosticRequests.Count == requests && !Native.DiagnosticsEnabled,
        "A failed GPU query stays blocked rather than being retried every update.");
    var data = f.World();
    var acquired = Native.AcquiredScopes.Count;
    f.Render(ref data);
    Check(Native.AcquiredScopes.Count == acquired + (level == FixedFoveationLevel.Off ? 0 : 1),
        "VRS continues after query failure; Off with blocked diagnostics stays dormant.");
    Native.RejectDiagnostics = true;
    FixedFoveationRuntime.SetDiagnostics(true);
    f.Tick();
    Check(Native.DiagnosticRequests.Count == requests + 2 && !Native.DiagnosticsEnabled && FixedFoveationRuntime.IsAvailable,
        "If the native query failure remains latched, an explicit retry fails only measurements and returns to the blocked state.");
    requests = Native.DiagnosticRequests.Count;
    f.Tick();
    Check(Native.DiagnosticRequests.Count == requests,
        "A failed explicit retry does not start an automatic retry loop.");
    Native.RejectDiagnostics = false;
    FixedFoveationRuntime.SetDiagnostics(true);
    f.Tick();
    Check(Native.DiagnosticRequests.Count == requests + 1 && Native.DiagnosticsEnabled,
        "An explicit measurement request clears the block and retries once.");
    Check(!FixedFoveationRuntime.Status.Contains("GPU query creation failed"),
        "A successful explicit retry clears the previous query error.");
    FixedFoveationRuntime.SetDiagnostics(false);
    f.Tick();
    Check(!Native.DiagnosticsEnabled && !ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value,
        "The user can still turn optional measurements off after a failure and retry.");
}

using (var f = new Fixture())
{
    f.Tick();
    FixedFoveationRuntime.SetDiagnostics(true);
    f.Tick();
    Check(Native.DiagnosticsEnabled && !Native.Enabled && ModConfiguration.Instance.FixedFoveation.RequestedLevel == FixedFoveationLevel.Off,
        "Explicit measurement can collect an Off baseline without enabling reduced shading.");
    foreach (var eye in new[] { 0, 1 })
    {
        var data = f.World(eye);
        f.Render(ref data);
    }
    Check(Native.AcquiredScopes.Count == 2 && Native.LiveScopes.Count == 0 && Native.LeftApplied == 0 && Native.RightApplied == 0,
        "The Off baseline brackets both eyes while native reduction remains disabled.");
    Check(Native.AcquiredScopes.All(s => s.Level == FixedFoveationLevel.Mild),
        "Diagnostic scopes use a valid native profile while the separate enable flag stays Off.");
    Native.LeftSequence = Native.RightSequence = 1;
    Native.LeftSampleMode = Native.RightSampleMode = 0;
    Native.LeftSampleCoarsestRate = Native.RightSampleCoarsestRate = 1;
    Native.LeftGpuMs = 2.5;
    Native.RightGpuMs = 2.6;
    f.Tick();
    Check(FixedFoveationRuntime.Status.Contains("World opaque GPU") && FixedFoveationRuntime.Status.Contains("Pixel shader calls"),
        "Fresh matching eye samples can show the measured world-opaque interval and invocation counts.");
    f.Tick();
    Check(FixedFoveationRuntime.Status.Contains("waiting for matching eye samples"),
        "Stale measurements are not presented as current GPU costs.");
    Native.LeftSequence = Native.RightSequence = 2;
    Native.LeftSampleMode = Native.RightSampleMode = 1;
    f.Tick();
    Check(FixedFoveationRuntime.Status.Contains("waiting for matching eye samples"),
        "Measurements from another shading mode cannot be shown as an Off baseline.");
    FixedFoveationRuntime.SetDiagnostics(false);
    for (var frame = 0; frame < 4; frame++)
        f.Tick();
    Native.Events.Clear();
    var quiet = f.World();
    f.Render(ref quiet);
    RenderPipelineManager.EndFrame();
    Check(!Native.DiagnosticsEnabled && Native.Events.Count == 0,
        "Disabling measurements returns Off to a dormant per-frame path.");
}

GazeRuntimeChecks.Run(Check);
StrongerLevelsChecks.Run(Check);
StrongerMeasurementChecks.Run(Check);
FlippedTargetChecks.Run(Check);
Console.WriteLine($"PASS {checks} source-linked fixed-foveation runtime checks (Unity/URP/native boundary doubles; no GPU assertions).");

internal sealed class Fixture : IDisposable
{
    internal readonly FixedFoveationRuntime Runtime = new();
    internal readonly UniversalRenderer Renderer = new();
    private bool _disabled;

    internal Fixture(RuntimePlatform platform = RuntimePlatform.WindowsPlayer, bool wine = false, int vendor = 0x10DE,
        GraphicsDeviceType graphics = GraphicsDeviceType.Direct3D11)
    {
        Native.ResetFixture();
        UnityEngine.XR.OpenXR.NovrEyeGaze.Reset();
        Native.Wine = wine;
        Application.platform = platform;
        SystemInfo.graphicsDeviceVendorID = vendor;
        SystemInfo.graphicsDeviceType = graphics;
        ModConfiguration.Instance = new();
        APIBus.MainCamera = new();
        XRSettings.enabled = true;
        XRSettings.eyeTextureResolutionScale = 1.25f;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        GameManager.HasAircraft = true;
        Time.frameCount = 1;
        Time.unscaledTime = 0;
        Texture2D.Created = Texture2D.Destroyed = Texture2D.NativePointerReads = 0;
        CommandBufferPool.Acquired = CommandBufferPool.Released = 0;
        Harmony.Patches = Harmony.Unpatches = 0;
        Debug.Logs.Clear();
        Debug.Warnings.Clear();
        Invoke("OnEnable");
    }
    internal void Tick()
    {
        Time.frameCount++;
        Time.unscaledTime += 1;
        Invoke("Update");
    }
    internal void Activate()
    {
        Tick();
        FixedFoveationRuntime.SetLevel(1);
        Tick();
    }
    internal RenderingData World(int eye = 0) => new()
    {
        cameraData = new CameraData
        {
            camera = APIBus.MainCamera,
            renderer = Renderer,
            xr = new XrPass { multipassId = eye, View = Matrix4x4.EyeView(APIBus.MainCamera.transform.rotation, APIBus.MainCamera.transform.position) },
            renderType = CameraRenderType.Base,
            cameraTargetDescriptor = new RenderTextureDescriptor
            {
                width = 2000,
                height = 2000,
                volumeDepth = 1,
                dimension = TextureDimension.Tex2D,
                msaaSamples = 4
            }
        }
    };
    internal void Execute(ScriptableRenderPass pass, ref RenderingData data)
    {
        pass.OnCameraSetup(new CommandBuffer(), ref data);
        pass.Execute(default, ref data);
    }
    internal void Render(ref RenderingData data)
    {
        var renderer = data.cameraData.renderer;
        renderer.AddRenderPasses(ref data);
        foreach (var pass in renderer.Passes.OrderBy(p => (int)p.renderPassEvent))
            Execute(pass, ref data);
    }
    internal void Disable()
    {
        if (_disabled)
            return;
        _disabled = true;
        Runtime.isActiveAndEnabled = false;
        Invoke("OnDisable");
    }
    internal void Reenable()
    {
        if (!_disabled)
            throw new InvalidOperationException("Fixture must be disabled before re-enable.");
        _disabled = false;
        Runtime.isActiveAndEnabled = true;
        Invoke("OnEnable");
    }
    private void Invoke(string name)
    {
        try
        {
            typeof(FixedFoveationRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Runtime, null);
        }
        catch (TargetInvocationException error) { throw error.InnerException!; }
    }
    public void Dispose() => Disable();
}
