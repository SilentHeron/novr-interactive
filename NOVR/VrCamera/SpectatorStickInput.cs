using System;

namespace NOVR.VrCamera;

internal readonly struct SpectatorStickMotion
{
    internal readonly float YawDegrees, PitchDegrees, Distance, PanX, PanForward;
    internal SpectatorStickMotion(float yaw, float pitch, float distance, float panX, float panForward)
    {
        YawDegrees = yaw;
        PitchDegrees = pitch;
        Distance = distance;
        PanX = panX;
        PanForward = panForward;
    }
    internal bool HasMotion => YawDegrees != 0 || PitchDegrees != 0 || Distance != 0 || PanX != 0 || PanForward != 0;
    internal bool HasOrbitMotion => YawDegrees != 0 || PitchDegrees != 0 || Distance != 0;
}

// Each hand must return to center after ownership or tracking changes. This
// prevents a menu scroll or a held flight input from moving the spectator view.
internal sealed class SpectatorStickInput
{
    private bool _leftArmed, _rightArmed;
    internal void Reset() => _leftArmed = _rightArmed = false;

    internal SpectatorStickMotion Update(bool available, bool leftValid, bool rightValid,
        float leftX, float leftY, float rightX, float rightY, float deltaSeconds)
    {
        if (!available || !Finite(deltaSeconds) || deltaSeconds <= 0)
        {
            Reset();
            return default;
        }
        Filter(leftValid, leftX, leftY, ref _leftArmed, out leftX, out leftY);
        Filter(rightValid, rightX, rightY, ref _rightArmed, out rightX, out rightY);
        var dt = Math.Min(deltaSeconds, .05f);
        return new SpectatorStickMotion(rightX * 75f * dt, -rightY * 60f * dt,
            -leftY * 1.5f * dt, leftX * dt, leftY * dt);
    }

    private static void Filter(bool valid, float x, float y, ref bool armed, out float filteredX, out float filteredY)
    {
        filteredX = filteredY = 0;
        if (!valid || !Finite(x) || !Finite(y))
        {
            armed = false;
            return;
        }
        // Clamp before taking the norm: even finite but malformed XR values
        // must not overflow or produce unbounded motion.
        x = Math.Max(-1, Math.Min(1, x));
        y = Math.Max(-1, Math.Min(1, y));
        var magnitude = (float)Math.Sqrt(x * x + y * y);
        if (!armed)
        {
            armed = magnitude <= .15f;
            return;
        }
        if (magnitude <= .2f)
            return;
        var amount = (Math.Min(magnitude, 1f) - .2f) / .8f / magnitude;
        filteredX = x * amount;
        filteredY = y * amount;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
