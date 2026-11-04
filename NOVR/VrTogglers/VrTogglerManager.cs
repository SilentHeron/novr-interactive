namespace NOVR.VrTogglers;

public class VrTogglerManager
{
    private readonly VrToggler _toggler;

    public VrTogglerManager()
    {
        _toggler = new XrPluginOpenXrToggler();
        _toggler.SetVrEnabled(true);
    }

    public void ToggleVr()
    {
        _toggler.SetVrEnabled(!_toggler.IsVrEnabled);
    }
}
