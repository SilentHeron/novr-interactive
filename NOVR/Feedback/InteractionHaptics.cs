using UnityEngine;
using UnityEngine.XR;

namespace NOVR.MotionControls;

// Discrete interactions may include audio; sustained movement feedback uses PulseOnly.
internal static class InteractionHaptics
{
    internal static void Pulse(InputDevice device, InteractionCue preset)
    {
        if (InteractionFeedbackProfile.TryGet(preset, out var strength, out var seconds))
            Pulse(device, strength, seconds, preset);
    }

    internal static void Pulse(InputDevice device, float strength = .25f, float seconds = .035f, InteractionCue cue = InteractionCue.Click)
    {
        InteractionAudio.Play(device, cue);
        PulseOnly(device, strength, seconds);
    }

    // Movement feedback must not produce a repeating button/detent sound.
    internal static void PulseOnly(InputDevice device, float strength, float seconds)
    {
        var level = ModConfiguration.Instance.MotionControls.HapticStrength.Value;
        if (level <= 0 || !device.isValid || !device.TryGetHapticCapabilities(out var caps) || !caps.supportsImpulse)
            return;
        device.SendHapticImpulse(0, Mathf.Clamp01(strength * level), seconds);
    }
}
