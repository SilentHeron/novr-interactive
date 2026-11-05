using System;

namespace NOVR.MotionControls;

internal enum HandOptionWheelEvent
{
    None, Open, Cancel, Commit, ChangePage
}

internal sealed class HandOptionWheelOwnership
{
    internal int Owner { get; private set; } = -1;
    private readonly int[] _consumed = { -1, -1 };
    internal bool Owns(int side, int frame) => side >= 0 && side < 2 &&
        (Owner == side || frame >= 0 && _consumed[side] == frame);
    internal void Open(int side, int frame)
    {
        Owner = side;
        _consumed[side] = frame;
    }
    internal void Close(int frame)
    {
        if (Owner >= 0)
            _consumed[Owner] = frame;
        Owner = -1;
    }
}

// No Unity input or action dispatch here: cancellation has priority over every
// release edge, including a click and return-to-centre in the same sample.
internal sealed class HandOptionWheelGesture
{
    internal const float SelectRadius = .45f, CentreRadius = .22f;
    internal bool IsOpen
    {
        get; private set;
    }
    internal int Hovered { get; private set; } = -1;
    internal int Committed { get; private set; } = -1;
    private bool _clickArmed, _triggerArmed, _centred;

    internal HandOptionWheelEvent Update(bool available, bool click, float trigger, float x, float y, int count, bool canChangePage = true)
    {
        Committed = -1;
        if (!available || !Finite(x) || !Finite(y) || !Finite(trigger))
        {
            var wasOpen = IsOpen;
            Reset();
            return wasOpen ? HandOptionWheelEvent.Cancel : HandOptionWheelEvent.None;
        }
        var clicked = click && _clickArmed;
        _clickArmed = !click;
        var page = canChangePage && trigger >= .75f && _triggerArmed;
        if (trigger <= .25f)
            _triggerArmed = true;
        else if (page)
            _triggerArmed = false;
        var radius = Math.Sqrt((double)x * x + (double)y * y);
        if (clicked)
        {
            if (IsOpen)
            {
                Close();
                return HandOptionWheelEvent.Cancel;
            }
            IsOpen = true;
            Hovered = -1;
            _centred = radius <= CentreRadius;
            _triggerArmed = trigger <= .25f;
            return HandOptionWheelEvent.Open;
        }
        if (!IsOpen)
            return HandOptionWheelEvent.None;
        if (page)
        {
            Hovered = -1;
            _centred = false;
            return HandOptionWheelEvent.ChangePage;
        }
        if (radius <= CentreRadius)
        {
            if (_centred && Hovered >= 0 && Hovered < count)
            {
                var selected = Hovered;
                Close();
                Committed = selected;
                return HandOptionWheelEvent.Commit;
            }
            Hovered = -1;
            _centred = true;
        }
        else if (_centred && radius >= SelectRadius)
            Hovered = Sector(x, y, count);
        return HandOptionWheelEvent.None;
    }

    internal static int Sector(float x, float y, int count)
    {
        if (count <= 0 || !Finite(x) || !Finite(y) || x == 0 && y == 0)
            return -1;
        // Native action zero is up, increasing clockwise around the wheel.
        var angle = Math.Atan2(x, y) * 180d / Math.PI;
        if (angle < 0)
            angle += 360;
        return (int)Math.Floor((angle + 180d / count) / (360d / count)) % count;
    }
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private void Close()
    {
        IsOpen = false;
        Hovered = -1;
        _centred = false;
    }
    internal void Reset()
    {
        Close();
        Committed = -1;
        _clickArmed = _triggerArmed = false;
    }
}
