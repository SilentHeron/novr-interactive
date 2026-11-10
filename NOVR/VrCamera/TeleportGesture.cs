using System;

namespace NOVR.VrCamera;

internal enum TeleportEvent
{
    None, Aim, Teleport, Return, Cancel
}

// Thumbstick gesture for walking around the aircraft at selection: push
// forward to aim, let go to teleport; pull back and let go to return to where
// the menu is. No Unity input here; the caller owns tracking and menus.
internal sealed class TeleportGesture
{
    internal const float Engage = .7f, Release = .3f;
    private bool _armed;
    private int _state; // 0 idle, 1 aiming, 2 returning

    internal bool Aiming => _state == 1;

    internal TeleportEvent Update(bool available, float x, float y)
    {
        if (!available || float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
        {
            var active = _state != 0;
            Reset();
            return active ? TeleportEvent.Cancel : TeleportEvent.None;
        }
        var magnitude = (float)Math.Sqrt((double)x * x + (double)y * y);
        // A stick already pushed when the screen opens, a menu closes or
        // tracking returns does nothing until it has been centred once.
        if (!_armed)
        {
            _armed = magnitude <= Release;
            return TeleportEvent.None;
        }
        if (_state == 0)
        {
            if (y >= Engage && Math.Abs(x) <= y)
            {
                _state = 1;
                return TeleportEvent.Aim;
            }
            if (y <= -Engage && Math.Abs(x) <= -y)
                _state = 2;
            return TeleportEvent.None;
        }
        if (magnitude <= Release)
        {
            var result = _state == 1 ? TeleportEvent.Teleport : TeleportEvent.Return;
            _state = 0;
            return result;
        }
        // Swinging the stick to the opposite side is a change of mind.
        if (magnitude >= Engage && (_state == 1 ? y < 0 : y > 0))
        {
            _state = 0;
            _armed = false;
            return TeleportEvent.Cancel;
        }
        return _state == 1 ? TeleportEvent.Aim : TeleportEvent.None;
    }

    internal void Reset()
    {
        _armed = false;
        _state = 0;
    }
}

// The aiming arc and the boundary, as plain numbers.
internal static class TeleportArc
{
    internal const int Steps = 36;
    internal const float Speed = 11f, Gravity = 9.81f, StepSeconds = .05f;
    internal const float MinRadius = 15f, MaxRadius = 60f, EyeHeight = 1.7f;

    // Position along a thrown arc from `origin` in direction `aim` (unit length).
    internal static void Point(float ox, float oy, float oz, float ax, float ay, float az, int step,
        out float x, out float y, out float z)
    {
        var t = step * StepSeconds;
        x = ox + ax * Speed * t;
        y = oy + ay * Speed * t - .5f * Gravity * t * t;
        z = oz + az * Speed * t;
    }

    // How far from the aircraft the player may walk: a little over twice the
    // distance the selection camera itself stands off, within fixed limits.
    internal static float BoundaryRadius(float cameraDistance) =>
        float.IsNaN(cameraDistance) || float.IsInfinity(cameraDistance) ? MinRadius
            : Math.Max(MinRadius, Math.Min(MaxRadius, cameraDistance * 2.2f));

    // Pulls a ground point back onto the boundary circle around the aircraft.
    // Returns true when the point had to move.
    internal static bool ClampToBoundary(ref float x, ref float z, float centreX, float centreZ, float radius)
    {
        var dx = x - centreX;
        var dz = z - centreZ;
        var distance = (float)Math.Sqrt((double)dx * dx + (double)dz * dz);
        if (!(distance > radius) || distance <= 0f)
            return false;
        x = centreX + dx / distance * radius;
        z = centreZ + dz / distance * radius;
        return true;
    }
}
