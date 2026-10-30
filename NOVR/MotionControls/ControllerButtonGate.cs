namespace NOVR.MotionControls;

// Every context (flight, pointer, wrist menu) owns its gate. A held button cannot
// become a fresh action when tracking returns or a menu closes.
internal sealed class ControllerButtonGate
{
    private readonly GripLatch _latch = new();
    public bool Held { get; private set; }
    public bool Pressed { get; private set; }
    public bool Released { get; private set; }

    public void Update(bool available, float value)
    {
        var previous = Held;
        Held = _latch.Update(available, value, true);
        Pressed = Held && !previous;
        // Lost tracking cancels an action; it must not produce a click-on-release.
        Released = available && FlightControlMath.IsFinite(value) && previous && !Held;
    }

    public void Reset()
    {
        _latch.Reset();
        Held = Pressed = Released = false;
    }
}
