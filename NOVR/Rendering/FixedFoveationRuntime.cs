using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace NOVR.Rendering;

// Owns capability discovery and user intent. Native rendering is strictly opt-in;
// no renderer, antialiasing, headset resolution or streaming setting is changed.
internal sealed partial class FixedFoveationRuntime : MonoBehaviour
{
    private static FixedFoveationRuntime? _instance;
    internal static bool IsAvailable => _instance != null && _instance._available && !_instance._faulted;
    internal static string Status { get; private set; } = "Checking foveated rendering support…";
    private FixedFoveationNative? _native;
    private Harmony? _harmony;
    private bool _initialized, _available, _faulted, _alive;
    private bool _diagnostics;
    private bool _diagnosticsBlocked;
    private string _diagnosticsReason = "";
    private FixedFoveationLevel _level;
    private int _resetThroughFrame = -1;
    private float _nextStatus;
    private ulong _previousLeft, _previousRight;
    private ulong _previousLeftSample, _previousRightSample;
    private string _cameraReason = "Waiting for a flight.";
    private string _lastLog = "";

    private void OnEnable()
    {
        _instance = this;
        _initialized = false; _available = false; _faulted = false;
        _native = null; _level = FixedFoveationLevel.Off; _diagnostics = false;
        _diagnosticsBlocked = false; _diagnosticsReason = "";
        _resetThroughFrame = -1; _nextStatus = 0;
        _previousLeft = _previousRight = _previousLeftSample = _previousRightSample = 0;
        _gazeFrame = -1;
        _gazeReadWarning = false;
        _targetOrientation = -1;
        _alive = true;
        RenderPipelineManager.endFrameRendering += AfterFrame;
    }

    private void Update()
    {
        if (!_initialized)
        {
            _initialized = true;
            Initialize();
        }
        if (_native == null || _faulted) return;
        try
        {
            if (_diagnostics) SampleFrame();
            else ResetFrameStats();
            var diagnostics = IsAvailable && XRSettings.enabled && !_diagnosticsBlocked &&
                ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value;
            if (_diagnostics != diagnostics)
            {
                if (!_native.SetDiagnostics(diagnostics))
                    StopDiagnostics("The driver could not start GPU measurements.");
                else _diagnostics = diagnostics;
                _resetThroughFrame = Time.frameCount + 2;
                _nextStatus = 0;
            }
            var level = FixedFoveationPolicy.EffectiveLevel(
                ModConfiguration.Instance.FixedFoveation.RequestedLevel, IsAvailable, XRSettings.enabled);
            if (_level != level)
            {
                if (!_native.Enable(level != FixedFoveationLevel.Off))
                    throw new InvalidOperationException("Driver did not accept the requested mode.");
                _level = level;
                _resetThroughFrame = Time.frameCount + 2;
                _nextStatus = 0;
            }
            if (Time.unscaledTime < _nextStatus) return;
            _nextStatus = Time.unscaledTime + .5f;
            var report = JsonUtility.FromJson<NativeReport>(_native.ReadReport());
            if (report == null) throw new InvalidOperationException("Missing driver status.");
            if (report.failed) throw new InvalidOperationException(report.reason);
            _available = report.supported;
            // A new unsupported report must disarm before this frame's passes,
            // not wait for the next Update to recompute the requested mode.
            if (!_available && (_level != FixedFoveationLevel.Off || _diagnostics))
            {
                _native.Enable(false);
                _native.SetDiagnostics(false);
                _level = FixedFoveationLevel.Off;
                _diagnostics = false;
                _resetThroughFrame = Time.frameCount + 2;
                IssueImmediate(_native.ResetEvent, IntPtr.Zero);
            }
            if (_diagnostics && !report.diagnosticsEnabled)
                StopDiagnostics(report.diagnosticsReason);
            var appliedBoth = report.leftApplied > _previousLeft && report.rightApplied > _previousRight;
            _previousLeft = report.leftApplied; _previousRight = report.rightApplied;
            var renderLevel = CurrentRenderLevel;
            var fallback = renderLevel != _level;
            appliedBoth &= report.leftProfile == (int)renderLevel - 1 && report.rightProfile == (int)renderLevel - 1;
            Status = !_available ? "Unavailable: " + report.reason :
                _level == FixedFoveationLevel.Off ?
                    (XRSettings.enabled ? "Off. Compatible NVIDIA hardware detected." : "Inactive outside VR.") :
                appliedBoth ? (fallback ? _level + " selected; Strong fallback active in both eyes." :
                    _level + ": shading commands applied to both eyes.") + "\n" + RateStatus(report, renderLevel) :
                _level + " selected. " + _cameraReason;
            if (_diagnostics)
            {
                var fresh = report.leftSequence > _previousLeftSample && report.rightSequence > _previousRightSample;
                var sameMode = report.leftSampleMode == (int)renderLevel && report.rightSampleMode == (int)renderLevel;
                var label = fallback ? "World opaque GPU (Strong fallback)" : "World opaque GPU";
                Status += fresh && sameMode && SamplesMatchRates(report, renderLevel) && report.leftGpuMs >= 0 && report.rightGpuMs >= 0
                    ? $"\n{label}: {report.leftGpuMs:F2} / {report.rightGpuMs:F2} ms · Pixel shader calls: {report.leftPixelInvocations / 1000000.0:F2} / {report.rightPixelInvocations / 1000000.0:F2} M"
                    : "\nGPU measurement: waiting for matching eye samples.";
                Status += FrameLine();
                _previousLeftSample = report.leftSequence; _previousRightSample = report.rightSequence;
            }
            else if (_diagnosticsBlocked && ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value)
                Status += "\nGPU measurement unavailable: " + _diagnosticsReason + "\nRestart the game to retry measurements.";
            if (_lastLog != Status)
            {
                _lastLog = Status;
                Debug.Log("[NOVR] Foveated rendering: " + Status.Replace('\n', ' '));
            }
        }
        catch (Exception error) { Fail(error); }
    }

