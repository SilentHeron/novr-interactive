namespace NOVR.MotionControls;

public enum ControllerButton
{
    None, Trigger, Primary, Secondary, Menu, Shoulder, Top, Outside, StickClick,
    ShoulderOrPrimary, OutsideOrSecondary, PrimaryOrStickClick, PrimaryOrSecondary, SecondaryOrStickClick, SecondaryOrNone
}

// The in-headset help uses the same hand/profile choices as input sampling.
internal static class ControllerButtonNames
{
    internal static ControllerButton Resolve(ControllerButton button, bool frame) => button switch
    {
        ControllerButton.ShoulderOrPrimary => frame ? ControllerButton.Shoulder : ControllerButton.Primary,
        ControllerButton.OutsideOrSecondary => frame ? ControllerButton.Outside : ControllerButton.Secondary,
        ControllerButton.PrimaryOrStickClick => frame ? ControllerButton.Primary : ControllerButton.StickClick,
        ControllerButton.PrimaryOrSecondary => frame ? ControllerButton.Primary : ControllerButton.Secondary,
        ControllerButton.SecondaryOrStickClick => frame ? ControllerButton.Secondary : ControllerButton.StickClick,
        ControllerButton.SecondaryOrNone => frame ? ControllerButton.Secondary : ControllerButton.None,
        _ => button
    };
    internal static string Describe(ControllerButton button, bool left, bool frame)
    {
        button = Resolve(button, frame);
        return button switch
        {
            ControllerButton.Trigger => "Trigger",
            ControllerButton.StickClick => "Thumbstick click",
            ControllerButton.Primary => frame ? (left ? "D-pad down" : "A") : (left ? "X" : "A"),
            ControllerButton.Secondary => frame ? (left ? "D-pad right" : "X") : (left ? "Y" : "B"),
            ControllerButton.Menu => frame ? (left ? "View" : "Menu") : "Menu (if available)",
            ControllerButton.Shoulder => frame ? "Shoulder" : "Shoulder (if available)",
            ControllerButton.Top => frame ? (left ? "D-pad up" : "Y") : "Unavailable",
            ControllerButton.Outside => frame ? (left ? "D-pad left" : "B") : "Unavailable",
            _ => "Unassigned"
        };
    }
}
