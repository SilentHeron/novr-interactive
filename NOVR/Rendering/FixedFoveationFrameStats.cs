using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.Rendering;

// The native GPU queries time only the world-opaque scope. Whole-frame numbers
// decide whether foveation helps at all (a CPU-bound mission may not), so the
// measurement readout also shows the smoothed frame interval and, when the XR
// runtime reports it, the application's total GPU time per frame.
internal sealed partial class FixedFoveationRuntime
{
    private static readonly List<XRDisplaySubsystem> Displays = new();
    private float _frameMs = -1, _appGpuMs = -1;

    private void SampleFrame()
    {
        var interval = Time.unscaledDeltaTime * 1000f;
        if (interval > 0 && interval < 250)
            _frameMs = _frameMs < 0 ? interval : Mathf.Lerp(_frameMs, interval, .05f);
        Displays.Clear();
        SubsystemManager.GetInstances(Displays);
        foreach (var display in Displays)
        {
            // Unity reports seconds; ignore runtimes that return nothing useful.
            if (display == null || !display.running || !display.TryGetAppGPUTimeLastFrame(out var seconds) ||
                !(seconds > 0) || seconds > .25f)
                continue;
            var gpu = seconds * 1000f;
            _appGpuMs = _appGpuMs < 0 ? gpu : Mathf.Lerp(_appGpuMs, gpu, .05f);
            break;
        }
    }

    private void ResetFrameStats() => _frameMs = _appGpuMs = -1;

    private string FrameLine() => _frameMs <= 0 ? "" :
        $"\nWhole frame: {_frameMs:F1} ms ({1000f / _frameMs:F0} fps) · app GPU " +
        (_appGpuMs > 0 ? $"{_appGpuMs:F1} ms" : "not reported by runtime");
}
