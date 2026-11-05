using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.Native;
using UnityEngine.XR;

var passed = 0;
void Check(string name, Action body) { body(); passed++; Console.WriteLine("PASS " + name); }
void Assert(bool condition) { if (!condition) throw new InvalidOperationException("Feedback assertion failed"); }
void Near(float value, float expected) => Assert(Math.Abs(value - expected) < .00001f);
var leftOutput = new DeviceOutput();
var rightOutput = new DeviceOutput();
var left = new InputDevice(11, leftOutput);
var right = new InputDevice(22, rightOutput);
void Reset()
{
    UnityEngine.Time.unscaledTime += 10f;
    leftOutput.Pulses.Clear();
    rightOutput.Pulses.Clear();
    InteractionAudio.Requests.Clear();
    leftOutput.Valid = leftOutput.Capabilities = leftOutput.SupportsImpulse = true;
    rightOutput.Valid = rightOutput.Capabilities = rightOutput.SupportsImpulse = true;
    leftOutput.SendSucceeds = rightOutput.SendSucceeds = true;
    TrackedControllers.Left = new ControllerSample { Valid = true, Device = left };
    TrackedControllers.Right = new ControllerSample { Valid = true, Device = right };
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = 1.5f;
    WristMenu.IsOpen = true;
}
var presets = new[] {
    (InteractionCue.Touch, .16f, .025f), (InteractionCue.Grab, .35f, .045f),
    (InteractionCue.Switch, .5f, .065f), (InteractionCue.Detent, .65f, .075f)
};
foreach (var (cue, amplitude, duration) in presets)
    Check(cue + " sample uses its shared pulse and actual sound cue", () =>
    {
        Reset();
        var button = new FeedbackPreviewButton { Cue = cue };
        button.Arm(true, left);
        button.Play();
        Assert(leftOutput.Pulses.Count == 1 && rightOutput.Pulses.Count == 0);
        Near(leftOutput.Pulses[0].amplitude, Math.Min(1, amplitude * 1.5f));
        Near(leftOutput.Pulses[0].duration, duration);
        Assert(InteractionAudio.Requests.Count == 1 && InteractionAudio.Requests[0] == (11UL, cue));
    });
