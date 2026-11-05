using System;

namespace NOVR.VrUi.SpecialBehavior;

// Both the user's gaze and the face of the wrist card must agree. Different
// enter/exit thresholds and a short dwell avoid flicker while reading it.
internal sealed class HudWidgetWristVisibility
{
    private bool _shown;
    private float _dwell;
    internal float Alpha
    {
        get; private set;
    }

    internal float Update(bool tracked, bool blocked, bool editing, float gazeDot, float faceDot, float distance, float deltaTime)
    {
        if (!tracked || blocked || !Finite(gazeDot) || !Finite(faceDot) || !Finite(distance))
        {
            Reset();
            return 0;
        }
        if (editing)
        {
            _shown = false;
            _dwell = 0;
            return Alpha = 1;
        }
        var dt = Finite(deltaTime) ? Math.Max(0, Math.Min(.1f, deltaTime)) : 0;
        var looking = distance >= .12f && distance <= 1.15f &&
            gazeDot >= (_shown ? .79f : .90f) && faceDot >= (_shown ? .28f : .52f);
        if (looking)
        {
            _dwell += dt;
            if (_dwell >= .10f)
                _shown = true;
        }
        else
        {
            _dwell = 0;
            _shown = false;
        }
        var target = _shown ? 1f : 0f;
        Alpha = target > Alpha ? Math.Min(target, Alpha + dt * 8) : Math.Max(target, Alpha - dt * 10);
        return Alpha;
    }
    internal void Reset()
    {
        _shown = false;
        _dwell = Alpha = 0;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
