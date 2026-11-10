namespace NOVR.VrTogglers;

public class VrTogglerManager
{
    private readonly VrToggler _toggler;

    public bool IsVrEnabled => _toggler.IsVrEnabled;

    // Why the last start attempt failed; null once VR is running.
    public string? LastFailure { get; private set; }

    public VrTogglerManager() : this(new XrPluginOpenXrToggler())
    {
    }

    internal VrTogglerManager(VrToggler toggler)
    {
        _toggler = toggler;
        TryEnable();
    }

    // A headset or runtime that is not ready is an expected state, not a
    // reason to abandon startup: report it and let the caller try again.
    public bool TryEnable()
    {
        try
        {
            _toggler.SetVrEnabled(true);
            LastFailure = _toggler.IsVrEnabled ? null : "The VR runtime did not start.";
        }
        catch (System.Exception error)
        {
            LastFailure = error.Message;
        }
        return _toggler.IsVrEnabled;
    }

    public void ToggleVr()
    {
        _toggler.SetVrEnabled(!_toggler.IsVrEnabled);
    }
}
