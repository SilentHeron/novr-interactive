using NOVR.VrUi.Native;
using Rewired;

internal static class CaptureLifetimeChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool value, string description)
        { if (!value) throw new Exception(description); passed++; }

        var panel = new NativeSettingsPanel();
        panel.Enable();
        Check(panel.Renders == 0, "Initial enable does not render an uninitialized settings tab");
        var mapper = panel.BeginCapture();
        panel.Disable();
        Check(!panel.HasCapture && !mapper.Listening, "Parent hide stops active global input polling and clears capture references");
        Check(mapper.Clears == 1 && panel.CanceledCallbacks == 0,
            "Cancellation removes listeners before Clear can synchronously call back");
        Check(panel.Status == "Binding capture canceled." && panel.Renders == 0,
            "Hidden menu records cancellation without rebuilding hidden controls");
        InputMapper.PollAll();
        Check(mapper.Assignments == 0 && panel.MappedCallbacks == 0,
            "Input after hiding cannot remap or save a setting");
        panel.Enable();
        Check(panel.Renders == 1 && !panel.HasCapture, "Reopen refreshes the current tab once without restarting capture");
        panel.Enable(); panel.Disable(); panel.Enable();
        Check(panel.Renders == 1 && mapper.Clears == 1, "Repeated lifecycle callbacks leave capture stopped and avoid redundant renders");

        panel.QueueCapture(); panel.Disable();
        Check(!panel.HasCapture, "Parent hide also discards the delayed capture before it starts");
        panel.Enable();
        Check(panel.Renders == 2 && !panel.HasCapture, "Reopen cannot resume the delayed capture");
        mapper = panel.BeginCapture();
        InputMapper.PollAll();
        Check(mapper.Assignments == 1 && panel.MappedCallbacks == 1 && !panel.HasCapture,
            "A new explicit capture after reopen still completes normally");
        Check(mapper.Clears == 0, "Normal mapped-event release retains its existing completion semantics");

        var destroyed = new NativeSettingsPanel();
        mapper = destroyed.BeginCapture(); destroyed.DestroyPanel();
        InputMapper.PollAll();
        Check(!destroyed.HasCapture && !mapper.Listening && mapper.Assignments == 0,
            "Destroy stops capture even if disable was not delivered first");
        Check(destroyed.CanceledCallbacks == 0 && destroyed.Renders == 0,
            "Destroy cannot synchronously redraw or receive mapper callbacks");
        destroyed.DestroyPanel(); destroyed.Disable(); destroyed.Enable();
        Check(mapper.Clears == 1 && destroyed.Renders == 0, "Destroy cleanup is idempotent and drops pending refresh");

        var queued = new NativeSettingsPanel(); queued.QueueCapture(); queued.DestroyPanel();
        Check(!queued.HasCapture, "Destroy discards a queued capture as well as active captures");
        queued.Enable(); Check(queued.Renders == 0, "Destroyed queued capture leaves no deferred UI work");

        var hidden = new NativeSettingsPanel(); hidden.QueueCapture(); hidden.Disable(); hidden.Visible = false; hidden.Enable();
        Check(hidden.Renders == 0 && !hidden.HasCapture, "An actually closed settings page does not rebuild on unrelated parent activation");
        hidden.Visible = true; hidden.Enable();
        Check(hidden.Renders == 0, "Closed-page cleanup does not retain a later refresh request");

        var normalClose = new NativeSettingsPanel(); mapper = normalClose.BeginCapture();
        normalClose.CancelNormally(); normalClose.Disable(); normalClose.Enable();
        Check(mapper.Clears == 1 && !normalClose.HasCapture && normalClose.Renders == 0,
            "Existing explicit close cancellation is not repeated by the new lifecycle hooks");
        return passed;
    }
}

namespace NOVR.VrUi.Native
{
    // Only the unrelated settings/renderer boundary is doubled. The cancellation,
    // release and lifecycle methods are compiled directly from production source.
    public partial class NativeSettingsPanel
    {
        private object? _queuedBinding, _activeBinding;
        private InputMapper? _inputMapper;
        private string _bindingStatus = "";
        private bool _wasVisible = true;
        internal int Renders, MappedCallbacks, CanceledCallbacks;
        internal bool HasCapture => _queuedBinding != null || _activeBinding != null || _inputMapper != null;
        internal string Status => _bindingStatus;
        internal bool Visible { set => _wasVisible = value; }
        internal void Enable() => OnEnable();
        internal void Disable() => OnDisable();
        internal void DestroyPanel() => OnDestroy();
        internal void CancelNormally() => CancelBindingCapture("Binding capture canceled.");
        internal void QueueCapture() { _queuedBinding = new object(); _bindingStatus = "Release input, then remap."; }
        internal InputMapper BeginCapture()
        {
            _queuedBinding = null; _activeBinding = new object(); _bindingStatus = "Listening…";
            _inputMapper = new InputMapper();
            _inputMapper.Mapped += () => { MappedCallbacks++; ReleaseInputMapper(); };
            _inputMapper.Canceled += () => { CanceledCallbacks++; ReleaseInputMapper(); RenderCurrentTab(); };
            return _inputMapper;
        }
        private void RenderCurrentTab() => Renders++;
    }
}

namespace Rewired
{
    // Model the relevant installed contract: a mapper polls independently of
    // the panel, and Clear/Stop can raise cancellation synchronously.
    internal sealed class InputMapper
    {
        private static readonly List<InputMapper> Polling = new();
        internal bool Listening = true;
        internal int Clears, Assignments;
        internal Action? Mapped, Canceled;
        internal InputMapper() => Polling.Add(this);
        public void RemoveAllEventListeners() { Mapped = null; Canceled = null; }
        public void Clear()
        {
            Clears++; Listening = false; Polling.Remove(this);
            Canceled?.Invoke(); RemoveAllEventListeners();
        }
        internal static void PollAll()
        {
            foreach (var mapper in Polling.ToArray())
            {
                if (!mapper.Listening) continue;
                mapper.Assignments++; mapper.Listening = false; Polling.Remove(mapper); mapper.Mapped?.Invoke();
            }
        }
    }
}
