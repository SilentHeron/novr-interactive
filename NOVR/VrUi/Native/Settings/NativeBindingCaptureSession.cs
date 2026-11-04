using Rewired;

namespace NOVR.VrUi.Native;

// Owns the global Rewired mapper and its delayed start. The settings view owns
// save/error messages; the catalog interprets maps. Hiding the view must
// always cancel this session because InputMapper polls independently of Unity UI.
internal sealed class NativeBindingCaptureSession<TBinding> where TBinding : class
{
    private InputMapper? _mapper;
    private float _startTime;

    internal TBinding? Queued
    {
        get; private set;
    }
    internal TBinding? Active
    {
        get; private set;
    }
    internal bool Pending => Queued != null || Active != null || _mapper != null;

    internal void Queue(TBinding binding, float startTime)
    {
        Cancel();
        Queued = binding;
        _startTime = startTime;
    }

    internal TBinding? TakeReady(float now)
    {
        if (Queued == null || now < _startTime)
            return null;
        var binding = Queued;
        Queued = null;
        return binding;
    }

    internal void Attach(TBinding binding, InputMapper mapper)
    {
        Cancel();
        Active = binding;
        _mapper = mapper;
    }

    internal void Cancel()
    {
        Queued = null;
        var mapper = _mapper;
        Release();
        // Clear calls Stop, which can synchronously raise CanceledEvent.
        // Release detaches first so hidden-menu cleanup cannot redraw or save.
        mapper?.Clear();
    }

    internal void Release()
    {
        _mapper?.RemoveAllEventListeners();
        _mapper = null;
        Active = null;
    }
}
