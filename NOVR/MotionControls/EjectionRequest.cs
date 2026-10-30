namespace NOVR.MotionControls;

internal enum EjectionRequestState { Idle, Pending, Accepted, Unconfirmed }

// A panel request must be acknowledged by the aircraft before the UI claims
// ejection has started. A missed request requires another deliberate pull.
internal sealed class EjectionRequest
{
    private float _requestedAt, _noticeUntil;
    private int _requestedFrame;
    internal EjectionRequestState State { get; private set; }
    internal bool BlocksInput => State is EjectionRequestState.Pending or EjectionRequestState.Accepted;

    internal void Begin(float time, int frame)
    {
        State = EjectionRequestState.Pending;
        _requestedAt = time; _requestedFrame = frame;
    }

    internal void Update(bool nativeStarted, float time, int frame)
    {
        if (State == EjectionRequestState.Idle) return;
        if (nativeStarted) { State = EjectionRequestState.Accepted; return; }
        // The action is scheduled for the next game frame. Even a slow frame
        // must get its chance to consume it before a timeout is reported.
        if (State == EjectionRequestState.Pending && frame > _requestedFrame + 1 && time - _requestedAt >= 1f)
        {
            State = EjectionRequestState.Unconfirmed;
            _noticeUntil = time + 2f;
        }
        else if (State == EjectionRequestState.Unconfirmed && time >= _noticeUntil) Reset();
    }

    internal void Reset() => State = EjectionRequestState.Idle;
}
