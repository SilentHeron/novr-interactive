using System;

namespace NOVR.MotionControls;

// Map ownership survives a deflected-stick handoff until neutral. Neither an
// inherited click nor the release of a map click may reach a flight binding.
internal sealed class CockpitMapInputState
{
    private readonly ControllerButtonGate _click = new();
    private bool _context, _armed, _handoff;
    internal bool Reserved { get; private set; }
    internal float PanX { get; private set; }
    internal float PanY { get; private set; }
    internal bool SelectPressed { get; private set; }

    internal void Update(bool available, bool selected, bool stickHeld, float x, float y, bool clicked)
    {
        PanX = PanY = 0; SelectPressed = false;
        if (!available || !FlightControlMath.IsFinite(x) || !FlightControlMath.IsFinite(y))
        {
            _handoff |= _context; _context = _armed = false; _click.Reset();
            Reserved = _handoff; return;
        }
        var neutral = x * x + y * y <= .18f * .18f && !clicked;
        if (!selected || !stickHeld)
        {
            if (_context) _handoff = true;
            _context = _armed = false; _click.Reset();
            // Flight thumbstick yaw starts outside 0.12, earlier than map pan.
            // Returning ownership at the larger map deadzone leaks small yaw.
            if (x * x + y * y <= .12f * .12f && !clicked) _handoff = false;
            Reserved = _handoff;
            return;
        }
        _context = Reserved = true; _handoff = false;
        if (!_armed)
        {
            if (neutral) { _armed = true; _click.Update(true, 0); }
            return;
        }
        _click.Update(true, clicked ? 1 : 0);
        SelectPressed = _click.Pressed;
        var magnitude = (float)Math.Sqrt(x * x + y * y);
        if (magnitude <= .18f) return;
        var strength = Math.Min(1, (magnitude - .18f) / .82f) / magnitude;
        PanX = x * strength; PanY = y * strength;
    }

    internal void Reset()
    {
        _click.Reset(); _context = _armed = _handoff = Reserved = SelectPressed = false;
        PanX = PanY = 0;
    }
}
