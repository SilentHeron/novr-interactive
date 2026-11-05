using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using Devices = UnityEngine.InputSystem.InputSystem;

var checks = 0;
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
void Near(double expected, double actual, string label) => Check(Math.Abs(expected - actual) < .000001, label);
var fullTracking = InputTrackingState.Position | InputTrackingState.Rotation;
var head = InputDevices.Head;
head.Set(CommonUsages.isTracked, true);
head.Set(CommonUsages.trackingState, fullTracking);
head.Set(CommonUsages.devicePosition, new Vector3(.1f, 1.4f, .3f));
head.Set(CommonUsages.deviceRotation, new Quaternion(0, 0, 0, 1));
var eye = new EyeGazeInteraction.EyeGazeDevice
{
    lastUpdateTime = 10,
    pose = new GazeControl
    {
        Value = new GazeValue
        {
            isTracked = true,
            trackingState = fullTracking,
            position = new Vector3(.1f, 1.4f, .31f),
            rotation = new Quaternion(0, 0, 0, 2)
        }
    }
};
Devices.devices.Add(eye);
InputState.currentTime = 10.02;
NovrEyeGazeReadState Next(out NovrEyeGazePose value) { Time.frameCount++; return NovrEyeGaze.Read(out value); }
void Reject(NovrEyeGazeReadState expected, string label)
{
    Check(Next(out var value) == expected, label);
    Check(value.Equals(default(NovrEyeGazePose)), label + " clears previous pose");
}

Check(Next(out var first) == NovrEyeGazeReadState.Tracked, "valid tracked gaze");
Near(1, first.Rotation.w, "normalized gaze orientation");
Near(10, first.InputTimestamp, "actual event time");
Near(.02, first.AgeSeconds, "actual event age");
Check(first.HeadPosition == new Vector3(.1f, 1.4f, .3f), "head device fallback");
Check(first.Position == eye.pose.Value.position, "tracking space position preserved");
eye.pose.Value.position = new Vector3(2, 3, 4);
Check(NovrEyeGaze.Read(out var second) == NovrEyeGazeReadState.Tracked && first.Equals(second), "same sample for both rendered eyes");
Check(eye.pose.Reads == 1, "runtime gaze read only once per frame");
Check(Next(out var later) == NovrEyeGazeReadState.Tracked && later.Position == new Vector3(2, 3, 4), "next frame refreshes input");

// A perfectly steady fixation remains fresh while state events continue.
for (var i = 0; i < 180; i++)
{
    eye.lastUpdateTime += 1.0 / 90;
    InputState.currentTime = eye.lastUpdateTime + .003;
    Check(Next(out _) == NovrEyeGazeReadState.Tracked, "stationary gaze freshness");
}
InputState.currentTime = eye.lastUpdateTime + .101;
Reject(NovrEyeGazeReadState.Stale, "stopped event stream");
InputState.currentTime = eye.lastUpdateTime + .001;
Check(Next(out _) == NovrEyeGazeReadState.Tracked, "fresh stream recovers");

// Session changes invalidate even within the cached stereo frame.
OpenXRUtility.IsSessionFocused = false;
Check(NovrEyeGaze.Read(out var unfocused) == NovrEyeGazeReadState.Unfocused && unfocused.Equals(default(NovrEyeGazePose)), "focus loss invalidates cache");
OpenXRUtility.IsSessionFocused = true;
Check(NovrEyeGaze.Read(out _) == NovrEyeGazeReadState.Tracked, "focus recovery resamples");
XRSettings.enabled = false;
Reject(NovrEyeGazeReadState.Unavailable, "XR off");
XRSettings.enabled = true;
OpenXRRuntime.ExtensionEnabled = false;
Reject(NovrEyeGazeReadState.Unsupported, "unsupported headset/runtime");
OpenXRRuntime.ExtensionEnabled = true;

eye.pose.Value.isTracked = false;
Reject(NovrEyeGazeReadState.Untracked, "blink or lost gaze tracking");
eye.pose.Value.isTracked = true;
eye.pose.Value.trackingState = InputTrackingState.Position;
Reject(NovrEyeGazeReadState.Untracked, "orientation must be tracked");
eye.pose.Value.trackingState = fullTracking;
head.Set(CommonUsages.trackingState, InputTrackingState.Rotation);
Reject(NovrEyeGazeReadState.Untracked, "head tracking incomplete");
head.Set(CommonUsages.trackingState, fullTracking);
head.Set(CommonUsages.centerEyePosition, new Vector3(.11f, 1.5f, .32f));
head.Set(CommonUsages.centerEyeRotation, new Quaternion(0, 1, 0, 1));
Check(Next(out var centered) == NovrEyeGazeReadState.Tracked && centered.HeadPosition == new Vector3(.11f, 1.5f, .32f), "prefer center eye pose");
Near(Math.Sqrt(.5), centered.HeadRotation.y, "normalize head orientation");

foreach (var rotation in new[] { new Quaternion(), new Quaternion(float.NaN, 0, 0, 1), new Quaternion(0, float.PositiveInfinity, 0, 1) })
{
    eye.pose.Value.rotation = rotation;
    Reject(NovrEyeGazeReadState.Invalid, "invalid rotation");
}
eye.pose.Value.rotation = new Quaternion(0, 0, 0, 1);
eye.pose.Value.position = new Vector3(0, float.NaN, 0);
Reject(NovrEyeGazeReadState.Invalid, "invalid gaze origin");
eye.pose.Value.position = new Vector3(0, 1, 0);
head.Set(CommonUsages.centerEyeRotation, new Quaternion());
Reject(NovrEyeGazeReadState.Invalid, "invalid head orientation");
head.Set(CommonUsages.centerEyeRotation, new Quaternion(0, 0, 0, 1));
foreach (var time in new[] { double.NaN, double.PositiveInfinity, -1, InputState.currentTime + 1 })
{
    eye.lastUpdateTime = time;
    Reject(NovrEyeGazeReadState.Invalid, "invalid input timestamp");
}
eye.lastUpdateTime = InputState.currentTime + .01;
Check(Next(out var predicted) == NovrEyeGazeReadState.Tracked, "small positive prediction offset tolerated");
Near(0, predicted.AgeSeconds, "reported age never negative");

eye.added = false;
Reject(NovrEyeGazeReadState.Unavailable, "removed device");
eye.added = true;
eye.enabled = false;
Reject(NovrEyeGazeReadState.Unavailable, "disabled device");
var replacement = new EyeGazeInteraction.EyeGazeDevice { lastUpdateTime = InputState.currentTime, pose = eye.pose };
Devices.devices.Add(replacement);
Check(Next(out _) == NovrEyeGazeReadState.Tracked, "replacement device acquired");
replacement.pose = null;
Reject(NovrEyeGazeReadState.Unavailable, "device layout not ready");
Devices.devices.Clear();
Devices.devices.Add(new UnityEngine.InputSystem.InputDevice());
Reject(NovrEyeGazeReadState.Unavailable, "unrelated input is not gaze");

Console.WriteLine($"{checks} eye gaze input checks passed (source linked; no headset or game).");
