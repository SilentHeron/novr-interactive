namespace NOVR.VrUi.Native;

public partial class NativeSettingsPanel
{
    private bool _captureRefreshPending;

    private void OnDisable()
    {
        // A parent-menu handoff can hide this component without SetVisible.
        // Rewired's mapper polls a global event, independent of our enabled state.
        if (_queuedBinding == null && _inputMapper == null && _activeBinding == null) return;
        CancelBindingCapture("Binding capture canceled.");
        _captureRefreshPending = true;
    }

    private void OnEnable()
    {
        if (!_captureRefreshPending) return;
        _captureRefreshPending = false;
        // Keep the selected tab and its scroll/filter state, but remove the old
        // listening prompt when a suspended settings menu becomes visible again.
        if (_wasVisible) RenderCurrentTab();
    }

    private void OnDestroy()
    {
        CancelBindingCapture(null);
        _captureRefreshPending = false;
    }

    private void CancelBindingCapture(string? status)
    {
        _queuedBinding = null;
        _activeBinding = null;
        if (_inputMapper != null)
        {
            var mapper = _inputMapper;
            _inputMapper = null;
            // Clear calls Stop, which can synchronously raise CanceledEvent.
            // Detach first so cleanup cannot redraw or save the hidden menu.
            mapper.RemoveAllEventListeners();
            mapper.Clear();
        }

        if (!string.IsNullOrWhiteSpace(status)) _bindingStatus = status!;
    }

    private void ReleaseInputMapper()
    {
        if (_inputMapper == null) return;

        _inputMapper.RemoveAllEventListeners();
        _inputMapper = null;
        _activeBinding = null;
    }
}