    private static string RateStatus(NativeReport report, FixedFoveationLevel level)
    {
        if (level <= FixedFoveationLevel.Strong) return "Centre → edge: 1×1 / 1×1 / 2×2 shading.";
        if (report.leftProfile != (int)level - 1 || report.rightProfile != (int)level - 1 ||
            report.leftCoarsestRate != report.rightCoarsestRate)
            return "Waiting for matching eye shading rates.";
        if (report.leftCoarsestRate == 4) return "Centre → edge: 1×1 / 2×2 / 4×4 shading.";
        if (report.leftCoarsestRate != 2) return "Waiting for applied shading rates.";
        var msaa = report.leftClampReason == "msaa" || report.rightClampReason == "msaa";
        return "Centre → edge: 1×1 / 2×2 / 2×2 (" + (msaa ? "MSAA limit)." : "driver limit).");
    }

    private static bool SamplesMatchRates(NativeReport report, FixedFoveationLevel level)
    {
        var left = report.leftSampleCoarsestRate;
        var right = report.rightSampleCoarsestRate;
        if (level == FixedFoveationLevel.Off) return left == 1 && right == 1;
        if (report.leftProfile != (int)level - 1 || report.rightProfile != (int)level - 1) return false;
        if (level <= FixedFoveationLevel.Strong) return left == 2 && right == 2;
        // Rejected 4x4/MSAA changes must not mix coarse rates in one comparison.
        return (left == 2 || left == 4) && left == right &&
            report.leftProfile == (int)level - 1 && report.rightProfile == (int)level - 1 &&
            left == report.leftCoarsestRate && right == report.rightCoarsestRate;
    }

