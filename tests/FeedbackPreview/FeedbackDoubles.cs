// Boundary doubles record device output; they do not model SteamVR, UGUI,
// audio playback/mixing, or perceived cue strength on real controllers.
using NOVR.MotionControls;

namespace UnityEngine
{
    public class MonoBehaviour
    {
        public bool isActiveAndEnabled = true;
    }
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
    }
}
namespace UnityEngine.XR
{
    public struct HapticCapabilities
    {
        public bool supportsImpulse;
    }
    public sealed class DeviceOutput
    {
        public bool Valid = true, Capabilities = true, SupportsImpulse = true;
        public readonly List<(uint channel, float amplitude, float duration)> Pulses = new();
    }
    public readonly struct InputDevice(ulong id, DeviceOutput? output)
    {
        public ulong deviceId => id;
        public bool isValid => output?.Valid == true;
        public bool TryGetHapticCapabilities(out HapticCapabilities capabilities)
        {
            capabilities = new HapticCapabilities { supportsImpulse = output?.SupportsImpulse == true };
            return output?.Capabilities == true;
        }
        public bool SendHapticImpulse(uint channel, float amplitude, float duration)
        {
            output!.Pulses.Add((channel, amplitude, duration));
            return true;
        }
    }
}
namespace NOVR
{
    public sealed class ValueEntry
    {
        public float Value;
    }
    public sealed class FeedbackSettings
    {
        public readonly ValueEntry HapticStrength = new() { Value = 1.5f };
    }
    public sealed class ModConfiguration
    {
        public static readonly ModConfiguration Instance = new();
        public readonly FeedbackSettings MotionControls = new();
    }
}
namespace NOVR.MotionControls
{
    internal struct ControllerSample
    {
        internal bool Valid; internal UnityEngine.XR.InputDevice Device;
    }
    internal static class TrackedControllers
    {
        internal static ControllerSample Left, Right;
        internal static ControllerSample Read(bool left) => left ? Left : Right;
    }
    internal static class InteractionAudio
    {
        internal static readonly List<(ulong device, InteractionCue cue)> Requests = new();
        internal static void Play(UnityEngine.XR.InputDevice device, InteractionCue cue) => Requests.Add((device.deviceId, cue));
    }
}
namespace NOVR.VrUi
{
    internal static class WristMenu
    {
        internal static bool IsOpen = true;
    }
}
