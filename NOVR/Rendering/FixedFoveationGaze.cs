using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.OpenXR;

namespace NOVR.Rendering;

internal sealed partial class FixedFoveationRuntime
{
    private NovrEyeGazeReadState _gazeState = NovrEyeGazeReadState.Unavailable;
    private int _gazeFrame = -1;
    private bool _gazeReadWarning;

    private FixedFoveationLevel CurrentRenderLevel => FixedFoveationPolicy.WithGazeFallback(_level,
        ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value,
        _gazeFrame >= 0 && Time.frameCount - _gazeFrame <= 1 && _gazeState == NovrEyeGazeReadState.Tracked);

    internal static string EyeTrackingStatus
    {
        get
        {
            if (!ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value) return "Centre: fixed.";
            var owner = _instance;
            if (owner == null || !IsAvailable) return "Eye tracking inactive on this setup.";
            if (owner._level == FixedFoveationLevel.Off) return "Eye tracking selected; foveation is Off.";
            if (Time.frameCount - owner._gazeFrame > 1) return "Eye tracking: waiting for a flight view.";
            var state = owner._gazeState switch
            {
                NovrEyeGazeReadState.Tracked => "Centre: following tracked eyes.",
                NovrEyeGazeReadState.Unsupported => "Fixed fallback: runtime has no eye-gaze support.",
                NovrEyeGazeReadState.Unfocused => "Fixed fallback: VR session is not focused.",
                NovrEyeGazeReadState.Untracked => "Fixed fallback: eyes are not tracked.",
                NovrEyeGazeReadState.Stale => "Fixed fallback: waiting for fresh eye tracking.",
                NovrEyeGazeReadState.Invalid => "Fixed fallback: eye pose is invalid.",
                _ => "Fixed fallback: no eye gaze. Check headset settings."
            };
            return owner.CurrentRenderLevel != owner._level ? state + " Using Strong." : state;
        }
    }

    internal static void SetEyeTracking(bool enabled)
    {
        if (enabled && !IsAvailable) return;
        ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value = enabled;
        if (_instance != null)
        {
            _instance._gazeFrame = -1;
            _instance._nextStatus = 0;
        }
    }

    private bool ResolveCenter(CameraData cameraData, Matrix4x4 projection, bool flippedTarget, out float x, out float y)
    {
        if (!FixedFoveationCameraPolicy.TryCenter(projection.m02, projection.m12, out x, out y)) return false;
        if (_level != FixedFoveationLevel.Off && ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value)
            ResolveGaze(cameraData, projection, ref x, ref y);
        // URP draws this bracketed opaque pass into an intermediate texture
        // whenever a camera stack or post-processing is active. On Direct3D the
        // engine flips that projection, so the top of the eye image lands on the
        // last target rows. The rate texture addresses target rows, not the
        // upright image: mirror the centre so the protected region stays where
        // the pilot actually looks. A fixed centre barely moves (its vertical
        // asymmetry is small); an eye-tracked centre would otherwise move
        // opposite to vertical gaze and expose coarse shading in the fovea.
        if (flippedTarget) y = 1 - y;
        return true;
    }

    private void ResolveGaze(CameraData cameraData, Matrix4x4 projection, ref float x, ref float y)
    {
        // The input reader shares one fresh sample between both eyes. Remove
        // its head rotation from the actual camera pose to recover the complete
        // tracking origin, including NOVR's recenter calibration. Use the actual
        // eye view as well as its projection: eyes can be independently canted.
        _gazeFrame = Time.frameCount;
        try
        {
            _gazeState = NovrEyeGaze.Read(out var pose);
            if (_gazeState != NovrEyeGazeReadState.Tracked) return;
            // The small test regions need fresher input than the legacy presets.
            // This is sample age, not a claim about end-to-end tracking latency.
            if (_level > FixedFoveationLevel.Strong && pose.AgeSeconds > .05)
            { _gazeState = NovrEyeGazeReadState.Stale; return; }
            var camera = cameraData.camera;
            var eyeView = cameraData.xr.GetViewMatrix(0);
            var originRotation = camera.transform.rotation * Quaternion.Inverse(pose.HeadRotation);
            var direction = eyeView.MultiplyVector(originRotation * (pose.Rotation * Vector3.forward));
            // MultiplyVector excludes seat/aircraft translation. Eye view uses
            // -Z forward; the projection helper accepts Unity +Z directions.
            if (FoveationGazePolicy.TryProject(direction.x, direction.y, -direction.z,
                projection.m00, projection.m11, projection.m02, projection.m12, out var gazeX, out var gazeY))
            { x = gazeX; y = gazeY; }
            else _gazeState = NovrEyeGazeReadState.Invalid;
        }
        catch (Exception error)
        {
            _gazeState = NovrEyeGazeReadState.Unavailable;
            if (!_gazeReadWarning)
            {
                _gazeReadWarning = true;
                Debug.LogWarning("[NOVR] Eye input unavailable; using fixed foveation: " + error.Message);
            }
        }
    }
}