    private void Initialize()
    {
        try
        {
            var windows = Application.platform == RuntimePlatform.WindowsPlayer;
            var reason = FixedFoveationPolicy.UnavailableReason(windows,
                windows && FixedFoveationNative.IsWine(),
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11,
                SystemInfo.graphicsDeviceVendorID);
            if (reason != null) { Status = reason; return; }
            _native = new FixedFoveationNative();
            // One tiny resource obtains the actual Unity D3D11 device. Never
            // call GetNativeTexturePtr each frame: that can stall the renderer.
            var bootstrap = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                bootstrap.Apply(false, false);
                if (!_native.InitializeDevice(bootstrap.GetNativeTexturePtr()))
                    throw new InvalidOperationException("Could not identify Unity's D3D11 device.");
            }
            finally { Destroy(bootstrap); }
            PreparePasses();
            _harmony = new Harmony("silentheron.novr.fixed-foveation");
            var hook = AccessTools.Method(typeof(ScriptableRenderer), "AddRenderPasses") ??
                throw new MissingMethodException("Unsupported URP render-pass entry point.");
            _harmony.Patch(hook, postfix: new HarmonyMethod(typeof(FixedFoveationRuntime), nameof(AddPasses)));
            IssueImmediate(_native.ProbeEvent, IntPtr.Zero);
            Status = "Checking NVIDIA variable-rate shading support…";
        }
        catch (Exception error) { Fail(error); }
    }

    internal static void SetLevel(int value)
    {
        var level = FixedFoveationPolicy.Normalize(value);
        if (level != FixedFoveationLevel.Off && !IsAvailable) return;
        ModConfiguration.Instance.FixedFoveation.Level.Value = level;
        if (_instance != null) _instance._nextStatus = 0;
    }

    internal static void SetDiagnostics(bool enabled)
    {
        if (enabled && !IsAvailable) return;
        ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value = enabled;
        if (_instance != null)
        {
            _instance._diagnosticsBlocked = false;
            _instance._diagnosticsReason = "";
            _instance._nextStatus = 0;
        }
    }

    private void StopDiagnostics(string reason)
    {
        // Query support is independent of VRS. A failed measurement must not
        // switch off otherwise working shading or leave baseline passes active.
        _diagnostics = false;
        _diagnosticsBlocked = true;
        _diagnosticsReason = string.IsNullOrEmpty(reason) ? "Driver queries unavailable." : reason;
        _native?.SetDiagnostics(false);
        _resetThroughFrame = Time.frameCount + 2;
    }

    private void Fail(Exception error)
    {
        _faulted = true; _available = false; _level = FixedFoveationLevel.Off; _diagnostics = false;
        Status = "Unavailable: " + error.Message;
        if (_native != null)
        {
            try
            {
                _native.Enable(false);
                _native.SetDiagnostics(false);
                IssueImmediate(_native.ResetEvent, IntPtr.Zero);
            }
            catch { /* Keep the feature disarmed when the native boundary fails. */ }
        }
        Debug.LogWarning("[NOVR] Foveated rendering stopped: " + error.Message);
    }

    private void OnDisable()
    {
        _alive = false;
        RenderPipelineManager.endFrameRendering -= AfterFrame;
        _harmony?.UnpatchSelf(); _harmony = null;
        if (_native != null)
        {
            try
            {
                _native.Enable(false);
                _native.SetDiagnostics(false);
                // Render events already queued still own their packets. End
                // retires them before releasing driver resources at shutdown.
                if (_pending != IntPtr.Zero)
                    IssueImmediate(_native.EndEvent, _pending);
                _pending = IntPtr.Zero;
                IssueImmediate(_native.ShutdownEvent, IntPtr.Zero);
            }
            catch (Exception error) { Debug.LogWarning("[NOVR] Foveation cleanup: " + error.Message); }
        }
        _available = false;
        if (_instance == this) { _instance = null; Status = "Off."; }
    }

    // Native counters establish successful API calls, not shader-work savings.
    [Serializable]
    private sealed class NativeReport
    {
        public bool supported = false, failed = false;
        public string reason = "Waiting for render-thread probe.";
        public ulong leftApplied = 0, rightApplied = 0;
        public ulong leftSequence = 0, rightSequence = 0, leftPixelInvocations = 0, rightPixelInvocations = 0;
        public int leftSampleMode = -1, rightSampleMode = -1;
        public double leftGpuMs = -1, rightGpuMs = -1;
        public bool diagnosticsEnabled = false;
        public string diagnosticsReason = "Driver queries unavailable.";
        public int leftProfile = -1, rightProfile = -1;
        public int leftCoarsestRate = 0, rightCoarsestRate = 0;
        public string leftClampReason = "none", rightClampReason = "none";
        public int leftSampleCoarsestRate = 0, rightSampleCoarsestRate = 0;
    }
}