Check("Right-hand click is sent only to the right controller", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Touch };
    button.Arm(false, right);
    button.Play();
    Assert(rightOutput.Pulses.Count == 1 && leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Single().device == 22);
});
Check("A button without a controller press stays silent", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Touch };
    for (var i = 0; i < 500; i++)
        button.Play();
    Assert(leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Count == 0);
});
Check("One press cannot repeat a cue on repeated click dispatch", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Switch };
    button.Arm(true, left);
    for (var i = 0; i < 500; i++)
        button.Play();
    Assert(leftOutput.Pulses.Count == 1 && InteractionAudio.Requests.Count == 1);
    button.Arm(true, left);
    button.Play();
    Assert(leftOutput.Pulses.Count == 2 && InteractionAudio.Requests.Count == 2);
});
Check("Cancelled pointer release clears the sample", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Grab };
    button.Arm(true, left);
    button.Cancel();
    button.Play();
    Assert(InteractionAudio.Requests.Count == 0);
});
Check("Leaving the feedback page clears its armed sample", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Grab };
    button.Arm(true, left);
    typeof(FeedbackPreviewButton).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
    button.Play();
    Assert(InteractionAudio.Requests.Count == 0);
});
Check("Closed menu cannot play or defer a sample", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Detent };
    button.Arm(true, left);
    WristMenu.IsOpen = false;
    button.Play();
    WristMenu.IsOpen = true;
    button.Play();
    Assert(InteractionAudio.Requests.Count == 0);
});
Check("Disabled button cannot play or defer a sample", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Detent };
    button.Arm(true, left);
    button.isActiveAndEnabled = false;
    button.Play();
    button.isActiveAndEnabled = true;
    button.Play();
    Assert(InteractionAudio.Requests.Count == 0);
});
Check("Tracking loss consumes the old request without replay on recovery", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Touch };
    button.Arm(true, left);
    TrackedControllers.Left.Valid = false;
    button.Play();
    TrackedControllers.Left.Valid = true;
    button.Play();
    Assert(InteractionAudio.Requests.Count == 0);
});
Check("Device replacement between press and release cannot inherit a sample", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Touch };
    button.Arm(true, left);
    TrackedControllers.Left.Device = new InputDevice(33, leftOutput);
    button.Play();
    Assert(InteractionAudio.Requests.Count == 0 && leftOutput.Pulses.Count == 0);
});
Check("Zero haptics preserves the independent audio request", () =>
{
    Reset();
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = 0;
    InteractionHaptics.Pulse(left, InteractionCue.Grab);
    Assert(leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Single().cue == InteractionCue.Grab);
});
Check("Unsupported haptics preserves the independent audio request", () =>
{
    Reset();
    leftOutput.SupportsImpulse = false;
    InteractionHaptics.Pulse(left, InteractionCue.Grab);
    Assert(leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Count == 1);
});
Check("Missing device capabilities skip the motor safely", () =>
{
    Reset();
    leftOutput.Capabilities = false;
    InteractionHaptics.Pulse(left, InteractionCue.Switch);
    Assert(leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Count == 1);
});
Check("High strength is clamped to the device amplitude limit", () =>
{
    Reset();
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = 2;
    InteractionHaptics.Pulse(left, InteractionCue.Detent);
    Near(leftOutput.Pulses.Single().amplitude, 1);
});
Check("Adjusted strength is used at click completion", () =>
{
    Reset();
    var button = new FeedbackPreviewButton { Cue = InteractionCue.Grab };
    button.Arm(true, left);
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = .5f;
    button.Play();
    Near(leftOutput.Pulses.Single().amplitude, .175f);
});
Check("A non-preview cue never reaches haptics or sound through the preset API", () =>
{
    Reset();
    InteractionHaptics.Pulse(left, InteractionCue.Eject);
    InteractionHaptics.Pulse(left, (InteractionCue)999);
    Assert(leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Count == 0);
});
Check("Existing explicit pulse path retains its custom amplitude and sound", () =>
{
    Reset();
    InteractionHaptics.Pulse(right, .18f, .025f);
    Near(rightOutput.Pulses.Single().amplitude, .27f);
    Near(rightOutput.Pulses.Single().duration, .025f);
    Assert(InteractionAudio.Requests.Single().cue == InteractionCue.Click);
});
Check("The four samples have distinct nonempty synthesized waveforms", () =>
{
    var samples = presets.Select(item => InteractionSoundWave.Create(item.Item1)).ToArray();
    Assert(samples.All(wave => wave.Length > 0 && wave.Any(value => Math.Abs(value) > .001f)));
    for (var a = 0; a < samples.Length; a++)
        for (var b = a + 1; b < samples.Length; b++)
            Assert(!samples[a].SequenceEqual(samples[b]));
});
Check("Stick movement pulses use the strength setting without producing interaction audio", () =>
{
    Reset();
    InteractionHaptics.PulseOnly(right, .12f, .02f);
    Near(rightOutput.Pulses.Single().amplitude, .18f);
    Near(rightOutput.Pulses.Single().duration, .02f);
    Assert(leftOutput.Pulses.Count == 0 && InteractionAudio.Requests.Count == 0);
    Reset();
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = 0;
    InteractionHaptics.PulseOnly(right, .42f, .06f);
    Assert(rightOutput.Pulses.Count == 0 && InteractionAudio.Requests.Count == 0);
});
Check("Soft stick motion preserves unfinished grab feedback on that hand only", () =>
{
    Reset();
    InteractionHaptics.Pulse(left, InteractionCue.Grab);
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    InteractionHaptics.PulseMotion(right, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 1 && rightOutput.Pulses.Count == 1);
    Near(rightOutput.Pulses[0].amplitude, .03f);
    Assert(InteractionAudio.Requests.Count == 1);
    UnityEngine.Time.unscaledTime += .03f;
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 1);
    UnityEngine.Time.unscaledTime += .02f;
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 2);
});
Check("Soft movement overlap never locks out another movement pulse or a stronger cue", () =>
{
    Reset();
    InteractionHaptics.PulseMotion(left, .015f, .025f);
    UnityEngine.Time.unscaledTime += .01f;
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 2 && InteractionAudio.Requests.Count == 0);
    InteractionHaptics.Pulse(left, InteractionCue.Detent);
    Assert(leftOutput.Pulses.Count == 3 && InteractionAudio.Requests.Count == 1);
    Near(leftOutput.Pulses[2].amplitude, .975f);
    UnityEngine.Time.unscaledTime += .03f;
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 3);
});
Check("Silent priority feedback also protects its duration without changing existing output", () =>
{
    Reset();
    InteractionHaptics.PulseOnly(right, .4f, .08f);
    InteractionHaptics.PulseOnly(right, .3f, .03f);
    Assert(rightOutput.Pulses.Count == 2 && InteractionAudio.Requests.Count == 0);
    Near(rightOutput.Pulses[0].amplitude, .6f);
    Near(rightOutput.Pulses[1].amplitude, .45f);
    UnityEngine.Time.unscaledTime += .04f;
    InteractionHaptics.PulseMotion(right, .02f, .025f);
    Assert(rightOutput.Pulses.Count == 2);
    UnityEngine.Time.unscaledTime += .05f;
    InteractionHaptics.PulseMotion(right, .02f, .025f);
    Assert(rightOutput.Pulses.Count == 3);
});
Check("Zero strength and invalid motion impulses remain silent and do not reserve a controller", () =>
{
    Reset();
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = 0;
    InteractionHaptics.PulseOnly(left, .4f, .08f);
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 0);
    ModConfiguration.Instance.MotionControls.HapticStrength.Value = 1.5f;
    foreach (var invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
    {
        InteractionHaptics.PulseMotion(left, invalid, .025f);
        InteractionHaptics.PulseMotion(left, .02f, invalid);
    }
    Assert(leftOutput.Pulses.Count == 0);
    InteractionHaptics.PulseOnly(left, 0, .08f);
    Assert(leftOutput.Pulses.Count == 1); // Preserve the existing explicit pulse API.
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 2);
});
Check("Unavailable or rejected impulses do not create stale motion suppression", () =>
{
    foreach (var failure in new[] { 0, 1, 2, 3 })
    {
        Reset();
        leftOutput.Valid = failure != 0;
        leftOutput.Capabilities = failure != 1;
        leftOutput.SupportsImpulse = failure != 2;
        leftOutput.SendSucceeds = failure != 3;
        InteractionHaptics.PulseOnly(left, .4f, .08f);
        var attempts = leftOutput.Pulses.Count;
        leftOutput.Valid = leftOutput.Capabilities = leftOutput.SupportsImpulse = leftOutput.SendSucceeds = true;
        InteractionHaptics.PulseMotion(left, .02f, .025f);
        Assert(leftOutput.Pulses.Count == attempts + 1);
    }
});
Check("Clock rewind clears stale reservations and controller replacement keeps independent feedback", () =>
{
    Reset();
    InteractionHaptics.PulseOnly(left, .4f, .08f);
    var replacement = new InputDevice(33, rightOutput);
    InteractionHaptics.PulseMotion(replacement, .02f, .025f);
    Assert(rightOutput.Pulses.Count == 1);
    UnityEngine.Time.unscaledTime -= 1f;
    InteractionHaptics.PulseMotion(left, .02f, .025f);
    Assert(leftOutput.Pulses.Count == 2);
});
Check("Priority reservations use public device equality across copies and four distinct handles", () =>
{
    Reset();
    var outputs = Enumerable.Range(0, 4).Select(_ => new DeviceOutput()).ToArray();
    var devices = outputs.Select((output, index) => new InputDevice((ulong)(100 + index), output)).ToArray();
    for (var i = 0; i < devices.Length; i++)
        InteractionHaptics.PulseOnly(devices[i], .4f, .08f);
    for (var i = 0; i < devices.Length; i++)
    {
        var copied = devices[i];
        var sameHandle = new InputDevice((ulong)(100 + i), outputs[i]);
        Assert(copied.Equals(sameHandle) && copied == sameHandle);
        Assert(!copied.Equals(devices[(i + 1) % devices.Length]));
        InteractionHaptics.PulseMotion(copied, .02f, .025f);
        InteractionHaptics.PulseMotion(sameHandle, .02f, .025f);
        Assert(outputs[i].Pulses.Count == 1);
    }
    UnityEngine.Time.unscaledTime += .1f;
    for (var i = 0; i < devices.Length; i++)
    {
        InteractionHaptics.PulseMotion(devices[i], .02f, .025f);
        Assert(outputs[i].Pulses.Count == 2);
    }
});
Console.WriteLine($"{passed} feedback preview checks passed (device/audio doubles; no real controller or game actions).");
