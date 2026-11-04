using System.Collections.Generic;

namespace NOVR.MotionControls;

// A sample needs a fresh controller click. Cancelled presses and disconnected
// devices cannot leave a request for a later mouse click or another controller.
internal sealed class FeedbackPreviewGate<T> where T : struct
{
    private bool _armed;
    private T _device;

    internal void Arm(T device)
    {
        _device = device;
        _armed = true;
    }
    internal void Cancel() => _armed = false;
    internal bool Consume(bool available, T device)
    {
        var play = _armed && available && EqualityComparer<T>.Default.Equals(_device, device);
        Cancel();
        return play;
    }
}
