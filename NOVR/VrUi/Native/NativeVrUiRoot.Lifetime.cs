namespace NOVR.VrUi.Native;

// Owns scene discovery, action callbacks and presentation teardown. Panels keep
// their selection during suspension; presentation input must not survive it.
public partial class NativeVrUiRoot
{
    private bool _nativeActionsSubscribed;

    private void SuspendNativePresentation(UtilityWidgetMode utilityMode = UtilityWidgetMode.Hidden)
    {
        if (_root != null)
            _root.SetActive(false);
        _recenterPending = false;
        ClearNativeCursorProjectionReference();
        SetUtilityWidgetMode(utilityMode);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        _mainCanvasDiscovery.Enable();
        if (!_nativeActionsSubscribed)
        {
            _actions.ActionInvoked += OnNativeActionInvoked;
            _nativeActionsSubscribed = true;
        }
    }

    protected override void OnDisable()
    {
        ReleaseNativePresentation();
        base.OnDisable();
    }

    private void ReleaseNativePresentation()
    {
        _mainCanvasDiscovery.Disable();
        if (_nativeActionsSubscribed)
        {
            _actions.ActionInvoked -= OnNativeActionInvoked;
            _nativeActionsSubscribed = false;
        }
        RestoreOriginalMainCanvas();
        _startupSplash?.Dispose();
        NOUIManager.I?.SetStartupPresentation(false);
        SuspendNativePresentation();
    }

    private void OnDestroy()
    {
        ReleaseNativePresentation();
        if (_root != null)
            Destroy(_root);
        if (_recenterWidgetRoot != null)
            Destroy(_recenterWidgetRoot);
    }
}
