#pragma warning disable CS0649
namespace NOVR.MotionControls;

internal static class TrackedControllers
{
    internal static ControllerHardwareProfile Left, Right;
    internal static ControllerHardwareProfile Profile(bool left) => left ? Left : Right;
}
internal struct ControllerSample
{
    internal bool NativeFrame, Primary, Secondary, Top, Outside, StickClick, Menu, Shoulder;
    internal float Trigger;
}
internal sealed class ControllerActions
{
    internal static ControllerActions? Instance;
    internal void ResetButtons() { }
}
internal sealed class ModConfiguration
{
    internal static readonly ModConfiguration Instance = new();
    internal readonly MotionControlsConfig MotionControls = new();
}
internal sealed class MotionControlsConfig { internal readonly ValueSetting<bool> LeftHanded = new(); }
internal sealed class ValueSetting<T> { internal T Value = default!; }
