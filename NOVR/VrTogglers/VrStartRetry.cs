namespace NOVR.VrTogglers;

// When the headset or its runtime is not ready as the game starts, try again a
// few times on our own, then leave further attempts to the player. Creating an
// OpenXR instance can launch the runtime's own application, so an endless
// automatic retry would keep reopening it after the player closed it.
internal sealed class VrStartRetry
{
    internal static readonly float[] AutomaticDelays = { 5f, 15f, 30f };
    private int _attempts;
    private float _nextAttempt = float.PositiveInfinity;

    internal int AutomaticAttemptsLeft => System.Math.Max(0, AutomaticDelays.Length - _attempts);
    internal bool WaitingToRetry => !float.IsPositiveInfinity(_nextAttempt);

    // Seconds until the next automatic attempt, or -1 when none is scheduled.
    internal float SecondsUntilRetry(float now) => WaitingToRetry ? System.Math.Max(0f, _nextAttempt - now) : -1f;

    internal void Failed(float now)
    {
        _nextAttempt = _attempts < AutomaticDelays.Length ? now + AutomaticDelays[_attempts] : float.PositiveInfinity;
        _attempts++;
    }

    internal void Succeeded()
    {
        _attempts = 0;
        _nextAttempt = float.PositiveInfinity;
    }

    // True once per scheduled attempt; the caller reports the outcome with
    // Failed or Succeeded.
    internal bool Due(float now)
    {
        if (now < _nextAttempt)
            return false;
        _nextAttempt = float.PositiveInfinity;
        return true;
    }
}
