using System;

namespace NOVR.MotionControls;

internal enum EjectConfirmationEvent
{
    None, Cancel, Confirm
}

// Pure input state: the caller owns the confirmation display and eject action.
// Losing context always cancels; a stalled frame can never finish a hold.
internal sealed class EjectConfirmationGesture
{
    private const float StartRadius = .22f, HoldRadius = .30f, CancelRadius = .45f;
    private const float MaxFrameSeconds = .15f, CancelTrigger = .75f;
    private const double HoldSeconds = 1.2d;
    private double _heldSeconds;
    private bool _clickArmed;

    internal bool Active
    {
        get; private set;
    }
    internal bool Holding
    {
        get; private set;
    }
    internal float Progress => (float)Math.Min(1d, _heldSeconds / HoldSeconds);

    internal void Begin()
    {
        Reset();
        Active = true;
    }

    internal void Reset()
    {
        Active = Holding = _clickArmed = false;
        _heldSeconds = 0d;
    }

    internal EjectConfirmationEvent Update(bool available, bool stickClick, float trigger,
        float x, float y, float unscaledDeltaTime)
    {
        if (!Active)
            return EjectConfirmationEvent.None;
        if (!available || !Finite(trigger) || trigger < 0f || !Finite(x) || !Finite(y) ||
            !Finite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
            return Cancel();
        var radius = Math.Sqrt((double)x * x + (double)y * y);
        if (trigger >= CancelTrigger || radius > CancelRadius)
            return Cancel();
        if (unscaledDeltaTime > MaxFrameSeconds)
        {
            InterruptHold();
            return EjectConfirmationEvent.None;
        }
        if (!stickClick)
        {
            InterruptHold();
            _clickArmed = true;
            return EjectConfirmationEvent.None;
        }
        if (!Holding)
        {
            var freshClick = _clickArmed;
            _clickArmed = false;
            if (freshClick && radius <= StartRadius)
                Holding = true;
            // This frame's elapsed time preceded the press: do not count it.
            return EjectConfirmationEvent.None;
        }
        if (radius > HoldRadius)
        {
            InterruptHold();
            return EjectConfirmationEvent.None;
        }
        _heldSeconds += unscaledDeltaTime;
        if (_heldSeconds < HoldSeconds)
            return EjectConfirmationEvent.None;
        Active = Holding = _clickArmed = false;
        _heldSeconds = HoldSeconds;
        return EjectConfirmationEvent.Confirm;
    }

    private void InterruptHold()
    {
        Holding = _clickArmed = false;
        _heldSeconds = 0d;
    }
    private EjectConfirmationEvent Cancel()
    {
        Reset();
        return EjectConfirmationEvent.Cancel;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
