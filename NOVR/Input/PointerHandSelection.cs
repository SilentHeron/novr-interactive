namespace NOVR.MotionControls;

// Pointer ownership is deliberate: movement, hover, target distance and axis
// noise never choose a hand. A press/drag owns its release frame as well.
internal sealed class PointerHandSelection
{
    private readonly ControllerButtonGate _left = new(), _right = new();
    private readonly ControllerButtonGate _leftGrip = new(), _rightGrip = new();
    private uint _leftButtons, _rightButtons;
    private bool _leftValid, _rightValid;
    internal bool Left
    {
        get; private set;
    } // Right initially; retain the last chosen hand across menus.
    internal ControllerButtonGate Trigger => Left ? _left : _right;
    internal bool InputPressed { get; private set; }

    internal void Update(bool leftAvailable, float leftTrigger, float leftGrip, uint leftButtons,
        bool rightAvailable, float rightTrigger, float rightGrip, uint rightButtons)
    {
        _left.Update(leftAvailable, leftTrigger);
        _right.Update(rightAvailable, rightTrigger);
        _leftGrip.Update(leftAvailable, leftGrip);
        _rightGrip.Update(rightAvailable, rightGrip);
        var leftPressed = ButtonPressed(leftAvailable, leftButtons, ref _leftValid, ref _leftButtons) || _left.Pressed || _leftGrip.Pressed;
        var rightPressed = ButtonPressed(rightAvailable, rightButtons, ref _rightValid, ref _rightButtons) || _right.Pressed || _rightGrip.Pressed;
        InputPressed = leftPressed || rightPressed;
        // Consume both hands' edges even during capture: a press ignored while
        // dragging must not become a delayed takeover when the drag ends.
        if (Trigger.Held || Trigger.Released)
            return;
        if (Left ? rightAvailable && rightPressed : leftAvailable && leftPressed)
            Left = !Left;
    }

    private static bool ButtonPressed(bool available, uint buttons, ref bool valid, ref uint previous)
    {
        var pressed = available && valid && (buttons & ~previous) != 0;
        previous = buttons;
        valid = available;
        return pressed;
    }

    internal void Reset()
    {
        _left.Reset();
        _right.Reset();
        _leftGrip.Reset();
        _rightGrip.Reset();
        _leftValid = _rightValid = false;
        _leftButtons = _rightButtons = 0;
        InputPressed = false;
    }
}
