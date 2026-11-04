#pragma warning disable CS0649
namespace NOVR.MotionControls;

internal static class TrackedControllers
{
    internal static ControllerHardwareProfile Left, Right;
    internal static ControllerHardwareProfile Profile(bool left) => left ? Left : Right;
    internal static bool Available = true;
    internal static readonly ControllerSample[] Samples = new ControllerSample[2];
    internal static ControllerSample Read(bool left) => Samples[left ? 0 : 1];
}
internal struct ControllerSample
{
    internal bool Valid, NativeFrame, Primary, Secondary, Top, Outside, StickClick, Menu, Shoulder;
    internal float Trigger;
}
internal sealed class ModConfiguration
{
    internal static readonly ModConfiguration Instance = new();
    internal readonly MotionControlsConfig MotionControls = new();
    internal ControllerBindings ControllerBindings = null!;
}
internal sealed class MotionControlsConfig
{
    internal readonly ValueSetting<bool> LeftHanded = new();
}
internal sealed class ValueSetting<T>
{
    internal T Value = default!;
}
