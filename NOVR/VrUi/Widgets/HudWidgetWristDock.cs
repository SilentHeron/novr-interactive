using System;

namespace NOVR.VrUi.SpecialBehavior;

// Readiness is only a preview: mounting still requires an explicit grip release.
// Tracking/context loss must reset this gate, never complete it.
internal sealed class HudWidgetWristDock
{
    internal const float DwellSeconds = .18f;
    private const float MaximumObservationGap = .25f;
    private bool _observing;
    private float _startedAt;
    private float _lastTime;

    internal bool Ready
    {
        get; private set;
    }
    internal bool BecameReady
    {
        get; private set;
    }

    internal bool Update(bool eligible, float now)
    {
        BecameReady = false;
        if (!eligible || !Finite(now))
        {
            Reset();
            return false;
        }

        // A pause, clock reset or tracking restart is not evidence that the
        // widget remained over a wrist for the missing interval.
        if (!_observing || now < _lastTime || now - _lastTime > MaximumObservationGap)
        {
            Reset();
            _observing = true;
            _startedAt = _lastTime = now;
            return false;
        }

        _lastTime = now;
        if (!Ready && now - _startedAt >= DwellSeconds)
        {
            Ready = true;
            BecameReady = true;
        }
        return Ready;
    }

    internal bool Release(bool deliberateGripRelease)
    {
        var attach = deliberateGripRelease && Ready;
        Reset();
        return attach;
    }

    internal void Reset()
    {
        _observing = false;
        Ready = BecameReady = false;
        _startedAt = _lastTime = 0;
    }

    // Arguments are metres in the rendered widget's local rectangle frame.
    // The center cap prevents a large panel brushing a wrist at its edge from
    // unexpectedly turning into a wrist card.
    internal static bool IsNear(float localX, float localY, float localZ, float halfWidth, float halfHeight)
    {
        if (!Finite(localX) || !Finite(localY) || !Finite(localZ) ||
            !Finite(halfWidth) || !Finite(halfHeight) || halfWidth <= 0 || halfHeight <= 0)
            return false;

        const float padding = .03f;
        const float planeDistance = .10f;
        const float centerDistance = .22f;
        return Math.Abs(localZ) <= planeDistance &&
            Math.Abs(localX) <= halfWidth + padding &&
            Math.Abs(localY) <= halfHeight + padding &&
            localX * localX + localY * localY + localZ * localZ <= centerDistance * centerDistance;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
