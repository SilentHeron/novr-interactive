using System;

namespace NOVR.MotionControls;

// Tap and hold for one button, answered with the thresholds the game itself
// passes to its input queries, so its own click and press delay settings keep
// applying. It follows a ControllerButtonGate: a press that the gate cancels
// (lost tracking, a menu, a released handle) ends without a tap.
internal sealed class ControllerPressTimer
{
    private float _pressedAt;
    private float _heldFor = -1f, _previouslyHeldFor = -1f, _releasedAfter = -1f;

    // Once per frame, after the gate.
    internal void Update(ControllerButtonGate gate, float now)
    {
        if (gate.Pressed)
            _pressedAt = now;
        _previouslyHeldFor = _heldFor;
        _heldFor = gate.Held ? Elapsed(now) : -1f;
        _releasedAfter = gate.Released ? Elapsed(now) : -1f;
    }

    private float Elapsed(float now) => Math.Max(0f, now - _pressedAt);

    // Released this frame after at least `minimum` seconds. A positive
    // `expireIn` also requires the release by then; zero never expires, which
    // is how the game's input library reads the same argument.
    internal bool Tapped(float minimum, float expireIn) =>
        _releasedAfter >= 0f && _releasedAfter >= minimum && (!(expireIn > 0f) || _releasedAfter <= expireIn);

    // True on the one frame a continuing press reaches `seconds`.
    internal bool HoldReached(float seconds) =>
        _heldFor >= 0f && _heldFor >= seconds && !(_previouslyHeldFor >= 0f && _previouslyHeldFor >= seconds);

    internal void Reset() => _heldFor = _previouslyHeldFor = _releasedAfter = -1f;
}
