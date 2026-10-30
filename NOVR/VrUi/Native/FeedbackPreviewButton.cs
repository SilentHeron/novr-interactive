using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrUi.Native;

// Marker and one-shot receiver. Only ControllerUiPointer arms this component;
// the ordinary Button click event completes it after a valid release.
internal sealed class FeedbackPreviewButton : MonoBehaviour
{
    private readonly FeedbackPreviewGate<InputDevice> _gate = new();
    private bool _left;
    internal InteractionCue Cue;

    internal void Arm(bool left, InputDevice device) { _left = left; _gate.Arm(device); }
    internal void Cancel() => _gate.Cancel();
    internal void Play()
    {
        var hand = TrackedControllers.Read(_left);
        if (_gate.Consume(isActiveAndEnabled && WristMenu.IsOpen && hand.Valid, hand.Device))
            InteractionHaptics.Pulse(hand.Device, Cue);
    }
    private void OnDisable() => Cancel();
}
