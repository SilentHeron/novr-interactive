namespace NOVR.VrTogglers;

public abstract class VrToggler
{
    public bool IsVrEnabled { get; private set; }

    private bool _isSetUp;

    protected abstract bool SetUp();
    protected abstract bool EnableVr();
    protected abstract bool DisableVr();

    public void SetVrEnabled(bool nextVrEnabled)
    {
        if (nextVrEnabled == IsVrEnabled) return;
        if (nextVrEnabled)
        {
            if (!_isSetUp)
            {
                _isSetUp = SetUp();
                if (!_isSetUp) return;
            }
            IsVrEnabled = EnableVr();
        }
        else if (DisableVr())
        {
            IsVrEnabled = false;
        }
    }
}
