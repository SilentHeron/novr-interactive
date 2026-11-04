using System;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace NOVR.MotionControls;

// Read-only performance information, refreshed on demand after the startup log.
internal sealed class PcVrCapabilities : MonoBehaviour
{
    internal static string Report { get; private set; } = "Waiting for the VR runtime…";
    private float _nextCheck;
    private bool _reported;
    private bool _queryWarning;
    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 1;
        if (!XRSettings.enabled) { Report = "VR runtime is not active."; _reported = false; _queryWarning = false; return; }
        if (_reported && !WristMenu.IsPerformancePage) return;
        try
        {
            var runtime = OpenXRRuntime.name;
            if (string.IsNullOrWhiteSpace(runtime)) runtime = "OpenXR";
            var backend = SystemInfo.graphicsDeviceType.ToString();
            var width = XRSettings.eyeTextureWidth;
            var height = XRSettings.eyeTextureHeight;
            var eyeSize = width > 0 && height > 0 ? $"{width} × {height} per eye" : "Waiting for eye textures";
            Report = $"Runtime: {runtime}\nGraphics: {backend}\n\nHeadset rendering: {eyeSize}\n" +
                $"Desktop mirror: {Screen.width} × {Screen.height}\n\n" +
                "Lower headset resolution if the GPU is overloaded.\n" +
                "A lower refresh rate allows more time per frame.\n\n" +
                "Mirror size does not change headset resolution.";
            _queryWarning = false;
            if (!_reported)
            {
                Debug.Log($"[NOVR] PC VR performance: runtime={runtime} {OpenXRRuntime.version}; graphics={backend}; " +
                    $"eyeTexture={width}x{height}; desktopMirror={Screen.width}x{Screen.height}.");
                _reported = true;
            }
        }
        catch (Exception e)
        {
            Report = "VR performance information is not available yet.";
            if (!_queryWarning) Debug.LogWarning("[NOVR] VR performance query deferred: " + e.GetType().Name);
            _queryWarning = true;
        }
    }
}
