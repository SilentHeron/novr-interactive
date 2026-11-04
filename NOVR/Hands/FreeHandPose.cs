using UnityEngine;

namespace NOVR.MotionControls;

// Free-hand fingers follow the controller's pointing axis, while the wrist
// remains at its grip-space position. This does not affect held control sockets.
internal static class FreeHandPose
{
    internal static Quaternion Rotation(ControllerSample sample, Vector3 correction)
    {
        var basis = sample.HasAimPose && Valid(sample.AimRotation) ? sample.AimRotation : Fallback(sample);
        return basis * Quaternion.Euler(correction);
    }

    private static Quaternion Fallback(ControllerSample sample)
    {
        // Installed SteamVR controller definitions: openxr_grip -> openxr_aim.
        // Keep unknown hardware's existing grip convention rather than guessing.
        var pitch = sample.Profile == ControllerHardwareProfile.SteamFrame ? 42.8091f
            : sample.Profile == ControllerHardwareProfile.Touch ? 60f : 0f;
        return sample.Rotation * Quaternion.Euler(pitch, 0, 0);
    }

    private static bool Valid(Quaternion rotation)
    {
        var length = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
        return FlightControlMath.IsFinite(length) && length > .5f && length < 1.5f;
    }
}
