namespace NOVR.VrUi.Native;

public partial class NativeSettingsPanel
{
    private bool _captureRefreshPending;

    private void OnDisable()
    {
        // A parent-menu handoff can hide this component without SetVisible.
        // Rewired's mapper polls a global event, independent of our enabled state.
        if (!_bindingCapture.Pending)
            return;
        CancelBindingCapture("Binding capture canceled.");
        _captureRefreshPending = true;
    }

    private void OnEnable()
    {
        if (!_captureRefreshPending)
            return;
        _captureRefreshPending = false;
        // Keep the selected tab and its scroll/filter state, but remove the old
        // listening prompt when a suspended settings menu becomes visible again.
        if (_wasVisible)
            RenderCurrentTab();
    }

    private void OnDestroy()
    {
        CancelBindingCapture(null);
        _captureRefreshPending = false;
    }

    private void CancelBindingCapture(string? status)
    {
        _bindingCapture.Cancel();
        if (!string.IsNullOrWhiteSpace(status))
            _bindingStatus = status!;
    }

}
