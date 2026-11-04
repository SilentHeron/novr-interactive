using System;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed class NozzleHudContext
{
    private bool _sampled, _tilted;
    private float _angle, _command, _lastTime, _holdUntil = float.NegativeInfinity;

    internal bool Update(bool cockpitReady, bool fitted, float angleDegrees, float customAxis1, bool hovering, float now)
    {
        if (!cockpitReady || !fitted || !Finite(angleDegrees) || !Finite(customAxis1) || !Finite(now))
        {
            Reset();
            return false;
        }
        if (_sampled && now < _lastTime)
            Reset();
        _lastTime = now;
        // Native SwivelDuctSystem and DuctedThrustSystem both use 0 degrees
        // for forward thrust and customAxis1=1 for their forward command.
        var angle = Math.Abs(angleDegrees);
        var command = Math.Max(0, Math.Min(1, 1 - customAxis1));
        var changed = _sampled && (Math.Abs(angle - _angle) >= .25f || Math.Abs(command - _command) >= .005f);
        if (!_sampled || changed)
        {
            _angle = angle;
            _command = command;
            _sampled = true;
        }
        _tilted = hovering || (_tilted ? angle > .25f || command > .0025f : angle >= 1 || command >= .01f);
        if (_tilted || changed)
            _holdUntil = now + 1.5f;
        return _tilted || now < _holdUntil;
    }

    internal void Reset()
    {
        _sampled = _tilted = false;
        _angle = _command = _lastTime = 0;
        _holdUntil = float.NegativeInfinity;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
