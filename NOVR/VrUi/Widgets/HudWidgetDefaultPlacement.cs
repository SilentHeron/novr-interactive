using System;
using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Only a new, unsaved aircraft slot uses this gate. Once captured, positions
// belong to the cockpit and never follow later head/seat/recenter movement.
internal sealed class HudWidgetDefaultPlacement
{
    private const float SettleSeconds = .15f, SettleDistance = .04f;
    private bool _sampling;
    private int _lastFrame = -1, _samples;
    private float _since;
    private Vector3 _reference;

    internal void Reset() { _sampling = false; _lastFrame = -1; _samples = 0; }

    internal bool Observe(bool valid, Vector3 head, float now, int frame)
    {
        if (!valid || !Finite(head) || float.IsNaN(now) || float.IsInfinity(now)) { Reset(); return false; }
        if (!_sampling || now < _since || (head - _reference).magnitude > SettleDistance)
        {
            _sampling = true; _reference = head; _since = now; _lastFrame = frame; _samples = 1;
            return false;
        }
        if (frame != _lastFrame) { _lastFrame = frame; _samples++; }
        return _samples >= 3 && now - _since >= SettleSeconds;
    }

    internal static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    // Offsets are physical meters around the seated user's center, expressed
    // along the cockpit's forward/right/up axes rather than the user's gaze.
    internal static Dictionary<string, HudWidgetPose> AtHead(Vector3 head)
    {
        if (!Finite(head)) throw new ArgumentException("A tracked seated position is required.", nameof(head));
        var poses = HudWidgetProfiles.FactoryDefaults;
        foreach (var id in HudWidgetProfiles.Ids)
        {
            var pose = poses[id]; pose.Position += head; poses[id] = pose;
        }
        return poses;
    }
}
