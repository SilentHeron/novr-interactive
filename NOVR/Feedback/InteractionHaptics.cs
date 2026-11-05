using System;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.MotionControls;

// Discrete interactions may include audio; movement feedback remains silent.
internal static class InteractionHaptics
{
    // A soft movement impulse must not replace an unfinished grab, detent or
    // targeting cue on the same motor. Four slots cover both hands plus device
    // replacement without growing a cache or allocating during interaction.
    private struct PriorityPulse
    {
        internal InputDevice Device;
        internal float Until;
        internal bool Occupied;
    }

    private static readonly PriorityPulse[] PriorityPulses = new PriorityPulse[4];
    private static float _lastClock = float.NegativeInfinity;

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
        => Send(device, strength, seconds, protect: true);

    internal static void PulseMotion(InputDevice device, float strength, float seconds)
    {
        if (!(strength > 0f) || !(seconds > 0f) || float.IsInfinity(strength) || float.IsInfinity(seconds) ||
            !ReadClock(out var now))
            return;
        foreach (var pulse in PriorityPulses)
            if (pulse.Occupied && pulse.Device.Equals(device) && now < pulse.Until)
                return;
        // Motion impulses may overlap one another; they never reserve the motor
        // or delay a subsequent stronger interaction cue.
        Send(device, strength, seconds, protect: false);
    }

    private static void Send(InputDevice device, float strength, float seconds, bool protect)
    {
        var level = ModConfiguration.Instance.MotionControls.HapticStrength.Value;
        if (level <= 0 || !device.isValid || !device.TryGetHapticCapabilities(out var caps) || !caps.supportsImpulse)
            return;
        var amplitude = Mathf.Clamp01(strength * level);
        if (device.SendHapticImpulse(0, amplitude, seconds) && protect && amplitude > 0f &&
            seconds > 0f && !float.IsInfinity(seconds) && ReadClock(out var now))
            Protect(device, now, seconds);
    }

    private static bool ReadClock(out float now)
    {
        now = Time.unscaledTime;
        if (float.IsNaN(now) || float.IsInfinity(now))
            return false;
        if (now < _lastClock)
            Array.Clear(PriorityPulses, 0, PriorityPulses.Length);
        _lastClock = now;
        return true;
    }

    private static void Protect(InputDevice device, float now, float seconds)
    {
        var slot = 0;
        for (var i = 0; i < PriorityPulses.Length; i++)
        {
            var pulse = PriorityPulses[i];
            if (pulse.Occupied && pulse.Device.Equals(device))
            {
                PriorityPulses[i].Until = Math.Max(pulse.Until, now + seconds);
                return;
            }
            if (!pulse.Occupied || pulse.Until < PriorityPulses[slot].Until)
                slot = i;
        }
        PriorityPulses[slot] = new PriorityPulse { Device = device, Until = now + seconds, Occupied = true };
    }
}
