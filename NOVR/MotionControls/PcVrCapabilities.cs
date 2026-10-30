using System;
using System.Collections.Generic;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace NOVR.MotionControls;

// Read-only support report. Gaze input alone does not reduce shading work;
// never treat a stored foveation strength/flag as proof that DFR is running.
internal sealed class PcVrCapabilities : MonoBehaviour
{
    internal static string Report { get; private set; } = "Waiting for the VR runtime…";
    private readonly List<InputDevice> _eyes = new();
    private float _nextCheck;
    private bool _reported;
    private void Update()
    {
        if (Time.unscaledTime < _nextCheck) return;
        _nextCheck = Time.unscaledTime + 1;
        if (!XRSettings.enabled) { Report = "VR runtime is not active."; _reported = false; return; }
        // The detailed report is on demand once the startup capability record
        // has been written. No per-frame gaze polling or rendering overhead.
        if (_reported && !WristMenu.IsPerformancePage) return;
        try
        {
            var available = OpenXRRuntime.GetAvailableExtensions();
            var eyeSupported = Array.IndexOf(available, "XR_EXT_eye_gaze_interaction") >= 0;
            var providerFoveation = Array.IndexOf(available, "XR_FB_foveation") >= 0 &&
                Array.IndexOf(available, "XR_META_foveation_eye_tracked") >= 0;
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, _eyes);
            var tracking = false;
            foreach (var eye in _eyes)
                tracking |= eye.isValid && eye.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked;
            var eyeText = tracking ? "Eye gaze: receiving tracking" : eyeSupported ? "Eye gaze: supported; no tracked sample" : "Eye gaze: unavailable from runtime";
            var backend = SystemInfo.graphicsDeviceType.ToString();
            Report = $"{OpenXRRuntime.name} · {backend}\n{eyeText}\n\nPC dynamic foveated rendering: NOT ACTIVE\n" +
                (providerFoveation ? "Runtime advertises foveation; renderer integration\nand visual validation are still required." :
                    "This runtime has no Unity eye-foveation provider.\nThe current renderer needs a separate integration.") +
                "\n\nFrame's foveated streaming is separate.\nIt does not prove reduced PC rendering work.\nHead-aimed EOTS works without eye tracking.";
            if (!_reported)
            {
                Debug.Log($"[NOVR] PC VR capabilities: runtime={OpenXRRuntime.name} {OpenXRRuntime.version}; graphics={backend}; " +
                    $"Unity foveation caps={SystemInfo.foveatedRenderingCaps}; eye extension={eyeSupported}; eye action enabled={OpenXRRuntime.IsExtensionEnabled("XR_EXT_eye_gaze_interaction")}; " +
                    $"eye-foveation provider={providerFoveation}. PC DFR is not active.");
                _reported = true;
            }
        }
        catch (Exception e)
        {
            Report = "VR capability query is not available yet.\nPC dynamic foveated rendering: NOT ACTIVE";
            if (!_reported) Debug.LogWarning("[NOVR] VR capability query deferred: " + e.GetType().Name);
            _reported = true;
        }
    }
}
