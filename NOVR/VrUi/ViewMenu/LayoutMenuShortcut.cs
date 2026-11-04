using NOVR.MotionControls;

namespace NOVR.VrUi;

// The option wheel cannot own input during placement. Either hand can reopen
// layout controls without relying on a Menu button intercepted by Steam Link.
internal sealed class LayoutMenuShortcut
{
    private readonly ControllerButtonGate _left = new(), _right = new();

    internal bool Update(bool editing, bool cockpitAvailable, bool menuOpen,
        bool leftValid, bool leftClick, bool rightValid, bool rightClick)
    {
        var available = editing && cockpitAvailable && !menuOpen;
        _left.Update(available && leftValid, leftClick ? 1 : 0);
        _right.Update(available && rightValid, rightClick ? 1 : 0);
        return _left.Pressed || _right.Pressed;
    }

    internal void Reset()
    {
        _left.Reset();
        _right.Reset();
    }
}
