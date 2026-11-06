namespace NOVR.VrUi;

internal enum UiPointerOwner
{
    None,
    Mouse,
    Controller
}

// Tracking and hover are availability, not intent. Once the mouse takes over,
// a controller must receive a fresh button press to reclaim the pointer.
internal sealed class UiPointerOwnership
{
    internal UiPointerOwner Owner { get; private set; }

    internal UiPointerOwner Update(bool mouseAvailable, bool mouseInput, bool mouseCapture,
        bool controllerAvailable, bool controllerInput, bool controllerCapture)
    {
        if (Owner == UiPointerOwner.Mouse && mouseAvailable && mouseCapture ||
            Owner == UiPointerOwner.Controller && controllerAvailable && controllerCapture)
            return Owner;
        if (mouseAvailable && mouseInput)
            return Owner = UiPointerOwner.Mouse;
        if (controllerAvailable && controllerInput)
            return Owner = UiPointerOwner.Controller;
        if (Owner == UiPointerOwner.Mouse && mouseAvailable ||
            Owner == UiPointerOwner.Controller && controllerAvailable)
            return Owner;
        return Owner = controllerAvailable ? UiPointerOwner.Controller :
            mouseAvailable ? UiPointerOwner.Mouse : UiPointerOwner.None;
    }

    internal void Reset() => Owner = UiPointerOwner.None;
}
