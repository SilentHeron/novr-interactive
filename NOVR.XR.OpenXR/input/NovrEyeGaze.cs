using System;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace UnityEngine.XR.OpenXR;

public enum NovrEyeGazeReadState {
    Unavailable,
    Unsupported,
    Unfocused,
    Untracked,
    Stale,
    Invalid,
    Tracked
}

/// <summary>Eye and head poses in the same Unity XR tracking space.</summary>
public readonly struct NovrEyeGazePose {
    public readonly Vector3 Position, HeadPosition;
    public readonly Quaternion Rotation, HeadRotation;
    // Input System state-event time, not the time this reader was called.
    public readonly double InputTimestamp, AgeSeconds;

    internal NovrEyeGazePose(Vector3 position, Quaternion rotation, Vector3 headPosition,
        Quaternion headRotation, double timestamp, double age) {
        Position = position;
        Rotation = rotation;
        HeadPosition = headPosition;
        HeadRotation = headRotation;
        InputTimestamp = timestamp;
        AgeSeconds = age;
    }
}

/// <summary>
/// Reads standard OpenXR eye input without changing gameplay targeting. Call on
/// Unity's main thread; both rendered eyes receive one coherent sample per frame.
/// Unsupported, unfocused, untracked or stale input never returns a retained pose.
/// </summary>
public static class NovrEyeGaze {
    public const double MaximumSampleAgeSeconds = .10;
    private const double MaximumFutureSeconds = .05;
    private const InputTrackingState PoseTracking = InputTrackingState.Position | InputTrackingState.Rotation;
    private static int _frame = -1;
    private static NovrEyeGazeReadState _state;
    private static NovrEyeGazePose _pose;

    public static NovrEyeGazeReadState Read(out NovrEyeGazePose pose) {
        // Recheck session ownership even when the two eye renders share a frame.
        if (!XRSettings.enabled)
            return Inactive(NovrEyeGazeReadState.Unavailable, out pose);
        if (!OpenXRUtility.IsSessionFocused)
            return Inactive(NovrEyeGazeReadState.Unfocused, out pose);
        if (!OpenXRRuntime.IsExtensionEnabled(EyeGazeInteraction.extensionString))
            return Inactive(NovrEyeGazeReadState.Unsupported, out pose);
        if (_frame != Time.frameCount) {
            _frame = Time.frameCount;
            _state = ReadRuntime(out _pose);
        }
        pose = _pose;
        return _state;
    }

    private static NovrEyeGazeReadState Inactive(NovrEyeGazeReadState state, out NovrEyeGazePose pose) {
        _frame = -1;
        _pose = pose = default;
        return _state = state;
    }

    private static NovrEyeGazeReadState ReadRuntime(out NovrEyeGazePose pose) {
        pose = default;
        // The registered profile supplies this typed device and its actual event
        // timestamp. Do not fall back to timestamp-less legacy feature values:
        // those cannot distinguish a stopped gaze stream from a steady fixation.
        var devices = InputSystem.InputSystem.devices;
        EyeGazeInteraction.EyeGazeDevice? eye = null;
        for (var i = 0; i < devices.Count; i++)
            if (devices[i] is EyeGazeInteraction.EyeGazeDevice candidate && candidate.added && candidate.enabled &&
                (eye == null || candidate.lastUpdateTime > eye.lastUpdateTime))
                eye = candidate;
        if (eye?.pose == null)
            return NovrEyeGazeReadState.Unavailable;

        var sample = eye.pose.ReadValue();
        if (!sample.isTracked || (sample.trackingState & PoseTracking) != PoseTracking)
            return NovrEyeGazeReadState.Untracked;
        var timestamp = eye.lastUpdateTime;
        var age = InputState.currentTime - timestamp;
        if (!Finite(timestamp) || !Finite(age) || timestamp < 0 || age < -MaximumFutureSeconds)
            return NovrEyeGazeReadState.Invalid;
        if (age > MaximumSampleAgeSeconds)
            return NovrEyeGazeReadState.Stale;

        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!head.isValid || !head.TryGetFeatureValue(CommonUsages.isTracked, out var headTracked) || !headTracked ||
            !head.TryGetFeatureValue(CommonUsages.trackingState, out var headTracking) ||
            (headTracking & PoseTracking) != PoseTracking)
            return NovrEyeGazeReadState.Untracked;
        if ((!head.TryGetFeatureValue(CommonUsages.centerEyePosition, out var headPosition) &&
             !head.TryGetFeatureValue(CommonUsages.devicePosition, out headPosition)) ||
            (!head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var headRotation) &&
             !head.TryGetFeatureValue(CommonUsages.deviceRotation, out headRotation)))
            return NovrEyeGazeReadState.Untracked;

        if (!Finite(sample.position) || !Finite(headPosition) ||
            !Normalize(sample.rotation, out var rotation) || !Normalize(headRotation, out headRotation))
            return NovrEyeGazeReadState.Invalid;
        pose = new NovrEyeGazePose(sample.position, rotation, headPosition, headRotation, timestamp, Math.Max(0, age));
        return NovrEyeGazeReadState.Tracked;
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Normalize(Quaternion value, out Quaternion normalized) {
        normalized = default;
        var lengthSquared = (double)value.x * value.x + (double)value.y * value.y +
                            (double)value.z * value.z + (double)value.w * value.w;
        if (!Finite(lengthSquared) || lengthSquared < .000001)
            return false;
        var inverseLength = 1 / Math.Sqrt(lengthSquared);
        normalized = new Quaternion((float)(value.x * inverseLength), (float)(value.y * inverseLength),
            (float)(value.z * inverseLength), (float)(value.w * inverseLength));
        return true;
    }
}
