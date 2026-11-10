// External input/audio boundaries for the real CockpitMapInteraction callback.
// Hand contacts and touch gates run in production source; no copied hit policy.
using UnityEngine;
public enum CameraMode
{
    cockpit, external
}
public sealed class ControlMapper
{
    public bool isOpen;
}
public enum CursorFlags
{
    None, GameMenu
}
public static class CursorManager
{
    public static CursorFlags Flags; public static CursorFlags GetFlags() => Flags;
}
public sealed class RadialMenuMain
{
    public static bool InUse; public static bool IsInUse() => InUse;
}
namespace NOVR
{
    internal sealed class ModConfiguration
    {
        internal static readonly ModConfiguration Instance = new();
        internal readonly Settings MotionControls = new();
        internal readonly ImageSettings ImageQuality = new();
        internal sealed class Settings
        {
            internal readonly BooleanSetting LeftHanded = new();
        }
        internal sealed class ImageSettings
        {
            internal readonly BooleanSetting SmoothCockpitMap = new() { Value = true };
        }
        internal sealed class BooleanSetting
        {
            internal bool Value;
        }
    }
    internal static class LayerHelper
    {
        internal static int GetVrUiLayer() => 30;
        internal static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (var child in root.Children)
                SetLayerRecursive(child, layer);
        }
    }
}
namespace NOVR.VrUi
{
    internal static class WristMenu
    {
        internal static bool IsOpen;
    }
}
namespace NOVR.MotionControls
{
    internal enum ControllerButton
    {
        StickClick, Primary
    }
    internal static class ControllerButtonNames
    {
        internal static ControllerButton Resolve(ControllerButton button, bool frame) => button;
    }
    internal struct ControllerSample
    {
        internal bool Valid, HasAxis, StickClick, NativeFrame;
        internal Vector2 Axis;
        internal Vector3 Position;
        internal int Device;
    }
    internal static class TrackedControllers
    {
        internal static bool Available = true;
        internal static readonly ControllerSample[] Samples = new ControllerSample[2];
        internal static ControllerSample Read(bool left) => Samples[left ? 0 : 1];
        internal static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
    internal sealed class MotionFlightController
    {
        internal static MotionFlightController? Instance;
        internal bool FlightActive = true, StickHeld, ThrottleHeld;
    }
    internal static class CockpitLayout
    {
        internal static bool Active;
    }
    internal static class NativeHandOptionWheel
    {
        internal static int Owner = -1; internal static bool OwnsHand(bool left) => Owner == (left ? 0 : 1);
    }
    internal static class HelmetNightVision
    {
        internal static int Owner = -1; internal static bool OwnsHand(bool left) => Owner == (left ? 0 : 1);
    }
    internal static class PilotHandVisuals
    {
        internal static Vector3 TouchPoint(bool left, ControllerSample sample, Transform tracking, bool fingertip = false) => tracking.TransformPoint(sample.Position);
    }
    internal enum InteractionCue
    {
        Touch, Click
    }
    internal static class InteractionHaptics
    {
        internal readonly record struct Event(int Device, float Strength, float Seconds, InteractionCue? Cue);
        internal static readonly List<Event> Events = new();
        internal static void Pulse(int device, float strength, float seconds, InteractionCue cue) => Events.Add(new(device, strength, seconds, cue));
        internal static void PulseOnly(int device, float strength, float seconds) => Events.Add(new(device, strength, seconds, null));
    }
}

namespace NOVR.VrUi.SpecialBehavior
{
    // The GPU copy and the camera measurement have their own suite
    // (image-quality). Here they are stand-ins the checks can steer.
    internal sealed partial class TerrainDetail
    {
        internal static float Density = float.NaN;
        internal static readonly List<SpriteCopy> Copies = new();
        internal static readonly List<UnityEngine.Sprite> Destroyed = new();
        private static partial SpriteCopy? MakeCopy(UnityEngine.Sprite source)
        {
            var stand = new UnityEngine.Sprite();
            var copy = new SpriteCopy(source, stand, () => Destroyed.Add(stand));
            Copies.Add(copy);
            return copy;
        }
        private static partial float PixelsPerTexel(UnityEngine.UI.Image image, UnityEngine.Transform widget) => Density;
    }
}
